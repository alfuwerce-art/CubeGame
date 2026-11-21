"""Synthetic tests for the Alpha capture -> clip -> Unity pipeline (TEST_DATA_NOT_ALPHA).

A fake capture is generated from KNOWN joint frames J: M = T(-anchor) * J * P, written in the
exact JSONL layout of pose_capture.py, so every step (loading, joint recovery, pitch layer,
walk/time clip building, validation, Unity flattening) is checked against ground truth.
"""
from __future__ import annotations

import json
import math
import os
import struct
import tempfile
import unittest

from tools.alpha_models.anim import alpha_clip as ac
from tools.alpha_models.anim import humanoid_rig as rig
from tools.alpha_models.anim import mat4
from tools.alpha_models.runtime import pose_capture as pc
from tools.alpha_models.transfer import prepare_unity as pu

SIZES = {"body": (10, 8, 10), "head": (18, 16, 16), "hair": (18, 16, 16), "weapon": (13, 13, 33),
         "hand_l": (5, 6, 4), "hand_r": (5, 6, 4), "foot_l": (5, 8, 4), "foot_r": (5, 8, 4)}


def rot_z(deg):
    c, s = math.cos(math.radians(deg)), math.sin(math.radians(deg))
    return [c, s, 0, 0, -s, c, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1.0]


def scale(x, y, z):
    return [x, 0, 0, 0, 0, y, 0, 0, 0, 0, z, 0, 0, 0, 0, 1.0]


def true_joint(name, u):
    """Synthetic 'animation': each part swings with u (phase or normalised time)."""
    base = {"body": (0, 0, 0), "head": (0, 0, 12), "hair": (0, 0, 12), "weapon": (6, 4, 2),
            "hand_l": (-6, 2, -2), "hand_r": (6, 2, -2), "foot_l": (-3, 0, -10), "foot_r": (3, 0, -10)}[name]
    s = -1.0 if name == "hand_r" else 1.0
    j = mat4.mul(scale(s, 1, 1), rot_z(30 * math.sin(u) + (10 if name == "weapon" else 0)))
    return mat4.mul(j, mat4.translate(base[0] + 2 * math.cos(u), base[1] + 3 * math.sin(u), base[2]))


def placement(yaw=25.0, k=0.086):
    return mat4.mul(mat4.mul(scale(k, k, k), rot_z(yaw)), mat4.translate(1.5, -2.0, 154.0))


def frame_record(state, time_ms, action, amp, phase, joints, pitch=0.0, accel=(0, 80, 0), on_back=True,
                 draw_sites=None):
    P = placement()
    ch = bytearray(0x180)
    struct.pack_into("<3f", ch, 0x40, *accel)
    struct.pack_into("<f", ch, 0x58, pitch)
    struct.pack_into("<f", ch, 0xB0, 0.95)
    this = bytearray(0xC0)
    struct.pack_into("<i", this, 4, time_ms)
    struct.pack_into("<i", this, 8, state)
    parts = []
    for p in rig.PARTS:
        size = SIZES[p.name]
        j = ac.pitch_layer(p.name, joints[p.name], pitch, on_back)   # the game ADDS the pitch layer
        a = rig.anchor_for(p, size, on_back=on_back)
        m = mat4.mul(mat4.mul(mat4.translate(-a[0], -a[1], -a[2]), j), P)
        hdr = bytearray(0x60)
        struct.pack_into("<i", hdr, 0x3C, 100)
        struct.pack_into("<3i", hdr, 0x44, *size)
        site = (draw_sites or {}).get(p.name, p.draw_site)
        parts.append({"upload_site": f"0x{p.upload_site:06X}", "matrix": m,
                      "draw_site": f"0x{site:06X}", "mesh": "0x1", "mesh_hdr": hdr.hex()})
    return {"t": time_ms / 1000.0, "creature": "0x1", "this": "0x2", "state": state, "indices": [],
            "fields": {"type_68": action, "amp_1188": amp, "phase_118c": phase},
            "this_hdr": this.hex(), "creature_hdr": ch.hex(), "esp_body": "0x0",
            "frame_check": True, "placement": P, "root": P, "parts": parts}


def write_capture(records):
    fd, path = tempfile.mkstemp(suffix=".jsonl")
    with os.fdopen(fd, "w", encoding="utf-8") as fh:
        for r in records:
            fh.write(json.dumps(r) + "\n")
    return path


