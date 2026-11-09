"""Issue #7: Dragon 0x65 — root cause in Alpha's own appearance data, opt-in visual correction proposal.

* default (no correction): the appearance is Alpha's, byte for byte, and the executed drawModel reproduces
  the defect the producer saw (paws far above the ground plane, a hole between body and tail);
* ``dragon_0x65_v1``: only kind 0x65, only the three documented offsets; paws on the ground plane of the
  finished quadrupeds, tail and legs touching the body; head and body matrices unchanged;
* the proposal refuses to patch a value that is not the documented Alpha value, and it changes the
  library cache key (a corrected library is never reused as the Alpha one, or vice versa).
"""
from __future__ import annotations

import math
import sqlite3
import struct
import unittest
from pathlib import Path

from tools.alpha_models.universal import visual_correction as vc

ROOT = Path(__file__).resolve().parents[3]
CUBE = ROOT / ".scratch" / "alphatest2" / "Cube.exe"
DB = ROOT / ".scratch" / "alphatest2" / "data1.db"
SIZES = ROOT / ".scratch" / "catalog" / "registry_sizes.json"
HAVE_ALPHA = CUBE.exists() and DB.exists() and SIZES.exists()
GROUND_Z = -12.0           # lowest voxel centre of the paws/feet at rest: cat 0x1E and human 0x02


class CorrectionUnitTests(unittest.TestCase):
    def _raw(self, values):
        raw = bytearray(0xAC)
        for off, v in values.items():
            struct.pack_into("<3f", raw, off, *v)
        return bytes(raw)

    def test_off_by_default_and_no_op_outside_block(self):
        raw = self._raw({off: a for off, (_, a, _) in vc.CORRECTIONS["dragon_0x65_v1"]["fields"].items()})
        self.assertIsNone(vc.current())
        self.assertEqual(vc.apply(0x65, raw), raw)
        with vc.active("dragon_0x65_v1"):
            self.assertNotEqual(vc.apply(0x65, raw), raw)
            self.assertEqual(vc.apply(0x1E, raw), raw)      # other kinds are never touched
        self.assertIsNone(vc.current())

    def test_only_documented_fields_change(self):
        c = vc.CORRECTIONS["dragon_0x65_v1"]
        raw = self._raw({off: a for off, (_, a, _) in c["fields"].items()})
        with vc.active("dragon_0x65_v1"):
            new = vc.apply(0x65, raw)
        changed = {i for i in range(len(raw)) if raw[i] != new[i]}
        allowed = {o + i for o in c["fields"] for i in range(12)}
        self.assertTrue(changed and changed <= allowed)
        for off, (_, _, proposed) in c["fields"].items():
            self.assertEqual(struct.unpack_from("<3f", new, off), proposed)

    def test_refuses_unexpected_alpha_value(self):
        raw = self._raw({0x7C: (9.0, 9.0, 9.0)})
        with vc.active("dragon_0x65_v1"):
            with self.assertRaises(ValueError):
                vc.apply(0x65, raw)

    def test_unknown_name_and_provenance(self):
        with self.assertRaises(KeyError):
            with vc.active("no_such_fix"):
                pass
        p = vc.provenance("dragon_0x65_v1")
        self.assertEqual(p["status"], "VISUAL_CORRECTION_PROPOSAL_NOT_ALPHA")
        self.assertEqual(set(p["fields"]), {"offset_hand", "offset_foot", "offset_tail"})

    def test_action_profile_kept_only_under_the_declared_correction(self):
        from tools.alpha_models.universal import contract as ct
        pid = "alpha_actions/creature_ai_d64_dragon_0x65"
        parts = ["head", "body", "tail", "hand_l", "hand_r", "foot_l", "foot_r"]
        corrected_family = "alpha_creature/corrected"
        prof, status, reasons = ct.action_profile_for(0x65, [0, 0], corrected_family, parts)
        self.assertIsNone(prof)                                     # pinned family still guards Alpha parity
        self.assertIn("SPECIMEN_APPEARANCE_DIFFERS", reasons[0])
        with vc.active("dragon_0x65_v1"):
            prof, status, reasons = ct.action_profile_for(0x65, [0, 0], corrected_family, parts)
            self.assertEqual(prof, pid)
            self.assertIn("VISUAL_CORRECTION_PROPOSAL_NOT_ALPHA", reasons[0])
            self.assertIsNone(ct.action_profile_for(0x1E, [0, 0], corrected_family, ["head"])[0])
            # the Alpha family itself needs no note
            self.assertEqual(ct.action_profile_for(0x65, [0, 0], "alpha_creature/ccddb0483c", parts)[2], [])

    def test_cache_key_distinguishes_the_correction(self):
        from tools.alpha_models.universal import batch_runner as br
        if not (ROOT / ".scratch" / "alphatest2" / "Cube.exe").exists():
            self.skipTest("local Alpha copy missing")
        from tools.alpha_models.universal import contract as ct
        prof = next(p for p, v in ct.ACTION_PROFILES.items()
                    if any(s["kind"] == 0x65 for s in v["specimens"]))
        e = {"kind": "0x65", "seed": [0, 0], "profile": prof}
        plain = br.bake_cache_key(e)
        with vc.active("dragon_0x65_v1"):
            fixed = br.bake_cache_key(e)
        self.assertNotIn("visual_correction", plain)
        self.assertEqual(fixed["visual_correction"], "dragon_0x65_v1")
        self.assertIn("visual_correction.py", plain["code_sha256"])


