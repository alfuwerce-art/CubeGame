"""Minimal `.cub` → Wavefront OBJ (+MTL) converter for the S05A-0 pilot.

Design goals, in order:

1. *Verifiable*: every face written here is derived from the decoded voxel grid
   with hidden-face culling; the tool can print the expected face count so a
   test can compare it with the geometry it actually wrote.
2. *Portable*: OBJ/MTL is understood by Unity's built-in ModelImporter and by
   most DCC tools, so an independent consumer can be checked.
3. *Small*: no third-party dependencies, no attempt to become a model editor.

Colour handling: `.cub` stores one flat RGB per voxel and shipped models use few
distinct colours (<40 in the samples), so one OBJ material per distinct colour
is practical and keeps per-face colour in a format that needs no extensions.

Axes: the model is written with +Y up and +Z towards the model's back, i.e.
``obj = (x, h, d)`` from ``voxel(x, d, h)``. Unit size defaults to 1.0 per voxel
and can be changed with ``--scale``.
"""

from __future__ import annotations

import argparse
import sqlite3
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from cub_format import VoxelModel, encode_model, parse_stored  # noqa: E402

# (dx, dy, dz) neighbour offsets and matching quad corners (in +Y up space).
# Corner order is counter-clockwise when viewed from outside, so OBJ/Unity get
# correct outward normals without extra flipping.
FACE_DEFS = (
    # +X
    ((1, 0, 0), ((1, 0, 0), (1, 1, 0), (1, 1, 1), (1, 0, 1))),
    # -X
    ((-1, 0, 0), ((0, 0, 1), (0, 1, 1), (0, 1, 0), (0, 0, 0))),
    # +Y
    ((0, 1, 0), ((0, 1, 0), (0, 1, 1), (1, 1, 1), (1, 1, 0))),
    # -Y
    ((0, -1, 0), ((0, 0, 0), (1, 0, 0), (1, 0, 1), (0, 0, 1))),
    # +Z
    ((0, 0, 1), ((0, 0, 1), (1, 0, 1), (1, 1, 1), (0, 1, 1))),
    # -Z
    ((0, 0, -1), ((1, 0, 0), (0, 0, 0), (0, 1, 0), (1, 1, 0))),
)


def is_solid(model: VoxelModel, x: int, d: int, h: int) -> bool:
    if not (0 <= x < model.width and 0 <= d < model.depth and 0 <= h < model.height):
        return False
    px = model.pixels
    offset = ((h * model.depth + d) * model.width + x) * 3
    return bool(px[offset] or px[offset + 1] or px[offset + 2])


def expected_face_count(model: VoxelModel) -> int:
    """Number of culled quad faces (4 vertices each) for the model."""
    total = 0
    for h in range(model.height):
        for d in range(model.depth):
            for x in range(model.width):
                if not is_solid(model, x, d, h):
                    continue
                for (dx, dy, dz), _corners in FACE_DEFS:
                    # neighbour in voxel space: dy is height, dz is depth
                    if not is_solid(model, x + dx, d + dz, h + dy):
                        total += 1
    return total