class CaptureLoadingTests(unittest.TestCase):
    def test_parts_mapped_and_joint_recovered_exactly(self):
        joints = {p.name: true_joint(p.name, 0.7) for p in rig.PARTS}
        path = write_capture([frame_record(0, 10, 0, 0.0, 0.0, joints)])
        fr = ac.load_capture(path)[0]
        self.assertEqual(fr.problems, [])
        self.assertEqual(set(fr.parts), {p.name for p in rig.PARTS})
        got = ac._frame_joints(fr, on_back=True)
        for name, j in joints.items():
            self.assertLess(mat4.max_abs_diff(got[name], j), 1e-9, name)

    def test_unmapped_site_is_reported(self):
        joints = {p.name: true_joint(p.name, 0.1) for p in rig.PARTS}
        path = write_capture([frame_record(0, 10, 0, 0.0, 0.0, joints, draw_sites={"body": 0x123456})])
        fr = ac.load_capture(path)[0]
        self.assertTrue(any(p.startswith("UNMAPPED_SITE") for p in fr.problems))
        self.assertFalse(ac._usable(fr))

    def test_pitch_layer_is_removed(self):
        joints = {p.name: true_joint(p.name, 1.1) for p in rig.PARTS}
        path = write_capture([frame_record(2, 500, 57, 0.0, 0.0, joints, pitch=-12.0, on_back=False)])
        fr = ac.load_capture(path)[0]
        got = ac._frame_joints(fr, on_back=False)
        for name in ("head", "hair", "weapon", "body"):
            self.assertLess(mat4.max_abs_diff(got[name], joints[name]), 1e-9, name)
        raw = ac._frame_joints(fr, on_back=False, normalize_pitch=False)
        self.assertGreater(mat4.max_abs_diff(raw["head"], joints["head"]), 1e-3)

    def test_decompose_compose_roundtrip_with_mirror(self):
        m = mat4.mul(mat4.mul(scale(-1.2, 1.2, 1.2), rot_z(40)), mat4.translate(1, 2, 3))
        t, q, s = ac.decompose(m)
        self.assertLess(s[0], 0)
        self.assertLess(mat4.max_abs_diff(ac.compose(t, q, s), m), 1e-9)


class ClipBuildingTests(unittest.TestCase):
    def _walk_capture(self, cycles=4, per_cycle=24, accel_stop_at=None):
        recs = []
        n = cycles * per_cycle
        for i in range(n):
            phase = 5.0 + i * 2 * math.pi / per_cycle + 0.013 * (i % 3)
            joints = {p.name: true_joint(p.name, phase) for p in rig.PARTS}
            accel = (0, 0, 0) if accel_stop_at is not None and i >= accel_stop_at else (0, 80, 0)
            r = frame_record(0, 1000 + 16 * i, 0, 0.19, phase, joints, accel=accel)
            r["t"] = i / 60.0
            recs.append(r)
        return write_capture(recs)

    def test_walk_clip_is_repeatable_function_of_phase(self):
        path = self._walk_capture()
        clip = ac.build_walk_clip(ac.load_capture(path), path)
        v = ac.validate_walk(clip, clip["_steady_frames"])
        self.assertEqual(clip["status"], "TRANSFERRED_FROM_ALPHA")
        self.assertTrue(clip["loop"])
        self.assertEqual(v["cycles"], 4)
        self.assertLess(v["max_translation_vox"], 0.05)
        self.assertAlmostEqual(clip["source"]["cycle_seconds"], 24 / 60.0, places=1)

    def test_walk_requires_input_held(self):
        path = self._walk_capture(cycles=2, accel_stop_at=10)
        with self.assertRaises(ValueError):
            ac.build_walk_clip(ac.load_capture(path), path)

    def test_time_clip_cut_at_motion_stop_and_runs_match(self):
        recs = []
        for run in range(2):
            for i in range(60):
                t = i * 17 + run * 3
                u = min(t, 600) / 600.0 * 2.0          # motion stops at 600 ms, then a hold
                joints = {p.name: true_joint(p.name, u) for p in rig.PARTS}
                recs.append(frame_record(2, t, 57, 0.0, 0.0, joints, on_back=False))
            recs.append(frame_record(0, 99999, 0, 0.0, 0.0, {p.name: true_joint(p.name, 0) for p in rig.PARTS}))
        path = write_capture(recs)
        frames = ac.load_capture(path)
        clip = ac.build_time_clip(frames, path, 2, 57, "atk")
        self.assertLessEqual(clip["source"]["hold_from_ms"], 600 + 17)
        self.assertGreaterEqual(clip["source"]["hold_from_ms"], 600 - 17)
        v = ac.validate_time(clip, frames)
        self.assertEqual(v["other_runs"], 1)
        self.assertLess(v["interpolated"]["max_translation_vox"], 0.1)

    def test_reconstruct_check_is_exact(self):
        joints = {p.name: true_joint(p.name, 0.4) for p in rig.PARTS}
        path = write_capture([frame_record(0, 1, 0, 0.0, 0.0, joints)])
        self.assertLess(ac.reconstruct_check(ac.load_capture(path), True), 1e-9)

    def test_sample_loop_wraps(self):
        clip = {"loop": True, "duration": 10.0,
                "keys": [{"x": 1.0, "m": {"body": mat4.translate(0, 0, 0)}},
                         {"x": 9.0, "m": {"body": mat4.translate(8, 0, 0)}}]}
        mid = ac.sample(clip, 0.0)["body"]          # halfway between key 9 and key 1+10
        self.assertAlmostEqual(mid[12], 4.0, places=6)


