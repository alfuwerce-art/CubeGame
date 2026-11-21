"""Produce the local A/B review plates from conventionally rendered mesh views."""

import argparse
import hashlib
import json
from pathlib import Path
import sys

from PIL import Image, ImageDraw, ImageFont

sys.path.insert(0, str(Path(__file__).resolve().parent))
import compare_mannequin_v3_render as compare

ROOT = compare.ROOT
RUN = ROOT / "outputs/prototypes/mannequin_geo_02/run1"
TARGET = ROOT / "references/characters_v1/human_warrior_reference_768x1024.png"
TARGET_SHA = "569ef0b5a79b24fe9fbb886508a06d50900c25f91738699c0b3bfdb4843d1a08"
APPROVED_SHA = "b95e4666a3b3caae543e3ba4663cce8d7f9ad3d026f381cca2231fee04d841c5"
CATALOG = [
    ("01", "SCALP", (402, 325), ["SCALP"]),
    ("02", "FACE", (416, 522), ["FACE"]),
    ("03", "EAR", (575, 488), ["EAR_L"]),
    ("04", "CHEST", (401, 669), ["CHEST"]),
    ("05", "SHOULDERS / ARMS", (213, 690), ["SHOULDER_R", "ARM_R"]),
    ("06", "WAIST", (371, 742), ["WAIST"]),
    ("07", "LEGS / FEET", (447, 919), ["SHIN_L", "FOOT_L"]),
    ("08", "BACK", (610, 674), ["BACK"]),
    ("09", "MAIN_HAND / OFF_HAND", None, ["MAIN_HAND", "OFF_HAND"]),
    ("10", "BEARD", None, ["BEARD"]),
]
COLORS = ("#a6d7fb", "#ffdb91", "#cbb5ff", "#a4e5b6", "#ffd4ba")
BG = "#171a20"
TEXT = "#eff2f5"
DIM = "#bcc4cf"


def font(size):
    try:
        return ImageFont.truetype("arial.ttf", size)
    except OSError:
        return ImageFont.load_default()


def render(name, run=RUN):
    return Image.open(run / "renders" / (name + ".png")).convert("RGB")


BODY_VIEWS = ("FRONT", "BACK", "LEFT", "RIGHT", "THREE_QUARTER", "IDLE", "WALK", "ATTACK")


def overview(run=RUN, views=BODY_VIEWS, columns=4, filename="review_overview.png"):
    width, height, padding, label = 300, 360, 16, 30
    rows = (len(views) + columns - 1) // columns
    canvas = Image.new("RGB", (width * columns + padding * (columns + 1),
                              (height + label) * rows + padding * (rows + 1)), BG)
    draw = ImageDraw.Draw(canvas)
    for i, view in enumerate(views):
        x = padding + i % columns * (width + padding)
        y = padding + i // columns * (height + label + padding)
        canvas.paste(render(view, run).resize((width, height), Image.Resampling.LANCZOS), (x, y))
        draw.text((x + 4, y + height + 5), view.replace("_", " "), fill=TEXT, font=font(18))
    canvas.save(run / filename, optimize=True)


def module_plate(run=RUN, revision="GEO-02"):
    if hashlib.sha256(TARGET.read_bytes()).hexdigest() != TARGET_SHA:
        raise ValueError("Reference B differs from the documented owner crop")
    sockets = json.loads((run / "module_sockets.json").read_text(encoding="utf-8"))["sockets"]
    target_box = (44, 222, 720, 988)
    target = Image.open(TARGET).convert("RGB").crop(target_box)
    panes = [(24, 106), (469, 106), (864, 106)]
    sizes = [(421, 477), (371, 477), (371, 477)]
    canvas = Image.new("RGB", (1259, 738), BG)
    draw = ImageDraw.Draw(canvas)
    draw.text((24, 19), "Reference B | Module placement", fill=TEXT, font=font(34))
    draw.text((24, 63), "Style reference and attachment locations on the unmodified carrier render",
              fill=DIM, font=font(18))
    for image, origin, size, title in zip(
        (target, render("FRONT", run), render("BACK", run)), panes, sizes,
        ("REFERENCE B · documented owner crop", f"{revision} · FRONT", f"{revision} · BACK")
    ):
        canvas.paste(image.resize(size, Image.Resampling.LANCZOS), origin)
        draw.text((origin[0], 593), title, fill=TEXT, font=font(17))

    def marker(x, y, label, color):
        radius = 13
        draw.ellipse((x - radius, y - radius, x + radius, y + radius), fill=BG, outline=color, width=2)
        draw.text((x, y + 1), label, fill=color, font=font(12), anchor="mm")

    for i, (label, title, reference_xy, anchors) in enumerate(CATALOG):
        color = COLORS[i % len(COLORS)]
        if reference_xy:
            x = panes[0][0] + (reference_xy[0] - target_box[0]) * sizes[0][0] / target.width
            y = panes[0][1] + (reference_xy[1] - target_box[1]) * sizes[0][1] / target.height
            marker(x, y, label, color)
        for anchor in anchors:
            back = anchor == "BACK"
            px, py = panes[2 if back else 1]
            width, height = sizes[2 if back else 1]
            m = sockets[anchor]["rest_matrix"]
            # Blender orthographic cameras from the validation run, ground target z=.92 m.
            x_m, z_m = m[0][3], m[2][3]
            u = 0.5 + (-x_m if back else x_m) / (2.2 * 1000 / 1200)
            v = 0.5 - (z_m - .92) / 2.2
            marker(px + width * u, py + height * v, label, color)
        x = 24 + i % 5 * 246
        y = 647 + i // 5 * 44
        marker(x + 13, y, label, color)
        draw.text((x + 34, y), title, fill=TEXT, font=font(16), anchor="lm")
    canvas.save(run / "compare" / "reference_b_module_map.png", optimize=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--reference-a", type=Path, default=compare.REF)
    args = parser.parse_args()
    reference_a = args.reference_a.resolve()
    if hashlib.sha256(reference_a.read_bytes()).hexdigest() != APPROVED_SHA:
        raise ValueError("Reference A differs from the approved V3 sheet")
    compare.main(run=RUN, task="MANNEQUIN-GEO-02", reference=reference_a)
    overview()
    module_plate()
