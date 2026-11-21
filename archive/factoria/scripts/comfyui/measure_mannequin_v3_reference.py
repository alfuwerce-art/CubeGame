#!/usr/bin/env python3
"""Measure the owner-approved MANNEQUIN V3 reference (COMFY-STYLE-12 STYLE LOCK).

Input: the approved render with two figures (HUMAN MALE - MANNEQUIN V3, HUMAN FEMALE -
MANNEQUIN V3), registered byte-for-byte in outputs/factory/mannequin_v3/.

The image is a lit 3D render, not an orthographic technical plate, so every number below is a
*pixel measurement of the visible front projection*. It is reported as a preliminary design
parameter set, never as an exact 3D dimension (task section 2).

Method:
  1. background tone from the four image corners;
  2. body mask = clearly brighter than the background. Two effects must be survived:
     (a) shadowed seams (neck under the jaw) fall below the threshold, so one figure splits into
         head / torso / limbs; (b) the caption text and rules are as bright as the body.
     Tiny components are dropped first (caption glyphs are never >= min_comp_px), the two
     figures are labelled on a dilated copy, and the final mask is the raw threshold restricted
     to those two blobs;
  3. per-row total width, runs and the central run (the body column through the symmetry axis);
  4. landmarks by explicit rules - every rule is printed into the JSON;
  5. diagnostic overlay so the detected landmarks can be checked by eye.

Usage (ComfyUI embedded python carries PIL/numpy/cv2):
  python_embeded\\python.exe scripts\\comfyui\\measure_mannequin_v3_reference.py
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os

import cv2
import numpy as np


def sha256(path: str) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def body_mask(rgb: np.ndarray, bridge_px: int = 61,
              min_comp_px: int = 1500) -> tuple[list[np.ndarray], np.ndarray, dict]:
    """Return one mask per figure (left first), the union mask, and the measurement card."""
    gray = cv2.cvtColor(rgb, cv2.COLOR_RGB2GRAY)
    h, w = gray.shape
    corners = [gray[0, 0], gray[0, w - 1], gray[h - 1, 0], gray[h - 1, w - 1]]
    bg = float(np.median(corners))
    body_bright = float(np.percentile(gray, 99.0))
    thr = bg + 0.25 * (body_bright - bg)
    raw = (gray > thr).astype(np.uint8)
    raw = cv2.morphologyEx(raw, cv2.MORPH_CLOSE, np.ones((3, 3), np.uint8))

    n, labels, stats, _ = cv2.connectedComponentsWithStats(raw, connectivity=8)
    solid = np.zeros_like(raw)
    kept_parts = 0
    for i in range(1, n):
        if stats[i, cv2.CC_STAT_AREA] >= min_comp_px:
            solid[labels == i] = 1
            kept_parts += 1

    grown = cv2.dilate(solid, np.ones((bridge_px, bridge_px), np.uint8), iterations=1)
    n2, labels2, stats2, _ = cv2.connectedComponentsWithStats(grown, connectivity=8)
    order = sorted(range(1, n2), key=lambda i: stats2[i, cv2.CC_STAT_AREA], reverse=True)[:2]
    order.sort(key=lambda i: stats2[i, cv2.CC_STAT_LEFT])   # left figure first
    figures = [((solid == 1) & (labels2 == i)).astype(np.uint8) for i in order]
    keep = (figures[0] | figures[1]) if len(figures) == 2 else np.zeros_like(solid)
    info = {
        "background_gray": round(bg, 1),
        "body_bright_p99_gray": round(body_bright, 1),
        "threshold_gray": round(thr, 1),
        "min_component_px": min_comp_px,
        "components_raw": int(n - 1),
        "components_solid": kept_parts,
        "bridge_px": bridge_px,
        "figures_kept": len(order),
    }
    return figures, keep, info


def row_profile(comp: np.ndarray, min_run: int, center_x: float) -> list[dict]:
    ys, xs = np.nonzero(comp)
    y0, y1 = int(ys.min()), int(ys.max())
    out = []
    for y in range(y0, y1 + 1):
        row = np.nonzero(comp[y])[0]
        if len(row) == 0:
            out.append({"y": y, "total_w": 0, "total_x": None, "runs": [], "n_runs": 0,
                        "central": None, "central_w": 0})
            continue
        parts = np.split(row, np.where(np.diff(row) > 1)[0] + 1)
        runs = [(int(p[0]), int(p[-1])) for p in parts if len(p) >= min_run]
        central = next((r for r in runs if r[0] <= center_x <= r[1]), None)
        out.append({
            "y": y,
            "total_w": int(row.max() - row.min() + 1),
            "total_x": [int(row.min()), int(row.max())],
            "runs": runs,
            "n_runs": len(runs),
            "central": list(central) if central else None,
            "central_w": (central[1] - central[0] + 1) if central else 0,
        })
    return out


def probe_shadowed(gray: np.ndarray, band: list[dict], center_x: float, min_run: int,
                   bg: float, body_bright: float) -> dict:
    """Neck is dark: re-threshold only that band, and report the level that reveals it."""
    best = {"width_px": None, "gray_threshold": None, "rows": 0}
    for frac in (0.20, 0.16, 0.12, 0.08, 0.05, 0.03):
        thr = bg + frac * (body_bright - bg)
        widths, rows = [], 0
        for r in band:
            xs = np.nonzero(gray[r["y"]] > thr)[0]
            if len(xs) == 0:
                continue
            parts = np.split(xs, np.where(np.diff(xs) > 1)[0] + 1)
            runs = [(int(p[0]), int(p[-1])) for p in parts if len(p) >= min_run]
            cen = next((v for v in runs if v[0] <= center_x <= v[1]), None)
            if cen:
                widths.append(cen[1] - cen[0] + 1)
                rows += 1
        if rows >= 3:
            best = {"width_px": int(np.median(widths)), "gray_threshold": round(thr, 1),
                    "rows": rows}
            break
    return best


def landmarks(comp: np.ndarray, gray: np.ndarray, bg: float, body_bright: float) -> dict:
    ys, xs = np.nonzero(comp)
    x0, y0, x1, y1 = int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max())
    total_h = y1 - y0 + 1
    total_w = x1 - x0 + 1
    center_x = (x0 + x1) / 2.0
    min_run = max(4, int(total_h * 0.008))
    rows = row_profile(comp, min_run, center_x)
    by_y = {r["y"]: r for r in rows}

    def frac(v):
        return round((v - y0) / total_h, 4) if v is not None else None

    # --- head block -------------------------------------------------------
    head_band = [r for r in rows if r["y"] <= y0 + total_h * 0.14]
    head_width = int(np.median([r["central_w"] or r["total_w"] for r in head_band]))

    jaw_bottom = None
    for r in rows:
        if r["y"] <= y0 + total_h * 0.08:
            continue
        if (r["central_w"] or r["total_w"]) >= head_width * 0.85:
            jaw_bottom = r["y"]
        elif jaw_bottom is not None:
            break
    head_h = (jaw_bottom - y0 + 1) if jaw_bottom else None

    ear_rows = [r for r in rows if r["total_w"] > head_width * 1.06
                and r["y"] <= (jaw_bottom or y0 + total_h * 0.4)]
    ear_span = max((r["total_w"] for r in ear_rows), default=None)
    ear_y0 = min((r["y"] for r in ear_rows), default=None)
    ear_y1 = max((r["y"] for r in ear_rows), default=None)

    # --- shoulders / arms --------------------------------------------------
    arm_span = max(r["total_w"] for r in rows)
    arm_rows = [r["y"] for r in rows if r["total_w"] > 0.90 * arm_span]
    arm_top, arm_bottom = min(arm_rows), max(arm_rows)

    shoulder_top = None
    for r in rows:
        if jaw_bottom is not None and r["y"] > jaw_bottom and r["total_w"] > head_width * 1.6:
            shoulder_top = r["y"]
            break

    neck_band = [r for r in rows if jaw_bottom is not None and shoulder_top is not None
                 and jaw_bottom < r["y"] < shoulder_top]
    neck = probe_shadowed(gray, neck_band, center_x, min_run, bg, body_bright) if neck_band else {
        "width_px": None, "gray_threshold": None, "rows": 0}

    # hand: outermost block of the arm band on the viewer-left side
    widest = max(rows, key=lambda r: r["total_w"])
    hand_run = widest["runs"][0] if widest["runs"] else None
    hand_x0, hand_x1 = (hand_run[0], hand_run[1]) if hand_run else (None, None)
    hand_rows = [r["y"] for r in rows
                 if hand_x0 is not None and r["runs"] and r["runs"][0][0] <= hand_x0 + 2]

    # --- torso -------------------------------------------------------------
    crotch = None
    for r in rows:
        if r["y"] <= arm_bottom + 3 or r["n_runs"] < 2:
            continue
        legs = [v for v in r["runs"] if (v[1] - v[0] + 1) >= total_h * 0.05]
        if len(legs) >= 2 and legs[0][1] < center_x < legs[-1][0]:
            crotch = r["y"]
            break

    torso_band = [r for r in rows if arm_bottom < r["y"] and (crotch is None or r["y"] < crotch)]
    chest_probe = by_y.get((shoulder_top or arm_top) + 8)
    chest_width = (chest_probe["central_w"] or None) if chest_probe else None
    waist = min((r for r in torso_band if r["central_w"]), key=lambda r: r["central_w"],
                default=None) if any(r["central_w"] for r in torso_band) else None
    pelvis = max((r for r in torso_band if r["central_w"]), key=lambda r: r["central_w"],
                 default=None) if any(r["central_w"] for r in torso_band) else None

    # --- legs / feet -------------------------------------------------------
    leg_band = [r for r in rows if crotch is not None and r["y"] > crotch and r["n_runs"] >= 2]
    thigh = leg_band[int(len(leg_band) * 0.12)] if leg_band else None
    thigh_w = (thigh["runs"][0][1] - thigh["runs"][0][0] + 1) if thigh else None
    leg_gap = (thigh["runs"][-1][0] - thigh["runs"][0][1] - 1) if thigh and len(thigh["runs"]) >= 2 else None
    foot_row = by_y.get(y1 - max(2, int(total_h * 0.01)))
    foot_w = (foot_row["runs"][0][1] - foot_row["runs"][0][0] + 1) if foot_row and foot_row["runs"] else None
    foot_span = foot_row["total_w"] if foot_row else None

    return {
        "bbox_xyxy": [x0, y0, x1, y1],
        "figure_width_px": total_w,
        "figure_height_px": total_h,
        "head": {
            "crown_y": y0, "head_width_px": head_width,
            "head_width_over_height": round(head_width / total_h, 4),
            "jaw_bottom_y": jaw_bottom, "head_block_height_px": head_h,
            "head_block_over_height": round(head_h / total_h, 4) if head_h else None,
            "ear_y0": ear_y0, "ear_y1": ear_y1,
            "ear_height_px": (ear_y1 - ear_y0 + 1) if ear_y0 is not None else None,
            "ear_span_px": ear_span,
            "ear_protrusion_px": (round((ear_span - head_width) / 2, 1) if ear_span else None),
            "ear_band_frac": [frac(ear_y0), frac(ear_y1)],
        },
        "neck": {
            "neck_width_px": neck["width_px"],
            "neck_gray_threshold": neck["gray_threshold"],
            "neck_rows_detected": neck["rows"],
            "shoulder_top_y": shoulder_top,
            "shoulder_top_frac": frac(shoulder_top),
        },
        "arms": {
            "arm_span_px": arm_span,
            "arm_span_over_height": round(arm_span / total_h, 4),
            "arm_span_over_figure_width": round(arm_span / total_w, 4),
            "arm_top_y": arm_top, "arm_bottom_y": arm_bottom,
            "arm_band_frac": [frac(arm_top), frac(arm_bottom)],
            "arm_band_height_px": arm_bottom - arm_top + 1,
            "hand_run_px": [hand_x0, hand_x1] if hand_run else None,
            "hand_width_px": (hand_x1 - hand_x0 + 1) if hand_run else None,
            "hand_height_px": (max(hand_rows) - min(hand_rows) + 1) if hand_rows else None,
        },
        "torso": {
            "chest_width_px": chest_width,
            "chest_over_height": round(chest_width / total_h, 4) if chest_width else None,
            "waist_width_px": waist["central_w"] if waist else None,
            "waist_y": waist["y"] if waist else None,
            "waist_y_frac": frac(waist["y"]) if waist else None,
            "pelvis_width_px": pelvis["central_w"] if pelvis else None,
            "pelvis_over_height": round(pelvis["central_w"] / total_h, 4) if pelvis else None,
            "pelvis_over_shoulders": (round(pelvis["central_w"] / arm_span, 4)
                                      if pelvis else None),
            "crotch_y": crotch,
            "crotch_frac": frac(crotch),
        },
        "legs": {
            "leg_length_px": (y1 - crotch + 1) if crotch is not None else None,
            "leg_over_height": round((y1 - crotch + 1) / total_h, 4) if crotch is not None else None,
            "thigh_y": thigh["y"] if thigh else None,
            "thigh_width_px": thigh_w,
            "thigh_over_height": round(thigh_w / total_h, 4) if thigh_w else None,
            "leg_gap_px": leg_gap,
            "foot_width_px": foot_w,
            "foot_span_px": foot_span,
            "sole_y": y1,
        },
        "row_profile_every_8px": [r for r in rows if (r["y"] - y0) % 8 == 0],
    }


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--image", default="outputs/factory/mannequin_v3/REFERENCE_MANNEQUIN_V3_OWNER.png")
    ap.add_argument("--out", default="measurements/comfy_style_12/mannequin_v3")
    ap.add_argument("--overlay", default="outputs/factory/mannequin_v3/REFERENCE_MANNEQUIN_V3_LANDMARKS.png")
    args = ap.parse_args()

    bgr = cv2.imread(args.image, cv2.IMREAD_COLOR)
    if bgr is None:
        raise SystemExit(f"cannot read {args.image}")
    rgb = cv2.cvtColor(bgr, cv2.COLOR_BGR2RGB)
    gray = cv2.cvtColor(bgr, cv2.COLOR_BGR2GRAY)
    masks, keep, mask_info = body_mask(rgb)
    if len(masks) != 2:
        raise SystemExit(f"expected 2 figures, found {len(masks)}")
    bg = mask_info["background_gray"]
    body_bright = mask_info["body_bright_p99_gray"]

    overlay = bgr.copy()
    result = {
        "task": "COMFY-STYLE-12",
        "role": "OWNER-APPROVED VISUAL REFERENCE (MANNEQUIN V3) - pixel measurement",
        "image": args.image,
        "image_sha256": sha256(args.image),
        "image_size_xy": [int(rgb.shape[1]), int(rgb.shape[0])],
        "mask": mask_info,
        "note": ("Numbers are measurements of a lit 3D front render, not orthographic or 3D "
                 "dimensions. Use them as preliminary design parameters (task section 2)."),
        "figures": {},
    }

    names = ["HUMAN_MALE_MANNEQUIN_V3", "HUMAN_FEMALE_MANNEQUIN_V3"]
    for name, comp in zip(names, masks):
        lm = landmarks(comp, gray, bg, body_bright)
        result["figures"][name] = {"area_px": int(comp.sum()), "landmarks": lm}
        x0, y0, x1, y1 = lm["bbox_xyxy"]
        cv2.rectangle(overlay, (x0, y0), (x1, y1), (0, 200, 255), 1)
        marks = {
            "crown": (lm["head"]["crown_y"], (0, 255, 0)),
            "jaw": (lm["head"]["jaw_bottom_y"], (255, 0, 0)),
            "ear0": (lm["head"]["ear_y0"], (0, 255, 0)),
            "ear1": (lm["head"]["ear_y1"], (0, 255, 0)),
            "chest_top": (lm["neck"]["shoulder_top_y"], (0, 165, 255)),
            "arm0": (lm["arms"]["arm_top_y"], (255, 0, 255)),
            "arm1": (lm["arms"]["arm_bottom_y"], (255, 0, 255)),
            "crotch": (lm["torso"]["crotch_y"], (0, 255, 255)),
            "waist": (lm["torso"]["waist_y"], (255, 255, 0)),
            "sole": (lm["legs"]["sole_y"], (200, 200, 255)),
        }
        for key, (y, color) in marks.items():
            if y is None:
                continue
            cv2.line(overlay, (x0 - 16, y), (x1 + 16, y), color, 1)
            cv2.putText(overlay, key, (x1 + 18, y + 4), cv2.FONT_HERSHEY_SIMPLEX, 0.4,
                        color, 1, cv2.LINE_AA)
        cv2.putText(overlay, f"{name} H={lm['figure_height_px']}", (x0, y0 - 10),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.55, (0, 255, 255), 1, cv2.LINE_AA)

    os.makedirs(args.out, exist_ok=True)
    with open(os.path.join(args.out, "REFERENCE_MEASUREMENT.json"), "w",
              encoding="utf-8", newline="\n") as f:
        json.dump(result, f, ensure_ascii=False, indent=2)
        f.write("\n")
    if args.overlay:
        os.makedirs(os.path.dirname(os.path.abspath(args.overlay)), exist_ok=True)
        cv2.imwrite(args.overlay, overlay)

    print(json.dumps({"image_sha256": result["image_sha256"], "mask": mask_info},
                     ensure_ascii=False, indent=2))
    for n in result["figures"]:
        lm = result["figures"][n]["landmarks"]
        print(f"{n} H={lm['figure_height_px']} W={lm['figure_width_px']} | "
              f"head {lm['head']['head_width_px']}x{lm['head']['head_block_height_px']} "
              f"({lm['head']['head_block_over_height']:.3f}H) | "
              f"ears span {lm['head']['ear_span_px']} y{lm['head']['ear_y0']}-{lm['head']['ear_y1']} | "
              f"neck {lm['neck']['neck_width_px']} | chest {lm['torso']['chest_width_px']} "
              f"waist {lm['torso']['waist_width_px']} pelvis {lm['torso']['pelvis_width_px']} | "
              f"armspan {lm['arms']['arm_span_px']} band {lm['arms']['arm_band_height_px']} "
              f"hand {lm['arms']['hand_width_px']}x{lm['arms']['hand_height_px']} | "
              f"crotch {lm['torso']['crotch_y']} thighs {lm['legs']['thigh_width_px']} "
              f"gap {lm['legs']['leg_gap_px']} foot {lm['legs']['foot_width_px']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
