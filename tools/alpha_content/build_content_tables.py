"""D86 M0-01: build the committed Alpha content tables used by the Unity game.

The game must not read Alpha files at runtime: the *numbers* (ids, names, routes, action
lists) are distilled here from the already reviewed evidence files, while the *assets*
(geometry, clips) stay in the local content cache outside Git.

Inputs (all reviewed, committed, with their own provenance):

* ``tools/m0_transfer/data/alpha_confirmed_tables.json`` — 24 item types, 20 weapon subtypes,
  16 abilities, region name table, inventory/consumable/slot rules (M0-00 package).
* ``tools/alpha_models/catalog/walk_oracle_evidence.json`` — 156 creature kinds and their parts.
* ``tools/alpha_models/catalog/spawn_role_evidence.json`` — 26 passive objects and their faction.
* ``tools/alpha_models/universal/npc_action_evidence.json`` — per-kind motion family and the
  actions the AI trigger launches.
* ``tools/alpha_models/catalog/item_layer_evidence.json`` — 13 storage slots, weapon/offhand
  classes with anchors, 101 item-dependent actions.

Output: ``Assets/CubeGame/Content/Generated/AlphaContentTables.g.cs`` (committed).

    python -m tools.alpha_content.build_content_tables            # write
    python -m tools.alpha_content.build_content_tables --check    # verify no drift
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUTPUT = ROOT / "Assets/CubeGame/Content/Generated/AlphaContentTables.g.cs"

TABLES = ROOT / "tools/m0_transfer/data/alpha_confirmed_tables.json"
WALK = ROOT / "tools/alpha_models/catalog/walk_oracle_evidence.json"
SPAWN = ROOT / "tools/alpha_models/catalog/spawn_role_evidence.json"
NPC = ROOT / "tools/alpha_models/universal/npc_action_evidence.json"
ITEM = ROOT / "tools/alpha_models/catalog/item_layer_evidence.json"

EVIDENCE = {"DIRECT": "Direct", "PARTIAL": "Partial", "UNKNOWN": "Unknown",
            "BLOCKED": "Blocked", "PRODUCER_RULE_D67": "ProducerRule",
            "PRODUCER_RULE": "ProducerRule", "CODE_DERIVED_MEASURED": "CodeDerived",
            "VERIFIED_EMULATION": "VerifiedEmulation"}


def load(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def cs(value) -> str:
    """C# string literal; non-ASCII characters are escaped so the file stays ASCII."""
    if value is None:
        return "null"
    out = []
    for ch in str(value):
        if ch == "\\":
            out.append("\\\\")
        elif ch == '"':
            out.append('\\"')
        elif ch == "\n":
            out.append("\\n")
        elif ch == "\t":
            out.append("\\t")
        elif 32 <= ord(ch) < 127:
            out.append(ch)
        else:
            out.append("\\u%04x" % ord(ch))
    return '"' + "".join(out) + '"'


def evidence(value) -> str:
    if not value:
        return "AlphaEvidence.Unknown"
    return "AlphaEvidence." + EVIDENCE.get(str(value), "Unknown")


def int_array(values) -> str:
    values = list(values)
    if not values:
        return "new int[0]"
    return "new[] { " + ", ".join(str(int(v)) for v in values) + " }"


def str_array(values) -> str:
    values = list(values)
    if not values:
        return "new string[0]"
    return "new[] { " + ", ".join(cs(v) for v in values) + " }"


def kind_int(key: str) -> int:
    return int(str(key), 16)


def kind_hex(value: int) -> str:
    return "0x%02x" % value


