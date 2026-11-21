using System;

namespace CubeGame.Core.Progression
{
    /// <summary>
    /// Числовые кривые Cube World Alpha 2013, восстановленные в S00-R02.
    ///
    /// PROVENANCE (D42, REFERENCE_VERIFIED): это собственная реализация математических форм,
    /// восстановленных из Alpha, а не перенос декомпилированного кода. Эталон для сверки:
    /// docs/research/alpha2013/tools/alpha_curves_reference.py --check (PASS, exit 0, 2026-11-03).
    ///
    /// Все функции не зависят от Unity и не содержат состояния: только формулы.
    /// </summary>
    public static class AlphaMath
    {
        /// <summary>Множитель звёздности игрока: у роли «игрок» (Alpha 0x060 == 0) он фиксирован 2.0.</summary>
        public const double PlayerStarFactor = 2.0;

        /// <summary>
        /// Базовая насыщающаяся кривая <c>1 - 1/((n-1)*0.05 + 1)</c> (Alpha <c>0043ca60</c>).
        /// Предел при больших n — 1.0.
        /// </summary>
        public static double CurveLevel05(double n)
        {
            return 1.0 - 1.0 / ((n - 1.0) * 0.05 + 1.0);
        }

        /// <summary>
        /// Множитель уровня <c>2^(curve_level05(L)*3)</c> (Alpha <c>00444db0</c>/<c>0043cff0</c>).
        /// L=1 → 1.0, L=11 → 2.0, предел 8.0.
        /// </summary>
        public static double LevelFactor(int level)
        {
            if (level < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(level), level, "Уровень начинается с 1 (Alpha).");
            }

            return LevelFactor((double)level);
        }

        /// <summary>
        /// Множитель уровня для дробного уровня: у предметов уровень равен
        /// <c>level + 0.1·powerBonus</c>, поэтому формула должна принимать нецелое значение.
        /// </summary>
        public static double LevelFactor(double level)
        {
            if (level < 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(level), level, "Уровень начинается с 1 (Alpha).");
            }

            return Math.Pow(2.0, CurveLevel05(level) * 3.0);
        }

        /// <summary>
        /// Множитель звёздности <c>2^(S*0.25)</c> (Alpha <c>00444db0</c>).
        /// S=0 → 1.0, S=4 → 2.0, S=8 → 4.0.
        /// </summary>
        public static double StarFactor(int stars)
        {
            if (stars < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(stars), stars, "Звёзды не могут быть отрицательными.");
            }

            return Math.Pow(2.0, stars * 0.25);
        }

        /// <summary>
        /// Стоимость следующего уровня <c>int((1 - 1/((L-1)*0.05 + 1))*1000 + 50)</c>
        /// (Alpha <c>00444d60</c>). Асимптота 1050: L=1 → 50, L=10 → 360, L=100 → 881.
        /// </summary>
        public static int ExperienceCostForNextLevel(int level)
        {
            if (level < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(level), level, "Уровень начинается с 1 (Alpha).");
            }

            return (int)(CurveLevel05(level) * 1000.0 + 50.0);
        }

        /// <summary>
        /// Базовая скорость в специальной форме <c>0.75 + 0.0625*S</c> (Alpha <c>00447700</c>).
        /// PLACEHOLDER-контекст: множитель восстановлен, но масштаб единиц движения — OQ-23.
        /// </summary>
        public static double FormHaste(int stars)
        {
            return 0.75 + 0.0625 * stars;
        }

        /// <summary>
        /// «Power factor» ранга <c>1 - 1/(rank*0.1 + 1)</c> (Alpha <c>0043ed60</c>).
        /// </summary>
        public static double PowerFactor(int rank)
        {
            if (rank < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(rank), rank, "Ранг не может быть отрицательным.");
            }

            return 1.0 - 1.0 / (rank * 0.1 + 1.0);
        }

        /// <summary>
        /// Опыт за убийство <c>max(1, curve_level05(level + stars)*20)</c>
        /// (Alpha <c>00595a60</c>, статус PARTIAL — точка вызова уточняется).
        /// </summary>
        public static double KillExperience(int level, int stars)
        {
            return Math.Max(1.0, CurveLevel05(level + stars) * 20.0);
        }
    }
}
