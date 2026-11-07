"""Data for the S05B visual review scene (one Unity scene, all 15 S05B-1 kinds).

Nothing is re-modelled or re-animated here: every specimen is produced by the existing universal
pipeline (`pipeline.prepare_unity`) from the SAME job files the accepted batches used:

* locomotion / appearance variants  - S05B-1 batch jobs (coverage B, every kind x seed at 60 Hz);
* NPC actions (seed 0-0)             - S05B-A batch jobs (creature-AI action libraries, D60);
* HumanMale 0x02 player library      - the S05B-A regression job (93+ verified player actions,
                                       iron greatsword), kept as its own specimen.

Each prepared `Assets/AlphaTransfer/Data` is copied to
`<project>/Assets/StreamingAssets/Review/<specimen id>/`, and `index.json` lists every specimen with
its models, clip provenance and action profile. A specimen whose preparation fails is listed with
its problems (FAIL) - never silently dropped. Everything stays in .scratch (D54/D59: Alpha data is
never committed or published).

    python -m tools.alpha_models.review.build_review --project .scratch/review/unity/S05B_VisualReview
"""
from __future__ import annotations

import argparse
import contextlib
import hashlib
import io
import json
import shutil
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
BATCH = ROOT / ".scratch" / "universal" / "batch"
S05B1 = BATCH / "s05b1"
S05BA = BATCH / "s05ba"
CLIPS_ROOT = ROOT / ".scratch" / "universal" / "clips"
STAGE = ROOT / ".scratch" / "review" / "stage"
FORMAT = "cubegame.visual_review/1"

# The 15 kinds of S05B-1 in review order: humanoids, cats, runners.
# HumanMale exactly as equipped during the live S05A-2 capture (session2.jsonl, wood-greatmace02,
# subtype 17): the only humanoid specimen whose family walk/idle clips are TRANSFERRED_FROM_ALPHA
# (captured weapon geometry must match, so the greatsword specimens cannot use them).
CAPTURE_JOB = {
    "kind": "0x02", "seed_variant": [0, 0], "variant": "review_0x02_as_captured_greatmace",
    "generated_by": "build_review (S05B visual review): S05A-2 capture equipment, no new data",
    "locomotion": "formula", "amp": 0.19, "speed_xy": 3.7,
    "equipment": {"weapon": {"subtype": 17, "model": "wood-greatmace02.cub"}},
    "note": "captured walk/idle TRANSFERRED_FROM_ALPHA of the family next to the formula walk; "
            "armour/gloves of the captured player are not reproduced (seed 0-0 base appearance)",
}

KIND_ORDER = ["0x02", "0x03", "0x00", "0x01", "0x07", "0x08", "0x0d", "0x0e",
              "0x1e", "0x1f", "0x20", "0x3f", "0x40", "0x41", "0x42"]


def specimen_id(kind: str, seed, tag: str = "") -> str:
    return f"{kind}_s{seed[0]}-{seed[1]}" + (f"_{tag}" if tag else "")


def select_specimens(entries, s05ba_jobs: dict, regression_job: Path | None, capture_job: Path | None = None):
    """(id, kind, seed, name, job path, source) for every kind x seed of coverage B.

    Seed 0-0 of a kind with an S05B-A job takes that job (same models + its action library);
    HumanMale additionally gets the player-library specimen (regression job) next to its
    unarmed S05B-1 seeds. Order: KIND_ORDER, then seed 0-0 first, then the rest by seed.
    """
    seen, rows = set(), []
    for e in entries:
        key = (e["kind"], tuple(e["seed"]))
        if key in seen:
            continue
        seen.add(key)
        rows.append(e)
    unknown = sorted({k for k, _ in seen} - set(KIND_ORDER))
    if unknown:
        raise ValueError(f"kinds outside the S05B-1 review set: {unknown}")
    rows.sort(key=lambda e: (KIND_ORDER.index(e["kind"]), tuple(e["seed"]) != (0, 0), tuple(e["seed"])))
    out = []
    for e in rows:
        kind, seed = e["kind"], tuple(e["seed"])
        sid = specimen_id(kind, seed)
        if seed == (0, 0) and kind in s05ba_jobs:
            out.append(dict(id=sid, kind=kind, seed=list(seed), name=e["name_hint"], job=s05ba_jobs[kind],
                            source="S05B-A (D60) job: models + creature-AI action library"))
        else:
            out.append(dict(id=sid, kind=kind, seed=list(seed), name=e["name_hint"],
                            job=S05B1 / "jobs" / f"{e['id'].rsplit('_', 1)[0]}_60hz.json",
                            source="S05B-1 (D59) job: models + walk/idle"))
        if kind == "0x02" and seed == (0, 0) and regression_job is not None:
            out.append(dict(id=specimen_id(kind, seed, "player"), kind=kind, seed=list(seed), name=e["name_hint"],
                            job=regression_job,
                            source="S05A-2 player action library (regression job, iron greatsword)"))
        if kind == "0x02" and seed == (0, 0) and capture_job is not None:
            out.append(dict(id=specimen_id(kind, seed, "capture"), kind=kind, seed=list(seed), name=e["name_hint"],
                            job=capture_job,
                            source="S05A-2 live-capture equipment (wood-greatmace02): captured walk/idle"))
    return out


