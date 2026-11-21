using System;
using CubeGame.Core.Characters;
using NUnit.Framework;

namespace CubeGame.Tests.EditMode
{
    /// <summary>
    /// Табличные тесты расчёта характеристик персонажа S02.
    ///
    /// Эталон max HP — проверенный сейв Alpha 2013: Warrior 1 уровня без урона имеет 268.0 HP
    /// (100 база · 2.0 звёздный множитель игрока · 1.0 уровень · 1.3 Warrior + 2·4 экипировка).
    /// Источник: docs/research/alpha2013/BEHAVIOR_SPECS/D_character.md §D4.2.
    /// </summary>
    public sealed class CharacterStatsTests
    {
        private const double Tolerance = 1e-6;

        [Test]
        public void WarriorLevelOneWithEquipmentMatchesVerifiedAlphaSave()
        {
            double health = CharacterStats.CalcMaxHealth(
                CharacterStats.DefaultBaseHealth,
                1,
                CharacterClass.Warrior,
                CharacterSpecialization.Primary,
                equipmentHealthBonus: 8.0);

            Assert.That(health, Is.EqualTo(268.0).Within(Tolerance),
                "проверенный сейв Alpha: max HP = 268.0");
        }

        [TestCase(CharacterClass.Warrior, 1.3)]
        [TestCase(CharacterClass.Ranger, 1.1)]
        [TestCase(CharacterClass.Mage, 1.0)]
        [TestCase(CharacterClass.Rogue, 1.2)]
        public void ClassHealthMultipliersMatchAlpha(CharacterClass characterClass, double expectedMultiplier)
        {
            Assert.That(CharacterStats.ClassHealthMultiplier(characterClass), Is.EqualTo(expectedMultiplier).Within(Tolerance));
        }

        [TestCase(CharacterClass.Warrior, 260.0)]
        [TestCase(CharacterClass.Ranger, 220.0)]
        [TestCase(CharacterClass.Mage, 200.0)]
        [TestCase(CharacterClass.Rogue, 240.0)]
        public void PlayerHealthUsesClassMultiplierAtLevelOne(CharacterClass characterClass, double expectedHealth)
        {
            double health = CharacterStats.CalcMaxHealth(
                CharacterStats.DefaultBaseHealth, 1, characterClass, CharacterSpecialization.Primary);

            Assert.That(health, Is.EqualTo(expectedHealth).Within(1e-4));
        }

        [Test]
        public void SecondWarriorSpecializationAddsGuardianMultiplier()
        {
            double primary = CharacterStats.CalcMaxHealth(100.0, 1, CharacterClass.Warrior, CharacterSpecialization.Primary);
            double secondary = CharacterStats.CalcMaxHealth(100.0, 1, CharacterClass.Warrior, CharacterSpecialization.Secondary);

            Assert.That(secondary, Is.EqualTo(325.0).Within(1e-4), "100 · 2.0 · 1.3 · 1.25 = 325");
            Assert.That(secondary / primary, Is.EqualTo(1.25).Within(1e-9));
        }

        [Test]
        public void SpecializationMultiplierAppliesOnlyToWarrior()
        {
            foreach (CharacterClass other in new[] { CharacterClass.Ranger, CharacterClass.Mage, CharacterClass.Rogue })
            {
                Assert.AreEqual(1.0, CharacterStats.SpecializationHealthMultiplier(other, CharacterSpecialization.Secondary),
                    $"{other}: множитель 1.25 относится только к Warrior+Secondary");
            }
        }

        [Test]
        public void LevelElevenDoublesHealthThroughLevelFactor()
        {
            double health = CharacterStats.CalcMaxHealth(100.0, 11, CharacterClass.Warrior, CharacterSpecialization.Primary);
            Assert.That(health, Is.EqualTo(520.0).Within(1e-4), "levelFactor(11) = 2.0 → 100·2·2·1.3");
        }

