using System;
using System.Collections.Generic;
using CubeGame.Features.World;

namespace CubeGame.Features.Items
{
    /// <summary>
    /// Alpha 2013 town vendors: stock generators, trade and service rules (ALPHA_DECOMPILED with byte-checked
    /// call sites). The town constructor <c>Chunk_generateObjects</c> writes the vendor role into Spawn <c>+0x30</c>
    /// and fills the vendor inventory once: 0x80 → <c>0x5C9B30</c>, 0x81 and 0x83 → <c>0x5CA0A0</c>,
    /// 0x82 → <c>0x5C8390</c> (calls at 0x5BCE53, 0x5BD20B, 0x5BD3E7, 0x5BD02F); 0x84 (innkeeper) has no stock.
    /// The vendor tab shows the whole vendor inventory (<c>0x4A2300</c> copies <c>+0x11DC</c>), and buying copies the
    /// item without removing it, so stock is unlimited.
    /// </summary>
    public static class AlphaShop
    {
        public const int ItemVendor = 0x80;
        public const int WeaponVendor = 0x81;
        public const int ArmorVendor = 0x82;
        public const int WeaponVendor2 = 0x83;
        public const int Tiers = 30;

        /// <summary>Alpha keeps at most ten buyback entries (<c>GameController+0x800D3C</c>, oldest removed first).</summary>
        public const int BuybackCapacity = 10;

        public static bool IsVendor(int role) => role >= ItemVendor && role <= WeaponVendor2;

        /// <summary>Stock of a vendor role in Alpha insertion order (identical records are merged as stacks by the caller).</summary>
        public static List<AlphaItemRecord> Stock(AlphaCrtRandom rng, int role)
        {
            switch (role)
            {
                case ItemVendor: return ItemStock(rng);
                case WeaponVendor:
                case WeaponVendor2: return WeaponStock(rng);
                case ArmorVendor: return ArmorStock(rng);
                default: return new List<AlphaItemRecord>();
            }
        }

        /// <summary>Tier level: <c>inv(t/30) + rand % (inv((t+0.99999)/30) − inv(t/30) + 1)</c>, at least 1; tier 0 is level 1.</summary>
        public static int TierLevel(AlphaCrtRandom rng, int tier)
        {
            int low = (int)AlphaLoot.CurveInverse20P1(tier / 30f);
            int high = (int)AlphaLoot.CurveInverse20P1((tier + 0.99999f) / 30f);
            int level = low + rng.Next() % (high - low + 1);
            if (level < 1) level = 1;
            return tier == 0 ? 1 : level;
        }

        /// <summary>Inlined <c>0x5F8530(0)</c>: one discarded roll then the 1 % / 0.1 % / 0.01 % steps, seed = full <c>rand()</c>.</summary>
        private static AlphaItemRecord Rolled(AlphaCrtRandom rng, int type, int subtype, int modifier, int level)
        {
            int rarity = AlphaLoot.RollRarity(rng, 0);
            int seed = rng.Next();
            return new AlphaItemRecord(type, subtype, seed, 0, rarity, modifier, level);
        }

        /// <summary>Picks <c>rand%3+1</c> pool entries without repetition (<c>rand % remaining</c>, then shift).</summary>
        private static void Pick(AlphaCrtRandom rng, List<AlphaItemRecord> pool, List<AlphaItemRecord> stock)
        {
            int count = rng.Next() % 3 + 1;
            for (int i = 0; i < count && pool.Count > 0; i++)
            {
                int index = (int)((uint)rng.Next() % (uint)pool.Count);
                stock.Add(pool[index]);
                pool.RemoveAt(index);
            }
        }

        /// <summary>
        /// 0x5C8390 armour vendor: per tier 21 candidates — Warrior set (material 0x01: shoulder, chest, gloves, boots),
        /// the Mage/Ranger/Rogue sets (0x19/0x1A/0x1B: two shoulders, chest, gloves, boots), amulet and ring with
        /// modifier 0x0B/0x0C — of which <c>rand%3+1</c> are stocked.
        /// </summary>
        public static List<AlphaItemRecord> ArmorStock(AlphaCrtRandom rng)
        {
            var stock = new List<AlphaItemRecord>();
            for (int tier = 0; tier < Tiers; tier++)
            {
                int level = TierLevel(rng, tier);
                var pool = new List<AlphaItemRecord>(21);
                foreach (int type in new[] { 7, 4, 5, 6 }) pool.Add(Rolled(rng, type, 0, 0x01, level));
                foreach (int material in new[] { 0x19, 0x1A, 0x1B })
                {
                    foreach (int type in new[] { 7, 7, 4, 5, 6 }) pool.Add(Rolled(rng, type, 0, material, level));
                }

                foreach (int type in new[] { 8, 9 })
                {
                    int modifier = rng.Next() % 2 != 0 ? 0x0B : 0x0C;
                    pool.Add(Rolled(rng, type, 0, modifier, level));
                }

                Pick(rng, pool, stock);
            }

            return stock;
        }

