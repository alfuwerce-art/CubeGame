using System;
using System.Collections.Generic;
using CubeGame.Core.Progression;
using CubeGame.Features.World;

namespace CubeGame.Features.Items
{
    /// <summary>Where a generated record goes: the ground near the source, or directly into the receiver.</summary>
    public enum AlphaLootDelivery
    {
        Ground,
        Direct,
    }

    /// <summary>One generated Alpha item with its count and, for ground drops, the scatter offset in blocks.</summary>
    public readonly struct AlphaLootDrop
    {
        public AlphaLootDrop(AlphaItemRecord record, int count, double offsetX, double offsetZ, string source)
        {
            Record = record;
            Count = count;
            OffsetX = offsetX;
            OffsetZ = offsetZ;
            Source = source;
        }

        public AlphaItemRecord Record { get; }
        public int Count { get; }

        /// <summary>Alpha horizontal offset <c>1 − 2·rand/32767</c> (fixed 1/65536 units → blocks).</summary>
        public double OffsetX { get; }

        public double OffsetZ { get; }

        /// <summary>Which Alpha branch produced it (diagnostics and provenance registry).</summary>
        public string Source { get; }

        public override string ToString() => $"{Source}: {Record} x{Count}";
    }

    /// <summary>Inputs of <see cref="AlphaLoot.DeathDrops"/>: the Alpha creature fields the death routine reads.</summary>
    public sealed class AlphaDeathInput
    {
        /// <summary>Creature <c>+0x60</c>: 0 player, 1 hostile, 3 excluded; any other value is a non-hostile actor.</summary>
        public int Role = 1;

        /// <summary>Creature kind <c>+0x64</c> (0x8F is a chest).</summary>
        public int Kind;

        /// <summary>Creature level <c>+0x190</c>.</summary>
        public int Level = 1;

        /// <summary>Creature stars <c>+0x1A8</c>.</summary>
        public int Stars;

        /// <summary>Creature flags <c>+0x7E</c> (0x8/0x10 leftovers instead of coins, 0x200 elite, 0x800 no loot).</summary>
        public int Flags;

        /// <summary>
        /// Equipped records in Alpha collection order 0x990, 0xAA8, 0x530, 0x878, 0x760, 0x648, 0x418
        /// (offhand, weapon, chest, shoulders, boots, gloves, amulet). Empty slots are omitted.
        /// </summary>
        public List<AlphaItemRecord> Equipped = new List<AlphaItemRecord>();

        /// <summary>Bag entries <c>+0x11DC</c> (record, count). Dropped only by hostile actors.</summary>
        public List<(AlphaItemRecord Record, int Count)> Bags = new List<(AlphaItemRecord, int)>();

        /// <summary>
        /// Food subtypes of the world list read by 0x5F79E0 (<c>this+0x88</c>). The list contents are not recovered
        /// (UNKNOWN); when it is empty the 1/5 food drop consumes its rolls but delivers nothing.
        /// </summary>
        public IReadOnlyList<int> FoodSubtypes = Array.Empty<int>();
    }

    /// <summary>
    /// Alpha 2013 item generation, loot and reward routines, ported from Cube.exe (SHA-256
    /// 84a7a132…6dc30bf) with the MSVC CRT <c>rand</c> (<see cref="AlphaCrtRandom"/>). Every routine consumes
    /// the stream in the original order, including rolls whose result Alpha discards, so a seeded stream
    /// reproduces one Alpha sequence. Provenance per routine is ALPHA_CODE_STATIC (byte-verified branches noted).
    /// What seeds the stream at each call site is the caller's decision and is recorded there.
    /// </summary>
    public static class AlphaLoot
    {
        public const int ChestKind = 0x8F;
        public const int ChestSlots = 10;

        public const int TypeConsumable = 0x01;
        public const int TypeFormula = 0x02;
        public const int TypeMaterial = 0x0B;
        public const int TypeCoin = 0x0C;
        public const int TypePlatinum = 0x0D;
        public const int TypeLeftovers = 0x0E;
        public const int TypeCandle = 0x12;
        public const int TypeFood = 0x14;

        public const int CoinCopper = 0x0A;
        public const int CoinGold = 0x0B;
        public const int CoinSilver = 0x0C;

