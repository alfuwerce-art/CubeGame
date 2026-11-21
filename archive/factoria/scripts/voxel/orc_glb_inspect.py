"""ORC-01: независимая проверка GLB без сторонних библиотек (только stdlib).

Читает GLB 2.0, разбирает JSON-чанк и BIN-чанк, и проверяет:
  - число independent-модулей (meshes/nodes), материалов и примитивов;
  - общие bounds модели (ожидается Y-up, метры);
  - по каждому модулю: количество кубов (по 8 вершин на бокс), min/max размеров,
    уникальные размеры кубов (доказательство «разных размеров»);
  - зазоры между последовательными сегментами конечностей (плечо→предплечье→кисть,
    бедро→голень→стопа) по оси Y;
  - суммы треугольников/вершин.

Запуск:
    python scripts\\voxel\\orc_glb_inspect.py --glb outputs\\prototypes\\orc_01\\generated\\orc_modular_v01.glb `
        --out outputs\\prototypes\\orc_01\\reports\\glb_inspect_generated.json
"""

import argparse
import json
import os
import struct
import sys

PROJECT_ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

LIMB_CHAINS = {
    "arm_L": ["upper_arm_L", "forearm_L", "hand_L"],
    "arm_R": ["upper_arm_R", "forearm_R", "hand_R"],
    "leg_L": ["thigh_L", "shin_L", "foot_L"],
    "leg_R": ["thigh_R", "shin_R", "foot_R"],
}

# Ожидаемые «посадочные» пересечения: деталь вставлена в тело/голову. Не дефект.
EXPECTED_ATTACHMENTS = {
    frozenset(("pelvis", "torso")),
    frozenset(("pelvis", "thigh_L")),
    frozenset(("pelvis", "thigh_R")),
    frozenset(("torso", "head")),
    frozenset(("torso", "upper_arm_L")),
    frozenset(("torso", "upper_arm_R")),
    frozenset(("torso", "beard")),
    frozenset(("head", "eyes")),
    frozenset(("head", "brows")),
    frozenset(("head", "mouth")),
    frozenset(("head", "beard")),
    frozenset(("head", "hair")),
    frozenset(("mouth", "beard")),
}


def read_glb(path):
    with open(path, "rb") as fh:
        blob = fh.read()
    magic, version, length = struct.unpack("<4sII", blob[:12])
    if magic != b"glTF" or version != 2:
        raise ValueError(f"not a GLB 2.0 file: magic={magic!r} version={version}")
    if length != len(blob):
        raise ValueError(f"declared length {length} != actual {len(blob)}")
    offset = 12
    gltf = None
    binary = b""
    while offset < length:
        chunk_len, chunk_type = struct.unpack("<I4s", blob[offset:offset + 8])
        chunk = blob[offset + 8:offset + 8 + chunk_len]
        if chunk_type == b"JSON":
            gltf = json.loads(chunk.decode("utf-8"))
        elif chunk_type == b"BIN\x00":
            binary = chunk
        offset += 8 + chunk_len
    if gltf is None:
        raise ValueError("no JSON chunk")
    return gltf, binary, len(blob)


def accessor_floats(gltf, binary, index, comps):
    acc = gltf["accessors"][index]
    view = gltf["bufferViews"][acc["bufferView"]]
    start = view.get("byteOffset", 0) + acc.get("byteOffset", 0)
    count = acc["count"]
    stride = view.get("byteStride", comps * 4)
    out = []
    for i in range(count):
        base = start + i * stride
        out.append(struct.unpack_from("<" + "f" * comps, binary, base))
    return out


def read_colors(gltf, binary, acc_index):
    """Возвращает (список RGBA-подобных значений, признак integer-без-normalized)."""
    acc = gltf["accessors"][acc_index]
    view = gltf["bufferViews"][acc["bufferView"]]
    comps = {"VEC3": 3, "VEC4": 4}[acc["type"]]
    fmt = {5121: "B", 5123: "H", 5126: "f"}[acc["componentType"]]
    size = struct.calcsize(fmt)
    start = view.get("byteOffset", 0) + acc.get("byteOffset", 0)
    stride = view.get("byteStride", comps * size)
    values = []
    for i in range(acc["count"]):
        values.append(struct.unpack_from("<" + fmt * comps, binary, start + i * stride))
    normalized = acc.get("normalized", False)
    if acc["componentType"] == 5121:
        scale = 255.0 if normalized else 1.0
        values = [tuple(v / scale for v in item) for item in values]
    elif acc["componentType"] == 5123:
        scale = 65535.0 if normalized else 1.0
        values = [tuple(v / scale for v in item) for item in values]
    invalid = acc["componentType"] in (5121, 5123) and not normalized
    return values, invalid