def build_obj(model: VoxelModel, name: str, scale: float = 1.0) -> tuple[str, str]:
    """Return (obj_text, mtl_text)."""
    colors: dict[tuple[int, int, int], int] = {}
    for i in range(0, len(model.pixels), 3):
        r, g, b = model.pixels[i], model.pixels[i + 1], model.pixels[i + 2]
        if r or g or b:
            colors.setdefault((r, g, b), len(colors) + 1)

    mtl_lines: list[str] = ["# CubeGame S05A-0 pilot conversion — generated, do not edit"]
    mtl_lines.append(f"# source: {name}; voxels {model.width}x{model.depth}x{model.height}")
    for color, index in colors.items():
        r, g, b = color
        mtl_lines.append(f"newmtl c{index:03d}")
        mtl_lines.append(f"Kd {r / 255:.6f} {g / 255:.6f} {b / 255:.6f}")
        mtl_lines.append("Ka 0.000000 0.000000 0.000000")
        mtl_lines.append("Ks 0.000000 0.000000 0.000000")
        mtl_lines.append("d 1.0")
        mtl_lines.append("illum 1")
        mtl_lines.append("")

    obj: list[str] = [
        "# CubeGame S05A-0 pilot conversion — generated, do not edit",
        f"# source: {name}",
        f"# dimensions: width={model.width} depth={model.depth} height={model.height}",
        f"# voxels: {model.voxel_count} total, {model.non_empty_count} solid",
        f"# colours: {len(colors)}",
        "mtllib " + name + ".mtl",
        f"o {name}",
    ]

    vertex_index = 1
    quads_by_color: dict[int, list[tuple[int, int, int, int]]] = {}
    for h in range(model.height):
        for d in range(model.depth):
            for x in range(model.width):
                if not is_solid(model, x, d, h):
                    continue
                offset = ((h * model.depth + d) * model.width + x) * 3
                color = (model.pixels[offset], model.pixels[offset + 1], model.pixels[offset + 2])
                index = colors[color]
                for (dx, dy, dz), corners in FACE_DEFS:
                    if is_solid(model, x + dx, d + dz, h + dy):
                        continue
                    face_vertices = []
                    for cx, cy, cz in corners:
                        vx = (x + cx) * scale
                        vy = (h + cy) * scale
                        vz = (d + cz) * scale
                        obj.append(f"v {vx:.6f} {vy:.6f} {vz:.6f}")
                        face_vertices.append(vertex_index)
                        vertex_index += 1
                    quads_by_color.setdefault(index, []).append(tuple(face_vertices))

    for index in sorted(quads_by_color):
        obj.append(f"g c{index:03d}")
        obj.append(f"usemtl c{index:03d}")
        for quad in quads_by_color[index]:
            a, b_, c, d_ = quad
            obj.append(f"f {a} {b_} {c} {d_}")

    return "\n".join(obj) + "\n", "\n".join(mtl_lines) + "\n"


def load_from_db(db: Path, key: str) -> VoxelModel:
    uri = "file:" + db.as_posix() + "?mode=ro&immutable=1"
    con = sqlite3.connect(uri, uri=True)
    row = con.execute("SELECT value FROM blobs WHERE key = ?", (key,)).fetchone()
    if row is None:
        raise SystemExit(f"key not found: {key}")
    return parse_stored(bytes(row[0]))


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    source = ap.add_mutually_exclusive_group(required=True)
    source.add_argument("--db", type=Path)
    source.add_argument("--decoded", type=Path, help="already decoded .cubdec payload")
    ap.add_argument("--key", help="blob key when --db is used")
    ap.add_argument("--out-dir", required=True, type=Path)
    ap.add_argument("--name", help="output stem (defaults to the blob key stem)")
    ap.add_argument("--scale", type=float, default=1.0, help="OBJ units per voxel")
    ap.add_argument("--dump-decoded", action="store_true",
                    help="also write <name>.cubdec (de-obfuscated payload) for the Unity builder")
    args = ap.parse_args(argv)

    from cub_format import parse_decoded

    if args.db:
        if not args.key:
            ap.error("--key is required with --db")
        model = load_from_db(args.db, args.key)
        stem = args.name or Path(args.key).stem
    else:
        model = parse_decoded(args.decoded.read_bytes())
        stem = args.name or args.decoded.stem

    args.out_dir.mkdir(parents=True, exist_ok=True)
    obj_text, mtl_text = build_obj(model, stem, args.scale)
    (args.out_dir / f"{stem}.obj").write_text(obj_text, encoding="utf-8", newline="\n")
    (args.out_dir / f"{stem}.mtl").write_text(mtl_text, encoding="utf-8", newline="\n")
    if args.dump_decoded:
        (args.out_dir / f"{stem}.cubdec").write_bytes(encode_model(model))

    quads = expected_face_count(model)
    print(f"{stem}: dims={model.width}x{model.depth}x{model.height} "
          f"solid={model.non_empty_count} faces={quads} vertices={quads * 4} "
          f"materials={len({c for c in solid_pairs(model)})}")
    return 0


def solid_pairs(model: VoxelModel):
    px = model.pixels
    for i in range(0, len(px), 3):
        if px[i] or px[i + 1] or px[i + 2]:
            yield (px[i], px[i + 1], px[i + 2])


if __name__ == "__main__":
    raise SystemExit(main())