        /// <summary>Alpha class byte <c>+0x140</c> = class index + 1 (Warrior 1, Ranger 2, Mage 3, Rogue 4); −1 = all.</summary>
        public const int AllClasses = -1;

        // ---------------------------------------------------------------- shared helpers

        /// <summary>0x5F8530: <c>rand%(max+1)</c> plus 1 % / 0.1 % / 0.01 % steps; <paramref name="forceMax"/> sets max+1; cap 4. Byte-verified.</summary>
        public static int RollRarity(AlphaCrtRandom rng, int max, bool forceMax = false)
        {
            int divisor = max + 1;
            if (divisor <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(max));
            }

            int count = rng.Next() % divisor;
            if (rng.Next() % 100 == 0) count++;
            if (rng.Next() % 1000 == 0) count++;
            if (rng.Next() % 10000 == 0) count++;
            if (forceMax) count = divisor;
            return count > 4 ? 4 : count;
        }

        /// <summary>0x43CA60.</summary>
        public static float CurveLevel05(float n) => 1f - 1f / ((n - 1f) * 0.05f + 1f);

        /// <summary>0x4462F0, inverse of <see cref="CurveLevel05"/>.</summary>
        public static float CurveInverse20P1(float frac) => (1f / (1f - frac) - 1f) * 20f + 1f;

        /// <summary>
        /// 0x4C7E80 <c>item_rerollStackCount</c>, applied to the item level: for L &gt; 1 a new level is drawn
        /// between the levels whose curve value is <c>±fraction</c> away (upper bound only when <paramref name="cap"/>).
        /// </summary>
        public static AlphaItemRecord RerollLevel(AlphaCrtRandom rng, AlphaItemRecord item, float fraction, bool cap)
        {
            short level = unchecked((short)item.Level);
            if (level <= 1)
            {
                return item;
            }

            float f = CurveLevel05(level);
            if (f - fraction <= 0f)
            {
                return item.WithLevel(1);
            }

            int min = (int)CurveInverse20P1(f - fraction);
            int max = level;
            if (min < 1) min = 1;
            if (cap)
            {
                float upper = CurveLevel05(max) + fraction;
                if (upper > 0.9999f) upper = 0.9999f;
                max = (int)CurveInverse20P1(upper);
            }

            int span = max - min + 1;
            int roll = rng.Next();
            return item.WithLevel(unchecked((short)(roll % span + min)));
        }

        /// <summary>0x445F60 <c>math_pow2Mul</c>: <c>(float)2^(curve(level)·3) · (float)2^(stars·0.25)</c>.</summary>
        public static float Pow2Mul(float level, int stars)
        {
            double a = Math.Pow(2.0, (1.0 - 1.0 / ((level - 1.0) * 0.05 + 1.0)) * 3.0);
            double b = Math.Pow(2.0, (float)stars * 0.25);
            return (float)a * (float)b;
        }

        private static float Unit(AlphaCrtRandom rng) => 1f - rng.Next() * 2f / 32767f;

        private static void Scatter(AlphaCrtRandom rng, out double x, out double z)
        {
            x = Unit(rng);
            z = Unit(rng);
        }

        // ---------------------------------------------------------------- class item generators

        /// <summary>
        /// 0x5F51E0 armour/jewellery pool (byte-verified): per class a material and the shoulders/chest/gloves/boots
        /// set, then amulet and ring with modifier 0x0B/0x0C; the result is <c>pool[rand % count]</c>.
        /// Warrior 1 → modifier 0x01 with one shoulder; Mage 3 → 0x19, Ranger 2 → 0x1A, Rogue 4 → 0x1B with two
        /// shoulders each. <paramref name="alphaClass"/> &lt; 0 adds every class. Seed is <c>rand%100</c>.
        /// </summary>
        public static AlphaItemRecord GenerateArmor(AlphaCrtRandom rng, int level, int rarity, int alphaClass)
        {
            var pool = new List<AlphaItemRecord>(24);
            bool all = alphaClass < 0;
            if (alphaClass == 1 || all) AddSet(rng, pool, level, rarity, 0x01, 7, 4, 5, 6);
            if (alphaClass == 3 || all) AddSet(rng, pool, level, rarity, 0x19, 7, 7, 4, 5, 6);
            if (alphaClass == 2 || all) AddSet(rng, pool, level, rarity, 0x1A, 7, 7, 4, 5, 6);
            if (alphaClass == 4 || all) AddSet(rng, pool, level, rarity, 0x1B, 7, 7, 4, 5, 6);
            int amuletMod = rng.Next() % 2 == 0 ? 0x0B : 0x0C;
            pool.Add(new AlphaItemRecord(8, 0, rng.Next() % 100, 0, rarity, amuletMod, level));
            int ringMod = rng.Next() % 2 == 0 ? 0x0B : 0x0C;
            pool.Add(new AlphaItemRecord(9, 0, rng.Next() % 100, 0, rarity, ringMod, level));
            return pool[(int)((uint)rng.Next() % (uint)pool.Count)];
        }

