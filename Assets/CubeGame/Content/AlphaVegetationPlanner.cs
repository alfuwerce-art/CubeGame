// CubeGame.Content — deterministic placement of W-18 climate objects inside a world cell (D86 M0-02C).
//
// The selection comes from AlphaScatterSelector (translated Alpha tree). This planner turns it into
// concrete positions: only staged kinds are placed, a kind that is selected by Alpha but not staged is
// reported instead of being replaced, and the positions are a deterministic CubeGame contract because
// the original spacing function (World_placeObjectWithSpacing) is not ported yet.
using System;
using System.Collections.Generic;
using CubeGame.Core.Generation;

namespace CubeGame.Content
{
    public struct AlphaScatterPlacement
    {
        public string StableId;
        public int Kind;
        public int RegionX, RegionZ, CellX, CellZ;
        public double X, Z;
        public string Source;   // the selector rule
    }

    public static class AlphaVegetationPlanner
    {
        /// <summary>CUBEGAME_CONTRACT: how many objects one cell may receive from the selection.</summary>
        public const int MaxObjectsPerCell = 8;
        public const string Status =
            "CUBEGAME_CONTRACT: positions are deterministic CubeGame placement; World_placeObjectWithSpacing is not ported";

        public static List<AlphaScatterPlacement> Plan(uint seed, int regionX, int regionZ, int cellX, int cellZ,
            double humidity, double temperature, double cellValue, double spacing, int level, int mode,
            IReadOnlyList<int> stagedKinds, List<string> problems = null)
        {
            var result = new List<AlphaScatterPlacement>();
            var selection = AlphaScatterSelector.Select(humidity, temperature, cellValue, spacing, level, mode);
            if (selection.ObjectKinds == null || selection.ObjectKinds.Length == 0) return result;
            int cellSize = AlphaContentTables.RegionBlocks / AlphaContentTables.RegionCells;
            int perKind = Math.Max(1, MaxObjectsPerCell / selection.ObjectKinds.Length);
            var seen = new HashSet<int>();
            for (int k = 0; k < selection.ObjectKinds.Length; k++)
            {
                int kind = selection.ObjectKinds[k];
                if (!AlphaContentDatabase.TryCreatureKind(kind, out var def))
                {
                    problems?.Add($"SCATTER_KIND_UNKNOWN 0x{kind:x2}");
                    continue;
                }
                bool staged = stagedKinds != null && Contains(stagedKinds, kind);
                if (!staged)
                {
                    // Alpha selects this object but the local content cache does not have it.
                    problems?.Add($"SCATTER_KIND_NOT_STAGED 0x{kind:x2}");
                    continue;
                }
                for (int n = 0; n < perKind; n++)
                {
                    uint h = DeterministicHash.Combine((int)seed, regionX, regionZ, cellX, cellZ, k, n);
                    int localX = (int)(h % (uint)cellSize);
                    int localZ = (int)((h >> 8) % (uint)cellSize);
                    double x = regionX * AlphaContentTables.RegionBlocks + cellX * cellSize + localX + 0.5;
                    double z = regionZ * AlphaContentTables.RegionBlocks + cellZ * cellSize + localZ + 0.5;
                    result.Add(new AlphaScatterPlacement
                    {
                        StableId = $"scatter:{regionX}:{regionZ}:{cellX}:{cellZ}:{kind}:{n}",
                        Kind = kind,
                        RegionX = regionX, RegionZ = regionZ, CellX = cellX, CellZ = cellZ,
                        X = x, Z = z,
                        Source = selection.Rule,
                    });
                }
                seen.Add(kind);
            }
            if (problems != null && seen.Count == 0 && selection.ObjectKinds.Length > 0)
                problems.Add("SCATTER_NO_STAGED_KINDS_IN_SELECTION");
            return result;
        }

        static bool Contains(IReadOnlyList<int> list, int value)
        {
            for (int i = 0; i < list.Count; i++) if (list[i] == value) return true;
            return false;
        }

        public static List<string> Validate()
        {
            var p = new List<string>();
            var staged = new List<int> { 0x15, 0x2e, 0x2f };
            var a = Plan(7u, 0, 0, 0, 0, 0.5, 0.5, 0.0, 0.5, 6, 0, staged);
            var b = Plan(7u, 0, 0, 0, 0, 0.5, 0.5, 0.0, 0.5, 6, 0, staged);
            if (a.Count == 0) p.Add("SCATTER_PLAN_EMPTY");
            if (a.Count != b.Count) p.Add("SCATTER_PLAN_NON_DETERMINISTIC");
            for (int i = 0; i < Math.Min(a.Count, b.Count); i++)
            {
                if (a[i].StableId != b[i].StableId || a[i].X != b[i].X || a[i].Z != b[i].Z)
                    p.Add("SCATTER_PLAN_NON_DETERMINISTIC");
                if (Array.IndexOf(staged.ToArray(), a[i].Kind) < 0) p.Add($"SCATTER_PLAN_KIND {a[i].Kind}");
            }
            var problems = new List<string>();
            var missing = Plan(7u, 0, 0, 0, 0, 0.5, 0.5, 0.0, 0.5, 6, 0, new List<int>(), problems);
            if (missing.Count != 0) p.Add("SCATTER_PLAN_WITHOUT_KINDS");
            if (!problems.Contains("SCATTER_KIND_NOT_STAGED 0x15")) p.Add("SCATTER_PLAN_MUST_REPORT_UNSTAGED");
            return p;
        }

        public static string Summary()
        {
            var staged = new List<int> { 0x15, 0x2e, 0x2f };
            int dry = Plan(1u, 0, 0, 0, 0, 0.5, 0.5, 0.0, 0.5, 6, 0, staged).Count;
            int climate = Plan(1u, 0, 0, 0, 0, 0.5, 0.5, 0.5, 0.0, 6, 0, staged).Count;
            return $"vegetation dry={dry} climate={climate} status=TRANSLATED_FROM_CODE_PLACEMENT";
        }
    }
}
