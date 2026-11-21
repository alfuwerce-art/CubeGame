using System;
using CubeGame.Core.Progression;

namespace CubeGame.Core.Characters
{
    /// <summary>Флаги «апгрейдов» существа Alpha (поле <c>0x7e</c>), влияющие на урон оружия.</summary>
    [Flags]
    public enum WeaponUpgradeFlags
    {
        /// <summary>Без флагов.</summary>
        None = 0,

        /// <summary>Alpha <c>0x7e &amp; 0x8</c>: <c>+= levelFactor · starFactor · 2.0</c>. Семантика флага — UNKNOWN.</summary>
        Upgrade8 = 0x8,

        /// <summary>Alpha <c>0x7e &amp; 0x10</c>: <c>+= levelFactor · starFactor · 8.0</c>. Семантика флага — UNKNOWN.</summary>
        Upgrade16 = 0x10,
    }

    /// <summary>
    /// Формулы характеристик персонажа, восстановленные в S00-R02 (D42, REFERENCE_VERIFIED/PARTIAL).
    ///
    /// <para>
    /// <c>calcMaxHealth</c> (Alpha <c>00444db0</c>, в реконструкции ошибочно назван
    /// <c>stat_calcAttackDamage</c>):
    /// <c>base(0x178) · levelFactor(L) · starFactorOr2 · classMult · specMult + equipmentBonuses</c>.
    /// </para>
    /// <para>
    /// <c>calcWeaponDamage</c> (Alpha <c>0043dc60</c>): реализована только подтверждённая часть —
    /// <c>base(0x180) · levelFactor</c> плюс флаги апгрейдов. Множители типа атаки
    /// (<c>0x57 ×50</c>, <c>0x5b ×2</c>, <c>0x5d ×5</c>, <c>0x60 ×3</c>) НЕ реализованы:
    /// их привязка к типам удара не установлена (OQ/UNKNOWN), и подменять её догадкой запрещено (D42).
    /// </para>
    /// Чистая логика: без Unity.
    /// </summary>
    public static class CharacterStats
    {
        /// <summary>Базовая величина HP <c>0x178</c> у проверенного сейва Warrior 1 уровня (REFERENCE_VERIFIED).</summary>
        public const double DefaultBaseHealth = 100.0;

        /// <summary>База урона оружия <c>0x180</c> после <c>Player_resetState</c> (Alpha: 1.0).</summary>
        public const double DefaultWeaponBase = 1.0;

        public const double WarriorHealthMultiplier = 1.3;
        public const double RangerHealthMultiplier = 1.1;
        public const double MageHealthMultiplier = 1.0;
        public const double RogueHealthMultiplier = 1.2;

        /// <summary>
        /// Дополнительный множитель второй специализации. Alpha: <c>1.25</c> при <c>class == 1 &amp;&amp; spec == 1</c>;
        /// с учётом записи <c>+0x140 = classIdx + 1</c> это Warrior + Guardian (PARTIAL: вывод из двух
        /// утверждений спецификации, отдельным прогоном не проверялся).
        /// </summary>
        public const double GuardianHealthMultiplier = 1.25;

        /// <summary>Множитель HP класса (Alpha: Warrior 1.3, Ranger 1.1, Mage 1.0, Rogue 1.2).</summary>
        public static double ClassHealthMultiplier(CharacterClass characterClass)
        {
            switch (characterClass)
            {
                case CharacterClass.Warrior:
                    return WarriorHealthMultiplier;
                case CharacterClass.Ranger:
                    return RangerHealthMultiplier;
                case CharacterClass.Mage:
                    return MageHealthMultiplier;
                case CharacterClass.Rogue:
                    return RogueHealthMultiplier;
                default:
                    throw new ArgumentOutOfRangeException(nameof(characterClass), characterClass, "Неизвестный класс.");
            }
        }

        /// <summary>Множитель HP специализации (1.25 только для Warrior + вторая специализация).</summary>
        public static double SpecializationHealthMultiplier(CharacterClass characterClass, CharacterSpecialization specialization)
        {
            return characterClass == CharacterClass.Warrior && specialization == CharacterSpecialization.Secondary
                ? GuardianHealthMultiplier
                : 1.0;
        }

        /// <summary>
        /// Максимальное HP игрока: <c>base · levelFactor · 2.0 · classMult · specMult + equipmentBonus</c>.
        /// Звёздный множитель игрока фиксирован 2.0 (Alpha: роль <c>0x060 == 0</c>).
        /// </summary>
        /// <param name="baseHealth">База <c>0x178</c>.</param>
        /// <param name="level">Уровень персонажа (с 1).</param>
        /// <param name="characterClass">Класс.</param>
        /// <param name="specialization">Специализация.</param>
        /// <param name="equipmentHealthBonus">
        /// Сумма подтверждённых бонусов надетой экипировки. В проверенном сейве Alpha это +8 при двух
        /// предметах; какая именно характеристика предмета даёт эти единицы — UNKNOWN (не подменяется догадкой).
        /// </param>
        public static double CalcMaxHealth(
            double baseHealth,
            int level,
            CharacterClass characterClass,
            CharacterSpecialization specialization,
            double equipmentHealthBonus = 0.0)
        {
            if (baseHealth <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(baseHealth), baseHealth, "База HP должна быть положительной.");
            }

            if (equipmentHealthBonus < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(equipmentHealthBonus), equipmentHealthBonus, "Бонус экипировки не может быть отрицательным.");
            }

            double health = baseHealth
                            * AlphaMath.LevelFactor(level)
                            * AlphaMath.PlayerStarFactor
                            * ClassHealthMultiplier(characterClass)
                            * SpecializationHealthMultiplier(characterClass, specialization);

            return health + equipmentHealthBonus;
        }

        /// <summary>
        /// Максимальное HP существа, не являющегося игроком: звёздный множитель <c>2^(0.25·S)</c>
        /// (Alpha: роль <c>0x060 != 0</c>). Класс и специализация у мобов не применяются.
        /// </summary>
        public static double CalcCreatureMaxHealth(double baseHealth, int level, int stars, double equipmentHealthBonus = 0.0)
        {
            return baseHealth * AlphaMath.LevelFactor(level) * AlphaMath.StarFactor(stars) + equipmentHealthBonus;
        }

        /// <summary>
        /// Подтверждённая часть урона оружия: <c>base · levelFactor</c> плюс флаги апгрейдов.
        /// Множители типа атаки не применяются — их соответствие типам удара не восстановлено.
        /// </summary>
        public static double CalcWeaponDamage(double weaponBase, int level, WeaponUpgradeFlags upgrades, int stars = 0)
        {
            if (weaponBase <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(weaponBase), weaponBase, "База урона должна быть положительной.");
            }

            double levelFactor = AlphaMath.LevelFactor(level);
            double damage = weaponBase * levelFactor;

            if ((upgrades & WeaponUpgradeFlags.Upgrade8) != 0)
            {
                damage += levelFactor * AlphaMath.StarFactor(stars) * 2.0;
            }

            if ((upgrades & WeaponUpgradeFlags.Upgrade16) != 0)
            {
                damage += levelFactor * AlphaMath.StarFactor(stars) * 8.0;
            }

            return damage;
        }
    }
}
