using System;
using System.Collections.Generic;
using System.Globalization;
using stewart_platform.Control;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using HelixToolkit.Wpf;
using stewart_platform.Services;
using stewart_platform.Visualization;

namespace stewart_platform
{
    public partial class MainWindow : Window
    {
        // Existing platform configuration and kinematics are not modified.
        private readonly RobotConfig config = new RobotConfig();
        private readonly StewartPlatform platform;

        private readonly PlatformConnectionService connection = new PlatformConnectionService();
        private readonly PlatformMovementService movement = new PlatformMovementService();
        private readonly StewartVisualizer visualizer;
        private readonly DispatcherTimer sendTimer;
        private readonly DispatcherTimer movementTimer;
        private readonly double[] targetValues = new double[6]; // X, Y, Z, Rx, Ry, Rz
        private bool isInternalUpdate;
        private bool isClosedLoop;
        private bool hasFeedback;
        private DateTime lastFeedbackUtc = DateTime.MinValue;
        private double feedbackRoll, feedbackPitch;
        private DateTime lastControllerUpdateUtc = DateTime.MinValue;
        private readonly DispatcherTimer controlTimer;
        private readonly PlatformLevelController levelController = new PlatformLevelController();
        private const double MaximumSafeRotationDegrees = 8.0;
        private bool isSending;
        private bool transmissionInhibited;

        // Closed-loop is a one-time leveling procedure, not a permanent servo governor.
        private enum LevelingPhase { OpenLoop, ReturningToZero, Leveling, ManualReady }
        private LevelingPhase levelingPhase = LevelingPhase.OpenLoop;
        private double heldRollTrim;
        private double heldPitchTrim;


        public MainWindow()
        {
            InitializeComponent();

            platform = new StewartPlatform(config);
            SetupSliders();

            visualizer = new StewartVisualizer(
                VisBase, VisPlatform,
                new List<TubeVisual3D> { VisHorn0, VisHorn1, VisHorn2, VisHorn3, VisHorn4, VisHorn5 },
                new List<TubeVisual3D> { VisRod0, VisRod1, VisRod2, VisRod3, VisRod4, VisRod5 });

            connection.FeedbackLineReceived += ProcessFeedbackLine;
            LoadAvailablePorts();

            sendTimer = new DispatcherTimer();
            sendTimer.Interval = TimeSpan.FromMilliseconds(40);
            sendTimer.Tick += SendDataToPlatform;

            movementTimer = new DispatcherTimer();
            movementTimer.Interval = TimeSpan.FromMilliseconds(20);
            movementTimer.Tick += MovementTimer_Tick;

            controlTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
            controlTimer.Tick += ControlTimer_Tick;
            controlTimer.Start();

            platform.ApplyTranslationAndRotation(0, 0, 0, 0, 0, 0);
            Update3DVisualization();
            UpdateModeUI();
        }

        private void SetupSliders()
        {
            SldPosX.Minimum = -config.MaxTranslation; SldPosX.Maximum = config.MaxTranslation;
            SldPosY.Minimum = -config.MaxTranslation; SldPosY.Maximum = config.MaxTranslation;
            SldPosZ.Minimum = -config.MaxTranslation; SldPosZ.Maximum = config.MaxTranslation;

            SldRotX.Minimum = -config.MaxRotation; SldRotX.Maximum = config.MaxRotation;
            SldRotY.Minimum = -config.MaxRotation; SldRotY.Maximum = config.MaxRotation;
            SldRotZ.Minimum = -config.MaxRotation; SldRotZ.Maximum = config.MaxRotation;
        }

        private void LoadAvailablePorts()
        {
            string[] ports = PlatformConnectionService.GetAvailablePorts();
            ComboUsbPort.ItemsSource = ports;
            ComboDonglePort.ItemsSource = ports;
            if (ports.Length > 0)
            {
                ComboUsbPort.SelectedIndex = 0;
                ComboDonglePort.SelectedIndex = 0;
            }
        }

        private void BtnRefreshPorts_Click(object sender, RoutedEventArgs e) => LoadAvailablePorts();

