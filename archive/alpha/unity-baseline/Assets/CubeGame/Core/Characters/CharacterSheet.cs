using System;
using CubeGame.Core.Progression;

namespace CubeGame.Core.Characters
{
    /// <summary>
    /// Чистое состояние «лист персонажа»: прогрессия + класс/специализация + подтверждённые
    /// бонусы экипировки. Не знает ни о Unity, ни о сцене, ни о сохранениях.
    ///
    /// Ограничение S02: это НЕ боевая система. Здесь только вычисляемые характеристики,
    /// восстановленные в S00-R02; урон по цели, попадания, блок, комбо и способности
    /// в эту партию не входят.
    /// </summary>
    public sealed class CharacterSheet
    {
        private double _equipmentHealthBonus;

        public CharacterSheet(
            CharacterClass characterClass = CharacterClass.Warrior,
            CharacterSpecialization specialization = CharacterSpecialization.Primary,
            double baseHealth = CharacterStats.DefaultBaseHealth,
            double weaponBase = CharacterStats.DefaultWeaponBase)
        {
            if (baseHealth <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(baseHealth), baseHealth, "База HP должна быть положительной.");
            }

            if (weaponBase <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(weaponBase), weaponBase, "База урона должна быть положительной.");
            }

            Class = characterClass;
            Specialization = specialization;
            BaseHealth = baseHealth;
            WeaponBase = weaponBase;
            Progression = new ProgressionState();
        }

        public CharacterClass Class { get; }

        public CharacterSpecialization Specialization { get; }

        /// <summary>База HP персонажа (Alpha <c>0x178</c>).</summary>
        public double BaseHealth { get; set; }

        /// <summary>База урона оружия (Alpha <c>0x180</c>).</summary>
        public double WeaponBase { get; set; }

        public WeaponUpgradeFlags WeaponUpgrades { get; set; } = WeaponUpgradeFlags.None;

        public ProgressionState Progression { get; private set; }

        /// <summary>
        /// Сумма подтверждённых бонусов надетой экипировки. Точное соответствие «характеристика
        /// предмета → бонус HP» в Alpha не установлено (UNKNOWN), поэтому величина задаётся явно.
        /// </summary>
        public double EquipmentHealthBonus
        {
            get => _equipmentHealthBonus;
            set
            {
                if (value < 0.0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "Бонус экипировки не может быть отрицательным.");
                }

                _equipmentHealthBonus = value;
            }
        }

        public double MaxHealth => CharacterStats.CalcMaxHealth(BaseHealth, Progression.Level, Class, Specialization, _equipmentHealthBonus);

        public double WeaponDamage => CharacterStats.CalcWeaponDamage(WeaponBase, Progression.Level, WeaponUpgrades);

        /// <summary>Добавляет опыт; возвращает число полученных уровней.</summary>
        public int AddExperience(long amount)
        {
            return Progression.AddExperience(amount);
        }

        /// <summary>Восстанавливает прогрессию из сохранения (без пересчёта уровней).</summary>
        public void RestoreProgression(int level, long experience)
        {
            Progression = ProgressionState.FromSave(level, experience);
        }

        public override string ToString()
        {
            return $"{Class}/{Specialization} {Progression} hp={MaxHealth:F1} weapon={WeaponDamage:F2}";
        }
    }
}
