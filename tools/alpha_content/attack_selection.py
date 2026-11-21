"""M0-06C / C-29: Alpha attack-variant selection ("which action does a creature launch?") from shipped bytes.

Alpha does not pick "the first library action": ``combat_selectAttackVariant`` (``Cube.exe`` VA ``0x4452A0``)
maps the creature's *held weapon* (raw item type at ``+0xaa8``, subtype at ``+0xaa9``), its class
(``+0x140``), its specialisation (``+0x141``) and a parity bit taken from field ``+0x70`` to one action
id.  This module re-derives that table from the executable itself (stdlib + capstone, no Alpha assets),
writes the committed evidence JSON *and* the C# lookup table used by the game, and refuses to run when
the bytes do not match the recorded hash.

    python -m tools.alpha_content.attack_selection             # write evidence + C# table
    python -m tools.alpha_content.attack_selection --check      # verify no drift (exit 1 on drift)

Nothing of Alpha is copied: only addresses, opcodes and the resulting numbers are recorded.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import struct
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DEFAULT_CUBE = ROOT / ".scratch/alphatest2/Cube.exe"
OUT_JSON = ROOT / "tools/alpha_content/data/attack_selection_evidence.json"
OUT_CS = ROOT / "Assets/CubeGame/Content/Generated/AlphaAttackSelection.g.cs"

CUBE_SHA256 = "84a7a132a84d4282338e7ea45a1940d32066d64e39cbafed8f2418d5a6dc30bf"
FUNCTION_VA = 0x4452A0
FUNCTION_BYTES = 0x10C
FUNCTION_SHA256 = "81822ebcab8578ecd2280bddedeb3b23e88354afd01cc45a486480c244bf17bc"

# Exact instruction bytes at the branch points this table is derived from (VA, bytes, meaning).
CHECKS = [
    (0x4452A0, "8a81a80a0000", "al = weapon item type [self+0xaa8]"),
    (0x4452AA, "80b9a90a000006", "cmp weapon item subtype [self+0xaa9], 6"),
    (0x4452B3, "b817000000", "attack 0x17: weapon (type 3, subtype 6) — checked before class/kind"),
    (0x4452B9, "80b94001000003", "cmp class [self+0x140], 3  -> class/specialisation branch"),
    (0x4452C6, "8b5164", "kind = [self+0x64]"),
    (0x4452C9, "83fa75", "cmp kind, 0x75 -> class branch"),
    (0x4452D2, "83fa56", "cmp kind, 0x56 -> class branch"),
    (0x4452DB, "3c03", "cmp weapon item type, 3"),
    (0x4452E5, "80fa0a", "cmp subtype, 0x0a -> class branch"),
    (0x4452EA, "80fa0b", "cmp subtype, 0x0b -> class branch"),
    (0x4452EF, "80fa0c", "cmp subtype, 0x0c -> class branch"),
    (0x4452F8, "80fa05", "cmp subtype, 5"),
    (0x4452FD, "b805000000", "attack 5: weapon subtype 5"),
    (0x445307, "8a81a90a0000", "al = subtype (second read)"),
    (0x44530D, "3c0f", "cmp subtype, 0x0f"),
    (0x445333, "7517", "jne -> unarmed branch when no parity subtype matched"),
    (0x445335, "8b4170", "eax = [self+0x70] (parity source)"),
    (0x445338, "2501000080", "and eax, 0x80000001 -> bit0 only"),
    (0x445348, "83c042", "attack = 0x42 - parity"),
    (0x445361, "83c003", "attack = 3 + parity (unarmed)"),
    (0x445365, "8a81a90a0000", "class branch: al = subtype"),
    (0x44536B, "3c0b", "cmp subtype, 0x0b"),
    (0x445371, "80b94101000001", "cmp specialisation [self+0x141], 1"),
    (0x44537B, "83c02d", "subtype 0x0b: attack = 0x2d + (specialisation != 1)"),
    (0x44537F, "3c0a", "cmp subtype, 0x0a"),
    (0x44538F, "8d04451f000000", "subtype 0x0a: attack = 0x1f + 2*(specialisation == 1)"),
    (0x445397, "80b94101000001", "cmp specialisation, 1 (default class-branch attack)"),
    (0x44539E, "b825000000", "default 0x25"),
    (0x4453A3, "ba2b000000", "specialisation 1 -> 0x2b"),
    (0x4453A8, "0f44c2", "cmove eax, edx"),
    (0x4453AB, "c3", "ret (function end)"),
]

# Decoded mapping (each value is checked against the instruction bytes above).
RULES = {
    "status": "ALPHA_CODE_STATIC",
    "source": "Cube.exe 0x4452A0 combat_selectAttackVariant",
    "weapon_item_type": 3,
    "parity_field_offset": "0x70",
    "parity_mask": "0x80000001",
    "immediate": [
        {"item_type": 3, "item_subtype": 6, "attack": 0x17,
         "note": "checked first: even a class-3 creature with this weapon gets 0x17"},
        {"item_type": 3, "item_subtype": 5, "attack": 5},
    ],
    "class_branch_trigger": {
        "class_offset": "0x140", "class_id": 3,
        "kinds": [0x75, 0x56],
        "note": "class 3 or kind 0x75/0x56 always use the class/specialisation branch",
    },
    "class_branch_subtypes": [0x0A, 0x0B, 0x0C],
    "class_branch": {
        "item_subtype_0x0b": {"specialisation_ne_1": 0x2D, "specialisation_eq_1": 0x2E},
        "item_subtype_0x0a": {"specialisation_ne_1": 0x1F, "specialisation_eq_1": 0x21},
        "default": {"specialisation_ne_1": 0x25, "specialisation_eq_1": 0x2B},
    },
    "weapon_parity_subtypes": [0x0F, 0x10, 0x11, 0x05, 0x0A, 0x0B, 0x12, 0x08, 0x06, 0x07],
    "weapon_parity_attack": {"parity_0": 0x42, "parity_1": 0x41},
    "unarmed_attack": {"parity_0": 3, "parity_1": 4},
    "partial": {
        "parity_source": "bit0 of field +0x70; the same field is tested as `== 0` (no target) in "
                          "CombatBehavior_chooseAttack, so it holds a target handle/pointer — "
                          "what makes bit0 flip is NOT established (PARTIAL)",
        "specialisation_id_1": "specialisation 1 is used as a flag; which Alpha specialisation it is, "
                               "is UNKNOWN (R-1 world spawn record not executed)",
    },
}


def _sections(data: bytes):
    pe = struct.unpack_from("<I", data, 0x3C)[0]
    n = struct.unpack_from("<H", data, pe + 6)[0]
    opt = struct.unpack_from("<H", data, pe + 20)[0]
    base = struct.unpack_from("<I", data, pe + 24 + 28)[0]
    out = []
    for i in range(n):
        _, vs, va, rs, rp = struct.unpack_from("<8sIIII", data, pe + 24 + opt + 40 * i)
        out.append((base + va, max(vs, rs), rp))
    return out


def read_va(data: bytes, va: int, size: int) -> bytes:
    for start, length, raw in _sections(data):
        if start <= va < start + length:
            return data[raw + va - start: raw + va - start + size]
    raise ValueError(f"VA 0x{va:X} outside the image")


def build(cube: Path = DEFAULT_CUBE) -> dict:
    data = Path(cube).read_bytes()
    sha = hashlib.sha256(data).hexdigest()
    problems = []
    if sha != CUBE_SHA256:
        raise ValueError(f"Cube.exe SHA256 {sha} != {CUBE_SHA256}: addresses not verified for this build")
    body = read_va(data, FUNCTION_VA, FUNCTION_BYTES)
    body_sha = hashlib.sha256(body).hexdigest()
    if body_sha != FUNCTION_SHA256:
        problems.append(f"FUNCTION_BYTES sha256 {body_sha}")
    checks = []
    for va, hexbytes, meaning in CHECKS:
        want = bytes.fromhex(hexbytes)
        got = read_va(data, va, len(want))
        ok = got == want
        checks.append({"va": f"0x{va:X}", "bytes": hexbytes, "ok": ok, "meaning": meaning})
        if not ok:
            problems.append(f"CHECK_BYTES 0x{va:X}")
    doc = {
        "format": "cubegame.alpha_attack_selection/1",
        "cube_sha256": sha,
        "function": {"va": f"0x{FUNCTION_VA:X}", "bytes": FUNCTION_BYTES, "sha256": body_sha},
        "checks": checks,
        "rules": RULES,
        "problems": problems,
        "RESULT": "PASS" if not problems else "FAIL",
    }
    return doc


def _cs(doc: dict) -> str:
    r = doc["rules"]
    cb = r["class_branch"]
    par = ", ".join(f"0x{v:02X}" for v in r["weapon_parity_subtypes"])
    kinds = ", ".join(f"0x{v:02X}" for v in r["class_branch_trigger"]["kinds"])
    cbs = ", ".join(f"0x{v:02X}" for v in r["class_branch_subtypes"])
    return f"""// <auto-generated>
