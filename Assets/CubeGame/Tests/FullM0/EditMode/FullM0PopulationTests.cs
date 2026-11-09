using System.Collections.Generic;
using System.Linq;
using CubeGame.Core.Persistence;
using CubeGame.Features.Contracts;
using CubeGame.Features.Creatures;
using CubeGame.Features.Persistence;
using CubeGame.Features.Session;
using CubeGame.Features.World;
using CubeGame.Gameplay;
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
            var source = new AlphaWorldPopulation(new WorldSeed(666666), _ => 3, (_, __, ___) => 40.0);
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
