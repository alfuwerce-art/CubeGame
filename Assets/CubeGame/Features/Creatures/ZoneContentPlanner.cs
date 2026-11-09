// CubeGame.Features.Creatures — turns one world zone into concrete content (D86 M0-02/M0-06/M0-07).
//
// This is the composition layer between the (Claude-owned) world facts and the (DeepSeek-owned) content
// rules: W-18 vegetation selection, structure templates, the creature spawn planner and the surface
// heights the world reports. It is pure and deterministic — same seed + same source → same plan, no
// Unity calls, no shared state — so a zone can be planned twice (unload/reload) with identical ids.
//
// Honest statuses carried on every placement:
//   * selection: TRANSLATED_FROM_CODE (W-18 tree 0x5F56C0, not live-verified);
//   * position/density: CUBEGAME_CONTRACT (Alpha's placement policy is not recovered, see
//     docs/reports/D86_W18_ADDRESS_VERIFICATION.md);
//   * surface height: whatever the world reports for that column (no invented terrain);
//   * removed objects: never re-placed.
using System;
using System.Collections.Generic;
using CubeGame.AlphaModels;
using CubeGame.Content;

namespace CubeGame.Features.Creatures
{
    /// <summary>What a zone is allowed to contain. The caller decides; nothing here is invented silently.</summary>
    public sealed class ContentPlanSettings
    {
        /// <summary>Staged passive Alpha object kinds used by the W-18 climate selection.</summary>
        public IReadOnlyList<int> StagedObjectKinds = Array.Empty<int>();

        /// <summary>Staged fightable creature kinds (monsters, NPC factions).</summary>
        public IReadOnlyList<int> CreatureKinds = Array.Empty<int>();

        /// <summary>Staged passive kinds used as explicit CubeGame decor when W-18 selects nothing staged.</summary>
        public IReadOnlyList<int> DecorKinds = Array.Empty<int>();

        public IReadOnlyList<string> StructureTemplates = new[] { AlphaStructureTemplates.HouseId };

        public int VegetationPerZone = AlphaVegetationPlanner.MaxObjectsPerZone;
        public int CreatureSlots = 3;
        public int DecorSlots = 4;
        public bool PlaceStructures = true;

        /// <summary>
        /// Derive the three kind lists from an Alpha actor library: staged passive objects feed W-18 and decor,
        /// the rest become creatures. Keeps the game from passing kinds whose visuals were never prepared.
        /// </summary>
        public static ContentPlanSettings FromLibrary(AlphaActorLibrary library)
        {
            var settings = new ContentPlanSettings();
            if (library == null) return settings;
            var passive = new List<int>();
            var combat = new List<int>();
            foreach (var kind in CreatureVisualCatalog.AvailableKinds(library))
                (CubeGame.Features.Creatures.CreatureKinds.IsPassiveObject(kind) ? passive : combat).Add(kind);
            settings.StagedObjectKinds = passive;
            settings.DecorKinds = passive;
            settings.CreatureKinds = combat;
            return settings;
        }
    }

    /// <summary>Deterministic zone → content planner (no Unity, no side effects).</summary>
    public static class ZoneContentPlanner
    {
        public const string PositionProvenance =
            "CUBEGAME_CONTRACT: zone-scoped deterministic positions, Alpha placement policy not recovered";

        public const string DecorProvenance =
            "STAGED_PASSIVE_FALLBACK_NOT_ALPHA: staged Alpha object used as decor because W-18 selected nothing staged";

        public static ContentPlan Plan(WorldZoneKey zone, IContentZoneSource source, ContentPlanSettings settings)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var plan = new ContentPlan(zone);
            var problems = plan.Problems;
            if (!AlphaWorldScale.IsInsideWorld(zone))
            {
                problems.Add($"CONTENT_ZONE_OUTSIDE_WORLD {zone}");
                return plan;
            }

            ZoneClimate climate;
            try
            {
                climate = source.ClimateOf(AlphaWorldScale.CellOf(zone));
            }
            catch (Exception exception)
            {
                problems.Add($"CONTENT_CLIMATE_UNAVAILABLE {AlphaWorldScale.CellOf(zone)} {exception.GetType().Name}");
                return plan;
            }

            if (settings == null) settings = new ContentPlanSettings();
            uint seed = source.Seed;
            var ids = new HashSet<string>(StringComparer.Ordinal);