        private static void AddSet(AlphaCrtRandom rng, List<AlphaItemRecord> pool, int level, int rarity, int modifier, params int[] types)
        {
            foreach (int type in types)
            {
                pool.Add(new AlphaItemRecord(type, 0, rng.Next() % 100, 0, rarity, modifier, level));
            }
        }

        /// <summary>
        /// 0x5F8AD0 weapon pool (byte-verified). Warrior: one-hander <c>rand%3</c>, two-hander <c>0x0F+rand%3</c>,
        /// shield 0x0D (modifier 0x01). Rogue: dagger 3, longsword 5, fist 4 (0x01). Ranger: bow 6, then one discarded
        /// roll, boomerang 8 (0x02). Mage: staff 0x0A, wand 0x0B (0x02), bracelet 0x0C with modifier 0x0B/0x0C.
        /// </summary>
        public static AlphaItemRecord GenerateWeapon(AlphaCrtRandom rng, int level, int rarity, int alphaClass)
        {
            var pool = new List<AlphaItemRecord>(16);
            bool all = alphaClass < 0;
            int modifier = 0x01;
            if (alphaClass == 1 || all)
            {
                int sub = rng.Next() % 3;
                pool.Add(Weapon(sub, rng.Next() % 100, rarity, modifier, level));
                sub = rng.Next() % 3 + 0x0F;
                pool.Add(Weapon(sub, rng.Next() % 100, rarity, modifier, level));
                pool.Add(Weapon(0x0D, rng.Next() % 100, rarity, modifier, level));
            }

            if (alphaClass == 4 || all)
            {
                pool.Add(Weapon(3, rng.Next() % 100, rarity, modifier, level));
                pool.Add(Weapon(5, rng.Next() % 100, rarity, modifier, level));
                pool.Add(Weapon(4, rng.Next() % 100, rarity, modifier, level));
            }

            modifier = 0x02;
            if (alphaClass == 2 || all)
            {
                pool.Add(Weapon(6, rng.Next() % 100, rarity, modifier, level));
                rng.Next();
                pool.Add(Weapon(8, rng.Next() % 100, rarity, modifier, level));
            }

            if (alphaClass == 3 || all)
            {
                pool.Add(Weapon(0x0A, rng.Next() % 100, rarity, modifier, level));
                pool.Add(Weapon(0x0B, rng.Next() % 100, rarity, modifier, level));
                int braceletMod = rng.Next() % 2 != 0 ? 0x0B : 0x0C;
                pool.Add(Weapon(0x0C, rng.Next() % 100, rarity, braceletMod, level));
            }

            if (pool.Count == 0)
            {
                // Alpha divides by zero here (class 0 never reaches the weapon generator in the traced callers).
                throw new ArgumentOutOfRangeException(nameof(alphaClass), alphaClass, "no weapon pool for this class");
            }

            return pool[(int)((uint)rng.Next() % (uint)pool.Count)];
        }

        private static AlphaItemRecord Weapon(int subtype, int seed, int rarity, int modifier, int level) =>
            new AlphaItemRecord(3, subtype, seed, 0, rarity, modifier, level);

        /// <summary>0x5F6D50: formula (type 2) for an item of either pool; <c>+0x08</c> keeps the target type.</summary>
        public static AlphaItemRecord GenerateFormula(AlphaCrtRandom rng, int level, int rarity)
        {
            AlphaItemRecord target = rng.Next() % 2 == 0
                ? GenerateArmor(rng, level, rarity, AllClasses)
                : GenerateWeapon(rng, level, rarity, AllClasses);
            return new AlphaItemRecord(TypeFormula, target.Subtype, target.Seed, target.Type, target.Rarity, target.Modifier, target.Level);
        }

