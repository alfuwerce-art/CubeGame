"""Authorised batch executor of the universal Alpha -> Unity pipeline (S05B-1, decision D59).

Only a scope registered in ``transfer_planner.BATCH_AUTHORIZATIONS`` runs; the scope pins the decision,
the kinds and the coverage rules. Nothing here names a creature: the kind list of a scope is a
producer decision, and the selector (``family walk has a live Alpha capture present locally``) is
re-computed from the static table and must give exactly that list, otherwise the batch refuses
(``SCOPE_DRIFT``).

Flow (one isolated Unity project for the whole batch, results copied out after every run):

  entries   phase 1: every kind at seed (0, 0) + per-family 30/120 Hz samples
            phase 2: extra seed variants until every model of every slot of the kind is covered once
  gate      phase 2 starts only when every phase-1 entry AND the action regression are PASS
  resume    ``batch_state.json``: an entry is skipped only when its last verdict is PASS and the job,
            the input hashes and the copied report files are unchanged (SHA256)
  summary   ``summary.json`` + ``summary.md``: one row per (kind, seed, fps), model coverage per kind

Alpha-derived outputs (jobs with capture paths, Unity reports, frames, sheets) stay under .scratch (D54).
"""
from __future__ import annotations

import collections
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
SCRATCH = ROOT / ".scratch"
RUN_TRANSFER = ROOT / "tools" / "alpha_models" / "transfer" / "run_transfer.ps1"
CONTACT_SHEET = ROOT / "tools" / "alpha_models" / "transfer" / "entity_contact_sheet.ps1"

# Every Unity step a locomotion-only universal job must pass (run_transfer.ps1 prints "<NAME>=PASS exit=0").
REQUIRED_STEPS = ("SELFTEST_RESULT", "TRANSFER_RESULT", "PLAYMODE_RESULT", "PARAMWALK_RESULT",
                  "LOCOMOTION_PLAYMODE_RESULT", "ENTITY_CONTRACT_RESULT", "ENTITY_PLAYMODE_RESULT")
REGRESSION_STEPS = REQUIRED_STEPS[:5] + ("CONTROLLER_PLAYMODE_RESULT", "NATIVE_LIFECYCLE_RESULT",
                                         "NATIVE_LIFECYCLE_PLAYMODE_RESULT")
# report files copied out of TransferOutput after every run (text verdicts + entity frames for the sheets)
KEEP_REPORTS = ("selftest.txt", "transfer_report.txt", "playmode.txt", "paramwalk.txt", "locomotion_playmode.txt",
                "entity_contract.txt", "entity_playmode.txt", "entity_frames.tsv", "controller_playmode.txt",
                "native_lifecycle.txt", "native_lifecycle_playmode.txt")
VARIANT_SLOTS = ("head", "hair", "hand", "foot", "body", "tail")
ACTION_STEPS = REQUIRED_STEPS + ("CONTROLLER_PLAYMODE_RESULT",)

SCOPES = {
    "s05b1": {
        "decision": "D59",
        "title": "S05B-1: geometry + rig + walk/idle of the families with a live Alpha capture",
        "kinds": (0x00, 0x01, 0x02, 0x03, 0x07, 0x08, 0x0d, 0x0e, 0x1e, 0x1f, 0x20, 0x3f, 0x40, 0x41, 0x42),
        "coverage": "B",                      # every model of every slot of the kind at least once
        "fps_samples": (30, 120),             # one kind per family (lowest kind number) re-run at these rates
        "project": SCRATCH / "universal" / "unity" / "S05B1",
        "out": SCRATCH / "universal" / "batch" / "s05b1",
        "regression": {
            "job": ROOT / "tools" / "alpha_models" / "universal" / "jobs" / "human_0x02_lifecycle.json",
            "project": SCRATCH / "s05a2" / "unity" / "AlphaTransfer",
            # S05A2-CONTINE-02 / UNIVERSAL-CONTINUE-01 numbers (reports in docs/reports): must be equal
            "expect": {30: {"actions": 98, "frames": 18023, "native_frames": 1484},
                       60: {"actions": 98, "frames": 35927, "native_frames": 2962},
                       120: {"actions": 98, "frames": 71585, "native_frames": 5919}},
        },
    },
}
# S05B-A part B (D60): creature-AI actions of the S05B-1 kinds. HumanMale 0x02 is not in the list: its
# specimen carries the player profile and its actions are covered by the same regression.
SCOPES["s05ba"] = {
    "decision": "D60",
    "title": "S05B-A part B: original NPC actions (creature AI) of the S05B-1 kinds",
    "mode": "actions",
    "kinds": (0x00, 0x01, 0x03, 0x07, 0x08, 0x0d, 0x0e, 0x1e, 0x1f, 0x20, 0x3f, 0x40, 0x41, 0x42),
    "phase1_kinds": (0x03, 0x1e, 0x3f),       # the part-A pilot specimens, one per family
    "coverage": "BASE",
    "fps_samples": (30, 120),                 # phase-1 kinds re-run at these rates
    "project": SCRATCH / "universal" / "unity" / "S05BA",
    "out": SCRATCH / "universal" / "batch" / "s05ba",
    "regression": SCOPES["s05b1"]["regression"],
}
# S05B-2 (D62): the FIRST new motion family — no kind of it was imported by S05B-1 and no live capture of
# it exists locally, so the walk is the Alpha parametric formula (RECONSTRUCTED_FROM_EVIDENCE) and every
# entry is classified FORMULA_ONLY / live_check=NOT_CAPTURED instead of a live-verified PASS. Actions of
# this family have no proven AI trigger: they stay BLOCKED (no profile is invented).
SCOPES["s05b2"] = {
    "decision": "D62",
    "title": "S05B-2 pilot: first never-imported motion family (biped with fore limbs, head, no hair slot)",
    "families": ("alpha_creature/773a44f932",),
    "kinds": (0x4c, 0x4d, 0x4e, 0x4f),
    "coverage": "BASE",                       # one model per slot: seed (0, 0) is the full model coverage
    "fps_samples": (30, 120),
    "live_frames": "formula_only",            # no capture of this family: classified, never claimed as live
    "project": SCRATCH / "universal" / "unity" / "S05B2",
    "out": SCRATCH / "universal" / "batch" / "s05b2",
    "regression": SCOPES["s05b1"]["regression"],
}


