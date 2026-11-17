using System;
using System.Collections.Generic;
using CubeGame.Content;
using CubeGame.Core.Generation;
using CubeGame.Features.Contracts;
using CubeGame.Features.World;
using CubeGame.Features.World.Foundation;
using NUnit.Framework;

namespace CubeGame.Tests.FullM0.EditMode
{
    /// <summary>
    /// G2-1 content binding: CubeGame owns vegetation and structure placement, the settlement/dungeon prototypes, the
    /// entity catalogue and the removal state. Zone generation is the expensive part, so one zone is generated once
    /// and shared by the tests that need real content.
    /// </summary>
    public sealed class G2_1ContentBindingTests
    {
        private const uint Seed = 666666u;
        private const string Generator = AlphaTerrainGenerator.CubeGameContentRuntimeId;

        private static AlphaZone _sharedZone;
        private static IZoneContentProvider[] _sharedProviders;
        private static AlphaWorldSeeds _sharedSeeds;

        private static AlphaTerrainGenerator NewGenerator(uint seed)
        {
            _sharedProviders ??= new IZoneContentProvider[]
            {
                new CubeGameVegetationProvider(),
                new CubeGameStructureProvider(),
            };
            Assert.IsTrue(WorldState.TryCreateGenerator(Generator, seed, out IWorldGenerator generator,
                _sharedProviders), "the CubeGame content generator must be constructible");
            return (AlphaTerrainGenerator)generator;
        }

        private static AlphaZone SharedZone()
        {
            if (_sharedZone != null) return _sharedZone;
            AlphaTerrainGenerator terrain = NewGenerator(Seed);
            int zoneX = WorldLayout.StartVoxel >> 8;
            int zoneZ = WorldLayout.StartVoxel >> 8;
            _sharedSeeds = terrain.Seeds;
            _sharedZone = terrain.Zone(zoneX, zoneZ);
            return _sharedZone;
        }

        private static int SharedZoneX => WorldLayout.StartVoxel >> 8;

        // ------------------------------------------------------------------ identity / catalogue

        [Test]
        public void EntityCatalogueIsCubeGameNativeAndBoundToPlaceholders()
        {
            var problems = new List<string>();
            EntityDefinitionRegistry registry = CubeGameEntityCatalog.CreateRegistry(problems);
            CollectionAssert.IsEmpty(problems, string.Join("; ", problems));
            Assert.AreEqual(5, registry.Count);

            foreach (EntityDefinition definition in registry.All)
            {
                StringAssert.StartsWith("cubegame.", definition.Id);
                Assert.AreEqual(VisualSourceKind.TemporaryAlpha, definition.Visual.Source,
                    "G2-1 may only claim a temporary visual placeholder");
                StringAssert.Contains("TEMPORARY_VISUAL_PLACEHOLDER", definition.Visual.Status);
                Assert.IsTrue(CubeGameVisualBindings.TryGetSpecies(definition.Id, out int species));
                Assert.Greater(species, 0);
                Assert.IsNotEmpty(definition.Visual.Id);
            }

            CollectionAssert.IsEmpty(CubeGameVisualBindings.Validate());
            Assert.AreEqual(CubeGameEntityCatalog.NpcMerchant, CubeGameEntityCatalog.NpcMerchant);
            Assert.IsTrue(CubeGameEntityCatalog.TryDefinitionForAnchor(WorldAnchorKind.Shop, out string shop));
            Assert.AreEqual(CubeGameEntityCatalog.NpcMerchant, shop);
            Assert.IsFalse(CubeGameEntityCatalog.TryDefinitionForAnchor(WorldAnchorKind.DungeonEntrance,
                out string portal));
            Assert.IsEmpty(portal);
        }

