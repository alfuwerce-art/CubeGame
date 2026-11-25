"""Compare two exports of the female mannequin and check the protected mesh."""

import argparse
from collections import Counter, defaultdict
from hashlib import sha256
import json
from pathlib import Path
import struct

from validate_source import validate


ATTRIBUTES = (
    ("POSITION", "3f"), ("NORMAL", "3f"), ("TEXCOORD_0", "2f"),
    ("JOINTS_0", "4B"), ("WEIGHTS_0", "4f"),
)
EPSILON = .00002


def load_mesh(path):
    doc = json.loads(path.read_text(encoding="utf-8"))
    binary = path.with_name(doc["buffers"][0]["uri"]).read_bytes()

    def data(index):
        accessor = doc["accessors"][index]
        view = doc["bufferViews"][accessor["bufferView"]]
        offset = view["byteOffset"]
        return binary[offset:offset + view["byteLength"]]

    sections = {}
    for primitive in doc["meshes"][0]["primitives"]:
        name = doc["materials"][primitive["material"]]["name"]
        attributes = primitive["attributes"]
        vertices = tuple(zip(*(struct.iter_unpack("<" + fmt, data(attributes[key]))
                               for key, fmt in ATTRIBUTES)))
        indices = (index for index, in struct.iter_unpack("<I", data(primitive["indices"])))
        sections[name] = Counter(tuple(vertices[i] for i in triangle)
                                 for triangle in zip(indices, indices, indices))
    return doc, data(doc["skins"][0]["inverseBindMatrices"]), sections


def edit_zone(position):
    x, y, _ = position
    return abs(x) <= .115 + EPSILON and .225 - EPSILON <= y <= .53 + EPSILON


def measure(positions):
    waist = [x for x, y, _ in positions if abs(y - .35) < EPSILON]
    chest = [z for x, y, z in positions if abs(x) < .1 and .43 <= y <= .52]
    pelvis = [z for x, y, z in positions if abs(x) < .1 and .24 <= y <= .31]
    return {key: round(100 * value, 2) for key, value in (
        ("waist_width_cm", max(waist) - min(waist)),
        ("chest_front_cm", max(chest)),
        ("pelvis_rear_cm", -min(pelvis)),
    )}


def position_key(position):
    return tuple(round(value * 100_000) for value in position)


def extents(points, dimension):
    return min(p[dimension] for p in points), max(p[dimension] for p in points)


def shells(triangles):
    neighbors = defaultdict(set)
    for triangle in triangles:
        a, b, c = (position_key(vertex[0]) for vertex in triangle)
        neighbors[a].update((b, c))
        neighbors[b].update((a, c))
        neighbors[c].update((a, b))

    remaining = set(neighbors)
    components = []
    while remaining:
        first = remaining.pop()
        component = {first}
        visit = [first]
        while visit:
            adjacent = neighbors[visit.pop()] & remaining
            remaining.difference_update(adjacent)
            component.update(adjacent)
            visit.extend(adjacent)
        components.append(component)
    return sorted(components, key=len, reverse=True)


def reversed_edges(triangles):
    directed = Counter()
    for triangle, count in triangles.items():
        a, b, c = (position_key(vertex[0]) for vertex in triangle)
        for edge in ((a, b), (b, c), (c, a)):
            directed[edge] += count
    return sum(count != directed[b, a] for (a, b), count in directed.items())


def voxel_planes(triangles):
    """Check the geometry as well as the exported axis normals."""
    for triangle in triangles:
        normal = triangle[0][1]
        axes = [i for i, value in enumerate(normal) if abs(value) > EPSILON]
        if len(axes) != 1:
            return False
        axis = axes[0]
        if any(v[1] != normal or abs(v[0][axis] - triangle[0][0][axis]) > EPSILON
               for v in triangle):
            return False
    return True


