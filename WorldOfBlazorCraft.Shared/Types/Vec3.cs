using System;

namespace WorldOfBlazorCraft.Shared.Types
{
    public struct Vec3
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }

        public Vec3(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static Vec3 Zero => new Vec3(0.0, 0.0, 0.0);

        public double Distance2D(Vec3 other)
        {
            double dx = X - other.X;
            double dz = Z - other.Z;
            return Math.Sqrt(dx * dx + dz * dz);
        }

        public double AngleTo(Vec3 other)
        {
            return Math.Atan2(other.X - X, other.Z - Z);
        }
    }
}
