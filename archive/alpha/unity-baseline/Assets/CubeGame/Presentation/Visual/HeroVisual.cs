// CubeGame.Presentation.Visual — the one runtime path from the hero's state to its visual (M0-F2A).
//
//   HeroState (appearance + equipped items)
//     → HeroVisualComposer.Compose          (pure, deterministic; this file)
//     → HeroVisualSpec.ToRequest            (descriptor with resolved model files)
//     → AlphaPlayerVisualBuilder.Build      (rig + part swaps + item layers)
//     → GameVisualSink                      (the only owner of the hero view in the game scene)
//
// Evidence per decision (see docs/reports/M0_F2A_CHARACTER_RUNTIME_REPORT.md):
//   * species = race/gender (C1 0x42C080), face/haircut model = registry index of switch table 0x42C41C →
//     asset-catalog registry loaders (ALPHA_CODE_STATIC); hair tint = hair_rgb/255 (D61);
//   * shared humanoid skeleton: Creature_drawModel poses every humanoid block from the species appearance
//     block (offsets, scales). Species whose block equals the action rig species (0x02) except head/weapon
//     scale draw exactly with its poses, so the hero uses the full 0x02 action library with the race's body,
//     hands and feet models, head/hair scaled by ScaleHead and the weapon by ScaleWeapon (ALPHA_CODE_STATIC
//     constants, EMULATED_ALPHA_CODE poses). A species that differs elsewhere (0x0C OrcFemale: offset_head,
//     scale_body) keeps its own actor and is reported PARTIAL;
//   * item model = executed item_icon_id selector (material/rarity/seed) — EMULATED_ALPHA_CODE;
//   * slots: drawModel blocks of the prepared humanoid rig (weapon, off-hand, body, hands, feet). Shoulder,
//     neck, lamp, glider and pet blocks are not in the rig (PARTIAL); rings are never drawn by Alpha.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CubeGame.Content;
using CubeGame.Features.Contracts;
using CubeGame.Features.Session;
using UnityEngine;

namespace CubeGame.Presentation.Visual
{
    /// <summary>Bytes of an item that decide its look (Alpha +0x00 type, +0x01 subtype, +0x0D material, +0x0C rarity, +0x04 seed).</summary>
    public readonly struct HeroVisualItem : IEquatable<HeroVisualItem>
    {
        public HeroVisualItem(int type, int subtype, int material = 0, int rarity = 0, int seed = 0)
        {
            Type = type;
            Subtype = subtype;
            Material = material;
            Rarity = rarity;
            Seed = seed;
        }

        public int Type { get; }
        public int Subtype { get; }
        public int Material { get; }
        public int Rarity { get; }
        public int Seed { get; }

        public static HeroVisualItem? FromEvent(EquipmentVisualChanged e) =>
            e.Item.HasValue ? new HeroVisualItem(e.Item.Value.Type, e.Item.Value.Subtype, e.Material, e.Rarity, e.Seed) : (HeroVisualItem?)null;

        public bool Equals(HeroVisualItem o) =>
            Type == o.Type && Subtype == o.Subtype && Material == o.Material && Rarity == o.Rarity && Seed == o.Seed;

        public override bool Equals(object obj) => obj is HeroVisualItem o && Equals(o);

        public override int GetHashCode() => ((Type * 64 + Subtype) * 64 + Material) * 8 + Rarity ^ Seed * 397;

        public override string ToString() => $"{Type}/{Subtype} m{Material} r{Rarity} s{Seed}";
    }

    /// <summary>How a weapon class is held/drawn (Alpha weapon subtype classes, item_layer evidence).</summary>
    public enum HeroWeaponGrip
    {
        None = 0,
        OneHanded,
        TwoHanded,
        Bow,
        Crossbow,
        Boomerang,
        Staff,
        Wand,
        Bracelet,
        Shield,
        Quiver,
        Tool,
        Torch,
        Arrow,
        Unknown,
    }

    /// <summary>Resolved, deterministic description of the hero visual. Equal state ⇒ equal <see cref="Signature"/>.</summary>
    public sealed class HeroVisualSpec
    {
        public HeroAppearance Appearance;
        public int Kind;
        /// <summary>Species whose rig and action clips draw the hero (0x02 for the shared humanoid skeleton).</summary>
        public int RigKind;
        public bool SharedActionRig;
        public string HeadModel;
        public string HairModel;
        public bool HeadMarkerTransparent;
        public bool HairMarkerTransparent;
        /// <summary>ScaleHead(kind) / ScaleHead(rig kind): head and hair blocks.</summary>
        public float HeadScale = 1f;
        /// <summary>ScaleWeapon(kind) / ScaleWeapon(rig kind).</summary>
        public float WeaponScale = 1f;
        public Color HairColour = Color.white;