def select_batch_specimens(batch_dir: Path, entries=None, prefix: str = ""):
    """Specimens of one authorised batch scope (e.g. D62/S05B-2): its own jobs, no S05B-1 kinds.

    The job of an entry is ``<batch_dir>/jobs/<entry id>.json`` (written by batch_runner.run_one), so the
    review scene shows exactly the specimens the batch verified — nothing is re-generated by hand.
    ``prefix`` gives the specimen id its scope name (needed when several scopes share the kind/seed id)."""
    entries = entries if entries is not None else json.loads(
        (batch_dir / "entries.json").read_text(encoding="utf-8"))["entries"]
    jobs = {e["id"]: batch_dir / "jobs" / f"{e['id']}.json" for e in entries}
    seen, rows = set(), []
    for e in entries:
        key = (e["kind"], tuple(e["seed"]))
        if key in seen:
            continue
        seen.add(key)
        rows.append(e)
    rows.sort(key=lambda e: (int(e["kind"], 16), tuple(e["seed"]) != (0, 0), tuple(e["seed"])))
    out = []
    for e in rows:
        job = jobs[e["id"]]
        if not job.exists():
            continue
        src = "models + rig + walk/idle (formula)"
        if "action_manifest" in json.loads(job.read_text(encoding="utf-8")):
            src = "models + rig + walk/idle + creature-AI action library"
        out.append(dict(id=f"{prefix}{e['id']}", kind=e["kind"], seed=list(e["seed"]),
                        name=e.get("name_hint") or f"kind {e['kind']}", job=job,
                        source=f"batch {batch_dir.name} job: {src}"))
    return out


def _profile_info(profile: str) -> dict:
    from tools.alpha_models.universal import contract as ct
    p = ct.ACTION_PROFILES.get(profile or "", {})
    return {"trigger": p.get("trigger", ""), "liveCheck": p.get("live_check", ""),
            "equipmentObserved": p.get("equipment_observed"), "authorization": p.get("authorization", ""),
            "blockedExamples": [f"{k}: {v}" for k, v in (p.get("blocked_examples") or {}).items()]}


def prepare_one(spec: dict, project: Path, prepare=None) -> dict:
    """Run the pipeline for one specimen and copy its Data to StreamingAssets/Review/<id>."""
    from tools.alpha_models.universal import pipeline as pl
    prepare = prepare or pl.prepare_unity
    job = json.loads(Path(spec["job"]).read_text(encoding="utf-8"))
    job.pop("reference_capture", None)   # live reference frames are a batch check, not review data
    stage = STAGE / spec["id"]
    data = stage / "Assets" / "AlphaTransfer" / "Data"
    if data.exists():
        shutil.rmtree(data)
    buf = io.StringIO()
    try:
        with contextlib.redirect_stdout(buf):
            r = prepare(job, str(stage), clips_root=str(CLIPS_ROOT))
    except Exception as exc:  # recorded, never hidden
        r = {"PREPARE_RESULT": "FAIL", "problems": [f"EXCEPTION {type(exc).__name__}: {exc}"], "clips": [],
             "skipped_clips": [], "models": []}
    dst = project / "Assets" / "StreamingAssets" / "Review" / spec["id"]
    if dst.exists():
        shutil.rmtree(dst)
    rec = {k: spec[k] for k in ("id", "kind", "seed", "name", "source")}
    rec["job"] = str(Path(spec["job"]).relative_to(ROOT)).replace("\\", "/")
    rec["jobSha256"] = hashlib.sha256(Path(spec["job"]).read_bytes()).hexdigest()
    rec["prepare"] = r["PREPARE_RESULT"]
    rec["problems"] = list(r.get("problems") or [])
    rec["skippedClips"] = list(r.get("skipped_clips") or [])
    if r["PREPARE_RESULT"] == "PASS":
        shutil.copytree(data, dst, ignore=shutil.ignore_patterns("*.meta", "paramwalk_golden.json"))
        rig = json.loads((dst / "rig.json").read_text(encoding="utf-8"))
        con = json.loads((dst / "contract.json").read_text(encoding="utf-8"))
        rec["family"] = rig["rig"]
        rec["models"] = {p["name"]: p["model"] for p in rig["parts"]}
        rec["clips"] = con.get("importedClips", [])
        rec["controllerMode"] = con.get("controllerMode", "")
        rec["actionProfile"] = con.get("actionProfile", "")
        rec["actionsStatus"] = con.get("actionsStatus", "")
        rec["actionsReason"] = con.get("actionsReason", "")
        rec.update(_profile_info(con.get("actionProfile", "")))
    return rec


