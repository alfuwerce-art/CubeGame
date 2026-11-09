using System;
using CubeGame.Core.Items;
using CubeGame.Features.Contracts;

namespace CubeGame.Features.Items
{
    /// <summary>
    /// One stack in the hero's bags. Item types that Core supports (2–9: formula, weapon, armour, amulet, ring)
    /// carry a Core <see cref="ItemInstance"/> — stats and combat come from Core S04 and the item is stored by the
    /// Core save. Other Alpha types (1 consumables, materials, quest items, lamp…) are kept as raw Alpha ids with a
    /// count (adapter of D86 §4: no Core enum migration before the M0-00D decision) and stored in the session file.
    ///
    /// Core type/subtype bytes ARE the Alpha numbers (the Core damage table is keyed by Alpha subtype numbers);
    /// only some Core enum names differ (CORE-ENUM-MISMATCH), so the raw id is the plain byte pair.
    /// </summary>
    public sealed class InventoryEntry
    {
        private InventoryEntry(ItemInstance core, RawAlphaItemId raw, int count)
        {
            Core = core;
            Raw = raw;
            Count = count;
        }

        /// <summary>Core item (null for raw-only Alpha types).</summary>
        public ItemInstance Core { get; }

        public RawAlphaItemId Raw { get; }

        /// <summary>Stack size (1 for Core equipment items).</summary>
        public int Count { get; private set; }

        public bool IsCore => Core != null;

        public static InventoryEntry FromCore(ItemInstance item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            return new InventoryEntry(item, new RawAlphaItemId((byte)item.Type, item.Subtype), 1);
        }

        public static InventoryEntry FromRaw(RawAlphaItemId raw, int count)
        {
            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            if (ItemInstance.IsSupportedType(raw.Type))
            {
                throw new ArgumentException($"type {raw.Type} is a Core item type; use FromCore", nameof(raw));
            }

            return new InventoryEntry(null, raw, count);
        }

        public bool CanStackWith(InventoryEntry other)
        {
            return other != null && !IsCore && !other.IsCore && Raw.Equals(other.Raw);
        }

        internal void Add(int amount) => Count += amount;

        internal void Remove(int amount) => Count -= amount;

        public override string ToString() => IsCore ? Core.ToString() : $"{Raw} x{Count}";
    }
}