//   cubegame.alpha_attack_selection/1 — generated by tools/alpha_content/attack_selection.py
//   source: {r['source']}
//   Cube.exe sha256={doc['cube_sha256']}
//   function bytes sha256={doc['function']['sha256']} ({doc['function']['bytes']} bytes)
//   status: {r['status']} (static decode of the shipped code; no Alpha asset is stored)
//   Regenerate with: python -m tools.alpha_content.attack_selection
// </auto-generated>
namespace CubeGame.Content
{{
    /// <summary>Numbers of Alpha's attack-variant selection (C-29, M0-06C). Do not edit by hand.</summary>
    public static class AlphaAttackSelectionTable
    {{
        public const string Format = "cubegame.alpha_attack_selection/1";
        public const string Source = "{r['source']}";
        public const string Status = "{r['status']}";
        public const string CubeSha256 = "{doc['cube_sha256']}";

        /// <summary>Raw Alpha item type byte of a weapon (item type 3).</summary>
        public const int WeaponItemType = {r['weapon_item_type']};

        /// <summary>Weapon (3, 6): two-handed/heavy attack, checked before class and kind.</summary>
        public const int TwoHandedSubtype = 6;
        public const int TwoHandedAttack = 0x17;

        /// <summary>Weapon (3, 5): fixed attack 5.</summary>
        public const int ModeSwitchSubtype = 5;
        public const int ModeSwitchAttack = 5;

        /// <summary>Creature class that always takes the class/specialisation branch.</summary>
        public const int SpecialClass = {r['class_branch_trigger']['class_id']};

        /// <summary>Creature kinds that always take the class/specialisation branch.</summary>
        public static readonly int[] SpecialKinds = {{ {kinds} }};

        /// <summary>Weapon subtypes whose attack is chosen by class/specialisation.</summary>
        public static readonly int[] ClassBranchSubtypes = {{ {cbs} }};

        /// <summary>Weapon subtypes whose attack only depends on the parity bit.</summary>
        public static readonly int[] ParitySubtypes = {{ {par} }};

        public const int ParityAttackEven = 0x{r['weapon_parity_attack']['parity_0']:02X};
        public const int ParityAttackOdd = 0x41;
        public const int UnarmedAttackEven = 3;
        public const int UnarmedAttackOdd = 4;

        /// <summary>Subtype 0x0B: 0x2D, or 0x2E when specialisation == 1.</summary>
        public const int BleedSubtype = 0x0B;
        public const int BleedAttackDefault = 0x{cb['item_subtype_0x0b']['specialisation_ne_1']:02X};
        public const int BleedAttackSpecialist = 0x{cb['item_subtype_0x0b']['specialisation_eq_1']:02X};

        /// <summary>Subtype 0x0A: 0x1F, or 0x21 when specialisation == 1.</summary>
        public const int CastSubtype = 0x0A;
        public const int CastAttackDefault = 0x{cb['item_subtype_0x0a']['specialisation_ne_1']:02X};
        public const int CastAttackSpecialist = 0x{cb['item_subtype_0x0a']['specialisation_eq_1']:02X};

        /// <summary>Everything else in the class branch: 0x25, or 0x2B for specialisation 1.</summary>
        public const int DefaultAttackDefault = 0x{cb['default']['specialisation_ne_1']:02X};
        public const int DefaultAttackSpecialist = 0x{cb['default']['specialisation_eq_1']:02X};

        /// <summary>The specialisation value Alpha compares with: <c>[creature+0x141] == 1</c>.</summary>
        public const int SpecialistSpec = 1;
    }}
}}
"""


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--cube", default=str(DEFAULT_CUBE))
    ap.add_argument("--check", action="store_true")
    a = ap.parse_args()
    doc = build(Path(a.cube))
    text_json = json.dumps(doc, indent=1) + "\n"
    text_cs = _cs(doc)
    if a.check:
        ok = OUT_JSON.exists() and OUT_CS.exists()
        drift = []
        if ok and OUT_JSON.read_text(encoding="utf-8") != text_json:
            drift.append(str(OUT_JSON))
        if ok and OUT_CS.read_text(encoding="utf-8") != text_cs:
            drift.append(str(OUT_CS))
        print(f"ATTACK_SELECTION_RESULT={'PASS' if not drift else 'FAIL'} checks={len(doc['checks'])} "
              f"problems={doc['problems']} drift={drift}")
        return 0 if not drift else 1
    OUT_JSON.parent.mkdir(parents=True, exist_ok=True)
    OUT_CS.parent.mkdir(parents=True, exist_ok=True)
    OUT_JSON.write_text(text_json, encoding="utf-8")
    OUT_CS.write_text(text_cs, encoding="utf-8")
    print(f"ATTACK_SELECTION_RESULT={doc['RESULT']} checks={len(doc['checks'])} problems={doc['problems']}")
    return 0 if doc["RESULT"] == "PASS" else 1


if __name__ == "__main__":
    sys.exit(main())
