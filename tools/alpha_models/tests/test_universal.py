"""Tests of the universal Alpha -> Unity pipeline (tools/alpha_models/universal).

Two groups:
* synthetic (TEST_DATA_NOT_ALPHA) — the x86 interpreter on hand-assembled code, the universal
  rig resolver and families on made-up appearances, capture -> clip on a fake cat whose joint
  frames are known exactly, and every refusal path (wrong slot, unknown/unverified draw site,
  part set != static prediction, appearance change, foreign family clip, no clips -> FAIL);
* local evidence — only when the research copy of Cube.exe/data1.db exists in .scratch (never in
  Git): the emulated appearance tables and the model registry against values that were checked
  against runtime captures on 2026-11-05.
"""
from __future__ import annotations

import json
import math
import os
import shutil
import struct
import tempfile
import unittest

from tools.alpha_models.anim import mat4
from tools.alpha_models.universal import alpha_static as st
from tools.alpha_models.universal import creature_rig as cr
from tools.alpha_models.universal import pipeline as pl
from tools.alpha_models.universal import uclip
from tools.alpha_models.universal.x86emu import CPU, EmuError

EXE = r"D:\Projects\CubeGame\.scratch\alphatest2\Cube.exe"
DB = r"D:\Projects\CubeGame\.scratch\alphatest2\data1.db"
HAVE_ALPHA = os.path.exists(EXE) and os.path.exists(DB)


# ----------------------------------------------------------------------------- x86 interpreter
class FakeImage:
    """Minimal image: one code blob at a base VA (+ optional data)."""

    def __init__(self, base, code, data=None):
        self.mem = {base + i: b for i, b in enumerate(code)}
        for va, blob in (data or {}).items():
            for i, b in enumerate(blob):
                self.mem[va + i] = b

    def read(self, va, n):
        try:
            return bytes(self.mem[va + i] for i in range(n))
        except KeyError as e:
            raise ValueError(f"unmapped {va:#x}") from e


class X86EmuTests(unittest.TestCase):
    BASE = 0x401000

    def run_code(self, code, args=(), ecx=None, data=None, iat=None):
        cpu = CPU(FakeImage(self.BASE, code, data), iat_stubs=iat)
        return cpu, cpu.call(self.BASE, args=args, ecx=ecx)

    def test_cdecl_args_store_and_idiv_remainder(self):
        # push ebp; mov ebp,esp; mov eax,[ebp+8]; cdq; mov ecx,7; idiv ecx; mov eax,edx;
        # mov ecx,[ebp+0xc]; mov word [ecx+0x14],ax; pop ebp; ret
        code = bytes.fromhex("55 8bec 8b4508 99 b907000000 f7f9 8bc2 8b4d0c 66894114 5d c3")
        cpu, eax = self.run_code(code, args=(-23, 0x20000000))
        self.assertEqual(eax, (-23 - 7 * -3) & 0xFFFFFFFF)          # C remainder: -2
        self.assertEqual(struct.unpack("<h", cpu.mem.read(0x20000014, 2))[0], -2)

    def test_jump_table_and_conditional(self):
        # mov eax,[esp+4]; cmp eax,1; ja +7 (to default); jmp [eax*4+table]; mov eax,99; ret
        # case0: mov eax,10; ret   case1: mov eax,11; ret
        table = 0x402000
        code = bytes.fromhex("8b442404 83f801 7707 ff2485" + table.to_bytes(4, "little").hex()
                             + " b863000000 c3 b80a000000 c3 b80b000000 c3")
        case0 = self.BASE + len(bytes.fromhex("8b442404 83f801 7707 ff248500000000 b863000000 c3"))
        data = {table: case0.to_bytes(4, "little") + (case0 + 6).to_bytes(4, "little")}
        self.assertEqual(self.run_code(code, args=(0,), data=data)[1], 10)
        self.assertEqual(self.run_code(code, args=(1,), data=data)[1], 11)
        self.assertEqual(self.run_code(code, args=(5,), data=data)[1], 99)

    def test_import_needs_registered_stub(self):
        iat = 0x403000
        code = bytes.fromhex("a1" + iat.to_bytes(4, "little").hex() + " ffd0 c3")   # mov eax,[iat]; call eax
        self.assertEqual(self.run_code(code, data={iat: b"\0\0\0\0"}, iat={iat: lambda c: 1234})[1], 1234)
        with self.assertRaises(EmuError):
            self.run_code(code, data={iat: b"\0\0\0\0"})                          # no stub: never guessed

    def test_sse_float_copy_and_divide(self):
        # movss xmm0,[c1]; movss xmm1,[c2]; divss xmm0,xmm1; mov ecx,[esp+4]; movss [ecx],xmm0; ret
        c1, c2 = 0x404000, 0x404004
        code = bytes.fromhex("f30f1005" + c1.to_bytes(4, "little").hex() + " f30f100d" + c2.to_bytes(4, "little").hex()
                             + " f30f5ec1 8b4c2404 f30f1101 c3")
        cpu, _ = self.run_code(code, args=(0x20000000,), data={c1: struct.pack("<f", 9.0), c2: struct.pack("<f", 4.0)})
        self.assertAlmostEqual(struct.unpack("<f", cpu.mem.read(0x20000000, 4))[0], 2.25)

    def test_unknown_opcode_is_an_error_not_a_guess(self):
        with self.assertRaises(EmuError):
            self.run_code(bytes.fromhex("0f0b c3"))      # ud2


