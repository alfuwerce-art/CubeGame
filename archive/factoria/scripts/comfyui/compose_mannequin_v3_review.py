#!/usr/bin/env python3
"""Build the COMFY-STYLE-12 / MANNEQUIN V3 review composites (reference vs generated frames).

Panels are placed at the SAME figure scale so head size, shoulder width, pelvis width and limb
length can be compared directly by eye, as the task requires.

Usage:
  python_embeded\\python.exe scripts\\comfyui\\compose_mannequin_v3_review.py
"""
from __future__ import annotations

import json
import os

import cv2
import numpy as np

REFERENCE = "outputs/factory/mannequin_v3/REFERENCE_MANNEQUIN_V3_OWNER.png"
OUT = "outputs/factory/mannequin_v3"
PANEL_H = 760

# cropped reference figures (measured bboxes)
REF_CROPS = {
    "male": (240, 97, 782, 754),
    "female": (1023, 113, 1505, 754),
}
# generated frames share the guide framing (crown 174, sole 850 on 768x1024)
GEN_CROP = (60, 130, 710, 900)

GROUPS = {
    "review_male.png": [
        ("V3 REFERENCE (approved)", REFERENCE, "male_ref"),
        ("A  block plate only (flat)", f"{OUT}/run_male_1/MANNEQUIN_V3_MALE_00001_.png", "gen"),
        ("B  block plate + depth 0.55", f"{OUT}/run_male_diag_depth55/MANNEQUIN_V3_MALE_D55_00001_.png", "gen"),
        ("C  ref plate clean", f"{OUT}/run_male_diag_refclean/MANNEQUIN_V3_MALE_RC_00001_.png", "gen"),
        ("D  ref plate clean + depth 0.55", f"{OUT}/run_male_diag_clean_d55/MANNEQUIN_V3_MALE_CD55_00001_.png", "gen"),
        ("E  ref plate + torso boundaries (CHOSEN)", f"{OUT}/run_male_diag_torso/MANNEQUIN_V3_MALE_TR_00001_.png", "gen"),
    ],
    "review_female.png": [
        ("V3 REFERENCE (approved)", REFERENCE, "female_ref"),
        ("F  ref plate clean", f"{OUT}/run_female_1/MANNEQUIN_V3_FEMALE_00001_.png", "gen"),
        ("G  ref plate clean + depth 0.55", f"{OUT}/run_female_diag_clean_d55/MANNEQUIN_V3_FEMALE_CD55_00001_.png", "gen"),
        ("H  ref plate sealed + depth 0.55", f"{OUT}/run_female_diag_sealed_d55/MANNEQUIN_V3_FEMALE_SD55_00001_.png", "gen"),
        ("I  ref plate + torso boundaries (CHOSEN)", f"{OUT}/run_female_diag_torso/MANNEQUIN_V3_FEMALE_TR_00001_.png", "gen"),
    ],
    "review_guides.png": [
        ("MALE GUIDE (block plate)", f"{OUT}/HUMAN_MALE_MANNEQUIN_GUIDE.png", "full"),
        ("MALE REFPLATE", f"{OUT}/HUMAN_MALE_MANNEQUIN_REFPLATE.png", "full"),
        ("FEMALE GUIDE (block plate)", f"{OUT}/HUMAN_FEMALE_MANNEQUIN_GUIDE.png", "full"),
        ("FEMALE REFPLATE", f"{OUT}/HUMAN_FEMALE_MANNEQUIN_REFPLATE.png", "full"),
    ],
    "review_flat_diagnostics.png": [
        ("block plate only (flat)", f"{OUT}/run_male_1/MANNEQUIN_V3_MALE_00001_.png", "gen"),
        ("block plate + depth", f"{OUT}/run_male_diag_depth55/MANNEQUIN_V3_MALE_D55_00001_.png", "gen"),
        ("ref plate (raw crop)", f"{OUT}/run_male_diag_refplate/MANNEQUIN_V3_MALE_REFP_00001_.png", "gen"),
        ("ref plate clean", f"{OUT}/run_male_diag_refclean/MANNEQUIN_V3_MALE_RC_00001_.png", "gen"),
    ],
}


def load_panel(path: str, mode: str) -> np.ndarray | None:
    if not os.path.isfile(path):
        return None
    img = cv2.imread(path, cv2.IMREAD_COLOR)
    if img is None:
        return None
    if mode == "gen":
        x0, y0, x1, y1 = GEN_CROP
        img = img[y0:y1, x0:x1]
    elif mode in ("male_ref", "female_ref"):
        x0, y0, x1, y1 = REF_CROPS["male" if mode == "male_ref" else "female"]
        img = img[y0:y1, x0:x1]
    h, w = img.shape[:2]
    scale = PANEL_H / h
    return cv2.resize(img, (max(1, int(round(w * scale))), PANEL_H), interpolation=cv2.INTER_AREA)


def label(img: np.ndarray, text: str, color=(40, 40, 40)) -> np.ndarray:
    pad = 6
    bar = np.full((40, img.shape[1], 3), 245, np.uint8)
    out = np.vstack([bar, img])
    cv2.putText(out, text, (pad, 27), cv2.FONT_HERSHEY_SIMPLEX, 0.55, color, 1, cv2.LINE_AA)
    return out


def main() -> int:
    card = {"task": "COMFY-STYLE-12", "composites": {}}
    for name, panels in GROUPS.items():
        imgs = []
        for title, path, mode in panels:
            p = load_panel(path, mode)
            if p is None:
                print(f"MISSING {path}")
                continue
            imgs.append(label(p, title))
        if not imgs:
            continue
        h = max(i.shape[0] for i in imgs)
        padded = []
        for i in imgs:
            if i.shape[0] < h:
                i = np.vstack([i, np.full((h - i.shape[0], i.shape[1], 3), 245, np.uint8)])
            padded.append(i)
        sep = np.full((h, 3, 3), 245, np.uint8)
        sheet = padded[0]
        for i in padded[1:]:
            sheet = np.hstack([sheet, sep, i])
        dst = os.path.join(OUT, name)
        cv2.imwrite(dst, sheet)
        card["composites"][name] = {"file": dst, "size_xy": [int(sheet.shape[1]), int(sheet.shape[0])],
                                    "panels": [t for t, _, _ in panels]}
        print(json.dumps({"composite": dst, "size": card["composites"][name]["size_xy"]}))
    with open(os.path.join("measurements", "comfy_style_12", "mannequin_v3",
                           "REVIEW_COMPOSITES.json"), "w", encoding="utf-8", newline="\n") as f:
        json.dump(card, f, ensure_ascii=False, indent=2)
        f.write("\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
