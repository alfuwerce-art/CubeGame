using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CubeGame.Features.Combat;
using CubeGame.Features.Contracts;
using CubeGame.Features.Creatures;
using CubeGame.Features.Persistence;
using CubeGame.Features.Session;
using CubeGame.Features.World;
using CubeGame.Gameplay.Streaming;
using NUnit.Framework;
using UnityEngine;

namespace CubeGame.Tests.FullM0.EditMode
{
    /// <summary>
    /// P7-B runtime: the world generator /9 runs the ported Alpha dungeon 0x5CC510 in type-3 zones on the zone CRT
    /// stream, its creatures become zone spawns, and killing the master completes a type-5 mission (0x59642A).
    /// </summary>
    public sealed class FullM0P7DungeonRuntimeTests
    {
        private const uint Seed = 666666;
        private static readonly ZoneKey DungeonZone = new ZoneKey(32812, 32796);

        private static AlphaTerrainGenerator Terrain()
        {
            Assert.IsTrue(D3ZonePopulation.TryCreateGenerator(AlphaTerrainGenerator.WorldRuntimeId, Seed,
                out IWorldGenerator generator));
            return (AlphaTerrainGenerator)generator;
        }

        private static List<long[]> AlphaSpawns()
        {
            string json = File.ReadAllText(Path.Combine(Application.dataPath,
                "CubeGame/Tests/FullM0/EditMode/Fixtures/alpha_dungeon_contents_trace_666666_s1.json"));
            int open = json.IndexOf('[', json.IndexOf("\"spawns\"", StringComparison.Ordinal));
            int end = json.IndexOf("]]", open, StringComparison.Ordinal);
            return Regex.Matches(json.Substring(open + 1, end - open), @"\[([^\[\]]*)\]").Cast<Match>()
                .Select(m => m.Groups[1].Value.Split(',').Select(x => (long)double.Parse(x, CultureInfo.InvariantCulture)).ToArray())
                .ToList();
        }

        private static bool IsDungeonSpawn(SpawnDefinition s) => D3ZonePopulation.IsDungeonCreature(s.BehaviourKey)
            || (s.BehaviourKey == AlphaDungeonContentProvider.ChestBehaviourKey && s.Provenance.StartsWith("EMULATED"));

        [Test]
        public void WorldGeneratorBuildsAlphasDungeonOnTheZoneStream()
        {
            AlphaTerrainGenerator terrain = Terrain();
            var provider = (AlphaDungeonZoneProvider)terrain.ContentProviders[0];
            AlphaZone zone = terrain.Zone(DungeonZone.X, DungeonZone.Z);

            // Alpha's CRT state at the call 0x5E84C3 for this zone (dungeon oracle entry)
            Assert.AreEqual(unchecked((int)1811628196u), provider.LastCrtIn, "zone stream at 0x5CC510");

            List<SpawnDefinition> dungeon = zone.ContentSpawns.Where(IsDungeonSpawn).ToList();
            List<long[]> alpha = AlphaSpawns();
            Assert.AreEqual(alpha.Count, dungeon.Count, "dungeon spawns");
            for (int i = 0; i < alpha.Count; i++)
            {
                long[] a = alpha[i];
                SpawnDefinition s = dungeon[i];
                string tag = $"spawn {i}";
                Assert.AreEqual(a[0], s.Position.X, 1e-3, tag + " x");
                Assert.AreEqual(a[1], s.Position.Z, 1e-3, tag + " z (Alpha y)");
                Assert.AreEqual(a[2], s.Position.Y, 1e-3, tag + " height");
                Assert.AreEqual(a[5], s.Level, tag + " level");
                if (a[3] == 6)
                {
                    Assert.AreEqual(AlphaDungeonContentProvider.ChestObjectId, s.WorldObjectId, tag + " chest");
                    continue;
                }

                Assert.AreEqual(a[4], s.Kind.Value, tag + " kind");
                Assert.AreEqual(a[3], s.Faction, tag + " role");
                Assert.AreEqual(a[8] != 0, s.BehaviourKey == AlphaDungeonContents.MasterBehaviourKey, tag + " master");
            }

            Assert.AreEqual(dungeon.Count, dungeon.Select(s => s.SpawnIndex).Distinct().Count(), "stable spawn indices");
            var population = new D3ZonePopulation(terrain);
            Assert.AreEqual(alpha.Count, population.SpawnsForZone(DungeonZone).Count(IsDungeonSpawn), "population channel 1");
        }

        [Test]
        public void OldWorldsKeepTheContractDungeon()
        {
            Assert.IsTrue(D3ZonePopulation.TryCreateGenerator(AlphaTerrainGenerator.PopulationRuntimeId, Seed,
                out IWorldGenerator generator));
            var terrain = (AlphaTerrainGenerator)generator;
            Assert.IsFalse(terrain.ContentProviders.Any(p => p is AlphaDungeonZoneProvider));
            Assert.IsFalse(terrain.Zone(DungeonZone.X, DungeonZone.Z).ContentSpawns.Any(s => D3ZonePopulation.IsDungeonCreature(s.BehaviourKey)));
        }

        [Test]
        public void KillingTheDungeonMasterCompletesTheTypeFiveMission()
        {
            AlphaTerrainGenerator terrain = Terrain();
            var session = new GameSession();
            Assert.IsTrue(session.EnterMainMenu());
            Assert.IsTrue(session.BeginNewWorld("slot0", new NewWorldRequest { GeneratorId = "test" }, 0));
            Assert.IsTrue(session.CompleteLoading());
            session.Hero.Missions.AttachWorld(terrain, (int)Seed, null);
            int columnX = DungeonZone.X >> 3, columnZ = DungeonZone.Z >> 3;
            Assert.IsTrue(GameSlotStore.RestoreMissions(session.Hero, new SessionSaveData
            {
                alphaMissions = new[]
                {
                    new AlphaMissionSaveData
                    {
                        columnX = columnX, columnZ = columnZ, level = 1, type = (int)AlphaMissionType.Dungeon,
                        state = (int)AlphaMissionState.Active, required = 1,
                        targetZoneX = DungeonZone.X, targetZoneZ = DungeonZone.Z,
                    },
                },
            }));
            Assert.IsTrue(session.Hero.Missions.TryGet(columnX, columnZ, out AlphaMission mission));

            List<SpawnDefinition> spawns = terrain.Zone(DungeonZone.X, DungeonZone.Z).ContentSpawns;
            SpawnDefinition master = spawns.Single(s => s.BehaviourKey == AlphaDungeonContents.MasterBehaviourKey);
            SpawnDefinition guard = spawns.First(s => s.BehaviourKey == AlphaDungeonContents.CreatureBehaviourKey);

            Kill(session, guard);
            Assert.AreEqual(AlphaMissionState.Active, mission.State, "a room creature does not finish the mission");
            Kill(session, master);
            Assert.AreEqual(AlphaMissionState.Complete, mission.State, "the master finishes the type-5 mission");
        }

        private static void Kill(GameSession session, SpawnDefinition spawn)
        {
            StableEntityId id = StableEntityId.ForZoneSpawn(DungeonZone, MissionJournal.ContentChannel, spawn.SpawnIndex);
            Combatant creature = Combatant.CreateCreature(id, spawn.Kind, "Dungeon", spawn.Level, 0);
            creature.Hostile = true;
            creature.Position = session.Hero.Position;
            session.Combat.Register(creature);
            creature.Health.ApplyDamage(creature.Health.CurrentHealth);
            session.Events.Publish(new EntityDied { Entity = id, Killer = session.Hero.Id, TimeMs = session.Clock.TimeMs });
        }
    }
}
