"""D64 targeted checks of the full-catalog methods (no Unity; Alpha-dependent ones skip without .scratch)."""
from __future__ import annotations

import json
import os
import struct
import tempfile
import unittest
from pathlib import Path

from tools.alpha_models.universal import creature_rig as cr
from tools.alpha_models.universal.x86emu import CPU

ROOT = Path(__file__).resolve().parents[3]
EXE = ROOT / ".scratch" / "alphatest2" / "Cube.exe"
DB = ROOT / ".scratch" / "alphatest2" / "data1.db"
HAVE_ALPHA = EXE.exists() and DB.exists()


class _Img:
    """Tiny flat image for interpreter tests."""
    def __init__(self, code, base=0x401000):
        self.code, self.base = code, base

    def read(self, va, n):
        o = va - self.base
        if o < 0 or o + n > len(self.code):
            raise ValueError("unmapped")
        return self.code[o:o + n]


class InterpreterTests(unittest.TestCase):
    def test_cmovcc_moves_only_when_condition_holds(self):
        # mov eax,1 ; mov ecx,7 ; cmp eax,2 ; cmovl eax,ecx ; cmp eax,2 ; cmovl eax,ecx(no) ; ret
        code = bytes.fromhex("b801000000" "b907000000" "83f802" "0f4cc1" "83f802" "0f4cc1" "c3")
        cpu = CPU(_Img(code))
        self.assertEqual(cpu.call(0x401000), 7)


class AnchorRuleTests(unittest.TestCase):
    def test_equipment_anchor_laws(self):
        s = (6, 8, 10)
        self.assertEqual(cr.anchor("neck", s), (3.0, 0.0, 10.0))
        self.assertEqual(cr.anchor("lamp", s), (3.0, 4.0, 10.0))
        self.assertEqual(cr.anchor("pet", s), (3.0, 4.0, 0.0))
        for b in ("neck_item", "shoulder_item", "offhand_a", "quiver", "special_item", "held_item", "pet_item", "lamp_item"):
            self.assertEqual(cr.BY_NAME[b].status, "VERIFIED_EMULATION", b)
        self.assertEqual(cr.BY_NAME["offhand_b"].anchor, "by_subtype")
        self.assertEqual([b.name for b in cr.BLOCKS if b.status == "UNVERIFIED"], [])
        self.assertEqual(cr.equipment_anchor("weapon", 12), "tail")          # bracelet worn: (sx/2, sy, sz/2)
        self.assertEqual(cr.equipment_anchor("weapon", 15), "weapon")
        self.assertEqual(cr.equipment_anchor("offhand_b", 20), "pet")
        self.assertEqual(cr.equipment_anchor("offhand_b", 0), "center")    # golem's thrown rock (state 71)
        with self.assertRaises(ValueError):
            cr.equipment_anchor("offhand_b", 13)

    def test_unity_runtime_knows_every_python_rule(self):
        cs = (ROOT / "tools/alpha_models/transfer/unity/Runtime/AlphaRig.cs").read_text(encoding="utf-8")
        rules = {b.anchor for b in cr.BLOCKS} - {"head", "unknown", "by_subtype"}
        rules |= {r for t in cr.EQUIPMENT_ANCHOR_BY_SUBTYPE.values() for r in t.values()}
        for rule in rules:
            self.assertIn(f'rule == "{rule}"', cs, rule)


@unittest.skipUnless(HAVE_ALPHA, "local research copy of Alpha (.scratch) not present")
class RegistryTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        from tools.alpha_models.anim import pe_bytes as pb
        from tools.alpha_models.universal import alpha_static as st
        cls.image = pb.load(str(EXE))
        cls.reg, cls.stats, cls.info = st.model_registry(cls.image, detail=True)

    def test_every_load_is_decoded(self):
        s = self.stats
        self.assertEqual((s["loads"], s["indexed"], s["overwritten_loads"]), (2557, 2550, 7))
        self.assertEqual((s["member_slot"], s["undecoded_index"], s["no_name"], s["several_names"]), (0, 0, 0, 0))

    def test_b3_fixes(self):
        # 1300: two loads, the later one is what Alpha keeps (OrcMale head)
        self.assertEqual(self.reg[1300], "orc-head.cub")
        self.assertEqual([o["name"] for o in self.info[1300]["overwritten"]], ["orc-head-m01.cub"])
        # 29: dark-troll eyes, loaded with flag 0 (push 0) -> DarkTroll hair slot
        self.assertEqual((self.reg[29], self.info[29]["load_flag"]), ("dark-troll-eyes.cub", 0))
        # 2564: a generator loop between the vector calls; only the last one feeds the load
        self.assertEqual(self.reg[2564], "sphinx01.cub")

    def test_load_flags_equal_d61_table(self):
        from tools.alpha_models.universal import voxel_markers as vm
        lf = vm.load_flags(self.image)["flags"]
        diff = [i for i, v in self.info.items() if v["name"] in lf and lf[v["name"]] != {v["load_flag"]}]
        self.assertEqual(diff, [])

    def test_item_selector_is_executed(self):
        from tools.alpha_models.catalog import item_models as im
        sel = im.Selector(self.image)

        def name(t, sub=0, mat=0, b0c=0, seed=0):
            idx, _, err = sel(im._item({0: t, 1: sub, 0xD: mat, 0xC: b0c}, seed))
            self.assertIsNone(err)
            return self.reg.get(idx) if idx is not None else None
        self.assertIn("greatsword", name(3, 15))
        self.assertIn("shield", name(3, 13))
        self.assertEqual(name(23, 0), "glider.cub")
        self.assertEqual(name(23, 1), "boat2.cub")
        self.assertIsNone(name(0))
        self.assertEqual(name(17, seed=1), self.reg[2114])       # seed % 4 selector (period found by the sweep)


