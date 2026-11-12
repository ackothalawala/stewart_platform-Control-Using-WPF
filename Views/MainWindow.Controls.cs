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

    }
}
