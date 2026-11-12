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

    }
}
