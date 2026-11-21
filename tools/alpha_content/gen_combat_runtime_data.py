"""M0-F2B: Alpha combat-runtime data (projectile path + per-attack numbers) from the shipped bytes.

The game needs a *verified* table of "which weapon launches which attack, and how long does that
attack take" before any ranged/projectile runtime can be written. This module re-derives that table
from ``Cube.exe`` itself and from the reviewed item-layer evidence — nothing is copied from the
decompilation by hand and nothing of Alpha (models, textures, geometry) is stored:

* ``combat_selectAttackVariant`` (VA ``0x4452A0``) — weapon ``(item type, subtype)`` -> attack id;
  the instruction bytes are re-checked here through the anchors already committed in
  :mod:`tools.alpha_content.attack_selection` (one source of truth, no duplicate list of opcodes).
* ``combat_getAttackTiming`` (VA ``0x43D1A0``) — the *cast* switch (``0..0x69``): either a fixed
  millisecond value or a base constant divided by ``haste * attackSpeed``. The same function holds a
  second switch (``0x43D510``) that adds the recovery time.
* ``combat_getReloadFrames`` (VA ``0x447310``) — the recovery switch (``0..0x68``). It is decoded
  separately *and* compared with the second switch of ``combat_getAttackTiming``; a difference between
  the two is reported as a conflicting row instead of being averaged away.
* ``combat_getStaminaCost`` (VA ``0x444270``) — stamina base per attack id.
* the muzzle/trail path: attack ids for which ``GameController`` computes
  ``projectile_getMuzzlePosition`` (VA ``0x446BB0``) and builds a swept
  ``render_build_projectile_mesh`` (VA ``0x4C04C0``). The gates are byte-checked.

What the bytes *do not* say stays ``UNKNOWN``: Alpha has no flying projectile entity in this code
(the ranged hit is a raycast from the muzzle, the visible "projectile" is a per-frame swept mesh), and
the flight parameters of the item-layer arrow/boomerang models are not constants of ``Cube.exe``. The
generated table therefore carries no speed/gravity/lifetime numbers at all.

CLI::

    python -m tools.alpha_content.gen_combat_runtime_data            # write evidence + C# table
    python -m tools.alpha_content.gen_combat_runtime_data --check    # drift check (exit 1 on drift)
"""
from __future__ import annotations

import argparse
import hashlib
import json
import sys
from pathlib import Path

from capstone import CS_ARCH_X86, CS_MODE_32, Cs
from capstone.x86 import X86_OP_IMM, X86_OP_MEM, X86_OP_REG

ROOT = Path(__file__).resolve().parents[2]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from tools.alpha_content import attack_selection as variant  # noqa: E402
from tools.alpha_models.anim import pe_bytes as pb  # noqa: E402

DEFAULT_CUBE = ROOT / ".scratch/alphatest2/Cube.exe"
ITEM_LAYER = ROOT / "tools/alpha_models/catalog/item_layer_evidence.json"
OUT_JSON = ROOT / "tools/alpha_content/data/combat_runtime_evidence.json"
OUT_CS = ROOT / "Assets/CubeGame/Content/Generated/AlphaCombatRuntimeData.g.cs"

CUBE_SHA256 = "84a7a132a84d4282338e7ea45a1940d32066d64e39cbafed8f2418d5a6dc30bf"

# Switch functions: (name, VA, expected max case). The first switch of every function is located by
# its own prologue pattern, not by these numbers; the numbers are only asserted afterwards.
TIMING_VA = 0x43D1A0
TIMING_RECOVER_SWITCH_VA = 0x43D510
RELOAD_VA = 0x447310
STAMINA_VA = 0x444270

MUZZLE_VA = 0x446BB0                 # projectile_getMuzzlePosition
TRAIL_VA = 0x4C04C0                  # render_build_projectile_mesh
MUZZLE_PROLOGUE_BYTES = 0x80         # hash window: identifies the function head, not the whole body
TRAIL_PROLOGUE_BYTES = 0x80

# Byte anchors for the two muzzle gates and the special cases (VA, bytes, meaning).
PROJECTILE_CHECKS = [
    (0x4B630D, "8a4668", "mov al, byte ptr [esi+0x68] (current attack id, wand/bracelet trail gate)"),
    (0x4B6310, "3c25", "cmp al, 0x25 (bracelet attack, even branch)"),
    (0x4B6314, "3c26", "cmp al, 0x26 (wand wind-up action)"),
    (0x4B6318, "3c2b", "cmp al, 0x2b (bracelet attack, specialist branch)"),
    (0x4B631C, "3c2c", "cmp al, 0x2c (wand wind-up action, specialist branch)"),
    (0x4B467B, "8a4668", "mov al, byte ptr [esi+0x68] (second trail gate)"),
    (0x4B467E, "3c1c", "cmp al, 0x1c"),
    (0x4B4693, "3c5f", "cmp al, 0x5f"),
    (0x4B4697, "3c5e", "cmp al, 0x5e"),
    (0x4B6495, "807e6826", "cmp byte ptr [esi+0x68], 0x26 (muzzle length special case)"),
    (0x615FE9, "80786868", "cmp byte ptr [eax+0x68], 0x68 (bomb: fixed range instead of raycast)"),
]

