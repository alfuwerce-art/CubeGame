// D1 — EditMode checks of the landmark gate for Alpha spawner objects.
using CubeGame.Features.Creatures;
using CubeGame.Features.World;
using NUnit.Framework;

namespace CubeGame.Tests.Creatures.EditMode
{
    public class AlphaPopulationRulesTests
    {
        [Test]
        public void OnlyVillageAndRuinsRunTheVillageRuinsGenerator()
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
            StringAssert.Contains("NO_VILLAGE_RUINS_GENERATOR", AlphaPopulationRules.ExplainEmptyZone(LandmarkType.Valley));
            // D1 review-fix: the gate is a generator call, never a statement that other zones are empty.
            StringAssert.Contains("not a population gate", AlphaPopulationRules.Status);
            StringAssert.DoesNotContain("NO_ALPHA_SPAWNERS", AlphaPopulationRules.ExplainEmptyZone(LandmarkType.Valley));
        }
    }
}
