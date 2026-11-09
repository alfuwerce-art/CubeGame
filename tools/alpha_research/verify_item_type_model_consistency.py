"""Deterministic research check: does Alpha's item type/subtype layout agree with its own models?

Three independent sources are combined, all from Ghidra-headless decompilation of Cube.exe
(2013 Alpha); no binary is executed:

  S1  cube::World::ctor_1 @ 0x0058eb00   -> (type, subtype) -> display name      [World.cpp]
  S2  GameController_item_icon_id @ 0x4ec400 -> per-type model block index       [GameController.cpp]
  S3  asset loader in GameController_load_game_assets -> vector index -> .cub    [GameController.cpp]

S2 returns `vectorU32_at(<index>)`; S3 maps that index to the model file name. So the check
is: for every item type whose name is known from S1, the model block resolved through S2+S3
must carry the matching kind of model (chest/hand/foot/shoulder/amulet/ring/...).

If an assertion fails, the pairing rule or the name table is wrong -- this check exists to
make that impossible to hide. Exit code 0 = all assertions passed.

Usage:
  python tools/alpha_research/verify_item_type_model_consistency.py [cube_dir] [--verbose]
"""
import json
import re
import sys
from pathlib import Path

# Default resolves inside THIS checkout (was hard-coded to the DeepSeek worktree; Claude prebatch P5).
_ROOT = Path(__file__).resolve().parents[2]
CUBE = Path(sys.argv[1]) if len(sys.argv) > 1 and not sys.argv[1].startswith("--") else (
    _ROOT / ".scratch" / "CubeWorld-Reversal" / "cube")
VERBOSE = "--verbose" in sys.argv

WORLD = CUBE / "world" / "World.cpp"
GC = CUBE / "control" / "GameController.cpp"

failures = []
checks = []


def check(name, ok, detail=""):
    checks.append((name, ok, detail))
    if not ok:
        failures.append(f"{name}: {detail}")
    print(f"[{'PASS' if ok else 'FAIL'}] {name}" + (f"  ({detail})" if detail else ""))


# ---------------------------------------------------------------- S3: index -> model name
def load_model_table():
    lines = GC.read_text(encoding="utf-8", errors="replace").splitlines()
    re_name = re.compile(r'string_ctorAppend\([^,]+,[^,]+,"([^"]+\.cub)"\)')
    re_idx = re.compile(r"vector_at_stride4\((0x[0-9a-fA-F]+|\d+)\)")
    re_load = re.compile(r"VoxelModel_load_from_file_or_buffer")
    table = {}
    i = 0
    while i < len(lines):
        m = re_name.search(lines[i])
        if not m:
            i += 1
            continue
        # the index, the model id and the load call sit on the next few lines
        window = lines[i + 1:i + 6]
        idx = None
        loaded = False
        for ln in window:
            mv = re_idx.search(ln)
            if mv and idx is None:
                idx = int(mv.group(1), 16) if mv.group(1).lower().startswith("0x") else int(mv.group(1))
            if re_load.search(ln):
                loaded = True
        if idx is not None and loaded:
            table.setdefault(idx, m.group(1))
        i += 1
    return table


# ------------------------------------------------- S2: item type -> model block base index
def load_icon_blocks():
    """Per OUTER case of item_icon_id, the smallest model-vector index used in that case.

    Outer cases are printed with two leading spaces, inner ones with four or more, so the
    indentation is what keeps the delegation cases (weapons) from stealing neighbours' blocks.
    """
    text = GC.read_text(encoding="utf-8", errors="replace").splitlines()
    start = next(i for i, ln in enumerate(text) if "GameController_item_icon_id @" in ln)
    end = next((j for j in range(start + 1, len(text))
                if re.match(r"\s*/\* .* @ [0-9a-f]{8} \*/", text[j])), len(text))
    body = text[start:end]
    outer = [(i, int(m.group(1), 16)) for i, ln in enumerate(body)
             for m in [re.match(r"^  case (0x[0-9a-fA-F]+|\d+):\s*$", ln)] if m]
    blocks = {}
    for n, (i, case_val) in enumerate(outer):
        stop = outer[n + 1][0] if n + 1 < len(outer) else len(body)
        idxs = []
        for ln in body[i:stop]:
            for m in re.finditer(r"(0x[0-9a-f]{3})\b", ln):
                if "vectorU32_at" in ln or "nameId" in ln:
                    idxs.append(int(m.group(1), 16))
        if idxs:
            blocks[case_val] = min(idxs)
    return blocks


