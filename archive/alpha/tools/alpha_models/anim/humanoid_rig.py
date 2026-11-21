"""Rig description of the Alpha 2013 humanoid as drawn by ``Creature_drawModel`` (0x4128F0).

Every entry is backed by the shipped machine code (call-site VAs inside drawModel) and by the
decompiled draw block that builds the part matrix (line numbers of the CubeWorld-Reversal
``GameController.cpp`` reconstruction, kept locally in ``.scratch``):

* the part matrix is ``M = T(-anchor(size)) * S * R... * parentCopy`` (row-vector convention),
  then ``render_uploadModelViewMatrices(M)`` and ``GameController_draw_mesh`` (thiscall, ECX=mesh);
* ``anchor(size)`` is the point of the model grid that sits on the joint. For every humanoid
  body part it is the grid centre ``size/2`` (``getField_0x44/48`` and ``getter_off4c`` are the
  model's X/Y/Z voxel extents). For the weapon it depends on the weapon class and on whether the
  weapon is in the hand or on the back (see ``weapon_anchor``).

The joint frame ``J = T(anchor(capturedSize)) * M * P^-1`` (P = creature placement) is
therefore model-size independent, which is what lets a clip captured on one model drive a
compatible model of a different size: ``M' = T(-anchor(newSize)) * J * P'``.

LIMITS (do not generalise silently):
* only the humanoid draw path with 8 drawn parts (body, head, hair, weapon, 2 hands, 2 feet) has
  been captured and verified; shoulders/chest armour/back items (other call sites) are listed as
  ``UNVERIFIED`` and are not mapped;
* the weapon anchor rule is proven for item category 3 subtypes 15/16/17 (greatsword /
  greataxe / greatmace) in hand, and for the generic "on the back" branch; other weapon
  subtypes use other offsets (``weapon_anchor`` raises for them).
"""
from __future__ import annotations

from dataclasses import dataclass

RIG_ID = "alpha_humanoid_v1"


@dataclass(frozen=True)
class RigPart:
    name: str
    draw_site: int      # VA of `call GameController_draw_mesh`
    upload_site: int    # VA of `call render_uploadModelViewMatrices`
    alpha_slot: str     # matrix slot in the draw object (self[0x3b + 16k])
    anchor: str         # "center" | "weapon"
    mirrored: bool      # Alpha draws it with scale(-s, s, s)
    evidence: str


PARTS = (
    RigPart("body", 0x41FD96, 0x41FD6F, "0x5b (part 2)", "center", False,
            "GameController.cpp:6108 translate(-sx/2,-sy/2,-sz/2); model slot self[0x31] <- creature+0x94"),
    RigPart("head", 0x4206C2, 0x4206B7, "0x6b (part 3)", "center", False,
            "GameController.cpp:6270 translate(-sx/2,-sy/2,-sz/2) (non-special branch)"),
    RigPart("hair", 0x420A38, 0x420A2D, "0x7b (part 4)", "center", False,
            "GameController.cpp:6327 same matrix chain as head; captured matrices identical to head"),
    RigPart("weapon", 0x421A01, 0x4219DE, "0xab (part 7) <- copy of part 1 (hand)", "weapon", False,
            "GameController.cpp:6528-6663 weapon block; anchor depends on action / subtype"),
    RigPart("hand_l", 0x423214, 0x4231F1, "0xcb (part 9)", "center", False,
            "GameController.cpp:7162 scale(s,s,s) translate(-size/2)"),
    RigPart("hand_r", 0x4235B1, 0x4235A6, "0xdb (part 10)", "center", True,
            "GameController.cpp:7229 scale(-s,s,s) translate(-size/2)"),
    RigPart("foot_l", 0x423AFA, 0x423AD7, "0x12b (part 15)", "center", False,
            "GameController.cpp:7337 translate(-size/2)"),
    RigPart("foot_r", 0x423D1F, 0x423CFC, "0x13b (part 16)", "center", False,
            "GameController.cpp:7373 translate(-size/2)"),
)

