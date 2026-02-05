using System;

namespace stewart_platform
{
    public class RobotConfig
    {
        // --- Dimensions (mm) ---
        public double BaseRadius { get; set; } = 86.0;
        public double PlatformRadius { get; set; } = 56.0;
        public double HornLength { get; set; } = 36.845;
        public double RodLength { get; set; } = 144.0;
        public double InitialHeight { get; set; } = 135.0;

        // --- Base & Platform Angles (degrees) ---
        public double[] BaseAngles { get; set; } =
            { -60, -120, -180, -240, -300, -360 };

        public double[] PlatformAngles { get; set; } =
            { -75, -105, -195, -225, -315,-345 };

        // --- Servo Orientation (Beta) in radians ---
        public double[] BetaAngles { get; set; } =
        {
           Math.PI / 6,      // Servo 0: -60° + 90° = 30° (PI/6)
             5 * Math.PI / 6,  // Servo 1: -120° - 90° = -210° -> 150° (5PI/6)
             -Math.PI / 2,     // Servo 2: -180° + 90° = -90° (-PI/2)
             Math.PI / 6,      // Servo 3: -240° - 90° = -330° -> 30° (PI/6)
             5 * Math.PI / 6,  // Servo 4: -300° + 90° = -210° -> 150° (5PI/6)
             -Math.PI / 2
        };

        // --- HOME ORIENTATION OFFSET ---
        // Compensates geometric yaw caused by optimized platform layout
        public double HomeYawOffsetDeg { get; set; } = 0.0;

        // --- Motion Limits ---
        public double MaxTranslation { get; set; } = 35.0;
        public double MaxRotation { get; set; } = 5.0;

        // --- Serial ---
        public int BaudRate { get; set; } = 115200;
    }
}
