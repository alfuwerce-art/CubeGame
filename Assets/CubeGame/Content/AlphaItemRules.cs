// CubeGame.Content — item rules that the visual layer must obey (D86 M0-04B, M0-00B item layer).
//
// These are the reviewed Alpha item facts: 13 equipment storage slots, 20 weapon subtypes with
// their two-handed/dual flags, 101 item-dependent actions and the consumable action codes.
// The visual layer may READ them; item ownership, stats, stacking and transactions stay in the
// game domain (Claude's Features/Items), never here.
using System;
using System.Collections.Generic;

namespace CubeGame.Content
{
    public static class AlphaItemRules
    {
        /// <summary>Action codes of the consumable use actions (Alpha consumables.actions).</summary>
        public static bool IsConsumableAction(int action) =>
            action == AlphaContentTables.ConsumableActionFirst || action == AlphaContentTables.ConsumableActionSecond;

        public static bool TrySlot(string nameOrOffset, out AlphaItemSlotDef slot) =>
            AlphaContentDatabase.TryItemSlot(nameOrOffset, out slot);

        public static bool TryWeaponSubtype(int subtype, out AlphaWeaponSubtypeDef def) =>
            AlphaContentDatabase.TryWeaponSubtype(subtype, out def);

        public static bool IsTwoHanded(int subtype) =>
            AlphaContentDatabase.TryWeaponSubtype(subtype, out var def) && def.TwoHanded;

        public static bool IsDualCapable(int subtype) =>
            AlphaContentDatabase.TryWeaponSubtype(subtype, out var def) && def.DualCapable;

        public static bool TryAction(int action, out AlphaItemActionDef def) =>
            AlphaContentDatabase.TryItemAction(action, out def);

        /// <summary>Action draws the main-hand weapon in the hand (otherwise it is on the back).</summary>
        public static bool WeaponInHand(int action) =>
            AlphaContentDatabase.TryItemAction(action, out var def) && def.Weapon == "IN_HAND";

        public static bool OffhandInHand(int action) =>
            AlphaContentDatabase.TryItemAction(action, out var def) && def.Offhand == "IN_HAND";

        public static List<int> TwoHandedSubtypes()
        {
            var list = new List<int>();
            foreach (var w in AlphaContentTables.WeaponSubtypes) if (w.TwoHanded) list.Add(w.Id);
            return list;
        }

        /// <summary>Slots Alpha actually draws; the rest is storage only (rings) or unproven.</summary>
        public static List<AlphaItemSlotDef> RenderedSlots()
        {
            var list = new List<AlphaItemSlotDef>();
            foreach (var s in AlphaContentTables.ItemSlots) if (s.Rendered) list.Add(s);
            return list;
        }

        public static List<string> Validate()
        {
            var p = new List<string>();
            if (AlphaContentTables.ItemSlots.Length != 13 && AlphaContentTables.ItemSlots.Length != 14)
                p.Add($"ITEM_SLOT_COUNT {AlphaContentTables.ItemSlots.Length}");
            if (!IsConsumableAction(80) || !IsConsumableAction(81)) p.Add("ITEM_CONSUMABLE_ACTIONS");
            if (TwoHandedSubtypes().Count == 0) p.Add("ITEM_NO_TWO_HANDED_SUBTYPE");
            foreach (var s in AlphaContentTables.ItemSlots)
                if (s.Rendered && string.IsNullOrEmpty(s.AnchorLaw)) p.Add($"ITEM_RENDERED_SLOT_NO_LAW {s.Name}");
            foreach (var a in AlphaContentTables.ItemActions)
            {
                if (a.Id <= 0) p.Add($"ITEM_ACTION_ID {a.Id}");
                if (a.Weapon != "IN_HAND" && a.Weapon != "ON_BACK" && a.Weapon != "NONE" && !string.IsNullOrEmpty(a.Weapon))
                    p.Add($"ITEM_ACTION_WEAPON_STATE {a.Id} {a.Weapon}");
            }
            return p;
        }
    }
}
