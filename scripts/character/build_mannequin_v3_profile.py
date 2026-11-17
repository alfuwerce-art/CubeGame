"""MANNEQUIN-RIG-01: build the male V3 block-mannequin profile (pixels -> meters).

Source of every number: the owner-approved reference
`outputs/factory/mannequin_v3/REFERENCE_MANNEQUIN_V3_OWNER.png` (local, not published)
and its pixel measurement `measurements/comfy_style_12/mannequin_v3/REFERENCE_MEASUREMENT.json`
(HUMAN_MALE_MANNEQUIN_V3).  Block boundaries were refined by a seam scan of the same
render (dark grooves between blocks), see BLOCK_SEAMS below.

No new proportions are invented: front-view widths/heights are taken from the render and
converted with one scale factor.  Only DEPTH is a design parameter, because a single front
render does not contain depth; each depth value is listed explicitly with its reason.

Usage:
    python scripts/character/build_mannequin_v3_profile.py
"""
from __future__ import annotations

import json
import hashlib
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
REF_IMAGE = ROOT / "outputs" / "factory" / "mannequin_v3" / "REFERENCE_MANNEQUIN_V3_OWNER.png"
MEASUREMENT = ROOT / "measurements" / "comfy_style_12" / "mannequin_v3" / "REFERENCE_MEASUREMENT.json"
OUT = ROOT / "scripts" / "character" / "profiles" / "mannequin_v3_male.json"

# Reference frame of the male figure (pixels, y grows downward).
CROWN_Y = 97.0
SOLE_Y = 754.0
CENTER_X = 510.5
FIGURE_H_PX = SOLE_Y - CROWN_Y          # 657 px -> 1.80 m
HEIGHT_M = 1.80
SCALE = HEIGHT_M / FIGURE_H_PX          # m per px
AREA_HALF_X_PX = 271.0                  # bbox 240..782 -> arm span 543 px

# Block bands read from the reference render (x0, x1, y0, y1 in px).
# y-bands come from vertical seam scans (x=460/511/540), x-bands from horizontal seam
# scans (y=355..415): seams at x = 311, 371, 426, 595, 649, 710 and y = 326, 346, 471,
# 479, 549, 634, 646, 704, 718.
BLOCKS = {
    # name            x0    x1    y0    y1   depth_m  y_off_m  comment
    "head_skull":   (398,  623,   97,  307,  0.600,  0.000),  # depth ~ front width -> cubic head
    "head_chin":    (434,  587,  307,  326,  0.520,  0.000),  # chin step under the skull cube
    "ear_l":        (358,  398,  188,  261,  0.200,  0.020),  # small separate ear blocks, set slightly back
    "neck":         (451,  570,  326,  346,  0.260,  0.000),
    "chest":        (426,  595,  346,  471,  0.360,  0.000),  # compact torso, broad at the shoulder line
    "waist":        (456,  565,  471,  479,  0.280,  0.000),  # recessed waist connector
    "pelvis":       (430,  593,  479,  549,  0.340,  0.000),
    "crotch":       (470,  550,  549,  565,  0.240,  0.000),
    "upper_arm":    (371,  426,  343,  421,  0.200,  0.000),
    "elbow":        (363,  371,  355,  405,  0.150,  0.000),
    "forearm":      (320,  363,  352,  408,  0.160,  0.000),
    "wrist":        (311,  320,  360,  400,  0.150,  0.000),
    "hand":         (240,  311,  344,  420,  0.200,  0.000),  # block hand, widest arm segment
    "thigh":        (430,  494,  484,  634,  0.180,  0.000),
    "knee":         (441,  483,  634,  646,  0.140,  0.000),
    "shin":         (437,  491,  646,  704,  0.160,  0.000),
    "ankle":        (443,  485,  704,  718,  0.150,  0.000),
    "foot":         (419,  501,  718,  754,  0.300, -0.060),  # foot extends forward (-Y)
}