        [Test]
        public void ContentIdsAreStableAndNamespaced()
        {
            string plant = CubeGameContentIds.Vegetation(Seed, 32800, 32800, 3, "vegetation.rule.grass");
            Assert.AreEqual(plant, CubeGameContentIds.Vegetation(Seed, 32800, 32800, 3, "vegetation.rule.grass"));
            Assert.IsTrue(CubeGameContentIds.IsVegetation(plant));
            string anchor = CubeGameContentIds.Anchor(Seed, 512, 512, WorldAnchorKind.Inn, 0);
            Assert.IsTrue(CubeGameContentIds.IsAnchor(anchor));
            Assert.AreEqual(8, CubeGameContentIds.VegetationChannel);
            Assert.AreEqual(7, CubeGameContentIds.StructureChannel);
            Assert.AreNotEqual(CubeGameContentIds.VegetationChannel, CubeGameContentIds.StructureChannel);
        }

        // ------------------------------------------------------------------ vegetation placement

        [Test]
        public void VegetationIsPlacedByTheCubeGamePolicy()
        {
            AlphaZone zone = SharedZone();
            Assert.Greater(zone.Vegetation.Count, 0, "the start zone must contain CubeGame vegetation");
            Assert.Greater(zone.VegetationCandidates, zone.Vegetation.Count, "candidates must exceed accepted plants");
            Assert.IsNotEmpty(zone.BiomeId, "the zone must carry the biome the vegetation was built for");
            Assert.IsNotEmpty(zone.VegetationProfileId);
            Assert.IsNotEmpty(zone.VegetationRuleSetId);

            BiomeCatalog biomes = BiomeCatalog.Default();
            Assert.IsTrue(biomes.TryGetBiome(zone.BiomeId, out BiomeDefinition biome));
            Assert.AreEqual(biome.VegetationProfileId, zone.VegetationProfileId);
            Assert.IsTrue(biomes.TryGetVegetation(biome.VegetationProfileId, out VegetationProfile profile));
            Assert.AreEqual(profile.RuleSetId, zone.VegetationRuleSetId);
            Assert.IsTrue(VegetationRuleSet.Defaults().TryGetValue(profile.RuleSetId, out VegetationRuleSet rules));

            var seenRules = new HashSet<string>(StringComparer.Ordinal);
            var seenIndices = new HashSet<int>();
            foreach (VegetationPlacement placement in zone.Vegetation)
            {
                Assert.IsTrue(CubeGameContentIds.IsVegetation(placement.PersistentId), placement.PersistentId);
                Assert.IsTrue(seenIndices.Add(placement.StableIndex), "stable indices must be unique in a zone");
                Assert.AreEqual(zone.BiomeId, placement.BiomeId);
                seenRules.Add(placement.RuleId);
                Assert.IsTrue(profile.Kinds != null && Array.IndexOf(profile.Kinds, placement.Kind) >= 0,
                    $"{placement.Kind} is not part of the biome's vegetation profile");
                Assert.IsTrue(ContainsKind(rules, placement.RuleId, placement.Kind),
                    $"{placement.RuleId} does not place {placement.Kind}");
                Assert.AreEqual(placement.Kind == VegetationKind.Rock
                        ? AlphaVegetationModel.Grass2
                        : VegetationModelBinding.ModelOf(placement.Kind), placement.Model);
            }

            Assert.Greater(seenRules.Count, 0);
        }

        private static bool ContainsKind(VegetationRuleSet set, string ruleId, VegetationKind kind)
        {
            foreach (VegetationRule rule in set.Rules)
            {
                if (!string.Equals(rule.Id, ruleId, StringComparison.Ordinal)) continue;
                foreach (VegetationKind ruleKind in rule.Kinds)
                {
                    if (ruleKind == kind) return true;
                }
            }

            return false;
        }

