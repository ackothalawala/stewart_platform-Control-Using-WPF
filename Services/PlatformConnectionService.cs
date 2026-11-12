using System;
using System.IO.Ports;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace stewart_platform.Services
{
    // Owns hardware connections and transport-specific I/O; never accesses WPF controls.
    public class PlatformConnectionService
    {
        private SerialPort? arduinoPort;
        private ClientWebSocket? wsClient;
        private bool isWifiMode;

        public bool IsConnected { get; private set; }
        public event Action<string?>? FeedbackLineReceived;

        public static string[] GetAvailablePorts() => SerialPort.GetPortNames();

        public async Task ConnectWifiAsync(string url)
        {
            isWifiMode = true;
            wsClient = new ClientWebSocket();
            await wsClient.ConnectAsync(new Uri(url), CancellationToken.None);
            _ = Task.Run(ReceiveWebSocketData);
            IsConnected = true;
        }

        public void ConnectSerial(string portName, int baudRate)
        {
            isWifiMode = false;
            arduinoPort = new SerialPort(portName, baudRate);
            arduinoPort.Open();
            arduinoPort.DataReceived += ArduinoPort_DataReceived;
            IsConnected = true;
        }

        public async Task SendAsync(string dataStr)
        {
            if (!IsConnected) return;

            if (isWifiMode && wsClient != null && wsClient.State == WebSocketState.Open)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(dataStr);
                await wsClient.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text,
                    true, CancellationToken.None);
            }
            else if (!isWifiMode && arduinoPort != null && arduinoPort.IsOpen)
            {
                // Match the working Avalonia USB sender: six values + newline.
                arduinoPort.Write(dataStr + "\n");
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
                        FeedbackLineReceived?.Invoke(msg);
                    }
                }
                catch { break; } // Keep original behavior: stop receiving on error.
            }
        }

        private void ArduinoPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (arduinoPort == null || !arduinoPort.IsOpen) return;
            try
            {
                string? line = arduinoPort.ReadLine();
                FeedbackLineReceived?.Invoke(line);
            }
            catch { } // Keep original behavior for serial receive errors.
        }

        public void Disconnect()
        {
            if (arduinoPort != null && arduinoPort.IsOpen)
            {
                arduinoPort.DataReceived -= ArduinoPort_DataReceived;
                arduinoPort.Close();
            }

            if (wsClient != null && wsClient.State == WebSocketState.Open)
            {
                // Retain original fire-and-forget WebSocket closure behavior.
                _ = wsClient.CloseAsync(WebSocketCloseStatus.NormalClosure,
                    "Client disconnecting", CancellationToken.None);
            }
            IsConnected = false;
        }
    }
}
