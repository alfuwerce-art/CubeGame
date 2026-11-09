// CubeGame.Content — structure templates and deterministic placement (D86 M0-02C/M0-07A).
//
// Alpha evidence fixes the SHAPE of the buildings (house always 3x3x4 with style 1..5, dungeon room
// 3x3x3 with step 7) but not the rules of "what goes where" (W-16 rules UNRECOVERED, W-18 climate
// table not extracted). This module therefore provides templates + a deterministic candidate
// placement that asks the world generator whether the footprint is free/flat; it never invents a
// town layout or a biome assignment.
using System;
using System.Collections.Generic;
using CubeGame.Core.Generation;

namespace CubeGame.Content
{
    public readonly struct AlphaStructureTemplateDef
    {
        public readonly string Id;
        public readonly int Width, Depth, Height;
        public readonly int StyleFirst, StyleLast;
        public readonly string Evidence;
        public readonly string Status;

        public AlphaStructureTemplateDef(string id, int width, int depth, int height, int styleFirst, int styleLast,
            string evidence, string status)
        {
            Id = id; Width = width; Depth = depth; Height = height;
            StyleFirst = styleFirst; StyleLast = styleLast; Evidence = evidence; Status = status;
        }
    }

    public static class AlphaStructureTemplates
    {
        public const string HouseId = "house_3x3x4";
        public const string DungeonRoomId = "dungeon_room_3x3x3";
        /// <summary>Dungeon rooms repeat with step 7 (Q-16, PARTIAL).</summary>
        public const int DungeonRoomStep = 7;

        static readonly AlphaStructureTemplateDef[] Templates =
        {
            new AlphaStructureTemplateDef(HouseId, 3, 3, 4, 1, 5,
                "W-17: house object, always 3x3x4, style field +0x60 = 1..5, Chunk_generateObjects 005af170",
                "SIZE_DIRECT / INTERIOR_LAYOUT_UNKNOWN"),
            new AlphaStructureTemplateDef(DungeonRoomId, 3, 3, 3, 0, 0,
                "Q-16: dungeon rooms 3x3x3 with step 7, L-corridors 005c52a0, cell 2 bytes",
                "PARTIAL"),
        };

        public static IReadOnlyList<AlphaStructureTemplateDef> All => Templates;

        public static bool TryGet(string id, out AlphaStructureTemplateDef def)
        {
            foreach (var t in Templates)
                if (t.Id == id) { def = t; return true; }
            def = default;
            return false;
        }
    }

    public struct AlphaStructurePlacement
    {
        public string StableId;
        public string TemplateId;
        public int RegionX, RegionZ, CellX, CellZ;
        /// <summary>Zone the footprint starts in (zone-level planning); -1 for the legacy cell-level plan.</summary>
        public int ZoneX, ZoneZ;
        public int X, Z, Rotation;
        public int Width, Depth, Height;
    }

    /// <summary>
    /// Deterministic structure candidates inside a world cell. The caller (world generator) decides
    /// whether a footprint is free and flat; a cell without a usable footprint yields an explicit
    /// problem instead of a forced building.
    /// </summary>
    public static class AlphaStructurePlacementPlanner
    {
        public const int MaxStructuresPerCell = 1;
        /// <summary>How many candidate footprints per cell are tried before giving up (CUBEGAME_CONTRACT).</summary>
        public const int PlacementAttempts = 4;

