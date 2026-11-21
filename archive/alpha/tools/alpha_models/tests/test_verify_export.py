"""Tests for `verify_export.py` — per-face matching and its negative cases.

Every test builds a synthetic voxel model (no Cube World assets), converts it with
`cub_to_obj.build_obj`, optionally damages the OBJ text, and checks that the
verifier accepts or rejects it for the *right* reason.
"""

from __future__ import annotations

import re
import struct
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from cub_format import parse_decoded  # noqa: E402
from cub_to_obj import build_obj  # noqa: E402
from verify_export import verify  # noqa: E402


def payload(width: int, depth: int, height: int, solid: dict[tuple[int, int, int], tuple[int, int, int]]):
    pixels = bytearray(width * depth * height * 3)
    for (x, d, h), rgb in solid.items():
        offset = ((h * depth + d) * width + x) * 3
        pixels[offset:offset + 3] = bytes(rgb)
    return struct.pack("<iii", width, depth, height) + bytes(pixels)


def bar_model():
    """3x1x1 bar of three different colours: 12 faces, asymmetric."""
    return payload(3, 1, 1, {
        (0, 0, 0): (10, 0, 0),
        (1, 0, 0): (0, 20, 0),
        (2, 0, 0): (0, 0, 30),
    })


def slab_model():
    """2x2x1 slab: 16 faces over four distinct colours."""
    return payload(2, 2, 1, {
        (0, 0, 0): (10, 10, 10),
        (1, 0, 0): (20, 20, 20),
        (0, 1, 0): (30, 30, 30),
        (1, 1, 0): (40, 40, 40),
    })


class ObjText:
    """Tiny OBJ rewriter that keeps `v` lines intact and edits material blocks."""

    def __init__(self, text: str):
        self.header: list[str] = []
        self.vertices: list[str] = []
        self.blocks: list[list[str]] = []  # [["usemtl c001", "f ...", ...], ...]
        self._parse(text)

    def _parse(self, text: str) -> None:
        current = None
        for line in text.splitlines():
            if line.startswith("v "):
                self.vertices.append(line)
            elif line.startswith("usemtl "):
                current = [line]
                self.blocks.append(current)
            elif line.startswith("f ") and current is not None:
                current.append(line)
            elif line.startswith(("g ", "o ", "mtllib ", "#")) or not line.strip():
                self.header.append(line)

    @property
    def face_lines(self) -> list[tuple[int, str]]:
        return [(b, line) for b, block in enumerate(self.blocks)
                for line in block if line.startswith("f ")]

    def render(self) -> str:
        out = ["# mutated OBJ for tests"]
        out.extend(line for line in self.header if line.startswith(("mtllib", "o ")))
        out.extend(self.vertices)
        for block in self.blocks:
            out.extend(block)
        return "\n".join(out) + "\n"

    # --- mutations -------------------------------------------------------
    def remove_face(self, index: int) -> "ObjText":
        block, _ = self.face_lines[index]
        for line in self.blocks[block]:
            if line.startswith("f "):
                self.blocks[block].remove(line)
                break
        return self

    def duplicate_face(self, index: int) -> "ObjText":
        block, line = self.face_lines[index]
        self.blocks[block].append(line)
        return self

    def flip_winding(self, index: int) -> "ObjText":
        block, line = self.face_lines[index]
        parts = line.split()
        flipped = " ".join([parts[0], parts[1], parts[4], parts[3], parts[2]])
        position = self.blocks[block].index(line)
        self.blocks[block][position] = flipped
        return self

    def move_face_to_other_material(self, index: int) -> "ObjText":
        block, line = self.face_lines[index]
        target = (block + 1) % len(self.blocks)
        self.blocks[block].remove(line)
        self.blocks[target].append(line)
        return self

    def move_face_square(self, index: int, corners: tuple[tuple[int, int, int], ...]) -> "ObjText":
        """Re-point one quad at an arbitrary square given by four integer corners."""
        block, line = self.face_lines[index]
        new_indices = []
        for (x, y, z) in corners:
            self.vertices.append(f"v {x:.6f} {y:.6f} {z:.6f}")
            new_indices.append(len(self.vertices))
        replacement = "f " + " ".join(str(i) for i in new_indices)
        position = self.blocks[block].index(line)
        self.blocks[block][position] = replacement
        return self

    def shuffle_faces(self) -> "ObjText":
        for block in self.blocks:
            faces = [line for line in block if line.startswith("f ")]
            faces.reverse()
            mutable = [line for line in block if not line.startswith("f ")]
            self.blocks[self.blocks.index(block)] = mutable + faces
        return self


class VerifierCase(unittest.TestCase):
    """Shared harness: write a synthetic .cubdec/.obj/.mtl and verify it."""

    model_payload = staticmethod(bar_model)

    def setUp(self):
        self._tmp = tempfile.TemporaryDirectory()
        self.dir = Path(self._tmp.name)
        self.load_payload(self.model_payload())

    def load_payload(self, raw: bytes) -> None:
        """(Re)initialise the fixture files from a raw decoded `.cubdec` payload."""
        self.payload = raw
        self.model = parse_decoded(raw)
        obj_text, mtl_text = build_obj(self.model, "synthetic")
        self.obj_text = obj_text
        self.mtl_text = mtl_text
        (self.dir / "synthetic.cubdec").write_bytes(raw)
        (self.dir / "synthetic.mtl").write_text(mtl_text, encoding="utf-8")

    def tearDown(self):
        self._tmp.cleanup()

    def run_verify(self, obj_text: str | None = None):
        obj_path = self.dir / "synthetic.obj"
        obj_path.write_text(obj_text if obj_text is not None else self.obj_text, encoding="utf-8")
        return verify(self.dir / "synthetic.cubdec", obj_path, self.dir / "synthetic.mtl")

    def assert_fail(self, obj_text, needle: str):
        ok, problems, stats = self.run_verify(obj_text)
        self.assertFalse(ok, f"expected FAIL, got problems={problems}")
        joined = " | ".join(problems)
        self.assertIn(needle, joined, f"expected message containing {needle!r}, got: {joined}")
        return problems, stats

    def assert_pass(self, obj_text=None):
        ok, problems, stats = self.run_verify(obj_text)
        self.assertTrue(ok, f"expected PASS, got problems={problems}")
        return stats


