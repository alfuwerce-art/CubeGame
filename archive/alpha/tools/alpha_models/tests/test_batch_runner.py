"""Batch executor of S05B-1 (D59) without Unity: scope selection, variant coverage, resume, hashes, gate.

Synthetic data only (TEST_DATA_NOT_ALPHA): invented kinds, seeds and model indices; the Unity runner is a
stub that writes report files the same way run_transfer.ps1 does.
"""
from __future__ import annotations

import copy
import json
import shutil
import unittest
from pathlib import Path

from tools.alpha_models.universal import batch_runner as br

ROOT = Path(__file__).resolve().parents[3]
TMP = ROOT / ".scratch" / "tmp" / "test_batch_runner"

SCOPE = {"decision": "DTEST", "kinds": (0x91, 0x92, 0x95), "coverage": "B", "fps_samples": (30,),
         "regression": {"job": None, "project": None,
                        "expect": {60: {"actions": 3, "frames": 100, "native_frames": 10}}}}


def row(kind, family, capture=True, readiness="AUTO_READY", walk=("RECONSTRUCTED_FROM_EVIDENCE", "TRANSFERRED_FROM_ALPHA")):
    job = {"kind": f"0x{kind:02x}", "seed_variant": [0, 0], "variant": f"auto_0x{kind:02x}", "locomotion": "formula",
           "equipment": {"weapon": {"subtype": 15}}, "action_manifest": "x/actions.json", "clips": "x",
           "actions_status": "profile"}
    if capture:
        job["reference_capture"] = {"path": "cap.jsonl", "kind": "0x91", "sha256": "ab" * 32}
    return {"kind": f"0x{kind:02x}", "name_hint": f"Synthetic{kind:02x}", "family_id": family,
            "readiness": readiness, "job": job, "channels": {"walk": {"sources": list(walk)}}}


PLAN = {"entities": [row(0x91, "fam/a"), row(0x92, "fam/a"), row(0x95, "fam/b"),
                     row(0x96, "fam/c", capture=False, walk=("RECONSTRUCTED_FROM_EVIDENCE",)),
                     row(0x97, "fam/d", readiness="BLOCKED")]}
STATIC = {"kinds": {"0x91": {"slot_variants": {"head": [10, 11], "hair": [20, 21, 22], "body": [1]}},
                    "0x92": {"slot_variants": {"body": [2]}},
                    "0x95": {"slot_variants": {"hand": [30, 31]}}}}


def stub_slots(kind, s2, s3):
    """head follows seed[2] % 2, hair seed[3] % 3, hand (s2 + s3) % 2 — invented."""
    if kind == 0x91:
        return {"head": 10 + s2 % 2, "hair": 20 + s3 % 3, "body": 1}
    if kind == 0x92:
        return {"body": 2}
    return {"hand": 30 + (s2 + s3) % 2}


