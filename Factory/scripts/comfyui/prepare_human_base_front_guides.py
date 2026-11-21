"""Draw the two original, registered technical guide plates used by COMFY-STYLE-11."""
from argparse import ArgumentParser
from pathlib import Path
import json
from PIL import Image, ImageDraw

SIZE = (768, 1024)
BACKGROUND = (158, 158, 158)
AXIS_X = 384
BODY_TOP = 174
SOLE_Y = 850
SHOULDER_Y = 417
# Shared crown/sole, neck, wrist and foot positions. All polygon coordinates below
# describe the left projection half. Interior shades indicate broad planes.
PROFILES = {
    "MALE": {"shoulder": 279, "waist": 305, "hip": 287},
    "FEMALE": {"shoulder": 291, "waist": 312, "hip": 283},
}


def polygon(draw, points, gray, reflect=False):
    if reflect:
        points = [(2 * AXIS_X - x, y) for x, y in points]
    draw.polygon(points, fill=(gray, gray, gray))


def render(profile):
    im = Image.new("RGB", SIZE, BACKGROUND)
    draw = ImageDraw.Draw(im)
    shoulder = profile["shoulder"]
    hip = profile["hip"]
    waist = profile["waist"]
    for reflect in (False, True):
        # Feet share the same material as the leg. There are no toes, seams or shoes.
        polygon(draw, [(hip, 584), (371, 584), (364, 726), (357, 814),
                       (357, SOLE_Y), (261, SOLE_Y), (261, 823), (279, 810), (274, 719)], 98, reflect)
        polygon(draw, [(hip+16, 607), (361, 620), (355, 726), (349, 814),
                       (349, 833), (274, 833), (274, 823), (289, 813), (287, 717)], 109, reflect)
        # The shoulder, forearm, hand and wrist have a common horizontal centre.
        polygon(draw, [(shoulder+14, 374), (shoulder-35, 374), (shoulder-35, 383), (164, 383),
                       (164, 391), (145, 391), (145, 379), (92, 379), (92, 454),
                       (145, 454), (145, 442), (161, 442), (161, 451), (shoulder+14, 451)], 98, reflect)
        polygon(draw, [(shoulder+12, 381), (shoulder-39, 381), (shoulder-39, 390), (169, 390),
                       (169, 398), (152, 398), (152, 386), (99, 386), (99, 443),
                       (143, 443), (143, 433), (164, 433), (164, 440), (shoulder+12, 440)], 114, reflect)
        # The torso and pelvis form one stepped volume without waistbands or necklines.
        polygon(draw, [(AXIS_X, 350), (shoulder+23, 350), (shoulder+23, 365), (shoulder, 365),
                       (shoulder, 445), (waist, 513), (waist, 535), (hip, 559), (hip, 618),
                       (341, 638), (AXIS_X, 638)], 98, reflect)
        polygon(draw, [(AXIS_X, 357), (shoulder+30, 357), (shoulder+30, 372), (shoulder+8, 372),
                       (shoulder+8, 441), (waist+11, 511), (waist+11, 539), (hip+10, 564),
                       (hip+10, 606), (343, 622), (AXIS_X, 622)], 118, reflect)
    draw.rectangle((347, 303, 421, 361), fill=(107,107,107))
    # 14x8 occupied width/height of the Alpha socket at an 18 px design scale.
    polygon(draw, [(277,BODY_TOP),(491,BODY_TOP),(491,192),(510,192),(510,292),
                   (492,292),(492,318),(276,318),(276,292),(258,292),(258,192),(277,192)], 98)
    polygon(draw, [(282,180),(486,180),(486,200),(502,200),(502,285),
                   (485,285),(485,307),(283,307),(283,285),(266,285),(266,200),(282,200)], 117)
    return im


def main():
    parser = ArgumentParser(description=__doc__)
    parser.add_argument("--out", required=True, type=Path)
    args = parser.parse_args()
    args.out.mkdir(parents=True, exist_ok=True)
    for gender, profile in PROFILES.items():
        stem = f"HUMAN_{gender}_BASE_FRONT"
        plate = stem + "_GUIDE_V1.png"
        render(profile).save(args.out / plate)
        pack = {
            "asset_type": "BASE_BODY",
            "race": "HUMAN",
            "gender": gender,
            "identity_id": f"human_{gender.lower()}_body_v1",
            "socket_id": f"HUMAN_{gender}_BODY_SOCKET_V1",
            "geometry_revision": f"{stem}_GUIDE_V1",
            "camera_revision": "COMFY_STYLE_11_BODY_ORTHO_FRONT_V1",
            "views": {"FRONT": {"geometry_guide": plate, "anchor_plate": plate}},
        }
        (args.out / f"{stem}_PACK.json").write_text(json.dumps(pack, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
