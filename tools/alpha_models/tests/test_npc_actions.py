"""S05B-A (D60): creature-AI action triggers, profile gates and provenance rules.

Synthetic tests run everywhere; the trigger tests execute Alpha's own AI bytes and therefore need
the local research copy (.scratch, never published) — they are skipped without it.
"""
import copy
import json
import tempfile
import unittest
from pathlib import Path

from tools.alpha_models.universal import action_baker as ab
from tools.alpha_models.universal import ai_trigger as at
from tools.alpha_models.universal import contract as ct
from tools.alpha_models.universal import creature_rig as cr

CAT_PID = "alpha_actions/creature_ai_cat_0x1e"
HF_PID = "alpha_actions/creature_ai_humanfemale_0x03"
RUN_PID = "alpha_actions/creature_ai_plainrunner_0x3f"
CAT_FAMILY = "alpha_creature/ced63e5e96"
HAVE_EXE = Path(at.DEFAULT_EXE).exists()


class CreatureAiProfileRules(unittest.TestCase):
    def test_cat_profile_is_emulated_creature_ai_without_equipment(self):
        p = ct.ACTION_PROFILES[CAT_PID]
        self.assertEqual(p["status"], "EMULATED_ALPHA_CODE")      # D60 rule 1: never TRANSFERRED
        self.assertEqual(p["trigger"], "creature_ai")
        self.assertEqual(p["equipment"], {})
        self.assertIn(p["live_check"], ("MATCHED", "MISMATCH", "NOT_CAPTURED"))
        self.assertEqual(sorted(p["launched_actions"]), sorted(p["primary_chain"]))

    def test_emulated_profiles_never_claim_transferred(self):
        for pid, p in ct.ACTION_PROFILES.items():
            self.assertNotEqual(p["status"], "TRANSFERRED_FROM_ALPHA", pid)

    def test_profile_does_not_spread_to_other_kinds_of_the_family(self):
        # D60 rule 2: a kind of the same drawModel family WITHOUT its own trigger evidence stays blocked
        saved = copy.deepcopy(ct.ACTION_PROFILES)
        try:
            for pid in [p for p in ct.ACTION_PROFILES if p.endswith(("_0x1f", "_0x20"))]:
                del ct.ACTION_PROFILES[pid]
            for kind in (0x1F, 0x20):
                pid, status, reasons = ct.action_profile_for(kind, (0, 0), CAT_FAMILY,
                                                             ["body", "tail", "head", "hand_l", "hand_r", "foot_l", "foot_r"])
                self.assertIsNone(pid)
                self.assertEqual(status, "BLOCKED")
                self.assertTrue(reasons[0].startswith("POSES_FAMILY_COMPATIBLE_TRIGGER_UNPROVEN"), reasons)
        finally:
            ct.ACTION_PROFILES.clear()
            ct.ACTION_PROFILES.update(saved)

    def test_every_kind_gets_only_its_own_profile(self):
        for pid, p in ct.ACTION_PROFILES.items():
            if p["trigger"] != "creature_ai":
                continue
            kind = p["specimens"][0]["kind"]
            got, _, _ = ct.action_profile_for(kind, (0, 0), p["evidence_families"][0], ["body"])
            self.assertEqual(got, pid)
            self.assertTrue(pid.endswith(f"_0x{kind:02x}"))
            self.assertEqual(len(p["specimens"]), 1)

    def test_specimen_kind_gets_the_profile(self):
        pid, status, _ = ct.action_profile_for(0x1E, (0, 0), CAT_FAMILY,
                                               ["body", "tail", "head", "hand_l", "hand_r", "foot_l", "foot_r"])
        self.assertEqual((pid, status), (CAT_PID, "EMULATED_ALPHA_CODE"))

    def test_player_specimen_keeps_its_input_profile(self):
        parts = ["body", "head", "hair", "hand_l", "hand_r", "foot_l", "foot_r"]
        pid = ct.action_profile_for(0x02, (0, 0), "alpha_creature/e66d96be5a", parts)[0]
        self.assertEqual(ct.ACTION_PROFILES[pid]["trigger"], "player_input")
        self.assertEqual(ct.action_profile_for(0x03, (0, 0), "alpha_creature/e66d96be5a", parts)[0], HF_PID)

    def test_every_creature_ai_profile_names_launch_vas_for_its_chain(self):
        for pid, p in ct.ACTION_PROFILES.items():
            if p["trigger"] != "creature_ai":
                continue
            self.assertEqual(sorted(p["launched_actions"]), sorted(p["primary_chain"]), pid)
            self.assertEqual(p["equipment"], {}, pid)        # no NPC weapon verified
            self.assertTrue(p["seeds_checked"], pid)

    def test_evidence_file_excludes_the_player_specimen(self):
        ev = json.loads(Path(ct.NPC_EVIDENCE_PATH).read_text(encoding="utf-8"))
        self.assertEqual(ev["kinds"]["0x02"]["status"], "EXCLUDED")
        self.assertEqual(sum(e["status"] == "TRIGGER_EMULATED" for e in ev["kinds"].values()), 14)

    def test_other_seed_of_the_specimen_is_not_the_specimen(self):
        pid, _, _ = ct.action_profile_for(0x1E, (1, 0), CAT_FAMILY, ["body"])
        self.assertIsNone(pid)