        /// <summary>
        /// 0x5F7A60 (byte-verified at 0x5F7B12…0x5F84FB): one random pick from 26 candidates of level
        /// <paramref name="level"/>. One shared template record is rewritten field by field and pushed 25 times, each
        /// with its own <see cref="RollRarity"/>(<paramref name="quality"/>): weapons 3/rand%3, 3/0x0D (modifier 1),
        /// 3/6, 3/7, 3/0x0A, 3/0x0B (modifier 2); then types 7, 4, 5, 6 with modifier 1 and 7, 7, 4, 5, 6 with
        /// modifiers 0x19, 0x1A, 0x1B. Four and five <c>rand()</c> results before the first two are discarded by the
        /// original. The 26th candidate is a formula (0x5F6D50); the pick is <c>(unsigned)rand() % 26</c>.
        /// </summary>
        public static AlphaItemRecord RandomEquipment(AlphaCrtRandom rng, int level, int quality)
        {
            var list = new List<AlphaItemRecord>(26);
            int type = 3, subtype = 0, modifier = 0;

            void Push() => list.Add(new AlphaItemRecord(type, subtype, 0, 0, RollRarity(rng, quality), modifier, level));

            for (int i = 0; i < 4; i++) rng.Next();
            modifier = 1;
            subtype = rng.Next() % 3;
            Push();
            for (int i = 0; i < 5; i++) rng.Next();
            subtype = 0x0D;
            Push();
            modifier = 2;
            foreach (int s in new[] { 6, 7, 0x0A, 0x0B })
            {
                subtype = s;
                Push();
            }

            type = 7;
            subtype = 0;
            modifier = 1;
            foreach (int t in new[] { 7, 4, 5, 6 })
            {
                type = t;
                Push();
            }

            foreach (int m in new[] { 0x19, 0x1A, 0x1B })
            {
                modifier = m;
                foreach (int t in new[] { 7, 7, 4, 5, 6 })
                {
                    type = t;
                    Push();
                }
            }

            list.Add(GenerateFormula(rng, level, quality));
            return list[(int)((uint)rng.Next() % (uint)list.Count)];
        }

        // ---------------------------------------------------------------- chest contents

        /// <summary>
        /// 0x5F8750 <c>Item_generateStats</c> (byte-verified): one chest slot of level <paramref name="level"/>.
        /// 1/2 copper coins; otherwise 1/6 a consumable/silver branch (switch <c>rand%5</c>: 0 type 1/1, 1 and 2 silver
        /// coins — case 1 falls through, keeping subtype 4 — 3 type 1/7, 4 type 1/5), else <c>rand%2</c>: 1 candle
        /// (type 0x12, seed <c>rand%3</c>), 0 an empty slot (the 0x0C0B/0x18 branch is unreachable in Alpha).
        /// Coin count = <c>rand % (int(2^((1+2·r/32767)·L/4))·2 + 2)</c> stored in the level field.
        /// </summary>
        public static AlphaItemRecord ChestSlot(AlphaCrtRandom rng, int level)
        {
            if (rng.Next() % 2 != 0)
            {
                return Coins(rng, level, CoinCopper, 0);
            }

            if (rng.Next() % 6 == 0)
            {
                switch (rng.Next() % 5)
                {
                    case 0:
                        return new AlphaItemRecord(TypeConsumable, 1, level: level);
                    case 1:
                        return Coins(rng, level, CoinSilver, 4);
                    case 2:
                        return Coins(rng, level, CoinSilver, 0);
                    case 3:
                        return new AlphaItemRecord(TypeConsumable, 7, level: level);
                    default:
                        return new AlphaItemRecord(TypeConsumable, 5, level: level);
                }
            }

            if (rng.Next() % 2 == 1)
            {
                return new AlphaItemRecord(TypeCandle, 0, seed: rng.Next() % 3, level: level);
            }

            return default;
        }

