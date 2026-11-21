#!/usr/bin/env python3
"""ORC-02R stage 1: измерить существующего Orc CubeGame — ТОЛЬКО числа.

Читает read-only источник CubeGame, извлекает габариты/пропорции/палитру и пишет
компактный JSON. Ни одна воксельная сетка или вершина в выход не попадает и
дальше в генерации не используется: генератор читает только этот JSON.

    python scripts/orc02r/measure_reference.py --out measurements/orc_reference.json
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import struct
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
DEFAULT_CONTENT = os.environ.get("CUBEGAME_ALPHA_CONTENT") or r"D:\Projects\CubeGame\.scratch\content"
ACTOR_RELPATH = r"actors\d86_kind_0x0b_formula"
MARKERS = ((255, 0, 0), (0, 255, 0), (0, 0, 255))


def sha256_file(path: str) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as fh:
        for chunk in iter(lambda: fh.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def read_cubdec(path: str):
    """.cubdec: int32 w, d, h + w*d*h*3 байт; индекс ((z*d + y)*w + x)."""
    with open(path, "rb") as fh:
        blob = fh.read()
    w, d, h = struct.unpack_from("<iii", blob, 0)
    expected = 12 + w * d * h * 3
    if len(blob) != expected or min(w, d, h) <= 0:
        raise SystemExit(f"CUBDEC_BAD {path}: {len(blob)} != {expected} ({w}x{d}x{h})")
    cells = {}
    for z in range(h):
        for y in range(d):
            row = 12 + ((z * d + y) * w) * 3
            for x in range(w):
                o = row + x * 3
                rgb = (blob[o], blob[o + 1], blob[o + 2])
                if rgb == (0, 0, 0) or rgb in MARKERS:
                    continue
                cells[(x, y, z)] = rgb
    return w, d, h, cells


def solid_bbox(cells):
    xs = [c[0] for c in cells]
    ys = [c[1] for c in cells]
    zs = [c[2] for c in cells]
    return (min(xs), max(xs)), (min(ys), max(ys)), (min(zs), max(zs))


def dims_from_bbox(bb):
    return (bb[0][1] - bb[0][0] + 1, bb[1][1] - bb[1][0] + 1, bb[2][1] - bb[2][0] + 1)


def clip_part_transform(clip, part_name):
    """Позиция части по кадрам клипа: 16 float на часть, трансляция в [12:15]."""
    order = clip.get("parts") or []
    if part_name not in order:
        return None
    idx = order.index(part_name)
    xs, ys, zs, scales = [], [], [], []
    for key in clip.get("keys") or []:
        flat = key["m"]
        m = flat[idx * 16:(idx + 1) * 16]
        xs.append(m[12]); ys.append(m[13]); zs.append(m[14])
        scales.append(round((m[0] ** 2 + m[1] ** 2 + m[2] ** 2) ** 0.5, 4))
    return {"x": [min(xs), max(xs)], "y": [min(ys), max(ys)], "z": [min(zs), max(zs)],
            "uniform_scale_values": sorted(set(scales))}


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description="ORC-02R reference measurement (numbers only)")
    ap.add_argument("--source-root", default=DEFAULT_CONTENT)
    ap.add_argument("--actor-relpath", default=ACTOR_RELPATH)
    ap.add_argument("--out", default=os.path.join(ROOT, "measurements", "orc_reference.json"))
    args = ap.parse_args(argv)

    actor_dir = os.path.join(args.source_root, args.actor_relpath)
    rig_path = os.path.join(actor_dir, "rig.json")
    if not os.path.isfile(rig_path):
        print(f"REFERENCE_ERROR SOURCE_MISSING {rig_path}", file=sys.stderr)
        return 2
    with open(rig_path, "r", encoding="utf-8") as fh:
        rig = json.load(fh)
    clip_path = os.path.join(actor_dir, "clips", "walk.json")
    with open(clip_path, "r", encoding="utf-8") as fh:
        clip = json.load(fh)

    voxel_m = float(rig["placementScale"])  # 1 Alpha voxel в метрах модели
    parts = {}
    hashes = {"rig.json": sha256_file(rig_path), "clips/walk.json": sha256_file(clip_path)}
    palette_counts = {}
    for part in rig["parts"]:
        path = os.path.join(actor_dir, part["file"])
        w, d, h, cells = read_cubdec(path)
        hashes[part["file"]] = sha256_file(path)
        bb = solid_bbox(cells)
        dims = dims_from_bbox(bb)
        tr = clip_part_transform(clip, part["name"])
        scales = (tr or {}).get("uniform_scale_values") or [1.0]
        parts[part["name"]] = {
            "grid_voxels": [w, d, h],
            "solid_voxels": len(cells),
            "solid_bbox_voxels": [dims[0], dims[1], dims[2]],
            "solid_bbox_m": [round(dims[i] * voxel_m, 4) for i in range(3)],
            "clip_scale_values": scales,
            "clip_translation_x_m": None if not tr else [round(v * voxel_m, 4) for v in tr["x"]],
            "clip_translation_z_m": None if not tr else [round(v * voxel_m, 4) for v in tr["z"]],
        }
        for rgb, n in __import__("collections").Counter(cells.values()).items():
            palette_counts[rgb] = palette_counts.get(rgb, 0) + n

    def dims_of(name, axis_scale=1.0):
        return [round(v * axis_scale, 4) for v in parts[name]["solid_bbox_m"]]

    body = dims_of("body")
    head_scale = parts["head"]["clip_scale_values"][0] if parts["head"]["clip_scale_values"] else 1.0
    head = dims_of("head", head_scale)
    hand = dims_of("hand_l")
    foot = dims_of("foot_l")

    # В источнике нет отдельных костей/мешей конечностей: body — единый блок,
    # руки/ноги геометрией не смоделированы (висят только кисти/стопы).
    total_height_m = round(body[2] + head[2], 4)
    stance_m = round(abs(parts["foot_l"]["clip_translation_x_m"][0]) * 2, 4) if \
        parts["foot_l"]["clip_translation_x_m"] else None
    hand_span_m = round(abs(parts["hand_l"]["clip_translation_x_m"][0]) * 2, 4) if \
        parts["hand_l"]["clip_translation_x_m"] else None

    measured = {
        "voxel_size_m": round(voxel_m, 6),
        "total_height_m": total_height_m,
        "torso": {"width_m": body[0], "depth_m": body[1], "height_m": body[2]},
        "head": {"width_m": head[0], "depth_m": head[1], "height_m": head[2],
                 "clip_scale": head_scale},
        "hand": {"width_m": hand[0], "depth_m": hand[1], "height_m": hand[2]},
        "foot": {"width_m": foot[0], "depth_m": foot[1], "height_m": foot[2]},
        "foot_stance_m": stance_m,
        "hand_span_m": hand_span_m,
        "head_over_torso_height": round(head[2] / body[2], 4),
        "head_over_total_height": round(head[2] / total_height_m, 4),
        "torso_width_over_height": round(body[0] / total_height_m, 4),
        "foot_length_over_height": round(foot[1] / total_height_m, 4),
        "hand_length_over_height": round(hand[1] / total_height_m, 4),
        "silhouette": "TRUNK_BLOB_PLUS_HEAD; конечностей в геометрии источника нет",
    }
    observed_palette = [
        {"hex": "#%02x%02x%02x" % rgb, "voxels": n}
        for rgb, n in sorted(palette_counts.items(), key=lambda kv: -kv[1])[:10]
    ]

    out = {
        "schema": "factoria.reference.orc.v1",
        "measured_at": "2026-11-13",
        "source": {
            "actor_relpath": args.actor_relpath,
            "read_only": True,
            "hashes": hashes,
            "parts": parts,
            "clip_used_for_placement": "clips/walk.json (rest/bind pose в источнике отсутствует)",
        },
        "measurements": measured,
        "observed_palette": observed_palette,
        "measurement_provenance": {
            "MEASURED_GEOMETRY": ["voxel_size_m", "torso.*", "head.*", "hand.*", "foot.*"],
            "MEASURED_CLIP": ["foot_stance_m", "hand_span_m", "head.clip_scale"],
            "DERIVED_FROM_MEASURED": ["total_height_m (trunk + head, ноги в источнике отсутствуют)",
                                      "ratios *"],
            "ABSENT_IN_SOURCE": ["отдельная геометрия плеча/предплечья", "отдельная геометрия бедра/голени",
                                 "rest/bind pose", "ширина плеч по суставам", "ширина таза по суставам"],
        },
        "note": "Только числовые параметры. Ни одна воксельная сетка/вершина источника в генерации не участвует.",
    }
    os.makedirs(os.path.dirname(os.path.abspath(args.out)), exist_ok=True)
    with open(args.out, "w", encoding="utf-8") as fh:
        json.dump(out, fh, ensure_ascii=False, indent=2, sort_keys=True)
    print("REFERENCE_OK " + json.dumps({
        "out": os.path.abspath(args.out),
        "height_m": measured["total_height_m"],
        "torso_m": measured["torso"],
        "head_m": measured["head"],
        "hand_m": measured["hand"],
        "foot_m": measured["foot"],
        "stance_m": measured["foot_stance_m"],
    }, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    sys.exit(main())
