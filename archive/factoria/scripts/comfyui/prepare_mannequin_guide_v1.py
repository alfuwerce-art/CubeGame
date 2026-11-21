#!/usr/bin/env python3
"""Draw the deterministic ControlNet-only technical guide for COMFY-STYLE-12 / MANNEQUIN_V1.

The guide is NOT a style reference and is never an image input of the sampler. It is a flat,
strictly symmetric FRONT T-pose plate whose only job is to give the Canny branch of
`controlnet-union-sdxl-1.0` (a) one continuous outer silhouette and (b) the internal
boundaries of the large body volumes:

  * one filled silhouette - no background notches, no separate floating tiles;
  * each body part carries an inset top / side / bottom plane band, so the edge map
    contains cuboid plane steps instead of a single flat outline;
  * joint grooves separate shoulder / upper arm / forearm / hand and thigh / shin / foot;
  * the head is one empty block with a stepped jaw: no eyes, mouth, hair, beard, ears;
  * mirrored pixel-exactly around x = 383.5 (768 px canvas), full hands and feet,
    horizontal arms, straight legs, neutral 158-grey background.

Tone contrast inside the plate is large (>=60 levels) because the union ControlNet receives
`Canny(0.35, 0.75)`: a subtle 8-level shading change would not survive Canny.

Usage (ComfyUI embedded python carries PIL/cv2):
  python_embeded\\python.exe scripts\\comfyui\\prepare_mannequin_guide_v1.py \
      --out outputs/factory/mannequin_v1 --canny-preview
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os

from PIL import Image, ImageDraw

SIZE = (768, 1024)
BACKGROUND = (158, 158, 158)
AXIS = 383.5  # pixel-grid symmetry axis (768 px, even width)
SOLE_Y = 850
CROWN_Y = 174

FACE = 198
SIDE = 138
TOP = 242
BOTTOM = 116
RING_TONE = 96
RING_PX = 6

# name, x0, y0, x1, y1  (x1/y1 exclusive). Left viewer half; mirrored automatically
# unless the block is centred on the symmetry axis.
CENTER_PARTS = [
    ("cranium", 276, CROWN_Y, 492, 290),
    ("jaw", 294, 290, 474, 318),
    ("neck", 345, 318, 422, 364),
    ("chest", 278, 364, 489, 498),
    ("abdomen", 300, 498, 467, 556),
    ("pelvis", 287, 556, 480, 638),
]
SIDE_PARTS = [
    ("shoulder", 236, 362, 282, 456),
    ("upper_arm", 194, 372, 236, 450),
    ("forearm", 152, 378, 194, 444),
    ("hand", 104, 370, 152, 452),
    ("thigh", 296, 638, 372, 742),
    ("knee", 302, 742, 366, 766),
    ("shin", 306, 766, 362, 818),
    ("ankle", 312, 818, 356, 826),
    ("foot", 292, 826, 376, SOLE_Y),
]
# Parts are painted in this order: broad volumes first, details on top.
PAINT_ORDER = [
    "cranium", "jaw", "neck", "chest", "abdomen", "pelvis",
    "shoulder", "upper_arm", "forearm", "hand",
    "thigh", "knee", "shin", "ankle", "foot",
]


def clamp(v: int) -> int:
    return max(0, min(255, v))


def tone(value: int) -> tuple[int, int, int]:
    v = clamp(value)
    return (v, v, v)


def mirrored(x0: int, x1: int) -> tuple[int, int]:
    return (int(round(2 * AXIS - x1)), int(round(2 * AXIS - x0)))


def rects() -> list[dict]:
    """All parts in absolute canvas coordinates, left and right halves."""
    out = []
    for name, x0, y0, x1, y1 in CENTER_PARTS:
        out.append({"name": name, "side": "C", "x0": x0, "y0": y0, "x1": x1, "y1": y1})
    for name, x0, y0, x1, y1 in SIDE_PARTS:
        out.append({"name": name, "side": "L", "x0": x0, "y0": y0, "x1": x1, "y1": y1})
        mx0, mx1 = mirrored(x0, x1)
        out.append({"name": name, "side": "R", "x0": mx0, "y0": y0, "x1": mx1, "y1": y1})
    return out


def paint_part(draw: ImageDraw.ImageDraw, p: dict, band: int) -> None:
    """Front face plus inset plane bands of one cuboid (all inside the silhouette)."""
    x0, y0, x1, y1 = p["x0"], p["y0"], p["x1"] - 1, p["y1"] - 1
    b = max(3, band)
    draw.rectangle((x0, y0, x1, y1), fill=tone(FACE))
    t = max(2, b // 2)                                   # top plane
    draw.rectangle((x0 + 1, y0 + 1, x1 - 1, y0 + t), fill=tone(TOP))
    draw.rectangle((x0 + 1, y1 - b, x1 - 1, y1 - 1), fill=tone(BOTTOM))  # bottom plane
    draw.rectangle((x0 + 1, y0 + 1, x0 + b - 1, y1 - 1), fill=tone(SIDE))  # left plane
    draw.rectangle((x1 - b + 1, y0 + 1, x1 - 1, y1 - 1), fill=tone(SIDE))  # right plane


def render(variant: str = "solid", silhouette: str | None = None) -> tuple[Image.Image, list[dict]]:
    parts = rects()
    bands = {"neck": 4, "knee": 4, "ankle": 3, "hand": 5, "foot": 5}

    # 1. Silhouette mask: the union of the block parts, or an existing approved binary
    #    FRONT plate when --variant mask_ring is used.
    mask = Image.new("L", SIZE, 0)
    md = ImageDraw.Draw(mask)
    if variant == "mask_ring":
        if not silhouette or not os.path.isfile(silhouette):
            raise SystemExit(f"--silhouette is required for mask_ring: {silhouette}")
        src = Image.open(silhouette).convert("L")
        if src.size != SIZE:
            raise SystemExit(f"silhouette must be {SIZE}, got {src.size}")
        mask = src.point(lambda v: 255 if v > 127 else 0)
        parts = []
    else:
        for p in parts:
            md.rectangle((p["x0"], p["y0"], p["x1"] - 1, p["y1"] - 1), fill=255)

    # 2. Plate tone. Three variants, all keeping ONE body:
    #    solid - only the stepped outer contour (pose guidance proved too weak alone:
    #            the DIAG1 frame lost the T-pose and drifted to a smooth mannequin);
    #    ring  - the same contour plus one inset bevel ring around the whole figure, so the
    #            edge map carries a double contour (volume cue) without per-part rectangles;
    #    planes- per-part inset bands (kept for the record: the PRIMARY frame fragmented
    #            into floating slabs because ControlNet followed every inner rectangle).
    canvas = Image.new("RGB", SIZE, tone(FACE))
    cd = ImageDraw.Draw(canvas)
    if variant == "planes":
        by_name = {}
        for p in parts:
            by_name.setdefault(p["name"], []).append(p)
        for name in PAINT_ORDER:
            for p in by_name[name]:
                paint_part(cd, p, bands.get(name, 6))
    elif variant in ("ring", "mask_ring"):
        import cv2
        import numpy as np

        arr = np.array(mask, dtype=np.uint8)
        kernel = np.ones((2 * RING_PX + 1, 2 * RING_PX + 1), np.uint8)
        inner = cv2.erode(arr, kernel, iterations=1)
        ring = (arr > 0) & (inner == 0)
        pixels = np.array(canvas, dtype=np.uint8)
        pixels[ring] = (RING_TONE, RING_TONE, RING_TONE)
        canvas = Image.fromarray(pixels, mode="RGB")

    im = Image.new("RGB", SIZE, BACKGROUND)
    im.paste(canvas, (0, 0), mask)
    return im, parts


def canny_preview(src: str, dst: str, low: float = 0.35, high: float = 0.75) -> dict:
    """Same operator ComfyUI's stock Canny node applies to the control image."""
    import cv2
    import numpy as np

    img = cv2.imread(src, cv2.IMREAD_COLOR)
    gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
    edges = cv2.Canny(gray, int(low * 255), int(high * 255))
    cv2.imwrite(dst, edges)
    ys, xs = np.nonzero(edges)
    return {
        "file": dst,
        "sha256": sha256(dst),
        "low_threshold": low,
        "high_threshold": high,
        "edge_pixels": int(edges.sum() // 255),
        "bbox_xyxy": [int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max())] if len(xs) else None,
    }


