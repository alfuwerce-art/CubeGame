"""Marker/tint analysis for `.cub` models (S05A-1).

Background, verified in the original client (`VoxelModel_set_data` @ 0x004e7650):
the loader scans every voxel and pushes the coordinates of voxels whose colour is
exactly pure green `(0,255,0)`, pure red `(255,0,0)` or pure blue `(0,0,255)` into
three separate coordinate lists stored on the model. The colour buffer itself is
copied unchanged, so those voxels stay in the geometry as well.

What is NOT established: what each list means, and whether a given pure-colour
voxel is a marker or a deliberate decorative colour. Therefore this module does not
auto-assign semantics; it measures each pure-colour group and attaches a
**heuristic** label that callers may accept or ignore:

* `ANCHOR_CANDIDATE`      - tiny group (<= 16 voxels), fully on the surface
                            (matches the observed green/blue clusters);
* `VOLUME_TINT_CANDIDATE` - large surface group (observed: pure red in every hair
                            model, never in head/body/hand models), consistent with
                            a placeholder colour that the game replaces;
* `UNKNOWN_DECORATIVE`    - anything else, including isolated pure-colour voxels
                            that may simply be decoration.

Callers must treat the label as a hint, not as proof. `excluded_voxels()` builds the
mask used by the converter when an operator explicitly decides which groups to hide.
"""

from __future__ import annotations

import argparse
import json
import sqlite3
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from cub_format import VoxelModel, parse_stored  # noqa: E402
from cub_to_obj import is_solid  # noqa: E402

MARKER_COLORS = {
    "red": (255, 0, 0),
    "green": (0, 255, 0),
    "blue": (0, 0, 255),
}

ANCHOR_MAX_VOXELS = 16
TINT_MIN_VOXELS = 64
TINT_MIN_EXPOSURE = 0.5


def is_exposed(model: VoxelModel, x: int, d: int, h: int) -> bool:
    for dx, dz, dy in ((1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1)):
        if not is_solid(model, x + dx, d + dz, h + dy):
            return True
    return False


def marker_groups(model: VoxelModel) -> dict[str, dict]:
    """Measure each pure-colour group: voxels, bbox, exposure, heuristic label."""
    groups: dict[str, dict] = {}
    for name, color in MARKER_COLORS.items():
        voxels = []
        for h in range(model.height):
            for d in range(model.depth):
                for x in range(model.width):
                    if is_solid(model, x, d, h) and model.rgb_at(x, d, h) == color:
                        voxels.append((x, d, h))
        if not voxels:
            continue
        exposed = sum(1 for (x, d, h) in voxels if is_exposed(model, x, d, h))
        xs = [v[0] for v in voxels]; ds = [v[1] for v in voxels]; hs = [v[2] for v in voxels]
        exposure = exposed / len(voxels)
        label, reason = classify(len(voxels), exposure)
        groups[name] = {
            "color": list(color),
            "count": len(voxels),
            "exposed": exposed,
            "exposure": round(exposure, 4),
            "bbox_min": [min(xs), min(ds), min(hs)],
            "bbox_max": [max(xs), max(ds), max(hs)],
            "heuristic_label": label,
            "heuristic_reason": reason,
            "voxels": [list(v) for v in sorted(voxels)],
        }
    return groups


def classify(count: int, exposure: float) -> tuple[str, str]:
    if count <= ANCHOR_MAX_VOXELS and exposure >= 0.99:
        return ("ANCHOR_CANDIDATE",
                f"{count} <= {ANCHOR_MAX_VOXELS} voxels, fully exposed: matches the "
                f"green/blue clusters seen on humanoid models")
    if count >= TINT_MIN_VOXELS and exposure >= TINT_MIN_EXPOSURE:
        return ("VOLUME_TINT_CANDIDATE",
                f"{count} >= {TINT_MIN_VOXELS} voxels, {exposure:.0%} exposed: large surface "
                f"area in a single pure colour, consistent with a replaceable base colour")
    return ("UNKNOWN_DECORATIVE",
            f"{count} voxels, {exposure:.0%} exposed: no evidence for a marker role")


def excluded_voxels(model: VoxelModel, colors: list[str]) -> set[tuple[int, int, int]]:
    """Voxel coordinates to treat as empty, for the colours named by the caller."""
    excluded: set[tuple[int, int, int]] = set()
    groups = marker_groups(model)
    for name in colors:
        group = groups.get(name)
        if group is None:
            continue
        excluded.update(tuple(v) for v in group["voxels"])
    return excluded


def describe_model(model: VoxelModel) -> dict:
    groups = marker_groups(model)
    return {
        "dimensions": list(model.dimensions),
        "solid_voxels": model.non_empty_count,
        "marker_groups": {name: {k: v for k, v in group.items() if k != "voxels"}
                          for name, group in groups.items()},
    }


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--db", required=True, type=Path)
    ap.add_argument("--key", required=True, action="append")
    ap.add_argument("--out", required=True, type=Path)
    ap.add_argument("--with-voxels", action="store_true",
                    help="include every marker voxel coordinate in the output")
    args = ap.parse_args(argv)

    uri = "file:" + args.db.as_posix() + "?mode=ro&immutable=1"
    con = sqlite3.connect(uri, uri=True)
    report = {"note": "heuristic labels only; see tools/alpha_models/cub_markers.py", "models": {}}
    for key in args.key:
        row = con.execute("SELECT value FROM blobs WHERE key = ?", (key,)).fetchone()
        if row is None:
            report["models"][key] = {"error": "key not found"}
            continue
        model = parse_stored(bytes(row[0]))
        entry = describe_model(model)
        if args.with_voxels:
            entry["marker_groups_full"] = marker_groups(model)
        report["models"][key] = entry

    args.out.parent.mkdir(parents=True, exist_ok=True)
    args.out.write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")
    print(f"wrote {args.out}")
    for key, entry in report["models"].items():
        if "error" in entry:
            print(f"  {key}: {entry['error']}")
            continue
        summary = ", ".join(f"{name}:{g['count']}({g['heuristic_label']})"
                            for name, g in entry["marker_groups"].items())
        print(f"  {key}: {summary or 'no pure RGB markers'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
