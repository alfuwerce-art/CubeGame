// CubeGame.Content — Alpha content registries (D86 M0-01, M0-04/M0-06 data side).
//
// The game never reads Alpha executables or databases at runtime. This module is the single
// in-repo source of the *numbers* distilled from reviewed Alpha evidence (ids, names, routes,
// slots, actions, creature kinds): tools/alpha_content/build_content_tables.py writes
// Generated/AlphaContentTables.g.cs from the committed evidence JSON. Geometry and clips stay
// in the local content cache outside Git (see CubeGame.AlphaModels.AlphaContentSource).
using System;
using System.Collections.Generic;
using System.Text;

namespace CubeGame.Content
{
    /// <summary>Lookup/validation facade over the generated Alpha content tables.</summary>
    public static class AlphaContentDatabase
    {
        public const string Format = AlphaContentTables.Format;

        // Counts the reviewed evidence guarantees; a drift means the generator or the evidence
        // changed, and every consumer of these tables must be re-checked before it is trusted.
        public const int ExpectedItemTypes = 24;
        public const int ExpectedWeaponSubtypes = 20;
        public const int ExpectedAbilities = 16;
        public const int ExpectedCreatureKinds = 156;
        public const int ExpectedItemActions = 101;

        static readonly Dictionary<int, AlphaItemTypeDef> ItemTypeById = new Dictionary<int, AlphaItemTypeDef>();
        static readonly Dictionary<int, AlphaWeaponSubtypeDef> WeaponBySubtype = new Dictionary<int, AlphaWeaponSubtypeDef>();
        static readonly Dictionary<int, AlphaAbilityDef> AbilityById = new Dictionary<int, AlphaAbilityDef>();
        static readonly Dictionary<int, AlphaRegionTypeDef> RegionByType = new Dictionary<int, AlphaRegionTypeDef>();
        static readonly Dictionary<int, AlphaCreatureKindDef> KindById = new Dictionary<int, AlphaCreatureKindDef>();
        static readonly Dictionary<string, AlphaItemSlotDef> SlotByName = new Dictionary<string, AlphaItemSlotDef>(StringComparer.Ordinal);
        static readonly Dictionary<string, AlphaItemSlotDef> SlotByOffset = new Dictionary<string, AlphaItemSlotDef>(StringComparer.Ordinal);
        static readonly Dictionary<int, AlphaItemActionDef> ItemActionById = new Dictionary<int, AlphaItemActionDef>();

        static AlphaContentDatabase()
        {
            foreach (var t in AlphaContentTables.ItemTypes) ItemTypeById[t.Id] = t;
            foreach (var w in AlphaContentTables.WeaponSubtypes) WeaponBySubtype[w.Id] = w;
            foreach (var a in AlphaContentTables.Abilities) AbilityById[a.Id] = a;
            foreach (var r in AlphaContentTables.RegionTypes) RegionByType[r.Type] = r;
            foreach (var k in AlphaContentTables.CreatureKinds) KindById[k.Kind] = k;
            foreach (var s in AlphaContentTables.ItemSlots)
            {
                if (!string.IsNullOrEmpty(s.Name)) SlotByName[s.Name] = s;
                SlotByOffset[s.Offset] = s;
            }
            foreach (var a in AlphaContentTables.ItemActions) ItemActionById[a.Id] = a;
        }

        public static IReadOnlyList<AlphaItemTypeDef> ItemTypes => AlphaContentTables.ItemTypes;
        public static IReadOnlyList<AlphaWeaponSubtypeDef> WeaponSubtypes => AlphaContentTables.WeaponSubtypes;
        public static IReadOnlyList<AlphaAbilityDef> Abilities => AlphaContentTables.Abilities;
        public static IReadOnlyList<AlphaRegionTypeDef> RegionTypes => AlphaContentTables.RegionTypes;
        public static IReadOnlyList<AlphaCreatureKindDef> CreatureKinds => AlphaContentTables.CreatureKinds;
        public static IReadOnlyList<AlphaItemSlotDef> ItemSlots => AlphaContentTables.ItemSlots;
        public static IReadOnlyList<AlphaItemActionDef> ItemActions => AlphaContentTables.ItemActions;

        public static bool TryItemType(int id, out AlphaItemTypeDef def) => ItemTypeById.TryGetValue(id, out def);
        public static bool TryWeaponSubtype(int subtype, out AlphaWeaponSubtypeDef def) => WeaponBySubtype.TryGetValue(subtype, out def);
        public static bool TryAbility(int id, out AlphaAbilityDef def) => AbilityById.TryGetValue(id, out def);
        public static bool TryRegionType(int type, out AlphaRegionTypeDef def) => RegionByType.TryGetValue(type, out def);
        public static bool TryCreatureKind(int kind, out AlphaCreatureKindDef def) => KindById.TryGetValue(kind, out def);
        public static bool TryItemSlot(string nameOrOffset, out AlphaItemSlotDef def)
        {
            if (nameOrOffset == null) { def = default; return false; }
            return SlotByName.TryGetValue(nameOrOffset, out def) || SlotByOffset.TryGetValue(nameOrOffset, out def);
        }
        public static bool TryItemAction(int id, out AlphaItemActionDef def) => ItemActionById.TryGetValue(id, out def);

