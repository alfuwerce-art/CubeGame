using System;
using CubeGame.Core.Progression;
using NUnit.Framework;

namespace CubeGame.Tests.EditMode
{
    /// <summary>
    /// Табличные тесты прогрессии S02.
    ///
    /// Эталон значений: docs/research/alpha2013/tools/alpha_curves_reference.py --check (PASS, exit 0, 2026-11-03).
    /// Каждое ожидаемое число взято из спецификации D_character §D4.1 / E_combat §E2, а не подобрано.
    /// </summary>
    public sealed class ProgressionTests
    {
        private const double Tolerance4Dp = 5e-4;

        [TestCase(1, 50)]
        [TestCase(2, 97)]
        [TestCase(3, 140)]
        [TestCase(4, 180)]
        [TestCase(5, 216)]
        [TestCase(6, 249)]
        [TestCase(7, 280)]
        [TestCase(8, 309)]
        [TestCase(9, 335)]
        [TestCase(10, 360)]
        [TestCase(11, 383)]
        [TestCase(12, 404)]
        [TestCase(20, 537)]
        [TestCase(50, 760)]
        [TestCase(100, 881)]
        public void ExperienceCostMatchesAlphaReference(int level, int expectedCost)
        {
            Assert.AreEqual(expectedCost, AlphaMath.ExperienceCostForNextLevel(level),
                $"cost(L={level}) должен совпадать с Alpha-эталоном");
        }

        [Test]
        public void ExperienceCostApproachesAsymptote()
        {
            Assert.AreEqual(1049, AlphaMath.ExperienceCostForNextLevel(1_000_000),
                "предел стоимости уровня — 1050, при больших L целое значение 1049");
        }

        [Test]
        public void ExperienceCostIsMonotonic()
        {
            int previous = 0;
            for (int level = 1; level <= 500; level++)
            {
                int cost = AlphaMath.ExperienceCostForNextLevel(level);
                Assert.GreaterOrEqual(cost, previous, $"стоимость уровня должна расти: L={level}");
                previous = cost;
            }
        }

        [TestCase(1, 1.0000)]
        [TestCase(5, 1.4142)]
        [TestCase(11, 2.0000)]
        [TestCase(21, 2.8284)]
        // REVIEW-FIX-01: эталонная таблица alpha_curves_reference.py исправлена —
        // levelFactor(51) = 4.416358054 → 4.4164 (было 4.4160), levelFactor(101) = 5.656854249 → 5.6569
        // (было 5.6574). Старый прогон печатал PASS только потому, что допуск 5e-4 «поглощал»
        // ошибку округления (|5.6569 − 5.6574| в float64 = 4.999999999999723e-4).
        [TestCase(51, 4.4164)]
        [TestCase(101, 5.6569)]
        public void LevelFactorMatchesAlphaReference(int level, double expected)
        {
            Assert.That(AlphaMath.LevelFactor(level), Is.EqualTo(expected).Within(Tolerance4Dp),
                $"levelFactor(L={level})");
        }

        [Test]
        public void LevelFactorApproachesEight()
        {
            Assert.That(AlphaMath.LevelFactor(1_000_000), Is.EqualTo(8.0).Within(1e-3),
                "предел levelFactor — 2^3 = 8");
        }

        [TestCase(0, 1.0000)]
        [TestCase(1, 1.1892)]
        [TestCase(2, 1.4142)]
        [TestCase(4, 2.0000)]
        [TestCase(8, 4.0000)]
        public void StarFactorMatchesAlphaReference(int stars, double expected)
        {
            Assert.That(AlphaMath.StarFactor(stars), Is.EqualTo(expected).Within(Tolerance4Dp),
                $"starFactor(S={stars})");
        }

        [TestCase(0, 0.750)]
        [TestCase(1, 0.812)]
        [TestCase(4, 1.000)]
        [TestCase(8, 1.250)]
        public void FormHasteMatchesAlphaReference(int stars, double expected)
        {
            Assert.That(AlphaMath.FormHaste(stars), Is.EqualTo(expected).Within(Tolerance4Dp),
                $"formHaste(S={stars})");
        }

        [TestCase(0, 0.0)]
        [TestCase(9, 0.4737)]
        public void PowerFactorMatchesAlphaReference(int rank, double expected)
        {
            Assert.That(AlphaMath.PowerFactor(rank), Is.EqualTo(expected).Within(Tolerance4Dp),
                $"powerFactor(rank={rank})");
        }

        [Test]
        public void KillExperienceIsClampedToOne()
        {
            Assert.AreEqual(1.0, AlphaMath.KillExperience(1, 0), 1e-9);
            Assert.Greater(AlphaMath.KillExperience(50, 0), 1.0);
        }

        [Test]
        public void AddExperienceRaisesLevelAndKeepsRemainder()
        {
            var progression = new ProgressionState();
            Assert.AreEqual(1, progression.Level);
            Assert.AreEqual(50, progression.ExperienceToNextLevel);

            int gained = progression.AddExperience(50);

            Assert.AreEqual(1, gained, "50 XP при L=1 даёт ровно один уровень");
            Assert.AreEqual(2, progression.Level);
            Assert.AreEqual(0, progression.Experience);
            Assert.AreEqual(97, progression.ExperienceToNextLevel, "следующая стоимость — cost(L=2) = 97");
        }

