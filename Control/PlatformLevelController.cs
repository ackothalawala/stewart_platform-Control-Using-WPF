using System;

namespace stewart_platform.Control
{
    /// <summary>
    /// Small, bounded roll/pitch feedback trim for an IMU mounted on the moving plate.
    /// It corrects the commanded pose (degrees); the existing inverse kinematics
    /// still calculates the six servo angles. No yaw PID: MPU6050 yaw drifts.
    /// </summary>
    public sealed class PlatformLevelController
    {
        // Conservative starting values. Tune only after verifying sensor axis signs.
        public double ProportionalGain { get; set; } = 0.35;
        public double IntegralGain { get; set; } = 0.04;
        public double RollCorrectionSign { get; set; } = 1.0;
        public double PitchCorrectionSign { get; set; } = 1.0;
        public double DeadbandDegrees { get; set; } = 0.7;
        public double MaxTrimDegrees { get; set; } = 4.0;
        public double MaxTrimChangePerUpdateDegrees { get; set; } = 0.25;

        private double rollIntegral;
        private double pitchIntegral;
        private double rollTrim;
        private double pitchTrim;
        private double filteredRoll;
        private double filteredPitch;
        private bool initialized;
        private DateTime stableSince = DateTime.MinValue;

        public double RollError { get; private set; }
        public double PitchError { get; private set; }
        public bool IsStable { get; private set; }
        public double FilteredRoll => filteredRoll;
        public double FilteredPitch => filteredPitch;

        public void Reset()
        {
            rollIntegral = pitchIntegral = rollTrim = pitchTrim = 0;
            RollError = PitchError = 0;
            initialized = false;
            IsStable = false;
            stableSince = DateTime.MinValue;
        }

        /// <summary>Returns the next bounded roll/pitch pose commands in degrees.</summary>
        public (double Roll, double Pitch) Update(
            double targetRoll, double targetPitch,
            double measuredRoll, double measuredPitch,
            double seconds, DateTime nowUtc)
        {
            if (!double.IsFinite(measuredRoll) || !double.IsFinite(measuredPitch))
                throw new ArgumentException("Invalid IMU feedback");

            seconds = Math.Clamp(seconds, 0.01, 0.20);
            if (!initialized)
            {
                filteredRoll = measuredRoll;
                filteredPitch = measuredPitch;
                initialized = true;
            }
            else
            {
                // Low-pass filter to suppress accelerometer vibration.
                filteredRoll += 0.25 * (measuredRoll - filteredRoll);
                filteredPitch += 0.25 * (measuredPitch - filteredPitch);
            }

            RollError = targetRoll - filteredRoll;
            PitchError = targetPitch - filteredPitch;

            rollTrim = UpdateAxis(RollError, ref rollIntegral, rollTrim, RollCorrectionSign, seconds);
            pitchTrim = UpdateAxis(PitchError, ref pitchIntegral, pitchTrim, PitchCorrectionSign, seconds);

            bool within = Math.Abs(RollError) <= DeadbandDegrees &&
                          Math.Abs(PitchError) <= DeadbandDegrees;
            if (!within) stableSince = DateTime.MinValue;
            else if (stableSince == DateTime.MinValue) stableSince = nowUtc;

            IsStable = within && (nowUtc - stableSince).TotalMilliseconds >= 600;
            return (targetRoll + rollTrim, targetPitch + pitchTrim);
        }

        private double UpdateAxis(double error, ref double integral, double previousTrim,
                                  double direction, double seconds)
        {
            // Do not wind up while near the requested attitude.
            if (Math.Abs(error) > DeadbandDegrees)
                integral = Math.Clamp(integral + error * seconds, -20.0, 20.0);

            double desired = direction * (ProportionalGain * error + IntegralGain * integral);
            desired = Math.Clamp(desired, -MaxTrimDegrees, MaxTrimDegrees);
            return Math.Clamp(desired, previousTrim - MaxTrimChangePerUpdateDegrees,
                                        previousTrim + MaxTrimChangePerUpdateDegrees);
        }
    }
}
