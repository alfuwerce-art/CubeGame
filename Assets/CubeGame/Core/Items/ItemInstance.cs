using System;

namespace CubeGame.Core.Items
{
    /// <summary>
    /// Экземпляр предмета: «данные + seed», а не ссылка на шаблон.
    ///
    /// Модель Alpha (F_items §F1, DIRECT по структуре): тип, подтип, seed варианта, редкость 0…4,
    /// модификатор/материал, уровень, «powerBonus» (число элементов полезной нагрузки), размер стека.
    /// <c>Item_equals</c> в Alpha сравнивает ровно этот набор — поэтому идентичность предмета
    /// включает seed и модификатор.
    ///
    /// Ограничение S02: это фундамент данных. Крафт, лут-таблицы и инвентарный интерфейс не реализуются.
    /// </summary>
    public sealed class ItemInstance : IEquatable<ItemInstance>
    {
        /// <summary>Максимальная редкость предмета (Alpha: кламп в 4).</summary>
        public const int MaxRarity = 4;

        public ItemInstance(
            ItemType type,
            byte subtype,
            int level,
            int rarity,
            int seed,
            ItemModifier modifier = ItemModifier.None,
            int powerBonus = 0,
            int stackCount = 1)
        {
            if (!TryValidateValues(type, subtype, level, rarity, powerBonus, stackCount, out string error))
            {
                throw new ArgumentOutOfRangeException(nameof(type), $"Некорректные данные предмета: {error}.");
            }

            Type = type;
            Subtype = subtype;
            Level = level;
            Rarity = rarity;
            Seed = seed;
            Modifier = modifier;
            PowerBonus = powerBonus;
            StackCount = stackCount;
        }

        /// <summary>Тип предмета (Alpha <c>+0x00</c>).</summary>
        public ItemType Type { get; }

        /// <summary>Подтип (для оружия — <see cref="WeaponSubtype"/>, иначе 0).</summary>
        public byte Subtype { get; }

        /// <summary>Уровень предмета (Alpha <c>+0x10</c>, i16).</summary>
        public int Level { get; }

        /// <summary>Редкость/звёзды предмета 0…4 (Alpha <c>+0x0c</c>).</summary>
        public int Rarity { get; }

        /// <summary>Seed варианта: влияет на материал/цвет/название (Alpha <c>+0x04</c>).</summary>
        public int Seed { get; }

        /// <summary>Модификатор/материал (Alpha <c>+0x0d</c>).</summary>
        public ItemModifier Modifier { get; }

        /// <summary>«powerBonus» — число элементов полезной нагрузки (Alpha <c>+0x114</c>).</summary>
        public int PowerBonus { get; }

        /// <summary>Количество в стеке (Alpha <c>+0x118</c>).</summary>
        public int StackCount { get; }

        /// <summary>Уровень предмета в формулах: <c>level + 0.1·powerBonus</c> (Alpha <c>00445f60</c>).</summary>
        public double EffectiveLevel => ItemStatsCalculator.EffectiveLevel(Level, PowerBonus);

