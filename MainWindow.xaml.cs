using System;
using System.Collections.Generic; // For Lists
using System.IO.Ports;
using System.Windows;
using System.Windows.Media.Media3D; // For Point3D
using System.Windows.Threading;
using HelixToolkit.Wpf; // For 3D Visuals

namespace stewart_platform
{
    public partial class MainWindow : Window
    {
        // 1. The Physics Engine
        StewartPlatform platform = new StewartPlatform();

        // 2. Hardware Comms
        SerialPort? arduinoPort;
        DispatcherTimer sendTimer;
        DispatcherTimer resetAnimationTimer;

        // 3. Lists to hold our 3D Objects (Makes code cleaner)
        List<TubeVisual3D> hornVisuals = new List<TubeVisual3D>();
        List<TubeVisual3D> rodVisuals = new List<TubeVisual3D>();

        public MainWindow()
        {
            InitializeComponent();

            // --- SETUP 3D VISUAL LISTS ---
            // We put the XAML objects into lists so we can loop through them
            hornVisuals.Add(VisHorn0); hornVisuals.Add(VisHorn1);
            hornVisuals.Add(VisHorn2); hornVisuals.Add(VisHorn3);
            hornVisuals.Add(VisHorn4); hornVisuals.Add(VisHorn5);

            rodVisuals.Add(VisRod0); rodVisuals.Add(VisRod1);
            rodVisuals.Add(VisRod2); rodVisuals.Add(VisRod3);
            rodVisuals.Add(VisRod4); rodVisuals.Add(VisRod5);

            LoadAvailablePorts();

            // Setup Timers
            sendTimer = new DispatcherTimer();
            sendTimer.Interval = TimeSpan.FromMilliseconds(40);
            sendTimer.Tick += SendDataToArduino;

            resetAnimationTimer = new DispatcherTimer();
            resetAnimationTimer.Interval = TimeSpan.FromMilliseconds(20);
            resetAnimationTimer.Tick += AnimateResetStep;

            // Initial Draw (Robot starts at Home Position)
            platform.ApplyTranslationAndRotation(0, 0, 0, 0, 0, 0);
            Update3DVisualization();
        }

        private void LoadAvailablePorts()
        {
            string[] ports = SerialPort.GetPortNames();
            PortSelector.ItemsSource = ports;
            if (ports.Length > 0) PortSelector.SelectedIndex = 0;
        }

        // --- CORE CONTROL ---
        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (platform == null) return;

            // 1. Calculate Physics
            double rx = SldRotX.Value * (Math.PI / 180.0);
            double ry = SldRotY.Value * (Math.PI / 180.0);
            double rz = SldRotZ.Value * (Math.PI / 180.0);

            platform.ApplyTranslationAndRotation(
                SldPosX.Value, SldPosY.Value, SldPosZ.Value,
                rx, ry, rz
            );

            // 2. Update the Screen
            UpdateUI();
            Update3DVisualization();
        }

        // --- 3D VISUALIZATION ENGINE ---
        private void Update3DVisualization()
        {
            // 1. Draw Base (Hexagon Loop)
            var basePath = new Point3DCollection();
            foreach (var p in platform.BasePoints) basePath.Add(p);
            basePath.Add(platform.BasePoints[0]); // Close the loop
            VisBase.Path = basePath;

            // 2. Draw Platform (Hexagon Loop)
            var platPath = new Point3DCollection();
            foreach (var p in platform.PlatformPoints) platPath.Add(p);
            platPath.Add(platform.PlatformPoints[0]); // Close the loop
            VisPlatform.Path = platPath;

            // 3. Draw Legs
            for (int i = 0; i < 6; i++)
            {
                // Horn: Base -> Horn End
                var hornPath = new Point3DCollection();
                hornPath.Add(platform.BasePoints[i]);
                hornPath.Add(platform.HornEndPoints[i]);
                hornVisuals[i].Path = hornPath;

                // Rod: Horn End -> Platform
                var rodPath = new Point3DCollection();
                rodPath.Add(platform.HornEndPoints[i]);
                rodPath.Add(platform.PlatformPoints[i]);
                rodVisuals[i].Path = rodPath;
            }
        }

