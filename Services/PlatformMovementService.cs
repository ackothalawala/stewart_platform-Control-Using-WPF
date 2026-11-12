using System;

namespace stewart_platform.Services
{
    public class PlatformMovementService
    {
        // Exact same step rule as the original MoveAxisTowards method.
        public double MoveTowards(double current, double target, double step)
        {
            double diff = target - current;
            return Math.Abs(diff) > step
                ? current + Math.Sign(diff) * step
                : target;
        }
    }
}
