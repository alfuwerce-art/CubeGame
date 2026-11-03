using System;
using CubeGame.Core.Equipment;
using CubeGame.Core.Items;
using NUnit.Framework;

namespace CubeGame.Tests.EditMode
{
    /// <summary>
    /// EditMode-тесты модели экипировки S04 (модуль A): совместимость слотов, замена, снятие,
    /// запрет дублирования экземпляра и атомарность при ошибке.
    /// </summary>
    public sealed class EquipmentLoadoutTests
    {
        [Test]
        public void EquippingCompatibleItemOccupiesSlot()
        {
            var loadout = new EquipmentLoadout();
            ItemInstance chest = CreateChest();

            ItemInstance previous = loadout.Equip(EquipmentSlot.Chest, chest);

            Assert.IsNull(previous, "слот был пуст");
            Assert.AreSame(chest, loadout.GetEquipped(EquipmentSlot.Chest));
            Assert.IsFalse(loadout.IsSlotEmpty(EquipmentSlot.Chest));
            Assert.AreEqual(1, loadout.EquippedCount);
        }

        [Test]
        public void WeaponFitsBothWeaponSlots()
        {
            var loadout = new EquipmentLoadout();
            ItemInstance main = CreateSword(1);
            ItemInstance off = CreateSword(2);

            loadout.Equip(EquipmentSlot.MainHand, main);
            loadout.Equip(EquipmentSlot.OffHand, off);

            Assert.AreSame(main, loadout.GetEquipped(EquipmentSlot.MainHand));
            Assert.AreSame(off, loadout.GetEquipped(EquipmentSlot.OffHand));
        }

        [TestCase(EquipmentSlot.Chest, ItemType.Weapon)]
        [TestCase(EquipmentSlot.Gloves, ItemType.Chest)]
        [TestCase(EquipmentSlot.Boots, ItemType.Gloves)]
        [TestCase(EquipmentSlot.Shoulders, ItemType.Boots)]
        [TestCase(EquipmentSlot.MainHand, ItemType.Chest)]
        [TestCase(EquipmentSlot.OffHand, ItemType.Shoulders)]
        public void WrongTypeForSlotIsRejected(EquipmentSlot slot, ItemType itemType)
        {
            var loadout = new EquipmentLoadout();
            ItemInstance item = CreateItemOfType(itemType);

            Assert.Throws<ArgumentException>(() => loadout.Equip(slot, item), "несовместимый тип должен отвергаться");
            Assert.AreEqual(0, loadout.EquippedCount, "после отказа экипировка не меняется");
        }

        [Test]
        public void ReplacingItemReturnsPreviousAndKeepsNew()
        {
            var loadout = new EquipmentLoadout();
            ItemInstance first = CreateSword(1);
            ItemInstance second = CreateSword(10);

            loadout.Equip(EquipmentSlot.MainHand, first);
            ItemInstance replaced = loadout.Equip(EquipmentSlot.MainHand, second);

            Assert.AreSame(first, replaced, "возвращается вытесненный предмет");
            Assert.AreSame(second, loadout.GetEquipped(EquipmentSlot.MainHand));
            Assert.AreEqual(1, loadout.EquippedCount);
        }

        [Test]
        public void UnequipReturnsItemAndClearsSlot()
        {
            var loadout = new EquipmentLoadout();
            ItemInstance boots = CreateItemOfType(ItemType.Boots);
            loadout.Equip(EquipmentSlot.Boots, boots);

            ItemInstance removed = loadout.Unequip(EquipmentSlot.Boots);

            Assert.AreSame(boots, removed);
            Assert.IsNull(loadout.GetEquipped(EquipmentSlot.Boots));
            Assert.IsTrue(loadout.IsSlotEmpty(EquipmentSlot.Boots));
            Assert.IsNull(loadout.Unequip(EquipmentSlot.Boots), "повторное снятие пустого слота безопасно");
        }

        [Test]
        public void SameInstanceCannotOccupyTwoSlots()
        {
            var loadout = new EquipmentLoadout();
            ItemInstance sword = CreateSword(1);
            loadout.Equip(EquipmentSlot.MainHand, sword);

            Assert.Throws<InvalidOperationException>(() => loadout.Equip(EquipmentSlot.OffHand, sword));

            Assert.AreSame(sword, loadout.GetEquipped(EquipmentSlot.MainHand), "первый слот не тронут");
            Assert.IsNull(loadout.GetEquipped(EquipmentSlot.OffHand), "второй слот остался пустым");
        }