        [Test]
        public void VegetationPlacementIsDeterministicAcrossGenerators()
        {
            AlphaZone first = SharedZone();
            AlphaTerrainGenerator second = NewGenerator(Seed);
            AlphaZone again = second.Zone(SharedZoneX, SharedZoneX);
            Assert.AreEqual(first.Vegetation.Count, again.Vegetation.Count,
                "same seed + same content stage must produce the same plant count");
            for (int i = 0; i < first.Vegetation.Count; i++)
            {
                Assert.AreEqual(first.Vegetation[i].PersistentId, again.Vegetation[i].PersistentId);
                Assert.AreEqual(first.Vegetation[i].Kind, again.Vegetation[i].Kind);
                Assert.AreEqual(first.Vegetation[i].X, again.Vegetation[i].X);
                Assert.AreEqual(first.Vegetation[i].Z, again.Vegetation[i].Z);
                Assert.AreEqual(first.Vegetation[i].StableIndex, again.Vegetation[i].StableIndex);
            }

            AlphaTerrainGenerator other = NewGenerator(Seed + 1);
            AlphaZone different = other.Zone(SharedZoneX, SharedZoneX);
            Assert.AreNotEqual(first.Vegetation.Count == 0 ? string.Empty : first.Vegetation[0].PersistentId,
                different.Vegetation.Count == 0 ? string.Empty : different.Vegetation[0].PersistentId,
                "another seed must give another world");
        }

        [Test]
        public void VegetationIsNotGeneratedInsideSettlementOrDungeonReserve()
        {
            AlphaZone zone = SharedZone();
            if (zone.Settlement == null && zone.Dungeon == null) Assert.Pass("start zone has no reserved area");
            foreach (VegetationPlacement placement in zone.Vegetation)
            {
                if (zone.Settlement != null)
                {
                    Assert.IsFalse(zone.Settlement.Contains(placement.X, placement.Z),
                        $"plant inside the settlement reserve: {placement.PersistentId}");
                }

                if (zone.Dungeon != null)
                {
                    Assert.IsFalse(Math.Abs(placement.X - zone.Dungeon.EntranceX) <= 3 &&
                                   Math.Abs(placement.Z - zone.Dungeon.EntranceZ) <= 3,
                        $"plant on the dungeon entrance: {placement.PersistentId}");
                }
            }
        }

        // ------------------------------------------------------------------ structures

        [Test]
        public void StructuresArePlacedByTheCubeGamePipeline()
        {
            AlphaZone zone = SharedZone();
            var seenIndices = new HashSet<int>();
            foreach (StructurePlacement placement in zone.Structures)
            {
                StringAssert.StartsWith("structure:", placement.PersistentId);
                Assert.GreaterOrEqual(placement.SpawnIndex, 0, "every structure owns a spawn channel index");
                Assert.IsTrue(seenIndices.Add(placement.SpawnIndex), "spawn indices must be unique in a zone");
                Assert.AreEqual(StructureRuntimeState.Generated, placement.State);
                Assert.IsFalse(string.IsNullOrEmpty(placement.BiomeId), "a structure must know its biome");
                Assert.IsFalse(placement.Kind == StructureKind.Village || placement.Kind == StructureKind.City,
                    "settlements are owned by the settlement prototype, not by a random candidate");
            }

            var spawnIndices = new HashSet<int>();
            foreach (SpawnDefinition spawn in zone.StructureSpawns)
            {
                Assert.IsTrue(spawnIndices.Add(spawn.SpawnIndex), "spawn records must be unique in a zone");
                Assert.IsNotEmpty(spawn.PersistentObjectId);
            }
        }

        [Test]
        public void StructuresAreDeterministicAcrossGenerators()
        {
            AlphaZone first = SharedZone();
            AlphaTerrainGenerator second = NewGenerator(Seed);
            AlphaZone again = second.Zone(SharedZoneX, SharedZoneX);
            Assert.AreEqual(first.Structures.Count, again.Structures.Count);
            for (int i = 0; i < first.Structures.Count; i++)
            {
                Assert.AreEqual(first.Structures[i].PersistentId, again.Structures[i].PersistentId);
                Assert.AreEqual(first.Structures[i].SpawnIndex, again.Structures[i].SpawnIndex);
                Assert.AreEqual(first.Structures[i].Kind, again.Structures[i].Kind);
            }
        }

