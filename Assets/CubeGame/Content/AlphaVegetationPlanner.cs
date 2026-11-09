// CubeGame.Content — deterministic placement of W-18 climate objects inside a zone or a landmark cell
// (D86 M0-02C/M0-06, corrected for the executed Alpha scale).
//
// The selection comes from AlphaScatterSelector (translated Alpha tree 0x5F56C0). This planner turns
// it into concrete positions. Two levels are offered:
//   * PlanZone — the streaming unit of the CubeGame/Claude world (zone = 256×256 voxels). This is what
//     the runtime content streamer uses: every zone produces its own deterministic objects, so a zone
//     can be unloaded and reloaded without depending on neighbours.
//   * Plan — the older landmark-cell variant (cell = 8×8 zones = 2 048 voxels), kept for callers that
//     already work in cells.
//
// Positions are an explicit CubeGame contract: only staged kinds are placed, a kind that Alpha selects
// but the local cache does not have is reported instead of being replaced, and no Alpha placement
// policy (density, spacing enforcement, `rotateAndPlace` caller) is claimed. See
// docs/reports/D86_W18_ADDRESS_VERIFICATION.md for what 0x5EFFA0 really is.
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

    /// <summary>One placed climate object inside a single zone (absolute voxel coordinates).</summary>
    public struct AlphaZoneScatterPlacement
    {
        public string StableId;
        public int Kind;
        public WorldZoneKey Zone;
        public double X, Z;
        public string Source;
    }

    public static class AlphaVegetationPlanner
    {
        /// <summary>CUBEGAME_CONTRACT: how many objects one landmark cell may receive from the selection.</summary>
        public const int MaxObjectsPerCell = 8;

        /// <summary>CUBEGAME_CONTRACT: how many objects one zone may receive from the selection.</summary>
        public const int MaxObjectsPerZone = 6;

        public const string Status =
            "CUBEGAME_CONTRACT: positions are deterministic CubeGame placement; Alpha's placement policy is not recovered";

        /// <summary>
        /// Cell-level plan. Coordinates use the executed Alpha scale: region = 16 384 voxels,
        /// cell = 2 048 voxels (previously the generated table's zone count was read as a voxel size).
        /// </summary>
        public static List<AlphaScatterPlacement> Plan(uint seed, int regionX, int regionZ, int cellX, int cellZ,
            double humidity, double temperature, double cellValue, double spacing, int level, int mode,
            IReadOnlyList<int> stagedKinds, List<string> problems = null)
        {
            var result = new List<AlphaScatterPlacement>();
            var selection = AlphaScatterSelector.Select(humidity, temperature, cellValue, spacing, level, mode);
            if (selection.ObjectKinds == null || selection.ObjectKinds.Length == 0) return result;
            int perKind = Math.Max(1, MaxObjectsPerCell / selection.ObjectKinds.Length);
            int cellSize = AlphaWorldScale.CellVoxels;
            double cellOriginX = AlphaWorldScale.RegionOriginVoxel(regionX) + AlphaWorldScale.CellOriginVoxel(cellX);
            double cellOriginZ = AlphaWorldScale.RegionOriginVoxel(regionZ) + AlphaWorldScale.CellOriginVoxel(cellZ);
            var seen = new HashSet<int>();
            for (int k = 0; k < selection.ObjectKinds.Length; k++)
            {
                int kind = selection.ObjectKinds[k];
                if (!AlphaContentDatabase.TryCreatureKind(kind, out _))
                {
                    problems?.Add($"SCATTER_KIND_UNKNOWN 0x{kind:x2}");
                    continue;
                }
                if (!Contains(stagedKinds, kind))
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
                    result.Add(new AlphaScatterPlacement
                    {
                        StableId = $"scatter:{regionX}:{regionZ}:{cellX}:{cellZ}:{kind}:{n}",
                        Kind = kind,
                        RegionX = regionX, RegionZ = regionZ, CellX = cellX, CellZ = cellZ,
                        X = cellOriginX + localX + 0.5,
                        Z = cellOriginZ + localZ + 0.5,
                        Source = selection.Rule,
                    });
                }
                seen.Add(kind);
            }
            if (problems != null && seen.Count == 0 && selection.ObjectKinds.Length > 0)
                problems.Add("SCATTER_NO_STAGED_KINDS_IN_SELECTION");
            return result;
        }

        /// <summary>
        /// Zone-level plan: the same W-18 selection, but positions and stable ids are scoped to one zone
        /// (256×256 voxels) so that zone streaming can load and unload content independently.
        /// Returns only kinds that are staged; unstaged selections are reported, never substituted.
        /// </summary>
        public static List<AlphaZoneScatterPlacement> PlanZone(uint seed, int zoneX, int zoneZ,
            double humidity, double temperature, double cellValue, double spacing, int level, int mode,
            IReadOnlyList<int> stagedKinds, List<string> problems = null, int maxPerZone = MaxObjectsPerZone)
        {
            var result = new List<AlphaZoneScatterPlacement>();
            var zone = new WorldZoneKey(zoneX, zoneZ);
            if (!AlphaWorldScale.IsInsideWorld(zone))
            {
                problems?.Add($"SCATTER_ZONE_OUTSIDE_WORLD {zone}");
                return result;
            }
            var selection = AlphaScatterSelector.Select(humidity, temperature, cellValue, spacing, level, mode);
            if (selection.ObjectKinds == null || selection.ObjectKinds.Length == 0) return result;
            if (maxPerZone <= 0) return result;
            int kindsForPlacement = 0;
            for (int i = 0; i < selection.ObjectKinds.Length; i++) if (Contains(stagedKinds, selection.ObjectKinds[i])) kindsForPlacement++;
            int perKind = kindsForPlacement > 0 ? Math.Max(1, maxPerZone / kindsForPlacement) : 0;
            int originX = AlphaWorldScale.ZoneOriginVoxel(zoneX);
            int originZ = AlphaWorldScale.ZoneOriginVoxel(zoneZ);
            var seen = new HashSet<int>();
            for (int k = 0; k < selection.ObjectKinds.Length; k++)
            {
                int kind = selection.ObjectKinds[k];
                if (!AlphaContentDatabase.TryCreatureKind(kind, out _))
                {
                    problems?.Add($"SCATTER_KIND_UNKNOWN 0x{kind:x2}");
                    continue;
                }
                if (!Contains(stagedKinds, kind))
                {
                    problems?.Add($"SCATTER_KIND_NOT_STAGED 0x{kind:x2}");
                    continue;
                }
                for (int n = 0; n < perKind; n++)
                {
                    uint h = DeterministicHash.Combine((int)seed, zoneX, zoneZ, kind, n, 1);
                    int localX = (int)(h % (uint)AlphaWorldScale.ZoneVoxels);
                    int localZ = (int)((h >> 8) % (uint)AlphaWorldScale.ZoneVoxels);
                    result.Add(new AlphaZoneScatterPlacement
                    {
                        StableId = $"scatter:z:{zoneX}:{zoneZ}:{kind}:{n}",
                        Kind = kind,
                        Zone = zone,
                        X = originX + localX + 0.5,
                        Z = originZ + localZ + 0.5,
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
            if (list == null) return false;
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
                if (a[i].X < 0 || a[i].Z < 0 || a[i].X >= AlphaWorldScale.CellVoxels || a[i].Z >= AlphaWorldScale.CellVoxels)
                    p.Add($"SCATTER_PLAN_OUTSIDE_CELL {a[i].X},{a[i].Z}");
            }

            var zonePlan = PlanZone(7u, AlphaWorldScale.StartZoneX, AlphaWorldScale.StartZoneZ,
                0.5, 0.5, 0.0, 0.5, 6, 0, staged);
            var zonePlanAgain = PlanZone(7u, AlphaWorldScale.StartZoneX, AlphaWorldScale.StartZoneZ,
                0.5, 0.5, 0.0, 0.5, 6, 0, staged);
            if (zonePlan.Count == 0) p.Add("SCATTER_ZONE_PLAN_EMPTY");
            if (zonePlan.Count != zonePlanAgain.Count) p.Add("SCATTER_ZONE_PLAN_NON_DETERMINISTIC");
            int zoneOrigin = AlphaWorldScale.ZoneOriginVoxel(AlphaWorldScale.StartZoneX);
            for (int i = 0; i < Math.Min(zonePlan.Count, zonePlanAgain.Count); i++)
            {
                if (zonePlan[i].StableId != zonePlanAgain[i].StableId || zonePlan[i].X != zonePlanAgain[i].X ||
                    zonePlan[i].Z != zonePlanAgain[i].Z)
                    p.Add("SCATTER_ZONE_PLAN_NON_DETERMINISTIC");
                if (zonePlan[i].X < zoneOrigin || zonePlan[i].Z < zoneOrigin ||
                    zonePlan[i].X >= zoneOrigin + AlphaWorldScale.ZoneVoxels ||
                    zonePlan[i].Z >= zoneOrigin + AlphaWorldScale.ZoneVoxels)
                    p.Add($"SCATTER_ZONE_PLAN_OUTSIDE_ZONE {zonePlan[i].X},{zonePlan[i].Z}");
            }

            var problems = new List<string>();
            var missing = Plan(7u, 0, 0, 0, 0, 0.5, 0.5, 0.0, 0.5, 6, 0, new List<int>(), problems);
            if (missing.Count != 0) p.Add("SCATTER_PLAN_WITHOUT_KINDS");
            if (!problems.Contains("SCATTER_KIND_NOT_STAGED 0x15")) p.Add("SCATTER_PLAN_MUST_REPORT_UNSTAGED");

            var zoneProblems = new List<string>();
            var outside = PlanZone(7u, -1, 0, 0.5, 0.5, 0.0, 0.5, 6, 0, staged, zoneProblems);
            if (outside.Count != 0) p.Add("SCATTER_ZONE_PLAN_OUTSIDE_WORLD_PLACED");
            if (!zoneProblems.Contains("SCATTER_ZONE_OUTSIDE_WORLD zone(-1,0)")) p.Add("SCATTER_ZONE_PLAN_MUST_REPORT_OUTSIDE");
            return p;
        }

        public static string Summary()
        {
            var staged = new List<int> { 0x15, 0x2e, 0x2f };
            int dry = Plan(1u, 0, 0, 0, 0, 0.5, 0.5, 0.0, 0.5, 6, 0, staged).Count;
            int climate = Plan(1u, 0, 0, 0, 0, 0.5, 0.5, 0.5, 0.0, 6, 0, staged).Count;
            int zone = PlanZone(1u, AlphaWorldScale.StartZoneX, AlphaWorldScale.StartZoneZ,
                0.5, 0.5, 0.0, 0.5, 6, 0, staged).Count;
            return $"vegetation dry={dry} climate={climate} startZone={zone} status=TRANSLATED_FROM_CODE_PLACEMENT";
        }
    }
}
