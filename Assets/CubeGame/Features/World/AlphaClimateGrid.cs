using System.Collections.Concurrent;
using CubeGame.Features.Contracts;

namespace CubeGame.Features.World
{
    /// <summary>
    /// One 0x1C-byte "climate cell" per region (Alpha <c>FUN_005D7A70</c>, grid at world+0x4000BC).
    /// Semantics of <see cref="Field3"/>/<see cref="Field4"/> are PARTIAL (B_world: "vegetation density 0.3…0.7",
    /// "rockiness 0.4…1.0"; generateRegion reads Field4 &gt; 0.81 as "watery" for the orientation rule).
    /// </summary>
    public sealed class AlphaClimateCell
    {
        /// <summary>Anchor X in voxels, snapped to the centre of an 8-zone column (0x800).</summary>
        public int AnchorX { get; internal set; }

        /// <summary>Anchor Z in voxels.</summary>
        public int AnchorZ { get; internal set; }

        /// <summary>Byte +8: special flag (set for 1 in 10 "rare" cells, cleared for water cells).</summary>
        public bool Flag { get; internal set; }

        public float Field3 { get; internal set; }

        public float Field4 { get; internal set; }

        /// <summary>+0x14: cell id = rz·0x400 + rx + worldSeed.</summary>
        public int Id { get; internal set; }

        /// <summary>+0x18: base terrain height in voxels (land 1…≈160; water cells −100; start region 20…69 if water).</summary>
        public int BaseHeight { get; internal set; }

        public bool IsWater => BaseHeight < 1;
    }

    /// <summary>
    /// Lazily generated climate cells, one per region, ported from <c>FUN_005D7A70</c> and verified against the
    /// executed bytes (EMULATED_ALPHA_CODE fixture, FullM0AlphaOracleTests): own CRT stream
    /// <c>srand(rx + 0x108A + rz·0x400 + seed·3)</c>, the same rand() call order, the same float formulas.
    /// Thread-safe (chunk meshes are generated on worker threads).
    /// </summary>
    public sealed class AlphaClimateGrid
    {
        private readonly ConcurrentDictionary<long, AlphaClimateCell> _cells =
            new ConcurrentDictionary<long, AlphaClimateCell>();
        private readonly AlphaWorldSeeds _seeds;
        private readonly int _spawnX;
        private readonly int _spawnZ;

        /// <param name="spawnX">Alpha world+0x8000F0: spawn point in voxels.</param>
        /// <param name="spawnZ">Alpha world+0x8000F4.</param>
        public AlphaClimateGrid(AlphaWorldSeeds seeds, int spawnX, int spawnZ)
        {
            _seeds = seeds;
            _spawnX = spawnX;
            _spawnZ = spawnZ;
        }

        public int CachedCount => _cells.Count;

        /// <summary>Returns the cell of region (rx, rz), or null outside the 1024×1024 grid.</summary>
        public AlphaClimateCell Get(int rx, int rz)
        {
            if (rx < 0 || rz < 0 || rx >= WorldLayout.RegionGrid || rz >= WorldLayout.RegionGrid)
            {
                return null;
            }

            long key = ((long)rx << 20) | (uint)rz;
            // Climate cells are immutable after generation. Parallel terrain rows share the same inputs
            // without taking a global lock for every noise sample.
            return _cells.TryGetValue(key, out AlphaClimateCell cell)
                ? cell
                : _cells.GetOrAdd(key, Generate(rx, rz));
        }

        internal void EvictOutside(RegionKey center)
        {
            foreach (long key in _cells.Keys)
            {
                if (System.Math.Abs((int)(key >> 20) - center.X) >= 5 ||
                    System.Math.Abs((int)(key & 0xFFFFF) - center.Z) >= 5)
                {
                    _cells.TryRemove(key, out _);
                }
            }
        }

        private AlphaClimateCell Generate(int rx, int rz)
        {
            int seed = _seeds.WorldSeed;
            var rng = new AlphaCrtRandom(unchecked(rx + 0x108A + rz * 0x400 + seed * 3));
            var cell = new AlphaClimateCell { Id = unchecked(rz * 0x400 + rx + seed), Flag = false };

            // Alpha: the spawn region is ((int)spawn >> 8) >> 6 on both axes.
            bool spawn = rx == (_spawnX >> 8) >> 6 && rz == (_spawnZ >> 8) >> 6;

            int anchorX;
            int anchorZ;
            bool evenFirst = (rng.Next() & 1) == 0;
            if (evenFirst || spawn)
            {
                cell.Field3 = Unit(rng.Next(), 0.4f, 0.3f);
                cell.Field4 = Unit(rng.Next(), 0.4f, 0.4f);
                if (spawn)
                {
                    return Finish(cell, rng, rx, rz, _spawnX, _spawnZ, true);
                }

                if (rng.Next() % 10 == 0)
                {
                    cell.Flag = true;
                    cell.Field4 = Unit(rng.Next(), 0.2f, 0.8f);
                    cell.Field3 = Unit(rng.Next(), 0.5f, 0f);
                }
            }
            else
            {
                cell.Field3 = (rng.Next() & 1) == 0 ? Unit(rng.Next(), 0.1f, 0f) : Unit(rng.Next(), 0.1f, 0.9f);
                cell.Field4 = (rng.Next() & 1) == 0 ? Unit(rng.Next(), 0.1f, 0f) : Unit(rng.Next(), 0.1f, 0.9f);
            }

            int r1 = rng.Next();
            int r2 = rng.Next();
            anchorZ = rz * WorldLayout.RegionSize + r1 % 0x3C00 + 0x200;
            anchorX = rx * WorldLayout.RegionSize + 0x200 + r2 % 0x3C00;
            return Finish(cell, rng, rx, rz, anchorX, anchorZ, false);
        }

        private AlphaClimateCell Finish(AlphaClimateCell cell, AlphaCrtRandom rng, int rx, int rz, int anchorX, int anchorZ, bool spawn)
        {
            cell.AnchorX = Snap(anchorX);
            cell.AnchorZ = Snap(anchorZ);

            double seed = _seeds.WorldSeed;
            float n1 = AlphaNoise.Sample(rx * 1.4 + seed, rz * 1.4 + seed + 843.0);
            float n2 = AlphaNoise.Sample(rx * 4.0 + seed, rz * 4.0 + seed + 843.0);
            int baseHeight = (int)(((n1 + 1.0f) * 100.0f - 70.0f) + n2 * 30.0f);
            cell.BaseHeight = baseHeight;
            if (baseHeight >= 1)
            {
                return cell;
            }

            cell.Flag = false;
            if (spawn)
            {
                cell.BaseHeight = rng.Next() % 0x32 + 0x14;
                return cell;
            }

            baseHeight -= 100;
            cell.BaseHeight = baseHeight < -100 ? -100 : baseHeight;
            if ((rng.Next() & 1) == 0)
            {
                cell.Field3 = Unit(rng.Next(), 0.1f, 0.9f);
                cell.Field4 = Unit(rng.Next(), 0.1f, 0.9f);
            }
            else
            {
                cell.Field3 = Unit(rng.Next(), 0.4f, 0.3f);
                cell.Field4 = Unit(rng.Next(), 0.4f, 0.4f);
            }

            return cell;
        }

        private static int Snap(int voxels)
        {
            return (voxels & unchecked((int)0xFFFFF800)) + 0x400;
        }

        private static float Unit(int r, float span, float offset)
        {
            return ((float)r * span) / 32767.0f + offset;
        }
    }
}