BY_DRAW_SITE = {p.draw_site: p for p in PARTS}
BY_NAME = {p.name: p for p in PARTS}

# Actions (creature+0x68) for which the weapon is drawn ON THE BACK (GameController.cpp:6548-6556).
WEAPON_ON_BACK_ACTIONS = frozenset(
    [0x00, 0x15, ord("m"), ord("j"), ord("k"), ord("i"), ord("["), ord("`"), ord("P"),
     ord("R"), ord("U"), ord("O"), ord("S"), ord("Q"), ord("T")])
# Weapon subtypes (creature+0xaa9, item category 3) whose IN-HAND anchor is (sx/2, sy/2, 4):
# the generic `else { fVar2 = -4.0; goto LAB_0042198a }` branch (GameController.cpp:6640-6655).
TWO_HANDED_SUBTYPES = {15: "greatsword", 16: "greataxe", 17: "greatmace"}
# Two-handed M1 attack chain chosen by combat_selectAttackAnim (0x445020) for subtypes 15/16/17:
#   action 57 ('9') -> 67 ('C') -> 58 (':'); drawModel states 2 -> 37 -> 3.
TWO_HANDED_ATTACK_ACTIONS = {57: 2, 67: 37, 58: 3}


def weapon_anchor(size, on_back, subtype=None):
    sx, sy, sz = size
    if on_back:
        # back branch: translate(-sx/2, -sy/2, -sz/2) after the fixed back rotation
        return (sx / 2.0, sy / 2.0, sz / 2.0)
    if subtype is not None and subtype not in TWO_HANDED_SUBTYPES:
        raise ValueError(f"in-hand anchor for weapon subtype {subtype} is not verified")
    return (sx / 2.0, sy / 2.0, 4.0)


def anchor_for(part, size, on_back=None, subtype=None):
    if part.anchor == "center":
        return (size[0] / 2.0, size[1] / 2.0, size[2] / 2.0)
    if part.anchor == "weapon":
        if on_back is None:
            raise ValueError("weapon anchor needs on_back")
        return weapon_anchor(size, on_back, subtype)
    raise ValueError(f"unknown anchor rule {part.anchor}")


def check_model_compatibility(model_sizes):
    """Return a list of problems for a {part: (sx,sy,sz)} model set against this rig.

    The rig itself places parts by joints, so any size works geometrically; the checks catch
    inputs that cannot be what the rig expects (missing parts, zero/negative sizes, sizes far
    outside the humanoid range the clips were captured with).
    """
    problems = []
    for part in PARTS:
        if part.name not in model_sizes:
            problems.append(f"MISSING_PART {part.name}")
            continue
        size = model_sizes[part.name]
        if len(size) != 3 or any((not isinstance(v, int)) or v <= 0 for v in size):
            problems.append(f"BAD_SIZE {part.name} {size}")
            continue
        limits = {"body": 24, "head": 32, "hair": 32, "weapon": 64,
                  "hand_l": 12, "hand_r": 12, "foot_l": 12, "foot_r": 12}
        if max(size) > limits[part.name]:
            problems.append(f"SIZE_OUT_OF_RANGE {part.name} {size} max>{limits[part.name]}")
    extra = sorted(set(model_sizes) - set(BY_NAME))
    for name in extra:
        problems.append(f"UNKNOWN_PART {name}")
    if "hand_l" in model_sizes and "hand_r" in model_sizes and \
            tuple(model_sizes["hand_l"]) != tuple(model_sizes["hand_r"]):
        problems.append("HANDS_DIFFER Alpha draws both hands from ONE model (mirrored)")
    if "foot_l" in model_sizes and "foot_r" in model_sizes and \
            tuple(model_sizes["foot_l"]) != tuple(model_sizes["foot_r"]):
        problems.append("FEET_DIFFER Alpha draws both feet from ONE model")
    return problems