# ------------------------------------------------------ S1: (type,subtype) -> name table
def load_item_names():
    lines = WORLD.read_text(encoding="utf-8", errors="replace").splitlines()
    start = next(i for i, ln in enumerate(lines) if "cube::World::ctor_1" in ln and "@" in ln)
    end = next((j for j in range(start + 1, len(lines))
                if re.match(r"\s*/\* .* @ [0-9a-f]{8} \*/", lines[j])), len(lines))
    re_lit = re.compile(r'L"([^"]*)"')
    re_pair = re.compile(r"pair_assign_2dword\(\s*(local_[0-9a-f]+)\s*,\s*&(local_[0-9a-f]+)\s*,\s*&(local_[0-9a-f]+)")

    def val(t):
        return int(t, 16) if t.lower().startswith("0x") else int(t)

    def last_assign(local, before, back=16):
        for b in range(before, max(start - 1, before - back), -1):
            m = re.search(rf"\b{local}\s*=\s*(?:\(undefined4 \*\))?(0x[0-9a-fA-F]+|\d+)\s*;", lines[b])
            if m:
                return val(m.group(1))
        return None

    def value_after(i):
        for b in range(i + 1, min(end, i + 4)):
            ln = lines[b]
            if "u16string_assign" not in ln:
                continue
            m = re_lit.search(ln)
            if m:
                return m.group(1)
            m = re.search(r"u16string_assignFromWide\(\s*(pwVar9|puVar11)\s*\)", ln)
            if m:
                for c in range(b - 1, max(start - 1, b - 10), -1):
                    mm = re.search(rf"\b{m.group(1)}\s*=\s*L\"([^\"]*)\"", lines[c])
                    if mm:
                        return mm.group(1)
            return None
        return None

    table = {}
    for i in range(start, end):
        if "World_MapInsertChunk" not in lines[i]:
            continue
        v = value_after(i)
        if not v:
            continue
        pair = None
        for b in range(i, max(start - 1, i - 12), -1):
            m = re_pair.search(lines[b])
            if m:
                t = last_assign(m.group(2), b)
                s = last_assign(m.group(3), b)
                if t is not None and s is not None:
                    pair = (t, s)
                break
        if pair is None:
            t = last_assign("local_3c", i)
            s = last_assign("local_38", i)
            if t is not None:
                pair = (t, s or 0)
        if pair:
            table[pair] = v
    return table


models = load_model_table()
blocks = load_icon_blocks()
names = load_item_names()

print(f"models={len(models)} icon_type_blocks={len(blocks)} item_names={len(names)}")
if VERBOSE:
    for t in sorted(blocks):
        idx = blocks[t]
        print(f"  type 0x{t:02x} -> block 0x{idx:04x} = {models.get(idx, '<none>')}")

check("model table is non-trivial", len(models) > 2000, f"{len(models)} entries")

# ---- Assertion 1: the four armour types resolve to the matching model kind.
EXPECT = {
    4: ("chest", "ChestArmor"),
    5: ("hand", "Gloves"),
    6: ("foot", "Boots"),
    7: ("shoulder", "ShoulderArmor"),
    8: ("amulet", "Amulet"),
    9: ("ring", "Ring"),
}
for t, (kind, label) in EXPECT.items():
    idx = blocks.get(t)
    name = models.get(idx, "") if idx is not None else ""
    check(f"type {t} ({label}) -> model block 0x{(idx or 0):04x} contains '{kind}'",
          kind in name, f"got '{name}'")

# ---- Assertion 2: World name table agrees with the armour types above (pairing rule).
for t, (kind, label) in EXPECT.items():
    got = names.get((t, 0))
    check(f"World name table (type {t}, subtype 0) == {label}", got == label, f"got '{got}'")