def sha256(path: str) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default="outputs/factory/mannequin_v1")
    ap.add_argument("--name", default="MANNEQUIN_GUIDE_V1")
    ap.add_argument("--variant", choices=("solid", "ring", "mask_ring", "planes"), default="solid")
    ap.add_argument("--silhouette", help="binary FRONT plate used by --variant mask_ring")
    ap.add_argument("--canny-preview", action="store_true")
    args = ap.parse_args()
    os.makedirs(args.out, exist_ok=True)

    im, parts = render(args.variant, args.silhouette)
    png = os.path.join(args.out, args.name + ".png")
    im.save(png, optimize=True)

    meta = {
        "task": "COMFY-STYLE-12",
        "role": "ControlNet-only technical guide (never a sampler image input)",
        "name": args.name,
        "variant": args.variant,
        "silhouette_source": args.silhouette,
        "canvas": list(SIZE),
        "background": list(BACKGROUND),
        "symmetry_axis_x": AXIS,
        "crown_y": CROWN_Y,
        "sole_y": SOLE_Y,
        "tones": {"face": FACE, "side_plane": SIDE, "top_plane": TOP, "bottom_plane": BOTTOM,
                  "ring": RING_TONE, "ring_px": RING_PX},
        "head": "cranium + jaw blocks only, no facial or hair detail",
        "parts": parts,
        "file": png,
        "sha256": sha256(png),
        "generator": "scripts/comfyui/prepare_mannequin_guide_v1.py",
    }
    if args.canny_preview:
        meta["canny"] = canny_preview(png, os.path.join(args.out, args.name + "_CANNY.png"))
        meta["canny"]["role"] = "diagnostic preview of the ControlNet input, not a pipeline input"

    with open(os.path.join(args.out, args.name + ".json"), "w", encoding="utf-8", newline="\n") as f:
        json.dump(meta, f, ensure_ascii=False, indent=2)
        f.write("\n")
    print(json.dumps({k: meta[k] for k in ("file", "sha256")}, ensure_ascii=False))
    if "canny" in meta:
        print(json.dumps(meta["canny"], ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