class BakerGates(unittest.TestCase):
    def _job(self, **kw):
        return {"kind": "0x1e", "seed_variant": [0, 0], **kw}

    def test_unproven_action_is_refused_before_any_binary_is_read(self):
        with tempfile.TemporaryDirectory(dir=str(ab.ROOT / ".scratch")) as d:
            with self.assertRaisesRegex(ValueError, "ACTION_TRIGGER_NOT_PROVEN"):
                ab.bake_job(self._job(), [3], d, exe="missing.exe")

    def test_unverified_equipment_is_a_blocker_not_a_stand_in(self):
        with tempfile.TemporaryDirectory(dir=str(ab.ROOT / ".scratch")) as d:
            with self.assertRaisesRegex(ValueError, "EQUIPMENT_UNVERIFIED"):
                ab.bake_job(self._job(equipment={"weapon": {"subtype": 15}}), [75], d, exe="missing.exe")

    def test_kind_without_profile_is_refused(self):
        with tempfile.TemporaryDirectory(dir=str(ab.ROOT / ".scratch")) as d:
            with self.assertRaisesRegex(ValueError, "ACTION_PROFILE_REQUIRED"):
                ab.bake_job({"kind": "0x21", "seed_variant": [0, 0]}, [75], d, exe="missing.exe")


@unittest.skipUnless(HAVE_EXE, "local Alpha research copy (.scratch) not present")
class TriggerFromAlphaBytes(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        from tools.alpha_models.anim import pe_bytes
        cls.image = pe_bytes.load(str(at.DEFAULT_EXE))

    def _launched(self, kind):
        rep = at.analyse_kind(self.image, kind)
        self.assertEqual(rep["errors"], {}, rep["errors"])
        return {int(a): v["write_vas"] for a, v in rep["actions"].items()}, rep

    def test_cat_launches_only_action_75_from_the_flag_branch(self):
        acts, rep = self._launched(0x1E)
        self.assertEqual(rep["appearance_flags_7e"], "0x0031")
        self.assertEqual(acts, {75: ["0x42E2BF"]})
        self.assertEqual(ct.ACTION_PROFILES[CAT_PID]["launched_actions"], {75: 0x42E2BF})

    def test_runner_and_humanoid_differ_from_the_cat(self):
        runner, _ = self._launched(0x3F)
        self.assertEqual(sorted(runner), [6])
        self.assertEqual({a: int(v[0], 16) for a, v in runner.items()},
                         ct.ACTION_PROFILES[RUN_PID]["launched_actions"])
        human, _ = self._launched(0x03)
        self.assertEqual(sorted(human), [3, 4, 6, 54])
        self.assertEqual({a: int(v[0], 16) for a, v in human.items()},
                         ct.ACTION_PROFILES[HF_PID]["launched_actions"])

    def test_ability_is_executed_and_cooldown_is_the_condition_of_3_and_4(self):
        rep = at.analyse_kind(self.image, 0x03)
        for a in ("3", "4"):
            self.assertTrue(all(e["cooldowns"] == "cooldown_0x36" for e in rep["actions"][a]["examples"]))
            self.assertEqual(rep["actions"][a]["examples"][0]["ability_usable"], "executed")
        self.assertTrue(all(e["cooldowns"] == "no_cooldown" for e in rep["actions"]["54"]["examples"]))


if __name__ == "__main__":
    unittest.main()
