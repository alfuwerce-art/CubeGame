"""Batch executor, S05B-A part B (D60) action scope, without Unity or Alpha.

Synthetic kinds and profiles only (TEST_DATA_NOT_ALPHA); the Unity runner and the baker are stubs.
"""
from __future__ import annotations

import copy
import json
import shutil
import unittest
from pathlib import Path

from tools.alpha_models.universal import batch_runner as br
from tools.alpha_models.universal import contract as ct

ROOT = Path(__file__).resolve().parents[3]
TMP = ROOT / ".scratch" / "tmp" / "test_batch_actions"

SCOPE = {"decision": "DTEST", "mode": "actions", "kinds": (0x91, 0x92, 0x95), "phase1_kinds": (0x91, 0x95),
         "coverage": "BASE", "fps_samples": (30,),
         "regression": {"job": None, "project": None,
                        "expect": {60: {"actions": 3, "frames": 100, "native_frames": 10}}}}
PROFILES = {0x91: ("alpha_actions/creature_ai_test_0x91", {75: 0x1}),
            0x92: ("alpha_actions/creature_ai_test_0x92", {75: 0x1}),
            0x95: ("alpha_actions/creature_ai_test_0x95", {3: 0x2, 6: 0x3})}


def row(kind, family, profile=True):
    job = {"kind": f"0x{kind:02x}", "seed_variant": [0, 0], "variant": f"auto_0x{kind:02x}", "locomotion": "formula",
           "reference_capture": {"path": "cap.jsonl", "kind": "0x91", "sha256": "ab" * 32}}
    # plan rows carry the profile id only (as transfer_planner.plan writes them)
    acts = ({"status": "AVAILABLE", "profile": PROFILES[kind][0]} if profile
            else {"status": "BLOCKED", "profile": None})
    return {"kind": f"0x{kind:02x}", "name_hint": f"Synthetic{kind:02x}", "family_id": family,
            "readiness": "AUTO_READY", "job": job,
            "channels": {"walk": {"sources": ["RECONSTRUCTED_FROM_EVIDENCE", "TRANSFERRED_FROM_ALPHA"]},
                         "actions": acts}}


def plan(**no_profile):
    return {"entities": [row(0x91, "fam/a", not no_profile.get("x91")), row(0x92, "fam/a"), row(0x95, "fam/b")]}


class ActionStub:
    """Writes TransferOutput like run_transfer.ps1 for an ACTIONS entity."""

    def __init__(self, project, mode="ACTIONS", completed=None, regression=False):
        self.project, self.mode, self.completed, self.regression, self.calls = Path(project), mode, completed, regression, []

    def __call__(self, job_path, fps, log_path):
        key = Path(job_path).stem
        self.calls.append(key)
        job = json.loads(Path(job_path).read_text(encoding="utf-8"))
        out = self.project / "TransferOutput"
        if out.exists():
            shutil.rmtree(out)
        out.mkdir(parents=True)
        if self.regression:
            steps = br.REGRESSION_STEPS
            (out / "controller_playmode.txt").write_text(
                f"frames=100 simulated_fps={fps} actions_completed=3 required=3\n", encoding="utf-8")
            (out / "native_lifecycle_playmode.txt").write_text("frames=10\n", encoding="utf-8")
            (out / "entity_playmode.txt").write_text("controller_mode=ACTIONS\n", encoding="utf-8")
        else:
            steps = br.ACTION_STEPS
            kind = int(job["kind"], 16)
            pid, launched = PROFILES[kind]
            n = len(launched) if self.completed is None else self.completed
            (out / "entity_playmode.txt").write_text(
                f"ENTITY_PLAYMODE_RESULT=PASS\ncontroller_mode={self.mode} action_profile={pid} trigger=creature_ai\n",
                encoding="utf-8")
            (out / "controller_playmode.txt").write_text(
                f"frames=300 simulated_fps={fps} actions_completed={n} required={len(launched)}\n", encoding="utf-8")
        (out / "paramwalk.txt").write_text("PARAMWALK_RESULT=PASS alpha_frames=484\n", encoding="utf-8")
        lines = [f"{s}=PASS exit=0" for s in steps] + ["RESULT=PASS"]
        Path(log_path).write_text("\n".join(lines), encoding="utf-8")
        return 0, "\n".join(lines), out


