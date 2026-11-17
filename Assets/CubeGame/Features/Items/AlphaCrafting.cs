using System.Collections.Generic;
using CubeGame.Features.World;

namespace CubeGame.Features.Items
{
    /// <summary>Crafting station a recipe requires (Alpha recipe <c>+0x118</c>, labels from 0x42F910).</summary>
    public enum AlphaCraftStation
    {
        None = 0,
        Water = 1,
        Furnace = 3,
        Anvil = 4,
        SpinningWheel = 5,
        Loom = 6,
        Saw = 7,
        Workbench = 8,
        Campfire = 9,
    }

    /// <summary>One Alpha recipe: the item it makes, the station it needs and its ingredients (record, quantity).</summary>
    public sealed class AlphaRecipe
    {
        public AlphaRecipe(AlphaItemRecord output, AlphaCraftStation station, List<(AlphaItemRecord Item, int Count)> ingredients)
        {
            Output = output;
            Station = station;
            Ingredients = ingredients;
        }

        public AlphaItemRecord Output { get; }
        public AlphaCraftStation Station { get; }
        public IReadOnlyList<(AlphaItemRecord Item, int Count)> Ingredients { get; }

        /// <summary>Alpha crafting tab (0x4A161D): 0 Weapons, 1 Armor, 2 Amulets, 3 Cooking, 4 Alchemy, 5 Formulas.</summary>
        public int Category => AlphaCrafting.Category(Output);
    }

    /// <summary>
    /// Alpha 2013 crafting (ALPHA_CODE_STATIC / ALPHA_DECOMPILED):
    /// recipes are the items the hero has learned (creature <c>+0x1D28</c>); <c>0x59CFF0</c> turns a learned item into
    /// its station and ingredient list; <c>0x4A19D0</c> counts how many times it can be made from the bags;
    /// <c>0x4709C0</c> consumes the ingredients and adds the item; <c>0x47FAE0</c> teaches recipes on character
    /// creation and at every level; using a formula (type 2) teaches its target item (0x47D740).
    /// </summary>
    public static class AlphaCrafting
    {
        /// <summary>Station objects (Alpha world object ids checked by 0x42F910 within 4 blocks).</summary>
        public static int StationObject(AlphaCraftStation station)
        {
            switch (station)
            {
                case AlphaCraftStation.Furnace: return 0x47;
                case AlphaCraftStation.Anvil: return 0x48;
                case AlphaCraftStation.SpinningWheel: return 0x49;
                case AlphaCraftStation.Loom: return 0x4A;
                case AlphaCraftStation.Saw: return 0x4B;
                case AlphaCraftStation.Workbench: return 0x4C;
                case AlphaCraftStation.Campfire: return 0x41;
                default: return -1;
            }
        }

        /// <summary>Station search radius: squared distance below 16 (0x4325A0).</summary>
        public const double StationRadius = 4.0;

        /// <summary>0x4A161D tab of a learned item; −1 when the item has no crafting tab.</summary>
        public static int Category(AlphaItemRecord item)
        {
            switch (item.Type)
            {
                case 3: return 0;
                case 4: case 5: case 6: case 7: return 1;
                case 8: case 9: return 2;
                case 1: return item.Subtype == 1 || item.Subtype == 2 ? 4 : 3;
                case 0x0B: return 5;
                default: return -1;
            }
        }

        private static AlphaItemRecord M(int subtype, int modifier = 0, int rarity = 0) =>
            new AlphaItemRecord(0x0B, subtype, rarity: rarity, modifier: modifier, level: 1);

