using System;
using System.Collections.Generic;
using CubeGame.Core.Equipment;
using CubeGame.Core.Items;
using CubeGame.Features.Contracts;

namespace CubeGame.Features.Items
{
    /// <summary>Alpha creature offsets for equipment outside the Core S04 loadout.</summary>
    public enum ExtraEquipmentSlot
    {
        Neck = 0x418,
        LeftRing = 0xBC0,
        RightRing = 0xCD8,
        Light = 0xDF0,
        Travel = 0xF08,
    }

    /// <summary>
    /// The hero's bags and equipment (M0-04B). Alpha layout: 4 containers × 50 slots (I-06, DIRECT).
    /// Equipment uses the Core S04 <see cref="EquipmentLoadout"/> and five identified Alpha slots (C5, I-08).
    /// Every operation either succeeds completely or
    /// refuses with a reason and changes nothing — items are never duplicated or lost.
    /// </summary>
    public sealed class HeroInventory
    {
        /// <summary>Alpha: 4 containers (I-06).</summary>
        public const int Bags = 4;

        /// <summary>Alpha: 50 slots per container (I-06, stride 0x11c).</summary>
        public const int SlotsPerBag = 50;

        public const int Capacity = Bags * SlotsPerBag;

        /// <summary>Stack limit: Alpha checks thresholds ≤ 49 (I-07, PARTIAL — the exact cap is not proven).</summary>
        public const int MaxStack = 49;

        /// <summary>Alpha weapon subtype 13 = shield (off hand).</summary>
        public const byte ShieldSubtype = 13;

        private static readonly RawAlphaItemId Lamp = new RawAlphaItemId(0x18, 0);
        private static readonly RawAlphaItemId HangGlider = new RawAlphaItemId(0x17, 0);
        private static readonly RawAlphaItemId Boat = new RawAlphaItemId(0x17, 1);

        public static IReadOnlyList<ExtraEquipmentSlot> ExtraSlots { get; } = Array.AsReadOnly(new[]
        {
            ExtraEquipmentSlot.Neck, ExtraEquipmentSlot.LeftRing, ExtraEquipmentSlot.RightRing,
            ExtraEquipmentSlot.Light, ExtraEquipmentSlot.Travel,
        });

        private readonly InventoryEntry[] _slots = new InventoryEntry[Capacity];
        private readonly EquipmentLoadout _loadout;
        private readonly Dictionary<ExtraEquipmentSlot, InventoryEntry> _extra = new Dictionary<ExtraEquipmentSlot, InventoryEntry>();

        public HeroInventory(EquipmentLoadout loadout)
        {
            _loadout = loadout ?? throw new ArgumentNullException(nameof(loadout));
        }

        public EquipmentLoadout Loadout => _loadout;

        public string LastRejection { get; private set; } = string.Empty;

        /// <summary>Raised after every successful change (slot or equipment); the session saves/visualises from it.</summary>
        public event Action Changed;

        /// <summary>Raised when an equipment slot changes: slot, new item (null = empty).</summary>
        public event Action<EquipmentSlot, ItemInstance> EquipmentChanged;

        public InventoryEntry this[int slot] => slot >= 0 && slot < Capacity ? _slots[slot] : null;

        public InventoryEntry GetExtra(ExtraEquipmentSlot slot) =>
            _extra.TryGetValue(slot, out InventoryEntry entry) ? entry : null;

        public bool HangGliderEquipped => GetExtra(ExtraEquipmentSlot.Travel)?.Raw.Equals(HangGlider) == true;

