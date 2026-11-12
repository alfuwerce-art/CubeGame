using System.Collections.Generic;
using System.Linq;
using CubeGame.Core.Persistence;
using CubeGame.Features.Contracts;
using CubeGame.Features.Creatures;
using CubeGame.Features.Persistence;
using CubeGame.Features.Session;
using CubeGame.Features.World;
using CubeGame.Gameplay;
using CubeGame.Gameplay.Streaming;
using CubeGame.Persistence;
using NUnit.Framework;

namespace CubeGame.Tests.FullM0.EditMode
{
    /// <summary>C6: world population in the running game — stable ids, activation, killed stay dead.</summary>
    public sealed class FullM0PopulationTests
    {
        private sealed class FakeSource : IWorldPopulationSource
        {
            public int Calls;

            public IReadOnlyList<SpawnDefinition> SpawnsForZone(ZoneKey zone)
            {
                Calls++;
                var list = new List<SpawnDefinition>();
                for (int i = 0; i < 3; i++)
                {
                    list.Add(new SpawnDefinition
                    {
                        Region = new RegionKey(zone.X >> 6, zone.Z >> 6),
                        SpawnIndex = i,
                        Kind = new KindId(0x13),
                        Position = new WorldPosition(zone.X * 256 + 10 + i, 30, zone.Z * 256 + 10),
                        Faction = 1,
                        BehaviourKey = AlphaWorldPopulation.BehaviourKey,
                    });
                }

                return list;
            }
        }

        private static WorldPosition At(int zoneX, int zoneZ) => new WorldPosition(zoneX * 256 + 128, 30, zoneZ * 256 + 128);

        [Test]
        public void D3GeneratorKeepsLegacyHouseAndIndicesButDoesNotPublishActorDecor()
        {
            const uint seed = 20131234u;
            const int zoneIndex = WorldLayout.StartVoxel / WorldLayout.ZoneSize;
            Assert.IsTrue(D3ZonePopulation.TryCreateGenerator(AlphaTerrainGenerator.Id, seed, out IWorldGenerator legacy));
            Assert.IsTrue(D3ZonePopulation.TryCreateGenerator(AlphaTerrainGenerator.ContentId, seed, out IWorldGenerator current));
            Assert.AreEqual(AlphaTerrainGenerator.Id, legacy.GeneratorId);
            Assert.AreEqual(AlphaTerrainGenerator.ContentId, current.GeneratorId);
            Assert.IsFalse(D3ZonePopulation.TryCreateGenerator("cubegame.alpha_terrain/unknown", seed, out _));
            Assert.IsEmpty(((AlphaTerrainGenerator)legacy).Zone(zoneIndex, zoneIndex).ContentSpawns);

            var terrain = (AlphaTerrainGenerator)current;
            AlphaZone generated = terrain.Zone(zoneIndex, zoneIndex);
            SpawnDefinition house = generated.ContentSpawns.Single(s => s.BehaviourKey == AlphaZoneContentProvider.StructureBehaviourKey);
            int style = 1 + (int)(CubeGame.Core.Generation.DeterministicHash.Combine((int)house.Position.X, (int)house.Position.Z) % 5);
            int x = (int)(house.Position.X - 1.5);
            int z = (int)(house.Position.Z - 1.5);
            int roofY = (int)house.Position.Y + (style == 1 ? 3 : 2);
            Assert.IsTrue(Voxel.IsSolid(current.Voxels(x, z).Read(roofY)), "house geometry is in the streamed columns");
            Assert.IsFalse(Voxel.IsSolid(legacy.Voxels(x, z).Read(roofY)), "legacy slot keeps its original terrain");

            var population = new WorldPopulation(new WorldDeltaStore(), activeRadius: 0, keepRadius: 0);
            population.AddSource(new FakeSource(), 0);
            population.AddSource(new D3ZonePopulation(terrain), 1);
            var center = new ZoneKey(zoneIndex, zoneIndex);
            population.Update(At(center.X, center.Z));
            LiveSpawn[] spawns = population.Live.Where(s => s.Channel == 1).ToArray();
            Assert.IsTrue(generated.ContentSpawns.Any(s => s.BehaviourKey == AlphaZoneContentProvider.PassiveBehaviourKey));
            Assert.IsEmpty(spawns, "legacy actor decor is not published; house stays geometry");
        }

