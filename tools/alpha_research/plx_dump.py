"""Read-only decoder of the Alpha 2013 Plasma widget files (``gui.plx``, ``start.plx`` …) for research.

Evidence tool of M0 P6 (Alpha HUD / menus). It prints the template tree of named nodes with the bounding box of
every mesh relative to the node pivot, the fill / stroke colours and the text runs (string, pixel size, colour).
It never writes Alpha data anywhere; the output is text for the report.

Format (ALPHA_CODE_STATIC, recovered from Cube.exe):
  * ``GameController_load_widget_file`` 0x6539xx -> ``GameController_deserialize_widget_tree`` 0x6555D0;
  * the stream is a sequence of ``token, u32 length, payload`` blocks; ``StreamReader_readToken`` 0x6886xx reads a
    u32 id, where id 0 defines a new name (``u32 newId, u32 len, bytes``) before the real id follows;
  * once the ``Seal`` block is read, defined names are scrambled with ``decodeScrambledBuffer`` 0x6880C0:
    ``out[(i + seed % n) % n] = in[i] - key[((i + seed % n) % n) & 7]``, ``seed = u32(key[0:4])``;
  * the 8-byte key was recovered by known plaintext: the ``Seal`` payload must decode to ``PlasmaGraphics``
    (that is the check 0x6555D0 performs); key = ``5ccdc75e0b263afb`` (consistent for all 14 bytes).
  * property names are ``Widget.bindPos``, ``Node.transformation``, ``SmoothMeshShape.vertexPositions`` …
    (string compares in ``Widget_readProperty`` 0x6878xx).

    python tools/alpha_research/plx_dump.py "<Cube world>/gui.plx" hpbar mpbar abilitybutton
"""
from __future__ import annotations

import struct
import sys

KEY = bytes.fromhex("5ccdc75e0b263afb")


def unscramble(buf: bytes) -> bytes:
    n = len(buf)
    if n == 0:
        return b""
    seed = struct.unpack("<I", KEY[:4])[0]
    out = bytearray(n)
    for i in range(n):
        d = (i + seed % n) % n
        out[d] = (buf[i] - KEY[d & 7]) & 0xFF
    return bytes(out)


class Plx:
    def __init__(self, path: str):
        self.data = open(path, "rb").read()
        self.names: dict[int, str] = {}
        self.order: list[dict] = []
        self.objects: dict[str, list[dict]] = {}
        for token, start, length in self._parse(0, len(self.data)) or []:
            obj = self._object(start, length)
            obj["_type"] = self.names[token]
            self.order.append(obj)
            self.objects.setdefault(obj["_type"], []).append(obj)

    def _u32(self, at: int) -> int:
        return struct.unpack_from("<I", self.data, at)[0]

    def _token(self, at: int, end: int) -> tuple[int, int]:
        token = self._u32(at)
        at += 4
        while token == 0:
            block = self._u32(at)
            at += 4
            block_end = at + block
            if block_end > end:
                raise ValueError("name block overruns")
            new_id, length = self._u32(at), self._u32(at + 4)
            raw = self.data[at + 8:at + 8 + length]
            self.names[new_id] = raw.decode("latin1") if new_id == 1 else unscramble(raw).decode("latin1", "replace")
            at = block_end
            token = self._u32(at)
            at += 4
        return token, at

    def _parse(self, at: int, end: int):
        saved = dict(self.names)
        found = []
        try:
            while at < end:
                token, at = self._token(at, end)
                if token not in self.names:
                    raise ValueError("unknown token")
                length = self._u32(at)
                at += 4
                if at + length > end:
                    raise ValueError("block overruns")
                found.append((token, at, length))
                at += length
            if at != end:
                raise ValueError("trailing bytes")
            return found
        except (ValueError, struct.error):
            self.names.clear()
            self.names.update(saved)
            return None

    def _object(self, at: int, length: int) -> dict:
        obj: dict = {}
        for token, start, size in self._parse(at, at + length) or []:
            key = self.names[token].split(".", 1)[-1]
            nested = self._parse(start, start + size) if size >= 8 else None
            value = [self.data[s:s + n] for _, s, n in nested] if nested else self.data[start:start + size]
            obj.setdefault(key, []).append(value)
        return obj


