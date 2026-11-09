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
        public void RegistrationAddsTheProvidersToTheWorldRegistry()
        {
            int before = ZoneContentRegistry.Providers.Count;
            int added = AlphaContentProviders.RegisterDefaults();
            Assert.AreEqual(2, added);
            Assert.AreEqual(before + 2, ZoneContentRegistry.Providers.Count);
        }

        [Test]
        public void NotesNameTheUnknownDungeonRule()
        {
            StringAssert.Contains("UNKNOWN", AlphaContentProviders.Notes);
            StringAssert.Contains("type 1", AlphaContentProviders.Notes);
        }
    }
}
