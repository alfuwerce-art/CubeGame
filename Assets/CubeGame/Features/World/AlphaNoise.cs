using System;

namespace CubeGame.Features.World
{
    /// <summary>
    /// Port of Alpha <c>perlinNoise2D_cosInterp</c> (Cube.exe 0x004C0EF0, B_world §B3, DIRECT): value noise on a
    /// hashed integer lattice (row multiplier 0x39, <c>h ^= h·0x2000</c>, polynomial 0xEC4D / 0x131071F / 0xD208DD0D),
    /// cosine interpolation with π as float 3.1415927, result rounded to float. Lattice coordinates use C
    /// truncation toward zero, exactly as the shipped code (<c>(int)x</c>), not floor.
    /// </summary>
    public static class AlphaNoise
    {
        private const double Pi = 3.1415927410125732; // (double)(float)3.1415927
        private const double Scale = 9.313225746154785e-10; // 2^-30

        public static float Sample(double x, double y)
        {
            int ix = (int)x;
            int iy = (int)y;
            double fx = ix;
            double fy = iy;

            unchecked
            {
                int row0 = iy * 0x39;
                int row1 = (int)(fy + 1.0) * 0x39;
                int x1 = (int)(fx + 1.0);

                uint g00 = Mix((uint)(ix + row0));
                uint g10 = Mix((uint)(row0 + x1));
                uint g01 = Mix((uint)(ix + row1));
                uint g11 = Mix((uint)(row1 + x1));

                double wx = (1.0 - Math.Cos((x - fx) * Pi)) * 0.5;
                double wy = (1.0 - Math.Cos((y - fy) * Pi)) * 0.5;

                double top = Node(g00) * (1.0 - wx) + Node(g10) * wx;
                double bottom = Node(g01) * (1.0 - wx) + Node(g11) * wx;
                return (float)(top * (1.0 - wy) + bottom * wy);
            }
        }

        private static uint Mix(uint h)
        {
            unchecked
            {
                return h ^ (h * 0x2000u);
            }
        }

        private static double Node(uint h)
        {
            unchecked
            {
                uint v = ((h * h * 0xEC4Du + 0x131071Fu) * h + 0xD208DD0Du) & 0x7FFFFFFFu;
                return 1.0 - v * Scale;
            }
        }
    }
}
