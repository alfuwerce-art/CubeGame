"""Build the GEO-03 faceted carrier using the Factoria mannequin rig."""

import hashlib
import json
import math
from pathlib import Path
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
import mannequin_geo_02 as geo
import mannequin_geo_02r as previous
import mannequin_v3_build as rigging
import mannequin_v3_validate as posing

ROOT = geo.ROOT
PROFILE_DIR = ROOT / "scripts/character/profiles"
PROFILE = PROFILE_DIR / "mannequin_geo_03_male.json"
RUN = ROOT / "outputs/prototypes/mannequin_geo_03/run1"
BASELINE = RUN.parent / "baseline_geo_02r"
BLEND = "mannequin_geo_03.blend"
GLB = "mannequin_geo_03.glb"
SIDES = (("l", 1), ("r", -1))
SEAMS = (
    ("torso_upper", 0, "torso_pelvis", -1),
    ("upper_arm_{side}", -1, "forearm_{side}", 0),
    ("forearm_{side}", -1, "hand_{side}", 0),
    ("thigh_{side}", 0, "shin_{side}", -1),
    ("shin_{side}", 0, "foot_{side}", -1),
)


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8"))


def write_json(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def chamfered(a0, a1, b0, b1, cut):
    return [(a0 + cut, b0), (a1 - cut, b0), (a1, b0 + cut), (a1, b1 - cut),
            (a1 - cut, b1), (a0 + cut, b1), (a0, b1 - cut), (a0, b0 + cut)]


def surface(name, rings, mat, *, open_center=False):
    """Close a sequence of matching polygon loops. Store loop IDs for skinning and QA."""
    count = len(rings[0])
    vertices = [tuple(v) for ring in rings for v in ring]
    faces = [tuple(reversed(range(count)))]
    for row in range(len(rings) - 1):
        faces.extend((row * count + i, row * count + (i + 1) % count,
                      (row + 1) * count + (i + 1) % count, (row + 1) * count + i)
                     for i in range(count))
    end = (len(rings) - 1) * count
    if open_center:
        center = sum((Vector(p) for p in rings[-1]), Vector()) / count
        center.x = max(rings[-1], key=lambda p: abs(p[0]))[0] - math.copysign(.006, center.x)
        idx = len(vertices)
        vertices.append(tuple(center))
        faces.extend((end + i, end + (i + 1) % count, idx) for i in range(count))
    else:
        faces.append(tuple(range(end, end + count)))
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    mesh.materials.append(mat)
    obj["body_part_id"] = name
    obj["section_size"] = count
    obj["section_count"] = len(rings)
    return obj


def vertical_sections(rows, sign, foot):
    for z, cx, front, back, half, cut in rows:
        ring = chamfered(-half, half, front, back, cut)
        if foot and z <= .103:
            ring = [(x * (0.76 + .24 * (back - y) / (back - front)), y) for x, y in ring]
        yield [(sign * (cx + x), y, z) for x, y in ring]


def horizontal_sections(rows, sign):
    for x, front, back, low, high, cut in rows:
        yield [(sign * x, y, z) for y, z in chamfered(front, back, low, high, cut)]


def ear_surface(spec, side, sign, mat):
    origin = Vector(spec["origin"])
    outward = Vector(spec["outward"]).normalized()
    across = Vector((-outward.y, outward.x, 0))
    up = Vector((0, 0, 1))
    outline = spec["outline"]
    rings = []
    for shape, depths in (
        (.78, [-.014] * len(outline)), (1.0, spec["rim_depth"]),
        (.73, [d + .005 for d in spec["rim_depth"]]), (.50, spec["cavity_depth"]),
    ):
        rings.append([])
        for (u, v), depth in zip(outline, depths):
            point = origin + across * (u * shape) + up * (v * shape) + outward * depth
            rings[-1].append((sign * point.x, point.y, point.z))
    return surface("ear_" + side, rings, mat, open_center=True)


def finger_ring(center, along, width, depth):
    across = Vector((0, 1, 0))
    across = (across - along * along.dot(across)).normalized()
    up = along.cross(across).normalized()
    return [center + across * y + up * z for y, z in
            chamfered(-width / 2, width / 2, -depth / 2, depth / 2, min(width, depth) * .18)]


def fingers(profile, side, sign, mat, objects, weights, bones):
    for name, spec in profile["fingers"].items():
        start, joint, tip = map(Vector, spec["points"])
        first, second = (joint - start).normalized(), (tip - joint).normalized()
        w, h = spec["width"], spec["depth"]
        first_id, second_id = [f"{name}_{i:02d}_{side}" for i in (1, 2)]
        reflected = lambda p: (sign * p[0], p[1], p[2])
        hinge = finger_ring(joint, (first + second).normalized(), w[1], h[1])
        phalanges = (
            (first_id, start, joint, "hand_" + side, [
                finger_ring(start - first * .009, first, .84 * w[0], .83 * h[0]),
                finger_ring(start.lerp(joint, .35), first, w[0], h[0]),
                finger_ring(start.lerp(joint, .72), first, w[1], h[1]), hinge,
            ]),
            (second_id, joint, tip, first_id, [
                hinge, finger_ring(joint.lerp(tip, .29), second, w[1] * .95, h[1] * .94),
                finger_ring(tip - second * .007, second, w[2], h[2]),
                finger_ring(tip, second, .77 * w[2], .72 * h[2]),
            ]),
        )
        for part, head, tail, parent, rings in phalanges:
            objects[part] = surface(part, [[reflected(p) for p in ring] for ring in rings], mat)
            bones.append(dict(name=part, head=reflected(head), tail=reflected(tail), parent=parent,
                              connected=part == second_id, deform=True))
        weights[first_id] = {0: {"hand_" + side: .75, first_id: .25}, -1: {first_id: .5, second_id: .5}}
        weights[second_id] = {0: {first_id: .5, second_id: .5}}


def geometry(profile, mat):
    objects, weights = {}, {}
    for part, rows in profile["z_sections"].items():
        if part in ("thigh", "shin", "foot"):
            for side, sign in SIDES:
                name = part + "_" + side
                objects[name] = surface(name, list(vertical_sections(rows, sign, part == "foot")), mat)
        else:
            objects[part] = surface(part, list(vertical_sections(rows, 1, False)), mat)
    bones = read_json(PROFILE_DIR / profile["rig_profile"])["bones"]
    extras = {}
    for side, sign in SIDES:
        for part, rows in profile["x_sections"].items():
            name = part + "_" + side
            objects[name] = surface(name, list(horizontal_sections(rows, sign)), mat)
        ear = ear_surface(profile["ear"], side, sign, mat)
        objects[ear.name] = ear
        weights["upper_arm_" + side] = {
            0: {"spine_02": 1}, 1: {"spine_02": .55, "upperarm_" + side: .45},
        }
        weights["thigh_" + side] = {-1: {"pelvis": 1}, -2: {"pelvis": .45, "thigh_" + side: .55}}
        fingers(profile, side, sign, mat, objects, weights, bones)
        extras.update({b["name"]: b["name"] for b in bones[20:]})
    carriers = {**rigging.PART_BONE, **extras}
    for name, i, other, j in SEAMS:
        targets = [(name.format(side=side), other.format(side=side)) for side, _ in SIDES] \
            if "{side}" in name else [(name, other)]
        for a, b in targets:
            shared = {carriers[a]: .5, carriers[b]: .5}
            weights.setdefault(a, {})[i] = shared
            weights.setdefault(b, {})[j] = shared
    weights["neck"] = {0: {"spine_02": 1}, -1: {"head": 1}, -2: {"neck": .55, "head": .45}}
    return objects, bones, carriers, weights


def apply_skin(rig, objects, carriers, weights):
    for part, obj in objects.items():
        obj.parent = rig
        obj["body_part_bone"] = carriers[part]
        modifier = obj.modifiers.new("Armature", "ARMATURE")
        modifier.object = rig
        count, sections = obj["section_size"], obj["section_count"]
        rows = {i % sections: value for i, value in weights.get(part, {}).items()}
        for vertex in obj.data.vertices:
            values = rows.get(vertex.index // count, {carriers[part]: 1.0})
            for name, value in values.items():
                group = obj.vertex_groups.get(name) or obj.vertex_groups.new(name=name)
                group.add([vertex.index], value, "REPLACE")


def create_carrier(profile):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version = 0
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1
    scene.render.fps = 24
    scene.frame_start = 1
    scene.frame_end = 25
    material = rigging.make_material()
    material.diffuse_color = (.49, .48, .45, 1)
    shader = material.node_tree.nodes["Principled BSDF"]
    shader.inputs["Base Color"].default_value = (.49, .48, .45, 1)
    shader.inputs["Roughness"].default_value = .78
    objects, bones, carriers, weights = geometry(profile, material)
    rig = rigging.build_armature({"bones": bones})
    rig["geometry_revision"] = profile["profile"]
    apply_skin(rig, objects, carriers, weights)
    bpy.context.view_layer.update()
    return rig, objects


def main():
    profile = read_json(PROFILE)
    RUN.mkdir(parents=True, exist_ok=True)
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if args == ["draft"]:
        import mannequin_geo_03_review as review
        rig, objects = create_carrier(profile)
        draft = RUN.parent / "geometry_study"
        draft.mkdir(exist_ok=True)
        review.render(rig, profile, draft, draft=True)
        return
    import mannequin_geo_03_pipeline as pipeline
    if args == ["build"]:
        pipeline.build(profile)
    elif args == ["validate"]:
        pipeline.validate(profile)
    elif args in (["render"], ["render", "--baseline"]):
        import mannequin_geo_03_review as review
        old = "--baseline" in args
        path = BASELINE / "mannequin_v3_rig.blend" if old else RUN / BLEND
        bpy.ops.wm.open_mainfile(filepath=str(path))
        review.render(bpy.data.objects["mannequin_v3_rig"], profile,
                      RUN / ("baseline_renders" if old else "renders"), baseline=old)
    else:
        raise ValueError("Pass -- draft | build | validate | render [--baseline]")


if __name__ == "__main__":
    sys.modules["mannequin_geo_03"] = sys.modules[__name__]
    main()
