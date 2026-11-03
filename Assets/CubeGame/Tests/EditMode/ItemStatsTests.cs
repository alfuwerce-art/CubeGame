using System;
using CubeGame.Core.Items;
using NUnit.Framework;

namespace CubeGame.Tests.EditMode
{
    /// <summary>
    /// Тесты структуры и вычисления статов предметов S02.
    ///
    /// Эталон: docs/research/alpha2013/tools/alpha_item_stats_reference.py --check (PASS, exit 0, 2026-11-03)
    /// и таблица контрольных значений F_items §F2. Ожидания не подбирались вручную.
    /// </summary>
    public sealed class ItemStatsTests
    {
        private const double Tolerance = 5e-3;

        [TestCase(1, 0, WeaponSubtype.Sword, 2.00)]
        [TestCase(1, 4, WeaponSubtype.Sword, 4.00)]
        [TestCase(1, 0, WeaponSubtype.Greatmace, 8.00)]
        [TestCase(1, 4, WeaponSubtype.Greatmace, 16.00)]
        [TestCase(10, 0, WeaponSubtype.Sword, 3.81)]
        [TestCase(10, 4, WeaponSubtype.Sword, 7.63)]
        [TestCase(10, 0, WeaponSubtype.Greatmace, 15.25)]
        [TestCase(10, 4, WeaponSubtype.Greatmace, 30.51)]
        [TestCase(50, 0, WeaponSubtype.Sword, 8.76)]
        [TestCase(50, 4, WeaponSubtype.Sword, 17.51)]
        [TestCase(50, 0, WeaponSubtype.Greatmace, 35.03)]
        [TestCase(50, 4, WeaponSubtype.Greatmace, 70.06)]
        public void WeaponDamageMatchesAlphaReference(int level, int rarity, WeaponSubtype subtype, double expected)
        {
            Assert.That(ItemStatsCalculator.WeaponDamage(level, rarity, subtype),
                Is.EqualTo(expected).Within(Tolerance),
                $"weapon damage level={level} rarity={rarity} subtype={subtype}");
        }

        [Test]
        public void LongswordUsesRegularMultiplier()
        {
            // Alpha 004c7f60: longsword → ×4 (не ×8, хотя входит в «спецоружие» для других расчётов).
            Assert.That(ItemStatsCalculator.WeaponDamage(1, 0, WeaponSubtype.Longsword),
                Is.EqualTo(4.0).Within(Tolerance));
        }

        [Test]
        public void ShieldUsesSmallMultiplier()
        {
            Assert.That(ItemStatsCalculator.WeaponDamage(1, 0, WeaponSubtype.Shield),
                Is.EqualTo(2.0).Within(Tolerance));
        }

        [TestCase(1, 0, 10.00)]
        [TestCase(1, 4, 20.00)]
        [TestCase(10, 0, 19.07)]
        [TestCase(10, 4, 38.13)]
        [TestCase(50, 0, 43.78)]
        [TestCase(50, 4, 87.57)]
        public void ChestArmorMatchesAlphaReference(int level, int rarity, double expected)
        {
            Assert.That(ItemStatsCalculator.Armor(level, rarity, ItemType.Chest),
                Is.EqualTo(expected).Within(Tolerance),
                $"armor level={level} rarity={rarity}");
        }

        [Test]
        public void NonChestArmorUsesHalfRarityFactor()
        {
            double chest = ItemStatsCalculator.Armor(1, 0, ItemType.Chest);
            double boots = ItemStatsCalculator.Armor(1, 0, ItemType.Boots);

            Assert.That(boots, Is.EqualTo(chest * 0.5).Within(Tolerance), "у не-нагрудника rarityFactor 0.5");
        }

        [Test]
        public void WeaponAndRingGiveNoArmor()
        {
            Assert.AreEqual(0.0, ItemStatsCalculator.Armor(10, 2, ItemType.Weapon));
            Assert.AreEqual(0.0, ItemStatsCalculator.Armor(10, 2, ItemType.Ring));
        }

        [Test]
        public void ArmorModifierM01AddsMaterialMultiplier()
        {
            double plain = ItemStatsCalculator.Armor(1, 0, ItemType.Chest);
            double upgraded = ItemStatsCalculator.Armor(1, 0, ItemType.Chest, ItemModifier.M01);

            // base·5·1·(2.0 + 1.0) против base·5·1·2.0
            Assert.That(upgraded, Is.EqualTo(plain * 1.5).Within(Tolerance));
        }