# Part = list of (block, mirrored); connectors belong to the PROXIMAL block so a joint
# opens cleanly when the distal segment rotates away.
# The reference render is the character's FRONT view, so pixels on the image left
# belong to the character's RIGHT side: the base blocks build the _r limbs and the
# mirrored copies build the _l limbs.  +X is the character's LEFT.
PARTS = {
    "torso_pelvis": [("waist", 0), ("pelvis", 0), ("crotch", 0)],
    "torso_upper": [("chest", 0)],
    "neck": [("neck", 0)],
    "head": [("head_skull", 0), ("head_chin", 0)],
    "ear_r": [("ear_l", 0)],
    "ear_l": [("ear_l", 1)],
    "upper_arm_r": [("upper_arm", 0), ("elbow", 0)],
    "forearm_r": [("forearm", 0), ("wrist", 0)],
    "hand_r": [("hand", 0)],
    "upper_arm_l": [("upper_arm", 1), ("elbow", 1)],
    "forearm_l": [("forearm", 1), ("wrist", 1)],
    "hand_l": [("hand", 1)],
    "thigh_r": [("thigh", 0), ("knee", 0)],
    "shin_r": [("shin", 0)],
    "foot_r": [("foot", 0), ("ankle", 0)],
    "thigh_l": [("thigh", 1), ("knee", 1)],
    "shin_l": [("shin", 1)],
    "foot_l": [("foot", 1), ("ankle", 1)],
}


def box_px(name, mirror):
    x0, x1, y0, y1, depth, y_off = BLOCKS[name]
    if mirror:
        x0, x1 = 2 * CENTER_X - x1, 2 * CENTER_X - x0
    cx = (x0 + x1) / 2.0
    cy = (y0 + y1) / 2.0
    return {
        "source_block": name,
        "x": round((cx - CENTER_X) * SCALE, 4),
        "y": round(y_off, 4),
        "z": round((SOLE_Y - cy) * SCALE, 4),
        "sx": round((x1 - x0) * SCALE, 4),
        "sy": round(depth, 4),
        "sz": round((y1 - y0) * SCALE, 4),
        "px": {"x": [x0, x1], "y": [y0, y1]},
    }


