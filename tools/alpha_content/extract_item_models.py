"""D86 M0-04B: stage item model payloads for the local runtime cache.

The accepted converter (``cub_format.deobfuscate``/``parse_decoded``, D61/D65) is reused as-is; no
new conversion method is introduced and nothing is written into Git. The decoded payload
(``.cubdec``) is what ``VoxelMesh`` consumes at runtime.

    python -m tools.alpha_content.extract_item_models \
        --db .scratch/alphatest2/data1.db --out .scratch/content/items
"""

from __future__ import annotations

import argparse
import json
import sqlite3
from pathlib import Path

from tools.alpha_models.cub_format import deobfuscate, parse_decoded

ROOT = Path(__file__).resolve().parents[2]
EVIDENCE = ROOT / "tools/alpha_models/catalog/item_layer_evidence.json"
DEFAULT_DB = ROOT / ".scratch/alphatest2/data1.db"


def default_names() -> list[str]:
    contract = json.loads(EVIDENCE.read_text(encoding="utf-8"))["contract"]
    names = []
    for key in ("weaponClasses", "offhandClasses"):
        for row in contract.get(key, []):
            model = row.get("model")
            if model and model not in names:
                names.append(model)
    return names


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--db", default=str(DEFAULT_DB))
    ap.add_argument("--out", default=str(ROOT / ".scratch/content/items"))
    ap.add_argument("--names", nargs="*", default=None)
    ap.add_argument("--item-types", action="store_true",
                    help="also stage the default armour/quest models named in alpha_confirmed_tables")
    a = ap.parse_args(argv)
    names = a.names if a.names else default_names()
    if a.item_types:
        tables = json.loads((ROOT / "tools/m0_transfer/data/alpha_confirmed_tables.json").read_text(encoding="utf-8"))
        for row in tables["itemTypes"]:
            model = row.get("model")
            if model and model not in names:
                names.append(model)
    db = Path(a.db)
    if not db.exists():
        print("EXTRACT_ITEM_MODELS=FAIL db missing " + str(db))
        return 1
    out = Path(a.out)
    out.mkdir(parents=True, exist_ok=True)
    written, missing, problems = 0, [], []
    con = sqlite3.connect(f"file:{db}?mode=ro&immutable=1", uri=True)
    try:
        for name in names:
            row = con.execute("select value from blobs where key=?", (name,)).fetchone()
            if row is None:
                missing.append(name)
                continue
            try:
                decoded = deobfuscate(row[0])
                model = parse_decoded(decoded)
            except Exception as exc:  # noqa: BLE001 - report the exact converter failure
                problems.append(f"{name}: {exc}")
                continue
            (out / (Path(name).stem + ".cubdec")).write_bytes(decoded)
            written += 1
            if model.width <= 0 or model.depth <= 0 or model.height <= 0:
                problems.append(f"{name}: EMPTY_SIZE")
    finally:
        con.close()
    for name in missing:
        print("  ABSENT_IN_DB", name)
    for problem in problems:
        print("  PROBLEM", problem)
    verdict = "PASS" if written and not problems else "FAIL"
    print(f"EXTRACT_ITEM_MODELS={verdict} written={written} absent={len(missing)} problems={len(problems)} out={out}")
    return 0 if verdict == "PASS" else 1


if __name__ == "__main__":
    raise SystemExit(main())
