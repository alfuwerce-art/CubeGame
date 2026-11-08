using System;
using System.Globalization;

namespace CubeGame.M0Pilots.World
{
    /// <summary>
    /// Microsoft CRT rand(): state = state * 214013 + 2531011; result = (state &gt;&gt; 16) &amp; 0x7FFF.
    /// Proven for Alpha in docs/research/alpha2013/BEHAVIOR_SPECS/B_world.md §B7.
    /// This is a clean-room reimplementation of the documented LCG, not decompiled code.
    /// </summary>
    public sealed class AlphaRandom
    {
        private uint _state;

        public AlphaRandom(int seed)
        {
            Seed(seed);
        }

        public void Seed(int seed)
        {
            _state = unchecked((uint)seed);
        }

        public int Next()
        {
            _state = unchecked(_state * 214013u + 2531011u);
            return (int)((_state >> 16) & 0x7FFFu);
        }
    }

    /// <summary>
    /// Region descriptor produced by the documented Alpha region constructor.
    /// Evidence: M0_00B_P0_MECHANICS_EVIDENCE §C.3, B_world.md §B7.
    /// The meaning of Region+0xc (biome versus distance band) remains PARTIAL:
    /// the numeric type is exact, the name mapping is only known for listed keys.
    /// </summary>
    public sealed class RegionDescriptor
    {
        public int X;
        public int Z;
        public int Seed;
        public int Type;
        public int Variant;
        public int Orientation;
        public string Name;
        public bool NameKnown;
    }

    public static class AlphaRegion
    {
        public const int Grid = 1024;
        public const int RegionBlocks = 64;
        public const int WorldBlocks = 65536;
        public const int CellsPerRegion = 8;
        public const int ChunkBlocks = 16; // pilot-only CubeGame contract, not an Alpha constant

        /// <summary>FUN_005a4710: distance-band type. Exact formula from P0 evidence §C.3.</summary>
        public static int TypeAt(int x, int z)
        {
            if (x == 512 && z == 512)
            {
                return 1;
            }

            double dx = 512.0 - x;
            double dz = 512.0 - z;
            return 2 + (int)(0.75 * Math.Sqrt(dx * dx + dz * dz));
        }

        /// <summary>
        /// srand(masterSeed + z*0x400 + x); type = FUN_005a4710; variant only when type &gt; 4;
        /// orientation always consumes one rand() call.
        /// </summary>
        public static RegionDescriptor Generate(int masterSeed, int x, int z)
        {
            if (x < 0 || x >= Grid || z < 0 || z >= Grid)
            {
                throw new ArgumentOutOfRangeException(nameof(x), "Region coordinates must be in 0..1023.");
            }

            int seed = unchecked(masterSeed + z * 0x400 + x);
            var random = new AlphaRandom(seed);
            int type = TypeAt(x, z);
            int variant = 0;
            if (type > 4)
            {
                variant = random.Next() % 5;
            }

            int orientation = random.Next() & 3;

            string name;
            bool nameKnown = TryName(type, variant, out name);

            return new RegionDescriptor
            {
                X = x,
                Z = z,
                Seed = seed,
                Type = type,
                Variant = variant,
                Orientation = orientation,
                Name = name,
                NameKnown = nameKnown,
            };
        }

        /// <summary>World block (0..65535) to region and local block (0..63).</summary>
        public static void WorldToRegion(int worldX, int worldZ, out int regionX, out int regionZ, out int localX, out int localZ)
        {
            if (worldX < 0 || worldX >= WorldBlocks || worldZ < 0 || worldZ >= WorldBlocks)
            {
                throw new ArgumentOutOfRangeException(nameof(worldX), "World coordinates must be in 0..65535.");
            }

            regionX = worldX / RegionBlocks;
            regionZ = worldZ / RegionBlocks;
            localX = worldX % RegionBlocks;
            localZ = worldZ % RegionBlocks;
        }

        /// <summary>Local block inside a region (0..63) to 8x8 quest cell (0..7) and cell-local block.</summary>
        public static void LocalToCell(int localX, int localZ, out int cellX, out int cellZ, out int blockX, out int blockZ)
        {
            if (localX < 0 || localX >= RegionBlocks || localZ < 0 || localZ >= RegionBlocks)
            {
                throw new ArgumentOutOfRangeException(nameof(localX), "Local coordinates must be in 0..63.");
            }

            int cellSize = RegionBlocks / CellsPerRegion;
            cellX = localX / cellSize;
            cellZ = localZ / cellSize;
            blockX = localX % cellSize;
            blockZ = localZ % cellSize;
        }

        /// <summary>
        /// Known names from World::ctor_1. Unknown keys return false rather than an invented name.
        /// </summary>
        public static bool TryName(int type, int variant, out string name)
        {
            name = null;
            if (type == 0 && variant == 5)
            {
                name = "Ruins";
                return true;
            }

            switch (type)
            {
                case 1: name = "Village"; return true;
                case 2: name = "Mountain"; return true;
                case 3: name = "Forest"; return true;
                case 4: name = "Lake"; return true;
                case 6: name = "Canyon"; return true;
                case 7: name = "Valley"; return true;
                case 8: name = "Crater"; return true;
                case 11: name = "Rock"; return true;
                case 12: name = "Tree"; return true;
                case 13: name = "Peak"; return true;
                case 15: name = "Island"; return true;
                default: return false;
            }
        }

        public static string Describe(RegionDescriptor region)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "region({0},{1}) seed={2} type={3} variant={4} orientation={5} name={6}",
                region.X, region.Z, region.Seed, region.Type, region.Variant, region.Orientation,
                region.NameKnown ? region.Name : "UNKNOWN");
        }
    }
}
