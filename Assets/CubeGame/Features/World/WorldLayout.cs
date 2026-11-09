using System;
using CubeGame.Features.Contracts;

namespace CubeGame.Features.World
{
    /// <summary>
    /// World geometry of Alpha, in terrain voxels (1 CubeGame block = 1 Alpha terrain voxel = 1 Unity unit).
    ///
    /// Proven by execution (M0-02 report): <c>generateBiomeContent(x, z)</c> is called with zone coordinates
    /// 0…0xFFFF (zone grid index <c>x &gt;&gt; 6 &lt; 0x400</c>) and generates 256×256 voxels per zone; the terrain functions
    /// take voxel coordinates (region = <c>voxel &gt;&gt; 14</c>). Hence: zone = 256 voxels, region = 64 zones = 16 384 voxels,
    /// 1024×1024 regions, world edge 2^24 voxels. Earlier CubeGame docs called a zone a "block" — corrected here.
    /// </summary>
    public static class WorldLayout
    {
        public const int ZoneSize = 256;

        public const int ZonesPerRegion = 64;

        /// <summary>Region edge in voxels (Alpha 0x4000).</summary>
        public const int RegionSize = ZoneSize * ZonesPerRegion;

        public const int RegionGrid = 1024;

        /// <summary>World edge in voxels (2^24).</summary>
        public const int WorldVoxels = RegionSize * RegionGrid;

        /// <summary>Render/stream chunk edge in voxels (CubeGame presentation unit; Alpha streams whole zones).</summary>
        public const int ChunkSize = 32;

        public const int CenterRegion = 512;

        /// <summary>
        /// Start voxel on both axes: centre of zone 32800 (= saved Alpha hero position 32 800.5 zones, OQ-22 PARTIAL),
        /// inside the centre region (512, 512) whose level is 1. Also used as Alpha's spawn point (world+0x8000F0).
        /// </summary>
        public const int StartVoxel = 32800 * ZoneSize + ZoneSize / 2;

        public static RegionKey RegionOf(int x, int z)
        {
            return new RegionKey(x >> 14, z >> 14);
        }

        public static ZoneKey ZoneOf(int x, int z)
        {
            return new ZoneKey(x >> 8, z >> 8);
        }

        public static ChunkKey ChunkOf(int x, int z)
        {
            return new ChunkKey(FloorDiv(x, ChunkSize), FloorDiv(z, ChunkSize));
        }

        public static bool IsInside(RegionKey region)
        {
            return region.X >= 0 && region.Z >= 0 && region.X < RegionGrid && region.Z < RegionGrid;
        }

        public static bool IsInside(ChunkKey chunk)
        {
            const int chunks = WorldVoxels / ChunkSize;
            return chunk.X >= 0 && chunk.Z >= 0 && chunk.X < chunks && chunk.Z < chunks;
        }

        public static bool IsInsideVoxels(int x, int z)
        {
            return x >= 0 && z >= 0 && x < WorldVoxels && z < WorldVoxels;
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

        public static int Chebyshev(ChunkKey a, ChunkKey b)
        {
            return Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Z - b.Z));
        }
    }
}
