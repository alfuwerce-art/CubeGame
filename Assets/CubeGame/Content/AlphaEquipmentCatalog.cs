// CubeGame.Content — armour equipment models (chest/hands/feet/shoulder) from item_icon_id.
//
// Slot laws come from item_layer evidence: chest swaps the body block, hands swap hand_l/hand_r,
// feet swap foot_l/foot_r (all "center"), shoulder is a separate shoulder_item block that the prepared
// humanoid rig does not have — so the shoulder type is reported, not guessed.
using System.Collections.Generic;

namespace CubeGame.Content
{
    public enum EquipmentSlot
    {
        None = 0,
        Chest = 1,
        Hands = 2,
        Feet = 3,
        Shoulder = 4,
    }

    public static class AlphaEquipmentCatalog
    {
        public static IReadOnlyList<AlphaEquipmentDef> Items => AlphaEquipmentModels.Items;

        public static bool TryModel(int type, int subtype, out AlphaEquipmentDef def)
        {
            foreach (var item in AlphaEquipmentModels.Items)
                if (item.Type == type && item.Subtype == subtype) { def = item; return true; }
            def = default;
            return false;
        }

        public static EquipmentSlot SlotOf(int type)
        {
            switch (type)
            {
                case 4: return EquipmentSlot.Chest;
                case 5: return EquipmentSlot.Hands;
                case 6: return EquipmentSlot.Feet;
                case 7: return EquipmentSlot.Shoulder;
                default: return EquipmentSlot.None;
            }
        }

        public static List<string> Validate()
        {
            var p = new List<string>();
            if (AlphaEquipmentModels.Items.Length != 32) p.Add($"EQUIPMENT_ITEMS {AlphaEquipmentModels.Items.Length} != 32");
            foreach (var def in new[]
                     {
                         (4, 0, "iron-chest1.cub"), (4, 5, "silk-chest5.cub"), (4, 7, "wool-chest5.cub"),
                         (5, 0, "iron-hand1.cub"), (6, 3, "iron-foot4.cub"), (7, 6, "linen-shoulder5.cub"),
                     })
            {
                if (!TryModel(def.Item1, def.Item2, out var found) || found.Model != def.Item3)
                    p.Add($"EQUIPMENT_MODEL {def.Item1}/{def.Item2}");
            }
            if (SlotOf(4) != EquipmentSlot.Chest || SlotOf(5) != EquipmentSlot.Hands ||
                SlotOf(6) != EquipmentSlot.Feet || SlotOf(7) != EquipmentSlot.Shoulder || SlotOf(1) != EquipmentSlot.None)
                p.Add("EQUIPMENT_SLOT_LAW");
            var seen = new HashSet<string>();
            foreach (var item in AlphaEquipmentModels.Items)
                if (!seen.Add(item.Type + "/" + item.Subtype)) p.Add($"EQUIPMENT_DUPLICATE {item.Type}/{item.Subtype}");
            return p;
        }

        public static string Summary() =>
            $"equipmentItems={AlphaEquipmentModels.Items.Length} slots=4 tiers=8 selector={AlphaEquipmentModels.Selector}";
    }
}
