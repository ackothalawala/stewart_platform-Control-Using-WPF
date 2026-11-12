using System;
using System.Windows.Media.Media3D;
using stewart_platform.Kinematics;

namespace stewart_platform
{
    // Public API kept intact for MainWindow and StewartVisualizer.
    public class StewartPlatform
    {
        private readonly RobotConfig config;
        private readonly PlatformGeometry geometry;
        private readonly PlatformPoseTransformer poseTransformer;
        private readonly ServoKinematics servoKinematics;

        public Point3D[] BasePoints { get; private set; } = new Point3D[6];
        public Point3D[] PlatformPoints { get; private set; } = new Point3D[6];
        public Point3D[] HornEndPoints { get; private set; } = new Point3D[6];
        public double[] Alpha { get; private set; } = new double[6];

        private Vector3D Translation;
        private Vector3D Rotation;

        public StewartPlatform(RobotConfig config)
        {
            this.config = config ?? throw new ArgumentNullException(nameof(config));
            geometry = new PlatformGeometry(config);
            poseTransformer = new PlatformPoseTransformer(config);
            servoKinematics = new ServoKinematics(config);

            // Preserve the original initialization behavior:
            // only the base points are initialized here.
            geometry.Initialize(BasePoints);
        }

        // All rotation inputs remain in radians, as before.
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
            for (int i = 0; i < 6; i++)
            {
                Vector3D q = poseTransformer.Transform(
                    geometry.LocalPlatformPoints[i], Translation, Rotation);
                PlatformPoints[i] = new Point3D(q.X, q.Y, q.Z);

                ServoResult result = servoKinematics.Calculate(
                    q, geometry.LocalBasePoints[i], config.BetaAngles[i]);
                Alpha[i] = result.AngleRadians;
                HornEndPoints[i] = result.HornEndPoint;
            }
        }

        public double GetAlphaDegree(int i) => Alpha[i] * 180.0 / Math.PI;
    }
}
