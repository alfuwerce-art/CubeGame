"""Transfer planner of the universal Alpha -> Unity pipeline (S05A2-UNIVERSAL-CONTINUE-01).

Modes (one code path for every creature; nothing here names a creature or counts its limbs):

  SCAN      static catalogue -> entity contracts (category, body plan, parts, family, channels)
  PLAN      contracts -> per-entity transfer plan (what is automatic, what needs research, what is
            blocked and why) + generated jobs + coverage matrix (JSON + Markdown)
  VALIDATE  existing data vs the contract: repository jobs, family clips, action manifests
  PILOT     prepare (and optionally run in Unity) ONLY allow-listed reference specimens
  BATCH     authorised scopes only: (decision, scope) must be in BATCH_AUTHORIZATIONS (S05B-1 = D59,
            S05B-A = D60, S05B-2 = D62); execution lives in batch_runner.py (phases, coverage, resume, hashes,
            summary). A scope of a never-captured motion family is formula-only: its entries are reported as
            FORMULA_ONLY with live_check=NOT_CAPTURED, never as a live-verified PASS.

All Alpha-derived outputs stay under .scratch (D54). The static table is built beforehand by
``python -m tools.alpha_models.universal.pipeline scan`` (no Alpha run).

    python -m tools.alpha_models.universal.transfer_planner scan --static T --out .scratch/universal/plan/scan.json
    python -m tools.alpha_models.universal.transfer_planner plan --static T --out-dir .scratch/universal/plan
    python -m tools.alpha_models.universal.transfer_planner validate --static T [--manifest M]
    python -m tools.alpha_models.universal.transfer_planner pilot --static T --kinds 0x20,0x3f [--execute]
    python -m tools.alpha_models.universal.transfer_planner batch --authorization D59 --scope s05b1 [--phase 1] [--dry-run]
"""
from __future__ import annotations

import argparse
import collections
import functools
import glob
import json
import os
import subprocess
import sys
from pathlib import Path

from tools.alpha_models.universal import contract as ct
from tools.alpha_models.universal import creature_rig as cr

ROOT = Path(__file__).resolve().parents[3]
SCRATCH = ROOT / ".scratch"
DEFAULT_STATIC = SCRATCH / "universal" / "static" / "alpha_static.json"
DEFAULT_CLIPS = SCRATCH / "universal" / "clips"
JOBS_DIR = Path(__file__).resolve().parent / "jobs"

# Reference specimens already researched and piloted under D51/D57 (S05A-2). PILOT refuses every
# other kind: adding a kind here is a producer decision, not a code change made to get a PASS.
PILOT_ALLOWLIST = {
    0x02: "HumanMale: 8-part humanoid, weapon layer, action controller (S05A-2 specimen)",
    0x20: "WhiteCat: quadruped + tail, captured walk (universal prototype 2026-11-05)",
    0x1e: "Cat: same family as WhiteCat, never captured (family transfer without Alpha run)",
    0x03: "HumanFemale: humanoid NPC of the creature-AI action pilot (D60, S05B-A step A2)",
    0x3f: "PlainRunner: body + two feet, captured walk (universal prototype 2026-11-05)",
}
# (decision, scope) pairs that authorise BATCH. Only producer decisions go here; every other decision
# id or scope is refused. D59 (2026-11-06): S05B-1 locomotion. D60 (2026-11-06): S05B-A creature actions.
# D62 (2026-11-06): S05B-2 new motion family of a never-imported kind set (one family per batch until the
# producer approves the first one); every other decision/scope pair stays refused.
BATCH_AUTHORIZATIONS: tuple = (("D59", "s05b1"), ("D60", "s05ba"), ("D62", "s05b2"), ("D63", "d63_full"),
                               ("D63", "d63_actions"))
# D64: the D63 full-catalog run starts only after the producer approved the D64 research result;
# the runner requires this explicit acknowledgement (no silent start by any agent)
APPROVAL_REQUIRED = {("D63", "d63_full"): "--producer-approved-d64", ("D63", "d63_actions"): "--producer-approved-d64"}

# Unity locomotion inputs the planner may fill in. They are NOT Alpha data: amplitude 0.19 is the
# steady-walk value seen in captures; speed 3.7 is the median AI walk speed of the captured cat/runner.
DEFAULT_LOCOMOTION = {"amp": 0.19, "speed_xy": 3.7,
                      "speed_status": "PLACEHOLDER_NOT_ALPHA (median captured AI walk speed)"}


