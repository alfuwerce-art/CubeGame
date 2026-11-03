"""Read-only inventory of `data1.db` (Cube World Alpha 2013 voxel models).

Usage::

    python tools/alpha_models/cub_probe.py --db "D:/Games/Cube world/data1.db" --out report.json

The database is opened with SQLite `mode=ro&immutable=1`, so the original file
cannot be modified. Only metadata and aggregate metrics are written: model
names, dimensions, voxel/colour statistics. No asset bytes are exported here.
"""

from __future__ import annotations

import argparse
import json
import sqlite3
import sys
from collections import Counter
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from cub_format import CubFormatError, parse_stored, solid_colors  # noqa: E402


def open_readonly(db_path: Path) -> sqlite3.Connection:
    if not db_path.exists():
        raise SystemExit(f"database not found: {db_path}")
    uri = "file:" + db_path.as_posix() + "?mode=ro&immutable=1"
    return sqlite3.connect(uri, uri=True)


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--db", required=True, type=Path)
    ap.add_argument("--out", type=Path, default=None)
    ap.add_argument("--sample-limit", type=int, default=0,
                    help="also decode the first N models and report colour stats")
    args = ap.parse_args(argv)

    con = open_readonly(args.db)
    cur = con.cursor()
    cur.execute("SELECT name FROM sqlite_master WHERE type='table'")
    tables = [row[0] for row in cur.fetchall()]
    cur.execute("SELECT key, length(value) FROM blobs")
    rows = cur.fetchall()
    total_bytes = sum(length for _, length in rows)
    ext = Counter(Path(key).suffix.lower() for key, _ in rows)

    result: dict = {
        "db": str(args.db),
        "tables": tables,
        "records": len(rows),
        "total_value_bytes": total_bytes,
        "extensions": dict(ext),
        "decode_ok": 0,
        "decode_failed": [],
        "dimension_histogram": {},
        "models": [],
    }

    dims = Counter()
    models = []
    for index, (key, length) in enumerate(rows):
        cur.execute("SELECT value FROM blobs WHERE key = ?", (key,))
        stored = cur.fetchone()[0]
        if index < args.sample_limit or args.sample_limit <= 0:
            try:
                model = parse_stored(bytes(stored))
            except CubFormatError as exc:  # keep going: report every broken entry
                result["decode_failed"].append({"key": key, "error": str(exc)})
            else:
                result["decode_ok"] += 1
                dims[f"{model.width}x{model.height}x{model.depth}"] += 1
                colors = solid_colors(model)
                models.append({
                    "key": key,
                    "stored_bytes": length,
                    "dims": [model.width, model.height, model.depth],
                    "voxels": model.voxel_count,
                    "non_empty": model.non_empty_count,
                    "unique_colors": len(colors),
                    "top_colors": [[list(c), n] for c, n in list(colors.items())[:5]],
                })

    result["dimension_histogram"] = dict(dims.most_common())
    result["models"] = models

    text = json.dumps(result, indent=2, ensure_ascii=False)
    if args.out:
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(text, encoding="utf-8")
        print(f"wrote {args.out}")
    print(f"records={result['records']} bytes={total_bytes} extensions={dict(ext)}")
    print(f"decoded_ok={result['decode_ok']} failed={len(result['decode_failed'])}")
    print(f"distinct dimensions={len(dims)}")
    return 0 if not result["decode_failed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
