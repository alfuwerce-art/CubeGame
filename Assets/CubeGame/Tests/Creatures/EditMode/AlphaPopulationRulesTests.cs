// D1 — EditMode checks of the landmark gate for Alpha spawner objects.
using CubeGame.Features.Creatures;
using CubeGame.Features.World;
using NUnit.Framework;

namespace CubeGame.Tests.Creatures.EditMode
{
    public class AlphaPopulationRulesTests
    {
        [Test]
        public void OnlyVillageAndRuinsRunTheSpawnerPass()
        {
            CollectionAssert.IsEmpty(AlphaPopulationRules.Validate());
            foreach (LandmarkType landmark in System.Enum.GetValues(typeof(LandmarkType)))
            {
                Assert.AreEqual((int)landmark == 1 || (int)landmark == 5,
                    AlphaPopulationRules.CarriesSpawnerObjects(landmark), landmark.ToString());
            }
            CollectionAssert.AreEqual(new[] { LandmarkType.Village, LandmarkType.Ruins }, AlphaPopulationRules.SpawnerLandmarkTypes);
        }

        [Test]
        public void EmptyZoneIsExplained()
        {
            Assert.IsEmpty(AlphaPopulationRules.ExplainEmptyZone(LandmarkType.Village));
            StringAssert.Contains("NO_ALPHA_SPAWNERS", AlphaPopulationRules.ExplainEmptyZone(LandmarkType.Valley));
            StringAssert.Contains("Chunk_generateObjects", AlphaPopulationRules.Status);
        }
    }
}
