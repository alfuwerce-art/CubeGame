#!/usr/bin/env python3
"""COMFY-STYLE-07 — prepare CubeGame character references from owner screenshots.

Input: Unity character-selection screenshot (UI, 3D scenery background).
Output (per character):
  <name>_cutout.png            RGBA cutout, native pixel scale (proportions untouched)
  <name>_on_neutral.png        same cutout composited on neutral grey
  <name>_reference_768x1024.png  nearest-neighbour integer upscale, centred on neutral grey
  <name>.json                  source sha256, crop box, alpha bbox, scale factor, checks

Segmentation is OpenCV GrabCut (deterministic, no AI weights): border = definite
background, inner rect = probable foreground, then largest component + hole fill.
The prompt style is NOT altered: no blur, no resampling except an integer
nearest-neighbour upscale for the 768x1024 reference.

Runs on the ComfyUI embedded python (has cv2/numpy/PIL), e.g.:
  D:\\programs\\ComfyUI\\python_embeded\\python.exe -s prepare_character_reference.py \
      --source "D:\\Projects\\референсы для CudeGame\\человек без шейдеров.png" \
      --name human_warrior --box 660,300,1060,760 \
      --out "references/characters_v1" --measurements measurements/character_references.json
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os

import cv2
import numpy as np
from PIL import Image

NEUTRAL = (158, 158, 158)


def sha256_file(path: str) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def segment(pil_crop: Image.Image, rect: tuple[int, int, int, int], k: int = 18) -> np.ndarray:
    """Returns uint8 mask (255 = character) for the crop.

    Two-stage segmentation, deterministic and without AI weights:
      1. k-means colour clusters over the crop; a cluster whose pixels are mostly
         in the outer ring (background) is treated as definite background, a cluster
         that never appears in the ring as definite foreground.
      2. GrabCut refines the ambiguous pixels, seeded by that trimap and by the
         owner-supplied character rectangle (probable foreground inside the rect,
         probable background outside it).

    The stage-1 colour model is what keeps dark boots/legs: they are not a
    background colour, so GrabCut cannot silently classify them as scenery.
    """
    rgb = np.array(pil_crop.convert("RGB"))
    bgr = cv2.cvtColor(rgb, cv2.COLOR_RGB2BGR)
    h, w = bgr.shape[:2]
    x0, y0, x1, y1 = rect

    # ---- stage 1: colour clusters vs. outer ring -----------------------------
    ring = 16
    ring_mask = np.zeros((h, w), np.uint8)
    ring_mask[:ring, :] = 1
    ring_mask[-ring:, :] = 1
    ring_mask[:, :ring] = 1
    ring_mask[:, -ring:] = 1
    data = rgb.reshape(-1, 3).astype(np.float32)
    criteria = (cv2.TERM_CRITERIA_EPS + cv2.TERM_CRITERIA_MAX_ITER, 30, 1.0)
    _, labels, _ = cv2.kmeans(data, k, None, criteria, 3, cv2.KMEANS_PP_CENTERS)
    labels = labels.reshape(h, w)
    ring_flat = ring_mask.reshape(-1) == 1
    lab_flat = labels.reshape(-1)
    bg_clusters, fg_clusters = set(), set()
    for c in range(k):
        sel = lab_flat == c
        n = int(sel.sum())
        if n < 80:
            continue
        frac_ring = float((sel & ring_flat).sum()) / n
        if frac_ring >= 0.55:
            bg_clusters.add(c)
        elif frac_ring == 0.0:
            fg_clusters.add(c)

    mask = np.full((h, w), cv2.GC_PR_BGD, np.uint8)
    mask[np.isin(labels, list(bg_clusters))] = cv2.GC_BGD
    mask[np.isin(labels, list(fg_clusters))] = cv2.GC_FGD
    mask[max(0, y0):min(h, y1), max(0, x0):min(w, x1)][
        mask[max(0, y0):min(h, y1), max(0, x0):min(w, x1)] == cv2.GC_PR_BGD] = cv2.GC_PR_FGD
    border = max(12, ring // 2)
    mask[:border, :] = cv2.GC_BGD
    mask[-border:, :] = cv2.GC_BGD
    mask[:, :border] = cv2.GC_BGD
    mask[:, -border:] = cv2.GC_BGD

    # ---- stage 2: GrabCut refinement -----------------------------------------
    bgd, fgd = np.zeros((1, 65), np.float64), np.zeros((1, 65), np.float64)
    cv2.grabCut(bgr, mask, None, bgd, fgd, 8, cv2.GC_INIT_WITH_MASK)
    fg = np.where((mask == cv2.GC_FGD) | (mask == cv2.GC_PR_FGD), 255, 0).astype(np.uint8)
    # purge pixels of definite background colours (thin scenery strips behind the
    # ears, horizon lines); they cannot be part of the character's palette
    fg[np.isin(labels, list(bg_clusters))] = 0
    # thin horizontal scenery lines (horizon, grass edges) survive GrabCut when their
    # grey matches armour/stone; voxel blocks are all >5 px tall, so a vertical
    # opening removes only such 1-4 px strips
    fg = cv2.morphologyEx(fg, cv2.MORPH_OPEN, np.ones((5, 1), np.uint8))
    fg = cv2.morphologyEx(fg, cv2.MORPH_OPEN, np.ones((3, 3), np.uint8))
    fg = cv2.morphologyEx(fg, cv2.MORPH_CLOSE, np.ones((3, 3), np.uint8))
    # keep the largest component
    n, labels, stats, _ = cv2.connectedComponentsWithStats(fg, 8)
    if n > 1:
        biggest = 1 + int(np.argmax(stats[1:, cv2.CC_STAT_AREA]))
        fg = np.where(labels == biggest, 255, 0).astype(np.uint8)
    # fill holes so face/interior detail is opaque
    ff = fg.copy()
    hh, ww = fg.shape
    m2 = np.zeros((hh + 2, ww + 2), np.uint8)
    cv2.floodFill(ff, m2, (0, 0), 255)
    holes = cv2.bitwise_not(ff)
    fg = cv2.bitwise_or(fg, holes)
    return fg


def bbox_of(mask: np.ndarray) -> tuple[int, int, int, int]:
    ys, xs = np.where(mask > 0)
    return int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1


def width_profile(mask: np.ndarray) -> np.ndarray:
    return (mask > 0).sum(axis=1).astype(np.float32)


def head_fraction(mask: np.ndarray, bbox: tuple[int, int, int, int]) -> dict:
    """Locate the neck as the first narrow waist below the head.

    The references are compact voxel characters: the head is a wide block, the neck
    a narrow gap, the shoulders wide again. No model is loaded - this is just the
    silhouette width profile of the prepared mask.
    """
    x0, y0, x1, y1 = bbox
    prof = width_profile(mask)[y0:y1]
    h = len(prof)
    if h < 30:
        return {"head_px": None, "head_fraction": None, "neck_y": None}
    k = 5
    smooth = np.convolve(prof, np.ones(k) / k, mode="same")
    peak = float(smooth.max())
    neck = None
    for i in range(int(h * 0.18), int(h * 0.55)):
        if smooth[i] < 0.62 * peak and smooth[i] <= smooth[i - 1] and smooth[i] <= smooth[i + 1]:
            neck = i
            break
    if neck is None:
        return {"head_px": None, "head_fraction": None, "neck_y": None}
    return {
        "head_px": int(neck),
        "head_fraction": round(neck / h, 3),
        "neck_y": int(y0 + neck),
        "shoulder_px": int(round(float(smooth[min(h - 1, neck + int(0.10 * h))]))),
        "total_px": int(h),
    }


def build_reference(cut_rgba: Image.Image, canvas=(768, 1024), fill_h=0.86, bottom_margin=52) -> tuple[Image.Image, int]:
    w, h = canvas
    cw, ch = cut_rgba.size
    scale = max(1, min((w - 16) // cw, int(h * fill_h) // ch))
    big = cut_rgba.resize((cw * scale, ch * scale), Image.NEAREST)
    out = Image.new("RGB", canvas, NEUTRAL)
    x = (w - big.width) // 2
    y = h - bottom_margin - big.height
    out.paste(big, (x, max(0, y)), big)
    return out, scale


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--source", required=True)
    ap.add_argument("--name", required=True)
    ap.add_argument("--rect", "--box", dest="rect", required=True,
                    help="x0,y0,x1,y1 — character silhouette box in source pixels")
    ap.add_argument("--pad", type=int, default=30, help="crop padding around the character box")
    ap.add_argument("--out", required=True)
    ap.add_argument("--measurements", default=None)
    args = ap.parse_args()

    rx0, ry0, rx1, ry1 = (int(v) for v in args.rect.split(","))
    x0, y0 = max(0, rx0 - args.pad), max(0, ry0 - args.pad)
    x1, y1 = rx1 + args.pad, ry1 + args.pad
    os.makedirs(args.out, exist_ok=True)
    src = Image.open(args.source)
    x1, y1 = min(x1, src.width), min(y1, src.height)
    crop = src.crop((x0, y0, x1, y1))
    rect_in_crop = (rx0 - x0, ry0 - y0, rx1 - x0, ry1 - y0)

    mask = segment(crop, rect_in_crop)
    bx0, by0, bx1, by1 = bbox_of(mask)
    alpha = Image.fromarray(mask, "L")
    cut = crop.convert("RGBA")
    cut.putalpha(alpha)

    # tight crop around the character: no scaling, only padding removal
    pad = 6
    tx0, ty0 = max(0, bx0 - pad), max(0, by0 - pad)
    tx1, ty1 = min(cut.width, bx1 + pad), min(cut.height, by1 + pad)
    tight = cut.crop((tx0, ty0, tx1, ty1))

    on_neutral = Image.new("RGB", tight.size, NEUTRAL)
    on_neutral.paste(tight, (0, 0), tight)
    ref, scale = build_reference(tight)

    paths = {
        "cutout": os.path.join(args.out, f"{args.name}_cutout.png"),
        "on_neutral": os.path.join(args.out, f"{args.name}_on_neutral.png"),
        "reference": os.path.join(args.out, f"{args.name}_reference_768x1024.png"),
    }
    cut.save(paths["cutout"])
    on_neutral.save(paths["on_neutral"])
    ref.save(paths["reference"])

    entry = {
        "name": args.name,
        "source": os.path.abspath(args.source),
        "source_sha256": sha256_file(args.source),
        "source_size": list(src.size),
        "character_rect_source": [rx0, ry0, rx1, ry1],
        "crop_pad": args.pad,
        "crop_box": [x0, y0, x1, y1],
        "crop_size": list(crop.size),
        "character_bbox_in_crop": [bx0, by0, bx1, by1],
        "character_size_px": [bx1 - bx0, by1 - by0],
        "aspect_h_over_w": round((by1 - by0) / max(1, bx1 - bx0), 3),
        "proportions_from_silhouette": head_fraction(mask, (bx0, by0, bx1, by1)),
        "tight_size": list(tight.size),
        "reference_scale_integer": scale,
        "reference_size": list(ref.size),
        "outputs": {k: {"path": os.path.abspath(v), "sha256": sha256_file(v), "bytes": os.path.getsize(v)}
                    for k, v in paths.items()},
        "segmentation": "cv2.grabCut (GC_INIT_WITH_MASK), largest component + hole fill, no resampling",
        "neutral_color_rgb": list(NEUTRAL),
    }
    with open(os.path.join(args.out, f"{args.name}.json"), "w", encoding="utf-8", newline="\n") as f:
        json.dump(entry, f, ensure_ascii=False, indent=2)
        f.write("\n")

    if args.measurements:
        os.makedirs(os.path.dirname(os.path.abspath(args.measurements)), exist_ok=True)
        data = {"references": {}}
        if os.path.exists(args.measurements):
            with open(args.measurements, "r", encoding="utf-8") as f:
                data = json.load(f)
        data.setdefault("references", {})[args.name] = entry
        with open(args.measurements, "w", encoding="utf-8", newline="\n") as f:
            json.dump(data, f, ensure_ascii=False, indent=2)
            f.write("\n")

    print(f"{args.name}: crop={crop.size} char={entry['character_size_px']} "
          f"tight={tight.size} scale=x{scale} -> {ref.size}")
    for k, v in paths.items():
        print(f"  {k}: {v} ({entry['outputs'][k]['bytes']} B)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
