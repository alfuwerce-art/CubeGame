// CubeGame.Presentation.Visual — assembles the player's visual from a character descriptor
// (D86 M0-03C/M0-04B). Claude's character creation decides race/class/equipment; this builder turns
// that decision into one AlphaActorView with the Alpha hair tint, weapon and off-hand layers applied,
// and reports every refusal instead of silently substituting a model.
using System.Collections.Generic;
using CubeGame.AlphaModels;
using CubeGame.AlphaTransfer;
using CubeGame.Content;
using CubeGame.Features.Creatures;
using UnityEngine;

namespace CubeGame.Presentation.Visual
{
    public struct CharacterVisualRequest
    {
        /// <summary>Alpha kind of the playable race (0x02 HumanMale by default).</summary>
        public int Kind;
        public bool HasHairColour;
        public Color HairColour;
        /// <summary>Alpha weapon subtype (0..25) or -1 for no weapon.</summary>
        public int WeaponSubtype;
        public string WeaponModelPath;
        /// <summary>Alpha off-hand class subtype or -1 for none.</summary>
        public int OffhandSubtype;
        public string OffhandModelPath;
        /// <summary>Armour subtypes (0..7) or -1: chest swaps body, hands swap both hands, feet swap both feet.</summary>
        public int ChestSubtype;
        public int HandsSubtype;
        public int FeetSubtype;
        /// <summary>Held item (Alpha type/subtype, e.g. 1/1 life potion) or -1.</summary>
        public int HeldType;
        public int HeldSubtype;

        /// <summary>Descriptor with every optional slot disabled (use this instead of `default`).</summary>
        public static CharacterVisualRequest Default => new CharacterVisualRequest
        {
            Kind = 0x02, WeaponSubtype = -1, OffhandSubtype = -1,
            ChestSubtype = -1, HandsSubtype = -1, FeetSubtype = -1, HeldType = -1, HeldSubtype = -1,
        };
    }

    public sealed class CharacterVisualResult
    {
        public AlphaActorView View;
        public readonly List<string> Problems = new List<string>();
        public bool Ok => View != null && View.Ready && Problems.Count == 0;
        public string Status => Ok ? "READY " + (View != null ? View.ActorId : "") : "PROBLEMS " + string.Join(",", Problems);
    }

    public static class AlphaPlayerVisualBuilder
    {
        public static CharacterVisualResult Build(CharacterVisualRequest request, Transform parent, AlphaActorLibrary library,
            string itemsDirectory = null)
        {
            var result = new CharacterVisualResult();
            if (library == null)
            {
                result.Problems.Add("NO_ACTOR_LIBRARY");
                return result;
            }
            if (!CreatureVisualCatalog.TryResolve(request.Kind, library, out var actorId, out var reason))
            {
                result.Problems.Add(reason);
                return result;
            }
            if (!library.TryGet(actorId, out var def) || !def.Ready)
            {
                result.Problems.Add("ACTOR_NOT_READY " + actorId);
                return result;
            }
            var view = AlphaActorView.Spawn(def, parent, result.Problems);
            result.View = view;
            if (view == null || !view.Ready) return result;

            if (request.HasHairColour && !AlphaAppearance.TrySetHairColour(view.Rig, request.HairColour, out var hairReason))
                result.Problems.Add(hairReason);

            if (request.WeaponSubtype >= 0)
            {
                if (string.IsNullOrEmpty(request.WeaponModelPath))
                    result.Problems.Add("WEAPON_MODEL_PATH_MISSING");
                else if (!AlphaWeaponLayer.TryEquipFromFile(view.Rig, request.WeaponModelPath, request.WeaponSubtype, out var weaponReason))
                    result.Problems.Add(weaponReason);
            }

            if (request.OffhandSubtype >= 0)
            {
                if (string.IsNullOrEmpty(request.OffhandModelPath))
                    result.Problems.Add("OFFHAND_MODEL_PATH_MISSING");
                else if (!AlphaOffhandLayer.TryEquipFromFile(view.Rig, request.OffhandModelPath, request.OffhandSubtype, out var offhandReason))
                    result.Problems.Add(offhandReason);
            }

            ApplyArmour(result, view, itemsDirectory, 4, request.ChestSubtype);
            ApplyArmour(result, view, itemsDirectory, 5, request.HandsSubtype);
            ApplyArmour(result, view, itemsDirectory, 6, request.FeetSubtype);

            if (request.HeldType >= 0)
            {
                if (string.IsNullOrEmpty(itemsDirectory)) result.Problems.Add("EQUIPMENT_ITEMS_DIRECTORY_MISSING");
                else if (!AlphaHeldItemLayer.TryEquipFromItem(view.Rig, itemsDirectory, request.HeldType,
                             request.HeldSubtype, out var heldReason))
                    result.Problems.Add(heldReason);
            }
            return result;
        }