        private static AlphaItemRecord Coins(AlphaCrtRandom rng, int level, int modifier, int subtype)
        {
            float exponent = (rng.Next() * 2f / 32767f + 1f) * level * 0.25f;
            float scale = (float)Math.Pow(2.0, exponent);
            int truncated = TruncateToInt(scale);
            int divisor = unchecked(truncated * 2 + 2);
            int roll = rng.Next();
            int count = divisor == 0 ? 0 : roll % divisor;
            return new AlphaItemRecord(TypeCoin, subtype, modifier: modifier, level: unchecked((short)count));
        }

        /// <summary>SSE <c>cvttss2si</c>: out-of-range values become int.MinValue (the "integer indefinite").</summary>
        private static int TruncateToInt(float value)
        {
            if (float.IsNaN(value) || value >= 2147483648f || value < -2147483648f)
            {
                return int.MinValue;
            }

            return (int)value;
        }

        /// <summary>0x5C9F90: a container receives <see cref="ChestSlots"/> slots from <see cref="ChestSlot"/>.</summary>
        public static List<AlphaItemRecord> ChestContents(AlphaCrtRandom rng, int level)
        {
            var slots = new List<AlphaItemRecord>(ChestSlots);
            for (int i = 0; i < ChestSlots; i++)
            {
                slots.Add(ChestSlot(rng, level));
            }

            return slots;
        }

        // ---------------------------------------------------------------- mission reward

        /// <summary>
        /// Region mission completion in 0x595A60 (byte-verified at 0x596919…0x596A69): platinum coins
        /// <c>int(curve_level05(level+stars)·50 + 1)</c>, then one class item of the mission level with
        /// rarity = mission stars; even roll → armour/jewellery, odd → weapon. Both go directly into the bags.
        /// </summary>
        public static List<AlphaLootDrop> MissionReward(AlphaCrtRandom rng, int missionLevel, int missionStars, int alphaClass)
        {
            int platinum = (int)(CurveLevel05(missionLevel + missionStars) * 50f + 1f);
            var drops = new List<AlphaLootDrop>
            {
                new AlphaLootDrop(new AlphaItemRecord(TypePlatinum, 0, level: unchecked((short)platinum)), 1, 0, 0, "mission.platinum"),
            };
            AlphaItemRecord item = rng.Next() % 2 == 0
                ? GenerateArmor(rng, missionLevel, missionStars, alphaClass)
                : GenerateWeapon(rng, missionLevel, missionStars, alphaClass);
            drops.Add(new AlphaLootDrop(item, 1, 0, 0, "mission.item"));
            return drops;
        }

        // ---------------------------------------------------------------- death

