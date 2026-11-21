"""Compose GEO-02R owner comparison plates from the verified final Blender renders."""

import hashlib
import json
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parent))
import review_mannequin_geo_02 as review

RUN = review.ROOT / "outputs/prototypes/mannequin_geo_02r/run1"
DETAILS = (
    "HEAD_FRONT", "HEAD_EAR", "FEET",
    "HAND_NEUTRAL", "HAND_OPEN", "HAND_FIST",
    "HAND_GRIP", "HAND_RELEASE", "ATTACK_GRIP",
)


def main():
    report = json.loads((RUN / "validation.json").read_text(encoding="utf-8"))
    required = {"blend": "mannequin_v3_rig.blend", "glb": "mannequin_v3_male_rig.glb"}
    actual = {name: hashlib.sha256((RUN / filename).read_bytes()).hexdigest()
              for name, filename in required.items()}
    if (report["result"] != "PASS" or actual != report["artifacts"].get("sha256")
            or not set(name + ".png" for name in (*review.BODY_VIEWS, *DETAILS))
            <= set(report["numbers"].get("render_files", []))):
        raise ValueError("Review requires all renders from the current validated asset build.")
    reference = review.compare.REF
    if hashlib.sha256(reference.read_bytes()).hexdigest() != review.APPROVED_SHA:
        raise ValueError("Reference A differs from the approved V3 sheet")
    review.compare.main(run=RUN, task="MANNEQUIN-GEO-02R")
    review.overview(run=RUN)
    review.overview(run=RUN, views=DETAILS, columns=3, filename="review_anatomy.png")
    review.module_plate(run=RUN, revision="GEO-02R")


if __name__ == "__main__":
    main()
