// M0-F2A — EditMode checks of the one hero visual path (no content cache needed): saved appearance and equipped
// items → HeroVisualSpec, the executed item model selector, the shared humanoid rig rule and determinism.
using System.Collections.Generic;
using System.IO;
using CubeGame.Content;
using CubeGame.Core.Characters;
using CubeGame.Core.Equipment;
using CubeGame.Features.Contracts;
using CubeGame.Features.Session;
using CubeGame.Gameplay;
using CubeGame.Presentation.Visual;
using NUnit.Framework;

namespace CubeGame.Tests.Creatures.EditMode
{
    public class HeroVisualComposerTests
    {
        static Dictionary<VisualEquipmentSlot, HeroVisualItem> Gear(params (VisualEquipmentSlot Slot, HeroVisualItem Item)[] items)
        {
            var d = new Dictionary<VisualEquipmentSlot, HeroVisualItem>();
            foreach (var (slot, item) in items) d[slot] = item;
            return d;
        }

        static HeroAppearance Look(int race, int gender, int face = 0, int hair = 0, byte r = 0xFF, byte g = 0xC8, byte b = 0x32) =>
            new HeroAppearance(race, gender, face, hair, r, g, b);

        // ----- appearance mapping -----

        [Test]
        public void RaceGenderFaceAndHaircutSelectTheAlphaRegistryModels()
        {
            HeroVisualSpec human = HeroVisualComposer.Compose(Look(0, 0), null);
            Assert.AreEqual(0x02, human.Kind);
            Assert.AreEqual("human-head-m01.cub", human.HeadModel, "registry 0x4DE");
            Assert.AreEqual("human-hair-m01.cub", human.HairModel, "registry 0x4E4");
            Assert.IsFalse(human.HeadMarkerTransparent, "head loader flag 1");
            Assert.IsTrue(human.HairMarkerTransparent, "hair loader flag 0: marker voxels transparent");

            HeroVisualSpec face3 = HeroVisualComposer.Compose(Look(0, 0, face: 2, hair: 4), null);
            Assert.AreEqual("human-head-m03.cub", face3.HeadModel);
            Assert.AreEqual("human-hair-m05.cub", face3.HairModel);
            Assert.AreNotEqual(human.Signature, face3.Signature, "another face is another visual");

            HeroVisualSpec elfFemale = HeroVisualComposer.Compose(Look(1, 1, face: 5), null);
            Assert.AreEqual(0x01, elfFemale.Kind);
            Assert.AreEqual("elf-head-f06.cub", elfFemale.HeadModel, "registry 0x4DD");
        }

        [Test]
        public void EveryPlayableSpeciesHasHeadAndHairModelsForEveryEditorIndex()
        {
            for (int race = 0; race < AlphaHeroCreation.RaceCount; race++)
            {
                for (int gender = 0; gender < 2; gender++)
                {
                    KindId kind = AlphaHeroCreation.KindOf(race, gender);
                    for (int face = 0; face < AlphaHeroCreation.FaceCount(kind); face++)
                    {
                        for (int hair = 0; hair < AlphaHeroCreation.HaircutCount(kind); hair++)
                        {
                            HeroVisualSpec spec = HeroVisualComposer.Compose(Look(race, gender, face, hair), null);
                            Assert.IsNotNull(spec.HeadModel, $"race {race} gender {gender} face {face}");
                            Assert.IsNotNull(spec.HairModel, $"race {race} gender {gender} hair {hair}");
                            StringAssert.Contains("head", spec.HeadModel);
                            StringAssert.Contains("hair", spec.HairModel);
                        }
                    }
                }
            }
        }

        [Test]
        public void HairColourIsTheSavedRgbOverTwoFiftyFive()
        {
            HeroVisualSpec spec = HeroVisualComposer.Compose(Look(0, 0, r: 0x20, g: 0x80, b: 0xFF), null);
            Assert.AreEqual(0x20 / 255f, spec.HairColour.r, 1e-6f);
            Assert.AreEqual(0x80 / 255f, spec.HairColour.g, 1e-6f);
            Assert.AreEqual(1f, spec.HairColour.b, 1e-6f);
        }