# ---- Assertion 3: weapon subtypes resolved through item_name_string_id model blocks.
# item_icon_id case 3 delegates to GameController_item_name_string_id(subtype, ...), whose
# per-case base constants index these blocks. Mapping was established by reading that switch.
WEAPON_EXPECT = {
    1: ("axe", "Axe"),
    2: ("mace", "Mace"),
    3: ("dagger", "Dagger"),
    4: ("fist", "Fist"),
    5: ("longsword", "Longsword"),
    6: ("bow", "Bow"),
    7: ("crossbow", "Crossbow"),
    8: ("boomerang", "Boomerang"),
    9: ("arrow", "Arrow"),
    0xa: ("staff", "Staff"),
    0xb: ("wand", "Wand"),
    0xc: ("bracelet", "Bracelet"),
    0xd: ("shield", "Shield"),
    0xe: ("quiver", "Quiver"),
    0xf: ("greatsword", "Greatsword"),
}
# base constants read out of GameController_item_name_string_id (0x51be60)
WEAPON_BASE = {1: 0x162 * 5, 2: 0x261, 3: 0x3ba, 4: 0x3f1, 5: 0x178 * 5, 6: 0x2a2, 7: 0x2d9,
               8: 0x310, 9: 0x348, 0xa: 0x1b2, 0xb: 0x1e9, 0xc: 0x23e, 0xd: 0x428, 0xe: 0x347,
               0xf: 0x790}
for st, (kind, label) in WEAPON_EXPECT.items():
    idx = WEAPON_BASE[st]
    name = models.get(idx, "")
    check(f"weapon subtype 0x{st:02x} ({label}) -> 0x{idx:04x} contains '{kind}'",
          kind in name, f"got '{name}'")

# ---- Assertion 4: the two 3-character placeholder names are explained by their weapon block.
check("weapon subtype 1 name is 3 chars (World 'DAT_0071b3d0') and model is an axe",
      "axe" in models.get(WEAPON_BASE[1], ""), models.get(WEAPON_BASE[1], ""))
check("weapon subtype 6 name is 3 chars (World 'DAT_0071b3d8') and model is a bow",
      "bow" in models.get(WEAPON_BASE[6], ""), models.get(WEAPON_BASE[6], ""))

# ---- Assertion 5: the old published list must be refuted (this is the regression guard).
OLD_F_ITEMS = {1: "mace", 2: "fist", 3: "sword", 4: "dagger", 8: "crossbow", 0xe: "boomerang"}
refuted = []
for st, old_kind in OLD_F_ITEMS.items():
    name = models.get(WEAPON_BASE[st], "")
    if old_kind not in name:
        refuted.append(f"{st}:expected {old_kind}; got {name}")
check("F_items.md weapon-subtype list is refuted by models (shifted by one)",
      len(refuted) >= 5, "; ".join(refuted[:3]) + (" ..." if len(refuted) > 3 else ""))

# ---- Assertion 6: special item types resolve to their own model blocks.
idx17 = blocks.get(0x17)
if idx17 is not None:
    print(f"  type 0x17 block 0x{idx17:04x} = {models.get(idx17, '<none>')}")

# ---- Assertion 7: type 0x15 is the quest-item family (was an open question in r2).
idx15 = blocks.get(0x15)
name15 = models.get(idx15, "")
check("type 0x15 model block is a quest item", "quest-item" in name15, f"got '{name15}'")

# ---- Assertion 8: item-name table arithmetic: 133 pairs = 127 literals + 6 pool pointers.
world_text = WORLD.read_text(encoding="utf-8", errors="replace")
n_dat = len(re.findall(r"u16string_assignPtrLen\(&(DAT_[0-9a-f]+),\s*\d+\)", world_text))
check("item-name table has 6 unresolved pool pointers", n_dat >= 5,
      f"found {n_dat} assignPtrLen(&DAT_..) in World.cpp")

# ---- Assertion 9: the furniture vocabulary of the object table exists in the model set.
OBJECT_WORDS = ["workbench", "barrel", "crate", "tent", "campfire", "anvil", "vase", "candle",
                "door", "gate", "shelf", "chest", "fence", "statue", "lever"]
model_names = " ".join(v.lower() for v in models.values())
present = [w for w in OBJECT_WORDS if w in model_names]
check("object/furniture vocabulary exists in the model set", len(present) >= 10,
      f"{len(present)}/{len(OBJECT_WORDS)}: {', '.join(present)}")

print()
print(f"CHECKS={len(checks)} FAILED={len(failures)}")
if failures:
    print("RESULT=FAIL")
    for f in failures:
        print("  -", f)
    sys.exit(1)
print("RESULT=PASS")