class StubRunner:
    """Writes TransferOutput like run_transfer.ps1; ``fail`` = entry keys (job variant suffix) that FAIL.

    ``alpha_frames`` > 0 emulates a live capture of the motion family; 0 emulates a never-captured family
    (the paramwalk report then carries the formula-only note, as AlphaParamWalkCheck.cs writes it)."""

    def __init__(self, project, fail=(), regression=False, numbers=None, alpha_frames=484):
        self.project, self.fail, self.calls = Path(project), set(fail), []
        self.regression, self.numbers = regression, numbers or {"actions": 3, "frames": 100, "native_frames": 10}
        self.alpha_frames = alpha_frames

    def __call__(self, job_path, fps, log_path):
        key = Path(job_path).stem
        self.calls.append(key)
        out = self.project / "TransferOutput"
        if out.exists():
            shutil.rmtree(out)
        out.mkdir(parents=True)
        bad = key in self.fail
        steps = br.REGRESSION_STEPS if self.regression else br.REQUIRED_STEPS
        lines = [f"{s}={'FAIL' if bad and s == 'PARAMWALK_RESULT' else 'PASS'} exit={1 if bad and s == 'PARAMWALK_RESULT' else 0}"
                 for s in steps] + [f"RESULT={'FAIL' if bad else 'PASS'}"]
        note = "" if self.alpha_frames else "\nALPHA not_available (no capture of this motion family in the job: formula-only kind)"
        (out / "paramwalk.txt").write_text(
            f"PARAMWALK_RESULT=PASS golden=900 alpha_frames={self.alpha_frames}{note}\n", encoding="utf-8")
        (out / "entity_playmode.txt").write_text(
            "ENTITY_PLAYMODE_RESULT=PASS\ncontroller_mode=LOCOMOTION_ONLY\naction=- locomotion_only_refused=True\n",
            encoding="utf-8")
        if self.regression:
            n = self.numbers
            (out / "controller_playmode.txt").write_text(
                f"CONTROLLER_PLAYMODE_RESULT=PASS\nframes={n['frames']} simulated_fps={fps} "
                f"actions_completed={n['actions']} required={n['actions']}\n", encoding="utf-8")
            (out / "native_lifecycle_playmode.txt").write_text(
                f"NATIVE_LIFECYCLE_PLAYMODE_RESULT=PASS\nfps={fps} frames={n['native_frames']} stage=9\n", encoding="utf-8")
        Path(log_path).write_text("\n".join(lines), encoding="utf-8")
        return (1 if bad else 0), "\n".join(lines), out


