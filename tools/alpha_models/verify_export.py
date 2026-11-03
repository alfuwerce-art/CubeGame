"""Per-face integrity check for converted models: decoded `.cubdec` vs exported OBJ/MTL.

The previous version compared aggregate numbers (face count, total area, bounding
box, colour presence), which cannot prove that *each* exported quad is the quad
the voxel grid actually exposes. This version rebuilds both sets and matches them
one by one:

* expected set — every exposed face of the voxel solid, keyed by the square it
  occupies (the frozenset of its four integer corner points, which is unique for
  a face on the integer lattice) with its outward normal and voxel colour;
* actual set — every `f` quad in the OBJ, keyed the same way, with the normal
  derived from the winding order and the colour taken from the material the OBJ
  assigns to that quad.

Then it reports missing faces, extra faces, duplicated faces, faces whose winding
is inverted (normal points the wrong way), and faces painted with the wrong
material. Face and vertex order in the file is irrelevant — only the resulting
set matters.

Also kept: unit-area / axis-alignment checks, bounding box == solid extent, and
`usemtl` entries must exist in the MTL with a `Kd` equal to an actual voxel colour.

Usage::

    python tools/alpha_models/verify_export.py --cubdec a.cubdec --obj a.obj --mtl a.mtl
"""

from __future__ import annotations

import argparse
import sys
from collections import Counter
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from cub_format import parse_decoded  # noqa: E402
from cub_to_obj import FACE_DEFS, is_solid  # noqa: E402

Point = tuple[int, int, int]
Square = frozenset
MAX_EXAMPLES = 3


class VerificationError(Exception):
    """Raised when the inputs cannot be parsed at all."""


def parse_obj(text: str):
    """Return (vertices, faces, materials_per_face). Faces are index tuples (0-based)."""
    vertices: list[tuple[float, float, float]] = []
    faces: list[tuple[int, ...]] = []
    material_of_face: list[str] = []
    current_material = ""
    for line in text.splitlines():
        if line.startswith("v "):
            parts = line.split()
            if len(parts) < 4:
                raise VerificationError(f"malformed vertex line: {line!r}")
            vertices.append((float(parts[1]), float(parts[2]), float(parts[3])))
        elif line.startswith("usemtl "):
            current_material = line.split(None, 1)[1].strip()
        elif line.startswith("f "):
            index = []
            for part in line.split()[1:]:
                index.append(int(part.split("/")[0]) - 1)
            faces.append(tuple(index))
            material_of_face.append(current_material)
    return vertices, faces, material_of_face


def parse_mtl(text: str) -> dict[str, tuple[int, int, int]]:
    result: dict[str, tuple[int, int, int]] = {}
    name = None
    for line in text.splitlines():
        if line.startswith("newmtl "):
            name = line.split(None, 1)[1].strip()
        elif line.startswith("Kd ") and name:
            _, r, g, b = line.split()[:4]
            result[name] = (round(float(r) * 255), round(float(g) * 255), round(float(b) * 255))
            name = None
    return result


def model_color(model, x: int, d: int, h: int) -> tuple[int, int, int]:
    offset = ((h * model.depth + d) * model.width + x) * 3
    return (model.pixels[offset], model.pixels[offset + 1], model.pixels[offset + 2])


def expected_faces(model, scale: float = 1.0):
    """Map every exposed voxel face to (square key, normal, colour).

    Keys live in *voxel* space (integers); ``scale`` is applied to the OBJ side
    when the actual faces are converted back, so the comparison is scale-free.
    """
    expected: dict[frozenset, tuple[tuple[int, int, int], tuple[int, int, int]]] = {}
    for h in range(model.height):
        for d in range(model.depth):
            for x in range(model.width):
                if not is_solid(model, x, d, h):
                    continue
                color = model_color(model, x, d, h)
                for (dx, dy, dz), corners in FACE_DEFS:
                    if is_solid(model, x + dx, d + dz, h + dy):
                        continue
                    # voxel (x, d, h) -> world (x, h, d) as in cub_to_obj.build_obj
                    points = frozenset(
                        (x + cx, h + cy, d + cz) for cx, cy, cz in corners
                    )
                    expected[points] = ((dx, dy, dz), color)
    return expected


def _round(value: float) -> int:
    rounded = round(value)
    if abs(value - rounded) > 1e-3:
        raise VerificationError(f"coordinate {value} is not on the integer lattice")
    return int(rounded)


