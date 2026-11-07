"""S05B-2 / D69: which creature kinds Alpha spawns as PASSIVE objects, proven from the shipped bytes.

Background (docs/research/alpha2013/BEHAVIOR_SPECS/E_combat.md): creature byte ``+0x60`` is the role /
faction (0 player, 1 hostile monster, 3 ally/pet, 5 friendly NPC, 6 passive). In ``Server.exe`` the
world generator fills a spawn record (record+0x28 = faction, record+0x2c = kind) that is copied into
the creature at 0x535DEB..0x535DF4 (``+0x60 <- rec+0x28``, ``+0x64 <- rec+0x2c``). Two consumers make
"passive" mean "does not move or act":

* 0x5360AE ``cmp byte [esi+0x60], 6 / je`` skips the creation of ``cube::RandomWalkBehavior``
  (RTTI of the object built by 0x41CB90) — a passive creature never wanders;
* 0x53E2A8 ``cmp byte [esi+0x60], 6 / je`` skips the action-timer update of ``+0x68/+0x6c`` in the
  server creature tick — a passive creature never runs an action.

This module re-verifies every instruction it relies on by its exact bytes (stdlib only, no
disassembler) and emits the evidence + the resulting GAME USAGE policy. Nothing of Alpha is copied:
only addresses, opcodes and kind numbers.

    python -m tools.alpha_models.catalog.spawn_roles --out tools/alpha_models/catalog/spawn_role_evidence.json
"""
from __future__ import annotations

import argparse
import hashlib
import json
import struct
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
DEFAULT_SERVER = ROOT / ".scratch/alphatest2/Server.exe"
DEFAULT_CUBE = ROOT / ".scratch/alphatest2/Cube.exe"
EVIDENCE_PATH = Path(__file__).with_name("spawn_role_evidence.json")
FORMAT = "cubegame.spawn_role_evidence/1"
SERVER_SHA256 = "1ffbcbce251e1506a63c9ce18ab0009d5553fcbb9e6202be2bc3a7d870d34fb1"
FACTION_PASSIVE = 6

# (va, exact bytes, meaning). Each spawn group: the faction write and the kind writes that reach it.
SERVER_CHECKS = {
    "spawn_copy": [
        (0x535DEB, "8a4728", "mov al,[edi+0x28]  (record faction)"),
        (0x535DEE, "884660", "mov [esi+0x60],al  (creature faction)"),
        (0x535DF1, "8b472c", "mov eax,[edi+0x2c] (record kind)"),
        (0x535DF4, "894664", "mov [esi+0x64],eax (creature kind)"),
    ],
    "passive_consumers": [
        (0x5360AE, "807e6006", "cmp byte [esi+0x60],6 -> skip RandomWalkBehavior (0x41CB90)"),
        (0x53E2A8, "807e6006", "cmp byte [esi+0x60],6 -> skip action timer +0x68/+0x6c update"),
    ],
}
# group -> (faction write, [(va, bytes, kind)])
PASSIVE_SPAWNS = {
    "ore_deposits (0x51A4DB; rand%10 selector 0x51A4EF)": (
        (0x51A4DB, "c7462806000000"),
        [(0x51A503, "c7462c84000000", 0x84), (0x51A529, "c7462c8a000000", 0x8A),
         (0x51A532, "c7462c89000000", 0x89), (0x51A53B, "c7462c88000000", 0x88),
         (0x51A544, "c7462c87000000", 0x87), (0x51A54D, "c7462c85000000", 0x85),
         (0x51A556, "c7462c83000000", 0x83)]),
    "plants_a (all branches jump to 0x51F2A0)": (
        (0x51F2A0, "c7472806000000"),
        [(0x51F325, "c7472c79000000", 0x79), (0x51F33B, "c7472c7a000000", 0x7A),
         (0x51F351, "c7472c7d000000", 0x7D), (0x51F367, "c7472c7b000000", 0x7B),
         (0x51F3CF, "c7472c80000000", 0x80), (0x51F3DB, "c7472c7c000000", 0x7C),
         # 0x51F295..0x51F29D: eax = rand&1 ? 0x7e : 0x82 -> mov [edi+0x2c],eax, falls into 0x51F2A0
         (0x51F298, "0582000000", 0x82), (0x51F295, "83e0fc", 0x7E)]),
    "plants_b (0x51F4B6)": (
        (0x51F4B6, "c7402806000000"),
        [(0x51F47E, "c7402c78000000", 0x78), (0x51F487, "c7402c7d000000", 0x7D),
         (0x51F496, "c7402c7f000000", 0x7F), (0x51F4A5, "c7402c7b000000", 0x7B)]),
    "scarecrow (0x4E53B2)": ((0x4E53B2, "c7402806000000"), [(0x4E53C5, "c7402c8c000000", 0x8C)]),
    "vase (0x506390)": ((0x506390, "c7402806000000"), [(0x506397, "c7402c8f000000", 0x8F)]),
    "bomb (0x536FBA kind, 0x536FC1 faction on the creature itself)": (
        (0x536FC1, "c6466006"), [(0x536FBA, "c70190000000", 0x90)]),
}
# Cube.exe 0x43E310: role check that groups the training targets (faction 6 and kind 0x8c/0x8d/0x8e)
CUBE_CHECKS = [(0x43E310, "8a4160", "mov al,[ecx+0x60]"), (0x43E32A, "3d8c000000", "cmp eax,0x8c"),
               (0x43E331, "3d8e000000", "cmp eax,0x8e"), (0x43E338, "3d8d000000", "cmp eax,0x8d")]

