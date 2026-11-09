#!/usr/bin/env python3
"""Generate the M0-00 pilot constants from confirmed Alpha 2013 tables.

The JSON file is the single source of truth for identifiers that were proven in
docs/research/alpha2013. This script validates the data and emits a C# file used
by the isolated pilot lab (tools/m0_pilots). It deliberately contains no game
logic: only IDs, names, and a content hash.

Usage:
    python tools/m0_transfer/gen_alpha_tables.py
    python tools/m0_transfer/gen_alpha_tables.py --check
"""

from __future__ import annotations

import argparse
import hashlib
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DATA = ROOT / "tools" / "m0_transfer" / "data" / "alpha_confirmed_tables.json"
OUT = ROOT / "tools" / "m0_pilots" / "Generated" / "AlphaTables.g.cs"

ALLOWED_CONFIDENCE = {
    "DIRECT",
    "PARTIAL",
    "UNKNOWN",
    "PLACEHOLDER_NOT_ALPHA",
}

REQUIRED_TOP_LEVEL = [
    "itemTypes",
    "weaponSubtypes",
    "equipmentStorage",
    "inventory",
    "consumables",
    "abilities",
    "abilityCosts",
    "starterKits",
    "regions",
    "factions",
    "movement",
    "spawn",
    "quests",
    "combat",
]


class DataError(Exception):
    pass


def load() -> dict:
    if not DATA.exists():
        raise DataError(f"missing data file: {DATA}")
    with DATA.open("r", encoding="utf-8") as handle:
        data = json.load(handle)
    return data


def validate(data: dict) -> None:
    for key in REQUIRED_TOP_LEVEL:
        if key not in data:
            raise DataError(f"missing top-level table: {key}")

    items = data["itemTypes"]
    if len(items) != 24:
        raise DataError(f"expected 24 documented item types, got {len(items)}")
    assert_unique(items, "itemTypes")
    for entry in items:
        if not 0 <= entry["id"] <= 255:
            raise DataError(f"item type out of byte range: {entry}")
        assert_confidence(entry, "itemTypes")

    weapons = data["weaponSubtypes"]
    assert_unique(weapons, "weaponSubtypes")
    for entry in weapons:
        assert_confidence(entry, "weaponSubtypes")
    if [entry["id"] for entry in weapons] != sorted(entry["id"] for entry in weapons):
        raise DataError("weaponSubtypes must be sorted by id")

    slots = data["equipmentStorage"]["entries"]
    if len(slots) != 13 or data["equipmentStorage"]["slots"] != 13:
        raise DataError("equipment storage must contain exactly 13 slots")
    if [entry["index"] for entry in slots] != list(range(13)):
        raise DataError("equipment slot indices must be 0..12 in order")
    if len({entry["offset"] for entry in slots}) != 13:
        raise DataError("equipment slot offsets must be unique")

    abilities = data["abilities"]
    if len(abilities) != 16:
        raise DataError(f"expected 16 named abilities, got {len(abilities)}")
    assert_unique(abilities, "abilities")

    quests = data["quests"]["types"]
    if [entry["id"] for entry in quests] != list(range(1, 14)):
        raise DataError("quest types must be 1..13")

    regions = data["regions"]["nameTable"]
    assert_unique(regions, "regions.nameTable")

    kits = data["starterKits"]
    if [kit["classId"] for kit in kits] != [1, 2, 3, 4]:
        raise DataError("starter kits must cover Alpha class ids 1..4")
    rogue = kits[3]
    if not (rogue["mainHand"] and rogue["offHand"]):
        raise DataError("Rogue starter kit must prove dual wield with two hands")

    for entry in data["abilityCosts"]["mana"]:
        if entry["base"] < 0 or entry["scale"] < 0:
            raise DataError(f"negative mana cost: {entry}")

    channels = data["abilityCosts"]["channeled"]
    if len(channels) != 32 or len(set(channels)) != 32:
        raise DataError("channeled table must contain 32 unique ability ids")


def entry_id(entry: dict) -> int:
    if "id" in entry:
        return entry["id"]
    if "type" in entry:
        return entry["type"]
    raise DataError(f"entry has neither id nor type: {entry}")


def assert_unique(entries: list, table: str) -> None:
    ids = [entry_id(entry) for entry in entries]
    if len(ids) != len(set(ids)):
        raise DataError(f"duplicate ids in {table}: {ids}")


def assert_confidence(entry: dict, table: str) -> None:
    confidence = entry.get("confidence", "DIRECT")
    if confidence not in ALLOWED_CONFIDENCE:
        raise DataError(f"invalid confidence {confidence!r} in {table}: {entry}")


