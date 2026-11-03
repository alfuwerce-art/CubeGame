using System;
using System.Collections.Generic;
using CubeGame.Core.Combat;
using NUnit.Framework;

namespace CubeGame.Tests.EditMode
{
    /// <summary>
    /// EditMode-тесты резолвера удара S03: пул здоровья, попадание/промах, митигация,
    /// детерминизм RNG, атомарность при ошибке и статистика на детерминированном источнике.
    ///
    /// Все броски задаются явно (<see cref="FixedCombatRandom"/>) либо детерминированным
    /// <see cref="SeededCombatRandom"/>, поэтому тесты воспроизводимы.
    /// </summary>
    public sealed class CombatResolutionTests
    {
        private const double Tolerance = 1e-9;

        // --- Пул здоровья (модуль D) -----------------------------------------

        [Test]
        public void HealthPoolStartsAtMaximum()
        {
            var pool = new HealthPool(100.0);
            Assert.AreEqual(100.0, pool.CurrentHealth, Tolerance);
            Assert.AreEqual(100.0, pool.MaxHealth, Tolerance);
            Assert.IsFalse(pool.IsDead);
        }

        [Test]
        public void DamageReducesHealthAndNeverGoesBelowZero()
        {
            var pool = new HealthPool(100.0);

            Assert.AreEqual(30.0, pool.ApplyDamage(30.0), Tolerance);
            Assert.AreEqual(70.0, pool.CurrentHealth, Tolerance);

            Assert.AreEqual(70.0, pool.ApplyDamage(1000.0), Tolerance, "фактически снято не больше текущего HP");
            Assert.AreEqual(0.0, pool.CurrentHealth, Tolerance);
            Assert.IsTrue(pool.IsDead);

            Assert.AreEqual(0.0, pool.ApplyDamage(10.0), Tolerance, "мёртвая цель не получает урон повторно");
            Assert.AreEqual(0.0, pool.CurrentHealth, Tolerance);
        }

        [Test]
        public void HealStopsAtMaximumAndDoesNotRevive()
        {
            var pool = new HealthPool(100.0);
            pool.ApplyDamage(60.0);

            Assert.AreEqual(60.0, pool.Heal(500.0), Tolerance, "лечение клампится по максимуму");
            Assert.AreEqual(100.0, pool.CurrentHealth, Tolerance);
            Assert.AreEqual(0.0, pool.Heal(10.0), Tolerance, "на полном HP лечить нечего");

            pool.ApplyDamage(200.0);
            Assert.AreEqual(0.0, pool.Heal(50.0), Tolerance, "мёртвого нельзя вылечить в этой партии");
            Assert.IsTrue(pool.IsDead);
        }

