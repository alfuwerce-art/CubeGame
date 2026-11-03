using CubeGame.Core.Characters;
using CubeGame.Core.Combat;
using NUnit.Framework;

namespace CubeGame.Tests.EditMode
{
    /// <summary>
    /// EditMode-тесты формул боя S03.
    ///
    /// Эталон: docs/research/alpha2013/BEHAVIOR_SPECS/E_combat.md (§E2, §E5, §E6) и
    /// docs/research/alpha2013/tools/alpha_combat_reference.py --check (PASS, exit 0, 2026-11-03).
    /// Множители ID атак и модификаторы подтипа оружия помечены PARTIAL и проверяются как
    /// изолированные функции, а не как безусловная часть урона.
    /// </summary>
    public sealed class CombatFormulaTests
    {
        private const double Tolerance = 1e-9;

        [Test]
        public void WeaponDamageBaseMatchesAlphaReference()
        {
            Assert.That(CombatFormulas.WeaponDamage(1.0, 1), Is.EqualTo(1.0).Within(Tolerance),
                "base(0x180)=1 · levelFactor(1)=1");
            Assert.That(CombatFormulas.WeaponDamage(1.0, 11), Is.EqualTo(2.0).Within(Tolerance),
                "levelFactor(11) = 2.0");
            Assert.That(CombatFormulas.WeaponDamage(2.0, 5), Is.EqualTo(2.0 * 1.4142135623730951).Within(1e-6),
                "base=2 · levelFactor(5)=1.414214");
        }

        [Test]
        public void WeaponDamageUsesConfirmedUpgradeFlags()
        {
            Assert.That(CombatFormulas.WeaponDamage(1.0, 1, WeaponUpgradeFlags.Upgrade8), Is.EqualTo(3.0).Within(Tolerance));
            Assert.That(CombatFormulas.WeaponDamage(1.0, 1, WeaponUpgradeFlags.Upgrade16), Is.EqualTo(9.0).Within(Tolerance));
        }

        [TestCase(0x57, 50.0)]
        [TestCase(0x5B, 2.0)]
        [TestCase(0x5D, 5.0)]
        [TestCase(0x60, 3.0)]
        public void AttackIdMultipliersMatchResearchTable(int attackId, double expected)
        {
            Assert.IsTrue(CombatFormulas.TryGetAttackIdMultiplier(attackId, out double multiplier),
                $"ID 0x{attackId:X} должен быть в таблице");
            Assert.That(multiplier, Is.EqualTo(expected).Within(Tolerance));
        }

        [Test]
        public void UnknownAttackIdHasNoMultiplier()
        {
            Assert.IsFalse(CombatFormulas.TryGetAttackIdMultiplier(0x55, out double multiplier));
            Assert.That(multiplier, Is.EqualTo(1.0).Within(Tolerance));
            Assert.That(CombatFormulas.ApplyAttackIdMultiplier(7.0, 0x55), Is.EqualTo(7.0).Within(Tolerance),
                "неизвестный ID не должен менять урон");
        }

        [Test]
        public void AttackIdMultiplierIsIsolatedFromBaseWeaponDamage()
        {
            // PARTIAL: множитель не входит в WeaponDamage — только отдельной функцией.
            double baseDamage = CombatFormulas.WeaponDamage(1.0, 1);
            Assert.That(baseDamage, Is.EqualTo(1.0).Within(Tolerance));
            Assert.That(CombatFormulas.ApplyAttackIdMultiplier(baseDamage, 0x57), Is.EqualTo(50.0).Within(Tolerance));
        }

        [TestCase(0, 1.25)]
        [TestCase(32767, 0.75)]
        public void DamageVarianceMatchesReferenceBounds(int roll, double expected)
        {
            Assert.That(CombatFormulas.DamageVariance(roll), Is.EqualTo(expected).Within(Tolerance));
        }

        [Test]
        public void DamageVarianceMidpointIsAboutOne()
        {
            Assert.That(CombatFormulas.DamageVariance(16383), Is.EqualTo(1.0).Within(1e-3));
        }

        [Test]
        public void DamageVarianceIsMonotonicAndInsideRange()
        {
            double previous = double.MaxValue;
            for (int roll = 0; roll <= CombatFormulas.MaxRoll; roll += 97)
            {
                double variance = CombatFormulas.DamageVariance(roll);
                Assert.That(variance, Is.InRange(CombatFormulas.MinVariance, CombatFormulas.MaxVariance));
                Assert.LessOrEqual(variance, previous, $"разброс должен убывать с ростом roll ({roll})");
                previous = variance;
            }
        }