# ----------------------------------------------------------------------------- rig / families
def appearance(slots, **over):
    a = {"flags": 0x31, "hair_rgb": [0, 0, 0], "size": [0.96, 0.96, 2.16],
         "slots": {n: -1 for n, *_ in st.SLOTS}}
    a["slots"].update(slots)
    for _, n in st.SCALES:
        a[n] = 1.0
    for _, n in st.PITCHES:
        a[n] = 0.0
    for _, n in st.OFFSETS:
        a[n] = [0.0, 0.0, 0.0]
    a.update(over)
    return a


CAT = {"head": 408, "hand": 409, "foot": 410, "body": 407, "tail": 411}


class RigResolverTests(unittest.TestCase):
    def test_slots_expand_to_draw_blocks(self):
        r = cr.resolve(0x20, appearance(CAT))
        self.assertEqual(r.parts, ["body", "tail", "head", "hand_l", "hand_r", "foot_l", "foot_r"])
        self.assertTrue(r.supported)
        self.assertEqual(r.models["hand_r"], 409)

    def test_same_params_same_family_regardless_of_models(self):
        a = cr.resolve(0x1e, appearance({**CAT, "head": 398, "body": 397}))
        b = cr.resolve(0x20, appearance(CAT))
        self.assertEqual(a.family_id, b.family_id)

    def test_any_motion_param_splits_family(self):
        base = cr.resolve(0x20, appearance(CAT))
        for k, v in (("scale_tail", 1.1), ("offset_foot", [3.0, -4.1, -8.5]), ("flags", 0x30), ("pitch_tail", -30.0)):
            self.assertNotEqual(cr.resolve(0x20, appearance(CAT, **{k: v})).family_id, base.family_id, k)

    def test_part_set_splits_family(self):
        self.assertNotEqual(cr.resolve(0x20, appearance(CAT)).family_id,
                            cr.resolve(0x20, appearance({k: v for k, v in CAT.items() if k != "tail"})).family_id)

    def test_equipment_is_a_layer_not_a_family(self):
        a = cr.resolve(2, appearance({"head": 1, "hair": 2, "hand": 3, "foot": 4, "body": 5}))
        b = cr.resolve(2, appearance({"head": 1, "hair": 2, "hand": 3, "foot": 4, "body": 5}), equipment=("weapon",))
        self.assertEqual(a.family_id, b.family_id)
        self.assertIn("weapon", b.parts)

    def test_unverified_block_and_kind_branch_are_exceptions(self):
        wings = cr.resolve(0x37, appearance({"body": 1, "head": 2, "foot": 3, "slot_9a": 4}))
        self.assertFalse(wings.supported)
        self.assertTrue(any("UNVERIFIED_DRAW_BLOCK" in e for e in wings.exceptions))
        mole = cr.resolve(0x57, appearance(CAT))
        self.assertTrue(any("KIND_SPECIFIC_CODE" in e for e in mole.exceptions))
        self.assertFalse(cr.resolve(0x50, appearance({})).supported)          # no parts at all

    def test_anchor_rules(self):
        self.assertEqual(cr.anchor("tail", (4, 5, 10)), (2.0, 5.0, 5.0))
        self.assertEqual(cr.anchor("head", (14, 15, 10), kind=0x20), (7.0, 7.5, 5.0))
        self.assertEqual(cr.anchor("head", (10, 10, 10), kind=0x65), (5.0, 2.0, 5.0))
        with self.assertRaises(ValueError):
            cr.anchor("unknown", (1, 1, 1))


