using System;
using System.Windows.Media.Media3D;

namespace stewart_platform
{
    public class StewartPlatform
    {
        // --- PHYSICAL CONSTANTS ---
        private readonly double[] BASE_ANGLES = { -50, -70, -170, -190, -290, -310 };
        private readonly double[] PLATFORM_ANGLES = { -54, -66, -174, -186, -294, -306 };

        // Beta angles in Radians
        private readonly double[] BETA = {
            Math.PI / 6, -5 * Math.PI / 6, -Math.PI / 2,
            Math.PI / 2, 5 * Math.PI / 6, -Math.PI / 6
        };

        // Dimensions (mm)
        public const double BASE_RADIUS = 76;
        public const double PLATFORM_RADIUS = 60;
        public const double HORN_LENGTH = 40;
        public const double ROD_LENGTH = 130;
        private const double INITIAL_HEIGHT = 120.28183632;

        // 3D DRAWING POINTS (Public so the UI can see them)
        public Point3D[] BasePoints { get; private set; } = new Point3D[6];      // b[]
        public Point3D[] PlatformPoints { get; private set; } = new Point3D[6];  // q[] (The top joints)
        public Point3D[] HornEndPoints { get; private set; } = new Point3D[6];   // a[] (The servo arm ends)

        // Internal Math Vectors
        private Vector3D[] b = new Vector3D[6];
        private Vector3D[] p = new Vector3D[6];

        // Servo Angles
        public double[] Alpha { get; private set; } = new double[6];

        // Current State
        private Vector3D Translation;
        private Vector3D Rotation;

        public StewartPlatform()
        {
            // Initialize Geometry
            for (int i = 0; i < 6; i++)
            {
                // Calculate Base Points (b)
                double xb = BASE_RADIUS * Math.Cos(ToRadians(BASE_ANGLES[i]));
                double yb = BASE_RADIUS * Math.Sin(ToRadians(BASE_ANGLES[i]));
                b[i] = new Vector3D(xb, yb, 0);
                BasePoints[i] = new Point3D(xb, yb, 0); // Store for drawing

                // Calculate Platform Points (p)
                double px = PLATFORM_RADIUS * Math.Cos(ToRadians(PLATFORM_ANGLES[i]));
                double py = PLATFORM_RADIUS * Math.Sin(ToRadians(PLATFORM_ANGLES[i]));
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
            Vector3D h0 = new Vector3D(0, 0, INITIAL_HEIGHT);

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
                double L = l.LengthSquared - (ROD_LENGTH * ROD_LENGTH) + (HORN_LENGTH * HORN_LENGTH);
                double M = 2 * HORN_LENGTH * (q.Z - b[i].Z);
                double N = 2 * HORN_LENGTH * (Math.Cos(BETA[i]) * (q.X - b[i].X) + Math.Sin(BETA[i]) * (q.Y - b[i].Y));

                double val = L / Math.Sqrt(M * M + N * N);
                if (val < -1) val = -1;
                if (val > 1) val = 1;

                Alpha[i] = Math.Asin(val) - Math.Atan2(N, M);

                // 4. Calculate 'a' point (Horn End) for Drawing [Matches Processing source: 79]
                double ax = HORN_LENGTH * Math.Cos(Alpha[i]) * Math.Cos(BETA[i]) + b[i].X;
                double ay = HORN_LENGTH * Math.Cos(Alpha[i]) * Math.Sin(BETA[i]) + b[i].Y;
                double az = HORN_LENGTH * Math.Sin(Alpha[i]) + b[i].Z;

                HornEndPoints[i] = new Point3D(ax, ay, az);
            }
        }

        private double ToRadians(double degrees) { return degrees * (Math.PI / 180.0); }
        public double GetAlphaDegree(int index) { return Alpha[index] * (180.0 / Math.PI); }
    }
}