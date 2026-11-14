"""Trace the visible Human V1 landmarks and preview a face-free head on V2."""
from argparse import ArgumentParser
from hashlib import sha256
from pathlib import Path
import json

from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont

SIZE = (768, 1024)
OFFSET = (-12, -274)
CROP = (175, 365, 615, 633)


def read_image(path, expected):
    if sha256(path.read_bytes()).hexdigest() != expected:
        raise ValueError(f"Unexpected file {path}")
    with Image.open(path) as image:
        if image.size != SIZE:
            raise ValueError(f"Unexpected canvas {path}")
        return image.convert("RGB")


# Pixel coordinates on archived Human V1. Hair obscures the crown and temple
# boundaries. The unoccluded ear tips and the lower cheeks and chin are traced.
PLANES = {
    "neck": [(355, 614), (435, 614), (434, 633), (355, 633)],
    "ear_left": [(197, 451), (203, 445), (242, 445), (252, 451), (252, 559),
                 (247, 562), (201, 562), (198, 557)],
    "ear_right": [(540, 450), (548, 444), (585, 444), (590, 451), (590, 557),
                  (586, 562), (539, 562)],
    "ear_root_left": [(240, 472), (264, 472), (264, 547), (247, 547), (247, 531), (240, 531)],
    "ear_root_right": [(528, 472), (550, 472), (550, 531), (545, 531), (545, 547), (528, 547)],
    "temple_left": [(256, 406), (282, 393), (287, 407), (289, 556), (317, 582),
                    (317, 615), (307, 615), (307, 594), (283, 594), (283, 585),
                    (268, 585), (268, 566), (256, 566)],
    "temple_right": [(512, 393), (536, 406), (536, 562), (526, 562), (526, 584),
                     (510, 584), (510, 593), (485, 593), (485, 615), (476, 615),
                     (476, 583), (506, 557), (506, 406)],
    "top": [(282, 393), (512, 393), (512, 411), (282, 411)],
    "forehead": [(282, 410), (512, 410), (512, 551), (282, 551)],
    "cheek_left": [(282, 549), (327, 549), (327, 588), (286, 588), (282, 582)],
    "cheek_right": [(467, 549), (512, 549), (512, 582), (507, 588), (467, 588)],
    "chin": [(321, 549), (472, 549), (472, 586), (477, 588), (477, 614),
             (475, 616), (316, 616), (314, 613), (314, 589), (321, 585)],
}
LIGHT = {
    "neck": ((67, 28, 24), (126, 57, 38)),
    "ear_left": ((157, 79, 56), (123, 49, 34)),
    "ear_right": ((202, 124, 79), (248, 193, 145)),
    "ear_root_left": ((96, 40, 26), (135, 64, 44)),
    "ear_root_right": ((153, 72, 44), (215, 132, 77)),
    "temple_left": ((119, 59, 42), (197, 123, 90)),
    "temple_right": ((189, 115, 78), (225, 158, 112)),
    "top": ((225, 168, 121), (255, 217, 163)),
    "forehead": ((231, 178, 135), (245, 202, 157)),
    "cheek_left": ((238, 180, 135), (246, 191, 144)),
    "cheek_right": ((236, 181, 137), (250, 196, 150)),
    "chin": ((235, 187, 145), (239, 190, 144)),
}


def polygon_mask(points):
    mask = Image.new("L", SIZE)
    ImageDraw.Draw(mask).polygon(points, fill=255)
    return mask


def shade_region(name, points):
    mask = polygon_mask(points)
    left, top, right, bottom = mask.getbbox()
    width = right - left
    shade = Image.new("RGB", (width, 1))
    low, high = LIGHT[name]
    shade.putdata([tuple(round(a + (b - a) * i / (width - 1)) for a, b in zip(low, high))
                   for i in range(width)])
    return mask, shade.resize((width, bottom - top)), (left, top)


def reconstruct(style):
    carrier = Image.new("RGB", SIZE)
    alpha = Image.new("L", SIZE)
    observed = Image.new("L", SIZE)
    for name, points in PLANES.items():
        mask, tile, origin = shade_region(name, points)
        carrier.paste(tile, origin, mask.crop(mask.getbbox()))
        alpha = ImageChops.lighter(alpha, mask)
    # Transfer only the exposed, face-free skin. The eye and mouth footprints
    # stay under the manually reconstructed surface, along with all hair.
    for area in [
        [(203, 450), (215, 450), (215, 553), (205, 557)],
        [(572, 450), (585, 450), (585, 555), (571, 554)],
        [(290, 552), (343, 553), (343, 585), (290, 585)],
        [(445, 553), (502, 552), (504, 585), (445, 586)],
        [(322, 591), (472, 591), (472, 611), (322, 611)],
    ]:
        native = ImageChops.multiply(polygon_mask(area), alpha)
        # Feather only RGB inside the geometric silhouette.
        feathered = ImageChops.multiply(native.filter(ImageFilter.GaussianBlur(2)), native)
        carrier.paste(style, mask=feathered)
        observed = ImageChops.lighter(observed, native)
    return carrier, alpha, observed


