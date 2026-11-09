"""D86 M0-02/M0-07: distill the world constructor name tables into a committed evidence file.

The extractor (``tools/alpha_research/extract_world_ctor_tables.py``) reads the decompiled
``cube::World::ctor_1`` body and pairs insert calls with the string assignment that follows.
Its raw output lives in ``.scratch``; this script keeps only ids/names/ambiguity (no paths into the
game installation, no assets) so the game can use the same numbers without the Alpha copy.

    python tools/alpha_research/extract_world_ctor_tables.py .scratch/CubeWorld-Reversal/cube/world/World.cpp .scratch/tmp/world_ctor_raw.json
    python -m tools.alpha_content.build_world_evidence --raw .scratch/tmp/world_ctor_raw.json
"""

from __future__ import annotations

import argparse
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "tools/alpha_content/data/world_ctor_evidence.json"
FORMAT = "cubegame.world_ctor_evidence/1"


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--raw", default=str(ROOT / ".scratch/tmp/world_ctor_raw.json"))
    ap.add_argument("--out", default=str(OUT))
    ap.add_argument("--check", action="store_true", help="verify that the committed file is current")
    a = ap.parse_args(argv)
    raw = json.loads(Path(a.raw).read_text(encoding="utf-8"))

    def object_row(row):
        return {"id": int(row["id"]), "name": row["name"], "ambiguous": bool(row.get("ambiguous", False))}

    def item_row(row):
        return {"type": int(row["type"]), "subtype": int(row["subtype"]), "name": row["name"],
                "ambiguous": bool(row.get("ambiguous", False))}

    evidence = {
        "format": FORMAT,
        "status": "RESEARCH_DRAFT_NOT_APPROVED",
        "note": "Name tables of cube::World::ctor_1 (Alpha 2013 client). Ids/names only: evidence data "
                "for the M0 content tables, not an approved game design and not a license to redistribute assets.",
        "source": {
            "file": ".scratch/CubeWorld-Reversal/cube/world/World.cpp",
            "symbol": raw.get("source_symbol", "cube::World::ctor_1 @ 0x0058eb00"),
            "extractor": "tools/alpha_research/extract_world_ctor_tables.py",
            "pairing_rule": raw.get("pairing_rule", ""),
        },
        "objects": sorted((object_row(r) for r in raw["object_map"]), key=lambda r: r["id"]),
        "items": sorted((item_row(r) for r in raw["item_map"]), key=lambda r: (r["type"], r["subtype"])),
        "skills": sorted(({"id": int(r["id"]), "name": r["name"]} for r in raw["skill_map"]), key=lambda r: r["id"]),
    }
    text = json.dumps(evidence, indent=1, ensure_ascii=True) + "\n"
    out = Path(a.out)
    if a.check:
        if not out.exists() or out.read_text(encoding="utf-8") != text:
            print("WORLD_CTOR_EVIDENCE=FAIL drift")
            return 1
        print(f"WORLD_CTOR_EVIDENCE=PASS objects={len(evidence['objects'])} items={len(evidence['items'])} "
              f"skills={len(evidence['skills'])}")
        return 0
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(text, encoding="utf-8", newline="\n")
    print(f"WORLD_CTOR_EVIDENCE=PASS objects={len(evidence['objects'])} items={len(evidence['items'])} "
          f"skills={len(evidence['skills'])} out={out}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