def build():
    parts = {}
    for part, blocks in PARTS.items():
        parts[part] = [box_px(block, bool(mirror)) for block, mirror in blocks]

    def bz(block, key):
        return box_px(block, False)[key]

    def z_of(ypx):
        return round((SOLE_Y - ypx) * SCALE, 4)

    def x_of(xpx):
        # Image right = world +X = the character's LEFT (front view, character faces -Y).
        return round((xpx - CENTER_X) * SCALE, 4)

    # Vertical landmarks (meters, sole = 0).
    lm = {
        "crown_z": z_of(97),
        "jaw_step_z": z_of(307),
        "head_bottom_z": z_of(326),
        "chest_top_z": z_of(346),
        "chest_bottom_z": z_of(471),
        "waist_z": z_of(479),
        "crotch_z": z_of(549),
        "arm_center_z": z_of(382),
        "shoulder_joint_x": abs(x_of(426)),
        "elbow_x": abs(x_of(367)),
        "wrist_x": abs(x_of(315.5)),
        "hand_end_x": abs(x_of(240)),
        "hip_x": abs(x_of(461)),
        "hip_z": z_of(484),
        "knee_z": z_of(634),
        "ankle_z": z_of(704),
    }
    lm["foot_toe_y"] = round(-0.060 - 0.300 / 2.0, 4)
    lm["foot_heel_y"] = round(-0.060 + 0.300 / 2.0, 4)

    # Bones: pivots sit on block seams, so every rigid segment rotates around its own
    # edge and cannot cut into the neighbour.
    ax = lm["arm_center_z"]
    chest_top_z = lm["chest_top_z"]
    leg_x = abs(x_of(461))
    bones = [
        ("root", (0.0, 0.0, 0.0), (0.0, 0.0, 0.1400), None, False, False),
        ("pelvis", (0.0, 0.0, lm["crotch_z"]), (0.0, 0.0, lm["waist_z"]), "root", False, True),
        ("spine_01", (0.0, 0.0, lm["waist_z"]), (0.0, 0.0, lm["chest_bottom_z"]), "pelvis", True, True),
        ("spine_02", (0.0, 0.0, lm["chest_bottom_z"]), (0.0, 0.0, lm["chest_top_z"]), "spine_01", True, True),
        ("neck", (0.0, 0.0, chest_top_z), (0.0, 0.0, lm["head_bottom_z"]), "spine_02", True, True),
        ("head", (0.0, 0.0, lm["head_bottom_z"]), (0.0, 0.0, lm["crown_z"]), "neck", True, True),
    ]

    for side, sign in (("l", 1.0), ("r", -1.0)):
        bones += [
            (f"clavicle_{side}", (round(0.03 * sign, 4), 0.0, round(chest_top_z - 0.02, 4)),
             (round(lm["shoulder_joint_x"] * sign, 4), 0.0, ax), "spine_02", False, True),
            (f"upperarm_{side}", (round(lm["shoulder_joint_x"] * sign, 4), 0.0, ax),
             (round(lm["elbow_x"] * sign, 4), 0.0, ax), f"clavicle_{side}", True, True),
            (f"lowerarm_{side}", (round(lm["elbow_x"] * sign, 4), 0.0, ax),
             (round(lm["wrist_x"] * sign, 4), 0.0, ax), f"upperarm_{side}", True, True),
            (f"hand_{side}", (round(lm["wrist_x"] * sign, 4), 0.0, ax),
             (round(lm["hand_end_x"] * sign, 4), 0.0, ax), f"lowerarm_{side}", True, True),
            (f"thigh_{side}", (round(leg_x * sign, 4), 0.0, lm["hip_z"]),
             (round(leg_x * sign, 4), 0.0, lm["knee_z"]), "pelvis", False, True),
            (f"calf_{side}", (round(leg_x * sign, 4), 0.0, lm["knee_z"]),
             (round(leg_x * sign, 4), 0.0, lm["ankle_z"]), f"thigh_{side}", True, True),
            (f"foot_{side}", (round(leg_x * sign, 4), 0.0, lm["ankle_z"]),
             (round(leg_x * sign, 4), -0.2200, 0.0300), f"calf_{side}", True, True),
        ]

    bone_doc = []
    for name, head, tail, parent, connected, deform in bones:
        bone_doc.append({"name": name, "head": list(head), "tail": list(tail),
                         "parent": parent, "connected": bool(connected), "deform": bool(deform)})

    doc = {
        "profile": "HUMAN_MALE_MANNEQUIN_V3_BLOCK_RIG_PROTOTYPE",
        "status": "PROTOTYPE_FOR_RIG_TEST_NOT_PRODUCTION",
        "units": "meters",
        "up_axis": "Z",
        "forward_axis": "-Y",
        "height_m": HEIGHT_M,
        "proportions": {
            "head_cube_over_height": round(0.5753 / HEIGHT_M, 4),
            "head_with_chin_step_over_height": round(229 * SCALE / HEIGHT_M, 4),
            "head_width_over_height": round(225 * SCALE / HEIGHT_M, 4),
            "arm_span_over_height": round(2 * lm["hand_end_x"] / HEIGHT_M, 4),
            "crotch_leg_over_height": round(lm["crotch_z"] / HEIGHT_M, 4),
            "chest_width_over_height": round(bz("chest", "sx") / HEIGHT_M, 4),
            "pelvis_width_over_height": round(bz("pelvis", "sx") / HEIGHT_M, 4),
            "thigh_width_over_height": round(bz("thigh", "sx") / HEIGHT_M, 4),
            "reference_measured": {
                "head_block_over_height": 0.3161,
                "head_width_over_height": 0.3419,
                "arm_span_over_height": 0.8252,
                "leg_length_over_height": 0.310,
                "chest_width_over_height": 0.2570,
                "pelvis_width_over_height": 0.2523,
                "thigh_width_over_height": 0.0988,
            },
        },
        "conversion": {
            "reference_image": str(REF_IMAGE.relative_to(ROOT)).replace("\\", "/"),
            "reference_sha256": hashlib.sha256(REF_IMAGE.read_bytes()).hexdigest() if REF_IMAGE.is_file() else None,
            "measurement_card": str(MEASUREMENT.relative_to(ROOT)).replace("\\", "/"),
            "figure_px": {"crown_y": CROWN_Y, "sole_y": SOLE_Y, "center_x": CENTER_X, "height_px": FIGURE_H_PX},
            "meters_per_pixel": round(SCALE, 8),
            "depth_policy": "front render gives no depth; depth is an explicit design value per block",
        },
        "landmarks_m": lm,
        "parts": parts,
        "bones": bone_doc,
    }
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(json.dumps(doc, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    total_boxes = sum(len(v) for v in parts.values())
    print(f"PROFILE={OUT}")
    print(f"parts={len(parts)} blocks={total_boxes} bones={len(bone_doc)}")
    pr = doc["proportions"]
    print(f"height={HEIGHT_M} arm_span={pr['arm_span_over_height']} "
          f"head_cube={pr['head_cube_over_height']} head_total={pr['head_with_chin_step_over_height']} "
          f"leg={pr['crotch_leg_over_height']}")
    return doc


if __name__ == "__main__":
    build()

