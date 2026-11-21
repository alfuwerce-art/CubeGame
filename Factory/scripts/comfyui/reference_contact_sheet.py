#!/usr/bin/env python3
"""COMFY-STYLE-07 — contact sheet for the reference-guided comparison.

One grid with every run of a reference batch report, labelled with the parameters
that matter for comparing modes (IPAdapter weight / weight_type, ControlNet strength,
img2img denoise), seed, time, peak VRAM and the closeness heuristics.

Usage:
  python reference_contact_sheet.py --report measurements/reference_batch_report_v1.json \
      --out outputs/factory/reference_v1/contact_sheet.png [--cols 5] [--thumb-width 320]
"""
from __future__ import annotations

import argparse
import json
import os
import sys

from PIL import Image, ImageDraw, ImageFont

BG = (22, 22, 26)
FG = (235, 235, 235)
ACCENT = {"IPADAPTER": (140, 200, 255), "CONTROLNET": (150, 235, 170),
          "IMG2IMG": (255, 200, 140), "COMBO": (230, 170, 255)}


def load_font(size: int):
    for name in ("segoeui.ttf", "arial.ttf", "DejaVuSans.ttf"):
        try:
            return ImageFont.truetype(name, size)
        except Exception:
            continue
    return ImageFont.load_default()


def mode_label(run: dict) -> tuple[str, tuple]:
    if run.get("ipadapter_weight") is not None and run.get("controlnet_strength") is not None:
        return f"IPA {run['ipadapter_weight']} + CN {run['controlnet_strength']}", ACCENT["COMBO"]
    if run.get("ipadapter_weight") is not None:
        wt = run.get("ipadapter_weight_type") or "linear"
        return f"IPAdapter {run['ipadapter_weight']} / {wt}", ACCENT["IPADAPTER"]
    if run.get("controlnet_strength") is not None:
        return f"ControlNet {run['controlnet_strength']}", ACCENT["CONTROLNET"]
    if run.get("denoise") is not None:
        return f"img2img denoise {run['denoise']}", ACCENT["IMG2IMG"]
    return "base", FG


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--report", required=True)
    ap.add_argument("--out", required=True)
    ap.add_argument("--cols", type=int, default=5)
    ap.add_argument("--thumb-width", type=int, default=320)
    ap.add_argument("--title", default="CubeGame Factory - reference-guided characters (COMFY-STYLE-07)")
    args = ap.parse_args()

    with open(args.report, "r", encoding="utf-8") as f:
        report = json.load(f)
    runs = [r for r in report["runs"] if r.get("image") and os.path.exists(r["image"])]
    if not runs:
        print("no images in report")
        return 2

    font, small, title_font = load_font(17), load_font(14), load_font(26)
    cols = max(1, args.cols)
    rows = (len(runs) + cols - 1) // cols
    thumb_w = args.thumb_width
    first = Image.open(runs[0]["image"])
    thumb_h = int(thumb_w * first.height / first.width)
    pad, label_h, header = 14, 92, 54
    cell_w, cell_h = thumb_w + pad * 2, thumb_h + label_h + pad * 2
    sheet = Image.new("RGB", (cols * cell_w + pad, header + rows * cell_h + pad), BG)
    draw = ImageDraw.Draw(sheet)
    draw.text((pad, 12), args.title, fill=FG, font=title_font)

    for idx, run in enumerate(runs):
        r, c = divmod(idx, cols)
        x0, y0 = pad + c * cell_w, header + r * cell_h
        img = Image.open(run["image"]).convert("RGB").resize((thumb_w, thumb_h), Image.LANCZOS)
        sheet.paste(img, (x0, y0))
        label, color = mode_label(run)
        ty = y0 + thumb_h + 6
        draw.text((x0, ty), run["run_id"], fill=FG, font=small)
        draw.text((x0, ty + 18), label, fill=color, font=font)
        lora = run.get("lora_strength")
        lora_txt = ""
        if lora is not None:
            lora_txt = f"  LoRA {lora}"
        draw.text((x0, ty + 40),
                  f"{run['resolution']['width']}x{run['resolution']['height']} seed {run['seed']}{lora_txt} "
                  f"{run['steps']}st CFG {run['cfg']}", fill=FG, font=small)
        draw.text((x0, ty + 56), f"{run['seconds']}s  VRAM {run.get('peak_gpu_used_mb_whole_card')} MB",
                  fill=FG, font=small)
        cl = run.get("closeness") or {}
        draw.text((x0, ty + 72), f"L1 {cl.get('l1_mean_0_255')}  IoU {cl.get('silhouette_iou')}",
                  fill=(200, 200, 120), font=small)

    os.makedirs(os.path.dirname(os.path.abspath(args.out)), exist_ok=True)
    sheet.save(args.out)
    print(f"contact sheet: {args.out} ({sheet.width}x{sheet.height}, {len(runs)} runs)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
