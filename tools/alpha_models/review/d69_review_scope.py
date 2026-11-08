"""Issue #4 (S05B-2 / D69 independent review): one review batch scope for the visual check.

Collects, without regenerating anything by hand, the exact jobs that the D69 verification ran
(`d69_verify.run(out=...)` writes `<out>/jobs/<entry>.json`) plus the HumanMale player-library regression
job, into `.scratch/universal/batch/<name>/{entries.json, jobs/}` so `setup_review.ps1 -BatchDir` can build
an ISOLATED review project (never the producer's open FullCatalogReview).

    python -m tools.alpha_models.review.d69_review_scope --verify-out .scratch/review_s05b2_d69/verify
    powershell -File tools/alpha_models/review/setup_review.ps1 `
        -Project .scratch/review/unity/S05B2_D69_Review -BatchDir .scratch/universal/batch/review_s05b2_d69
"""
from __future__ import annotations

import argparse
import json
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
BATCH = ROOT / ".scratch" / "universal" / "batch"
NAME = "review_s05b2_d69"

# (verify entry key, kind, display name). Order = review order in the issue: Dragon, HumanMale, Golem,
# PlantCreature, Egg, Bush, other passive objects, then the 14 kinds with the D69-added actions 37/40/42/43.
SPECIMENS = [
    ("0x65_actions", 0x65, "Dragon (head anchor head_0x65, D65 actions)"),
    ("regression_0x02_60hz", 0x02, "HumanMale (player action library, regression job)"),
    ("0x6f_actions", 0x6F, "Golem (control creature, actions kept)"),
    ("0x45_actions", 0x45, "PlantCreature (plant MONSTER, actions kept)"),
    ("0x18_static", 0x18, "Egg (STATIC_OBJECT, producer rule D67)"),
    ("0x78_static", 0x78, "Bush (STATIC_OBJECT, Alpha faction 6)"),
    ("0x8c_static", 0x8C, "Scarecrow (STATIC_OBJECT, Alpha faction 6)"),
    ("0x8f_static", 0x8F, "Vase (STATIC_OBJECT, Alpha faction 6)"),
    ("0x83_static", 0x83, "Ore deposit (STATIC_OBJECT, Alpha faction 6)"),
] + [(f"0x{k:02x}_merged", k, f"kind 0x{k:02x}: D60 + D69 actions 37/40/42/43")
     for k in (0x00, 0x01, 0x03, 0x07, 0x08, 0x0D, 0x0E, 0x1E, 0x1F, 0x20, 0x3F, 0x40, 0x41, 0x42)]


def build(verify_out: Path, name: str = NAME) -> dict:
    from tools.alpha_models.universal import batch_runner as br
    dst = BATCH / name
    if dst.exists():
        shutil.rmtree(dst)
    (dst / "jobs").mkdir(parents=True)
    reg_job = Path(br.SCOPES["s05b1"]["regression"]["job"])
    entries, missing = [], []
    for key, kind, label in SPECIMENS:
        src = reg_job if key.startswith("regression_") else verify_out / "jobs" / f"{key}.json"
        if not src.exists():
            missing.append(key)
            continue
        eid = f"0x{kind:02x}_s0-0_{key.split('_', 1)[1] if not key.startswith('regression_') else 'player'}"
        shutil.copyfile(src, dst / "jobs" / f"{eid}.json")
        entries.append({"id": eid, "kind": f"0x{kind:02x}", "seed": [0, 0], "name_hint": label,
                        "source_job": str(src.relative_to(ROOT)).replace("\\", "/")})
    summary_src = verify_out / "summary.json"
    summary = json.loads(summary_src.read_text(encoding="utf-8")) if summary_src.exists() else {}
    (dst / "entries.json").write_text(json.dumps({"entries": entries}, indent=1), encoding="utf-8")
    (dst / "summary.json").write_text(json.dumps(
        {"BATCH_RESULT": summary.get("D69_VERIFY_RESULT", "NO_SUMMARY"), "decision": "D69 review #4",
         "live_check": "NOT_CAPTURED (Alpha not launched in the review)"}, indent=1), encoding="utf-8")
    print(f"REVIEW_SCOPE={dst} entries={len(entries)} missing={missing}")
    return {"dir": dst, "entries": entries, "missing": missing}


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--verify-out", default=str(ROOT / ".scratch" / "review_s05b2_d69" / "verify"))
    ap.add_argument("--name", default=NAME)
    a = ap.parse_args(argv)
    r = build(Path(a.verify_out).resolve(), a.name)
    return 0 if not r["missing"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
