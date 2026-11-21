"""Alpha 2013 item attach points: where a drawn item model sits on the creature, and when.

THE GAP THIS CLOSES (D71 / M0-00B, 2026-11-09)
------------------------------------------------
The creature pipeline already knows *that* drawModel draws an equipment block and which slot the
model comes from (``creature_rig.BLOCKS``, D64 ``equipment_probe``). What was NOT established:

* the *in-hand* anchor law of the weapon block for anything except the two-handed subtypes
  15/16/17 (``humanoid_rig.weapon_anchor`` raises for every other subtype);
* the *in-hand* anchors of the off-hand block at all (``creature_rig.EQUIPMENT_ANCHOR_BY_SUBTYPE``
  only had the subtypes that reach the in-hand branch at IDLE: 4 / 12 / 20);
* *when* a block swings from the back to the hand, i.e. the action predicate that makes the same
  item appear at two different points (``humanoid_rig.WEAPON_ON_BACK_ACTIONS`` is only the weapon
  half of it — the off-hand has its own subtype exclusions).

Everything below is taken from the weapon / off-hand draw blocks of ``Creature_drawModel``
(VA 0x4128F0, ``.scratch/CubeWorld-Reversal/cube/control/GameController.cpp`` lines 6528-6666 weapon,
6689-6865 off-hand) and independently checked against the executed bytes by
``tools/alpha_models/catalog/item_layer.py``:

* the anchor is a linear function of the model's voxel grid ``size``: ``anchor(s) = coeff*s + const``
  (the ``const`` part is why a size-sweep alone can never recover it: it is invisible to
  d(anchor)/d(size), see ``anchor_probe``);
* ``scale`` is the uniform scale the block multiplies the model with (bracelet 0.6);
* the branch predicate is a pure function of the action byte, the item subtype and one creature
  flag bit (0x124 & 0x10).

Status vocabulary: ``CODE_DERIVED_MEASURED`` = the law comes from the decompiled branch AND its
linear part equals the emulated measurement (``item_layer --verify``); ``CODE_DERIVED`` = read from
code, the measurement cannot see its constant (special arrow branch); ``UNVERIFIED`` = not needed /
not established, never guessed. Row-vector convention as everywhere in this project:
``M = T(-anchor) * chain * B``, so the mesh pivot is the returned grid point.

Nothing here is a game rule: it describes what Alpha does. Where the second hand goes during a
two-handed attack is the project's own decision (D52) and lives in the character pilot, not here.
"""
from __future__ import annotations

from dataclasses import dataclass, field

from tools.alpha_models.anim import humanoid_rig

# --------------------------------------------------------------------------- representation
STATUS_OK = "CODE_DERIVED_MEASURED"
STATUS_CODE_ONLY = "CODE_DERIVED"
STATUS_UNVERIFIED = "UNVERIFIED"


@dataclass(frozen=True)
class SizeLaw:
    """``anchor(size) = coeff[i]*size[i] + const[i]`` with an optional uniform mesh scale."""

    name: str
    coeff: tuple
    const: tuple = (0.0, 0.0, 0.0)
    scale: float = 1.0
    status: str = STATUS_OK
    evidence: str = ""

    def anchor(self, size) -> tuple:
        if len(size) != 3:
            raise ValueError(f"size must have 3 components, got {size!r}")
        return tuple(self.coeff[i] * float(size[i]) + self.const[i] for i in range(3))

    def as_dict(self) -> dict:
        return {"law": self.name, "coeff": list(self.coeff), "const": list(self.const),
                "scale": self.scale, "status": self.status, "evidence": self.evidence}


CENTER = SizeLaw("center", (0.5, 0.5, 0.5), evidence="drawModel translate(-size/2) "
                                                   "(GameController.cpp:6108 body block)")
# weapon back branch (6555-6578): translate(-sx/2,-sy/2,-sz/2) after the fixed back chain
WEAPON_ON_BACK = SizeLaw("center", (0.5, 0.5, 0.5),
                         evidence="weapon block back branch: GameController.cpp:6555-6578")
# off-hand back branch (6707-6747): same -size/2 translate
OFFHAND_ON_BACK = SizeLaw("center", (0.5, 0.5, 0.5),
                          evidence="off-hand block back branch: GameController.cpp:6707-6747")

# in-hand weapon block, GameController.cpp:6609-6662 (the branch that copies the hand frame)
WEAPON_IN_HAND_GENERIC = SizeLaw(
    "weapon_generic_in_hand", (0.5, 0.5, 0.0), (0.0, 0.0, 4.0),
    evidence="else-branch fVar2=-4.0 -> translate(-sx/2,-sy/2,-4.0) (GameController.cpp:6651-6654, "
             "6662); includes greatsword/greataxe/greatmace 15/16/17")