        public bool Equals(ItemInstance other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Type == other.Type
                   && Subtype == other.Subtype
                   && Level == other.Level
                   && Rarity == other.Rarity
                   && Seed == other.Seed
                   && Modifier == other.Modifier
                   && PowerBonus == other.PowerBonus
                   && StackCount == other.StackCount;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ItemInstance);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Type;
                hash = (hash * 397) ^ Subtype;
                hash = (hash * 397) ^ Level;
                hash = (hash * 397) ^ Rarity;
                hash = (hash * 397) ^ Seed;
                hash = (hash * 397) ^ (int)Modifier;
                hash = (hash * 397) ^ PowerBonus;
                hash = (hash * 397) ^ StackCount;
                return hash;
            }
        }

        public override string ToString()
        {
            return $"{Type}/{Subtype} L{Level} r{Rarity} seed={Seed} mod={(byte)Modifier:X2} power={PowerBonus} x{StackCount}";
        }

        /// <summary>
        /// Создаёт предмет из «сырых» целочисленных полей сохранения с полной проверкой.
        ///
        /// Зачем отдельный вход: в файле сохранения поля хранятся как <c>int</c>, и простое
        /// приведение <c>int → byte</c> теряет данные (например, subtype = 256 превращается в 0).
        /// Здесь проверяются тип, диапазон подтипа, модификатор и значения, поэтому предмет либо
        /// создан корректно, либо возвращается диагностическая ошибка без исключения.
        ///
        /// Границы проверок разделены на два вида (см. S02_REPORT, REVIEW-FIX-01):
        /// достоверные ограничения Alpha (редкость 0…4, уровень с 1, подтип только у оружия)
        /// и ограничения текущей модели CubeGame (известный набор типов/модификаторов, подтип
        /// оружия 1…0x14 — вне этой таблицы имена в Alpha отсутствуют, OQ-09).
        /// </summary>
        public static bool TryCreateFromIdentifiers(
            int typeId,
            int subtypeId,
            int level,
            int rarity,
            int seed,
            int modifierId,
            int powerBonus,
            int stackCount,
            out ItemInstance item,
            out string error)
        {
            item = null;

            if (typeId < byte.MinValue || typeId > byte.MaxValue || !IsSupportedType(typeId))
            {
                error = $"неизвестный тип предмета {typeId}";
                return false;
            }

            if (subtypeId < byte.MinValue || subtypeId > byte.MaxValue)
            {
                error = $"подтип {subtypeId} вне диапазона байта (0…255) — приведение потеряло бы данные";
                return false;
            }

            if (modifierId < byte.MinValue || modifierId > byte.MaxValue || !IsSupportedModifier(modifierId))
            {
                error = $"неизвестный модификатор {modifierId}";
                return false;
            }

            var type = (ItemType)(byte)typeId;
            var subtype = (byte)subtypeId;
            var modifier = (ItemModifier)(byte)modifierId;

            if (!TryValidateValues(type, subtype, level, rarity, powerBonus, stackCount, out error))
            {
                return false;
            }

            item = new ItemInstance(type, subtype, level, rarity, seed, modifier, powerBonus, stackCount);
            error = string.Empty;
            return true;
        }

        /// <summary>Поддерживается ли тип предмета текущей моделью CubeGame.</summary>
        public static bool IsSupportedType(int typeId)
        {
            switch (typeId)
            {
                case (int)ItemType.Formula:
                case (int)ItemType.Weapon:
                case (int)ItemType.Chest:
                case (int)ItemType.Boots:
                case (int)ItemType.Gloves:
                case (int)ItemType.Shoulders:
                case (int)ItemType.Amulet:
                case (int)ItemType.Ring:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Поддерживается ли модификатор текущей моделью CubeGame (смысл значений — OQ-09).</summary>
        public static bool IsSupportedModifier(int modifierId)
        {
            switch (modifierId)
            {
                case (int)ItemModifier.None:
                case (int)ItemModifier.M01:
                case (int)ItemModifier.M0B:
                case (int)ItemModifier.M0C:
                case (int)ItemModifier.M12:
                case (int)ItemModifier.M13:
                case (int)ItemModifier.M17:
                case (int)ItemModifier.M19:
                case (int)ItemModifier.M1A:
                case (int)ItemModifier.M1B:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Проверка значений без исключения (используется валидатором сохранений).</summary>
        public static bool TryValidateValues(
            ItemType type,
            byte subtype,
            int level,
            int rarity,
            int powerBonus,
            int stackCount,
            out string error)
        {
            if (!IsSupportedType((int)type))
            {
                error = $"неизвестный тип предмета {(int)type}";
                return false;
            }

            if (type == ItemType.Weapon)
            {
                // Границы взяты из таблицы подтипов Alpha (1…0x14); имена вне неё не восстановлены.
                if (subtype < (byte)WeaponSubtype.Mace || subtype > (byte)WeaponSubtype.Torch)
                {
                    error = $"подтип оружия {subtype} вне известного диапазона 1…0x14";
                    return false;
                }
            }
            else if (subtype != 0)
            {
                error = $"для типа {(int)type} подтип должен быть 0, получено {subtype}";
                return false;
            }

            if (level < 1)
            {
                error = $"уровень предмета {level} меньше 1";
                return false;
            }

            if (rarity < 0 || rarity > MaxRarity)
            {
                error = $"редкость {rarity} вне диапазона 0…{MaxRarity}";
                return false;
            }

            if (powerBonus < 0)
            {
                error = $"отрицательный powerBonus {powerBonus}";
                return false;
            }

            if (stackCount < 1)
            {
                error = $"размер стека {stackCount} меньше 1";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