        /// <summary>
        /// 0x5CA0A0 weapon vendor: per tier 11 candidates — Warrior one-hander <c>rand%3</c>, two-hander
        /// <c>0x0F+rand%3</c>, shield; Rogue dagger, longsword, fist (material 0x01); Ranger bow, a crossbow-sized gap
        /// of five discarded rolls, boomerang; Mage staff, wand (0x02) and bracelet (0x0B/0x0C).
        /// </summary>
        public static List<AlphaItemRecord> WeaponStock(AlphaCrtRandom rng)
        {
            var stock = new List<AlphaItemRecord>();
            for (int tier = 0; tier < Tiers; tier++)
            {
                int level = TierLevel(rng, tier);
                var pool = new List<AlphaItemRecord>(11);
                int sub = rng.Next() % 3;
                pool.Add(Rolled(rng, 3, sub, 0x01, level));
                sub = rng.Next() % 3 + 0x0F;
                pool.Add(Rolled(rng, 3, sub, 0x01, level));
                foreach (int s in new[] { 0x0D, 3, 5, 4 }) pool.Add(Rolled(rng, 3, s, 0x01, level));
                pool.Add(Rolled(rng, 3, 6, 0x02, level));
                for (int i = 0; i < 5; i++) rng.Next();
                pool.Add(Rolled(rng, 3, 8, 0x02, level));
                pool.Add(Rolled(rng, 3, 0x0A, 0x02, level));
                pool.Add(Rolled(rng, 3, 0x0B, 0x02, level));
                int braceletModifier = rng.Next() % 2 != 0 ? 0x0B : 0x0C;
                pool.Add(Rolled(rng, 3, 0x0C, braceletModifier, level));
                Pick(rng, pool, stock);
            }

            return stock;
        }

        private static readonly int[] TierZeroFood = { 0x22, 0x23, 0x13, 0x1A, 0x1E, 0x57 };

        /// <summary>
        /// 0x5C9B30 item vendor. Tier 0 (level 1): flask (0x0B/0x0C, modifier 0x18), lamp (0x18, modifier 1), lamps of
        /// rarity 1…4 with chances 1/10, 1/20, 1/30, 1/40, hang glider and boat (0x17/0 and 0x17/1, modifier 2), one
        /// food (0x14) of six subtypes. Every tier: <c>rand%3</c> formulas (rarity <c>rand%2</c> + steps) of the tier
        /// level from either class generator, then with 1/2 a bomb (type 1/7) of the tier level.
        /// </summary>
        public static List<AlphaItemRecord> ItemStock(AlphaCrtRandom rng)
        {
            var stock = new List<AlphaItemRecord>();
            for (int tier = 0; tier < Tiers; tier++)
            {
                int level = TierLevel(rng, tier);
                if (tier == 0)
                {
                    stock.Add(new AlphaItemRecord(0x0B, 0x0C, modifier: 0x18, level: 1));
                    stock.Add(new AlphaItemRecord(0x18, 0, modifier: 0x01, level: 1));
                    int rarity = 1;
                    for (int divisor = 10; divisor < 50; divisor += 10, rarity++)
                    {
                        if (rng.Next() % divisor == 0)
                        {
                            stock.Add(new AlphaItemRecord(0x18, 0, rarity: rarity, modifier: 0x01, level: 1));
                        }
                    }

                    stock.Add(new AlphaItemRecord(0x17, 0, modifier: 0x02, level: 1));
                    stock.Add(new AlphaItemRecord(0x17, 1, modifier: 0x02, level: 1));
                    stock.Add(new AlphaItemRecord(AlphaLoot.TypeFood, TierZeroFood[rng.Next() % 6], level: 1));
                }

                int formulas = rng.Next() % 3;
                for (int i = 0; i < formulas; i++)
                {
                    int formulaRarity = rng.Next() % 2;
                    if (rng.Next() % 100 == 0) formulaRarity++;
                    if (rng.Next() % 1000 == 0) formulaRarity++;
                    if (rng.Next() % 10000 == 0) formulaRarity++;
                    if (formulaRarity > 4) formulaRarity = 4;
                    AlphaItemRecord target = rng.Next() % 2 == 0
                        ? AlphaLoot.GenerateArmor(rng, level, formulaRarity, AlphaLoot.AllClasses)
                        : AlphaLoot.GenerateWeapon(rng, level, formulaRarity, AlphaLoot.AllClasses);
                    stock.Add(new AlphaItemRecord(AlphaLoot.TypeFormula, target.Subtype, target.Seed, target.Type,
                        target.Rarity, target.Modifier, target.Level));
                }

                if (rng.Next() % 2 != 0)
                {
                    stock.Add(new AlphaItemRecord(AlphaLoot.TypeConsumable, 7, level: level));
                }
            }

            return stock;
        }

        /// <summary>Identification price <c>0x450920</c>: <c>max(1, value/2)</c> copper.</summary>
        public static int IdentifyPrice(InventoryEntry item) => Math.Max(1, AlphaTradeValue.Of(item) / 2);

        /// <summary>
        /// Identification (0x47B600 branch on a type 0x0E item): even roll → weapon, odd → armour/jewellery, all
        /// classes, level and rarity of the unidentified item.
        /// </summary>
        public static AlphaItemRecord Identify(AlphaCrtRandom rng, AlphaItemRecord unidentified)
        {
            return rng.Next() % 2 == 0
                ? AlphaLoot.GenerateWeapon(rng, unidentified.Level, unidentified.Rarity, AlphaLoot.AllClasses)
                : AlphaLoot.GenerateArmor(rng, unidentified.Level, unidentified.Rarity, AlphaLoot.AllClasses);
        }

        /// <summary>
        /// Adaptation price <c>computeLevelDiffBonus 0x40F4F0</c> in platinum: for every level the item lags behind the
        /// hero, <c>cost = (int)(cost + 2^((rarity − 1 + itemLevel)·0.25)·2)</c>; 0 when the item is not lower.
        /// </summary>
        public static int AdaptPrice(int itemLevel, int itemRarity, int heroLevel)
        {
            if (itemLevel >= heroLevel)
            {
                return 0;
            }

            float step = (float)Math.Pow(2.0, (float)(itemRarity - 1 + itemLevel) * 0.25f);
            int cost = 0;
            for (int i = heroLevel - itemLevel; i > 0; i--)
            {
                cost = (int)(cost + step * 2f);
            }

            return cost;
        }
    }
}
