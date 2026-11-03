using System;
using CubeGame.Core.Characters;
using CubeGame.Core.Equipment;
using CubeGame.Core.Items;
using NUnit.Framework;

namespace CubeGame.Tests.EditMode
{
    /// <summary>
    /// EditMode-тесты агрегатора характеристик экипировки S04 (модуль B).
    ///
    /// Эталоны статов предметов — `alpha_item_stats_reference.py --check` (PASS):
    /// sword L1 r0 → 2.00, greatmace L1 r0 → 8.00, chest L1 r0 → armor 10.00.
    /// Никаких собственных формул здесь нет: агрегатор только вызывает вычислители S02.
    /// </summary>
    public sealed class EquipmentStatsTests
    {
        private const double Tolerance = 1e-9;

        [Test]
        public void SingleWeaponStatsMatchItemReference()
        {
            var loadout = new EquipmentLoadout();
            loadout.Equip(EquipmentSlot.MainHand, CreateSword(level: 1, rarity: 0));

            EquipmentStatSummary summary = EquipmentStats.Compute(loadout);

            Assert.That(summary.MainHandDamage, Is.EqualTo(2.0).Within(Tolerance), "sword L1 r0 → 2.00");
            Assert.That(summary.OffHandDamage, Is.EqualTo(0.0).Within(Tolerance));
            Assert.That(summary.WeaponDamageBonus, Is.EqualTo(2.0).Within(Tolerance));
            Assert.That(summary.TotalArmor, Is.EqualTo(0.0).Within(Tolerance), "оружие не даёт броню предмета");
            Assert.That(summary.TotalBlockValue, Is.EqualTo(0.0).Within(Tolerance), "оружие не даёт блок");
            Assert.AreEqual(1, summary.PerItem.Count);
            Assert.AreEqual(EquipmentSlot.MainHand, summary.PerItem[0].Slot);
        }

        [Test]
        public void TwoWeaponsProduceIndependentValues()
        {
            var loadout = new EquipmentLoadout();
            loadout.Equip(EquipmentSlot.MainHand, CreateSword(level: 1, rarity: 0));
            loadout.Equip(EquipmentSlot.OffHand, CreateGreatmace(level: 1, rarity: 0));

            EquipmentStatSummary summary = EquipmentStats.Compute(loadout);

            Assert.That(summary.MainHandDamage, Is.EqualTo(2.0).Within(Tolerance));
            Assert.That(summary.OffHandDamage, Is.EqualTo(8.0).Within(Tolerance), "greatmace L1 r0 → 8.00");
            Assert.That(summary.WeaponDamageBonus, Is.EqualTo(10.0).Within(Tolerance), "аддитивная сумма оружия");
            Assert.AreEqual(2, summary.PerItem.Count);
        }

        [Test]
        public void ArmorAggregatesAcrossSlots()
        {
            var loadout = new EquipmentLoadout();
            loadout.Equip(EquipmentSlot.Chest, new ItemInstance(ItemType.Chest, 0, 1, 0, seed: 0));
            loadout.Equip(EquipmentSlot.Boots, new ItemInstance(ItemType.Boots, 0, 1, 0, seed: 0));

            EquipmentStatSummary summary = EquipmentStats.Compute(loadout);

            // chest L1 r0 seed0 → armor 10.00 (×5·1·2.0); boots → ×5·0.5·2.0 = 5.00
            Assert.That(summary.TotalArmor, Is.EqualTo(15.0).Within(1e-6));
            Assert.That(summary.TotalBlockValue, Is.EqualTo(1.5).Within(1e-6), "block: 1.0 + 0.5");
        }

        [Test]
        public void WeaponAndArmorStatsAreNotMixed()
        {
            var loadout = new EquipmentLoadout();
            loadout.Equip(EquipmentSlot.MainHand, CreateSword(level: 10, rarity: 2));
            loadout.Equip(EquipmentSlot.Chest, new ItemInstance(ItemType.Chest, 0, 10, 2, seed: 0));

            EquipmentStatSummary summary = EquipmentStats.Compute(loadout);

            Assert.That(summary.WeaponDamageBonus, Is.EqualTo(ItemStatsCalculator.WeaponDamage(10, 2, WeaponSubtype.Sword)).Within(1e-9));
            Assert.That(summary.TotalArmor, Is.EqualTo(ItemStatsCalculator.Armor(10, 2, ItemType.Chest, ItemModifier.None, 0, 0)).Within(1e-9));
            Assert.That(summary.TotalBlockValue, Is.EqualTo(ItemStatsCalculator.BlockValue(10, 2, ItemType.Chest)).Within(1e-9));
            Assert.AreNotEqual(summary.TotalArmor, summary.TotalBlockValue, "Armor и BlockValue — разные величины");
        }

