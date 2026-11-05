"""Universal Alpha -> Unity asset/animation pipeline (research prototype, DRAFT architecture).

Stages (docs/architecture/UNIVERSAL_ASSET_ANIMATION_PIPELINE.md):

  scan          Model Scanner + Entity/Rig Resolver + family table, static (no Alpha run)
  build-clips   Animation System Resolver, runtime route: capture -> family clips (+ validation)
  prepare-unity Binding Generator + Unity data for ONE kind (models from the static tables,
                clips of its motion family); refuses unsupported kinds and foreign clips
  batch         Batch Orchestrator: every kind -> AUTO_READY | NEEDS_CAPTURE | EXCEPTION queue

All Alpha-derived outputs (tables, clips, models) are written under ``.scratch`` only (D54).

    python -m tools.alpha_models.universal.pipeline scan --out .scratch/universal/static/alpha_static.json
    python -m tools.alpha_models.universal.pipeline build-clips --capture CAP.jsonl --kind 0x20
            --clips-root .scratch/universal/clips
    python -m tools.alpha_models.universal.pipeline prepare-unity --job JOB.json --project P
    python -m tools.alpha_models.universal.pipeline batch --static T.json --clips-root DIR --out Q.json
"""
from __future__ import annotations

import argparse
import collections
import glob
import json
import os
import sqlite3
import sys
from pathlib import Path

from tools.alpha_models.anim import pe_bytes as pb
from tools.alpha_models.universal import alpha_static as st
from tools.alpha_models.universal import creature_rig as cr
from tools.alpha_models.universal import uclip

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from cub_format import deobfuscate, parse_decoded  # noqa: E402

DEFAULT_EXE = r"D:\Projects\CubeGame\.scratch\alphatest2\Cube.exe"
DEFAULT_DB = r"D:\Projects\CubeGame\.scratch\alphatest2\data1.db"

# Clip acceptance thresholds — identical to the humanoid S05A-2 thresholds (not relaxed).
WALK_MAX_TRANSLATION_VOX = 0.25
WALK_MAX_ROTATION_DEG = 3.0


def family_dir(clips_root, family_id):
    return os.path.join(clips_root, family_id.replace("/", "_"))


# --------------------------------------------------------------------------- build-clips
def build_clips(capture, kind, clips_root, creature=None):
    frames = uclip.load_capture(capture, kind=kind, creature=creature)
    if not frames:
        return {"BUILD_RESULT": "FAIL", "problems": [f"NO_FRAMES kind=0x{kind:02x}"]}
    creatures = sorted({f.creature for f in frames})
    if creature is None and len(creatures) > 1:
        return {"BUILD_RESULT": "FAIL", "problems": [f"SEVERAL_CREATURES {creatures} (pass --creature)"]}
    res = uclip.frame_resolution(frames[0])
    problems = []
    if not res.supported:
        return {"BUILD_RESULT": "FAIL", "problems": [f"UNSUPPORTED {e}" for e in res.exceptions]}
    # appearance must be constant: a different appearance would mean a different family
    if len({f.appearance for f in frames}) != 1:
        problems.append("APPEARANCE_CHANGED_DURING_CAPTURE")
    counts = collections.Counter(tuple(f.problems) for f in frames)
    usable = sum(uclip.usable(f, res) for f in frames)
    rt = uclip.roundtrip(frames, res)
    if rt > 1e-6:
        problems.append(f"ROUNDTRIP {rt}")
    out_dir = family_dir(clips_root, res.family_id)
    written, report = [], {"kind": f"0x{kind:02x}", "family_id": res.family_id, "parts": res.parts,
                           "frames": len(frames), "usable": usable,
                           "frame_problems": {";".join(k) or "none": v for k, v in counts.items()},
                           "roundtrip_max_abs": rt}
    try:
        walk = uclip.build_walk_clip(frames, res, capture)
        v = uclip.validate_walk_holdout(walk)
        walk["validation"] = {"leave_one_cycle_out": v,
                              "thresholds": {"max_translation_vox": WALK_MAX_TRANSLATION_VOX,
                                             "max_rotation_deg": WALK_MAX_ROTATION_DEG}}
        report["walk"] = {"keys": len(walk["keys"]), "cycles": walk["source"]["cycles"],
                          "phase_rate_rad_per_s": walk["source"]["phase_rate_rad_per_s"],
                          "amplitude": walk["source"]["amplitude_creature_1188"], "holdout": v}
        if v.get("cycles", 0) < 3:
            problems.append(f"WALK_TOO_FEW_CYCLES {v.get('cycles')}")
        elif v["max_translation_vox"] > WALK_MAX_TRANSLATION_VOX or v["max_rotation_deg"] > WALK_MAX_ROTATION_DEG:
            problems.append(f"WALK_NOT_REPEATABLE {v['max_translation_vox']:.3f}vox {v['max_rotation_deg']:.2f}deg")
        else:
            written.append(uclip.write_clip(walk, out_dir))
    except ValueError as e:
        report["walk"] = {"status": "NOT_CAPTURED", "reason": str(e)}
    idle = [f for f in frames if f.state == 0 and f.action == 0 and abs(f.amp) < 1e-6 and uclip.usable(f, res)]
    if idle:
        clip = uclip._header("idle", "time_ms", True, [idle[0]], res, capture,
                             {"drawmodel_state": 0, "action_creature_68": 0, "idle_frames_seen": len(idle)})
        clip["duration"] = 0.0
        clip["keys"] = [{"x": 0.0, "m": uclip.joints(idle[0])}]
        clip["validation"] = {"single_static_frame": True}
        written.append(uclip.write_clip(clip, out_dir))
        report["idle"] = {"frames_seen": len(idle)}
    else:
        report["idle"] = {"status": "NOT_CAPTURED", "reason": "creature never stood still (amp>0) in this capture"}
    report["clips_written"] = written
    report["BUILD_RESULT"] = "PASS" if (written and not problems) else "FAIL"
    report["problems"] = problems
    with open(os.path.join(out_dir if written else clips_root, f"build_0x{kind:02x}.json"), "w", encoding="utf-8") as fh:
        json.dump(report, fh, indent=1)
    return report


