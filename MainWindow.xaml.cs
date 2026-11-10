using System;
using System.Collections.Generic;
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

            platform.ApplyTranslationAndRotation(0, 0, 0, 0, 0, 0);
            Update3DVisualization();
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
            connection.Disconnect();
            BtnConnect.Content = "Connect Platform";
            TxtStatus.Text = "Disconnected";
            TxtStatus.Foreground = Brushes.Red;
        }

        private async void SendDataToPlatform(object? sender, EventArgs e)
        {
            if (!connection.IsConnected) return;
            for (int i = 0; i < 6; i++) if (double.IsNaN(platform.Alpha[i])) return;

            try
            {
                string dataStr = ServoCommandFormatter.Format(platform);
                await connection.SendAsync(dataStr);
            }
            catch (Exception)
            {
                DisconnectFromPlatform();
            }
        }

        private void ProcessFeedbackLine(string? line)
        {
            if (!SensorFeedbackParser.TryGetFields(line, out string[] parts)) return;

            Dispatcher.Invoke(() =>
            {
                if (double.TryParse(parts[0], out double roll)) TxtSensorRoll.Text = $"{roll:F1}°";
                if (double.TryParse(parts[1], out double pitch)) TxtSensorPitch.Text = $"{pitch:F1}°";
                if (double.TryParse(parts[2], out double yaw)) TxtSensorYaw.Text = $"{yaw:F1}°";
                if (double.TryParse(parts[3], out double temp)) TxtSensorTemp.Text = $"{temp:F1}°C";
            });
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

            double rx = SldRotX.Value * (Math.PI / 180.0);
            double ry = SldRotY.Value * (Math.PI / 180.0);
            double rz = SldRotZ.Value * (Math.PI / 180.0);
            platform.ApplyTranslationAndRotation(SldPosX.Value, SldPosY.Value, SldPosZ.Value, rx, ry, rz);
            UpdateUI();
            Update3DVisualization();
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
                movementTimer.Stop();
        }

        private bool MoveAxisTowards(Slider sld, int targetIndex, double step)
        {
            double target = targetValues[targetIndex];
            sld.Value = movement.MoveTowards(sld.Value, target, step);
            return sld.Value == target;
        }

        private void BtnSetPos_Click(object sender, RoutedEventArgs e)
        {
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
            for (int i = 0; i < 6; i++) targetValues[i] = 0;
            movementTimer.Start();
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
