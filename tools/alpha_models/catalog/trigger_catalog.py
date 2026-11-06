"""D64 / B-1: creature-AI action launches for EVERY kind, by executing Alpha's AI bytes, with the NPC
state made an explicit CONDITION instead of requiring a live observation.

Finding: the attack-choice region of ``CombatBehavior::vfunc_0`` (``ai_trigger``, VA 0x42D87F) reads,
besides the world inputs ai_trigger already sweeps, exactly these creature fields for every kind that
attacks at all: class ``+0x140``, specialization ``+0x141``, weapon ``+0xaa8/+0xaa9`` and off-hand
``+0x990/+0x991`` (read-logged emulation, ``reads`` in the output). In Server.exe the spawn copies
class/spec from the world spawn record (VA 0x535DF7: record+0x30/+0x31 -> creature+0x140/+0x141) and
then applies per-kind overrides (VA 0x522719..0x52273C: kind 0x68 -> class 2 spec 1, kind 0x56 ->
class 3 spec 1). Which class a given world spawn record carries is world-generation data, NOT a
property of the kind; therefore an action is recorded together with the (class, spec, weapon,
off-hand) conditions under which Alpha's own code launches it. Nothing is assumed about which of
those conditions a live NPC had.

Evidence status of a record: ``TRIGGER_EMULATED_CONDITIONAL`` (each action carries its conditions;
``unconditional`` = launched under every swept condition). ``live_check = NOT_CAPTURED``.
Kinds whose AI region never writes an action: ``NO_AI_ATTACK`` (passive / not a combat kind) — a
proven negative for this region, not a guess.

python -m tools.alpha_models.catalog.trigger_catalog --out .scratch/catalog/trigger_catalog.json
"""
from __future__ import annotations

import argparse
import itertools
import json
import struct
import sys
from concurrent.futures import ProcessPoolExecutor
from pathlib import Path

from tools.alpha_models.anim import pe_bytes
from tools.alpha_models.universal import ai_trigger as at
from tools.alpha_models.universal import alpha_oracle as ao
from tools.alpha_models.universal import alpha_static as st
from tools.alpha_models.universal import anim_states as an

ROOT = Path(__file__).resolve().parents[3]
DEFAULT_EXE = ROOT / ".scratch/alphatest2/Cube.exe"

CLASSES = range(0, 5)          # 0 = none, 1..4 Alpha classes
SPECS = range(0, 2)
WEAPONS = [(0, 0)] + [(3, s) for s in range(0, 26)]     # none + every weapon subtype (type 3)
OFFHANDS = [(0, 0), (4, 0)]                              # none, shield (type 4)


def conditions():
    """class x spec x weapon with an empty off-hand, plus a shield on the unarmed creature."""
    out = [(c, s, w, (0, 0)) for c, s, w in itertools.product(CLASSES, SPECS, WEAPONS)]
    out += [(c, s, (0, 0), (4, 0)) for c, s in itertools.product(CLASSES, SPECS)]
    return out
SERVER_CLASS_OVERRIDES = {0x68: (2, 1), 0x56: (3, 1)}     # Server.exe 0x522719..0x52273C

# reduced world grid per condition (ai_trigger.sweep uses the full grid for single conditions)
GRID_DIST = at.DIST_CASES
GRID_RES = (0.0, 0.81, 1.0)
GRID_RAND = at.RAND_CASES
GRID_COOLDOWN = {"no_cooldown": (), "cooldown_0x36": (0x36,)}


def base_creature(image, kind, seed=(0, 0)):
    raw = st.AppearanceEmulator(image).run(kind, *seed)["raw"]
    c = bytearray(0x2000)
    c[0x78:0x78 + len(raw)] = raw
    struct.pack_into("<I", c, 0x64, kind)
    for off, fmt, v in ao.RUNTIME_DEFAULTS:
        struct.pack_into(fmt, c, off, v)
    c[0x5C] |= 1
    c[0x60] = 1
    return c


def launch_set(image, c):
    """Actions the region writes over the reduced grid, with closure over produced prior actions."""
    acts, errors, seen, queue = {}, set(), set(), [0]
    while queue:
        prior = queue.pop(0)
        if prior in seen:
            continue
        seen.add(prior)
        for d2, res, rs, cnt, cds in itertools.product(GRID_DIST.values(), GRID_RES, GRID_RAND.values(), (0, 1),
                                                       GRID_COOLDOWN.values()):
            r = at.run_region(image, c, dist2=d2, resource=res, rand_seq=rs, counter=cnt, prior_action=prior,
                              cooldowns=cds)
            if r["error"]:
                errors.add(r["error"])
                continue
            if r["writes"]:
                va, a = r["writes"][-1]
                acts.setdefault(a, set()).add(va)
                if a not in seen:
                    queue.append(a)
    return {a: sorted(v) for a, v in acts.items()}, sorted(errors)