        [Test]
        public void ValueEqualButDistinctInstancesAreAllowedInDifferentSlots()
        {
            var loadout = new EquipmentLoadout();
            ItemInstance first = CreateSword(1, seed: 77);
            ItemInstance second = CreateSword(1, seed: 77);

            loadout.Equip(EquipmentSlot.MainHand, first);
            loadout.Equip(EquipmentSlot.OffHand, second);

            Assert.AreEqual(2, loadout.EquippedCount, "дублирование запрещено для одного экземпляра, а не для равных данных");
        }

        [Test]
        public void NullItemIsRejectedWithoutStateChange()
        {
            var loadout = new EquipmentLoadout();
            Assert.Throws<ArgumentNullException>(() => loadout.Equip(EquipmentSlot.MainHand, null));
            Assert.AreEqual(0, loadout.EquippedCount);
        }

        [Test]
        public void UnknownSlotIsRejected()
        {
            var loadout = new EquipmentLoadout();
            Assert.Throws<ArgumentOutOfRangeException>(() => loadout.Equip((EquipmentSlot)42, CreateSword(1)));
            Assert.Throws<ArgumentOutOfRangeException>(() => loadout.Unequip((EquipmentSlot)42));
        }

        [Test]
        public void FailedEquipKeepsPreviousItem()
        {
            var loadout = new EquipmentLoadout();
            ItemInstance chest = CreateChest();
            loadout.Equip(EquipmentSlot.Chest, chest);

            Assert.Throws<ArgumentException>(() => loadout.Equip(EquipmentSlot.Chest, CreateSword(1)));

            Assert.AreSame(chest, loadout.GetEquipped(EquipmentSlot.Chest), "неудачная замена не портит слот");
        }

        [Test]
        public void SlotCompatibilityTableMatchesModel()
        {
            Assert.IsTrue(EquipmentLoadout.IsSlotCompatible(EquipmentSlot.MainHand, ItemType.Weapon));
            Assert.IsTrue(EquipmentLoadout.IsSlotCompatible(EquipmentSlot.OffHand, ItemType.Weapon));
            Assert.IsTrue(EquipmentLoadout.IsSlotCompatible(EquipmentSlot.Chest, ItemType.Chest));
            Assert.IsTrue(EquipmentLoadout.IsSlotCompatible(EquipmentSlot.Gloves, ItemType.Gloves));
            Assert.IsTrue(EquipmentLoadout.IsSlotCompatible(EquipmentSlot.Boots, ItemType.Boots));
            Assert.IsTrue(EquipmentLoadout.IsSlotCompatible(EquipmentSlot.Shoulders, ItemType.Shoulders));

            Assert.IsFalse(EquipmentLoadout.IsSlotCompatible(EquipmentSlot.Chest, ItemType.Ring));
            Assert.IsFalse(EquipmentLoadout.IsSlotCompatible(EquipmentSlot.MainHand, ItemType.Amulet));
        }

        private static ItemInstance CreateSword(int level, int seed = 1)
        {
            return new ItemInstance(ItemType.Weapon, (byte)WeaponSubtype.Sword, level, 0, seed);
        }

        private static ItemInstance CreateChest()
        {
            return new ItemInstance(ItemType.Chest, 0, 10, 2, seed: 5);
        }

        private static ItemInstance CreateItemOfType(ItemType type)
        {
            switch (type)
            {
                case ItemType.Weapon:
                    return CreateSword(1);
                case ItemType.Chest:
                    return CreateChest();
                case ItemType.Boots:
                    return new ItemInstance(ItemType.Boots, 0, 5, 0, seed: 1);
                case ItemType.Gloves:
                    return new ItemInstance(ItemType.Gloves, 0, 5, 0, seed: 1);
                case ItemType.Shoulders:
                    return new ItemInstance(ItemType.Shoulders, 0, 5, 0, seed: 1);
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, "Тестовый тип не поддержан.");
            }
        }
    }
}
