using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Windows;
using System.Windows.Controls; // For Slider
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using HelixToolkit.Wpf;

namespace stewart_platform
{
    public partial class MainWindow : Window
    {
        // 1. Configuration & Engine
        RobotConfig config = new RobotConfig(); // Loads the settings
        StewartPlatform platform;

        // 2. Hardware Comms
        SerialPort? arduinoPort;
        DispatcherTimer sendTimer;
        DispatcherTimer resetAnimationTimer;

        // 3. Visual Lists
        List<TubeVisual3D> hornVisuals = new List<TubeVisual3D>();
        List<TubeVisual3D> rodVisuals = new List<TubeVisual3D>();

        public MainWindow()
        {
            InitializeComponent();

            // --- A. INITIALIZE MATH WITH CONFIG ---
            platform = new StewartPlatform(config);

            // --- B. SETUP UI LIMITS FROM CONFIG ---
            // This ensures sliders match the config limits automatically
            SetupSliders();

            // --- C. SETUP 3D VISUALS ---
            hornVisuals.Add(VisHorn0); hornVisuals.Add(VisHorn1);
            hornVisuals.Add(VisHorn2); hornVisuals.Add(VisHorn3);
            hornVisuals.Add(VisHorn4); hornVisuals.Add(VisHorn5);

            rodVisuals.Add(VisRod0); rodVisuals.Add(VisRod1);
            rodVisuals.Add(VisRod2); rodVisuals.Add(VisRod3);
            rodVisuals.Add(VisRod4); rodVisuals.Add(VisRod5);

            LoadAvailablePorts();

            // --- D. TIMERS ---
            sendTimer = new DispatcherTimer();
            sendTimer.Interval = TimeSpan.FromMilliseconds(40);
            sendTimer.Tick += SendDataToArduino;

            resetAnimationTimer = new DispatcherTimer();
            resetAnimationTimer.Interval = TimeSpan.FromMilliseconds(20);
            resetAnimationTimer.Tick += AnimateResetStep;

            // Initial Draw
            platform.ApplyTranslationAndRotation(0, 0, 0, 0, 0, 0);
            Update3DVisualization();
        }

        private void SetupSliders()
        {
            // Apply limits from RobotConfig to the sliders
            SldPosX.Minimum = -config.MaxTranslation; SldPosX.Maximum = config.MaxTranslation;
            SldPosY.Minimum = -config.MaxTranslation; SldPosY.Maximum = config.MaxTranslation;
            SldPosZ.Minimum = -config.MaxTranslation; SldPosZ.Maximum = config.MaxTranslation;

            SldRotX.Minimum = -config.MaxRotation; SldRotX.Maximum = config.MaxRotation;
            SldRotY.Minimum = -config.MaxRotation; SldRotY.Maximum = config.MaxRotation;
            SldRotZ.Minimum = -config.MaxRotation; SldRotZ.Maximum = config.MaxRotation;
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

            // Convert UI degrees to radians for math
            double rx = SldRotX.Value * (Math.PI / 180.0);
            double ry = SldRotY.Value * (Math.PI / 180.0);
            double rz = SldRotZ.Value * (Math.PI / 180.0);

            platform.ApplyTranslationAndRotation(
                SldPosX.Value, SldPosY.Value, SldPosZ.Value,
                rx, ry, rz
            );

            UpdateUI();
            Update3DVisualization();
        }

        // --- 3D VISUALIZATION ---
        private void Update3DVisualization()
        {
            // 1. Draw Base
            var basePath = new Point3DCollection();
            foreach (var p in platform.BasePoints) basePath.Add(p);
            basePath.Add(platform.BasePoints[0]);
            VisBase.Path = basePath;

            // 2. Draw Platform
            var platPath = new Point3DCollection();
            foreach (var p in platform.PlatformPoints) platPath.Add(p);
            platPath.Add(platform.PlatformPoints[0]);
            VisPlatform.Path = platPath;

            // 3. Draw Legs
            for (int i = 0; i < 6; i++)
            {
                var hornPath = new Point3DCollection();
                hornPath.Add(platform.BasePoints[i]);
                hornPath.Add(platform.HornEndPoints[i]);
                hornVisuals[i].Path = hornPath;

                var rodPath = new Point3DCollection();
                rodPath.Add(platform.HornEndPoints[i]);
                rodPath.Add(platform.PlatformPoints[i]);
                rodVisuals[i].Path = rodPath;
            }
        }

        // --- BUTTONS ---
        private void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            if (arduinoPort != null && arduinoPort.IsOpen)
            {
                try
                {
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
                    // Use BaudRate from Config
                    arduinoPort = new SerialPort(PortSelector.SelectedItem.ToString(), config.BaudRate);
                    arduinoPort.Open();
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
            double translationStep = 0.5;
            double rotationStep = 0.05;

            bool MoveTowardsZero(Slider sld, double step)
            {
                if (Math.Abs(sld.Value) > step)
                {
                    if (sld.Value > 0) sld.Value -= step;
                    else sld.Value += step;
                    return false;
                }
                else
                {
                    sld.Value = 0;
                    return true;
                }
            }

            bool x = MoveTowardsZero(SldPosX, translationStep);
            bool y = MoveTowardsZero(SldPosY, translationStep);
            bool z = MoveTowardsZero(SldPosZ, translationStep);
            bool rx = MoveTowardsZero(SldRotX, rotationStep);
            bool ry = MoveTowardsZero(SldRotY, rotationStep);
            bool rz = MoveTowardsZero(SldRotZ, rotationStep);

            if (x && y && z && rx && ry && rz)
            {
                resetAnimationTimer.Stop();
            }
        }

        // --- SERIAL COMMUNICATION ---
        private void SendDataToArduino(object? sender, EventArgs e)
        {
            if (arduinoPort == null || !arduinoPort.IsOpen) return;

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
            catch (Exception) { sendTimer.Stop(); }
        }

        private void ArduinoPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (arduinoPort == null || !arduinoPort.IsOpen) return;
            try
            {
                string line = arduinoPort.ReadLine();
                if (line.StartsWith("FB:"))
                {
                    string cleanData = line.Substring(3).Trim();
                    string[] parts = cleanData.Split(',');

                    if (parts.Length == 4)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            if (double.TryParse(parts[0], out double roll)) TxtSensorRoll.Text = $"{roll:F1}°";
                            if (double.TryParse(parts[1], out double pitch)) TxtSensorPitch.Text = $"{pitch:F1}°";
                            if (double.TryParse(parts[2], out double yaw)) TxtSensorYaw.Text = $"{yaw:F1}°";
                            if (double.TryParse(parts[3], out double temp)) TxtSensorTemp.Text = $"{temp:F1}°C";
                        });
                    }
                }
            }
            catch { }
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
    }
}