class BatchRunnerTests(unittest.TestCase):
    def setUp(self):
        if TMP.exists():
            shutil.rmtree(TMP)
        TMP.mkdir(parents=True)
        self.scope_name = "test_scope"
        sc = copy.deepcopy(SCOPE)
        sc["regression"]["job"] = TMP / "reg_job.json"
        (TMP / "reg_job.json").write_text(json.dumps({"kind": "0x91", "variant": "reg"}), encoding="utf-8")
        br.SCOPES[self.scope_name] = sc
        self.inputs = {"static_sha256": "s1", "captures": ["ab" * 32]}

    def tearDown(self):
        br.SCOPES.pop(self.scope_name, None)

    def run_batch(self, runner, reg, phases=(1, 2), plan=PLAN, inputs=None):
        return br.execute(self.scope_name, plan, STATIC, stub_slots, runner, TMP / "out", inputs or self.inputs,
                          reg, phases, log=lambda *_: None)

    # ---------------------------------------------------------------- selection
    def test_scope_selector_matches_pinned_kinds(self):
        rows, problems = br.select_scope(PLAN, br.SCOPES[self.scope_name])
        self.assertEqual(problems, [])
        self.assertEqual([r["kind"] for r in rows], ["0x91", "0x92", "0x95"])

    def test_scope_drift_refuses(self):
        plan = copy.deepcopy(PLAN)
        plan["entities"][3] = row(0x96, "fam/c")          # a fourth kind gains a live capture
        r = self.run_batch(StubRunner(TMP / "p"), None, plan=plan)
        self.assertEqual(r["BATCH_RESULT"], "REFUSED")
        self.assertIn("SCOPE_DRIFT", r["problems"][0])

    def test_kind_outside_scope_never_runs(self):
        runner = StubRunner(TMP / "p")
        self.run_batch(runner, StubRunner(TMP / "r", regression=True))
        self.assertFalse(any(c.startswith(("0x96", "0x97")) for c in runner.calls))

    # ---------------------------------------------------------------- coverage
    def test_variant_seeds_cover_every_model_minimally(self):
        seeds, problems = br.variant_seeds(0x91, stub_slots, STATIC["kinds"]["0x91"]["slot_variants"])
        self.assertEqual(problems, [])
        self.assertEqual(seeds[0]["seed"], [0, 0])
        self.assertEqual(len(seeds), 3)                        # max(2 heads, 3 hairs)
        got = {(s, v) for sd in seeds for s, v in sd["models"].items()}
        self.assertTrue({("head", 10), ("head", 11), ("hair", 20), ("hair", 21), ("hair", 22)} <= got)

    def test_variant_seeds_reports_uncoverable_model(self):
        _, problems = br.variant_seeds(0x91, stub_slots, {"head": [10, 11, 99]})
        self.assertEqual(problems, ["VARIANT_NOT_COVERED 0x91 head=99"])

    def test_entries_phase1_base_plus_fps_sample_phase2_variants(self):
        rows, _ = br.select_scope(PLAN, br.SCOPES[self.scope_name])
        entries, cov, problems = br.build_entries(rows, br.SCOPES[self.scope_name], stub_slots, STATIC)
        self.assertEqual(problems, [])
        p1 = [e["id"] for e in entries if e["phase"] == 1]
        self.assertEqual(p1, ["0x91_s0-0_60hz", "0x91_s0-0_30hz", "0x92_s0-0_60hz", "0x95_s0-0_60hz", "0x95_s0-0_30hz"])
        self.assertEqual(sum(e["phase"] == 2 for e in entries), 2 + 1)    # 0x91: 2 more seeds, 0x95: 1 more

    def test_job_has_no_action_library_and_keeps_capture(self):
        e = {"id": "0x91_s1-1_60hz", "seed": [1, 1]}
        job = br.job_for_entry(PLAN["entities"][0], e)
        for k in ("equipment", "action_manifest", "clips", "actions_status"):
            self.assertNotIn(k, job)
        self.assertEqual(job["seed_variant"], [1, 1])
        self.assertIn("reference_capture", job)

    # ---------------------------------------------------------------- execution / gate / resume
    def test_full_pass_and_summary(self):
        r = self.run_batch(StubRunner(TMP / "p"), StubRunner(TMP / "r", regression=True))
        self.assertEqual(r["BATCH_RESULT"], "PASS", r["problems"])
        self.assertEqual(r["kinds"]["0x91"]["variant_models"], "5/5")
        self.assertTrue((TMP / "out" / "summary.md").exists())
        self.assertTrue((TMP / "out" / "runs" / "0x91_s0-0_60hz" / "paramwalk.txt").exists())

    def test_phase1_failure_blocks_phase2(self):
        runner = StubRunner(TMP / "p", fail={"0x92_s0-0_60hz"})
        r = self.run_batch(runner, StubRunner(TMP / "r", regression=True))
        self.assertEqual(r["BATCH_RESULT"], "INCOMPLETE")
        self.assertTrue(any(p.startswith("PHASE2_BLOCKED") for p in r["problems"]))
        self.assertFalse(any(e["phase"] == 2 and e["verdict"] != "NOT_RUN" for e in r["entries"]))
        # failure of one kind does not stop the other phase-1 kinds
        self.assertIn("0x95_s0-0_60hz", runner.calls)

    def test_regression_numbers_must_match(self):
        bad = StubRunner(TMP / "r", regression=True, numbers={"actions": 3, "frames": 101, "native_frames": 10})
        r = self.run_batch(StubRunner(TMP / "p"), bad)
        self.assertEqual(r["regression"][0]["verdict"], "FAIL")
        self.assertIn("REGRESSION_NUMBER frames 101 != 100", r["regression"][0]["problems"])
        self.assertTrue(any(p.startswith("PHASE2_BLOCKED") for p in r["problems"]))

    def test_resume_skips_only_unchanged_pass(self):
        first = StubRunner(TMP / "p", fail={"0x95_s0-0_60hz"})
        self.run_batch(first, StubRunner(TMP / "r", regression=True), phases=(1,))
        again = StubRunner(TMP / "p")
        self.run_batch(again, StubRunner(TMP / "r", regression=True), phases=(1,))
        self.assertEqual(again.calls, ["0x95_s0-0_60hz"])             # only the FAIL is re-run
        # a tampered report invalidates the PASS
        (TMP / "out" / "runs" / "0x91_s0-0_60hz" / "paramwalk.txt").write_text("PARAMWALK_RESULT=PASS alpha_frames=1",
                                                                             encoding="utf-8")
        third = StubRunner(TMP / "p")
        self.run_batch(third, StubRunner(TMP / "r", regression=True), phases=(1,))
        self.assertEqual(third.calls, ["0x91_s0-0_60hz"])
        # changed inputs (another capture hash) re-run everything
        fourth = StubRunner(TMP / "p")
        self.run_batch(fourth, StubRunner(TMP / "r", regression=True), phases=(1,),
                       inputs={"static_sha256": "s1", "captures": ["cd" * 32]})
        self.assertEqual(len(fourth.calls), 5)

    def test_judge_requires_live_frames_and_locomotion_only(self):
        d = TMP / "judge"
        d.mkdir()
        steps = {s: {"verdict": "PASS", "exit": 0} for s in br.REQUIRED_STEPS}
        steps["RESULT"] = {"verdict": "PASS", "exit": None}
        (d / "paramwalk.txt").write_text("PARAMWALK_RESULT=PASS alpha_frames=0", encoding="utf-8")
        (d / "entity_playmode.txt").write_text("controller_mode=ACTIONS", encoding="utf-8")
        p = br.judge_entry(steps, 0, d)
        self.assertIn("PARAMWALK_WITHOUT_LIVE_FRAMES", p)
        self.assertIn("ENTITY_NOT_LOCOMOTION_ONLY", p)
        steps["ENTITY_PLAYMODE_RESULT"] = {"verdict": "PASS", "exit": 1}
        self.assertTrue(any(x.startswith("STEP_FAIL ENTITY_PLAYMODE_RESULT") for x in br.judge_entry(steps, 0, d)))

    def test_outputs_must_stay_in_scratch(self):
        with self.assertRaisesRegex(ValueError, "BATCH_OUTPUT_MUST_BE_IN_SCRATCH"):
            br.in_scratch(ROOT / "docs")