        private void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            if (connection.IsConnected)
                DisconnectFromPlatform();
            else
            {
                LoadAvailablePorts();
                ModalOverlay.Visibility = Visibility.Visible;
            }
        }

        private void BtnModalCancel_Click(object sender, RoutedEventArgs e)
        {
            ModalOverlay.Visibility = Visibility.Collapsed;
        }

        private async void BtnModalConnect_Click(object sender, RoutedEventArgs e)
        {
            ModalOverlay.Visibility = Visibility.Collapsed;
            TxtStatus.Text = "Connecting...";
            TxtStatus.Foreground = Brushes.Orange;

            bool isWifiMode = RadioWifi.IsChecked == true;
            try
            {
                if (isWifiMode)
                {
                    string url = TxtWifiUrl.Text.Trim();
                    await connection.ConnectWifiAsync(url);
                }
                else
                {
                    ComboBox activeCombo = RadioSerial.IsChecked == true ? ComboUsbPort : ComboDonglePort;
                    if (activeCombo.SelectedItem == null) throw new Exception("No COM port selected");
                    string portName = activeCombo.SelectedItem.ToString() ?? "";
                    connection.ConnectSerial(portName, config.BaudRate);
                }

                hasFeedback = false;
                transmissionInhibited = false;
                levelController.Reset();
                levelingPhase = LevelingPhase.OpenLoop;
                heldRollTrim = heldPitchTrim = 0;
                UpdateModeUI();
                sendTimer.Start();
                BtnConnect.Content = "Disconnect";

                if (RadioWifi.IsChecked == true) TxtStatus.Text = "Connected (Wi-Fi)";
                else if (RadioDongle.IsChecked == true) TxtStatus.Text = "Connected (Dongle)";
                else TxtStatus.Text = "Connected (USB)";

                TxtStatus.Foreground = Brushes.Green;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Connection Failed: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                DisconnectFromPlatform();
            }
        }

        private void DisconnectFromPlatform()
        {
            sendTimer.Stop();
            isClosedLoop = false;
            levelingPhase = LevelingPhase.OpenLoop;
            heldRollTrim = heldPitchTrim = 0;
            movementTimer.Stop();
            levelController.Reset();
            UpdateModeUI();
            connection.Disconnect();
            BtnConnect.Content = "Connect Platform";
            TxtStatus.Text = "Disconnected";
            TxtStatus.Foreground = Brushes.Red;
        }

        private async void SendDataToPlatform(object? sender, EventArgs e)
        {
            if (!connection.IsConnected || isSending || transmissionInhibited) return;
            // Fail safe: do not keep issuing fresh commands without fresh feedback.
            // Closed loop is disabled (and sending stops) when feedback is lost.
            if (isClosedLoop && !FeedbackIsFresh())
            {
                InhibitCommands("Sensor feedback lost — commands halted. Reconnect to resume.");
                return;
            }
            for (int i = 0; i < 6; i++)
            {
                double angle = platform.GetAlphaDegree(i);
                if (!double.IsFinite(angle) || Math.Abs(angle) > 90.0)
                {
                    if (isClosedLoop) InhibitCommands("Invalid servo angle — commands halted. Reconnect to resume.");
                    return;
                }
            }
            isSending = true;
            try
            {
                string dataStr = ServoCommandFormatter.Format(platform);
                await connection.SendAsync(dataStr);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Send failed: " + ex);
                DisconnectFromPlatform();
            }
            finally { isSending = false; }
        }

