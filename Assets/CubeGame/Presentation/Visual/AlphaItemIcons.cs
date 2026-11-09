// CubeGame.Presentation.Visual — icon lookup of Alpha items (D6, UI data).
//
// Alpha ships one icon atlas whose entries are keyed by the item's model name (the same staged names the world
// uses: "life-potion", "iron-sword1" …). Consumables/held items have a proven model name in the item layer
// (AlphaHeldItemDef.Model, item_icon_id selector 0x004EC400). Weapons and armour pick their model by
// material/rarity, and that mapping (item_icon_id per material) is NOT recovered — they are refused here
// instead of receiving a guessed icon.
using CubeGame.Content;
using CubeGame.Features.Contracts;

namespace CubeGame.Presentation.Visual
{
    /// <summary>Alpha item id → atlas entry name (held items only; the rest stays UNKNOWN).</summary>
    public static class AlphaItemIcons
    {
        public const string Status =
            "ALPHA_DATA: model/icon names of held items from the item layer (0x004EC400); weapon/armour icon by material UNKNOWN";

        /// <summary>Atlas entry name of a model file ("life-potion.cub" → "life-potion").</summary>
        public static string EntryOf(string alphaModelName)
        {
            if (string.IsNullOrEmpty(alphaModelName)) return null;
            return alphaModelName.EndsWith(".cub", System.StringComparison.OrdinalIgnoreCase)
                ? alphaModelName.Substring(0, alphaModelName.Length - 4)
                : alphaModelName;
        }

        /// <summary>Atlas entry of a raw Alpha item; false when the item has no proven model name.</summary>
        public static bool TryEntry(RawAlphaItemId item, out string entry)
        {
            entry = null;
            if (AlphaHeldItemCatalog.TryModel(item.Type, item.Subtype, out AlphaHeldItemDef held))
            {
                entry = EntryOf(held.Model);
                return !string.IsNullOrEmpty(entry);
            }

            return false;
        }

        /// <summary>Sprite of the item from a loaded atlas, or null with a reason.</summary>
        public static UnityEngine.Sprite TryGetSprite(AlphaIconAtlas atlas, RawAlphaItemId item, out string reason)
        {
            reason = string.Empty;
            if (atlas == null)
            {
                reason = "NO_ICON_ATLAS";
                return null;
            }

            if (!TryEntry(item, out string entry))
            {
                reason = $"ICON_UNKNOWN {item} ({Status})";
                return null;
            }

            UnityEngine.Sprite sprite = atlas.TryGetSprite(entry);
            if (sprite == null) reason = "ICON_NOT_IN_ATLAS " + entry;
            return sprite;
        }
    }
}
