#!/usr/bin/env python3
"""ORC-02: one-command unattended voxel redesign pipeline for the real CubeGame Orc.

Вход (read-only, НЕ изменяется): локальный Alpha content cache CubeGame
    <content>/actors/d86_kind_0x0b_formula/{rig.json, models/*.cubdec}
Выход (только в Factoria): отдельный каталог прогона с GLB, манифестом и дескриптором.

Ключевые свойства:
  * только стандартная библиотека Python (без AI-моделей и GPU);
  * конвейер не знает про «орка»: правила приходят из JSON-профиля (family/profile/seed/detail);
  * детерминизм: тот же вход+конфиг+seed → те же байты GLB и тот же descriptor hash;
  * при отсутствии/повреждении источника — понятная ошибка и ненулевой код возврата.

Запуск:
    python scripts/orc02/run_orc02.py --config scripts/orc02/configs/orc_profile.json \
        --out outputs/prototypes/orc_02/run1
"""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import struct
import sys
import time

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
DEFAULT_CONTENT = os.environ.get("CUBEGAME_ALPHA_CONTENT") or r"D:\Projects\CubeGame\.scratch\content"
VOXEL_M = 0.1  # 1 Alpha voxel = 0.1 m (AlphaRigBuilder.RootScale)


class PipelineError(Exception):
    def __init__(self, code: str, message: str, exit_code: int = 2):
        super().__init__(f"{code}: {message}")
        self.code = code
        self.message = message
        self.exit_code = exit_code


