using System;
using System.Windows.Media.Media3D;

namespace stewart_platform.Kinematics
{
    // Applies X/Y/Z rotation, translation, initial height and home yaw offset.
    internal sealed class PlatformPoseTransformer
    {
        private readonly double initialHeight;
        private readonly double homeYawOffsetRad;

        internal PlatformPoseTransformer(RobotConfig config)
        {
            initialHeight = config.InitialHeight;
            homeYawOffsetRad = config.HomeYawOffsetDeg * Math.PI / 180.0;
        }

        internal Vector3D Transform(Vector3D p, Vector3D translation, Vector3D rotation)
        {
            Vector3D h0 = new Vector3D(0, 0, initialHeight);
            double rz = rotation.Z + homeYawOffsetRad;

            double cx = Math.Cos(rotation.X); double sx = Math.Sin(rotation.X);
            double cy = Math.Cos(rotation.Y); double sy = Math.Sin(rotation.Y);
            double cz = Math.Cos(rz); double sz = Math.Sin(rz);

            // Same rotation matrix and operation order as the original StewartMath.cs.
            double qx = (cz * cy) * p.X
                      + (-sz * cx + cz * sy * sx) * p.Y
                      + (sz * sx + cz * sy * cx) * p.Z;

            double qy = (sz * cy) * p.X
                      + (cz * cx + sz * sy * sx) * p.Y
                      + (-cz * sx + sz * sy * cx) * p.Z;

            double qz = (-sy) * p.X
                      + (cy * sx) * p.Y
                      + (cy * cx) * p.Z;

            return new Vector3D(qx, qy, qz) + translation + h0;
        }
    }
}