# --------------------------------------------------------------------------- prepare-unity
def _exposed_faces(model):
    from tools.alpha_models.transfer.prepare_unity import exposed_faces
    return exposed_faces(model)


def _unity_clip(clip, part_order):
    from tools.alpha_models.transfer.prepare_unity import unity_clip
    return unity_clip(clip, part_order)


def resolve_kind(exe, kind, seed=(0, 0)):
    st.verify_exe(exe)      # refuses any other Cube.exe build (UnknownBuild)
    image = pb.load(exe)
    run = st.AppearanceEmulator(image).run(kind, *seed)
    app = st.decode_appearance(run["raw"])
    registry, reg_stats = st.model_registry(image)
    return image, app, registry, reg_stats


def prepare_unity(job, project, exe=DEFAULT_EXE, db=DEFAULT_DB, clips_root=None):
    problems = []
    kind = int(str(job["kind"]), 0)
    seed = tuple(job.get("seed_variant", [0, 0]))
    _, app, registry, _ = resolve_kind(exe, kind, seed)
    # equipment is not in the appearance; a job may add the weapon layer (humanoid families)
    weapon = (job.get("equipment") or {}).get("weapon")
    res = cr.resolve(kind, app, equipment=("weapon",) if weapon else ())
    subtype = -1
    if weapon:
        subtype = int(weapon["subtype"])
        from tools.alpha_models.anim import humanoid_rig
        if subtype not in humanoid_rig.TWO_HANDED_SUBTYPES:
            problems.append(f"WEAPON_SUBTYPE_NOT_VERIFIED {subtype}")
    if not res.supported:
        problems += [f"UNSUPPORTED {e}" for e in res.exceptions]
    clips_dir = job.get("clips") or family_dir(clips_root, res.family_id)
    data_dir = Path(project) / "Assets" / "AlphaTransfer" / "Data"
    (data_dir / "models").mkdir(parents=True, exist_ok=True)
    (data_dir / "clips").mkdir(parents=True, exist_ok=True)
    for old in glob.glob(str(data_dir / "clips" / "*.json")) + glob.glob(str(data_dir / "models" / "*.cubdec")):
        os.remove(old)    # a stale clip/model of another family must never be picked up
    con = sqlite3.connect(f"file:{db}?mode=ro&immutable=1", uri=True)
    parts_out = []
    for p in res.parts:
        idx = res.models[p]
        name = registry.get(idx) if isinstance(idx, int) else None
        if idx == "equipment:weapon":
            name = weapon["model"]
        if name is None:
            problems.append(f"MODEL_INDEX_UNRESOLVED {p} {idx}")
            continue
        row = con.execute("select value from blobs where key=?", (name,)).fetchone()
        if row is None:
            problems.append(f"MODEL_NOT_IN_DB {p} {name}")
            continue
        decoded = deobfuscate(row[0])
        model = parse_decoded(decoded)
        size = (model.width, model.depth, model.height)
        (data_dir / "models" / f"{p}.cubdec").write_bytes(decoded)
        rule = cr.BY_NAME[p].anchor
        if rule == "head":
            rule = "center"           # kind 0x65 (the only other head rule) is refused above
        parts_out.append({"name": p, "model": name, "file": f"models/{p}.cubdec", "size": list(size),
                          "anchor": rule, "mirrored": cr.BY_NAME[p].mirrored,
                          "expectedVertices": _exposed_faces(model) * 4})
    rig_json = {"rig": res.family_id, "variant": job.get("variant", f"kind_0x{kind:02x}"),
                "weaponSubtype": subtype, "parts": parts_out,
                "kind": kind, "placementScale": app["size"][0] / 11.2}
    (data_dir / "rig.json").write_text(json.dumps(rig_json, indent=1), encoding="utf-8")
    written, skipped = [], []
    for path in sorted(glob.glob(os.path.join(clips_dir, "*.alphaclip.json"))):
        clip = json.load(open(path, encoding="utf-8"))
        if clip.get("rig") != res.family_id:
            skipped.append(f"{os.path.basename(path)} rig={clip.get('rig')}")
            continue
        if clip.get("status") != "TRANSFERRED_FROM_ALPHA":
            skipped.append(f"{os.path.basename(path)} status={clip.get('status')}")
            continue
        if sorted(p["name"] for p in clip["parts"]) != sorted(res.parts):
            skipped.append(f"{os.path.basename(path)} parts={[p['name'] for p in clip['parts']]} != rig {res.parts}")
            continue
        uc = _unity_clip(clip, res.parts)
        (data_dir / "clips" / f"{clip['name']}.json").write_text(json.dumps(uc), encoding="utf-8")
        written.append(clip["name"])
    if not written:
        problems.append(f"NO_FAMILY_CLIPS {res.family_id} in {clips_dir}")
    result = "PASS" if not problems else "FAIL"
    out = {"PREPARE_RESULT": result, "problems": problems, "kind": f"0x{kind:02x}",
           "family_id": res.family_id, "parts": res.parts, "models": [p["model"] for p in parts_out],
           "clips": written, "skipped_clips": skipped}
    print(json.dumps(out, indent=1))
    return out