WEAPON_IN_HAND = {
    "generic": WEAPON_IN_HAND_GENERIC,
    "fist": SizeLaw("weapon_fist_in_hand", (0.5, 0.5, 0.5),
                    evidence="subtype 4: fVar2=-sz/2 (6647-6650) -> translate(-size/2)"),
    "bow": SizeLaw("weapon_bow_in_hand", (0.5, 1.0, 0.5), (0.0, -1.5, 0.0),
                   evidence="subtype 6: fVar23=1.5-sy, fVar2=-sz/2 (6633-6638)"),
    "crossbow": SizeLaw("weapon_crossbow_in_hand", (0.5, 0.0, 0.5),
                        evidence="subtype 7: fVar23=0, fVar2=-sz/2 (6640-6645)"),
    "boomerang": SizeLaw("weapon_boomerang_in_hand", (0.5, 0.0, 0.0), (0.0, 2.0, 4.0),
                         evidence="subtype 8: fVar2=-4.0, fVar23=-2.0 (6656-6658)"),
    "staff": SizeLaw("weapon_staff_in_hand", (0.5, 0.5, 0.0), (0.0, 0.0, 10.0),
                     evidence="subtype 10: fVar2=-10.0 (6618-6620)"),
    "bracelet": SizeLaw("weapon_bracelet_in_hand", (0.5, 1.0, 0.5), (0.0, -1.0, 0.0), 0.6,
                        evidence="subtype 12: scale 0.6, fVar23=1-sy, fVar2=-sz/2 (6622-6631)"),
    "arrow": SizeLaw("weapon_arrow_in_hand", (0.5, 0.0, 0.0), (1.0, -0.5, -1.0),
                     status=STATUS_CODE_ONLY,
                     evidence="model == arrow (vectorU32_at(0x348)): fVar21=-sx/2-1, y=0.5, z=1 "
                              "(6609-6614); constant not observable by a size sweep"),
}

# off-hand in-hand branch, GameController.cpp:6805-6850. The main weapon's subtype 8 (boomerang)
# changes the off-hand law (6833-6846) -- an intentional cross-item dependency, not a guess.
OFFHAND_IN_HAND = {
    "generic": SizeLaw("offhand_generic_in_hand", (0.5, 0.5, 0.0), (0.0, 0.0, 4.0),
                       evidence="else fVar2=-4.0 (6839-6842)"),
    "fist": SizeLaw("offhand_fist_in_hand", (0.5, 0.5, 0.5),
                    evidence="subtype 4: rotateY(180), fVar2=-sz/2 (6834-6842) -> centre"),
    "bow": SizeLaw("offhand_bow_in_hand", (0.5, 1.0, 0.5), (0.0, -1.5, 0.0),
                   evidence="subtype 6: fVar23=1.5-sy (6806-6811)"),
    "crossbow": SizeLaw("offhand_crossbow_in_hand", (0.5, 0.0, 0.5),
                        evidence="subtype 7: fVar23=0 (6813-6817)"),
    "bracelet": SizeLaw("offhand_bracelet_in_hand", (0.5, 1.0, 0.5), (0.0, -1.0, 0.0), 0.6,
                        evidence="subtype 12: scale 0.6, fVar23=1-sy (6819-6825)"),
    "shield": SizeLaw("offhand_shield_in_hand", (0.5, 0.0, 0.5), (0.0, 3.0, 0.0),
                      evidence="subtype 13: fVar23=3.0 (6827-6831)"),
    "boomerang_main": SizeLaw("offhand_boomerang_main_in_hand", (0.5, 0.0, 0.0), (0.0, 2.0, 4.0),
                              evidence="main weapon subtype 8: fVar2=-4.0, fVar23=-2.0 (6844-6846)"),
    "arrow": SizeLaw("offhand_arrow_in_hand", (0.5, 0.0, 0.0), (0.0, 0.0, 0.0),
                     status=STATUS_CODE_ONLY,
                     evidence="off-hand model == arrow (6788-6793): fVar21=1-sx/2, y=0.5, z=1; "
                              "constant not observable by a size sweep"),
}