        [Test]
        public void EmptyLoadoutHasZeroStats()
        {
            EquipmentStatSummary summary = EquipmentStats.Compute(new EquipmentLoadout());

            Assert.AreEqual(0, summary.PerItem.Count);
            Assert.That(summary.WeaponDamageBonus, Is.EqualTo(0.0).Within(Tolerance));
            Assert.That(summary.TotalArmor, Is.EqualTo(0.0).Within(Tolerance));
            Assert.That(summary.TotalBlockValue, Is.EqualTo(0.0).Within(Tolerance));
        }

        [Test]
        public void ItemParametersChangeComputedStats()
        {
            var low = new EquipmentLoadout();
            low.Equip(EquipmentSlot.MainHand, CreateSword(level: 1, rarity: 0));

            var high = new EquipmentLoadout();
            high.Equip(EquipmentSlot.MainHand, CreateSword(level: 10, rarity: 4));

            double lowDamage = EquipmentStats.Compute(low).MainHandDamage;
            double highDamage = EquipmentStats.Compute(high).MainHandDamage;

            Assert.Greater(highDamage, lowDamage, "уровень и редкость должны повышать стат");
            Assert.That(highDamage, Is.EqualTo(ItemStatsCalculator.WeaponDamage(10, 4, WeaponSubtype.Sword)).Within(1e-9));
        }

        [Test]
        public void PowerBonusAndModifierParticipateWhereFormulaUsesThem()
        {
            var plain = new EquipmentLoadout();
            plain.Equip(EquipmentSlot.Chest, new ItemInstance(ItemType.Chest, 0, 10, 2, seed: 0));

            var boosted = new EquipmentLoadout();
            boosted.Equip(EquipmentSlot.Chest, new ItemInstance(ItemType.Chest, 0, 10, 2, seed: 0, ItemModifier.M01, powerBonus: 5));

            EquipmentStatSummary plainStats = EquipmentStats.Compute(plain);
            EquipmentStatSummary boostedStats = EquipmentStats.Compute(boosted);

            Assert.Greater(boostedStats.TotalArmor, plainStats.TotalArmor, "powerBonus и модификатор входят в формулу брони");
        }

        [Test]
        public void EquippingArmorDoesNotChangeCharacterMaxHealth()
        {
            // OQ-25 остаётся OPEN: агрегатор не подставляет Armor/BlockValue в HP.
            var sheet = new CharacterSheet();
            double healthWithoutArmor = EquipmentCombatAdapter.ComputeMaxHealth(sheet);

            var loadout = new EquipmentLoadout();
            loadout.Equip(EquipmentSlot.Chest, new ItemInstance(ItemType.Chest, 0, 10, 4, seed: 0));
            loadout.Equip(EquipmentSlot.Boots, new ItemInstance(ItemType.Boots, 0, 10, 4, seed: 0));

            double healthWithArmor = EquipmentCombatAdapter.ComputeMaxHealth(sheet);

            Assert.That(healthWithArmor, Is.EqualTo(healthWithoutArmor).Within(Tolerance),
                "HP не «додумывается» из экипировки (OQ-25 OPEN)");
            Assert.That(healthWithArmor, Is.EqualTo(260.0).Within(1e-6), "Warrior L1: 100·2·1.3");
        }

        [Test]
        public void ExplicitEquipmentHealthBonusStillApplies()
        {
            var sheet = new CharacterSheet { EquipmentHealthBonus = 8.0 };

            Assert.That(EquipmentCombatAdapter.ComputeMaxHealth(sheet), Is.EqualTo(268.0).Within(1e-6),
                "явный бонус (как в проверенном сейве) продолжает работать");
        }

        [Test]
        public void NullArgumentsAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => EquipmentStats.Compute(null));
            Assert.Throws<ArgumentNullException>(() => EquipmentStats.ComputeItem(EquipmentSlot.MainHand, null));
            Assert.Throws<ArgumentNullException>(() => EquipmentCombatAdapter.ComputeTargetArmor(null));
            Assert.Throws<ArgumentNullException>(() => EquipmentCombatAdapter.ComputeMaxHealth(null));
        }

        private static ItemInstance CreateSword(int level, int rarity, int seed = 1)
        {
            return new ItemInstance(ItemType.Weapon, (byte)WeaponSubtype.Sword, level, rarity, seed);
        }

        private static ItemInstance CreateGreatmace(int level, int rarity, int seed = 2)
        {
            return new ItemInstance(ItemType.Weapon, (byte)WeaponSubtype.Greatmace, level, rarity, seed);
        }
    }
}