def kind_record(image, kind, seed=(0, 0)):
    c0 = base_creature(image, kind, seed)
    by_cond, errors = {}, set()
    for cls, spec, wpn, off in conditions():
        c = bytearray(c0)
        c[0x140], c[0x141] = cls, spec
        an.equip(c, 0xAA8, *wpn)
        an.equip(c, 0x990, *off)
        acts, err = launch_set(image, c)
        errors.update(err)
        by_cond[(cls, spec, wpn, off)] = acts
    n = len(by_cond)
    actions = {}
    for cond, acts in by_cond.items():
        for a, vas in acts.items():
            e = actions.setdefault(a, {"write_vas": set(), "conditions": []})
            e["write_vas"].update(vas)
            e["conditions"].append(cond)
    out_actions = {}
    for a, e in sorted(actions.items()):
        conds = e["conditions"]
        out_actions[str(a)] = {
            "write_vas": [f"0x{v:06X}" for v in sorted(e["write_vas"])],
            "unconditional": len(conds) == n,
            "condition_count": len(conds),
            "conditions": _compress(conds),
        }
    default = SERVER_CLASS_OVERRIDES.get(kind, (0, 0))
    default_acts = by_cond[(default[0], default[1], (0, 0), (0, 0))]
    status = "TRIGGER_EMULATED_CONDITIONAL" if out_actions else "NO_AI_ATTACK"
    if errors:
        status = "PARTIAL"
    return {"kind": f"0x{kind:02x}", "seed": list(seed), "status": status,
            "appearance_flags_7e": f"0x{struct.unpack_from('<H', c0, 0x7E)[0]:04x}",
            "conditions_swept": n, "actions": out_actions,
            "default_state": {"class_spec": list(default), "weapon": "none", "offhand": "none",
                              "source": "Server.exe 0x522719 override" if kind in SERVER_CLASS_OVERRIDES
                              else "class 0 / unarmed (no override for this kind)",
                              "actions": sorted(default_acts)},
            "errors": sorted(errors), "live_check": "NOT_CAPTURED"}


def _compress(conds):
    """Conditions as a list of {class, spec, weapon, offhand} groups (cartesian blocks)."""
    by_ws = {}
    for cls, spec, wpn, off in conds:
        by_ws.setdefault((wpn, off), set()).add((cls, spec))
    blocks = {}
    for (wpn, off), cs in by_ws.items():
        blocks.setdefault(json.dumps(sorted(cs)), []).append((wpn, off))
    out = []
    for cs, wos in blocks.items():
        weapons = sorted({w for w, _ in wos})
        offs = sorted({o for _, o in wos})
        out.append({"class_spec": json.loads(cs),
                    "weapons": "all_swept" if len(weapons) == len(WEAPONS) else [list(w) for w in weapons],
                    "offhands": [list(o) for o in offs]})
    return out


def _worker(args):
    exe, kinds = args
    image = pe_bytes.load(exe)
    out = []
    for k in kinds:
        try:
            out.append(kind_record(image, k))
        except Exception as e:  # noqa: BLE001 — reported per kind, never guessed
            out.append({"kind": f"0x{k:02x}", "status": "PARTIAL", "errors": [f"{type(e).__name__}: {e}"[:200]]})
    return out


def build(exe=DEFAULT_EXE, kinds=None, workers=11):
    st.verify_exe(exe)
    kinds = list(kinds if kinds is not None else range(0, st.KIND_MAX + 1))
    chunks = [(str(exe), kinds[i::workers]) for i in range(workers)]
    recs = []
    with ProcessPoolExecutor(workers) as ex:
        for r in ex.map(_worker, chunks):
            recs += r
    recs.sort(key=lambda r: int(r["kind"], 16))
    counts = {}
    for r in recs:
        counts[r["status"]] = counts.get(r["status"], 0) + 1
    return {"format": "cubegame.alpha_trigger_catalog/1", "decision": "D64",
            "region": "CombatBehavior::vfunc_0 0x42D87F..0x42EAEF (executed)",
            "condition_domains": {"class": list(CLASSES), "spec": list(SPECS), "weapons": WEAPONS,
                                  "offhands": OFFHANDS, "count": len(conditions()),
                                  "note": "shield only on the unarmed creature"},
            "world_grid": {"distance": list(GRID_DIST), "resource_170": GRID_RES, "rand": list(GRID_RAND),
                           "counter_70": [0, 1], "cooldowns": list(GRID_COOLDOWN)},
            "server_class_overrides": {f"0x{k:02x}": v for k, v in SERVER_CLASS_OVERRIDES.items()},
            "metrics": {"kinds": len(recs), "by_status": counts},
            "kinds": {r["kind"]: r for r in recs}}


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--exe", default=str(DEFAULT_EXE))
    ap.add_argument("--out", required=True)
    ap.add_argument("--kinds", help="comma list, e.g. 0x4c,0x57 (default: all)")
    ap.add_argument("--workers", type=int, default=11)
    a = ap.parse_args(argv)
    out = Path(a.out).resolve()
    if not out.is_relative_to(ROOT / ".scratch"):
        raise SystemExit("ALPHA_OUTPUT_MUST_BE_IN_SCRATCH")
    kinds = [int(x, 0) for x in a.kinds.split(",")] if a.kinds else None
    res = build(a.exe, kinds, a.workers)
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(res, indent=1), encoding="utf-8")
    print(json.dumps({"TRIGGER_CATALOG": "DONE", **res["metrics"]}))
    return 0




