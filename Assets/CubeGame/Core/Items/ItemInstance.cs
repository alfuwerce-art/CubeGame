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
            ValidateTypeAndSubtype(type, subtype);

            if (level < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(level), level, "Уровень предмета начинается с 1.");
            }

            if (rarity < 0 || rarity > MaxRarity)
            {
                throw new ArgumentOutOfRangeException(nameof(rarity), rarity, "Редкость предмета: 0…4 (Alpha кламп 4).");
            }

            if (powerBonus < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(powerBonus), powerBonus, "powerBonus не может быть отрицательным.");
            }

            if (stackCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(stackCount), stackCount, "Размер стека не меньше 1.");
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

        private static void ValidateTypeAndSubtype(ItemType type, byte subtype)
        {
            switch (type)
            {
                case ItemType.Weapon:
                    if (subtype > (byte)WeaponSubtype.Torch || subtype == 0)
                    {
                        throw new ArgumentOutOfRangeException(nameof(subtype), subtype, "Подтип оружия вне известного диапазона (1…0x14).");
                    }

                    break;
                case ItemType.Formula:
                case ItemType.Chest:
                case ItemType.Boots:
                case ItemType.Gloves:
                case ItemType.Shoulders:
                case ItemType.Amulet:
                case ItemType.Ring:
                    if (subtype != 0)
                    {
                        throw new ArgumentOutOfRangeException(nameof(subtype), subtype, "Для этого типа подтип не используется.");
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, "Тип предмета вне модели S02.");
            }
        }
    }
}
