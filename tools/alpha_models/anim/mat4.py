"""Tiny row-vector 4x4 helpers (stdlib only) for Alpha matrices.

Alpha (Direct3D 9) uses the row-vector convention: ``world_row = v_row @ M``; rows 0..2 of
``M`` are the (scaled) basis axes, row 3 is the translation. All matrices are flat lists of
16 floats in memory order.
"""
from __future__ import annotations

import math

IDENTITY = [1.0, 0, 0, 0, 0, 1.0, 0, 0, 0, 0, 1.0, 0, 0, 0, 0, 1.0]


def mul(a, b):
    """a @ b (apply a first, then b, in row-vector convention)."""
    return [sum(a[r * 4 + k] * b[k * 4 + c] for k in range(4)) for r in range(4) for c in range(4)]


def det3(m):
    a = [[m[r * 4 + c] for c in range(3)] for r in range(3)]
    return (a[0][0] * (a[1][1] * a[2][2] - a[1][2] * a[2][1])
            - a[0][1] * (a[1][0] * a[2][2] - a[1][2] * a[2][0])
            + a[0][2] * (a[1][0] * a[2][1] - a[1][1] * a[2][0]))


def inverse_affine(m):
    a = [[m[r * 4 + c] for c in range(3)] for r in range(3)]
    det = det3(m)
    if abs(det) < 1e-12:
        raise ValueError("singular matrix")
    inv = [[0.0] * 3 for _ in range(3)]
    for i in range(3):
        for j in range(3):
            minor = [[a[x][y] for y in range(3) if y != i] for x in range(3) if x != j]
            inv[i][j] = ((-1) ** (i + j)) * (minor[0][0] * minor[1][1] - minor[0][1] * minor[1][0]) / det
    t = m[12:15]
    ti = [-sum(t[k] * inv[k][c] for k in range(3)) for c in range(3)]
    return [inv[0][0], inv[0][1], inv[0][2], 0.0, inv[1][0], inv[1][1], inv[1][2], 0.0,
            inv[2][0], inv[2][1], inv[2][2], 0.0, ti[0], ti[1], ti[2], 1.0]


def transform_point(m, p):
    return [p[0] * m[0] + p[1] * m[4] + p[2] * m[8] + m[12],
            p[0] * m[1] + p[1] * m[5] + p[2] * m[9] + m[13],
            p[0] * m[2] + p[1] * m[6] + p[2] * m[10] + m[14]]


def axis_scales(m):
    return [math.sqrt(m[r * 4] ** 2 + m[r * 4 + 1] ** 2 + m[r * 4 + 2] ** 2) for r in range(3)]


def max_abs_diff(a, b):
    return max(abs(x - y) for x, y in zip(a, b))


def translate(x, y, z):
    return [1.0, 0, 0, 0, 0, 1.0, 0, 0, 0, 0, 1.0, 0, x, y, z, 1.0]
