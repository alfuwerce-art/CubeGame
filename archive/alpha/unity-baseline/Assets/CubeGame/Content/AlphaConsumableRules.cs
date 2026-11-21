// CubeGame.Content — evidenced consumable-use rules (D86 M0-04B, part of B-U1).
//
// item_layer evidence (executed Alpha code) gives the consumable block, use actions and cooldowns:
//   block held_item; actions {80, 81}; itemCooldown(type, subtype) =
//   type 1 && subtype 1 -> 3000 ms, other type-1 items -> 10000 ms, every other item type -> 0.
// The heal/effect amount is NOT recovered — it stays with the game's item layer (B-U1 open).
using System.Collections.Generic;

namespace CubeGame.Content
{
    public static class AlphaConsumableRules
    {
        /// <summary>Alpha item type that uses the held_item block and the use actions.</summary>
        public const int ConsumableType = 1;
        public const string Block = "held_item";
        public const string Status = "EVIDENCED_COOLDOWN: heal/effect amount is NOT recovered (B-U1 open)";

        /// <summary>Alpha action ids started when a consumable is used (item_layer consumable.actions).</summary>
        public static readonly int[] UseActions = { 80, 81 };

        /// <summary>itemCooldown(type, subtype) from the executed item layer; 0 means no cooldown block.</summary>
        public static int CooldownMs(int type, int subtype)
        {
            if (type != ConsumableType) return 0;
            return subtype == 1 ? 3000 : 10000;
        }

        public static bool IsConsumable(int type) => type == ConsumableType;

        public static List<string> Validate()
        {
            var p = new List<string>();
            if (UseActions.Length != 2 || UseActions[0] != 80 || UseActions[1] != 81)
                p.Add("CONSUMABLE_ACTIONS");
            if (CooldownMs(1, 1) != 3000) p.Add("CONSUMABLE_LIFE_POTION_COOLDOWN");
            if (CooldownMs(1, 2) != 10000) p.Add("CONSUMABLE_OTHER_COOLDOWN");
            if (CooldownMs(1, 0) != 10000) p.Add("CONSUMABLE_SUBTYPE0_COOLDOWN");
            if (CooldownMs(3, 0) != 0) p.Add("CONSUMABLE_WEAPON_COOLDOWN");
            if (!IsConsumable(1) || IsConsumable(3)) p.Add("CONSUMABLE_TYPE");
            if (!AlphaContentDatabase.TryItemType(ConsumableType, out var def) || def.Name != "Consumable")
                p.Add("CONSUMABLE_TYPE_TABLE");
            return p;
        }

        public static string Summary() =>
            $"consumables type={ConsumableType} actions=80,81 cooldownLifePotion=3000 other=10000 status={Status}";
    }
}
