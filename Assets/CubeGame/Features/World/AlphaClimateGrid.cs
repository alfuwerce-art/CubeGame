using System.Collections.Generic;
using CubeGame.Features.Contracts;

namespace CubeGame.Features.World
{
    /// <summary>
    /// One 0x1C-byte "climate cell" per region (Alpha <c>FUN_005d7a70</c>, grid at world+0x4000BC).
    /// Field names describe position in the record; semantics of <see cref="Field3"/>/<see cref="Field4"/> are
    /// PARTIAL (B_world: "vegetation density 0.3…0.7", "rockiness 0.4…1.0"; generateRegion reads Field4 &gt; 0.81 as
    /// "watery" for the orientation rule).
    /// </summary>
    public sealed class AlphaClimateCell
    {
        /// <summary>Anchor X in coarse units (256 per block), snapped to an 8-block column centre.</summary>
        public int AnchorX { get; internal set; }

        /// <summary>Anchor Z in coarse units.</summary>
        public int AnchorZ { get; internal set; }

        /// <summary>Byte +8: special flag (set for 1 in 10 "rare" cells, cleared for water cells).</summary>
        public bool Flag { get; internal set; }

        public float Field3 { get; internal set; }

        public float Field4 { get; internal set; }

        /// <summary>+0x14: cell id = rz·0x400 + rx + worldSeed.</summary>
        public int Id { get; internal set; }

        /// <summary>+0x18: base terrain height in blocks (land 1…≈160; water cells −100; start region 20…69).</summary>
        public int BaseHeight { get; internal set; }

        public bool IsWater => BaseHeight < 1;
    }

    /// <summary>
    /// Lazily generated climate cells, one per region, reproduced from the decompiled body of
    /// <c>FUN_005d7a70</c> (RECONSTRUCTED_FROM_DECOMPILATION — the bytes were not executed): own CRT stream
    /// <c>srand(rx + 0x108A + rz·0x400 + seed·3)</c>, the same rand() call order, the same float formulas.
    /// The start region uses the "spawn" branch (Alpha compares with world+0x8000F0, here the start region).
    /// </summary>
    public sealed class AlphaClimateGrid
    {
        private readonly Dictionary<long, AlphaClimateCell> _cells = new Dictionary<long, AlphaClimateCell>();
        private readonly AlphaWorldSeeds _seeds;
        private readonly RegionKey _spawnRegion;

        public AlphaClimateGrid(AlphaWorldSeeds seeds, RegionKey spawnRegion)
        {
            _seeds = seeds;
            _spawnRegion = spawnRegion;
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
            if (_cells.TryGetValue(key, out AlphaClimateCell cell))
            {
                return cell;
            }

            cell = Generate(rx, rz);
            _cells.Add(key, cell);
            return cell;
        }

        private AlphaClimateCell Generate(int rx, int rz)
        {
            int seed = _seeds.WorldSeed;
            var rng = new AlphaCrtRandom(unchecked(rx + 0x108A + rz * 0x400 + seed * 3));
            var cell = new AlphaClimateCell { Id = unchecked(rz * 0x400 + rx + seed), Flag = false };
            bool spawn = rx == _spawnRegion.X && rz == _spawnRegion.Z;

            int anchorX;
            int anchorZ;
            bool evenFirst = (rng.Next() & 1) == 0;
            if (evenFirst || spawn)
            {
                cell.Field3 = Unit(rng.Next(), 0.4f, 0.3f);
                cell.Field4 = Unit(rng.Next(), 0.4f, 0.4f);
                if (spawn)
                {
                    anchorX = (WorldLayout.StartBlock * WorldLayout.CoarsePerBlock) + 128;
                    anchorZ = (WorldLayout.StartBlock * WorldLayout.CoarsePerBlock) + 128;
                    return Finish(cell, rng, rx, rz, anchorX, anchorZ, true);
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
            anchorZ = rz * WorldLayout.CoarsePerRegion + r1 % 0x3C00 + 0x200;
            anchorX = rx * WorldLayout.CoarsePerRegion + 0x200 + r2 % 0x3C00;
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

        private static int Snap(int coarse)
        {
            return (coarse & unchecked((int)0xFFFFF800)) + 0x400;
        }

        private static float Unit(int r, float span, float offset)
        {
            return ((float)r * span) / 32767.0f + offset;
        }
    }
}
