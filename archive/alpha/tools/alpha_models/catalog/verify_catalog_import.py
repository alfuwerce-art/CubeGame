"""Verify the EXISTING Unity catalog import (D65: 2856 models) against Alpha's data1.db.

Why a verifier and not a converter: D65 already imported every model into the isolated Unity project
(``CATALOG_IMPORT_RESULT=PASS models=2856 pass=2856``). Re-converting them is forbidden by D66/D71
("не конвертировать повторно уже проверенные модели"). What can still go wrong silently is that the
*input data* changed under the imported meshes (a different Alpha copy, a repacked DB, an edited
manifest). This tool therefore re-derives every number the Unity importer reported from the Alpha
source, read-only, without writing any asset:

per record
  * the blob exists in ``data1.db`` and ``sha256(blob) == record.sha256`` (the model is the one that
    was imported);
  * ``deobfuscate`` + the ``.cub`` header parse;
  * ``voxel_markers.mesh_stats`` (the verified Python mirror of ``VoxelMesh.Build``, D61 marker rule)
    reproduces ``faces`` / ``vertices``;
  * ``solid`` and ``markers`` match the manifest under the same rule (flag-0 models count their
    marker voxels as transparent, flag-1 models do not draw them but count them as solid);

globally
  * unique names, every ``verdict`` PASS, a known route, a prefab path that matches the route;
  * ``data1.db`` holds 2857 blobs: 2856 imported + exactly one model that Alpha draws with zero faces
    (``undead-hair-m01.cub``, the documented ``ALPHA_VISIBLE_EMPTY_BY_MARKER_RULE`` case), so the
    catalogue is complete, not merely non-failing.

The output is a verdict, not a claim of visual acceptance: ``CATALOG_VERIFY_RESULT=PASS`` means the
imported models still match Alpha's bytes.

    python -m tools.alpha_models.catalog.verify_catalog_import \
        --manifest D:/Projects/CubeGame/.scratch/catalog/unity/AlphaCatalog/CatalogOutput/catalog_import.json \
        --db "D:/Games/Cube world/data1.db" --out .scratch/catalog/catalog_verify.json

Exit code 1 on any problem; ``--limit N`` verifies the first N records (smoke run).
"""
from __future__ import annotations

import argparse
import collections
import hashlib
import json
import sqlite3
import sys
from pathlib import Path

from tools.alpha_models.cub_format import deobfuscate, parse_decoded
from tools.alpha_models.universal import voxel_markers as vm

ROOT = Path(__file__).resolve().parents[3]
KNOWN_ROUTES = ("CREATURE_PART", "EQUIPMENT_ATTACH", "EQUIPMENT_SWAP", "ITEM_STATIC", "DB_ONLY",
                "REGISTRY_STATIC", "REGISTRY_OVERWRITTEN", "FILE_LOADER_ABSENT")
EXPECTED_BLOBS = 2857
EXPECTED_IMPORTED = 2856


def _filled(dec: bytes) -> int:
    m = parse_decoded(dec)
    px = m.pixels
    return sum(1 for i in range(0, len(px), 3) if (px[i], px[i + 1], px[i + 2]) != (0, 0, 0))


