using System;

namespace CubeGame.Core.Combat
{
    /// <summary>Исход проверки попадания.</summary>
    public enum HitOutcome
    {
        /// <summary>Промах.</summary>
        Miss = 0,

        /// <summary>Попадание.</summary>
        Hit = 1,
    }

    /// <summary>Результат проверки попадания с диагностикой (для логов и тестов).</summary>
    public readonly struct HitCheckResult
    {
        public HitCheckResult(HitOutcome outcome, double chance, int roll)
        {
            Outcome = outcome;
            Chance = chance;
            Roll = roll;
        }

        public HitOutcome Outcome { get; }

        /// <summary>Эффективный шанс попадания (после модификаторов подтипа оружия).</summary>
        public double Chance { get; }

        /// <summary>Израсходованный бросок 0…32767.</summary>
        public int Roll { get; }

        public bool IsHit => Outcome == HitOutcome.Hit;

        public override string ToString()
        {
            return $"{Outcome} chance={Chance:F3} roll={Roll}";
        }
    }

    /// <summary>
    /// Проверка попадания по подтверждённой формуле Alpha (E6, <c>REFERENCE_VERIFIED</c>):
    /// <c>hit = roll / 32767.0 &lt; chance</c>.
    ///
    /// Базовая формула <c>chance</c> для конкретного персонажа **не восстановлена** (OQ-01),
    /// поэтому шанс всегда передаёт вызывающий код; здесь нет «балансного» значения по умолчанию.
    /// </summary>
    public static class HitResolver
    {
        public static bool IsHit(double chance, int roll)
        {
            ValidateChance(chance);
            ValidateRoll(roll);

            return roll / (double)CombatFormulas.MaxRoll < chance;
        }

        public static HitCheckResult Check(double chance, int roll)
        {
            return new HitCheckResult(IsHit(chance, roll) ? HitOutcome.Hit : HitOutcome.Miss, chance, roll);
        }

        private static void ValidateChance(double chance)
        {
            if (double.IsNaN(chance) || double.IsInfinity(chance) || chance < 0.0 || chance > 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(chance), chance, "Шанс попадания должен быть в диапазоне 0…1.");
            }
        }

        private static void ValidateRoll(int roll)
        {
            if (roll < 0 || roll > CombatFormulas.MaxRoll)
            {
                throw new ArgumentOutOfRangeException(nameof(roll), roll, $"Бросок должен быть в диапазоне 0…{CombatFormulas.MaxRoll}.");
            }
        }
    }
}
