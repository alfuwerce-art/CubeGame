using CubeGame.Core.Characters;
using CubeGame.Core.Combat;
using CubeGame.Core.Equipment;
using CubeGame.Core.Items;
using NUnit.Framework;

namespace CubeGame.Tests.EditMode
{
    /// <summary>
    /// EditMode-тесты интеграции «экипировка → боевой расчёт» S04 (модули C–D).
    ///
    /// Проверяемые правила:
    /// <list type="bullet">
    /// <item>вклад оружия аддитивен и <b>не</b> масштабируется уровнем персонажа повторно (нет двойного levelFactor);</item>
    /// <item>смена оружия меняет результат при одинаковом seed и одинаковой защите цели;</item>
    /// <item>броня цели влияет на защиту только через документированный адаптер (Σ BlockValue, E3 `PARTIAL`);</item>
    /// <item>частичные механики (модификатор подтипа, ID атак) выключены по умолчанию;</item>
    /// <item>OQ-25 не подменяется догадкой: HP не меняется от экипировки.</item>
    /// </list>
    /// </summary>
    public sealed class EquipmentCombatIntegrationTests
    {
        private const double Tolerance = 1e-9;

        [Test]
        public void AdapterBuildsAttackFromSheetAndWeapon()
        {
            CharacterSheet sheet = CreateSheetAtLevel(1);
            var loadout = new EquipmentLoadout();
            loadout.Equip(EquipmentSlot.MainHand, CreateSword(level: 1, rarity: 0));

            CombatAttack attack = new EquipmentCombatAdapter().BuildAttack(sheet, loadout, 0.75, 0.0, 0.0);

            Assert.That(attack.WeaponBase, Is.EqualTo(1.0).Within(Tolerance), "база 0x180 остаётся базой персонажа");
            Assert.That(attack.ItemWeaponDamageBonus, Is.EqualTo(2.0).Within(Tolerance), "sword L1 r0 → 2.00 аддитивно");
            Assert.AreEqual(1, attack.Level);
            Assert.AreEqual(0.75, attack.HitChance, Tolerance);
            Assert.IsFalse(attack.UseAttackIdMultiplier, "множители ID атак не включаются автоматически");
            Assert.AreEqual(0, attack.EquippedItemType, "частичный модификатор подтипа выключен по умолчанию");
        }

        [Test]
        public void WeaponContributionIsNotMultipliedByCharacterLevelTwice()
        {
            CharacterSheet levelEleven = CreateSheetAtLevel(11);
            var loadout = new EquipmentLoadout();
            loadout.Equip(EquipmentSlot.MainHand, CreateSword(level: 1, rarity: 0));

            CombatAttack attack = new EquipmentCombatAdapter().BuildAttack(levelEleven, loadout, 1.0, 0.0, 0.0);
            CombatOutcome outcome = new DamageResolver(new FixedCombatRandom(0, 0))
                .Resolve(attack, new HealthPool(1000.0));

            // base 1.0 · levelFactor(11)=2 → 2.0; предмет +2.0 аддитивно; raw = 4.0 · 1.25 = 5.0
            Assert.That(outcome.BaseDamage, Is.EqualTo(4.0).Within(Tolerance));
            Assert.That(outcome.RawDamage, Is.EqualTo(5.0).Within(Tolerance));

            // Если бы урон предмета передавался как WeaponBase, получилось бы (1+2)·2 = 6 → raw 7.5.
            Assert.AreNotEqual(7.5, outcome.RawDamage, "двойное масштабирование уровня запрещено");
        }

        [Test]
        public void WeaponSwapChangesDamageWithSameSeed()
        {
            CharacterSheet sheet = CreateSheetAtLevel(1);
            var loadout = new EquipmentLoadout();
            int seed = 424242;

            loadout.Equip(EquipmentSlot.MainHand, CreateSword(level: 1, rarity: 0));
            CombatOutcome sword = Strike(sheet, loadout, seed);

            loadout.Equip(EquipmentSlot.MainHand, CreateGreatmace(level: 1, rarity: 0));
            CombatOutcome greatmace = Strike(sheet, loadout, seed);

            Assert.AreEqual(sword.HitRoll, greatmace.HitRoll, "seed и параметры одинаковые → одинаковые броски");
            Assert.AreEqual(sword.VarianceRoll, greatmace.VarianceRoll);
            Assert.That(sword.BaseDamage, Is.EqualTo(3.0).Within(1e-6), "base 1.0 · lf(1) + предмет 2.0");
            Assert.That(greatmace.BaseDamage, Is.EqualTo(9.0).Within(1e-6), "base 1.0 · lf(1) + предмет 8.0");
            Assert.That(sword.RawDamage, Is.EqualTo(sword.BaseDamage * sword.VarianceMultiplier).Within(1e-9));
            Assert.That(greatmace.RawDamage, Is.EqualTo(greatmace.BaseDamage * greatmace.VarianceMultiplier).Within(1e-9));
            Assert.Greater(greatmace.RawDamage, sword.RawDamage);
        }