        [Test]
        public void HumanoidSpeciesShareTheActionRigExceptOrcFemale()
        {
            foreach (int kind in new[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0D, 0x0E, 0x0F, 0x10 })
            {
                Assert.IsTrue(HeroVisualComposer.SharesActionRig(kind, out string reason), $"0x{kind:x2}: {reason}");
            }

            Assert.IsFalse(HeroVisualComposer.SharesActionRig(0x0C, out string orcFemale));
            StringAssert.Contains("offset_head", orcFemale);
            StringAssert.Contains("scale_body", orcFemale);

            HeroVisualSpec spec = HeroVisualComposer.Compose(Look(3, 1), null);   // Orc female
            Assert.AreEqual(0x0C, spec.Kind);
            Assert.AreEqual(0x0C, spec.RigKind, "own actor: poses of the shared rig would be wrong");
            Assert.That(spec.Problems, Has.Some.Contains("SPECIES_POSE_DIFFERS"));
        }

        [Test]
        public void SpeciesHeadAndWeaponScalesComeFromTheAppearanceBlock()
        {
            HeroVisualSpec dwarf = HeroVisualComposer.Compose(Look(2, 0), null);
            Assert.AreEqual(0x02, dwarf.RigKind);
            Assert.AreEqual(0.9f / 1.01f, dwarf.HeadScale, 1e-6f, "scale_head 0.9 vs 1.01");
            Assert.AreEqual(1.2f / 0.95f, dwarf.WeaponScale, 1e-6f, "scale_weapon 1.2 vs 0.95");
            HeroVisualSpec elf = HeroVisualComposer.Compose(Look(1, 0), null);
            Assert.AreEqual(1f, elf.HeadScale);
            Assert.AreEqual(1f, elf.WeaponScale);
        }

        // ----- equipment / weapon mapping -----

        [Test]
        public void ItemModelFollowsMaterialRarityAndSeed()
        {
            Assert.IsTrue(AlphaCharacterModels.TrySelectItemModel(3, 17, 2, 0, 0, out string greatmace, out _));
            Assert.AreEqual("wood-greatmace02.cub", greatmace, "warrior kit: greatmace material 2");
            Assert.IsTrue(AlphaCharacterModels.TrySelectItemModel(3, 17, 1, 0, 0, out string iron, out _));
            Assert.AreEqual("iron-greatmace1.cub", iron);
            Assert.IsTrue(AlphaCharacterModels.TrySelectItemModel(3, 0, 0, 0, 3, out string seeded, out _));
            Assert.AreEqual("iron-sword1-random3.cub", seeded, "seed 3 of period 11");
            Assert.IsTrue(AlphaCharacterModels.TrySelectItemModel(3, 0, 0, 1, 0, out string rarity1, out _));
            Assert.AreEqual("iron-sword2.cub", rarity1, "byte +0x0C 1");
            Assert.IsTrue(AlphaCharacterModels.TrySelectItemModel(5, 0, 1, 0, 0, out string gloves, out _));
            Assert.AreEqual("iron-hand1.cub", gloves);
            Assert.IsFalse(AlphaCharacterModels.TrySelectItemModel(3, 1, 5, 0, 0, out _, out string empty));
            StringAssert.Contains("ITEM_SELECTOR_EMPTY_SLOT", empty);
            Assert.IsFalse(AlphaCharacterModels.TrySelectItemModel(3, 0, 64, 0, 0, out _, out string domain));
            StringAssert.Contains("ITEM_OUTSIDE_SELECTOR_DOMAIN", domain);
        }

