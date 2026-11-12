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
            // Avalonia bakes home yaw into body-frame geometry before pose rotation.
            double offsetCos = Math.Cos(homeYawOffsetRad);
            double offsetSin = Math.Sin(homeYawOffsetRad);
            double px = offsetCos * p.X - offsetSin * p.Y;
            double py = offsetSin * p.X + offsetCos * p.Y;
            double pz = p.Z;

            double cx = Math.Cos(rotation.X); double sx = Math.Sin(rotation.X);
            double cy = Math.Cos(rotation.Y); double sy = Math.Sin(rotation.Y);
            double cz = Math.Cos(rotation.Z); double sz = Math.Sin(rotation.Z);

            // Same rotation matrix and operation order as the original StewartMath.cs.
            double qx = (cz * cy) * px
                      + (-sz * cx + cz * sy * sx) * py
                      + (sz * sx + cz * sy * cx) * pz;

            double qy = (sz * cy) * px
                      + (cz * cx + sz * sy * sx) * py
                      + (-cz * sx + sz * sy * cx) * pz;

            double qz = (-sy) * px
                      + (cy * sx) * py
                      + (cy * cx) * pz;

            return new Vector3D(qx, qy, qz) + translation + h0;
        }
    }
}