        // --- REVIEW-FIX-01 / FIX-03: арифметика seed ---------------------------

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(100)]
        [TestCase(int.MaxValue)]
        [TestCase(int.MinValue)]
        [TestCase(-1)]
        [TestCase(-7)]
        [TestCase(-100)]
        public void SeedMaterialIndexMatchesPythonReference(int seed)
        {
            // Эталон: python -c "print((seed << 3) % 0x15)" (неотрицательный остаток, как в
            // alpha_item_stats_reference.py). Значения зафиксированы вручную из прогона 2026-11-03.
            int expected;
            switch (seed)
            {
                case 0: expected = 0; break;
                case 1: expected = 8; break;
                case 100: expected = 2; break;
                case int.MaxValue: expected = 8; break;
                case int.MinValue: expected = 5; break;
                case -1: expected = 13; break;
                case -7: expected = 7; break;
                case -100: expected = 19; break;
                default: throw new ArgumentOutOfRangeException(nameof(seed));
            }

            Assert.AreEqual(expected, ItemStatsCalculator.SeedMaterialIndex(seed),
                $"индекс материала для seed={seed} должен совпадать с Python-эталоном");
        }

        [TestCase(0, 10.0)]
        [TestCase(1, 8.0)]
        [TestCase(100, 9.5)]
        [TestCase(int.MaxValue, 8.0)]
        [TestCase(int.MinValue, 8.75)]
        [TestCase(-1, 6.75)]
        [TestCase(-100, 5.25)]
        public void ArmorMaterialMultiplierMatchesPythonReference(int seed, double expectedArmor)
        {
            // Нагрудник, уровень 1, редкость 0: base·5·1·(1 + (1 − index/20)).
            double armor = ItemStatsCalculator.Armor(1, 0, ItemType.Chest, ItemModifier.None, seed);
            Assert.That(armor, Is.EqualTo(expectedArmor).Within(Tolerance),
                $"броня при seed={seed} (материальный множитель {ItemStatsCalculator.SeedMaterialMultiplier(seed)})");
        }

        [Test]
        public void SeedMaterialIndexIsPeriodicAndNeverNegative()
        {
            for (int seed = -60; seed <= 60; seed++)
            {
                int index = ItemStatsCalculator.SeedMaterialIndex(seed);
                Assert.That(index, Is.InRange(0, 20), $"индекс вне 0…20 для seed={seed}");
                Assert.AreEqual(index, ItemStatsCalculator.SeedMaterialIndex(seed + 21),
                    $"индекс должен повторяться с периодом 21 (seed={seed})");
            }
        }

        [Test]
        public void LargeSeedDoesNotOverflowIntShift()
        {
            // Прежняя реализация делала seed << 3 в int: для int.MaxValue сдвиг переполнялся
            // и давал отрицательный остаток (материал 2.4 вместо 1.6). Проверяем оба края диапазона.
            Assert.AreEqual(1.6, ItemStatsCalculator.SeedMaterialMultiplier(int.MaxValue), 1e-9);
            Assert.AreEqual(1.75, ItemStatsCalculator.SeedMaterialMultiplier(int.MinValue), 1e-9);
        }

        [Test]
        public void ArmorMaterialMultiplierDependsOnSeed()
        {
            // Alpha 004c70b0: materialMult = 1 + (1 - (seed*8 mod 21)/20), диапазон 1.0…2.0.
            // Формулировка F_items «в пределах ±5 %» была неточной; в REVIEW-FIX-01 исправлена
            // в спецификации (формула не менялась).
            double seedZero = ItemStatsCalculator.Armor(1, 0, ItemType.Chest, ItemModifier.None, seed: 0);
            double seedOne = ItemStatsCalculator.Armor(1, 0, ItemType.Chest, ItemModifier.None, seed: 1);
            double seedLarge = ItemStatsCalculator.Armor(1, 0, ItemType.Chest, ItemModifier.None, seed: 100);

            Assert.That(seedZero, Is.EqualTo(10.0).Within(Tolerance), "seed 0 → materialMult 2.0");
            Assert.That(seedOne, Is.EqualTo(8.0).Within(Tolerance), "seed 1 → materialMult 1.6");
            Assert.That(seedLarge / seedZero, Is.InRange(0.49, 1.01), "seed меняет материал в пределах 1.0…2.0");
        }

        [Test]
        public void BlockValueAppliesOnlyToArmorTypes()
        {
            Assert.That(ItemStatsCalculator.BlockValue(1, 0, ItemType.Chest), Is.EqualTo(1.0).Within(Tolerance));
            Assert.That(ItemStatsCalculator.BlockValue(1, 0, ItemType.Boots), Is.EqualTo(0.5).Within(Tolerance));
            Assert.AreEqual(0.0, ItemStatsCalculator.BlockValue(1, 0, ItemType.Weapon));
        }