        [Test]
        public void UnequippingWeaponReturnsDocumentedBaseline()
        {
            CharacterSheet sheet = CreateSheetAtLevel(1);
            var loadout = new EquipmentLoadout();
            loadout.Equip(EquipmentSlot.MainHand, CreateGreatmace(level: 1, rarity: 0));
            Strike(sheet, loadout, 424242);

            loadout.Unequip(EquipmentSlot.MainHand);
            EquipmentStatSummary summary = EquipmentStats.Compute(loadout);
            CombatAttack attack = new EquipmentCombatAdapter().BuildAttack(sheet, loadout, 1.0, 0.0, 0.0);

            Assert.That(summary.WeaponDamageBonus, Is.EqualTo(0.0).Within(Tolerance), "снятое оружие не даёт бонуса");
            Assert.That(attack.ItemWeaponDamageBonus, Is.EqualTo(0.0).Within(Tolerance));

            CombatOutcome baseline = Strike(sheet, loadout, 424242);
            Assert.That(baseline.BaseDamage, Is.EqualTo(1.0).Within(1e-9),
                "без оружия остаётся только база персонажа base(0x180)=1.0");
            Assert.That(baseline.RawDamage, Is.EqualTo(baseline.BaseDamage * baseline.VarianceMultiplier).Within(1e-9));
        }

        [Test]
        public void TwoEquippedWeaponsAddTheirDamage()
        {
            CharacterSheet sheet = CreateSheetAtLevel(1);
            var loadout = new EquipmentLoadout();
            loadout.Equip(EquipmentSlot.MainHand, CreateSword(level: 1, rarity: 0));
            loadout.Equip(EquipmentSlot.OffHand, CreateGreatmace(level: 1, rarity: 0));

            EquipmentStatSummary summary = EquipmentStats.Compute(loadout);
            CombatOutcome outcome = Strike(sheet, loadout, 0);

            // PARTIAL: оба оружия входят в аддитивный список item_weaponDamage (E2).
            Assert.That(summary.WeaponDamageBonus, Is.EqualTo(10.0).Within(Tolerance));
            Assert.That(outcome.BaseDamage, Is.EqualTo(11.0).Within(1e-6), "base 1.0 + 2.0 + 8.0");
            Assert.That(outcome.RawDamage, Is.EqualTo(outcome.BaseDamage * outcome.VarianceMultiplier).Within(1e-9));
        }

        [Test]
        public void TargetArmorFromEquipmentChangesAppliedDamage()
        {
            CharacterSheet sheet = CreateSheetAtLevel(1);
            var attackerLoadout = new EquipmentLoadout();
            attackerLoadout.Equip(EquipmentSlot.MainHand, CreateSword(level: 1, rarity: 0));

            var bareTarget = new EquipmentLoadout();
            var armoredTarget = new EquipmentLoadout();
            armoredTarget.Equip(EquipmentSlot.Chest, new ItemInstance(ItemType.Chest, 0, 10, 2, seed: 0));
            armoredTarget.Equip(EquipmentSlot.Boots, new ItemInstance(ItemType.Boots, 0, 10, 2, seed: 0));

            double armorBare = EquipmentCombatAdapter.ComputeTargetArmor(bareTarget);
            double armorArmored = EquipmentCombatAdapter.ComputeTargetArmor(armoredTarget);

            Assert.That(armorBare, Is.EqualTo(0.0).Within(Tolerance));
            Assert.That(armorArmored,
                Is.EqualTo(ItemStatsCalculator.BlockValue(10, 2, ItemType.Chest) + ItemStatsCalculator.BlockValue(10, 2, ItemType.Boots))
                    .Within(1e-9),
                "защита цели = Σ BlockValue (E3, PARTIAL)");

            CombatOutcome bare = Strike(sheet, attackerLoadout, 424242, armorBare, blockFraction: 0.5);
            CombatOutcome armored = Strike(sheet, attackerLoadout, 424242, armorArmored, blockFraction: 0.5);

            Assert.AreEqual(bare.HitRoll, armored.HitRoll);
            Assert.That(bare.RawDamage, Is.EqualTo(armored.RawDamage).Within(Tolerance), "урон до защиты одинаков");
            Assert.That(armored.AbsorbedDamage, Is.GreaterThan(bare.AbsorbedDamage), "броня поглощает больше");
            Assert.That(armored.AppliedDamage, Is.LessThan(bare.AppliedDamage), "итоговый урон меньше");
        }

        [Test]
        public void ArmorStatIsNotUsedAsTargetArmor()
        {
            // ItemArmorStat (item_computeStat_armor) и BlockValue — разные величины; адаптер использует BlockValue.
            var loadout = new EquipmentLoadout();
            loadout.Equip(EquipmentSlot.Chest, new ItemInstance(ItemType.Chest, 0, 10, 2, seed: 0));

            EquipmentStatSummary summary = EquipmentStats.Compute(loadout);

            Assert.That(summary.TotalArmor, Is.GreaterThan(summary.TotalBlockValue),
                "броня предмета и блок — не одно и то же число");
            Assert.That(EquipmentCombatAdapter.ComputeTargetArmor(loadout), Is.EqualTo(summary.TotalBlockValue).Within(1e-12));
            Assert.AreNotEqual(summary.TotalArmor, EquipmentCombatAdapter.ComputeTargetArmor(loadout));
        }