# Attack ids proven to reach the muzzle/trail path (byte anchors above).
MUZZLE_GATE_A = (0x25, 0x26, 0x2B, 0x2C)     # 0x4B630D..0x4B631E
MUZZLE_GATE_B = (0x1C, 0x5E, 0x5F)           # 0x4B467B..0x4B4699
BOMB_ATTACK_ID = 0x68                         # 0x615FE9

# Weapon subtypes the item-layer evidence attributes to a model (D64/D71 execution of item_icon_id).
PROJECTILE_CAPABLE = (6, 7, 8, 9, 10, 11, 12)

NAME = "Alpha combat runtime"


# --------------------------------------------------------------------------- PE / instruction helpers
def sha256_file(path: Path) -> str:
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


class Disassembler:
    """Thin read-only wrapper: VA -> bytes / instructions / float constants."""

    def __init__(self, image: pb.Image):
        self.image = image
        self._md = Cs(CS_ARCH_X86, CS_MODE_32)
        self._md.detail = True

    def read(self, va: int, size: int) -> bytes:
        return self.image.read(va, size)

    def u32(self, va: int) -> int:
        return self.image.u32(va)

    def i32(self, va: int) -> int:
        return self.image.i32(va)

    def f32(self, va: int) -> float:
        import struct

        return struct.unpack("<f", self.read(va, 4))[0]

    def insns(self, lo: int, size: int) -> list:
        return list(self._md.disasm(self.read(lo, size), lo))

    def call_sites(self, target_va: int) -> list[int]:
        """Every ``E8 rel32`` whose destination is ``target_va`` (linear scan of the image)."""
        data = self.image.data
        offset = 0
        found = []
        while True:
            index = data.find(b"\xe8", offset)
            if index < 0:
                break
            offset = index + 1
            try:
                va = self._va_of(index)
            except pb.PEError:
                continue
            if va is None:
                continue
            rel = int.from_bytes(data[index + 1:index + 5], "little", signed=True)
            if va + 5 + rel == target_va:
                found.append(va)
        return found

    def _va_of(self, offset: int):
        for section in self.image.sections:
            if section.raw_offset <= offset < section.raw_offset + section.raw_size:
                return self.image.image_base + section.virtual_address + (offset - section.raw_offset)
        return None


# --------------------------------------------------------------------------- switch decoding
def decode_jump_switch(dis: Disassembler, func_va: int, span: int = 0x240) -> dict:
    """Locate ``switch (byte [self+0x68])`` in ``func_va`` and decode its jump table.

    Returns ``{max_case, index_table_va, jump_table_va, default_va, groups}`` where ``groups`` maps a
    case block VA to the list of attack ids that land on it.
    """
    seq = dis.insns(func_va, span)
    max_case = None
    index_table = None
    jump_table = None
    default_va = None
    seen_load = False
    seen_cmp = False
    for index, ins in enumerate(seq):
        mem_disp = None
        if len(ins.operands) > 1 and ins.operands[1].type == X86_OP_MEM:
            mem_disp = ins.operands[1].mem.disp
        if ins.mnemonic == "movzx" and mem_disp == 0x68:
            seen_load = True
        elif (ins.mnemonic == "cmp" and seen_load and max_case is None
              and ins.operands[0].type == X86_OP_REG
              and dis._md.reg_name(ins.operands[0].reg) == "eax"
              and ins.operands[1].type == X86_OP_IMM):
            max_case = ins.operands[1].imm
            seen_cmp = True
            if index + 1 < len(seq) and seq[index + 1].mnemonic == "ja":
                default_va = seq[index + 1].operands[0].imm
        elif (ins.mnemonic == "movzx" and seen_cmp and index_table is None
              and mem_disp is not None and mem_disp > 0x10000):
            index_table = mem_disp
        elif ins.mnemonic == "jmp" and ins.operands[0].type == X86_OP_MEM and index_table is not None:
            ref = ins.operands[0].mem
            if ref.disp > 0x10000:
                jump_table = ref.disp
                break
    if max_case is None or index_table is None or jump_table is None:
        raise RuntimeError(f"0x{func_va:X}: switch (byte [self+0x68]) not found")
    groups = {}
    for attack_id in range(max_case + 1):
        group = dis.read(index_table + attack_id, 1)[0]
        groups.setdefault(group, []).append(attack_id)
    return {
        "max_case": max_case,
        "index_table_va": index_table,
        "jump_table_va": jump_table,
        "default_va": default_va,
        "groups": groups,
    }