        [Test]
        public void WeaponClassesGetTheirGrip()
        {
            Assert.AreEqual(HeroWeaponGrip.OneHanded, HeroVisualComposer.GripOf(0));
            Assert.AreEqual(HeroWeaponGrip.TwoHanded, HeroVisualComposer.GripOf(15));
            Assert.AreEqual(HeroWeaponGrip.TwoHanded, HeroVisualComposer.GripOf(16));
            Assert.AreEqual(HeroWeaponGrip.TwoHanded, HeroVisualComposer.GripOf(17));
            Assert.AreEqual(HeroWeaponGrip.Bow, HeroVisualComposer.GripOf(6));
            Assert.AreEqual(HeroWeaponGrip.Crossbow, HeroVisualComposer.GripOf(7));
            Assert.AreEqual(HeroWeaponGrip.Staff, HeroVisualComposer.GripOf(10));
            Assert.AreEqual(HeroWeaponGrip.Wand, HeroVisualComposer.GripOf(11));
            Assert.AreEqual(HeroWeaponGrip.Shield, HeroVisualComposer.GripOf(13));
        }

        [Test]
        public void StarterKitsOfEveryClassComposeWithoutProblems()
        {
            foreach (CharacterClass heroClass in new[] { CharacterClass.Warrior, CharacterClass.Ranger, CharacterClass.Mage, CharacterClass.Rogue })
            {
                var gear = new Dictionary<VisualEquipmentSlot, HeroVisualItem>();
                foreach (StarterKitItem kit in AlphaHeroCreation.StarterKit(heroClass))
                {
                    if (kit.Core == null || !kit.Equip.HasValue) continue;
                    gear[GameBootstrap.VisualSlotOf(kit.Equip.Value, kit.Core)] = new HeroVisualItem(
                        (int)kit.Core.Type, kit.Core.Subtype, (int)kit.Core.Modifier, kit.Core.Rarity, kit.Core.Seed);
                }

                HeroVisualSpec spec = HeroVisualComposer.Compose(AlphaHeroCreation.Default, gear);
                Assert.IsEmpty(spec.Problems, heroClass + ": " + string.Join("; ", spec.Problems));
                Assert.IsNotNull(spec.WeaponModel, heroClass.ToString());
                switch (heroClass)
                {
                    case CharacterClass.Warrior:
                        Assert.AreEqual("wood-greatmace02.cub", spec.WeaponModel);
                        Assert.AreEqual(HeroWeaponGrip.TwoHanded, spec.WeaponGrip);
                        break;
                    case CharacterClass.Ranger:
                        Assert.AreEqual(HeroWeaponGrip.Bow, spec.WeaponGrip);
                        break;
                    case CharacterClass.Mage:
                        Assert.AreEqual(HeroWeaponGrip.Staff, spec.WeaponGrip);
                        break;
                    case CharacterClass.Rogue:
                        Assert.AreEqual("iron-dagger1.cub", spec.WeaponModel);
                        Assert.AreEqual("iron-dagger1.cub", spec.OffhandModel);
                        Assert.IsTrue(spec.DualWield, "dagger + dagger");
                        break;
                }
            }
        }

        [Test]
        public void ArmourSlotsAndShieldUseTheSelectedModels()
        {
            HeroVisualSpec spec = HeroVisualComposer.Compose(AlphaHeroCreation.Default, Gear(
                (VisualEquipmentSlot.MainHand, new HeroVisualItem(3, 0, 1)),
                (VisualEquipmentSlot.OffHand, new HeroVisualItem(3, 13, 2)),
                (VisualEquipmentSlot.Chest, new HeroVisualItem(4, 0, 1)),
                (VisualEquipmentSlot.Hands, new HeroVisualItem(5, 0, 1)),
                (VisualEquipmentSlot.Feet, new HeroVisualItem(6, 0, 1))));
            Assert.IsEmpty(spec.Problems, string.Join("; ", spec.Problems));
            Assert.AreEqual("iron-sword1.cub", spec.WeaponModel);
            Assert.AreEqual("wood-shield01.cub", spec.OffhandModel);
            Assert.AreEqual(HeroWeaponGrip.Shield, spec.OffhandGrip);
            Assert.IsFalse(spec.DualWield, "a shield is not a second weapon");
            Assert.AreEqual("iron-chest1.cub", spec.ChestModel);
            Assert.AreEqual("iron-hand1.cub", spec.HandsModel);
            Assert.AreEqual("iron-foot1.cub", spec.FeetModel);

            CharacterVisualRequest request = spec.ToRequest(@"D:\content");
            Assert.AreEqual(Path.Combine(@"D:\content", "items", "iron-sword1.cubdec"), request.WeaponModelPath);
            Assert.AreEqual(Path.Combine(@"D:\content", "appearance", "human-head-m01.cubdec"), request.HeadModelPath);
            Assert.IsTrue(request.HideCapturedWeaponWhenEmpty);
            Assert.AreEqual(0x02, request.RigKind);
            Assert.IsNull(request.RacePartsKind, "the human rig keeps its own parts");
        }