        public HeroVisualItem? MainHand, OffHand, Chest, Hands, Feet;
        public string WeaponModel;
        public int WeaponSubtype = -1;
        public HeroWeaponGrip WeaponGrip;
        public string OffhandModel;
        public int OffhandSubtype = -1;
        public HeroWeaponGrip OffhandGrip;
        public bool DualWield;
        public string ChestModel, HandsModel, FeetModel;

        /// <summary>What could not be shown and why (never silently replaced).</summary>
        public readonly List<string> Problems = new List<string>();

        public string Signature
        {
            get
            {
                var sb = new StringBuilder();
                sb.Append("kind=").Append(Kind.ToString("x2")).Append(" rig=").Append(RigKind.ToString("x2"))
                    .Append(" head=").Append(HeadModel).Append(" hair=").Append(HairModel)
                    .Append(" hs=").Append(HeadScale.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).Append(" ws=").Append(WeaponScale.ToString("R", System.Globalization.CultureInfo.InvariantCulture))
                    .Append(" rgb=").Append(Appearance.HairR).Append(',').Append(Appearance.HairG).Append(',').Append(Appearance.HairB)
                    .Append(" main=").Append(WeaponModel).Append('#').Append(WeaponSubtype)
                    .Append(" off=").Append(OffhandModel).Append('#').Append(OffhandSubtype)
                    .Append(" chest=").Append(ChestModel).Append(" hands=").Append(HandsModel).Append(" feet=").Append(FeetModel);
                return sb.ToString();
            }
        }

        /// <summary>Descriptor for <see cref="AlphaPlayerVisualBuilder"/> with files from the local content cache.</summary>
        public CharacterVisualRequest ToRequest(string contentRoot)
        {
            string items = contentRoot != null ? Path.Combine(contentRoot, "items") : null;
            string appearance = contentRoot != null ? Path.Combine(contentRoot, HeroVisualComposer.AppearanceFolder) : null;
            CharacterVisualRequest r = CharacterVisualRequest.Default;
            r.Kind = Kind;
            r.RigKind = RigKind;
            r.RacePartsKind = SharedActionRig && RigKind != Kind ? Kind : (int?)null;
            r.HeadModelPath = File(appearance, HeadModel);
            r.HairModelPath = File(appearance, HairModel);
            r.HeadMarkerTransparent = HeadMarkerTransparent;
            r.HairMarkerTransparent = HairMarkerTransparent;
            r.HeadScale = HeadScale;
            r.HasHairColour = true;
            r.HairColour = HairColour;
            r.HideCapturedWeaponWhenEmpty = true;
            r.WeaponScale = WeaponScale;
            if (WeaponModel != null)
            {
                r.WeaponSubtype = WeaponSubtype;
                r.WeaponModelPath = File(items, WeaponModel);
            }

            if (OffhandModel != null)
            {
                r.OffhandSubtype = OffhandSubtype;
                r.OffhandModelPath = File(items, OffhandModel);
            }

            r.ChestModelPath = File(items, ChestModel);
            r.HandsModelPath = File(items, HandsModel);
            r.FeetModelPath = File(items, FeetModel);
            if (r.ChestModelPath != null) r.ChestSubtype = Chest?.Subtype ?? 0;
            if (r.HandsModelPath != null) r.HandsSubtype = Hands?.Subtype ?? 0;
            if (r.FeetModelPath != null) r.FeetSubtype = Feet?.Subtype ?? 0;
            return r;
        }

        static string File(string dir, string model) =>
            dir == null || string.IsNullOrEmpty(model) ? null : Path.Combine(dir, AlphaCharacterModels.PayloadName(model));
    }

    /// <summary>Pure composition of the hero visual from the saved character state.</summary>
    public static class HeroVisualComposer
    {
        /// <summary>Species of the action rig (humanoid with the full hero action library and the weapon track).</summary>
        public const int ActionRigKind = 0x02;

        /// <summary>Folder of the local content cache that holds head/hair payloads (staged by gen_character_models.py).</summary>
        public const string AppearanceFolder = "appearance";

        public const string SlotNoRigBlock = "SLOT_NO_RIG_BLOCK";
        public const string SlotNotDrawnByAlpha = "SLOT_NOT_DRAWN_BY_ALPHA";

