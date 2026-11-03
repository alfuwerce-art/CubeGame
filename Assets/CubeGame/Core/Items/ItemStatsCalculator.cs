using System;

namespace CubeGame.Core.Items
{
    /// <summary>
    /// Единая формула статов предмета Alpha (F_items §F2, REFERENCE_VERIFIED).
    ///
    /// <para>
    /// <c>stat = 2^((1 - 1/((L-1)*0.05+1))*3) · 2^(rarity*0.25) · M_type · M_modifier</c>,
    /// где <c>L = level + 0.1·powerBonus</c>. Первый множитель — <c>levelFactor</c>, второй —
    /// <c>starFactor</c>: предметы используют ту же шкалу прогрессии, что персонажи.
    /// </para>
    /// <para>
    /// Эталон: docs/research/alpha2013/tools/alpha_item_stats_reference.py --check (PASS, exit 0).
    /// Числа НЕ придуманы: каждая таблица взята из спецификации; неизвестные ветви (например,
    /// полный смысл модификаторов, OQ-09) возвращают нейтральный множитель и помечены в коде.
    /// </para>
    /// </summary>
    public static class ItemStatsCalculator
    {
        /// <summary>Множитель урона для sword/dagger/shield (Alpha <c>item_weaponDamage</c>).</summary>
        public const double WeaponDamageMultiplierSmall = 2.0;

        /// <summary>Множитель урона для longsword и «обычного» оружия.</summary>
        public const double WeaponDamageMultiplierRegular = 4.0;

        /// <summary>Множитель урона для «спецоружия» (двуручное/дальнобойное).</summary>
        public const double WeaponDamageMultiplierSpecial = 8.0;

        /// <summary>Уровень предмета в формулах: <c>level + 0.1*powerBonus</c> (Alpha <c>00445f60</c>).</summary>
        public static double EffectiveLevel(int level, int powerBonus)
        {
            if (level < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(level), level, "Уровень предмета начинается с 1.");
            }

            if (powerBonus < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(powerBonus), powerBonus, "powerBonus не может быть отрицательным.");
            }

