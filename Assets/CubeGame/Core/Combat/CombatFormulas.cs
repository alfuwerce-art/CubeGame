using System;
using CubeGame.Core.Characters;
using CubeGame.Core.Progression;

namespace CubeGame.Core.Combat
{
    /// <summary>
    /// Формулы боя Cube World Alpha 2013, восстановленные в S00-R02 (E_combat §E2, §E3, §E5, §E6).
    ///
    /// Каждая часть помечена уровнем достоверности по D42:
    /// <list type="bullet">
    /// <item><c>REFERENCE_VERIFIED</c> — база урона/levelFactor, проверка попадания, порог блока;</item>
    /// <item><c>STRONGLY_SUPPORTED</c> — разброс урона <c>1.25 − 0.5·roll/32767</c>;</item>
    /// <item><c>PARTIAL</c> — множители по числовым ID атак, модификаторы шанса по подтипу оружия.</item>
    /// </list>
    /// Чистая логика: без UnityEngine, без состояния.
    /// </summary>
    public static class CombatFormulas
    {
        /// <summary>Максимальный бросок Alpha в бою (<c>rand()</c> → 0…32767).</summary>
        public const int MaxRoll = 32767;

        /// <summary>Максимальный бросок проверки блока (Alpha: <c>item_rarityScaled()</c> → 0…100).</summary>
        public const int BlockRollMax = 100;

        /// <summary>Минимальный множитель разброса урона (roll = 32767).</summary>
        public const double MinVariance = 0.75;

        /// <summary>Максимальный множитель разброса урона (roll = 0).</summary>
        public const double MaxVariance = 1.25;

        /// <summary>
        /// Разброс урона <c>v = 1.25 − 0.5·roll/32767</c> (E5, <c>STRONGLY_SUPPORTED</c>).
        /// roll = 0 → 1.25; roll = 32767 → 0.75.
        /// </summary>
        public static double DamageVariance(int roll)
        {
            ValidateRoll(roll, nameof(roll));

            return MaxVariance - 0.5 * roll / MaxRoll;
        }

        /// <summary>
        /// Урон оружия: <c>base(0x180) · levelFactor</c> плюс подтверждённые флаги апгрейдов
        /// (<c>STRONGLY_SUPPORTED</c>, E2). Множители по ID атаки здесь не применяются.
        /// </summary>
        public static double WeaponDamage(double weaponBase, int level, WeaponUpgradeFlags upgrades = WeaponUpgradeFlags.None, int stars = 0)
        {
            return CharacterStats.CalcWeaponDamage(weaponBase, level, upgrades, stars);
        }

        /// <summary>
        /// Множитель по числовому ID атаки (E5: <c>0x57 ×50</c>, <c>0x5B ×2</c>, <c>0x5D ×5</c>, <c>0x60 ×3</c>).
        ///
        /// <para>
        /// СТАТУС: <c>PARTIAL</c>. Числа восстановлены, но место применения в Alpha (какая ветвь
        /// атаки и на каком этапе) не доказано, а названия/назначение способностей не установлены.
        /// Поэтому функция изолирована и применяется резолвером **только** при явном флаге
        /// <see cref="CombatAttack.UseAttackIdMultiplier"/>; по умолчанию урон считается без множителя.
        /// </para>
        /// </summary>
        public static bool TryGetAttackIdMultiplier(int attackId, out double multiplier)
        {
            switch (attackId)
            {
                case 0x57:
                    multiplier = 50.0;
                    return true;
                case 0x5B:
                    multiplier = 2.0;
                    return true;
                case 0x5D:
                    multiplier = 5.0;
                    return true;
                case 0x60:
                    multiplier = 3.0;
                    return true;
                default:
                    multiplier = 1.0;
                    return false;
            }
        }

        /// <summary>Применяет множитель ID атаки; для неизвестного ID возвращает урон без изменений.</summary>
        public static double ApplyAttackIdMultiplier(double damage, int attackId)
        {
            if (damage < 0.0 || double.IsNaN(damage) || double.IsInfinity(damage))
            {
                throw new ArgumentOutOfRangeException(nameof(damage), damage, "Урон должен быть конечным и неотрицательным.");
            }

            return TryGetAttackIdMultiplier(attackId, out double multiplier) ? damage * multiplier : damage;
        }

        /// <summary>
        /// Порог успешного блока: <c>int((1 − 1/((L−1)·0.05+1))·100 + 1)</c> (E6, <c>REFERENCE_VERIFIED</c>).
        /// L=1 → 1, L=2 → 5, L=5 → 17, L=10 → 32, L=20 → 49, L=50 → 72.
        /// </summary>
        public static int BlockThreshold(int level)
        {
            return (int)(AlphaMath.CurveLevel05(level) * 100.0 + 1.0);
        }

        /// <summary>
        /// Бросок блока: успешен, если <paramref name="roll"/> не превышает порог (E6).
        /// Полный алгоритм блока (тип щита, энергия блока) не восстановлен — <c>PARTIAL</c>.
        /// </summary>
        public static bool RollBlock(int level, int roll)
        {
            if (roll < 0 || roll > BlockRollMax)
            {
                throw new ArgumentOutOfRangeException(nameof(roll), roll, "Бросок блока должен быть в диапазоне 0…100.");
            }

            return roll <= BlockThreshold(level);
        }

        /// <summary>
        /// Модификатор шанса попадания по подтипу оружия (E6).
        ///
        /// <para>
        /// СТАТУС: <c>PARTIAL</c>. В спецификации записано: для «материала оружия» (<c>0xaa8 == 3</c>,
        /// т.е. в слоте основной руки предмет типа «оружие») подтипы <c>0,1,2,0x0C</c> дают
        /// <c>chance × 0.5</c>, подтипы <c>8,10</c> — <c>chance × 0.3</c>. Семантика этой ветви
        /// (почему именно эти подтипы) не доказана, поэтому функция изолирована и вызывается только
        /// резолвером; базовый шанс передаёт вызывающий код (OQ-01 остаётся `UNKNOWN`).
        /// </para>
        /// </summary>
        public static double ApplyWeaponSubtypeHitModifier(double chance, int equippedItemType, int weaponSubtype)
        {
            if (double.IsNaN(chance) || double.IsInfinity(chance) || chance < 0.0 || chance > 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(chance), chance, "Шанс попадания должен быть в диапазоне 0…1.");
            }

            // Модификатор относится только к оружию в слоте основной руки (тип предмета 3).
            if (equippedItemType != (int)Items.ItemType.Weapon)
            {
                return chance;
            }

            switch (weaponSubtype)
            {
                case 0:
                case 1:
                case 2:
                case 0x0C:
                    return chance * 0.5;
                case 8:
                case 10:
                    return chance * 0.3;
                default:
                    return chance;
            }
        }

        private static void ValidateRoll(int roll, string parameterName)
        {
            if (roll < 0 || roll > MaxRoll)
            {
                throw new ArgumentOutOfRangeException(parameterName, roll, $"Бросок должен быть в диапазоне 0…{MaxRoll}.");
            }
        }
    }
}