        /// <summary>Structural validation of the committed tables; empty list means the tables are usable.</summary>
        public static List<string> Validate()
        {
            var p = new List<string>();
            if (AlphaContentTables.ItemTypes.Length != ExpectedItemTypes)
                p.Add($"CONTENT_ITEM_TYPES {AlphaContentTables.ItemTypes.Length} != {ExpectedItemTypes}");
            if (AlphaContentTables.WeaponSubtypes.Length != ExpectedWeaponSubtypes)
                p.Add($"CONTENT_WEAPON_SUBTYPES {AlphaContentTables.WeaponSubtypes.Length} != {ExpectedWeaponSubtypes}");
            if (AlphaContentTables.Abilities.Length != ExpectedAbilities)
                p.Add($"CONTENT_ABILITIES {AlphaContentTables.Abilities.Length} != {ExpectedAbilities}");
            if (AlphaContentTables.CreatureKinds.Length != ExpectedCreatureKinds)
                p.Add($"CONTENT_CREATURE_KINDS {AlphaContentTables.CreatureKinds.Length} != {ExpectedCreatureKinds}");
            if (AlphaContentTables.ItemActions.Length != ExpectedItemActions)
                p.Add($"CONTENT_ITEM_ACTIONS {AlphaContentTables.ItemActions.Length} != {ExpectedItemActions}");
            if (AlphaContentTables.ItemSlots.Length < 13)
                p.Add($"CONTENT_ITEM_SLOTS {AlphaContentTables.ItemSlots.Length} < 13");
            if (ItemTypeById.Count != AlphaContentTables.ItemTypes.Length) p.Add("CONTENT_DUPLICATE_ITEM_TYPE");
            if (WeaponBySubtype.Count != AlphaContentTables.WeaponSubtypes.Length) p.Add("CONTENT_DUPLICATE_WEAPON_SUBTYPE");
            if (AbilityById.Count != AlphaContentTables.Abilities.Length) p.Add("CONTENT_DUPLICATE_ABILITY");
            if (KindById.Count != AlphaContentTables.CreatureKinds.Length) p.Add("CONTENT_DUPLICATE_KIND");
            if (ItemActionById.Count != AlphaContentTables.ItemActions.Length) p.Add("CONTENT_DUPLICATE_ITEM_ACTION");

            foreach (var t in AlphaContentTables.ItemTypes)
                if (t.Id <= 0 || string.IsNullOrEmpty(t.Name)) p.Add($"CONTENT_ITEM_TYPE_INCOMPLETE {t.Id} '{t.Name}'");
            foreach (var w in AlphaContentTables.WeaponSubtypes)
                if (w.Id < 0 || string.IsNullOrEmpty(w.Name)) p.Add($"CONTENT_WEAPON_SUBTYPE_INCOMPLETE {w.Id} '{w.Name}'");
            foreach (var k in AlphaContentTables.CreatureKinds)
            {
                if (k.Parts == null || k.Parts.Length == 0) p.Add($"CONTENT_KIND_NO_PARTS 0x{k.Kind:x2}");
                if (k.Status != "ORACLE_VERIFIED" && k.Status != "FORMULA_VERIFIED" && k.Status != "UNKNOWN")
                    p.Add($"CONTENT_KIND_STATUS 0x{k.Kind:x2} {k.Status}");
                if (k.Passive && string.IsNullOrEmpty(k.PassiveBasis)) p.Add($"CONTENT_PASSIVE_WITHOUT_BASIS 0x{k.Kind:x2}");
            }
            bool anyRendered = false;
            foreach (var s in AlphaContentTables.ItemSlots) anyRendered |= s.Rendered;
            if (!anyRendered) p.Add("CONTENT_NO_RENDERED_SLOT");
            return p;
        }

        public static string Summary()
        {
            var kinds = AlphaContentTables.CreatureKinds;
            int passive = 0, combat = 0;
            foreach (var k in kinds) { if (k.Passive) passive++; else combat++; }
            var sb = new StringBuilder();
            sb.Append("format=").Append(Format)
              .Append(" itemTypes=").Append(AlphaContentTables.ItemTypes.Length)
              .Append(" weaponSubtypes=").Append(AlphaContentTables.WeaponSubtypes.Length)
              .Append(" abilities=").Append(AlphaContentTables.Abilities.Length)
              .Append(" regions=").Append(AlphaContentTables.RegionTypes.Length)
              .Append(" kinds=").Append(kinds.Length).Append(" passive=").Append(passive).Append(" combat=").Append(combat)
              .Append(" slots=").Append(AlphaContentTables.ItemSlots.Length)
              .Append(" itemActions=").Append(AlphaContentTables.ItemActions.Length)
              .Append(" weaponAnchors=").Append(AlphaContentTables.WeaponAnchors.Length)
              .Append(" offhandAnchors=").Append(AlphaContentTables.OffhandAnchors.Length);
            return sb.ToString();
        }
    }
}
