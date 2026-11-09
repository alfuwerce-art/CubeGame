using System;

namespace CubeGame.Features.World
{
    /// <summary>
    /// Per-world seed table derived from the master seed exactly like Alpha (crt_stl.cpp 11431-11560, DIRECT):
    /// <c>srand(master)</c>; then <c>rand()%100000</c> into, in this order: +0x800188 (world seed), +0x800168 (4),
    /// +0x80018C…+0x800288 (64 contiguous values written by loops of 20,2,2,2,2,8,6,6,10,4,2), +0x800178 (4),
    /// +0x80028C (2), +0x800294 (2). Values are addressed by their Alpha struct offset.
    /// </summary>
    public sealed class AlphaWorldSeeds
    {
        private const int First = 0x800168;
        private const int Last = 0x800298;

        private readonly int[] _fields = new int[(Last - First) / 4 + 1];

        public AlphaWorldSeeds(uint masterSeed)
        {
            MasterSeed = masterSeed;
            var rng = new AlphaCrtRandom(unchecked((int)masterSeed));
            WorldSeed = rng.Next() % 100000;
            Fill(rng, 0x800168, 4);
            Fill(rng, 0x80018C, 64);
            Fill(rng, 0x800178, 4);
            Fill(rng, 0x80028C, 4);
        }

        public uint MasterSeed { get; }

        /// <summary>Alpha <c>world+0x800188</c>: the seed every region/zone/climate stream is derived from.</summary>
        public int WorldSeed { get; }

        /// <summary>Value at Alpha struct offset (0x800168…0x800298, 4-byte aligned).</summary>
        public int At(int alphaOffset)
        {
            int delta = alphaOffset - First;
            if (delta < 0 || delta % 4 != 0 || delta / 4 >= _fields.Length || alphaOffset == 0x800188)
            {
                if (alphaOffset == 0x800188)
                {
                    return WorldSeed;
                }

                throw new ArgumentOutOfRangeException(nameof(alphaOffset));
            }

            return _fields[delta / 4];
        }

        private void Fill(AlphaCrtRandom rng, int offset, int count)
        {
            for (int i = 0; i < count; i++)
            {
                _fields[(offset - First) / 4 + i] = rng.Next() % 100000;
            }
        }
    }
}
