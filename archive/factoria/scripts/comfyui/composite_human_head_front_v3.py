"""Composite the inspected V3 head sample inside the registered FRONT carrier."""
from argparse import ArgumentParser
from hashlib import sha256
from pathlib import Path
import sys

from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont

sys.path.insert(0, str(Path(__file__).resolve().parent))
from prepare_human_head_front_v3 import (
    REPO, SPEC, STEM, carrier_geometry, checked_image, paint_carrier, raster_planes, read_json, write_json,
)


def comparison(images, path):
    tile_size = (264, 352)
    sheet = Image.new("RGB", (840, 398), (34, 34, 35))
    draw = ImageDraw.Draw(sheet)
    font = ImageFont.load_default(size=16)
    for index, (caption, frame) in enumerate(images):
        x = 12 + index * 276
        sheet.paste(frame.resize(tile_size, Image.Resampling.LANCZOS), (x, 8))
        draw.text((x, 370), caption, font=font, fill=(239, 239, 239))
    sheet.save(path, quality=93, subsampling=0)


def composite(source, sample_path, output):
    spec = read_json(SPEC)
    socket = read_json(REPO / spec["profile"])["head_socket"]
    size = tuple(spec["frame"]["size"])
    v2 = checked_image(source, spec["body_plate"], size)
    style = checked_image(source, spec["style_reference"], size)
    with Image.open(sample_path) as image:
        if image.size != size:
            raise ValueError("Head sample canvas differs from the carrier")
        sample = image.convert("RGB")
    shapes, metrics = carrier_geometry(spec, socket)
    labels, matte, _ = raster_planes(size, shapes)
    draft = paint_carrier(labels, shapes)
    # Keep the exact silhouette. Feather sampled shading into the technical
    # rim, where diffusion may have mixed the head and background.
    interior = matte.filter(ImageFilter.MinFilter(9)).filter(ImageFilter.GaussianBlur(1))
    carrier = Image.composite(Image.blend(draft, sample, 0.85), draft, interior)
    image = Image.composite(carrier, v2, matte)

    difference = ImageChops.difference(image, v2)
    outside = Image.composite(difference, Image.new("RGB", size), ImageChops.invert(matte))
    if outside.getbbox() is not None:
        raise ValueError("V3 differs from V2 outside the registered head and neck")
    output.mkdir(parents=True, exist_ok=True)
    target = output / f"{STEM}.png"
    sheet = output / f"{STEM}_COMPARISON.jpg"
    image.save(target)
    comparison((("HUMAN V1 STYLE", style), ("V2 REJECTED", v2), ("V3 HEAD LOCK", image)), sheet)

    def file_digest(path):
        return {"file": path.name, "sha256": sha256(path.read_bytes()).hexdigest()}

    write_json(output / "COMPOSITE.json", {
        "carrier": spec["id"],
        "sample": file_digest(sample_path),
        "result": file_digest(target),
        "comparison": file_digest(sheet),
        "geometry": metrics,
        "sample_interior_blend": {"opacity": 0.85, "erosion_radius_px": 4, "feather_radius_px": 1},
        "mask_bbox_xyxy_exclusive": list(matte.getbbox()),
        "outside_mask_rgb_pixels_changed": 0,
    })


def main():
    parser = ArgumentParser(description=__doc__)
    parser.add_argument("--source", required=True, type=Path, help="COMFY-STYLE-11B local review directory")
    parser.add_argument("--sample", required=True, type=Path, help="Raw PNG from the one ComfyUI request")
    parser.add_argument("--out", required=True, type=Path)
    args = parser.parse_args()
    composite(args.source, args.sample, args.out)


if __name__ == "__main__":
    main()