        /// <summary>
        /// 0x59B180 <c>Creature_spawnDeathEffects</c> (byte-verified at the branch points): every ground item a
        /// dying creature or opened chest scatters, in Alpha order.
        /// <list type="number">
        /// <item>Role 0/3 or flag 0x800: nothing (one roll is still not consumed).</item>
        /// <item>One equipped item: <c>rand%4==0</c> or flag 0x200, chosen by <c>rand % count</c>.</item>
        /// <item>Hostile or chest, <c>rand%10==0</c>: class item of level <c>L−1+rand%2</c>, rarity
        /// <c>RollRarity(stars)</c>, all classes, level re-rolled ±0.05; then 1/5 food from the world list.</item>
        /// <item><c>rand%3+1</c> kind materials (type 0x0B) for the kinds of the 0x59C424 table.</item>
        /// <item>Hostile or chest, <c>rand%50==0</c>: formula with rarity <c>RollRarity(stars+1)</c>.</item>
        /// <item>Flags 0x18 clear: coins <c>pow2Mul(L,stars)·10·(1+2r)</c> (×10 with 0x200) split into
        /// copper/silver/gold; otherwise a 1/20 leftovers item with tier from <c>rand%1000 + 20·stars</c>.</item>
        /// <item>Hostile: every bag item except life potions (1/1), one ground item per unit.</item>
        /// </list>
        /// </summary>
        public static List<AlphaLootDrop> DeathDrops(AlphaCrtRandom rng, AlphaDeathInput c)
        {
            var drops = new List<AlphaLootDrop>();
            if (c == null)
            {
                throw new ArgumentNullException(nameof(c));
            }

            if (c.Role == 0 || c.Role == 3 || (c.Flags & 0x800) != 0)
            {
                return drops;
            }

            rng.Next();
            bool hostileOrChest = c.Role == 1 || c.Kind == ChestKind;

            if (c.Equipped != null && c.Equipped.Count > 0)
            {
                if (rng.Next() % 4 == 0 || (c.Flags & 0x200) != 0)
                {
                    Scatter(rng, out double x, out double z);
                    int index = (int)((uint)rng.Next() % (uint)c.Equipped.Count);
                    rng.Next();
                    drops.Add(new AlphaLootDrop(c.Equipped[index], 1, x, z, "death.equipped"));
                }
            }

            if (hostileOrChest && rng.Next() % 10 == 0)
            {
                int level = c.Level - 1 + rng.Next() % 2;
                if (level < 1) level = 1;
                bool weapon = rng.Next() % 2 != 0;
                int rarity = RollRarity(rng, c.Stars);
                AlphaItemRecord item = weapon
                    ? GenerateWeapon(rng, level, rarity, AllClasses)
                    : GenerateArmor(rng, level, rarity, AllClasses);
                item = RerollLevel(rng, item, 0.05f, true);
                Scatter(rng, out double x, out double z);
                rng.Next();
                drops.Add(new AlphaLootDrop(item, 1, x, z, "death.classItem"));

                if (rng.Next() % 5 == 0)
                {
                    RollRarity(rng, c.Stars);
                    int roll = rng.Next();
                    AlphaItemRecord food = default;
                    if (c.FoodSubtypes != null && c.FoodSubtypes.Count > 0)
                    {
                        int sub = c.FoodSubtypes[(int)((uint)roll % (uint)c.FoodSubtypes.Count)];
                        food = new AlphaItemRecord(TypeFood, sub);
                    }

                    Scatter(rng, out x, out z);
                    rng.Next();
                    if (!food.IsEmpty)
                    {
                        drops.Add(new AlphaLootDrop(food, 1, x, z, "death.food"));
                    }
                }
            }

            int materials = rng.Next() % 3 + 1;
            for (int i = 0; i < materials; i++)
            {
                AlphaItemRecord material = KindMaterial(rng, c.Kind);
                if (material.IsEmpty)
                {
                    break;
                }

                Scatter(rng, out double x, out double z);
                rng.Next();
                drops.Add(new AlphaLootDrop(material, 1, x, z, "death.material"));
            }

            if (hostileOrChest)
            {
                if (rng.Next() % 50 == 0)
                {
                    Scatter(rng, out double x, out double z);
                    rng.Next();
                    int rarity = RollRarity(rng, c.Stars + 1);
                    drops.Add(new AlphaLootDrop(GenerateFormula(rng, c.Level, rarity), 1, x, z, "death.formula"));
                }

                if ((c.Flags & 0x18) == 0)
                {
                    float value = Pow2Mul(c.Level, c.Stars) * 10f;
                    value *= rng.Next() * 2f / 32767f + 1f;
                    if ((c.Flags & 0x200) != 0) value *= 10f;
                    int total = TruncateToInt(value);
                    int copper = total % 100;
                    int hundreds = total / 100;
                    int silver = hundreds % 100;
                    int gold = hundreds / 100;
                    AddCoins(rng, drops, copper, CoinCopper);
                    AddCoins(rng, drops, silver, CoinSilver);
                    AddCoins(rng, drops, gold, CoinGold);
                }
                else
                {
                    int r = rng.Next() % 1000 + c.Stars * 20;
                    int tier = r < 700 ? 0 : r < 950 ? 1 : r > 998 ? 3 : 2;
                    if ((c.Flags & 0x200) != 0 && ++tier > 3) tier = 3;
                    if (rng.Next() % 20 == 0 && tier != 0)
                    {
                        int seed = rng.Next();
                        Scatter(rng, out double x, out double z);
                        rng.Next();
                        drops.Add(new AlphaLootDrop(new AlphaItemRecord(TypeLeftovers, 0, seed, 0, tier, 0, c.Level), 1, x, z, "death.leftovers"));
                    }
                }
            }

            if (c.Role == 1 && c.Bags != null)
            {
                foreach ((AlphaItemRecord record, int count) in c.Bags)
                {
                    if (record.IsEmpty || count <= 0 || (record.Type == 1 && record.Subtype == 1))
                    {
                        continue;
                    }

                    for (int n = 0; n < count; n++)
                    {
                        Scatter(rng, out double x, out double z);
                        rng.Next();
                        drops.Add(new AlphaLootDrop(record, 1, x, z, "death.bag"));
                    }
                }
            }

            return drops;
        }

