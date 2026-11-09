// D1 — EditMode checks of the zone-type rule for Alpha spawner objects (static decode of 0x5E4850/0x5AF170).
using CubeGame.Features.Creatures;
using NUnit.Framework;

namespace CubeGame.Tests.Creatures.EditMode
{
    public class AlphaPopulationRulesTests
    {
        [Test]
        public void OnlyZoneTypesOneAndSevenCarrySpawners()
        {
            CollectionAssert.IsEmpty(AlphaPopulationRules.Validate());
            Assert.IsTrue(AlphaPopulationRules.CarriesSpawnerObjects(1));
            Assert.IsTrue(AlphaPopulationRules.CarriesSpawnerObjects(7));
            Assert.IsFalse(AlphaPopulationRules.CarriesSpawnerObjects(0));
            Assert.IsFalse(AlphaPopulationRules.CarriesSpawnerObjects(3), "settlement type 3 has decorations, not spawners");
            CollectionAssert.AreEqual(new[] { 1, 7 }, AlphaPopulationRules.SpawnerZoneTypes);
        }

        [Test]
        public void EmptyZoneIsExplained()
        {
            Assert.IsEmpty(AlphaPopulationRules.ExplainEmptyZone(1));
            StringAssert.Contains("NO_ALPHA_SPAWNERS", AlphaPopulationRules.ExplainEmptyZone(5));
            StringAssert.Contains("Chunk_generateObjects", AlphaPopulationRules.Status);
        }
    }
}
