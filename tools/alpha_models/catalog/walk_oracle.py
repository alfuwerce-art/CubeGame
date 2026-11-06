"""D64 / B-2 + B-4: the walk formula of every kind checked against Alpha's OWN drawModel, executed.

``param_walk.joints`` (the formula the Unity runtime evaluates) is compared with
``alpha_oracle`` — ``Creature_drawModel`` (VA 0x4128F0) executed byte by byte in ``drawmodel_emu``
on the kind's own appearance (``creature_generateAppearance`` executed). The oracle was verified
against live captures of three different motion families to float precision
(docs/research/alpha2013/ALPHA_ANIMATION_SYSTEM.md); executing it needs no live NPC.

Status per kind (all appearance variants that change motion parameters are checked):
  ORACLE_VERIFIED      formula == executed Alpha bytes (pos <= 1e-4 vox, rot <= 1e-3 deg) on every
                       sample (5 phase/amplitude points x on-ground/airborne) and every modelled part;
                       kind-specific drawModel branches (0x57/0x60/0x65) are executed, so they are
                       covered by construction.
  ORACLE_MISMATCH      the formula differs from Alpha: the formula must not be used for this kind.
  UNMODELLED_PARTS     some drawn part is not produced by the formula (listed).
  NO_WALK              the kind has no part the formula models.
This is the B-4 trust domain: ``ALPHA_FORMULA_VERIFIED`` = ``ORACLE_VERIFIED`` (Alpha code executed),
``live_check`` stays ``NOT_CAPTURED`` unless a live capture exists.

python -m tools.alpha_models.catalog.walk_oracle --out tools/alpha_models/catalog/walk_oracle_evidence.json
"""
from __future__ import annotations

import argparse
import json
import sys
from concurrent.futures import ProcessPoolExecutor
from pathlib import Path

from tools.alpha_models.anim import pe_bytes as pb
from tools.alpha_models.universal import alpha_oracle as ao
from tools.alpha_models.universal import alpha_static as st

ROOT = Path(__file__).resolve().parents[3]
DEFAULT_EXE = ROOT / ".scratch/alphatest2/Cube.exe"
DEFAULT_SIZES = ROOT / ".scratch/catalog/registry_sizes.json"   # D64: rebuilt from the fixed registry
EVIDENCE_PATH = Path(__file__).with_name("walk_oracle_evidence.json")
FORMAT = "cubegame.walk_oracle_evidence/1"
POS_TOL, ROT_TOL = 1e-4, 1e-3


def motion_seed_representatives(image, kind, sweep=st.SEED_SWEEP):
    """One seed per distinct motion-parameter signature (same sweep as alpha_static.kind_table)."""
    emu = st.AppearanceEmulator(image)
    reps = {}
    for s in range(sweep):
        for seed in ((s, 0), (0, s)):
            d = st.decode_appearance(emu.run(kind, *seed)["raw"])
            sig = json.dumps(st.motion_params(d), sort_keys=True)
            reps.setdefault(sig, list(seed))
    return list(reps.values())


def check_kind(oracle, kind):
    from tools.alpha_models.universal import param_walk as pw
    seeds = motion_seed_representatives(oracle.image, kind)
    worst = {"pos": 0.0, "rot": 0.0}
    unmodelled, modelled, per_seed = set(), set(), []
    for seed in seeds:
        w = _compare(oracle, kind, tuple(seed), pw)
        per_seed.append({"seed": seed, "pos": w["pos"], "rot": w["rot"], "parts": sorted(w["parts"])})
        worst["pos"], worst["rot"] = max(worst["pos"], w["pos"]), max(worst["rot"], w["rot"])
        unmodelled.update(w["unmodelled"])
        modelled.update(w["parts"])
    if not modelled:
        status = "NO_WALK"
    elif worst["pos"] > POS_TOL or worst["rot"] > ROT_TOL:
        status = "ORACLE_MISMATCH"
    elif unmodelled:
        status = "UNMODELLED_PARTS"
    else:
        status = "ORACLE_VERIFIED"
    return {"kind": f"0x{kind:02x}", "status": status, "max_pos_vox": worst["pos"], "max_rot_deg": worst["rot"],
            "motion_seeds": seeds, "parts": sorted(modelled), "unmodelled_parts": sorted(unmodelled),
            "kind_branch": st.KIND_SPECIAL_BRANCHES.get(kind), "per_seed": per_seed,
            "live_check": "NOT_CAPTURED"}


