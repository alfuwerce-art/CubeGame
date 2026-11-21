#!/usr/bin/env python3
"""ORC-02R stage 2: генерация НОВОЙ воксельной модели Orc.

Вход:  measurements/orc_reference.json (только числа) + профиль гуманоида (данные).
Выход: новый GLB + descriptor.json. Геометрия CubeGame здесь не читается и не
используется: генератор не знает ни одного вокселя/вершины источника.

    python scripts/orc02r/generate_orc.py \
        --measurements measurements/orc_reference.json \
        --profile scripts/orc02r/profiles/orc.json \
        --out outputs/prototypes/orc_02r/run1
"""

from __future__ import annotations

import argparse
import copy
import hashlib
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
if HERE not in sys.path:
    sys.path.insert(0, HERE)

from humanoid_base import HumanoidBuilder, MODULE_ORDER  # noqa: E402
from voxelkit import LEVELS, build_glb, greedy_merge  # noqa: E402

GENERATOR = "Factoria HumanoidBase v1 + orc profile (ORC-02R new geometry)"

# Кто «владеет» вокселем при пересечении модулей (первый в списке сохраняет его).
PRIORITY = [
    "head", "eyes", "brows", "mouth", "hair", "torso", "pelvis",
    "pouch", "weapon_club",
    "thigh_L", "thigh_R", "shin_L", "shin_R", "foot_L", "foot_R",
    "upper_arm_L", "upper_arm_R", "forearm_L", "forearm_R", "hand_L", "hand_R",
]
CHAINS = {
    "leg_L": ["thigh_L", "shin_L", "foot_L"],
    "leg_R": ["thigh_R", "shin_R", "foot_R"],
    "arm_L": ["upper_arm_L", "forearm_L", "hand_L"],
    "arm_R": ["upper_arm_R", "forearm_R", "hand_R"],
    "spine": ["pelvis", "torso", "head"],
}


