"""Cross-check: Unity mesh geometry vs the Python `.cub` converter (S05A-1-REVIEW-FIX-01).

The fixture `fixtures/unity_geometry_dump.json` is produced inside the isolated Unity project by
`CharacterPilotVerifier.DumpGeometry` for three *synthetic* voxel models (no Cube World assets).
Here the same models are rebuilt with `cub_to_obj.build_obj` and compared vertex-by-vertex.

This is what proves that the Unity builder and the Python converter agree after aligning the
coordinate systems: both work in grid space, so vertices must match exactly (0.001 tolerance is
only used because the dump prints three decimals).

Regenerate the fixture after any change to the mesh builder:

    Unity.exe -batchmode -quit -projectPath <pilot project> -executeMethod CharacterPilotVerifier.DumpGeometry
    copy <pilot>/PilotOutput/geometry_dump.json tools/alpha_models/tests/fixtures/
"""

from __future__ import annotations

import hashlib
import json
import struct
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from cub_format import parse_decoded  # noqa: E402
from cub_to_obj import build_obj  # noqa: E402

FIXTURE = Path(__file__).resolve().parent / "fixtures" / "unity_geometry_dump.json"


def make_payload(width, depth, height, voxels):
    """voxels: list of (x, d, h, r, g, b)."""
    data = bytearray(12 + width * depth * height * 3)
    data[0:12] = struct.pack("<iii", width, depth, height)
    for (x, d, h, r, g, b) in voxels:
        offset = 12 + ((h * depth + d) * width + x) * 3
        data[offset:offset + 3] = bytes((r, g, b))
    return bytes(data)


# Must stay identical to CharacterPilotVerifier.GeometryCases in the Unity pilot.
CASES = {
    "asymmetric_4x1x2": {
        "payload": make_payload(4, 1, 2, [
            (3, 0, 0, 10, 20, 30),
            (0, 0, 1, 40, 50, 60),
        ]),
        "hidden": [],
    },
    "empty_edges_5x5x5": {
        "payload": make_payload(5, 5, 5, [
            (2, 1, 3, 200, 100, 50),
        ]),
        "hidden": [],
    },
    "masked_markers_4x1x1": {
        "payload": make_payload(4, 1, 1, [
            (0, 0, 0, 0, 255, 0),
            (1, 0, 0, 255, 255, 255),
            (2, 0, 0, 255, 0, 0),
            (3, 0, 0, 120, 130, 140),
        ]),
        "hidden": [(0, 0, 0)],
    },
}


def parse_obj_vertices(text: str):
    """Vertices plus indices triangulated exactly like the Unity builder (fan: 0-1-2, 0-2-3)."""
    vertices = []
    triangles = []
    for line in text.splitlines():
        if line.startswith("v "):
            _, x, y, z = line.split()[:4]
            vertices.append((round(float(x), 3), round(float(y), 3), round(float(z), 3)))
        elif line.startswith("f "):
            quad = [int(part.split("/")[0]) - 1 for part in line.split()[1:]]
            triangles.extend([quad[0], quad[1], quad[2], quad[0], quad[2], quad[3]])
    return vertices, triangles


class UnityGeometryCrossCheck(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        if not FIXTURE.exists():
            raise unittest.SkipTest(f"unity geometry fixture missing: {FIXTURE}")
        cls.dump = {entry["name"]: entry for entry in json.loads(FIXTURE.read_text(encoding="utf-8"))}

    def test_all_cases_present(self):
        self.assertEqual(sorted(self.dump), sorted(CASES))

    def test_payloads_match_the_unity_cases(self):
        for name, case in CASES.items():
            digest = hashlib.sha256(case["payload"]).hexdigest()
            self.assertEqual(digest, self.dump[name]["payloadSha256"],
                             f"{name}: synthetic payload drifted from the Unity test data")

    def test_vertices_match_python_converter(self):
        for name, case in CASES.items():
            model = parse_decoded(case["payload"])
            excluded = set(case["hidden"]) or None
            obj_text, _mtl = build_obj(model, name, excluded=excluded)
            py_vertices, py_triangles = parse_obj_vertices(obj_text)
            unity_vertices = [tuple(v) for v in self.dump[name]["vertices"]]
            self.assertEqual(len(py_vertices), len(unity_vertices), f"{name}: vertex count differs")
            self.assertEqual(py_vertices, unity_vertices,
                             f"{name}: vertices differ between Unity and the Python converter")
            self.assertEqual(py_triangles, self.dump[name]["triangles"],
                             f"{name}: triangulated indices differ")

    def test_face_count_and_grid_min_match(self):
        for name, case in CASES.items():
            model = parse_decoded(case["payload"])
            excluded = set(case["hidden"]) or None
            obj_text, _ = build_obj(model, name, excluded=excluded)
            faces = sum(1 for line in obj_text.splitlines() if line.startswith("f "))
            self.assertEqual(faces, self.dump[name]["faces"], f"{name}: face count differs")

            visible = [(x, d, h) for h in range(model.height) for d in range(model.depth)
                       for x in range(model.width)
                       if (model.rgb_at(x, d, h) != (0, 0, 0)) and (x, d, h) not in (excluded or set())]
            grid_min = (min(p[0] for p in visible), min(p[2] for p in visible), min(p[1] for p in visible))
            self.assertEqual(f"({grid_min[0]},{grid_min[1]},{grid_min[2]})",
                             self.dump[name]["gridMin"], f"{name}: grid min differs")

    def test_hidden_marker_removes_its_faces(self):
        """The masked case must contain no face belonging to the hidden marker voxel."""
        name = "masked_markers_4x1x1"
        case = CASES[name]
        model = parse_decoded(case["payload"])
        excluded = set(case["hidden"])
        obj_text, _ = build_obj(model, name, excluded=excluded)
        py_vertices, _ = parse_obj_vertices(obj_text)
        # the hidden voxel sits at x=0, so no quad may lie in the x=0 plane
        self.assertFalse([v for v in py_vertices if v[0] == 0.0],
                         "geometry still contains faces of the hidden marker voxel")

    def test_asymmetric_case_is_not_shifted(self):
        """Regression for FIX-01: every vertex must use the final grid minimum, not an early one."""
        name = "asymmetric_4x1x2"
        unity_vertices = [tuple(v) for v in self.dump[name]["vertices"]]
        self.assertTrue(all(v[0] >= 0 and v[1] >= 0 and v[2] >= 0 for v in unity_vertices))
        # the first voxel in (h, d, x) order sits at x=3, the later one at x=0: a single-pass
        # minimum would emit the early vertices at x=3..4 and shift the pair
        self.assertEqual(min(v[0] for v in unity_vertices), 0)
        self.assertEqual(max(v[0] for v in unity_vertices), 4)
        self.assertEqual(min(v[1] for v in unity_vertices), 0)
        self.assertEqual(max(v[1] for v in unity_vertices), 2)


if __name__ == "__main__":
    unittest.main(verbosity=2)
