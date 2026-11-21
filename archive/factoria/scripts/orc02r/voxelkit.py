#!/usr/bin/env python3
"""Factoria voxel toolkit: сетка вокселей, семантические уровни, слияние в кубоиды, GLB.

Используется новым процедурным генератором (ORC-02R). Никаких данных CubeGame
здесь нет: только примитивы построения и экспорт.

Соглашения:
  * 1 воксель = `voxel_m` метров (масштаб задаёт профиль);
  * x — влево/вправо (симметрия относительно плоскости x = 0);
  * y — вперёд/назад, ПЕРЕД = -Y (персонаж смотрит в -Y);
  * z — вверх, z = 0 — уровень пола;
  * каждый воксель несёт материал (цвет) и уровень детализации LARGE/MEDIUM/SMALL.
"""

from __future__ import annotations

import json
import struct
import zlib

LEVELS = ("LARGE", "MEDIUM", "SMALL")


class Sculpt:
    """Единый воксельный объём с разметкой частей тела.

    В отличие от отдельных Grid, объём строится непрерывно: трубы конечностей и
    торса проходят сквозь стыки, поэтому силуэт не ломается. Модульность
    получается разметкой (part) и последующей резкой одного слоя вокселей на
    стыке — зазор 1 воксель, как требует Factoria LEGO-style.
    """

    def __init__(self):
        self.cells: dict[tuple[int, int, int], tuple[str, str, str]] = {}

    # ------------------------------------------------------------ примитивы --
    def put(self, x: int, y: int, z: int, part: str, mat: str, level: str = "MEDIUM"):
        self.cells[(x, y, z)] = (mat, level, part)

    def box(self, x0: int, x1: int, y0: int, y1: int, z0: int, z1: int,
            part: str, mat: str, level: str = "MEDIUM"):
        x0, x1, y0, y1, z0, z1 = (int(round(v)) for v in (x0, x1, y0, y1, z0, z1))
        for x in range(min(x0, x1), max(x0, x1) + 1):
            for y in range(min(y0, y1), max(y0, y1) + 1):
                for z in range(min(z0, z1), max(z0, z1) + 1):
                    self.cells[(x, y, z)] = (mat, level, part)

    def carve(self, x0, x1, y0, y1, z0, z1):
        x0, x1, y0, y1, z0, z1 = (int(round(v)) for v in (x0, x1, y0, y1, z0, z1))
        for x in range(x0, x1 + 1):
            for y in range(y0, y1 + 1):
                for z in range(z0, z1 + 1):
                    self.cells.pop((x, y, z), None)

    def repaint(self, x0, x1, y0, y1, z0, z1, mat: str, level: str = "SMALL",
                parts=None):
        x0, x1, y0, y1, z0, z1 = (int(round(v)) for v in (x0, x1, y0, y1, z0, z1))
        n = 0
        for x in range(x0, x1 + 1):
            for y in range(y0, y1 + 1):
                for z in range(z0, z1 + 1):
                    cur = self.cells.get((x, y, z))
                    if cur is None:
                        continue
                    if parts and cur[2] not in parts:
                        continue
                    self.cells[(x, y, z)] = (mat, level, cur[2])
                    n += 1
        return n

    def section(self, z0: int, z1: int, w: float, d: float, part, mat: str,
                level: str = "MEDIUM", cx: float = 0.0, cy: float = 0.0, p: float = 4.0):
        rx, ry = max(w / 2.0, 0.5), max(d / 2.0, 0.5)
        x0, x1 = int(round(cx - rx - 0.5)), int(round(cx + rx + 0.5))
        y0, y1 = int(round(cy - ry - 0.5)), int(round(cy + ry + 0.5))
        for z in range(z0, z1 + 1):
            part_name = part(z) if callable(part) else part
            for x in range(x0, x1 + 1):
                nx = abs((x + 0.5 - cx) / rx)
                if nx > 1.0:
                    continue
                for y in range(y0, y1 + 1):
                    ny = abs((y + 0.5 - cy) / ry)
                    if (nx ** p + ny ** p) <= 1.0:
                        self.cells[(x, y, z)] = (mat, level, part_name)

    def tube(self, sections, part, mat: str, level: str = "MEDIUM", p: float = 4.0):
        for (z_a, w_a, d_a, cx_a, cy_a), (z_b, w_b, d_b, cx_b, cy_b) in zip(sections, sections[1:]):
            steps = z_b - z_a
            if steps <= 0:
                continue
            for i in range(steps):
                t = i / steps
                self.section(z_a + i, z_a + i,
                             w_a + (w_b - w_a) * t, d_a + (d_b - d_a) * t, part, mat, level,
                             cx_a + (cx_b - cx_a) * t, cy_a + (cy_b - cy_a) * t, p)
        z_last, w, d, cx, cy = sections[-1]
        self.section(z_last, z_last, w, d, part, mat, level, cx, cy, p)

    def ellipsoid(self, cx, cy, cz, rx, ry, rz, part: str, mat: str,
                  level: str = "MEDIUM", p: float = 2.6):
        for x in range(int(cx - rx - 1), int(cx + rx + 2)):
            for y in range(int(cy - ry - 1), int(cy + ry + 2)):
                for z in range(int(cz - rz - 1), int(cz + rz + 2)):
                    nx = abs((x + 0.5 - cx) / rx)
                    ny = abs((y + 0.5 - cy) / ry)
                    nz = abs((z + 0.5 - cz) / rz)
                    if nx ** p + ny ** p + nz ** p <= 1.0:
                        self.cells[(x, y, z)] = (mat, level, part)

    # ---------------------------------------------------------- преобразования --
    def mirror_side(self, side: str = "L") -> "Sculpt":
        """Создаёт противоположную сторону из уже построенной (L -> R)."""
        out = Sculpt()
        for (x, y, z), (mat, level, part) in self.cells.items():
            if part.endswith("_" + side):
                new_part = part[:-2] + ("_R" if side == "L" else "_L")
                out.cells[(-1 - x, y, z)] = (mat, level, new_part)
        return out

    def merge(self, other: "Sculpt"):
        self.cells.update(other.cells)

    def cut(self, part: str, z: int, mats=None, keep_mats=()):
        """Убирает весь слой вокселей части на стыке (полный зазор)."""
        dropped = 0
        for pos, val in [it for it in self.cells.items()
                         if it[1][2] == part and it[0][2] == z
                         and (mats is None or it[1][0] in mats)
                         and it[1][0] not in keep_mats]:
            del self.cells[pos]
            dropped += 1
        return dropped

    def cut_shell(self, part: str, z: int, keep_mats=(), depth: int = 1):
        """Срезает только НАРУЖНУЮ оболочку части на стыке, оставляя внутренний сердечник.

        Такой шов читается как канавка (как у модульных фигурок), а не как чёрная
        дыра на всю толщину конечности; части при этом остаются раздельными.
        """
        cells_at_z = {pos: val for pos, val in self.cells.items()
                      if val[2] == part and pos[2] == z and val[0] not in keep_mats}
        if not cells_at_z:
            return 0
        removed = 0
        for (x, y, _), _val in list(cells_at_z.items()):
            neighbours = sum(1 for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))
                             if (x + dx, y + dy, z) in cells_at_z)
            if neighbours < 4:
                del self.cells[(x, y, z)]
                removed += 1
        return removed

    def to_grids(self, order) -> dict[str, "Grid"]:
        grids = {}
        for name in order:
            grids[name] = Grid(name)
        for pos, (mat, level, part) in self.cells.items():
            if part not in grids:
                grids[part] = Grid(part)
            grids[part].cells[pos] = (mat, level)
        return grids


