"""P6 — Alpha interface icons: data3.db blobs -> catalog + local staging (no images in Git).

``data3.db`` holds the interface PNGs that ``GameController``'s constructor registers by name
(``skills.png``, ``crafting.png``, ``inventory.png``, ``worldmap.png``, ``system.png``, ``help.png`` for the six
menu buttons; ``attack-left.png``, ``slam.png``, ``smash.png`` … for the hotbar actions). The blobs use the same
obfuscated container as the models and sounds (``cub_format.deobfuscate``); every decoded blob is checked for the
PNG signature here, not assumed.

Outputs:
  * ``tools/alpha_content/data/gui_icon_catalog.json`` — committed inventory (names, sizes, sha256; no pixels);
  * ``<stage>/*.png`` — decoded files in the local content cache (``.scratch/content/gui``, never committed).

    python -m tools.alpha_content.extract_gui_icons --db "<Cube world>/data3.db" \
        --stage .scratch/content/gui --catalog tools/alpha_content/data/gui_icon_catalog.json
"""
from __future__ import annotations

import argparse
import hashlib
import json
import sqlite3
import struct
import sys
from contextlib import closing
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT))
from tools.alpha_models.cub_format import deobfuscate  # noqa: E402

DEFAULT_DB = Path(r"D:\Games\Cube world\data3.db")
OUT_JSON = ROOT / "tools/alpha_content/data/gui_icon_catalog.json"
DEFAULT_STAGE = ROOT / ".scratch/content/gui"
PNG = b"\x89PNG\r\n\x1a\n"

# Names the runtime needs; their presence is a gate (ALPHA_CODE_STATIC: GameController ctor 0x45A…, menu-button
# loop over skills/crafting/inventory/worldmap/system/help and the action-id -> icon map).
REQUIRED = ("skills.png", "crafting.png", "inventory.png", "worldmap.png", "system.png", "help.png", "none.png")


def png_size(decoded: bytes) -> tuple[int, int]:
    if decoded[:8] != PNG or decoded[12:16] != b"IHDR":
        raise ValueError("not a PNG")
    return struct.unpack(">II", decoded[16:24])


def build(db: Path, stage: Path | None) -> dict:
    with closing(sqlite3.connect(f"{db.resolve().as_uri()}?mode=ro&immutable=1", uri=True)) as con:
        rows = con.execute("select rowid, key, value from blobs order by rowid").fetchall()
    if stage is not None:
        stage.mkdir(parents=True, exist_ok=True)
    icons, problems = [], []
    for rowid, name, blob in rows:
        if Path(name).name != name or not name.lower().endswith(".png"):
            problems.append(f"INVALID_NAME {name} rowid {rowid}")
            continue
        decoded = deobfuscate(blob)
        try:
            width, height = png_size(decoded)
        except ValueError as error:
            problems.append(f"INVALID_PNG {name}: {error}")
            continue
        if stage is not None:
            (stage / name).write_bytes(decoded)
        icons.append({"name": name, "rowid": rowid, "width": width, "height": height,
                      "sha256": hashlib.sha256(decoded).hexdigest()})
    icons.sort(key=lambda icon: icon["name"])
    names = {icon["name"] for icon in icons}
    problems += [f"MISSING {name}" for name in REQUIRED if name not in names]
    return {
        "format": "cubegame.alpha_gui_icons/1",
        "source": db.name,
        "dbSha256": hashlib.sha256(db.read_bytes()).hexdigest(),
        "codec": "Alpha blob container -> cub_format.deobfuscate -> PNG (verified per file)",
        "count": len(icons),
        "icons": icons,
        "problems": problems,
        "RESULT": "PASS" if not problems else "FAIL",
    }


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--db", default=str(DEFAULT_DB))
    ap.add_argument("--stage", default=str(DEFAULT_STAGE))
    ap.add_argument("--catalog", default=str(OUT_JSON))
    ap.add_argument("--check", action="store_true")
    a = ap.parse_args()
    catalog = Path(a.catalog)
    doc = build(Path(a.db), None if a.check else Path(a.stage))
    text = json.dumps(doc, indent=1) + "\n"
    if a.check:
        drift = not catalog.exists() or catalog.read_text(encoding="utf-8") != text
        ok = not drift and doc["RESULT"] == "PASS"
        print(f"GUI_ICON_CATALOG_RESULT={'PASS' if ok else 'FAIL'} count={doc['count']} drift={drift}")
        return 0 if ok else 1
    catalog.parent.mkdir(parents=True, exist_ok=True)
    catalog.write_text(text, encoding="utf-8")
    print(f"GUI_ICON_CATALOG_RESULT={doc['RESULT']} count={doc['count']} staged={a.stage} problems={doc['problems']}")
    return 0 if doc["RESULT"] == "PASS" else 1


if __name__ == "__main__":
    raise SystemExit(main())
