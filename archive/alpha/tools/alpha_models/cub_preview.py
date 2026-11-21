"""Orthographic preview of a decoded `.cub` model (Pillow).

Diagnostic tool for the S05A-0 pilot: renders nearest-colour projections so the
geometry and axis orientation can be judged without launching the original game.
It is not an asset exporter.

Axes follow the file layout (`width`, `depth`, `height` — see `cub_format.py`).
Images are drawn with y pointing down, then flipped so the vertical axis grows
upwards, matching the OBJ/Unity convention used by the converter.
"""

from __future__ import annotations

import argparse
import sqlite3
import sys
from pathlib import Path

from PIL import Image, ImageDraw

sys.path.insert(0, str(Path(__file__).resolve().parent))

from cub_format import VoxelModel, parse_stored  # noqa: E402

BACKGROUND = (24, 24, 28)
GRID = (70, 70, 82)
TEXT = (205, 205, 215)

#: view name -> (image width, image height, slice count, voxel lookup)
VIEWS = {
    # looking along -depth: horizontal = x, vertical = height
    "front": lambda m: (m.width, m.height, m.depth,
                        lambda u, v, s: m.rgb_at(u, s, v)),
    # looking along +x: horizontal = depth, vertical = height
    "side": lambda m: (m.depth, m.height, m.width,
                       lambda u, v, s: m.rgb_at(s, u, v)),
    # looking down: horizontal = x, vertical = depth (far side at the bottom)
    "top": lambda m: (m.width, m.depth, m.height,
                      lambda u, v, s: m.rgb_at(u, v, s)),
}


def project(model: VoxelModel, view: str) -> Image.Image:
    u_size, v_size, slice_count, lookup = VIEWS[view](model)
    image = Image.new("RGB", (u_size, v_size), BACKGROUND)
    pixels = image.load()
    # Painter's algorithm: far slices first, near slices overwrite them.
    order = range(slice_count) if view == "side" else range(slice_count - 1, -1, -1)
    for s in order:
        for v in range(v_size):
            for u in range(u_size):
                r, g, b = lookup(u, v, s)
                if r or g or b:
                    pixels[u, v] = (r, g, b)
    return image


def render_sheet(model: VoxelModel, scale: int = 6, label: str = "") -> Image.Image:
    views = [project(model, name) for name in ("front", "side", "top")]
    pad, label_h = 8, 18
    total_w = sum(v.width * scale for v in views) + pad * (len(views) + 1)
    total_h = max(v.height * scale for v in views) + pad * 2 + label_h
    sheet = Image.new("RGB", (total_w, total_h + (label_h if label else 0)), BACKGROUND)
    draw = ImageDraw.Draw(sheet)
    x_cursor = pad
    for name, view in zip(("front  x/height", "side  depth/height", "top  x/depth"), views):
        scaled = view.resize((view.width * scale, view.height * scale), Image.NEAREST)
        scaled = scaled.transpose(Image.FLIP_TOP_BOTTOM)
        sheet.paste(scaled, (x_cursor, pad + label_h))
        draw.rectangle([x_cursor - 1, pad + label_h - 1,
                        x_cursor + scaled.width, pad + label_h + scaled.height], outline=GRID)
        draw.text((x_cursor, 3), name, fill=TEXT)
        x_cursor += scaled.width + pad
    if label:
        draw.text((pad, total_h + 3), label, fill=(150, 200, 150))
    return sheet


def load_model(db: Path, key: str) -> VoxelModel:
    uri = "file:" + db.as_posix() + "?mode=ro&immutable=1"
    con = sqlite3.connect(uri, uri=True)
    row = con.execute("SELECT value FROM blobs WHERE key = ?", (key,)).fetchone()
    if row is None:
        raise SystemExit(f"key not found: {key}")
    return parse_stored(bytes(row[0]))


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--db", required=True, type=Path)
    ap.add_argument("--key", required=True, action="append",
                    help="blob key inside the database, e.g. 'orc-head-m01.cub' (repeatable)")
    ap.add_argument("--out-dir", required=True, type=Path)
    ap.add_argument("--scale", type=int, default=6)
    args = ap.parse_args(argv)

    args.out_dir.mkdir(parents=True, exist_ok=True)
    for key in args.key:
        model = load_model(args.db, key)
        label = (f"{key}  w={model.width} d={model.depth} h={model.height}  "
                 f"solid={model.non_empty_count}/{model.voxel_count}")
        sheet = render_sheet(model, args.scale, label)
        out = args.out_dir / (Path(key).stem + "_preview.png")
        sheet.save(out)
        print(f"wrote {out} ({sheet.width}x{sheet.height})")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