# Game-usage decisions that are NOT proven by a spawn site (each keeps its own basis, never mixed).
#   PRODUCER_RULE_D67: ordinary plants/flowers/bushes and the egg never walk or attack in CubeGame.
#   SAME_OBJECT_CLASS: same object family as proven passive kinds, no spawn site of its own found.
POLICY_WITHOUT_SPAWN_SITE = {
    0x18: ("PRODUCER_RULE_D67", "Egg (egg1.cub): producer rule — the egg never walks or attacks"),
    0x81: ("PRODUCER_RULE_D67", "FirTree (fir-tree.cub): ordinary plant"),
    0x86: ("SAME_OBJECT_CLASS", "SandstoneDeposit: ore deposit family (0x83..0x8a proven passive)"),
    0x8B: ("SAME_OBJECT_CLASS", "IceCrystalDeposit: ore deposit family (0x83..0x8a proven passive)"),
    0x8D: ("SAME_OBJECT_CLASS", "Aim (aim.cub): training target, grouped with Scarecrow 0x8c by Cube.exe 0x43E310"),
    0x8E: ("SAME_OBJECT_CLASS", "Dummy (dummy.cub): training target, grouped with Scarecrow 0x8c by Cube.exe 0x43E310"),
}


def _sections(data):
    pe = struct.unpack_from("<I", data, 0x3C)[0]
    n = struct.unpack_from("<H", data, pe + 6)[0]
    opt = struct.unpack_from("<H", data, pe + 20)[0]
    base = struct.unpack_from("<I", data, pe + 24 + 28)[0]
    out = []
    for i in range(n):
        _, vs, va, rs, rp = struct.unpack_from("<8sIIII", data, pe + 24 + opt + 40 * i)
        out.append((base + va, max(vs, rs), rp))
    return out


def read_va(data, va, n, sections=None):
    for start, size, raw in sections or _sections(data):
        if start <= va < start + size:
            off = raw + va - start
            return data[off:off + n]
    raise ValueError(f"VA 0x{va:X} outside the image")


def _check(data, secs, va, hexbytes):
    want = bytes.fromhex(hexbytes)
    got = read_va(data, va, len(want), secs)
    return {"va": f"0x{va:X}", "bytes": hexbytes, "ok": got == want}