def sha256_file(path: str) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as fh:
        for chunk in iter(lambda: fh.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def apply_overrides(profile: dict, pairs) -> dict:
    out = copy.deepcopy(profile)
    for pair in pairs or []:
        if "=" not in pair:
            raise SystemExit(f"--override ожидает key=value, получено: {pair}")
        key, raw = pair.split("=", 1)
        node = out
        parts = key.split(".")
        for p in parts[:-1]:
            if p not in node or not isinstance(node[p], dict):
                raise SystemExit(f"--override: нет секции {key}")
            node = node[p]
        try:
            value = json.loads(raw)
        except json.JSONDecodeError:
            value = raw
        node[parts[-1]] = value
    return out


def resolve_overlaps(modules: dict) -> tuple[dict, dict]:
    """Гарантирует, что ни один воксель не принадлежит двум модулям."""
    owner = {}
    conflicts = []
    clean = {name: type(modules[name])(name) for name in modules}
    for name in PRIORITY + [n for n in modules if n not in PRIORITY]:
        if name not in modules:
            continue
        for pos, value in modules[name].cells.items():
            if pos in owner:
                conflicts.append({"voxel": list(pos), "kept": owner[pos], "dropped": name})
                continue
            owner[pos] = name
            clean[name].cells[pos] = value
    by_module = {}
    for c in conflicts:
        by_module.setdefault(c["dropped"], 0)
        by_module[c["dropped"]] += 1
    return clean, {"count": len(conflicts), "by_module": by_module, "examples": conflicts[:10]}


def joint_report(modules: dict) -> dict:
    """Зазоры между сегментами по Z (LEGO-модульность) и габариты в вокселях."""
    zr = {}
    for name, grid in modules.items():
        if not grid.cells:
            continue
        zs = [k[2] for k in grid.cells]
        zr[name] = (min(zs), max(zs))
    gaps = {}
    for chain, names in CHAINS.items():
        for a, b in zip(names, names[1:]):
            if a not in zr or b not in zr:
                continue
            lower, upper = (a, b) if zr[a][1] < zr[b][1] else (b, a)
            gap = zr[upper][0] - zr[lower][1] - 1
            gaps[f"{lower}->{upper}"] = gap
    return {"z_ranges_voxels": {k: list(v) for k, v in zr.items()}, "gaps_voxels": gaps}


def ascii_views(modules: dict) -> str:
    """Быстрый текстовый превью силуэта (до Blender): фронт и профиль."""
    chars = {"head": "H", "eyes": "E", "brows": "B", "mouth": "M", "hair": "w", "beard": "b",
             "torso": "T", "pelvis": "P", "upper_arm_L": "a", "upper_arm_R": "a",
             "forearm_L": "f", "forearm_R": "f", "hand_L": "h", "hand_R": "h",
             "thigh_L": "t", "thigh_R": "t", "shin_L": "s", "shin_R": "s",
             "foot_L": "F", "foot_R": "F"}
    pts = {}
    for name, grid in modules.items():
        for (x, y, z) in grid.cells:
            pts.setdefault((x, y, z), chars.get(name, "?"))
    if not pts:
        return "(пусто)"
    xs = [p[0] for p in pts]; ys = [p[1] for p in pts]; zs = [p[2] for p in pts]
    lines = []
    lines.append("FRONT (x ->, z up)")
    for z in range(max(zs), min(zs) - 1, -1):
        row = []
        for x in range(min(xs), max(xs) + 1):
            col = [pts[(x, y, z)] for y in range(min(ys), max(ys) + 1) if (x, y, z) in pts]
            row.append(col[0] if col else ".")
        lines.append(f"{z:>3} " + "".join(row))
    lines.append("SIDE (y ->, z up), front = left")
    for z in range(max(zs), min(zs) - 1, -1):
        row = []
        for y in range(min(ys), max(ys) + 1):
            col = [pts[(x, y, z)] for x in range(min(xs), max(xs) + 1) if (x, y, z) in pts]
            row.append(col[0] if col else ".")
        lines.append(f"{z:>3} " + "".join(row))
    return "\n".join(lines)


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description="ORC-02R new geometry generator")
    ap.add_argument("--measurements", default=os.path.join("measurements", "orc_reference.json"))
    ap.add_argument("--profile", default=os.path.join("scripts", "orc02r", "profiles", "orc.json"))
    ap.add_argument("--out", required=True)
    ap.add_argument("--override", action="append", default=[], help="key.path=value (число/строка)")
    ap.add_argument("--label", default="run", help="метка прогона для descriptor")
    ap.add_argument("--ascii", action="store_true", help="напечатать текстовый превью силуэта")
    args = ap.parse_args(argv)

    with open(args.measurements, "r", encoding="utf-8") as fh:
        measurements = json.load(fh)
    with open(args.profile, "r", encoding="utf-8") as fh:
        profile_raw = json.load(fh)
    profile = apply_overrides(profile_raw, args.override)
    seed = int(profile.get("seed", 0))
    voxel_m = float(profile["voxel_size_m"])

    builder = HumanoidBuilder(profile)
    raw_modules = builder.build()
    if args.ascii:
        print(ascii_views(raw_modules))
    modules, overlap = resolve_overlaps(raw_modules)

    detail = profile.get("detail", {})
    max_box = int(detail.get("max_box_voxels", 6))
    tint = float(detail.get("tint_amplitude", 0.05))
    boxes = {name: greedy_merge(grid, max_box) for name, grid in modules.items()}

    module_stats, level_stats = {}, {lv: {"voxels": 0, "boxes": 0} for lv in LEVELS}
    for name, grid in modules.items():
        st = grid.stats()
        bx = boxes.get(name, [])
        sizes = {(b["size"][0], b["size"][1], b["size"][2]) for b in bx}
        bbox_m = None
        if st["bbox_voxels"]:
            bb = st["bbox_voxels"]
            bbox_m = {
                "x": [round((bb["x"][0]) * voxel_m, 3), round((bb["x"][1] + 1) * voxel_m, 3)],
                "y": [round((bb["y"][0]) * voxel_m, 3), round((bb["y"][1] + 1) * voxel_m, 3)],
                "z": [round((bb["z"][0]) * voxel_m, 3), round((bb["z"][1] + 1) * voxel_m, 3)],
            }
        module_stats[name] = {
            "voxels": st["voxels"], "voxels_by_level": st["by_level"], "boxes": len(bx),
            "unique_box_sizes": len(sizes), "bbox_m": bbox_m,
        }
        for lv, n in st["by_level"].items():
            level_stats[lv]["voxels"] += n
        for b in bx:
            level_stats[b["level"]]["boxes"] += 1

    glb = build_glb(boxes, profile["palette"], voxel_m, GENERATOR, tint, seed)
    joints = joint_report(modules)

    total_voxels = sum(s["voxels"] for s in module_stats.values())
    total_boxes = sum(s["boxes"] for s in module_stats.values())
    z_extent = max((s["bbox_m"]["z"][1] for s in module_stats.values() if s["bbox_m"]), default=0.0)
    anatomical_m = round((profile["landmarks"]["skull_top"] + 1) * voxel_m, 3)
    ref = measurements["measurements"]
    ref_deviation = None
    if ref.get("total_height_m"):
        ref_deviation = round((anatomical_m - ref["total_height_m"]) / ref["total_height_m"] * 100.0, 2)

    descriptor = {
        "task": "ORC-02R",
        "label": args.label,
        "generator": GENERATOR,
        "seed": seed,
        "voxel_size_m": voxel_m,
        "inputs": {
            "measurements": {"path": os.path.abspath(args.measurements),
                             "sha256": sha256_file(args.measurements)},
            "profile": {"path": os.path.abspath(args.profile),
                        "sha256": sha256_file(args.profile),
                        "name": profile.get("name"),
                        "overrides": args.override},
        },
        "source_geometry_reused": False,
        "source_geometry_note": "источник CubeGame читает только measure_reference.py; "
                                "генератор получает исключительно числа измерений",
        "measured_fields_used": [
            "measurements.voxel_size_m (масштаб сетки)",
            "measurements.total_height_m (проверка роста)",
            "measurements.torso.*, head.*, hand.*, foot.* (проверка пропорций)",
            "measurements.foot_stance_m, hand_span_m (ширина стойки)",
        ],
        "reference_check": {
            "reference_total_height_m": ref.get("total_height_m"),
            "generated_anatomical_height_m": anatomical_m,
            "generated_total_height_with_hair_m": round(z_extent, 3),
            "deviation_percent": ref_deviation,
            "reference_torso_m": ref.get("torso"),
            "reference_head_m": ref.get("head"),
            "note": "рост (по макушке черепа) сверяется с измерениями старого Orc; силуэтные "
                    "пропорции (крупная голова, короткие конечности, широкие плечи) взяты с "
                    "УТВЕРЖДЁННОГО визуального эталона ORC-02R, а не из старой модели",
        },
        "levels": {lv: level_stats[lv] for lv in LEVELS},
        "modules": module_stats,
        "module_order": [n for n in MODULE_ORDER if n in module_stats],
        "totals": {"modules": len([m for m in module_stats if module_stats[m]["voxels"] > 0]),
                   "voxels": total_voxels, "boxes": total_boxes},
        "joints": joints,
        "joint_cuts": builder.cut_log,
        "overlap_resolution": overlap,
    }

    os.makedirs(args.out, exist_ok=True)
    glb_path = os.path.join(args.out, "orc_new.glb")
    with open(glb_path, "wb") as fh:
        fh.write(glb)
    descriptor["outputs"] = {"glb": glb_path, "glb_bytes": len(glb),
                             "glb_sha256": hashlib.sha256(glb).hexdigest()}
    with open(os.path.join(args.out, "descriptor.json"), "w", encoding="utf-8") as fh:
        json.dump(descriptor, fh, ensure_ascii=False, indent=2, sort_keys=True)
    print("GENERATE_OK " + json.dumps({
        "glb": glb_path, "bytes": len(glb), "sha256": descriptor["outputs"]["glb_sha256"],
        "modules": descriptor["totals"]["modules"], "voxels": total_voxels, "boxes": total_boxes,
        "height_m": round(z_extent, 3), "reference_height_m": ref.get("total_height_m"),
        "overlaps_trimmed": overlap["count"],
    }, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    sys.exit(main())
