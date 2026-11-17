// CubeGame.Gameplay.Streaming — G2-1: runtime binding of the CubeGame content foundation.
//
// The providers decide what the world contains; this component is the gameplay side of that content:
//   * authoritative state of structures (generated / loaded / destroyed) derived from the zone content and the
//     world delta, so a destroyed structure never comes back after unload/reload or save/load;
//   * vegetation removal through the same delta (channel 8), used by the chunk vegetation mesh;
//   * the dungeon prototype session: find the entrance, enter the test room, exit back to the same entrance;
//   * the facts the debug overlay shows (biome, vegetation policy, candidates/accepted, structures, settlement,
//     dungeon, entity definition and visual recipe of nearby spawns).
using System;
using System.Collections.Generic;
using CubeGame.Content;
using CubeGame.Features.Contracts;
using CubeGame.Features.World;
using CubeGame.Features.World.Foundation;
using ContentStates = CubeGame.Features.World.Foundation.StructureRuntimeState;

namespace CubeGame.Gameplay.Streaming
{
    /// <summary>What the runtime knows about one placed structure.</summary>
    public sealed class ContentStructureState
    {
        public string PersistentId = string.Empty;
        public string RuleId = string.Empty;
        public StructureKind Kind;
        public int ZoneX;
        public int ZoneZ;
        public int X;
        public int Y;
        public int Z;
        public int SpawnIndex = -1;
        public string State = ContentStates.Generated;
        public bool Loaded;

        public override string ToString() =>
            $"{Kind} {PersistentId} ({X},{Y},{Z}) {State}{(Loaded ? " loaded" : " unloaded")}";
    }

    /// <summary>Runtime owner of the CubeGame content state of the loaded world.</summary>
    public sealed class CubeGameContentRuntime
    {
        private readonly AlphaTerrainGenerator _terrain;
        private readonly WorldDeltaStore _deltas;
        private readonly EntityDefinitionRegistry _entities;
        private readonly BiomeCatalog _biomes;
        private readonly HashSet<string> _destroyed = new HashSet<string>(StringComparer.Ordinal);

        public CubeGameContentRuntime(AlphaTerrainGenerator terrain, WorldDeltaStore deltas,
            EntityDefinitionRegistry entities, BiomeCatalog biomes = null)
        {
            _terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
            _deltas = deltas ?? throw new ArgumentNullException(nameof(deltas));
            _entities = entities;
            _biomes = biomes ?? BiomeCatalog.Default();
        }

        public bool IsCubeGameWorld => AlphaTerrainGenerator.UsesCubeGameContent(_terrain.GeneratorId);

        /// <summary>Dungeon instance the hero is currently inside (empty = in the open world).</summary>
        public string InsideDungeonInstanceId { get; private set; } = string.Empty;

        public string ActiveDungeonInstanceId => InsideDungeonInstanceId;

        public WorldDeltaStore Deltas => _deltas;

        /// <summary>
        /// Structures of a zone with their authoritative state. <paramref name="allowGenerate"/> = false only reads
        /// already generated zones (the overlay must never trigger generation).
        /// </summary>
        public List<ContentStructureState> StructuresOfZone(ZoneKey zone, bool allowGenerate)
        {
            var result = new List<ContentStructureState>();
            AlphaZone content = ZoneContent(zone, allowGenerate);
            if (content == null) return result;
            foreach (StructurePlacement placement in content.Structures)
            {
                result.Add(ToState(zone, content, placement));
            }

            if (content.Settlement != null)
            {
                int settlementIndex = SpawnIndexOf(content, content.Settlement.PersistentId);
                result.Add(new ContentStructureState
                {
                    PersistentId = content.Settlement.PersistentId,
                    RuleId = content.Settlement.PersistentId,
                    Kind = StructureKind.Village,
                    ZoneX = zone.X,
                    ZoneZ = zone.Z,
                    X = content.Settlement.CenterX,
                    Y = content.Settlement.SurfaceY + 1,
                    Z = content.Settlement.CenterZ,
                    SpawnIndex = settlementIndex,
                    State = IsRemoved(zone, settlementIndex) ? ContentStates.Destroyed : content.Settlement.State,
                    Loaded = true,
                });
            }

            if (content.Dungeon != null)
            {
                int portalIndex = SpawnIndexOf(content, content.Dungeon.PortalId);
                result.Add(new ContentStructureState
                {
                    PersistentId = content.Dungeon.PortalId,
                    RuleId = "structure.dungeon_entrance",
                    Kind = StructureKind.DungeonEntrance,
                    ZoneX = zone.X,
                    ZoneZ = zone.Z,
                    X = content.Dungeon.EntranceX,
                    Y = content.Dungeon.EntranceY,
                    Z = content.Dungeon.EntranceZ,
                    SpawnIndex = portalIndex,
                    State = IsRemoved(zone, portalIndex) ? ContentStates.Destroyed : content.Dungeon.State,
                    Loaded = true,
                });
            }

            return result;
        }

