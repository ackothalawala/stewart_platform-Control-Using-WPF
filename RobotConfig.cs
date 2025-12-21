namespace stewart_platform
{
    public class RobotConfig
    {
        //  Physical Dimensions (mm) 
        public double BaseRadius { get; set; } = 300.0;
        public double PlatformRadius { get; set; } = 284.0;
        public double HornLength { get; set; } = 40.0;
        public double RodLength { get; set; } = 118.0;
        public double InitialHeight { get; set; } = 100.0;

        // Base and Platform Angles (degrees)

        public double[] BaseAngles { get; set; } = { -50.0, -70.0, -170.0, -190.0, -290.0, -310.0 };
        public double[] PlatformAngles { get; set; } = { -54.0, -66.0, -174.0, -186.0, -294.0, -306.0 };

        //Beta Angles (radians)
        public double[] BetaAngles { get; set; } = {
            Math.PI / 6,      // 30 deg
            -5 * Math.PI / 6, // -150 deg
            -Math.PI / 2,     // -90 deg
            Math.PI / 2,      // 90 deg
            5 * Math.PI / 6,  // 150 deg
            -Math.PI / 6      // -30 deg
        };

        // Sliders Safety Limits 
        public double MaxTranslation { get; set; } = 35.0; // mm
        public double MaxRotation { get; set; } = 5.0;     // degrees

        // Communication 
        public int BaudRate { get; set; } = 115200;

       
    }
}