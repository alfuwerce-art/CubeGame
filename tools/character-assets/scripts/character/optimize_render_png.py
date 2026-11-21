"""MANNEQUIN-RIG-01: keep the published review renders small (palette PNG, in place).

The renders are flat-shaded grey mannequin stills on a plain background, so an adaptive
64-colour palette is visually lossless for review purposes and ~3x smaller than RGBA.
Run after `mannequin_v3_validate.py` (Blender writes RGBA PNGs).

Usage:
    python scripts/character/optimize_render_png.py [folder]
"""
from __future__ import annotations

import sys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
DEFAULT = ROOT / "outputs" / "prototypes" / "mannequin_v3_rig" / "run1" / "renders"


def main():
    folder = Path(sys.argv[1]) if len(sys.argv) > 1 else DEFAULT
    total_before = total_after = 0
    for path in sorted(folder.glob("*.png")):
        before = path.stat().st_size
        img = Image.open(path).convert("RGB")
        img.convert("P", palette=Image.ADAPTIVE, colors=64, dither=Image.FLOYDSTEINBERG).save(
            path, optimize=True)
        after = path.stat().st_size
        total_before += before
        total_after += after
        print(f"{path.name:34s} {before // 1024:5d} KB -> {after // 1024:4d} KB")
    print(f"TOTAL {total_before // 1024} KB -> {total_after // 1024} KB")


if __name__ == "__main__":
    main()
