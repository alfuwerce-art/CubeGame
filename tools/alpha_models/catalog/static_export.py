"""D64: export catalog models as Unity input (.cubdec + manifest) — every route, resumable, SHA256-pinned.

Geometry of EVERY data1.db model goes to Unity through ONE accepted mesher (``VoxelMesh.cs``, the
D61 marker rule) — creature parts, equipment, items, props, buildings, map tiles, unused content.
What differs per route is only metadata (route, binding, load flag); the animated use of creature
parts and equipment is the separate creature pipeline (``transfer_planner``), which reuses the same
geometry.

Manifest record (per model): name, file, sha256 of the blob, route, load flag, markerTransparent
(= Alpha load flag 0, D61), expected grid size, expected solid and marker voxel counts. The Unity
importer (``AlphaCatalogImport.cs``) must reproduce size, solid and marker counts exactly.

Resume: a model whose blob SHA256 and export record are unchanged is not rewritten; ``--select``
limits the export (route list, name glob, or ``sample`` = one model per route for the
representative check).

python -m tools.alpha_models.catalog.static_export --project .scratch/catalog/unity/AlphaCatalog [--select sample]
"""
from __future__ import annotations

import argparse
import collections
import fnmatch
import hashlib
import json
import sqlite3
import sys
from pathlib import Path

from tools.alpha_models.cub_format import deobfuscate, parse_decoded
from tools.alpha_models.universal import voxel_markers as vm

ROOT = Path(__file__).resolve().parents[3]
DEFAULT_DB = ROOT / ".scratch/alphatest2/data1.db"
DEFAULT_CATALOG = ROOT / ".scratch/catalog/asset_catalog.json"
FORMAT = "cubegame.alpha_catalog_manifest/1"


def counts(dec, marker_transparent):
    m = parse_decoded(dec)
    px = m.pixels
    cols = collections.Counter(zip(px[0::3], px[1::3], px[2::3]))
    filled = sum(n for c, n in cols.items() if c != (0, 0, 0))
    markers = sum(cols.get(c, 0) for c in vm.MARKERS) if marker_transparent else 0
    return [m.width, m.depth, m.height], filled - markers, markers


def select(models, sel):
    if not sel or sel == "all":
        return sorted(models)
    if sel == "sample":
        seen, out = set(), []
        for n in sorted(models):
            m = models[n]
            key = (m["route"], m["status"]["VISUAL"])
            if key not in seen and m.get("in_db"):
                seen.add(key)
                out.append(n)
        return out
    routes = set(sel.split(","))
    return sorted(n for n, m in models.items() if m["route"] in routes or any(fnmatch.fnmatch(n, p) for p in routes))


def export(project, catalog=DEFAULT_CATALOG, db=DEFAULT_DB, sel=None):
    project = Path(project).resolve()
    if not project.is_relative_to((ROOT / ".scratch").resolve()):
        raise SystemExit("ALPHA_OUTPUT_MUST_BE_IN_SCRATCH")
    cat = json.loads(Path(catalog).read_text(encoding="utf-8"))
    data = project / "Assets" / "AlphaCatalog" / "Data"
    (data / "models").mkdir(parents=True, exist_ok=True)
    man_path = data / "manifest.json"
    old = {}
    if man_path.exists():
        old = {r["name"]: r for r in json.loads(man_path.read_text(encoding="utf-8")).get("models", [])}
    con = sqlite3.connect(f"file:{db}?mode=ro&immutable=1", uri=True)
    recs, written, skipped, absent = [], 0, 0, []
    for n in select(cat["models"], sel):
        m = cat["models"][n]
        if not m.get("in_db"):
            absent.append({"name": n, "route": m["route"], "reason": "ALPHA_EMPTY_MODEL (no blob; Alpha draws nothing)"})
            continue
        flag = next((l["load_flag"] for l in m["loaders"] if l["load_flag"] is not None), None)
        transparent = flag == 0
        f = data / "models" / (n.replace("/", "_") + "dec")         # x.cub -> x.cubdec
        prev = old.get(n)
        if prev and prev["sha256"] == m["blob_sha256"] and prev["markerTransparent"] == transparent and f.exists():
            recs.append(prev)
            skipped += 1
            continue
        blob = con.execute("select value from blobs where key=?", (n,)).fetchone()[0]
        if hashlib.sha256(blob).hexdigest() != m["blob_sha256"]:
            raise SystemExit(f"DB_DRIFT {n}: blob differs from the catalog (rebuild the catalog)")
        dec = deobfuscate(blob)
        size, solid, markers = counts(dec, transparent)
        f.write_bytes(dec)
        recs.append({"name": n, "file": f"models/{f.name}", "sha256": m["blob_sha256"], "route": m["route"],
                     "loadFlag": -1 if flag is None else flag, "markerTransparent": transparent,
                     "size": size, "solid": solid, "markers": markers})
        written += 1
    man = {"format": FORMAT, "catalog_db_sha256": cat["db_sha256"], "selection": sel or "all", "models": recs,
           "absent": absent}
    man_path.write_text(json.dumps(man, indent=1), encoding="utf-8")
    return {"STATIC_EXPORT": "DONE", "models": len(recs), "written": written, "unchanged": skipped,
            "absent_in_db": len(absent), "manifest": str(man_path)}


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--project", required=True)
    ap.add_argument("--catalog", default=str(DEFAULT_CATALOG))
    ap.add_argument("--db", default=str(DEFAULT_DB))
    ap.add_argument("--select", default=None, help="all | sample | comma list of routes / name globs")
    a = ap.parse_args(argv)
    print(json.dumps(export(a.project, a.catalog, a.db, a.select)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
