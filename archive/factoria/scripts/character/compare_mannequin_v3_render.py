"""MANNEQUIN-RIG-01: compare the generated block mannequin with the approved V3 reference.

Reads
  * the owner reference render (local, git-ignored) and its measured male figure,
  * the silhouette render of the generated model (transparent PNG),
and produces a same-scale side-by-side, a silhouette overlay and a numeric proportion
table.  All widths are normalised by the figure height, so the comparison does not
depend on the render resolution.

The reference is a lit render, so deep shadow grooves inside the silhouette contain no
bright pixels; every width is therefore taken as a robust statistic over non-empty rows
inside a band of the figure height.

Usage:
    python scripts/character/compare_mannequin_v3_render.py
"""
from __future__ import annotations

import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
REF = ROOT / "outputs" / "factory" / "mannequin_v3" / "REFERENCE_MANNEQUIN_V3_OWNER.png"
RUN = ROOT / "outputs" / "prototypes" / "mannequin_v3_rig" / "run1"
MODEL_ALPHA = RUN / "renders" / "tpose_front_silhouette.png"
OUT = RUN / "compare"                       # review images (local only: they show the owner reference)
REPORT_JSON = RUN / "PROPORTIONS_COMPARISON.json"
REF_MALE_BBOX = (240, 97, 782, 754)          # measured male figure in the reference sheet
TARGET_H = 900
METER_HEIGHT = 1.80

# bands as fractions of the figure height measured from the crown
BANDS = {
    "head_cube_width": (0.02, 0.10, "median"),
    "ear_span_width": (0.12, 0.26, "max"),
    "arm_span_width": (0.30, 0.52, "max"),
    "chest_width": (0.50, 0.56, "median"),
    "waist_width": (0.55, 0.63, "min"),
    "pelvis_width": (0.63, 0.70, "max"),
    "thigh_width": (0.76, 0.82, "min"),
    "foot_width": (0.93, 1.00, "max"),
}


def mask_from_reference(img: Image.Image, bbox) -> np.ndarray:
    gray = np.asarray(img.convert("L")).astype(np.int16)
    x0, y0, x1, y1 = bbox
    return gray[y0:y1, x0:x1] > 133       # threshold from REFERENCE_MEASUREMENT.json


def mask_from_alpha(img: Image.Image) -> np.ndarray:
    return np.asarray(img.convert("RGBA"))[:, :, 3] > 128


def fill_holes(mask: np.ndarray) -> np.ndarray:
    """Fill interior holes (the reference render has dark shadow grooves inside the body)."""
    h, w = mask.shape
    outside = np.zeros((h, w), dtype=bool)
    stack = []
    for x in range(w):
        for y in (0, h - 1):
            if not mask[y, x] and not outside[y, x]:
                outside[y, x] = True
                stack.append((y, x))
    for y in range(h):
        for x in (0, w - 1):
            if not mask[y, x] and not outside[y, x]:
                outside[y, x] = True
                stack.append((y, x))
    while stack:
        y, x = stack.pop()
        for ny, nx in ((y - 1, x), (y + 1, x), (y, x - 1), (y, x + 1)):
            if 0 <= ny < h and 0 <= nx < w and not mask[ny, nx] and not outside[ny, nx]:
                outside[ny, nx] = True
                stack.append((ny, nx))
    return mask | ~outside


def row_widths(mask: np.ndarray) -> np.ndarray:
    """Horizontal extent of the body in every row (0 for empty rows)."""
    out = []
    for row in mask:
        idx = np.where(row)[0]
        out.append(0.0 if idx.size == 0 else float(idx[-1] - idx[0] + 1))
    return np.asarray(out)


def profile(mask: np.ndarray) -> dict:
    rows = np.where(mask.any(axis=1))[0]
    cols = np.where(mask.any(axis=0))[0]
    y0, y1 = int(rows[0]), int(rows[-1]) + 1
    x0, x1 = int(cols[0]), int(cols[-1]) + 1
    sub = mask[y0:y1, x0:x1]
    height = sub.shape[0]
    widths = row_widths(sub)
    out = {"height_px": height, "width_px": int(sub.shape[1]),
           "bbox_local": [x0, y0, x1, y1]}
    for name, (f0, f1, stat) in BANDS.items():
        band = widths[int(height * f0): max(int(height * f1), int(height * f0) + 1)]
        band = band[band > 0]
        if band.size == 0:
            out[name] = 0.0
            continue
        value = {"median": float(np.median(band)), "max": float(band.max()),
                 "min": float(band.min())}[stat]
        out[name] = round(value / height, 4)
    # arm span from the widest row of the whole figure
    out["arm_span_width"] = round(float(widths.max()) / height, 4)
    fingers = {
        "head_cube_over_height": out["head_cube_width"],
        "ear_span_over_height": out["ear_span_width"],
        "arm_span_over_height": out["arm_span_width"],
        "chest_over_height": out["chest_width"],
        "waist_over_height": out["waist_width"],
        "pelvis_over_height": out["pelvis_width"],
        "thigh_over_height": out["thigh_width"],
        "foot_over_height": out["foot_width"],
    }
    out["ratios"] = fingers
    return out


def crop_mask(mask: np.ndarray) -> np.ndarray:
    rows = np.where(mask.any(axis=1))[0]
    cols = np.where(mask.any(axis=0))[0]
    return mask[rows[0]: rows[-1] + 1, cols[0]: cols[-1] + 1]