        /// <summary>Spawn index of a prototype object (settlement marker, portal) from the zone's spawn records.</summary>
        private static int SpawnIndexOf(AlphaZone content, string persistentId)
        {
            foreach (SpawnDefinition spawn in content.StructureSpawns)
            {
                if (string.Equals(spawn.PersistentObjectId, persistentId, StringComparison.Ordinal))
                {
                    return spawn.SpawnIndex;
                }
            }

            return -1;
        }

        public List<ContentStructureState> StructuresAround(ZoneKey centre, int radiusZones, bool allowGenerate)
        {
            var result = new List<ContentStructureState>();
            for (int dx = -radiusZones; dx <= radiusZones; dx++)
            {
                for (int dz = -radiusZones; dz <= radiusZones; dz++)
                {
                    var zone = new ZoneKey(centre.X + dx, centre.Z + dz);
                    if (!WorldLayout.IsInside(zone)) continue;
                    result.AddRange(StructuresOfZone(zone, allowGenerate));
                }
            }

            return result;
        }

        /// <summary>Nearest structure to a world position (generates the searched zones: explicit player action).</summary>
        public bool TryNearestStructure(double x, double z, int radiusZones, out ContentStructureState nearest)
        {
            nearest = null;
            ZoneKey centre = WorldLayout.ZoneOf((int)Math.Floor(x), (int)Math.Floor(z));
            double best = double.MaxValue;
            foreach (ContentStructureState state in StructuresAround(centre, radiusZones, true))
            {
                if (state.State == ContentStates.Destroyed) continue;
                double dx = state.X - x;
                double dz = state.Z - z;
                double distance = dx * dx + dz * dz;
                if (distance >= best) continue;
                best = distance;
                nearest = state;
            }

            return nearest != null;
        }

        /// <summary>Destroys a structure: the delta is the save, the runtime state is derived from it.</summary>
        public bool DestroyStructure(ContentStructureState state, long timeMs)
        {
            if (state == null || !IsCubeGameWorld) return false;
            if (state.SpawnIndex < 0)
            {
                _destroyed.Add(state.PersistentId);
                return true;
            }

            var zone = new ZoneKey(state.ZoneX, state.ZoneZ);
            _deltas.Record(new WorldDelta
            {
                Region = new RegionKey(zone.X >> 6, zone.Z >> 6),
                Kind = WorldDeltaKind.ObjectRemoved,
                X = zone.X,
                Y = CubeGameContentIds.StructureChannel * 4096 + state.SpawnIndex,
                Z = zone.Z,
                Value = 1,
                TimeMs = timeMs,
            });
            _destroyed.Add(state.PersistentId);
            return true;
        }

        /// <summary>True when the structure is gone for good (delta or the prototype's own removal).</summary>
        public bool IsStructureRemoved(ZoneKey zone, StructurePlacement placement) =>
            placement == null || _destroyed.Contains(placement.PersistentId) ||
            (placement.SpawnIndex >= 0 && _deltas.IsObjectRemoved(zone, CubeGameContentIds.StructureChannel,
                placement.SpawnIndex));

        public bool IsVegetationRemoved(ZoneKey zone, int stableIndex) =>
            stableIndex >= 0 && _deltas.IsObjectRemoved(zone, CubeGameContentIds.VegetationChannel, stableIndex);

        /// <summary>Structure removal recorded on the structure spawn channel (settlement/portal markers use −1).</summary>
        private bool IsRemoved(ZoneKey zone, int spawnIndex) =>
            spawnIndex >= 0 &&
            _deltas.IsObjectRemoved(zone, CubeGameContentIds.StructureChannel, spawnIndex);

