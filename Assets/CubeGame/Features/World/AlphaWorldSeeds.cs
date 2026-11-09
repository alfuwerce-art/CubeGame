using System;

namespace CubeGame.Features.World
{
    /// <summary>
    /// Per-world seed table derived from the master seed exactly like Alpha (crt_stl.cpp 11431-11459, DIRECT):
    /// <c>srand(master)</c>; world seed (+0x800188) = rand()%100000; 4 noise offsets (+0x800168); 20 table values
    /// (+0x80018C…+0x8001D8); 2 values (+0x8001DC, +0x8001E0); 2 values (+0x8001E4…). rand() ≤ 0x7FFF, so the
    /// modulo never changes the value but is kept for fidelity.
    /// </summary>
    public sealed class AlphaWorldSeeds
    {
        public AlphaWorldSeeds(uint masterSeed)
        {
            MasterSeed = masterSeed;
            var rng = new AlphaCrtRandom(unchecked((int)masterSeed));
            WorldSeed = rng.Next() % 100000;
            NoiseOffsets = Fill(rng, 4);
            Tables = Fill(rng, 20);
            MountainOffsets = Fill(rng, 2);
            ExtraOffsets = Fill(rng, 2);
        }

        public uint MasterSeed { get; }

        /// <summary>Alpha <c>world+0x800188</c>: the seed every region/zone/climate stream is derived from.</summary>
        public int WorldSeed { get; }

        /// <summary>+0x800168…+0x800174.</summary>
        public int[] NoiseOffsets { get; }

        /// <summary>+0x80018C…+0x8001D8 (index = (offset − 0x80018C) / 4).</summary>
        public int[] Tables { get; }

        /// <summary>+0x8001DC, +0x8001E0 (used by the mountain-mask function 0x5EFFA0).</summary>
        public int[] MountainOffsets { get; }

        /// <summary>+0x8001E4, +0x8001E8.</summary>
        public int[] ExtraOffsets { get; }

        /// <summary>Reads a table value by its Alpha struct offset (0x80018C…0x8001D8).</summary>
        public int TableAt(int alphaOffset)
        {
            int index = (alphaOffset - 0x80018C) / 4;
            if (index < 0 || index >= Tables.Length || (alphaOffset - 0x80018C) % 4 != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(alphaOffset));
            }

            return Tables[index];
        }

        private static int[] Fill(AlphaCrtRandom rng, int count)
        {
            var values = new int[count];
            for (int i = 0; i < count; i++)
            {
                values[i] = rng.Next() % 100000;
            }

            return values;
        }
    }
}