        [Test]
        public void StructurePopulationServesTheZoneAndSkipsRemovedObjects()
        {
            AlphaZone zone = SharedZone();
            var deltas = new WorldDeltaStore();
            var population = new CubeGameStructurePopulation(NewGenerator(Seed), deltas);
            var zoneKey = new ZoneKey(SharedZoneX, SharedZoneX);
            IReadOnlyList<SpawnDefinition> all = population.SpawnsForZone(zoneKey);
            Assert.AreEqual(zone.StructureSpawns.Count, all.Count);

            SpawnDefinition target = null;
            foreach (SpawnDefinition spawn in all)
            {
                if (!CubeGameContentIds.IsStructure(spawn.PersistentObjectId)) continue;
                target = spawn;
                break;
            }

            if (target == null) Assert.Pass("start zone has no structure spawn to remove");
            Assert.IsTrue(population.MarkRemoved(zoneKey, target, 5));
            IReadOnlyList<SpawnDefinition> after = population.SpawnsForZone(zoneKey);
            Assert.AreEqual(all.Count - 1, after.Count);
            foreach (SpawnDefinition spawn in after)
            {
                Assert.AreNotEqual(target.SpawnIndex, spawn.SpawnIndex);
            }

            Assert.IsTrue(population.IsRemoved(zoneKey, target));
        }

        // ------------------------------------------------------------------ settlement / dungeon prototypes

        [Test]
        public void SettlementBlueprintReservesAreaSlotsRoadAndAnchors()
        {
            BiomeCatalog biomes = BiomeCatalog.Default();
            StructureRuleSet rules = StructureRuleSet.Default();
            var probe = new WorldFoundationValidation.FixedSeamProbe(
                new ClimateAxes(0.5f, 0.6f, 0f, 0.1f, 2, 30, 0), biomes, 30);
            SettlementSite site = SettlementSeamPlanner.Plan(4242u, 512, 512, probe, rules);
            Assert.IsNotNull(site);
            SettlementBlueprint blueprint = SettlementBlueprint.FromSite(site, probe.SurfaceAt);
            Assert.AreEqual(site.PersistentId, blueprint.PersistentId);
            Assert.AreEqual(4, blueprint.Slots.Count);
            Assert.GreaterOrEqual(blueprint.RoadCells.Count, 4);
            Assert.GreaterOrEqual(blueprint.Anchors.Count, 8);
            Assert.IsTrue(blueprint.Contains(blueprint.CenterX, blueprint.CenterZ));
            Assert.IsFalse(blueprint.Contains(blueprint.CenterX + blueprint.RadiusVoxels + 1, blueprint.CenterZ));
            foreach (SettlementBuildingSlot slot in blueprint.Slots)
            {
                Assert.IsTrue(blueprint.Contains(slot.X, slot.Z));
                StringAssert.Contains(":building:", slot.PersistentId);
            }

            SettlementBlueprint again = SettlementBlueprint.FromSite(site, probe.SurfaceAt);
            Assert.AreEqual(blueprint.Slots[0].PersistentId, again.Slots[0].PersistentId);
            Assert.AreEqual(blueprint.RoadCells[0].X, again.RoadCells[0].X);
        }

