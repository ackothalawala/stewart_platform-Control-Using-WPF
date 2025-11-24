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
            Math.PI / 6,
            -5 * Math.PI / 6,
            -Math.PI / 2,
            Math.PI / 2,
            5 * Math.PI / 6,
            -Math.PI / 6
        };

        // Dimensions (mm)
        private const double BASE_RADIUS = 76;
        private const double PLATFORM_RADIUS = 60;
        private const double HORN_LENGTH = 40;
        private const double ROD_LENGTH = 130;
        private const double INITIAL_HEIGHT = 120.28183632;

        // Vectors
        private Vector3D[] b = new Vector3D[6]; // Base joints
        private Vector3D[] p = new Vector3D[6]; // Platform joints

        // Public property to access the angles
        public double[] Alpha { get; private set; } = new double[6];

        // Current State
        private Vector3D Translation;
        private Vector3D Rotation;

        // Constructor
        public StewartPlatform()
        {
            // Initialize Geometry (Setup b and p vectors)
            for (int i = 0; i < 6; i++)
            {
                // Calculate Base Points (b)
                double xb = BASE_RADIUS * Math.Cos(ToRadians(BASE_ANGLES[i]));
                double yb = BASE_RADIUS * Math.Sin(ToRadians(BASE_ANGLES[i]));
                b[i] = new Vector3D(xb, yb, 0);

                // Calculate Platform Points (p)
                double px = PLATFORM_RADIUS * Math.Cos(ToRadians(PLATFORM_ANGLES[i]));
                double py = PLATFORM_RADIUS * Math.Sin(ToRadians(PLATFORM_ANGLES[i]));
                p[i] = new Vector3D(px, py, 0);
            }
        }

        // Main Control Function
        public void ApplyTranslationAndRotation(double x, double y, double z, double rotX, double rotY, double rotZ)
        {
            Translation = new Vector3D(x, y, z);
            Rotation = new Vector3D(rotX, rotY, rotZ);
            CalculateAngles();
        }

        private void CalculateAngles()
        {
            // Initial height vector
            Vector3D h0 = new Vector3D(0, 0, INITIAL_HEIGHT);

            for (int i = 0; i < 6; i++)
            {
                // 1. Calculate q[i] - The position of the platform joint in 3D space
                // We apply Rotation Matrix manually here to match your Processing code logic
                double cx = Math.Cos(Rotation.X);
                double sx = Math.Sin(Rotation.X);
                double cy = Math.Cos(Rotation.Y);
                double sy = Math.Sin(Rotation.Y);
                double cz = Math.Cos(Rotation.Z);
                double sz = Math.Sin(Rotation.Z);

                // Matrix multiplication for Rotation (Roll-Pitch-Yaw)
                double qx = (cz * cy) * p[i].X +
                            (-sz * cx + cz * sy * sx) * p[i].Y +
                            (sz * sx + cz * sy * cx) * p[i].Z;

                double qy = (sz * cy) * p[i].X +
                            (cz * cx + sz * sy * sx) * p[i].Y +
                            (-cz * sx + sz * sy * cx) * p[i].Z;

                double qz = (-sy) * p[i].X +
                            (cy * sx) * p[i].Y +
                            (cy * cx) * p[i].Z;

                Vector3D q = new Vector3D(qx, qy, qz);

                // Apply Translation + Initial Height
                q = q + Translation + h0;

                // 2. Calculate l vector (Leg vector from Base to Platform)
                Vector3D l = q - b[i];

                // 3. Inverse Kinematics (Finding Alpha)
                double L = l.LengthSquared - (ROD_LENGTH * ROD_LENGTH) + (HORN_LENGTH * HORN_LENGTH);
                double M = 2 * HORN_LENGTH * (q.Z - b[i].Z);
                double N = 2 * HORN_LENGTH * (Math.Cos(BETA[i]) * (q.X - b[i].X) + Math.Sin(BETA[i]) * (q.Y - b[i].Y));

                // Solve for Alpha
                double val = L / Math.Sqrt(M * M + N * N);

                // Safety check for math errors (NaN)
                if (val < -1) val = -1;
                if (val > 1) val = 1;

                Alpha[i] = Math.Asin(val) - Math.Atan2(N, M);
            }
        }

        // Helper to convert Degrees to Radians
        private double ToRadians(double degrees)
        {
            return degrees * (Math.PI / 180.0);
        }

        // Helper to get Degrees for UI display
        public double GetAlphaDegree(int index)
        {
            return Alpha[index] * (180.0 / Math.PI);
        }
    }
}