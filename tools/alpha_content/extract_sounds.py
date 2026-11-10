"""D6 — Alpha sound inventory: data2.db blobs -> catalog + local staging (no audio in Git).

``data2.db`` holds 93 ``.wav`` blobs in the same obfuscated container as the models (``cub_format.deobfuscate``
inverts it; every blob is then a real RIFF/WAVE file — verified here, not assumed).

What is proven and what is not:
  * the inventory (names, sizes, sha256, decoded WAVE header) — ALPHA_DATA (this tool);
  * selected event mappings recovered from Cube.exe (see PROVEN_EVENTS and the F4 report);
  * therefore sounds are keyed by NAME here, never by an invented id.

Outputs:
  * ``tools/alpha_content/data/sound_catalog.json`` — committed inventory (no audio);
  * ``<stage>/sounds/*.wav`` — decoded files in the local content cache (``.scratch``, never committed);
  * ``Assets/CubeGame/Content/Generated/AlphaSoundCatalog.g.cs`` — names for the runtime.

    python -m tools.alpha_content.extract_sounds --db "<Cube world>/data2.db" \
        --stage .scratch/content/sounds --catalog tools/alpha_content/data/sound_catalog.json
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

DEFAULT_DB = Path(r"D:\Games\Cube world\data2.db")
OUT_JSON = ROOT / "tools/alpha_content/data/sound_catalog.json"
OUT_CS = ROOT / "Assets/CubeGame/Content/Generated/AlphaSoundCatalog.g.cs"
DEFAULT_STAGE = ROOT / ".scratch/content/sounds"

# 0x484350 dispatches these IDs to filenames. The IDs and database rowids are independent.
PROVEN_EVENTS = {
    "missioncomplete.wav": "MISSION_COMPLETE (Q-09; 0x1E, calls 0x48F9BE/0x4907D7)",
    "pickup.wav": "PICKUP (0x2D, call 0x48CE0C)",
    "levelup2.wav": "LEVEL_UP (0x1D, call 0x493E8F)",
    "menu-open2.wav": "MENU_OPEN (0x53, call 0x48B33D)",
    "menu-close2.wav": "MENU_CLOSE (0x54, call 0x48B33D)",
    "menu-select.wav": "MENU_SELECT (0x55, call 0x42B834)",
    "craft.wav": "CRAFT (0x59, call 0x470C9A)",
}


def wave_info(decoded: bytes) -> dict:
    if len(decoded) < 12 or decoded[:4] != b"RIFF" or decoded[8:12] != b"WAVE":
        raise ValueError("missing RIFF/WAVE header")
    end = struct.unpack_from("<I", decoded, 4)[0] + 8
    if end != len(decoded):
        raise ValueError("RIFF size disagrees with file length")

    fmt = None
    data_size = None
    offset = 12
    while offset < end:
        if end - offset < 8:
            raise ValueError("truncated chunk header")
        kind, size = struct.unpack_from("<4sI", decoded, offset)
        start = offset + 8
        offset = start + size + (size & 1)
        if offset > end:
            raise ValueError("truncated chunk")
        if kind == b"fmt ":
            if fmt is not None or size < 16:
                raise ValueError("invalid fmt chunk")
            fmt = struct.unpack_from("<HHIIHH", decoded, start)
        elif kind == b"data":
            if data_size is not None:
                raise ValueError("multiple data chunks")
            data_size = size

    if fmt is None or not data_size:
        raise ValueError("missing fmt or data")
    encoding, channels, rate, byte_rate, block_align, bits = fmt
    if ((encoding, bits) not in ((1, 16), (3, 32)) or channels not in (1, 2)
            or rate <= 0 or block_align != channels * (bits // 8)
            or byte_rate != rate * block_align or data_size % block_align):
        raise ValueError("unsupported encoding or inconsistent frame layout")
    return {"encoding": encoding, "channels": channels, "rate": rate, "bits": bits}


def build(db: Path, stage: Path | None) -> dict:
    with closing(sqlite3.connect(f"{db.resolve().as_uri()}?mode=ro&immutable=1", uri=True)) as con:
        rows = con.execute("select rowid, key, value from blobs order by rowid").fetchall()
    if stage is not None:
        stage.mkdir(parents=True, exist_ok=True)
    sounds, problems, seen = [], [], set()
    for rowid, name, blob in rows:
        if Path(name).name != name or not name.lower().endswith(".wav") or name.casefold() in seen:
            problems.append(f"INVALID_OR_DUPLICATE_NAME {name} rowid {rowid}")
            continue
        seen.add(name.casefold())
        decoded = deobfuscate(blob)
        try:
            info = wave_info(decoded)
        except ValueError as error:
            problems.append(f"INVALID_WAVE {name}: {error}")
            continue
        if stage is not None:
            (stage / name).write_bytes(decoded)
        sounds.append({
            "name": name,
            "rowid": rowid,
            "bytes": len(blob),
            "decodedBytes": len(decoded),
            "sha256": hashlib.sha256(decoded).hexdigest(),
            **info,
            "event": PROVEN_EVENTS.get(name),
        })
    sounds.sort(key=lambda s: s["name"])
    if len(sounds) != 93:
        problems.append(f"EXPECTED_93_WAV_FOUND_{len(sounds)}")
    return {
        "format": "cubegame.alpha_sound_catalog/1",
        "source": db.name,
        "dbSha256": hashlib.sha256(db.read_bytes()).hexdigest(),
        "codec": "Alpha blob container -> cub_format.deobfuscate -> RIFF/WAVE (verified per file)",
        "eventMapping": "PARTIAL: verified call sites in the F4 report; Alpha sound IDs are independent of database rowids",
        "count": len(sounds),
        "sounds": sounds,
        "problems": problems,
        "RESULT": "PASS" if not problems else "FAIL",
    }


def cs(doc: dict) -> str:
    names = ",\n            ".join(f'"{s["name"]}"' for s in doc["sounds"])
    return f"""// <auto-generated>
