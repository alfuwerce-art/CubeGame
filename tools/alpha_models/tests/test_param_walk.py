"""Tests of the parametric Alpha walk (tools/alpha_models/universal/param_walk.py).

* synthetic tests — TEST_DATA_NOT_ALPHA appearances: chain algebra, gait symmetry, flag branches,
  smoothing, classification, clip/spec export, coverage;
* local evidence — only when the captures exist in .scratch (never in Git): the formula must
  reproduce every captured frame of the human, WhiteCat and PlainRunner within float32 precision.
"""
from __future__ import annotations

import math
import os
import unittest

from tools.alpha_models.anim import mat4
from tools.alpha_models.anim.alpha_clip import decompose, pose_error
from tools.alpha_models.universal import param_walk as pw

SCRATCH = r"D:\Projects\CubeGame\.scratch"
CAP_HUMAN = os.path.join(SCRATCH, "s05a2", "cap", "session2.jsonl")
CAP_BATCH = os.path.join(SCRATCH, "universal", "cap", "batch1.jsonl")
STATIC = os.path.join(SCRATCH, "universal", "static", "alpha_static.json")
HAVE_CAPTURES = os.path.exists(CAP_HUMAN) and os.path.exists(CAP_BATCH)

BIPED = pw.SYNTHETIC_APPEARANCES["biped"]
QUAD = pw.SYNTHETIC_APPEARANCES["quadruped"]


def full_app(base, **over):
    app = dict(base, angle_48=0.0, angle_4c=0.0, angle_50=0.0, angle_54=0.0, angle_5c=0.0)
    app.update(over)
    return app


def rot_deg(m):
    _, q, _ = decompose(m)
    return math.degrees(2 * math.acos(min(1.0, abs(q[0]))))


class FormulaTests(unittest.TestCase):
    def test_rest_pose_is_offsets_and_scales(self):
        j = pw.joints(BIPED, pw.WalkState(1.0, 0.0), ["body", "foot_l", "tail"])
        self.assertLess(mat4.max_abs_diff(j["body"], mat4.mul(pw.scale(0.9), mat4.translate(0, 1, -3))), 1e-12)
        t, _, s = decompose(j["foot_l"])
        self.assertAlmostEqual(t[0], -2.0)
        self.assertAlmostEqual(t[2], -9.0)
        self.assertAlmostEqual(s[0], 0.8)
        self.assertAlmostEqual(rot_deg(j["tail"]), 20.0, places=6)    # pitch_tail only

    def test_swing_amplitude_matches_code_constants(self):
        k = pw.core(BIPED, pw.WalkState(0.0, 0.2))
        self.assertAlmostEqual(k["w"], 0.2 * 2.1991148)
        self.assertAlmostEqual(k["d0"], 1.5 * math.degrees(k["w"]))
        self.assertAlmostEqual(k["bob"], 4 * k["w"])
        self.assertAlmostEqual(pw.core(BIPED, pw.WalkState(0.0, 0.2, boost=True))["w"], 1.25 * k["w"])
        self.assertAlmostEqual(pw.core(QUAD, pw.WalkState(0.0, 0.2))["w"], 0.75 * k["w"])

    def test_foot_orientation_is_twice_the_hip_pitch(self):
        st = pw.WalkState(0.3, 0.19)
        k = pw.core(BIPED, st)
        self.assertAlmostEqual(rot_deg(pw.joints(BIPED, st, ["foot_l"])["foot_l"]), 2 * abs(k["d0"]), places=6)

    def test_gait_half_cycle_symmetry(self):
        a = pw.joints(BIPED, pw.WalkState(0.0, 0.19), ["foot_l"])["foot_l"]
        b = pw.joints(BIPED, pw.WalkState(math.pi, 0.19), ["foot_r"])["foot_r"]
        self.assertLess(pose_error(a, b)[1], 1e-6)

    def test_biped_hands_translate_quadruped_hands_pitch(self):
        jb = pw.joints(BIPED, pw.WalkState(0.0, 0.19), ["hand_l", "hand_r"])
        jq = pw.joints(QUAD, pw.WalkState(0.0, 0.19), ["hand_l"])
        self.assertLess(rot_deg(jb["hand_l"]), 1e-9)
        self.assertGreater(rot_deg(jq["hand_l"]), 10.0)
        self.assertLess(mat4.det3(jb["hand_r"]), 0)     # mirrored right hand
        self.assertGreater(mat4.det3(jb["hand_l"]), 0)

    def test_airborne_bit1_freezes_swing(self):
        app = pw.SYNTHETIC_APPEARANCES["airborne_bit1"]
        air = pw.joints(app, pw.WalkState(0.0, 0.19, on_ground=False), ["foot_l", "body"])
        rest = pw.joints(app, pw.WalkState(0.0, 0.0), ["foot_l", "body"])
        for p in air:
            self.assertLess(mat4.max_abs_diff(air[p], rest[p]), 1e-12)
        ground = pw.joints(app, pw.WalkState(0.0, 0.19, on_ground=True), ["foot_l"])
        self.assertGreater(mat4.max_abs_diff(ground["foot_l"], rest["foot_l"]), 0.1)

    def test_head_lean_rotates_about_neck(self):
        j0 = pw.joints(BIPED, pw.WalkState(0.0, 0.0, head_lean_deg=0.0), ["head"])["head"]
        j1 = pw.joints(BIPED, pw.WalkState(0.0, 0.0, head_lean_deg=10.0), ["head"])["head"]
        neck0 = mat4.transform_point(mat4.inverse_affine(j0), [0, 0, 0])
        # the neck point (3 voxels below the joint) does not move
        p0 = mat4.transform_point(j0, [0, 0, -3 / 1.1])
        p1 = mat4.transform_point(j1, [0, 0, -3 / 1.1])
        self.assertLess(max(abs(a - b) for a, b in zip(p0, p1)), 1e-9, neck0)
        self.assertAlmostEqual(rot_deg(j1), 10.0, places=6)

    def test_lean_targets(self):
        self.assertEqual(pw.head_lean_target(BIPED, True, False), 10.0)
        self.assertEqual(pw.head_lean_target(BIPED, False, True), 5.0)
        self.assertEqual(pw.head_lean_target(BIPED, False, False), 0.0)
        self.assertEqual(pw.head_lean_target(QUAD, True, True), 0.0)

    def test_lean_step_equals_array_lerp_loop(self):
        # GameController.cpp:1231 array_lerp(&f, &1, n, 0.01): f += (1 - f) * 0.01, n times
        f = 0.0
        for _ in range(37):
            f += (1.0 - f) * 0.01
        self.assertAlmostEqual(pw.lean_step(0.0, 1.0, 37), f, places=12)

    def test_phase_rate_matches_measured_ratios(self):
        # dphi/(|v| dt) measured on the captures: 1.37 (size.x 0.96), 1.22 (size.x 1.2)
        self.assertAlmostEqual(pw.phase_rate({"size": [0.96]}, 1.0), 1.369, places=2)
        self.assertAlmostEqual(pw.phase_rate({"size": [1.2]}, 1.0), 1.2247, places=3)


