"""Extract the static name tables built in cube::World::ctor_1 @ 0x0058eb00 (client).

Evidence base: Ghidra-headless decompilation of Cube.exe (2013 Alpha), file
`cube/world/World.cpp`. No game binary is executed; this is a text re-read of the
decompiled body plus a deterministic pairing rule.

Three tables live in this constructor and must be separated, because two of them are
single-int-keyed and their id ranges overlap:

  * ITEM     key = (type, subtype) dword pair -> item display name      (0x2810..0x38f0 region)
  * OBJECT   key = int (house/furniture object type) -> object name      (Statue..CustomizationBench)
  * SKILL    key = int (skill-tree node id) -> skill/ability name        (SkillPetTaming..AbilitySneak)

Pairing rule (validated, see docs/research/alpha2013/ALPHA_STATIC_TABLES_ITEM_SKILL.md):
`World_MapInsertChunk` / `World_map_find_or_insert_int` behave like `map::operator[]`:
they produce the value slot and the wide-string assignment that IMMEDIATELY FOLLOWS
the call fills that slot. Validated against four independently known facts:
type 2 = Formula (crafting recipe), type 8 = Amulet, type 9 = Ring (both confirmed by
the equipment slot type checks in Equipment_sum_slot_values @ 0x446150), and the
`pwVar9 = L".."` style sub-block (weapon tail) where the value is bound by name.

Known residual ambiguity (reported, not hidden): inside the equipment block and the
weapon head block the decompiler emits `assignPtrLen(L"..",n)` (no pwVar9), and a
"value belongs to the NEXT key" reading there would reproduce the older published
lists (1 mace, 2 fist, 3 sword / 5 boots, 6 gloves). The two readings cannot both be
right; see the ambiguity table in the research document. This script emits the
"value follows its own insert" reading and marks those ids as AMBIGUOUS.
"""
import json
import re
import sys
from pathlib import Path

SRC = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(
    r"D:\Projects\CubeGame-deepseek\.scratch\CubeWorld-Reversal\cube\world\World.cpp")
OUT = Path(sys.argv[2]) if len(sys.argv) > 2 else Path(
    r"D:\Projects\CubeGame-deepseek\.scratch\research\world_ctor_tables.json")

lines = SRC.read_text(encoding="utf-8", errors="replace").splitlines()
start = next(i for i, ln in enumerate(lines) if "cube::World::ctor_1" in ln and "@" in ln)
end = next((j for j in range(start + 1, len(lines))
            if re.match(r"\s*/\* .* @ [0-9a-f]{8} \*/", lines[j])), len(lines))
print(f"ctor_1: header_line={start+1} body=[{start+1}..{end}] lines={end-start}")

RE_LIT = re.compile(r'L"([^"]*)"')
RE_DAT = re.compile(r"&(DAT_[0-9a-f]+)\s*,\s*(0x[0-9a-fA-F]+|\d+)")
RE_PAIRASSIGN = re.compile(
    r"pair_assign_2dword\(\s*(local_[0-9a-f]+)\s*,\s*&(local_[0-9a-f]+)\s*,\s*&(local_[0-9a-f]+)")

# Block boundaries inside the body (1-based line numbers of the insert calls), derived
# from the surrounding statements; used only to route single-int keys to the right table.
SKILL_BLOCK = (3945, 4056)
OBJECT_BLOCK = (3670, 3944)
ITEM_BLOCK = (2810, 3670)


def val(tok):
    return int(tok, 16) if tok.lower().startswith("0x") else int(tok)


def last_assign(local, before, back=16):
    for b in range(before, max(start - 1, before - back), -1):
        m = re.search(rf"\b{local}\s*=\s*(?:\(undefined4 \*\))?(0x[0-9a-fA-F]+|\d+)\s*;", lines[b])
        if m:
            return val(m.group(1)), b
    return None, None


def value_after_insert(i):
    for b in range(i + 1, min(end, i + 4)):
        ln = lines[b]
        if "u16string_assign" not in ln:
            continue
        m = RE_LIT.search(ln)
        if m:
            return {"name": m.group(1), "resolved": True, "line": b + 1, "style": "literal_after"}
        m = RE_DAT.search(ln)
        if m:
            return {"name": None, "resolved": False, "dat": m.group(1),
                    "dat_len": val(m.group(2)), "line": b + 1, "style": "dat_after"}
        m = re.search(r"u16string_assignFromWide\(\s*(pwVar9|puVar11)\s*\)", ln)
        if m:
            var = m.group(1)
            for c in range(b - 1, max(start - 1, b - 10), -1):
                mm = re.search(rf"\b{var}\s*=\s*(L\"([^\"]*)\"|&(DAT_[0-9a-f]+))", lines[c])
                if mm:
                    if mm.group(2) is not None:
                        return {"name": mm.group(2), "resolved": True, "line": c + 1,
                                "style": "pwvar_before"}
                    return {"name": None, "resolved": False, "dat": mm.group(3), "dat_len": None,
                            "line": c + 1, "style": "pwvar_before"}
        return None
    return None


