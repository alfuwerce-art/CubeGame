"""Local depth and waist profiles for the female mannequin's torso."""

from collections import defaultdict
from functools import cache

import model
import voxel_detail


# Heights, half-widths, and depth offsets use the existing centimeter axes.
WAIST_PROFILE = (
    (27.0, 10.2), (30.5, 9.5), (34.5, 8.25), (36.0, 8.25),
    (40.5, 9.45), (44.0, 10.65), (46.0, 11.45),
)
CHEST_HEIGHT = (
    (39.75, 0), (41.5, .2), (43.25, .75), (45, 2.14), (46.75, 3.25),
    (48, 3.10), (49.5, 2.34), (51.25, .95), (53, 0),
)
CHEST_SPAN = ((0, .6), (2.5, .87), (4.25, 1), (6.5, .9), (8.5, .48), (11.5, 0))
CHEST_LOWER_EDGE = ((0, .95), (4, 0), (6, .25), (8, .8), (11.5, 1.55))
# Blend the lower ribs into the base torso through the marked area.
ABDOMEN_HEIGHT = (
    (26, 0), (30, .4), (32, .528), (34, .28), (37, .14),
    (40, .1), (42, .17), (43.25, .25), (45, 0),
)
ABDOMEN_SPAN = ((0, .9), (2, 1), (4.5, .95), (6.25, .62), (8.5, 0))
PELVIS_HEIGHT = (
    (22.5, 0), (23.5, .13), (25, .65), (26.5, 1.24),
    (28, 1.52), (29.5, 1.34), (31.5, .87), (33.5, .35), (36, 0),
)
PELVIS_SPAN = ((0, .55), (2.25, .8), (4.75, 1), (7, .92), (8.75, .57), (11.5, 0))
PELVIS_LOWER_EDGE = ((0, .9), (2.5, .35), (5, 0), (7, .15), (9, .9), (11.5, 1.3))
PELVIS_BLEND_HEIGHT = ((28.5, 0), (30.5, 1), (33, 1), (35, 0))


def _sample(profile, position, fallback=0):
    for (low, first), (high, last) in zip(profile, profile[1:]):
        if low <= position <= high:
            return model.mix(first, last, (position - low) / (high - low))
    return fallback


def waist_width(y, fallback):
    return _sample(WAIST_PROFILE, y, fallback)


def _depth_offset(x, y, z):
    if z > 2.5:
        # Lift the lower contour at the center and flanks, keeping the collar join fixed.
        chest_y = y - _sample(CHEST_LOWER_EDGE, x) * model.clamp((48 - y) / 5)
        chest = _sample(CHEST_HEIGHT, chest_y) * _sample(CHEST_SPAN, x)
        abdomen = _sample(ABDOMEN_HEIGHT, y) * _sample(ABDOMEN_SPAN, x)
        return (chest + abdomen) * model.clamp((z - 2.5) / 3.5)
    if z < -2.5:
        pelvis_y = y - _sample(PELVIS_LOWER_EDGE, x) * model.clamp((30 - y) / 5)
        pelvis = _sample(PELVIS_HEIGHT, pelvis_y) * _sample(PELVIS_SPAN, x)
        return -pelvis * model.clamp((-z - 2.5) / 3)
    return 0


def _pelvis_blend_weight(vertex, step):
    x, y, z = vertex
    taper = _sample(PELVIS_BLEND_HEIGHT, y * step)
    rear = model.clamp((1.5 - z * step) / 2)
    flank = model.clamp((abs(x) * step - 7) / 1.5) * model.clamp((4.5 - z * step) / 2)
    return .64 * taper * max(rear, flank)


def _blend_pelvis(faces, shape, step):
    """Distribute the upper pelvis ledge across adjacent rows of flat faces."""
    neighbors = defaultdict(set)
    for _, _, _, corners in faces:
        for start, end in zip(corners, corners[1:] + corners[:1]):
            neighbors[start].add(end)
            neighbors[end].add(start)

    weights = {vertex: _pelvis_blend_weight(vertex, step) for vertex in neighbors}
    movable = {vertex: tuple(sorted(neighbors[vertex])) for vertex, weight in weights.items() if weight}
    positions = {vertex: shape(vertex) for vertex in movable}
    for _ in range(14):
        next_positions = {}
        for vertex, adjacent in movable.items():
            points = (positions.get(point, shape(point)) for point in adjacent)
            center = (sum(axis) / len(adjacent) for axis in zip(*points))
            next_positions[vertex] = tuple(model.mix(p, q, weights[vertex])
                                           for p, q in zip(positions[vertex], center))
        positions = next_positions
    return positions


def _fine_detail(cell, step):
    x, y, z = ((value + .5) * step for value in cell)
    x = abs(x)
    if 40 <= y < 53 and z >= 4:
        # Keep the broad forward planes coarse and use the finer cells at the folds.
        return x < 1.5 or x >= 7.5 or y < 44.5 or y >= 49.5 or z >= 10
    if 32 <= y < 40 and z >= 4 and x < 7.5:
        return True
    return 29 <= y < 35 and (z <= -2 or x >= 7)


def contour_faces(skin, step):
    """Use the torso profiles as a stencil for a stepped voxel surface."""
    @cache
    def shape(vertex):
        x, y, z = vertex
        depth = _depth_offset(abs(x) * step, y * step, z * step)
        return x, y, z + depth / step

    faces = []
    for cell, tone in skin.cells.items():
        x, y, z = cell
        if not (-11.5 <= x * step < 11.5 and 22.5 <= y * step < 53):
            continue
        for direction, corners in model.FACE:
            dx, dy, dz = direction
            if (x + dx, y + dy, z + dz) in skin.cells or (cell, direction) in skin.open_faces:
                continue
            original = tuple((x + a, y + b, z + c) for a, b, c in corners)
            faces.append((cell, tone, direction, original))

    pelvis = _blend_pelvis(faces, shape, step)
    shaped = (tuple(pelvis.get(point, shape(point)) for point in original)
              for _, _, _, original in faces)
    # Keep a row of full-size voxels inside the original torso boundaries for welding.
    bounds = tuple(round(value / step) for value in (-11, 11, 23, 52.5))
    yield from voxel_detail.surface(skin, shaped, bounds, lambda cell: _fine_detail(cell, step),
                                    mirror_x=True)
