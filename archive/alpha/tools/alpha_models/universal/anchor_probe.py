"""Derive the anchor rule of a drawModel block by changing the model size in the emulator.

Every drawn part is M = T(-anchor(size)) * J * P. Running Alpha's drawModel twice with only one
model dimension changed gives anchor(size) as a function of size, and shows whether J itself
depends on the size (which would make the block not transferable by joint frames alone).
"""
from __future__ import annotations

from tools.alpha_models.anim import mat4


def _inv3(m):
    a = [[m[r * 4 + c] for c in range(3)] for r in range(3)]
    det = (a[0][0] * (a[1][1] * a[2][2] - a[1][2] * a[2][1]) - a[0][1] * (a[1][0] * a[2][2] - a[1][2] * a[2][0])
           + a[0][2] * (a[1][0] * a[2][1] - a[1][1] * a[2][0]))
    inv = [[0.0] * 3 for _ in range(3)]
    for i in range(3):
        for j in range(3):
            minor = [[a[x][y] for y in range(3) if y != i] for x in range(3) if x != j]
            inv[i][j] = ((-1) ** (i + j)) * (minor[0][0] * minor[1][1] - minor[0][1] * minor[1][0]) / det
    return inv


def probe(make_emulator, model_index, base_size, site, frames=(600, 600, 16), delta=4):
    """Return {"anchor_coeff": 3x3 d(anchor)/d(size), "anchor_const": vec, "J_depends_on_size": bool}.

    make_emulator(sizes_override) -> Emulator; the block is identified by its draw site."""
    def run(size):
        em = make_emulator({model_index: size})
        for dt in frames:
            fr = em.frame(dt_ms=dt)
        Pi = mat4.inverse_affine(fr["placement"])
        for p in fr["parts"]:
            if p["draw_site"] == site:
                return mat4.mul(p["matrix"], Pi)
        return None
    m0 = run(tuple(base_size))
    if m0 is None:
        return None
    coeff, rot_dep = [], 0.0
    for axis in range(3):
        s = list(base_size)
        s[axis] += delta
        m1 = run(tuple(s))
        rot_dep = max(rot_dep, max(abs(m1[r * 4 + c] - m0[r * 4 + c]) for r in range(3) for c in range(3)))
        dt_ = [m1[12 + i] - m0[12 + i] for i in range(3)]
        inv = _inv3(m0)              # translation row = -a * R + t_J  ->  da = -dt * R^-1
        da = [-sum(dt_[k] * inv[k][c] for k in range(3)) for c in range(3)]
        coeff.append([v / delta for v in da])
    a0 = [sum(coeff[ax][c] * base_size[ax] for ax in range(3)) for c in range(3)]
    return {"d_anchor_d_size": coeff, "anchor_at_base": a0, "rotation_changes_with_size": rot_dep > 1e-5,
            "max_rotation_delta": rot_dep}