def _block_value(dis: Disassembler, block_va: int, func_lo: int, func_hi: int, hops: int = 2):
    """Decode one case block: fixed integer/cast or a scaled float base constant.

    Returns ``(mode, value, condition)`` where mode is ``"fixed"``, ``"scaled"`` or ``None``
    (not decodable -> the caller records UNKNOWN instead of inventing a number).
    """
    va = block_va
    for _ in range(hops):
        mode = None
        value = None
        condition = None
        jump = None
        terminal = False
        for ins in dis.insns(va, 0x60):
            name = ins.mnemonic
            if len(ins.operands) > 1 and ins.operands[1].type == X86_OP_MEM \
                    and ins.operands[1].mem.disp == 0x68 and name in ("movzx", "mov"):
                terminal = True          # the next switch of the same function starts here
                break
            if name == "cmp" and ins.operands[0].type == X86_OP_MEM \
                    and ins.operands[0].mem.disp == 0xAA9 and ins.operands[1].type == X86_OP_IMM:
                condition = ins.operands[1].imm
                continue
            if name == "movss" and len(ins.operands) > 1 and ins.operands[1].type == X86_OP_MEM \
                    and ins.operands[1].mem.base == 0 and ins.operands[1].mem.disp > 0x10000:
                mode, value = "scaled", round(dis.f32(ins.operands[1].mem.disp), 6)
                break
            if name in ("xor", "mov") and ins.operands[0].type == X86_OP_REG \
                    and dis._md.reg_name(ins.operands[0].reg) in ("eax", "ebx", "edi"):
                if name == "xor" and ins.operands[1].type == X86_OP_REG \
                        and ins.operands[0].reg == ins.operands[1].reg:
                    mode, value = "fixed", 0
                elif ins.operands[1].type == X86_OP_IMM:
                    mode, value = "fixed", ins.operands[1].imm
                else:
                    mode, value = None, None      # copied from another register: not a constant
                break
            if name == "jmp" and ins.operands[0].type == X86_OP_IMM:
                jump = ins.operands[0].imm
                break
            if name in ("ret", "pop"):
                terminal = True
                break
        if value is not None:
            return mode, value, condition
        if mode is None and not terminal and jump is not None:
            if not (func_lo <= jump < func_hi) or jump == va:
                return None, None, condition
            va = jump
            continue
        return None, None, condition
    return None, None, None


def decode_table(dis: Disassembler, func_va: int) -> dict:
    """Full decode of one switch function: per-attack value + the default branch + hash window."""
    switch = decode_jump_switch(dis, func_va)
    groups = switch["groups"]
    table_end = max(switch["index_table_va"] + switch["max_case"] + 1,
                    switch["jump_table_va"] + 4 * len(groups))
    func_hi = table_end + 0x40
    rows = {}
    targets = {}
    for group, ids in sorted(groups.items()):
        block = dis.u32(switch["jump_table_va"] + 4 * group)
        targets[group] = block
        mode, value, condition = _block_value(dis, block, func_va, func_hi)
        for attack_id in ids:
            rows[attack_id] = {"mode": mode, "value": value, "condition_subtype": condition}
    default_va = switch["default_va"]
    default = {"mode": None, "value": None, "condition_subtype": None}
    if default_va is not None:
        mode, value, condition = _block_value(dis, default_va, func_va, func_hi)
        default = {"mode": mode, "value": value, "condition_subtype": condition}
    # the region that is hashed for drift: code + both tables, padded to 4 bytes
    pad = (4 - table_end % 4) % 4
    region = dis.read(func_va, table_end + pad - func_va)
    return {
        "function_va": func_va,
        "bytes": len(region),
        "sha256": sha256_bytes(region),
        "max_case": switch["max_case"],
        "index_table_va": switch["index_table_va"],
        "jump_table_va": switch["jump_table_va"],
        "default_va": default_va,
        "default": default,
        "rows": rows,
    }


# --------------------------------------------------------------------------- weapon -> attack
def class_branch_attack(subtype: int, specialist: bool) -> int:
    rules = variant.RULES
    branch = rules["class_branch"]
    if subtype == rules["class_branch_subtypes"][1]:            # 0x0B
        key = "item_subtype_0x0b"
    elif subtype == rules["class_branch_subtypes"][0]:          # 0x0A
        key = "item_subtype_0x0a"
    else:
        key = "default"
    return branch[key]["specialisation_eq_1" if specialist else "specialisation_ne_1"]