        [Test]
        public void BlockModifierReducesValue()
        {
            Assert.That(ItemStatsCalculator.BlockValue(1, 0, ItemType.Chest, ItemModifier.M12),
                Is.EqualTo(0.8).Within(Tolerance), "модификатор 0x12 → ×0.8");
            Assert.That(ItemStatsCalculator.BlockValue(1, 0, ItemType.Chest, ItemModifier.M19),
                Is.EqualTo(0.75).Within(Tolerance), "модификаторы 0x17/0x19 → ×0.75");
        }

        [TestCase(ItemType.Weapon, 20)]
        [TestCase(ItemType.Chest, 20)]
        [TestCase(ItemType.Boots, 10)]
        [TestCase(ItemType.Ring, 10)]
        [TestCase(ItemType.Amulet, 15)]
        [TestCase(ItemType.Formula, 2)]
        public void ValueUsesTypeMultiplier(ItemType type, int expected)
        {
            Assert.AreEqual(expected, ItemStatsCalculator.Value(1, 0, type), $"value type={type}");
        }

        [Test]
        public void ValueIsNeverBelowOne()
        {
            Assert.GreaterOrEqual(ItemStatsCalculator.Value(1, 0, ItemType.Formula), 1);
        }

        [Test]
        public void PowerBonusRaisesEffectiveLevel()
        {
            Assert.AreEqual(10.5, ItemStatsCalculator.EffectiveLevel(10, 5), 1e-9);
            double withoutBonus = ItemStatsCalculator.WeaponDamage(10, 0, WeaponSubtype.Sword);
            double withBonus = ItemStatsCalculator.WeaponDamage(10, 0, WeaponSubtype.Sword, powerBonus: 5);
            Assert.Greater(withBonus, withoutBonus, "powerBonus должен повышать статы");
        }

        [TestCase(true, false, false, 1)]
        [TestCase(true, true, false, 2)]
        [TestCase(true, true, true, 3)]
        [TestCase(false, false, false, 0)]
        public void RarityRollSumsIndependentRolls(bool oneIn100, bool oneIn1000, bool oneIn10000, int expected)
        {
            Assert.AreEqual(expected, ItemStatsCalculator.RollRarity(oneIn100, oneIn1000, oneIn10000));
        }

        [Test]
        public void ItemIdentityIncludesSeedAndModifier()
        {
            var item = new ItemInstance(ItemType.Chest, 0, 10, 2, seed: 777, modifier: ItemModifier.M01);
            var sameItem = new ItemInstance(ItemType.Chest, 0, 10, 2, seed: 777, modifier: ItemModifier.M01);
            var otherSeed = new ItemInstance(ItemType.Chest, 0, 10, 2, seed: 778, modifier: ItemModifier.M01);
            var otherModifier = new ItemInstance(ItemType.Chest, 0, 10, 2, seed: 777, modifier: ItemModifier.M1A);

            Assert.IsTrue(item.Equals(sameItem), "одинаковые поля → одинаковый предмет");
            Assert.IsFalse(item.Equals(otherSeed), "seed входит в идентичность предмета");
            Assert.IsFalse(item.Equals(otherModifier), "модификатор входит в идентичность предмета");
            Assert.IsFalse(item.Equals(null));
        }

        [Test]
        public void ItemValidationRejectsOutOfRangeData()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ItemInstance(ItemType.Chest, 0, 0, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ItemInstance(ItemType.Chest, 0, 1, 5, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ItemInstance(ItemType.Chest, 1, 1, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ItemInstance(ItemType.Weapon, 0, 1, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ItemInstance(ItemType.Weapon, 3, 1, 0, 0, stackCount: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ItemInstance((ItemType)77, 0, 1, 0, 0));
        }

        [Test]
        public void ItemExposesEffectiveLevel()
        {
            var item = new ItemInstance(ItemType.Weapon, (byte)WeaponSubtype.Sword, 10, 0, 1, powerBonus: 5);
            Assert.AreEqual(10.5, item.EffectiveLevel, 1e-9);
            Assert.AreEqual(3.81, ItemStatsCalculator.WeaponDamage(item.Level, item.Rarity, WeaponSubtype.Sword), Tolerance);
        }

        [Test]
        public void SpecialWeaponGroupMatchesAlphaList()
        {
            Assert.IsTrue(ItemStatsCalculator.IsSpecialWeapon(WeaponSubtype.Greatsword));
            Assert.IsTrue(ItemStatsCalculator.IsSpecialWeapon(WeaponSubtype.Staff));
            Assert.IsTrue(ItemStatsCalculator.IsSpecialWeapon(WeaponSubtype.Crossbow));
            Assert.IsTrue(ItemStatsCalculator.IsSpecialWeapon(WeaponSubtype.Reserved06));
            Assert.IsFalse(ItemStatsCalculator.IsSpecialWeapon(WeaponSubtype.Sword));
            Assert.IsFalse(ItemStatsCalculator.IsSpecialWeapon(WeaponSubtype.Shield));
        }
    }
}
