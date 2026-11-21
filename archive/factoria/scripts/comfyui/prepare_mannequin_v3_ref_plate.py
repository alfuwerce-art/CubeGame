#!/usr/bin/env python3
"""Derive a technical control plate from the owner-approved MANNEQUIN V3 render (COMFY-STYLE-12).

The deterministic block plates (prepare_mannequin_v3_guides.py) describe the approved
proportions but only as a two-tone silhouette, which the stack reproduced as a flat cutout.
This script takes the approved render itself as the control input: one figure is cropped,
scaled by the same similarity transform as the block plates (crown 174, sole 850, symmetry
axis 383.5 on a 768x1024 canvas) and written as a portable PNG.

It is a technical ControlNet input only: it never enters the sampler as an image (EmptyLatent
stays), and it is not a style reference for other assets.

Usage:
  python_embeded\\python.exe scripts\\comfyui\\prepare_mannequin_v3_ref_plate.py
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os

import cv2
import numpy as np

SIZE = (768, 1024)
AXIS = 383.5
CROWN_Y = 174
SOLE_Y = 850
FIGURE_H = SOLE_Y - CROWN_Y

# Measured figure bounding boxes in the approved render (see REFERENCE_MEASUREMENT.json).
FIGURES = {
    "HUMAN_MALE_MANNEQUIN_REFPLATE": {
        "bbox_xyxy": [240, 97, 782, 754],
        "crown_y_ref": 97,
        "figure_height_ref": 658,
        "center_x_ref": 511,
    },
    "HUMAN_FEMALE_MANNEQUIN_REFPLATE": {
        "bbox_xyxy": [1023, 113, 1505, 754],
        "crown_y_ref": 113,
        "figure_height_ref": 642,
        "center_x_ref": 1264,
    },
}


def sha256(path: str) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def build(img: np.ndarray, spec: dict, mask: np.ndarray | None = None) -> np.ndarray:
    x0, y0, x1, y1 = spec["bbox_xyxy"]
    crop = img[y0:y1 + 1, x0:x1 + 1]
    m = None if mask is None else ((mask[y0:y1 + 1, x0:x1 + 1] > 0).astype(np.uint8) * 255)
    s = FIGURE_H / spec["figure_height_ref"]
    w = int(round(crop.shape[1] * s))
    h = int(round(crop.shape[0] * s))
    resized = cv2.resize(crop, (w, h), interpolation=cv2.INTER_AREA)
    canvas = np.full((SIZE[1], SIZE[0], 3), 158, dtype=np.uint8)
    left = int(round(AXIS - w / 2))
    top = CROWN_Y
    if m is None:
        canvas[top:top + h, left:left + w] = resized
    else:
        mres = cv2.resize(m, (w, h), interpolation=cv2.INTER_NEAREST)
        mres = cv2.dilate(mres, np.ones((3, 3), np.uint8))
        region = canvas[top:top + h, left:left + w]
        region[mres > 127] = resized[mres > 127]
        canvas[top:top + h, left:left + w] = region
    return canvas


def canny_preview(src: str, dst: str, low: float = 0.35, high: float = 0.75) -> dict:
    img = cv2.imread(src, cv2.IMREAD_COLOR)
    gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
    edges = cv2.Canny(gray, int(low * 255), int(high * 255))
    cv2.imwrite(dst, edges)
    ys, xs = np.nonzero(edges)
    return {"file": dst, "sha256": sha256(dst), "low": low, "high": high,
            "edge_pixels": int(edges.sum() // 255),
            "bbox_xyxy": [int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max())]}


def reinforce_edges(plate: np.ndarray, profile: dict, tone: int = 96, px: int = 2,
                    only: set[str] | None = None) -> tuple[np.ndarray, list]:
    """Draw the measured block boundaries of the deterministic guide onto the render plate.

    Reason: in a flat front projection the female chest box merges with the shoulder bar (its
    top and side edges are hidden), so Canny hands the model one wide horizontal bar and the
    torso collapses to a narrow column. The block boundaries are measured rectangles from the
    approved render, so this only makes the existing geometry explicit - it adds no new shape.
    """
    import importlib.util
    here = os.path.dirname(os.path.abspath(__file__))
    path = os.path.join(here, "prepare_mannequin_v3_guides.py")
    spec = importlib.util.spec_from_file_location("prep_v3", path)
    prep = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(prep)
    parts = prep.parts_of(profile)
    out = plate.copy()
    for p in parts:
        if only and p["name"] not in only:
            continue
        cv2.rectangle(out, (p["x0"], p["y0"]), (p["x1"] - 1, p["y1"] - 1),
                      (tone, tone, tone), px)
    return out, parts


def seal_horizontal_gaps(plate: np.ndarray, mask: np.ndarray, pixels: int,
                         tone: int = 158) -> tuple[np.ndarray, dict]:
    """Bridge the shadow gaps along the arm chain without touching vertical gaps.

    In the approved female render the arm modules are separated by deep shadow slits (the
    connectors are almost invisible), so the control plate teaches the model floating tiles.
    A horizontal-only close (pixels wide) plus a telea inpaint of the closed-minus-real region
    keeps the silhouette steps but makes the arm chain one continuous volume, as it already is
    in the male render. The leg gap is vertical and therefore untouched.
    """
    h, w = mask.shape
    k = np.ones((1, pixels), np.uint8)
    closed = cv2.morphologyEx(mask, cv2.MORPH_CLOSE, k)
    gap = ((closed > 0) & (mask == 0)).astype(np.uint8) * 255
    sealed = cv2.inpaint(plate, gap, 5, cv2.INPAINT_TELEA)
    return sealed, {"kernel_px": pixels, "orientation": "horizontal",
                    "filled_pixels": int((gap > 0).sum())}


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--reference", default="outputs/factory/mannequin_v3/REFERENCE_MANNEQUIN_V3_OWNER.png")
    ap.add_argument("--out", default="outputs/factory/mannequin_v3")
    ap.add_argument("--meta", default="measurements/comfy_style_12/mannequin_v3")
    ap.add_argument("--seal-horizontal", type=int, default=0,
                    help="seal horizontal shadow gaps of the arm chain, in pixels")
    ap.add_argument("--reinforce-torso", action="store_true",
                    help="write a variant with only the measured chest/waist/pelvis boundaries")
    ap.add_argument("--reinforce", action="store_true",
                    help="also write a variant with the measured block boundaries drawn in")
    args = ap.parse_args()
    os.makedirs(args.out, exist_ok=True)
    os.makedirs(args.meta, exist_ok=True)

    img = cv2.imread(args.reference, cv2.IMREAD_COLOR)
    if img is None:
        raise SystemExit(f"cannot read {args.reference}")

    # figure masks from the same measurement code as the proportion report
    import importlib.util
    here = os.path.dirname(os.path.abspath(__file__))
    spec_path = os.path.join(here, "measure_mannequin_v3_reference.py")
    spec_mod = importlib.util.spec_from_file_location("measure_v3", spec_path)
    measure = importlib.util.module_from_spec(spec_mod)
    spec_mod.loader.exec_module(measure)
    figs, _keep, _info = measure.body_mask(cv2.cvtColor(img, cv2.COLOR_BGR2RGB))
    masks = {"HUMAN_MALE_MANNEQUIN_REFPLATE": figs[0],
             "HUMAN_FEMALE_MANNEQUIN_REFPLATE": figs[1]}

    card = {
        "task": "COMFY-STYLE-12",
        "role": "ControlNet-only plate derived from the owner-approved MANNEQUIN V3 render",
        "reference": args.reference,
        "reference_sha256": sha256(args.reference),
        "canvas": list(SIZE),
        "crown_y": CROWN_Y,
        "sole_y": SOLE_Y,
        "symmetry_axis_x": AXIS,
        "note": ("One figure cropped from the approved render, scaled by the same transform as "
                 "the block plates. Technical control input only, never a sampler image."),
        "generator": "scripts/comfyui/prepare_mannequin_v3_ref_plate.py",
        "plates": {},
    }
    for name, spec in FIGURES.items():
        plate = build(img, spec, masks.get(name))
        png = os.path.join(args.out, name + ".png")
        cv2.imwrite(png, plate)
        entry = {"bbox_xyxy_ref": spec["bbox_xyxy"], "file": png, "sha256": sha256(png),
                 "scale": round(FIGURE_H / spec["figure_height_ref"], 6),
                 "background_cleaned": True,
                 "canny": canny_preview(png, os.path.join(args.out, name + "_CANNY.png"))}

        if args.reinforce:
            import importlib.util
            here = os.path.dirname(os.path.abspath(__file__))
            spec_path = os.path.join(here, "prepare_mannequin_v3_guides.py")
            prep_spec = importlib.util.spec_from_file_location("prep_v3", spec_path)
            prep = importlib.util.module_from_spec(prep_spec)
            prep_spec.loader.exec_module(prep)
            guide_name = name.replace("_REFPLATE", "_GUIDE")
            reinforced, parts = reinforce_edges(plate, prep.PROFILES[guide_name])
            rpng = os.path.join(args.out, name.replace("_REFPLATE", "_REFPLATE_REINFORCED") + ".png")
            cv2.imwrite(rpng, reinforced)
            entry["reinforced"] = {
                "file": rpng, "sha256": sha256(rpng),
                "tone": 96, "line_px": 2,
                "boundaries": [{"name": p["name"], "side": p["side"],
                                "rect": [p["x0"], p["y0"], p["x1"], p["y1"]]} for p in parts],
                "canny": canny_preview(rpng, rpng.replace(".png", "_CANNY.png")),
            }
        if args.reinforce_torso:
            import importlib.util
            here = os.path.dirname(os.path.abspath(__file__))
            spec_path = os.path.join(here, "prepare_mannequin_v3_guides.py")
            prep_spec = importlib.util.spec_from_file_location("prep_v3", spec_path)
            prep = importlib.util.module_from_spec(prep_spec)
            prep_spec.loader.exec_module(prep)
            guide_name = name.replace("_REFPLATE", "_GUIDE")
            torso, parts = reinforce_edges(plate, prep.PROFILES[guide_name],
                                           only={"chest", "abdomen", "waist", "pelvis"})
            tpng = os.path.join(args.out, name.replace("_REFPLATE", "_REFPLATE_TORSO") + ".png")
            cv2.imwrite(tpng, torso)
            entry["torso_reinforced"] = {
                "file": tpng, "sha256": sha256(tpng), "tone": 96, "line_px": 2,
                "parts": sorted({"chest", "abdomen", "waist", "pelvis"}),
                "boundaries": [{"name": p["name"], "side": p["side"],
                                "rect": [p["x0"], p["y0"], p["x1"], p["y1"]]}
                               for p in parts if p["name"] in {"chest", "abdomen", "waist", "pelvis"}],
                "canny": canny_preview(tpng, tpng.replace(".png", "_CANNY.png")),
            }

        if args.seal_horizontal:
            fmask = ((masks[name][spec["bbox_xyxy"][1]:spec["bbox_xyxy"][3] + 1,
                                 spec["bbox_xyxy"][0]:spec["bbox_xyxy"][2] + 1] > 0)
                     .astype(np.uint8) * 255)
            s = FIGURE_H / spec["figure_height_ref"]
            fh = int(round(fmask.shape[0] * s))
            fw = int(round(fmask.shape[1] * s))
            fsmall = cv2.resize(fmask, (fw, fh), interpolation=cv2.INTER_NEAREST)
            canvas_mask = np.zeros((SIZE[1], SIZE[0]), np.uint8)
            left = int(round(AXIS - fw / 2))
            canvas_mask[CROWN_Y:CROWN_Y + fh, left:left + fw] = fsmall
            sealed, info = seal_horizontal_gaps(plate, canvas_mask, args.seal_horizontal)
            spng = os.path.join(args.out, name.replace("_REFPLATE", "_REFPLATE_SEALED") + ".png")
            cv2.imwrite(spng, sealed)
            entry["sealed"] = {"file": spng, "sha256": sha256(spng), **info,
                               "canny": canny_preview(spng, spng.replace(".png", "_CANNY.png"))}

        card["plates"][name] = entry
        print(json.dumps({"plate": name, "sha256": entry["sha256"],
                          "canny_edges": entry["canny"]["edge_pixels"],
                          "reinforced_sha256": (entry.get("reinforced") or {}).get("sha256")},
                         ensure_ascii=False))

    with open(os.path.join(args.meta, "REFERENCE_PLATES.json"), "w", encoding="utf-8", newline="\n") as f:
        json.dump(card, f, ensure_ascii=False, indent=2)
        f.write("\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
