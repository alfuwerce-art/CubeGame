// CubeGame.Presentation.Visual — hero loadout → character visual descriptor (D4).
//
// The gameplay side (Claude, M0-04) publishes EquipmentVisualChanged with raw Alpha item ids; this mapping
// turns such an event into the descriptor AlphaPlayerVisualBuilder consumes, so armour, weapons and the
// off-hand appear on the hero model. Alpha evidence: item type 3 = weapon (subtype = Alpha weapon subtype,
// item_icon_id picks the model), types 4/5/6 = chest/hands/feet armour (W-19 armour models), other weapon
// subtypes are the off-hand class.
//
// Nothing is substituted silently: a slot this layer cannot show (neck, rings, lamp, pet, back) or an item
// without a staged model returns a problem string and leaves the descriptor unchanged.
using System;
using System.IO;
using CubeGame.Content;
using CubeGame.Features.Contracts;

namespace CubeGame.Presentation.Visual
{
    /// <summary>Pure mapping of equipment events to the character visual descriptor.</summary>
    public static class HeroLoadout
    {
        public const string SlotNotImplemented = "SLOT_NOT_IMPLEMENTED";
        public const string ItemTypeMismatch = "ITEM_TYPE_MISMATCH";
        public const string ModelNotStaged = "MODEL_NOT_STAGED";

        /// <summary>
        /// Applies one equipment change. Returns false and a problem when the slot or the item cannot be shown;
        /// <paramref name="request"/> is then returned unchanged in <paramref name="updated"/>.
        /// </summary>
        public static bool TryApply(CharacterVisualRequest request, VisualEquipmentSlot slot, RawAlphaItemId? item,
            string itemsDirectory, out CharacterVisualRequest updated, out string problem)
        {
            updated = request;
            problem = string.Empty;
            switch (slot)
            {
                case VisualEquipmentSlot.MainHand:
                    if (item == null)
                    {
                        updated.WeaponSubtype = -1;
                        updated.WeaponModelPath = null;
                        return true;
                    }

                    if (item.Value.Type != 3)
                    {
                        problem = $"{ItemTypeMismatch} MainHand type {item.Value.Type}";
                        return false;
                    }

                    if (!TryWeaponModel(item.Value.Subtype, itemsDirectory, out string weaponPath, out problem)) return false;
                    updated.WeaponSubtype = item.Value.Subtype;
                    updated.WeaponModelPath = weaponPath;
                    return true;

                case VisualEquipmentSlot.OffHand:
                    if (item == null)
                    {
                        updated.OffhandSubtype = -1;
                        updated.OffhandModelPath = null;
                        return true;
                    }

                    if (!TryWeaponModel(item.Value.Subtype, itemsDirectory, out string offhandPath, out problem)) return false;
                    updated.OffhandSubtype = item.Value.Subtype;
                    updated.OffhandModelPath = offhandPath;
                    return true;

                case VisualEquipmentSlot.Chest:
                    if (!TryArmour(4, item, itemsDirectory, out int chest, out problem)) return false;
                    updated.ChestSubtype = chest;
                    return true;

                case VisualEquipmentSlot.Hands:
                    if (!TryArmour(5, item, itemsDirectory, out int hands, out problem)) return false;
                    updated.HandsSubtype = hands;
                    return true;

                case VisualEquipmentSlot.Feet:
                    if (!TryArmour(6, item, itemsDirectory, out int feet, out problem)) return false;
                    updated.FeetSubtype = feet;
                    return true;

                default:
                    problem = $"{SlotNotImplemented} {slot}";
                    return false;
            }
        }

        /// <summary>Staged file of an Alpha model name ("iron-sword1.cub" → "&lt;items&gt;/iron-sword1.cubdec").</summary>
        public static string ModelFile(string itemsDirectory, string alphaModelName)
        {
            if (string.IsNullOrEmpty(itemsDirectory) || string.IsNullOrEmpty(alphaModelName)) return null;
            string name = alphaModelName.EndsWith(".cub", StringComparison.OrdinalIgnoreCase)
                ? alphaModelName.Substring(0, alphaModelName.Length - 4) + ".cubdec"
                : alphaModelName;
            return Path.Combine(itemsDirectory, name);
        }

        static bool TryWeaponModel(int subtype, string itemsDirectory, out string path, out string problem)
        {
            path = null;
            problem = string.Empty;
            if (!AlphaItemRules.TryWeaponSubtype(subtype, out AlphaWeaponSubtypeDef def) || string.IsNullOrEmpty(def.DefaultModel))
            {
                problem = $"{ModelNotStaged} weapon subtype {subtype}";
                return false;
            }

            path = ModelFile(itemsDirectory, def.DefaultModel);
            if (path == null)
            {
                problem = $"{ModelNotStaged} items directory missing for subtype {subtype}";
                return false;
            }

            return true;
        }

        static bool TryArmour(int alphaItemType, RawAlphaItemId? item, string itemsDirectory, out int subtype,
            out string problem)
        {
            subtype = -1;
            problem = string.Empty;
            if (item == null) return true;
            if (item.Value.Type != alphaItemType)
            {
                problem = $"{ItemTypeMismatch} armour type {item.Value.Type} != {alphaItemType}";
                return false;
            }

            if (!AlphaEquipmentCatalog.TryModel(alphaItemType, item.Value.Subtype, out AlphaEquipmentDef def))
            {
                problem = $"{ModelNotStaged} armour {alphaItemType}/{item.Value.Subtype}";
                return false;
            }

            if (ModelFile(itemsDirectory, def.Model) == null)
            {
                problem = $"{ModelNotStaged} items directory missing for armour {alphaItemType}/{item.Value.Subtype}";
                return false;
            }

            subtype = item.Value.Subtype;
            return true;
        }
    }
}