        public static HeroVisualSpec Compose(HeroAppearance appearance, IReadOnlyDictionary<VisualEquipmentSlot, HeroVisualItem> equipped)
        {
            var spec = new HeroVisualSpec();
            HeroAppearance a = AlphaHeroCreation.Normalize(appearance);
            spec.Appearance = a;
            spec.Kind = AlphaHeroCreation.KindOf(a).Value;
            spec.HairColour = new Color(a.HairR / 255f, a.HairG / 255f, a.HairB / 255f, 1f);

            spec.SharedActionRig = SharesActionRig(spec.Kind, out string rigReason);
            spec.RigKind = spec.SharedActionRig ? ActionRigKind : spec.Kind;
            if (!spec.SharedActionRig) spec.Problems.Add(rigReason);
            if (AlphaCharacterModels.SpeciesAppearance.TryGetValue(spec.Kind, out AlphaSpeciesAppearanceDef species) &&
                AlphaCharacterModels.SpeciesAppearance.TryGetValue(spec.RigKind, out AlphaSpeciesAppearanceDef rig))
            {
                spec.HeadScale = species.ScaleHead / rig.ScaleHead;
                spec.WeaponScale = species.ScaleWeapon / rig.ScaleWeapon;
            }

            int head = AlphaHeroCreation.HeadModel(a);
            int hair = AlphaHeroCreation.HairModel(a);
            if (AlphaCharacterModels.TryAppearanceModel(head, out AlphaAppearanceModelDef headDef))
            {
                spec.HeadModel = headDef.Model;
                spec.HeadMarkerTransparent = headDef.MarkerTransparent;
            }
            else
            {
                spec.Problems.Add($"HEAD_MODEL_UNKNOWN registry 0x{head:X}");
            }

            if (AlphaCharacterModels.TryAppearanceModel(hair, out AlphaAppearanceModelDef hairDef))
            {
                spec.HairModel = hairDef.Model;
                spec.HairMarkerTransparent = hairDef.MarkerTransparent;
            }
            else
            {
                spec.Problems.Add($"HAIR_MODEL_UNKNOWN registry 0x{hair:X}");
            }

            if (equipped != null)
            {
                // Fixed slot order: the result never depends on the order the events arrived in.
                foreach (VisualEquipmentSlot slot in SlotOrder)
                {
                    if (equipped.TryGetValue(slot, out HeroVisualItem item)) Apply(spec, slot, item);
                }
            }

            if (spec.WeaponModel != null && spec.OffhandModel != null &&
                spec.OffhandGrip != HeroWeaponGrip.Shield && spec.OffhandGrip != HeroWeaponGrip.Quiver)
            {
                spec.DualWield = true;
            }

            if (spec.WeaponModel != null && !spec.SharedActionRig)
            {
                spec.Problems.Add($"WEAPON_TRACK_MISSING kind 0x{spec.Kind:x2}: the species actor has no weapon block");
            }

            return spec;
        }

        static readonly VisualEquipmentSlot[] SlotOrder =
        {
            VisualEquipmentSlot.MainHand, VisualEquipmentSlot.OffHand, VisualEquipmentSlot.Chest, VisualEquipmentSlot.Hands,
            VisualEquipmentSlot.Feet, VisualEquipmentSlot.Shoulders, VisualEquipmentSlot.Neck, VisualEquipmentSlot.Ring1,
            VisualEquipmentSlot.Ring2, VisualEquipmentSlot.Lamp, VisualEquipmentSlot.Pet, VisualEquipmentSlot.Special,
            VisualEquipmentSlot.Back,
        };

        static void Apply(HeroVisualSpec spec, VisualEquipmentSlot slot, HeroVisualItem item)
        {
            switch (slot)
            {
                case VisualEquipmentSlot.MainHand:
                    if (!Expect(spec, slot, item, 3)) return;
                    if (!Select(spec, slot, item, out string weapon)) return;
                    spec.MainHand = item;
                    spec.WeaponModel = weapon;
                    spec.WeaponSubtype = item.Subtype;
                    spec.WeaponGrip = GripOf(item.Subtype);
                    return;
                case VisualEquipmentSlot.OffHand:
                    if (!Expect(spec, slot, item, 3)) return;
                    if (!AlphaItemRules.TryOffhandAnchors(item.Subtype, out _))
                    {
                        spec.Problems.Add($"OFFHAND_CLASS_UNKNOWN subtype {item.Subtype}");
                        return;
                    }

                    if (!Select(spec, slot, item, out string offhand)) return;
                    spec.OffHand = item;
                    spec.OffhandModel = offhand;
                    spec.OffhandSubtype = item.Subtype;
                    spec.OffhandGrip = GripOf(item.Subtype);
                    return;
                case VisualEquipmentSlot.Chest:
                    if (Expect(spec, slot, item, 4) && Select(spec, slot, item, out string chest))
                    {
                        spec.Chest = item;
                        spec.ChestModel = chest;
                    }

                    return;
                case VisualEquipmentSlot.Hands:
                    if (Expect(spec, slot, item, 5) && Select(spec, slot, item, out string hands))
                    {
                        spec.Hands = item;
                        spec.HandsModel = hands;
                    }

                    return;
                case VisualEquipmentSlot.Feet:
                    if (Expect(spec, slot, item, 6) && Select(spec, slot, item, out string feet))
                    {
                        spec.Feet = item;
                        spec.FeetModel = feet;
                    }

                    return;
                case VisualEquipmentSlot.Ring1:
                case VisualEquipmentSlot.Ring2:
                    // Creature_drawModel reads the ring slots (+0xBC0/+0xCD8) but draws no block for them.
                    return;
                default:
                    spec.Problems.Add($"{SlotNoRigBlock} {slot} item {item}");
                    return;
            }
        }

