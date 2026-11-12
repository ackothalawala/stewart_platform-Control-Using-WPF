using System;

namespace stewart_platform
{
    public class RobotConfig
    {
        // --- Dimensions (mm) ---
        public double BaseRadius { get; set; } = 336.0;
        public double PlatformRadius { get; set; } = 294.6;
        public double HornLength { get; set; } = 45.0;
        public double RodLength { get; set; } = 120.0;
        public double InitialHeight { get; set; } = 100.0;


        // --- Base & Platform Angles (degrees) ---
        public double[] BaseAngles { get; set; } =
            { -50, -70, -170, -190, -290, -310 };

        public double[] PlatformAngles { get; set; } =
            {-80, -100, -200, -220, -320, -340 };

        // --- Servo Orientation (Beta) in radians ---
        public double[] BetaAngles { get; set; } =
        {
           Math.PI / 6, -5 * Math.PI / 6, -Math.PI / 2,
           Math.PI / 2, 5 * Math.PI / 6, -Math.PI / 6
        };

        // --- HOME ORIENTATION OFFSET ---
        // Compensates geometric yaw caused by optimized platform layout
        public double HomeYawOffsetDeg { get; set; } = 30.0;

        // --- Motion Limits ---
        public double MaxTranslation { get; set; } = 30.0;
        public double MaxRotation { get; set; } = 30.0;

        // --- Serial ---
        public int BaudRate { get; set; } = 115200;
    }
}


