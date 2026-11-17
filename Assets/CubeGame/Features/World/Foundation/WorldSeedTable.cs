// CubeGame.Features.World.Foundation — G2-A: named seed axes for CubeGame code.
//
// AlphaWorldSeeds stores the per-world rand() table keyed by Alpha struct offsets (0x800168 … 0x800298).
// That table is correct and verified (FullM0AlphaOracleTests), but "At(0x8001BC)" is not an API for a game that
// has its own generation: nobody can tell what 0x8001BC means six months later, and a new CubeGame system cannot
// ask for "the broad terrain frequency" without reading the ported Alpha code.
//
// This wrapper keeps the verified table untouched and gives the *same* values names that describe their role in
// the generator. Provenance (the offset) stays in the code and in the description, so nothing is invented.
using System;
using System.Collections.Generic;

namespace CubeGame.Features.World.Foundation
{
    /// <summary>Named role of one Alpha seed slot used by the CubeGame world generator.</summary>
    public enum WorldSeedAxis
    {
        /// <summary>world+0x800188 — the seed of every region/zone/climate stream.</summary>
        WorldSeed = 0,

        /// <summary>0x800168/16C — fine terrain-height octave (0.001 voxel⁻¹).</summary>
        HeightFineX,
        HeightFineZ,

        /// <summary>0x800170/174 — broad terrain-height octave (0.01 voxel⁻¹).</summary>
        HeightBroadX,
        HeightBroadZ,

        /// <summary>0x800178/17C — cave body noise.</summary>
        CaveX,
        CaveZ,

        /// <summary>0x8001DC/1E0 — mountain-mask offsets.</summary>
        MountainX,
        MountainZ,

        /// <summary>0x8001BC/1C0 — low-frequency base-height octave.</summary>
        BaseLowX,
        BaseLowZ,

        /// <summary>0x8001B4/1B8 — medium-frequency base-height octave.</summary>
        BaseMidX,
        BaseMidZ,
    }

    /// <summary>One named value of the seed table, with provenance.</summary>
    public readonly struct WorldSeedValue
    {
        public WorldSeedValue(WorldSeedAxis axis, string name, int alphaOffset, int value)
        {
            Axis = axis;
            Name = name;
            AlphaOffset = alphaOffset;
            Value = value;
        }

        public WorldSeedAxis Axis { get; }

        public string Name { get; }

        /// <summary>Alpha struct offset this value is read from (provenance, not an API).</summary>
        public int AlphaOffset { get; }

        public int Value { get; }

        public override string ToString() => $"{Name}=0x{Value:x8} (0x{AlphaOffset:x})";
    }

    /// <summary>
    /// The verified per-world seed table with CubeGame names. Values are identical to
    /// <see cref="AlphaWorldSeeds"/> — this is a naming layer, not a second generator.
    /// </summary>
    public sealed class WorldSeedTable
    {
        // Offsets proven by the ported generator (see AlphaTerrainGenerator/AlphaZoneGenerator call sites).
        private const int OffsetHeightFineX = 0x800168;
        private const int OffsetHeightFineZ = 0x80016C;
        private const int OffsetHeightBroadX = 0x800170;
        private const int OffsetHeightBroadZ = 0x800174;
        private const int OffsetCaveX = 0x800178;
        private const int OffsetCaveZ = 0x80017C;
        private const int OffsetBaseMidX = 0x8001B4;
        private const int OffsetBaseMidZ = 0x8001B8;
        private const int OffsetBaseLowX = 0x8001BC;
        private const int OffsetBaseLowZ = 0x8001C0;
        private const int OffsetMountainX = 0x8001DC;
        private const int OffsetMountainZ = 0x8001E0;

        private static readonly WorldSeedAxis[] Axes =
        {
            WorldSeedAxis.WorldSeed,
            WorldSeedAxis.HeightFineX, WorldSeedAxis.HeightFineZ,
            WorldSeedAxis.HeightBroadX, WorldSeedAxis.HeightBroadZ,
            WorldSeedAxis.CaveX, WorldSeedAxis.CaveZ,
            WorldSeedAxis.MountainX, WorldSeedAxis.MountainZ,
            WorldSeedAxis.BaseLowX, WorldSeedAxis.BaseLowZ,
            WorldSeedAxis.BaseMidX, WorldSeedAxis.BaseMidZ,
        };

        private readonly AlphaWorldSeeds _seeds;

        public WorldSeedTable(uint masterSeed)
        {
            _seeds = new AlphaWorldSeeds(masterSeed);
        }

        public uint MasterSeed => _seeds.MasterSeed;

        /// <summary>Seed every region/zone/climate stream is derived from (Alpha world+0x800188).</summary>
        public int WorldSeed => _seeds.WorldSeed;

