"""Check the exported voxel surface and skin accessors."""
import collections
import json
from pathlib import Path
import struct

SOURCE = Path(__file__).resolve().parents[1] / "Staging" / "ElfRanger.gltf"


def validate(source):
    document = json.loads(source.read_text(encoding="utf-8"))
    binary = source.with_suffix(".bin").read_bytes()

    def accessor(index, spec):
        view = document["bufferViews"][document["accessors"][index]["bufferView"]]
        start = view["byteOffset"]
        return struct.iter_unpack("<" + spec, binary[start:start + view["byteLength"]])

    report = {}
    for primitive in document["meshes"][0]["primitives"]:
        attr = primitive["attributes"]
        name = document["materials"][primitive["material"]]["name"]
        positions = [tuple(round(c * 100_000) for c in p) for p in accessor(attr["POSITION"], "3f")]
        normals = list(accessor(attr["NORMAL"], "3f"))
        weights = list(accessor(attr["WEIGHTS_0"], "4f"))
        indices = [index for index, in accessor(primitive["indices"], "I")]
        edges = collections.Counter()
        triangles = set()
        reversed_or_degenerate = duplicate = 0
        for a, b, c in zip(indices[::3], indices[1::3], indices[2::3]):
            p, q, r = positions[a], positions[b], positions[c]
            ux, uy, uz = (v - w for w, v in zip(p, q))
            vx, vy, vz = (v - w for w, v in zip(p, r))
            cross = (uy * vz - uz * vy, uz * vx - ux * vz, ux * vy - uy * vx)
            reversed_or_degenerate += sum(x * y for x, y in zip(cross, normals[a])) <= 0
            triangle = tuple(sorted((p, q, r)))
            duplicate += triangle in triangles
            triangles.add(triangle)
            for start, end in ((p, q), (q, r), (r, p)):
                edges[tuple(sorted((start, end)))] += 1
        report[name] = {
            "triangles": len(indices) // 3,
            "open_edges": sum(n == 1 for n in edges.values()),
            "nonmanifold_edges": sum(n > 2 for n in edges.values()),
            "reversed_or_degenerate_triangles": reversed_or_degenerate,
            "duplicate_triangles": duplicate,
            "non_normalized_weights": sum(abs(sum(w) - 1) > 1e-5 for w in weights),
        }
    return report


if __name__ == "__main__":
    results = validate(SOURCE)
    print(json.dumps(results, indent=2))
    success = all(not n for m in results.values() for k, n in m.items() if k != "triangles")
    raise SystemExit(0 if success else 1)