        // --- BUTTONS & ANIMATION ---
        private void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            if (arduinoPort != null && arduinoPort.IsOpen)
            {
                try
                {
                    // Unsubscribe from event before closing to prevent errors
                    arduinoPort.DataReceived -= ArduinoPort_DataReceived;
                    arduinoPort.Close();
                }
                catch { }
                sendTimer.Stop();
                BtnConnect.Content = "Connect";
                TxtStatus.Text = "Disconnected";
                TxtStatus.Foreground = System.Windows.Media.Brushes.Red;
            }
            else
            {
                if (PortSelector.SelectedItem == null) return;
                try
                {
                    arduinoPort = new SerialPort(PortSelector.SelectedItem.ToString(), 115200);
                    arduinoPort.Open();

                    // [New] Listen for data coming BACK from Arduino
                    arduinoPort.DataReceived += ArduinoPort_DataReceived;

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

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            resetAnimationTimer.Start();
        }

        private void AnimateResetStep(object? sender, EventArgs e)
        {
            double step = 1.0;

            bool MoveSliderTowardsZero(System.Windows.Controls.Slider sld)
            {
                if (Math.Abs(sld.Value) > 0.1)
                {
                    if (sld.Value > 0) sld.Value -= step;
                    else sld.Value += step;

                    if (Math.Abs(sld.Value) < step) sld.Value = 0;
                    return false;
                }
                else
                {
                    sld.Value = 0;
                    return true;
                }
            }

            bool xDone = MoveSliderTowardsZero(SldPosX);
            bool yDone = MoveSliderTowardsZero(SldPosY);
            bool zDone = MoveSliderTowardsZero(SldPosZ);
            bool rxDone = MoveSliderTowardsZero(SldRotX);
            bool ryDone = MoveSliderTowardsZero(SldRotY);
            bool rzDone = MoveSliderTowardsZero(SldRotZ);

            if (xDone && yDone && zDone && rxDone && ryDone && rzDone)
            {
                resetAnimationTimer.Stop();
            }
        }

        // --- UI TEXT UPDATES ---
        private void UpdateUI()
        {
            TxtServo0.Text = $"Servo 0: {platform.GetAlphaDegree(0):F2}°";
            TxtServo1.Text = $"Servo 1: {platform.GetAlphaDegree(1):F2}°";
            TxtServo2.Text = $"Servo 2: {platform.GetAlphaDegree(2):F2}°";
            TxtServo3.Text = $"Servo 3: {platform.GetAlphaDegree(3):F2}°";
            TxtServo4.Text = $"Servo 4: {platform.GetAlphaDegree(4):F2}°";
            TxtServo5.Text = $"Servo 5: {platform.GetAlphaDegree(5):F2}°";
        }

        // --- SERIAL SENDING ---
        private void SendDataToArduino(object? sender, EventArgs e)
        {
            if (arduinoPort == null || !arduinoPort.IsOpen) return;

            // Safety Check for NaN
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

        private void ArduinoPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (arduinoPort == null || !arduinoPort.IsOpen) return;

            try
            {
                
                string line = arduinoPort.ReadLine();

                if (line.StartsWith("FB:"))
                {
                    // Remove header and split by comma
                    string cleanData = line.Substring(3).Trim();
                    string[] parts = cleanData.Split(',');

                    if (parts.Length == 4)
                    {
                        // We must use Dispatcher to update UI from a background thread
                        Dispatcher.Invoke(() =>
                        {
                            // Parse values (MPU6050_light returns degrees)
                            double roll = double.Parse(parts[0]);
                            double pitch = double.Parse(parts[1]);
                            double yaw = double.Parse(parts[2]);
                            double temp = double.Parse(parts[3]);

                            // Update TextBlocks
                            TxtSensorRoll.Text = $"{roll:F1}°";
                            TxtSensorPitch.Text = $"{pitch:F1}°";
                            TxtSensorYaw.Text = $"{yaw:F1}°";
                            TxtSensorTemp.Text = $"{temp:F1}°C";
                        });
                    }
                }
            }
            catch (Exception)
            {
                // Serial read errors can happen (timeouts, noise), usually safe to ignore in this loop
            }
        }
    }
}