        public int HeightFineX => _seeds.At(OffsetHeightFineX);
        public int HeightFineZ => _seeds.At(OffsetHeightFineZ);
        public int HeightBroadX => _seeds.At(OffsetHeightBroadX);
        public int HeightBroadZ => _seeds.At(OffsetHeightBroadZ);
        public int CaveX => _seeds.At(OffsetCaveX);
        public int CaveZ => _seeds.At(OffsetCaveZ);
        public int BaseMidX => _seeds.At(OffsetBaseMidX);
        public int BaseMidZ => _seeds.At(OffsetBaseMidZ);
        public int BaseLowX => _seeds.At(OffsetBaseLowX);
        public int BaseLowZ => _seeds.At(OffsetBaseLowZ);
        public int MountainX => _seeds.At(OffsetMountainX);
        public int MountainZ => _seeds.At(OffsetMountainZ);

        /// <summary>Provenance of the verified table (kept as a fact, not as an API).</summary>
        public AlphaWorldSeeds AlphaTable => _seeds;

        public int At(WorldSeedAxis axis)
        {
            switch (axis)
            {
                case WorldSeedAxis.WorldSeed: return WorldSeed;
                case WorldSeedAxis.HeightFineX: return HeightFineX;
                case WorldSeedAxis.HeightFineZ: return HeightFineZ;
                case WorldSeedAxis.HeightBroadX: return HeightBroadX;
                case WorldSeedAxis.HeightBroadZ: return HeightBroadZ;
                case WorldSeedAxis.CaveX: return CaveX;
                case WorldSeedAxis.CaveZ: return CaveZ;
                case WorldSeedAxis.MountainX: return MountainX;
                case WorldSeedAxis.MountainZ: return MountainZ;
                case WorldSeedAxis.BaseLowX: return BaseLowX;
                case WorldSeedAxis.BaseLowZ: return BaseLowZ;
                case WorldSeedAxis.BaseMidX: return BaseMidX;
                case WorldSeedAxis.BaseMidZ: return BaseMidZ;
                default: throw new ArgumentOutOfRangeException(nameof(axis));
            }
        }

        private static int OffsetOf(WorldSeedAxis axis)
        {
            switch (axis)
            {
                case WorldSeedAxis.WorldSeed: return 0x800188;
                case WorldSeedAxis.HeightFineX: return OffsetHeightFineX;
                case WorldSeedAxis.HeightFineZ: return OffsetHeightFineZ;
                case WorldSeedAxis.HeightBroadX: return OffsetHeightBroadX;
                case WorldSeedAxis.HeightBroadZ: return OffsetHeightBroadZ;
                case WorldSeedAxis.CaveX: return OffsetCaveX;
                case WorldSeedAxis.CaveZ: return OffsetCaveZ;
                case WorldSeedAxis.MountainX: return OffsetMountainX;
                case WorldSeedAxis.MountainZ: return OffsetMountainZ;
                case WorldSeedAxis.BaseLowX: return OffsetBaseLowX;
                case WorldSeedAxis.BaseLowZ: return OffsetBaseLowZ;
                case WorldSeedAxis.BaseMidX: return OffsetBaseMidX;
                case WorldSeedAxis.BaseMidZ: return OffsetBaseMidZ;
                default: throw new ArgumentOutOfRangeException(nameof(axis));
            }
        }

        /// <summary>Every named axis with its value and provenance (debug overlay, reports, tests).</summary>
        public IReadOnlyList<WorldSeedValue> Describe()
        {
            var values = new List<WorldSeedValue>(Axes.Length);
            foreach (WorldSeedAxis axis in Axes)
            {
                values.Add(new WorldSeedValue(axis, axis.ToString(), OffsetOf(axis), At(axis)));
            }

            return values;
        }

        /// <summary>Self-check: every axis resolves and the named offsets stay distinct where they must be.</summary>
        public static List<string> Validate()
        {
            var problems = new List<string>();
            var seen = new Dictionary<int, WorldSeedAxis>();
            foreach (WorldSeedAxis axis in Axes)
            {
                int offset = OffsetOf(axis);
                if (seen.TryGetValue(offset, out WorldSeedAxis other))
                {
                    problems.Add($"WORLD_SEED_OFFSET_SHARED {axis}/{other} 0x{offset:x}");
                }
                else
                {
                    seen.Add(offset, axis);
                }

                try
                {
                    var table = new WorldSeedTable(12345u);
                    table.At(axis);
                }
                catch (Exception exception)
                {
                    problems.Add($"WORLD_SEED_AXIS_FAILED {axis}: {exception.GetType().Name}");
                }
            }

            var probe = new WorldSeedTable(777u);
            if (probe.MasterSeed != 777u) problems.Add("WORLD_SEED_MASTER_ROUNDTRIP");
            if (probe.WorldSeed != probe.AlphaTable.WorldSeed) problems.Add("WORLD_SEED_WORLD_MISMATCH");
            return problems;
        }
    }
}
