// CubeGame.Features.World.Foundation — G2-1B: CubeGame owns structure placement and the settlement/dungeon prototypes.
//
// The Alpha content provider still exists for old worlds; a CubeGame content world (generator id .../10) runs this
// provider instead of it. For every zone:
//
//     StructurePlacementPipeline candidates -> validation -> prototype geometry -> persistent id -> spawn records
//     SettlementSite -> SettlementBlueprint (area, building slots, road stub, NPC/service anchors)
//     DungeonEntranceSite -> DungeonPrototype (entrance + carved test room + return portal)
//
// Geometry is a prototype (platforms, posts, cairns): the point is that the world owns the content and that every
// object has a stable identity the runtime, the save and the debug overlay can agree on.
using System;
using System.Collections.Generic;
using CubeGame.Content;
using CubeGame.Core.Generation;
using CubeGame.Features.Contracts;

namespace CubeGame.Features.World.Foundation
{
    /// <summary>Prototype geometry writes shared by all structure kinds (all coordinates are absolute voxels).</summary>
    internal static class CubeGameStructureGeometry
    {
        internal static readonly uint Stone = Voxel.Pack(96, 96, 104, 6);
        internal static readonly uint Marker = Voxel.Pack(208, 96, 48, 6);
        internal static readonly uint Path = Voxel.Pack(128, 104, 72, 6);
        internal static readonly uint Wood = Voxel.Pack(104, 72, 40, 6);

        /// <summary>Levels a square platform to the highest column of the footprint and caps it.</summary>
        internal static void Platform(ZoneContentContext context, int centerX, int centerZ, int radius, uint cap)
        {
            int top = Top(context, centerX, centerZ);
            if (top <= 0) return;
            for (int x = centerX - radius; x <= centerX + radius; x++)
            {
                for (int z = centerZ - radius; z <= centerZ + radius; z++)
                {
                    int columnTop = Top(context, x, z);
                    if (columnTop <= 0) continue;
                    int level = Math.Max(columnTop, top);
                    for (int y = columnTop + 1; y < level; y++) context.Write(x, y, z, Stone);
                    context.Write(x, level, z, cap);
                }
            }
        }

        /// <summary>Vertical post of the given height standing on the column's surface.</summary>
        internal static void Post(ZoneContentContext context, int x, int z, int height, uint voxel)
        {
            int top = Top(context, x, z);
            if (top <= 0) return;
            for (int i = 1; i <= Math.Max(1, height); i++) context.Write(x, top + i, z, voxel);
        }

        /// <summary>Flat marker on the ground (path/plaza voxel).</summary>
        internal static void GroundMark(ZoneContentContext context, int x, int z, uint voxel)
        {
            int top = Top(context, x, z);
            if (top <= 0) return;
            context.Write(x, top, z, voxel);
        }

        private static int Top(ZoneContentContext context, int x, int z) =>
            context.Contains(x, z) ? context.Zone.Column(x, z).TopSolid() : 0;
    }

    /// <summary>CubeGame structure stage of zone generation.</summary>
    public sealed class CubeGameStructureProvider : IZoneContentProvider
    {
        public const string ProviderKey = "cubegame.structures.v1";

        /// <summary>First spawn index reserved for settlement anchors (structures use 0…2047).</summary>
        public const int AnchorIndexBase = 2048;

        private readonly BiomeCatalog _biomes;
        private readonly StructureRuleSet _rules;

        public CubeGameStructureProvider(BiomeCatalog biomes = null, StructureRuleSet rules = null)
        {
            _biomes = biomes ?? BiomeCatalog.Default();
            _rules = rules ?? StructureRuleSet.Default();
        }

        public string Key => ProviderKey;

        public ZoneStructureStats LastZoneStats { get; private set; }

