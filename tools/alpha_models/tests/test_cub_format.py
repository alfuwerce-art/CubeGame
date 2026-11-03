"""Unit tests for the `.cub` decode/convert helpers (S05A-0 pilot).

Run::

    python -m unittest discover -s tools/alpha_models/tests -t tools/alpha_models

The tests build synthetic payloads, so no Cube World assets are needed and none
are copied into the repository.
"""

from __future__ import annotations

import struct
import unittest
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from cub_format import (  # noqa: E402
    CubFormatError,
    VoxelModel,
    deobfuscate,
    encode_model,
    find_anchor_colors,
    obfuscate,
    parse_decoded,
    parse_stored,
    solid_colors,
)
from cub_to_obj import build_obj, expected_face_count, is_solid  # noqa: E402


def make_payload(width: int, depth: int, height: int, solid: dict[tuple[int, int, int], tuple[int, int, int]]):
    pixels = bytearray(width * depth * height * 3)
    for (x, d, h), (r, g, b) in solid.items():
        offset = ((h * depth + d) * width + x) * 3
        pixels[offset:offset + 3] = bytes((r, g, b))
    return struct.pack("<iii", width, depth, height) + bytes(pixels)


class ObfuscationTests(unittest.TestCase):
    def test_round_trip_is_identity(self):
        data = bytes(range(256)) * 3 + b"\x00\x01\x02"
        self.assertEqual(deobfuscate(obfuscate(data)), data)
        self.assertEqual(obfuscate(deobfuscate(data)), data)

    def test_empty_payload(self):
        self.assertEqual(deobfuscate(b""), b"")

    def test_obfuscation_actually_changes_bytes(self):
        data = bytes(range(64))
        self.assertNotEqual(deobfuscate(data), data)

    def test_round_trip_preserves_length(self):
        data = bytes((i * 7) & 0xFF for i in range(1000))
        self.assertEqual(len(deobfuscate(data)), len(data))


class ParseTests(unittest.TestCase):
    def test_single_voxel(self):
        model = parse_decoded(make_payload(1, 1, 1, {(0, 0, 0): (10, 20, 30)}))
        self.assertEqual(model.dimensions, (1, 1, 1))
        self.assertEqual(model.rgb_at(0, 0, 0), (10, 20, 30))
        self.assertEqual(model.non_empty_count, 1)

    def test_index_order_matches_documented_layout(self):
        # Put a voxel at x=1, d=0, h=0 of a 2x1x1 model and read it back.
        model = parse_decoded(make_payload(2, 1, 1, {(1, 0, 0): (1, 2, 3)}))
        self.assertEqual(model.rgb_at(1, 0, 0), (1, 2, 3))
        self.assertEqual(model.rgb_at(0, 0, 0), (0, 0, 0))

    def test_empty_voxel_is_black(self):
        model = parse_decoded(make_payload(4, 4, 4, {}))
        self.assertEqual(model.non_empty_count, 0)

    def test_truncated_header_rejected(self):
        with self.assertRaises(CubFormatError):
            parse_decoded(b"\x01\x00\x00")

    def test_size_mismatch_rejected(self):
        payload = make_payload(2, 2, 2, {})[:-1]  # one byte short
        with self.assertRaises(CubFormatError):
            parse_decoded(payload)

    def test_non_positive_dimension_rejected(self):
        for dims in ((0, 1, 1), (1, -2, 1), (1, 1, 0)):
            with self.assertRaises(CubFormatError):
                parse_decoded(struct.pack("<iii", *dims))

    def test_absurd_dimension_rejected(self):
        with self.assertRaises(CubFormatError):
            parse_decoded(struct.pack("<iii", 100000, 100000, 100000) + b"\x00" * 16)

    def test_out_of_bounds_lookup_rejected(self):
        model = parse_decoded(make_payload(1, 1, 1, {}))
        with self.assertRaises(CubFormatError):
            model.rgb_at(1, 0, 0)

    def test_garbage_payload_does_not_crash_with_wrong_exception(self):
        garbage = bytes((i * 37 + 11) & 0xFF for i in range(300))
        try:
            parse_decoded(garbage)
        except CubFormatError:
            pass  # expected for most garbage inputs

    def test_stored_round_trip(self):
        payload = make_payload(2, 2, 2, {(1, 0, 1): (200, 100, 50)})
        model = parse_stored(obfuscate(payload))
        self.assertEqual(model.rgb_at(1, 0, 1), (200, 100, 50))
        self.assertEqual(encode_model(model), payload)