def floats(raw: bytes) -> tuple:
    return struct.unpack("<%df" % (len(raw) // 4), raw[:len(raw) // 4 * 4])


def first(value):
    while isinstance(value, list):
        value = value[0] if value else b""
    return value


def wide(raw) -> str:
    raw = first(raw)
    if len(raw) < 4:
        return ""
    count = struct.unpack("<I", raw[:4])[0]
    return raw[4:4 + 2 * count].decode("utf-16le", "replace")


def i32(raw) -> int:
    return struct.unpack("<i", first(raw)[:4])[0]


def dump(path: str, wanted: list[str]) -> None:
    plx = Plx(path)
    nodes = plx.objects["Node"]
    transforms = plx.objects["Transformation"]
    shapes = [o for o in plx.order if o["_type"] in ("SmoothMeshShape", "TextShape")]

    def transform(index: int):
        t = transforms[index]
        return floats(first(t["translation"][0])), floats(first(t["pivot"][0])), floats(first(t["deformation"][0]))

    def apply(m, p):
        (tx, ty), (px, py), d = m[0][:2], m[1][:2], m[2]
        x, y = p[0] - px, p[1] - py
        return d[0] * x + d[4] * y + px + tx, d[1] * x + d[5] * y + py + ty

    def describe(shape_index: int):
        shape = shapes[shape_index]
        if shape["_type"] == "TextShape":
            color = floats(first(shape.get("color", [b""])))
            size = floats(first(shape.get("pixelSize", [b""])))
            return f"text {wide(shape.get('string', [b''])[0])!r} size={size} color={tuple(round(c, 2) for c in color)}", []
        positions = shape["vertexPositions"][0]
        count = i32(positions[0])
        xy = floats(positions[1])[:2 * count]
        colors = shape["vertexColors"][0]
        stroke = shape["strokeColors"][0]
        widths = shape["strokeWidths"][0]
        fill = tuple(round(c, 2) for c in floats(colors[1])[:4]) if len(colors) > 1 else ()
        line = tuple(round(c, 2) for c in floats(stroke[1])[:4]) if len(stroke) > 1 else ()
        width = floats(widths[1])[:1] if len(widths) > 1 else ()
        return f"mesh n={count} fill={fill} stroke={line} width={width}", [(xy[2 * k], xy[2 * k + 1]) for k in range(count)]

    def walk(index: int, chain: list, depth: int):
        node = nodes[index]
        own = transform(i32(node["transformation"][0]))
        chain = [own] + chain
        shape_index = i32(node["shape"][0])
        text, points = describe(shape_index) if shape_index >= 0 else ("", [])
        placed = []
        for p in points:
            for m in chain[:-1]:  # relative to the template node itself
                p = apply(m, p)
            placed.append(p)
        box = ""
        if placed:
            xs, ys = [p[0] for p in placed], [p[1] for p in placed]
            box = f" bbox x[{min(xs):.1f},{max(xs):.1f}] y[{min(ys):.1f},{max(ys):.1f}]"
        print("  " * depth + f"{wide(node['wname'][0])}: {text}{box}")
        for child in node.get("child", []):
            walk(i32(child), chain, depth + 1)

    for index, node in enumerate(nodes):
        name = wide(node["wname"][0])
        if name in wanted:
            translation, pivot, deform = transform(i32(node["transformation"][0]))
            print(f"=== {name} node#{index} pivot={tuple(round(v, 2) for v in pivot[:2])} "
                  f"scale={deform[0]:.3f},{deform[5]:.3f}")
            walk(index, [], 0)


if __name__ == "__main__":
    if len(sys.argv) < 3:
        print(__doc__)
        raise SystemExit(2)
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    dump(sys.argv[1], sys.argv[2:])
