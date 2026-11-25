"""Shared mannequin head on the approved coarse voxel grid."""

import model


WIDTH = 32
HEIGHT = 34
DEPTH = 31.5
FACE_PLANE = 19.5

# Half-width in columns and the inclusive depth range, from chin to crown.
# The jaw and cheeks share the same front column.
ROWS = ((2, 2, 8), (3, 1, 8)) + ((4, 0, 8),) * 6 + ((3, 1, 7), (2, 2, 6))


def build(scale, base):
    model.Volume("Head", model.fixed("Head"))
    cells = set()
    for y, (half_width, rear, front) in enumerate(ROWS):
        for x in range(-half_width, half_width + 1):
            for z in range(rear, front + 1):
                # Set the outer side panels back by one whole block at each end.
                if half_width == 4 and abs(x) == half_width and z in (rear, front):
                    continue
                cells.add((x, y, z))

    width_step, height_step, depth_step = WIDTH / 9, HEIGHT / len(ROWS), DEPTH / 9
    back_plane = FACE_PLANE - DEPTH
    faces = []
    for x, y, z in sorted(cells):
        for normal, corners in model.FACE:
            nx, ny, nz = normal
            if (x + nx, y + ny, z + nz) in cells:
                continue
            face = tuple((((x + i) - .5) * width_step * scale,
                          base + (y + j) * height_step * scale,
                          (back_plane + (z + k) * depth_step) * scale)
                         for i, j, k in corners)
            faces.append((face, "skin"))
    return faces
