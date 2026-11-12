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

    }
}
