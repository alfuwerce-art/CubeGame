"""Compose GEO-03 owner review plates from the hash-pinned Blender renders."""

import hashlib
import json
from pathlib import Path
import sys

from PIL import Image, ImageDraw
sys.path.insert(0, str(Path(__file__).resolve().parent))
import review_mannequin_geo_02 as plates

ROOT = plates.ROOT
RUN = ROOT / "outputs/prototypes/mannequin_geo_03/run1"
BASELINE = RUN.parent / "baseline_geo_02r"
DETAILS = ("HEAD_FRONT", "HEAD_EAR", "EAR_DETAIL", "HAND_OPEN", "HAND_FIST",
           "HAND_GRIP", "FEET_FRONT", "FEET_SIDE", "TWO_HAND_GRIP")
COMPARE = ("FRONT", "THREE_QUARTER", "IDLE", "HEAD_EAR", "HAND_OPEN", "FEET_SIDE")


def read(path):
    return json.loads(path.read_text(encoding="utf-8"))


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def validate_views(directory, blend, required):
    manifest = read(directory / "render_manifest.json")
    if sha(blend) != manifest["source_blend_sha256"]:
        raise ValueError(f"{directory} was rendered from a different BLEND")
    for name in required:
        path = directory / f"{name}.png"
        if sha(path) != manifest["views"][name]["sha256"]:
            raise ValueError(f"Image differs from Blender render manifest: {path}")
    return manifest["views"]


def compare_previous():
    w, h, gutter, padding = 252, 302, 8, 20
    group = 2*w + gutter
    canvas = Image.new("RGB", (3*group + 4*padding, 2*(h+66) + 104), plates.BG)
    draw = ImageDraw.Draw(canvas)
    draw.text((padding, 18), "GEO-02R → GEO-03", fill=plates.TEXT, font=plates.font(35))
    draw.text((padding, 63), "Одинаковые камеры, масштаб и свет", fill=plates.DIM, font=plates.font(19))
    for i, name in enumerate(COMPARE):
        x, y = padding + (i%3)*(group + padding), 101+(i//3)*(h+66)
        for j, revision in enumerate(("GEO-02R", "GEO-03")):
            directory = RUN / ("baseline_renders" if j == 0 else "renders")
            src = Image.open(directory / (name + ".png")).convert("RGB")
            u = x + j*(w+gutter)
            canvas.paste(src.resize((w, h), Image.Resampling.LANCZOS), (u, y+23))
            draw.text((u+3, y+h+27), revision, fill=plates.DIM, font=plates.font(17))
        draw.text((x+3, y), name.replace("_", " "), fill=plates.TEXT, font=plates.font(17))
    canvas.save(RUN / "compare/geo_02r_vs_geo_03.png", optimize=True)


def module_map(views):
    sockets = read(RUN / "module_sockets.json")["sockets"]
    reference = plates.TARGET
    if sha(reference) != plates.TARGET_SHA:
        raise ValueError("Reference B does not match the owner crop")
    crop = (62, 228, 706, 970)
    target = Image.open(reference).convert("RGB").crop(crop)
    height, top = 450, 103
    model_w = 375
    ref_w = round(height*target.width/target.height)
    panes = ((22, ref_w, "REFERENCE B"), (22 + ref_w + 15, model_w, "GEO-03 · FRONT"),
             (22 + ref_w + 30+model_w, model_w, "GEO-03 · BACK"))
    canvas = Image.new("RGB", (panes[-1][0]+model_w+22, 724), plates.BG)
    draw = ImageDraw.Draw(canvas)
    draw.text((22, 16), "Reference B · зоны модульной сборки", fill=plates.TEXT, font=plates.font(34))
    draw.text((23, 60), "Точки крепления и поверхности примерки на базовом манекене",
              fill=plates.DIM, font=plates.font(20))
    for (x, width, caption), image in zip(panes, (target, plates.render("FRONT", RUN), plates.render("BACK", RUN))):
        canvas.paste(image.resize((width, height), Image.Resampling.LANCZOS), (x, top))
        draw.text((x, top+height+9), caption, fill=plates.TEXT, font=plates.font(17))

    landmarks = [
        ("A", "Волосы", (404, 328), ("SCALP",)),
        ("B", "Лицо", (411, 519), ("FACE",)),
        ("C", "Уши", (576, 492), ("EAR_L",)),
        ("D", "Торс и ворот", (404, 654), ("CHEST",)),
        ("E", "Плечо и рукав", (214, 701), ("SHOULDER_R", "ARM_R")),
        ("F", "Пояс и таз", (397, 744), ("WAIST",)),
        ("G", "Голень и обувь", (472, 883), ("SHIN_L", "FOOT_L")),
        ("H", "Спина и ножны", (582, 686), ("BACK",)),
        ("I", "Основной и доп. хват", None, ("MAIN_HAND", "OFF_HAND")),
    ]

    def mark(x, y, letter, color):
        r = 10
        draw.ellipse((x-r, y-r, x+r, y+r), fill=plates.BG, outline=color, width=2)
        draw.text((x, y), letter, fill=color, font=plates.font(12), anchor="mm")

    for i, (letter, label, ref, ids) in enumerate(landmarks):
        color = plates.COLORS[i % len(plates.COLORS)]
        if ref:
            x = panes[0][0] + ref_w * (ref[0]-crop[0])/target.width
            y = top + height * (ref[1]-crop[1])/target.height
            mark(x, y, letter, color)
        for socket in ids:
            back = socket == "BACK"
            origin, width, _ = panes[2 if back else 1]
            view = views["BACK" if back else "FRONT"]
            m = sockets[socket]["rest_matrix"]
            u = .5 + (-1 if back else 1) * m[0][3] / view["ortho_width_m"]
            v = .5 - (m[2][3]-view["center_m"][2]) / view["ortho_height_m"]
            mark(origin + u*width, top + v*height, letter, color)
        x, y = 36 + (i % 3)*381, 603 + (i//3)*37
        mark(x, y, letter, color)
        draw.text((x+20, y), label, fill=plates.TEXT, font=plates.font(19), anchor="lm")
    canvas.save(RUN / "compare/reference_b_module_map.png", optimize=True)


def main():
    validation = read(RUN / "validation.json")
    blend, glb = RUN / "mannequin_geo_03.blend", RUN / "mannequin_geo_03.glb"
    if validation["sha256"] != {"blend": sha(blend), "glb": sha(glb)}:
        raise ValueError("Validation report refers to another build")
    views = validate_views(RUN/"renders", blend, (
        *plates.BODY_VIEWS, *DETAILS, "tpose_front_silhouette", "ATTACK_GRIP", "WALK_PASS", "ATTACK_WINDUP",
    ))
    validate_views(RUN/"baseline_renders", BASELINE/"mannequin_v3_rig.blend", COMPARE)
    ref = plates.compare.REF
    if sha(ref) != plates.APPROVED_SHA:
        raise ValueError("Reference A differs from the approved V3 sheet")
    plates.compare.main(run=RUN, task="MANNEQUIN-GEO-03")
    plates.overview(run=RUN)
    plates.overview(run=RUN, views=DETAILS, columns=3, filename="review_anatomy.png")
    compare_previous()
    module_map(views)


if __name__ == "__main__":
    main()
