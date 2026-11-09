// CubeGame.Content — held-item visuals: which model an item draws in the hand (D86 M0-04B).
//
// The mapping comes from executing Alpha's item_icon_id (0x004EC400) over the item domains; only the
// items whose model is a held/attached object are listed (consumables 1/*, hang glider/boat 23/*,
// lantern 24/*). What the item DOES (heal, cooldown) stays in the game's item layer.
using System.Collections.Generic;

namespace CubeGame.Content
{
    public static class AlphaHeldItemCatalog
    {
        public static IReadOnlyList<AlphaHeldItemDef> Items => AlphaHeldItemModels.Items;

        /// <summary>Exact (type, subtype) match first, then a wildcard entry with subtype -1 (lantern).</summary>
        public static bool TryModel(int type, int subtype, out AlphaHeldItemDef def)
        {
            AlphaHeldItemDef wildcard = default;
            bool hasWildcard = false;
            foreach (var item in AlphaHeldItemModels.Items)
            {
                if (item.Type != type) continue;
                if (item.Subtype == subtype) { def = item; return true; }
                if (item.Subtype == -1) { wildcard = item; hasWildcard = true; }
            }
            def = wildcard;
            return hasWildcard;
        }

        /// <summary>Model file name for an item, or null when Alpha draws no held model for it.</summary>
        public static string ModelOf(int type, int subtype) =>
            TryModel(type, subtype, out var def) ? def.Model : null;

        public static List<string> Validate()
        {
            var p = new List<string>();
            if (AlphaHeldItemModels.Items.Length < 14)
                p.Add($"HELD_ITEMS {AlphaHeldItemModels.Items.Length} < 14");
            if (string.IsNullOrEmpty(AlphaHeldItemModels.Selector) || !AlphaHeldItemModels.Selector.Contains("004EC400"))
                p.Add("HELD_ITEM_SELECTOR");
            int consumables = 0, glider = 0, boat = 0, lamp = 0;
            var seen = new HashSet<string>();
            foreach (var item in AlphaHeldItemModels.Items)
            {
                if (string.IsNullOrEmpty(item.Model)) p.Add($"HELD_ITEM_MODEL 0x{item.Type:x2}/{item.Subtype}");
                if (!seen.Add(item.Type + "/" + item.Subtype)) p.Add($"HELD_ITEM_DUPLICATE {item.Type}/{item.Subtype}");
                if (item.Type == 1) consumables++;
                if (item.Type == 23 && item.Subtype == 0) glider++;
                if (item.Type == 23 && item.Subtype == 1) boat++;
                if (item.Type == 24) lamp++;
                if (!AlphaContentDatabase.TryItemType(item.Type, out _)) p.Add($"HELD_ITEM_TYPE 0x{item.Type:x2}");
            }
            if (consumables != 11) p.Add($"HELD_CONSUMABLES {consumables}");
            if (glider != 1 || boat != 1 || lamp != 1) p.Add($"HELD_SPECIALS glider={glider} boat={boat} lamp={lamp}");
            if (ModelOf(1, 1) != "life-potion.cub") p.Add("HELD_LIFE_POTION");
            if (ModelOf(24, 7) != "lantern02.cub") p.Add("HELD_LANTERN_WILDCARD");
            return p;
        }

        public static string Summary() =>
            $"heldItems={AlphaHeldItemModels.Items.Length} consumables=11 glider=1 boat=1 lamp=1 selector={AlphaHeldItemModels.Selector}";
    }
}