def verify(manifest, db, limit=None):
    man = json.loads(Path(manifest).read_text(encoding="utf-8"))
    records = man.get("records") if isinstance(man, dict) else man
    if not isinstance(records, list) or not records:
        return {"result": "FAIL", "problems": ["MANIFEST_HAS_NO_RECORDS"]}
    if limit:
        records = records[:limit]
    problems, seen = [], set()
    con = sqlite3.connect(f"file:{db}?mode=ro&immutable=1", uri=True)
    blob_names = {r[0] for r in con.execute("select key from blobs")}
    routes = collections.Counter()
    verified = 0
    for r in records:
        name = r.get("name")
        if not name:
            problems.append("RECORD_WITHOUT_NAME")
            continue
        if name in seen:
            problems.append(f"DUPLICATE_NAME {name}")
        seen.add(name)
        if r.get("verdict") != "PASS":
            problems.append(f"VERDICT_NOT_PASS {name}: {r.get('verdict')} {r.get('problem')!r}")
        route = r.get("route")
        routes[route] += 1
        if route not in KNOWN_ROUTES:
            problems.append(f"UNKNOWN_ROUTE {name}: {route}")
        prefab = r.get("prefab") or ""
        if route and f"/{route}/" not in prefab:
            problems.append(f"PREFAB_ROUTE_MISMATCH {name}: {prefab}")
        if name not in blob_names:
            problems.append(f"MODEL_NOT_IN_DB {name}")
            continue
        blob = con.execute("select value from blobs where key=?", (name,)).fetchone()[0]
        sha = hashlib.sha256(blob).hexdigest()
        if sha != r.get("sha256"):
            problems.append(f"BLOB_SHA256_DRIFT {name}: {sha} != {r.get('sha256')}")
        dec = deobfuscate(blob)
        m = parse_decoded(dec)
        size = [m.width, m.depth, m.height]
        stats = vm.mesh_stats(dec, strict=not r.get("markerTransparent", False))
        transparent = bool(r.get("markerTransparent"))
        markers = sum(stats["markers"]) if transparent else 0
        filled = _filled(dec)
        solid = filled - markers
        if stats["faces"] != r.get("faces"):
            problems.append(f"FACES_MISMATCH {name}: {stats['faces']} != {r.get('faces')} size={size}")
        if stats["vertices"] != r.get("vertices"):
            problems.append(f"VERTICES_MISMATCH {name}: {stats['vertices']} != {r.get('vertices')}")
        if markers != r.get("markers"):
            problems.append(f"MARKERS_MISMATCH {name}: {markers} != {r.get('markers')}")
        if solid != r.get("solid"):
            problems.append(f"SOLID_MISMATCH {name}: {solid} != {r.get('solid')}")
        verified += 1
    # completeness: the DB holds one model Alpha draws with zero faces (not imported by design)
    if limit is None:
        missing = sorted(blob_names - seen)
        if len(blob_names) != EXPECTED_BLOBS:
            problems.append(f"DB_BLOB_COUNT {len(blob_names)} != {EXPECTED_BLOBS}")
        if len(records) != EXPECTED_IMPORTED:
            problems.append(f"IMPORTED_COUNT {len(records)} != {EXPECTED_IMPORTED}")
        if len(missing) != 1:
            problems.append(f"MISSING_FROM_MANIFEST {missing}")
        else:
            blob = con.execute("select value from blobs where key=?", (missing[0],)).fetchone()[0]
            stats = vm.mesh_stats(deobfuscate(blob), strict=False)
            if stats["faces"] != 0:
                problems.append(f"MISSING_MODEL_NOT_ALPHA_EMPTY {missing[0]} faces={stats['faces']}")
    con.close()
    return {"result": "PASS" if not problems else "FAIL", "records": len(records),
            "verified": verified, "problems": problems[:50], "problem_count": len(problems),
            "routes": dict(sorted(routes.items())), "blobs_in_db": len(blob_names),
            "manifest_sha256": hashlib.sha256(Path(manifest).read_bytes()).hexdigest()}


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--manifest", required=True)
    ap.add_argument("--db", required=True)
    ap.add_argument("--limit", type=int, default=None)
    ap.add_argument("--out", default=None, help="JSON report (must stay inside .scratch)")
    a = ap.parse_args(argv)
    res = verify(a.manifest, a.db, a.limit)
    if a.out:
        out = Path(a.out).resolve()
        if not out.is_relative_to(ROOT / ".scratch"):
            raise SystemExit("ALPHA_OUTPUT_MUST_BE_IN_SCRATCH")
        out.parent.mkdir(parents=True, exist_ok=True)
        out.write_text(json.dumps(res, indent=1), encoding="utf-8")
    print(f"CATALOG_VERIFY_RESULT={res['result']} records={res.get('records')} "
          f"verified={res.get('verified')} problems={res.get('problem_count')}")
    for p in res["problems"][:20]:
        print("  ", p)
    return 0 if res["result"] == "PASS" else 1


if __name__ == "__main__":
    sys.exit(main())
