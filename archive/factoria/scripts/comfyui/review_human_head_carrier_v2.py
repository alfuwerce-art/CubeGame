"""Build a deterministic FRONT comparison from the registered carrier and saved V3."""
from argparse import ArgumentParser
from hashlib import sha256
from pathlib import Path
import sys

from PIL import Image, ImageChops, ImageDraw

sys.path.insert(0, str(Path(__file__).resolve().parent))
from composite_human_head_front_v3 import comparison
from prepare_human_head_front_v3 import REPO, SPEC, carrier_geometry, checked_image, paint_carrier, raster_planes, read_json, write_json

SPEC_V2 = SPEC.with_name("HUMAN_MALE_HEAD_CARRIER_V2.json")
PREVIOUS = REPO / "measurements/comfy_style_11c/COMPOSITE.json"


def save_image(image, path):
    image.save(path)
    return {"file": path.name, "sha256": sha256(path.read_bytes()).hexdigest()}


def review(v2_dir, v3_dir, output):
    baseline = read_json(SPEC)
    spec = read_json(SPEC_V2)
    socket = read_json(REPO / spec["profile"])["head_socket"]
    if socket["id"] != spec["socket_id"]:
        raise ValueError("Carrier and profile use different head sockets")
    size = tuple(spec["frame"]["size"])
    body = checked_image(v2_dir, baseline["body_plate"], size)
    style = checked_image(v2_dir, baseline["style_reference"], size)
    previous = checked_image(v3_dir, read_json(PREVIOUS)["result"], size)
    shapes, metrics = carrier_geometry(spec, socket)
    labels, mask, geometry = raster_planes(size, shapes)

    carrier = paint_carrier(labels, shapes)
    # The original neck already has the lighting of the unchanged body plate.
    neck = labels.point([0, 255] + [0] * 254)
    carrier.paste(previous, mask=neck)
    # Where the narrower jaw exposes the old head, substitute nearby background
    # at the same height. Leave the registered neckline and shoulders intact.
    old_head = Image.new("L", size)
    ImageDraw.Draw(old_head).polygon(
        [(271, 181), (324, 161), (495, 165), (496, 210), (515, 210), (515, 289),
         (494, 290), (494, 322), (444, 337), (424, 337), (344, 337), (327, 328),
         (277, 329), (275, 294), (259, 289), (259, 212), (271, 211)],
        fill=255,
    )
    exposed = ImageChops.subtract(old_head, mask)
    background = body.copy()
    x0, y0, x1, y1 = old_head.getbbox()
    axis = spec["frame"]["axis_x"]
    # Read pixels from clear strips to the left and right on the same rows.
    background.paste(body.crop((65, y0, 65 + axis - x0, y1)), (x0, y0))
    background.paste(body.crop((563, y0, 563 + x1 - axis, y1)), (axis, y0))
    clean_plate = Image.composite(background, body, exposed)
    corrected = Image.composite(carrier, clean_plate, mask)

    _, old_mask, _ = raster_planes(size, carrier_geometry(baseline, socket)[0])
    edit = ImageChops.lighter(mask, old_mask)
    changed = ImageChops.difference(corrected, previous)
    if Image.composite(changed, Image.new("RGB", size), ImageChops.invert(edit)).getbbox():
        raise ValueError("Correction escaped the head and neck area")
    if Image.composite(ImageChops.difference(corrected, body), Image.new("RGB", size),
                       ImageChops.invert(ImageChops.lighter(mask, exposed))).getbbox():
        raise ValueError("Corrected frame changed the V2 plate outside the head, neck and restored background")

    output.mkdir(parents=True, exist_ok=True)
    result = save_image(corrected, output / "HUMAN_MALE_HEAD_GEOMETRY_REVIEW.png")
    carrier.putalpha(mask)
    plate = save_image(carrier, output / f"{spec['id']}.png")
    guide = save_image(geometry, output / f"{spec['id']}_GEOMETRY.png")
    sheet = output / "HUMAN_MALE_HEAD_GEOMETRY_COMPARISON.jpg"
    comparison((("HUMAN V1 STYLE", style), ("V3 / FIRST CARRIER", previous),
                ("REVISED HEAD CARRIER", corrected)), sheet)
    write_json(output / "REVIEW.json", {
        "status": "READY_FOR_OWNER_VISUAL_REVIEW",
        "comfyui_requests": 0,
        "geometry": metrics,
        "human_v1_observations": spec["measurements"],
        "result": result,
        "carrier": plate,
        "guide": guide,
        "comparison": {"file": sheet.name, "sha256": sha256(sheet.read_bytes()).hexdigest()},
        "background_restore_pixels": exposed.histogram()[255],
        "outside_head_and_neck_rgb_pixels_changed": 0,
    })


def main():
    parser = ArgumentParser(description=__doc__)
    parser.add_argument("--v2-dir", required=True, type=Path)
    parser.add_argument("--v3-dir", required=True, type=Path)
    parser.add_argument("--out", required=True, type=Path)
    args = parser.parse_args()
    review(args.v2_dir, args.v3_dir, args.out)


if __name__ == "__main__":
    main()