class Grid:
    """Плотная разреженная воксельная сетка одной части модели."""

    def __init__(self, name: str):
        self.name = name
        self.cells: dict[tuple[int, int, int], tuple[str, str]] = {}

    # ------------------------------------------------------------ примитивы --
    def put(self, x: int, y: int, z: int, mat: str, level: str = "MEDIUM"):
        self.cells[(x, y, z)] = (mat, level)

    def box(self, x0: int, x1: int, y0: int, y1: int, z0: int, z1: int,
            mat: str, level: str = "MEDIUM"):
        """Кубоид по включительным границам."""
        for x in range(min(x0, x1), max(x0, x1) + 1):
            for y in range(min(y0, y1), max(y0, y1) + 1):
                for z in range(min(z0, z1), max(z0, z1) + 1):
                    self.cells[(x, y, z)] = (mat, level)

    def carve(self, x0, x1, y0, y1, z0, z1):
        for x in range(x0, x1 + 1):
            for y in range(y0, y1 + 1):
                for z in range(z0, z1 + 1):
                    self.cells.pop((x, y, z), None)

    def repaint(self, x0, x1, y0, y1, z0, z1, mat, level="SMALL"):
        """Перекрасить только уже существующие воксели (без добавления новых)."""
        n = 0
        for x in range(x0, x1 + 1):
            for y in range(y0, y1 + 1):
                for z in range(z0, z1 + 1):
                    if (x, y, z) in self.cells:
                        self.cells[(x, y, z)] = (mat, level)
                        n += 1
        return n

    def section(self, z0: int, z1: int, w: float, d: float, mat: str, level: str = "MEDIUM",
                cx: float = 0.0, cy: float = 0.0, p: float = 4.0):
        """Слой от z0 до z1 включительно: суперэллипс (w x d) с центром (cx, cy).

        p=2 — эллипс, p≈4 — скруглённый прямоугольник, p>=8 — почти куб.
        """
        rx, ry = max(w / 2.0, 0.5), max(d / 2.0, 0.5)
        x0, x1 = int(round(cx - rx - 0.5)), int(round(cx + rx + 0.5))
        y0, y1 = int(round(cy - ry - 0.5)), int(round(cy + ry + 0.5))
        for z in range(z0, z1 + 1):
            for x in range(x0, x1 + 1):
                nx = abs((x + 0.5 - cx) / rx)
                if nx > 1.0:
                    continue
                for y in range(y0, y1 + 1):
                    ny = abs((y + 0.5 - cy) / ry)
                    if (nx ** p + ny ** p) <= 1.0:
                        self.cells[(x, y, z)] = (mat, level)

    def tube(self, sections, mat: str, level: str = "MEDIUM", p: float = 4.0):
        """Сегмент переменного сечения: sections = [(z, w, d, cx, cy), ...] по возрастанию z."""
        for (z_a, w_a, d_a, cx_a, cy_a), (z_b, w_b, d_b, cx_b, cy_b) in zip(sections, sections[1:]):
            steps = z_b - z_a
            if steps <= 0:
                continue
            for i in range(steps):
                t = i / steps
                z = z_a + i
                w = w_a + (w_b - w_a) * t
                d = d_a + (d_b - d_a) * t
                cx = cx_a + (cx_b - cx_a) * t
                cy = cy_a + (cy_b - cy_a) * t
                self.section(z, z, w, d, mat, level, cx, cy, p)
        z_last, w, d, cx, cy = sections[-1]
        self.section(z_last, z_last, w, d, mat, level, cx, cy, p)

    def ellipsoid(self, cx, cy, cz, rx, ry, rz, mat: str, level: str = "MEDIUM", p: float = 2.6):
        for x in range(int(cx - rx - 1), int(cx + rx + 2)):
            for y in range(int(cy - ry - 1), int(cy + ry + 2)):
                for z in range(int(cz - rz - 1), int(cz + rz + 2)):
                    nx = abs((x + 0.5 - cx) / rx)
                    ny = abs((y + 0.5 - cy) / ry)
                    nz = abs((z + 0.5 - cz) / rz)
                    if nx ** p + ny ** p + nz ** p <= 1.0:
                        self.cells[(x, y, z)] = (mat, level)

    # ------------------------------------------------------------ преобразования --
    def mirrored_x(self, name: str) -> "Grid":
        """Зеркальная копия относительно плоскости x = 0 (x -> -1 - x)."""
        out = Grid(name)
        for (x, y, z), (mat, level) in self.cells.items():
            out.cells[(-1 - x, y, z)] = (mat, level)
        return out

    def stats(self) -> dict:
        if not self.cells:
            return {"voxels": 0, "by_level": {}, "bbox_voxels": None}
        xs = [k[0] for k in self.cells]
        ys = [k[1] for k in self.cells]
        zs = [k[2] for k in self.cells]
        by_level = {lv: 0 for lv in LEVELS}
        for _, level in self.cells.values():
            by_level[level] = by_level.get(level, 0) + 1
        return {
            "voxels": len(self.cells),
            "by_level": by_level,
            "bbox_voxels": {"x": [min(xs), max(xs)], "y": [min(ys), max(ys)], "z": [min(zs), max(zs)]},
        }


