// CubeGame.Features.Creatures — which Alpha actions require a weapon in hand (D2, R-2 data side).
//
// Alpha's item-action table marks the actions that are launched only with a weapon (`item_W_*` rows of
// item_layer_evidence: AlphaItemActionDef.WeaponOnly, action set [1, 5, 14, 19, 22, 23, …] per the S05B-2
// disposition matrix). Those actions exist in an actor's library, but a creature without a weapon must never
// launch them — that is the data rule this file exposes to the AI and to gameplay.
//
// What is NOT proven here: which weapon subtype a given NPC holds (R-1: the world spawn record that assigns
// class/weapon/specialisation is not executed), and the per-subtype action variant choice beyond C-29's
// `combat_selectAttackVariant`. So the rule is "weapon in hand or not", never an invented subtype mapping.
using System.Collections.Generic;
using CubeGame.Content;
using CubeGame.Features.Contracts;

namespace CubeGame.Features.Creatures
{
    /// <summary>Weapon-gated Alpha actions and the "may this loadout launch it" rule.</summary>
    public static class AlphaNpcWeaponActions
    {
        public const string Status =
            "ALPHA_DATA: weapon-only actions from the item-action table; R-1 (which NPC holds which weapon) NOT executed";

        /// <summary>Raw Alpha item type of a weapon.</summary>
        public const int WeaponItemType = 3;

        static readonly List<int> WeaponOnlyList = BuildWeaponOnly();
        static readonly HashSet<int> WeaponOnlySet = new HashSet<int>(WeaponOnlyList);

        /// <summary>Action ids that the item table launches only with a weapon in hand.</summary>
        public static IReadOnlyList<int> WeaponOnly => WeaponOnlyList;

        public static bool IsWeaponOnly(int action) => WeaponOnlySet.Contains(action);

        /// <summary>True when the creature has a weapon in its main hand (Alpha item type 3).</summary>
        public static bool HasWeapon(CreatureState state) =>
            state != null && state.WeaponItemType == WeaponItemType;

        /// <summary>
        /// May this action be launched with the current loadout? Weapon-only actions need a weapon; everything
        /// else is unaffected by this rule (other gates belong to the AI and to gameplay).
        /// </summary>
        public static bool IsAllowed(int action, bool hasWeapon) => !IsWeaponOnly(action) || hasWeapon;

        public static bool IsAllowed(int action, CreatureState state) => IsAllowed(action, HasWeapon(state));

        /// <summary>Data check: the table really contains weapon-only actions (no hand-written list).</summary>
        public static List<string> Validate()
        {
            var problems = new List<string>();
            if (WeaponOnlyList.Count == 0) problems.Add("WEAPON_ONLY_NONE");
            foreach (int action in WeaponOnlyList)
            {
                if (!AlphaItemRules.TryAction(action, out AlphaItemActionDef def))
                {
                    problems.Add($"WEAPON_ONLY_NOT_IN_TABLE {action}");
                    continue;
                }

                if (!def.WeaponOnly) problems.Add($"WEAPON_ONLY_FLAG_MISMATCH {action}");
            }

            return problems;
        }

        static List<int> BuildWeaponOnly()
        {
            var list = new List<int>();
            foreach (AlphaItemActionDef def in AlphaContentTables.ItemActions)
            {
                if (def.WeaponOnly) list.Add(def.Id);
            }

            list.Sort();
            return list;
        }
    }
}
