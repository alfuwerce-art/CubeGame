// D1 / M0-06B — EditMode checks of the Alpha world population source (executed spawn table + contract parts).
using System.Collections.Generic;
using CubeGame.Content;
using CubeGame.Features.Contracts;
using CubeGame.Features.Creatures;
using NUnit.Framework;

namespace CubeGame.Tests.Creatures.EditMode
{
    public class AlphaWorldPopulationTests
    {
        static readonly ZoneKey ZoneA = new ZoneKey(32800, 32800);
        static readonly ZoneKey ZoneB = new ZoneKey(32801, 32800);

        static AlphaWorldPopulation Population(uint seed, int level = 5, bool npc = false, bool withGround = true)
        {
            return new AlphaWorldPopulation(new WorldSeed(seed),
                z => level,
                withGround ? (System.Func<ZoneKey, int, int, double>)((z, x, zz) => 64.0 + x * 0.0) : null,
                npc);
        }

        [Test]
        public void SameSeedAndZoneGiveTheSameSpawns()
        {
            var a = Population(666666).SpawnsForZone(ZoneA);
            var b = Population(666666).SpawnsForZone(ZoneA);
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Kind, b[i].Kind);
                Assert.AreEqual(a[i].SpawnIndex, b[i].SpawnIndex);
                Assert.AreEqual(a[i].Position, b[i].Position);
                Assert.AreEqual(a[i].Yaw, b[i].Yaw);
            }
        }

        [Test]
        public void DifferentSeedOrZoneChangesThePopulation()
        {
            var a = Population(666666).SpawnsForZone(ZoneA);
            var b = Population(666667).SpawnsForZone(ZoneA);
            var c = Population(666666).SpawnsForZone(ZoneB);
            bool differSeed = a.Count != b.Count;
            for (int i = 0; !differSeed && i < a.Count; i++) differSeed = a[i].Kind.Value != b[i].Kind.Value || !a[i].Position.Equals(b[i].Position);
            bool differZone = a.Count != c.Count;
            for (int i = 0; !differZone && i < a.Count; i++) differZone = !a[i].Position.Equals(c[i].Position);
            Assert.IsTrue(differSeed, "a different world seed must change the population");
            Assert.IsTrue(differZone, "a different zone must change the population");
        }

        [Test]
        public void KindsComeFromTheExecutedTableAndAreNeverPassiveObjects()
        {
            var table = new HashSet<int>(AlphaSpawnTable.Kinds);
            foreach (var spawn in Population(42, 12).SpawnsForZone(ZoneA))
            {
                CollectionAssert.Contains(table, spawn.Kind.Value);
                Assert.IsFalse(CreatureKinds.IsPassiveObject(spawn.Kind.Value), "plants/ore are zone content, not creatures");
                Assert.AreEqual(AlphaWorldPopulation.BehaviourKey, spawn.BehaviourKey);
                StringAssert.Contains("EMULATED_ALPHA_CODE", spawn.Provenance);
                StringAssert.Contains("CUBEGAME_CONTRACT", spawn.Provenance);
            }
        }

        [Test]
        public void LevelOfTheZoneIsAppliedAndSpawnsStaggeredInsideIt()
        {
            var spawns = Population(7, 9).SpawnsForZone(ZoneA);
            Assert.IsNotEmpty(spawns);
            var indices = new HashSet<int>();
            foreach (var spawn in spawns)
            {
                Assert.AreEqual(9, spawn.Level);
                Assert.Less(spawn.SpawnIndex, 4096);
                Assert.IsTrue(indices.Add(spawn.SpawnIndex), "spawn indices are unique inside a zone");
                double minX = ZoneA.X * (double)AlphaWorldScale.ZoneVoxels;
                Assert.GreaterOrEqual(spawn.Position.X, minX);
                Assert.Less(spawn.Position.X, minX + AlphaWorldScale.ZoneVoxels);
                Assert.AreEqual(64.0, spawn.Position.Y, "ground height comes from the world generator hook");
            }
        }

        [Test]
        public void SpawnIdsAreStableAcrossReloads()
        {
            var before = Population(123).SpawnsForZone(ZoneA);
            var after = Population(123).SpawnsForZone(ZoneA);
            for (int i = 0; i < before.Count; i++)
            {
                var idBefore = StableEntityId.ForSpawn(before[i].Region, before[i].SpawnIndex, 0);
                var idAfter = StableEntityId.ForSpawn(after[i].Region, after[i].SpawnIndex, 0);
                Assert.AreEqual(idBefore, idAfter);
            }
        }

        [Test]
        public void NpcTableContainsOnlyNpcKinds()
        {
            var npc = Population(99, 6, npc: true).SpawnsForZone(ZoneA);
            Assert.IsNotEmpty(npc);
            foreach (var spawn in npc)
            {
                Assert.LessOrEqual(spawn.Kind.Value, 0x17, "NPC rows of the executed table hold kinds 0x00…0x17");
            }
        }

        [Test]
        public void WeightedPickUsesTheNearestExecutedLevelAndNeverInventsOne()
        {
            var rows = new[]
            {
                new AlphaSpawnEntryDef(1, 0x04, 10),
                new AlphaSpawnEntryDef(3, 0x06, 10),
                new AlphaSpawnEntryDef(5, 0x08, 10),
            };
            Assert.AreEqual(0x06, AlphaWorldPopulation.PickKind(rows, 4, 0));      // level 4 -> level-3 rows
            Assert.AreEqual(0x08, AlphaWorldPopulation.PickKind(rows, 30, 0));     // beyond max -> level-5 rows
            Assert.AreEqual(0x04, AlphaWorldPopulation.PickKind(rows, 1, 0));
            Assert.AreEqual(-1, AlphaWorldPopulation.PickKind(new AlphaSpawnEntryDef[0], 5, 0));
        }
    }
}
