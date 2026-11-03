using System;
using System.Collections.Generic;
using CubeGame.Core.Items;

namespace CubeGame.Core.Equipment
{
    /// <summary>
    /// Чистая модель экипировки: слот → экземпляр предмета.
    ///
    /// Правила (собственная модель CubeGame, S04):
    /// <list type="bullet">
    /// <item>совместимость слота и типа предмета проверяется явной таблицей;</item>
    /// <item>один и тот же экземпляр нельзя надеть в два слота;</item>
    /// <item>операция либо выполняется полностью, либо не меняет состояние (проверки до записи);</item>
    /// <item>идентификаторы предмета уже проверены моделью <see cref="ItemInstance"/> — здесь они не дублируются.</item>
    /// </list>
    ///
    /// ЧЕГО ЗДЕСЬ НЕТ (правила не утверждены, E_combat/F_items их не доказывают):
    /// двуручное оружие, сочетания «оружие + щит», классовые ограничения и выбор атакующей руки
    /// не дают автоматического эффекта. Два оружия могут быть надеты одновременно как данные,
    /// без игрового вывода из этого сочетания (см. S04_REPORT, открытый вопрос S04-OQ-01).
    /// </summary>
    public sealed class EquipmentLoadout
    {
        private static readonly EquipmentSlot[] AllSlots =
        {
            EquipmentSlot.MainHand,
            EquipmentSlot.OffHand,
            EquipmentSlot.Chest,
            EquipmentSlot.Gloves,
            EquipmentSlot.Boots,
            EquipmentSlot.Shoulders,
        };

        private readonly ItemInstance[] _slots = new ItemInstance[AllSlots.Length];

        /// <summary>Все поддерживаемые слоты (фиксированный порядок).</summary>
        public static IReadOnlyList<EquipmentSlot> Slots => AllSlots;

        /// <summary>Совместим ли тип предмета со слотом.</summary>
        public static bool IsSlotCompatible(EquipmentSlot slot, ItemType itemType)
        {
            switch (slot)
            {
                case EquipmentSlot.MainHand:
                case EquipmentSlot.OffHand:
                    return itemType == ItemType.Weapon;
                case EquipmentSlot.Chest:
                    return itemType == ItemType.Chest;
                case EquipmentSlot.Gloves:
                    return itemType == ItemType.Gloves;
                case EquipmentSlot.Boots:
                    return itemType == ItemType.Boots;
                case EquipmentSlot.Shoulders:
                    return itemType == ItemType.Shoulders;
                default:
                    return false;
            }
        }

        /// <summary>Предмет в слоте или <c>null</c>, если слот пуст.</summary>
        public ItemInstance GetEquipped(EquipmentSlot slot)
        {
            return _slots[Index(slot)];
        }

        public bool IsSlotEmpty(EquipmentSlot slot)
        {
            return GetEquipped(slot) == null;
        }

        /// <summary>Число занятых слотов.</summary>
        public int EquippedCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _slots.Length; i++)
                {
                    if (_slots[i] != null)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>Надетые предметы (снимок, без пустых слотов).</summary>
        public List<ItemInstance> GetEquippedItems()
        {
            var items = new List<ItemInstance>(_slots.Length);
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] != null)
                {
                    items.Add(_slots[i]);
                }
            }

            return items;
        }

        /// <summary>
        /// Надевает предмет в слот и возвращает ранее надетый предмет (или <c>null</c>).
        /// При любой ошибке состояние экипировки не меняется.
        /// </summary>
        public ItemInstance Equip(EquipmentSlot slot, ItemInstance item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            int index = Index(slot);

            if (!IsSlotCompatible(slot, item.Type))
            {
                throw new ArgumentException(
                    $"Предмет типа {item.Type} несовместим со слотом {slot}.", nameof(item));
            }

            if (ContainsInOtherSlot(item, index))
            {
                throw new InvalidOperationException(
                    "Этот экземпляр предмета уже надет в другой слот; дублирование запрещено.");
            }

            ItemInstance previous = _slots[index];
            _slots[index] = item;
            return previous;
        }

        /// <summary>Снимает предмет из слота и возвращает его (или <c>null</c>, если слот был пуст).</summary>
        public ItemInstance Unequip(EquipmentSlot slot)
        {
            int index = Index(slot);
            ItemInstance previous = _slots[index];
            _slots[index] = null;
            return previous;
        }

        /// <summary>Надет ли этот экземпляр (сравнение по ссылке).</summary>
        public bool Contains(ItemInstance item)
        {
            if (item == null)
            {
                return false;
            }

            for (int i = 0; i < _slots.Length; i++)
            {
                if (ReferenceEquals(_slots[i], item))
                {
                    return true;
                }
            }

            return false;
        }

        private bool ContainsInOtherSlot(ItemInstance item, int targetIndex)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (i != targetIndex && ReferenceEquals(_slots[i], item))
                {
                    return true;
                }
            }

            return false;
        }

        private static int Index(EquipmentSlot slot)
        {
            if (!Enum.IsDefined(typeof(EquipmentSlot), slot))
            {
                throw new ArgumentOutOfRangeException(nameof(slot), slot, "Неизвестный слот экипировки.");
            }

            return (int)slot;
        }
    }
}
