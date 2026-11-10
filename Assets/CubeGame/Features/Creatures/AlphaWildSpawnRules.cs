// CubeGame.Features.Creatures — how Alpha's zone generator places wild creatures (D1 REVIEW-FIX).
//
// Source: the 3×3 loop of the zone generator, Cube.exe 0x5EAE6A…0x5EBBB1 (inside 0x5E4850). Byte-verified in
// tools/alpha_models/world/spawn_pipeline_evidence.py (group "wild_grid"):
//   * 3 cells per axis (0x5EBBAE), a cell is skipped when rand()&3 == 0 (0x5EAF9E);
//   * position on each axis = zone origin + cell·85 + 24 + rand()%10 (0x5EAFB3, 0x5EAFC0, 0x5EAFC3);
//   * kind = 0x5F56C0(climate…) (0x5EB48C); level = min + rand()%(max−min+1) of 0x444020(kind) (0x5EB7B2);
//   * a hostile template (faction 1) gets the time-of-day window 06:00…24:00 (0x5EB83E/0x5EB845);
//   * pack: count range of 0x443F40(kind) (0x5EB87A), extra members = min − 1 + rand()%(max−min+1), each member
//     is its own template (0x5EB8D9) with kind 0x5F8590(leader kind) (0x5EBADD) and its own level roll.
// The two per-kind tables are executed Alpha code (AlphaSpawnPipelineData, EMULATED_ALPHA_CODE).
//
// ALPHA_DECOMPILED (read, not byte-checked): members stand on a circle of radius 8 at angle 2π·i/count; the
// companion-kind switch of 0x5F8590 (transcribed below).
// NOT MODELLED here (present in Alpha, see the report): the climate skips (humidity/temperature below 0.2),
// the landmark mask, the 20-voxel exclusion around earlier objects, the height tests, the ground-type plant
// overrides and the 1 % flag 0x200.
using System;
using CubeGame.Content;

namespace CubeGame.Features.Creatures
{
    /// <summary>Recovered constants and pure rules of Alpha's wild-creature grid.</summary>
    public static class AlphaWildSpawnRules
    {
        public const string Status = "ALPHA_CODE_STATIC: Cube.exe 0x5EAE6A-0x5EBBB1 (byte-verified constants)";

        public const int CellsPerAxis = 3;
        public const int CellStep = 85;
        public const int CellOffset = 24;
        public const int JitterRange = 10;

        /// <summary>A cell is skipped when rand()&amp;3 == 0.</summary>
        public const int SkipMask = 3;

        /// <summary>ALPHA_DECOMPILED: radius of the pack circle around the leader.</summary>
        public const double PackRadius = 8.0;

        /// <summary>Time-of-day window written to hostile templates of the grid (world-time ms).</summary>
        public const int HostileWindowStartMs = 21600000;
        public const int HostileWindowEndMs = 86400000;

        public const int FactionHostile = 1;
        public const int FactionPassive = 6;

        public static bool CellSkipped(int rand) => (rand & SkipMask) == 0;

        /// <summary>Local coordinate of a cell on one axis for a rand() value.</summary>
        public static int CellCoordinate(int cell, int rand) => cell * CellStep + CellOffset + rand % JitterRange;

        /// <summary>Level of a non-passive template: min + rand()%(max − min + 1) of the kind's executed range.</summary>
        public static int Level(int kind, int rand)
        {
            AlphaSpawnPipelineData.LevelRange(kind, out int min, out int max);
            return min + rand % (max - min + 1);
        }

        /// <summary>Extra pack members besides the leader: min − 1 + rand()%(max − min + 1).</summary>
        public static int ExtraPackMembers(int leaderKind, int rand)
        {
            AlphaSpawnPipelineData.PackRange(leaderKind, out int min, out int max);
            return min - 1 + rand % (max - min + 1);
        }

        /// <summary>Offset of pack member <paramref name="index"/> of <paramref name="count"/> from the leader.</summary>
        public static void PackOffset(int index, int count, out double dx, out double dz)
        {
            double angle = index * (2.0 * Math.PI) / count;
            dx = Math.Cos(angle) * PackRadius;
            dz = Math.Sin(angle) * PackRadius;
        }

        /// <summary>
        /// ALPHA_DECOMPILED 0x5F8590: kind of a pack member. Paired kinds re-roll inside their pair
        /// (rand()&amp;1), kinds 0x25…0x28 inside their group of four (rand()&amp;3), every other kind is kept.
        /// </summary>
        public static int CompanionKind(int leaderKind, int rand)
        {
            switch (leaderKind)
            {
                case 0x00: case 0x01: return 0x00 + (rand & 1);
                case 0x02: case 0x03: return 0x02 + (rand & 1);
                case 0x04: case 0x05: return 0x04 + (rand & 1);
                case 0x07: case 0x08: return 0x07 + (rand & 1);
                case 0x09: case 0x0A: return 0x09 + (rand & 1);
                case 0x0B: case 0x0C: return 0x0B + (rand & 1);
                case 0x0D: case 0x0E: return 0x0D + (rand & 1);
                case 0x0F: case 0x10: return 0x0F + (rand & 1);
                case 0x16: case 0x17: return 0x16 + (rand & 1);
                case 0x25: case 0x26: case 0x27: case 0x28: return 0x25 + (rand & 3);
                case 0x53: case 0x54: return 0x53 + (rand & 1);
                default: return leaderKind;
            }
        }
    }
}
