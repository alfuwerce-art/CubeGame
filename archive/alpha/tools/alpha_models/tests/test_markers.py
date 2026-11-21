"""Tests for marker/tint analysis and marker-aware conversion (S05A-1).

Synthetic models only: no Cube World assets are used or copied.
"""

from __future__ import annotations

import json
import struct
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from cub_format import parse_decoded  # noqa: E402
from cub_markers import (  # noqa: E402
    ANCHOR_MAX_VOXELS,
    classify,
    excluded_voxels,
    marker_groups,
)
from cub_to_obj import build_obj, expected_face_count, is_solid  # noqa: E402
from verify_export import verify  # noqa: E402

RED = (255, 0, 0)
GREEN = (0, 255, 0)
BLUE = (0, 0, 255)
SKIN = (255, 211, 182)
WHITE = (255, 255, 255)


def payload(width, depth, height, solid):
    pixels = bytearray(width * depth * height * 3)
    for (x, d, h), rgb in solid.items():
        offset = ((h * depth + d) * width + x) * 3
        pixels[offset:offset + 3] = bytes(rgb)
    return struct.pack("<iii", width, depth, height) + bytes(pixels)


def edges_faces(vertices, faces):
    """Set of face squares (frozenset of corner points) from a parsed OBJ face list."""
    return {frozenset(vertices[i] for i in f) for f in faces}


def parse_obj_text(text):
    vertices, faces = [], []
    for line in text.splitlines():
        if line.startswith("v "):
            _, x, y, z = line.split()[:4]
            vertices.append((round(float(x), 3), round(float(y), 3), round(float(z), 3)))
        elif line.startswith("f "):
            faces.append(tuple(int(p.split("/")[0]) - 1 for p in line.split()[1:]))
    return vertices, faces


class MarkerClassificationTests(unittest.TestCase):
    def test_tiny_exposed_group_is_anchor_candidate(self):
        model = parse_decoded(payload(4, 1, 1, {
            (0, 0, 0): RED, (1, 0, 0): GREEN, (2, 0, 0): BLUE, (3, 0, 0): SKIN,
        }))
        groups = marker_groups(model)
        self.assertEqual(groups["green"]["count"], 1)
        self.assertEqual(groups["green"]["heuristic_label"], "ANCHOR_CANDIDATE")
        self.assertEqual(groups["blue"]["heuristic_label"], "ANCHOR_CANDIDATE")
        self.assertEqual(groups["red"]["heuristic_label"], "ANCHOR_CANDIDATE")

    def test_large_exposed_group_is_tint_candidate(self):
        solid = {(x, d, 0): RED for x in range(10) for d in range(10)}
        model = parse_decoded(payload(10, 10, 1, solid))
        groups = marker_groups(model)
        self.assertEqual(groups["red"]["count"], 100)
        self.assertEqual(groups["red"]["heuristic_label"], "VOLUME_TINT_CANDIDATE")

    def test_hidden_group_is_not_a_tint_candidate(self):
        solid = {(x, 0, 0): RED for x in range(1)}
        solid.update({(x, d, h): SKIN for x in range(3) for d in range(3) for h in range(3) if (x, d, h) != (0, 0, 0)})
        # keep the red voxel buried inside skin: use a 3x3x3 block with red at its centre
        solid = {(x, d, h): SKIN for x in range(3) for d in range(3) for h in range(3)}
        solid[(1, 1, 1)] = RED
        model = parse_decoded(payload(3, 3, 3, solid))
        group = marker_groups(model)["red"]
        self.assertEqual(group["exposed"], 0)
        self.assertEqual(group["heuristic_label"], "UNKNOWN_DECORATIVE")

    def test_classify_thresholds(self):
        self.assertEqual(classify(ANCHOR_MAX_VOXELS, 1.0)[0], "ANCHOR_CANDIDATE")
        self.assertEqual(classify(ANCHOR_MAX_VOXELS + 1, 1.0)[0], "UNKNOWN_DECORATIVE")
        self.assertEqual(classify(100, 0.51)[0], "VOLUME_TINT_CANDIDATE")
        self.assertEqual(classify(100, 0.10)[0], "UNKNOWN_DECORATIVE")

    def test_bbox_and_coordinates_are_reported(self):
        model = parse_decoded(payload(3, 2, 2, {
            (0, 1, 1): GREEN, (2, 0, 0): BLUE,
        }))
        groups = marker_groups(model)
        self.assertEqual(groups["green"]["bbox_min"], [0, 1, 1])
        self.assertEqual(groups["green"]["bbox_max"], [0, 1, 1])
        self.assertEqual(groups["blue"]["bbox_min"], [2, 0, 0])

    def test_no_markers_model_returns_empty_groups(self):
        model = parse_decoded(payload(2, 2, 2, {(0, 0, 0): SKIN}))
        self.assertEqual(marker_groups(model), {})