def _compare(oracle, kind, seed, pw, samples=((0.0, 0.0), (0.8, 0.19), (2.2, 0.19), (4.0, 0.12), (5.5, 0.06))):
    from tools.alpha_models.anim.alpha_clip import pose_error
    worst = {"pos": 0.0, "rot": 0.0, "parts": set(), "unmodelled": set()}
    for og in (True, False):
        for phase, amp in samples:
            j, raw, app = oracle.walk_frame(kind, phase, amp, og, seed=seed)
            worst["unmodelled"].update(raw)
            parts = [p for p in j if p in pw.MODELLED_PARTS]
            worst["unmodelled"].update(p for p in j if p not in pw.MODELLED_PARTS)
            model = pw.joints(app, pw.WalkState(phase, amp, on_ground=og, head_lean_deg=0.0), parts)
            for p in parts:
                e = pose_error(model[p], j[p])
                worst["pos"], worst["rot"] = max(worst["pos"], e[0]), max(worst["rot"], e[1])
                worst["parts"].add(p)
    return worst


def _worker(args):
    exe, sizes, kinds = args
    oracle = ao.load(exe, sizes)
    out = []
    for k in kinds:
        try:
            out.append(check_kind(oracle, k))
        except Exception as e:  # noqa: BLE001 — reported, never guessed
            out.append({"kind": f"0x{k:02x}", "status": "ORACLE_ERROR", "error": f"{type(e).__name__}: {e}"[:200]})
    return out


def build(exe=DEFAULT_EXE, sizes=DEFAULT_SIZES, kinds=None, workers=2):
    st.verify_exe(exe)
    kinds = list(kinds if kinds is not None else range(0, st.KIND_MAX + 1))
    chunks = [(str(exe), str(sizes), kinds[i::workers]) for i in range(workers)]
    recs = []
    with ProcessPoolExecutor(workers) as ex:
        for r in ex.map(_worker, chunks):
            recs += r
    recs.sort(key=lambda r: int(r["kind"], 16))
    counts = {}
    for r in recs:
        counts[r["status"]] = counts.get(r["status"], 0) + 1
    return {"format": FORMAT, "decision": "D64", "exe_sha256": st.EXPECTED_EXE_SHA256,
            "oracle": "Creature_drawModel 0x4128F0 executed (drawmodel_emu)", "tolerance": [POS_TOL, ROT_TOL],
            "metrics": {"kinds": len(recs), "by_status": counts}, "kinds": {r["kind"]: r for r in recs}}


def load_evidence(path=EVIDENCE_PATH):
    if not Path(path).exists():
        return {}
    return json.loads(Path(path).read_text(encoding="utf-8")).get("kinds", {})


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--exe", default=str(DEFAULT_EXE))
    ap.add_argument("--sizes", default=str(DEFAULT_SIZES))
    ap.add_argument("--out", default=str(EVIDENCE_PATH))
    ap.add_argument("--kinds")
    ap.add_argument("--workers", type=int, default=2)
    ap.add_argument("--merge", action="store_true", help="update only --kinds inside the existing --out file")
    a = ap.parse_args(argv)
    kinds = [int(x, 0) for x in a.kinds.split(",")] if a.kinds else None
    res = build(a.exe, a.sizes, kinds, a.workers)
    if a.merge and Path(a.out).exists():
        old = json.loads(Path(a.out).read_text(encoding="utf-8"))
        old["kinds"].update(res["kinds"])
        counts = {}
        for r in old["kinds"].values():
            counts[r["status"]] = counts.get(r["status"], 0) + 1
        old["metrics"] = {"kinds": len(old["kinds"]), "by_status": counts}
        res = old
    Path(a.out).write_text(json.dumps(res, indent=1), encoding="utf-8")
    print(json.dumps({"WALK_ORACLE": "DONE", **res["metrics"]}))
    return 0


if __name__ == "__main__":
    sys.exit(main())
