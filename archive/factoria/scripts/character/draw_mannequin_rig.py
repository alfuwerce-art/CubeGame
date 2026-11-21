"""MANNEQUIN-RIG-01: draw the rig layout (front + side) from the block profile.

Produces `outputs/prototypes/mannequin_v3_rig/run1/rig_diagram.png`:
the rigid block parts as light rectangles and the 20 bones as coloured segments with
their pivots, in the rest (T) pose.  This documents the rig without opening Blender.

Usage:
    python scripts/character/draw_mannequin_rig.py
"""
from __future__ import annotations

import json
from pathlib import Path

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.patches import Rectangle

ROOT = Path(__file__).resolve().parents[2]
PROFILE = ROOT / "scripts" / "character" / "profiles" / "mannequin_v3_male.json"
OUT = ROOT / "outputs" / "prototypes" / "mannequin_v3_rig" / "run1"
COLORS = {
    "root": "#8c8c8c", "pelvis": "#d1495b", "spine_01": "#d1495b", "spine_02": "#d1495b",
    "neck": "#e08e45", "head": "#e08e45",
}


def bone_color(name: str) -> str:
    if name in COLORS:
        return COLORS[name]
    if "arm" in name or "clavicle" in name or name.startswith("hand"):
        return "#2f6db3"
    if "thigh" in name or "calf" in name or name.startswith("foot"):
        return "#3b8f5a"
    return "#777777"


def main():
    profile = json.loads(PROFILE.read_text(encoding="utf-8"))
    parts = profile["parts"]
    bones = profile["bones"]
    fig, axes = plt.subplots(1, 2, figsize=(13, 8.4), facecolor="#f4f3ef")
    views = [("FRONT (X / Z)", 0, 2), ("SIDE (Y / Z)", 1, 2)]
    for ax, (title, axis_h, axis_v) in zip(axes, views):
        ax.set_facecolor("#f4f3ef")
        for part, boxes in parts.items():
            for box in boxes:
                if axis_h == 0:
                    h0, h1 = box["x"] - box["sx"] / 2, box["x"] + box["sx"] / 2
                else:
                    h0, h1 = box["y"] - box["sy"] / 2, box["y"] + box["sy"] / 2
                v0, v1 = box["z"] - box["sz"] / 2, box["z"] + box["sz"] / 2
                ax.add_patch(Rectangle((h0, v0), h1 - h0, v1 - v0, facecolor="#dcd9d2",
                                       edgecolor="#9b978e", linewidth=0.8, zorder=1))
        for spec in bones:
            h0 = spec["head"][axis_h]
            h1 = spec["tail"][axis_h]
            v0 = spec["head"][axis_v]
            v1 = spec["tail"][axis_v]
            ax.plot([h0, h1], [v0, v1], color=bone_color(spec["name"]), linewidth=3.2,
                    solid_capstyle="round", zorder=3)
            ax.plot([h0], [v0], "o", color="#1d1d1d", markersize=4.2, zorder=4)
        # labels for the main chains only, so the figure stays readable
        for name in ("root", "pelvis", "spine_01", "spine_02", "neck", "head",
                     "clavicle_l", "upperarm_l", "lowerarm_l", "hand_l",
                     "thigh_l", "calf_l", "foot_l"):
            spec = next(s for s in bones if s["name"] == name)
            h = spec["head"][axis_h]
            v = spec["head"][axis_v]
            side = "right" if h >= 0 else "left"
            ax.annotate(name, (h, v), textcoords="offset points",
                        xytext=(6 if h >= 0 else -6, 2), ha="left" if h >= 0 else "right",
                        fontsize=6.6, color="#333333", zorder=5)
        ax.set_title(title, fontsize=11)
        ax.set_aspect("equal")
        ax.set_xlim(-0.95, 0.95 if axis_h == 0 else 0.55)
        ax.set_ylim(-0.03, 1.86)
        ax.grid(True, color="#e2e0da", linewidth=0.6)
        ax.set_axisbelow(True)
        for spine in ax.spines.values():
            spine.set_visible(False)
        ax.tick_params(labelsize=7, colors="#666666")
    fig.suptitle("MANNEQUIN-RIG-01  |  block parts + 20-bone humanoid rig (rest pose, meters)",
                 fontsize=12)
    fig.text(0.5, 0.015,
             "grey = rigid block parts   red = spine chain   orange = neck/head   "
             "blue = arms   green = legs   dot = bone pivot",
             ha="center", fontsize=8, color="#555555")
    OUT.mkdir(parents=True, exist_ok=True)
    fig.tight_layout(rect=(0, 0.04, 1, 0.96))
    path = OUT / "rig_diagram.png"
    fig.savefig(path, dpi=150)
    print("RIG_DIAGRAM=" + str(path))


if __name__ == "__main__":
    main()

