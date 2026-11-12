// D3 — EditMode checks of the city service provider (Alpha town furniture, F-U5 unknown trade rules).
using CubeGame.Content;
using CubeGame.Features.Contracts;
using CubeGame.Features.Creatures;
using NUnit.Framework;

namespace CubeGame.Tests.Creatures.EditMode
{
    public class AlphaCityContentProviderTests
    {
        static readonly WorldZoneKey Zone = new WorldZoneKey(32802, 32802);
        const uint Seed = 20261110u;

        static int Flat(int x, int z) => 70;

        [Test]
        public void ServiceObjectsAreTheAlphaTownFurnitureAsPassiveSpawns()
        {
            var spawns = AlphaCityContentProvider.Plan(Seed, Zone, Flat, 5, 0);
            Assert.AreEqual(5, spawns.Count);
            for (int i = 0; i < spawns.Count; i++)
            {
                Assert.AreEqual(i, spawns[i].SpawnIndex);
                Assert.AreEqual(AlphaCityContentProvider.ServiceBehaviourKey, spawns[i].BehaviourKey);
                Assert.AreEqual((int)CreatureFaction.Passive, spawns[i].Faction);
                Assert.AreEqual(71.0, spawns[i].Position.Y, "furniture stands on the voxel above the ground");
                Assert.IsTrue(spawns[i].WorldObjectId.HasValue);
                Assert.IsTrue(AlphaNpcServices.TryServiceFor(spawns[i].WorldObjectId.Value, out var service),
                    $"object {spawns[i].WorldObjectId} is a mapped service object");
                StringAssert.Contains(service.ObjectName, spawns[i].Provenance);
                StringAssert.Contains("F-U5", spawns[i].Provenance);
            }
        }

        [Test]
        public void PlanIsDeterministicAndContinuesTheZoneIndices()
        {
            var a = AlphaCityContentProvider.Plan(Seed, Zone, Flat, 4, 12);
            var b = AlphaCityContentProvider.Plan(Seed, Zone, Flat, 4, 12);
            Assert.AreEqual(a.Count, b.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Position, b[i].Position);
                Assert.AreEqual(a[i].WorldObjectId, b[i].WorldObjectId);
                Assert.AreEqual(12 + i, a[i].SpawnIndex);
            }
        }

        [Test]
        public void DifferentSeedsPlaceDifferentFurniture()
        {
            var a = AlphaCityContentProvider.Plan(Seed, Zone, Flat, 5, 0);
            var b = AlphaCityContentProvider.Plan(Seed + 1, Zone, Flat, 5, 0);
            bool differs = a.Count != b.Count;
            for (int i = 0; !differs && i < a.Count; i++)
                differs = a[i].WorldObjectId != b[i].WorldObjectId || !a[i].Position.Equals(b[i].Position);
            Assert.IsTrue(differs);
        }

        [Test]
        public void ColumnsWithoutGroundAreSkipped()
        {
            var spawns = AlphaCityContentProvider.Plan(Seed, Zone, (x, z) => x % 2 == 0 ? 70 : 0, 4, 0);
            Assert.Less(spawns.Count, 4, "objects over a void are not placed");
            foreach (var spawn in spawns)
            {
                Assert.AreEqual(0, spawn.Position.X % 2 == 0 ? 0 : 1, "only columns with ground were used");
            }
        }

        [Test]
        public void ProviderWithoutCityPredicateDoesNothing()
        {
            var provider = new AlphaCityContentProvider(null);
            Assert.AreEqual(AlphaCityContentProvider.ProviderKey, provider.Key);
            Assert.Throws<System.ArgumentNullException>(() => provider.Populate(null));
            Assert.IsEmpty(AlphaCityContentProvider.Plan(Seed, Zone, Flat, 0, 0));
        }
    }
}