        public void Populate(ZoneContentContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            int zoneX = context.Zone.Zx;
            int zoneZ = context.Zone.Zz;
            uint seed = context.Terrain.MasterSeed;
            var problems = new List<string>();
            var rejections = new List<StructureRejection>();
            var probe = new ZoneStructureProbe(context, _biomes);
            var used = new HashSet<int>();
            int spawnIndex = 0;

            // 1. Generic pipeline candidates (village/camp/ruin/tower/caves/landmark).
            var placements = StructurePlacementPipeline.PlanZone(_rules, _biomes, seed, zoneX, zoneZ,
                AlphaTerrainGenerator.Level(zoneX >> 6, zoneZ >> 6), probe, rejections, problems);
            foreach (StructurePlacement placement in placements)
            {
                // Villages/cities and dungeon entrances have dedicated prototypes below; the generic pipeline only
                // owns camps, ruins, towers, cave entrances and landmarks.
                if (placement.Kind == StructureKind.Village || placement.Kind == StructureKind.City ||
                    placement.Kind == StructureKind.DungeonEntrance)
                {
                    continue;
                }

                placement.SpawnIndex = spawnIndex++;
                placement.BiomeId = probe.BiomeOf(placement.X, placement.Z);
                BuildPrototype(context, placement, seed);
                context.Zone.Structures.Add(placement);
                context.Zone.StructureSpawns.Add(ToSpawn(placement, zoneX, zoneZ));
            }

            // 2. Settlement prototype: reserved area, building slots, one road stub, NPC/service anchors.
            SettlementBlueprint settlement = CubeGameContentPlans.SettlementForZone(seed, zoneX, zoneZ, probe, _rules,
                problems);
            if (settlement != null)
            {
                BuildSettlement(context, settlement, zoneX, zoneZ);
                int markerIndex = AnchorIndexBase + 1500;
                if (used.Add(markerIndex))
                {
                    context.Zone.StructureSpawns.Add(new SpawnDefinition
                    {
                        Region = new RegionKey(zoneX >> 6, zoneZ >> 6),
                        SpawnIndex = markerIndex,
                        Kind = new KindId(0),
                        Position = new WorldPosition(settlement.CenterX + 0.5, settlement.SurfaceY + 1,
                            settlement.CenterZ + 0.5),
                        Yaw = 0,
                        Faction = 6,
                        Level = settlement.Level,
                        BehaviourKey = AlphaZoneContentProviderKeys.StaticObject,
                        EntityDefinitionId = string.Empty,
                        PersistentObjectId = settlement.PersistentId,
                        Provenance = "CUBEGAME_NATIVE: settlement marker prototype",
                    });
                }

                foreach (NpcAnchorSpec anchor in settlement.Anchors)
                {
                    if (!CubeGameEntityCatalog.TryDefinitionForAnchor(anchor.Kind, out string definitionId)) continue;
                    int index = AnchorIndexBase + anchor.Index + (int)anchor.Kind * 8;
                    if (!used.Add(index))
                    {
                        problems.Add($"STRUCTURE_ANCHOR_INDEX_REUSED {anchor.PersistentId}");
                        continue;
                    }

                    context.Zone.StructureSpawns.Add(AnchorSpawn(seed, zoneX, zoneZ, anchor, definitionId, index));
                }
            }

            // 3. Dungeon prototype: entrance frame, carved test room, portal spawn.
            DungeonPrototype dungeon = CubeGameContentPlans.DungeonForZone(seed, zoneX, zoneZ, probe, problems);
            if (dungeon != null)
            {
                BuildDungeon(context, dungeon);
                int index = AnchorIndexBase + 1000;
                if (used.Add(index))
                {
                    context.Zone.StructureSpawns.Add(PortalSpawn(seed, zoneX, zoneZ, dungeon, index));
                }
            }

            // 4. Spawn profile hooks: the biome decides how many creatures the zone holds. Identity is CubeGame's
            // (creature.wild); the body is the temporary Alpha placeholder bound in CubeGameVisualBindings.
            BiomeDefinition biome = _biomes.Resolve(ZoneAxes(context, zoneX, zoneZ));
            string biomeId = biome?.Id ?? string.Empty;
            if (biome != null && _biomes.TryGetSpawn(biome.SpawnProfileId, out SpawnProfile spawnProfile))
            {
                AddCreatures(context, seed, zoneX, zoneZ, biomeId, spawnProfile, used);
            }

            context.Zone.BiomeId = biomeId;
            LastZoneStats = new ZoneStructureStats(zoneX, zoneZ, placements.Count, settlement, dungeon,
                rejections.Count, problems);
            context.Zone.StructureRejections = rejections.Count;
            context.Zone.StructureProblems = problems;
        }

        // ------------------------------------------------------------------ prototypes