@unittest.skipUnless(HAVE_ALPHA, "local Alpha copy (.scratch/alphatest2) missing")
class DragonGeometryTests(unittest.TestCase):
    """World voxel geometry of the executed drawModel frame at rest (phase 0, amplitude 0)."""

    @classmethod
    def setUpClass(cls):
        from tools.alpha_models.cub_format import deobfuscate, parse_decoded
        from tools.alpha_models.universal import alpha_oracle as ao
        import json
        cls.registry = json.loads((ROOT / ".scratch/catalog/alpha_static.json").read_text(encoding="utf-8"))["registry"]
        con = sqlite3.connect(f"file:{DB}?mode=ro&immutable=1", uri=True)
        cls._vox = {}

        def vox(name):
            if name not in cls._vox:
                m = parse_decoded(deobfuscate(con.execute("select value from blobs where key=?", (name,)).fetchone()[0]))
                cls._vox[name] = [(x, d, h) for h in range(m.height) for d in range(m.depth) for x in range(m.width)
                                  if any(m.rgb_at(x, d, h))]
            return cls._vox[name]
        cls.vox = staticmethod(vox)
        cls.oracle = ao.load(str(CUBE), str(SIZES))
        cls.alpha = cls.frame(0x65, None)
        cls.fixed = cls.frame(0x65, "dragon_0x65_v1")
        cls.cat = cls.frame(0x1E, None)

    @classmethod
    def frame(cls, kind, correction):
        from tools.alpha_models.anim import mat4
        from tools.alpha_models.universal import creature_rig as cr
        with vc.active(correction):
            em, app = cls.oracle.emulator(kind, (0, 0), True, {0x1188: ("<f", 0.0), 0x118C: ("<f", 0.0)})
            for dt in (600, 600, 16):
                fr = em.frame(dt_ms=dt)
        Pi = mat4.inverse_affine(fr["placement"])
        out = {}
        for p in fr["parts"]:
            name = cr.BY_DRAW_SITE[p["draw_site"]].name
            M = mat4.mul(p["matrix"], Pi)
            out[name] = {"M": M, "pts": [mat4.transform_point(M, (x + .5, d + .5, h + .5))
                                         for x, d, h in cls.vox(cls.registry[str(p["model_index"])])]}
        return out

    @staticmethod
    def low(parts, name):
        return min(p[2] for p in parts[name]["pts"])

    @staticmethod
    def gap(parts, a, b):
        return min(math.dist(p, q) for p in parts[a]["pts"] for q in parts[b]["pts"])

    LEGS = ("hand_l", "hand_r", "foot_l", "foot_r")

    def test_reference_quadruped_ground_plane(self):
        for leg in self.LEGS:
            self.assertAlmostEqual(self.low(self.cat, leg), GROUND_Z, places=3)

    def test_alpha_parity_reproduces_the_rejected_silhouette(self):
        # root cause documented: Alpha's own offsets put the paws >= 8 voxels above the ground plane
        # and leave a hole of > 4 voxels between body and tail
        for leg in self.LEGS:
            self.assertGreater(self.low(self.alpha, leg) - GROUND_Z, 8.0, leg)
        self.assertGreater(self.gap(self.alpha, "body", "tail"), 4.0)

    def test_proposal_paws_on_ground_and_parts_connected(self):
        for leg in self.LEGS:
            self.assertAlmostEqual(self.low(self.fixed, leg), GROUND_Z, places=3)
            self.assertLessEqual(self.gap(self.fixed, "body", leg), 1.0, leg)
        self.assertLessEqual(self.gap(self.fixed, "body", "tail"), 1.1)
        self.assertLessEqual(self.gap(self.fixed, "body", "head"), 1.0)

    def test_proposal_keeps_head_and_body(self):
        for part in ("head", "body"):
            for a, b in zip(self.alpha[part]["M"], self.fixed[part]["M"]):
                self.assertAlmostEqual(a, b, places=5, msg=part)