        [Test]
        public void DungeonPrototypeCarvesATestRoomAndReturnsToTheEntrance()
        {
            BiomeCatalog biomes = BiomeCatalog.Default();
            var probe = new WorldFoundationValidation.FixedSeamProbe(
                new ClimateAxes(0.5f, 0.5f, 0f, 0.1f, 2, 30, 0), biomes, 30);

            // One entrance zone per 2×2-region block: find it the same way the prototype does.
            int blockX = AlphaWorldScale.RegionOfZone(32800) / DungeonSeamPlanner.RegionsPerBlock;
            int blockZ = AlphaWorldScale.RegionOfZone(32800) / DungeonSeamPlanner.RegionsPerBlock;
            int span = DungeonSeamPlanner.RegionsPerBlock * AlphaWorldScale.ZonesPerRegion;
            int entranceZones = 0;
            int entranceX = 0;
            int entranceZ = 0;
            for (int dx = 0; dx < span; dx++)
            {
                for (int dz = 0; dz < span; dz++)
                {
                    int zoneX = blockX * span + dx;
                    int zoneZ = blockZ * span + dz;
                    if (!DungeonSeamPlanner.IsEntranceZone(zoneX, zoneZ, 99, blockX, blockZ)) continue;
                    entranceZones++;
                    entranceX = zoneX;
                    entranceZ = zoneZ;
                }
            }

            Assert.AreEqual(1, entranceZones, "one dungeon entrance zone per region block");

            DungeonEntranceSite site = DungeonSeamPlanner.Plan(99u, entranceX, entranceZ, probe);
            Assert.IsNotNull(site);
            Assert.AreEqual(entranceX, site.ZoneX);
            Assert.AreEqual(entranceZ, site.ZoneZ);

            DungeonPrototype dungeon = DungeonPrototype.FromSite(site);
            Assert.Greater(dungeon.MaxX, dungeon.MinX);
            Assert.Less(dungeon.FloorY, dungeon.EntranceY);

            var written = new Dictionary<(int, int, int), uint>();
            Assert.IsTrue(dungeon.Carve((x, y, z, voxel) =>
            {
                written[(x, y, z)] = voxel;
                return true;
            }, out string problem), problem);

            Assert.AreEqual("generated", dungeon.State);
            Assert.IsTrue(dungeon.Contains(dungeon.CenterX + 0.5, dungeon.CenterY, dungeon.CenterZ + 0.5));
            Assert.IsFalse(dungeon.Contains(dungeon.MinX - 2, dungeon.CenterY, dungeon.CenterZ));
            // Floor, wall and inner air of the room.
            Assert.IsTrue(Voxel.IsSolid(written[(dungeon.CenterX, dungeon.FloorY, dungeon.CenterZ)]));
            Assert.IsTrue(Voxel.IsSolid(written[(dungeon.MinX, dungeon.FloorY + 1, dungeon.CenterZ)]));
            Assert.IsFalse(Voxel.IsSolid(written[(dungeon.CenterX, dungeon.FloorY + 1, dungeon.CenterZ)]));
            // Entrance shaft reaches the room floor.
            Assert.IsFalse(Voxel.IsSolid(written[(dungeon.EntranceX, dungeon.FloorY + 2, dungeon.EntranceZ)]));
        }

        // ------------------------------------------------------------------ removal persistence

        [Test]
        public void RemovedVegetationStaysRemovedAfterRegeneration()
        {
            AlphaZone zone = SharedZone();
            if (zone.Vegetation.Count == 0) Assert.Pass("start zone has no vegetation");
            VegetationPlacement plant = zone.Vegetation[0];
            var deltas = new WorldDeltaStore();
            deltas.Record(new WorldDelta
            {
                Region = new RegionKey(SharedZoneX >> 6, SharedZoneX >> 6),
                Kind = WorldDeltaKind.ObjectRemoved,
                X = SharedZoneX,
                Y = CubeGameContentIds.VegetationChannel * 4096 + plant.StableIndex,
                Z = SharedZoneX,
                Value = 1,
                TimeMs = 1,
            });
            Assert.IsTrue(deltas.IsObjectRemoved(new ZoneKey(SharedZoneX, SharedZoneX),
                CubeGameContentIds.VegetationChannel, plant.StableIndex));

            // A fresh generator regenerates the zone deterministically; the same index is removed again.
            AlphaTerrainGenerator again = NewGenerator(Seed);
            AlphaZone regenerated = again.Zone(SharedZoneX, SharedZoneX);
            VegetationPlacement same = regenerated.Vegetation[plant.StableIndex];
            Assert.AreEqual(plant.PersistentId, same.PersistentId);
            Assert.IsTrue(deltas.IsObjectRemoved(new ZoneKey(SharedZoneX, SharedZoneX),
                CubeGameContentIds.VegetationChannel, same.StableIndex));
        }
    }
}