class RigTests(unittest.TestCase):
    def test_weapon_anchor_rules(self):
        self.assertEqual(rig.weapon_anchor((3, 13, 37), on_back=False, subtype=15), (1.5, 6.5, 4.0))
        self.assertEqual(rig.weapon_anchor((3, 13, 37), on_back=True), (1.5, 6.5, 18.5))
        with self.assertRaises(ValueError):
            rig.weapon_anchor((3, 13, 37), on_back=False, subtype=5)

    def test_compatibility_problems(self):
        sizes = dict(SIZES)
        self.assertEqual(rig.check_model_compatibility(sizes), [])
        bad = dict(SIZES)
        del bad["foot_r"]
        bad["hand_r"] = (6, 6, 4)
        bad["tail"] = (3, 3, 3)
        probs = rig.check_model_compatibility(bad)
        self.assertTrue(any(p.startswith("MISSING_PART foot_r") for p in probs))
        self.assertTrue(any(p.startswith("HANDS_DIFFER") for p in probs))
        self.assertTrue(any(p.startswith("UNKNOWN_PART tail") for p in probs))

    def test_sites_unique(self):
        self.assertEqual(len({p.draw_site for p in rig.PARTS}), len(rig.PARTS))
        self.assertEqual(len({p.upload_site for p in rig.PARTS}), len(rig.PARTS))


class CaptureToolTests(unittest.TestCase):
    def test_find_call_sites(self):
        start = 0x1000
        target = 0x1050
        code = bytearray(64)
        code[4] = 0xE8
        code[5:9] = (target - (start + 4 + 5)).to_bytes(4, "little", signed=True)
        code[20] = 0xE8
        code[21:25] = (0x9999 - (start + 20 + 5)).to_bytes(4, "little", signed=True)
        sites = pc.find_call_sites(lambda va, n: bytes(code[va - start:va - start + n]), start, start + 64, target)
        self.assertEqual(sites, [start + 4])


class PrepareUnityTests(unittest.TestCase):
    def test_unity_clip_flattens_in_rig_order(self):
        order = [p.name for p in rig.PARTS]
        clip = {"name": "c", "status": "TRANSFERRED_FROM_ALPHA", "rig": rig.RIG_ID, "param": "time_ms",
                "loop": False, "duration": 10.0, "source": {"weapon_on_back": False},
                "parts": [{"name": n, "captured_size": list(SIZES[n])} for n in reversed(order)],
                "keys": [{"x": 0.0, "m": {n: mat4.translate(i, 0, 0) for i, n in enumerate(order)}}]}
        uc = pu.unity_clip(clip, order)
        self.assertEqual(uc["parts"], order)
        self.assertEqual(len(uc["keys"][0]["m"]), 16 * len(order))
        self.assertEqual(uc["keys"][0]["m"][16 * 3 + 12], 3.0)
        self.assertEqual(uc["capturedSizes"][:3], list(SIZES[order[0]]))

    def test_unity_clip_rejects_part_mismatch(self):
        clip = {"name": "c", "parts": [{"name": "body", "captured_size": [1, 1, 1]}], "keys": []}
        with self.assertRaises(ValueError):
            pu.unity_clip(clip, ["body", "head"])


if __name__ == "__main__":
    unittest.main()