        /// <summary>0x59CFF0: station and ingredients of a learned item (the output is the item itself).</summary>
        public static AlphaRecipe Build(AlphaItemRecord item)
        {
            var list = new List<(AlphaItemRecord, int)>();
            var station = AlphaCraftStation.None;
            int mod = item.Modifier;
            switch (item.Type)
            {
                case 1:
                    switch (item.Subtype)
                    {
                        case 1: list.Add((M(0x16), 1)); list.Add((M(0x1A), 1)); break;
                        case 2: list.Add((M(0x17), 1)); list.Add((M(0x0C, 0x18), 1)); break;
                        case 4: list.Add((M(0x14), 1)); station = AlphaCraftStation.Campfire; break;
                        case 5: list.Add((M(0x1B), 1)); break;
                        case 6: list.Add((M(0x0F), 1)); list.Add((M(0x15), 1)); station = AlphaCraftStation.Campfire; break;
                        case 8: list.Add((M(0x11), 1)); break;
                        case 9: list.Add((M(0x10), 1)); station = AlphaCraftStation.Campfire; break;
                    }

                    break;
                case 3:
                    switch (item.Subtype)
                    {
                        case 0: case 1: case 2: case 3: case 0x0D:
                            list.Add((M(0x0A, 1), 8)); station = AlphaCraftStation.Anvil; break;
                        case 4:
                            list.Add((M(0x0A, 1), 6)); list.Add((M(0x09, 0x1B), 2)); station = AlphaCraftStation.Anvil; break;
                        case 5: case 0x0F: case 0x10: case 0x11:
                            list.Add((M(0x0A, 1), 16)); station = AlphaCraftStation.Anvil; break;
                        case 6:
                            list.Add((M(0x0A, 2), 15)); list.Add((M(0x09, 0x1A), 1)); station = AlphaCraftStation.Workbench; break;
                        case 7:
                            list.Add((M(0x0A, 1), 6)); list.Add((M(0x0A, 2), 9)); list.Add((M(0x09, 0x1A), 1));
                            station = AlphaCraftStation.Workbench; break;
                        case 8: case 0x0A: case 0x0B:
                            list.Add((M(0x0A, 2), 16)); station = AlphaCraftStation.Workbench; break;
                        case 0x0C:
                            list.Add((M(0x0A, mod), 8)); station = AlphaCraftStation.Anvil; break;
                    }

                    break;
                case 4: case 5: case 6: case 7:
                    int count = item.Type == 4 ? 20 : item.Type == 5 ? 6 : 8;
                    if (mod == 1)
                    {
                        list.Add((M(0x0A, 1), count)); station = AlphaCraftStation.Anvil;
                    }
                    else
                    {
                        list.Add((M(0x09, mod), count)); station = AlphaCraftStation.Loom;
                    }

                    break;
                case 8:
                    list.Add((M(0x0A, mod), 6));
                    break;
                case 9:
                    list.Add((M(0x0A, mod), 3));
                    break;
                case 0x0B:
                    switch (item.Subtype)
                    {
                        case 9:
                            if (mod == 0x19) list.Add((M(0x06), 1));
                            else if (mod == 0x1A) list.Add((M(0x05, 0x15), 1));
                            else if (mod == 0x1B) list.Add((M(0x0B, 0x1B), 1));
                            else break;
                            station = AlphaCraftStation.SpinningWheel;
                            break;
                        case 0x0A:
                            list.Add((M(mod == 2 ? 1 : 0, mod), 1));
                            station = mod == 2 ? AlphaCraftStation.Saw : AlphaCraftStation.Furnace;
                            break;
                        case 0x16:
                            list.Add((M(0x18), 1)); station = AlphaCraftStation.Campfire; break;
                        case 0x1A:
                            list.Add((M(0x0C, 0x18), 1)); station = AlphaCraftStation.Water; break;
                    }

                    break;
            }

            if (item.Rarity >= 1 && item.Rarity <= 4)
            {
                list.Add((M(0, 0x0C + item.Rarity, item.Rarity), 2));
            }

            return new AlphaRecipe(item, station, list);
        }

        /// <summary>0x4A19D0: how many times the bags cover every ingredient (minimum over ingredients of sum/quantity).</summary>
        public static int Craftable(AlphaRecipe recipe, HeroInventory bags)
        {
            if (recipe.Ingredients.Count == 0)
            {
                return 0;
            }

            int best = -1;
            foreach ((AlphaItemRecord item, int need) in recipe.Ingredients)
            {
                int have = CountOf(bags, item);
                int times = have / need;
                if (best < 0 || times < best) best = times;
            }

            return best;
        }

        /// <summary>Sum of bag stacks whose whole Alpha record matches (<c>Item_equals</c>).</summary>
        public static int CountOf(HeroInventory bags, AlphaItemRecord item)
        {
            int total = 0;
            for (int i = 0; i < HeroInventory.Capacity; i++)
            {
                InventoryEntry e = bags[i];
                if (e != null && !e.IsCore && e.Record.Equals(item)) total += e.Count;
            }

            return total;
        }