        private void ProcessFeedbackLine(string? line)
        {
            if (!SensorFeedbackParser.TryGetFields(line, out string[] parts)) return;
            // Parses ESP32's FB:roll,pitch,yaw,temp regardless of Windows locale.
            if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double roll) ||
                !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double pitch) ||
                !double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double yaw) ||
                !double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out double temp) ||
                !double.IsFinite(roll) || !double.IsFinite(pitch) ||
                !double.IsFinite(yaw) || !double.IsFinite(temp)) return;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                feedbackRoll = roll;
                feedbackPitch = pitch;
                hasFeedback = true;
                lastFeedbackUtc = DateTime.UtcNow;
                TxtSensorRoll.Text = $"{Math.Truncate(roll):F0}°";
                TxtSensorPitch.Text = $"{Math.Truncate(pitch):F0}°";
                TxtSensorYaw.Text = $"{Math.Truncate(yaw):F0}°";
                TxtSensorTemp.Text = $"{temp:F1}°C";
            }));
        }

        private bool FeedbackIsFresh() => hasFeedback &&
            (DateTime.UtcNow - lastFeedbackUtc).TotalMilliseconds < 350;

        private void BtnModeToggle_Click(object sender, RoutedEventArgs e)
        {
            if (isClosedLoop)
            {
                DisableClosedLoop("Open loop: manual pose control.");
                return;
            }
            if (!CanStartLeveling()) return;
            isClosedLoop = true;
            heldRollTrim = heldPitchTrim = 0;
            BeginReturnToZero();
        }

        private bool CanStartLeveling()
        {
            if (transmissionInhibited)
            {
                MessageBox.Show("Commands inhibited. Disconnect, inspect hardware, then reconnect.");
                return false;
            }
            if (!connection.IsConnected)
            {
                MessageBox.Show("Connect the ESP32 first.");
                return false;
            }
            if (!FeedbackIsFresh() || Math.Abs(feedbackRoll) > 45 || Math.Abs(feedbackPitch) > 45)
            {
                MessageBox.Show("Recent, valid MPU6050 roll/pitch feedback is required.");
                return false;
            }
            return true;
        }

        private void BeginReturnToZero()
        {
            // Keep the previous leveling trim during the trip back to the user's zero.
            // This avoids an abrupt jump when Reset is clicked after manual operation.
            movementTimer.Stop();
            levelingPhase = LevelingPhase.ReturningToZero;
            for (int i = 0; i < 6; i++) targetValues[i] = 0;
            UpdateModeUI();
            movementTimer.Start();
        }

        private void BeginLeveling()
        {
            if (!isClosedLoop) return;
            levelingPhase = LevelingPhase.Leveling;
            // Reuse the last successful baseline to avoid jumping to an untrimmed pose.
            levelController.ResetWithTrim((int)Math.Round(heldRollTrim),
                                          (int)Math.Round(heldPitchTrim));
            lastControllerUpdateUtc = DateTime.UtcNow;
            ApplyRequestedPose();
            UpdateModeUI();
        }

        private void CompleteLeveling(double finalRoll, double finalPitch)
        {
            heldRollTrim = finalRoll;
            heldPitchTrim = finalPitch;
            levelingPhase = LevelingPhase.ManualReady;
            // Continue applying the held leveling offset to manual roll/pitch commands.
            // Sensor feedback is now display-only until Reset is pressed.
            ApplyRequestedPose();
            UpdateModeUI();
            TxtLoopStatus.Text = "STABLE — leveling complete. Manual control unlocked.";
        }

        private void DisableClosedLoop(string message)
        {
            movementTimer.Stop();
            isClosedLoop = false;
            levelingPhase = LevelingPhase.OpenLoop;
            levelController.Reset();
            lastControllerUpdateUtc = DateTime.MinValue;
            heldRollTrim = heldPitchTrim = 0;
            ApplyRequestedPose();
            UpdateModeUI();
            TxtLoopStatus.Text = message;
        }

        private void InhibitCommands(string reason)
        {
            movementTimer.Stop();
            isClosedLoop = false;
            levelingPhase = LevelingPhase.OpenLoop;
            transmissionInhibited = true;
            levelController.Reset();
            lastControllerUpdateUtc = DateTime.MinValue;
            // Do not recalculate or send a new pose after a fault.
            TxtLoopStatus.Text = reason;
            UpdateModeUI(false);
        }

        private void UpdateModeUI(bool updateLabel = true)
        {
            BtnModeToggle.Content = isClosedLoop ? "Switch to Open Loop" : "Switch to Closed Loop";
            TxtModeStatus.Text = levelingPhase switch
            {
                LevelingPhase.ReturningToZero => "Closed Loop: returning to zero",
                LevelingPhase.Leveling => "Closed Loop: one-time leveling",
                LevelingPhase.ManualReady => "Closed Loop: leveled; manual control",
                _ => "Open Loop: manual pose"
            };
            bool locked = levelingPhase == LevelingPhase.ReturningToZero ||
                          levelingPhase == LevelingPhase.Leveling;
            PositionGroup.IsEnabled = !locked;
            RotationGroup.IsEnabled = !locked;
            StabilizingNotice.Visibility = locked ? Visibility.Visible : Visibility.Collapsed;
            TxtStabilizingNotice.Text = levelingPhase == LevelingPhase.ReturningToZero
                ? "Returning to zero. Please wait until leveling completes…"
                : "Stabilizing platform. Please wait until stable…";
            BtnReset.IsEnabled = !locked;
            if (updateLabel)
            {
                TxtLoopStatus.Text = levelingPhase switch
                {
                    LevelingPhase.ReturningToZero => "Returning all axes to zero…",
                    LevelingPhase.Leveling => "CORRECTING (roll/pitch)…",
                    LevelingPhase.ManualReady => "STABLE — manual controls available",
                    _ => "Open loop"
                };
            }
        }

        private void ControlTimer_Tick(object? sender, EventArgs e)
        {
            // ManualReady deliberately does NOT run the PI / discrete feedback controller.
            if (levelingPhase != LevelingPhase.Leveling) return;
            if (!connection.IsConnected || !FeedbackIsFresh())
            {
                InhibitCommands("Feedback timeout — commands halted; disconnect and inspect hardware.");
                return;
            }

            DateTime now = DateTime.UtcNow;
            double dt = lastControllerUpdateUtc == DateTime.MinValue ? 0.08
                      : (now - lastControllerUpdateUtc).TotalSeconds;
            lastControllerUpdateUtc = now;

            // Level only the home position (zero target); subsequent user movements
            // are open-loop relative to the leveled baseline.
            var (rollCommand, pitchCommand) = levelController.Update(
                0, 0, feedbackRoll, feedbackPitch, dt, now);
            rollCommand = Math.Clamp(rollCommand, -MaximumSafeRotationDegrees, MaximumSafeRotationDegrees);
            pitchCommand = Math.Clamp(pitchCommand, -MaximumSafeRotationDegrees, MaximumSafeRotationDegrees);
            heldRollTrim = rollCommand;
            heldPitchTrim = pitchCommand;
            ApplyRequestedPose();

            TxtErrRoll.Text = $"Roll error: {levelController.RollError:+0.0;-0.0;0.0}°";
            TxtErrPitch.Text = $"Pitch error: {levelController.PitchError:+0.0;-0.0;0.0}°";
            TxtLoopStatus.Text = levelController.IsCorrectionLimited
                ? "Correction limit reached — inspect orientation or hardware."
                : "CORRECTING (roll/pitch)…";

            if (levelController.IsStable)
                CompleteLeveling(rollCommand, pitchCommand);
        }

        private void ApplyPose(double x, double y, double z,
                               double rollDeg, double pitchDeg, double yawDeg)
        {
            double degToRad = Math.PI / 180.0;
            platform.ApplyTranslationAndRotation(x, y, z,
                rollDeg * degToRad, pitchDeg * degToRad, yawDeg * degToRad);
            UpdateUI();
            Update3DVisualization();
        }

        private void ApplyRequestedPose()
        {
            double rollOffset = isClosedLoop ? heldRollTrim : 0;
            double pitchOffset = isClosedLoop ? heldPitchTrim : 0;
            ApplyPose(SldPosX.Value, SldPosY.Value, SldPosZ.Value,
                SldRotX.Value + rollOffset, SldRotY.Value + pitchOffset,
                SldRotZ.Value);
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (platform == null) return;

            if (InpPosX != null) InpPosX.Text = SldPosX.Value.ToString("F1");
            if (InpPosY != null) InpPosY.Text = SldPosY.Value.ToString("F1");
            if (InpPosZ != null) InpPosZ.Text = SldPosZ.Value.ToString("F1");

            if (InpRotX != null) InpRotX.Text = SldRotX.Value.ToString("F1");
            if (InpRotY != null) InpRotY.Text = SldRotY.Value.ToString("F1");
            if (InpRotZ != null) InpRotZ.Text = SldRotZ.Value.ToString("F1");

            if (!isInternalUpdate)
            {
                targetValues[0] = SldPosX.Value;
                targetValues[1] = SldPosY.Value;
                targetValues[2] = SldPosZ.Value;
                targetValues[3] = SldRotX.Value;
                targetValues[4] = SldRotY.Value;
                targetValues[5] = SldRotZ.Value;
            }

            // Closed-loop control timer owns the corrected pose while active.
            if (levelingPhase == LevelingPhase.OpenLoop ||
                levelingPhase == LevelingPhase.ReturningToZero ||
                levelingPhase == LevelingPhase.ManualReady)
                ApplyRequestedPose();
        }

        private void MovementTimer_Tick(object? sender, EventArgs e)
        {
            isInternalUpdate = true;
            bool doneX = MoveAxisTowards(SldPosX, 0, 0.5);
            bool doneY = MoveAxisTowards(SldPosY, 1, 0.5);
            bool doneZ = MoveAxisTowards(SldPosZ, 2, 0.5);
            bool doneRx = MoveAxisTowards(SldRotX, 3, 0.1);
            bool doneRy = MoveAxisTowards(SldRotY, 4, 0.1);
            bool doneRz = MoveAxisTowards(SldRotZ, 5, 0.1);
            isInternalUpdate = false;

            if (doneX && doneY && doneZ && doneRx && doneRy && doneRz)
            {
                movementTimer.Stop();
                if (levelingPhase == LevelingPhase.ReturningToZero)
                    BeginLeveling();
            }
        }

        private bool MoveAxisTowards(Slider sld, int targetIndex, double step)
        {
            double target = targetValues[targetIndex];
            sld.Value = movement.MoveTowards(sld.Value, target, step);
            return sld.Value == target;
        }

        private void BtnSetPos_Click(object sender, RoutedEventArgs e)
        {
            if (levelingPhase == LevelingPhase.Leveling || levelingPhase == LevelingPhase.ReturningToZero) return;
            string axis = ((Button)sender).Tag?.ToString() ?? "";
            try
            {
                if (axis == "X") targetValues[0] = double.Parse(InpPosX.Text);
                if (axis == "Y") targetValues[1] = double.Parse(InpPosY.Text);
                if (axis == "Z") targetValues[2] = double.Parse(InpPosZ.Text);
                movementTimer.Start();
            }
            catch { MessageBox.Show("Invalid Number"); }
        }

        private void BtnSetRot_Click(object sender, RoutedEventArgs e)
        {
            if (levelingPhase == LevelingPhase.Leveling || levelingPhase == LevelingPhase.ReturningToZero) return;
            string axis = ((Button)sender).Tag?.ToString() ?? "";
            try
            {
                if (axis == "X") targetValues[3] = double.Parse(InpRotX.Text);
                if (axis == "Y") targetValues[4] = double.Parse(InpRotY.Text);
                if (axis == "Z") targetValues[5] = double.Parse(InpRotZ.Text);
                movementTimer.Start();
            }
            catch { MessageBox.Show("Invalid Number"); }
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            if (levelingPhase == LevelingPhase.Leveling ||
                levelingPhase == LevelingPhase.ReturningToZero) return;
            if (isClosedLoop)
            {
                if (!CanStartLeveling()) return;
                BeginReturnToZero();
            }
            else
            {
                for (int i = 0; i < 6; i++) targetValues[i] = 0;
                movementTimer.Start();
            }
        }

        private void Update3DVisualization() => visualizer.Update(platform);

        private void UpdateUI()
        {
            TxtServo0.Text = $"Servo 0: {platform.GetAlphaDegree(0):F2}°";
            TxtServo1.Text = $"Servo 1: {platform.GetAlphaDegree(1):F2}°";
            TxtServo2.Text = $"Servo 2: {platform.GetAlphaDegree(2):F2}°";
            TxtServo3.Text = $"Servo 3: {platform.GetAlphaDegree(3):F2}°";
            TxtServo4.Text = $"Servo 4: {platform.GetAlphaDegree(4):F2}°";
            TxtServo5.Text = $"Servo 5: {platform.GetAlphaDegree(5):F2}°";
        }
    }
}
