// CubeGame.Features.Creatures — kind of a wild creature template, recovered from Alpha 0x5F56C0 (M0-F1A).
//
// Alpha's wild grid (zone generator 0x5E4850) writes the template kind from the return value of 0x5F56C0
// (call 0x5EB48C, `mov [edi+0x2C],eax` 0x5EB491; arguments: voxel x, voxel z, height of the first non-solid
// voxel of the column, mode 0). The function samples four climate fields at (x, z), pushes a candidate list
// and returns ONE kind: list[rand() % n], or 0x3C for an empty list.
//
// Evidence: tools/alpha_models/world/creature_kind_evidence.py EXECUTES 0x5F56C0 (M0-02 oracle; only the four
// climate callees are stubbed) over 3150 inputs on both sides of every threshold → AlphaCreatureKindCases.g.cs;
// the tree below is checked against every executed case by AlphaCreatureKindSelectorTests. Status
// EMULATED_ALPHA_CODE for the selection rule. The climate inputs come from the existing executed ports of
// AlphaTerrainGenerator (0x5C4DD0 humidity, 0x5C4800 temperature, 0x5EF040 rare cell, 0x5EFFA0 mountain).
//
// NOT the same as Content/Generated/AlphaScatterTree.g.cs although both name 0x5F56C0: that translation of the
// decompiled text loses ids (0x23, 0x17, 0x63, 0x64, 2, 4, 7, 9), adds 0x97 and 0x6A/0x39/0x56/0x19 to the wrong
// branches, and returns a list instead of one kind. It is the W-18 content route's file and is left unchanged;
// the defect is handed to its owner (docs/reports/M0_F1A_POPULATION_RUNTIME_REPORT.md).
using System;

namespace CubeGame.Features.Creatures
{
    /// <summary>Climate sample at one voxel column: the four inputs of 0x5F56C0.</summary>
    public readonly struct AlphaClimateSample
    {
        public readonly float Humidity;
        public readonly float Temperature;
        public readonly float RareCell;
        public readonly float Mountain;

        public AlphaClimateSample(float humidity, float temperature, float rareCell, float mountain)
        {
            Humidity = humidity;
            Temperature = temperature;
            RareCell = rareCell;
            Mountain = mountain;
        }
    }

    /// <summary>Recovered Alpha 0x5F56C0: climate → candidate kinds → one kind.</summary>
    public static class AlphaCreatureKindSelector
    {
        public const string Status =
            "EMULATED_ALPHA_CODE: Cube.exe 0x5F56C0 executed over 3150 inputs (creature_kind_evidence.py)";

        /// <summary>Returned for an empty candidate list (0x5F56C0 epilogue).</summary>
        public const int EmptyListKind = 0x3C;

        /// <summary>Mode argument of the wild grid call (push 0 at 0x5EB462).</summary>
        public const int WildGridMode = 0;

        static readonly int[] Underground = { 0x91, 0x92, 0x93, 0x96, 0x98, 0x99, 0x9B, 0x9A };
        static readonly int[] Flagged = { 0x02, 0x04, 0x07, 0x09, 0x0B, 0x0F, 0x33, 0x30, 0x4C };
        static readonly int[] Wet = { 0x49, 0x52, 0x55, 0x70, 0x67, 0x3E, 0x6E };
        static readonly int[] Low = { 0x6A, 0x39, 0x56, 0x19 };
        static readonly int[] Dry = { 0x1C, 0x38, 0x41, 0x5A, 0x5D, 0x15, 0x31, 0x16, 0x17, 0x23 };
        static readonly int[] HumidWarm =
        {
            0x36, 0x59, 0x4B, 0x4A, 0x47, 0x40, 0x44, 0x3C, 0x3A, 0x43, 0x53, 0x54, 0x19, 0x66, 0x68, 0x69,
            0x58, 0x3E, 0x24, 0x32, 0x16, 0x17,
        };
        static readonly int[] HumidCold =
        {
            0x36, 0x3D, 0x48, 0x2A, 0x49, 0x42, 0x53, 0x54, 0x66, 0x67, 0x58, 0x3E, 0x24, 0x63,
        };
        static readonly int[] Temperate =
        {
            0x36, 0x4A, 0x1C, 0x38, 0x19, 0x35, 0x37, 0x3C, 0x3F, 0x43, 0x45, 0x46, 0x47, 0x57, 0x58, 0x64,
            0x5A, 0x5B, 0x5C, 0x23, 0x66, 0x68, 0x69, 0x22, 0x21, 0x1E, 0x1F, 0x20, 0x13, 0x14, 0x1A, 0x1B,
            0x62, 0x16, 0x17, 0x97,
        };

        /// <summary>Candidate list in the order the machine code pushes it (duplicates are weights, e.g. 0x15).</summary>
        /// <param name="level">Third argument: height of the first non-solid voxel of the column (&lt; 0 → underground).</param>
        /// <param name="mode">Fourth argument, low byte (0 for the wild grid).</param>
        public static int[] Candidates(AlphaClimateSample climate, int level, int mode = WildGridMode)
        {
            if (level < 0) return (int[])Underground.Clone();
            if ((mode & 0xFF) != 0) return (int[])Flagged.Clone();
            float h = climate.Humidity, t = climate.Temperature;
            if (!(climate.RareCell <= 0.1f)) return (int[])Wet.Clone();

            var ids = new System.Collections.Generic.List<int>(40) { 0x15, 0x2E };
            if (0.3f < climate.Mountain) ids.Add(0x2F);
            if (3 < level)
            {
                if (h < 0.2f || 0.8f <= h)
                {
                    if (h < 0.8f || 0.2f <= t)
                    {
                        if (h < 0.6f || t < 0.6f)
                        {
                            if (h < 0.2f) ids.AddRange(Dry);
                        }
                        else
                        {
                            ids.AddRange(HumidWarm);
                        }
                    }
                    else
                    {
                        ids.AddRange(HumidCold);
                    }
                }
                else
                {
                    ids.AddRange(Temperate);
                }
            }
            else
            {
                ids.AddRange(Low);
            }

            return ids.ToArray();
        }

        /// <summary>list[rand() % n] (unsigned modulo, 0x5F6CF8); <see cref="EmptyListKind"/> for an empty list.</summary>
        public static int Pick(int[] candidates, int rand)
        {
            if (candidates == null || candidates.Length == 0) return EmptyListKind;
            return candidates[(int)((uint)rand % (uint)candidates.Length)];
        }

        public static int Select(AlphaClimateSample climate, int level, int mode, int rand) =>
            Pick(Candidates(climate, level, mode), rand);

        /// <summary>
        /// Adapter for the wild grid: samples the climate at a voxel column. The functions are the world module's
        /// executed ports (AlphaTerrainGenerator.Humidity/Temperature/RareCellTerm/Mountain); <paramref name="level"/>
        /// returns the height of the first non-solid voxel of the column (Alpha scans from the column base).
        /// </summary>
        public static Func<int, int, int[]> FromClimate(Func<int, int, AlphaClimateSample> climate,
            Func<int, int, int> level)
        {
            if (climate == null) throw new ArgumentNullException(nameof(climate));
            if (level == null) throw new ArgumentNullException(nameof(level));
            return (x, z) => Candidates(climate(x, z), level(x, z), WildGridMode);
        }
    }
}