        /// <summary>
        /// 0x4709C0 material bonus: a crafted material other than the water flask gains +1 with 1/5, then +2 and +3 with
        /// further 1/5 rolls (the result quantity is 1 + bonus).
        /// </summary>
        public static int OutputCount(AlphaCrtRandom rng, AlphaItemRecord output)
        {
            if (output.Type != 0x0B || output.Subtype == 0x1A || rng.Next() % 5 != 0) return 1;
            if (rng.Next() % 5 != 0) return 2;
            return rng.Next() % 5 != 0 ? 3 : 4;
        }

        /// <summary>
        /// 0x47FAE0: recipes taught for <paramref name="level"/> (1 on character creation, the new level on level-up),
        /// in Alpha order; the caller skips recipes it already knows (Alpha <c>quest_hasActiveItemReq</c>).
        /// </summary>
        public static List<AlphaItemRecord> LearnedAtLevel(AlphaCrtRandom rng, int level, int alphaClass)
        {
            var learned = new List<AlphaItemRecord>();
            short l = unchecked((short)level);
            int mod4 = level % 4;
            int mod5 = level % 5;
            if (mod4 == 1) learned.Add(new AlphaItemRecord(1, 1, level: l));
            if (mod4 == 3) learned.Add(new AlphaItemRecord(1, 2, level: l));
            if (mod5 == 1) { learned.Add(new AlphaItemRecord(1, 4, level: l)); learned.Add(new AlphaItemRecord(1, 8, level: l)); }
            if (mod5 == 2) learned.Add(new AlphaItemRecord(1, 9, level: l));
            if (mod5 == 3) learned.Add(new AlphaItemRecord(1, 6, level: l));
            if (mod5 == 4) learned.Add(new AlphaItemRecord(1, 5, level: l));
            if (level == 1)
            {
                learned.Add(new AlphaItemRecord(0x0B, 0x1A, level: 1));
                learned.Add(new AlphaItemRecord(0x0B, 0x16, level: 1));
                if (alphaClass == 4) learned.Add(new AlphaItemRecord(0x0B, 9, modifier: 0x1B, level: 1));
                if (alphaClass == 2) learned.Add(new AlphaItemRecord(0x0B, 9, modifier: 0x1A, level: 1));
                if (alphaClass == 3) learned.Add(new AlphaItemRecord(0x0B, 9, modifier: 0x19, level: 1));
                foreach (int m in new[] { 1, 2, 0x0B, 0x0C }) learned.Add(new AlphaItemRecord(0x0B, 0x0A, modifier: m, level: 1));
            }

            int armourType = mod4 == 0 ? 7 : mod4 == 1 ? 4 : mod4 == 2 ? 6 : 5;
            int seed = rng.Next() % 11;
            int classMod = alphaClass == 1 ? 1 : alphaClass == 2 ? 0x1A : alphaClass == 3 ? 0x19 : alphaClass == 4 ? 0x1B : 0;
            learned.Add(new AlphaItemRecord(armourType, 0, seed, 0, 0, classMod, l));

            seed = rng.Next() % 11;
            int mod3 = level % 3;
            int sub = 0, wmod = 1;
            switch (alphaClass)
            {
                case 1:
                    sub = mod3 == 0 ? 0x0F + rng.Next() % 3 : mod3 == 1 ? rng.Next() % 3 : 0x0D;
                    break;
                case 2:
                    wmod = 2;
                    sub = mod3 == 0 ? 6 : mod3 == 1 ? 7 : 8;
                    break;
                case 3:
                    wmod = 2;
                    if (mod3 == 0) sub = 0x0A;
                    else if (mod3 == 1) sub = 0x0B;
                    else { sub = 0x0C; wmod = rng.Next() % 2 == 0 ? 0x0B : 0x0C; }
                    break;
                case 4:
                    sub = mod3 == 0 ? 3 : mod3 == 1 ? 5 : 4;
                    break;
            }

            learned.Add(new AlphaItemRecord(3, sub, seed, 0, 0, wmod, l));
            return learned;
        }

        /// <summary>0x47D740: the item a formula teaches (type from <c>+0x08</c>, which is then cleared).</summary>
        public static AlphaItemRecord FormulaTarget(AlphaItemRecord formula) =>
            new AlphaItemRecord(formula.Variant, formula.Subtype, formula.Seed, 0, formula.Rarity, formula.Modifier, formula.Level);
    }
}