        [Test]
        public void EnvironmentRegeneratesFromTheSameSeedWithoutActorRecords()
        {
            const uint seed = 666666;
            var key = WorldLayout.ZoneOf(8397145, 8396899);
            Assert.IsTrue(D3ZonePopulation.TryCreateGenerator(AlphaTerrainGenerator.WorldRuntimeId, seed, out var first));
            Assert.IsTrue(D3ZonePopulation.TryCreateGenerator(AlphaTerrainGenerator.WorldRuntimeId, seed, out var second));
            AlphaZone a = ((AlphaTerrainGenerator)first).Zone(key.X, key.Z);
            AlphaZone b = ((AlphaTerrainGenerator)second).Zone(key.X, key.Z);
            int plants = 0;
            for (int x = 0; x < 8; x++)
            {
                for (int z = 0; z < 8; z++)
                {
                    var chunk = new ChunkKey(key.X * 8 + x, key.Z * 8 + z);
                    var placements = a.PlantsIn(chunk);
                    CollectionAssert.AreEqual(placements, b.PlantsIn(chunk));
                    plants += placements.Count;
                    Assert.IsTrue(placements.All(p => WorldLayout.ChunkOf(p.X, p.Z).Equals(chunk)));
                }
            }
            Assert.Greater(plants, 0);
            Assert.IsTrue(a.Columns.Any(c => c.Voxels.Take(c.Count).Any(v => Voxel.Material(v) == 7 || Voxel.Material(v) == 8)));
            Assert.IsFalse(a.ContentSpawns.Any(s => s.BehaviourKey == AlphaZoneContentProvider.PassiveBehaviourKey));
        }

        [TestCase(AlphaTerrainGenerator.Id)]
        [TestCase(AlphaTerrainGenerator.ContentId)]
        [TestCase(AlphaTerrainGenerator.PreviousWorldRuntimeId)]
        [TestCase(AlphaTerrainGenerator.WorldRuntimeId)]
        public void CheckpointRetainsTheWorldGeneratorVersion(string generatorId)
        {
            var session = new GameSession();
            session.EnterMainMenu();
            Assert.IsTrue(session.BeginNewWorld("slot0", new NewWorldRequest { Seed = 9, GeneratorId = generatorId }, 0));
            Assert.IsTrue(session.CompleteLoading());
            var slots = new GameSlotStore(new MemoryStore(), new JsonSaveSerializer(), new JsonSessionSerializer(), new JsonWorldSerializer());
            Assert.IsTrue(slots.Save(session, 1L).IsSuccess);
            SlotLoadResult loaded = slots.Load("slot0", null);
            Assert.IsTrue(loaded.IsSuccess, loaded.ToString());
            Assert.IsTrue(D3ZonePopulation.TryCreateGenerator(loaded.World.GeneratorId, loaded.World.Seed.Value, out IWorldGenerator generator));
            Assert.AreEqual(generatorId, generator.GeneratorId);
        }

        [Test]
        public void ZoneSpawnIdsAreUniqueAcrossZonesOfOneRegion()
        {
            // Population sources number spawns per zone: the old region-keyed id would collide here.
            var a = StableEntityId.ForZoneSpawn(new ZoneKey(32800, 32800), 0, 1);
            var b = StableEntityId.ForZoneSpawn(new ZoneKey(32801, 32800), 0, 1);
            var c = StableEntityId.ForZoneSpawn(new ZoneKey(32800, 32800), 1, 1);
            Assert.AreNotEqual(a, b);
            Assert.AreNotEqual(a, c);
            Assert.AreEqual(EntityIdKind.ZoneSpawn, a.Kind);
            Assert.AreEqual(a, StableEntityId.ForZoneSpawn(new ZoneKey(32800, 32800), 0, 1), "deterministic");
        }

        [Test]
        public void ZonesAroundTheHeroArePopulatedOnce()
        {
            var deltas = new WorldDeltaStore();
            var population = new WorldPopulation(deltas);
            var source = new FakeSource();
            population.AddSource(source, 0);
            var spawned = new List<StableEntityId>();
            population.Spawned += s => spawned.Add(s.Id);

            population.Update(At(32800, 32800));
            Assert.AreEqual(9, population.ActiveZones.Count, "hero zone + 8 neighbours");
            Assert.AreEqual(27, population.LiveCount);
            Assert.AreEqual(27, spawned.Distinct().Count(), "no duplicate ids");

            population.Update(At(32800, 32800));
            Assert.AreEqual(27, spawned.Count, "standing still creates nothing new");
            Assert.AreEqual(9, source.Calls);
        }