class ColourTests(unittest.TestCase):
    def test_histogram_sorted_by_count(self):
        model = parse_decoded(make_payload(3, 1, 1, {
            (0, 0, 0): (5, 5, 5),
            (1, 0, 0): (5, 5, 5),
            (2, 0, 0): (9, 9, 9),
        }))
        colors = solid_colors(model)
        self.assertEqual(list(colors.items())[0], ((5, 5, 5), 2))
        self.assertEqual(list(colors.items())[1], ((9, 9, 9), 1))
        self.assertEqual(len(colors), 2)

    def test_anchor_colours_detected(self):
        model = parse_decoded(make_payload(3, 1, 1, {
            (0, 0, 0): (255, 0, 0),
            (1, 0, 0): (0, 255, 0),
            (2, 0, 0): (0, 0, 255),
        }))
        anchors = find_anchor_colors(model)
        self.assertEqual(anchors[(255, 0, 0)], [(0, 0, 0)])
        self.assertEqual(anchors[(0, 255, 0)], [(1, 0, 0)])
        self.assertEqual(anchors[(0, 0, 255)], [(2, 0, 0)])


class GeometryTests(unittest.TestCase):
    def test_single_voxel_has_six_faces(self):
        model = parse_decoded(make_payload(1, 1, 1, {(0, 0, 0): (1, 1, 1)}))
        self.assertEqual(expected_face_count(model), 6)

    def test_hidden_faces_are_culled(self):
        # 2x1x1 bar: 6 + 6 - 2 shared faces = 10
        model = parse_decoded(make_payload(2, 1, 1, {
            (0, 0, 0): (1, 1, 1), (1, 0, 0): (1, 1, 1)}))
        self.assertEqual(expected_face_count(model), 10)

    def test_solid_cube_interior_faces_culled(self):
        solid = {(x, d, h): (9, 9, 9) for x in range(2) for d in range(2) for h in range(2)}
        model = parse_decoded(make_payload(2, 2, 2, solid))
        self.assertEqual(expected_face_count(model), 24)  # 6 sides x 4 quads

    def test_diagonal_voxels_do_not_cull(self):
        # Two voxels touching only by an edge keep all their faces.
        model = parse_decoded(make_payload(2, 2, 1, {
            (0, 0, 0): (1, 1, 1), (1, 1, 0): (1, 1, 1)}))
        self.assertEqual(expected_face_count(model), 12)

    def test_is_solid_outside_bounds_is_false(self):
        model = parse_decoded(make_payload(2, 2, 2, {(0, 0, 0): (1, 1, 1)}))
        self.assertTrue(is_solid(model, 0, 0, 0))
        self.assertFalse(is_solid(model, 5, 0, 0))
        self.assertFalse(is_solid(model, -1, 0, 0))


class ObjTests(unittest.TestCase):
    def _count(self, text: str, prefix: str) -> int:
        return sum(1 for line in text.splitlines() if line.startswith(prefix))

    def test_obj_geometry_matches_expected_face_count(self):
        solid = {(x, d, h): (10 * x + 1, 20, 30)
                 for x in range(2) for d in range(2) for h in range(3)}
        model = parse_decoded(make_payload(2, 2, 3, solid))
        obj, mtl = build_obj(model, "synthetic", scale=1.0)
        faces = self._count(obj, "f ")
        vertices = self._count(obj, "v ")
        self.assertEqual(faces, expected_face_count(model))
        self.assertEqual(vertices, faces * 4)
        self.assertIn("mtllib synthetic.mtl", obj)
        self.assertIn("newmtl", mtl)

    def test_obj_scale_changes_bounds(self):
        model = parse_decoded(make_payload(2, 2, 2, {
            (x, d, h): (1, 2, 3) for x in range(2) for d in range(2) for h in range(2)}))
        obj1, _ = build_obj(model, "a", scale=1.0)
        obj2, _ = build_obj(model, "a", scale=0.5)
        def max_y(text):
            ys = [float(line.split()[2]) for line in text.splitlines() if line.startswith("v ")]
            return max(ys)
        self.assertAlmostEqual(max_y(obj1), 2.0)
        self.assertAlmostEqual(max_y(obj2), 1.0)

    def test_one_material_per_colour(self):
        model = parse_decoded(make_payload(2, 1, 1, {
            (0, 0, 0): (1, 1, 1), (1, 0, 0): (2, 2, 2)}))
        obj, mtl = build_obj(model, "c")
        self.assertEqual(self._count(mtl, "newmtl"), 2)
        self.assertEqual(self._count(obj, "usemtl"), 2)

    def test_empty_model_produces_valid_empty_obj(self):
        model = parse_decoded(make_payload(2, 2, 2, {}))
        obj, mtl = build_obj(model, "empty")
        self.assertEqual(self._count(obj, "f "), 0)
        self.assertEqual(self._count(mtl, "newmtl"), 0)
        self.assertIn("o empty", obj)


class VoxelModelModelTests(unittest.TestCase):
    def test_voxel_count(self):
        model = VoxelModel(3, 4, 5, bytes(3 * 4 * 5 * 3))
        self.assertEqual(model.voxel_count, 60)
        self.assertEqual(model.dimensions, (3, 4, 5))


if __name__ == "__main__":
    unittest.main(verbosity=2)