# ----------------------------------------------------------------------------- capture -> clip
SIZES = {"body": (14, 16, 8), "tail": (4, 5, 10), "head": (14, 15, 10), "hand_l": (7, 6, 8),
         "hand_r": (7, 6, 8), "foot_l": (7, 6, 8), "foot_r": (7, 6, 8)}


def rot_x(deg):
    c, s = math.cos(math.radians(deg)), math.sin(math.radians(deg))
    return [1, 0, 0, 0, 0, c, s, 0, 0, -s, c, 0, 0, 0, 0, 1.0]


def true_joint(name, phase):
    base = {"body": (0, 0, -4), "tail": (0, -8, -1), "head": (0, 9, -1), "hand_l": (-3, 4, -8.5),
            "hand_r": (3, 4, -8.5), "foot_l": (-3, -4.1, -8.5), "foot_r": (3, -4.1, -8.5)}[name]
    sx = -1.0 if name == "hand_r" else 1.0
    j = mat4.mul([sx, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1.0], rot_x(25 * math.sin(phase + len(name))))
    return mat4.mul(j, mat4.translate(base[0], base[1] + 2 * math.sin(phase), base[2]))


def cat_appearance_bytes(over=None):
    raw = bytearray(st.APPEARANCE_SIZE)
    struct.pack_into("<H", raw, 6, 0x31)
    struct.pack_into("<3f", raw, 8, 0.96, 0.96, 2.16)
    for n, off, *_ in st.SLOTS:
        struct.pack_into("<h", raw, off, CAT.get(n, -1))
    for off, _ in st.SCALES:
        struct.pack_into("<f", raw, off, 1.0)
    for k, v in (over or {}).items():
        struct.pack_into("<f", raw, k, v)
    return bytes(raw)


def record(phase, t, amp=0.19, kind=0x20, app=None, site_over=None, slot_over=None, drop=None):
    P = mat4.mul([0.0857, 0, 0, 0, 0, 0.0857, 0, 0, 0, 0, 0.0857, 0, 0, 0, 0, 1.0], mat4.translate(3, 4, 150))
    ch = bytearray(0x180)
    struct.pack_into("<I", ch, 0x64, kind)
    ch[0x78:0x78 + st.APPEARANCE_SIZE] = app or cat_appearance_bytes()
    this = bytearray(0x100)
    parts = []
    for name in cr.DRAW_ORDER:
        if name not in SIZES or name == drop:
            continue
        blk = cr.BY_NAME[name]
        a = cr.anchor(blk.anchor, SIZES[name], kind=kind)
        m = mat4.mul(mat4.mul(mat4.translate(-a[0], -a[1], -a[2]), true_joint(name, phase)), P)
        hdr = bytearray(0x60)
        struct.pack_into("<i", hdr, 0x3C, 100)
        struct.pack_into("<3i", hdr, 0x44, *SIZES[name])
        site = (site_over or {}).get(name, blk.draw_site)
        slot = (slot_over or {}).get(name, f"0x{blk.slot:02X}")
        parts.append({"upload_site": f"0x{blk.upload_site:06X}", "draw_site": f"0x{site:06X}", "matrix": m,
                      "mesh": "0x1", "mesh_hdr": hdr.hex(), "slot": slot})
    return {"t": t, "creature": "0x5", "kind": kind, "this": "0x2", "state": 0, "indices": [],
            "fields": {"type_68": 0, "amp_1188": amp, "phase_118c": phase},
            "this_hdr": this.hex(), "creature_hdr": ch.hex(), "esp_body": "0x0", "frame_check": True,
            "placement": P, "root": P, "parts": parts}


def write(records):
    fd, path = tempfile.mkstemp(suffix=".jsonl")
    with os.fdopen(fd, "w", encoding="utf-8") as fh:
        for r in records:
            fh.write(json.dumps(r) + "\n")
    return path


