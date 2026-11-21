"""S05B-A REVIEW-FIX-01 (D61): typed equipment_observed and full evidence drift (no Alpha needed:
kind_evidence is stubbed, TEST_DATA_NOT_ALPHA)."""
from __future__ import annotations

import json
import tempfile
import unittest
from pathlib import Path
from unittest import mock

from tools.alpha_models.universal import ai_trigger as at
from tools.alpha_models.universal import contract as ct


def record(**over):
    r = {"kind": "0x91", "status": "TRIGGER_EMULATED", "family_id": "fam/a", "appearance_flags_7e": "0x0000",
         "seeds_checked": [[0, 0], [1, 1]], "launched_actions": {"75": "0x42E2BF"},
         "condition_examples": {"75": [{"cooldowns": "no_cooldown", "counter_70": 0, "resource_170": 1.0}]},
         "observed_state": None, "observed_source": None, "equipment_observed": False,
         "equipment": "unarmed (weapon/off-hand slots empty) — NPC equipment of this kind not observed",
         "name_hint": "Synthetic"}
    r.update(over)
    return r


class EquipmentObserved(unittest.TestCase):
    def profiles(self, rec):
        with tempfile.TemporaryDirectory() as d:
            p = Path(d) / "ev.json"
            p.write_text(json.dumps({"region": "R", "kinds": {"0x91": rec}}), encoding="utf-8")
            before = list(ct.EVIDENCE_PROBLEMS)
            out = ct._creature_ai_profiles(str(p))
            new = ct.EVIDENCE_PROBLEMS[len(before):]
            del ct.EVIDENCE_PROBLEMS[len(before):]
            return out, new

    def test_not_observed_text_is_not_parsed_as_observed(self):
        out, _ = self.profiles(record())          # the text contains the word "observed"
        self.assertIs(out["alpha_actions/creature_ai_synthetic_0x91"]["equipment_observed"], False)

    def test_true_only_from_the_boolean(self):
        out, _ = self.profiles(record(equipment_observed=True, equipment="unarmed — observed"))
        self.assertIs(out["alpha_actions/creature_ai_synthetic_0x91"]["equipment_observed"], True)

    def test_missing_or_non_bool_refuses_the_profile(self):
        for bad in (None, "false", 0):
            rec = record(equipment_observed=bad)
            if bad is None:
                rec.pop("equipment_observed")
            out, problems = self.profiles(rec)
            self.assertEqual(out, {}, bad)
            self.assertTrue(problems and "must be a boolean" in problems[0])

    def test_committed_evidence_is_typed_and_only_0x03_observed(self):
        ev = json.loads(Path(ct.NPC_EVIDENCE_PATH).read_text(encoding="utf-8"))["kinds"]
        obs = {k for k, e in ev.items() if e.get("status") == "TRIGGER_EMULATED" and e["equipment_observed"] is True}
        self.assertEqual(obs, {"0x03"})
        self.assertTrue(all(isinstance(e["equipment_observed"], bool)
                            for e in ev.values() if e.get("status") == "TRIGGER_EMULATED"))
        self.assertEqual(ct.EVIDENCE_PROBLEMS, [])

    def test_equipment_observed_requires_the_full_equipment_block(self):
        self.assertTrue(at.equipment_observed({"fields": at.OBSERVED_FIELDS}))
        self.assertFalse(at.equipment_observed({"fields": at.HEADER_FIELDS}))
        self.assertFalse(at.equipment_observed(None))


class EvidenceDrift(unittest.TestCase):
    def test_all_declared_seeds_are_rerun(self):
        seen = {}

        def fake(image, kind, seeds, family, observed=None):
            seen["seeds"] = list(seeds)
            return record()
        with mock.patch.object(at, "kind_evidence", fake):
            self.assertEqual(at.evidence_drift_kind(None, "0x91", record()), [])
        self.assertEqual(seen["seeds"], [(0, 0), (1, 1)])

    def test_drift_in_a_non_base_seed_condition_is_reported(self):
        changed = record(condition_examples={"75": [{"cooldowns": "x", "counter_70": 9, "resource_170": 0.0}]})
        with mock.patch.object(at, "kind_evidence", lambda *a, **k: changed):
            p = at.evidence_drift_kind(None, "0x91", record())
        self.assertTrue(any("condition_examples" in x for x in p))

    def test_flipped_equipment_flag_is_drift(self):
        with mock.patch.object(at, "kind_evidence", lambda *a, **k: record(equipment_observed=True)):
            p = at.evidence_drift_kind(None, "0x91", record())
        self.assertTrue(any("equipment_observed" in x for x in p))

    def test_non_bool_equipment_flag_is_refused(self):
        p = at.evidence_drift_kind(None, "0x91", record(equipment_observed="no"))
        self.assertTrue(p and "EQUIPMENT_OBSERVED_NOT_BOOL" in p[0])

    def test_observed_state_must_have_a_reproducible_source(self):
        p = at.evidence_drift_kind(None, "0x91", record(observed_state="live dump"))
        self.assertTrue(p and "OBSERVED_STATE_WITHOUT_SOURCE" in p[0])

    def test_missing_or_changed_observed_capture_is_drift(self):
        with tempfile.TemporaryDirectory() as d:
            desc = {"capture": "cap.jsonl", "record_field": "creature_full", "kind": 3,
                    "fields": "OBSERVED_FIELDS", "source": "s", "sha256": "0" * 64}
            p = at.evidence_drift_kind(None, "0x91", record(observed_source=desc, observed_state="s"), root=d)
            self.assertTrue(p and "OBSERVED_SOURCE_MISSING" in p[0])
            Path(d, "cap.jsonl").write_text(json.dumps({"kind": 3, "creature_full": "00"}) + "\n", encoding="utf-8")
            p = at.evidence_drift_kind(None, "0x91", record(observed_source=desc, observed_state="s"), root=d)
            self.assertTrue(p and "OBSERVED_SOURCE_SHA256" in p[0])


if __name__ == "__main__":
    unittest.main()
