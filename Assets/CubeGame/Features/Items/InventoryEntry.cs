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
        private InventoryEntry(ItemInstance core, RawAlphaItemId raw, int count, AlphaItemRecord record)
        {
            Core = core;
            Raw = raw;
            Count = count;
            Record = record;
        }

        /// <summary>
        /// Full Alpha record of a raw entry (seed, variant, rarity, modifier, level). Raw entries created from a bare
        /// id carry the Alpha defaults (level 1, other fields 0). Core entries mirror their Core fields here.
        /// </summary>
        public AlphaItemRecord Record { get; }

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

            return new InventoryEntry(item, new RawAlphaItemId((byte)item.Type, item.Subtype), 1,
                new AlphaItemRecord((byte)item.Type, item.Subtype, item.Seed, 0, item.Rarity, (byte)item.Modifier, item.Level));
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

            return new InventoryEntry(null, raw, count, new AlphaItemRecord(raw.Type, raw.Subtype));
        }

        /// <summary>
        /// Entry for a generated Alpha record. Weapon/armour/jewellery (3–9) become Core items so stats, combat and
        /// the Core save keep one item model; every other type — including formulas (type 2), whose target type
        /// lives in <c>+0x08</c> and subtype in <c>+0x01</c> (0x5F6D50) — stays a raw entry with the full record.
        /// Coins must not reach the bags; route them through the wallet (<see cref="AlphaWallet"/>).
        /// </summary>
        public static InventoryEntry FromAlpha(AlphaItemRecord record, int count = 1)
        {
            if (record.IsEmpty)
            {
                throw new ArgumentException("empty Alpha record", nameof(record));
            }

            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            if (record.Type >= 3 && record.Type <= 9)
            {
                var core = new ItemInstance((ItemType)record.Type, (byte)record.Subtype, Math.Max(1, record.Level),
                    Math.Min(ItemInstance.MaxRarity, Math.Max(0, record.Rarity)), record.Seed, (ItemModifier)(byte)record.Modifier);
                return FromCore(core);
            }

            return new InventoryEntry(null, new RawAlphaItemId(record.Type, record.Subtype), count, record);
        }

        /// <summary>Copy of a raw entry with another count; the Alpha record is preserved.</summary>
        public InventoryEntry WithCount(int count)
        {
            if (IsCore)
            {
                throw new InvalidOperationException("Core entries are not stacks");
            }

            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            return new InventoryEntry(null, Raw, count, Record);
        }

        /// <summary>Alpha <c>Item_equals</c> 0x42F4A0: stacks merge only when the whole record matches.</summary>
        public bool CanStackWith(InventoryEntry other)
        {
            return other != null && !IsCore && !other.IsCore && Raw.Equals(other.Raw) && Record.Equals(other.Record);
        }

        internal void Add(int amount) => Count += amount;

        internal void Remove(int amount) => Count -= amount;

        public override string ToString() => IsCore ? Core.ToString() : $"{Record} x{Count}";
    }
}