        private static void BuildPrototype(ZoneContentContext context, StructurePlacement placement, uint seed)
        {
            switch (placement.Kind)
            {
                case StructureKind.Camp:
                    CubeGameStructureGeometry.Platform(context, placement.X, placement.Z, 2,
                        CubeGameStructureGeometry.Path);
                    CubeGameStructureGeometry.Post(context, placement.X - 2, placement.Z - 2, 2,
                        CubeGameStructureGeometry.Wood);
                    CubeGameStructureGeometry.Post(context, placement.X + 2, placement.Z - 2, 2,
                        CubeGameStructureGeometry.Wood);
                    CubeGameStructureGeometry.GroundMark(context, placement.X, placement.Z,
                        CubeGameStructureGeometry.Marker);
                    break;

                case StructureKind.Ruin:
                    for (int i = -2; i <= 2; i++)
                    {
                        CubeGameStructureGeometry.Post(context, placement.X + i, placement.Z - 2,
                            i == 0 ? 1 : 2, CubeGameStructureGeometry.Stone);
                        if (Math.Abs(i) == 2)
                        {
                            CubeGameStructureGeometry.Post(context, placement.X + i, placement.Z + 2, 2,
                                CubeGameStructureGeometry.Stone);
                        }
                    }

                    break;

                case StructureKind.Tower:
                    CubeGameStructureGeometry.Platform(context, placement.X, placement.Z, 2,
                        CubeGameStructureGeometry.Stone);
                    CubeGameStructureGeometry.Post(context, placement.X - 2, placement.Z - 2, 4,
                        CubeGameStructureGeometry.Stone);
                    CubeGameStructureGeometry.Post(context, placement.X + 2, placement.Z - 2, 4,
                        CubeGameStructureGeometry.Stone);
                    CubeGameStructureGeometry.Post(context, placement.X - 2, placement.Z + 2, 4,
                        CubeGameStructureGeometry.Stone);
                    CubeGameStructureGeometry.Post(context, placement.X + 2, placement.Z + 2, 4,
                        CubeGameStructureGeometry.Stone);
                    break;

                case StructureKind.CaveEntrance:
                    CubeGameStructureGeometry.Post(context, placement.X, placement.Z, 1,
                        CubeGameStructureGeometry.Stone);
                    CubeGameStructureGeometry.GroundMark(context, placement.X - 2, placement.Z,
                        CubeGameStructureGeometry.Marker);
                    break;

                case StructureKind.DungeonEntrance:
                    // The dedicated prototype below owns the actual dungeon; the pipeline candidate only marks the spot.
                    CubeGameStructureGeometry.GroundMark(context, placement.X, placement.Z,
                        CubeGameStructureGeometry.Marker);
                    break;

                default:
                    CubeGameStructureGeometry.Platform(context, placement.X, placement.Z, 1,
                        CubeGameStructureGeometry.Stone);
                    break;
            }

            _ = seed;
        }

        private static void BuildSettlement(ZoneContentContext context, SettlementBlueprint settlement, int zoneX,
            int zoneZ)
        {
            CubeGameStructureGeometry.Platform(context, settlement.CenterX, settlement.CenterZ, 5,
                CubeGameStructureGeometry.Path);
            foreach (SettlementBuildingSlot slot in settlement.Slots)
            {
                CubeGameStructureGeometry.Platform(context, slot.X, slot.Z, 2, CubeGameStructureGeometry.Stone);
                CubeGameStructureGeometry.Post(context, slot.X - 2, slot.Z - 2, 2,
                    CubeGameStructureGeometry.Wood);
                CubeGameStructureGeometry.Post(context, slot.X + 2, slot.Z - 2, 2,
                    CubeGameStructureGeometry.Wood);
            }

            foreach (SettlementRoadCell cell in settlement.RoadCells)
            {
                CubeGameStructureGeometry.GroundMark(context, cell.X, cell.Z, CubeGameStructureGeometry.Path);
            }

            CubeGameStructureGeometry.GroundMark(context, settlement.CenterX, settlement.CenterZ,
                CubeGameStructureGeometry.Marker);
            settlement.State = "generated";
            context.Zone.Settlement = settlement;
            _ = zoneX;
            _ = zoneZ;
        }

        private static void BuildDungeon(ZoneContentContext context, DungeonPrototype dungeon)
        {
            dungeon.Carve(context.Write, out string problem);
            // Entrance frame at the surface, so the hole is visible and the portal has a physical marker.
            CubeGameStructureGeometry.Post(context, dungeon.EntranceX - 2, dungeon.EntranceZ, 2,
                CubeGameStructureGeometry.Stone);
            CubeGameStructureGeometry.Post(context, dungeon.EntranceX + 2, dungeon.EntranceZ, 2,
                CubeGameStructureGeometry.Stone);
            CubeGameStructureGeometry.GroundMark(context, dungeon.EntranceX, dungeon.EntranceZ,
                CubeGameStructureGeometry.Marker);
            dungeon.State = "generated";
            dungeon.Problem = problem;
            context.Zone.Dungeon = dungeon;
        }