//   cubegame.alpha_sound_catalog/1 — generated by tools/alpha_content/extract_sounds.py
//   source: data2.db sha256={doc['dbSha256']}
//   codec: {doc['codec']}
//   event mapping: {doc['eventMapping']}
//   Regenerate: python -m tools.alpha_content.extract_sounds
// </auto-generated>
namespace CubeGame.Content
{{
    /// <summary>Names of the 93 Alpha sounds. Audio files live in the local cache, never in Git.</summary>
    public static class AlphaSoundCatalog
    {{
        public const string Format = "cubegame.alpha_sound_catalog/1";
        public const string Status = "ALPHA_DATA: inventory proven; event mapping PARTIAL (see F4 report)";

        /// <summary>Q-09 mission completion cue.</summary>
        public const string MissionComplete = "missioncomplete.wav";

        public static readonly string[] Names =
        {{
            {names}
        }};
    }}
}}
"""


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
        drift = (not catalog.exists() or catalog.read_text(encoding="utf-8") != text
                 or not OUT_CS.exists() or OUT_CS.read_text(encoding="utf-8") != cs(doc))
        ok = not drift and doc["RESULT"] == "PASS"
        print(f"SOUND_CATALOG_RESULT={'PASS' if ok else 'FAIL'} count={doc['count']} drift={drift}")
        return 0 if ok else 1
    catalog.parent.mkdir(parents=True, exist_ok=True)
    catalog.write_text(text, encoding="utf-8")
    OUT_CS.parent.mkdir(parents=True, exist_ok=True)
    OUT_CS.write_text(cs(doc), encoding="utf-8")
    print(f"SOUND_CATALOG_RESULT={doc['RESULT']} count={doc['count']} staged={a.stage} problems={doc['problems']}")
    return 0 if doc["RESULT"] == "PASS" else 1


if __name__ == "__main__":
    raise SystemExit(main())
