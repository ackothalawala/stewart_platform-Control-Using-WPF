using System;
using System.IO.Ports;
using System.Windows;
using System.Windows.Threading;

namespace stewart_platform
{
    public partial class MainWindow : Window
    {
        // The Physics Engine
        StewartPlatform platform = new StewartPlatform();

        // The Serial Connection
        SerialPort? arduinoPort;
        DispatcherTimer sendTimer;

        public MainWindow()
        {
            InitializeComponent();
            LoadAvailablePorts();

            // Setup timer to throttle serial data
            sendTimer = new DispatcherTimer();
            sendTimer.Interval = TimeSpan.FromMilliseconds(40);
            sendTimer.Tick += SendDataToArduino;
        }

        private void LoadAvailablePorts()
        {
            string[] ports = SerialPort.GetPortNames();
            PortSelector.ItemsSource = ports;
            if (ports.Length > 0) PortSelector.SelectedIndex = 0;
        }

        private void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            // Check if port exists and is open
            if (arduinoPort != null && arduinoPort.IsOpen)
            {
                // DISCONNECT
                try
                {
                    arduinoPort.Close();
                }
                catch { /* Ignore close errors */ }

                sendTimer.Stop();
                BtnConnect.Content = "Connect";
                TxtStatus.Text = "Disconnected";
                TxtStatus.Foreground = System.Windows.Media.Brushes.Red;
            }
            else
            {
                // CONNECT
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

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (platform == null) return; // Safety check

            // Convert to Radians because Math engine needs Radians
            double rx = SldRotX.Value * (Math.PI / 180.0);
            double ry = SldRotY.Value * (Math.PI / 180.0);
            double rz = SldRotZ.Value * (Math.PI / 180.0);

            // Update Physics
            platform.ApplyTranslationAndRotation(
                SldPosX.Value, SldPosY.Value, SldPosZ.Value,
                rx, ry, rz
            );

            UpdateUI();
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            SldPosX.Value = 0; SldPosY.Value = 0; SldPosZ.Value = 0;
            SldRotX.Value = 0; SldRotY.Value = 0; SldRotZ.Value = 0;
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

        private void SendDataToArduino(object? sender, EventArgs e)
        {
            if (arduinoPort == null || !arduinoPort.IsOpen) return;

            // Safety Check: Look for Math Errors (NaN) BEFORE creating the packet
            for (int i = 0; i < 6; i++)
            {
                // If any angle is "Not a Number" (Impossible math), STOP immediately.
                // This prevents the "Jump to 0" glitch.
                if (double.IsNaN(platform.Alpha[i]))
                {
                    return;
                }
            }

            try
            {
                // 1. Header
                byte[] header = { 0x6A, 0x6A };
                arduinoPort.Write(header, 0, 2);

                // 2. Data Packet
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