        [Test]
        public void CreatureHealthUsesStarFactorInsteadOfPlayerFactor()
        {
            Assert.That(CharacterStats.CalcCreatureMaxHealth(100.0, 1, 0), Is.EqualTo(100.0).Within(1e-6));
            Assert.That(CharacterStats.CalcCreatureMaxHealth(100.0, 1, 4), Is.EqualTo(200.0).Within(1e-6));
            Assert.That(CharacterStats.CalcCreatureMaxHealth(100.0, 1, 8), Is.EqualTo(400.0).Within(1e-6));
        }

        [Test]
        public void WeaponDamageBaseIsLevelFactorOnly()
        {
            Assert.That(
                CharacterStats.CalcWeaponDamage(1.0, 1, WeaponUpgradeFlags.None),
                Is.EqualTo(1.0).Within(1e-9),
                "подтверждённая часть: base(0x180) · levelFactor, базовое значение 1.0");

            Assert.That(
                CharacterStats.CalcWeaponDamage(1.0, 11, WeaponUpgradeFlags.None),
                Is.EqualTo(2.0).Within(1e-9),
                "levelFactor(11) = 2.0");
        }

        [TestCase(WeaponUpgradeFlags.None, 1.0)]
        [TestCase(WeaponUpgradeFlags.Upgrade8, 3.0)]
        [TestCase(WeaponUpgradeFlags.Upgrade16, 9.0)]
        [TestCase(WeaponUpgradeFlags.Upgrade8 | WeaponUpgradeFlags.Upgrade16, 11.0)]
        public void WeaponUpgradeFlagsAddLevelAndStarScaledDamage(WeaponUpgradeFlags flags, double expected)
        {
            double damage = CharacterStats.CalcWeaponDamage(1.0, 1, flags, stars: 0);
            Assert.That(damage, Is.EqualTo(expected).Within(1e-9));
        }

        [Test]
        public void WeaponUpgradeFlagsUseStarFactor()
        {
            // levelFactor(11) = 2.0, starFactor(4) = 2.0, флаг 0x8 → 2.0 + 2.0·2.0·2.0 = 10.0
            double damage = CharacterStats.CalcWeaponDamage(1.0, 11, WeaponUpgradeFlags.Upgrade8, stars: 4);
            Assert.That(damage, Is.EqualTo(10.0).Within(1e-9));
        }

        [Test]
        public void AttackTypeMultipliersAreNotApplied()
        {
            // Множители типа атаки (0x57 ×50, 0x5b ×2, 0x5d ×5, 0x60 ×3) не восстановлены (UNKNOWN),
            // поэтому урон не масштабируется ими: 1.0 остаётся 1.0.
            Assert.That(CharacterStats.CalcWeaponDamage(1.0, 1, WeaponUpgradeFlags.None, stars: 0),
                Is.EqualTo(1.0).Within(1e-12));
        }

        [Test]
        public void SheetCombinesProgressionAndEquipment()
        {
            var sheet = new CharacterSheet();
            sheet.EquipmentHealthBonus = 8.0;

            Assert.AreEqual(268.0, sheet.MaxHealth, 1e-6);
            Assert.AreEqual(1, sheet.Progression.Level);

            sheet.AddExperience(467);

            Assert.AreEqual(5, sheet.Progression.Level);
            // 100 · levelFactor(5)=1.414214 · 2.0 · 1.3 + 8 = 375.70
            Assert.That(sheet.MaxHealth, Is.EqualTo(100.0 * 1.4142135623730951 * 2.0 * 1.3 + 8.0).Within(1e-6));
        }

        [Test]
        public void SheetRejectsInvalidState()
        {
            var sheet = new CharacterSheet();
            Assert.Throws<ArgumentOutOfRangeException>(() => sheet.EquipmentHealthBonus = -1.0);
            Assert.Throws<ArgumentOutOfRangeException>(() => new CharacterSheet(baseHealth: 0.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CharacterSheet(weaponBase: 0.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => CharacterStats.CalcMaxHealth(-1.0, 1, CharacterClass.Mage, CharacterSpecialization.Primary));
            Assert.Throws<ArgumentOutOfRangeException>(() => CharacterStats.CalcWeaponDamage(0.0, 1, WeaponUpgradeFlags.None));
        }
    }
}