        static void ApplyArmour(CharacterVisualResult result, AlphaActorView view, string itemsDirectory, int type, int subtype)
        {
            if (subtype < 0) return;
            if (string.IsNullOrEmpty(itemsDirectory))
            {
                result.Problems.Add("EQUIPMENT_ITEMS_DIRECTORY_MISSING");
                return;
            }
            if (!AlphaEquipmentLayer.TryEquipFromItem(view.Rig, itemsDirectory, type, subtype, out var reason))
                result.Problems.Add(reason);
        }

        /// <summary>Check a descriptor against the staged content without building anything.</summary>
        public static List<string> Validate(CharacterVisualRequest request, AlphaActorLibrary library)
        {
            var p = new List<string>();
            if (!CreatureVisualCatalog.TryResolve(request.Kind, library, out _, out var reason)) p.Add(reason);
            if (request.HasHairColour &&
                (request.HairColour.r < 0f || request.HairColour.r > 1f || request.HairColour.g < 0f ||
                 request.HairColour.g > 1f || request.HairColour.b < 0f || request.HairColour.b > 1f))
                p.Add("HAIR_COLOUR_OUT_OF_RANGE");
            if (request.WeaponSubtype >= 0)
            {
                if (!AlphaItemRules.TryWeaponAnchors(request.WeaponSubtype, out var set) || !AlphaItemRules.IsAnchorMeasured(set.InHand))
                    p.Add($"WEAPON_SUBTYPE_NOT_EQUIPPABLE {request.WeaponSubtype}");
                if (string.IsNullOrEmpty(request.WeaponModelPath)) p.Add("WEAPON_MODEL_PATH_MISSING");
            }
            if (request.OffhandSubtype >= 0)
            {
                if (!AlphaItemRules.TryOffhandAnchors(request.OffhandSubtype, out var set) || !AlphaItemRules.IsAnchorMeasured(set.InHand))
                    p.Add($"OFFHAND_SUBTYPE_NOT_EQUIPPABLE {request.OffhandSubtype}");
                if (string.IsNullOrEmpty(request.OffhandModelPath)) p.Add("OFFHAND_MODEL_PATH_MISSING");
            }
            var armours = new (int Type, int Subtype)[]
            {
                (4, request.ChestSubtype), (5, request.HandsSubtype), (6, request.FeetSubtype),
            };
            foreach (var armour in armours)
            {
                if (armour.Subtype < 0) continue;
                if (!AlphaEquipmentCatalog.TryModel(armour.Type, armour.Subtype, out _))
                    p.Add($"EQUIPMENT_SUBTYPE_NOT_AVAILABLE {armour.Type}/{armour.Subtype}");
            }
            if (request.HeldType >= 0 && !AlphaHeldItemCatalog.TryModel(request.HeldType, request.HeldSubtype, out _))
                p.Add($"HELD_ITEM_NO_MODEL {request.HeldType}/{request.HeldSubtype}");
            return p;
        }
    }
}
