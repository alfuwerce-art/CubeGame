// D3 — EditMode checks of the dungeon content stage (Q-16 rooms, Q-17 chests, deterministic plan).
using CubeGame.Content;
using CubeGame.Features.Contracts;
using CubeGame.Features.Creatures;
using NUnit.Framework;

namespace CubeGame.Tests.Creatures.EditMode
{
    public class AlphaDungeonContentProviderTests
    {
        static readonly WorldZoneKey Zone = new WorldZoneKey(32810, 32810);
        const uint Seed = 666666u;

        [Test]
        public void PlanContainsRoomsThenChestsWithStableIndices()
        {
            var spawns = AlphaDungeonContentProvider.BuildPlan(Seed, Zone, 9, 3, 0, out var layout, out var chests);
            Assert.AreEqual(layout.Rooms.Count + chests.Count, spawns.Count);
            Assert.GreaterOrEqual(layout.Rooms.Count, 1);
            for (int i = 0; i < spawns.Count; i++)
            {
                Assert.AreEqual(i, spawns[i].SpawnIndex, "indices are dense and stable");
            }

            for (int i = 0; i < layout.Rooms.Count; i++)
            {
                Assert.AreEqual(AlphaDungeonContentProvider.RoomBehaviourKey, spawns[i].BehaviourKey);
                StringAssert.Contains("Q-16", spawns[i].Provenance);
            }

            for (int i = 0; i < chests.Count; i++)
            {
                var spawn = spawns[layout.Rooms.Count + i];
                Assert.AreEqual(AlphaDungeonContentProvider.ChestBehaviourKey, spawn.BehaviourKey);
                StringAssert.Contains("10 items", spawn.Provenance);
                Assert.AreEqual(chests[i].X, spawn.Position.X);
                Assert.AreEqual(chests[i].Y, spawn.Position.Y);
            }
        }

        [Test]
        public void PlanIsDeterministicForTheSameSeed()
        {
            var a = AlphaDungeonContentProvider.BuildPlan(Seed, Zone, 9, 2, 0, out _, out _);
            var b = AlphaDungeonContentProvider.BuildPlan(Seed, Zone, 9, 2, 0, out _, out _);
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Position, b[i].Position);
                Assert.AreEqual(a[i].BehaviourKey, b[i].BehaviourKey);
            }
        }

        [Test]
        public void IndicesContinueAfterTheZoneContent()
        {
            var spawns = AlphaDungeonContentProvider.BuildPlan(Seed, Zone, 2, 1, 40, out var layout, out var chests);
            Assert.AreEqual(40, spawns[0].SpawnIndex);
            Assert.AreEqual(40 + layout.Rooms.Count + chests.Count - 1, spawns[spawns.Count - 1].SpawnIndex);
        }

        [Test]
        public void ProviderWithoutDungeonPredicateDoesNothing()
        {
            var provider = new AlphaDungeonContentProvider(null);
            Assert.AreEqual(AlphaDungeonContentProvider.ProviderKey, provider.Key);
            Assert.Throws<System.ArgumentNullException>(() => provider.Populate(null));
        }

        [Test]
        public void ChestCountIsHonoured()
        {
            var spawns = AlphaDungeonContentProvider.BuildPlan(Seed, Zone, 4, 0, 0, out var layout, out var chests);
            Assert.AreEqual(layout.Rooms.Count, spawns.Count);
            Assert.IsEmpty(chests);
        }
    }
}
