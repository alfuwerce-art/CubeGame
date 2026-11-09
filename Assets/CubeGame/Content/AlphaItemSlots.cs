// CubeGame.Content — which Alpha item type goes to which equipment/draw slot (D86 M0-04B).
//
// Evidence: item_layer slots (chest/feet/hands/shoulder/neck/offhand/weapon/lamp/special/pet/held/ring_a/b
// with their block+law) and the 24-entry item type table. Types that are not equipped (material, coins,
// food, formulas, leftovers, painting, vase, candle, mana cube) are mapped to None instead of being guessed
// into a slot; quest items have no recovered draw slot and stay None with a note.
using System.Collections.Generic;

namespace CubeGame.Content
{
    public enum AlphaItemSlot
    {
        None = 0,
        Held = 1,
        Weapon = 2,
        Offhand = 3,
        Chest = 4,
        Hands = 5,
        Feet = 6,
        Shoulder = 7,
        Neck = 8,
        Ring = 9,
        Pet = 10,
        Special = 11,
        Lamp = 12,
    }

    public static class AlphaItemSlotRules
    {
        public const string EvidenceStatus =
            "SLOT_LAW_FROM_ITEM_LAYER: block+law per slot; item types from the 24-entry table";

        /// <summary>Slot for an item type; false when the type has no recovered slot (unknown/not drawn).</summary>
        public static bool TrySlot(int type, out AlphaItemSlot slot)
        {
            switch (type)
            {
                case 1: slot = AlphaItemSlot.Held; return true;      // consumables -> held_item
                case 3: slot = AlphaItemSlot.Weapon; return true;    // weapons and off-hand share type 3
                case 4: slot = AlphaItemSlot.Chest; return true;
                case 5: slot = AlphaItemSlot.Hands; return true;
                case 6: slot = AlphaItemSlot.Feet; return true;
                case 7: slot = AlphaItemSlot.Shoulder; return true;
                case 8: slot = AlphaItemSlot.Neck; return true;
                case 9: slot = AlphaItemSlot.Ring; return true;      // stored but never drawn (A.6)
                case 19: slot = AlphaItemSlot.Pet; return true;
                case 23: slot = AlphaItemSlot.Special; return true;  // hang glider / boat
                case 24: slot = AlphaItemSlot.Lamp; return true;
                default: slot = AlphaItemSlot.None; return false;
            }
        }

        /// <summary>Alpha attachment law of the slot block (empty when there is no block).</summary>
        public static string LawOf(AlphaItemSlot slot)
        {
            switch (slot)
            {
                case AlphaItemSlot.Held: return "center";       // held_item
                case AlphaItemSlot.Weapon: return "item_attach.weapon_present_law";
                case AlphaItemSlot.Offhand: return "item_attach.offhand_present_law";
                case AlphaItemSlot.Chest:
                case AlphaItemSlot.Hands:
                case AlphaItemSlot.Feet:
                case AlphaItemSlot.Shoulder: return "center";
                case AlphaItemSlot.Neck: return "neck";
                case AlphaItemSlot.Pet: return "pet";
                case AlphaItemSlot.Special: return "center";
                case AlphaItemSlot.Lamp: return "lamp";
                default: return "";
            }
        }

        /// <summary>Alpha blocks that are stored but never drawn (rings) are reported as not drawn.</summary>
        public static bool IsDrawn(AlphaItemSlot slot) => slot != AlphaItemSlot.Ring && slot != AlphaItemSlot.None;

        public static List<string> Validate()
        {
            var p = new List<string>();
            int mapped = 0;
            foreach (var type in AlphaContentTables.ItemTypes)
            {
                if (TrySlot(type.Id, out var slot))
                {
                    mapped++;
                    if (slot == AlphaItemSlot.None) p.Add($"SLOT_NONE_FOR_MAPPED {type.Id}");
                }
                else if (type.Id == 9) p.Add("SLOT_RING_NOT_MAPPED");
            }
            if (mapped != 11) p.Add($"SLOT_MAPPED_TYPES {mapped} != 11");
            if (AlphaContentTables.ItemTypes.Length != 24) p.Add($"SLOT_ITEM_TYPES {AlphaContentTables.ItemTypes.Length} != 24");
            if (TrySlot(3, out var weapon) == false || weapon != AlphaItemSlot.Weapon) p.Add("SLOT_WEAPON");
            if (!TrySlot(9, out var ring) || ring != AlphaItemSlot.Ring || IsDrawn(ring)) p.Add("SLOT_RING");
            if (!TrySlot(24, out var lamp) || LawOf(lamp) != "lamp") p.Add("SLOT_LAMP");
            if (!TrySlot(23, out var special) || LawOf(special) != "center") p.Add("SLOT_SPECIAL");
            if (TrySlot(11, out var material) || LawOf(material).Length != 0) p.Add("SLOT_MATERIAL_NOT_EQUIPPED");
            return p;
        }

        public static string Summary()
        {
            int mapped = 0;
            foreach (var type in AlphaContentTables.ItemTypes) if (TrySlot(type.Id, out _)) mapped++;
            return $"itemSlots mappedTypes={mapped} totalTypes={AlphaContentTables.ItemTypes.Length} status={EvidenceStatus}";
        }
    }
}