def scale_to_height(img: Image.Image, height: int) -> Image.Image:
    w, h = img.size
    return img.resize((max(1, round(w * height / h)), height), Image.LANCZOS)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    ref_img = Image.open(REF).convert("RGB")
    ref_mask = fill_holes(mask_from_reference(ref_img, REF_MALE_BBOX))
    model_alpha = Image.open(MODEL_ALPHA).convert("RGBA")
    model_mask = fill_holes(mask_from_alpha(model_alpha))

    ref_prof = profile(ref_mask)
    model_prof = profile(model_mask)
    table = {
        "reference": ref_prof["ratios"],
        "model": model_prof["ratios"],
        "delta": {k: round(model_prof["ratios"][k] - ref_prof["ratios"][k], 4)
                  for k in ref_prof["ratios"]},
        "meta": {
            "reference_px": {"height": ref_prof["height_px"], "width": ref_prof["width_px"]},
            "model_px": {"height": model_prof["height_px"], "width": model_prof["width_px"],
                         "arm_span_m": round(model_prof["ratios"]["arm_span_over_height"] * METER_HEIGHT, 4)},
        },
    }
    (REPORT_JSON).write_text(
        json.dumps({"task": "MANNEQUIN-RIG-01",
                    "reference": str(REF.relative_to(ROOT)).replace("\\", "/"),
                    "model_height_m": METER_HEIGHT, "table": table}, indent=2,
                   ensure_ascii=False) + "\n", encoding="utf-8")

    # --- same-scale side by side -------------------------------------------------
    ref_crop = ref_img.crop(REF_MALE_BBOX)
    model_crop = model_alpha.crop((model_prof["bbox_local"][0], model_prof["bbox_local"][1],
                                   model_prof["bbox_local"][2], model_prof["bbox_local"][3]))
    ref_scaled = scale_to_height(ref_crop, TARGET_H)
    model_scaled = scale_to_height(model_crop, TARGET_H)
    pad, label_h = 40, 46
    canvas = Image.new("RGB", (ref_scaled.width + model_scaled.width + pad * 3,
                               TARGET_H + label_h + pad * 2), (46, 46, 50))
    canvas.paste(ref_scaled, (pad, label_h + pad))
    bg = Image.new("RGB", model_scaled.size, (70, 70, 74))
    bg.paste(model_scaled, (0, 0), model_scaled)
    canvas.paste(bg, (pad * 2 + ref_scaled.width, label_h + pad))
    draw = ImageDraw.Draw(canvas)
    try:
        font = ImageFont.truetype("arial.ttf", 26)
    except Exception:
        font = ImageFont.load_default()
    draw.text((pad, 14), "V3 REFERENCE (approved male mannequin)", font=font, fill=(235, 235, 235))
    draw.text((pad * 2 + ref_scaled.width, 14), "GENERATED BLOCK MANNEQUIN (T-pose)",
              font=font, fill=(235, 235, 235))
    canvas.save(OUT / "compare_front.png")

    # --- silhouette difference ---------------------------------------------------
    h = TARGET_H
    ref_bin = crop_mask(ref_mask)
    model_bin = crop_mask(model_mask)
    ref_s = np.asarray(Image.fromarray(ref_bin.astype(np.uint8) * 255).resize(
        (max(1, round(ref_bin.shape[1] * h / ref_bin.shape[0])), h), Image.NEAREST)) > 127
    model_s = np.asarray(Image.fromarray(model_bin.astype(np.uint8) * 255).resize(
        (max(1, round(model_bin.shape[1] * h / model_bin.shape[0])), h), Image.NEAREST)) > 127
    width = max(ref_s.shape[1], model_s.shape[1])
    diff = np.zeros((h, width, 3), dtype=np.uint8)
    diff[:, :] = (40, 40, 44)

    def place(mask, colour_full, colour_only=None):
        x_off = (width - mask.shape[1]) // 2
        target = diff[:, x_off: x_off + mask.shape[1]]
        other = ref_s if mask is model_s else model_s
        other_off = (width - other.shape[1]) // 2
        other_layer = diff[:, other_off: other_off + other.shape[1]]
        # only-mask pixels are highlighted, shared pixels stay neutral grey
        shared = np.zeros_like(mask)
        ox0 = max(0, other_off - x_off)
        ox1 = min(mask.shape[1], other_off + other.shape[1] - x_off)
        if ox1 > ox0:
            shared[:, ox0:ox1] = other[:, ox0 - (other_off - x_off): ox1 - (other_off - x_off)]
        for c in range(3):
            target[:, :, c] = np.where(mask & shared, 165, target[:, :, c])
            if colour_only is not None:
                target[:, :, c] = np.where(mask & ~shared, colour_only[c], target[:, :, c])

    place(ref_s, None, (80, 130, 200))
    place(model_s, None, (225, 90, 70))
    img = Image.fromarray(diff)
    draw = ImageDraw.Draw(img)
    try:
        font = ImageFont.truetype("arial.ttf", 22)
    except Exception:
        font = ImageFont.load_default()
    draw.rectangle((18, 18, 610, 96), fill=(32, 32, 36))
    draw.text((30, 24), "silhouette difference at equal height", font=font, fill=(230, 230, 230))
    draw.rectangle((30, 58, 60, 78), fill=(225, 90, 70))
    draw.text((70, 56), "model only", font=font, fill=(225, 90, 70))
    draw.rectangle((200, 58, 230, 78), fill=(80, 130, 200))
    draw.text((240, 56), "reference only", font=font, fill=(80, 130, 200))
    draw.rectangle((420, 58, 450, 78), fill=(165, 165, 165))
    draw.text((460, 56), "shared", font=font, fill=(175, 175, 175))
    img.save(OUT / "compare_overlay.png")

    print("COMPARE_OUT=" + str(OUT))
    for key, ref_v in table["reference"].items():
        print(f"{key:26s} ref={ref_v:.4f} model={table['model'][key]:.4f} "
              f"delta={table['delta'][key]:+.4f}")
    return table


if __name__ == "__main__":
    main()