def canonical_hash(data: dict) -> str:
    canonical = json.dumps(data, ensure_ascii=False, sort_keys=True, separators=(",", ":"))
    return hashlib.sha256(canonical.encode("utf-8")).hexdigest()


def identifier(name: str) -> str:
    cleaned = "".join(ch if ch.isalnum() else "_" for ch in name)
    if not cleaned or cleaned[0].isdigit():
        cleaned = "T_" + cleaned
    return cleaned


def emit_constants(lines: list[str], class_name: str, summary: str, entries: list[dict]) -> None:
    lines.append(f"    /// <summary>{summary}</summary>")
    lines.append(f"    public static class {class_name}")
    lines.append("    {")
    for entry in entries:
        lines.append(
            f"        public const int {identifier(entry['name'])} = {entry_id(entry)};"
        )
    lines.append("")
    lines.append(f"        public static string NameOf(int id)")
    lines.append("        {")
    lines.append("            switch (id)")
    lines.append("            {")
    for entry in entries:
        lines.append(
            f"                case {entry_id(entry)}: return \"{entry['name']}\";"
        )
    lines.append("                default: return null;")
    lines.append("            }")
    lines.append("        }")
    lines.append("    }")
    lines.append("")


def render(data: dict, content_hash: str) -> str:
    lines: list[str] = []
    lines.append("// <auto-generated>")
    lines.append("// Generated by tools/m0_transfer/gen_alpha_tables.py from")
    lines.append("// tools/m0_transfer/data/alpha_confirmed_tables.json. Do not edit manually.")
    lines.append("// The JSON file is evidence data distilled from docs/research/alpha2013.")
    lines.append("// </auto-generated>")
    lines.append("")
    lines.append("namespace CubeGame.M0Pilots.Generated")
    lines.append("{")
    lines.append("    /// <summary>Content hash of the source evidence JSON (canonical form).</summary>")
    lines.append("    public static class AlphaTables")
    lines.append("    {")
    lines.append(f"        public const string ContentHash = \"{content_hash}\";")
    lines.append("    }")
    lines.append("")
    emit_constants(
        lines,
        "AlphaItemType",
        "Alpha item type ids (field +0x00), r3-corrected where models proved the mapping.",
        data["itemTypes"],
    )
    emit_constants(
        lines,
        "AlphaWeaponSubtype",
        "Alpha weapon subtype ids (item type 3, field +0x01).",
        data["weaponSubtypes"],
    )
    emit_constants(
        lines,
        "AlphaAbility",
        "Sixteen named skill-tree ability nodes from World::ctor_1.",
        data["abilities"],
    )
    emit_constants(
        lines,
        "AlphaRegionType",
        "Region name table keys known in World::ctor_1 (semantics of Region+0xc is PARTIAL).",
        [entry for entry in data["regions"]["nameTable"]],
    )
    emit_constants(
        lines,
        "AlphaFaction",
        "Creature role/faction values (+0x060).",
        data["factions"],
    )
    lines.append("}")
    lines.append("")
    return "\n".join(lines)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true", help="verify the committed C# file is up to date")
    args = parser.parse_args()

    try:
        data = load()
        validate(data)
    except (DataError, json.JSONDecodeError) as error:
        print(f"ALPHA_TABLES_RESULT=FAIL reason={error}")
        return 1

    content_hash = canonical_hash(data)
    rendered = render(data, content_hash)

    if args.check:
        if not OUT.exists():
            print(f"ALPHA_TABLES_CHECK=FAIL reason=missing {OUT}")
            return 1
        current = OUT.read_text(encoding="utf-8")
        if current != rendered:
            print("ALPHA_TABLES_CHECK=FAIL reason=generated file is stale; rerun without --check")
            return 1
        print(
            "ALPHA_TABLES_CHECK=PASS "
            f"items={len(data['itemTypes'])} weapons={len(data['weaponSubtypes'])} "
            f"abilities={len(data['abilities'])} slots={len(data['equipmentStorage']['entries'])} "
            f"hash={content_hash[:16]}"
        )
        return 0

    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(rendered, encoding="utf-8", newline="\n")
    print(
        "ALPHA_TABLES_GENERATED=PASS "
        f"path={OUT.relative_to(ROOT)} items={len(data['itemTypes'])} "
        f"weapons={len(data['weaponSubtypes'])} abilities={len(data['abilities'])} "
        f"hash={content_hash[:16]}"
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
