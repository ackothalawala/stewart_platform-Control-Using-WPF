using System;
using System.Windows.Media.Media3D;

namespace stewart_platform.Kinematics
{
    // Builds the six attachment points in their local coordinate systems.
    internal sealed class PlatformGeometry
    {
        private readonly RobotConfig config;

        internal Vector3D[] LocalBasePoints { get; } = new Vector3D[6];
        internal Vector3D[] LocalPlatformPoints { get; } = new Vector3D[6];

        internal PlatformGeometry(RobotConfig config)
        {
            this.config = config;
        }

        internal void Initialize(Point3D[] basePoints)
        {
            for (int i = 0; i < 6; i++)
            {
                double xb = config.BaseRadius * Math.Cos(ToRadians(config.BaseAngles[i]));
                double yb = config.BaseRadius * Math.Sin(ToRadians(config.BaseAngles[i]));
                LocalBasePoints[i] = new Vector3D(xb, yb, 0);
                basePoints[i] = new Point3D(xb, yb, 0);

                double px = config.PlatformRadius * Math.Cos(ToRadians(config.PlatformAngles[i]));
                double py = config.PlatformRadius * Math.Sin(ToRadians(config.PlatformAngles[i]));
                LocalPlatformPoints[i] = new Vector3D(px, py, 0);
            }
        }

        private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
    }
}