        [Test]
        public void SlotsWithoutRigBlockAreReportedAndRingsAreNeverDrawn()
        {
            HeroVisualSpec spec = HeroVisualComposer.Compose(AlphaHeroCreation.Default, Gear(
                (VisualEquipmentSlot.Shoulders, new HeroVisualItem(7, 0, 1)),
                (VisualEquipmentSlot.Ring1, new HeroVisualItem(9, 0, 0x0B)),
                (VisualEquipmentSlot.Chest, new HeroVisualItem(3, 0))));
            Assert.That(spec.Problems, Has.Some.Contains(HeroVisualComposer.SlotNoRigBlock + " Shoulders"));
            Assert.That(spec.Problems, Has.None.Contains("Ring1"), "Alpha draws no ring block: nothing to report");
            Assert.That(spec.Problems, Has.Some.Contains("ITEM_TYPE_MISMATCH Chest"));
            Assert.IsNull(spec.ChestModel, "a weapon is never drawn as armour");
        }

        [Test]
        public void EmptyHandsDrawNoWeapon()
        {
            HeroVisualSpec spec = HeroVisualComposer.Compose(AlphaHeroCreation.Default, Gear());
            Assert.IsNull(spec.WeaponModel);
            Assert.AreEqual(HeroWeaponGrip.None, spec.WeaponGrip);
            CharacterVisualRequest request = spec.ToRequest(@"D:\content");
            Assert.AreEqual(-1, request.WeaponSubtype);
            Assert.IsTrue(request.HideCapturedWeaponWhenEmpty, "the captured greatsword of the rig must disappear");
        }

        // ----- determinism / reconstruction -----

        [Test]
        public void CompositionDoesNotDependOnEventOrderAndSurvivesNormalisation()
        {
            var a = Gear(
                (VisualEquipmentSlot.MainHand, new HeroVisualItem(3, 3, 1)),
                (VisualEquipmentSlot.OffHand, new HeroVisualItem(3, 3, 1)),
                (VisualEquipmentSlot.Feet, new HeroVisualItem(6, 0, 1)));
            var b = Gear(
                (VisualEquipmentSlot.Feet, new HeroVisualItem(6, 0, 1)),
                (VisualEquipmentSlot.OffHand, new HeroVisualItem(3, 3, 1)),
                (VisualEquipmentSlot.MainHand, new HeroVisualItem(3, 3, 1)));
            HeroAppearance look = Look(2, 1, face: 3, hair: 2, r: 10, g: 20, b: 30);
            string first = HeroVisualComposer.Compose(look, a).Signature;
            Assert.AreEqual(first, HeroVisualComposer.Compose(look, b).Signature);
            Assert.AreEqual(first, HeroVisualComposer.Compose(AlphaHeroCreation.Normalize(look), a).Signature,
                "a saved (normalised) appearance reconstructs the same visual");
            Assert.AreNotEqual(first, HeroVisualComposer.Compose(look, Gear((VisualEquipmentSlot.MainHand, new HeroVisualItem(3, 3, 1, 0, 1)))).Signature);
        }

        [Test]
        public void GroundItemModelUsesTheSameSelector()
        {
            Assert.AreEqual("wood-shield01.cub", GameVisualSink.GroundItemModelName(new RawAlphaItemId(3, 13), 2));
            Assert.AreEqual("iron-shield01.cub", GameVisualSink.GroundItemModelName(new RawAlphaItemId(3, 13), 1));
            Assert.AreEqual("lantern02.cub", GameVisualSink.GroundItemModelName(new RawAlphaItemId(0x18, 0), 1));
        }
    }
}