records = []
for i in range(start, end):
    ln = lines[i]
    if "World_MapInsertChunk" in ln:
        kind = "pair"
    elif "World_map_find_or_insert_int" in ln:
        kind = "single"
    else:
        continue
    v = value_after_insert(i)
    if v is None:
        continue

    if kind == "pair":
        pair = None
        for b in range(i, max(start - 1, i - 12), -1):
            m = RE_PAIRASSIGN.search(lines[b])
            if m:
                t, _ = last_assign(m.group(2), b)
                s, _ = last_assign(m.group(3), b)
                if t is not None and s is not None:
                    pair = (t, s, b + 1, "pair_assign")
                break
        if pair is None:
            t, tb = last_assign("local_3c", i)
            s, _ = last_assign("local_38", i)
            if t is not None:
                pair = (t, s if s is not None else 0, tb + 1, "direct_pair")
        if pair is None:
            continue
        records.append({"kind": "pair", "type": pair[0], "subtype": pair[1],
                        "key_idiom": pair[3], "key_line": pair[2],
                        "value": v, "insert_line": i + 1})
    else:
        # key local is passed by address: `World_map_find_or_insert_int(&local_5c)`
        m = re.search(r"\(\s*&?(local_[0-9a-f]+)\s*\)", ln)
        loc = m.group(1) if m else None
        if loc is None:
            continue
        k, kb = last_assign(loc, i)
        if k is None:
            continue
        ln1 = i + 1
        table = ("skill" if SKILL_BLOCK[0] <= ln1 <= SKILL_BLOCK[1]
                 else "object" if OBJECT_BLOCK[0] <= ln1 <= OBJECT_BLOCK[1]
                 else "unknown_single")
        records.append({"kind": "single", "table": table, "id": k, "key_line": kb + 1,
                        "value": v, "insert_line": ln1})

pairs, singles = {}, {}
for r in records:
    if r["kind"] == "pair":
        pairs[(r["type"], r["subtype"])] = r
    else:
        singles[(r["table"], r["id"])] = r

# Items whose name block shows a non-monotonic key order and the `literal_after` style are
# the ones where an alternative pairing reproduces the older published lists.
AMBIGUOUS_TYPES_SUBTYPES = {(3, s) for s in (0, 1, 2, 3, 4, 6, 7, 8, 0xe)} | {(5, 0), (6, 0)}
AMBIGUOUS_SINGLES = {("object", 0x13)}

print(f"inserts={len(records)} pair_keys={len(pairs)} single_keys={len(singles)} "
      f"(skill={sum(1 for k in singles if k[0]=='skill')}, "
      f"object={sum(1 for k in singles if k[0]=='object')})")

data = {
    "extractor": "extract_world_ctor_tables.py",
    "source_file": str(SRC),
    "source_symbol": "cube::World::ctor_1 @ 0x0058eb00",
    "pairing_rule": "value string assignment immediately following the map insert",
    "item_map": [{"type": r["type"], "subtype": r["subtype"], "name": r["value"].get("name"),
                  "resolved": r["value"]["resolved"], "dat": r["value"].get("dat"),
                  "dat_len": r["value"].get("dat_len"), "style": r["value"]["style"],
                  "insert_line": r["insert_line"],
                  "ambiguous": (r["type"], r["subtype"]) in AMBIGUOUS_TYPES_SUBTYPES}
                 for _, r in sorted(pairs.items())],
    "object_map": [{"id": r["id"], "name": r["value"].get("name"),
                    "resolved": r["value"]["resolved"], "dat": r["value"].get("dat"),
                    "dat_len": r["value"].get("dat_len"), "insert_line": r["insert_line"],
                    "ambiguous": (r["table"], r["id"]) in AMBIGUOUS_SINGLES}
                   for k, r in sorted(singles.items()) if k[0] == "object"],
    "skill_map": [{"id": r["id"], "name": r["value"].get("name"),
                   "resolved": r["value"]["resolved"], "insert_line": r["insert_line"]}
                  for k, r in sorted(singles.items()) if k[0] == "skill"],
    "unrouted_single": [{"id": r["id"], "name": r["value"].get("name"),
                         "insert_line": r["insert_line"]}
                        for k, r in sorted(singles.items()) if k[0] == "unknown_single"],
}
OUT.parent.mkdir(parents=True, exist_ok=True)
OUT.write_text(json.dumps(data, indent=1, ensure_ascii=False), encoding="utf-8")
print("WROTE", OUT)


def write_tsv(path, header, rows):
    with path.open("w", encoding="utf-8", newline="\n") as f:
        f.write(header + "\n")
        for r in rows:
            f.write("\t".join("" if x is None else str(x) for x in r) + "\n")
    print("WROTE", path)


write_tsv(OUT.with_suffix(".item.tsv"), "type\tsubtype\tname\tdat\tdat_len\tstyle\tambiguous\tinsert_line",
          [(f"0x{r['type']:02x}", f"0x{r['subtype']:02x}", r["value"].get("name"),
            r["value"].get("dat"), r["value"].get("dat_len"), r["value"]["style"],
            int((r["type"], r["subtype"]) in AMBIGUOUS_TYPES_SUBTYPES), r["insert_line"])
           for _, r in sorted(pairs.items())])
write_tsv(OUT.with_suffix(".object.tsv"), "id\tname\tdat\tinsert_line",
          [(f"0x{r['id']:02x}", r["value"].get("name"), r["value"].get("dat"), r["insert_line"])
           for k, r in sorted(singles.items()) if k[0] == "object"])
write_tsv(OUT.with_suffix(".skill.tsv"), "id\tname\tinsert_line",
          [(f"0x{r['id']:02x}", r["value"].get("name"), r["insert_line"])
           for k, r in sorted(singles.items()) if k[0] == "skill"])
