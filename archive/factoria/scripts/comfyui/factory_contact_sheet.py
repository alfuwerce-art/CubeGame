#!/usr/bin/env python3
"""Build a contact sheet from the Factory character batch (COMFY-STYLE-05).

Reads the batch report, labels every image with the facts that matter for review
(mode, LoRA strength, resolution, seed, seconds, peak VRAM) and writes one PNG grid.
It does not judge the art - it only makes the comparison readable in one picture.

Usage:
  python factory_contact_sheet.py --report measurements/factory_batch_report.json \
      --out outputs/factory/characters/contact_sheet.png [--cols 4] [--thumb-width 460]
"""
from __future__ import annotations

import argparse
import json
import os
import sys

from PIL import Image, ImageDraw, ImageFont

BG = (24, 24, 28)
FG = (235, 235, 235)
ACCENT = {"BASE": (140, 200, 255), "FICTIVERSE": (150, 235, 170), "RAL": (255, 190, 140)}
WARN = (255, 120, 120)


def load_font(size: int):
    for name in ("segoeui.ttf", "arial.ttf", "DejaVuSans.ttf"):
        try:
            return ImageFont.truetype(name, size)
        except Exception:
            continue
    return ImageFont.load_default()


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--report", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--cols", type=int, default=4)
    ap.add_argument("--thumb-width", type=int, default=460)
    ap.add_argument("--title", default="CubeGame Factory - character generation (COMFY-STYLE-05)")
    args = ap.parse_args()

    with open(args.report, "r", encoding="utf-8") as f:
        report = json.load(f)
    runs = [r for r in report["runs"] if r.get("image") and os.path.exists(r["image"])]
    if not runs:
        print("no images to place")
        return 2

    font = load_font(18)
    font_small = load_font(15)
    font_title = load_font(26)

    cols = max(1, args.cols)
    rows = (len(runs) + cols - 1) // cols

    thumb_w = args.thumb_width
    first = Image.open(runs[0]["image"])
    thumb_h = int(thumb_w * first.height / first.width)
    pad, label_h, header = 16, 76, 56
    cell_w, cell_h = thumb_w + pad * 2, thumb_h + label_h + pad * 2
    width = cols * cell_w + pad
    height = header + rows * cell_h + pad

    sheet = Image.new("RGB", (width, height), BG)
    draw = ImageDraw.Draw(sheet)
    draw.text((pad, 14), args.title, fill=FG, font=font_title)

    for idx, run in enumerate(runs):
        r, c = divmod(idx, cols)
        x0 = pad + c * cell_w
        y0 = header + r * cell_h
        img = Image.open(run["image"]).convert("RGB").resize((thumb_w, thumb_h), Image.LANCZOS)
        sheet.paste(img, (x0, y0))
        mode = run["mode"]
        color = ACCENT.get(mode, FG)
        strength = run.get("lora_strength")
        line1 = f"{run['run_id']}"
        line2 = f"{mode}" + (f"  LoRA {strength}" if strength is not None else "  (no LoRA)")
        line3 = (f"{run['resolution']['width']}x{run['resolution']['height']}  seed {run['seed']}  "
                 f"{run['seconds']}s")
        line4 = f"peak VRAM {run.get('peak_gpu_used_mb_whole_card')} MB whole card"
        ty = y0 + thumb_h + 6
        draw.text((x0, ty), line1, fill=FG, font=font_small)
        draw.text((x0, ty + 18), line2, fill=color, font=font)
        draw.text((x0, ty + 40), line3, fill=FG, font=font_small)
        prod = run.get("production_use")
        l4color = WARN if prod == "NOT_ALLOWED_EXPERIMENT_ONLY" else FG
        suffix = "  [EXPERIMENT ONLY]" if prod == "NOT_ALLOWED_EXPERIMENT_ONLY" else ""
        draw.text((x0, ty + 60), line4 + suffix, fill=l4color, font=font_small)

    skipped = [r for r in report["runs"] if r.get("status") == "SKIPPED"]
    if skipped:
        note = "skipped: " + ", ".join(f"{r['id']} ({r.get('reason','')})" for r in skipped)
        draw.text((pad, height - 22), note[:220], fill=(200, 200, 120), font=font_small)

    os.makedirs(os.path.dirname(os.path.abspath(args.out)), exist_ok=True)
    sheet.save(args.out)
    print(f"contact sheet: {args.out} ({sheet.width}x{sheet.height}, {len(runs)} images)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