def build(project: Path, regression_job: Path | None = None, prepare=None, batch_dir: Path | None = None,
          batch_dirs=None) -> dict:
    capture_job = STAGE.parent / "jobs" / "0x02_capture_greatmace.json"
    if batch_dir is not None or batch_dirs:
        dirs = [Path(d).resolve() for d in (batch_dirs or ([batch_dir] if batch_dir else []))]
        specs, extra_notes = [], []
        multi = len(dirs) > 1
        for d in dirs:
            if not d.is_relative_to(BATCH.resolve()):
                raise ValueError("REVIEW_BATCH_DIR_MUST_BE_IN_.scratch/universal/batch")
            summary = json.loads((d / "summary.json").read_text(encoding="utf-8")) \
                if (d / "summary.json").exists() else {}
            batch_entries = json.loads((d / "entries.json").read_text(encoding="utf-8"))["entries"]
            specs += select_batch_specimens(d, batch_entries, prefix=f"{d.name}_" if multi else "")
            extra_notes.append(f"Batch scope {d.name}: {summary.get('BATCH_RESULT', 'NO_SUMMARY')} "
                               f"({summary.get('decision', '?')}); walk formula live frames: "
                               f"{summary.get('live_check', {})}. The walk is RECONSTRUCTED_FROM_EVIDENCE + "
                               f"ALPHA_FORMULA_VERIFIED, never TRANSFERRED_FROM_ALPHA; actions are "
                               f"EMULATED_ALPHA_CODE (no live capture).")
        expected_kinds = len({s["kind"] for s in specs})
        seen_ids, unique = set(), []
        for s in specs:                       # the same scope twice must not produce duplicate ids
            if s["id"] not in seen_ids:
                seen_ids.add(s["id"])
                unique.append(s)
        specs = unique
        specs.sort(key=lambda s: (int(s["kind"], 16), s["seed"] != [0, 0], s["seed"], s["id"]))
    else:
        entries = json.loads((S05B1 / "entries.json").read_text(encoding="utf-8"))["entries"]
        s05ba_jobs = {p.name[:4]: p for p in sorted((S05BA / "jobs").glob("0x*_s0-0_60hz.json"))}
        regression_job = regression_job or S05BA / "jobs" / "regression_0x02_actions_60hz.json"
        capture_job.parent.mkdir(parents=True, exist_ok=True)
        capture_job.write_text(json.dumps(CAPTURE_JOB, indent=1), encoding="utf-8")
        specs = select_specimens(entries, s05ba_jobs, regression_job if regression_job.exists() else None,
                                 capture_job)
        extra_notes = []
        expected_kinds = 15
    review = project / "Assets" / "StreamingAssets" / "Review"
    review.mkdir(parents=True, exist_ok=True)
    for old in review.iterdir():          # no stale specimen of an earlier build may survive
        if old.is_dir() and old.name not in {s["id"] for s in specs}:
            shutil.rmtree(old)
    recs = []
    for s in specs:
        rec = prepare_one(s, project, prepare)
        print(f"{rec['prepare']}  {rec['id']:<22} {rec['name']:<16} clips={len(rec.get('clips', []))}"
              + (f"  problems={rec['problems']}" if rec["problems"] else ""))
        recs.append(rec)
    kinds = sorted({r["kind"] for r in recs}, key=lambda k: int(k, 16))
    from tools.alpha_models.universal import voxel_markers as vm
    hair = vm.load_hair_evidence()
    index = {"format": FORMAT, "kinds": kinds, "expectedKinds": expected_kinds, "specimens": recs,
             "hairSamples": [{k: s[k] for k in ("kind", "hair_rgb", "capture", "records", "creatures")}
                             for s in hair["samples"]],
             "notes": ["Alpha-derived data: local review only (D54/D59/D60), never commit or publish.",
                       "Variants are seeds of coverage B: head/hair pairs exactly as Alpha's appearance "
                       "emulation produced them; free head/hair mixing is not part of the verified coverage.",
                       "D61: pure R/G/B voxels of flag-0 models (hair) are Alpha markers and are not drawn; "
                       "hair colour = per-instance hair_rgb (live samples in hairSamples)."] + extra_notes}
    (review / "index.json").write_text(json.dumps(index, indent=1), encoding="utf-8")
    fails = [r["id"] for r in recs if r["prepare"] != "PASS"]
    print(f"REVIEW_DATA_RESULT={'PASS' if not fails else 'FAIL'} specimens={len(recs)} kinds={len(kinds)} "
          f"failed={len(fails)} {fails if fails else ''}")
    return index


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--project", required=True)
    ap.add_argument("--batch-dir", action="append", default=None,
                    help="review authorised batch scopes (.scratch/universal/batch/<scope>); repeatable for a "
                         "combined scene (ids get the scope prefix). Without it the 15 S05B-1 kinds are built")
    a = ap.parse_args(argv)
    project = (ROOT / a.project).resolve() if not Path(a.project).is_absolute() else Path(a.project)
    if not str(project).lower().startswith(str((ROOT / ".scratch").resolve()).lower()):
        raise SystemExit("review project must stay inside the repository .scratch (isolated, git-ignored)")
    idx = build(project, batch_dirs=[Path(d) for d in a.batch_dir] if a.batch_dir else None)
    ok = all(r["prepare"] == "PASS" for r in idx["specimens"])
    return 0 if ok and (a.batch_dir or len(idx["kinds"]) == 15) else 1


if __name__ == "__main__":
    sys.exit(main())
