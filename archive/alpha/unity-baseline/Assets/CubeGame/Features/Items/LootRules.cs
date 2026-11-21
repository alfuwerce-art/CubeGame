using System;
using System.Collections.Generic;
using CubeGame.Core.Items;
using CubeGame.Features.Contracts;

namespace CubeGame.Features.Items
{
    /// <summary>
    /// Creature loot (M0-04C first slice). Alpha's loot tables (<c>loot_generateItemTable</c> 0x5C8390,
    /// <c>drop_getCountRange1</c> 0x443F40) are not restored (I-12, OQ-11 OPEN), so WHAT drops is a CubeGame
    /// placeholder; the rarity of a dropped Core item uses the restored Alpha rule
    /// <see cref="ItemStatsCalculator.RollRarity"/> (1 % / 0.1 % / 0.01 % steps, I-03 DIRECT).
    /// Money is never dropped by kills (Alpha I-16: money only from selling).
    /// </summary>
    public static class LootRules
    {
        public const string Provenance = "PLACEHOLDER_NOT_ALPHA (I-12 loot tables OQ-11); rarity by Alpha I-03";

        /// <summary>Alpha life potion (type 1, subtype 1) — a consumable Core does not model.</summary>
        public static readonly RawAlphaItemId LifePotion = new RawAlphaItemId(1, 1);

        private static readonly (ItemType Type, byte Subtype)[] Equipment =
        {
            (ItemType.Weapon, 0), (ItemType.Weapon, 1), (ItemType.Weapon, 2), (ItemType.Weapon, 5),
            (ItemType.Weapon, 15), (ItemType.Weapon, 16), (ItemType.Weapon, 17),
            (ItemType.Chest, 0), (ItemType.Gloves, 0), (ItemType.Boots, 0), (ItemType.Shoulders, 0),
        };

        /// <summary>
        /// Deterministic drop of one killed creature: one equipment piece of the creature's level (placeholder
        /// choice) with an Alpha rarity roll, plus a life potion with probability 1/4 (placeholder).
        /// </summary>
        public static List<InventoryEntry> Roll(int level, int seed)
        {
            var random = new Random(seed);
            var drop = new List<InventoryEntry>();
            (ItemType type, byte subtype) = Equipment[random.Next(Equipment.Length)];
            int rarity = ItemStatsCalculator.RollRarity(random.Next(100) == 0, random.Next(1000) == 0, random.Next(10000) == 0);
            var item = new ItemInstance(type, subtype, Math.Max(1, level), rarity, random.Next(1, int.MaxValue));
            drop.Add(InventoryEntry.FromCore(item));
            if (random.Next(4) == 0)
            {
                drop.Add(InventoryEntry.FromRaw(LifePotion, 1));
            }

            return drop;
        }
    }
}