def walk_capture(n=400, rate=5.0, dt=1 / 70.0, **kw):
    return [record(1.0 + rate * i * dt, i * dt, **kw) for i in range(n)]


class UniversalClipTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.mkdtemp()

    def tearDown(self):
        shutil.rmtree(self.tmp, ignore_errors=True)

    def test_joints_recovered_exactly_and_static_prediction_matches(self):
        frames = uclip.load_capture(write([record(0.3, 0.0)]), kind=0x20)
        res = uclip.frame_resolution(frames[0])
        self.assertTrue(uclip.usable(frames[0], res))
        for n in SIZES:
            self.assertLess(mat4.max_abs_diff(uclip.joint(frames[0], n), true_joint(n, 0.3)), 1e-9)
        self.assertLess(uclip.roundtrip(frames, res), 1e-9)

    def test_wrong_slot_unknown_and_unverified_sites_are_rejected(self):
        for kw, code in (({"slot_over": {"tail": "0x2F"}}, "SLOT_MISMATCH"),
                         ({"site_over": {"tail": 0x41F8BD}}, "UNKNOWN_DRAW_SITE"),
                         ({"site_over": {"tail": 0x4238C5}}, "UNVERIFIED_BLOCK")):
            fr = uclip.load_capture(write([record(0.3, 0.0, **kw)]))[0]
            self.assertTrue(any(p.startswith(code) for p in fr.problems), (code, fr.problems))
            self.assertFalse(uclip.usable(fr, uclip.frame_resolution(fr)))

    def test_missing_part_is_not_usable(self):
        fr = uclip.load_capture(write([record(0.3, 0.0, drop="tail")]))[0]
        self.assertFalse(uclip.usable(fr, uclip.frame_resolution(fr)))

    def test_walk_clip_holdout_and_build_pass(self):
        cap = write(walk_capture())
        rep = pl.build_clips(cap, 0x20, self.tmp)
        self.assertEqual(rep["BUILD_RESULT"], "PASS", rep)
        clip = json.load(open(rep["clips_written"][0], encoding="utf-8"))
        self.assertEqual(clip["rig"], cr.resolve(0x20, st.decode_appearance(cat_appearance_bytes())).family_id)
        self.assertEqual(clip["status"], "TRANSFERRED_FROM_ALPHA")
        self.assertLess(rep["walk"]["holdout"]["max_rotation_deg"], 0.5)
        self.assertAlmostEqual(rep["walk"]["phase_rate_rad_per_s"], 5.0, places=6)

    def test_non_repeatable_walk_is_refused(self):
        recs = walk_capture()
        for i, r in enumerate(recs):       # corrupt: the pose depends on time, not on phase
            r["parts"][0]["matrix"] = mat4.mul(rot_x(40 * math.sin(i * 0.37)), r["parts"][0]["matrix"])
        rep = pl.build_clips(write(recs), 0x20, self.tmp)
        self.assertEqual(rep["BUILD_RESULT"], "FAIL")
        self.assertTrue(any(p.startswith("WALK_NOT_REPEATABLE") for p in rep["problems"]), rep["problems"])
        self.assertEqual(rep["clips_written"], [])

    def test_appearance_change_is_a_problem(self):
        recs = walk_capture()
        recs[-1] = record(recs[-1]["fields"]["phase_118c"], recs[-1]["t"], app=cat_appearance_bytes({0x3C: 1.1}))
        rep = pl.build_clips(write(recs), 0x20, self.tmp)
        self.assertEqual(rep["BUILD_RESULT"], "FAIL")
        self.assertIn("APPEARANCE_CHANGED_DURING_CAPTURE", rep["problems"])

    def test_capture_without_walk_writes_nothing(self):
        rep = pl.build_clips(write([record(0.3, i / 70.0, amp=0.0) for i in range(5)]), 0x20, self.tmp)
        self.assertEqual(rep["walk"]["status"], "NOT_CAPTURED")
        # idle is written (static frame) but a walk is never claimed
        self.assertTrue(all(p.endswith("idle.alphaclip.json") for p in rep["clips_written"]))


