using System;
using System.Windows.Media.Media3D;

namespace stewart_platform.Kinematics
{
    // Solves one servo horn angle and its endpoint for a platform attachment point.
    internal sealed class ServoKinematics
    {
        private readonly double hornLength;
        private readonly double rodLength;

        internal ServoKinematics(RobotConfig config)
        {
            hornLength = config.HornLength;
            rodLength = config.RodLength;
        }

        internal ServoResult Calculate(Vector3D q, Vector3D b, double beta)
        {
            Vector3D l = q - b;

            double L = l.LengthSquared - (rodLength * rodLength) + (hornLength * hornLength);
            double M = 2 * hornLength * (q.Z - b.Z);
            double N = 2 * hornLength *
                (Math.Cos(beta) * (q.X - b.X) +
                 Math.Sin(beta) * (q.Y - b.Y));

            double val = L / Math.Sqrt(M * M + N * N);
            val = Math.Clamp(val, -1.0, 1.0);

            double alpha = Math.Asin(val) - Math.Atan2(N, M);

            double ax = hornLength * Math.Cos(alpha) * Math.Cos(beta) + b.X;
            double ay = hornLength * Math.Cos(alpha) * Math.Sin(beta) + b.Y;
            double az = hornLength * Math.Sin(alpha) + b.Z;

            return new ServoResult(alpha, new Point3D(ax, ay, az));
        }
    }

    internal readonly struct ServoResult
    {
        internal double AngleRadians { get; }
        internal Point3D HornEndPoint { get; }

        internal ServoResult(double angleRadians, Point3D hornEndPoint)
        {
            AngleRadians = angleRadians;
            HornEndPoint = hornEndPoint;
        }
    }
}