        /// <summary>Removes one CubeGame plant by identity (the delta is the state; the mesh stops drawing it).</summary>
        public bool RemoveVegetation(ZoneKey zone, VegetationPlacement placement, long timeMs)
        {
            if (placement == null || !IsCubeGameWorld) return false;
            _deltas.Record(new WorldDelta
            {
                Region = new RegionKey(zone.X >> 6, zone.Z >> 6),
                Kind = WorldDeltaKind.ObjectRemoved,
                X = zone.X,
                Y = CubeGameContentIds.VegetationChannel * 4096 + placement.StableIndex,
                Z = zone.Z,
                Value = 1,
                TimeMs = timeMs,
            });
            return true;
        }

        /// <summary>Removes the nearest CubeGame plant to a position (dev tool / smoke scenario).</summary>
        public bool RemoveVegetationNear(double x, double z, double radiusVoxels, long timeMs,
            out VegetationPlacement removed)
        {
            removed = null;
            ZoneKey zone = WorldLayout.ZoneOf((int)Math.Floor(x), (int)Math.Floor(z));
            AlphaZone content = ZoneContent(zone, true);
            if (content == null) return false;
            double best = radiusVoxels * radiusVoxels;
            foreach (VegetationPlacement placement in content.Vegetation)
            {
                if (IsVegetationRemoved(zone, placement.StableIndex)) continue;
                double dx = placement.X - x;
                double dz = placement.Z - z;
                double distance = dx * dx + dz * dz;
                if (distance > best) continue;
                best = distance;
                removed = placement;
            }

            if (removed == null) return false;
            _deltas.Record(new WorldDelta
            {
                Region = new RegionKey(zone.X >> 6, zone.Z >> 6),
                Kind = WorldDeltaKind.ObjectRemoved,
                X = zone.X,
                Y = CubeGameContentIds.VegetationChannel * 4096 + removed.StableIndex,
                Z = zone.Z,
                Value = 1,
                TimeMs = timeMs,
            });
            return true;
        }

        /// <summary>Content objects of a zone that the delta marks as removed (structures, anchors, portals).</summary>
        public int RemovedStructuresIn(ZoneKey zone)
        {
            AlphaZone content = ZoneContent(zone, true);
            if (content == null) return 0;
            int removed = 0;
            foreach (SpawnDefinition spawn in content.StructureSpawns)
            {
                if (_deltas.IsObjectRemoved(zone, CubeGameContentIds.StructureChannel, spawn.SpawnIndex)) removed++;
            }

            return removed;
        }

        /// <summary>Vegetation objects of a zone that the delta marks as removed.</summary>
        public int RemovedVegetationIn(ZoneKey zone)
        {
            AlphaZone content = ZoneContent(zone, true);
            if (content == null) return 0;
            int removed = 0;
            foreach (VegetationPlacement placement in content.Vegetation)
            {
                if (IsVegetationRemoved(zone, placement.StableIndex)) removed++;
            }

            return removed;
        }

        /// <summary>Settlement site nearest to a position (region-level reservation, no zone generation needed).</summary>
        public bool TryFindSettlement(double x, double z, int radiusRegions, out SettlementBlueprint settlement,
            out ZoneKey zone)
        {
            settlement = null;
            zone = default;
            if (!IsCubeGameWorld) return false;
            int centreX = AlphaWorldScale.RegionOfVoxel((int)Math.Floor(x));
            int centreZ = AlphaWorldScale.RegionOfVoxel((int)Math.Floor(z));
            var probe = new TerrainSeamProbe(_terrain, _biomes);
            var rules = StructureRuleSet.Default();
            double best = double.MaxValue;
            for (int rx = centreX - radiusRegions; rx <= centreX + radiusRegions; rx++)
            {
                for (int rz = centreZ - radiusRegions; rz <= centreZ + radiusRegions; rz++)
                {
                    SettlementSite site = SettlementSeamPlanner.Plan(_terrain.MasterSeed, rx, rz, probe, rules);
                    if (site == null) continue;
                    double dx = site.CenterX - x;
                    double dz = site.CenterZ - z;
                    double distance = dx * dx + dz * dz;
                    if (distance >= best) continue;
                    best = distance;
                    settlement = SettlementBlueprint.FromSite(site, probe.SurfaceAt);
                    zone = new ZoneKey(site.ZoneX, site.ZoneZ);
                }
            }

            return settlement != null;
        }