def local_out(path):
    p = Path(path).resolve()
    if not p.is_relative_to(SCRATCH.resolve()):
        raise ValueError("PLANNER_OUTPUT_MUST_BE_IN_SCRATCH")
    return p


def _file_sha256(path):
    import hashlib
    h = hashlib.sha256()
    with open(path, "rb") as fh:
        for chunk in iter(lambda: fh.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def load_static(path):
    t = json.load(open(path, encoding="utf-8"))
    if t.get("format", "").split("/")[0] != "cubegame.alpha_static":
        raise ValueError(f"NOT_A_STATIC_TABLE {t.get('format')}")
    return t


# --------------------------------------------------------------------------- SCAN
def scan(table, clips_root=None):
    sf = ct.specimen_families(table)
    entities = [ct.describe(int(k, 16), e, table, clips_root, spec_families=sf) for k, e in table["kinds"].items()]
    return {"format": "cubegame.transfer_scan/1", "exe_sha256": table.get("exe_sha256"),
            "entities": entities, "metrics": scan_metrics(entities)}


def scan_metrics(entities):
    fam = collections.defaultdict(list)
    for e in entities:
        if e.get("family_id"):
            fam[e["family_id"]].append(e["kind"])
    return {"kinds": len(entities),
            "categories": dict(collections.Counter(e.get("category", "NONE") for e in entities)),
            "body_plans": len({e.get("body_plan") for e in entities if e.get("body_plan")}),
            "motion_families": len(fam),
            "families_multi_kind": sum(1 for v in fam.values() if len(v) > 1),
            "readiness": dict(collections.Counter(e["readiness"] for e in entities))}


# --------------------------------------------------------------------------- PLAN
# Local folders searched for the live capture a family clip was built from (by file name + SHA256).
CAPTURE_DIRS = (SCRATCH / "universal" / "cap", SCRATCH / "s05a2" / "cap", SCRATCH / "paramwalk")


@functools.lru_cache(maxsize=64)
def find_capture(name, sha256, dirs=CAPTURE_DIRS):
    """Path of the exact capture (name AND sha256 must match), else None. Never a look-alike file."""
    import hashlib
    if not name or not sha256:
        return None
    for d in dirs:
        p = Path(d) / name
        if p.exists():
            h = hashlib.sha256()
            with open(p, "rb") as fh:
                for chunk in iter(lambda: fh.read(1 << 20), b""):
                    h.update(chunk)
            if h.hexdigest() == sha256:
                return p
    return None


# Local folders searched for an already baked action library (actions.json) of a profile specimen.
LIBRARY_DIRS = (SCRATCH / "lifecycle", SCRATCH / "animsys", SCRATCH / "s05ba")


def find_action_library(entity, dirs=LIBRARY_DIRS):
    """Most complete local action manifest for the entity's profile, else None.

    Accepted only with BAKE_RESULT=PASS and the same kind, seed, rig (family) and a weapon allowed by
    the profile; preference: native lifecycle reference, then most EMULATED_ALPHA_CODE entries."""
    acts = entity["channels"]["actions"]
    if acts["status"] != "AVAILABLE":
        return None
    prof = ct.ACTION_PROFILES[acts["profile"]]
    kind = int(entity["kind"], 16)
    best = None
    for d in dirs:
        for path in sorted(Path(d).rglob("actions.json")):
            try:
                m = json.loads(path.read_text(encoding="utf-8"))
            except (OSError, ValueError):
                continue
            if (m.get("format") != "cubegame.alpha_actions/1" or m.get("BAKE_RESULT") != "PASS"
                    or m.get("kind") != kind or m.get("rig") != entity["family_id"]
                    or list(m.get("seed_variant", [])) != list(entity["seed_variant"])):
                continue
            w = (prof.get("equipment") or {}).get("weapon")
            if w and (m.get("weapon", [0, 0])[0] != w["type"] or m.get("weapon", [0, 0])[1] not in w["subtypes"]):
                continue
            score = (bool(m.get("lifecycle_sha256")), sum(a["status"] == prof["status"] for a in m.get("actions", [])))
            if best is None or score > best[0]:
                best = (score, path, m)
    return best and {"path": best[1], "manifest": best[2]}


def job_for(entity, capture_dirs=CAPTURE_DIRS, library_dirs=LIBRARY_DIRS):
    """Generated job (same schema as tools/alpha_models/universal/jobs/*.json).

    When the family has a captured walk clip and its source capture is present locally (same SHA256),
    the job names it as ``reference_capture``: Unity then checks the formula against live Alpha frames
    of the family (for a never-captured kind: frames of the family's source kind)."""
    job = {"kind": entity["kind"], "seed_variant": entity["seed_variant"],
           "variant": f"auto_{entity['kind']}_{(entity.get('name_hint') or 'kind').lower()}",
           "generated_by": "transfer_planner PLAN", "contract": ct.FORMAT}
    walk = entity["channels"]["walk"]
    if any(s["source"] == "param_walk_formula" for s in walk["sources"]):
        job.update(locomotion="formula", amp=DEFAULT_LOCOMOTION["amp"], speed_xy=DEFAULT_LOCOMOTION["speed_xy"],
                   speed_status=DEFAULT_LOCOMOTION["speed_status"])
        for src in walk["sources"]:
            if src["source"] == "family_clip":
                cap = find_capture(src.get("capture"), src.get("capture_sha256"), capture_dirs)
                if cap is not None:
                    job["reference_capture"] = {"path": str(cap).replace("\\", "/"), "kind": src["rig_kind_source"],
                                                "sha256": src["capture_sha256"]}
                    job["reference_status"] = ("receiver kind captured" if src.get("receiver_captured") else
                                               f"family capture of kind {src['rig_kind_source']}; receiver not captured")
                break
    lib = find_action_library(entity, library_dirs)
    if lib is not None:
        prof = ct.ACTION_PROFILES[entity["channels"]["actions"]["profile"]]
        spec_weapon = (prof.get("specimen_equipment") or {}).get("weapon")
        weq = dict(spec_weapon) if spec_weapon else None
        if weq and lib["manifest"]["weapon"][1] != weq["subtype"]:
            weq = None                       # library baked with another weapon: no model evidence here
        if weq or (not spec_weapon and list(lib["manifest"].get("weapon", [0, 0])) == [0, 0]):
            if weq:
                job["equipment"] = {"weapon": weq}
            job["action_manifest"] = str(lib["path"].relative_to(ROOT)).replace("\\", "/")
            job["clips"] = str(lib["path"].parent.relative_to(ROOT)).replace("\\", "/")
            job["actions_status"] = f"profile {entity['channels']['actions']['profile']} (local library)"
            if prof.get("trigger") == "creature_ai":
                job["include_family_clips"] = True
    return job


def plan(scan_result):
    rows = []
    for e in scan_result["entities"]:
        ch = e.get("channels", {})
        row = {"kind": e["kind"], "name_hint": e.get("name_hint"), "category": e.get("category"),
               "body_plan": e.get("body_plan"), "family_id": e.get("family_id"),
               "parts": [p["name"] for p in e.get("parts", [])], "readiness": e["readiness"],
               "reasons": e["reasons"], "pilot_allowed": int(e["kind"], 16) in PILOT_ALLOWLIST,
               "channels": {n: _channel_summary(n, c) for n, c in ch.items()
                            if n in ("geometry", "walk", "idle", "actions")}}
        if e["readiness"] == "AUTO_READY":
            row["job"] = job_for(e)
            row["steps"] = ["models -> Unity meshes (expected vertices)", "rig.json (anchors, mirroring)",
                            "contract.json", "walk/idle: " + "+".join(s["provenance"] for s in ch["walk"]["sources"]),
                            "actions: " + ch["actions"]["status"]]
        rows.append(row)
    fams = collections.OrderedDict()
    for r in rows:
        if not r["family_id"]:
            continue
        f = fams.setdefault(r["family_id"], {"kinds": [], "category": r["category"], "body_plan": r["body_plan"],
                                             "readiness": r["readiness"], "walk_sources": set(), "actions": set()})
        f["kinds"].append(r["kind"])
        f["walk_sources"].update(r["channels"].get("walk", {}).get("sources", []))
        f["actions"].add(r["channels"].get("actions", {}).get("status"))
    for f in fams.values():
        f["walk_sources"] = sorted(f["walk_sources"])
        f["actions"] = sorted(a for a in f["actions"] if a)
    return {"format": "cubegame.transfer_plan/1", "metrics": plan_metrics(rows, fams), "families": fams,
            "entities": rows, "pilot_allowlist": {f"0x{k:02x}": v for k, v in PILOT_ALLOWLIST.items()},
            "batch": {"authorized": [list(x) for x in BATCH_AUTHORIZATIONS],
                      "other": "NOT_AUTHORIZED (further S05B-2 families need the producer's review of the "
                               "first one; creature actions need their own trigger evidence)"}}


def _channel_summary(name, c):
    if name == "walk":
        return {"status": c["status"], "sources": [s["provenance"] for s in c["sources"]],
                "formula": c.get("formula"), "reasons": c["reasons"]}
    if name == "idle":
        return {"status": c["status"], "sources": [s["provenance"] for s in c["sources"]], "reasons": c["reasons"]}
    if name == "actions":
        return {"status": c["status"], "profile": c.get("profile"), "reasons": c["reasons"]}
    return {"status": c["status"], "reasons": c.get("reasons", [])}


def plan_metrics(rows, fams):
    def reason_key(r):
        w = r.split(" ")
        return w[0] if len(w) < 2 or not w[0].startswith(("UNVERIFIED", "PART_NOT")) else f"{w[0]} {w[1]}"
    return {
        "kinds": len(rows),
        "by_readiness": dict(collections.Counter(r["readiness"] for r in rows)),
        "by_category": {cat: dict(collections.Counter(r["readiness"] for r in rows if r["category"] == cat))
                        for cat in sorted({r["category"] for r in rows if r["category"]})},
        "families": len(fams),
        "families_by_readiness": dict(collections.Counter(f["readiness"] for f in fams.values())),
        "walk_sources": dict(collections.Counter("+".join(r["channels"].get("walk", {}).get("sources", [])) or "NONE"
                                                 for r in rows)),
        "actions_available": [r["kind"] for r in rows if r["channels"].get("actions", {}).get("status") == "AVAILABLE"],
        "actions_blocked_reasons": dict(collections.Counter(r["channels"]["actions"]["reasons"][0].split(":")[0].split(" ")[0]
                                                            for r in rows if r["channels"].get("actions", {}).get("reasons"))),
        "blocking_reasons": dict(collections.Counter(reason_key(x) for r in rows for x in r["reasons"])),
    }


def plan_markdown(p):
    m = p["metrics"]
    out = ["# Transfer plan (generated, local)", "",
           "Generated by `transfer_planner plan`. AUTO_READY = geometry + rig + walk/idle can be produced "
           "without a new Alpha run; actions are a separate channel. Not an S05B authorisation.", "",
           "| Readiness | Kinds |", "|---|---|"]
    out += [f"| {k} | {v} |" for k, v in sorted(m["by_readiness"].items())]
    out += ["", "| Category | " + " | ".join(ct.READINESS) + " |", "|---|" + "---|" * len(ct.READINESS)]
    for cat, c in m["by_category"].items():
        out.append(f"| {cat} | " + " | ".join(str(c.get(r, 0)) for r in ct.READINESS) + " |")
    out += ["", "| Kind | Name hint | Category | Parts | Walk | Actions | Readiness | Reasons |",
            "|---|---|---|---|---|---|---|---|"]
    for r in p["entities"]:
        ch = r["channels"]
        out.append(f"| {r['kind']} | {r['name_hint'] or ''} | {r['category'] or ''} | {len(r['parts'])} | "
                   f"{'+'.join(ch.get('walk', {}).get('sources', [])) or 'BLOCKED'} | "
                   f"{ch.get('actions', {}).get('status', '')} | {r['readiness']} | "
                   f"{'; '.join(r['reasons'])[:120]} |")
    return "\n".join(out) + "\n"


COVERAGE_COLUMNS = ("kind", "name_hint", "category", "parts", "body_plan", "family", "readiness", "walk",
                    "idle", "actions", "pilot_allowed", "reasons")


def coverage_csv(p):
    """Metadata-only coverage matrix (statuses and reasons; no Alpha numbers, poses or models)."""
    import csv
    import io
    buf = io.StringIO()
    w = csv.writer(buf, lineterminator="\n")
    w.writerow(COVERAGE_COLUMNS)
    for r in p["entities"]:
        ch = r["channels"]
        w.writerow([r["kind"], r["name_hint"] or "", r["category"] or "", len(r["parts"]), r["body_plan"] or "",
                    (r["family_id"] or "").split("/")[-1], r["readiness"],
                    "+".join(ch.get("walk", {}).get("sources", [])) or "BLOCKED",
                    "+".join(ch.get("idle", {}).get("sources", [])) or "BLOCKED",
                    ch.get("actions", {}).get("status", "") + (
                        ":" + ch["actions"]["reasons"][0].split(":")[0].split(" ")[0] if ch.get("actions", {}).get("reasons") else ""),
                    int(r["pilot_allowed"]), "; ".join(r["reasons"])])
    return buf.getvalue()


# --------------------------------------------------------------------------- VALIDATE
def validate(table, clips_root, jobs_dir=JOBS_DIR, manifests=()):
    problems, checks = [], []
    scan_result = scan(table, clips_root)
    by_kind = {e["kind"]: e for e in scan_result["entities"]}
    # 1. repository jobs vs the contract
    for path in sorted(glob.glob(os.path.join(jobs_dir, "*.json"))):
        job = json.load(open(path, encoding="utf-8"))
        k = f"0x{int(str(job['kind']), 0):02x}"
        e = by_kind.get(k)
        name = os.path.basename(path)
        if e is None:
            problems.append(f"JOB_KIND_UNKNOWN {name} {k}")
            continue
        if list(job.get("seed_variant", [0, 0])) != [0, 0]:
            checks.append(f"JOB {name}: seed variant {job['seed_variant']} needs the emulator (not checked statically)")
            continue
        weapon = (job.get("equipment") or {}).get("weapon")
        if weapon:
            e = ct.describe(int(k, 16), table["kinds"][k], table, clips_root, equipment=job["equipment"])
            if e["channels"]["equipment"]["weapon"]["status"] != "SUPPORTED":
                problems.append(f"JOB_WEAPON_NOT_SUPPORTED {name} {e['channels']['equipment']['weapon']['reasons']}")
        if job.get("locomotion") == "formula" and e["channels"]["walk"]["formula"] != "FORMULA_VERIFIED":
            problems.append(f"JOB_FORMULA_NOT_VERIFIED {name} {e['channels']['walk']['formula']}")
        if job.get("action_manifest") and e["channels"]["actions"]["status"] != "AVAILABLE":
            problems.append(f"JOB_ACTIONS_WITHOUT_PROFILE {name} {e['channels']['actions']['reasons']}")
        if e["readiness"] == "BLOCKED":
            problems.append(f"JOB_FOR_BLOCKED_KIND {name} {e['reasons']}")
        checks.append(f"JOB {name}: kind {k} {e['category']} readiness={e['readiness']} OK")
    # 2. family clips on disk vs the family contract
    fam_parts, fam_layers = {}, {}
    for e in scan_result["entities"]:
        if e.get("family_id"):
            fam_parts[e["family_id"]] = [p["name"] for p in e["parts"] if p["role"] != "equipment"]
            # the weapon layer is optional for a family only when its body plan supports it
            if e["channels"]["equipment"]["weapon"]["status"] == "SUPPORTED":
                fam_layers[e["family_id"]] = cr.EQUIPMENT_BLOCKS["weapon"]
    for d in sorted(glob.glob(os.path.join(clips_root, "alpha_creature_*"))):
        fid = "alpha_creature/" + os.path.basename(d)[len("alpha_creature_"):]
        if fid not in fam_parts:
            problems.append(f"CLIP_DIR_FAMILY_UNKNOWN {os.path.basename(d)}")
            continue
        for path in sorted(glob.glob(os.path.join(d, "*.alphaclip.json"))):
            clip = json.load(open(path, encoding="utf-8"))
            ps = ct.validate_clip(clip, fid, fam_parts[fid], fam_layers.get(fid, ()))
            problems += ps
            if not ps:
                checks.append(f"CLIP {os.path.basename(d)}/{os.path.basename(path)} {clip['status']} "
                              f"{ct.clip_category(clip)} parts={len(fam_parts[fid])} OK")
    # 3. pinned action-profile evidence still matches the static table
    problems += ct.profile_family_drift(table)
    # 4. action manifests: profile evidence + every listed clip against the contract
    for mpath in manifests:
        problems += validate_manifest(mpath, table, checks)
    return {"VALIDATE_RESULT": "PASS" if not problems else "FAIL", "problems": problems, "checks": checks,
            "metrics": scan_result["metrics"]}


def validate_manifest(mpath, table, checks):
    problems = []
    man = json.load(open(mpath, encoding="utf-8"))
    kind = int(man["kind"]) if isinstance(man["kind"], int) else int(str(man["kind"]), 0)
    k = f"0x{kind:02x}"
    entry = table["kinds"].get(k)
    if entry is None:
        return [f"MANIFEST_KIND_UNKNOWN {mpath}"]
    weapon = man.get("weapon") or [0, 0]
    equipment = {"weapon": {"subtype": weapon[1]}} if weapon[0] else None
    e = ct.describe(kind, entry, table, None, seed_variant=man.get("seed_variant", [0, 0]), equipment=equipment)
    if e["channels"]["actions"]["status"] != "AVAILABLE":
        return [f"MANIFEST_WITHOUT_ACTION_PROFILE {k} {e['channels']['actions']['reasons']}"]
    if man.get("rig") != e["family_id"]:
        problems.append(f"MANIFEST_RIG_MISMATCH {man.get('rig')} != {e['family_id']}")
    profile = ct.ACTION_PROFILES[e["channels"]["actions"]["profile"]]
    parts = [p["name"] for p in e["parts"]]
    base = os.path.dirname(mpath)
    counts = collections.Counter()
    for a in man["actions"]:
        if a["status"] == "BLOCKED":
            if not a.get("reason"):
                problems.append(f"MANIFEST_BLOCKED_WITHOUT_REASON {a['clip']}")
            counts["BLOCKED"] += 1
            continue
        if a["status"] != profile["status"]:
            problems.append(f"MANIFEST_STATUS_NOT_PROFILE {a['clip']} {a['status']}")
        path = os.path.join(base, a.get("file", ""))
        if not os.path.exists(path):
            problems.append(f"MANIFEST_CLIP_FILE_MISSING {a['clip']}")
            continue
        clip = json.load(open(path, encoding="utf-8"))
        ps = ct.validate_clip(clip, e["family_id"], parts)
        cat = ct.clip_category(clip)
        if cat not in profile["categories"]:
            ps.append(f"CLIP_CATEGORY_NOT_IN_PROFILE {a['clip']} {cat}")
        problems += ps
        counts[cat] += 1
    checks.append(f"MANIFEST {mpath}: profile {e['channels']['actions']['profile']} kind {k} "
                  f"{dict(counts)}")
    return problems


# --------------------------------------------------------------------------- PILOT
def pilot(table, kinds, clips_root, out_dir, execute=False, python=sys.executable, fps=60):
    refused = [f"0x{k:02x}" for k in kinds if k not in PILOT_ALLOWLIST]
    if refused:
        return {"PILOT_RESULT": "REFUSED", "problems": [f"NOT_IN_PILOT_ALLOWLIST {', '.join(refused)} "
                                                       "(new kinds need a producer decision)"]}
    out_dir = local_out(out_dir)
    (out_dir / "jobs").mkdir(parents=True, exist_ok=True)
    sf = ct.specimen_families(table)
    results, problems = [], []
    for k in kinds:
        key = f"0x{k:02x}"
        e = ct.describe(k, table["kinds"][key], table, clips_root, spec_families=sf)
        if e["readiness"] != "AUTO_READY":
            problems.append(f"PILOT_KIND_NOT_READY {key} {e['readiness']} {e['reasons']}")
            continue
        job = job_for(e)
        jpath = out_dir / "jobs" / f"{job['variant']}.json"
        jpath.write_text(json.dumps(job, indent=1), encoding="utf-8")
        r = {"kind": key, "job": str(jpath), "category": e["category"], "parts": len(e["parts"]),
             "actions": e["channels"]["actions"]["status"]}
        if execute:
            project = SCRATCH / "universal" / "unity" / f"Pilot_{key}"
            cmd = ["powershell.exe", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
                   str(ROOT / "tools/alpha_models/transfer/run_transfer.ps1"), "-Project", str(project),
                   "-UniversalJob", str(jpath), "-Python", python, "-ControllerFps", str(fps)]
            p = subprocess.run(cmd, cwd=ROOT, capture_output=True, text=True)
            log = out_dir / f"run_{key}.log"
            log.write_text(p.stdout + "\n" + p.stderr, encoding="utf-8")
            r.update(exit=p.returncode, log=str(log), project=str(project),
                     verdicts=[ln for ln in p.stdout.splitlines() if "_RESULT=" in ln or ln.startswith("RESULT=")])
            if p.returncode != 0:
                problems.append(f"PILOT_RUN_FAIL {key} exit={p.returncode}")
        results.append(r)
    return {"PILOT_RESULT": "PASS" if not problems and results else "FAIL", "problems": problems,
            "results": results}


# --------------------------------------------------------------------------- BATCH
def batch_authorized(authorization, scope):
    """None when (decision, scope) is authorised, else the refusal reason."""
    from tools.alpha_models.universal import batch_runner as br
    if (authorization, scope) not in BATCH_AUTHORIZATIONS:
        return (f"NOT_AUTHORIZED (authorization={authorization!r}, scope={scope!r}); authorised: "
                f"{list(BATCH_AUTHORIZATIONS)}; other S05B work needs a producer decision")
    if scope not in br.SCOPES or br.SCOPES[scope]["decision"] != authorization:
        return f"SCOPE_NOT_DEFINED_FOR_DECISION {scope} {authorization}"
    return None


def batch(authorization=None, scope=None, table=None, clips_root=None, phases=(1, 2), dry_run=False,
          exe=None, python=sys.executable, out_dir=None):
    refusal = batch_authorized(authorization, scope)
    if refusal:
        return {"BATCH_RESULT": "NOT_AUTHORIZED", "problems": [refusal]}
    from tools.alpha_models.universal import batch_runner as br
    from tools.alpha_models.universal import pipeline as pl
    sc = br.SCOPES[scope]
    out_root = br.in_scratch(out_dir or sc["out"])
    p = plan(scan(table, clips_root))
    run_slots = br.emulator_slots(exe or pl.DEFAULT_EXE)
    inputs = {"static_sha256": table.get("_sha256"), "exe_sha256": table.get("exe_sha256"),
              "captures": sorted({(r.get("job") or {}).get("reference_capture", {}).get("sha256", "")
                                  for r in p["entities"] if not isinstance(sc["kinds"], str)
                                  and int(r["kind"], 16) in sc["kinds"]} - {""})}
    if dry_run:
        rows, problems = br.select_scope(p, sc)
        if problems:
            return {"BATCH_RESULT": "REFUSED", "problems": problems}
        entries, coverage, cp = br.build_entries(rows, sc, run_slots, table)
        out_root.mkdir(parents=True, exist_ok=True)
        br.write_json_atomic(out_root / "entries.json", {"entries": entries, "coverage": coverage})
        return {"BATCH_RESULT": "DRY_RUN", "problems": cp, "entries": len(entries),
                "by_phase": dict(collections.Counter(e["phase"] for e in entries)),
                "coverage": {k: {"wanted": v["wanted"], "seeds": [s["seed"] for s in v["seeds"]]}
                             for k, v in coverage.items()}}
    runner = br.unity_runner(sc["project"], python)
    reg = sc.get("regression")
    reg_runner = br.unity_runner(reg["project"], python) if reg else None
    evidence_check = None
    if sc.get("mode") == "actions":
        from tools.alpha_models.anim import pe_bytes as pb
        inputs["npc_evidence_sha256"] = _file_sha256(ct.NPC_EVIDENCE_PATH)
        image = pb.load(str(exe or pl.DEFAULT_EXE))
        if isinstance(sc["kinds"], str):
            # D64 profiles: the committed trigger evidence must match the code that produced it
            # (re-executing 280 NPC states per kind is the trigger_catalog job, not a pre-flight check)
            from tools.alpha_models.catalog import trigger_catalog as tc
            inputs["trigger_evidence_sha256"] = _file_sha256(str(tc.EVIDENCE_PATH))
            d60 = {s["kind"] for p in ct.ACTION_PROFILES.values() if p.get("authorization", "").startswith("D60")
                   for s in p["specimens"]}
            evidence_check = lambda kinds: tc.evidence_drift() + br.evidence_drift(image, [k for k in kinds if k in d60])
        else:
            evidence_check = lambda kinds: br.evidence_drift(image, kinds)
    summary = br.execute(scope, p, table, run_slots, runner, out_root, inputs, reg_runner, phases,
                         evidence_check=evidence_check)
    if summary.get("BATCH_RESULT") != "REFUSED":
        summary["contact_sheets"] = br.contact_sheets(out_root, summary)
        br.write_json_atomic(out_root / "summary.json", summary)
    return summary


# --------------------------------------------------------------------------- CLI
def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    sub = ap.add_subparsers(dest="mode", required=True)
    for name in ("scan", "plan", "validate", "pilot", "batch"):
        s = sub.add_parser(name)
        s.add_argument("--static", default=str(DEFAULT_STATIC))
        s.add_argument("--clips-root", default=str(DEFAULT_CLIPS))
        if name == "scan":
            s.add_argument("--out", required=True)
        if name == "plan":
            s.add_argument("--out-dir", required=True)
        if name == "validate":
            s.add_argument("--manifest", action="append", default=[])
            s.add_argument("--jobs-dir", default=str(JOBS_DIR))
            s.add_argument("--out", default=None)
        if name == "pilot":
            s.add_argument("--kinds", required=True)
            s.add_argument("--out-dir", default=str(SCRATCH / "universal" / "pilot"))
            s.add_argument("--execute", action="store_true")
            s.add_argument("--fps", type=int, choices=(30, 60, 120), default=60)
        if name == "batch":
            s.add_argument("--authorization", default=None)
            s.add_argument("--scope", default=None)
            s.add_argument("--phase", type=int, choices=(1, 2), action="append", default=None)
            s.add_argument("--dry-run", action="store_true")
            s.add_argument("--out-dir", default=None)
            s.add_argument("--producer-approved-d64", action="store_true",
                           help="required for D63/d63_full: the producer approved the D64 research result")
    a = ap.parse_args(argv)
    if a.mode == "batch":
        refusal = batch_authorized(a.authorization, a.scope)
        need = APPROVAL_REQUIRED.get((a.authorization, a.scope))
        if not refusal and need and not a.dry_run and not a.producer_approved_d64:
            refusal = f"APPROVAL_REQUIRED {need}: run only after the producer approved the D64 result"
        if refusal:
            print(json.dumps({"BATCH_RESULT": "NOT_AUTHORIZED", "problems": [refusal]}, indent=1))
            return 3
        table = load_static(a.static)
        table["_sha256"] = _file_sha256(a.static)
        r = batch(a.authorization, a.scope, table, a.clips_root, tuple(a.phase or (1, 2)), a.dry_run,
                  out_dir=a.out_dir)
        print(json.dumps({k: v for k, v in r.items() if k not in ("entries", "kinds")}, indent=1, default=str))
        return {"PASS": 0, "PASS_FORMULA_ONLY": 0, "PASS_ORACLE_VERIFIED": 0, "DRY_RUN": 0, "NOT_AUTHORIZED": 3,
                "REFUSED": 2}.get(r["BATCH_RESULT"], 1)
    table = load_static(a.static)
    if a.mode == "scan":
        r = scan(table, a.clips_root)
        out = local_out(a.out)
        out.parent.mkdir(parents=True, exist_ok=True)
        out.write_text(json.dumps(r, indent=1), encoding="utf-8")
        print(json.dumps({"SCAN": "DONE", **r["metrics"]}, indent=1))
        return 0
    if a.mode == "plan":
        p = plan(scan(table, a.clips_root))
        d = local_out(a.out_dir)
        (d / "jobs").mkdir(parents=True, exist_ok=True)
        (d / "plan.json").write_text(json.dumps(p, indent=1), encoding="utf-8")
        (d / "plan.md").write_text(plan_markdown(p), encoding="utf-8")
        (d / "coverage.csv").write_text(coverage_csv(p), encoding="utf-8")
        for r in p["entities"]:
            if r.get("job"):
                (d / "jobs" / f"{r['job']['variant']}.json").write_text(json.dumps(r["job"], indent=1), encoding="utf-8")
        print(json.dumps({"PLAN": "DONE", **{k: v for k, v in p["metrics"].items() if k != "by_category"}}, indent=1))
        return 0
    if a.mode == "validate":
        r = validate(table, a.clips_root, a.jobs_dir, a.manifest)
        if a.out:
            out = local_out(a.out)
            out.parent.mkdir(parents=True, exist_ok=True)
            out.write_text(json.dumps(r, indent=1), encoding="utf-8")
        print(json.dumps({k: v for k, v in r.items() if k != "metrics"}, indent=1))
        return 0 if r["VALIDATE_RESULT"] == "PASS" else 1
    if a.mode == "pilot":
        kinds = [int(x, 0) for x in a.kinds.split(",")]
        r = pilot(table, kinds, a.clips_root, a.out_dir, a.execute, fps=a.fps)
        print(json.dumps(r, indent=1))
        return 0 if r["PILOT_RESULT"] == "PASS" else (2 if r["PILOT_RESULT"] == "REFUSED" else 1)
    return 2


if __name__ == "__main__":
    sys.exit(main())