def stub_baker(fail=(), calls=None):
    def bake(job, actions, out):
        kind = int(job["kind"], 16)
        if calls is not None:
            calls.append(kind)
        if kind in fail:
            raise ValueError("ACTION_TRIGGER_NOT_PROVEN (stub)")
        Path(out).mkdir(parents=True, exist_ok=True)
        entries = []
        for a in actions:
            f = Path(out, f"action_{a:03d}.alphaclip.json")
            f.write_text(json.dumps({"name": f"action_{a:03d}", "stub": True}), encoding="utf-8")
            entries.append({"action": a, "status": "EMULATED_ALPHA_CODE", "file": f.name, "sha256": br.sha256_file(f)})
        Path(out, "actions.json").write_text(json.dumps({
            "BAKE_RESULT": "PASS", "profile": PROFILES[kind][0], "actions": entries}), encoding="utf-8")
    return bake


class ActionScope(unittest.TestCase):
    def setUp(self):
        if TMP.exists():
            shutil.rmtree(TMP)
        TMP.mkdir(parents=True)
        self.saved_scopes, self.saved_profiles = copy.deepcopy(br.SCOPES), copy.deepcopy(ct.ACTION_PROFILES)
        reg_job = TMP / "regression_job.json"
        reg_job.write_text(json.dumps({"kind": "0x02", "variant": "reg"}), encoding="utf-8")
        br.SCOPES["test"] = dict(SCOPE, regression=dict(SCOPE["regression"], job=reg_job))
        for kind, (pid, launched) in PROFILES.items():
            ct.ACTION_PROFILES[pid] = {"trigger": "creature_ai", "launched_actions": launched,
                                       "specimens": [{"kind": kind, "seed_variant": [0, 0]}]}

    def tearDown(self):
        br.SCOPES.clear(); br.SCOPES.update(self.saved_scopes)
        ct.ACTION_PROFILES.clear(); ct.ACTION_PROFILES.update(self.saved_profiles)

    def run_batch(self, runner=None, baker=None, evidence=lambda kinds: [], p=None, phases=(1, 2)):
        return br.execute("test", p or plan(), {"kinds": {}}, None, runner or ActionStub(TMP / "proj"),
                          TMP / "out", {"x": 1}, ActionStub(TMP / "reg", regression=True), phases,
                          log=lambda *_: None, evidence_check=evidence, baker=baker or stub_baker())

    def test_real_scope_is_pinned_to_d60_and_excludes_the_player_specimen(self):
        sc = self.saved_scopes["s05ba"]
        self.assertEqual(sc["decision"], "D60")
        self.assertNotIn(0x02, sc["kinds"])
        self.assertEqual(len(sc["kinds"]), 14)
        self.assertTrue(set(sc["phase1_kinds"]) <= set(sc["kinds"]))

    def test_without_evidence_check_the_batch_refuses(self):
        s = self.run_batch(evidence=None)
        self.assertEqual(s["BATCH_RESULT"], "REFUSED")
        self.assertIn("EVIDENCE_CHECK_NOT_RUN", s["problems"])

    def test_evidence_drift_refuses_before_any_run(self):
        runner = ActionStub(TMP / "proj")
        s = self.run_batch(runner=runner, evidence=lambda kinds: ["EVIDENCE_DRIFT 0x92"])
        self.assertEqual(s["BATCH_RESULT"], "REFUSED")
        self.assertEqual(runner.calls, [])

    def test_evidence_check_reads_both_evidence_sources(self):
        """D60 kinds are re-executed per kind; D64 kinds are covered by their committed record and by the
        pinned code hashes of the D64 evidence (D65: the D63 actions scope uses the D64 source)."""
        ev64 = ROOT / "tools/alpha_models/catalog/npc_trigger_evidence.json"
        ev60 = ROOT / "tools/alpha_models/universal/npc_action_evidence.json"
        if not ev64.exists():
            self.skipTest("D64 evidence not present")
        kinds64 = {int(k, 16) for k in json.loads(ev64.read_text(encoding="utf-8"))["kinds"]}
        kinds60 = {int(k, 16) for k in json.loads(ev60.read_text(encoding="utf-8"))["kinds"]}
        d64_only = sorted(kinds64 - kinds60)
        self.assertTrue(d64_only, "the D64 evidence must cover kinds the D60 records do not")
        self.assertEqual(br.evidence_drift(None, d64_only[:3] + [0x76]), [])
        self.assertEqual(br.evidence_drift(None, [0xFF]), ["EVIDENCE_MISSING 0xff"])

    def test_kind_without_creature_ai_profile_is_scope_drift(self):
        s = self.run_batch(p=plan(x91=True))
        self.assertEqual(s["BATCH_RESULT"], "REFUSED")
        self.assertTrue(s["problems"][0].startswith("SCOPE_DRIFT"))

    def test_happy_path_imports_the_kind_library_and_passes(self):
        runner = ActionStub(TMP / "proj")
        s = self.run_batch(runner=runner)
        self.assertEqual(s["BATCH_RESULT"], "PASS", s["problems"])
        self.assertEqual(s["counts"], {"PASS": 5})            # 0x91 @60/30, 0x95 @60/30, 0x92 @60 (phase 2)
        job = json.loads((TMP / "out" / "jobs" / "0x92_s0-0_60hz.json").read_text(encoding="utf-8"))
        self.assertTrue(job["action_manifest"].endswith("libraries/0x92/actions.json"))
        self.assertTrue(job["include_family_clips"])
        self.assertEqual(len(job["library_sha256"]), 64)
        self.assertEqual([e["phase"] for e in s["entries"] if e["kind"] == "0x92"], [2])

    def test_locomotion_only_entity_fails_and_blocks_phase_2(self):
        runner = ActionStub(TMP / "proj", mode="LOCOMOTION_ONLY")
        s = self.run_batch(runner=runner)
        self.assertEqual(s["BATCH_RESULT"], "INCOMPLETE")
        self.assertTrue(any(p.startswith("PHASE2_BLOCKED") for p in s["problems"]))
        bad = [e for e in s["entries"] if e["verdict"] == "FAIL"]
        self.assertTrue(all("ENTITY_NOT_ACTIONS" in e["problems"] for e in bad))
        self.assertNotIn("0x92_s0-0_60hz", runner.calls)

    def test_missing_completed_action_fails(self):
        s = self.run_batch(runner=ActionStub(TMP / "proj", completed=0), phases=(1,))
        self.assertTrue(any("ACTIONS_COMPLETED 0/" in p for e in s["entries"] for p in e["problems"]))

    def test_blocked_action_is_documented_and_not_required(self):
        """D65: a launch that the baker refuses (its pose needs a dynamic part, R-8) is BLOCKED in the
        library with a reason; the controller must play the EMULATED ones only, and the batch must not
        demand the profile size."""
        pid = "alpha_actions/creature_ai_test_0x95"
        lib = TMP / "lib" / "actions.json"
        lib.parent.mkdir(parents=True, exist_ok=True)
        lib.write_text(json.dumps({"actions": [
            {"clip": "action_003", "status": "EMULATED_ALPHA_CODE"},
            {"clip": "action_006", "status": "EMULATED_ALPHA_CODE"},
            {"clip": "action_072", "status": "BLOCKED", "reason": "ACTION_PART_SET_CHANGED t=208"},
        ]}), encoding="utf-8")
        rdir = TMP / "rdir"
        rdir.mkdir()
        (rdir / "controller_playmode.txt").write_text(
            "CONTROLLER_PLAYMODE_RESULT=PASS\nactions_completed=2 required=2\n", encoding="utf-8")
        (rdir / "entity_playmode.txt").write_text(
            "controller_mode=ACTIONS\naction_profile=" + pid + " trigger=creature_ai\n", encoding="utf-8")
        judge = br.action_judge({"profile": pid, "id": "0x95_s0-0_60hz"}, lib)
        self.assertEqual(judge(rdir), [])
        lib.write_text(json.dumps({"actions": [
            {"clip": "action_003", "status": "EMULATED_ALPHA_CODE"},
            {"clip": "action_072", "status": "BLOCKED"},
        ]}), encoding="utf-8")
        (rdir / "controller_playmode.txt").write_text(
            "CONTROLLER_PLAYMODE_RESULT=PASS\nactions_completed=1 required=1\n", encoding="utf-8")
        self.assertIn("BLOCKED_WITHOUT_REASON action_072",
                      br.action_judge({"profile": pid, "id": "0x95_s0-0_60hz"}, lib)(rdir))

    def test_refused_bake_is_a_fail_not_a_skip(self):
        s = self.run_batch(baker=stub_baker(fail={0x95}), phases=(1,))
        rows = {e["id"]: e for e in s["entries"]}
        self.assertEqual(rows["0x95_s0-0_60hz"]["verdict"], "FAIL")
        self.assertTrue(rows["0x95_s0-0_60hz"]["problems"][0].startswith("BAKE_FAILED"))


    # ---- REVIEW-FIX-01: SHA256 cache of baked libraries
    def entry(self, kind=0x92):
        return {"kind": f"0x{kind:02x}", "seed": [0, 0], "profile": PROFILES[kind][0]}

    def test_library_is_reused_only_with_the_same_key_and_intact_payloads(self):
        calls = []
        out = TMP / "out"
        br.bake_library(self.entry(), out, stub_baker(calls=calls))
        e = self.entry()
        br.bake_library(e, out, stub_baker(calls=calls))
        self.assertEqual(calls, [0x92])
        self.assertEqual(e["library_cache"], "REUSED")
        # a changed payload (clip file) invalidates the cache
        clip = out / "libraries" / "0x92" / "action_075.alphaclip.json"
        clip.write_text('{"tampered": true}', encoding="utf-8")
        e = self.entry()
        br.bake_library(e, out, stub_baker(calls=calls))
        self.assertTrue(e["library_cache"].startswith("PAYLOAD_SHA256"))
        self.assertEqual(calls, [0x92, 0x92])

    def test_changed_profile_or_code_invalidates_the_cache(self):
        calls = []
        out = TMP / "out"
        br.bake_library(self.entry(), out, stub_baker(calls=calls))
        ct.ACTION_PROFILES[PROFILES[0x92][0]]["note"] = "changed evidence record"
        e = self.entry()
        br.bake_library(e, out, stub_baker(calls=calls))
        self.assertEqual(e["library_cache"], "CACHE_KEY_CHANGED")
        key = br.bake_cache_key(self.entry())
        self.assertEqual(set(key["code_sha256"]), set(br.BAKE_CODE))
        self.assertEqual(len(key["exe_sha256"]), 64)
        self.assertEqual(len(key["evidence_sha256"]), 64)

    def test_library_without_recorded_payload_hashes_is_rebaked(self):
        out = TMP / "out"
        br.bake_library(self.entry(), out, stub_baker())
        man = out / "libraries" / "0x92" / "actions.json"
        m = json.loads(man.read_text(encoding="utf-8"))
        for a in m["actions"]:
            a.pop("sha256")
        man.write_text(json.dumps(m), encoding="utf-8")
        e = self.entry()
        br.bake_library(e, out, stub_baker())
        self.assertTrue(e["library_cache"].startswith("PAYLOAD_UNRECORDED"))


if __name__ == "__main__":
    unittest.main()