        /// <summary>
        /// Dungeon prototype nearest to a position. Entrances are reserved per region block, so the search is over
        /// blocks (each block is 384×384 zones); only the winning entrance zone is generated.
        /// </summary>
        public bool TryFindDungeon(double x, double z, int radiusBlocks, out DungeonPrototype dungeon, out ZoneKey zone)
        {
            dungeon = null;
            zone = default;
            if (!IsCubeGameWorld) return false;
            int centreRegionX = AlphaWorldScale.RegionOfVoxel((int)Math.Floor(x));
            int centreRegionZ = AlphaWorldScale.RegionOfVoxel((int)Math.Floor(z));
            int centreBlockX = centreRegionX / DungeonSeamPlanner.RegionsPerBlock;
            int centreBlockZ = centreRegionZ / DungeonSeamPlanner.RegionsPerBlock;
            double best = double.MaxValue;
            ZoneKey bestZone = default;
            for (int bx = centreBlockX - radiusBlocks; bx <= centreBlockX + radiusBlocks; bx++)
            {
                for (int bz = centreBlockZ - radiusBlocks; bz <= centreBlockZ + radiusBlocks; bz++)
                {
                    DungeonSeamPlanner.EntranceZone(unchecked((int)_terrain.MasterSeed), bx, bz, out int zoneX,
                        out int zoneZ);
                    var candidate = new ZoneKey(zoneX, zoneZ);
                    if (!WorldLayout.IsInside(candidate)) continue;
                    double cx = (zoneX + 0.5) * WorldLayout.ZoneSize;
                    double cz = (zoneZ + 0.5) * WorldLayout.ZoneSize;
                    double dx = cx - x;
                    double dz = cz - z;
                    double distance = dx * dx + dz * dz;
                    if (distance >= best) continue;
                    best = distance;
                    bestZone = candidate;
                }
            }

            if (best == double.MaxValue) return false;
            AlphaZone content = ZoneContent(bestZone, true);
            if (content?.Dungeon == null) return false;
            dungeon = content.Dungeon;
            zone = bestZone;
            return true;
        }

        /// <summary>Moves the hero into the test room. Returns false outside a CubeGame world or without a dungeon.</summary>
        public bool EnterDungeon(DungeonPrototype dungeon, out double x, out double y, out double z)
        {
            x = y = z = 0;
            if (dungeon == null || !IsCubeGameWorld) return false;
            x = dungeon.CenterX + 0.5;
            y = dungeon.CenterY + 0.2;
            z = dungeon.CenterZ + 0.5;
            InsideDungeonInstanceId = dungeon.InstanceId;
            return true;
        }

        /// <summary>Returns the hero to the same entrance that led into the instance.</summary>
        public bool ExitDungeon(DungeonPrototype dungeon, out double x, out double y, out double z)
        {
            x = y = z = 0;
            if (dungeon == null || string.IsNullOrEmpty(InsideDungeonInstanceId)) return false;
            x = dungeon.ReturnX + 0.5;
            y = dungeon.ReturnY + 0.5;
            z = dungeon.ReturnZ + 0.5;
            InsideDungeonInstanceId = string.Empty;
            return true;
        }