        [Test]
        public void FarZonesDespawnAndReturnWithTheSameIds()
        {
            var population = new WorldPopulation(new WorldDeltaStore());
            population.AddSource(new FakeSource(), 0);
            var despawned = new List<StableEntityId>();
            population.Despawned += s => despawned.Add(s.Id);
            population.Update(At(32800, 32800));
            List<StableEntityId> first = population.Live.Select(s => s.Id).OrderBy(i => i.Value).ToList();

            population.Update(At(32801, 32800));
            Assert.IsEmpty(despawned, "keep radius 2: nothing released yet");
            population.Update(At(32804, 32800));
            Assert.AreEqual(27, despawned.Count, "the first 3×3 zones are beyond the keep radius");

            population.Update(At(32800, 32800));
            CollectionAssert.IsSubsetOf(first, population.Live.Select(s => s.Id).ToList(), "same entities come back");
        }

        [Test]
        public void KilledSpawnStaysDeadAfterReleaseAndSave()
        {
            var deltas = new WorldDeltaStore();
            var population = new WorldPopulation(deltas);
            population.AddSource(new FakeSource(), 0);
            population.Update(At(32800, 32800));
            LiveSpawn victim = population.Live.First();
            Assert.IsTrue(population.MarkKilled(victim.Id, 1234));
            Assert.IsFalse(population.MarkKilled(StableEntityId.Hero, 1), "only live world spawns");

            population.Update(At(32806, 32800));
            population.Update(At(32800, 32800));
            Assert.IsFalse(population.TryGet(victim.Id, out _), "killed spawn is not rebuilt");
            Assert.AreEqual(26, population.LiveCount);

            // Through the save: the kill is an ordinary world delta.
            var session = new GameSession();
            session.EnterMainMenu();
            Assert.IsTrue(session.BeginNewWorld("slot0", new NewWorldRequest { Seed = 9, GeneratorId = "test" }, 0));
            Assert.IsTrue(session.CompleteLoading());
            var live = new WorldPopulation(session.WorldDeltas);
            live.AddSource(new FakeSource(), 0);
            live.Update(At(32800, 32800));
            LiveSpawn killed = live.Live.First();
            live.MarkKilled(killed.Id, 5);
            var store = new MemoryStore();
            var slots = new GameSlotStore(store, new JsonSaveSerializer(), new JsonSessionSerializer(), new JsonWorldSerializer());
            Assert.IsTrue(slots.Save(session, 1L).IsSuccess);
            SlotLoadResult loaded = slots.Load("slot0", "test");
            Assert.IsTrue(loaded.IsSuccess, loaded.ToString());
            var restored = new WorldPopulation(loaded.WorldDeltas);
            restored.AddSource(new FakeSource(), 0);
            restored.Update(At(32800, 32800));
            Assert.IsFalse(restored.TryGet(killed.Id, out _), "killed creature stays dead after loading the save");
            Assert.AreEqual(26, restored.LiveCount);
        }

        [Test]
        public void AlphaPopulationGivesDistinctIdsInEveryZone()
        {
            var source = new AlphaWorldPopulation(new WorldSeed(666666), _ => 3, (_, __, ___) => 40.0,
                kindSelector: (_, __, ___) => 0x13);
            var population = new WorldPopulation(new WorldDeltaStore());
            population.AddSource(source, 0);
            population.Update(At(32800, 32800));
            List<LiveSpawn> live = population.Live.ToList();
            Assert.Greater(live.Count, 0);
            Assert.AreEqual(live.Count, live.Select(s => s.Id).Distinct().Count());
            foreach (LiveSpawn s in live)
            {
                Assert.AreEqual(s.Zone, WorldLayout.ZoneOf((int)s.Definition.Position.X, (int)s.Definition.Position.Z), "spawn stands in its zone");
            }
        }

        private sealed class MemoryStore : ISaveStore
        {
            private readonly Dictionary<string, string> _files = new Dictionary<string, string>();

            public bool Exists(string key) => _files.ContainsKey(key);

            public string Read(string key) => _files.TryGetValue(key, out string value) ? value : null;

            public void Write(string key, string contents) => _files[key] = contents;

            public void Delete(string key) => _files.Remove(key);
        }
    }
}
