using System;
using CubeGame.Core.Characters;

namespace CubeGame.Core.Combat
{
    /// <summary>
    /// Вход боевого расчёта: характеристики атакующего и параметры цели.
    ///
    /// Модель минимальная: здесь нет слотов экипировки, способностей, состояний и таймингов.
    /// Шанс попадания задаётся явно (OQ-01: базовая формула <c>chance</c> в Alpha не восстановлена),
    /// доля блока — тоже явно (OQ-24: <c>+0x70</c> не восстановлен).
    /// </summary>
    public sealed class CombatAttack
    {
        private double _hitChance;
        private double _targetArmor;
        private double _blockFraction;

        public CombatAttack(double weaponBase, int level, double hitChance)
        {
            if (double.IsNaN(weaponBase) || double.IsInfinity(weaponBase) || weaponBase <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(weaponBase), weaponBase, "База урона оружия должна быть конечной и положительной.");
            }

            if (level < Progression.ProgressionState.MinLevel)
            {
                throw new ArgumentOutOfRangeException(nameof(level), level, "Уровень атакующего начинается с 1.");
            }

            WeaponBase = weaponBase;
            Level = level;
            HitChance = hitChance;
        }

        /// <summary>База урона оружия (Alpha <c>0x180</c>, после ресета 1.0).</summary>
        public double WeaponBase { get; }

        public int Level { get; }

        /// <summary>Звёзды атакующего (для флагов апгрейдов; у игрока звёздный множитель не используется).</summary>
        public int Stars { get; set; }

        public WeaponUpgradeFlags Upgrades { get; set; } = WeaponUpgradeFlags.None;

        /// <summary>
        /// Тип предмета в слоте основной руки. По умолчанию 0 — «не оружие»: частичный модификатор
        /// шанса (<c>PARTIAL</c>, E6) применяется только если вызывающий код явно указал тип оружия (3).
        /// </summary>
        public int EquippedItemType { get; set; }

        /// <summary>Подтип оружия (Alpha <c>+0x01</c>); используется только модификатором шанса.</summary>
        public int WeaponSubtype { get; set; }

        /// <summary>
        /// Шанс попадания 0…1, переданный вызывающим кодом. Базовая формула Alpha для персонажа
        /// не восстановлена (OQ-01), поэтому значение не «по умолчанию из игры», а вход.
        /// </summary>
        public double HitChance
        {
            get => _hitChance;
            set
            {
                if (double.IsNaN(value) || double.IsInfinity(value) || value < 0.0 || value > 1.0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Шанс попадания должен быть в диапазоне 0…1.");
                }

                _hitChance = value;
            }
        }

        /// <summary>Значение брони цели (подтверждённая часть E3 передаётся числом).</summary>
        public double TargetArmor
        {
            get => _targetArmor;
            set => _targetArmor = ValidateNonNegativeFinite(value, nameof(value));
        }

        /// <summary>Доля брони, сработавшая при ударе (0…1), задаётся явно (OQ-24 не закрыт).</summary>
        public double BlockFraction
        {
            get => _blockFraction;
            set
            {
                if (double.IsNaN(value) || double.IsInfinity(value) || value < 0.0 || value > 1.0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Доля блока должна быть в диапазоне 0…1.");
                }

                _blockFraction = value;
            }
        }

        /// <summary>
        /// Применять ли множитель по числовому ID атаки. По умолчанию <c>false</c>:
        /// место применения в Alpha не доказано (<c>PARTIAL</c>), поэтому включается только явно.
        /// </summary>
        public bool UseAttackIdMultiplier { get; set; }

        /// <summary>Числовой ID атаки (Alpha <c>+0x68</c>); названия и назначение не установлены.</summary>
        public int AttackId { get; set; }

        /// <summary>
        /// Аддитивный вклад экипированного оружия (<c>Σ item_weaponDamage</c>), добавляемый к
        /// <c>base(0x180)·levelFactor</c> уже после масштабирования уровнем персонажа.
        ///
        /// <para>
        /// СТАТУС: <c>PARTIAL</c>. Структура восстановлена по E_combat §E2
        /// (<c>base(0x180)·levelFactor + флаги + список item_weaponDamage</c>), но полный порядок
        /// применения в Alpha не доказан. Величина <b>не</b> умножается на множитель уровня персонажа —
        /// иначе получилось бы двойное масштабирование (<c>ItemStatsCalculator.WeaponDamage</c>
        /// уже содержит множитель уровня предмета).
        /// </para>
        /// </summary>
        public double ItemWeaponDamageBonus
        {
            get => _itemWeaponDamageBonus;
            set => _itemWeaponDamageBonus = ValidateNonNegativeFinite(value, nameof(value));
        }

        private double _itemWeaponDamageBonus;

        private static double ValidateNonNegativeFinite(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0.0)
            {
                throw new ArgumentOutOfRangeException(parameterName, value, "Значение должно быть конечным и неотрицательным.");
            }

            return value;
        }
    }
}
