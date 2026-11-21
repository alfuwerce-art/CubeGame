"""S05B-2 / D69: Unity verification of the D69 transfer fixes (reuses batch_runner's runner and judges).

Entries (all in an isolated project under .scratch, never the producer's open review project):
  * Dragon 0x65 with its D65 action library  -> head anchor fix (head_0x65), ACTIONS still complete
  * every STATIC_OBJECT kind (spawn_role_evidence) -> controller STATIC_OBJECT: the walk request is
    refused, the pose does not move, actions refused; geometry/rig PASS
  * control creatures: Golem 0x6f and PlantCreature 0x45 (a real plant MONSTER) keep ACTIONS
  * HumanMale 0x02 regression at 60 Hz: 98/98 actions, the same frame numbers as D59/D61/D65

    python -m tools.alpha_models.universal.d69_verify [--only 0x65,0x78] [--no-regression]
"""
from __future__ import annotations

import argparse
import json
import sys
import time
from pathlib import Path

from tools.alpha_models.catalog import spawn_roles as sr
from tools.alpha_models.universal import batch_runner as br

ROOT = br.ROOT
OUT = br.SCRATCH / "d69" / "verify"
PROJECT = br.SCRATCH / "d69" / "unity" / "D69Verify"
D63_FULL_JOBS = br.SCRATCH / "universal" / "batch" / "d63_full" / "jobs"
D63_ACT = br.SCRATCH / "universal" / "batch" / "d63_actions"
ACTION_KINDS = {0x65: "alpha_actions/creature_ai_d64_dragon_0x65"}
CONTROL_ACTION_KINDS = (0x6F, 0x45)


def _action_entry(kind):
    job_path = D63_ACT / "jobs" / f"0x{kind:02x}_s0-0_60hz.json"
    job = json.loads(job_path.read_text(encoding="utf-8"))
    from tools.alpha_models.universal import contract as ct
    prof = next(p for p, v in ct.ACTION_PROFILES.items()
                if any(s["kind"] == kind and list(s["seed_variant"]) == [0, 0] for s in v["specimens"]))
    job["variant"] = f"d69_0x{kind:02x}"
    return job, {"profile": prof, "id": f"0x{kind:02x}"}


S05BA_JOBS = br.SCRATCH / "universal" / "batch" / "s05ba" / "jobs"


def merged_kinds():
    from tools.alpha_models.universal import contract as ct
    return {ct.ACTION_PROFILES[p]["specimens"][0]["kind"]: p for p in ct.D69_MERGED_LAUNCHES}


def _merged_entry(kind, out):
    """D69 ADD_MISSING: re-bake the kind's library with the merged profile (D60 + D64 conditional launches)
    into the D69 output (the D60 library in .scratch/universal/batch/s05ba stays untouched)."""
    prof = merged_kinds()[kind]
    entry = {"kind": f"0x{kind:02x}", "seed": [0, 0], "profile": prof, "id": f"0x{kind:02x}"}
    lib = br.bake_library(entry, out)
    job = json.loads((S05BA_JOBS / f"0x{kind:02x}_s0-0_60hz.json").read_text(encoding="utf-8"))
    job.update(variant=f"d69_merged_0x{kind:02x}", action_manifest=br._rel(Path(lib) / "actions.json"),
               clips=br._rel(lib), actions_status=f"profile {prof} (D69 merged library)",
               library_sha256=br.sha256_file(Path(lib) / "actions.json"))
    return job, entry, lib


def entries(only=None):
    out = []
    for k in sorted(merged_kinds()):
        out.append(("merged", k))
    kinds = sorted(int(k, 16) for k in sr.load_evidence()["passive_kinds"])
    for k in [0x65, *CONTROL_ACTION_KINDS]:
        out.append(("actions", k))
    for k in kinds:
        out.append(("static", k))
    if only:
        out = [e for e in out if e[1] in only]
    return out