        [Test]
        public void AddExperienceCanRaiseSeveralLevelsAtOnce()
        {
            var progression = new ProgressionState();

            // 50 + 97 + 140 + 180 = 467 — точная сумма cost(1..4) по эталону Alpha.
            int gained = progression.AddExperience(467);

            Assert.AreEqual(4, gained, "467 XP поднимают персонажа с 1 до 5 уровня");
            Assert.AreEqual(5, progression.Level);
            Assert.AreEqual(0, progression.Experience);
            Assert.AreEqual(216, progression.ExperienceToNextLevel);
        }

        [Test]
        public void AddExperienceBelowThresholdKeepsSameLevel()
        {
            var progression = new ProgressionState();
            Assert.AreEqual(0, progression.AddExperience(49));
            Assert.AreEqual(1, progression.Level);
            Assert.AreEqual(49, progression.Experience);
        }

        [Test]
        public void ExperienceAccumulatesAcrossCalls()
        {
            var progression = new ProgressionState();
            progression.AddExperience(30);
            Assert.AreEqual(0, progression.AddExperience(19));
            Assert.AreEqual(1, progression.Level);
            Assert.AreEqual(1, progression.AddExperience(1));
            Assert.AreEqual(2, progression.Level);
            Assert.AreEqual(0, progression.Experience);
        }

        [Test]
        public void NoLevelCapIsApplied()
        {
            var progression = new ProgressionState();
            progression.AddExperience(100_000_000);
            Assert.Greater(progression.Level, 1000, "в Alpha верхнего капа уровня не найдено — он не вводится");
        }

        [Test]
        public void NegativeExperienceIsRejected()
        {
            var progression = new ProgressionState();
            Assert.Throws<ArgumentOutOfRangeException>(() => progression.AddExperience(-1));
        }

        [Test]
        public void InvalidConstructorArgumentsAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ProgressionState(0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ProgressionState(1, -1));
        }

        [Test]
        public void LevelFactorIsRejectedBelowLevelOne()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => AlphaMath.LevelFactor(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => AlphaMath.ExperienceCostForNextLevel(0));
        }

        [Test]
        public void RestoredStateDoesNotRecalculateLevels()
        {
            ProgressionState restored = ProgressionState.FromSave(10, 123);
            Assert.AreEqual(10, restored.Level);
            Assert.AreEqual(123, restored.Experience);
            Assert.AreEqual(360, restored.ExperienceToNextLevel);
        }

        // --- REVIEW-FIX-01 / FIX-02: безопасность прогрессии -------------------

        [Test]
        public void ExperienceNearLongMaxIsRejectedWithoutStateChange()
        {
            var progression = new ProgressionState(1, long.MaxValue - 5);

            Assert.Throws<ArgumentOutOfRangeException>(() => progression.AddExperience(10));

            Assert.AreEqual(1, progression.Level, "уровень не должен меняться при отказе");
            Assert.AreEqual(long.MaxValue - 5, progression.Experience, "опыт не должен меняться при отказе");
        }

        [Test]
        public void AmountAbovePerCallLimitIsRejectedWithoutStateChange()
        {
            var progression = new ProgressionState();

            Assert.Throws<ArgumentOutOfRangeException>(
                () => progression.AddExperience(ProgressionState.MaxExperiencePerCall + 1));

            Assert.AreEqual(1, progression.Level);
            Assert.AreEqual(0L, progression.Experience);
        }

        [Test]
        public void HugeRestoredExperienceCannotHangOrPartiallyApply()
        {
            // 2e9 опыта при минимальной стоимости 50 — нижняя оценка ~1.9 млн повышений,
            // больше лимита на вызов. Проверка обязана сработать до цикла (без зависания).
            var progression = new ProgressionState(1, 2_000_000_000L);

            Assert.Throws<ArgumentOutOfRangeException>(() => progression.AddExperience(1));

            Assert.AreEqual(1, progression.Level, "состояние не должно меняться при отказе");
            Assert.AreEqual(2_000_000_000L, progression.Experience);
        }

        [Test]
        public void LevelOverflowIsRejectedWithoutStateChange()
        {
            var progression = new ProgressionState(int.MaxValue, 0);

            Assert.Throws<InvalidOperationException>(() => progression.AddExperience(2000));

            Assert.AreEqual(int.MaxValue, progression.Level);
            Assert.AreEqual(0L, progression.Experience);
        }

        [Test]
        public void MaximumLevelStillAcceptsExperienceBelowCost()
        {
            var progression = new ProgressionState(int.MaxValue, 0);

            int gained = progression.AddExperience(AlphaMath.ExperienceCostForNextLevel(int.MaxValue) - 1);

            Assert.AreEqual(0, gained, "на предельном уровне опыт можно копить, но не повышать его");
            Assert.AreEqual(int.MaxValue, progression.Level);
        }

        [Test]
        public void LargeButValidExperienceIsAppliedQuickly()
        {
            var progression = new ProgressionState();

            int gained = progression.AddExperience(ProgressionState.MaxExperiencePerCall);

            Assert.Greater(gained, 900_000, "1e9 опыта даёт около 950 тысяч уровней (стоимость ~1049)");
            Assert.Less(progression.Experience, progression.ExperienceToNextLevel,
                "после массового повышения остаток опыта меньше стоимости уровня");
            Assert.AreEqual(1 + gained, progression.Level);
        }
    }
}
