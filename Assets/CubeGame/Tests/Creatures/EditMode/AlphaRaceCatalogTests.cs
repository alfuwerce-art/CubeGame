// D4 — EditMode checks of the playable race catalogue used by the character editor.
using CubeGame.Content;
using CubeGame.Features.Creatures;
using CubeGame.Presentation.Visual;
using NUnit.Framework;

namespace CubeGame.Tests.Creatures.EditMode
{
    public class AlphaRaceCatalogTests
    {
        [Test]
        public void CatalogueIsConsistentWithTheCommittedCreatureTable()
        {
            var problems = AlphaRaceCatalog.Validate();
            CollectionAssert.IsEmpty(problems);
            Assert.AreEqual(16, AlphaRaceCatalog.All.Length, "eight races × two sexes");
        }

        [Test]
        public void EveryRaceKindIsAHumanoidOfTheCatalogue()
        {
            foreach (var race in AlphaRaceCatalog.All)
            {
                Assert.IsTrue(AlphaContentDatabase.TryCreatureKind(race.Kind, out var def), race.DisplayName);
                Assert.IsFalse(CreatureKinds.IsPassiveObject(race.Kind), race.DisplayName + " is not a passive object");
                CollectionAssert.Contains(def.Parts, "head", race.DisplayName + " has a humanoid part set");
                CollectionAssert.Contains(def.Parts, "hand_r", race.DisplayName + " can hold a weapon");
            }
        }

        [Test]
        public void NpcOnlyHumanoidsAreNotOffered()
        {
            Assert.IsFalse(AlphaRaceCatalog.TryByKind(0x11, out _), "skeleton is an NPC");
            Assert.IsFalse(AlphaRaceCatalog.TryByKind(0x12, out _), "old man is an NPC");
            Assert.IsFalse(AlphaRaceCatalog.TryByKind(0x06, out _), "bullterrier is a pet, not a playable race");
        }

        [Test]
        public void RaceAndSexAreAddressableBothWays()
        {
            Assert.IsTrue(AlphaRaceCatalog.TryFind("Human", true, out var female));
            Assert.AreEqual(0x03, female.Kind);
            Assert.IsTrue(AlphaRaceCatalog.TryFind("Human", false, out var male));
            Assert.AreEqual(0x02, male.Kind);
            Assert.IsTrue(AlphaRaceCatalog.TryByKind(0x0D, out var frogman));
            Assert.AreEqual("FrogmanMale", frogman.DisplayName);
            Assert.IsFalse(AlphaRaceCatalog.TryFind("Dragon", false, out _));
        }

        [Test]
        public void PreviewRequestCarriesRaceAndHairButNoEquipment()
        {
            Assert.IsTrue(AlphaRaceCatalog.TryByKind(0x09, out var dwarf));
            var request = AlphaRaceCatalog.PreviewRequest(dwarf, UnityEngine.Color.red, true);
            Assert.AreEqual(0x09, request.Kind);
            Assert.IsTrue(request.HasHairColour);
            Assert.AreEqual(-1, request.WeaponSubtype, "equipment comes from the loadout, not from the race");
            Assert.AreEqual(-1, request.ChestSubtype);
        }

        [Test]
        public void AvailabilityWithoutContentCacheIsHonest()
        {
            var problems = new System.Collections.Generic.List<string>();
            var available = AlphaRaceCatalog.Available(null, problems);
            CollectionAssert.IsEmpty(available);
            Assert.AreEqual(AlphaRaceCatalog.All.Length, problems.Count);
            StringAssert.Contains("NO_ACTOR_LIBRARY", problems[0]);
        }
    }
}