def verify(before, after, *, voxel_head=False):
    old_doc, old_bind, old_parts = load_mesh(before)
    new_doc, new_bind, new_parts = load_mesh(after)
    for key in ("scene", "scenes", "nodes", "skins", "materials", "samplers", "textures"):
        assert old_doc[key] == new_doc[key], key
    assert old_bind == new_bind, "Inverse bind matrices changed"
    assert "animations" not in old_doc and "animations" not in new_doc, "Expected the T-pose export"
    assert list(old_parts) == list(new_parts) == ["BaseBody", "Head", "Ears"], "Sections changed"
    assert old_parts["Ears"] == new_parts["Ears"], "Ears changed"
    head_bounds = None
    if voxel_head:
        assert old_parts["Head"] != new_parts["Head"], "No head corner change"
        old_head, new_head = ([v[0] for t in part["Head"] for v in t] for part in (old_parts, new_parts))
        head_bounds = [extents(new_head, axis) for axis in range(3)]
        assert [extents(old_head, axis) for axis in range(3)] == head_bounds, "Head bounds changed"
        assert voxel_planes(new_parts["Head"]), "The revised head uses non-voxel faces"
    else:
        assert old_parts["Head"] == new_parts["Head"], "Head changed"
    old_texture = before.with_name(old_doc["images"][0]["uri"])
    new_texture = after.with_name(new_doc["images"][0]["uri"])
    assert old_texture.read_bytes() == new_texture.read_bytes(), "Atlas changed"
    assert before.with_name("grip.json").read_bytes() == after.with_name("grip.json").read_bytes()

    old_body, new_body = old_parts["BaseBody"], new_parts["BaseBody"]
    removed, added = old_body - new_body, new_body - old_body
    assert removed and added, "No torso change"
    edited = [vertex[0] for triangle in removed.keys() | added.keys() for vertex in triangle]
    assert all(map(edit_zone, edited)), "A triangle changed outside the torso and upper thigh zone"
    if voxel_head:
        assert voxel_planes(added), "A revised torso face is off the voxel axes"
        coordinates = {a for t in added for v in t for a in v[0]}
        assert all(abs(value - round(value / .0025) * .0025) <= EPSILON
                   for value in coordinates), "Torso vertices are off the quarter-centimeter grid"
        assert any(abs(value - round(value / .005) * .005) > EPSILON
                   for value in coordinates), "The smaller grid is missing"
    before_points, after_points = ([v[0] for tri in body for v in tri] for body in (old_body, new_body))
    assert extents(before_points, 0) == extents(after_points, 0), "Arm span changed"
    assert extents(before_points, 1) == extents(after_points, 1), "Body height changed"
    old_shells, new_shells = (shells(body) for body in (old_body, new_body))
    assert set(map(frozenset, old_shells[1:])) == set(map(frozenset, new_shells[1:])), (
        "Disconnected shells changed"
    )
    for changed, torso in ((removed, old_shells[0]), (added, new_shells[0])):
        assert all(position_key(v[0]) in torso for t in changed for v in t), "Torso is split"

    regions = {
        "arms_and_hands": lambda p: abs(p[0]) >= .13 - EPSILON,
        "neck_and_shoulders": lambda p: p[1] >= .535 - EPSILON,
        "legs_and_feet_below_pelvis": lambda p: p[1] <= .225 + EPSILON,
    }
    protected = {"Ears": sum(new_parts["Ears"].values())}
    if not voxel_head:
        protected["Head"] = sum(new_parts["Head"].values())
    for name, contains in regions.items():
        old, new = ({t: count for t, count in body.items() if all(contains(v[0]) for v in t)}
                    for body in (old_body, new_body))
        assert old == new, name
        protected[name] = sum(new.values())
    protected["BaseBody_outside_edit_zone"] = sum(
        count for triangle, count in new_body.items() if not all(edit_zone(v[0]) for v in triangle)
    )
    topology = validate(after)
    assert all(not count for stats in topology.values() for key, count in stats.items()
               if key != "triangles"), topology
    winding = {name: reversed_edges(part) for name, part in new_parts.items()}
    assert not any(winding.values()), winding

    def digest(path, texture):
        return {p.name: sha256(p.read_bytes()).hexdigest()
                for p in (path, path.with_suffix(".bin"), texture, path.with_name("grip.json"))}

    bottom, top = extents([v[0] for part in new_parts.values() for tri in part for v in tri], 1)
    report = {
        "sha256": {"before": digest(before, old_texture), "after": digest(after, new_texture)},
        "bind_joints_unchanged": len(new_doc["skins"][0]["joints"]),
        "global_height_cm": round(100 * (top - bottom), 2),
        "body_shells": {"before": len(old_shells), "after": len(new_shells)},
        "protected_triangles_identical_with_normals_uv_and_weights": protected,
        "changed_triangles": {"before": sum(removed.values()), "after": sum(added.values())},
        "edit_bounds_cm": [tuple(round(100 * value, 2) for value in extents(edited, dim))
                           for dim in range(3)],
        "measurements": {"before": measure(before_points), "after": measure(after_points)},
        "topology": topology,
        "opposite_edge_winding_errors": winding,
    }
    if head_bounds:
        report["head_bounds_identical_cm"] = [
            tuple(round(100 * value, 3) for value in axis) for axis in head_bounds
        ]
        report["torso_voxel_sizes_cm"] = [.5, .25]
    return report


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("before", type=Path)
    parser.add_argument("after", type=Path)
    parser.add_argument("--output", type=Path)
    parser.add_argument("--voxel-head", action="store_true",
                        help="Verify the revised voxel head with the original overall dimensions")
    args = parser.parse_args()
    report = json.dumps(verify(args.before, args.after, voxel_head=args.voxel_head), indent=2) + "\n"
    if args.output:
        args.output.write_text(report, encoding="utf-8")
    else:
        print(report, end="")
