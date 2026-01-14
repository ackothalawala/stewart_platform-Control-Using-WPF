using System;
using System.Windows.Media.Media3D;

namespace stewart_platform
{
    public class StewartPlatform
    {
        // Variables that hold the config values
        private double BaseRadius;
        private double PlatformRadius;
        private double HornLength;
        private double RodLength;
        private double InitialHeight;

        private double[] BaseAngles;
        private double[] PlatformAngles;
        private double[] Beta;

        // 3D Drawing Points (Public for UI)
        public Point3D[] BasePoints { get; private set; } = new Point3D[6];
        public Point3D[] PlatformPoints { get; private set; } = new Point3D[6];
        public Point3D[] HornEndPoints { get; private set; } = new Point3D[6];

        // Internal Math Vectors
        private Vector3D[] b = new Vector3D[6];
        private Vector3D[] p = new Vector3D[6];
        public double[] Alpha { get; private set; } = new double[6];

        // Current State
        private Vector3D Translation;
        private Vector3D Rotation;

        // CONSTRUCTOR: Now accepts the Config object
        public StewartPlatform(RobotConfig config)
        {
            // 1. Load Dimensions from Config
            this.BaseRadius = config.BaseRadius;
            this.PlatformRadius = config.PlatformRadius;
            this.HornLength = config.HornLength;
            this.RodLength = config.RodLength;
            this.InitialHeight = config.InitialHeight;

            // 2. Load Angles from Config
            this.BaseAngles = config.BaseAngles;
            this.PlatformAngles = config.PlatformAngles;
            this.Beta = config.BetaAngles;

            // 3. Initialize Geometry
            InitializePlatform();
        }

        private void InitializePlatform()
        {
            for (int i = 0; i < 6; i++)
            {
                // Calculate Base Points (b) using Configured Angles
                double xb = BaseRadius * Math.Cos(ToRadians(BaseAngles[i]));
                double yb = BaseRadius * Math.Sin(ToRadians(BaseAngles[i]));
                b[i] = new Vector3D(xb, yb, 0);
                BasePoints[i] = new Point3D(xb, yb, 0);

                // Calculate Platform Points (p) using Configured Angles
                double px = PlatformRadius * Math.Cos(ToRadians(PlatformAngles[i]));
                double py = PlatformRadius * Math.Sin(ToRadians(PlatformAngles[i]));
                p[i] = new Vector3D(px, py, 0);
            }
        }

        public void ApplyTranslationAndRotation(double x, double y, double z, double rotX, double rotY, double rotZ)
        {
            Translation = new Vector3D(x, y, z);
            Rotation = new Vector3D(rotX, rotY, rotZ);
            CalculateAngles();
        }

        private void CalculateAngles()
        {
            Vector3D h0 = new Vector3D(0, 0, InitialHeight);

            for (int i = 0; i < 6; i++)
            {
                // 1. Calculate q[i] (Platform Joint World Position)
                double cx = Math.Cos(Rotation.X); double sx = Math.Sin(Rotation.X);
                double cy = Math.Cos(Rotation.Y); double sy = Math.Sin(Rotation.Y);
                double cz = Math.Cos(Rotation.Z); double sz = Math.Sin(Rotation.Z);

                double qx = (cz * cy) * p[i].X + (-sz * cx + cz * sy * sx) * p[i].Y + (sz * sx + cz * sy * cx) * p[i].Z;
                double qy = (sz * cy) * p[i].X + (cz * cx + sz * sy * sx) * p[i].Y + (-cz * sx + sz * sy * cx) * p[i].Z;
                double qz = (-sy) * p[i].X + (cy * sx) * p[i].Y + (cy * cx) * p[i].Z;

                Vector3D q = new Vector3D(qx, qy, qz);
                q = q + Translation + h0;

                // Save q for drawing
                PlatformPoints[i] = new Point3D(q.X, q.Y, q.Z);

                // 2. Calculate l vector
                Vector3D l = q - b[i];

                // 3. Inverse Kinematics
                double L = l.LengthSquared - (RodLength * RodLength) + (HornLength * HornLength);
                double M = 2 * HornLength * (q.Z - b[i].Z);
                // Note: Using Beta[i] from config
                double N = 2 * HornLength * (Math.Cos(Beta[i]) * (q.X - b[i].X) + Math.Sin(Beta[i]) * (q.Y - b[i].Y));

                double val = L / Math.Sqrt(M * M + N * N);
                if (val < -1) val = -1;
                if (val > 1) val = 1;

                Alpha[i] = Math.Asin(val) - Math.Atan2(N, M);

                // 4. Calculate 'a' point (Horn End) for Drawing
                double ax = HornLength * Math.Cos(Alpha[i]) * Math.Cos(Beta[i]) + b[i].X;
                double ay = HornLength * Math.Cos(Alpha[i]) * Math.Sin(Beta[i]) + b[i].Y;
                double az = HornLength * Math.Sin(Alpha[i]) + b[i].Z;

                HornEndPoints[i] = new Point3D(ax, ay, az);
            }
        }

        private double ToRadians(double degrees) { return degrees * (Math.PI / 180.0); }
        public double GetAlphaDegree(int index) { return Alpha[index] * (180.0 / Math.PI); }
    }
}