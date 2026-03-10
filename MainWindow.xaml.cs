using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using HelixToolkit.Wpf;

namespace stewart_platform
{
    public partial class MainWindow : Window
    {
        // 1. Configuration & Engine
        RobotConfig config = new RobotConfig();
        StewartPlatform platform;

        // 2. Hardware Comms
        SerialPort? arduinoPort;
        ClientWebSocket? wsClient;
        bool isConnected = false;
        bool isWifiMode = false;

        DispatcherTimer sendTimer;

        // 3. Smooth Movement Engine
        DispatcherTimer movementTimer;
        private double[] targetValues = new double[6]; // X, Y, Z, Rx, Ry, Rz
        private bool isInternalUpdate = false;

        // 4. Visual Lists
        List<TubeVisual3D> hornVisuals = new List<TubeVisual3D>();
        List<TubeVisual3D> rodVisuals = new List<TubeVisual3D>();

        public MainWindow()
        {
            InitializeComponent();

            platform = new StewartPlatform(config);

            SetupSliders();

            hornVisuals.Add(VisHorn0); hornVisuals.Add(VisHorn1);
            hornVisuals.Add(VisHorn2); hornVisuals.Add(VisHorn3);
            hornVisuals.Add(VisHorn4); hornVisuals.Add(VisHorn5);

            rodVisuals.Add(VisRod0); rodVisuals.Add(VisRod1);
            rodVisuals.Add(VisRod2); rodVisuals.Add(VisRod3);
            rodVisuals.Add(VisRod4); rodVisuals.Add(VisRod5);

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
            string[] ports = SerialPort.GetPortNames();
            ModalPortSelector.ItemsSource = ports;
            if (ports.Length > 0) ModalPortSelector.SelectedIndex = 0;
        }

        private void BtnRefreshPorts_Click(object sender, RoutedEventArgs e)
        {
            LoadAvailablePorts();
        }

        // --- Connection Modal Logic ---
        private void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            if (isConnected)
            {
                DisconnectFromPlatform();
            }
            else
            {
                LoadAvailablePorts(); // Refresh ports before opening
                ModalOverlay.Visibility = Visibility.Visible;
            }
        }

        private void BtnModalCancel_Click(object sender, RoutedEventArgs e)
        {
            ModalOverlay.Visibility = Visibility.Collapsed;
        }

        private async void BtnModalConnect_Click(object sender, RoutedEventArgs e)
        {
            // Close Modal immediately
            ModalOverlay.Visibility = Visibility.Collapsed;
            TxtStatus.Text = "Connecting...";
            TxtStatus.Foreground = System.Windows.Media.Brushes.Orange;

            isWifiMode = RadioWifi.IsChecked == true;

            try
            {
                if (isWifiMode)
                {
                    // Establish WebSocket Connection
                    string url = TxtWifiUrl.Text.Trim();
                    wsClient = new ClientWebSocket();
                    await wsClient.ConnectAsync(new Uri(url), CancellationToken.None);

                    // Start listening loop in the background
                    _ = Task.Run(ReceiveWebSocketData);
                }
                else
                {
                    // Establish Serial Connection
                    if (ModalPortSelector.SelectedItem == null) throw new Exception("No COM port selected");
                    string portName = ModalPortSelector.SelectedItem.ToString() ?? "";
                    arduinoPort = new SerialPort(portName, config.BaudRate);
                    arduinoPort.Open();
                    arduinoPort.DataReceived += ArduinoPort_DataReceived;
                }

                isConnected = true;
                sendTimer.Start();
                BtnConnect.Content = "Disconnect";
                TxtStatus.Text = isWifiMode ? "Connected (Wi-Fi)" : "Connected (USB)";
                TxtStatus.Foreground = System.Windows.Media.Brushes.Green;
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

            if (arduinoPort != null && arduinoPort.IsOpen)
            {
                arduinoPort.DataReceived -= ArduinoPort_DataReceived;
                arduinoPort.Close();
            }

            if (wsClient != null && wsClient.State == WebSocketState.Open)
            {
                // Fire and forget closure
                _ = wsClient.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnecting", CancellationToken.None);
            }

            isConnected = false;
            BtnConnect.Content = "Connect Platform";
            TxtStatus.Text = "Disconnected";
            TxtStatus.Foreground = System.Windows.Media.Brushes.Red;
        }


        // --- Hardware Data Handling ---
        private async void SendDataToPlatform(object? sender, EventArgs e)
        {
            if (!isConnected) return;
            for (int i = 0; i < 6; i++) if (double.IsNaN(platform.Alpha[i])) return;

            try
            {
                string dataStr = "";
                for (int i = 0; i < 6; i++)
                {
                    int val = (int)(platform.GetAlphaDegree(i) * 100);
                    dataStr += val.ToString();
                    if (i < 5) dataStr += ",";
                }

                if (isWifiMode && wsClient != null && wsClient.State == WebSocketState.Open)
                {
                    // Send text payload over WebSocket
                    var bytes = Encoding.UTF8.GetBytes(dataStr);
                    await wsClient.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
                }
                else if (!isWifiMode && arduinoPort != null && arduinoPort.IsOpen)
                {
                    // Send bytes + string over Serial
                    byte[] header = { 0x6A, 0x6A };
                    arduinoPort.Write(header, 0, 2);
                    arduinoPort.Write(dataStr + "\n");
                }
            }
            catch (Exception)
            {
                DisconnectFromPlatform();
            }
        }

        private async Task ReceiveWebSocketData()
        {
            var buffer = new byte[1024];
            while (wsClient != null && wsClient.State == WebSocketState.Open)
            {
                try
                {
                    var result = await wsClient.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        string msg = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        ProcessFeedbackLine(msg);
                    }
                }
                catch { break; } // Exit loop if socket closes/errors
            }
        }

        private void ArduinoPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (arduinoPort == null || !arduinoPort.IsOpen) return;
            try
            {
                string? line = arduinoPort.ReadLine();
                ProcessFeedbackLine(line);
            }
            catch { }
        }

        // Shared parsing method for both USB and Wi-Fi
        private void ProcessFeedbackLine(string? line)
        {
            if (!string.IsNullOrEmpty(line) && line.StartsWith("FB:"))
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


        // --- UI & Movement Logic ---
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
            {
                movementTimer.Stop();
            }
        }

        private bool MoveAxisTowards(Slider sld, int targetIndex, double step)
        {
            double current = sld.Value;
            double target = targetValues[targetIndex];
            double diff = target - current;

            if (Math.Abs(diff) > step)
            {
                sld.Value += Math.Sign(diff) * step;
                return false;
            }
            else
            {
                sld.Value = target;
                return true;
            }
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

        private void Update3DVisualization()
        {
            var basePath = new Point3DCollection();
            foreach (var p in platform.BasePoints) basePath.Add(p);
            basePath.Add(platform.BasePoints[0]);
            VisBase.Path = basePath;

            var platPath = new Point3DCollection();
            foreach (var p in platform.PlatformPoints) platPath.Add(p);
            platPath.Add(platform.PlatformPoints[0]);
            VisPlatform.Path = platPath;

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