@unittest.skipUnless(HAVE_ALPHA, "local research copy of Alpha (.scratch) not present")
class LocalEvidenceTests(unittest.TestCase):
    """Checked against live captures on 2026-11-05 (pose_capture --kinds 0x20,0x3f,0x2)."""

    @classmethod
    def setUpClass(cls):
        from tools.alpha_models.anim import pe_bytes as pb
        cls.image = pb.load(EXE)
        cls.emu = st.AppearanceEmulator(cls.image)

    def test_cat_appearance_equals_runtime_values(self):
        d = st.decode_appearance(self.emu.run(0x20)["raw"])
        self.assertEqual(d["slots"], {"head": 408, "hair": -1, "hand": 409, "foot": 410, "body": 407,
                                      "tail": 411, "slot_98": -1, "slot_9a": -1})
        self.assertEqual(d["flags"], 0x31)
        self.assertEqual(d["offset_head"], [0.0, 9.0, -1.0])
        self.assertEqual(d["offset_foot"], [3.0, -4.1, -8.5])
        self.assertEqual(d["pitch_tail"], -30.0)
        self.assertAlmostEqual(d["scale_tail"], 1.1, places=6)

    def test_human_slots_equal_runtime_player(self):
        d = st.decode_appearance(self.emu.run(0x2)["raw"])
        self.assertEqual([d["slots"][n] for n in ("head", "hair", "hand", "foot", "body")], [1246, 1252, 430, 432, 1])

    def test_cat_family_is_shared_by_three_kinds(self):
        fams = {k: cr.resolve(k, st.decode_appearance(self.emu.run(k)["raw"])).family_id for k in (0x1e, 0x1f, 0x20)}
        self.assertEqual(len(set(fams.values())), 1, fams)

    def test_model_registry_resolves_captured_models(self):
        reg, stats = st.model_registry(self.image)
        self.assertEqual([reg[i] for i in (407, 408, 409, 410, 411)],
                         ["cat-body3.cub", "cat-head3.cub", "cat-hand3.cub", "cat-foot3.cub", "cat-tail3.cub"])
        self.assertEqual((reg[333], reg[334]), ("plainrunner-body.cub", "plainrunner-foot.cub"))
        self.assertEqual((reg[1], reg[430], reg[432], reg[1246]), ("body4.cub", "hand2.cub", "foot.cub", "human-head-m01.cub"))
        self.assertEqual(stats["undecoded_index"], 0)

    def test_every_kind_emulates(self):
        for k in range(0, st.KIND_MAX + 1):
            self.emu.run(k)     # raises EmuError on any unsupported instruction


class PrepareRefusalTests(unittest.TestCase):
    """prepare-unity must not produce a PASS from foreign or missing clips (no Unity needed)."""

    def test_other_exe_build_is_refused(self):
        fd, path = tempfile.mkstemp(suffix=".exe")
        os.write(fd, b"MZ not the verified build")
        os.close(fd)
        try:
            with self.assertRaises(st.UnknownBuild):
                st.verify_exe(path)
            with self.assertRaises(st.UnknownBuild):
                pl.resolve_kind(path, 0x20)
        finally:
            os.remove(path)

    @unittest.skipUnless(HAVE_ALPHA, "local research copy of Alpha (.scratch) not present")
    def test_foreign_family_clip_is_skipped_and_no_clip_fails(self):
        tmp = tempfile.mkdtemp()
        try:
            clips = os.path.join(tmp, "clips")
            os.makedirs(clips)
            json.dump({"rig": "alpha_creature/ffffffffff", "status": "TRANSFERRED_FROM_ALPHA", "name": "walk",
                       "parts": []}, open(os.path.join(clips, "walk.alphaclip.json"), "w"))
            out = pl.prepare_unity({"kind": "0x20", "clips": clips}, os.path.join(tmp, "proj"))
            self.assertEqual(out["PREPARE_RESULT"], "FAIL")
            self.assertTrue(any(p.startswith("NO_FAMILY_CLIPS") for p in out["problems"]))
            self.assertEqual(len(out["skipped_clips"]), 1)
            out2 = pl.prepare_unity({"kind": "0x37", "clips": clips}, os.path.join(tmp, "proj2"))   # Crow: wings
            self.assertTrue(any(p.startswith("UNSUPPORTED") for p in out2["problems"]))
        finally:
            shutil.rmtree(tmp, ignore_errors=True)


if __name__ == "__main__":
    unittest.main()
