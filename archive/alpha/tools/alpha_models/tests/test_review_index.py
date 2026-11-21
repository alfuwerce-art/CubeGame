"""Specimen selection of the S05B visual review (no Alpha data needed)."""
import unittest
from pathlib import Path

from tools.alpha_models.review import build_review as br


def entry(kind, seed, fps=60, name="X"):
    return {"id": f"{kind}_s{seed[0]}-{seed[1]}_{fps}hz", "kind": kind, "seed": list(seed), "name_hint": name}


class SelectSpecimens(unittest.TestCase):
    def test_dedupes_fps_and_orders_kinds_and_seed00_first(self):
        es = [entry("0x1e", (0, 0)), entry("0x03", (1, 2)), entry("0x03", (0, 0), 30), entry("0x03", (0, 0)),
              entry("0x02", (0, 0))]
        out = br.select_specimens(es, {}, None)
        self.assertEqual([o["id"] for o in out], ["0x02_s0-0", "0x03_s0-0", "0x03_s1-2", "0x1e_s0-0"])

    def test_seed00_uses_action_job_others_locomotion_job(self):
        es = [entry("0x03", (0, 0)), entry("0x03", (1, 2))]
        out = br.select_specimens(es, {"0x03": Path("ba.json")}, None)
        self.assertEqual(out[0]["job"], Path("ba.json"))
        self.assertTrue(str(out[1]["job"]).endswith("0x03_s1-2_60hz.json"))

    def test_humanmale_gets_player_and_capture_specimens(self):
        out = br.select_specimens([entry("0x02", (0, 0))], {}, Path("reg.json"), Path("cap.json"))
        self.assertEqual([o["id"] for o in out], ["0x02_s0-0", "0x02_s0-0_player", "0x02_s0-0_capture"])

    def test_kind_outside_review_set_is_refused(self):
        with self.assertRaises(ValueError):
            br.select_specimens([entry("0x62", (0, 0))], {}, None)

    def test_failed_prepare_is_recorded_not_dropped(self):
        spec = {"id": "0x03_s0-0", "kind": "0x03", "seed": [0, 0], "name": "HF", "source": "t",
                "job": br.ROOT / "tools" / "alpha_models" / "universal" / "jobs" / "cat_0x1e.json"}

        def boom(job, project, clips_root=None):
            raise RuntimeError("no data")
        import tempfile
        from unittest import mock
        with tempfile.TemporaryDirectory() as d, mock.patch.object(br, "STAGE", Path(d) / "stage"):
            rec = br.prepare_one(spec, Path(d), prepare=boom)
        self.assertEqual(rec["prepare"], "FAIL")
        self.assertIn("EXCEPTION RuntimeError: no data", rec["problems"][0])


if __name__ == "__main__":
    unittest.main()
