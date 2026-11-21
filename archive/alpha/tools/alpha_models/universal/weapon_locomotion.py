"""Separate two-handed back-weapon layer; the established body walk formula is unchanged.

drawModel weapon block 0x4216xx..0x421A01, action 0, subtypes 15/16/17.
This formula is RECONSTRUCTED_FROM_EVIDENCE, checked against the offline oracle.
Matrices use the fixed back anchor (model centre), including during action clips.
"""
import math
from tools.alpha_models.anim import mat4
from tools.alpha_models.universal import param_walk as pw


def joint(app, state, subtype):
    if subtype not in (15, 16, 17):
        raise ValueError(f"WEAPON_LOCOMOTION_SUBTYPE_UNVERIFIED {subtype}")
    k = pw.core(app, state)
    return pw.chain([pw.rot_z(math.degrees(k["w"] * .25)), mat4.translate(0, -7.1, .1),
                     pw.rot_y(-135), pw.rot_z(90), pw.scale(app["scale_weapon"])],
                    mat4.translate(0, 0, k["bob"]))