        public static bool CanEquipExtra(ExtraEquipmentSlot slot, InventoryEntry entry)
        {
            if (entry == null)
            {
                return false;
            }

            switch (slot)
            {
                case ExtraEquipmentSlot.Neck:
                    return entry.Raw.Type == (int)ItemType.Amulet;
                case ExtraEquipmentSlot.LeftRing:
                case ExtraEquipmentSlot.RightRing:
                    return entry.Raw.Type == (int)ItemType.Ring;
                case ExtraEquipmentSlot.Light:
                    return entry.Raw.Equals(Lamp);
                case ExtraEquipmentSlot.Travel:
                    return entry.Raw.Equals(HangGlider) || entry.Raw.Equals(Boat);
                default:
                    return false;
            }
        }

        /// <summary>Moves one item into an extra slot and returns the displaced item to the bags.</summary>
        public bool TryEquipExtra(int bagSlot, ExtraEquipmentSlot slot)
        {
            InventoryEntry entry = this[bagSlot];
            if (!CanEquipExtra(slot, entry))
            {
                return Reject($"bag slot {bagSlot} has no compatible item for {slot}");
            }

            InventoryEntry previous = GetExtra(slot);
            if (entry.Count == 1)
            {
                _slots[bagSlot] = previous;
                _extra[slot] = entry;
                return Done();
            }

            int free = previous != null ? FirstFree() : -1;
            if (previous != null && free < 0)
            {
                return Reject("bags are full");
            }

            _extra[slot] = InventoryEntry.FromRaw(entry.Raw, 1);
            entry.Remove(1);
            if (previous != null)
            {
                _slots[free] = previous;
            }

            return Done();
        }

        public bool TryUnequipExtra(ExtraEquipmentSlot slot)
        {
            InventoryEntry entry = GetExtra(slot);
            if (entry == null)
            {
                return Reject($"{slot} is empty");
            }

            int free = FirstFree();
            if (free < 0)
            {
                return Reject("bags are full");
            }

            _extra.Remove(slot);
            _slots[free] = entry;
            return Done();
        }

        internal bool TryEquipExtraDirect(ExtraEquipmentSlot slot, InventoryEntry entry)
        {
            if (!CanEquipExtra(slot, entry) || entry.Count != 1 || _extra.ContainsKey(slot))
            {
                return Reject($"cannot equip {entry} into {slot}");
            }

            _extra.Add(slot, entry);
            return Done();
        }

        // Existing negative placement codes -1..-6 are the Core slot indices. Extra slots encode Alpha offsets.
        internal static int ExtraPlacement(ExtraEquipmentSlot slot) => -1 - (int)slot;

        internal bool TryRestoreExtra(int placement, InventoryEntry entry) =>
            placement < 0 && TryEquipExtraDirect((ExtraEquipmentSlot)(-(placement + 1)), entry);

        public int UsedSlots
        {
            get
            {
                int used = 0;
                foreach (InventoryEntry entry in _slots)
                {
                    if (entry != null)
                    {
                        used++;
                    }
                }

                return used;
            }
        }

        /// <summary>Puts an entry into the bags: raw stacks merge first, then the first free slot. Refuses when full.</summary>
        public bool TryAdd(InventoryEntry entry)
        {
            if (entry == null)
            {
                return Reject("no item");
            }

            if (!entry.IsCore)
            {
                int remaining = entry.Count;
                for (int i = 0; i < Capacity && remaining > 0; i++)
                {
                    if (_slots[i] != null && _slots[i].CanStackWith(entry) && _slots[i].Count < MaxStack)
                    {
                        remaining -= Math.Min(remaining, MaxStack - _slots[i].Count);
                    }
                }

                int freeNeeded = (remaining + MaxStack - 1) / MaxStack;
                if (FreeSlots() < freeNeeded)
                {
                    return Reject("bags are full");
                }

                remaining = entry.Count;
                for (int i = 0; i < Capacity && remaining > 0; i++)
                {
                    if (_slots[i] != null && _slots[i].CanStackWith(entry) && _slots[i].Count < MaxStack)
                    {
                        int moved = Math.Min(remaining, MaxStack - _slots[i].Count);
                        _slots[i].Add(moved);
                        remaining -= moved;
                    }
                }

                for (int i = 0; i < Capacity && remaining > 0; i++)
                {
                    if (_slots[i] == null)
                    {
                        int moved = Math.Min(remaining, MaxStack);
                        _slots[i] = InventoryEntry.FromRaw(entry.Raw, moved);
                        remaining -= moved;
                    }
                }

                return Done();
            }

            int free = FirstFree();
            if (free < 0)
            {
                return Reject("bags are full");
            }

            _slots[free] = entry;
            return Done();
        }

