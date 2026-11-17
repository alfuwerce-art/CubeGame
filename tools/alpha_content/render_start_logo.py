"""P6 — renders the start.plx logo nodes ("cubeworld", "picroma") into the local content cache (never committed).

Alpha draws the start screen title as vector shapes of ``start.plx`` (no bitmap exists). This tool reads the file
read-only through ``tools/alpha_research/plx_dump.py`` and rasterises the two node trees exactly as stored:

  * meshes with shape flag 0x10 are pre-tessellated triangle strips (the outline bands of every letter);
  * meshes with flags 1/3 are paths: the ``face`` list holds the contours (count, indices…), ``vertexFlags`` marks
    on-curve (1) and off-curve (2) points with TrueType quadratic rules; filled even-odd, stroked with
    ``strokeWidths`` in the stroke colour;
  * colours are the per-vertex colours (vertical gradient of the path's vertex colours; per-triangle mean for strips);
  * a node whose Display has ``clipping`` = 1 is a gradient mesh (grid of vertex colours) clipped by its children:
    the children's shapes are painted with the bilinear gradient of that grid instead of their own fill;
  * container meshes (shape flag 0) are not drawn; node transforms are applied as in plx_dump.

Output: ``<stage>/start-logo.png`` and ``<stage>/start-picroma.png`` at Alpha's on-screen pixel size (node scale
1.954 / 1.222), 2× supersampled. Status: ALPHA_DATA geometry, renderer = EMULATED (Plasma's own smooth-mesh
renderer and blur are not ported; edges and gradients may differ slightly).

    python tools/alpha_content/render_start_logo.py --plx "<Cube world>/start.plx" --stage .scratch/content/gui
"""
from __future__ import annotations

import argparse
import math
import struct
import sys
import zlib
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools" / "alpha_research"))
from plx_dump import Plx, first, floats, i32, wide  # noqa: E402

SUPER = 2


def ints(raw) -> tuple:
    raw = first(raw)
    return struct.unpack("<%di" % (len(raw) // 4), raw[:len(raw) // 4 * 4])


def quad_contour(points, flags):
    """TrueType quadratic contour → polyline."""
    n = len(points)
    if n < 3:
        return points
    start = next((k for k in range(n) if flags[k] != 2), None)
    if start is None:
        return points
    pts = [points[(start + k) % n] for k in range(n)] + [points[start]]
    fl = [flags[(start + k) % n] for k in range(n)] + [1]
    out = [pts[0]]
    k = 1
    while k < len(pts):
        if fl[k] != 2:
            out.append(pts[k])
            k += 1
            continue
        ctrl = pts[k]
        nxt = pts[k + 1] if k + 1 < len(pts) else pts[0]
        end = nxt if fl[k + 1] != 2 else ((ctrl[0] + nxt[0]) / 2, (ctrl[1] + nxt[1]) / 2)
        p0 = out[-1]
        for s in range(1, 9):
            t = s / 8
            out.append(((1 - t) ** 2 * p0[0] + 2 * (1 - t) * t * ctrl[0] + t * t * end[0],
                        (1 - t) ** 2 * p0[1] + 2 * (1 - t) * t * ctrl[1] + t * t * end[1]))
        k += 1 if fl[k + 1] == 2 else 2
    return out


class Canvas:
    def __init__(self, w, h):
        self.w, self.h = w, h
        self.px = [[0.0, 0.0, 0.0, 0.0] for _ in range(w * h)]

    def blend(self, x, y, c):
        if 0 <= x < self.w and 0 <= y < self.h:
            d = self.px[y * self.w + x]
            a = c[3]
            out_a = a + d[3] * (1 - a)
            if out_a <= 0:
                return
            for i in range(3):
                d[i] = (c[i] * a + d[i] * d[3] * (1 - a)) / out_a
            d[3] = out_a

    def fill_contours(self, contours, color_at):
        edges = []
        for c in contours:
            for i in range(len(c)):
                a, b = c[i], c[(i + 1) % len(c)]
                if a[1] != b[1]:
                    edges.append((a, b))
        if not edges:
            return
        y0 = max(0, int(min(min(a[1], b[1]) for a, b in edges)))
        y1 = min(self.h - 1, int(max(max(a[1], b[1]) for a, b in edges)) + 1)
        for y in range(y0, y1 + 1):
            yc = y + 0.5
            xs = sorted(a[0] + (yc - a[1]) * (b[0] - a[0]) / (b[1] - a[1]) for a, b in edges if (a[1] > yc) != (b[1] > yc))
            for k in range(0, len(xs) - 1, 2):
                for x in range(max(0, int(math.ceil(xs[k] - 0.5))), min(self.w, int(math.floor(xs[k + 1] - 0.5)) + 1)):
                    self.blend(x, y, color_at(x, y))

    def triangle(self, t, color):
        (ax, ay), (bx, by), (cx, cy) = t
        area = (bx - ax) * (cy - ay) - (cx - ax) * (by - ay)
        if abs(area) < 1e-9:
            return
        for y in range(max(0, int(min(ay, by, cy))), min(self.h, int(max(ay, by, cy)) + 1)):
            for x in range(max(0, int(min(ax, bx, cx))), min(self.w, int(max(ax, bx, cx)) + 1)):
                px, py = x + 0.5, y + 0.5
                w0 = (bx - px) * (cy - py) - (cx - px) * (by - py)
                w1 = (cx - px) * (ay - py) - (ax - px) * (cy - py)
                w2 = (ax - px) * (by - py) - (bx - px) * (ay - py)
                if (w0 >= 0 and w1 >= 0 and w2 >= 0) or (w0 <= 0 and w1 <= 0 and w2 <= 0):
                    self.blend(x, y, color)

    def stroke(self, contours, width, color):
        r = width / 2
        if r <= 0.3 or color[3] <= 0:
            return
        stamp = [(dx, dy) for dy in range(-int(r) - 1, int(r) + 2) for dx in range(-int(r) - 1, int(r) + 2)
                 if dx * dx + dy * dy <= r * r]
        seen = set()
        for c in contours:
            for i in range(len(c)):
                a, b = c[i], c[(i + 1) % len(c)]
                steps = max(1, int(math.hypot(b[0] - a[0], b[1] - a[1])))
                for s in range(steps + 1):
                    x = int(a[0] + (b[0] - a[0]) * s / steps)
                    y = int(a[1] + (b[1] - a[1]) * s / steps)
                    for dx, dy in stamp:
                        key = (x + dx, y + dy)
                        if key not in seen:
                            seen.add(key)
                            self.blend(key[0], key[1], color)

    def png(self, path: Path):
        w, h = self.w // SUPER, self.h // SUPER
        rows = []
        for y in range(h):
            row = bytearray([0])
            for x in range(w):
                acc = [0.0, 0.0, 0.0, 0.0]
                for sy in range(SUPER):
                    for sx in range(SUPER):
                        p = self.px[(y * SUPER + sy) * self.w + x * SUPER + sx]
                        for i in range(3):
                            acc[i] += p[i] * p[3]
                        acc[3] += p[3]
                a = acc[3] / (SUPER * SUPER)
                rgb = [acc[i] / acc[3] if acc[3] > 0 else 0 for i in range(3)]
                row += bytes(int(max(0, min(1, v)) * 255 + 0.5) for v in rgb + [a])
            rows.append(bytes(row))

        def chunk(tag, data):
            return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)

        path.write_bytes(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 6, 0, 0, 0))
                         + chunk(b"IDAT", zlib.compress(b"".join(rows), 9)) + chunk(b"IEND", b""))