def build() -> tuple[str, dict]:
    tables = load(TABLES)
    walk = load(WALK)
    spawn = load(SPAWN)
    npc = load(NPC)
    item = load(ITEM)["contract"]

    # --- item types -------------------------------------------------------
    item_types = []
    for t in tables["itemTypes"]:
        item_types.append((int(t["id"]), t.get("name", ""), t.get("confidence"),
                           t.get("model") or "", t.get("note") or ""))
    item_types.sort()

    # --- weapon / offhand classes ----------------------------------------
    weapon_classes = {int(w["subtype"]): w for w in item["weaponClasses"] if "subtype" in w}
    weapon_types = []
    for w in tables["weaponSubtypes"]:
        wid = int(w["id"])
        cls = weapon_classes.get(wid, {})
        weapon_types.append((wid, w.get("name", ""), w.get("model") or "",
                             bool(cls.get("twoHanded", False)), bool(cls.get("dualCapable", False)),
                             cls.get("status") or w.get("confidence")))
    weapon_types.sort()

    # --- abilities and their stamina costs -------------------------------
    stamina = {}
    for row in tables["abilityCosts"].get("stamina", []):
        for aid in row.get("ids", []):
            stamina[int(aid)] = int(row["value"])
    default_stamina = next((int(r["value"]) for r in tables["abilityCosts"].get("stamina", [])
                            if not r.get("ids")), 0)
    abilities = []
    for a in tables["abilities"]:
        aid = int(a["id"])
        abilities.append((aid, a.get("name", ""), stamina.get(aid, default_stamina)))
    abilities.sort()

    # --- region types -----------------------------------------------------
    regions = [(int(r["type"]), r["name"]) for r in tables["regions"]["nameTable"]]
    ruins = tables["regions"].get("ruins") or {}
    if ruins:
        regions.append((int(ruins["type"]), ruins.get("name", "Ruins")))
    regions.sort()

    # --- 156 creature kinds ----------------------------------------------
    passive = {kind_int(k): v for k, v in spawn.get("passive_kinds", {}).items()}
    kinds = []
    for key, row in walk["kinds"].items():
        kid = int(row.get("kind", key), 16) if str(row.get("kind", key)).startswith("0x") else kind_int(key)
        npc_row = npc.get("kinds", {}).get(kind_hex(kid), {})
        passive_row = passive.get(kid, {})
        launched = sorted(int(a) for a in (npc_row.get("launched_actions") or {}).keys())
        parts = sorted(row.get("parts") or [])
        kinds.append({
            "kind": kid,
            "status": row.get("status", "UNKNOWN"),
            "parts": parts,
            "family": npc_row.get("family_id") or "",
            "actions": launched,
            "passive": kid in passive,
            "faction": int(passive_row["faction"]) if passive_row.get("faction") is not None else -1,
            "passiveBasis": passive_row.get("basis") or "",
            "nameHint": npc_row.get("name_hint") or "",
        })
    kinds.sort(key=lambda k: k["kind"])
    seen = [k["kind"] for k in kinds]
    assert len(seen) == len(set(seen)), "duplicate kind ids"

    # --- 13 equipment storage slots + the held-item block ------------------
    slots = []
    for s in item["slots"]:
        block = s.get("block") or ""
        law = s.get("law") or ""
        rendered = block not in ("", "NOT_DRAWN") and bool(law)
        slots.append((str(s.get("offset")), s.get("name", ""), [int(t) for t in s.get("itemTypes", [])],
                      block, law, s.get("status", ""), rendered))
    # the storage order is the Alpha offset order (0x418 .. 0x1020)
    slots.sort(key=lambda s: int(s[0], 16))

    # --- 101 item-dependent actions --------------------------------------
    actions = []
    for a in item["actions"]:
        actions.append((int(a["id"]), int(a.get("state", -1)), a.get("weapon", ""),
                        a.get("offhand", ""), bool(a.get("heldItem", False)),
                        bool(a.get("petItem", False)), bool(a.get("weaponOnly", False)),
                        bool(a.get("twoHandedChain", False))))
    actions.sort()

    lines = []
    lines.append("// <auto-generated>")
    lines.append("// Generated by tools/alpha_content/build_content_tables.py from reviewed evidence files.")
    lines.append("// Do not edit by hand: change the evidence JSON (with its provenance) and rebuild.")
    for path in (TABLES, WALK, SPAWN, NPC, ITEM):
        lines.append(f"//   {path.relative_to(ROOT).as_posix()} sha256={sha(path)}")
    lines.append("// </auto-generated>")
    lines.append("namespace CubeGame.Content")
    lines.append("{")
    lines.append("    /// <summary>Evidence status of one Alpha fact (mirrors the review vocabulary).</summary>")
    lines.append("    public enum AlphaEvidence { Unknown = 0, Direct, Partial, Blocked, ProducerRule, CodeDerived, VerifiedEmulation }")
    lines.append("")
    lines.append("    public readonly struct AlphaItemTypeDef")
    lines.append("    {")
    lines.append("        public readonly int Id; public readonly string Name; public readonly AlphaEvidence Evidence;")
    lines.append("        public readonly string DefaultModel; public readonly string Note;")
    lines.append("        public AlphaItemTypeDef(int id, string name, AlphaEvidence evidence, string model, string note)")
    lines.append("        { Id = id; Name = name; Evidence = evidence; DefaultModel = model; Note = note; }")
    lines.append("    }")
    lines.append("")
    lines.append("    public readonly struct AlphaWeaponSubtypeDef")
    lines.append("    {")
    lines.append("        public readonly int Id; public readonly string Name; public readonly string DefaultModel;")
    lines.append("        public readonly bool TwoHanded; public readonly bool DualCapable; public readonly AlphaEvidence Evidence;")
    lines.append("        public AlphaWeaponSubtypeDef(int id, string name, string model, bool twoHanded, bool dualCapable, AlphaEvidence evidence)")
    lines.append("        { Id = id; Name = name; DefaultModel = model; TwoHanded = twoHanded; DualCapable = dualCapable; Evidence = evidence; }")
    lines.append("    }")
    lines.append("")
    lines.append("    public readonly struct AlphaAbilityDef")
    lines.append("    {")
    lines.append("        public readonly int Id; public readonly string Name; public readonly int StaminaCost;")
    lines.append("        public AlphaAbilityDef(int id, string name, int staminaCost) { Id = id; Name = name; StaminaCost = staminaCost; }")
    lines.append("    }")
    lines.append("")
    lines.append("    public readonly struct AlphaRegionTypeDef")
    lines.append("    {")
    lines.append("        public readonly int Type; public readonly string Name;")
    lines.append("        public AlphaRegionTypeDef(int type, string name) { Type = type; Name = name; }")
    lines.append("    }")
    lines.append("")
    lines.append("    /// <summary>One Alpha creature/object kind (156 in the D65 catalog; 26 of them are passive objects).</summary>")
    lines.append("    public readonly struct AlphaCreatureKindDef")
    lines.append("    {")
    lines.append("        public readonly int Kind; public readonly string Status; public readonly string MotionFamily;")
    lines.append("        public readonly string[] Parts; public readonly int[] Actions; public readonly bool Passive;")
    lines.append("        public readonly int Faction; public readonly string PassiveBasis; public readonly string NameHint;")
    lines.append("        public AlphaCreatureKindDef(int kind, string status, string family, string[] parts, int[] actions,")
    lines.append("            bool passive, int faction, string passiveBasis, string nameHint)")
    lines.append("        { Kind = kind; Status = status; MotionFamily = family; Parts = parts; Actions = actions;")
    lines.append("          Passive = passive; Faction = faction; PassiveBasis = passiveBasis; NameHint = nameHint; }")
    lines.append("    }")
    lines.append("")
    lines.append("    public readonly struct AlphaItemSlotDef")
    lines.append("    {")
    lines.append("        public readonly string Offset; public readonly string Name; public readonly int[] ItemTypes;")
    lines.append("        public readonly string Block; public readonly string AnchorLaw; public readonly string Status;")
    lines.append("        public readonly bool Rendered;")
    lines.append("        public AlphaItemSlotDef(string offset, string name, int[] itemTypes, string block, string law, string status, bool rendered)")
    lines.append("        { Offset = offset; Name = name; ItemTypes = itemTypes; Block = block; AnchorLaw = law; Status = status; Rendered = rendered; }")
    lines.append("    }")
    lines.append("")
    lines.append("    public readonly struct AlphaItemActionDef")
    lines.append("    {")
    lines.append("        public readonly int Id; public readonly int DrawState; public readonly string Weapon; public readonly string Offhand;")
    lines.append("        public readonly bool HeldItem; public readonly bool PetItem; public readonly bool WeaponOnly; public readonly bool TwoHandedChain;")
    lines.append("        public AlphaItemActionDef(int id, int drawState, string weapon, string offhand, bool heldItem, bool petItem, bool weaponOnly, bool twoHandedChain)")
    lines.append("        { Id = id; DrawState = drawState; Weapon = weapon; Offhand = offhand; HeldItem = heldItem; PetItem = petItem; WeaponOnly = weaponOnly; TwoHandedChain = twoHandedChain; }")
    lines.append("    }")
    lines.append("")
    lines.append("    /// <summary>Committed Alpha content tables (ids/names/routes only; assets stay local).</summary>")
    lines.append("    public static class AlphaContentTables")
    lines.append("    {")
    lines.append('        public const string Format = "cubegame.alpha_content_tables/1";')
    lines.append("")
    lines.append("        public static readonly AlphaItemTypeDef[] ItemTypes =")
    lines.append("        {")
    for i, n, c, m, note in item_types:
        lines.append(f"            new AlphaItemTypeDef({i}, {cs(n)}, {evidence(c)}, {cs(m)}, {cs(note)}),")
    lines.append("        };")
    lines.append("")
    lines.append("        public static readonly AlphaWeaponSubtypeDef[] WeaponSubtypes =")
    lines.append("        {")
    for i, n, m, two, dual, status in weapon_types:
        lines.append(f"            new AlphaWeaponSubtypeDef({i}, {cs(n)}, {cs(m)}, {str(two).lower()}, {str(dual).lower()}, {evidence(status)}),")
    lines.append("        };")
    lines.append("")
    lines.append("        public static readonly AlphaAbilityDef[] Abilities =")
    lines.append("        {")
    for i, n, s in abilities:
        lines.append(f"            new AlphaAbilityDef({i}, {cs(n)}, {s}),")
    lines.append("        };")
    lines.append("")
    lines.append("        public static readonly AlphaRegionTypeDef[] RegionTypes =")
    lines.append("        {")
    for t, n in regions:
        lines.append(f"            new AlphaRegionTypeDef({t}, {cs(n)}),")
    lines.append("        };")
    lines.append("")
    lines.append("        public static readonly AlphaCreatureKindDef[] CreatureKinds =")
    lines.append("        {")
    for k in kinds:
        parts = str_array(k["parts"])
        acts = int_array(k["actions"])
        lines.append(f"            new AlphaCreatureKindDef({k['kind']}, {cs(k['status'])}, {cs(k['family'])}, {parts}, {acts}, "
                     f"{str(k['passive']).lower()}, {k['faction']}, {cs(k['passiveBasis'])}, {cs(k['nameHint'])}),")
    lines.append("        };")
    lines.append("")
    lines.append("        public static readonly AlphaItemSlotDef[] ItemSlots =")
    lines.append("        {")
    for off, name, types, block, law, status, rendered in slots:
        lines.append(f"            new AlphaItemSlotDef({cs(off)}, {cs(name)}, {int_array(types)}, "
                     f"{cs(block)}, {cs(law)}, {cs(status)}, {str(rendered).lower()}),")
    lines.append("        };")
    lines.append("")
    lines.append("        public static readonly AlphaItemActionDef[] ItemActions =")
    lines.append("        {")
    for aid, state, weapon, offhand, held, pet, weapon_only, chain in actions:
        lines.append(f"            new AlphaItemActionDef({aid}, {state}, {cs(weapon)}, {cs(offhand)}, "
                     f"{str(held).lower()}, {str(pet).lower()}, {str(weapon_only).lower()}, {str(chain).lower()}),")
    lines.append("        };")
    lines.append("")
    lines.append("        public const int InventoryContainers = %d;" % int(tables["inventory"]["containers"]))
    lines.append("        public const int InventorySlotsPerContainer = %d;" % int(tables["inventory"]["slotsPerContainer"]))
    lines.append("        public const int InventoryStackLimit = %d;" % int(tables["inventory"]["stackLimit"]))
    lines.append("        public const int EquipmentStorageSlots = %d;" % int(tables["equipmentStorage"]["slots"]))
    lines.append("        public const int ConsumableActionFirst = %d;" % int(tables["consumables"]["actions"][0]))
    lines.append("        public const int ConsumableActionSecond = %d;" % int(tables["consumables"]["actions"][1]))
    lines.append("        public const int WorldRegionGrid = %d;" % int(tables["regions"]["grid"]))
    lines.append("        public const int RegionBlocks = %d;" % int(tables["regions"]["regionBlocks"]))
    lines.append("        public const int WorldBlocks = %d;" % int(tables["regions"]["worldBlocks"]))
    lines.append("        public const int RegionCells = %d;" % int(tables["regions"]["cellsPerRegion"]))
    lines.append("    }")
    lines.append("}")

    counts = {"itemTypes": len(item_types), "weaponSubtypes": len(weapon_types),
              "abilities": len(abilities), "regions": len(regions), "kinds": len(kinds),
              "passiveKinds": sum(1 for k in kinds if k["passive"]), "slots": len(slots),
              "itemActions": len(actions)}
    return "\n".join(lines) + "\n", counts


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description="Build/verify the committed Alpha content tables.")
    ap.add_argument("--check", action="store_true", help="only verify that the generated file is up to date")
    a = ap.parse_args(argv)
    text, counts = build()
    summary = " ".join(f"{k}={v}" for k, v in counts.items())
    if a.check:
        if not OUTPUT.exists():
            print("ALPHA_CONTENT_TABLES=FAIL missing " + str(OUTPUT))
            return 1
        if OUTPUT.read_text(encoding="utf-8") != text:
            print("ALPHA_CONTENT_TABLES=FAIL drift (rebuild the tables)")
            return 1
        print("ALPHA_CONTENT_TABLES=PASS check " + summary)
        return 0
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(text, encoding="utf-8", newline="\n")
    print("ALPHA_CONTENT_TABLES=PASS " + summary)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
