using System;
using CubeGame.Features.Contracts;

namespace CubeGame.Features.World
{
    /// <summary>World geometry of Alpha (B_world §B1/B2, C_streaming §C1 — DIRECT).</summary>
    public static class WorldLayout
    {
        /// <summary>Region / zone edge in blocks.</summary>
        public const int RegionSize = 64;

        /// <summary>Regions per axis.</summary>
        public const int RegionGrid = 1024;

        /// <summary>World edge in blocks (0…65535).</summary>
        public const int WorldBlocks = RegionSize * RegionGrid;

        /// <summary>Coarse terrain units per block (Alpha 0x100).</summary>
        public const int CoarsePerBlock = 256;

        /// <summary>Coarse units per region (0x4000).</summary>
        public const int CoarsePerRegion = RegionSize * CoarsePerBlock;

        /// <summary>The centre region (0x200, 0x200); Alpha's level function returns 1 only here.</summary>
        public const int CenterRegion = 512;

        /// <summary>
        /// Start position: centre of the centre region, block 32800. Matches the saved Alpha hero position
        /// 32 800.5 (B_world §B1, OQ-22 PARTIAL); Alpha's own spawn field (world+0x8000F0) was not executed.
        /// </summary>
        public const int StartBlock = CenterRegion * RegionSize + RegionSize / 2;

        public static RegionKey RegionOf(int blockX, int blockZ)
        {
            return new RegionKey(FloorDiv(blockX, RegionSize), FloorDiv(blockZ, RegionSize));
        }

        public static bool IsInside(RegionKey region)
        {
            return region.X >= 0 && region.Z >= 0 && region.X < RegionGrid && region.Z < RegionGrid;
        }

        public static bool IsInsideBlocks(int blockX, int blockZ)
        {
            return blockX >= 0 && blockZ >= 0 && blockX < WorldBlocks && blockZ < WorldBlocks;
        }

        public static int FloorDiv(int value, int divisor)
        {
            int q = value / divisor;
            if (value % divisor != 0 && (value < 0) != (divisor < 0))
            {
                q--;
            }

            return q;
        }

        public static int Chebyshev(RegionKey a, RegionKey b)
        {
            return Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Z - b.Z));
        }
    }
}