class MarkerGeometryTests(unittest.TestCase):
    def setUp(self):
        # a 3x1x1 bar: pure-red decorative voxel in the middle
        self.model = parse_decoded(payload(3, 1, 1, {
            (0, 0, 0): WHITE, (1, 0, 0): RED, (2, 0, 0): SKIN,
        }))

    def test_default_conversion_keeps_every_voxel(self):
        obj, _ = build_obj(self.model, "bar")
        vertices, faces = parse_obj_text(obj)
        self.assertEqual(len(faces), expected_face_count(self.model))

    def test_decorative_red_is_kept_unless_explicitly_excluded(self):
        obj, _ = build_obj(self.model, "bar")
        _, faces = parse_obj_text(obj)
        self.assertEqual(len(faces), 14)  # 3 voxels in a row: 6 + 4 + 4
        excluded = excluded_voxels(self.model, ["green", "blue"])
        self.assertEqual(excluded, set(), "no green/blue voxels exist in this model")

    def test_exclusion_changes_only_faces_of_named_colour(self):
        base_obj, _ = build_obj(self.model, "bar")
        excluded = excluded_voxels(self.model, ["red"])
        self.assertEqual(excluded, {(1, 0, 0)})
        cut_obj, _ = build_obj(self.model, "bar", excluded=excluded)
        base_v, base_f = parse_obj_text(base_obj)
        cut_v, cut_f = parse_obj_text(cut_obj)
        base_squares = edges_faces(base_v, base_f)
        cut_squares = edges_faces(cut_v, cut_f)
        self.assertEqual(len(base_squares), 14)   # 3 voxels in a row: 6 + 4 + 4 faces
        self.assertEqual(len(cut_squares), 12)    # the middle voxel leaves two isolated cubes
        self.assertTrue(base_squares - cut_squares, "removing a voxel must remove faces")
        self.assertTrue(base_squares & cut_squares, "removing a voxel must keep unrelated faces")

    def test_face_count_after_exclusion_matches_recomputation(self):
        excluded = excluded_voxels(self.model, ["red"])
        expected = expected_face_count(self.model, excluded)
        obj, _ = build_obj(self.model, "bar", excluded=excluded)
        _, faces = parse_obj_text(obj)
        self.assertEqual(len(faces), expected)

    def test_is_solid_respects_exclusion(self):
        excluded = {(1, 0, 0)}
        self.assertTrue(is_solid(self.model, 1, 0, 0))
        self.assertFalse(is_solid(self.model, 1, 0, 0, excluded))
        self.assertTrue(is_solid(self.model, 0, 0, 0, excluded))

    def test_excluded_model_still_verifies(self):
        excluded = excluded_voxels(self.model, ["red"])
        obj, mtl = build_obj(self.model, "bar", excluded=excluded)
        with tempfile.TemporaryDirectory() as tmp:
            tmp = Path(tmp)
            (tmp / "bar.cubdec").write_bytes(payload(3, 1, 1, {
                (0, 0, 0): WHITE, (1, 0, 0): RED, (2, 0, 0): SKIN}))
            (tmp / "bar.obj").write_text(obj, encoding="utf-8")
            (tmp / "bar.mtl").write_text(mtl, encoding="utf-8")
            ok, problems, stats = verify(tmp / "bar.cubdec", tmp / "bar.obj", tmp / "bar.mtl",
                                        exclude_markers=("red",))
        self.assertTrue(ok, problems)
        self.assertEqual(stats["faces"], stats["expected_faces"])

    def test_verifier_without_exclusion_rejects_hidden_markers(self):
        excluded = excluded_voxels(self.model, ["red"])
        obj, mtl = build_obj(self.model, "bar", excluded=excluded)
        with tempfile.TemporaryDirectory() as tmp:
            tmp = Path(tmp)
            (tmp / "bar.cubdec").write_bytes(payload(3, 1, 1, {
                (0, 0, 0): WHITE, (1, 0, 0): RED, (2, 0, 0): SKIN}))
            (tmp / "bar.obj").write_text(obj, encoding="utf-8")
            (tmp / "bar.mtl").write_text(mtl, encoding="utf-8")
            ok, problems, _ = verify(tmp / "bar.cubdec", tmp / "bar.obj", tmp / "bar.mtl")
        self.assertFalse(ok)
        self.assertTrue(any("missing from OBJ" in p for p in problems), problems)


class MarkerCliTests(unittest.TestCase):
    def test_marker_json_has_coordinates(self):
        with tempfile.TemporaryDirectory() as tmp:
            tmp = Path(tmp)
            data = payload(4, 2, 2, {
                (0, 0, 0): SKIN, (1, 0, 0): GREEN, (2, 0, 0): BLUE, (3, 1, 1): RED,
            })
            (tmp / "sample.cubdec").write_bytes(data)
            out = tmp / "sample.markers.json"
            result = subprocess.run(
                [sys.executable, str(Path(__file__).resolve().parents[1] / "cub_to_obj.py"),
                 "--decoded", str(tmp / "sample.cubdec"), "--out-dir", str(tmp),
                 "--markers", str(out)],
                capture_output=True, text=True)
            self.assertEqual(result.returncode, 0, result.stderr)
            report = json.loads(out.read_text(encoding="utf-8"))
            self.assertEqual(report["marker_groups"]["green"]["voxels"], [[1, 0, 0]])
            self.assertEqual(report["marker_groups"]["blue"]["voxels"], [[2, 0, 0]])
            self.assertEqual(report["excluded_voxel_count"], 0)


if __name__ == "__main__":
    unittest.main(verbosity=2)