def run(only=None, regression=True, runner=None, out=OUT):
    out = Path(out)
    out.mkdir(parents=True, exist_ok=True)
    state_path = out / "state.json"
    state = br.load_state(state_path)
    runner = runner or br.unity_runner(PROJECT)
    inputs = {"code": "d69", "evidence_sha256": br.sha256_file(sr.EVIDENCE_PATH)}
    results = []
    for mode, kind in entries(only):
        key = f"0x{kind:02x}_{mode}"
        if mode == "merged":
            try:
                job, e, lib = _merged_entry(kind, out)
            except Exception as ex:   # a failed bake is a FAIL, never a skip
                rec = {"id": key, "verdict": "FAIL", "problems": [f"BAKE_FAILED {ex}"]}
                print(f"{key}: FAIL BAKE {ex}", flush=True)
                results.append(rec)
                continue
            rec = br.run_one(key, job, 60, out, state, state_path, runner, inputs, required=br.ACTION_STEPS,
                             judge_extra=br.action_judge(e, Path(lib) / "actions.json"),
                             locomotion_only=False, live_required="reference_capture" in job)
        elif mode == "actions":
            job, e = _action_entry(kind)
            rec = br.run_one(key, job, 60, out, state, state_path, runner, inputs, required=br.ACTION_STEPS,
                             judge_extra=br.action_judge(e, ROOT / job["action_manifest"]),
                             locomotion_only=False, live_required=False)
        else:
            job = json.loads((D63_FULL_JOBS / f"0x{kind:02x}_s0-0_60hz.json").read_text(encoding="utf-8"))
            job["variant"] = f"d69_static_0x{kind:02x}"

            def static_extra(rdir, kind=kind):
                t = (Path(rdir) / "entity_playmode.txt")
                t = t.read_text(encoding="utf-8") if t.exists() else ""
                return [] if "controller_mode=STATIC_OBJECT" in t else ["ENTITY_NOT_STATIC_OBJECT"]
            rec = br.run_one(key, job, 60, out, state, state_path, runner, inputs, judge_extra=static_extra,
                             locomotion_only=True, live_required=False)
        print(f"{key}: {rec['verdict']} {rec['problems'][:3]} {rec.get('seconds')}s", flush=True)
        results.append(rec)
    reg = None
    if regression:
        r = br.SCOPES["s05b1"]["regression"]
        reg_runner = br.unity_runner(r["project"])
        job = json.loads(Path(r["job"]).read_text(encoding="utf-8"))
        reg = br.run_one("regression_0x02_60hz", job, 60, out, state, state_path, reg_runner, inputs,
                         required=br.REGRESSION_STEPS, locomotion_only=False)
        got = br.regression_numbers(out / "runs" / "regression_0x02_60hz")
        exp = r["expect"][60]
        for k2, v in exp.items():
            if got.get(k2) != v:
                reg["problems"].append(f"REGRESSION_NUMBER {k2} {got.get(k2)} != {v}")
        reg["numbers"] = got
        reg["verdict"] = "PASS" if not reg["problems"] else "FAIL"
        print(f"regression_0x02_60hz: {reg['verdict']} {got} {reg['problems'][:3]}", flush=True)
    passed = sum(r["verdict"] == "PASS" for r in results)
    summary = {"format": "cubegame.d69_verify/1", "finished": time.strftime("%Y-%m-%dT%H:%M:%S"),
               "entries": len(results), "pass": passed,
               "fail": [r["id"] for r in results if r["verdict"] != "PASS"],
               "regression": reg and {"verdict": reg["verdict"], "numbers": reg.get("numbers"), "problems": reg["problems"]},
               "D69_VERIFY_RESULT": "PASS" if passed == len(results) and (not reg or reg["verdict"] == "PASS") else "FAIL"}
    br.write_json_atomic(out / "summary.json", summary)
    print(f"D69_VERIFY_RESULT={summary['D69_VERIFY_RESULT']} entries={len(results)} pass={passed} fail={summary['fail']}")
    return summary


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--only", default="")
    ap.add_argument("--no-regression", action="store_true")
    a = ap.parse_args()
    only = {int(x, 16) for x in a.only.split(",") if x} or None
    s = run(only, regression=not a.no_regression)
    sys.exit(0 if s["D69_VERIFY_RESULT"] == "PASS" else 1)


if __name__ == "__main__":
    main()