        /// <summary>Debug lines for the overlay: biome, vegetation policy, structures, settlement, dungeon, recipes.</summary>
        public List<string> DebugLines(double x, double z)
        {
            var lines = new List<string>();
            if (!IsCubeGameWorld)
            {
                lines.Add("content: legacy Alpha world (CubeGame content stage not active)");
                return lines;
            }

            ZoneKey zone = WorldLayout.ZoneOf((int)Math.Floor(x), (int)Math.Floor(z));
            AlphaZone content = ZoneContent(zone, false);
            if (content == null)
            {
                lines.Add($"content zone({zone.X},{zone.Z}): not generated yet");
                return lines;
            }

            int accepted = content.Vegetation.Count;
            int removed = 0;
            foreach (VegetationPlacement placement in content.Vegetation)
            {
                if (IsVegetationRemoved(zone, placement.StableIndex)) removed++;
            }

            lines.Add($"content zone({zone.X},{zone.Z}) biome={content.BiomeId} " +
                      $"vegProfile={content.VegetationProfileId} rules={content.VegetationRuleSetId} " +
                      $"candidates={content.VegetationCandidates} accepted={accepted} removed={removed}");
            lines.Add($"structures={content.Structures.Count} rejected={content.StructureRejections} " +
                      $"spawns={content.StructureSpawns.Count}");
            foreach (VegetationPlacement placement in content.Vegetation)
            {
                if (removed > 0 && IsVegetationRemoved(zone, placement.StableIndex)) continue;
                lines.Add($"veg {placement.Kind}→{placement.Model} {placement.PersistentId}");
                break;
            }

            foreach (StructurePlacement placement in content.Structures)
            {
                string state = IsStructureRemoved(zone, placement) ? ContentStates.Destroyed
                    : placement.State;
                lines.Add($"structure {placement.Kind} {placement.PersistentId} {state}");
                break;
            }

            if (content.Settlement != null)
            {
                lines.Add($"settlement {content.Settlement.PersistentId} slots={content.Settlement.Slots.Count} " +
                          $"anchors={content.Settlement.Anchors.Count} roads={content.Settlement.RoadCells.Count}");
                foreach (SpawnDefinition spawn in content.StructureSpawns)
                {
                    if (!CubeGameContentIds.IsAnchor(spawn.PersistentObjectId)) continue;
                    string recipe = ResolveRecipe(spawn.EntityDefinitionId);
                    lines.Add($"anchor {spawn.EntityDefinitionId} recipe={recipe} {spawn.PersistentObjectId}");
                    break;
                }
            }

            if (content.Dungeon != null)
            {
                lines.Add($"dungeon {content.Dungeon.InstanceId} entrance=({content.Dungeon.EntranceX}," +
                          $"{content.Dungeon.EntranceY},{content.Dungeon.EntranceZ}) lvl={content.Dungeon.Level} " +
                          $"inside={InsideDungeonInstanceId}");
            }

            foreach (string problem in content.StructureProblems)
            {
                lines.Add("! " + problem);
            }

            return lines;
        }

        private string ResolveRecipe(string definitionId)
        {
            if (string.IsNullOrEmpty(definitionId) || _entities == null) return "(none)";
            return _entities.TryGet(definitionId, out EntityDefinition definition)
                ? definition.Visual?.Id ?? "(none)"
                : "(missing definition)";
        }

        private ContentStructureState ToState(ZoneKey zone, AlphaZone content, StructurePlacement placement)
        {
            bool removed = IsStructureRemoved(zone, placement);
            return new ContentStructureState
            {
                PersistentId = placement.PersistentId,
                RuleId = placement.RuleId,
                Kind = placement.Kind,
                ZoneX = zone.X,
                ZoneZ = zone.Z,
                X = placement.X,
                Y = placement.SurfaceY + 1,
                Z = placement.Z,
                SpawnIndex = placement.SpawnIndex,
                State = removed ? ContentStates.Destroyed : placement.State,
                Loaded = true,
            };
        }

        private AlphaZone ZoneContent(ZoneKey zone, bool allowGenerate)
        {
            if (!WorldLayout.IsInside(zone)) return null;
            if (allowGenerate) return _terrain.Zone(zone.X, zone.Z);
            return _terrain.TryGetGeneratedZone(zone.X, zone.Z, out AlphaZone content) ? content : null;
        }

        /// <summary>Seam probe over the generator (used by settlement lookup before a zone exists).</summary>
        private sealed class TerrainSeamProbe : IWorldSeamProbe
        {
            private readonly AlphaTerrainGenerator _terrain;
            private readonly BiomeCatalog _biomes;

            public TerrainSeamProbe(AlphaTerrainGenerator terrain, BiomeCatalog biomes)
            {
                _terrain = terrain;
                _biomes = biomes;
            }

            public string BiomeOf(int x, int z)
            {
                if (!WorldLayout.IsInsideVoxels(x, z)) return string.Empty;
                return _biomes.Resolve(new ClimateAxes(_terrain.Temperature(x, z), _terrain.Humidity(x, z),
                    _terrain.RareCellTerm(x, z), _terrain.Mountain(x, z),
                    AlphaTerrainGenerator.Level(x >> 14, z >> 14), _terrain.Column(x, z).Surface,
                    _terrain.WaterLevel))?.Id ?? string.Empty;
            }

            public int SurfaceAt(int x, int z) =>
                WorldLayout.IsInsideVoxels(x, z) ? _terrain.Column(x, z).Surface : 0;

            public int RegionLevel(int regionX, int regionZ) => AlphaTerrainGenerator.Level(regionX, regionZ);
        }
    }
}
