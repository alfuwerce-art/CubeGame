// CubeGame.Features.Creatures — D3: zone content stage of Alpha's world generation (structures, vegetation, creatures).
//
// Alpha continues WorldInfo_generateBiomeContent after the terrain passes with the same zone CRT stream:
// giant tree (0x5DF960), World_populateRegionDecorations (0x5CC510) and the object/creature placement
// (GameController_pickSpawnPoint 0x4D4DE0 picks a stored spawn point). This provider plugs the existing
// DeepSeek content layer (ZoneContentPlanner: W-18 vegetation, structure templates, spawn slots) into the
// world generator's own content stage, so generated zones carry buildings, plants and creatures.
//
// Honest statuses (carried on every SpawnDefinition.Provenance):
//   * which objects a climate selects      TRANSLATED_FROM_CODE (W-18 tree 0x5F56C0, not live-verified);
//   * positions/density, structure geometry CUBEGAME_CONTRACT (Alpha placement/geometry not recovered yet);
//   * creature kind/level                   EMULATED_ALPHA_CODE for the executed spawn table is applied by
//                                           AlphaWorldPopulation; this provider uses the zone-plan creature
//                                           slots and the staged kind list it is given;
//   * surface height                        from the world (no invented terrain);
//   * removals                              owned by the world delta; this stage sees none.
using System;
using System.Collections.Generic;
using CubeGame.AlphaModels;
using CubeGame.Content;
using CubeGame.Features.Contracts;
using CubeGame.Features.World;

namespace CubeGame.Features.Creatures
{
    /// <summary>
    /// Content stage of zone generation: adapts <see cref="ZoneContentContext"/> to the content layer and
    /// publishes its placements as <see cref="SpawnDefinition"/> records with stable indices.
    /// </summary>
    public sealed class AlphaZoneContentProvider : IZoneContentProvider
    {
        public const string ProviderKey = "deepseek.zone-content.v1";

        /// <summary>Behaviour key of a passive object (Alpha faction 6: no walk, no actions — D67/D69).</summary>
        public const string PassiveBehaviourKey = "static_object";

        /// <summary>Behaviour key of a fightable creature/NPC (Features/AI).</summary>
        public const string CreatureBehaviourKey = "creature_ai";

        /// <summary>Behaviour key of a building footprint; its geometry is not ported yet (D3, CUBEGAME_CONTRACT).</summary>
        public const string StructureBehaviourKey = "structure_static";

        public const string StructureProvenance =
            "CUBEGAME_CONTRACT: structure template footprint; geometry PENDING (Alpha house templates not ported)";

        readonly ContentPlanSettings _settings;

        /// <param name="settings">
        /// Kind lists and density. Null → built once from the staged Alpha actor library
        /// (<see cref="ContentPlanSettings.FromLibrary"/>), so only kinds whose visuals exist are placed.
        /// </param>
        public AlphaZoneContentProvider(ContentPlanSettings settings = null)
        {
            _settings = settings;
        }

        public string Key => ProviderKey;

        /// <summary>Settings actually used by <see cref="Populate"/>; null until the first call when not supplied.</summary>
        public ContentPlanSettings Settings => _settings;

        public void Populate(ZoneContentContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            var zone = new WorldZoneKey(context.Zone.Zx, context.Zone.Zz);
            var settings = ResolveSettings();
            var source = new ContextZoneSource(context, zone);
            var plan = ZoneContentPlanner.Plan(zone, source, settings);
            int index = 0;
            for (int i = 0; i < plan.Items.Count; i++)
            {
                var item = plan.Items[i];
                if (item.Kind == ContentPlacementKind.Structure)
                {
                    // D3 step 3: the footprint gets real voxels (reconstructed block house of the proven size).
                    var origin = HouseOrigin(item);
                    AlphaHouseBuilder.Build(origin.X, origin.Y, origin.Z, item.Width, item.Depth, item.Height,
                        (x, y, z, voxel) => context.Write(x, y, z, voxel), DoorSide(item));
                }

                var spawn = ToSpawn(item, zone, index);
                if (spawn == null) continue;
                context.Spawns.Add(spawn);
                index++;
            }

            LastPlan = plan;
        }

        /// <summary>Plan of the most recent zone (diagnostics; null before the first call).</summary>
        public ContentPlan LastPlan { get; private set; }

        ContentPlanSettings ResolveSettings()
        {
            if (_settings != null) return _settings;
            try
            {
                return ContentPlanSettings.FromLibrary(AlphaActorLibrary.Shared);
            }
            catch (Exception)
            {
                // No local content cache: an empty plan is honest, inventing kinds is not.
                return new ContentPlanSettings();
            }
        }