        private static void AddCoins(AlphaCrtRandom rng, List<AlphaLootDrop> drops, int count, int modifier)
        {
            if (count == 0)
            {
                return;
            }

            Scatter(rng, out double x, out double z);
            rng.Next();
            drops.Add(new AlphaLootDrop(new AlphaItemRecord(TypeCoin, 0, modifier: modifier, level: unchecked((short)count)), 1, x, z, "death.coins"));
        }

        /// <summary>Kind → material switch of 0x59B717 (jump table 0x59C3D8/0x59C424, byte-verified).</summary>
        public static AlphaItemRecord KindMaterial(AlphaCrtRandom rng, int kind)
        {
            switch (kind)
            {
                case 0x3A: return Material(0x02, 0x13);
                case 0x46: return Material(0x12, 0);
                case 0x47: return Material(0x15, 0);
                case 0x78:
                case 0x79:
                case 0x7C:
                case 0x7E:
                    return rng.Next() % 2 == 0 ? Material(0x05, 0x15) : Material(0x01, 0x02);
                case 0x7A: return Material(0x1B, 0);
                case 0x7B: return Material(0x0B, 0x1B);
                case 0x7D: return Material(0x06, 0);
                case 0x7F: return Material(0x14, 0);
                case 0x80: return Material(0x17, 0);
                case 0x83: return Material(0x00, 0x0B);
                case 0x84: return Material(0x00, 0x01);
                case 0x85: return Material(0x00, 0x0C);
                case 0x86: return Material(0x00, 0x11);
                case 0x87: return new AlphaItemRecord(TypeMaterial, 0, rarity: 1, modifier: 0x0D);
                case 0x88: return new AlphaItemRecord(TypeMaterial, 0, rarity: 2, modifier: 0x0E);
                case 0x89: return new AlphaItemRecord(TypeMaterial, 0, rarity: 3, modifier: 0x0F);
                case 0x8A: return new AlphaItemRecord(TypeMaterial, 0, rarity: 4, modifier: 0x10);
                case 0x8B: return Material(0x08, 0x16);
                default: return default;
            }
        }

        private static AlphaItemRecord Material(int subtype, int modifier) =>
            new AlphaItemRecord(TypeMaterial, subtype, modifier: modifier, level: 1);
    }

    /// <summary>
    /// Wallet semantics of <c>GameController_addItemToInventory</c> 0x46EBE0 (ALPHA_CODE_STATIC): coin records
    /// never enter the bags. Type 0x0C adds its count (stored in the level field) to the money field with modifier
    /// 0x0A ×1 (copper), 0x0C ×100 (silver), 0x0B ×10000 (gold); type 0x0D adds to the platinum counter.
    /// </summary>
    public static class AlphaWallet
    {
        public static bool IsCurrency(AlphaItemRecord record) =>
            (record.Type == AlphaLoot.TypeCoin && (record.Modifier == AlphaLoot.CoinCopper || record.Modifier == AlphaLoot.CoinSilver || record.Modifier == AlphaLoot.CoinGold))
            || record.Type == AlphaLoot.TypePlatinum;

        /// <summary>Copper value of a coin record (0 for non-coins and platinum).</summary>
        public static long CopperValue(AlphaItemRecord record, int count = 1)
        {
            if (record.Type != AlphaLoot.TypeCoin)
            {
                return 0;
            }

            long units = (long)(short)record.Level * count;
            switch (record.Modifier)
            {
                case AlphaLoot.CoinCopper: return units;
                case AlphaLoot.CoinSilver: return units * 100;
                case AlphaLoot.CoinGold: return units * 10000;
                default: return 0;
            }
        }

        public static long PlatinumValue(AlphaItemRecord record, int count = 1) =>
            record.Type == AlphaLoot.TypePlatinum ? (long)(short)record.Level * count : 0;
    }
}