        // ------------------------------------------------------------------ spawn records

        /// <summary>Creature spawns decided by the biome's spawn profile (identity: creature.wild).</summary>
        private static void AddCreatures(ZoneContentContext context, uint seed, int zoneX, int zoneZ, string biomeId,
            SpawnProfile profile, HashSet<int> used)
        {
            int count = Math.Min(8, Math.Max(0, profile.MaxCreaturesPerZone));
            for (int i = 0; i < count; i++)
            {
                uint hash = DeterministicHash.Combine(unchecked((int)seed), zoneX, zoneZ, 0xC0DE, i);
                int lx = 16 + (int)(hash % (uint)(AlphaZone.Size - 32));
                int lz = 16 + (int)((hash >> 9) % (uint)(AlphaZone.Size - 32));
                int x = zoneX * AlphaZone.Size + lx;
                int z = zoneZ * AlphaZone.Size + lz;
                int top = context.Zone.Column(x, z).TopSolid();
                if (top <= 0) continue;
                int index = AnchorIndexBase + 2000 + i;
                if (!used.Add(index)) continue;
                int level = profile.MinLevel +
                            (int)((hash >> 17) % (uint)Math.Max(1, profile.MaxLevel - profile.MinLevel + 1));
                context.Zone.StructureSpawns.Add(new SpawnDefinition
                {
                    Region = new RegionKey(zoneX >> 6, zoneZ >> 6),
                    SpawnIndex = index,
                    Kind = new KindId(CubeGameVisualBindings.SpeciesOf(CubeGameEntityCatalog.CreatureWild)),
                    Position = new WorldPosition(x + 0.5, top + 1, z + 0.5),
                    Yaw = (hash >> 24) % 360,
                    Faction = 1,
                    Level = level,
                    BehaviourKey = AlphaZoneContentProviderKeys.CreatureAi,
                    EntityDefinitionId = CubeGameEntityCatalog.CreatureWild,
                    PersistentObjectId = $"creature:{seed}:{zoneX}:{zoneZ}:{i}",
                    Provenance = "CUBEGAME_NATIVE: spawn profile hook (" + biomeId + ")",
                });
            }
        }

        private static ClimateAxes ZoneAxes(ZoneContentContext context, int zoneX, int zoneZ)
        {
            int x = zoneX * AlphaZone.Size + AlphaZone.Size / 2;
            int z = zoneZ * AlphaZone.Size + AlphaZone.Size / 2;
            VoxelColumn column = context.Zone.Column(x, z);
            return new ClimateAxes(column.Temperature, column.Humidity, context.Terrain.RareCellTerm(x, z),
                context.Terrain.Mountain(x, z), AlphaTerrainGenerator.Level(zoneX >> 6, zoneZ >> 6),
                column.TopSolid(), context.Terrain.WaterLevel);
        }

        private static SpawnDefinition ToSpawn(StructurePlacement placement, int zoneX, int zoneZ)
        {
            return new SpawnDefinition
            {
                Region = new RegionKey(zoneX >> 6, zoneZ >> 6),
                SpawnIndex = placement.SpawnIndex,
                Kind = new KindId(0),
                Position = new WorldPosition(placement.X + 0.5, placement.SurfaceY + 1, placement.Z + 0.5),
                Yaw = placement.Rotation,
                Faction = 6,
                Level = 1,
                BehaviourKey = AlphaZoneContentProviderKeys.StaticObject,
                PersistentObjectId = placement.PersistentId,
                Provenance = "CUBEGAME_NATIVE: structure prototype marker",
            };
        }