def famrow(kind, family, readiness="AUTO_READY", formula="FORMULA_VERIFIED"):
    """Planner row of a never-captured motion family (no reference_capture, formula walk)."""
    return {"kind": f"0x{kind:02x}", "name_hint": f"New{kind:02x}", "family_id": family,
            "readiness": readiness, "reasons": [],
            "channels": {"walk": {"status": "AVAILABLE", "sources": ["RECONSTRUCTED_FROM_EVIDENCE"],
                                  "formula": formula, "reasons": []},
                         "actions": {"status": "BLOCKED", "profile": None, "reasons": ["NO_VERIFIED_ACTION_TRIGGER"]}},
            "job": {"kind": f"0x{kind:02x}", "seed_variant": [0, 0], "locomotion": "formula"}}


FAM_PLAN = {"entities": [famrow(0xa0, "fam/new"), famrow(0xa1, "fam/new"),
                         famrow(0xa2, "fam/other"), famrow(0xa4, "fam/solo")]}
FAM_STATIC = {"kinds": {f"0x{k:02x}": {"slot_variants": {"body": [k]}} for k in (0xa0, 0xa1, 0xa4)}}


class FormulaOnlyScopeTests(unittest.TestCase):
    """D62/S05B-2: a never-captured motion family is formula-only — classified, never a live-verified PASS."""

    def setUp(self):
        if TMP.exists():
            shutil.rmtree(TMP)
        TMP.mkdir(parents=True)
        self.live_name, self.formula_name = "test_live", "test_formula"
        base = {"decision": "DTEST", "coverage": "BASE", "fps_samples": (30,),
                "regression": {"job": TMP / "reg_job.json", "project": None,
                               "expect": {60: {"actions": 3, "frames": 100, "native_frames": 10}}}}
        (TMP / "reg_job.json").write_text(json.dumps({"kind": "0xa0", "variant": "reg"}), encoding="utf-8")
        br.SCOPES[self.live_name] = dict(base, kinds=(0xa4,), families=("fam/solo",), live_frames="required")
        br.SCOPES[self.formula_name] = dict(base, kinds=(0xa0, 0xa1), families=("fam/new",),
                                            live_frames="formula_only")
        self.inputs = {"static_sha256": "s1", "captures": []}

    def tearDown(self):
        br.SCOPES.pop(self.live_name, None)
        br.SCOPES.pop(self.formula_name, None)

    def run_batch(self, name, runner, reg):
        return br.execute(name, FAM_PLAN, FAM_STATIC, stub_slots, runner, TMP / "out", self.inputs,
                          reg, (1, 2), log=lambda *_: None)

    def test_family_selector_takes_every_ready_kind_of_the_pinned_family(self):
        rows, problems = br.select_scope(FAM_PLAN, br.SCOPES[self.formula_name])
        self.assertEqual(problems, [])
        self.assertEqual([r["kind"] for r in rows], ["0xa0", "0xa1"])   # both kinds of fam/new only

    def test_family_selector_drift_when_a_kind_stops_being_ready(self):
        plan = copy.deepcopy(FAM_PLAN)
        plan["entities"][1] = famrow(0xa1, "fam/new", readiness="NEEDS_RESEARCH")
        rows, problems = br.select_scope(plan, br.SCOPES[self.formula_name])
        self.assertEqual(len(problems), 1)
        self.assertIn("SCOPE_DRIFT", problems[0])
        self.assertIn("0xa1=NEEDS_RESEARCH", problems[0])

    def test_family_selector_drift_on_unverified_formula(self):
        plan = copy.deepcopy(FAM_PLAN)
        plan["entities"][0]["channels"]["walk"]["formula"] = "NEEDS_RESEARCH"
        _, problems = br.select_scope(plan, br.SCOPES[self.formula_name])
        self.assertIn("SCOPE_DRIFT", problems[0])

    def test_live_scope_still_requires_live_alpha_frames(self):
        r = self.run_batch(self.live_name, StubRunner(TMP / "p", alpha_frames=0),
                           StubRunner(TMP / "r", regression=True))
        self.assertEqual(r["entries"][0]["verdict"], "FAIL")
        self.assertIn("PARAMWALK_WITHOUT_LIVE_FRAMES", r["entries"][0]["problems"])
        self.assertNotEqual(r["BATCH_RESULT"], "PASS")

    def test_formula_scope_passes_but_is_reported_formula_only(self):
        r = self.run_batch(self.formula_name, StubRunner(TMP / "p", alpha_frames=0),
                           StubRunner(TMP / "r", regression=True))
        self.assertEqual(r["BATCH_RESULT"], "PASS_FORMULA_ONLY", r["problems"])
        self.assertEqual([e["live_check"] for e in r["entries"]], ["NOT_CAPTURED"] * len(r["entries"]))
        self.assertEqual(r["live_check"], {"CAPTURED": 0, "NOT_CAPTURED": len(r["entries"])})
        md = (TMP / "out" / "summary.md").read_text(encoding="utf-8")
        self.assertIn("PASS_FORMULA_ONLY", md)
        self.assertIn("NOT_CAPTURED", md)

    def test_formula_scope_does_not_accept_an_unexplained_zero_frame_check(self):
        d = TMP / "note"
        d.mkdir()
        (d / "paramwalk.txt").write_text("PARAMWALK_RESULT=PASS golden=900 alpha_frames=0\n", encoding="utf-8")
        (d / "entity_playmode.txt").write_text(
            "ENTITY_PLAYMODE_RESULT=PASS\ncontroller_mode=LOCOMOTION_ONLY\nlocomotion_only_refused=True\n",
            encoding="utf-8")
        steps = {s: {"verdict": "PASS", "exit": 0} for s in br.REQUIRED_STEPS}
        steps["RESULT"] = {"verdict": "PASS", "exit": None}
        p = br.judge_entry(steps, 0, d, live_required=False)
        self.assertIn("PARAMWALK_NO_LIVE_AND_NO_REASON", p)
        (d / "paramwalk.txt").write_text(
            "PARAMWALK_RESULT=PASS golden=900 alpha_frames=0\nALPHA not_available (no capture)\n", encoding="utf-8")
        self.assertEqual(br.judge_entry(steps, 0, d, live_required=False), [])


if __name__ == "__main__":
    unittest.main()