def render(plx: Plx, root_name: str, out: Path, margin: float = 10.0) -> tuple[int, int]:
    nodes = plx.objects["Node"]
    transforms = plx.objects["Transformation"]
    shapes = [o for o in plx.order if o["_type"] in ("SmoothMeshShape", "TextShape")]
    root = next(i for i, n in enumerate(nodes) if wide(n["wname"][0]) == root_name)

    def transform(index):
        t = transforms[index]
        return floats(first(t["translation"][0])), floats(first(t["pivot"][0])), floats(first(t["deformation"][0]))

    def apply(m, p):
        (tx, ty), (px, py), d = m[0][:2], m[1][:2], m[2]
        x, y = p[0] - px, p[1] - py
        return d[0] * x + d[4] * y + px + tx, d[1] * x + d[5] * y + py + ty

    items = []
    displays = plx.objects["Display"]

    def walk(index, chain, clip):
        node = nodes[index]
        chain = [transform(i32(node["transformation"][0]))] + chain
        si = i32(node["shape"][0])
        clipping = i32(displays[i32(node["display"][0])]["clipping"][0]) == 1
        if si >= 0 and shapes[si]["_type"] == "SmoothMeshShape":
            if clipping:
                clip = (shapes[si], chain)
            else:
                items.append((shapes[si], chain, clip))
        for child in node.get("child", []):
            walk(i32(child), chain, clip)

    walk(root, [], None)
    def placed(shape, chain):
        pos = shape["vertexPositions"][0]
        count = i32(pos[0])
        xy = floats(pos[1])[:2 * count]
        result = []
        for k in range(count):
            p = (xy[2 * k], xy[2 * k + 1])
            for m in chain:
                p = apply(m, p)
            result.append(p)
        colors = floats(shape["vertexColors"][0][1])
        return result, [tuple(colors[4 * k:4 * k + 4]) for k in range(count)]

    drawn = []
    for shape, chain, clip in items:
        flags = ints(shape["flags"][0])[0]
        if flags == 0:
            continue
        pos = shape["vertexPositions"][0]
        count = i32(pos[0])
        xy = floats(pos[1])[:2 * count]
        pts = []
        for k in range(count):
            p = (xy[2 * k], xy[2 * k + 1])
            for m in chain:
                p = apply(m, p)
            pts.append(p)
        colors = floats(shape["vertexColors"][0][1])
        vcol = [tuple(colors[4 * k:4 * k + 4]) for k in range(count)]
        stroke = floats(shape["strokeColors"][0][1])[:4]
        width = floats(shape["strokeWidths"][0][1])[0] if len(shape["strokeWidths"][0]) > 1 else 0.0
        scale = math.hypot(*[c for c in chain[0][2][:2]]) if chain else 1.0
        total_scale = 1.0
        for m in chain:
            total_scale *= math.hypot(m[2][0], m[2][1])
        drawn.append((flags, shape, pts, vcol, stroke, width * total_scale, placed(*clip) if clip else None))

    allpts = [p for d in drawn for p in d[2]]
    x0 = min(p[0] for p in allpts) - margin
    y0 = min(p[1] for p in allpts) - margin
    x1 = max(p[0] for p in allpts) + margin
    y1 = max(p[1] for p in allpts) + margin
    canvas = Canvas(int((x1 - x0) * SUPER) + 1, int((y1 - y0) * SUPER) + 1)

    def to_canvas(p):
        return ((p[0] - x0) * SUPER, (p[1] - y0) * SUPER)

    for flags, shape, pts, vcol, stroke, width, clip in drawn:
        cpts = [to_canvas(p) for p in pts]
        if flags & 0x10:
            for k in range(len(cpts) - 2):
                c = tuple(sum(vcol[k + j][i] for j in range(3)) / 3 for i in range(4))
                canvas.triangle((cpts[k], cpts[k + 1], cpts[k + 2]), c)
            continue
        vflags = ints(shape["vertexFlags"][0])[1:]
        face = ints(shape["face"][0])
        contours, at = [], 0
        while at < len(face):
            n = face[at]
            idx = face[at + 1:at + 1 + n]
            at += 1 + n
            if n >= 3:
                contours.append(quad_contour([cpts[i] for i in idx], [vflags[i] if i < len(vflags) else 1 for i in idx]))
        ys = [p[1] for p in cpts]
        lo, hi = min(ys), max(ys)
        mid = (lo + hi) / 2
        top = [vcol[i] for i in range(len(cpts)) if cpts[i][1] <= mid] or vcol
        bottom = [vcol[i] for i in range(len(cpts)) if cpts[i][1] > mid] or vcol
        ct = tuple(sum(c[i] for c in top) / len(top) for i in range(4))
        cb = tuple(sum(c[i] for c in bottom) / len(bottom) for i in range(4))

        def color_at(x, y, ct=ct, cb=cb, lo=lo, hi=hi):
            t = 0.0 if hi <= lo else min(1.0, max(0.0, (y - lo) / (hi - lo)))
            return tuple(ct[i] + (cb[i] - ct[i]) * t for i in range(4))

        if clip is not None:
            grid = {}
            for p, c in zip(*clip):
                q = to_canvas(p)
                grid[(round(q[0], 1), round(q[1], 1))] = c
            gx = sorted({k[0] for k in grid})
            gy = sorted({k[1] for k in grid})

            def color_at(x, y, grid=grid, gx=gx, gy=gy):
                def span(v, axis):
                    for i in range(len(axis) - 1):
                        if v <= axis[i + 1]:
                            return i, min(1.0, max(0.0, (v - axis[i]) / (axis[i + 1] - axis[i])))
                    return len(axis) - 2, 1.0
                i, tx = span(x, gx)
                j, ty = span(y, gy)
                corners = [grid.get((gx[i + a], gy[j + b])) for a, b in ((0, 0), (1, 0), (0, 1), (1, 1))]
                if any(c is None for c in corners):
                    return min(grid.items(), key=lambda kv: (kv[0][0] - x) ** 2 + (kv[0][1] - y) ** 2)[1]
                return tuple((corners[0][k] * (1 - tx) + corners[1][k] * tx) * (1 - ty)
                             + (corners[2][k] * (1 - tx) + corners[3][k] * tx) * ty for k in range(4))

        canvas.fill_contours(contours, color_at)
        if width > 0 and stroke[3] > 0:
            canvas.stroke(contours, width * SUPER, stroke)
    canvas.png(out)
    return canvas.w // SUPER, canvas.h // SUPER


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--plx", default=r"D:\Games\Cube world\start.plx")
    ap.add_argument("--stage", default=str(ROOT / ".scratch/content/gui"))
    a = ap.parse_args()
    stage = Path(a.stage)
    stage.mkdir(parents=True, exist_ok=True)
    plx = Plx(a.plx)
    for node, name in (("cubeworld", "start-logo.png"), ("picroma", "start-picroma.png")):
        size = render(plx, node, stage / name)
        print(f"START_LOGO {node} -> {stage / name} {size[0]}x{size[1]}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
