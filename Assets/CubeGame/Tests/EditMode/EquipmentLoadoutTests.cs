using System;
using System.Collections.Generic;
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
        [TestCase(EquipmentSlot.Boots, ItemType.Chest)]
        [TestCase(EquipmentSlot.Gloves, ItemType.Boots)]
        [TestCase(EquipmentSlot.Shoulders, ItemType.Gloves)]
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
            ItemInstance boots = CreateItemOfType(ItemType.Gloves);
            loadout.Equip(EquipmentSlot.Gloves, boots);

            ItemInstance removed = loadout.Unequip(EquipmentSlot.Gloves);

            Assert.AreSame(boots, removed);
            Assert.IsNull(loadout.GetEquipped(EquipmentSlot.Gloves));
            Assert.IsTrue(loadout.IsSlotEmpty(EquipmentSlot.Gloves));
            Assert.IsNull(loadout.Unequip(EquipmentSlot.Gloves), "повторное снятие пустого слота безопасно");
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
            Assert.IsTrue(EquipmentLoadout.IsSlotCompatible(EquipmentSlot.Boots, ItemType.Boots));
            Assert.IsTrue(EquipmentLoadout.IsSlotCompatible(EquipmentSlot.Gloves, ItemType.Gloves));
            Assert.IsTrue(EquipmentLoadout.IsSlotCompatible(EquipmentSlot.Shoulders, ItemType.Shoulders));

            Assert.IsFalse(EquipmentLoadout.IsSlotCompatible(EquipmentSlot.Chest, ItemType.Ring));
            Assert.IsFalse(EquipmentLoadout.IsSlotCompatible(EquipmentSlot.MainHand, ItemType.Amulet));
        }

        // --- REVIEW-FIX-01: защита коллекции Slots -----------------------------

        [Test]
        public void SlotsCannotBeCastToArray()
        {
            Assert.IsFalse(EquipmentLoadout.Slots is EquipmentSlot[],
                "внутренний массив не должен быть доступен через приведение");
        }

        [Test]
        public void SlotsCannotBeModifiedThroughIList()
        {
            var asList = EquipmentLoadout.Slots as IList<EquipmentSlot>;
            Assert.IsNotNull(asList, "контракт IReadOnlyList сохраняется, приведение к IList допустимо только для чтения");

            Assert.Throws<NotSupportedException>(() => asList[0] = EquipmentSlot.Gloves,
                "запись по индексу должна отвергаться");
            Assert.Throws<NotSupportedException>(() => asList.Add(EquipmentSlot.Gloves),
                "добавление должно отвергаться");
            Assert.Throws<NotSupportedException>(() => asList.RemoveAt(0),
                "удаление должно отвергаться");

            Assert.AreEqual(EquipmentSlot.MainHand, EquipmentLoadout.Slots[0], "содержимое не изменилось");
            Assert.AreEqual(6, EquipmentLoadout.Slots.Count);
        }

        [Test]
        public void SlotsKeepOrderAndNumericValues()
        {
            CollectionAssert.AreEqual(
                new[]
                {
                    EquipmentSlot.MainHand,
                    EquipmentSlot.OffHand,
                    EquipmentSlot.Chest,
                    EquipmentSlot.Boots,
                    EquipmentSlot.Gloves,
                    EquipmentSlot.Shoulders,
                },
                EquipmentLoadout.Slots,
                "порядок и состав слотов не должны меняться");

            Assert.AreEqual(0, (int)EquipmentSlot.MainHand);
            Assert.AreEqual(1, (int)EquipmentSlot.OffHand);
            Assert.AreEqual(2, (int)EquipmentSlot.Chest);
            Assert.AreEqual(3, (int)EquipmentSlot.Boots);
            Assert.AreEqual(4, (int)EquipmentSlot.Gloves);
            Assert.AreEqual(5, (int)EquipmentSlot.Shoulders);
        }

        [Test]
        public void LoadoutWorksAfterMutationAttempt()
        {
            var asList = EquipmentLoadout.Slots as IList<EquipmentSlot>;
            Assert.Throws<NotSupportedException>(() => asList.Clear());

            var loadout = new EquipmentLoadout();
            ItemInstance sword = CreateSword(1);
            loadout.Equip(EquipmentSlot.MainHand, sword);

            Assert.AreSame(sword, loadout.GetEquipped(EquipmentSlot.MainHand), "экипировка работает как прежде");
            Assert.AreEqual(1, loadout.EquippedCount);
            Assert.AreEqual(6, EquipmentLoadout.Slots.Count, "список слотов не повреждён");
            Assert.AreEqual(EquipmentSlot.Chest, EquipmentLoadout.Slots[2]);
        }

        private static ItemInstance CreateSword(int level, int seed = 1)
        {
            return new ItemInstance(ItemType.Weapon, (byte)WeaponSubtype.Dagger, level, 0, seed);
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
                case ItemType.Gloves:
                    return new ItemInstance(ItemType.Gloves, 0, 5, 0, seed: 1);
                case ItemType.Boots:
                    return new ItemInstance(ItemType.Boots, 0, 5, 0, seed: 1);
                case ItemType.Shoulders:
                    return new ItemInstance(ItemType.Shoulders, 0, 5, 0, seed: 1);
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, "Тестовый тип не поддержан.");
            }
        }
    }
}