            return level + powerBonus * 0.1;
        }

        /// <summary><c>math_pow2Mul</c> (Alpha <c>00445f60</c>): <c>levelFactor(L) · starFactor(rarity)</c>.</summary>
        public static double MathPow2Mul(double effectiveLevel, int rarity)
        {
            return Progression.AlphaMath.LevelFactor(effectiveLevel)
                   * Progression.AlphaMath.StarFactor(rarity);
        }

        /// <summary>
        /// Урон оружия (Alpha <c>item_weaponDamage</c> <c>004c7f60</c>):
        /// subtype 3/4/0x0D → ×2; 5 → ×4; «спецоружие» → ×8; иначе ×4.
        /// </summary>
        public static double WeaponDamage(int level, int rarity, WeaponSubtype subtype, int powerBonus = 0)
        {
            double baseValue = MathPow2Mul(EffectiveLevel(level, powerBonus), rarity);

            switch (subtype)
            {
                case WeaponSubtype.Sword:
                case WeaponSubtype.Dagger:
                case WeaponSubtype.Shield:
                    return baseValue * WeaponDamageMultiplierSmall;
                case WeaponSubtype.Longsword:
                    return baseValue * WeaponDamageMultiplierRegular;
                default:
                    return IsSpecialWeapon(subtype)
                        ? baseValue * WeaponDamageMultiplierSpecial
                        : baseValue * WeaponDamageMultiplierRegular;
            }
        }

        /// <summary>
        /// Броня (Alpha <c>item_computeStat_armor</c> <c>004c70b0</c>): только нагрудник/обувь/перчатки/наплечники.
        /// <c>base · 5 · rarityFactor · materialMult</c>, где rarityFactor 1.0 у нагрудника и 0.5 у остальных,
        /// а «материальный» множитель <c>1 + (1 - (seed*8 mod 21)/20)</c> принимает 21 значение в диапазоне
        /// <c>1.0…2.0</c> (см. <see cref="SeedMaterialMultiplier"/>; верхняя граница 2.0 — при seed, кратном 21).
        /// </summary>
        public static double Armor(int level, int rarity, ItemType type, ItemModifier modifier = ItemModifier.None,
            int seed = 0, int powerBonus = 0)
        {
            if (!IsArmorType(type))
            {
                return 0.0;
            }

            double rarityFactor = type == ItemType.Chest ? 1.0 : 0.5;
            double materialMultiplier = SeedMaterialMultiplier(seed);

            switch (modifier)
            {
                case ItemModifier.M01:
                    materialMultiplier += 1.0;
                    break;
                case ItemModifier.M1A:
                    materialMultiplier += 0.5;
                    break;
                case ItemModifier.M1B:
                    materialMultiplier += 0.75;
                    break;
            }

            double baseValue = MathPow2Mul(EffectiveLevel(level, powerBonus), rarity);
            return baseValue * 5.0 * rarityFactor * materialMultiplier;
        }

        /// <summary>
        /// Индекс «материала» брони: <c>(seed * 8) mod 21</c> в диапазоне <c>0…20</c>.
        ///
        /// <para>
        /// Арифметика: расширение до <c>long</c> выполняется ДО умножения, поэтому большие и
        /// отрицательные seed не теряют данные из-за переполнения <c>int</c>. Остаток приводится
        /// к неотрицательному значению (floor-mod), как в эталонном Python-скрипте
        /// (<c>(seed &lt;&lt; 3) % 0x15</c>), где <c>%</c> всегда даёт неотрицательный результат.
        /// </para>
        /// <para>
        /// СТАТУС: <c>PARTIAL</c>. Точная разрядность операции в Alpha для отрицательных/огромных seed
        /// не доказана (возможна 32-битная арифметика с переполнением или знакопеременный остаток).
        /// Здесь воспроизводится единственный воспроизводимый эталон — Python-скрипт S00-R02;
        /// заменять его «более удобной» версией без доказательств запрещено (см. S02_REPORT REVIEW-FIX-01).
        /// </para>
        /// </summary>
        public static int SeedMaterialIndex(int seed)
        {
            long scaled = (long)seed * 8L;
            long remainder = scaled % 21L;
            if (remainder < 0L)
            {
                remainder += 21L;
            }

            return (int)remainder;
        }

        /// <summary>
        /// «Материальный» множитель брони <c>1 + (1 - index/20)</c>: диапазон <c>1.0…2.0</c>
        /// (индекс 0 → 2.0, индекс 20 → 1.0). Это НЕ «±5 %»: формулировка в F_items исправлена в REVIEW-FIX-01.
        /// </summary>
        public static double SeedMaterialMultiplier(int seed)
        {
            return 1.0 + (1.0 - SeedMaterialIndex(seed) / 20.0);
        }

        /// <summary>
        /// Блок (Alpha <c>item_computeBlockValue</c> <c>004c6a90</c>): только 4/5/6/7.
        /// </summary>
        public static double BlockValue(int level, int rarity, ItemType type, ItemModifier modifier = ItemModifier.None,
            int powerBonus = 0)
        {
            if (!IsArmorType(type))
            {
                return 0.0;
            }

            double multiplier = type == ItemType.Chest ? 1.0 : 0.5;

            switch (modifier)
            {
                case ItemModifier.M12:
                    multiplier *= 0.8;
                    break;
                case ItemModifier.M13:
                case ItemModifier.M1A:
                case ItemModifier.M1B:
                    multiplier *= 0.85;
                    break;
                case ItemModifier.M17:
                case ItemModifier.M19:
                    multiplier *= 0.75;
                    break;
            }

            return MathPow2Mul(EffectiveLevel(level, powerBonus), rarity) * multiplier;
        }

        /// <summary>
        /// Стоимость предмета (Alpha <c>item_valueByType</c> <c>004c76e0</c>), минимум 1.
        /// </summary>
        public static int Value(int level, int rarity, ItemType type, int powerBonus = 0)
        {
            double multiplier;
            switch (type)
            {
                case ItemType.Weapon:
                case ItemType.Chest:
                    multiplier = 2.0;
                    break;
                case ItemType.Boots:
                case ItemType.Gloves:
                case ItemType.Ring:
                    multiplier = 1.0;
                    break;
                case ItemType.Shoulders:
                case ItemType.Amulet:
                    multiplier = 1.5;
                    break;
                default:
                    multiplier = 0.2;
                    break;
            }

            int value = (int)(MathPow2Mul(EffectiveLevel(level, powerBonus), rarity) * 10.0 * multiplier);
            return Math.Max(1, value);
        }

        /// <summary>
        /// Бросок редкости (F_items §F3): 1 % → +1, 0.1 % → +1, 0.01 % → +1, итог клампится в 4.
        /// Модель та же, что у звёзд мобов.
        /// </summary>
        public static int RollRarity(bool oneIn100, bool oneIn1000, bool oneIn10000)
        {
            int rarity = (oneIn100 ? 1 : 0) + (oneIn1000 ? 1 : 0) + (oneIn10000 ? 1 : 0);
            return Math.Min(rarity, ItemInstance.MaxRarity);
        }

        /// <summary>Арморные типы Alpha: нагрудник, обувь, перчатки, наплечники.</summary>
        public static bool IsArmorType(ItemType type)
        {
            return type == ItemType.Chest || type == ItemType.Boots || type == ItemType.Gloves || type == ItemType.Shoulders;
        }

        /// <summary>
        /// «Спецоружие» Alpha (<c>weapon_isSpecialRune</c>): двуручное и дальнобойное.
        /// Подтипы 6 и 7 входят в группу, но их имена не восстановлены (UNKNOWN).
        /// </summary>
        public static bool IsSpecialWeapon(WeaponSubtype subtype)
        {
            switch (subtype)
            {
                case WeaponSubtype.Longsword:
                case WeaponSubtype.Reserved06:
                case WeaponSubtype.Reserved07:
                case WeaponSubtype.Crossbow:
                case WeaponSubtype.Staff:
                case WeaponSubtype.Wand:
                case WeaponSubtype.Bracelet:
                case WeaponSubtype.Boomerang:
                case WeaponSubtype.Greatsword:
                case WeaponSubtype.Greataxe:
                case WeaponSubtype.Greatmace:
                    return true;
                default:
                    return false;
            }
        }
    }
}