def weapon_attack(subtype: int, item_type: int = 3, specialist: bool = False, class_is_mage: bool = False,
                  kind_special: bool = False) -> dict:
    """Replay ``combat_selectAttackVariant`` for one weapon (see AlphaAttackSelection.cs)."""
    rules = variant.RULES
    weapon_type = rules["weapon_item_type"]
    if item_type == weapon_type and subtype == rules["immediate"][0]["item_subtype"]:
        attack = rules["immediate"][0]["attack"]
        return {"attack_even": attack, "attack_odd": attack, "route": "weapon_6_before_class"}
    if class_is_mage or kind_special:
        return {"attack_even": class_branch_attack(subtype, specialist),
                "attack_odd": class_branch_attack(subtype, specialist),
                "route": "class_branch"}
    if item_type == weapon_type:
        if subtype in rules["class_branch_subtypes"]:
            attack = class_branch_attack(subtype, specialist)
            return {"attack_even": attack, "attack_odd": attack, "route": "class_branch_subtype"}
        if subtype == rules["immediate"][1]["item_subtype"]:
            attack = rules["immediate"][1]["attack"]
            return {"attack_even": attack, "attack_odd": attack, "route": "weapon_5"}
        if subtype in rules["weapon_parity_subtypes"]:
            return {"attack_even": rules["weapon_parity_attack"]["parity_0"],
                    "attack_odd": rules["weapon_parity_attack"]["parity_1"],
                    "route": "parity"}
    return {"attack_even": rules["unarmed_attack"]["parity_0"],
            "attack_odd": rules["unarmed_attack"]["parity_1"], "route": "unarmed"}


# --------------------------------------------------------------------------- build
def _weapons(item_layer: dict, problems: list[str]) -> list[dict]:
    classes = item_layer["contract"]["weaponClasses"]
    rows = []
    seen = set()
    for entry in sorted(classes, key=lambda e: (e["type"], e["subtype"])):
        subtype = entry["subtype"]
        if subtype in seen:
            problems.append(f"DUPLICATE_WEAPON_SUBTYPE {subtype}")
            continue
        seen.add(subtype)
        model = entry.get("model") or ""
        mapping = weapon_attack(subtype, entry["type"])
        muzzle = mapping["attack_even"] in MUZZLE_GATE_A or mapping["attack_odd"] in MUZZLE_GATE_A \
            or mapping["attack_even"] in MUZZLE_GATE_B or mapping["attack_odd"] in MUZZLE_GATE_B
        status = "ALPHA_CODE_STATIC"
        if subtype in PROJECTILE_CAPABLE:
            if not model.endswith(".cub"):
                problems.append(f"MISSING_MODEL_MAPPING subtype={subtype} label={entry['label']}")
                status = "PARTIAL"
            if not muzzle:
                status = "PARTIAL"     # model proven, muzzle/trail path not proven for this subtype
        if not model:
            status = "PARTIAL"
        rows.append({
            "item_type": entry["type"],
            "subtype": subtype,
            "label": entry["label"],
            "model": model,
            "model_index": entry.get("modelIndex"),
            "alias": "(alias" in entry["label"],
            "attack_even": mapping["attack_even"],
            "attack_odd": mapping["attack_odd"],
            "attack_route": mapping["route"],
            "muzzle_path_proven": muzzle,
            "status": status,
        })
    return rows


def _attack_rows(timing: dict, reload_: dict, stamina: dict, problems: list[str]) -> list[dict]:
    ceiling = timing["max_case"]
    rows = []
    for attack_id in range(ceiling + 1):
        cast = timing["rows"].get(attack_id, timing["default"])
        recover = reload_["rows"].get(attack_id, reload_["default"])
        stamina_row = stamina["rows"].get(attack_id, stamina["default"])
        if recover["mode"] is None:
            problems.append(f"UNDECODED_RECOVER attack={attack_id}")
        if cast["mode"] is None:
            problems.append(f"UNDECODED_CAST attack={attack_id}")
        if stamina_row["mode"] is None:
            problems.append(f"UNDECODED_STAMINA attack={attack_id}")
        rows.append({
            "id": attack_id,
            "cast_mode": cast["mode"] or "unknown",
            "cast": cast["value"],
            "cast_condition_subtype": cast["condition_subtype"],
            "cast_from_default": attack_id not in timing["rows"],
            "recover_mode": recover["mode"] or "unknown",
            "recover": recover["value"],
            "recover_from_default": attack_id not in reload_["rows"],
            "stamina_mode": stamina_row["mode"] or "unknown",
            "stamina": stamina_row["value"],
            "stamina_from_default": attack_id not in stamina["rows"],
            "muzzle_path_proven": attack_id in MUZZLE_GATE_A or attack_id in MUZZLE_GATE_B,
            "bomb": attack_id == BOMB_ATTACK_ID,
        })
    return rows


def _cross_check_recover(timing_switch: dict, reload_: dict) -> list[str]:
    """``combat_getAttackTiming`` adds the recovery switch; it must equal ``combat_getReloadFrames``."""
    conflicts = []
    for attack_id, row in reload_["rows"].items():
        second = timing_switch["rows"].get(attack_id)
        if second is None:
            continue
        if second["mode"] != row["mode"] or second["value"] != row["value"]:
            conflicts.append(f"RECOVER_CONFLICT attack={attack_id} timing={second} reload={row}")
    return conflicts


