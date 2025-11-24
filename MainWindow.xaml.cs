using System;
using System.IO.Ports;
using System.Windows;
using System.Windows.Threading; // Required for Timers

namespace stewart_platform
{
    public partial class MainWindow : Window
    {
        // The Physics Engine
        StewartPlatform platform = new StewartPlatform();

        // Serial & Timers
        SerialPort? arduinoPort;
        DispatcherTimer sendTimer;          // Timer for sending data to Arduino
        DispatcherTimer resetAnimationTimer; // Timer for the Reset Animation

        public MainWindow()
        {
            InitializeComponent();
            LoadAvailablePorts();

            // 1. Setup Data Timer (Throttles Serial Data)
            sendTimer = new DispatcherTimer();
            sendTimer.Interval = TimeSpan.FromMilliseconds(40); // 25 times per second
            sendTimer.Tick += SendDataToArduino;

            // 2. Setup Animation Timer (Handles the Slow Reset)
            resetAnimationTimer = new DispatcherTimer();
            resetAnimationTimer.Interval = TimeSpan.FromMilliseconds(20); // 50 times per second (Smooth)
            resetAnimationTimer.Tick += AnimateResetStep;
        }

        private void LoadAvailablePorts()
        {
            string[] ports = SerialPort.GetPortNames();
            PortSelector.ItemsSource = ports;
            if (ports.Length > 0) PortSelector.SelectedIndex = 0;
        }

        // --- CONNECTION HANDLER ---
        private void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            if (arduinoPort != null && arduinoPort.IsOpen)
            {
                // Disconnect
                try { arduinoPort.Close(); } catch { }
                sendTimer.Stop();
                BtnConnect.Content = "Connect";
                TxtStatus.Text = "Disconnected";
                TxtStatus.Foreground = System.Windows.Media.Brushes.Red;
            }
            else
            {
                // Connect
                if (PortSelector.SelectedItem == null) return;
                try
                {
                    arduinoPort = new SerialPort(PortSelector.SelectedItem.ToString(), 115200);
                    arduinoPort.Open();
                    sendTimer.Start();
                    BtnConnect.Content = "Disconnect";
                    TxtStatus.Text = "Connected";
                    TxtStatus.Foreground = System.Windows.Media.Brushes.Green;
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error: " + ex.Message);
                }
            }
        }

        // --- SLIDER CONTROLS ---
        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (platform == null) return;

            // Calculate Physics
            double rx = SldRotX.Value * (Math.PI / 180.0);
            double ry = SldRotY.Value * (Math.PI / 180.0);
            double rz = SldRotZ.Value * (Math.PI / 180.0);

            platform.ApplyTranslationAndRotation(
                SldPosX.Value, SldPosY.Value, SldPosZ.Value,
                rx, ry, rz
            );

            UpdateUI();
        }

        // --- NEW: SMOOTH RESET LOGIC ---
        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            // Instead of setting to 0 instantly, we start the animation timer.
            resetAnimationTimer.Start();
        }

        private void AnimateResetStep(object? sender, EventArgs e)
        {
            bool allZero = true;
            double step = 1.0; // Speed of reset (Higher = Faster, Lower = Slower)

            // Function to move a slider towards 0
            bool MoveSliderTowardsZero(System.Windows.Controls.Slider sld)
            {
                if (Math.Abs(sld.Value) > 0.1) // If not yet zero
                {
                    if (sld.Value > 0) sld.Value -= step;
                    else sld.Value += step;

                    // Prevent overshooting
                    if (Math.Abs(sld.Value) < step) sld.Value = 0;

                    return false; // Still moving
                }
                else
                {
                    sld.Value = 0; // Snap to exactly 0
                    return true; // Finished
                }
            }

            // Apply to all sliders
            bool xDone = MoveSliderTowardsZero(SldPosX);
            bool yDone = MoveSliderTowardsZero(SldPosY);
            bool zDone = MoveSliderTowardsZero(SldPosZ);
            bool rxDone = MoveSliderTowardsZero(SldRotX);
            bool ryDone = MoveSliderTowardsZero(SldRotY);
            bool rzDone = MoveSliderTowardsZero(SldRotZ);

            // If all sliders are at 0, stop the animation
            if (xDone && yDone && zDone && rxDone && ryDone && rzDone)
            {
                resetAnimationTimer.Stop();
            }
        }

        private void UpdateUI()
        {
            TxtServo0.Text = $"Servo 0: {platform.GetAlphaDegree(0):F2}°";
            TxtServo1.Text = $"Servo 1: {platform.GetAlphaDegree(1):F2}°";
            TxtServo2.Text = $"Servo 2: {platform.GetAlphaDegree(2):F2}°";
            TxtServo3.Text = $"Servo 3: {platform.GetAlphaDegree(3):F2}°";
            TxtServo4.Text = $"Servo 4: {platform.GetAlphaDegree(4):F2}°";
            TxtServo5.Text = $"Servo 5: {platform.GetAlphaDegree(5):F2}°";
        }

        // --- SERIAL COMMUNICATION ---
        private void SendDataToArduino(object? sender, EventArgs e)
        {
            if (arduinoPort == null || !arduinoPort.IsOpen) return;

            // Safety Check for NaN errors
            for (int i = 0; i < 6; i++)
            {
                if (double.IsNaN(platform.Alpha[i])) return;
            }

            try
            {
                byte[] header = { 0x6A, 0x6A };
                arduinoPort.Write(header, 0, 2);

                string data = "";
                for (int i = 0; i < 6; i++)
                {
                    int val = (int)(platform.GetAlphaDegree(i) * 100);
                    data += val.ToString();
                    if (i < 5) data += ",";
                }
                data += "\n";

                arduinoPort.Write(data);
            }
            catch (Exception ex)
            {
                sendTimer.Stop();
                MessageBox.Show("Serial Error: " + ex.Message);
            }
        }
    }
}