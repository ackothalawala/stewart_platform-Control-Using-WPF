namespace stewart_platform
{
    public class RobotConfig
    {
        //   Dimensions of Robot (mm) 
        public double BaseRadius { get; set; } = 86.0;
        public double PlatformRadius { get; set; } = 50.0;
        public double HornLength { get; set; } = 36.845;
        public double RodLength { get; set; } = 144.0;
        public double InitialHeight { get; set; } = 135.0;

        // Base servo and Platform uJ Angles in degrees 

        public double[] BaseAngles { get; set; } = { -0.0,-60.0, -120.0, -180.0, -240.0, -300.0 };
        public double[] PlatformAngles { get; set; } = { -75.0, -105.0, -195.0, -225.0, -315.0, -345.0 };

        //Beta Angles in radians
        public double[] BetaAngles { get; set; } = {
            Math.PI / 6,      // 30 deg
            -5 * Math.PI / 6, // -150 deg
            -Math.PI / 2,     // -90 deg
            Math.PI / 2,      // 90 deg
            5 * Math.PI / 6,  // 150 deg
            -Math.PI / 6      // -30 deg
        };

        // limits for translation and rotation 
        public double MaxTranslation { get; set; } = 35.0; //(mm)
        public double MaxRotation { get; set; } = 5.0;  //in degrees

        // set the baud rate for serial communication
        public int BaudRate { get; set; } = 115200;

       
    }
}