def translate(image):
    target = Image.new(image.mode, SIZE)
    target.paste(image, OFFSET)
    return target


def render(source, output):
    style = read_image(source / "HUMAN_V1_STYLE_REFERENCE.png",
                       "b9f3d6e41e68e8239ab61f70fc5ddf81f95e0e85124903841bdd2e1195977e77")
    body = read_image(source / "HUMAN_MALE_BASE_FRONT_V2.png",
                      "dea26a722921c0b30e9c57f0abb090d883dd9abb77e782f94f5595d87d48cfcd")
    head, alpha, observed = reconstruct(style)
    registered = translate(head)
    matte = translate(alpha)
    old_head = polygon_mask([(271, 181), (324, 161), (495, 165), (496, 210), (515, 210), (515, 289),
                             (494, 290), (494, 322), (444, 337), (424, 337), (344, 337),
                             (327, 328), (277, 329), (275, 294), (259, 289), (259, 212), (271, 211)])
    exposed = ImageChops.subtract(old_head, matte)
    background = body.copy()
    background.paste(body.crop((65, 161, 190, 338)), (259, 161))
    background.paste(body.crop((563, 161, 695, 338)), (384, 161))
    result = Image.composite(registered, Image.composite(background, body, exposed), matte)
    edit = ImageChops.lighter(matte, exposed)
    if Image.composite(ImageChops.difference(result, body), Image.new("RGB", SIZE),
                       ImageChops.invert(edit)).getbbox():
        raise ValueError("Unexpected body change")

    output.mkdir(parents=True, exist_ok=True)
    result.save(output / "HUMAN_MALE_HEAD_V1_TRACED_REVIEW.png")
    view = Image.new("RGB", SIZE, (110, 110, 111))
    view.paste(head, mask=alpha)
    sheet = Image.new("RGB", (1120, 428), (34, 34, 35))
    draw = ImageDraw.Draw(sheet)
    font = ImageFont.load_default(size=18)
    for i, (picture, box, label) in enumerate((
        (style, CROP, "HUMAN V1 / SAME SCALE"),
        (view, CROP, "V1 TRACE / FACE-FREE"),
        (result, (0, 75, 768, 930), "PLACEMENT / T-POSE"),
    )):
        x = 17 + 370 * i
        tile = picture.crop(box)
        tile.thumbnail((347, 355), Image.Resampling.LANCZOS)
        sheet.paste(tile, (x + (347 - tile.width) // 2, 18 + (355 - tile.height) // 2))
        draw.text((x, 387), label, fill=(245, 244, 243), font=font)
    sheet.save(output / "HUMAN_HEAD_V1_TRACE_COMPARISON.jpg", quality=94, subsampling=0)
    head.putalpha(alpha)
    head.crop(CROP).save(output / "HUMAN_HEAD_V1_TRACE.png")
    observed.crop(CROP).save(output / "HUMAN_HEAD_V1_VISIBLE_SKIN.png")
    report = {
        "status": "READY_FOR_OWNER_VISUAL_REVIEW",
        "method": "front trace with reconstructed occluded geometry",
        "prior_candidate": "owner reported continued mismatch",
        "comfyui_requests": 0,
        "placement_offset_xy": OFFSET,
        "head_nape_join_frame_y": 359,
        "comparison_reference_crop_xyxy": CROP,
        "source_coordinate_polygons": PLANES,
        "unobserved": ["crown", "temple edges under hair", "ear roots under hair", "depth"],
        "restored_background_pixels": exposed.histogram()[255],
        "rgb_differences_outside_head_region": 0,
        "files": {
            path.name: sha256(path.read_bytes()).hexdigest()
            for path in sorted(output.glob("HUMAN*.png")) + sorted(output.glob("HUMAN*.jpg"))
        },
    }
    (output / "TRACE.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    parser = ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, required=True, help="Local V2 review directory")
    parser.add_argument("--out", type=Path, required=True)
    args = parser.parse_args()
    render(args.source, args.out)
