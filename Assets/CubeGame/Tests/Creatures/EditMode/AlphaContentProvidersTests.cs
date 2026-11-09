// D3 — EditMode checks of the provider registration helper (order, keys, no invented dungeon rule).
using CubeGame.Features.Creatures;
using CubeGame.Features.World;
using NUnit.Framework;

namespace CubeGame.Tests.Creatures.EditMode
{
    public class AlphaContentProvidersTests
    {
        [Test]
        public void DefaultSetIsVegetationThenCity()
        {
            var providers = AlphaContentProviders.CreateDefault();
            Assert.AreEqual(2, providers.Length, "no dungeon provider without a caller predicate");
            Assert.AreEqual("deepseek.zone-content.v1", providers[0].Key);
            Assert.AreEqual("deepseek.city-content.v1", providers[1].Key);
        }

        [Test]
        public void DungeonProviderAppearsOnlyWithAPredicateAndComesLast()
        {
            var providers = AlphaContentProviders.CreateDefault(ctx => true);
            Assert.AreEqual(3, providers.Length);
            Assert.AreEqual("deepseek.dungeon-content.v1", providers[2].Key);
        }

        [Test]
        public void PredicatesAreNullSafe()
        {
            Assert.IsFalse(AlphaContentProviders.IsCityZone(null));
            Assert.IsFalse(AlphaContentProviders.ByRecordType(4)(null));
        }

        [Test]
        public void RegistrationAddsProvidersWithoutChangingTheLegacyGenerator()
        {
            var before = ZoneContentRegistry.Providers;
            try
            {
                int added = AlphaContentProviders.RegisterDefaults();
                Assert.AreEqual(2, added);
                Assert.AreEqual(before.Count + 2, ZoneContentRegistry.Providers.Count);

                Assert.IsTrue(WorldState.TryCreateGenerator(AlphaTerrainGenerator.Id, 666666u, out var generator));
                Assert.IsEmpty(((AlphaTerrainGenerator)generator).ContentProviders);
            }
            finally
            {
                ZoneContentRegistry.Clear();
                foreach (var provider in before) ZoneContentRegistry.Register(provider);
            }
        }

        [Test]
        public void NotesNameTheUnknownDungeonRule()
        {
            StringAssert.Contains("UNKNOWN", AlphaContentProviders.Notes);
            StringAssert.Contains("type 1", AlphaContentProviders.Notes);
        }
    }
}
