"""Replace a bounded voxel surface with steps on a half-size grid."""

from collections import defaultdict
from copy import copy
from itertools import product
import math

import model


SUBCELLS = tuple(product(range(2), repeat=3))


def _add(point, offset):
    return tuple(a + b for a, b in zip(point, offset))


def _parent(point):
    return tuple(value // 2 for value in point)


def _subcells(cell):
    x, y, z = cell
    return ((2 * x + i, 2 * y + j, 2 * z + k) for i, j, k in SUBCELLS)


def _sample_columns(faces, bounds):
    """Intersect each fine-grid column with the shaped shell along depth."""
    left, right, bottom, top = bounds
    columns = defaultdict(set)
    for quad in faces:
        for a, b, c in ((quad[0], quad[1], quad[2]), (quad[0], quad[2], quad[3])):
            ux, uy, uz = (q - p for p, q in zip(a, b))
            vx, vy, vz = (q - p for p, q in zip(a, c))
            area = ux * vy - uy * vx
            if abs(area) < 1e-9:
                continue
            x0, x1 = max(2 * left, math.floor(2 * min(a[0], b[0], c[0]))), min(
                2 * right, math.ceil(2 * max(a[0], b[0], c[0])))
            y0, y1 = max(2 * bottom, math.floor(2 * min(a[1], b[1], c[1]))), min(
                2 * top, math.ceil(2 * max(a[1], b[1], c[1])))
            for y in range(y0, y1):
                for x in range(x0, x1):
                    px, py = (x + .5) / 2 - a[0], (y + .5) / 2 - a[1]
                    u = (px * vy - py * vx) / area
                    v = (ux * py - uy * px) / area
                    if min(u, v, 1 - u - v) >= -1e-8:
                        depth = round(a[2] + u * uz + v * vz, 7)
                        columns[x, y].add((depth, -1 if area > 0 else 1))
    return columns


def _detail_cells(skin, shaped_faces, bounds, fine_at, mirror_x):
    left, right, bottom, top = bounds

    def inside(cell):
        x, y, _ = cell
        return left <= x < right and bottom <= y < top

    detail = copy(skin)
    # A coarse collar supplies the unchanged neighbors during voxel stitching.
    detail.cells = {fine: tone for cell, tone in skin.cells.items()
                    if left - 1 <= cell[0] <= right and bottom - 1 <= cell[1] <= top
                    and not inside(cell) for fine in _subcells(cell)}
    for (x, y), depths in _sample_columns(shaped_faces, bounds).items():
        winding = 0
        last = min(depth for depth, _ in depths)
        for depth, delta in sorted(depths):
            if winding > 0:
                start, end = (math.ceil(2 * value - .5) for value in (last, depth))
                for z in range(start, end):
                    point = x, y, z
                    detail.cells[point] = skin.cells.get(_parent(point), "skin")
            winding += delta
            last = depth
        if winding:
            raise ValueError(f"Unclosed voxel column at {(x, y)}")
    refined = defaultdict(set)
    for point in detail.cells:
        if inside(_parent(point)):
            refined[_parent(point)].add(point)

    for cell, points in refined.items():
        if len(points) == len(SUBCELLS) or fine_at(cell):
            continue
        if len(points) >= len(SUBCELLS) // 2:
            tone = skin.cells.get(cell, "skin")
            detail.cells.update((point, tone) for point in _subcells(cell))
        else:
            for point in points:
                del detail.cells[point]
    detail.stitch_diagonals()
    if mirror_x:
        # Use one side of the seam repair on both halves of the mannequin.
        right_side = {point: tone for point, tone in detail.cells.items() if point[0] >= 0}
        detail.cells = right_side | {(-x - 1, y, z): tone for (x, y, z), tone in right_side.items()}

    refined.clear()
    for point in detail.cells:
        if inside(_parent(point)):
            refined[_parent(point)].add(point)

    # Only full cells belong to the old grid. The partial cells supply the
    # smaller cubes and their interface with adjacent full cells.
    partial = {}
    for cell in skin.cells.keys() | refined.keys():
        if not inside(cell):
            continue
        fine = refined.get(cell, ())
        tone = skin.cells.get(cell, "skin")
        if len(fine) == len(SUBCELLS):
            skin.cells[cell] = tone
        else:
            skin.cells.pop(cell, None)
            if fine:
                partial[cell] = fine, tone
    return partial


def surface(skin, shaped_faces, bounds, fine_at, *, mirror_x=False):
    partial = _detail_cells(skin, shaped_faces, bounds, fine_at, mirror_x)
    occupied = {point for points, _ in partial.values() for point in points}
    detail_faces = []

    for cell, (points, tone) in sorted(partial.items()):
        for point in sorted(points):
            for direction, corners in model.FACE:
                outside = _add(point, direction)
                if outside not in occupied and _parent(outside) not in skin.cells:
                    detail_faces.append((tuple(_add(point, corner) for corner in corners),
                                         cell, tone, direction))

    face_corners = dict(model.FACE)
    for cell, direction in sorted({(_add(cell, outward), tuple(-v for v in outward))
                                   for cell in partial for outward, _ in model.FACE
                                   if _add(cell, outward) in skin.cells}):
        skin.open_faces.add((cell, direction))
        corners = face_corners[direction]
        other = _add(cell, direction)
        for point in _subcells(cell):
            outside = _add(point, direction)
            if _parent(outside) == other and outside not in occupied:
                detail_faces.append((tuple(_add(point, corner) for corner in corners),
                                     cell, skin.cells[cell], direction))

    fine_vertices = {point for face, _, _, _ in detail_faces for point in face}

    def coarse_units(points):
        return tuple(tuple(value / 2 for value in point) for point in points)

    for face, cell, tone, direction in detail_faces:
        yield coarse_units(face), cell, tone, direction

    left, right, bottom, top = bounds
    for cell, tone in skin.cells.items():
        x, y, _ = cell
        if not (left - 1 <= x <= right and bottom - 1 <= y <= top):
            continue
        for direction, corners in model.FACE:
            if (cell, direction) in skin.open_faces or _add(cell, direction) in skin.cells:
                continue
            rim = []
            for a, b in zip(corners, corners[1:] + corners[:1]):
                start = tuple(2 * (p + q) for p, q in zip(cell, a))
                mid = tuple(2 * p + u + v for p, u, v in zip(cell, a, b))
                rim.append(start)
                if mid in fine_vertices:
                    rim.append(mid)
            if len(rim) == 4:
                continue
            # Split only the shared edges of the large face to weld both grids.
            skin.open_faces.add((cell, direction))
            center = tuple(2 * p + (u + v) for p, u, v in zip(cell, corners[0], corners[2]))
            for start, end in zip(rim, rim[1:] + rim[:1]):
                yield coarse_units((center, start, end)), cell, tone, direction