        /// <param name="isFootprintFree">(x, z, width, depth) → true when the world allows a building there.</param>
        public static List<AlphaStructurePlacement> Plan(uint seed, int regionX, int regionZ, int cellX, int cellZ,
            IReadOnlyList<string> templateIds, Func<int, int, int, int, bool> isFootprintFree, List<string> problems = null)
        {
            var result = new List<AlphaStructurePlacement>();
            if (templateIds == null || templateIds.Count == 0) { problems?.Add("STRUCTURE_NO_TEMPLATES"); return result; }
            int cellSize = AlphaWorldScale.CellVoxels;
            int cellOriginX = AlphaWorldScale.RegionOriginVoxel(regionX) + AlphaWorldScale.CellOriginVoxel(cellX);
            int cellOriginZ = AlphaWorldScale.RegionOriginVoxel(regionZ) + AlphaWorldScale.CellOriginVoxel(cellZ);
            for (int slot = 0; slot < MaxStructuresPerCell; slot++)
            {
                uint h = DeterministicHash.Combine((int)seed, regionX, regionZ, cellX, cellZ, slot);
                string templateId = templateIds[(int)(h % (uint)templateIds.Count)];
                if (!AlphaStructureTemplates.TryGet(templateId, out var template))
                {
                    problems?.Add($"STRUCTURE_TEMPLATE_UNKNOWN {templateId}");
                    continue;
                }
                if (template.Width > cellSize || template.Depth > cellSize)
                {
                    problems?.Add($"STRUCTURE_TOO_LARGE {templateId} {template.Width}x{template.Depth} > cell {cellSize}");
                    continue;
                }
                bool placed = false;
                for (int attempt = 0; attempt < PlacementAttempts && !placed; attempt++)
                {
                    uint mix = DeterministicHash.Combine((int)h, attempt, slot, template.Width, template.Depth);
                    int maxX = cellSize - template.Width;
                    int maxZ = cellSize - template.Depth;
                    int offsetX = maxX > 0 ? (int)((mix >> 3) % (uint)(maxX + 1)) : 0;
                    int offsetZ = maxZ > 0 ? (int)((mix >> 13) % (uint)(maxZ + 1)) : 0;
                    int x = cellOriginX + offsetX;
                    int z = cellOriginZ + offsetZ;
                    if (isFootprintFree != null && !isFootprintFree(x, z, template.Width, template.Depth)) continue;
                    result.Add(new AlphaStructurePlacement
                    {
                        StableId = $"structure:{regionX}:{regionZ}:{cellX}:{cellZ}:{templateId}:{slot}",
                        TemplateId = templateId,
                        RegionX = regionX, RegionZ = regionZ, CellX = cellX, CellZ = cellZ,
                        ZoneX = -1, ZoneZ = -1,
                        X = x, Z = z, Rotation = (int)((mix >> 24) & 3u),
                        Width = template.Width, Depth = template.Depth, Height = template.Height,
                    });
                    placed = true;
                }
                if (!placed) problems?.Add($"STRUCTURE_NO_FREE_FOOTPRINT {regionX}:{regionZ}:{cellX}:{cellZ} {templateId}");
            }
            return result;
        }

        /// <summary>
        /// Zone-level candidate (streaming unit = 256×256 voxels). The footprint is searched inside the zone,
        /// so a structure belongs to exactly one zone and its stable id survives unload/reload. Alpha's own
        /// town/house placement rules (W-16) are NOT recovered: the caller's predicate decides.
        /// </summary>
        public static List<AlphaStructurePlacement> PlanZone(uint seed, int zoneX, int zoneZ,
            IReadOnlyList<string> templateIds, Func<int, int, int, int, bool> isFootprintFree,
            List<string> problems = null, int attempts = PlacementAttempts)
        {
            var result = new List<AlphaStructurePlacement>();
            var zone = new WorldZoneKey(zoneX, zoneZ);
            if (!AlphaWorldScale.IsInsideWorld(zone))
            {
                problems?.Add($"STRUCTURE_ZONE_OUTSIDE_WORLD {zone}");
                return result;
            }
            if (templateIds == null || templateIds.Count == 0) { problems?.Add("STRUCTURE_NO_TEMPLATES"); return result; }
            uint h = DeterministicHash.Combine((int)seed, zoneX, zoneZ, 0x57, 0);
            string templateId = templateIds[(int)(h % (uint)templateIds.Count)];
            if (!AlphaStructureTemplates.TryGet(templateId, out var template))
            {
                problems?.Add($"STRUCTURE_TEMPLATE_UNKNOWN {templateId}");
                return result;
            }
            int originX = AlphaWorldScale.ZoneOriginVoxel(zoneX);
            int originZ = AlphaWorldScale.ZoneOriginVoxel(zoneZ);
            int maxX = AlphaWorldScale.ZoneVoxels - template.Width;
            int maxZ = AlphaWorldScale.ZoneVoxels - template.Depth;
            if (maxX < 0 || maxZ < 0)
            {
                problems?.Add($"STRUCTURE_TOO_LARGE {templateId} {template.Width}x{template.Depth} > zone");
                return result;
            }
            for (int attempt = 0; attempt < Math.Max(1, attempts); attempt++)
            {
                uint mix = DeterministicHash.Combine((int)h, attempt, template.Width, template.Depth);
                int offsetX = (int)((mix >> 3) % (uint)(maxX + 1));
                int offsetZ = (int)((mix >> 13) % (uint)(maxZ + 1));
                int x = originX + offsetX;
                int z = originZ + offsetZ;
                if (isFootprintFree != null && !isFootprintFree(x, z, template.Width, template.Depth)) continue;
                result.Add(new AlphaStructurePlacement
                {
                    StableId = $"structure:z:{zoneX}:{zoneZ}:{templateId}:0",
                    TemplateId = templateId,
                    RegionX = AlphaWorldScale.RegionOfZone(zoneX), RegionZ = AlphaWorldScale.RegionOfZone(zoneZ),
                    CellX = AlphaWorldScale.CellOfZone(zoneX), CellZ = AlphaWorldScale.CellOfZone(zoneZ),
                    ZoneX = zoneX, ZoneZ = zoneZ,
                    X = x, Z = z, Rotation = (int)((mix >> 24) & 3u),
                    Width = template.Width, Depth = template.Depth, Height = template.Height,
                });
                return result;
            }
            problems?.Add($"STRUCTURE_NO_FREE_FOOTPRINT z:{zoneX}:{zoneZ} {templateId}");
            return result;
        }

