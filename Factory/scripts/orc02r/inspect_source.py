#!/usr/bin/env python3
"""ORC-02R: read-only инспекция исходного Orc — силуэты, объёмы, палитра.

Только чтение CubeGame content; ничего не пишет в CubeGame.
Печатает ASCII-проекции частей (фронт XY-плоскость, сбоку) и сводку, чтобы
получить реальные пропорции для генератора новой модели.

    python scripts/orc02r/inspect_source.py
"""

from __future__ import annotations

import argparse
import collections
import json
import os
import struct
import sys

DEFAULT_ACTOR = r"D:\Projects\CubeGame\.scratch\content\actors\d86_kind_0x0b_formula"


def read_cubdec(path, marker_transparent):
    with open(path, "rb") as fh:
        blob = fh.read()
    w, d, h = struct.unpack_from("<iii", blob, 0)
    cells = {}
    for z in range(h):
        for y in range(d):
            row = 12 + ((z * d + y) * w) * 3
            for x in range(w):
                o = row + x * 3
                r, g, b = blob[o], blob[o + 1], blob[o + 2]
                if r == 0 and g == 0 and b == 0:
                    continue
                if marker_transparent and (r, g, b) in ((255, 0, 0), (0, 255, 0), (0, 0, 255)):
                    continue
                cells[(x, y, z)] = (r, g, b)
    return w, d, h, cells


def front_view(cells, w, d, h):
    """X по горизонтали, Z по вертикали (Z сверху вниз), глубина свёрнута."""
    grid = [[0] * w for _ in range(h)]
    for (x, y, z) in cells:
        grid[z][x] += 1
    lines = []
    for z in range(h - 1, -1, -1):
        lines.append("".join("." if v == 0 else str(min(v, 9)) for v in grid[z]))
    return lines


def side_view(cells, w, d, h):
    """Y (глубина) по горизонтали, Z по вертикали."""
    grid = [[0] * d for _ in range(h)]
    for (x, y, z) in cells:
        grid[z][y] += 1
    lines = []
    for z in range(h - 1, -1, -1):
        lines.append("".join("." if v == 0 else str(min(v, 9)) for v in grid[z]))
    return lines


def top_view(cells, w, d, h):
    grid = [[0] * w for _ in range(d)]
    for (x, y, z) in cells:
        grid[y][x] += 1
    lines = []
    for y in range(d - 1, -1, -1):
        lines.append("".join("." if v == 0 else str(min(v, 9)) for v in grid[y]))
    return lines


def slice_at(cells, w, d, z):
    grid = [["." for _ in range(w)] for _ in range(d)]
    for (x, y, zz), rgb in cells.items():
        if zz == z:
            grid[y][x] = "#"
    return ["".join(row) for row in grid[::-1]]


def main(argv=None):
    ap = argparse.ArgumentParser()
    ap.add_argument("--actor", default=DEFAULT_ACTOR)
    args = ap.parse_args(argv)
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    with open(os.path.join(args.actor, "rig.json"), "r", encoding="utf-8") as fh:
        rig = json.load(fh)
    print(f"rig={rig['rig']} variant={rig['variant']} placementScale={rig.get('placementScale')}")
    for part in rig["parts"]:
        path = os.path.join(args.actor, part["file"])
        w, d, h, cells = read_cubdec(path, bool(part.get("markerTransparent")))
        xs = [c[0] for c in cells]
        ys = [c[1] for c in cells]
        zs = [c[2] for c in cells]
        print("=" * 78)
        print(f"PART {part['name']}  model={part['model']}  grid={w}x{d}x{h}  solid={len(cells)}")
        if cells:
            print(f"  bbox X[{min(xs)}..{max(xs)}] Y[{min(ys)}..{max(ys)}] Z[{min(zs)}..{max(zs)}]")
        counts = collections.Counter(cells.values())
        print("  palette: " + ", ".join(
            f"#{r:02x}{g:02x}{b:02x}x{n}" for (r, g, b), n in counts.most_common(8)))
        print("  --- FRONT (x right, z up) ---")
        for line in front_view(cells, w, d, h):
            print("   " + line)
        print("  --- SIDE (y right, z up) ---")
        for line in side_view(cells, w, d, h):
            print("   " + line)
        print("  --- TOP (x right, y up) ---")
        for line in top_view(cells, w, d, h):
            print("   " + line)
        # каждый 4-й срез по высоте, чтобы понять форму головы
        step = max(1, h // 6)
        for z in range(0, h, step):
            print(f"  --- SLICE z={z} ---")
            for line in slice_at(cells, w, d, z):
                print("   " + line)
    return 0


if __name__ == "__main__":
    sys.exit(main())