# --------------------------------------------------------------------------- batch
def batch(static_path, clips_root, db=DEFAULT_DB):
    table = json.load(open(static_path, encoding="utf-8"))
    registry = {int(k): v for k, v in table["registry"].items()}
    sizes = table.get("model_sizes", {})
    fam_clips = {}
    for d in glob.glob(os.path.join(clips_root, "alpha_creature_*")):
        clips = []
        for p in glob.glob(os.path.join(d, "*.alphaclip.json")):
            c = json.load(open(p, encoding="utf-8"))
            if c.get("status") == "TRANSFERRED_FROM_ALPHA":
                clips.append(c["name"])
        if clips:
            fam_clips["alpha_creature/" + os.path.basename(d)[len("alpha_creature_"):]] = sorted(clips)
    queue, fam_members, plans = [], collections.defaultdict(list), collections.Counter()
    for k, e in table["kinds"].items():
        kind = int(k, 16)
        res = cr.resolve(kind, e["appearance"])
        fam_members[res.family_id].append(k)
        plans[tuple(res.parts)] += 1
        missing = []
        for p, idx in res.models.items():
            if isinstance(idx, int) and (idx not in registry or sizes.get(registry[idx], 1) is None):
                missing.append(f"{p}:{idx}")
        variants = 1
        for vals in e.get("slot_variants", {}).values():
            variants = max(variants, len(vals))
        if not res.supported:
            status, reason = "EXCEPTION", res.exceptions
        elif missing:
            status, reason = "EXCEPTION", [f"MODEL_UNRESOLVED {m}" for m in missing]
        elif res.family_id in fam_clips:
            status, reason = "AUTO_READY", [f"clips {fam_clips[res.family_id]}"]
        else:
            status, reason = "NEEDS_CAPTURE", ["supported body plan; no verified clip for this motion family yet"]
        queue.append({"kind": k, "name_hint": e.get("name_hint"), "status": status, "family_id": res.family_id,
                      "parts": res.parts, "model_variants": variants, "uses_rand": e.get("uses_rand", False),
                      "reason": reason})
    by_status = collections.Counter(q["status"] for q in queue)
    fams = {}
    for q in queue:
        f = fams.setdefault(q["family_id"], {"kinds": [], "status": q["status"], "parts": q["parts"]})
        f["kinds"].append(q["kind"])
    metrics = {
        "kinds_total": len(queue),
        "body_plans": len(plans),
        "motion_families": len(fam_members),
        "families_multi_kind": sum(1 for v in fam_members.values() if len(v) > 1),
        "families_with_verified_clips": len(fam_clips),
        "kinds_by_status": dict(by_status),
        "kinds_auto_ready": [q["kind"] for q in queue if q["status"] == "AUTO_READY"],
        "families_needing_capture": sum(1 for f in fams.values() if f["status"] == "NEEDS_CAPTURE"),
        "families_exception": sum(1 for f in fams.values() if f["status"] == "EXCEPTION"),
        "registry_indexed_models": table["registry_stats"]["indexed"],
    }
    return {"format": "cubegame.universal_queue/1", "metrics": metrics, "families": fams, "queue": queue}


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    sub = ap.add_subparsers(dest="cmd", required=True)
    s = sub.add_parser("scan")
    s.add_argument("--exe", default=DEFAULT_EXE)
    s.add_argument("--db", default=DEFAULT_DB)
    s.add_argument("--decomp", default=r"D:\Projects\CubeGame\.scratch\CubeWorld-Reversal\cube")
    s.add_argument("--out", required=True)
    b = sub.add_parser("build-clips")
    b.add_argument("--capture", required=True)
    b.add_argument("--kind", required=True, type=lambda v: int(v, 0))
    b.add_argument("--creature", default=None)
    b.add_argument("--clips-root", default=r"D:\Projects\CubeGame\.scratch\universal\clips")
    p = sub.add_parser("prepare-unity")
    p.add_argument("--job", required=True)
    p.add_argument("--project", required=True)
    p.add_argument("--exe", default=DEFAULT_EXE)
    p.add_argument("--db", default=DEFAULT_DB)
    p.add_argument("--clips-root", default=r"D:\Projects\CubeGame\.scratch\universal\clips")
    q = sub.add_parser("batch")
    q.add_argument("--static", required=True)
    q.add_argument("--clips-root", default=r"D:\Projects\CubeGame\.scratch\universal\clips")
    q.add_argument("--out", required=True)
    a = ap.parse_args(argv)
    if a.cmd == "scan":
        return st.main(["scan", "--exe", a.exe, "--db", a.db, "--decomp", a.decomp, "--out", a.out])
    if a.cmd == "build-clips":
        r = uclip_report = build_clips(a.capture, a.kind, a.clips_root, a.creature)
        print(json.dumps(uclip_report, indent=1, default=str))
        return 0 if r["BUILD_RESULT"] == "PASS" else 1
    if a.cmd == "prepare-unity":
        job = json.load(open(a.job, encoding="utf-8"))
        r = prepare_unity(job, a.project, a.exe, a.db, a.clips_root)
        return 0 if r["PREPARE_RESULT"] == "PASS" else 1
    if a.cmd == "batch":
        r = batch(a.static, a.clips_root)
        os.makedirs(os.path.dirname(os.path.abspath(a.out)), exist_ok=True)
        with open(a.out, "w", encoding="utf-8") as fh:
            json.dump(r, fh, indent=1)
        print(json.dumps({"BATCH": "DONE", **r["metrics"]}, indent=1))
        return 0
    return 2


if __name__ == "__main__":
    sys.exit(main())