        public static List<string> Validate()
        {
            var p = new List<string>();
            if (AlphaStructureTemplates.All.Count == 0) p.Add("STRUCTURE_NO_TEMPLATES");
            if (!AlphaStructureTemplates.TryGet(AlphaStructureTemplates.HouseId, out var house)) p.Add("STRUCTURE_HOUSE_MISSING");
            else
            {
                if (house.Width != 3 || house.Depth != 3 || house.Height != 4) p.Add($"STRUCTURE_HOUSE_SIZE {house.Width}x{house.Depth}x{house.Height}");
                if (house.StyleFirst != 1 || house.StyleLast != 5) p.Add("STRUCTURE_HOUSE_STYLES");
            }
            if (!AlphaStructureTemplates.TryGet(AlphaStructureTemplates.DungeonRoomId, out var room)) p.Add("STRUCTURE_DUNGEON_MISSING");
            else if (room.Width != 3 || room.Depth != 3 || room.Height != 3) p.Add("STRUCTURE_DUNGEON_SIZE");

            var templates = new List<string> { AlphaStructureTemplates.HouseId };
            var zoneProblems = new List<string>();
            var room1 = AlphaStructurePlacementPlanner.PlanZone(7u, AlphaWorldScale.StartZoneX, AlphaWorldScale.StartZoneZ,
                templates, (x, z, w, d) => true, zoneProblems);
            var room2 = AlphaStructurePlacementPlanner.PlanZone(7u, AlphaWorldScale.StartZoneX, AlphaWorldScale.StartZoneZ,
                templates, (x, z, w, d) => true, new List<string>());
            if (room1.Count != 1 || room2.Count != 1) p.Add("STRUCTURE_ZONE_PLAN_MISSING");
            if (room1.Count == 1 && room2.Count == 1 && (room1[0].X != room2[0].X || room1[0].StableId != room2[0].StableId))
                p.Add("STRUCTURE_ZONE_PLAN_NON_DETERMINISTIC");
            int zoneOrigin = AlphaWorldScale.ZoneOriginVoxel(AlphaWorldScale.StartZoneX);
            if (room1.Count == 1 &&
                (room1[0].X < zoneOrigin || room1[0].Z < zoneOrigin ||
                 room1[0].X + room1[0].Width > zoneOrigin + AlphaWorldScale.ZoneVoxels ||
                 room1[0].Z + room1[0].Depth > zoneOrigin + AlphaWorldScale.ZoneVoxels))
                p.Add($"STRUCTURE_ZONE_PLAN_OUTSIDE_ZONE {room1[0].X},{room1[0].Z}");
            var blockedProblems = new List<string>();
            var blocked = AlphaStructurePlacementPlanner.PlanZone(7u, AlphaWorldScale.StartZoneX, AlphaWorldScale.StartZoneZ,
                templates, (x, z, w, d) => false, blockedProblems);
            if (blocked.Count != 0) p.Add("STRUCTURE_ZONE_PLAN_FORCED");
            if (!blockedProblems.Contains($"STRUCTURE_NO_FREE_FOOTPRINT z:{AlphaWorldScale.StartZoneX}:{AlphaWorldScale.StartZoneZ} " +
                                         AlphaStructureTemplates.HouseId))
                p.Add("STRUCTURE_ZONE_PLAN_MUST_REPORT_BLOCKED");
            return p;
        }
    }
}