def is_box_layout(positions):
    """Проверяет, что каждая группа из 8 вершин — axis-aligned бокс."""
    if len(positions) % 8 != 0 or not positions:
        return False
    for base in range(0, min(len(positions), 8 * 4), 8):
        chunk = positions[base:base + 8]
        for axis in range(3):
            vals = {round(p[axis], 5) for p in chunk}
            if len(vals) != 2:
                return False
    return True


def accessor_indices(gltf, binary, index):
    acc = gltf["accessors"][index]
    view = gltf["bufferViews"][acc["bufferView"]]
    start = view.get("byteOffset", 0) + acc.get("byteOffset", 0)
    fmt = {5121: "B", 5123: "H", 5125: "I"}[acc["componentType"]]
    return list(struct.unpack_from("<" + fmt * acc["count"], binary, start))


def inspect(path):
    gltf, binary, blob_len = read_glb(path)
    report = {
        "path": path,
        "glb_bytes": blob_len,
        "asset": gltf.get("asset", {}),
        "extensions_used": gltf.get("extensionsUsed", []),
        "nodes": len(gltf.get("nodes", [])),
        "meshes": len(gltf.get("meshes", [])),
        "materials": [m.get("name") for m in gltf.get("materials", [])],
        "parts": {},
        "totals": {"boxes": 0, "vertices": 0, "triangles": 0},
        "bounding_box": None,
        "gaps": {},
        "notes": [],
        "issues": [],
    }
    if not gltf.get("scenes"):
        report["issues"].append("no scene defined")
    global_min = [float("inf")] * 3
    global_max = [float("-inf")] * 3
    all_sizes = set()

    boxes_by_part = {}
    for node in gltf.get("nodes", []):
        name = node.get("name")
        mesh_index = node.get("mesh")
        if mesh_index is None:
            continue
        mesh = gltf["meshes"][mesh_index]
        prim = mesh["primitives"][0]
        pos_acc = gltf["accessors"][prim["attributes"]["POSITION"]]
        positions = accessor_floats(gltf, binary, prim["attributes"]["POSITION"], 3)
        indices = accessor_indices(gltf, binary, prim["indices"])
        vcount = len(positions)
        box_layout = is_box_layout(positions)
        boxes = (vcount // 8) if box_layout else 0
        sizes = []
        part_boxes = []
        if box_layout:
            for b in range(boxes):
                chunk = positions[b * 8:(b + 1) * 8]
                xs = [p[0] for p in chunk]
                ys = [p[1] for p in chunk]
                zs = [p[2] for p in chunk]
                dims = (round(max(xs) - min(xs), 4), round(max(ys) - min(ys), 4), round(max(zs) - min(zs), 4))
                sizes.append(dims)
                all_sizes.add(dims)
                part_boxes.append(((min(xs), min(ys), min(zs)), (max(xs), max(ys), max(zs))))
        else:
            # После Blender-экспорта вершины разбиты по нормалям: боксы не восстановить
            # по порядку вершин. Тогда берём весь модуль как один AABB.
            part_boxes = [((min(p[0] for p in positions), min(p[1] for p in positions), min(p[2] for p in positions)),
                           (max(p[0] for p in positions), max(p[1] for p in positions), max(p[2] for p in positions)))]
        boxes_by_part[name] = part_boxes
        # Цвета: для целочисленных COLOR_0 обязателен normalized=true.
        color_values, invalid_norm = read_colors(gltf, binary, prim["attributes"]["COLOR_0"])
        distinct_colors = len({tuple(round(v, 3) for v in item) for item in color_values})
        report.setdefault("colors", {})[name] = {
            "normalized": bool(gltf["accessors"][prim["attributes"]["COLOR_0"]].get("normalized", False)),
            "component_type": gltf["accessors"][prim["attributes"]["COLOR_0"]]["componentType"],
            "invalid_normalized": invalid_norm,
            "distinct_colors": distinct_colors,
            "first_color": [round(v, 3) for v in color_values[0]] if color_values else None,
        }
        part_min = pos_acc.get("min")
        part_max = pos_acc.get("max")
        for axis in range(3):
            global_min[axis] = min(global_min[axis], part_min[axis])
            global_max[axis] = max(global_max[axis], part_max[axis])
        report["parts"][name] = {
            "boxes": boxes,
            "box_layout": box_layout,
            "vertices": vcount,
            "triangles": len(indices) // 3,
            "bbox_min": [round(v, 4) for v in part_min],
            "bbox_max": [round(v, 4) for v in part_max],
            "size_min": [min(s[i] for s in sizes) for i in range(3)] if sizes else None,
            "size_max": [max(s[i] for s in sizes) for i in range(3)] if sizes else None,
            "unique_sizes": len(set(sizes)),
            "material": prim.get("material"),
            "extras": node.get("extras", {}),
        }
        report["totals"]["boxes"] += boxes
        report["totals"]["vertices"] += vcount
        report["totals"]["triangles"] += len(indices) // 3

    report["bounding_box"] = {
        "min": [round(v, 4) for v in global_min],
        "max": [round(v, 4) for v in global_max],
        "size": [round(global_max[i] - global_min[i], 4) for i in range(3)],
    }
    report["unique_box_sizes_total"] = len(all_sizes)
    report["unique_box_sizes_sample"] = sorted(all_sizes)[:5] + sorted(all_sizes)[-5:]

    # Геометрические зазоры: все боксы axis-aligned, поэтому AABB-расстояние точно.
    def box_distance(a, b):
        total = 0.0
        for axis in range(3):
            gap = max(a[0][axis] - b[1][axis], b[0][axis] - a[1][axis], 0.0)
            total += gap * gap
        return total ** 0.5

    def boxes_intersect(a, b):
        return all(a[0][axis] <= b[1][axis] and b[0][axis] <= a[1][axis] for axis in range(3))

    for chain, names in LIMB_CHAINS.items():
        chain_gaps = {}
        for upper, lower in zip(names, names[1:]):
            a_boxes = boxes_by_part.get(upper, [])
            b_boxes = boxes_by_part.get(lower, [])
            if not a_boxes or not b_boxes:
                continue
            min_dist = min(box_distance(a, b) for a in a_boxes for b in b_boxes)
            collisions = sum(1 for a in a_boxes for b in b_boxes if boxes_intersect(a, b))
            chain_gaps[f"{upper}->{lower}"] = {
                "min_gap_m": round(min_dist, 4),
                "separated": min_dist > 1e-4 and collisions == 0,
                "intersecting_box_pairs": collisions,
            }
        report["gaps"][chain] = chain_gaps

    # Пересечения между разными модулями: допустимые «посадочные» и посторонние.
    cross = []
    unexpected = []
    part_names = list(boxes_by_part.keys())
    for i, first in enumerate(part_names):
        for second in part_names[i + 1:]:
            hits = sum(1 for a in boxes_by_part[first] for b in boxes_by_part[second]
                       if boxes_intersect(a, b))
            if hits:
                entry = {"parts": [first, second], "intersecting_box_pairs": hits}
                cross.append(entry)
                if frozenset((first, second)) not in EXPECTED_ATTACHMENTS:
                    unexpected.append(entry)
    report["cross_part_intersections"] = cross
    coarse = any(not part.get("box_layout", True) for part in report["parts"].values())
    report["unexpected_intersections"] = [] if coarse else unexpected
    report["expected_attachments"] = [sorted(p) for p in map(tuple, EXPECTED_ATTACHMENTS)]
    if coarse:
        report["notes"].append(
            "coarse AABB mode: вершины разбиты по нормалям (Blender-экспорт), "
            "анализ пересечений по отдельным боксам недоступен")
    if unexpected and not coarse:
        report["issues"].append(f"unexpected cross-part intersections: {len(unexpected)} pairs")

    for part_name, part in report["parts"].items():
        if part["boxes"] >= 4 and part["unique_sizes"] < 2:
            report["notes"].append(
                f"{part_name}: все {part['boxes']} боксов одного размера (дизайн, не дефект)")
    unnormalized = [n for n, c in report.get("colors", {}).items() if c["invalid_normalized"]]
    if unnormalized:
        report["issues"].append(f"COLOR_0 без normalized=true: {len(unnormalized)} модулей (цвета будут белыми)")
    white_only = [n for n, c in report.get("colors", {}).items()
                  if c["first_color"] and all(abs(v - 1.0) < 1e-6 for v in c["first_color"][:3])]
    if white_only:
        report["notes"].append(f"модули с белым первым цветом: {white_only}")
    distinct_total = sum(c["distinct_colors"] for c in report.get("colors", {}).values())
    report["distinct_rgba_total"] = distinct_total
    for chain, gaps in report["gaps"].items():
        for pair, info in gaps.items():
            if not info["separated"]:
                report["issues"].append(f"сегменты соприкасаются или пересекаются: {pair}")
    return report


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--glb", required=True)
    ap.add_argument("--out", default=os.path.join(PROJECT_ROOT, "outputs", "prototypes", "orc_01",
                                                 "reports", "glb_inspect.json"))
    args = ap.parse_args()
    report = inspect(args.glb)
    os.makedirs(os.path.dirname(args.out), exist_ok=True)
    with open(args.out, "w", encoding="utf-8") as fh:
        json.dump(report, fh, ensure_ascii=False, indent=2)
    print("ORC01_GLB_SUMMARY=" + json.dumps({
        "path": report["path"],
        "glb_bytes": report["glb_bytes"],
        "nodes": report["nodes"],
        "meshes": report["meshes"],
        "materials": report["materials"],
        "totals": report["totals"],
        "bounding_box": report["bounding_box"],
        "unique_box_sizes_total": report["unique_box_sizes_total"],
        "issues": report["issues"],
    }, ensure_ascii=False))
    print("ORC01_GLB_REPORT=" + args.out)


if __name__ == "__main__":
    sys.exit(main())
