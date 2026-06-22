using System;
using System.Collections.Generic;

namespace WorldOfBlazorCraft.Shared.Engine.Physics
{
    public class Rng
    {
        private uint _s;

        public Rng(uint seed)
        {
            _s = seed;
            if (_s == 0)
            {
                _s = 0x9e3779b9;
            }
        }

        public double Next()
        {
            _s += 0x6d2b79f5;
            uint t = _s;
            t = (t ^ (t >> 15)) * (t | 1);
            t ^= t + (t ^ (t >> 7)) * (t | 61);
            uint finalValue = t ^ (t >> 14);
            return (double)finalValue / 4294967296.0;
        }

        public double Range(double min, double max)
        {
            return min + Next() * (max - min);
        }

        public int Int(int min, int max)
        {
            return (int)Math.Floor(Range(min, max + 1));
        }

        public bool Chance(double p)
        {
            return Next() < p;
        }

        public T Pick<T>(IList<T> arr)
        {
            if (arr == null || arr.Count == 0)
            {
                throw new ArgumentException("Array cannot be null or empty.");
            }
            return arr[(int)Math.Floor(Next() * arr.Count)];
        }
    }

    public static class Noise
    {
        public static double Hash2(int x, int y, uint seed)
        {
            uint h = seed;
            h = (h ^ ((uint)x * 374761393)) * 668265263;
            h = (h ^ ((uint)y * 1274126177)) * 461845907;
            h ^= h >> 13;
            h *= 1274126177;
            h ^= h >> 16;
            return (double)h / 4294967296.0;
        }

        private static double Smooth(double t)
        {
            return t * t * (3 - 2 * t);
        }

        public static double Noise2(double x, double y, uint seed)
        {
            int xi = (int)Math.Floor(x);
            int yi = (int)Math.Floor(y);
            double xf = x - xi;
            double yf = y - yi;

            double a = Hash2(xi, yi, seed);
            double b = Hash2(xi + 1, yi, seed);
            double c = Hash2(xi, yi + 1, seed);
            double d = Hash2(xi + 1, yi + 1, seed);

            double u = Smooth(xf);
            double v = Smooth(yf);

            return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v;
        }

        public static double Fbm2(double x, double y, uint seed, int octaves = 4)
        {
            double sum = 0;
            double amp = 0.5;
            double freq = 1;
            double total = 0;

            for (int i = 0; i < octaves; i++)
            {
                sum += Noise2(x * freq, y * freq, (uint)(seed + i * 1013)) * amp;
                total += amp;
                amp *= 0.5;
                freq *= 2;
            }

            return sum / total;
        }
    }
}