        static bool Expect(HeroVisualSpec spec, VisualEquipmentSlot slot, HeroVisualItem item, int type)
        {
            if (item.Type == type) return true;
            spec.Problems.Add($"ITEM_TYPE_MISMATCH {slot} type {item.Type} != {type}");
            return false;
        }

        static bool Select(HeroVisualSpec spec, VisualEquipmentSlot slot, HeroVisualItem item, out string model)
        {
            if (AlphaCharacterModels.TrySelectItemModel(item.Type, item.Subtype, item.Material, item.Rarity, item.Seed,
                    out model, out string problem))
            {
                return true;
            }

            spec.Problems.Add($"{slot}: {problem}");
            return false;
        }

        /// <summary>
        /// The species draws exactly with the poses of the action rig species: Creature_drawModel reads the same
        /// appearance fields for every humanoid block, so equal flags, offsets and body/hand/foot scales mean equal
        /// part matrices (head/weapon scale are applied separately).
        /// </summary>
        public static bool SharesActionRig(int kind, out string reason)
        {
            reason = null;
            if (!AlphaCharacterModels.SpeciesAppearance.TryGetValue(kind, out AlphaSpeciesAppearanceDef s) ||
                !AlphaCharacterModels.SpeciesAppearance.TryGetValue(ActionRigKind, out AlphaSpeciesAppearanceDef r))
            {
                reason = $"SPECIES_APPEARANCE_UNKNOWN 0x{kind:x2}";
                return false;
            }

            var diff = new List<string>();
            if (s.Flags != r.Flags) diff.Add("flags");
            if (s.ScaleBody != r.ScaleBody) diff.Add("scale_body");
            if (s.ScaleHand != r.ScaleHand) diff.Add("scale_hand");
            if (s.ScaleFoot != r.ScaleFoot) diff.Add("scale_foot");
            if (!Same(s.OffsetBody, r.OffsetBody)) diff.Add("offset_body");
            if (!Same(s.OffsetHead, r.OffsetHead)) diff.Add("offset_head");
            if (!Same(s.OffsetHand, r.OffsetHand)) diff.Add("offset_hand");
            if (!Same(s.OffsetFoot, r.OffsetFoot)) diff.Add("offset_foot");
            if (diff.Count == 0) return true;
            reason = $"SPECIES_POSE_DIFFERS 0x{kind:x2} vs 0x{ActionRigKind:x2}: {string.Join(",", diff)} (own actor, no weapon track)";
            return false;
        }

        static bool Same(float[] a, float[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        /// <summary>Weapon class of an Alpha weapon subtype (AlphaContentTables weapon subtypes, item_layer).</summary>
        public static HeroWeaponGrip GripOf(int subtype)
        {
            if (AlphaItemRules.TryWeaponSubtype(subtype, out AlphaWeaponSubtypeDef def) && def.TwoHanded) return HeroWeaponGrip.TwoHanded;
            switch (subtype)
            {
                case 0: case 1: case 2: case 3: case 4: case 5: return HeroWeaponGrip.OneHanded;
                case 6: return HeroWeaponGrip.Bow;
                case 7: return HeroWeaponGrip.Crossbow;
                case 8: return HeroWeaponGrip.Boomerang;
                case 9: return HeroWeaponGrip.Arrow;
                case 10: return HeroWeaponGrip.Staff;
                case 11: return HeroWeaponGrip.Wand;
                case 12: return HeroWeaponGrip.Bracelet;
                case 13: return HeroWeaponGrip.Shield;
                case 14: return HeroWeaponGrip.Quiver;
                case 18: case 19: return HeroWeaponGrip.Tool;
                case 20: return HeroWeaponGrip.Torch;
                default: return subtype >= 21 && subtype <= 25 ? HeroWeaponGrip.OneHanded : HeroWeaponGrip.Unknown;
            }
        }
    }
}