def build(server=DEFAULT_SERVER, cube=DEFAULT_CUBE):
    sdata = Path(server).read_bytes()
    sha = hashlib.sha256(sdata).hexdigest()
    if sha != SERVER_SHA256:
        raise ValueError(f"Server.exe SHA256 {sha} != {SERVER_SHA256}: addresses not verified for this build")
    ssec = _sections(sdata)
    cdata = Path(cube).read_bytes()
    csec = _sections(cdata)
    checks, problems = {}, []
    for group, rows in SERVER_CHECKS.items():
        checks[group] = [dict(_check(sdata, ssec, va, b), meaning=m) for va, b, m in rows]
    checks["cube_role_check"] = [dict(_check(cdata, csec, va, b), meaning=m) for va, b, m in CUBE_CHECKS]
    kinds = {}
    for group, ((fva, fbytes), writes) in PASSIVE_SPAWNS.items():
        fac = _check(sdata, ssec, fva, fbytes)
        if not fac["ok"]:
            problems.append(f"FACTION_WRITE_BYTES {group} {fac['va']}")
        for va, b, kind in writes:
            w = _check(sdata, ssec, va, b)
            if not w["ok"]:
                problems.append(f"KIND_WRITE_BYTES {group} {w['va']}")
                continue
            k = kinds.setdefault(f"0x{kind:02x}", {"faction": FACTION_PASSIVE, "basis": "ALPHA_CODE_SPAWN",
                                                    "sites": []})
            k["sites"].append({"group": group, "faction_write": fac["va"], "kind_write": w["va"]})
    for group in checks.values():
        problems += [f"CHECK_BYTES {c['va']}" for c in group if not c["ok"]]
    for kind, (basis, note) in POLICY_WITHOUT_SPAWN_SITE.items():
        k = f"0x{kind:02x}"
        if k in kinds:
            problems.append(f"POLICY_OVERLAPS_PROVEN {k}")
            continue
        kinds[k] = {"faction": None, "basis": basis, "note": note, "sites": []}
    return {"format": FORMAT, "decision": "D69", "server_sha256": sha,
            "cube_sha256": hashlib.sha256(cdata).hexdigest(),
            "meaning": {"faction_6": "passive: no RandomWalkBehavior (0x5360AE), no action timer (0x53E2A8)"},
            "checks": checks, "passive_kinds": dict(sorted(kinds.items())),
            "problems": problems, "RESULT": "PASS" if not problems else "FAIL"}


# --------------------------------------------------------------------------- consumers
def load_evidence(path=EVIDENCE_PATH):
    with open(path, encoding="utf-8") as fh:
        return json.load(fh)


def game_policy(kind, evidence=None):
    """GAME USAGE of a kind in CubeGame: ('STATIC_OBJECT', basis, reason) or ('CREATURE', None, None).

    STATIC_OBJECT = no walk, no actions in the game controller; the research clips stay untouched."""
    ev = evidence if evidence is not None else load_evidence()
    rec = ev["passive_kinds"].get(f"0x{int(kind):02x}")
    if not rec:
        return "CREATURE", None, None
    if rec["basis"] == "ALPHA_CODE_SPAWN":
        sites = ", ".join(s["kind_write"] for s in rec["sites"])
        return ("STATIC_OBJECT", "ALPHA_CODE_SPAWN",
                f"Alpha spawns this kind with faction 6 (passive; Server.exe kind write {sites}): "
                "no RandomWalkBehavior (0x5360AE), no action timer (0x53E2A8)")
    return "STATIC_OBJECT", rec["basis"], rec["note"]


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--server", default=str(DEFAULT_SERVER))
    ap.add_argument("--cube", default=str(DEFAULT_CUBE))
    ap.add_argument("--out", default=str(EVIDENCE_PATH))
    a = ap.parse_args()
    doc = build(a.server, a.cube)
    with open(a.out, "w", encoding="utf-8") as fh:
        json.dump(doc, fh, indent=1)
    print(f"SPAWN_ROLE_RESULT={doc['RESULT']} passive_kinds={len(doc['passive_kinds'])} problems={doc['problems']}")


if __name__ == "__main__":
    main()