        private static SpawnDefinition AnchorSpawn(uint seed, int zoneX, int zoneZ, NpcAnchorSpec anchor,
            string definitionId, int index)
        {
            _ = seed;
            int species = CubeGameVisualBindings.SpeciesOf(definitionId);
            return new SpawnDefinition
            {
                Region = new RegionKey(zoneX >> 6, zoneZ >> 6),
                SpawnIndex = index,
                Kind = new KindId(species),
                Position = new WorldPosition(anchor.X + 0.5, anchor.SurfaceY + 1, anchor.Z + 0.5),
                Yaw = 0,
                Faction = 5,
                Level = definitionId == CubeGameEntityCatalog.NpcMerchant ? 2 : 3,
                BehaviourKey = AlphaZoneContentProviderKeys.CreatureAi,
                EntityDefinitionId = definitionId,
                PersistentObjectId = anchor.PersistentId,
                Provenance = "CUBEGAME_NATIVE: settlement anchor",
            };
        }

        private static SpawnDefinition PortalSpawn(uint seed, int zoneX, int zoneZ, DungeonPrototype dungeon, int index)
        {
            return new SpawnDefinition
            {
                Region = new RegionKey(zoneX >> 6, zoneZ >> 6),
                SpawnIndex = index,
                Kind = new KindId(0),
                Position = new WorldPosition(dungeon.EntranceX + 0.5, dungeon.EntranceY + 1, dungeon.EntranceZ + 0.5),
                Yaw = 0,
                Faction = 6,
                Level = dungeon.Level,
                BehaviourKey = AlphaZoneContentProviderKeys.StaticObject,
                EntityDefinitionId = string.Empty,
                PersistentObjectId = dungeon.PortalId,
                Provenance = "CUBEGAME_NATIVE: dungeon portal prototype",
            };
        }

        /// <summary>Minimal faction ids the runtime uses; the full enum lives in the creatures module.</summary>
        private static class CreatureFactionId
        {
            public const int Passive = 6;
        }

        /// <summary>Read-only probe over the zone being generated.</summary>
        private sealed class ZoneStructureProbe : IStructureProbe, IWorldSeamProbe
        {
            private readonly ZoneContentContext _context;
            private readonly BiomeCatalog _biomes;

            public ZoneStructureProbe(ZoneContentContext context, BiomeCatalog biomes)
            {
                _context = context;
                _biomes = biomes;
            }

            public StructureFootprint Probe(int x, int z, int width, int depth)
            {
                int min = int.MaxValue;
                int max = int.MinValue;
                for (int i = 0; i < 4; i++)
                {
                    int px = x + (i == 1 || i == 3 ? Math.Max(0, width - 1) : 0);
                    int pz = z + (i >= 2 ? Math.Max(0, depth - 1) : 0);
                    if (!_context.Contains(px, pz)) return new StructureFootprint(false, int.MinValue, int.MinValue, "");
                    int surface = _context.Zone.Column(px, pz).TopSolid();
                    min = Math.Min(min, surface);
                    max = Math.Max(max, surface);
                }

                return new StructureFootprint(true, min, max, BiomeOf(x, z));
            }

            public string BiomeOf(int x, int z)
            {
                if (!_context.Contains(x, z)) return string.Empty;
                VoxelColumn column = _context.Zone.Column(x, z);
                return _biomes.Resolve(new ClimateAxes(column.Temperature, column.Humidity,
                    _context.Terrain.RareCellTerm(x, z), _context.Terrain.Mountain(x, z),
                    AlphaTerrainGenerator.Level(x >> 14, z >> 14), column.TopSolid(),
                    _context.Terrain.WaterLevel))?.Id ?? string.Empty;
            }

            public int SurfaceAt(int x, int z) => _context.Contains(x, z) ? _context.Zone.Column(x, z).TopSolid() : 0;

            public int RegionLevel(int regionX, int regionZ) => AlphaTerrainGenerator.Level(regionX, regionZ);
        }
    }

    /// <summary>Deterministic counters of one zone's structure stage.</summary>
    public readonly struct ZoneStructureStats
    {
        public ZoneStructureStats(int zoneX, int zoneZ, int placements, SettlementBlueprint settlement,
            DungeonPrototype dungeon, int rejections, IReadOnlyList<string> problems)
        {
            ZoneX = zoneX;
            ZoneZ = zoneZ;
            Placements = placements;
            SettlementId = settlement?.PersistentId ?? string.Empty;
            DungeonInstanceId = dungeon?.InstanceId ?? string.Empty;
            Rejections = rejections;
            Problems = problems ?? Array.Empty<string>();
        }

        public int ZoneX { get; }
        public int ZoneZ { get; }
        public int Placements { get; }
        public string SettlementId { get; }
        public string DungeonInstanceId { get; }
        public int Rejections { get; }
        public IReadOnlyList<string> Problems { get; }
    }
}
