from pathlib import Path
from hashlib import file_digest
import argparse
import json
import sys

import cv2
import numpy as np
from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parent))
from prepare_character_reference import NEUTRAL, bbox_of, segment

ROOT = Path(__file__).resolve().parents[2]

def digest(path):
    with path.open("rb") as stream:
        return file_digest(stream, "sha256").hexdigest()

def prepare(source, manifest, output):
    if digest(source) != manifest["source_sha256"]:
        raise ValueError(f"Source SHA-256 mismatch for {source.name}")
    x0, y0, x1, y1 = manifest["crop_xyxy"]
    rx0, ry0, rx1, ry1 = manifest["source_rect_xyxy"]
    with Image.open(source) as image:
        crop = image.crop((x0, y0, x1, y1)).convert("RGBA")
    cv2.setRNGSeed(20261114)
    mask = segment(crop, (rx0-x0, ry0-y0, rx1-x0, ry1-y0))
    correction = manifest["mask_correction"]
    if correction:
        if correction["method"] == "replace":
            mask.fill(0)
        polygon = np.array([(x-x0, y-y0) for x,y in correction["polygon_source_px"]], dtype=np.int32)
        cv2.fillPoly(mask, [polygon], 255)
    bx0, by0, bx1, by1 = bbox_of(mask)
    crop.putalpha(Image.fromarray(mask))
    tight = crop.crop((max(0, bx0-6), max(0, by0-6), min(crop.width, bx1+6), min(crop.height, by1+6)))
    scale = min(736 / tight.width, 872 / tight.height)
    scaled = tight.resize((round(tight.width*scale), round(tight.height*scale)), Image.Resampling.NEAREST)
    ref = Image.new("RGB", (768, 1024), NEUTRAL)
    ref.paste(scaled, ((768 - scaled.width)//2, 1024 - 52 - scaled.height), scaled)
    ref.save(output)
    if digest(output) != manifest["input_sha256"]:
        raise ValueError(f"Prepared reference SHA-256 mismatch for {output.name}")

def main():
    parser = argparse.ArgumentParser(description="Rebuild local COMFY-STYLE-08 inputs from verified owner screenshots.")
    parser.add_argument("--sources", required=True, type=Path)
    parser.add_argument("--out", required=True, type=Path)
    args = parser.parse_args()
    args.out.mkdir(parents=True, exist_ok=True)
    for path in sorted((ROOT / "races").glob("*/reference_manifest.json")):
        manifest = json.loads(path.read_text(encoding="utf-8"))
        prepare(args.sources / manifest["source_filename"], manifest, args.out / manifest["input_filename"])
        print(f"{manifest['reference_id']}: input hash verified")

if __name__ == "__main__":
    main()