@unittest.skipUnless(HAVE_ALPHA, "local Alpha copy (.scratch/alphatest2) missing")
class BakeWorkerTests(unittest.TestCase):
    """Regression of the first #7 run: action clips are baked in worker PROCESSES, which did not inherit the
    active correction (rig id corrected, matrices Alpha). The worker now re-activates it and checks its family."""

    def _bake(self, correction, tmp):
        import json
        from concurrent.futures import ProcessPoolExecutor
        from tools.alpha_models.universal import action_baker as ab, alpha_oracle as ao, creature_rig as cr
        from tools.alpha_models.universal import contract as ct
        pid = "alpha_actions/creature_ai_d64_dragon_0x65"
        with vc.active(correction):
            _, app = ao.load(str(CUBE), str(SIZES)).creature(0x65, (0, 0))
        res = cr.resolve(0x65, app)
        payload = (str(CUBE), str(SIZES), 0x65, (0, 0), (0, 0), res.parts, res.family_id, "", 16, str(tmp), 37,
                   pid, correction)
        with ProcessPoolExecutor(max_workers=1) as pool:          # a real separate process, like bake_job
            entry = pool.submit(ab._bake_one, payload).result()
        self.assertEqual(entry["status"], "EMULATED_ALPHA_CODE", entry)
        clip = json.loads((Path(tmp) / "action_037.alphaclip.json").read_text(encoding="utf-8"))
        return clip["keys"][0]["m"], res

    def test_worker_applies_the_correction_and_checks_family(self):
        import tempfile
        from tools.alpha_models.universal import action_baker as ab
        with tempfile.TemporaryDirectory() as a, tempfile.TemporaryDirectory() as b:
            alpha, res_a = self._bake(None, a)
            fixed, res_f = self._bake("dragon_0x65_v1", b)
            self.assertNotEqual(res_a.family_id, res_f.family_id)
            self.assertAlmostEqual(fixed["foot_l"][14] - alpha["foot_l"][14], -10.0, places=3)   # z: 1 -> -9
            self.assertAlmostEqual(fixed["head"][14], alpha["head"][14], places=5)
            # a worker asked for the Alpha body while the correction is on must refuse
            bad = (str(CUBE), str(SIZES), 0x65, (0, 0), (0, 0), res_a.parts, res_a.family_id, "", 16, a, 37,
                   "alpha_actions/creature_ai_d64_dragon_0x65", "dragon_0x65_v1")
            with self.assertRaises(ValueError):
                ab._bake_one(bad)


if __name__ == "__main__":
    unittest.main()
