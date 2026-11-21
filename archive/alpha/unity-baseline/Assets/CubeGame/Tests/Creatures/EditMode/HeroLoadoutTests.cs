// D4 — EditMode checks of the hero loadout mapping and of the ground item model lookup (no content cache needed).
using CubeGame.Content;
using CubeGame.Features.Contracts;
using CubeGame.Gameplay;
using CubeGame.Presentation.Visual;
using NUnit.Framework;

namespace CubeGame.Tests.Creatures.EditMode
{
    public class HeroLoadoutTests
    {
        const string Items = @"D:\items";

        static CharacterVisualRequest Request()
        {
            CharacterVisualRequest request = CharacterVisualRequest.Default;
            request.Kind = 0x02;
            return request;
        }

        [Test]
        public void MainHandWeaponUsesTheAlphaSubtypeAndModel()
        {
            var ok = HeroLoadout.TryApply(Request(), VisualEquipmentSlot.MainHand, new RawAlphaItemId(3, 15), Items,
                out var updated, out var problem);
            Assert.IsTrue(ok, problem);
            Assert.AreEqual(15, updated.WeaponSubtype);
            StringAssert.EndsWith("greatsword.cubdec", updated.WeaponModelPath);
            StringAssert.StartsWith(Items, updated.WeaponModelPath, "the model lives in the staged items directory");
        }

        [Test]
        public void EmptyMainHandRemovesTheWeapon()
        {
            var withWeapon = HeroLoadout.TryApply(Request(), VisualEquipmentSlot.MainHand, new RawAlphaItemId(3, 0), Items,
                out var armed, out _);
            Assert.IsTrue(withWeapon);
            Assert.IsTrue(HeroLoadout.TryApply(armed, VisualEquipmentSlot.MainHand, null, Items, out var empty, out var problem), problem);
            Assert.AreEqual(-1, empty.WeaponSubtype);
            Assert.IsNull(empty.WeaponModelPath);
        }

        [Test]
        public void ArmourSlotsFollowAlphaItemTypes()
        {
            Assert.IsTrue(HeroLoadout.TryApply(Request(), VisualEquipmentSlot.Chest, new RawAlphaItemId(4, 0), Items,
                out var chest, out var p1), p1);
            Assert.AreEqual(0, chest.ChestSubtype);
            Assert.IsTrue(HeroLoadout.TryApply(Request(), VisualEquipmentSlot.Hands, new RawAlphaItemId(5, 1), Items,
                out var hands, out var p2), p2);
            Assert.AreEqual(1, hands.HandsSubtype);
            Assert.IsTrue(HeroLoadout.TryApply(Request(), VisualEquipmentSlot.Feet, new RawAlphaItemId(6, 2), Items,
                out var feet, out var p3), p3);
            Assert.AreEqual(2, feet.FeetSubtype);
        }

        [Test]
        public void WrongItemTypeIsRefusedInsteadOfShownInTheWrongSlot()
        {
            Assert.IsFalse(HeroLoadout.TryApply(Request(), VisualEquipmentSlot.Chest, new RawAlphaItemId(3, 0), Items,
                out var unchanged, out var problem));
            StringAssert.Contains(HeroLoadout.ItemTypeMismatch, problem);
            Assert.AreEqual(Request().ChestSubtype, unchanged.ChestSubtype);
            Assert.IsFalse(HeroLoadout.TryApply(Request(), VisualEquipmentSlot.MainHand, new RawAlphaItemId(1, 1), Items,
                out _, out var weaponProblem));
            StringAssert.Contains(HeroLoadout.ItemTypeMismatch, weaponProblem);
        }

        [Test]
        public void UnsupportedSlotsAreReportedNotFaked()
        {
            Assert.IsFalse(HeroLoadout.TryApply(Request(), VisualEquipmentSlot.Ring1, new RawAlphaItemId(8, 0), Items,
                out _, out var problem));
            StringAssert.Contains(HeroLoadout.SlotNotImplemented, problem);
        }

        [Test]
        public void OffHandUsesTheWeaponSubtypeModel()
        {
            Assert.IsTrue(HeroLoadout.TryApply(Request(), VisualEquipmentSlot.OffHand, new RawAlphaItemId(3, 13), Items,
                out var updated, out var problem), problem);
            Assert.AreEqual(13, updated.OffhandSubtype);
            StringAssert.EndsWith(".cubdec", updated.OffhandModelPath);
        }

        [Test]
        public void GroundItemModelComesFromTheAlphaContentTables()
        {
            string consumable = GameVisualSink.GroundItemModelName(new RawAlphaItemId(1, 1));
            Assert.AreEqual("life-potion.cub", consumable, "AlphaHeldItemCatalog knows the life potion model");
            string weapon = GameVisualSink.GroundItemModelName(new RawAlphaItemId(3, 0));
            Assert.AreEqual("iron-sword1.cub", weapon);
            Assert.IsNull(GameVisualSink.GroundItemModelName(new RawAlphaItemId(99, 99)));
        }
    }
}
