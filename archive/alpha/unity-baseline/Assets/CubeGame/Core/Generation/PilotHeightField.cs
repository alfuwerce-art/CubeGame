using System;

namespace CubeGame.Core.Generation
{
    /// <summary>
    /// Deterministic height field of the pilot world: seed + block coordinates -> height in blocks.
    ///
    /// PROVENANCE: PLACEHOLDER_NOT_ALPHA. The shape (several octaves of lattice noise, module of
    /// the Alpha sampler) is inspired by the reconstructed Alpha height sampler, but neither the
    /// frequencies nor the amplitudes are verified against Alpha 2013. It must be replaced after
    /// the S00-03/S00-04 game tests. See docs/research/alpha2013/GENERATION_MOVEMENT_SPEC.md.
    /// </summary>
    public sealed class PilotHeightField
    {
        private readonly uint _seed;

        public PilotHeightField(uint seed)
        {
            _seed = seed;
        }

        public uint Seed => _seed;

        /// <summary>Terrain height in blocks at integer block coordinates (world space).</summary>
        public int HeightAt(int blockX, int blockZ)
        {
            float noise = 0f;
            float amplitude = 1f;
            float amplitudeSum = 0f;
            int scale = PilotWorldLayout.BaseScale;

            for (int octave = 0; octave < PilotWorldLayout.Octaves; octave++)
            {
                noise += SampleOctave(blockX, blockZ, scale, (uint)octave) * amplitude;
                amplitudeSum += amplitude;
                amplitude *= 0.5f;
                scale /= 2;
                if (scale < 2)
                {
                    scale = 2;
                }
            }

            noise /= amplitudeSum;              // noise in [-1, 1]
            float normalized = 0.5f + 0.5f * noise;
            normalized = Clamp01(normalized);

            int range = PilotWorldLayout.MaxHeight - PilotWorldLayout.MinHeight;
            return PilotWorldLayout.MinHeight + (int)Math.Round(normalized * range, MidpointRounding.AwayFromZero);
        }

        private float SampleOctave(int blockX, int blockZ, int scale, uint octave)
        {
            int latticeX = FloorDiv(blockX, scale);
            int latticeZ = FloorDiv(blockZ, scale);
            float fractionX = (blockX - latticeX * scale) / (float)scale;
            float fractionZ = (blockZ - latticeZ * scale) / (float)scale;

            if (PilotWorldLayout.SmoothInterpolation)
            {
                fractionX = SmoothStep(fractionX);
                fractionZ = SmoothStep(fractionZ);
            }

            uint salt = octave * 0x9E3779B1u;
            float a = ToUnit(DeterministicHash.Lattice(_seed ^ salt, latticeX, latticeZ));
            float b = ToUnit(DeterministicHash.Lattice(_seed ^ salt, latticeX + 1, latticeZ));
            float c = ToUnit(DeterministicHash.Lattice(_seed ^ salt, latticeX, latticeZ + 1));
            float d = ToUnit(DeterministicHash.Lattice(_seed ^ salt, latticeX + 1, latticeZ + 1));

            float top = a + (b - a) * fractionX;
            float bottom = c + (d - c) * fractionX;
            return (top + (bottom - top) * fractionZ) * 2f - 1f; // [-1, 1]
        }

        private static float ToUnit(uint hash)
        {
            return (hash & 0xFFFFFFu) / 16777215f;
        }

        private static float SmoothStep(float t)
        {
            return t * t * (3f - 2f * t);
        }

        private static float Clamp01(float value)
        {
            if (value < 0f)
            {
                return 0f;
            }

            return value > 1f ? 1f : value;
        }

        public static int FloorDiv(int value, int divisor)
        {
            int quotient = value / divisor;
            if ((value % divisor != 0) && ((value < 0) != (divisor < 0)))
            {
                quotient--;
            }

            return quotient;
        }
    }
}