# ------------------------------------------------------------------ слияние --
def greedy_merge(grid: Grid, max_box: int = 6):
    """Жадно сливает воксели в кубоиды одного материала и уровня детализации."""
    keyed: dict[tuple[str, str], set] = {}
    for pos, (mat, level) in grid.cells.items():
        keyed.setdefault((mat, level), set()).add(pos)
    boxes = []
    for (mat, level), remaining in sorted(keyed.items()):
        remaining = set(remaining)
        while remaining:
            start = min(remaining)
            size = [1, 1, 1]
            for axis in (0, 1, 2):
                while size[axis] < max_box:
                    ok = True
                    for a in range(size[0]):
                        for b in range(size[1]):
                            for c in range(size[2]):
                                q = [start[0] + a, start[1] + b, start[2] + c]
                                q[axis] = start[axis] + size[axis]
                                if tuple(q) not in remaining:
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
            boxes.append({"min": list(start), "size": size, "mat": mat, "level": level})
    return boxes


def module_boxes(modules: dict[str, Grid], max_box: int = 6):
    return {name: greedy_merge(grid, max_box) for name, grid in modules.items()}


# ----------------------------------------------------------------------- GLB --
def _tint(rgb, key: str, amplitude: float, seed: int):
    """Детерминированный микро-оттенок кубоида (не случайный scatter, а фактура)."""
    h = zlib.crc32((key + "|" + str(seed)).encode("utf-8")) & 0xFFFFFFFF
    # -1.0 .. 1.0
    t = ((h % 2000) / 1000.0) - 1.0
    f = 1.0 + t * amplitude
    return tuple(max(0, min(255, int(round(c * f)))) for c in rgb)


