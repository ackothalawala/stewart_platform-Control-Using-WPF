using System;
using System.Windows.Media.Media3D;

namespace stewart_platform
{
    public class StewartPlatform
    {
        // --- Config Values ---
        private double BaseRadius;
        private double PlatformRadius;
        private double HornLength;
        private double RodLength;
        private double InitialHeight;

        private double[] BaseAngles;
        private double[] PlatformAngles;
        private double[] Beta;

        // --- Home yaw offset (radians) ---
        private double HomeYawOffsetRad;

        // --- Public Drawing Points ---
        public Point3D[] BasePoints { get; private set; } = new Point3D[6];
        public Point3D[] PlatformPoints { get; private set; } = new Point3D[6];
        public Point3D[] HornEndPoints { get; private set; } = new Point3D[6];

        // --- Internal Math ---
        private Vector3D[] b = new Vector3D[6];
        private Vector3D[] p = new Vector3D[6];
        public double[] Alpha { get; private set; } = new double[6];

        // --- Current Pose ---
        private Vector3D Translation;
        private Vector3D Rotation;

        // --- Constructor ---
        public StewartPlatform(RobotConfig config)
        {
            // Load dimensions
            BaseRadius = config.BaseRadius;
            PlatformRadius = config.PlatformRadius;
            HornLength = config.HornLength;
            RodLength = config.RodLength;
            InitialHeight = config.InitialHeight;

            // Load geometry
            BaseAngles = config.BaseAngles;
            PlatformAngles = config.PlatformAngles;
            Beta = config.BetaAngles;

            // Load home yaw offset
            HomeYawOffsetRad = config.HomeYawOffsetDeg * Math.PI / 180.0;

            InitializePlatform();
        }

        private void InitializePlatform()
        {
            for (int i = 0; i < 6; i++)
            {
                double xb = BaseRadius * Math.Cos(ToRadians(BaseAngles[i]));
                double yb = BaseRadius * Math.Sin(ToRadians(BaseAngles[i]));
                b[i] = new Vector3D(xb, yb, 0);
                BasePoints[i] = new Point3D(xb, yb, 0);

                double px = PlatformRadius * Math.Cos(ToRadians(PlatformAngles[i]));
                double py = PlatformRadius * Math.Sin(ToRadians(PlatformAngles[i]));
                p[i] = new Vector3D(px, py, 0);
            }
        }

        public void ApplyTranslationAndRotation(
            double x, double y, double z,
            double rotX, double rotY, double rotZ)
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
                // --- Apply HOME YAW OFFSET ---
                double rz = Rotation.Z + HomeYawOffsetRad;

                double cx = Math.Cos(Rotation.X); double sx = Math.Sin(Rotation.X);
                double cy = Math.Cos(Rotation.Y); double sy = Math.Sin(Rotation.Y);
                double cz = Math.Cos(rz); double sz = Math.Sin(rz);

                double qx = (cz * cy) * p[i].X
                          + (-sz * cx + cz * sy * sx) * p[i].Y
                          + (sz * sx + cz * sy * cx) * p[i].Z;

                double qy = (sz * cy) * p[i].X
                          + (cz * cx + sz * sy * sx) * p[i].Y
                          + (-cz * sx + sz * sy * cx) * p[i].Z;

                double qz = (-sy) * p[i].X
                          + (cy * sx) * p[i].Y
                          + (cy * cx) * p[i].Z;

                Vector3D q = new Vector3D(qx, qy, qz) + Translation + h0;
                PlatformPoints[i] = new Point3D(q.X, q.Y, q.Z);

                Vector3D l = q - b[i];

                double L = l.LengthSquared - (RodLength * RodLength) + (HornLength * HornLength);
                double M = 2 * HornLength * (q.Z - b[i].Z);
                double N = 2 * HornLength *
                    (Math.Cos(Beta[i]) * (q.X - b[i].X) +
                     Math.Sin(Beta[i]) * (q.Y - b[i].Y));

                double val = L / Math.Sqrt(M * M + N * N);
                val = Math.Clamp(val, -1.0, 1.0);

                Alpha[i] = Math.Asin(val) - Math.Atan2(N, M);

                double ax = HornLength * Math.Cos(Alpha[i]) * Math.Cos(Beta[i]) + b[i].X;
                double ay = HornLength * Math.Cos(Alpha[i]) * Math.Sin(Beta[i]) + b[i].Y;
                double az = HornLength * Math.Sin(Alpha[i]) + b[i].Z;

                HornEndPoints[i] = new Point3D(ax, ay, az);
            }
        }

        private double ToRadians(double degrees) => degrees * Math.PI / 180.0;
        public double GetAlphaDegree(int i) => Alpha[i] * 180.0 / Math.PI;
    }
}
