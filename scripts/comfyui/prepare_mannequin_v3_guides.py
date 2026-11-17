#!/usr/bin/env python3
"""Draw the two COMFY-STYLE-12 / MANNEQUIN V3 ControlNet guides from the approved reference.

The owner approved one render (two figures: HUMAN MALE - MANNEQUIN V3, HUMAN FEMALE -
MANNEQUIN V3). Those figures are the new visual standard and replace every earlier body
variant. This script does not invent proportions: every part rectangle below is a measured
pixel rectangle of that render (see measure_mannequin_v3_reference.py and
measurements/comfy_style_12/mannequin_v3/REFERENCE_MEASUREMENT.json), converted to the
768x1024 SDXL canvas by one documented similarity transform per figure.

Transform (both figures get the same canvas height, i.e. the same coordinate system):
  scale  s = 676 / figure_height_px            (676 px from crown 174 to sole 850)
  x'     = 383.5 + (x_ref - center_x_ref) * s  (768 px canvas, symmetry axis 383.5)
  y'     = 174 + (y_ref - crown_y_ref) * s

The plate is a ControlNet-only technical input, never a sampler image and never a style
reference. It follows the recipe that produced the accepted MANNEQUIN_V1 / DIAG3 frame:
one continuous silhouette plus a 6 px inset ring (tone 96) on a face tone 198, background
158. Head: one empty cranium cube with a flat crown, a narrower stepped chin block, small
side ear cubes - no eyes, mouth, hair, plate, band or any second layer above the crown.

Usage (ComfyUI embedded python carries PIL/numpy/cv2):
  python_embeded\\python.exe scripts\\comfyui\\prepare_mannequin_v3_guides.py --canny-preview
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os

import cv2
import numpy as np
from PIL import Image, ImageDraw

SIZE = (768, 1024)
BACKGROUND = (158, 158, 158)
AXIS = 383.5
CROWN_Y = 174
SOLE_Y = 850
FIGURE_H = SOLE_Y - CROWN_Y          # 676

FACE = 198
RING_TONE = 96
RING_PX = 6

# Measured reference geometry, in pixels of REFERENCE_MANNEQUIN_V3_OWNER.png.
# rect = (x0, y0, x1, y1), x1/y1 exclusive. Left viewer half is mirrored automatically.
PROFILES = {
    "HUMAN_MALE_MANNEQUIN_GUIDE": {
        "ref": "HUMAN_MALE_MANNEQUIN_V3",
        "crown_y_ref": 97,
        "figure_height_ref": 658,
        "center_x_ref": 511,
        "center_parts": [
            # ref rect              part
            ((397, 97, 622, 265), "cranium"),      # 225 x 168, flat crown
            ((401, 265, 619, 305), "chin"),        # 218 x 40, stepped chin block
            ((451, 305, 571, 337), "neck"),        # 120 wide, short
            ((429, 335, 594, 470), "chest"),       # 165 x 135 compact chest
            ((455, 470, 567, 500), "abdomen"),     # 112, narrow waist
            ((429, 500, 594, 553), "pelvis"),      # 164, narrow pelvis
        ],
        "side_parts": [
            ((358, 188, 397, 265), "ear"),          # small cube ear, 39 x 77
            ((240, 343, 316, 421), "hand"),         # 70 x 79 block hand, overlaps the elbow
            ((305, 350, 375, 414), "arm_link"),     # merged connector (see notes)
            ((313, 344, 375, 410), "elbow"),
            ((373, 335, 430, 418), "shoulder"),     # reaches the chest edge: one body
            ((430, 553, 496, 637), "thigh"),
            ((437, 637, 492, 697), "shin"),
            ((417, 697, 501, 754), "foot"),
        ],
    },
    "HUMAN_FEMALE_MANNEQUIN_GUIDE": {
        "ref": "HUMAN_FEMALE_MANNEQUIN_V3",
        "crown_y_ref": 113,
        "figure_height_ref": 642,
        "center_x_ref": 1264,
        "center_parts": [
            ((1158, 113, 1367, 273), "cranium"),    # 209 x 160, same head type
            ((1161, 273, 1363, 313), "chin"),       # 202 x 40 stepped chin
            ((1210, 313, 1316, 346), "neck"),       # 106
            ((1193, 346, 1333, 430), "chest"),      # 140, slightly narrower
            ((1207, 430, 1321, 500), "waist"),      # 114, gently tapered
            ((1190, 500, 1339, 553), "pelvis"),     # 149, compact pelvis
        ],
        "side_parts": [
            ((1120, 200, 1158, 269), "ear"),        # 38 x 70
            ((1023, 350, 1096, 423), "hand"),       # 67 x 74, overlaps the connector
            ((1090, 356, 1152, 418), "arm_link"),
            ((1108, 346, 1174, 409), "elbow"),
            ((1171, 346, 1196, 430), "shoulder"),   # narrow: merges into the wide chest
            ((1190, 553, 1250, 636), "thigh"),
            ((1197, 637, 1247, 696), "shin"),
            ((1177, 697, 1255, 754), "foot"),
        ],
    },
}

# Painting order: broad volumes first so details sit on top of their neighbour.
PAINT_ORDER = [
    "shoulder", "elbow", "arm_link", "hand",
    "thigh", "shin", "foot",
    "ear", "neck", "chin", "cranium",
    "chest", "abdomen", "waist", "pelvis",
]

# Depth plate values (0 = background / far, 255 = nearest camera). A single front render
# carries no depth, so these are DESIGN parameters of the V3 block stack, not measurements:
# the head is the nearest volume, the chest sits behind it, the arm modules recede behind the
# shoulders and the feet step slightly forward. They are used only by the depth branch of
# controlnet-union-sdxl-1.0, never as sampler input.
DEPTH_VALUES = {
    "cranium": 255, "chin": 250, "neck": 170,
    "chest": 225, "abdomen": 230, "waist": 230, "pelvis": 228,
    "shoulder": 205, "arm_link": 185, "elbow": 190, "hand": 195, "ear": 210,
    "thigh": 215, "shin": 205, "foot": 235,
}


def sha256(path: str) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def transform(profile: dict, rect: tuple[int, int, int, int]) -> tuple[int, int, int, int]:
    s = FIGURE_H / profile["figure_height_ref"]
    x0, y0, x1, y1 = rect
    tx0 = int(round(AXIS + (x0 - profile["center_x_ref"]) * s))
    tx1 = int(round(AXIS + (x1 - profile["center_x_ref"]) * s))
    ty0 = int(round(CROWN_Y + (y0 - profile["crown_y_ref"]) * s))
    ty1 = int(round(CROWN_Y + (y1 - profile["crown_y_ref"]) * s))
    return tx0, ty0, tx1, ty1


def mirrored(x0: int, x1: int) -> tuple[int, int]:
    return (int(round(2 * AXIS - x1)), int(round(2 * AXIS - x0)))


def parts_of(profile: dict) -> list[dict]:
    out = []
    for rect, name in profile["center_parts"]:
        x0, y0, x1, y1 = transform(profile, rect)
        out.append({"name": name, "side": "C", "x0": x0, "y0": y0, "x1": x1, "y1": y1,
                    "ref_rect": list(rect)})
    for rect, name in profile["side_parts"]:
        x0, y0, x1, y1 = transform(profile, rect)
        out.append({"name": name, "side": "L", "x0": x0, "y0": y0, "x1": x1, "y1": y1,
                    "ref_rect": list(rect)})
        mx0, mx1 = mirrored(x0, x1)
        out.append({"name": name, "side": "R", "x0": mx0, "y0": y0, "x1": mx1, "y1": y1,
                    "ref_rect": list(rect)})
    return out


def render(name: str, profile: dict) -> tuple[Image.Image, list[dict], dict]:
    parts = parts_of(profile)
    mask = Image.new("L", SIZE, 0)
    md = ImageDraw.Draw(mask)
    for n in PAINT_ORDER:
        for p in parts:
            if p["name"] == n:
                md.rectangle((p["x0"], p["y0"], p["x1"] - 1, p["y1"] - 1), fill=255)

    # Exact symmetry: the union of the drawn shape and its mirror is symmetric by
    # construction, so a 1 px rounding of a measured rectangle cannot open a seam.
    arr = np.array(mask, dtype=np.uint8)
    arr = np.maximum(arr, arr[:, ::-1])
    # Seal 1-2 px seams left by rectangle rounding, so the plate stays ONE body
    # (the lesson of MANNEQUIN_V1 / DIAG3: a fragmented plate teaches ControlNet to draw
    # separate slabs instead of one mannequin).
    arr = cv2.morphologyEx(arr, cv2.MORPH_CLOSE, np.ones((5, 5), np.uint8))

    kernel = np.ones((2 * RING_PX + 1, 2 * RING_PX + 1), np.uint8)
    inner = cv2.erode(arr, kernel, iterations=1)
    ring = (arr > 0) & (inner == 0)

    canvas = np.full((SIZE[1], SIZE[0], 3), FACE, dtype=np.uint8)
    canvas[ring] = RING_TONE
    plate = np.full((SIZE[1], SIZE[0], 3), BACKGROUND, dtype=np.uint8)
    plate[arr > 0] = canvas[arr > 0]
    im = Image.fromarray(plate, mode="RGB")

    ys, xs = np.nonzero(arr)
    stats = {
        "mask_area_px": int((arr > 0).sum()),
        "mask_bbox_xyxy": [int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max())],
        "mask_components": int(cv2.connectedComponents((arr > 0).astype(np.uint8),
                                                       connectivity=8)[0] - 1),
        "symmetry_error_px": int(np.abs(arr.astype(np.int16) - arr[:, ::-1].astype(np.int16)).sum() // 255),
        "figure_height_px": int(ys.max() - ys.min() + 1),
        "head_frac_of_figure": None,
    }
    parts_by_name = {p["name"]: p for p in parts if p["side"] in ("C", "L")}
    if "cranium" in parts_by_name and "chin" in parts_by_name:
        head_h = parts_by_name["chin"]["y1"] - parts_by_name["cranium"]["y0"]
        stats["head_frac_of_figure"] = round(head_h / stats["figure_height_px"], 4)
    return im, parts, stats


def canny_preview(src: str, dst: str, low: float = 0.35, high: float = 0.75) -> dict:
    img = cv2.imread(src, cv2.IMREAD_COLOR)
    gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
    edges = cv2.Canny(gray, int(low * 255), int(high * 255))
    cv2.imwrite(dst, edges)
    ys, xs = np.nonzero(edges)
    return {"file": dst, "sha256": sha256(dst), "low": low, "high": high,
            "edge_pixels": int(edges.sum() // 255),
            "bbox_xyxy": [int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max())] if len(xs) else None}


def render_depth(profile: dict) -> tuple[Image.Image, dict]:
    """Grayscale depth plate of the same block stack (ControlNet union `depth` mode input)."""
    parts = parts_of(profile)
    img = np.zeros((SIZE[1], SIZE[0]), dtype=np.uint8)
    for n in PAINT_ORDER:
        v = DEPTH_VALUES[n]
        for p in parts:
            if p["name"] == n:
                img[p["y0"]:p["y1"], p["x0"]:p["x1"]] = v
    img = np.maximum(img, img[:, ::-1])
    im = Image.fromarray(img, mode="L")
    return im, {"values": DEPTH_VALUES,
                "note": "design depth parameters, not measured from the single front render"}


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default="outputs/factory/mannequin_v3")
    ap.add_argument("--meta", default="measurements/comfy_style_12/mannequin_v3")
    ap.add_argument("--reference", default="outputs/factory/mannequin_v3/REFERENCE_MANNEQUIN_V3_OWNER.png")
    ap.add_argument("--canny-preview", action="store_true")
    args = ap.parse_args()
    os.makedirs(args.out, exist_ok=True)
    os.makedirs(args.meta, exist_ok=True)

    card = {
        "task": "COMFY-STYLE-12",
        "role": "ControlNet-only technical guides for MANNEQUIN V3 (never sampler inputs)",
        "reference_image": args.reference,
        "reference_sha256": sha256(args.reference) if os.path.isfile(args.reference) else None,
        "canvas": list(SIZE),
        "symmetry_axis_x": AXIS,
        "crown_y": CROWN_Y,
        "sole_y": SOLE_Y,
        "figure_height_px": FIGURE_H,
        "tones": {"face": FACE, "ring": RING_TONE, "ring_px": RING_PX,
                  "background": list(BACKGROUND)},
        "head_rule": ("one empty cranium cube with a flat crown, narrower stepped chin block, "
                      "small side ear cubes; no eyes, mouth, hair, plate, band or second layer"),
        "variant": "silhouette + 6 px inset ring (DIAG3 recipe of COMFY-STYLE-12 MANNEQUIN_V1)",
        "arm_note": ("per arm the render shows a hand cube, a thin connector and an elbow cube "
                     "whose connectors merge with the silhouette; the plate keeps them merged "
                     "(one connected body) and reproduces the block widths"),
        "generator": "scripts/comfyui/prepare_mannequin_v3_guides.py",
        "guides": {},
    }

    for name, profile in PROFILES.items():
        im, parts, stats = render(name, profile)
        png = os.path.join(args.out, name + ".png")
        im.save(png, optimize=True)
        entry = {
            "figure": profile["ref"],
            "reference_transform": {
                "scale": round(FIGURE_H / profile["figure_height_ref"], 6),
                "crown_y_ref": profile["crown_y_ref"],
                "figure_height_ref": profile["figure_height_ref"],
                "center_x_ref": profile["center_x_ref"],
            },
            "parts": parts,
            "stats": stats,
            "file": png,
            "sha256": sha256(png),
        }
        if args.canny_preview:
            entry["canny"] = canny_preview(png, os.path.join(args.out, name + "_CANNY.png"))
        dim, dinfo = render_depth(profile)
        dpng = os.path.join(args.out, name.replace("_GUIDE", "_DEPTH") + ".png")
        dim.save(dpng, optimize=True)
        entry["depth"] = {"file": dpng, "sha256": sha256(dpng), **dinfo}
        card["guides"][name] = entry
        print(json.dumps({"guide": name, "sha256": entry["sha256"], "stats": stats,
                          "canny_edges": (entry.get("canny") or {}).get("edge_pixels")},
                         ensure_ascii=False))

    card["canny_parameters"] = {"low_threshold": 0.35, "high_threshold": 0.75}
    with open(os.path.join(args.meta, "GUIDES.json"), "w", encoding="utf-8", newline="\n") as f:
        json.dump(card, f, ensure_ascii=False, indent=2)
        f.write("\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