        /// <summary>Places an entry into an exact slot (save restore); refuses an occupied or invalid slot.</summary>
        public bool TryPlace(int slot, InventoryEntry entry)
        {
            if (entry == null || slot < 0 || slot >= Capacity || _slots[slot] != null)
            {
                return Reject($"slot {slot} is not free");
            }

            _slots[slot] = entry;
            return Done();
        }

        /// <summary>Removes <paramref name="amount"/> from a slot (whole entry for Core items) and returns what was removed.</summary>
        public InventoryEntry TakeFrom(int slot, int amount = int.MaxValue)
        {
            InventoryEntry entry = this[slot];
            if (entry == null)
            {
                Reject($"slot {slot} is empty");
                return null;
            }

            if (entry.IsCore || amount >= entry.Count)
            {
                _slots[slot] = null;
                Done();
                return entry;
            }

            if (amount <= 0)
            {
                Reject("nothing to take");
                return null;
            }

            entry.Remove(amount);
            Done();
            return InventoryEntry.FromRaw(entry.Raw, amount);
        }

        public bool TryBrewLifePotion()
        {
            int flower = FindFirst(AlphaLifePotionRecipe.Heartflower);
            int flask = FindFirst(AlphaLifePotionRecipe.WaterFlask);
            if (flower < 0 || flask < 0)
            {
                return Reject("a Life Potion needs one Heartflower and one Water Flask");
            }

            var potion = InventoryEntry.FromRaw(LootRules.LifePotion, 1);
            // If the output fits now, reserve it before removing ingredients. With full bags it
            // can also fit in a slot freed by consuming the last ingredient in a stack.
            if (!TryAdd(potion))
            {
                if (_slots[flower].Count > 1 && _slots[flask].Count > 1)
                {
                    return false;
                }

                TakeFrom(flower, 1);
                TakeFrom(flask, 1);
                return TryAdd(potion);
            }

            TakeFrom(flower, 1);
            TakeFrom(flask, 1);
            return true;
        }

        /// <summary>
        /// Equips the Core item of a bag slot into the first compatible equipment slot (weapons: main hand, then off
        /// hand when the main hand is taken). The previously equipped item returns to the same bag slot.
        /// </summary>
        public bool TryEquip(int bagSlot, out EquipmentSlot equippedTo)
        {
            equippedTo = EquipmentSlot.MainHand;
            InventoryEntry entry = this[bagSlot];
            if (entry == null)
            {
                return Reject($"slot {bagSlot} is empty");
            }

            if (!entry.IsCore)
            {
                return Reject($"{entry.Raw} cannot be equipped (not an equipment type in Core)");
            }

            if (!TryChooseSlot(entry.Core.Type, out equippedTo))
            {
                return Reject($"no equipment slot for {entry.Core.Type} (I-08: Alpha slot not in Core yet)");
            }

            if (entry.Core.Type == ItemType.Weapon && entry.Core.Subtype == ShieldSubtype)
            {
                equippedTo = EquipmentSlot.OffHand;
            }

            ItemInstance previous = _loadout.Equip(equippedTo, entry.Core);
            _slots[bagSlot] = previous != null ? InventoryEntry.FromCore(previous) : null;
            EquipmentChanged?.Invoke(equippedTo, entry.Core);
            return Done();
        }