# --------------------------------------------------------------------------- helpers
def sha256_file(path):
    h = hashlib.sha256()
    with open(path, "rb") as fh:
        for chunk in iter(lambda: fh.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def sha256_json(obj):
    return hashlib.sha256(json.dumps(obj, sort_keys=True).encode("utf-8")).hexdigest()


def in_scratch(path):
    p = Path(path).resolve()
    if not p.is_relative_to(SCRATCH.resolve()):
        raise ValueError("BATCH_OUTPUT_MUST_BE_IN_SCRATCH")
    return p


def write_json_atomic(path, obj):
    path = Path(path)
    tmp = path.with_suffix(path.suffix + ".tmp")
    tmp.write_text(json.dumps(obj, indent=1), encoding="utf-8")
    os.replace(tmp, path)


def entry_id(kind, seed, fps):
    return f"0x{kind:02x}_s{seed[0]}-{seed[1]}_{fps}hz"


# --------------------------------------------------------------------------- scope selection
def _profile_trigger(pid):
    from tools.alpha_models.universal import contract as ct
    return (ct.ACTION_PROFILES.get(pid or "") or {}).get("trigger")


def select_scope(plan_result, scope):
    """Kinds of the scope recomputed by the selector; refuses when they differ from the pinned list."""
    if scope.get("families"):
        # a never-captured motion family: the selector is "every kind of the pinned families is AUTO_READY
        # with a verified walk formula" (no live capture exists, so no capture selector can apply)
        found = sorted(int(r["kind"], 16) for r in plan_result["entities"]
                       if r.get("family_id") in set(scope["families"]) and r["readiness"] == "AUTO_READY"
                       and r["channels"]["walk"].get("formula") == "FORMULA_VERIFIED")
    else:
        found = sorted(int(r["kind"], 16) for r in plan_result["entities"]
                       if r["readiness"] == "AUTO_READY" and (r.get("job") or {}).get("reference_capture")
                       and "TRANSFERRED_FROM_ALPHA" in r["channels"]["walk"]["sources"]
                       and (scope.get("mode") != "actions" or (
                           r["channels"]["actions"]["status"] == "AVAILABLE"
                           and _profile_trigger(r["channels"]["actions"].get("profile")) == "creature_ai")))
    pinned = sorted(scope["kinds"])
    problems = []
    if found != pinned:
        reasons = []
        if scope.get("families"):
            for r in plan_result["entities"]:
                if r.get("family_id") in set(scope["families"]) and r["readiness"] != "AUTO_READY":
                    reasons.append(f"{r['kind']}={r['readiness']} {'; '.join(r.get('reasons') or [])}")
        problems.append("SCOPE_DRIFT selector=" + ",".join(f"0x{k:02x}" for k in found)
                        + " pinned=" + ",".join(f"0x{k:02x}" for k in pinned)
                        + ((" not_ready=[" + "; ".join(reasons) + "]") if reasons else ""))
    rows = {int(r["kind"], 16): r for r in plan_result["entities"]}
    return [rows[k] for k in pinned if k in rows], problems


# --------------------------------------------------------------------------- variant coverage
def variant_seeds(kind, run_slots, wanted, sweep=60):
    """Smallest greedy set of seeds (always starting with (0, 0)) whose resolved models cover every
    value of ``wanted`` ({slot: [model index, ...]}) at least once.

    ``run_slots(kind, s2, s3) -> {slot: model index}`` is the appearance emulator (or a test stub).
    Candidates: single-axis sweeps of seed[2] and seed[3], then pairs of their representatives (each
    pair is emulated, never assumed independent). Returns (seeds with covered values, problems)."""
    need = {(s, v) for s, vals in wanted.items() for v in vals if len(vals) > 1}
    cand = {}

    def add(seed):
        if seed not in cand:
            sl = run_slots(kind, *seed)
            cand[seed] = {(s, sl.get(s)) for s in wanted if sl.get(s) is not None}

    add((0, 0))
    for s in range(1, sweep):
        add((s, 0))
        add((0, s))
    reps2, reps3, seen2, seen3 = [], [], set(), set()
    for s in range(sweep):
        k2, k3 = frozenset(cand[(s, 0)]), frozenset(cand[(0, s)])
        if k2 not in seen2:
            seen2.add(k2)
            reps2.append(s)
        if k3 not in seen3:
            seen3.add(k3)
            reps3.append(s)
    for a in reps2:
        for b in reps3:
            add((a, b))
    chosen = [(0, 0)]
    covered = set(cand[(0, 0)])
    while need - covered:
        best = max(cand, key=lambda sd: (len((cand[sd] & need) - covered), -(sd[0] + sd[1]), -sd[0]))
        gain = (cand[best] & need) - covered
        if not gain:
            break
        chosen.append(best)
        covered |= gain
    missing = sorted(need - covered)
    problems = [f"VARIANT_NOT_COVERED 0x{kind:02x} {s}={v}" for s, v in missing]
    return [{"seed": list(sd), "models": {s: v for s, v in sorted(cand[sd])},
             "new": sorted(f"{s}={v}" for s, v in (cand[sd] & need))} for sd in chosen], problems


def emulator_slots(exe):
    """run_slots backed by the Cube.exe appearance emulator (pinned build, read-only)."""
    from tools.alpha_models.anim import pe_bytes as pb
    from tools.alpha_models.universal import alpha_static as st
    st.verify_exe(exe)
    emu = st.AppearanceEmulator(pb.load(exe))

    def run_slots(kind, s2, s3):
        d = st.decode_appearance(emu.run(kind, s2, s3)["raw"])
        return {n: v for n, v in d["slots"].items() if n in VARIANT_SLOTS and v != -1}
    return run_slots


# --------------------------------------------------------------------------- entries
def build_entries(rows, scope, run_slots, static_table):
    """Phase 1: base seed of every kind + fps samples; phase 2: remaining model variants (coverage B)."""
    entries, problems, coverage = [], [], {}
    sample_kinds = {}
    for r in rows:
        sample_kinds.setdefault(r["family_id"], int(r["kind"], 16))
    if scope.get("mode") == "actions":
        for r in rows:
            kind = int(r["kind"], 16)
            ph = 1 if kind in scope["phase1_kinds"] else 2
            coverage[r["kind"]] = {"wanted": {}, "seeds": [{"seed": [0, 0], "models": {}, "new": []}]}
            for fps in [60] + (list(scope["fps_samples"]) if ph == 1 else []):
                entries.append({"id": entry_id(kind, (0, 0), fps), "kind": r["kind"], "seed": [0, 0], "fps": fps,
                                "phase": ph, "family_id": r["family_id"], "name_hint": r.get("name_hint"),
                                "models": {}, "covers": [], "profile": r["channels"]["actions"]["profile"]})
        return entries, coverage, problems
    for r in rows:
        kind = int(r["kind"], 16)
        wanted = {s: v for s, v in static_table["kinds"][r["kind"]].get("slot_variants", {}).items()
                  if s in VARIANT_SLOTS}
        seeds, ps = variant_seeds(kind, run_slots, wanted) if scope["coverage"] == "B" else (
            [{"seed": [0, 0], "models": {}, "new": []}], [])
        problems += ps
        coverage[r["kind"]] = {"wanted": {s: len(v) for s, v in wanted.items()}, "seeds": seeds}
        for i, sv in enumerate(seeds):
            fps_list = [60] + (list(scope["fps_samples"]) if i == 0 and sample_kinds[r["family_id"]] == kind else [])
            for fps in fps_list:
                entries.append({"id": entry_id(kind, sv["seed"], fps), "kind": r["kind"], "seed": sv["seed"],
                                "fps": fps, "phase": 1 if i == 0 else 2, "family_id": r["family_id"],
                                "name_hint": r.get("name_hint"), "models": sv["models"], "covers": sv["new"]})
    return entries, coverage, problems


# code whose change must invalidate every baked library (П2 = drawModel oracle + the baker itself)
BAKE_CODE = ("action_baker.py", "alpha_oracle.py", "drawmodel_emu.py", "x86emu.py", "creature_rig.py",
             "alpha_static.py", "contract.py")
CACHE_KEY_FILE = "cache_key.json"


def bake_cache_key(entry, exe=None):
    """SHA256 inputs of one baked library (REVIEW-FIX-01): exe, profile record, evidence file, the
    baking code and the request itself. Any change -> the library is re-baked."""
    from tools.alpha_models.universal import action_baker as ab
    from tools.alpha_models.universal import contract as ct
    here = Path(__file__).resolve().parent
    prof = ct.ACTION_PROFILES[entry["profile"]]
    return {"format": "cubegame.bake_cache_key/1",
            "exe_sha256": sha256_file(exe or ab.DEFAULT_EXE),
            "profile": entry["profile"],
            "profile_sha256": sha256_json(prof),
            "evidence_sha256": sha256_file(ct.NPC_EVIDENCE_PATH),
            "code_sha256": {n: sha256_file(here / n) for n in BAKE_CODE},
            "request": {"kind": entry["kind"], "seed": list(entry["seed"]),
                        "actions": sorted(prof["launched_actions"])}}


def library_reusable(lib, key):
    """(reusable, reason): manifest PASS, the same cache key, and every clip payload's SHA256 equal to
    the one the manifest recorded when it was baked."""
    man = Path(lib) / "actions.json"
    if not man.exists():
        return False, "NO_MANIFEST"
    m = json.loads(man.read_text(encoding="utf-8"))
    if m.get("BAKE_RESULT") != "PASS" or m.get("profile") != key["profile"]:
        return False, "MANIFEST_NOT_PASS_OR_PROFILE"
    if sorted(a["action"] for a in m["actions"] if a["status"] == "EMULATED_ALPHA_CODE") != key["request"]["actions"]:
        return False, "ACTION_SET_CHANGED"
    kf = Path(lib) / CACHE_KEY_FILE
    if not kf.exists() or json.loads(kf.read_text(encoding="utf-8")) != key:
        return False, "CACHE_KEY_CHANGED"
    for a in m["actions"]:
        if a["status"] != "EMULATED_ALPHA_CODE":
            continue
        if not a.get("file") or not a.get("sha256"):
            return False, f"PAYLOAD_UNRECORDED action={a.get('action')}"
        f = Path(lib) / a["file"]
        if not f.exists() or sha256_file(f) != a["sha256"]:
            return False, f"PAYLOAD_SHA256 {a['file']}"
    return True, "REUSED"


def bake_library(entry, out_root, baker=None, exe=None):
    """П2 of one action entry: the kind's launched actions through the drawModel oracle. Reused only when
    library_reusable() holds; otherwise the directory is cleared and the library re-baked."""
    from tools.alpha_models.universal import action_baker as ab
    from tools.alpha_models.universal import contract as ct
    actions = sorted(ct.ACTION_PROFILES[entry["profile"]]["launched_actions"])
    lib = Path(out_root) / "libraries" / entry["kind"]
    key = bake_cache_key(entry, exe)
    ok, reason = library_reusable(lib, key)
    entry["library_cache"] = reason
    if ok:
        return lib
    if lib.exists():
        # only ever clears <out_root>/libraries/<kind> (out_root is checked to be in .scratch by callers)
        if lib.resolve().parent != (Path(out_root) / "libraries").resolve():
            raise ValueError(f"REFUSE_CLEAR {lib}")
        shutil.rmtree(lib)
    (baker or ab.bake_job)({"kind": entry["kind"], "seed_variant": list(entry["seed"])}, actions, str(lib))
    write_json_atomic(lib / CACHE_KEY_FILE, key)
    return lib


def _rel(p):
    return Path(p).resolve().relative_to(ROOT).as_posix()


def action_job(row, entry, lib):
    """Universal job of an action entry: planner job + this kind's batch-baked action library."""
    job = {k: v for k, v in row["job"].items()
           if k not in ("equipment", "action_manifest", "clips", "actions_status", "include_family_clips")}
    job.update(seed_variant=list(entry["seed"]), variant=f"s05ba_{entry['id']}",
               generated_by="batch_runner (D60 S05B-A part B)", batch_entry=entry["id"],
               action_manifest=_rel(Path(lib) / "actions.json"), clips=_rel(lib), include_family_clips=True,
               actions_status=f"profile {entry['profile']} (batch library)",
               library_sha256=sha256_file(Path(lib) / "actions.json"))
    return job


def action_judge(entry):
    """Extra checks of an action entry: ACTIONS mode with the kind's OWN profile and every launched
    action completed by the controller probe."""
    from tools.alpha_models.universal import contract as ct
    n = len(ct.ACTION_PROFILES[entry["profile"]]["launched_actions"])

    def extra(rdir):
        problems = []
        ep = Path(rdir) / "entity_playmode.txt"
        t = ep.read_text(encoding="utf-8") if ep.exists() else ""
        if "controller_mode=ACTIONS" not in t:
            problems.append("ENTITY_NOT_ACTIONS")
        if f"action_profile={entry['profile']} " not in t:
            problems.append("ENTITY_PROFILE_MISMATCH")
        got = regression_numbers(rdir)
        if got.get("actions") != n or got.get("required") != n:
            problems.append(f"ACTIONS_COMPLETED {got.get('actions')}/{got.get('required')} != {n}")
        return problems
    return extra


def run_entry(e, row, out_root, state, state_path, runner, inputs, scope, baker=None):
    live_required = scope.get("live_frames", "required") == "required"
    if scope.get("mode") != "actions":
        return run_one(e["id"], job_for_entry(row, e), e["fps"], out_root, state, state_path, runner, inputs,
                       live_required=live_required)
    try:
        lib = bake_library(e, out_root, baker)
    except Exception as ex:          # a refused/failed bake is a FAIL of the entry, never a skip
        rec = {"id": e["id"], "verdict": "FAIL", "exit": None, "steps": {}, "problems": [f"BAKE_FAILED {ex}"],
               "inputs": inputs, "reports": {}}
        state["entries"][e["id"]] = rec
        write_json_atomic(state_path, state)
        return rec
    return run_one(e["id"], action_job(row, e, lib), e["fps"], out_root, state, state_path, runner, inputs,
                   required=ACTION_STEPS, judge_extra=action_judge(e), locomotion_only=False,
                   live_required=live_required)


def evidence_drift(image, kinds, evidence_path=None):
    """П1 re-run for the scope kinds over EVERY declared seed and the declared observed state, compared
    field by field with the committed evidence (REVIEW-FIX-01: no base-seed-only shortcut)."""
    from tools.alpha_models.universal import ai_trigger as at
    from tools.alpha_models.universal import contract as ct
    ev = json.loads(Path(evidence_path or ct.NPC_EVIDENCE_PATH).read_text(encoding="utf-8"))
    problems = []
    for k in kinds:
        key = f"0x{k:02x}"
        e = ev["kinds"].get(key)
        if not e or e.get("status") != "TRIGGER_EMULATED":
            problems.append(f"EVIDENCE_MISSING {key}")
            continue
        problems += at.evidence_drift_kind(image, key, e)
    return problems


def rebuild_npc_evidence(out_path=None, entries_path=None):
    """Regenerate npc_action_evidence.json from the S05B-1 coverage seeds (reproducible П1 build)."""
    from tools.alpha_models.anim import pe_bytes
    from tools.alpha_models.universal import ai_trigger as at
    from tools.alpha_models.universal import contract as ct
    from tools.alpha_models.universal import transfer_planner as tp
    img = pe_bytes.load(str(at.DEFAULT_EXE))
    cov = json.loads(Path(entries_path or SCRATCH / "universal/batch/s05b1/entries.json").read_text(encoding="utf-8"))["coverage"]
    table = tp.load_static(str(tp.SCRATCH / "universal/static/alpha_static.json"))
    plan = {e["kind"]: e for e in tp.scan(table, str(tp.SCRATCH / "universal/clips"))["entities"]}
    out = at.build_evidence(img, {k: [s["seed"] for s in v["seeds"]] for k, v in cov.items()},
                            {k: plan[k]["family_id"] for k in cov})
    for k, e in out["kinds"].items():          # keep the display names of the committed file
        old = json.loads(Path(ct.NPC_EVIDENCE_PATH).read_text(encoding="utf-8"))["kinds"].get(k, {})
        for f in ("name_hint", "live_check"):
            if f in old:
                e[f] = old[f]
    Path(out_path or ct.NPC_EVIDENCE_PATH).write_text(json.dumps(out, indent=1), encoding="utf-8")
    return out


def job_for_entry(row, entry):
    """Universal job of the entry: the planner job with this seed; NO action library (S05B-1 is
    locomotion only, the humanoid's actions are checked by the separate regression run)."""
    job = {k: v for k, v in row["job"].items()
           if k not in ("equipment", "action_manifest", "clips", "actions_status")}
    job.update(seed_variant=list(entry["seed"]), variant=f"s05b1_{entry['id']}",
               generated_by="batch_runner (D59 S05B-1)", batch_entry=entry["id"])
    return job


# --------------------------------------------------------------------------- unity runs
def parse_verdicts(stdout):
    steps = {}
    for ln in stdout.splitlines():
        m = re.match(r"^([A-Z_]+_RESULT)=(\w+)(?: exit=(-?\d+))?", ln.strip())
        if m:
            steps[m.group(1)] = {"verdict": m.group(2), "exit": None if m.group(3) is None else int(m.group(3))}
        elif ln.strip().startswith("RESULT="):
            steps["RESULT"] = {"verdict": ln.strip().split("=", 1)[1], "exit": None}
    return steps


def unity_runner(project, python=sys.executable):
    """Real runner: run_transfer.ps1 on one job in the batch project; returns (exit, stdout, out_dir)."""
    def run(job_path, fps, log_path):
        cmd = ["powershell.exe", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", str(RUN_TRANSFER),
               "-Project", str(project), "-UniversalJob", str(job_path), "-Python", python,
               "-ControllerFps", str(fps)]
        p = subprocess.run(cmd, cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace")
        Path(log_path).write_text(p.stdout + "\n--- stderr ---\n" + p.stderr, encoding="utf-8")
        return p.returncode, p.stdout, Path(project) / "TransferOutput"
    return run


def copy_reports(src, dst):
    dst = Path(dst)
    if dst.exists():
        shutil.rmtree(dst)
    dst.mkdir(parents=True)
    hashes = {}
    for name in KEEP_REPORTS:
        p = Path(src) / name
        if p.exists():
            shutil.copy2(p, dst / name)
            hashes[name] = sha256_file(dst / name)
    if (Path(src) / "entity_frames").is_dir():
        shutil.copytree(Path(src) / "entity_frames", dst / "entity_frames")
    return hashes


def judge_entry(steps, exit_code, reports_dir, required=REQUIRED_STEPS, locomotion_only=True,
                live_required=True):
    """PASS only when every required step is PASS with exit 0, RESULT=PASS, the run exited 0, the entity
    is locomotion-only and the formula was compared with a non-zero number of live Alpha frames.

    ``live_required=False`` is only for a scope whose motion family was never captured (D62/S05B-2): the
    formula-vs-Alpha comparison cannot exist, so the report must say so explicitly and the entry is
    classified FORMULA_ONLY (``live_check=NOT_CAPTURED``) — it is never counted as a live-verified PASS."""
    problems = []
    if exit_code != 0:
        problems.append(f"RUN_EXIT={exit_code}")
    for s in required:
        st = steps.get(s)
        if not st:
            problems.append(f"STEP_MISSING {s}")
        elif st["verdict"] != "PASS" or st["exit"] != 0:
            problems.append(f"STEP_FAIL {s}={st['verdict']} exit={st['exit']}")
    if (steps.get("RESULT") or {}).get("verdict") != "PASS":
        problems.append("RESULT_NOT_PASS")
    rd = Path(reports_dir)
    pw = rd / "paramwalk.txt"
    if "PARAMWALK_RESULT" in required:
        text = pw.read_text(encoding="utf-8") if pw.exists() else ""
        m = re.search(r"alpha_frames=(\d+)", text)
        if not m or int(m.group(1)) == 0:
            if live_required:
                problems.append("PARAMWALK_WITHOUT_LIVE_FRAMES")
            elif "not_available" not in text:
                problems.append("PARAMWALK_NO_LIVE_AND_NO_REASON")
    ep = rd / "entity_playmode.txt"
    if locomotion_only:
        text = ep.read_text(encoding="utf-8") if ep.exists() else ""
        if "controller_mode=LOCOMOTION_ONLY" not in text:
            problems.append("ENTITY_NOT_LOCOMOTION_ONLY")
        if "locomotion_only_refused=True" not in text:
            problems.append("ACTIONS_NOT_REFUSED")
    return problems


def regression_numbers(reports_dir):
    rd = Path(reports_dir)
    out = {}
    cp = rd / "controller_playmode.txt"
    if cp.exists():
        t = cp.read_text(encoding="utf-8")
        for key, pat in (("frames", r"frames=(\d+)"), ("actions", r"actions_completed=(\d+)"),
                         ("required", r"required=(\d+)")):
            m = re.search(pat, t)
            if m:
                out[key] = int(m.group(1))
    npm = rd / "native_lifecycle_playmode.txt"
    if npm.exists():
        m = re.search(r"frames=(\d+)", npm.read_text(encoding="utf-8"))
        if m:
            out["native_frames"] = int(m.group(1))
    return out


# --------------------------------------------------------------------------- state / resume
def load_state(path):
    if Path(path).exists():
        return json.loads(Path(path).read_text(encoding="utf-8"))
    return {"format": "cubegame.batch_state/1", "entries": {}}


def can_skip(rec, job_sha, inputs, out_root):
    """Skip only a PASS whose job, inputs and copied report files are byte-identical."""
    if not rec or rec.get("verdict") != "PASS" or rec.get("job_sha256") != job_sha or rec.get("inputs") != inputs:
        return False
    rdir = Path(out_root) / "runs" / rec["id"]
    for name, h in rec.get("reports", {}).items():
        p = rdir / name
        if not p.exists() or sha256_file(p) != h:
            return False
    return bool(rec.get("reports"))


def paramwalk_alpha_frames(reports_dir):
    """Live Alpha frames the walk formula was compared with (0 when the family was never captured)."""
    p = Path(reports_dir) / "paramwalk.txt"
    m = re.search(r"alpha_frames=(\d+)", p.read_text(encoding="utf-8")) if p.exists() else None
    return int(m.group(1)) if m else 0


def run_one(entry_key, job, fps, out_root, state, state_path, runner, inputs, required=REQUIRED_STEPS,
            judge_extra=None, locomotion_only=True, live_required=True):
    out_root = Path(out_root)
    jobs_dir = out_root / "jobs"
    jobs_dir.mkdir(parents=True, exist_ok=True)
    job_path = jobs_dir / f"{entry_key}.json"
    job_path.write_text(json.dumps(job, indent=1), encoding="utf-8")
    job_sha = sha256_json(job)
    rec = state["entries"].get(entry_key)
    if can_skip(rec, job_sha, inputs, out_root):
        rec["skipped_on_resume"] = True
        return rec
    (out_root / "logs").mkdir(exist_ok=True)
    t0 = time.time()
    exit_code, stdout, src = runner(job_path, fps, out_root / "logs" / f"{entry_key}.log")
    steps = parse_verdicts(stdout)
    rdir = out_root / "runs" / entry_key
    reports = copy_reports(src, rdir) if Path(src).exists() else {}
    problems = judge_entry(steps, exit_code, rdir, required, locomotion_only, live_required)
    if judge_extra:
        problems += judge_extra(rdir)
    frames = paramwalk_alpha_frames(rdir)
    rec = {"id": entry_key, "verdict": "PASS" if not problems else "FAIL", "exit": exit_code,
           "steps": steps, "problems": problems, "job_sha256": job_sha, "inputs": inputs, "reports": reports,
           "alpha_frames": frames, "live_check": "CAPTURED" if frames else "NOT_CAPTURED",
           "seconds": round(time.time() - t0, 1), "finished": time.strftime("%Y-%m-%dT%H:%M:%S")}
    state["entries"][entry_key] = rec
    write_json_atomic(state_path, state)
    return rec


# --------------------------------------------------------------------------- batch
def execute(scope_name, plan_result, static_table, run_slots, runner, out_root, inputs,
            regression_runner=None, phases=(1, 2), log=print, evidence_check=None, baker=None):
    """Runs the scope; returns the summary dict. ``runner``/``regression_runner`` are injectable."""
    scope = SCOPES[scope_name]
    out_root = in_scratch(out_root)
    out_root.mkdir(parents=True, exist_ok=True)
    state_path = out_root / "batch_state.json"
    state = load_state(state_path)
    rows, problems = select_scope(plan_result, scope)
    if not problems and scope.get("mode") == "actions":
        problems = list(evidence_check(scope["kinds"])) if evidence_check else ["EVIDENCE_CHECK_NOT_RUN"]
    if problems:
        return {"BATCH_RESULT": "REFUSED", "problems": problems}
    entries, coverage, cov_problems = build_entries(rows, scope, run_slots, static_table)
    by_kind = {r["kind"]: r for r in rows}
    write_json_atomic(out_root / "entries.json", {"entries": entries, "coverage": coverage})
    results, gate = {}, []
    # phase 1 + regression
    if 1 in phases:
        for e in [x for x in entries if x["phase"] == 1]:
            log(f"[phase 1] {e['id']} {e['name_hint']}")
            results[e["id"]] = run_entry(e, by_kind[e["kind"]], out_root, state, state_path, runner, inputs,
                                         scope, baker)
            log(f"  -> {results[e['id']]['verdict']} {results[e['id']]['problems']}")
        reg = scope.get("regression")
        if reg and regression_runner:
            job = json.loads(Path(reg["job"]).read_text(encoding="utf-8"))
            for fps, expect in sorted(reg["expect"].items()):
                key = f"regression_0x02_actions_{fps}hz"
                log(f"[regression] {key}")

                def extra(rdir, expect=expect):
                    got = regression_numbers(rdir)
                    return [f"REGRESSION_NUMBER {k} {got.get(k)} != {v}" for k, v in expect.items() if got.get(k) != v]
                results[key] = run_one(key, job, fps, out_root, state, state_path, regression_runner, inputs,
                                       required=REGRESSION_STEPS, judge_extra=extra,
                                       locomotion_only=False)
                log(f"  -> {results[key]['verdict']} {results[key]['problems']}")
    phase1_keys = [e["id"] for e in entries if e["phase"] == 1] + (
        [f"regression_0x02_actions_{f}hz" for f in scope["regression"]["expect"]] if scope.get("regression") else [])
    not_pass = [k for k in phase1_keys if (results.get(k) or state["entries"].get(k) or {}).get("verdict") != "PASS"]
    if not_pass:
        gate.append("PHASE2_BLOCKED phase 1/regression not all PASS: " + ", ".join(not_pass))
    if 2 in phases and not gate:
        for e in [x for x in entries if x["phase"] == 2]:
            log(f"[phase 2] {e['id']} {e['name_hint']} covers {e['covers']}")
            results[e["id"]] = run_entry(e, by_kind[e["kind"]], out_root, state, state_path, runner, inputs,
                                         scope, baker)
            log(f"  -> {results[e['id']]['verdict']} {results[e['id']]['problems']}")
    summary = summarize(scope_name, entries, coverage, state, cov_problems + gate, phases)
    write_json_atomic(out_root / "summary.json", summary)
    (out_root / "summary.md").write_text(summary_markdown(summary), encoding="utf-8")
    return summary


def summarize(scope_name, entries, coverage, state, problems, phases):
    scope = SCOPES[scope_name]
    rows = []
    for e in entries:
        rec = state["entries"].get(e["id"]) or {}
        rows.append({**{k: e[k] for k in ("id", "kind", "name_hint", "seed", "fps", "phase", "covers", "models")},
                     "verdict": rec.get("verdict", "NOT_RUN"), "problems": rec.get("problems", []),
                     "steps": {k: v["verdict"] for k, v in (rec.get("steps") or {}).items()},
                     "live_check": rec.get("live_check"), "alpha_frames": rec.get("alpha_frames"),
                     "seconds": rec.get("seconds")})
    reg = [{"id": k, "verdict": v["verdict"], "problems": v["problems"]}
           for k, v in sorted(state["entries"].items()) if k.startswith("regression_")]
    counts = collections.Counter(r["verdict"] for r in rows)
    kinds = collections.OrderedDict()
    for r in rows:
        k = kinds.setdefault(r["kind"], {"name_hint": r["name_hint"], "runs": 0, "pass": 0, "models_covered": set()})
        k["runs"] += 1
        if r["verdict"] == "PASS":
            k["pass"] += 1
            k["models_covered"].update(f"{s}={v}" for s, v in r["models"].items())
    for k, v in kinds.items():
        wanted = coverage[k]["wanted"]
        need = sum(n for n in wanted.values() if n > 1)
        got = len([m for m in v["models_covered"] if wanted.get(m.split("=")[0], 0) > 1])
        v["models_covered"] = sorted(v["models_covered"])
        v["variant_models"] = f"{got}/{need}"
    formula_only = sum(1 for r in rows if r["verdict"] == "PASS" and r["live_check"] == "NOT_CAPTURED")
    all_pass = rows and all(r["verdict"] == "PASS" for r in rows) and all(x["verdict"] == "PASS" for x in reg) \
        and not problems
    # a run of a never-captured family is a technical PASS of the formula path only: it must not be
    # reported as a live-verified PASS (D62: provenance stays RECONSTRUCTED_FROM_EVIDENCE)
    result = ("PASS_FORMULA_ONLY" if formula_only else "PASS") if all_pass else (
        "INCOMPLETE" if counts.get("NOT_RUN") else "FAIL")
    return {"format": "cubegame.batch_summary/1", "scope": scope_name, "decision": scope["decision"],
            "BATCH_RESULT": result, "phases_requested": list(phases), "counts": dict(counts), "problems": problems,
            "live_check": {"CAPTURED": len(rows) - formula_only - counts.get("NOT_RUN", 0),
                           "NOT_CAPTURED": formula_only},
            "kinds": kinds, "regression": reg, "entries": rows}


def summary_markdown(s):
    out = [f"# Batch {s['scope']} ({s['decision']}) — {s['BATCH_RESULT']}", "",
           "Generated by `transfer_planner batch`. Technical PASS is not visual approval (producer). "
           "`NOT_CAPTURED` entries are the parametric walk formula only (no live Alpha frames of this "
           "motion family): RECONSTRUCTED_FROM_EVIDENCE, never TRANSFERRED_FROM_ALPHA.", "",
           f"Counts: {s['counts']}; live frames of the walk formula: {s['live_check']}", ""]
    if s["problems"]:
        out += ["Problems:", ""] + [f"* {p}" for p in s["problems"]] + [""]
    out += ["| Kind | Name | Runs | PASS | Variant models covered |", "|---|---|---|---|---|"]
    out += [f"| {k} | {v['name_hint']} | {v['runs']} | {v['pass']} | {v['variant_models']} |" for k, v in s["kinds"].items()]
    out += ["", "| Regression | Verdict | Problems |", "|---|---|---|"]
    out += [f"| {r['id']} | {r['verdict']} | {'; '.join(r['problems'])} |" for r in s["regression"]]
    out += ["", "| Entry | Phase | Hz | Live check | Covers | Verdict | Problems |", "|---|---|---|---|---|---|---|"]
    out += [f"| {r['id']} | {r['phase']} | {r['fps']} | {r['live_check']} | {' '.join(r['covers'])} | "
            f"{r['verdict']} | {'; '.join(r['problems'])[:160]} |" for r in s["entries"]]
    return "\n".join(out) + "\n"


# --------------------------------------------------------------------------- contact sheets
def contact_sheets(out_root, summary, sheet=CONTACT_SHEET):
    """One sheet per kind (rows = PASS runs at 60 Hz of every seed, labelled with seed + models)."""
    out_root = in_scratch(out_root)
    made = []
    for kind, info in summary["kinds"].items():
        runs = [r for r in summary["entries"] if r["kind"] == kind and r["fps"] == 60 and r["verdict"] == "PASS"]
        if not runs:
            continue
        # entity_contact_sheet.ps1 expects <project>/TransferOutput: give it a view folder per run
        views = []
        for r in runs:
            v = out_root / "sheet_views" / r["id"]
            if v.exists():
                shutil.rmtree(v)
            shutil.copytree(out_root / "runs" / r["id"], v / "TransferOutput")
            views.append(str(v))
        labels = [f"seed {r['seed'][0]}/{r['seed'][1]} " + " ".join(f"{s}={m}" for s, m in sorted(r["models"].items())
                                                                    if s in ("head", "hair", "hand"))
                  for r in runs]
        name = f"{kind}_{(info['name_hint'] or 'kind').lower()}.png"
        cmd = ["powershell.exe", "-NoProfile", "-STA", "-ExecutionPolicy", "Bypass", "-File", str(sheet),
               "-Projects", ",".join(views), "-Out", str(out_root / "visuals"), "-Labels", ",".join(labels),
               "-Name", name]
        p = subprocess.run(cmd, cwd=ROOT, capture_output=True, text=True)
        made.append({"kind": kind, "sheet": str(out_root / "visuals" / name), "rows": len(runs), "exit": p.returncode,
                     "out": (p.stdout + p.stderr).strip()[-400:]})
    return made