def build_glb(boxes_by_module: dict, palette: dict, voxel_m: float, generator: str,
              tint_amplitude: float = 0.05, seed: int = 0) -> bytes:
    """Пишет GLB 2.0: отдельный узел/меш на модуль, вертексные цвета (normalized)."""
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

    def add_view(payload: bytes) -> int:
        align()
        offset = len(binary)
        binary.extend(payload)
        views.append({"buffer": 0, "byteOffset": offset, "byteLength": len(payload), "target": 34962})
        return len(views) - 1

    for name, boxes in boxes_by_module.items():
        if not boxes:
            continue
        pos, col, idx = [], [], []
        for bi, box in enumerate(boxes):
            base = len(pos) // 3
            cx = box["min"][0] + box["size"][0] / 2.0
            cy = box["min"][1] + box["size"][1] / 2.0
            cz = box["min"][2] + box["size"][2] / 2.0
            # Центр модели по X — 0, пол по Z — 0; glTF: Y-up.
            for sx, sy, sz in signs:
                px = (cx + sx * box["size"][0] / 2.0) * voxel_m
                py = (cz + sz * box["size"][2] / 2.0) * voxel_m
                pz = -((cy + sy * box["size"][1] / 2.0) * voxel_m)
                pos.extend((px, py, pz))
                col.extend(_tint(palette[box["mat"]], f"{name}:{box['mat']}:{bi}",
                                 tint_amplitude, seed) + (255,))
            for tri in faces:
                idx.extend((base + tri[0], base + tri[1], base + tri[2]))
        xs, ys, zs = pos[0::3], pos[1::3], pos[2::3]
        pview = add_view(struct.pack("<%df" % len(pos), *pos))
        accessors.append({"bufferView": pview, "componentType": 5126, "count": len(pos) // 3,
                          "type": "VEC3", "min": [min(xs), min(ys), min(zs)],
                          "max": [max(xs), max(ys), max(zs)]})
        pos_acc = len(accessors) - 1
        cview = add_view(bytes(col))
        accessors.append({"bufferView": cview, "componentType": 5121, "count": len(col) // 4,
                          "type": "VEC4", "normalized": True})
        col_acc = len(accessors) - 1
        iview = add_view(struct.pack("<%dI" % len(idx), *idx))
        accessors.append({"bufferView": iview, "componentType": 5125, "count": len(idx),
                          "type": "SCALAR", "min": [0], "max": [len(pos) // 3 - 1]})
        idx_acc = len(accessors) - 1
        meshes.append({"name": name, "primitives": [{
            "attributes": {"POSITION": pos_acc, "COLOR_0": col_acc},
            "indices": idx_acc, "material": 0, "mode": 4}]})
        nodes.append({"name": name, "mesh": len(meshes) - 1,
                      "extras": {"body_part": name, "rig_ready": False}})

    doc = {
        "asset": {"version": "2.0", "generator": generator},
        "scene": 0, "scenes": [{"nodes": list(range(len(nodes)))}],
        "nodes": nodes, "meshes": meshes,
        "materials": [{"name": "orc_voxel_colors",
                       "pbrMetallicRoughness": {"baseColorFactor": [1, 1, 1, 1],
                                                "metallicFactor": 0.0, "roughnessFactor": 0.82},
                       "doubleSided": False}],
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