        /// <summary>Moves an equipped item back into the first free bag slot; refuses when the bags are full.</summary>
        public bool TryUnequip(EquipmentSlot slot)
        {
            ItemInstance item = _loadout.GetEquipped(slot);
            if (item == null)
            {
                return Reject($"{slot} is empty");
            }

            int free = FirstFree();
            if (free < 0)
            {
                return Reject("bags are full");
            }

            _loadout.Unequip(slot);
            _slots[free] = InventoryEntry.FromCore(item);
            EquipmentChanged?.Invoke(slot, null);
            return Done();
        }

        /// <summary>Equips a Core item directly (save restore / starting kit) without touching the bags.</summary>
        public bool TryEquipDirect(EquipmentSlot slot, ItemInstance item)
        {
            if (item == null || !EquipmentLoadout.IsSlotCompatible(slot, item.Type) || _loadout.GetEquipped(slot) != null)
            {
                return Reject($"cannot equip {item} into {slot}");
            }

            _loadout.Equip(slot, item);
            EquipmentChanged?.Invoke(slot, item);
            return Done();
        }

        /// <summary>All Core items the hero owns (bags + equipment) — the list the Core save v1 stores.</summary>
        public List<ItemInstance> AllCoreItems()
        {
            var items = new List<ItemInstance>();
            foreach (EquipmentSlot slot in EquipmentLoadout.Slots)
            {
                ItemInstance equipped = _loadout.GetEquipped(slot);
                if (equipped != null)
                {
                    items.Add(equipped);
                }
            }

            foreach (ExtraEquipmentSlot slot in ExtraSlots)
            {
                InventoryEntry entry = GetExtra(slot);
                if (entry != null && entry.IsCore)
                {
                    items.Add(entry.Core);
                }
            }

            foreach (InventoryEntry entry in _slots)
            {
                if (entry != null && entry.IsCore)
                {
                    items.Add(entry.Core);
                }
            }

            return items;
        }

        public int CountOf(RawAlphaItemId raw)
        {
            int count = 0;
            foreach (InventoryEntry entry in _slots)
            {
                if (entry != null && entry.Raw.Equals(raw))
                {
                    count += entry.Count;
                }
            }

            return count;
        }

        public int FindFirst(RawAlphaItemId raw)
        {
            for (int i = 0; i < Capacity; i++)
            {
                if (_slots[i] != null && _slots[i].Raw.Equals(raw))
                {
                    return i;
                }
            }

            return -1;
        }

        public void Clear()
        {
            Array.Clear(_slots, 0, _slots.Length);
            _extra.Clear();
            foreach (EquipmentSlot slot in EquipmentLoadout.Slots)
            {
                _loadout.Unequip(slot);
            }
        }

        private bool TryChooseSlot(ItemType type, out EquipmentSlot slot)
        {
            slot = EquipmentSlot.MainHand;
            if (type == ItemType.Weapon)
            {
                // Shield (Alpha subtype 13) goes to the off hand, every other weapon swaps the main hand.
                // Dual-wield / two-handed blocking rules of Alpha are not restored: PLACEHOLDER_NOT_ALPHA.
                slot = EquipmentSlot.MainHand;
                return true;
            }

            foreach (EquipmentSlot candidate in EquipmentLoadout.Slots)
            {
                if (EquipmentLoadout.IsSlotCompatible(candidate, type))
                {
                    slot = candidate;
                    return true;
                }
            }

            return false;
        }

        private int FirstFree()
        {
            for (int i = 0; i < Capacity; i++)
            {
                if (_slots[i] == null)
                {
                    return i;
                }
            }

            return -1;
        }

        private int FreeSlots()
        {
            int free = 0;
            foreach (InventoryEntry entry in _slots)
            {
                if (entry == null)
                {
                    free++;
                }
            }

            return free;
        }

        private bool Done()
        {
            LastRejection = string.Empty;
            Changed?.Invoke();
            return true;
        }

        private bool Reject(string reason)
        {
            LastRejection = reason;
            return false;
        }
    }
}
