// D3 — EditMode checks of the dungeon content stage (Q-16 rooms, Q-17 chests, deterministic plan).
using CubeGame.Content;
using CubeGame.Features.Contracts;
using CubeGame.Features.Creatures;
using CubeGame.Features.World;
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
                Assert.AreEqual(10, spawn.WorldObjectId);
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
        public void GeneratedZoneCarvesRoomsAndPlacesChestAnchorsInTheSameVolume()
        {
            var original = AlphaZoneGenerator.Generate(new AlphaTerrainGenerator(Seed), Zone.X, Zone.Z);
            var provider = new AlphaDungeonContentProvider(context => context.Zone.Zx == Zone.X && context.Zone.Zz == Zone.Z);
            var terrain = new AlphaTerrainGenerator(Seed, new[] { provider });
            var generated = AlphaZoneGenerator.Generate(terrain, Zone.X, Zone.Z);

            Assert.That(provider.LastLayout, Is.Not.Null, "the default room count must yield a plan");
            Assert.That(provider.LastChests, Has.Count.EqualTo(3));
            int changed = 0;
            foreach (var spawn in generated.ContentSpawns)
            {
                int x = (int)spawn.Position.X;
                int y = (int)spawn.Position.Y;
                int z = (int)spawn.Position.Z;
                Assert.That(x, Is.InRange(Zone.X * AlphaZone.Size, (Zone.X + 1) * AlphaZone.Size - 1));
                Assert.That(z, Is.InRange(Zone.Z * AlphaZone.Size, (Zone.Z + 1) * AlphaZone.Size - 1));
                var column = generated.Column(x, z);
                Assert.That(y, Is.GreaterThan(1));
                Assert.That(column.Read(y), Is.EqualTo(Voxel.Air));
                Assert.That(column.TopSolid(), Is.GreaterThan(y + AlphaDungeonRules.RoomSize - 1));
                if (Voxel.IsSolid(original.Column(x, z).Read(y))) changed++;
            }

            Assert.That(generated.ContentSpawns, Has.Count.EqualTo(provider.LastLayout.Rooms.Count + 3));
            Assert.That(changed, Is.GreaterThan(0), "the provider must clear actual terrain, not only emit records");
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
