"""D61 COLOR/TINT-FIX: Alpha marker transparency and hair tint rule (voxel_markers.py).

Synthetic cases always run (TEST_DATA_NOT_ALPHA); checks against the original executable and the
local model database run only where the local copy exists (.scratch, never committed)."""
from __future__ import annotations

import json
import sqlite3
import struct
import unittest
from pathlib import Path

from tools.alpha_models.universal import voxel_markers as vm

ROOT = Path(__file__).resolve().parents[3]
EXE = ROOT / ".scratch" / "alphatest2" / "Cube.exe"
DB = ROOT / ".scratch" / "alphatest2" / "data1.db"
CAPTURE = ROOT / ".scratch" / "s05a2" / "cap" / "session2.jsonl"


def cubdec(w, d, h, voxels):
    b = bytearray(struct.pack("<3i", w, d, h) + bytes(w * d * h * 3))
    for (x, y, z), c in voxels.items():
        o = 12 + ((z * d + y) * w + x) * 3
        b[o:o + 3] = bytes(c)
    return bytes(b)


class Synthetic(unittest.TestCase):
    def test_strict_model_draws_markers_as_colours(self):
        dec = cubdec(2, 1, 1, {(0, 0, 0): (255, 0, 0), (1, 0, 0): (10, 10, 10)})
        s = vm.mesh_stats(dec, strict=True)
        self.assertEqual(s["faces"], 10)            # two touching cubes: 12 - 2 shared faces
        self.assertEqual(s["markers"], [1, 0, 0])

    def test_marker_is_invisible_but_not_a_hole(self):
        dec = cubdec(2, 1, 1, {(0, 0, 0): (255, 0, 0), (1, 0, 0): (10, 10, 10)})
        s = vm.mesh_stats(dec, strict=False)
        self.assertEqual(s["hidden_voxels"], 1)
        self.assertEqual(s["faces"], 5)             # the face toward the marker is NOT emitted

    def test_only_exact_pure_colours_are_markers(self):
        for c in [(254, 0, 0), (255, 0, 1), (200, 0, 0), (255, 255, 255), (0, 255, 255)]:
            self.assertFalse(vm.transparent(c, strict=False), c)
        for c in vm.MARKERS:
            self.assertTrue(vm.transparent(c, strict=False))
            self.assertFalse(vm.transparent(c, strict=True))
        self.assertTrue(vm.transparent((0, 0, 0), strict=True))

    def test_flag_is_read_from_the_push_before_two_register_pushes(self):
        # push 1 ; mov ecx,[ecx+0x2420] ; push edi ; push eax ; mov byte [ebp-4],4 ; (call)
        win = bytes.fromhex("90" * 6 + "6a01" + "8b8920240000" + "57" + "50" + "c645fc04")
        self.assertEqual(vm._flag_before(win), 1)
        # push 0 ; push edi ; push esi ; mov ecx,[eax]
        win = bytes.fromhex("90" * 10 + "6a00" + "57" + "56" + "8b08")
        self.assertEqual(vm._flag_before(win), 0)
        self.assertIsNone(vm._flag_before(bytes.fromhex("90" * 20)))

    def test_hair_tint_is_an_instance_input_never_invented(self):
        app = {"hair_rgb": [0, 0, 0], "flags": 0}
        self.assertEqual(vm.part_colour("hair", app)["tint"], [])
        self.assertIn("NOT_SET", vm.part_colour("hair", app)["tintSource"])
        self.assertEqual(vm.part_colour("hair", app, [255, 200, 50])["tint"], [255, 200, 50])
        self.assertEqual(vm.part_colour("head", app, [255, 200, 50])["tint"], [])
        self.assertFalse(vm.part_colour("hair", {"hair_rgb": [1, 2, 3], "flags": 0x400})["tintMultipliesCreatureColour"])

    def test_fixed_rule_wins_over_any_job_colour(self):
        rule = {"rule": vm.HAIR_RULE_FIXED, "fixed": [255, 255, 255]}
        c = vm.part_colour("hair", {"flags": 0}, [1, 2, 3], rule)
        self.assertEqual((c["tint"], c["tintMode"]), ([255, 255, 255], "HAIR_FIXED_BY_KIND"))

    def test_msvc_rand_and_server_draw(self):
        g = vm.msvc_rand_stream(1)                     # CRT default seed: 41, 18467, 6334, 26500, 19169
        r = [next(g) for _ in range(5)]
        self.assertEqual(r, [41, 18467, 6334, 26500, 19169])
        d = vm.server_npc_draw(r)
        self.assertEqual(d["hair_rgb"], [6334 & 255, 18467 & 255, 41])
        self.assertEqual(d["seed_variant"], [0, 69])

    def test_ctab_parser_on_a_synthetic_shader(self):
        # D3DXSHADER_CONSTANTTABLE (7 dwords) + one D3DXSHADER_CONSTANTINFO (20 bytes) + name
        ct = struct.pack("<7I", 28, 0, 0xFFFE0300, 1, 28, 0, 0) + struct.pack("<IHHHHII", 48, 2, 32, 1, 0, 0, 0)
        ct += b"materialColor" + bytes(3)
        body = b"CTAB" + ct
        blob = struct.pack("<II", 0xFFFE0300, 0xFFFE | (len(body) // 4) << 16) + body + struct.pack("<I", 0x0000FFFF)
        self.assertEqual(vm.shader_constants(blob)["materialColor"], (2, 32, 1))

    def test_hair_evidence_is_live_capture_data(self):
        ev = vm.load_hair_evidence()
        self.assertEqual(ev["format"], "cubegame.hair_colour_evidence/1")
        human = {tuple(s["hair_rgb"]) for s in ev["samples"] if s["kind"] == "0x02"}
        self.assertIn((255, 200, 50), human)
        self.assertGreaterEqual(len(human), 3)        # NPCs of one kind carry different colours
        self.assertTrue(all(len(s["capture_sha256"]) == 64 for s in ev["samples"]))


@unittest.skipUnless(EXE.exists(), "local Alpha copy not present")
class AgainstAlphaCode(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        from tools.alpha_models.anim import pe_bytes
        cls.image = pe_bytes.load(str(EXE))

    def test_python_rule_equals_the_executed_match_transparent_color(self):
        cols = [(0, 0, 0), (255, 0, 0), (0, 255, 0), (0, 0, 255), (254, 0, 0), (255, 255, 255), (200, 0, 0),
                (255, 0, 1), (0, 255, 255), (1, 0, 0)]
        for strict in (False, True):
            for c in cols:
                self.assertEqual(vm.transparent_by_alpha_code(self.image, c, strict), vm.transparent(c, strict),
                                 (c, strict))

    def test_uniform30_is_vs_c32_material_colour_and_multiplies(self):
        ev = vm.material_colour_evidence(self.image)
        self.assertEqual(ev["vs_version"], 0xFFFE0300)
        self.assertEqual(ev["materialColor"], (2, 32, 1))          # float4 register c32
        self.assertTrue(ev["init_sets_reg_0x20_at_0x30"] and ev["upload_reads_0x30"] and ev["upload_calls_vtable_0x178"])

    def test_hair_rule_per_kind_by_client_code(self):
        from tools.alpha_models.universal import alpha_static as st
        emu = st.AppearanceEmulator(self.image)
        for k in (0x02, 0x03, 0x00, 0x01, 0x07, 0x08):
            self.assertEqual(vm.hair_rule(emu, k)["rule"], vm.HAIR_RULE_RANDOM, hex(k))
        for k in (0x0D, 0x0E):
            self.assertEqual(vm.hair_rule(emu, k), {"rule": vm.HAIR_RULE_FIXED, "fixed": [255, 255, 255]}, hex(k))

    def test_loader_flags_hair_0_heads_bodies_1(self):
        r = vm.load_flags(self.image)
        self.assertEqual(r["sites"], 2557)
        self.assertLessEqual(len(r["problems"]), 5)
        f = r["flags"]
        self.assertTrue(all(len(v) == 1 for v in f.values()))
        for n in ("human-hair-m01.cub", "elf-hair-f01.cub", "lizard-hair-m01.cub", "frogman-hair-m01.cub"):
            self.assertEqual(f[n], {0}, n)
        for n in ("human-head-m01.cub", "body1.cub", "cat-head.cub", "lizard-head-m01.cub"):
            self.assertEqual(f[n], {1}, n)


SERVER = ROOT / ".scratch" / "alphatest2" / "Server.exe"


@unittest.skipUnless(SERVER.exists() and EXE.exists(), "local Alpha server/client copy not present")
class AgainstServerCode(unittest.TestCase):
    def test_server_spawn_rule_equals_executed_server_bytes_and_client(self):
        from tools.alpha_models.anim import pe_bytes
        from tools.alpha_models.universal import alpha_static as st
        srv = pe_bytes.load(str(SERVER))
        emu = st.AppearanceEmulator(pe_bytes.load(str(EXE)))
        for state in (1, 12345, 0xDEADBEEF):
            g = vm.msvc_rand_stream(state)
            rands = [next(g) for _ in range(8)]
            want = vm.server_npc_draw(rands)
            for k in (0x02, 0x03, 0x00, 0x01, 0x07, 0x08, 0x0D, 0x0E, 0x1E, 0x3F):
                app, used = vm.server_npc_by_alpha_code(srv, k, rands)
                self.assertEqual(used, 5)
                client = st.decode_appearance(emu.run(k, *want["seed_variant"], hair=tuple(want["hair_rgb"]))["raw"])
                self.assertEqual(app, client, hex(k))
                rule = vm.hair_rule(emu, k)
                expect = want["hair_rgb"] if rule["rule"] == vm.HAIR_RULE_RANDOM else rule["fixed"]
                self.assertEqual(app["hair_rgb"], expect, hex(k))


@unittest.skipUnless(DB.exists() and CAPTURE.exists(), "local model database / capture not present")
class AgainstCapture(unittest.TestCase):
    def test_captured_player_hair_mesh_vertex_count(self):
        from tools.alpha_models.transfer.prepare_unity import deobfuscate
        con = sqlite3.connect(f"file:{DB}?mode=ro&immutable=1", uri=True)
        dec = deobfuscate(con.execute("select value from blobs where key=?", ("human-hair-m01.cub",)).fetchone()[0])
        rec = json.loads(CAPTURE.read_text(encoding="utf-8").splitlines()[0])
        hair = next(p for p in rec["parts"] if p["draw_site"] == "0x420A38")
        hdr = bytes.fromhex(hair["mesh_hdr"])
        alpha_vertices = struct.unpack_from("<I", hdr, 0x3C)[0]
        self.assertEqual(hdr[0x55], 0)                                   # model+0x55: loaded with flag 0
        lists = [(struct.unpack_from("<I", hdr, o + 4)[0] - struct.unpack_from("<I", hdr, o)[0]) // 12
                 for o in (4, 0x10, 0x1C)]
        s = vm.mesh_stats(dec, strict=False)
        self.assertEqual(lists, s["markers"])                          # 298 / 4 / 10
        self.assertEqual(s["vertices"], alpha_vertices)                # 2244
        self.assertNotEqual(vm.mesh_stats(dec, strict=True)["vertices"], alpha_vertices)


if __name__ == "__main__":
    unittest.main()