class ClassificationTests(unittest.TestCase):
    def test_quadruped_with_tail_is_verified(self):
        v = pw.classify(full_app(QUAD), ["body", "head", "tail", "hand_l", "hand_r", "foot_l", "foot_r"])
        self.assertEqual(v["status"], "FORMULA_VERIFIED", v)

    def test_biped_head_needs_ai_lean_term(self):
        v = pw.classify(full_app(BIPED), ["body", "head", "hand_l", "hand_r", "foot_l", "foot_r"])
        self.assertEqual(v["status"], "FORMULA_CODE_ONLY_TERMS")
        self.assertIn("head_lean_ai", v["code_only_terms"])

    def test_unknown_terms_and_parts_need_research(self):
        self.assertEqual(pw.classify(full_app(QUAD, angle_48=10.0), ["body"])["status"], "NEEDS_RESEARCH")
        self.assertEqual(pw.classify(full_app(QUAD, angle_50=5.0), ["body", "hand_l", "hand_r"])["status"],
                         "NEEDS_RESEARCH")
        # hand rest angles do not matter without hands
        self.assertEqual(pw.classify(full_app(QUAD, angle_50=5.0), ["body"])["status"], "FORMULA_VERIFIED")
        self.assertEqual(pw.classify(full_app(QUAD), ["body", "slot9a_a"])["status"], "NEEDS_RESEARCH")
        self.assertEqual(pw.classify(full_app(QUAD), ["body"], kind_exceptions=["KIND_SPECIFIC_CODE 0x57"])["status"],
                         "NEEDS_RESEARCH")

    def test_angle_58_is_code_only_with_feet(self):
        v = pw.classify(full_app(QUAD, angle_58=12.0), ["body", "foot_l", "foot_r"])
        self.assertEqual(v["status"], "FORMULA_CODE_ONLY_TERMS")
        self.assertEqual(pw.classify(full_app(QUAD, angle_58=12.0), ["body"])["status"], "FORMULA_VERIFIED")

    def test_generate_clip_refuses_research_and_marks_status(self):
        with self.assertRaises(ValueError):
            pw.generate_walk_clip(full_app(QUAD, angle_48=3.0), 0x99, "alpha_creature/x", ["body"], 0.19)
        clip = pw.generate_walk_clip(full_app(QUAD), 0x99, "alpha_creature/x", ["body", "foot_l", "foot_r"], 0.19,
                                     keys=24, speed_xy=3.7)
        self.assertEqual(clip["status"], "RECONSTRUCTED_FROM_EVIDENCE")
        self.assertEqual(len(clip["keys"]), 24)
        self.assertEqual(clip["param"], "walk_phase")
        self.assertGreater(clip["source"]["phase_rate_rad_per_s"], 0)
        self.assertNotEqual(clip["status"], "TRANSFERRED_FROM_ALPHA")

    def test_golden_cases_cover_every_part_and_branch(self):
        g = pw.golden_cases()
        self.assertEqual(g["status"], "TEST_DATA_NOT_ALPHA")
        parts = {c["part"] for c in g["cases"]}
        self.assertEqual(parts, set(pw.MODELLED_PARTS))
        self.assertTrue(any(not c["onGround"] for c in g["cases"]))
        self.assertTrue(any(c["p"]["angle_58"] != 0 for c in g["cases"]))

    def test_coverage_on_synthetic_table(self):
        def slots(**on):
            base = {"head": -1, "hair": -1, "hand": -1, "foot": -1, "body": -1, "tail": -1, "slot_98": -1, "slot_9a": -1}
            base.update(on)
            return base
        table = {"kinds": {
            "0x90": {"appearance": dict(full_app(QUAD), slots=slots(body=1, head=2, hand=3, foot=4, tail=5))},
            "0x91": {"appearance": dict(full_app(BIPED), slots=slots(body=1, head=2, hand=3, foot=4))},
            "0x92": {"appearance": dict(full_app(QUAD), slots=slots(body=1, slot_9a=7))},
        }}
        cov = pw.coverage(table)
        st = {r["kind"]: r["status"] for r in cov["kinds"]}
        self.assertEqual(st, {"0x90": "FORMULA_VERIFIED", "0x91": "FORMULA_CODE_ONLY_TERMS", "0x92": "NEEDS_RESEARCH"})


