"""Integrity check for converted models: decoded `.cubdec` vs exported OBJ/MTL.

Verifies, without trusting the converter's own bookkeeping:

* every exported quad has unit area and an axis-aligned normal;
* the number of quads equals the culled face count recomputed from the voxels;
* the total quad area equals the exposed surface area of the voxel solid;
* the OBJ bounding box equals the solid voxel extent (x=width, y=height, z=depth);
* every `usemtl` exists in the MTL file and its `Kd` equals an actual voxel colour.

Usage::

    python tools/alpha_models/verify_export.py --cubdec a.cubdec --obj a.obj --mtl a.mtl
"""

from __future__ import annotations

import argparse
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from cub_format import parse_decoded  # noqa: E402
from cub_to_obj import FACE_DEFS, expected_face_count, is_solid  # noqa: E402


def parse_obj(text: str):
    vertices: list[tuple[float, float, float]] = []
    faces: list[tuple[int, ...]] = []
    material_of_face: list[str] = []
    current_material = ""
    for line in text.splitlines():
        if line.startswith("v "):
            _, x, y, z = line.split()[:4]
            vertices.append((float(x), float(y), float(z)))
        elif line.startswith("usemtl "):
            current_material = line.split(None, 1)[1].strip()
        elif line.startswith("f "):
            idx = [int(part.split("/")[0]) - 1 for part in line.split()[1:]]
            faces.append(tuple(idx))
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


def exposed_surface_area(model) -> int:
    """Surface area in unit squares, computed independently of the converter."""
    area = 0
    for h in range(model.height):
        for d in range(model.depth):
            for x in range(model.width):
                if not is_solid(model, x, d, h):
                    continue
                for (dx, dy, dz), _ in FACE_DEFS:
                    if not is_solid(model, x + dx, d + dz, h + dy):
                        area += 1
    return area


def quad_area(v0, v1, v2) -> float:
    ax, ay, az = v1[0] - v0[0], v1[1] - v0[1], v1[2] - v0[2]
    bx, by, bz = v2[0] - v1[0], v2[1] - v1[1], v2[2] - v1[2]
    cx = ay * bz - az * by
    cy = az * bx - ax * bz
    cz = ax * by - ay * bx
    return (cx * cx + cy * cy + cz * cz) ** 0.5


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--cubdec", required=True, type=Path)
    ap.add_argument("--obj", required=True, type=Path)
    ap.add_argument("--mtl", required=True, type=Path)
    args = ap.parse_args(argv)

    model = parse_decoded(args.cubdec.read_bytes())
    vertices, faces, face_materials = parse_obj(args.obj.read_text(encoding="utf-8"))
    materials = parse_mtl(args.mtl.read_text(encoding="utf-8"))

    problems: list[str] = []
    expected_faces = expected_face_count(model)
    if len(faces) != expected_faces:
        problems.append(f"face count {len(faces)} != expected {expected_faces}")
    if len(vertices) != len(faces) * 4:
        problems.append(f"vertex count {len(vertices)} != 4 per face ({len(faces) * 4})")

    degenerate = 0
    non_axis_aligned = 0
    total_area = 0.0
    for face in faces:
        if len(face) != 4:
            problems.append("non-quad face encountered")
            break
        pts = [vertices[i] for i in face]
        area = quad_area(pts[0], pts[1], pts[2])
        if abs(area - 1.0) > 1e-4:
            degenerate += 1
        else:
            total_area += area
        # axis alignment: the face must span exactly one axis
        spans = [max(p[k] for p in pts) - min(p[k] for p in pts) for k in range(3)]
        if sum(1 for s in spans if abs(s - 1.0) < 1e-4) != 2 or \
           sum(1 for s in spans if abs(s) < 1e-4) != 1:
            non_axis_aligned += 1
    if degenerate:
        problems.append(f"{degenerate} quad(s) with area != 1")
    if non_axis_aligned:
        problems.append(f"{non_axis_aligned} quad(s) not axis aligned")
    if abs(total_area - exposed_surface_area(model)) > 1e-6:
        problems.append(f"total area {total_area} != exposed surface area {exposed_surface_area(model)}")

    # bounds
    min_x = min(v[0] for v in vertices); max_x = max(v[0] for v in vertices)
    min_y = min(v[1] for v in vertices); max_y = max(v[1] for v in vertices)
    min_z = min(v[2] for v in vertices); max_z = max(v[2] for v in vertices)
    extent = (max_x - min_x, max_y - min_y, max_z - min_z)
    solid = [(x, d, h) for h in range(model.height) for d in range(model.depth)
             for x in range(model.width) if is_solid(model, x, d, h)]
    sx = [p[0] for p in solid]; sd = [p[1] for p in solid]; sh = [p[2] for p in solid]
    expected_extent = (max(sx) - min(sx) + 1, max(sh) - min(sh) + 1, max(sd) - min(sd) + 1)
    if tuple(round(v, 3) for v in extent) != tuple(float(v) for v in expected_extent):
        problems.append(f"extent {extent} != solid extent {expected_extent} (x,y,z = width,height,depth)")

    # materials
    used = sorted(set(face_materials))
    unknown = [name for name in used if name not in materials]
    if unknown:
        problems.append(f"usemtl without MTL entry: {unknown[:5]}")
    model_colors = set()
    px = model.pixels
    for i in range(0, len(px), 3):
        if px[i] or px[i + 1] or px[i + 2]:
            model_colors.add((px[i], px[i + 1], px[i + 2]))
    bad_colors = [name for name in used if materials.get(name) not in model_colors]
    if bad_colors:
        problems.append(f"MTL colours not present in the model: {bad_colors[:5]}")

    status = "PASS" if not problems else "FAIL"
    print(f"{args.obj.name}: {status} faces={len(faces)} expected={expected_faces} "
          f"area={total_area:g} extent={tuple(round(v, 1) for v in extent)} "
          f"materials_used={len(used)}/{len(materials)} colours={len(model_colors)}")
    for p in problems:
        print("  PROBLEM:", p)
    return 0 if not problems else 1


if __name__ == "__main__":
    raise SystemExit(main())