def _normal_from_winding(points: list[Point]) -> tuple[int, int, int] | None:
    a, b, c = points[0], points[1], points[2]
    e1 = (b[0] - a[0], b[1] - a[1], b[2] - a[2])
    e2 = (c[0] - b[0], c[1] - b[1], c[2] - b[2])
    cross = (e1[1] * e2[2] - e1[2] * e2[1],
             e1[2] * e2[0] - e1[0] * e2[2],
             e1[0] * e2[1] - e1[1] * e2[0])
    length = max(abs(cross[0]), abs(cross[1]), abs(cross[2]))
    if length != 1:
        return None
    return cross


def actual_faces(vertices, faces, face_materials, materials, scale: float = 1.0):
    """Map exported quads to square key -> list of (normal, colour, face index)."""
    actual: dict[frozenset, list[tuple[tuple[int, int, int] | None, tuple[int, int, int] | None, int]]] = {}
    for index, face in enumerate(faces):
        if len(face) != 4:
            raise VerificationError(f"face {index} is not a quad ({len(face)} vertices)")
        for vertex_index in face:
            if not 0 <= vertex_index < len(vertices):
                raise VerificationError(
                    f"face {index} references vertex {vertex_index + 1}, "
                    f"but the file has {len(vertices)} vertices")
        points = [(_round(vertices[i][0] / scale),
                   _round(vertices[i][1] / scale),
                   _round(vertices[i][2] / scale)) for i in face]
        if len(set(points)) != 4:
            raise VerificationError(f"face {index} has repeated corners")
        normal = _normal_from_winding(points)
        color = materials.get(face_materials[index])
        actual.setdefault(frozenset(points), []).append((normal, color, index))
    return actual


def _fmt_square(square) -> str:
    return "{" + ",".join(str(p) for p in sorted(square)) + "}"


def _describe(mapping, limit=MAX_EXAMPLES) -> str:
    keys = list(mapping)
    shown = ", ".join(_fmt_square(k) for k in keys[:limit])
    suffix = "" if len(keys) <= limit else f" (+{len(keys) - limit} more)"
    return shown + suffix