def build(cube: Path = DEFAULT_CUBE, item_layer: Path = ITEM_LAYER) -> dict:
    data = Path(cube).read_bytes()
    sha = hashlib.sha256(data).hexdigest()
    if sha != CUBE_SHA256:
        raise ValueError(f"Cube.exe SHA256 {sha} != {CUBE_SHA256}: addresses not verified for this build")
    image = pb.load(str(cube))
    dis = Disassembler(image)
    problems: list[str] = []

    # 1. attack-variant table: reuse the committed anchors as the single source of truth.
    body = image.read(variant.FUNCTION_VA, variant.FUNCTION_BYTES)
    body_sha = sha256_bytes(body)
    checks = []
    if body_sha != variant.FUNCTION_SHA256:
        problems.append(f"FUNCTION_BYTES sha256 {body_sha} (combat_selectAttackVariant)")
    for va, hex_bytes, meaning in variant.CHECKS:
        want = bytes.fromhex(hex_bytes)
        ok = image.read(va, len(want)) == want
        checks.append({"va": f"0x{va:X}", "bytes": hex_bytes, "ok": ok, "meaning": meaning})
        if not ok:
            problems.append(f"CHECK_BYTES 0x{va:X}")

    # 2. projectile gates + the functions they call.
    projectile_checks = []
    for va, hex_bytes, meaning in PROJECTILE_CHECKS:
        want = bytes.fromhex(hex_bytes)
        ok = image.read(va, len(want)) == want
        projectile_checks.append({"va": f"0x{va:X}", "bytes": hex_bytes, "ok": ok, "meaning": meaning})
        if not ok:
            problems.append(f"PROJECTILE_CHECK_BYTES 0x{va:X}")
    muzzle_sites = dis.call_sites(MUZZLE_VA)
    trail_sites = dis.call_sites(TRAIL_VA)
    for expected, sites, label in ((4, muzzle_sites, "MUZZLE"), (1, trail_sites, "TRAIL")):
        if len(sites) != expected:
            problems.append(f"{label}_CALL_SITES expected={expected} got={[hex(s) for s in sites]}")

    # 3. per-attack numbers.
    timing = decode_table(dis, TIMING_VA)
    recover_switch = decode_table(dis, TIMING_RECOVER_SWITCH_VA)
    reload_table = decode_table(dis, RELOAD_VA)
    stamina_table = decode_table(dis, STAMINA_VA)
    problems.extend(_cross_check_recover(recover_switch, reload_table))
    attacks = _attack_rows(timing, reload_table, stamina_table, problems)

    # 4. weapon -> attack + item-layer models.
    item_layer_doc = json.loads(Path(item_layer).read_text(encoding="utf-8"))
    weapons = _weapons(item_layer_doc, problems)

    # 5. structural validation.
    ids = [row["id"] for row in attacks]
    if len(ids) != len(set(ids)):
        problems.append("DUPLICATE_ATTACK_ROW")
    for row in attacks:
        if not 0 <= row["id"] <= timing["max_case"]:
            problems.append(f"IMPOSSIBLE_ATTACK_ID {row['id']}")
        for key in ("cast", "recover", "stamina"):
            value = row[key]
            if value is not None and not -1e6 < value < 1e7:
                problems.append(f"IMPOSSIBLE_{key.upper()} attack={row['id']} value={value}")
    subtypes = [row["subtype"] for row in weapons]
    if len(subtypes) != len(set(subtypes)):
        problems.append("DUPLICATE_WEAPON_ROW")
    for row in weapons:
        for key in ("attack_even", "attack_odd"):
            if row[key] > timing["max_case"]:
                problems.append(f"IMPOSSIBLE_WEAPON_ATTACK subtype={row['subtype']} {key}={row[key]}")
    referenced = {row["attack_even"] for row in weapons} | {row["attack_odd"] for row in weapons}
    known = {row["id"] for row in attacks}
    for attack_id in sorted(referenced - known):
        problems.append(f"WEAPON_REFERS_TO_UNKNOWN_ATTACK {attack_id}")
    # nothing of the item layer may be silently defaulted: every projectile-capable subtype must exist.
    present = {row["subtype"] for row in weapons}
    for subtype in PROJECTILE_CAPABLE:
        if subtype not in present:
            problems.append(f"MISSING_PROJECTILE_SUBTYPE {subtype}")

    doc = {
        "format": "cubegame.alpha_combat_runtime/1",
        "cube_sha256": sha,
        "functions": {
            "combat_selectAttackVariant": {"va": f"0x{variant.FUNCTION_VA:X}", "bytes": variant.FUNCTION_BYTES,
                                           "sha256": body_sha},
            "combat_getAttackTiming": {"va": f"0x{TIMING_VA:X}", "bytes": timing["bytes"],
                                       "sha256": timing["sha256"], "max_case": timing["max_case"]},
            "combat_getAttackTiming_recover": {"va": f"0x{TIMING_RECOVER_SWITCH_VA:X}",
                                               "bytes": recover_switch["bytes"],
                                               "sha256": recover_switch["sha256"],
                                               "max_case": recover_switch["max_case"]},
            "combat_getReloadFrames": {"va": f"0x{RELOAD_VA:X}", "bytes": reload_table["bytes"],
                                       "sha256": reload_table["sha256"], "max_case": reload_table["max_case"]},
            "combat_getStaminaCost": {"va": f"0x{STAMINA_VA:X}", "bytes": stamina_table["bytes"],
                                      "sha256": stamina_table["sha256"], "max_case": stamina_table["max_case"]},
            "projectile_getMuzzlePosition": {"va": f"0x{MUZZLE_VA:X}",
                                             "head_sha256": sha256_bytes(image.read(MUZZLE_VA, MUZZLE_PROLOGUE_BYTES)),
                                             "call_sites": [f"0x{site:X}" for site in muzzle_sites]},
            "render_build_projectile_mesh": {"va": f"0x{TRAIL_VA:X}",
                                             "head_sha256": sha256_bytes(image.read(TRAIL_VA, TRAIL_PROLOGUE_BYTES)),
                                             "call_sites": [f"0x{site:X}" for site in trail_sites]},
        },
        "checks": checks,
        "projectile_checks": projectile_checks,
        "defaults": {
            "cast": timing["default"],
            "recover": reload_table["default"],
            "stamina": stamina_table["default"],
        },
        "muzzle_gate_a": list(MUZZLE_GATE_A),
        "muzzle_gate_b": list(MUZZLE_GATE_B),
        "bomb_attack_id": BOMB_ATTACK_ID,
        "attacks": attacks,
        "weapons": weapons,
        "unknown": {
            "projectile_flight": "no flying projectile entity in the decoded code: the ranged hit is a "
                                 "raycast from the muzzle position and the visible projectile is a swept "
                                 "mesh rebuilt every frame (render_build_projectile_mesh). Speed, "
                                 "gravity and lifetime of arrow.cub / wood-boomerang1.cub are NOT "
                                 "constants of Cube.exe and stay UNKNOWN.",
            "muzzle_owner": "the muzzle gates are keyed on the current action id (+0x68), which carries "
                            "both the wind-up action and the release attack id; which weapon is holding "
                            "the attack at the moment of the gate is PARTIAL.",
            "arrow_model": "subtypes 9 (arrow) and 14 (quiver) are drawn as held items only; no draw site "
                           "for them as a flying mesh was found.",
        },
        "problems": problems,
        "RESULT": "PASS" if not problems else "FAIL",
    }
    return doc