        [Test]
        public void DamageVarianceRejectsOutOfRangeRoll()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => CombatFormulas.DamageVariance(-1));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => CombatFormulas.DamageVariance(32768));
        }

        // --- Попадание (E6, REFERENCE_VERIFIED) --------------------------------

        [TestCase(0.5, 16383, true)]
        [TestCase(0.5, 16384, false)]
        [TestCase(0.0, 0, false)]
        [TestCase(1.0, 32766, true)]
        [TestCase(1.0, 32767, false)]
        public void HitCheckMatchesFormulaAtBoundaries(double chance, int roll, bool expected)
        {
            // hit = roll/32767 < chance; следствие формулы: при chance=1 бросок 32767 всё ещё промах.
            Assert.AreEqual(expected, HitResolver.IsHit(chance, roll), $"chance={chance} roll={roll}");
        }

        [Test]
        public void HitCheckResultCarriesDiagnostics()
        {
            HitCheckResult hit = HitResolver.Check(0.5, 100);
            Assert.IsTrue(hit.IsHit);
            Assert.AreEqual(0.5, hit.Chance, 1e-12);
            Assert.AreEqual(100, hit.Roll);

            HitCheckResult miss = HitResolver.Check(0.5, 30000);
            Assert.IsFalse(miss.IsHit);
            Assert.AreEqual(HitOutcome.Miss, miss.Outcome);
        }

        [Test]
        public void HitChanceOutsideRangeIsRejected()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => HitResolver.IsHit(-0.1, 0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => HitResolver.IsHit(1.1, 0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => HitResolver.IsHit(double.NaN, 0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => HitResolver.IsHit(double.PositiveInfinity, 0));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => HitResolver.IsHit(0.5, -1));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => HitResolver.IsHit(0.5, 32768));
        }

        [TestCase(3, 8, 0.15)]
        [TestCase(3, 10, 0.15)]
        [TestCase(3, 0x0C, 0.25)]
        [TestCase(3, 1, 0.25)]
        [TestCase(3, 2, 0.25)]
        [TestCase(3, 3, 0.5)]
        [TestCase(4, 8, 0.5)]
        public void WeaponSubtypeHitModifierMatchesSpecification(int equippedType, int subtype, double expected)
        {
            // PARTIAL: модификатор применяется только для оружия в слоте основной руки (тип 3).
            Assert.That(CombatFormulas.ApplyWeaponSubtypeHitModifier(0.5, equippedType, subtype),
                Is.EqualTo(expected).Within(Tolerance));
        }

        // --- Блок (E6) --------------------------------------------------------

        [TestCase(1, 1)]
        [TestCase(2, 5)]
        [TestCase(5, 17)]
        [TestCase(10, 32)]
        [TestCase(20, 49)]
        [TestCase(50, 72)]
        public void BlockThresholdMatchesAlphaReference(int level, int expected)
        {
            Assert.AreEqual(expected, CombatFormulas.BlockThreshold(level), $"порог блока L={level}");
        }

        [Test]
        public void BlockRollUsesThresholdInclusive()
        {
            Assert.IsTrue(CombatFormulas.RollBlock(1, 1), "roll = порог → блок успешен");
            Assert.IsFalse(CombatFormulas.RollBlock(1, 2), "roll > порог → блок не удался");
            Assert.IsTrue(CombatFormulas.RollBlock(10, 32));
            Assert.IsFalse(CombatFormulas.RollBlock(10, 33));
        }

        [Test]
        public void BlockRollRejectsOutOfRangeValues()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => CombatFormulas.RollBlock(1, -1));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => CombatFormulas.RollBlock(1, 101));
        }

        // --- Снижение урона (E3, PARTIAL) -------------------------------------

        [Test]
        public void MitigationMatchesResearchControlCase()
        {
            // Контрольный случай исследования: rawDamage = 30, armor = 10, blockFraction = 0.5 → 25.
            MitigationResult result = DamageMitigation.Apply(30.0, 10.0, 0.5);

            Assert.That(result.FinalDamage, Is.EqualTo(25.0).Within(Tolerance));
            Assert.That(result.AbsorbedDamage, Is.EqualTo(5.0).Within(Tolerance));
            Assert.That(result.RawDamage, Is.EqualTo(30.0).Within(Tolerance));
            Assert.That(result.Armor, Is.EqualTo(10.0).Within(Tolerance));
            Assert.That(result.BlockFraction, Is.EqualTo(0.5).Within(Tolerance));

            // Это контроль частичной формулы, а не доказательство полного алгоритма защиты Alpha.
        }

        [Test]
        public void MitigationNeverProducesNegativeDamage()
        {
            MitigationResult result = DamageMitigation.Apply(1.0, 100.0, 1.0);
            Assert.That(result.FinalDamage, Is.EqualTo(0.0).Within(Tolerance), "урон не уходит в минус");
            Assert.That(result.AbsorbedDamage, Is.EqualTo(100.0).Within(Tolerance));
        }

        [Test]
        public void MitigationWithZeroBlockFractionKeepsDamage()
        {
            MitigationResult result = DamageMitigation.Apply(12.5, 50.0, 0.0);
            Assert.That(result.FinalDamage, Is.EqualTo(12.5).Within(Tolerance));
            Assert.That(result.AbsorbedDamage, Is.EqualTo(0.0).Within(Tolerance));
        }

        [Test]
        public void MitigationRejectsInvalidInput()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => DamageMitigation.Apply(-1.0, 10.0, 0.5));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => DamageMitigation.Apply(double.NaN, 10.0, 0.5));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => DamageMitigation.Apply(1.0, -10.0, 0.5));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => DamageMitigation.Apply(1.0, double.PositiveInfinity, 0.5));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => DamageMitigation.Apply(1.0, 10.0, -0.1));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => DamageMitigation.Apply(1.0, 10.0, 1.5));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => DamageMitigation.Apply(1.0, 10.0, double.NaN));
        }
    }
}