        [Test]
        public void PartialHitModifierIsOffByDefaultAndExplicitWhenEnabled()
        {
            CharacterSheet sheet = CreateSheetAtLevel(1);
            var loadout = new EquipmentLoadout();
            loadout.Equip(EquipmentSlot.MainHand, new ItemInstance(ItemType.Weapon, (byte)WeaponSubtype.Boomerang, 1, 0, seed: 1));

            CombatAttack off = new EquipmentCombatAdapter().BuildAttack(sheet, loadout, 0.5, 0.0, 0.0);
            Assert.AreEqual(0, off.EquippedItemType, "по умолчанию подтип не передаётся");
            Assert.AreEqual(0, off.WeaponSubtype);

            CombatAttack on = new EquipmentCombatAdapter(enableWeaponSubtypeHitModifier: true)
                .BuildAttack(sheet, loadout, 0.5, 0.0, 0.0);
            Assert.AreEqual((int)ItemType.Weapon, on.EquippedItemType);
            Assert.AreEqual((int)WeaponSubtype.Boomerang, on.WeaponSubtype);

            // Включённый модификатор даёт ×0.3 для подтипа 8 → эффективный шанс 0.15.
            CombatOutcome miss = new DamageResolver(new FixedCombatRandom(30000)).Resolve(on, new HealthPool(100.0));
            Assert.That(miss.EffectiveHitChance, Is.EqualTo(0.15).Within(1e-9));
            Assert.AreEqual(CombatStatus.Miss, miss.Status);
        }

        [Test]
        public void EquipmentStrikeIsRepeatableForSameSeed()
        {
            CharacterSheet sheet = CreateSheetAtLevel(1);
            var loadout = new EquipmentLoadout();
            loadout.Equip(EquipmentSlot.MainHand, CreateSword(level: 10, rarity: 2));
            loadout.Equip(EquipmentSlot.Chest, new ItemInstance(ItemType.Chest, 0, 10, 2, seed: 0));

            CombatOutcome first = Strike(sheet, loadout, 777, targetArmor: 2.5, blockFraction: 0.25);
            CombatOutcome second = Strike(sheet, loadout, 777, targetArmor: 2.5, blockFraction: 0.25);

            Assert.AreEqual(first.ToLogLine(), second.ToLogLine(), "одинаковый seed → идентичный результат и лог");
        }

        [Test]
        public void MaxHealthIgnoresEquipmentButKeepsExplicitBonus()
        {
            CharacterSheet sheet = CreateSheetAtLevel(1);
            var loadout = new EquipmentLoadout();
            loadout.Equip(EquipmentSlot.Chest, new ItemInstance(ItemType.Chest, 0, 10, 4, seed: 0));

            Assert.That(EquipmentCombatAdapter.ComputeMaxHealth(sheet), Is.EqualTo(260.0).Within(1e-6),
                "OQ-25 OPEN: броня не превращается в HP");

            sheet.EquipmentHealthBonus = 8.0;
            Assert.That(EquipmentCombatAdapter.ComputeMaxHealth(sheet), Is.EqualTo(268.0).Within(1e-6),
                "явный бонус остаётся единственным источником +8");
        }

        private static CombatOutcome Strike(
            CharacterSheet sheet,
            EquipmentLoadout loadout,
            int seed,
            double targetArmor = 0.0,
            double blockFraction = 0.0)
        {
            CombatAttack attack = new EquipmentCombatAdapter().BuildAttack(sheet, loadout, 1.0, targetArmor, blockFraction);
            return new DamageResolver(new SeededCombatRandom(seed)).Resolve(attack, new HealthPool(1000.0));
        }

        private static CharacterSheet CreateSheetAtLevel(int level)
        {
            var sheet = new CharacterSheet();
            if (level > 1)
            {
                // Точные пороги из alpha_curves_reference: 1→11 = 2216 XP.
                int cumulative = level switch
                {
                    2 => 50,
                    5 => 467,
                    10 => 1856,
                    11 => 2216,
                    _ => throw new System.ArgumentOutOfRangeException(nameof(level), level, "Уровень не поддержан в тесте."),
                };
                sheet.AddExperience(cumulative);
                Assert.AreEqual(level, sheet.Progression.Level, "тест должен стартовать с ожидаемого уровня");
            }

            return sheet;
        }

        private static ItemInstance CreateSword(int level, int rarity, int seed = 1)
        {
            return new ItemInstance(ItemType.Weapon, (byte)WeaponSubtype.Dagger, level, rarity, seed);
        }

        private static ItemInstance CreateGreatmace(int level, int rarity, int seed = 2)
        {
            return new ItemInstance(ItemType.Weapon, (byte)WeaponSubtype.Greatmace, level, rarity, seed);
        }
    }
}