# subtype -> family of the in-hand law (type 3, item subtype byte creature+0xaa9 / +0x991)
WEAPON_SUBTYPE_LAW = {
    0: "generic", 1: "generic", 2: "generic", 3: "generic", 4: "fist", 5: "generic",
    6: "bow", 7: "crossbow", 8: "boomerang", 9: "arrow", 10: "staff", 11: "generic",
    12: "bracelet", 13: "generic", 14: "generic", 15: "generic", 16: "generic", 17: "generic",
    18: "generic", 19: "generic", 20: "generic", 21: "generic", 22: "generic", 23: "generic",
    24: "generic", 25: "generic",
}
OFFHAND_SUBTYPE_LAW = {
    0: "generic", 1: "generic", 2: "generic", 3: "generic", 4: "fist", 5: "generic",
    6: "bow", 7: "crossbow", 8: "boomerang_main", 9: "arrow", 10: "generic", 11: "generic",
    12: "bracelet", 13: "shield", 14: "generic", 15: "generic", 16: "generic", 17: "generic",
    18: "generic", 19: "generic", 20: "generic", 21: "generic", 22: "generic", 23: "generic",
    24: "generic", 25: "generic",
}
# Subtypes both hands may hold (dual wield); the off-hand block draws them in the hand.
DUAL_SUBTYPES = (4, 12, 20)
# Two-handed subtypes: the in-hand chain 57 -> 67 -> 58 is chosen for them by
# combat_selectAttackAnim (0x445020); the in-hand law is the generic one (code + measurement).
TWO_HANDED_SUBTYPES = (15, 16, 17)

# GameController.cpp:6547-6554 (weapon) and 6698-6706 (off-hand): actions whose weapon/off-hand is
# drawn on the back. The off-hand additionally excludes 20/4/12 (held in the hand even when idle);
# the weapon additionally excludes 4/12. The weapon branch also triggers on creature+0x124 & 0x10.
ON_BACK_ACTIONS = frozenset(humanoid_rig.WEAPON_ON_BACK_ACTIONS)
WEAPON_BACK_EXCLUDED_SUBTYPES = (4, 12)
OFFHAND_BACK_EXCLUDED_SUBTYPES = (20, 4, 12)
FLAG_SPECIAL_IS_ON = 0x10     # creature+0x124 bit 0x10: force the weapon onto the back branch


def weapon_on_back(action, subtype, flag_124=0) -> bool:
    """True when drawModel draws the weapon in the back branch (GameController.cpp:6547-6554)."""
    back = (int(action) & 0xFF) in ON_BACK_ACTIONS or (int(flag_124) & FLAG_SPECIAL_IS_ON)
    return bool(back and (int(subtype) & 0xFF) not in WEAPON_BACK_EXCLUDED_SUBTYPES)


def offhand_on_back(action, subtype) -> bool:
    """True when drawModel draws the off-hand item in the back branch (6698-6706)."""
    return bool((int(action) & 0xFF) in ON_BACK_ACTIONS
                and (int(subtype) & 0xFF) not in OFFHAND_BACK_EXCLUDED_SUBTYPES)


def weapon_law(subtype) -> SizeLaw:
    return WEAPON_IN_HAND[WEAPON_SUBTYPE_LAW[int(subtype) & 0xFF]]


def offhand_law(subtype, main_subtype=None) -> SizeLaw:
    family = OFFHAND_SUBTYPE_LAW[int(subtype) & 0xFF]
    if family == "boomerang_main" and main_subtype is not None and int(main_subtype) & 0xFF != 8:
        family = "generic"       # 6833: the boomerang law only applies when the MAIN weapon is 8
    return OFFHAND_IN_HAND[family]


def weapon_present_law(action, subtype, flag_124=0) -> SizeLaw:
    return WEAPON_ON_BACK if weapon_on_back(action, subtype, flag_124) else weapon_law(subtype)


def offhand_present_law(action, subtype, main_subtype=None) -> SizeLaw:
    return (OFFHAND_ON_BACK if offhand_on_back(action, subtype)
            else offhand_law(subtype, main_subtype))


# --------------------------------------------------------------------------- verification helpers
def coefficient_problems(law: SizeLaw, measured_coeff, tol=1e-4) -> list:
    """Compare the linear part of a declared law with an emulated d(anchor)/d(size) matrix.

    ``anchor_probe`` returns a 3x3 matrix (row = size axis, column = anchor component); a law with
    pure diagonal coefficients must reproduce it. This is how the constant-free part of every law
    above is checked against Alpha's own bytes."""
    problems = []
    for i in range(3):
        for j in range(3):
            want = law.coeff[j] if i == j else 0.0
            got = measured_coeff[i][j]
            if abs(got - want) > tol:
                problems.append(f"ANCHOR_COEFF_MISMATCH {law.name} d[{j}]/d[size{i}]={got:.6f} want {want}")
    return problems


def is_weapon_like(subtype) -> bool:
    """Item classes whose grip point is expected inside the gripping hand (used by the check)."""
    return int(subtype) & 0xFF in (0, 1, 2, 3, 4, 5, 15, 16, 17, 19, 20)


def grip_point(law: SizeLaw, size):
    return law.anchor(size)
