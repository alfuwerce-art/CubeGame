"""Issue #7 (S05B-2 / D70): Dragon 0x65 — Alpha parity vs the visual correction proposal, same checks.

1. ``before``: the Dragon job that D69/#4 verified (Alpha appearance, D64 action library);
2. ``after``:  the same job with ``visual_correction = dragon_0x65_v1`` (``universal/visual_correction.py``):
   the action library is re-baked by the drawModel oracle under the correction, the walk formula and the
   contract are prepared under it, so every part stays consistent in every clip;
3. both run through the batch runner in the D69 Unity project (``.scratch/d69/unity/D69Verify``; never the
   producer's FullCatalogReview), each must PASS with all actions executed;
4. a review scope ``.scratch/universal/batch/review_dragon_0x65`` with both specimens for
   ``setup_review.ps1`` (sheets: ``sides_*`` 4 sides + top, ``actions_*``, ``variants_0x65``).

    python -m tools.alpha_models.review.dragon_visual_fix            # bake + Unity runs + review scope
    python -m tools.alpha_models.review.dragon_visual_fix --scope-only
"""
from __future__ import annotations

import argparse
import json
import shutil
from pathlib import Path

from tools.alpha_models.universal import batch_runner as br
from tools.alpha_models.universal import d69_verify as dv
from tools.alpha_models.universal import visual_correction as vc

NAME = "dragon_0x65_v1"
OUT = br.SCRATCH / "review_dragon_0x65"
SCOPE = br.SCRATCH / "universal" / "batch" / "review_dragon_0x65"


def build_jobs(out=OUT):
    before, entry = dv._action_entry(0x65)
    before["variant"] = "issue7_alpha_0x65"
    lib_entry = {"kind": "0x65", "seed": [0, 0], "profile": entry["profile"], "id": "0x65_fix"}
    with vc.active(NAME):
        lib = br.bake_library(lib_entry, out)
    after = dict(before)
    after.update(variant="issue7_fix_0x65", visual_correction=NAME,
                 action_manifest=br._rel(Path(lib) / "actions.json"), clips=br._rel(lib),
                 actions_status=f"profile {entry['profile']} baked under {NAME} ({vc.STATUS})",
                 library_sha256=br.sha256_file(Path(lib) / "actions.json"))
    return {"before": (before, entry), "after": (after, dict(entry, id="0x65_fix"), lib)}


def run(out=OUT, runner=None):
    out = Path(out)
    out.mkdir(parents=True, exist_ok=True)
    state_path = out / "state.json"
    state = br.load_state(state_path)
    runner = runner or br.unity_runner(dv.PROJECT)
    inputs = {"code": "issue7", "correction": vc.provenance(NAME)}
    jobs = build_jobs(out)
    results = {}
    for key in ("before", "after"):
        job, entry = jobs[key][0], jobs[key][1]
        rec = br.run_one(f"0x65_{key}", job, 60, out, state, state_path, runner, inputs, required=br.ACTION_STEPS,
                         judge_extra=br.action_judge(entry, br.ROOT / job["action_manifest"]),
                         locomotion_only=False, live_required=False)
        print(f"0x65_{key}: {rec['verdict']} {rec['problems'][:3]} {rec.get('seconds')}s", flush=True)
        results[key] = rec
        (out / "jobs").mkdir(exist_ok=True)
        (out / "jobs" / f"0x65_{key}.json").write_text(json.dumps(job, indent=1), encoding="utf-8")
    ok = all(r["verdict"] == "PASS" for r in results.values())
    summary = {"format": "cubegame.issue7_dragon/1", "correction": vc.provenance(NAME),
               "runs": {k: {"verdict": r["verdict"], "problems": r["problems"]} for k, r in results.items()},
               "DRAGON_FIX_RESULT": "PASS" if ok else "FAIL"}
    br.write_json_atomic(out / "summary.json", summary)
    print(f"DRAGON_FIX_RESULT={summary['DRAGON_FIX_RESULT']}")
    return summary


def build_scope(out=OUT, dst=SCOPE):
    dst = Path(dst)
    if dst.exists():
        shutil.rmtree(dst)
    (dst / "jobs").mkdir(parents=True)
    entries = []
    for key, eid, label in (("before", "0x65_s0-0_alpha", "Dragon 0x65 - Alpha parity (as drawn by Alpha's code)"),
                            ("after", "0x65_s0-0_fix", f"Dragon 0x65 - {NAME}: {vc.STATUS}")):
        src = Path(out) / "jobs" / f"0x65_{key}.json"
        shutil.copyfile(src, dst / "jobs" / f"{eid}.json")
        entries.append({"id": eid, "kind": "0x65", "seed": [0, 0], "name_hint": label, "variant_tag": key,
                        "source_job": br._rel(src)})
    (dst / "entries.json").write_text(json.dumps({"entries": entries}, indent=1), encoding="utf-8")
    s = json.loads((Path(out) / "summary.json").read_text(encoding="utf-8"))
    (dst / "summary.json").write_text(json.dumps({"BATCH_RESULT": s["DRAGON_FIX_RESULT"], "decision": "issue #7",
                                                  "live_check": "NOT_CAPTURED (Alpha not launched)"}, indent=1),
                                      encoding="utf-8")
    print(f"REVIEW_SCOPE={dst} entries={len(entries)}")
    return dst


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--scope-only", action="store_true")
    a = ap.parse_args(argv)
    if not a.scope_only:
        s = run()
        if s["DRAGON_FIX_RESULT"] != "PASS":
            return 1
    build_scope()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