def verify(cubdec: Path, obj: Path, mtl: Path, scale: float = 1.0) -> tuple[bool, list[str], dict]:
    """Run every check; return (ok, problems, stats)."""
    model = parse_decoded(cubdec.read_bytes())
    try:
        vertices, faces, face_materials = parse_obj(obj.read_text(encoding="utf-8"))
    except (ValueError, VerificationError) as exc:
        return False, [f"cannot parse OBJ: {exc}"], {}
    try:
        materials = parse_mtl(mtl.read_text(encoding="utf-8"))
    except (ValueError, VerificationError) as exc:
        return False, [f"cannot parse MTL: {exc}"], {}

    problems: list[str] = []
    try:
        expected = expected_faces(model, scale)
        actual = actual_faces(vertices, faces, face_materials, materials, scale)
    except VerificationError as exc:
        return False, [str(exc)], {}

    missing = [k for k in expected if k not in actual]
    extra = [k for k in actual if k not in expected]
    duplicated = {k: v for k, v in actual.items() if len(v) > 1}

    if missing:
        problems.append(f"{len(missing)} face(s) present in the model but missing from OBJ: {_describe(missing)}")
    if extra:
        problems.append(f"{len(extra)} face(s) in OBJ that the model does not expose: {_describe(extra)}")
    if duplicated:
        problems.append(
            f"{len(duplicated)} face position(s) written more than once (max {max(len(v) for v in duplicated.values())}x): "
            f"{_describe(duplicated)}")

    # orientation and material of every matched face
    inverted: list[str] = []
    wrong_material: list[str] = []
    no_material: list[str] = []
    for square, entries in actual.items():
        reference = expected.get(square)
        if reference is None:
            continue
        expected_normal, expected_color = reference
        for normal, color, face_index in entries:
            if normal is None:
                problems.append(f"face {face_index} at {_fmt_square(square)} is not axis aligned")
                continue
            if normal != expected_normal:
                inverted.append(f"face {face_index} at {_fmt_square(square)}: normal {normal} != {expected_normal}")
            if color is None:
                no_material.append(f"face {face_index} at {_fmt_square(square)} has no MTL entry")
            elif color != expected_color:
                wrong_material.append(
                    f"face {face_index} at {_fmt_square(square)}: colour {color} != voxel colour {expected_color}")
    if inverted:
        problems.append(f"{len(inverted)} face(s) with inverted winding/normal: " + "; ".join(inverted[:MAX_EXAMPLES]))
    if no_material:
        problems.append(f"{len(no_material)} face(s) without material: " + "; ".join(no_material[:MAX_EXAMPLES]))
    if wrong_material:
        problems.append(f"{len(wrong_material)} face(s) with the wrong material: " + "; ".join(wrong_material[:MAX_EXAMPLES]))

    # geometric sanity of the file itself
    degenerate = 0
    for index, face in enumerate(faces):
        pts = [(_round(vertices[i][0] / scale), _round(vertices[i][1] / scale),
                _round(vertices[i][2] / scale)) for i in face]
        spans = [max(p[k] for p in pts) - min(p[k] for p in pts) for k in range(3)]
        if sorted(spans) != [0, 1, 1]:
            degenerate += 1
    if degenerate:
        problems.append(f"{degenerate} quad(s) are not unit squares")

    # bounds: the OBJ must cover exactly the solid voxel extent
    solid = [(x, d, h) for h in range(model.height) for d in range(model.depth)
             for x in range(model.width) if is_solid(model, x, d, h)]
    if not solid:
        problems.append("decoded model has no solid voxels")
        expected_extent = (0, 0, 0)
    else:
        expected_extent = (
            max(p[0] for p in solid) - min(p[0] for p in solid) + 1,
            max(p[2] for p in solid) - min(p[2] for p in solid) + 1,
            max(p[1] for p in solid) - min(p[1] for p in solid) + 1,
        )
    if vertices:
        extent = (
            _round((max(v[0] for v in vertices) - min(v[0] for v in vertices)) / scale),
            _round((max(v[1] for v in vertices) - min(v[1] for v in vertices)) / scale),
            _round((max(v[2] for v in vertices) - min(v[2] for v in vertices)) / scale),
        )
    else:
        extent = (0, 0, 0)
    if extent != expected_extent:
        problems.append(f"extent {extent} != solid extent {expected_extent} (x, y=height, z=depth)")
    if vertices:
        mins = (min(v[0] for v in vertices), min(v[1] for v in vertices), min(v[2] for v in vertices))
        if min(mins) < -1e-6:
            problems.append(f"negative coordinates in OBJ: min={mins}")

    # every usemtl must resolve, and every MTL colour must come from the model
    model_colors = {model_color(model, x, d, h)
                    for h in range(model.height) for d in range(model.depth)
                    for x in range(model.width) if is_solid(model, x, d, h)}
    unknown = sorted({name for name in face_materials if name not in materials})
    if unknown:
        problems.append(f"usemtl without MTL entry: {unknown[:5]}")
    stray = sorted({name for name, color in materials.items() if color not in model_colors})
    if stray:
        problems.append(f"MTL colours that no voxel uses: {stray[:5]}")

    stats = {
        "faces": len(faces),
        "expected_faces": len(expected),
        "vertices": len(vertices),
        "materials": len(materials),
        "colors": len(model_colors),
        "extent": extent,
        "missing": len(missing),
        "extra": len(extra),
        "duplicated": len(duplicated),
        "inverted": len(inverted),
        "wrong_material": len(wrong_material),
    }
    return (not problems), problems, stats


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--cubdec", required=True, type=Path)
    ap.add_argument("--obj", required=True, type=Path)
    ap.add_argument("--mtl", required=True, type=Path)
    ap.add_argument("--scale", type=float, default=1.0)
    args = ap.parse_args(argv)

    ok, problems, stats = verify(args.cubdec, args.obj, args.mtl, args.scale)
    status = "PASS" if ok else "FAIL"
    print(f"{args.obj.name}: {status} faces={stats.get('faces')} expected={stats.get('expected_faces')} "
          f"verts={stats.get('vertices')} extent={stats.get('extent')} "
          f"materials={stats.get('materials')} colours={stats.get('colors')}")
    if stats:
        print(f"  matched: missing={stats['missing']} extra={stats['extra']} "
              f"duplicated={stats['duplicated']} inverted={stats['inverted']} "
              f"wrong_material={stats['wrong_material']}")
    for problem in problems:
        print("  PROBLEM:", problem)
    return 0 if ok else 1


if __name__ == "__main__":
    raise SystemExit(main())