# --------------------------------------------------------------------------- emit
def _num(value, mode: str) -> str:
    if value is None:
        return "float.NaN"
    if mode == "fixed":
        return f"{int(value)}f"
    text = f"{float(value):.6f}".rstrip("0").rstrip(".")
    return f"{text}f"


def _mode(value: str) -> str:
    return {"fixed": "AlphaCombatValueMode.Fixed",
            "scaled": "AlphaCombatValueMode.ScaledByHasteAndSpeed",
            "unknown": "AlphaCombatValueMode.Unknown"}.get(value, "AlphaCombatValueMode.Unknown")


def _cs(doc: dict) -> str:
    attacks = doc["attacks"]
    weapons = doc["weapons"]
    muzzle_ids = sorted({row["id"] for row in attacks if row["muzzle_path_proven"]})
    attack_lines = "\n".join(
        "            new AlphaAttackCombatDef({id}, {cm}, {cv}, {cc}, {rm}, {rv}, {sm}, {sv},\n"
        "                {mz}, {bomb}, {cd}, {rd}, {sd}),".format(
            id=row["id"],
            cm=_mode(row["cast_mode"]), cv=_num(row["cast"], row["cast_mode"]),
            cc=row["cast_condition_subtype"] if row["cast_condition_subtype"] is not None else -1,
            rm=_mode(row["recover_mode"]), rv=_num(row["recover"], row["recover_mode"]),
            sm=_mode(row["stamina_mode"]), sv=_num(row["stamina"], row["stamina_mode"]),
            mz="true" if row["muzzle_path_proven"] else "false",
            bomb="true" if row["bomb"] else "false",
            cd="true" if row["cast_from_default"] else "false",
            rd="true" if row["recover_from_default"] else "false",
            sd="true" if row["stamina_from_default"] else "false")
        for row in attacks)
    weapon_lines = "\n".join(
        '            new AlphaWeaponCombatDef({type}, {subtype}, "{label}", "{model}", 0x{ae:02X}, 0x{ao:02X}, '
        '"{route}", {muzzle}, {alias}, "{status}"),'.format(
            type=row["item_type"], subtype=row["subtype"], label=row["label"], model=row["model"],
            ae=row["attack_even"], ao=row["attack_odd"], route=row["attack_route"],
            muzzle="true" if row["muzzle_path_proven"] else "false",
            alias="true" if row["alias"] else "false", status=row["status"])
        for row in weapons)
    muzzle_list = ", ".join(str(value) for value in muzzle_ids)
    first_unknown_weapon = max(row["subtype"] for row in weapons) + 1
    return f"""// <auto-generated>
//   cubegame.alpha_combat_runtime/1 — generated by tools/alpha_content/gen_combat_runtime_data.py
//   source: Cube.exe sha256={doc['cube_sha256']}
//   combat_selectAttackVariant  sha256={doc['functions']['combat_selectAttackVariant']['sha256']}
//   combat_getAttackTiming      sha256={doc['functions']['combat_getAttackTiming']['sha256']}
//   combat_getReloadFrames      sha256={doc['functions']['combat_getReloadFrames']['sha256']}
//   combat_getStaminaCost       sha256={doc['functions']['combat_getStaminaCost']['sha256']}
//   status: ALPHA_CODE_STATIC (static decode of the shipped code; no Alpha asset is stored)
//   Regenerate with: python -m tools.alpha_content.gen_combat_runtime_data
// </auto-generated>
namespace CubeGame.Content
{{
    /// <summary>How one Alpha attack number is produced. Numbers are milliseconds or stamina units.</summary>
    public enum AlphaCombatValueMode
    {{
        /// <summary>Alpha returned neither a fixed value nor a base constant: the row stays UNKNOWN.</summary>
        Unknown = 0,

        /// <summary>Alpha returns the number as is (no haste / attack-speed scaling).</summary>
        Fixed = 1,

        /// <summary>Alpha returns <c>base / (haste * attackSpeed)</c>.</summary>
        ScaledByHasteAndSpeed = 2,
    }}

    /// <summary>One Alpha action id (+0x68) with the three numbers the combat code derives from it.</summary>
    public readonly struct AlphaAttackCombatDef
    {{
        public readonly int Id;
        public readonly AlphaCombatValueMode CastMode;
        public readonly float Cast;
        /// <summary>Weapon subtype that switches this row to a second branch (0x0C bracelet), -1 = none.</summary>
        public readonly int CastConditionWeaponSubtype;
        public readonly AlphaCombatValueMode RecoverMode;
        public readonly float Recover;
        public readonly AlphaCombatValueMode StaminaMode;
        public readonly float Stamina;
        /// <summary>Proven to reach projectile_getMuzzlePosition / the swept trail. False = NOT proven.</summary>
        public readonly bool MuzzlePathProven;
        /// <summary>True for the bomb action (fixed range instead of a raycast).</summary>
        public readonly bool Bomb;
        /// <summary>Value came from the function's <c>default:</c> branch, not from an explicit case.</summary>
        public readonly bool CastFromDefault;
        public readonly bool RecoverFromDefault;
        public readonly bool StaminaFromDefault;

        public AlphaAttackCombatDef(int id, AlphaCombatValueMode castMode, float cast, int castCondition,
            AlphaCombatValueMode recoverMode, float recover, AlphaCombatValueMode staminaMode, float stamina,
            bool muzzlePathProven, bool bomb, bool castFromDefault, bool recoverFromDefault,
            bool staminaFromDefault)
        {{
            Id = id;
            CastMode = castMode;
            Cast = cast;
            CastConditionWeaponSubtype = castCondition;
            RecoverMode = recoverMode;
            Recover = recover;
            StaminaMode = staminaMode;
            Stamina = stamina;
            MuzzlePathProven = muzzlePathProven;
            Bomb = bomb;
            CastFromDefault = castFromDefault;
            RecoverFromDefault = recoverFromDefault;
            StaminaFromDefault = staminaFromDefault;
        }}

        /// <summary>True when Alpha knows an explicit case for every one of the three numbers.</summary>
        public bool IsExplicit => !CastFromDefault && !RecoverFromDefault && !StaminaFromDefault;

        /// <summary>Full attack cycle as Alpha computes it: cast + recovery.</summary>
        public float TotalMs => Cast + Recover;
    }}

    /// <summary>One Alpha weapon class (item type 3) and the action ids it selects.</summary>
    public readonly struct AlphaWeaponCombatDef
    {{
        public readonly int ItemType;
        public readonly int Subtype;
        public readonly string Label;
        public readonly string Model;
        public readonly int AttackEven;
        public readonly int AttackOdd;
        /// <summary>Which Alpha branch produced the ids: weapon_6_before_class, class_branch*, parity, weapon_5, unarmed.</summary>
        public readonly string Route;
        /// <summary>Proven to reach the muzzle / swept-trail path. False = NOT proven (not proven-absent).</summary>
        public readonly bool MuzzlePathProven;
        /// <summary>Item-layer alias row (the selector maps it onto an existing weapon model).</summary>
        public readonly bool Alias;
        public readonly string Status;

        public AlphaWeaponCombatDef(int itemType, int subtype, string label, string model, int attackEven,
            int attackOdd, string route, bool muzzlePathProven, bool alias, string status)
        {{
            ItemType = itemType;
            Subtype = subtype;
            Label = label;
            Model = model;
            AttackEven = attackEven;
            AttackOdd = attackOdd;
            Route = route;
            MuzzlePathProven = muzzlePathProven;
            Alias = alias;
            Status = status;
        }}
    }}

    /// <summary>Numbers of Alpha's combat runtime (M0-F2B). Do not edit by hand.</summary>
    public static class AlphaCombatRuntimeData
    {{
        public const string Format = "cubegame.alpha_combat_runtime/1";
        public const string Status = "ALPHA_CODE_STATIC";
        public const string CubeSha256 = "{doc['cube_sha256']}";

        /// <summary>VA of projectile_getMuzzlePosition (spawn position of the ranged hit).</summary>
        public const int MuzzlePositionVa = 0x{doc['functions']['projectile_getMuzzlePosition']['va'][2:]};

        /// <summary>VA of render_build_projectile_mesh (per-frame swept "projectile" mesh).</summary>
        public const int ProjectileTrailMeshVa = 0x{doc['functions']['render_build_projectile_mesh']['va'][2:]};

        /// <summary>Highest action id that Alpha's timer switch knows (0x{doc['functions']['combat_getAttackTiming']['max_case']:02X}).</summary>
        public const int MaxAttackId = 0x{doc['functions']['combat_getAttackTiming']['max_case']:02X};

        /// <summary>Action ids that take the muzzle / swept-trail path (gate A + gate B).</summary>
        public static readonly int[] MuzzleAttackIds = {{ {muzzle_list} }};

        /// <summary>The bomb action: fixed range instead of the voxel raycast.</summary>
        public const int BombAttackId = 0x{doc['bomb_attack_id']:02X};

        /// <summary>First weapon subtype the item-layer evidence does not contain ({first_unknown_weapon}).</summary>
        public const int FirstUnknownWeaponSubtype = {first_unknown_weapon};

        /// <summary>Every action id Alpha's timer switch knows, in order.</summary>
        public static readonly AlphaAttackCombatDef[] Attacks =
        {{
{attack_lines}
        }};

        /// <summary>Every weapon class of the item-layer evidence with its Alpha attack ids.</summary>
        public static readonly AlphaWeaponCombatDef[] Weapons =
        {{
{weapon_lines}
        }};

        /// <summary>True when <paramref name="attackId"/> has a decoded row (no silent default).</summary>
        public static bool TryGetAttack(int attackId, out AlphaAttackCombatDef def)
        {{
            for (int i = 0; i < Attacks.Length; i++)
            {{
                if (Attacks[i].Id == attackId)
                {{
                    def = Attacks[i];
                    return true;
                }}
            }}

            def = default;
            return false;
        }}

        /// <summary>True when <paramref name="subtype"/> is a weapon Alpha's item layer knows.</summary>
        public static bool TryGetWeapon(int subtype, out AlphaWeaponCombatDef def)
        {{
            for (int i = 0; i < Weapons.Length; i++)
            {{
                if (Weapons[i].Subtype == subtype)
                {{
                    def = Weapons[i];
                    return true;
                }}
            }}

            def = default;
            return false;
        }}
    }}
}}
"""


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--cube", default=str(DEFAULT_CUBE))
    ap.add_argument("--item-layer", default=str(ITEM_LAYER))
    ap.add_argument("--check", action="store_true")
    args = ap.parse_args(argv)

    doc = build(Path(args.cube), Path(args.item_layer))
    text_json = json.dumps(doc, indent=1) + "\n"
    text_cs = _cs(doc)
    # determinism: a second build must be byte-identical
    again = build(Path(args.cube), Path(args.item_layer))
    if json.dumps(again, indent=1) + "\n" != text_json or _cs(again) != text_cs:
        doc["problems"].append("NONDETERMINISTIC_OUTPUT")
        print(f"COMBAT_RUNTIME_RESULT=FAIL problems={doc['problems']}")
        return 1

    if args.check:
        drift = []
        if not OUT_JSON.exists() or OUT_JSON.read_text(encoding="utf-8") != text_json:
            drift.append(str(OUT_JSON))
        if not OUT_CS.exists() or OUT_CS.read_text(encoding="utf-8") != text_cs:
            drift.append(str(OUT_CS))
        print(f"COMBAT_RUNTIME_RESULT={'PASS' if not drift and doc['RESULT'] == 'PASS' else 'FAIL'} "
              f"attacks={len(doc['attacks'])} weapons={len(doc['weapons'])} "
              f"checks={len(doc['checks']) + len(doc['projectile_checks'])} problems={doc['problems']} drift={drift}")
        return 0 if not drift and doc["RESULT"] == "PASS" else 1

    OUT_JSON.parent.mkdir(parents=True, exist_ok=True)
    OUT_CS.parent.mkdir(parents=True, exist_ok=True)
    OUT_JSON.write_text(text_json, encoding="utf-8")
    OUT_CS.write_text(text_cs, encoding="utf-8")
    print(f"COMBAT_RUNTIME_RESULT={doc['RESULT']} attacks={len(doc['attacks'])} weapons={len(doc['weapons'])} "
          f"checks={len(doc['checks']) + len(doc['projectile_checks'])} problems={doc['problems']}")
    return 0 if doc["RESULT"] == "PASS" else 1


if __name__ == "__main__":
    sys.exit(main())
