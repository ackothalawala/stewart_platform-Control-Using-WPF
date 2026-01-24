namespace stewart_platform
{
    public class RobotConfig
    {
        //   Dimensions of Robot (mm) 
        public double BaseRadius { get; set; } = 86.0;
        public double PlatformRadius { get; set; } = 70.0;
        public double HornLength { get; set; } = 36.845;
        public double RodLength { get; set; } = 144.0;
        public double InitialHeight { get; set; } = 135.0;

        // Base servo and Platform uJ Angles in degrees 

        public double[] BaseAngles { get; set; } = { -0.0,-60.0, -120.0, -180.0, -240.0, -300.0 };
        public double[] PlatformAngles { get; set; } = { -0.0,-60.0, -120.0, -180.0, -240.0, -300.0  };

        //Beta Angles in radians
        public double[] BetaAngles { get; set; } = {
                0.0,              // 0°    servo 0 (at 0°)
                -Math.PI / 3,     // -60°  servo 1 (at -60°)
                -2 * Math.PI / 3, // -120° servo 2 (at -120°)
                Math.PI,          // 180°  servo 3 (at -180°)
                2 * Math.PI / 3,  // 120°  servo 4 (at -240°)
                Math.PI / 3       // 60°   servo 5 (at -300°)
         };

        // limits for translation and rotation 
        public double MaxTranslation { get; set; } = 35.0; //(mm)
        public double MaxRotation { get; set; } = 5.0;  //in degrees

        // set the baud rate for serial communication
        public int BaudRate { get; set; } = 115200;

       
    }
}