        [Test]
        public void HealthPoolRejectsInvalidNumbers()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new HealthPool(0.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HealthPool(-5.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HealthPool(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new HealthPool(double.PositiveInfinity));

            var pool = new HealthPool(100.0);
            Assert.Throws<ArgumentOutOfRangeException>(() => pool.ApplyDamage(-1.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => pool.ApplyDamage(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => pool.ApplyDamage(double.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => pool.Heal(-1.0));
            Assert.Throws<ArgumentOutOfRangeException>(() => pool.Heal(double.NaN));
            Assert.AreEqual(100.0, pool.CurrentHealth, Tolerance, "некорректные вызовы не меняют HP");
        }

        // --- Резолвер (модуль E) ---------------------------------------------

        [Test]
        public void DeterministicHitAppliesExpectedDamage()
        {
            // roll 0 → попадание при chance=1; roll 0 → разброс 1.25; урон 1 · 1.25 = 1.25.
            var target = new HealthPool(100.0);
            var resolver = new DamageResolver(new FixedCombatRandom(0, 0));
            CombatAttack attack = CreateAttack(hitChance: 1.0);

            CombatOutcome outcome = resolver.Resolve(attack, target);

            Assert.AreEqual(CombatStatus.Hit, outcome.Status);
            Assert.AreEqual(1.0, outcome.BaseDamage, Tolerance);
            Assert.AreEqual(1.25, outcome.VarianceMultiplier, Tolerance);
            Assert.AreEqual(1.25, outcome.RawDamage, Tolerance);
            Assert.AreEqual(0.0, outcome.AbsorbedDamage, Tolerance);
            Assert.AreEqual(1.25, outcome.AppliedDamage, Tolerance);
            Assert.AreEqual(100.0, outcome.HealthBefore, Tolerance);
            Assert.AreEqual(98.75, outcome.HealthAfter, Tolerance);
            Assert.AreEqual(98.75, target.CurrentHealth, Tolerance);
            Assert.IsFalse(outcome.KilledTarget);
        }

        [Test]
        public void MissDoesNotChangeHealthAndConsumesOneRoll()
        {
            // chance=0.5, roll=30000 → 30000/32767 = 0.9156 > 0.5 → промах.
            var random = new FixedCombatRandom(30000);
            var target = new HealthPool(100.0);
            var resolver = new DamageResolver(random);

            CombatOutcome outcome = resolver.Resolve(CreateAttack(hitChance: 0.5), target);

            Assert.AreEqual(CombatStatus.Miss, outcome.Status);
            Assert.AreEqual(0.0, outcome.AppliedDamage, Tolerance);
            Assert.AreEqual(100.0, target.CurrentHealth, Tolerance);
            Assert.AreEqual(-1, outcome.VarianceRoll, "на промахе бросок разброса не тратится");
            Assert.AreEqual(0, random.RemainingRolls);
        }

        [Test]
        public void HitConsumesTwoRolls()
        {
            var random = new FixedCombatRandom(0, 32767);
            var target = new HealthPool(100.0);
            var resolver = new DamageResolver(random);

            CombatOutcome outcome = resolver.Resolve(CreateAttack(hitChance: 1.0), target);

            Assert.AreEqual(0.75, outcome.VarianceMultiplier, Tolerance, "roll 32767 → минимальный разброс");
            Assert.AreEqual(0.75, outcome.AppliedDamage, Tolerance);
            Assert.AreEqual(0, random.RemainingRolls);
        }

        [Test]
        public void MitigationIsAppliedBeforeHealthChange()
        {
            // base 1, variance 1.25 → raw 1.25; armor 10, blockFraction 0.5 → поглощено 5 → урон 0.
            var target = new HealthPool(100.0);
            var resolver = new DamageResolver(new FixedCombatRandom(0, 0));
            CombatAttack attack = CreateAttack(hitChance: 1.0);
            attack.TargetArmor = 10.0;
            attack.BlockFraction = 0.5;

            CombatOutcome outcome = resolver.Resolve(attack, target);

            Assert.AreEqual(1.25, outcome.RawDamage, Tolerance);
            Assert.AreEqual(5.0, outcome.AbsorbedDamage, Tolerance);
            Assert.AreEqual(0.0, outcome.AppliedDamage, Tolerance, "урон не уходит в минус");
            Assert.AreEqual(100.0, target.CurrentHealth, Tolerance);
        }

        [Test]
        public void ControlCaseFromResearchReachesHealthPool()
        {
            // Приводим raw=30 через base=24 и разброс 1.25; armor=10, blockFraction=0.5 → 25.
            var target = new HealthPool(100.0);
            var resolver = new DamageResolver(new FixedCombatRandom(0, 0));
            CombatAttack attack = CreateAttack(weaponBase: 24.0, hitChance: 1.0);
            attack.TargetArmor = 10.0;
            attack.BlockFraction = 0.5;

            CombatOutcome outcome = resolver.Resolve(attack, target);

            Assert.That(outcome.RawDamage, Is.EqualTo(30.0).Within(Tolerance));
            Assert.That(outcome.AppliedDamage, Is.EqualTo(25.0).Within(Tolerance));
            Assert.That(target.CurrentHealth, Is.EqualTo(75.0).Within(Tolerance));
        }

        [Test]
        public void AttackIdMultiplierIsAppliedOnlyWhenExplicitlyEnabled()
        {
            var withoutMultiplier = new HealthPool(1000.0);
            var withMultiplier = new HealthPool(1000.0);

            CombatAttack plain = CreateAttack(hitChance: 1.0);
            CombatAttack flagged = CreateAttack(hitChance: 1.0);
            flagged.UseAttackIdMultiplier = true;
            flagged.AttackId = 0x5B; // ×2

            CombatOutcome plainOutcome = new DamageResolver(new FixedCombatRandom(0, 0)).Resolve(plain, withoutMultiplier);
            CombatOutcome flaggedOutcome = new DamageResolver(new FixedCombatRandom(0, 0)).Resolve(flagged, withMultiplier);

            Assert.That(plainOutcome.RawDamage, Is.EqualTo(1.25).Within(Tolerance));
            Assert.That(flaggedOutcome.RawDamage, Is.EqualTo(2.5).Within(Tolerance));
            Assert.IsTrue(flaggedOutcome.AttackIdMultiplierApplied);
            Assert.That(flaggedOutcome.AttackIdMultiplier, Is.EqualTo(2.0).Within(Tolerance));
            Assert.IsFalse(plainOutcome.AttackIdMultiplierApplied);
        }

        [Test]
        public void KillIsRecordedAndFurtherHitsAreIgnored()
        {
            var target = new HealthPool(1.0);
            var random = new FixedCombatRandom(0, 0);
            var resolver = new DamageResolver(random);

            CombatOutcome killing = resolver.Resolve(CreateAttack(hitChance: 1.0), target);
            Assert.IsTrue(killing.KilledTarget, "удар снимает последнее HP");
            Assert.IsTrue(killing.IsDead);
            Assert.AreEqual(0.0, target.CurrentHealth, Tolerance);

            CombatOutcome afterDeath = resolver.Resolve(CreateAttack(hitChance: 1.0), target);

            Assert.AreEqual(CombatStatus.TargetAlreadyDead, afterDeath.Status);
            Assert.AreEqual(0.0, afterDeath.AppliedDamage, Tolerance);
            Assert.AreEqual(0.0, target.CurrentHealth, Tolerance);
            Assert.AreEqual(0, random.RemainingRolls, "по мёртвой цели броски не тратятся");
        }

        [Test]
        public void FailureDuringCalculationLeavesHealthUntouched()
        {
            // Один бросок есть (попадание), второго нет → исключение до изменения HP.
            var target = new HealthPool(100.0);
            var resolver = new DamageResolver(new FixedCombatRandom(0));

            Assert.Throws<InvalidOperationException>(() => resolver.Resolve(CreateAttack(hitChance: 1.0), target));

            Assert.AreEqual(100.0, target.CurrentHealth, Tolerance, "HP не должно меняться при ошибке расчёта");
        }

        [Test]
        public void WeaponSubtypeModifierIsAppliedOnlyWhenExplicitlyConfigured()
        {
            // По умолчанию тип слота 0 → модификатор (PARTIAL) не применяется.
            CombatAttack plain = CreateAttack(hitChance: 0.5);
            CombatOutcome plainOutcome = new DamageResolver(new FixedCombatRandom(30000)).Resolve(plain, new HealthPool(100.0));
            Assert.That(plainOutcome.EffectiveHitChance, Is.EqualTo(0.5).Within(Tolerance),
                "без явного типа оружия шанс не меняется");
            Assert.AreEqual(CombatStatus.Miss, plainOutcome.Status, "0.9156 > 0.5 — промах");

            // Явно указан слот оружия и подтип 8 → chance × 0.3.
            CombatAttack rune = CreateAttack(hitChance: 0.5);
            rune.EquippedItemType = 3;
            rune.WeaponSubtype = 8;
            CombatOutcome runeOutcome = new DamageResolver(new FixedCombatRandom(30000)).Resolve(rune, new HealthPool(100.0));
            Assert.That(runeOutcome.EffectiveHitChance, Is.EqualTo(0.15).Within(Tolerance));
            Assert.AreEqual(CombatStatus.Miss, runeOutcome.Status, "0.9156 > 0.15 — промах");
        }

        [Test]
        public void SameSeedProducesSameOutcomeSequence()
        {
            List<double> first = RunSequence(seed: 12345, strikes: 20);
            List<double> second = RunSequence(seed: 12345, strikes: 20);

            CollectionAssert.AreEqual(first, second, "одинаковый seed обязан давать одинаковую последовательность");
        }

        [Test]
        public void DifferentSeedsProduceDifferentOutcomes()
        {
            List<double> first = RunSequence(seed: 1, strikes: 10);
            List<double> second = RunSequence(seed: 2, strikes: 10);

            CollectionAssert.AreNotEqual(first, second, "разные seed должны давать разные последовательности");
        }

        [Test]
        public void HitRateOnSeededSourceIsStatisticallyConsistent()
        {
            // chance = 0.5; формула hit = roll/32767 < 0.5 даёт ровно половину диапазона.
            // Ожидание для n = 10000: p = 0.5, σ = sqrt(n·p·(1−p)) = 50.
            // Допуск ±4σ = ±200 (вероятность выхода за пределы ~6e-5 для одной серии).
            const int strikes = 10000;
            const int tolerance = 200;
            var random = new SeededCombatRandom(777);
            var resolver = new DamageResolver(random);
            var target = new HealthPool(1_000_000.0);

            int hits = 0;
            for (int i = 0; i < strikes; i++)
            {
                CombatOutcome outcome = resolver.Resolve(CreateAttack(hitChance: 0.5), target);
                if (outcome.IsHit)
                {
                    hits++;
                }
            }

            Assert.That(hits, Is.InRange(strikes / 2 - tolerance, strikes / 2 + tolerance),
                $"попаданий {hits} из {strikes} при chance=0.5 (±4σ)");
        }

        [Test]
        public void InvalidAttackInputIsRejectedBeforeAnyHealthChange()
        {
            var target = new HealthPool(100.0);
            var resolver = new DamageResolver(new FixedCombatRandom(0, 0));

            Assert.Throws<ArgumentOutOfRangeException>(() => new CombatAttack(0.0, 1, 0.5));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CombatAttack(1.0, 0, 0.5));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CombatAttack(1.0, 1, 1.5));

            CombatAttack attack = CreateAttack(hitChance: 1.0);
            Assert.Throws<ArgumentOutOfRangeException>(() => attack.TargetArmor = -1.0);
            Assert.Throws<ArgumentOutOfRangeException>(() => attack.TargetArmor = double.NaN);
            Assert.Throws<ArgumentOutOfRangeException>(() => attack.BlockFraction = 1.5);
            Assert.Throws<ArgumentOutOfRangeException>(() => attack.HitChance = -0.5);

            Assert.Throws<ArgumentNullException>(() => resolver.Resolve(null, target));
            Assert.Throws<ArgumentNullException>(() => resolver.Resolve(attack, null));
            Assert.AreEqual(100.0, target.CurrentHealth, Tolerance);
        }

        private static CombatAttack CreateAttack(double weaponBase = 1.0, double hitChance = 1.0)
        {
            return new CombatAttack(weaponBase, 1, hitChance);
        }

        private static List<double> RunSequence(int seed, int strikes)
        {
            var resolver = new DamageResolver(new SeededCombatRandom(seed));
            var target = new HealthPool(1_000_000.0);
            var results = new List<double>(strikes);

            for (int i = 0; i < strikes; i++)
            {
                results.Add(resolver.Resolve(CreateAttack(hitChance: 0.7), target).AppliedDamage);
            }

            return results;
        }
    }
}