# --------------------------------------------------------------------------- committed evidence (D64)
EVIDENCE_PATH = Path(__file__).with_name("npc_trigger_evidence.json")
EVIDENCE_FORMAT = "cubegame.npc_trigger_evidence/1"
# code the evidence depends on: any change of these files (or of Cube.exe) invalidates it
EVIDENCE_CODE = ("tools/alpha_models/universal/ai_trigger.py", "tools/alpha_models/catalog/trigger_catalog.py",
                 "tools/alpha_models/universal/drawmodel_emu.py", "tools/alpha_models/universal/x86emu.py",
                 "tools/alpha_models/universal/alpha_static.py", "tools/alpha_models/universal/anim_states.py")


def code_hashes(root=ROOT):
    import hashlib
    return {p: hashlib.sha256((Path(root) / p).read_bytes().replace(b"\r\n", b"\n")).hexdigest()
            for p in EVIDENCE_CODE}


def export_evidence(catalog, static_table, out=EVIDENCE_PATH):
    """Per kind: unarmed launches by class/spec, weapon-conditional launches, family id (for the planner)."""
    from tools.alpha_models.universal import creature_rig as cr
    kinds = {}
    for k, r in catalog["kinds"].items():
        e = static_table["kinds"].get(k, {})
        app = e.get("appearance")
        fam = cr.resolve(int(k, 16), app).family_id if app else None
        unarmed, weapon = {}, {}
        for a, rec in (r.get("actions") or {}).items():
            for blk in rec["conditions"]:
                ws = blk["weapons"]
                if ws == "all_swept" or [0, 0] in ws:
                    for cs in blk["class_spec"]:
                        unarmed.setdefault(a, {"write_vas": rec["write_vas"], "class_spec": set()})["class_spec"].add(tuple(cs))
                if ws == "all_swept" or any(w != [0, 0] for w in ws):
                    weapon.setdefault(a, {"write_vas": rec["write_vas"], "weapon_conditions": []})
                    weapon[a]["weapon_conditions"].append(blk)
        kinds[k] = {"kind": k, "name_hint": e.get("name_hint"), "status": r["status"], "family_id": fam,
                    "appearance_flags_7e": r.get("appearance_flags_7e"),
                    "unarmed_actions": {a: {"write_vas": v["write_vas"], "class_spec": sorted(map(list, v["class_spec"]))}
                                        for a, v in sorted(unarmed.items(), key=lambda x: int(x[0]))},
                    "weapon_actions": {a: v for a, v in sorted(weapon.items(), key=lambda x: int(x[0]))},
                    "default_state": r.get("default_state"), "errors": r.get("errors", []),
                    "live_check": "NOT_CAPTURED", "equipment_observed": False}
    ev = {"format": EVIDENCE_FORMAT, "decision": "D64", "tool": "tools/alpha_models/catalog/trigger_catalog.py",
          "exe_sha256": st.EXPECTED_EXE_SHA256, "code_sha256": code_hashes(),
          "region": catalog["region"], "condition_domains": catalog["condition_domains"],
          "world_grid": catalog["world_grid"], "server_class_overrides": catalog["server_class_overrides"],
          "kinds": kinds}
    Path(out).write_text(json.dumps(ev, indent=1), encoding="utf-8")
    return ev


def evidence_drift(path=EVIDENCE_PATH, root=ROOT):
    """Problems when the committed evidence no longer matches the code it was produced by."""
    if not Path(path).exists():
        return ["TRIGGER_EVIDENCE_MISSING"]
    ev = json.loads(Path(path).read_text(encoding="utf-8"))
    now = code_hashes(root)
    return [f"TRIGGER_EVIDENCE_CODE_DRIFT {p}" for p, h in ev.get("code_sha256", {}).items() if now.get(p) != h]


def export_main(argv=None):
    ap = argparse.ArgumentParser(description="export the committed trigger evidence from a trigger catalog")
    ap.add_argument("--catalog", default=str(ROOT / ".scratch/catalog/trigger_catalog.json"))
    ap.add_argument("--static", default=str(ROOT / ".scratch/catalog/alpha_static.json"))
    ap.add_argument("--out", default=str(EVIDENCE_PATH))
    a = ap.parse_args(argv)
    ev = export_evidence(json.loads(Path(a.catalog).read_text(encoding="utf-8")),
                         json.loads(Path(a.static).read_text(encoding="utf-8")), a.out)
    counts = {}
    for r in ev["kinds"].values():
        counts[r["status"]] = counts.get(r["status"], 0) + 1
    print(json.dumps({"TRIGGER_EVIDENCE": "EXPORTED", "kinds": len(ev["kinds"]), "by_status": counts,
                      "kinds_with_unarmed_actions": sum(1 for r in ev["kinds"].values() if r["unarmed_actions"])}))
    return 0


if __name__ == "__main__":
    sys.exit(export_main(sys.argv[2:]) if len(sys.argv) > 1 and sys.argv[1] == "export" else main())