class PositiveTests(VerifierCase):
    def test_untouched_export_passes(self):
        stats = self.assert_pass()
        self.assertEqual(stats["faces"], stats["expected_faces"])
        self.assertEqual(stats["missing"], 0)
        self.assertEqual(stats["extra"], 0)
        self.assertEqual(stats["duplicated"], 0)
        self.assertEqual(stats["inverted"], 0)
        self.assertEqual(stats["wrong_material"], 0)

    def test_reordered_faces_pass(self):
        self.assert_pass(ObjText(self.obj_text).shuffle_faces().render())

    def test_unchanged_slab_passes(self):
        self.load_payload(slab_model())
        self.assert_pass()


class NegativeFaceSetTests(VerifierCase):
    def test_removed_face_fails(self):
        self.assert_fail(ObjText(self.obj_text).remove_face(3).render(), "missing from OBJ")

    def test_duplicated_face_fails(self):
        self.assert_fail(ObjText(self.obj_text).duplicate_face(2).render(), "written more than once")

    def test_flipped_winding_fails(self):
        self.assert_fail(ObjText(self.obj_text).flip_winding(0).render(), "inverted winding")

    def test_wrong_material_fails(self):
        self.assert_fail(ObjText(self.obj_text).move_face_to_other_material(0).render(),
                         "wrong material")


class MovedFaceTests(VerifierCase):
    """A face moved to an empty square inside the same bounding box must FAIL."""

    model_payload = staticmethod(lambda: payload(2, 2, 2, {
        (x, d, h): (10 * x + 1, 10 * d + 2, 10 * h + 3)
        for x in range(2) for d in range(2) for h in range(2)
    }))

    def test_moved_face_with_same_bounds_fails(self):
        before = self.assert_pass()
        # x=1 plane, square y in [0,1], z in [0,1] is inside the solid: not an exposed face.
        mutated = ObjText(self.obj_text).move_face_square(
            0, ((1, 0, 0), (1, 0, 1), (1, 1, 1), (1, 1, 0))).render()
        problems, stats = self.assert_fail(mutated, "does not expose")
        self.assertEqual(stats["extent"], before["extent"], "the mutation must not change the bounds")
        self.assertTrue(any("missing from OBJ" in p for p in problems), problems)


class StructuralDamageTests(VerifierCase):
    def test_truncated_obj_fails(self):
        lines = self.obj_text.splitlines()
        for index in range(len(lines) - 1, -1, -1):
            if lines[index].startswith("f "):
                del lines[index]
                break
        self.assert_fail("\n".join(lines) + "\n", "missing from OBJ")

    def test_foreign_vertex_index_fails(self):
        broken = re.sub(r"(?m)^f (\S+) (\S+) (\S+) (\S+)$",
                        lambda m: f"f {m.group(1)} {m.group(2)} {m.group(3)} 99999",
                        self.obj_text, count=1)
        ok, problems, _ = self.run_verify(broken)
        self.assertFalse(ok)
        self.assertTrue(any("references vertex" in p for p in problems), problems)

    def test_non_integer_coordinates_fail(self):
        broken = self.obj_text.replace("v 0.000000", "v 0.500000", 1)
        ok, problems, _ = self.run_verify(broken)
        self.assertFalse(ok)
        self.assertTrue(any("lattice" in p for p in problems), problems)

    def test_degenerate_quad_fails(self):
        lines = self.obj_text.splitlines()
        for index, line in enumerate(lines):
            if line.startswith("f "):
                parts = line.split()
                lines[index] = " ".join([parts[0], parts[1], parts[2], parts[3], parts[3]])
                break
        ok, problems, _ = self.run_verify("\n".join(lines) + "\n")
        self.assertFalse(ok)

    def test_wrong_mtl_colour_fails(self):
        (self.dir / "synthetic.mtl").write_text(
            self.mtl_text.replace("Kd 0.039216", "Kd 0.900000", 1), encoding="utf-8")
        ok, problems, _ = self.run_verify()
        self.assertFalse(ok)
        self.assertTrue(any("colour" in p for p in problems), problems)

    def test_missing_mtl_fails(self):
        (self.dir / "synthetic.mtl").write_text("# empty\n", encoding="utf-8")
        ok, problems, _ = self.run_verify()
        self.assertFalse(ok)
        self.assertTrue(any("MTL" in p for p in problems), problems)

    def test_empty_obj_fails(self):
        self.assert_fail("mtllib synthetic.mtl\no synthetic\n", "missing from OBJ")

    def test_empty_model_fails(self):
        empty = struct.pack("<iii", 2, 2, 2) + bytes(2 * 2 * 2 * 3)
        model = parse_decoded(empty)
        obj_text, mtl_text = build_obj(model, "synthetic")
        self.payload = empty
        self.model = model
        (self.dir / "synthetic.cubdec").write_bytes(empty)
        (self.dir / "synthetic.mtl").write_text(mtl_text, encoding="utf-8")
        ok, problems, _ = self.run_verify(obj_text)
        self.assertFalse(ok)
        self.assertTrue(any("no solid voxels" in p for p in problems), problems)


if __name__ == "__main__":
    unittest.main(verbosity=2)