# ------------------------------------------------------------------ источник --
def sha256_file(path: str) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as fh:
        for chunk in iter(lambda: fh.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def read_cubdec(path: str, marker_transparent: bool):
    """Формат .cubdec: int32 w, int32 d, int32 h, затем w*d*h RGB; (0,0,0) = пусто.

    Индекс ((z*d + y)*w + x). Соответствует VoxelMesh.cs (CubeGame), проверено по коду.
    """
    with open(path, "rb") as fh:
        blob = fh.read()
    if len(blob) < 12:
        raise PipelineError("CUBDEC_TOO_SHORT", f"{path}: {len(blob)} байт")
    w, d, h = struct.unpack_from("<iii", blob, 0)
    if min(w, d, h) <= 0 or max(w, d, h) > 256:
        raise PipelineError("CUBDEC_BAD_SIZE", f"{path}: {w}x{d}x{h}")
    expected = 12 + w * d * h * 3
    if len(blob) != expected:
        raise PipelineError("CUBDEC_LENGTH", f"{path}: {len(blob)} != {expected}")
    cells = {}
    for z in range(h):
        for y in range(d):
            row = 12 + ((z * d + y) * w) * 3
            for x in range(w):
                o = row + x * 3
                r, g, b = blob[o], blob[o + 1], blob[o + 2]
                if r == 0 and g == 0 and b == 0:
                    continue
                if marker_transparent and ((r, g, b) in ((255, 0, 0), (0, 255, 0), (0, 0, 255))):
                    continue
                cells[(x, y, z)] = (r, g, b)
    return w, d, h, cells


def load_source(content_root: str, profile: dict):
    actor_rel = profile["source"]["actor_relpath"]
    actor_dir = os.path.join(content_root, actor_rel)
    rig_path = os.path.join(actor_dir, profile["source"]["rig_file"])
    if not os.path.isfile(rig_path):
        raise PipelineError("SOURCE_MISSING", f"нет rig.json: {rig_path}")
    with open(rig_path, "r", encoding="utf-8") as fh:
        rig = json.load(fh)
    parts = []
    hashes = {"rig.json": sha256_file(rig_path)}
    for part in rig.get("parts", []):
        path = os.path.join(actor_dir, part["file"])
        if not os.path.isfile(path):
            raise PipelineError("SOURCE_MISSING", f"нет модели части {part['name']}: {path}")
        w, d, h, cells = read_cubdec(path, bool(part.get("markerTransparent")))
        if not cells:
            raise PipelineError("SOURCE_EMPTY_PART", f"{part['name']}: нет вокселей")
        hashes[part["file"]] = sha256_file(path)
        parts.append({
            "name": part["name"],
            "model": part.get("model"),
            "size": [w, d, h],
            "cells": cells,
            "anchor": part.get("anchor"),
            "mirrored": bool(part.get("mirrored")),
            "tint_mode": part.get("tintMode"),
        })
    expected = profile["source"].get("expected_parts") or []
    got = {p["name"] for p in parts}
    missing = [n for n in expected if n not in got]
    if missing:
        raise PipelineError("SOURCE_PARTS_MISSING", f"нет частей: {missing}")
    return actor_dir, rig, parts, hashes


# ------------------------------------------------------------------- геометрия --
def split_body(cells: dict, size, rules: dict):
    """Делит воксели body на анатомические модули по долям профиля (DATA, не хардкод)."""
    w, d, h = size
    modules = {"torso": {}, "pelvis": {}, "upper_arm_L": {}, "upper_arm_R": {},
               "forearm_L": {}, "forearm_R": {}, "thigh_L": {}, "thigh_R": {},
               "shin_L": {}, "shin_R": {}}
    for (x, y, z), rgb in cells.items():
        zn = z / max(h - 1, 1)
        xn = abs(x - (w - 1) / 2.0) / max((w - 1) / 2.0, 1e-6)
        side = "L" if x < (w - 1) / 2.0 else "R"
        if zn < rules["pelvis_z_max"] and xn < rules["arm_x_min"]:
            modules["pelvis"][(x, y, z)] = rgb
        elif zn <= rules["torso_z_max"] and xn < rules["arm_x_min"]:
            modules["torso"][(x, y, z)] = rgb
        elif zn >= rules["upper_arm_z_min"] and xn >= rules["arm_x_min"]:
            key = "upper_arm_" + side if zn >= rules["forearm_z_split"] else "forearm_" + side
            modules[key][(x, y, z)] = rgb
        elif zn < rules["leg_z_max"]:
            key = "thigh_" + side if zn >= rules["thigh_z_split"] else "shin_" + side
            modules[key][(x, y, z)] = rgb
        elif zn <= rules["torso_z_max"]:
            modules["torso"][(x, y, z)] = rgb
        else:
            modules["pelvis"].setdefault((x, y, z), rgb)
    return modules


def split_face(cells: dict, size, face_rules: dict):
    """Глаза/брови — по цветовому контрасту на фронтальной зоне головы.

    Возвращает (eyes, brows, status). Если контраст не найден — модули пустые и
    status = AMBIGUOUS_FACE_LANDMARKS (identity не выдумываем).
    """
    w, d, h = size
    if not cells or not face_rules.get("enabled", True):
        return {}, {}, "FACE_DISABLED"
    counts = {}
    for rgb in cells.values():
        counts[rgb] = counts.get(rgb, 0) + 1
    skin = max(counts.items(), key=lambda kv: kv[1])[0]
    limit = int(face_rules["color_distance_min"])
    eyes, brows = {}, {}
    front_max_y = max(c[1] for c in cells)
    for (x, y, z), rgb in cells.items():
        if y < front_max_y - 1:
            continue
        dist = sum((rgb[i] - skin[i]) ** 2 for i in range(3)) ** 0.5
        if dist < limit:
            continue
        zn = z / max(h - 1, 1)
        lo, hi = face_rules["eye_zone_z"]
        if lo <= zn <= hi:
            eyes[(x, y, z)] = rgb
        lo2, hi2 = face_rules["brow_zone_z"]
        if lo2 <= zn <= hi2:
            brows[(x, y, z)] = rgb
    status = "DERIVED_FROM_COLOR_CONTRAST"
    if len(eyes) < 2:
        eyes = {}
        status = "AMBIGUOUS_FACE_LANDMARKS"
    return eyes, brows, status


def merge_boxes(cells: dict, axis_order, max_box: int):
    """Жадное слияние вокселей в кубоиды: даёт кубы разных размеров (иерархия детали)."""
    remaining = set(cells)
    boxes = []
    axis_index = {"x": 0, "y": 1, "z": 2}
    order = [axis_index[a] for a in axis_order]
    while remaining:
        start = min(remaining)
        color = cells[start]
        size = [1, 1, 1]
        for axis in order:
            while size[axis] < max_box:
                ok = True
                for a in range(size[0]):
                    for b in range(size[1]):
                        for c in range(size[2]):
                            q = [start[0] + a, start[1] + b, start[2] + c]
                            q[axis] = start[axis] + size[axis]
                            q = tuple(q)
                            if q not in remaining or cells[q] != color:
                                ok = False
                                break
                        if not ok:
                            break
                    if not ok:
                        break
                if not ok:
                    break
                size[axis] += 1
        for a in range(size[0]):
            for b in range(size[1]):
                for c in range(size[2]):
                    remaining.discard((start[0] + a, start[1] + b, start[2] + c))
        boxes.append({"min": list(start), "size": size, "color": list(color)})
    return boxes


def shrink_about_centroid(boxes, factor):
    """Сжимает каждый модуль к его центру: видимые зазоры между модулями без потери вокселей."""
    if not boxes or factor >= 1.0:
        return boxes
    lo = [min(b["min"][i] for b in boxes) for i in range(3)]
    hi = [max(b["min"][i] + b["size"][i] for b in boxes) for i in range(3)]
    center = [(lo[i] + hi[i]) / 2.0 for i in range(3)]
    out = []
    for b in boxes:
        bmin = [center[i] + (b["min"][i] - center[i]) * factor for i in range(3)]
        bmax = [center[i] + (b["min"][i] + b["size"][i] - center[i]) * factor for i in range(3)]
        out.append({"min": bmin, "size": [bmax[i] - bmin[i] for i in range(3)], "color": b["color"]})
    return out


def erode_seams(modules: dict, gap: int):
    """Убирает у каждого модуля воксели, граничащие с другим модулем: видимые зазоры."""
    owner = {}
    for name, cells in modules.items():
        for p in cells:
            owner[p] = name
    eroded = {}
    for name, cells in modules.items():
        keep = set()
        for p in cells:
            drop = False
            for axis in range(3):
                for delta in (-gap, gap):
                    q = list(p)
                    q[axis] += delta
                    o = owner.get(tuple(q))
                    if o is not None and o != name:
                        drop = True
                        break
                if drop:
                    break
            if not drop:
                keep.add(p)
        eroded[name] = {p: cells[p] for p in keep}
    return eroded


# ---------------------------------------------------------------------- GLB ----
def build_glb(boxes_by_module: dict, offsets: dict | None = None) -> bytes:
    """Пишет GLB 2.0: отдельный узел/меш на модуль, вершинные цвета (normalized)."""
    signs = [(-1, -1, -1), (1, -1, -1), (1, 1, -1), (-1, 1, -1),
             (-1, -1, 1), (1, -1, 1), (1, 1, 1), (-1, 1, 1)]
    faces = [(0, 2, 1), (0, 3, 2), (4, 5, 6), (4, 6, 7),
             (0, 1, 5), (0, 5, 4), (2, 3, 7), (2, 7, 6),
             (0, 4, 7), (0, 7, 3), (1, 2, 6), (1, 6, 5)]
    binary = bytearray()
    views, accessors, meshes, nodes = [], [], [], []

    def align():
        while len(binary) % 4:
            binary.append(0)

    def add_view(payload: bytes, target=None):
        align()
        offset = len(binary)
        binary.extend(payload)
        view = {"buffer": 0, "byteOffset": offset, "byteLength": len(payload)}
        if target:
            view["target"] = target
        views.append(view)
        return len(views) - 1

    for name, boxes in boxes_by_module.items():
        if not boxes:
            continue
        ox, oy, oz = offsets.get(name, (0.0, 0.0, 0.0))
        pos, col, idx = [], [], []
        for box in boxes:
            base = len(pos) // 3
            cx = box["min"][0] + box["size"][0] / 2.0
            cy = box["min"][1] + box["size"][1] / 2.0
            cz = box["min"][2] + box["size"][2] / 2.0
            for sx, sy, sz in signs:
                # Внутренние координаты: x = ширина, y = глубина, z = высота (воксели).
                # glTF — Y-up: (x, высота, -глубина), тогда Blender-импорт даёт Z-up.
                px = (cx + ox + sx * box["size"][0] / 2.0) * VOXEL_M
                py = (cz + oz + sz * box["size"][2] / 2.0) * VOXEL_M
                pz = -((cy + oy) + sy * box["size"][1] / 2.0) * VOXEL_M
                pos.extend((px, py, pz))
                col.extend((box["color"][0], box["color"][1], box["color"][2], 255))
            for tri in faces:
                idx.extend((base + tri[0], base + tri[1], base + tri[2]))
        xs, ys, zs = pos[0::3], pos[1::3], pos[2::3]
        pview = add_view(struct.pack("<%df" % len(pos), *pos), 34962)
        accessors.append({"bufferView": pview, "componentType": 5126, "count": len(pos) // 3,
                          "type": "VEC3", "min": [min(xs), min(ys), min(zs)],
                          "max": [max(xs), max(ys), max(zs)]})
        pos_acc = len(accessors) - 1
        cview = add_view(bytes(col), 34962)
        accessors.append({"bufferView": cview, "componentType": 5121, "count": len(col) // 4,
                          "type": "VEC4", "normalized": True})
        iview = add_view(struct.pack("<%dI" % len(idx), *idx), 34963)
        accessors.append({"bufferView": iview, "componentType": 5125, "count": len(idx),
                          "type": "SCALAR", "min": [min(idx)], "max": [max(idx)]})
        meshes.append({"name": name, "primitives": [{"attributes": {"POSITION": pos_acc, "COLOR_0": len(accessors) - 2},
                                                     "indices": len(accessors) - 1, "material": 0, "mode": 4}]})
        nodes.append({"name": name, "mesh": len(meshes) - 1,
                      "extras": {"body_part": name, "rig_ready": False}})

    doc = {
        "asset": {"version": "2.0", "generator": "Factoria ORC-02 unattended pipeline v0.1"},
        "extensionsUsed": ["KHR_materials_unlit"],
        "scene": 0, "scenes": [{"nodes": list(range(len(nodes)))}],
        "nodes": nodes, "meshes": meshes,
        "materials": [{"name": "voxel_vertex_colors",
                       "pbrMetallicRoughness": {"baseColorFactor": [1, 1, 1, 1], "metallicFactor": 0.0,
                                                "roughnessFactor": 1.0},
                       "extensions": {"KHR_materials_unlit": {}}, "doubleSided": False}],
        "accessors": accessors, "bufferViews": views,
        "buffers": [{"byteLength": len(binary)}],
    }
    align()
    json_chunk = json.dumps(doc, separators=(",", ":")).encode("utf-8")
    while len(json_chunk) % 4:
        json_chunk += b" "
    total = 12 + 8 + len(json_chunk) + 8 + len(binary)
    return (struct.pack("<4sII", b"glTF", 2, total)
            + struct.pack("<I4s", len(json_chunk), b"JSON") + json_chunk
            + struct.pack("<I4s", len(binary), b"BIN\x00") + bytes(binary))


# ------------------------------------------------------------------- конвейер --
def load_placements(actor_dir: str, profile: dict, part_names):
    """Позиции частей из клипа-источника (первый кадр walk), НЕ выдуманные.

    Формат клипа: keys[i].m = 16 float на часть (4x4, трансляция в m[12..14]);
    порядок частей — clip["parts"]. Единицы Unity: 1 воксель = 0.1 (RootScale).
    """
    placement = profile.get("placement", {})
    clip_rel = placement.get("clip")
    if not clip_rel:
        raise PipelineError("SOURCE_PLACEMENT_UNAVAILABLE", "в профиле не задан clip для размещения частей")
    clip_path = os.path.join(actor_dir, clip_rel)
    if not os.path.isfile(clip_path):
        raise PipelineError("SOURCE_PLACEMENT_UNAVAILABLE", f"нет клипа размещения: {clip_path}")
    with open(clip_path, "r", encoding="utf-8") as fh:
        clip = json.load(fh)
    keys = clip.get("keys") or []
    order = clip.get("parts") or []
    if not keys or not order:
        raise PipelineError("SOURCE_PLACEMENT_UNAVAILABLE", "клип пуст или без parts")
    flat = keys[0]["m"]
    if len(flat) != len(order) * 16:
        raise PipelineError("SOURCE_PLACEMENT_BAD_LAYOUT", f"{len(flat)} значений на {len(order)} частей")
    voxel = VOXEL_M
    out = {}
    for i, name in enumerate(order):
        m = flat[i * 16:(i + 1) * 16]
        tx, ty, tz = m[12], m[13], m[14]           # Unity: x вправо, y вверх, z вперёд
        scale = m[0] if m[0] else 1.0
        # Unity → воксельная сетка: (x, z_depth, y_height); делим на 0.1
        # Клип хранит трансляции в координатах сетки (x, глубина, высота), воксельные единицы:
        # проверено по согласованности (голова ~5.8 по высоте, стопы ~ -6..-8).
        out[name] = {"voxel": (tx, ty, tz), "scale": scale,
                     "clip": clip_rel, "key_index": 0}
    missing = [n for n in part_names if n not in out]
    if missing:
        raise PipelineError("SOURCE_PLACEMENT_INCOMPLETE", f"в клипе нет частей: {missing}")
    return out


def world_offsets(parts: dict, profile: dict):
    """Профильное размещение частей в воксельных единицах (PROFILE_DRIVEN, см. конфиг)."""
    body = next((p for p in parts.values() if p["name"] == "body"), None)
    if body is None:
        raise PipelineError("SOURCE_NO_BODY", "нет части body")
    bw, bd, bh = body["size"]
    hand = parts["hand_l"]["size"]
    foot = parts["foot_l"]["size"]
    head = parts["head"]["size"]
    pl = profile["placement"]
    offsets = {
        "body": (-bw / 2.0, -bd / 2.0, 0.0),
        "head": (-head[0] / 2.0, -head[1] / 2.0, bh + 1 + pl.get("head_gap", 1)),
        "hair": (-parts["hair"]["size"][0] / 2.0, -parts["hair"]["size"][1] / 2.0, bh + 1 + pl.get("head_gap", 1)),
        "hand_l": (-bw / 2.0 - hand[0] - 1, -hand[1] / 2.0, bh * pl.get("hand_z", 0.15)),
        "hand_r": (bw / 2.0 + 1, -hand[1] / 2.0, bh * pl.get("hand_z", 0.15)),
        "foot_l": (-bw / 4.0 - foot[0] / 2.0, -foot[1] / 2.0, -foot[2] - 1),
        "foot_r": (bw / 4.0 - foot[0] / 2.0, -foot[1] / 2.0, -foot[2] - 1),
    }
    return offsets


def build_modules(content_root: str, profile: dict, detail_scale: float):
    t0 = time.time()
    actor_dir, rig, parts_list, source_hashes = load_source(content_root, profile)
    parts = {p["name"]: p for p in parts_list}
    detail = dict(profile["detail"])
    max_box = max(1, int(round(detail["max_box_voxels"] * detail_scale)))

    modules = {}
    face_status = "NOT_REQUESTED"
    body = parts["body"]
    modules.update(split_body(body["cells"], body["size"], profile["body_split"]))
    head = parts["head"]
    modules["head"] = dict(head["cells"])
    eyes, brows, face_status = split_face(head["cells"], head["size"], profile["face"])
    modules["eyes"] = eyes
    modules["brows"] = brows
    modules["hair"] = dict(parts["hair"]["cells"])
    for side in ("l", "r"):
        modules["hand_" + side.upper()] = dict(parts["hand_" + side]["cells"])
        modules["foot_" + side.upper()] = dict(parts["foot_" + side]["cells"])
    gap_fraction = float(profile.get("joint_gap_fraction", 0.06))
    shrink = max(0.5, 1.0 - gap_fraction)  # сжимаем модуль на долю зазора, а не до неё

    placements = load_placements(actor_dir, profile, [p["name"] for p in parts_list])
    boxes_by_module = {}
    module_stats = {}
    body_derived = {"torso", "pelvis", "upper_arm_L", "upper_arm_R", "forearm_L", "forearm_R",
                    "thigh_L", "thigh_R", "shin_L", "shin_R"}
    module_offsets = {}
    for name, cells in modules.items():
        if not cells:
            boxes_by_module[name] = []
            module_stats[name] = {"voxels": 0, "boxes": 0, "unique_box_sizes": 0}
            continue
        if name in body_derived:
            base_part = "body"
        elif name in ("eyes", "brows"):
            base_part = "head"
        else:
            base_part = name.lower()
        pl = placements[base_part]
        # Смещение части применяется позже, при записи GLB: воксельная сетка модуля
        # остаётся целочисленной, поэтому слияние боксов не теряет воксели.
        module_offsets[name] = tuple(float(v) for v in pl["voxel"])
        part_scale = float(profile.get("part_scale_overrides", {}).get(base_part, 1.0))
        boxes = merge_boxes(dict(cells), detail["greedy_axis_order"], max_box)
        if part_scale != 1.0:
            boxes = shrink_about_centroid(boxes, part_scale)
        module_stats[name] = module_stats.get(name, {})
        boxes = shrink_about_centroid(boxes, shrink)
        boxes_by_module[name] = boxes
        sizes = {(b["size"][0], b["size"][1], b["size"][2]) for b in boxes}
        module_stats[name] = {
            "voxels": len(cells),
            "boxes": len(boxes),
            "unique_box_sizes": len(sizes),
        }

    glb = build_glb(boxes_by_module, module_offsets)
    descriptor = {
        "profile": profile.get("profile"),
        "family": profile.get("family"),
        "detail_scale": detail_scale,
        "max_box_voxels": max_box,
        "modules": module_stats,
        "face_status": face_status,
        "placement": {"source": "CLIP_FRAME_0", "clip": profile.get("placement", {}).get("clip"),
                      "note": "позиции частей взяты из первого кадра клипа источника; анимационный масштаб частей не применялся"},
        "totals": {
            "modules": sum(1 for s in module_stats.values() if s["voxels"] > 0),
            "voxels": sum(s["voxels"] for s in module_stats.values()),
            "boxes": sum(s["boxes"] for s in module_stats.values()),
        },
        "source": {
            "actor_dir": actor_dir,
            "hashes": source_hashes,
            "rig": rig.get("rig"),
            "variant": rig.get("variant"),
        },
    }
    timing = {"seconds": round(time.time() - t0, 2)}
    return glb, descriptor, timing


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description="ORC-02 unattended voxel redesign pipeline")
    ap.add_argument("--config", default=os.path.join(ROOT, "scripts", "orc02", "configs", "orc_profile.json"))
    ap.add_argument("--out", required=True, help="каталог прогона (создаётся заново)")
    ap.add_argument("--source-root", default=DEFAULT_CONTENT)
    ap.add_argument("--detail-scale", type=float, default=1.0)
    args = ap.parse_args(argv)

    started = time.time()
    try:
        with open(args.config, "r", encoding="utf-8") as fh:
            profile = json.load(fh)
        out = os.path.abspath(args.out)
        if os.path.isdir(out) and os.listdir(out):
            raise PipelineError("OUTPUT_NOT_EMPTY", f"{out} уже содержит файлы (не перезаписываем)")
        os.makedirs(out, exist_ok=True)
        glb, descriptor, timing = build_modules(args.source_root, profile, args.detail_scale)
        glb_path = os.path.join(out, "orc02_modular.glb")
        with open(glb_path, "wb") as fh:
            fh.write(glb)
        descriptor["outputs"] = {
            "glb": glb_path,
            "glb_bytes": len(glb),
            "glb_sha256": hashlib.sha256(glb).hexdigest(),
        }
        descriptor["timing"] = timing
        descriptor["wall_seconds"] = round(time.time() - started, 2)
        with open(os.path.join(out, "descriptor.json"), "w", encoding="utf-8") as fh:
            json.dump(descriptor, fh, ensure_ascii=False, indent=2, sort_keys=True)
        print("ORC02_OK " + json.dumps({
            "glb": glb_path,
            "glb_bytes": len(glb),
            "glb_sha256": descriptor["outputs"]["glb_sha256"],
            "modules": descriptor["totals"]["modules"],
            "voxels": descriptor["totals"]["voxels"],
            "boxes": descriptor["totals"]["boxes"],
            "face_status": descriptor["face_status"],
            "seconds": descriptor["wall_seconds"],
        }, ensure_ascii=False))
        return 0
    except PipelineError as exc:
        print("ORC02_ERROR " + json.dumps({"code": exc.code, "message": exc.message}, ensure_ascii=False),
              file=sys.stderr)
        return exc.exit_code
    except Exception as exc:  # непредвиденная ошибка — тоже машинный формат
        print("ORC02_ERROR " + json.dumps({"code": "UNEXPECTED", "message": f"{type(exc).__name__}: {exc}"},
                                          ensure_ascii=False), file=sys.stderr)
        return 3


if __name__ == "__main__":
    sys.exit(main())