            // 1. Vegetation / passive objects: W-18 selection, zone-scoped positions.
            if (settings.VegetationPerZone > 0 && settings.StagedObjectKinds.Count > 0)
            {
                var vegetation = AlphaVegetationPlanner.PlanZone(seed, zone.X, zone.Z,
                    climate.Humidity, climate.Temperature, climate.CellValue, climate.Spacing,
                    climate.Level, climate.Mode, settings.StagedObjectKinds, problems, settings.VegetationPerZone);
                foreach (var placement in vegetation)
                {
                    var item = new ContentPlacement
                    {
                        StableId = placement.StableId,
                        Kind = ContentPlacementKind.Vegetation,
                        Species = placement.Kind,
                        Zone = zone,
                        X = placement.X,
                        Z = placement.Z,
                        // The W-18 table selects Alpha object/creature kinds; the faction is the catalog's, not a guess.
                        Faction = CreatureKinds.FactionOf(placement.Kind),
                        Level = climate.Level,
                        Provenance = placement.Source,
                    };
                    Add(plan, ids, source, item);
                }
            }
            else if (settings.VegetationPerZone > 0)
            {
                problems.Add("CONTENT_NO_STAGED_OBJECT_KINDS");
            }

            // 2. Explicit CubeGame decor (only when the caller opts in with a staged set).
            if (settings.DecorSlots > 0 && settings.DecorKinds.Count > 0 &&
                plan.VegetationCount == 0 && settings.VegetationPerZone > 0)
            {
                var decor = CreatureSpawnPlanner.PlanZone(seed ^ 0xDEC0u, zone.X, zone.Z, settings.DecorSlots,
                    settings.DecorKinds, CreatureSpawnPlanner.DecorSlotOffset, problems);
                foreach (var request in decor) AddCreature(plan, ids, source, zone, request, climate, DecorProvenance);
            }

            // 3. Creatures / NPCs.
            if (settings.CreatureSlots > 0 && settings.CreatureKinds.Count > 0)
            {
                var creatures = CreatureSpawnPlanner.PlanZone(seed, zone.X, zone.Z, settings.CreatureSlots,
                    settings.CreatureKinds, 0, problems);
                foreach (var request in creatures)
                    AddCreature(plan, ids, source, zone, request, climate, PositionProvenance);
            }
            else if (settings.CreatureSlots > 0)
            {
                problems.Add("CONTENT_NO_CREATURE_KINDS");
            }

            // 4. Structures: template candidate, only where the world says the footprint is usable.
            if (settings.PlaceStructures && settings.StructureTemplates != null && settings.StructureTemplates.Count > 0)
            {
                var structures = AlphaStructurePlacementPlanner.PlanZone(seed, zone.X, zone.Z,
                    settings.StructureTemplates, source.IsFlatAndFree, problems);
                foreach (var structure in structures)
                {
                    var item = new ContentPlacement
                    {
                        StableId = structure.StableId,
                        Kind = ContentPlacementKind.Structure,
                        TemplateId = structure.TemplateId,
                        Zone = zone,
                        X = structure.X + structure.Width * 0.5,
                        Z = structure.Z + structure.Depth * 0.5,
                        Yaw = structure.Rotation * 90f,
                        Width = structure.Width,
                        Depth = structure.Depth,
                        Height = structure.Height,
                        Provenance = "CUBEGAME_CONTRACT: template size from W-17 evidence, placement predicate from the world",
                    };
                    Add(plan, ids, source, item);
                }
            }

            return plan;
        }

        static void AddCreature(ContentPlan plan, HashSet<string> ids, IContentZoneSource source, WorldZoneKey zone,
            CreatureSpawnRequest request, ZoneClimate climate, string provenance)
        {
            bool passive = request.Faction == (int)CreatureFaction.Passive;
            var item = new ContentPlacement
            {
                StableId = request.StableId,
                Kind = passive ? ContentPlacementKind.Vegetation : ContentPlacementKind.Creature,
                Species = request.Kind,
                Zone = zone,
                X = request.X,
                Z = request.Z,
                Faction = request.Faction,
                Level = request.Level,
                Stars = request.Stars,
                Provenance = provenance,
            };
            Add(plan, ids, source, item);
        }

        static void Add(ContentPlan plan, HashSet<string> ids, IContentZoneSource source, ContentPlacement item)
        {
            if (!ids.Add(item.StableId))
            {
                plan.Problems.Add($"CONTENT_DUPLICATE_ID {item.StableId}");
                return;
            }
            if (source.IsRemoved(item.StableId))
            {
                plan.Problems.Add($"CONTENT_REMOVED {item.StableId}");
                ids.Remove(item.StableId);
                return;
            }
            int x = (int)Math.Floor(item.X);
            int z = (int)Math.Floor(item.Z);
            if (!source.TrySurfaceVoxel(x, z, out int surfaceY))
            {
                plan.Problems.Add($"CONTENT_NO_SURFACE {item.StableId} {x}:{z}");
                ids.Remove(item.StableId);
                return;
            }
            item.SurfaceY = surfaceY;
            plan.Items.Add(item);
        }
    }
}
