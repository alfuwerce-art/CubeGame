using System;

namespace CubeGame.Core.Combat
{
    /// <summary>Подробный результат снижения урона (для логов, тестов и отчёта удара).</summary>
    public readonly struct MitigationResult
    {
        public MitigationResult(
            double rawDamage,
            double armor,
            double blockFraction,
            double defenseCapacity,
            double absorbedDamage,
            double finalDamage)
        {
            RawDamage = rawDamage;
            Armor = armor;
            BlockFraction = blockFraction;
            DefenseCapacity = defenseCapacity;
            AbsorbedDamage = absorbedDamage;
            FinalDamage = finalDamage;
        }

        /// <summary>Урон до защиты.</summary>
        public double RawDamage { get; }

        /// <summary>Переданное значение брони цели.</summary>
        public double Armor { get; }

        /// <summary>Доля брони, реально сработавшая (0…1).</summary>
        public double BlockFraction { get; }

        /// <summary>
        /// Расчётная защита <c>blockFraction · armor</c> — сколько урона защита могла бы поглотить.
        /// Может быть больше входящего удара (диагностическая величина, не «поглощённый урон»).
        /// </summary>
        public double DefenseCapacity { get; }

        /// <summary>
        /// Фактически предотвращённый урон: <c>min(RawDamage, DefenseCapacity)</c>.
        /// Никогда не превышает <see cref="RawDamage"/>, поэтому <c>AbsorbedDamage + FinalDamage = RawDamage</c>.
        /// </summary>
        public double AbsorbedDamage { get; }

        /// <summary>Итоговый урон, не меньше нуля.</summary>
        public double FinalDamage { get; }

        public override string ToString()
        {
            return $"raw={RawDamage:F3} armor={Armor:F3} blockFraction={BlockFraction:F3} " +
                   $"capacity={DefenseCapacity:F3} absorbed={AbsorbedDamage:F3} final={FinalDamage:F3}";
        }
    }

    /// <summary>
    /// Снижение урона бронёй и блоком.
    ///
    /// <para>
    /// Восстановленная часть (E3, <c>PARTIAL</c>): <c>final = max(0, rawDamage − blockFraction · armor)</c>.
    /// Контрольный случай исследования: rawDamage = 30, armor = 10, blockFraction = 0.5 → 25.
    /// </para>
    /// <para>
    /// ЧЕГО ЗДЕСЬ НЕТ (сознательно, до новых доказательств): полного порядка операций защиты
    /// (блок → броня → HP), автоматического вычисления <c>blockFraction</c> из боевой серии
    /// (<c>+0x70</c>, OQ-24), поведения щита и энергии блока (<c>+0x174</c>).
    /// Доля блока всегда передаётся вызывающим кодом.
    /// </para>
    /// </summary>
    public static class DamageMitigation
    {
        public static MitigationResult Apply(double rawDamage, double armor, double blockFraction)
        {
            ValidateNonNegativeFinite(rawDamage, nameof(rawDamage));
            ValidateNonNegativeFinite(armor, nameof(armor));

            if (double.IsNaN(blockFraction) || double.IsInfinity(blockFraction) || blockFraction < 0.0 || blockFraction > 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(blockFraction), blockFraction, "Доля блока должна быть в диапазоне 0…1.");
            }

            // Расчётная защита — сколько урона защита могла бы поглотить.
            double defenseCapacity = blockFraction * armor;

            // Фактически предотвращённый урон не может превысить сам удар (REVIEW-FIX-01).
            double absorbed = Math.Min(rawDamage, defenseCapacity);
            double finalDamage = rawDamage - absorbed;
            if (finalDamage < 0.0)
            {
                finalDamage = 0.0;
            }

            return new MitigationResult(rawDamage, armor, blockFraction, defenseCapacity, absorbed, finalDamage);
        }

        private static void ValidateNonNegativeFinite(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0.0)
            {
                throw new ArgumentOutOfRangeException(parameterName, value, "Значение должно быть конечным и неотрицательным.");
            }
        }
    }
}