@unittest.skipUnless(HAVE_ALPHA, "local research copy of Alpha (.scratch) not present")
class EvidenceTests(unittest.TestCase):
    def test_walk_oracle_evidence_covers_every_kind(self):
        from tools.alpha_models.catalog import walk_oracle as wo
        ev = wo.load_evidence()
        self.assertEqual(len(ev), 156)
        self.assertTrue(all(r["status"] == "ORACLE_VERIFIED" for r in ev.values()))
        for k in ("0x57", "0x60", "0x65"):
            self.assertEqual(cr.walk_oracle_status(int(k, 16)), "ORACLE_VERIFIED")

    def test_hands_ignore_root_pitch(self):
        """InsectGuard 0x36 (hands + angle_48 = -45): formula == executed drawModel (D64 fix)."""
        from tools.alpha_models.anim.alpha_clip import pose_error
        from tools.alpha_models.universal import alpha_oracle as ao, param_walk as pw
        sizes = ROOT / ".scratch/catalog/registry_sizes.json"
        if not sizes.exists():
            self.skipTest("registry_sizes.json not rebuilt")
        o = ao.load(str(EXE), str(sizes))
        j, _, app = o.walk_frame(0x36, 0.8, 0.19, True)
        m = pw.joints(app, pw.WalkState(0.8, 0.19, on_ground=True, head_lean_deg=0.0), ["hand_l", "hand_r"])
        for p in ("hand_l", "hand_r"):
            e = pose_error(m[p], j[p])
            self.assertLess(e[0], 1e-4)
            self.assertLess(e[1], 1e-3)

    def test_trigger_evidence_drift_is_detected(self):
        from tools.alpha_models.catalog import trigger_catalog as tc
        if not tc.EVIDENCE_PATH.exists():
            self.skipTest("trigger evidence not exported")
        self.assertEqual(tc.evidence_drift(), [])
        ev = json.loads(tc.EVIDENCE_PATH.read_text(encoding="utf-8"))
        ev["code_sha256"]["tools/alpha_models/universal/ai_trigger.py"] = "0" * 64
        fd, p = tempfile.mkstemp(suffix=".json")
        os.close(fd)
        try:
            Path(p).write_text(json.dumps(ev), encoding="utf-8")
            self.assertTrue(any("ai_trigger.py" in x for x in tc.evidence_drift(p)))
        finally:
            os.remove(p)


@unittest.skipUnless(HAVE_ALPHA, "local research copy of Alpha (.scratch) not present")
class StaticExportTests(unittest.TestCase):
    def test_counts_follow_the_d61_marker_rule(self):
        """flag-0 model: markers are excluded from solid; the D61 mesher rule (Python mirror) agrees."""
        import sqlite3
        from tools.alpha_models.catalog import static_export as se
        from tools.alpha_models.cub_format import deobfuscate
        from tools.alpha_models.universal import voxel_markers as vm
        con = sqlite3.connect(f"file:{DB}?mode=ro&immutable=1", uri=True)
        dec = deobfuscate(con.execute("select value from blobs where key='dark-troll-eyes.cub'").fetchone()[0])
        size, solid, markers = se.counts(dec, True)
        self.assertGreater(markers, 0)
        ms = vm.mesh_stats(dec, strict=False)
        self.assertEqual(ms["hidden_voxels"], markers)
        _, solid1, markers1 = se.counts(dec, False)
        self.assertEqual((solid1, markers1), (solid + markers, 0))


if __name__ == "__main__":
    unittest.main()