@unittest.skipUnless(HAVE_CAPTURES, "local Alpha captures (.scratch) not present")
class LocalAlphaEvidenceTests(unittest.TestCase):
    """The formula vs EVERY usable state-0 frame (all amplitudes, boost on/off) of three body plans."""

    def check(self, path, kind, expect_parts):
        from tools.alpha_models.universal import uclip
        r = pw.validate_capture(uclip.load_capture(path, kind=kind))
        self.assertGreater(r["frames"], 500)
        self.assertEqual(set(r["parts"]), set(expect_parts))
        for p, v in r["parts"].items():
            if p in ("head", "hair") and r["head_lean_one_step"]:
                continue
            self.assertLessEqual(v["max_pos_vox"], pw.VALIDATE_MAX_POS_VOX, (p, v))
            self.assertLessEqual(v["max_rot_deg"], pw.VALIDATE_MAX_ROT_DEG, (p, v))
        return r

    def test_whitecat_quadruped_with_tail(self):
        self.check(CAP_BATCH, 0x20, ["body", "head", "tail", "hand_l", "hand_r", "foot_l", "foot_r"])

    def test_plainrunner_body_and_two_legs(self):
        self.check(CAP_BATCH, 0x3F, ["body", "foot_l", "foot_r"])

    def test_human_biped_and_head_lean_smoothing(self):
        r = self.check(CAP_HUMAN, 0x02, ["body", "head", "hair", "hand_l", "hand_r", "foot_l", "foot_r"])
        lean = r["head_lean_one_step"]
        self.assertGreater(lean["frames"], 500)
        self.assertLess(lean["p95_deg"], 0.05)

    @unittest.skipUnless(os.path.exists(STATIC), "static table not present")
    def test_coverage_of_all_kinds(self):
        import json
        with open(STATIC, encoding="utf-8") as fh:
            cov = pw.coverage(json.load(fh))
        self.assertEqual(cov["metrics"]["kinds"], 156)
        self.assertEqual(cov["metrics"]["families"], 106)
        self.assertEqual(sum(cov["metrics"]["kinds_by_status"].values()), 156)


if __name__ == "__main__":
    unittest.main()