        /// <summary>
        /// One placement → one spawn record. Null when the record cannot be expressed as an entity
        /// (structure without a species keeps its own behaviour key instead).
        /// </summary>
        public static SpawnDefinition ToSpawn(ContentPlacement placement, WorldZoneKey zone, int spawnIndex)
        {
            if (placement == null) throw new ArgumentNullException(nameof(placement));
            if (spawnIndex < 0 || spawnIndex > 0xFFF) throw new ArgumentOutOfRangeException(nameof(spawnIndex));
            var region = new RegionKey((int)Math.Floor(zone.X / (double)AlphaWorldScale.ZonesPerRegion),
                (int)Math.Floor(zone.Z / (double)AlphaWorldScale.ZonesPerRegion));
            string behaviour;
            string provenance = placement.Provenance;
            int faction = placement.Faction;
            // The catalogue decides what an entity is: a W-18 scatter can select a fightable kind too, and a
            // passive object never walks or acts (D67/D69) whatever channel placed it.
            bool passive = placement.Faction == (int)CreatureFaction.Passive ||
                           CreatureKinds.IsPassiveObject(placement.Species);
            switch (placement.Kind)
            {
                case ContentPlacementKind.Structure:
                    behaviour = StructureBehaviourKey;
                    faction = (int)CreatureFaction.Passive;
                    provenance = StructureProvenance;
                    break;
                case ContentPlacementKind.Vegetation:
                    behaviour = passive ? PassiveBehaviourKey : CreatureBehaviourKey;
                    if (passive) faction = (int)CreatureFaction.Passive;
                    break;
                default:
                    behaviour = passive ? PassiveBehaviourKey : CreatureBehaviourKey;
                    if (passive) faction = (int)CreatureFaction.Passive;
                    break;
            }

            return new SpawnDefinition
            {
                Region = region,
                SpawnIndex = spawnIndex,
                Kind = new KindId(placement.Species),
                Position = new WorldPosition(placement.X, placement.SurfaceY + 1, placement.Z),
                Yaw = placement.Yaw,
                Faction = faction,
                Level = placement.Level,
                ClassId = -1,
                SpecializationId = 0,
                Equipment = Array.Empty<RawAlphaItemId>(),
                BehaviourKey = behaviour,
                Provenance = provenance,
            };
        }

        /// <summary>Origin voxel of a structure's block geometry (placement X/Z are the footprint centre).</summary>
        public static (int X, int Y, int Z) HouseOrigin(ContentPlacement placement)
        {
            if (placement == null) throw new ArgumentNullException(nameof(placement));
            int x = (int)Math.Floor(placement.X - placement.Width * 0.5);
            int z = (int)Math.Floor(placement.Z - placement.Depth * 0.5);
            return (x, placement.SurfaceY + 1, z);
        }

        /// <summary>Door side (0 = -Z, 1 = +X, 2 = +Z, 3 = -X) from the placement's 90° rotation.</summary>
        public static int DoorSide(ContentPlacement placement)
        {
            if (placement == null) throw new ArgumentNullException(nameof(placement));
            int side = (int)Math.Round(placement.Yaw / 90.0) % 4;
            return side < 0 ? side + 4 : side;
        }

        /// <summary>Adapter: world zone context → the content layer's read-only source.</summary>
        sealed class ContextZoneSource : IContentZoneSource
        {
            readonly ZoneContentContext _context;
            readonly ZoneClimate _climate;
            readonly WorldCellKey _cell;

            public ContextZoneSource(ZoneContentContext context, WorldZoneKey zone)
            {
                _context = context;
                _cell = AlphaWorldScale.CellOf(zone);
                int cx = zone.X * AlphaZone.Size + AlphaZone.Size / 2;
                int cz = zone.Z * AlphaZone.Size + AlphaZone.Size / 2;
                _climate = new ZoneClimate
                {
                    Humidity = context.Terrain.Humidity(cx, cz),
                    Temperature = context.Terrain.Temperature(cx, cz),
                    CellValue = context.Terrain.RareCellTerm(cx, cz),
                    // W-18 fVar6 is the mountain mask value of 0x5EFFA0, not a placement policy
                    // (docs/reports/D86_W18_ADDRESS_VERIFICATION.md).
                    Spacing = context.Terrain.Mountain(cx, cz),
                    Level = AlphaTerrainGenerator.Level(zone.X >> 6, zone.Z >> 6),
                    Mode = 0,
                    CellType = 0,
                };
            }

            public uint Seed => _context.Terrain.MasterSeed;

            public ZoneClimate ClimateOf(WorldCellKey cell) => _climate;

            public bool TrySurfaceVoxel(int voxelX, int voxelZ, out int topSolidY)
            {
                topSolidY = 0;
                if (!_context.Contains(voxelX, voxelZ)) return false;
                topSolidY = _context.Zone.Column(voxelX, voxelZ).TopSolid();
                return topSolidY > 0;
            }

            public bool IsFlatAndFree(int voxelX, int voxelZ, int width, int depth)
            {
                if (width <= 0 || depth <= 0) return false;
                int min = int.MaxValue;
                int max = int.MinValue;
                for (int i = 0; i < 4; i++)
                {
                    int x = voxelX + (i == 1 || i == 3 ? width - 1 : 0);
                    int z = voxelZ + (i >= 2 ? depth - 1 : 0);
                    if (!_context.Contains(x, z)) return false;
                    int top = _context.Zone.Column(x, z).TopSolid();
                    if (top <= 0) return false;
                    if (top < min) min = top;
                    if (top > max) max = top;
                }

                // CUBEGAME_CONTRACT: flatness tolerance of one voxel, footprint must be built on the zone's own columns.
                return max - min <= 1;
            }

            public bool IsRemoved(string stableId) => false;   // removals are applied by the world delta owner (Claude)
        }
    }
}
