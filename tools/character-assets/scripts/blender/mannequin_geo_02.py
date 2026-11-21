"""Build or validate the isolated MANNEQUIN-GEO-02 revision with Blender."""

import json
from pathlib import Path
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
import mannequin_v3_build as build
import mannequin_v3_validate as validate

ROOT = Path(build.ROOT)
PROFILE = ROOT / "scripts/character/profiles/mannequin_geo_02_male.json"
RUN = ROOT / "outputs/prototypes/mannequin_geo_02/run1"
AXES = ("x", "y", "z")
NORMALS = {
    "front": (0, -1, 0), "back": (0, 1, 0), "top": (0, 0, 1),
    "left": (1, 0, 0), "right": (-1, 0, 0), "grip": (0, 0, 1),
}
GROUPS = set("FACE SCALP BEARD EAR CHEST SHOULDERS ARMS WAIST LEGS FEET BACK MAIN_HAND OFF_HAND".split())
CLIPS = {"idle": 25, "walk": 17, "attack": 15}
TOLERANCE_M = 0.0001


def prepare(profile):
    baseline = json.loads(PROFILE.with_name(profile["baseline"]).read_text(encoding="utf-8"))
    if set(profile["parts"]) != set(baseline["parts"]):
        raise ValueError("Part IDs differ from the baseline")
    profile["bones"] = baseline["bones"]
    profile["units"] = baseline["units"]
    profile["up_axis"] = baseline["up_axis"]
    profile["forward_axis"] = baseline["forward_axis"]
    profile["height_m"] = baseline["height_m"]
    profile["union_blocks"] = True
    return profile


def socket_matrix(spec, profile):
    block = next(b for b in profile["parts"][spec["part"]] if b["source_block"] == spec["block"])
    origin = Vector([block[a] + block["s" + a] * f for a, f in zip(AXES, spec["at"])])
    normal = Vector(NORMALS[spec["orientation"]])
    up = Vector({"top": (0, 1, 0), "grip": (0, -1, 0)}.get(spec["orientation"], (0, 0, 1)))
    right = up.cross(normal)
    mat = Matrix((right, up, normal)).transposed().to_4x4()
    mat.translation = origin + normal * spec.get("offset_m", 0)
    return mat


def matrix_rows(matrix):
    return [[round(value, 7) for value in row] for row in matrix]


def bounds_in_frame(boxes, world_to_frame):
    points = [
        world_to_frame @ Vector((x, y, z))
        for box in boxes
        for x in (box["x"] - box["sx"] / 2, box["x"] + box["sx"] / 2)
        for y in (box["y"] - box["sy"] / 2, box["y"] + box["sy"] / 2)
        for z in (box["z"] - box["sz"] / 2, box["z"] + box["sz"] / 2)
    ]
    return [[round(op(p[i] for p in points), 6) for i in range(3)] for op in (min, max)]


def face_uv(obj, plane_y):
    """Allocate the lower atlas band to the flat front, at uniform texel density."""
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(island_margin=0.025)
    bpy.ops.object.mode_set(mode="OBJECT")
    obj.select_set(False)
    mesh = obj.data
    layer = mesh.uv_layers.active
    layer.name = "MANNEQUIN_UV"
    for poly in mesh.polygons:
        front = all(abs(mesh.vertices[i].co.y - plane_y) < 1e-5 for i in poly.vertices)
        for i in poly.loop_indices:
            co = mesh.vertices[mesh.loops[i].vertex_index].co
            u, v = layer.uv[i].vector
            layer.uv[i].vector = (
                (0.5 + co.x * 0.72, 0.028 + (co.z - 1.17) * 0.72)
                if front else (0.02 + 0.96 * u, 0.51 + 0.47 * v)
            )


def finish_model(profile, rig, objects, run_dir):
    sockets = profile["sockets"]
    if {spec["group"] for spec in sockets.values()} != profile.get("socket_groups", GROUPS):
        raise ValueError("Socket groups differ from the GEO-02 contract")

    rig["geometry_revision"] = profile["profile"]
    collection = bpy.data.collections.new("MODULE_SOCKETS")
    bpy.context.scene.collection.children.link(collection)
    data = {
        "profile": profile["profile"],
        "units": "m",
        "rest_axes": "+X anatomical left, -Y forward, +Z up",
        "socket_axes": "+Z outward and +Y up on vertical surfaces, +Y back on top, +Y along grip",
        "coverage_policy": "Hide only fully covered body parts by ID. Empty coverage leaves the body visible.",
        "sockets": {},
        "body_parts": {},
    }
    part_bones = profile.get("part_bones", build.PART_BONE)
    part_frames = profile.get("part_frames", {})
    for name, obj in objects.items():
        obj["body_part_id"] = name
        obj["body_part_bone"] = part_bones[name]
        data["body_parts"][name] = {
            "bone": part_bones[name],
            "rest_bounds_m": bounds_in_frame(profile["parts"][name], part_frames.get(name, Matrix.Identity(4))),
            "hideable_by": [key for key, spec in sockets.items() if name in spec["covers"]],
        }

    for name, spec in sockets.items():
        bone = rig.data.bones[spec["bone"]]
        if part_bones[spec["part"]] != bone.name or not set(spec["covers"]) <= objects.keys():
            raise ValueError(f"Inconsistent socket parts or bone: {name}")
        rest = socket_matrix(spec, profile)
        bone_local = bone.matrix_local.inverted() @ rest
        obj = bpy.data.objects.new("SOCKET_" + name, None)
        obj.empty_display_type = "PLAIN_AXES"
        obj.empty_display_size = 0.022
        collection.objects.link(obj)
        obj.parent = rig
        obj.parent_type = "BONE"
        obj.parent_bone = bone.name
        # Blender bone-parented objects start at the bone tip, not its head.
        obj.matrix_basis = Matrix.Translation((0, -bone.length, 0)) @ bone_local
        obj["socket_id"] = name
        obj["socket_group"] = spec["group"]
        obj["socket_bone"] = bone.name
        obj["socket_fit"] = spec["fit"]
        obj["socket_body_part"] = spec["part"]
        obj["coverable_part_ids"] = ",".join(spec["covers"])
        obj["geometry_revision"] = profile["profile"]
        entry = {
            **spec,
            "node": obj.name,
            "rest_matrix": matrix_rows(rest),
            "bone_local_matrix": matrix_rows(bone_local),
            "carrier_bounds_socket_m": bounds_in_frame(profile["parts"][spec["part"]], rest.inverted()),
        }
        if name == "FACE":
            plane_y = rest.translation.y + spec["offset_m"]
            face_uv(objects["head"], plane_y)
            width, height = spec["face_size_m"]
            z = rest.translation.z
            entry["uv_bounds"] = [
                [0.5 - width * 0.36, 0.028 + (z - height / 2 - 1.17) * 0.72],
                [0.5 + width * 0.36, 0.028 + (z + height / 2 - 1.17) * 0.72],
            ]
        data["sockets"][name] = entry
    bpy.context.view_layer.update()
    (Path(run_dir) / "module_sockets.json").write_text(
        json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )


def matrix_error(a, b):
    return max(abs(a[i][j] - b[i][j]) for i in range(4) for j in range(4))


def face_surface_error(profile, head):
    spec = profile["sockets"]["FACE"]
    rest = socket_matrix(spec, profile)
    width, height = spec["face_size_m"]
    worst = 0.0
    for u in (-0.5, -0.25, 0, 0.25, 0.5):
        for v in (-0.5, -0.25, 0, 0.25, 0.5):
            start = rest @ Vector((u * width, v * height, 0.02))
            world_to_head = head.matrix_world.inverted()
            ray = world_to_head.to_3x3() @ Vector((0, 1, 0))
            hit, point, normal, _ = head.ray_cast(world_to_head @ start, ray, distance=0.05)
            if not hit or normal.dot(-ray.normalized()) < 0.999:
                return None
            depth = (head.matrix_world @ point).y - rest.translation.y
            worst = max(worst, abs(depth - spec["offset_m"]))
    return worst


def check_landing_sites(profile, meshes):
    errors = []
    for name, spec in profile["sockets"].items():
        mesh = meshes[spec["part"]]
        rest = socket_matrix(spec, profile)
        inv = mesh.matrix_world.inverted()
        if spec["orientation"] == "grip" and spec.get("external_grip"):
            continue  # Physical external grip is checked with the QA cylinder and posed fingers.
        if spec["orientation"] == "grip":
            bounds = bounds_in_frame(profile["parts"][spec["part"]], Matrix.Identity(4))
            if not all(lo < value < hi for lo, value, hi in zip(bounds[0], rest.translation, bounds[1])):
                errors.append(name)
            continue
        local = inv @ rest
        normal = local.to_3x3().col[2].normalized()
        step = 0.020
        hit, point, facing, _ = mesh.ray_cast(local.translation + normal * step, -normal, distance=0.05)
        target = local.translation - normal * spec.get("offset_m", 0)
        if not hit or (point - target).length > TOLERANCE_M or facing.dot(normal) < 0.99:
            errors.append(name)
    return errors


def check_segment_visibility(profile, rig, meshes):
    covered = {name for spec in profile["sockets"].values() for name in spec["covers"]}
    if not covered <= meshes.keys():
        return False
    hidden = {mesh.name: mesh.hide_render for mesh in meshes.values()}
    for name in covered:
        meshes[name].hide_render = True
    ok = ({name for name, mesh in meshes.items() if mesh.hide_render} == covered
          and not rig.hide_render and not meshes["head"].hide_render
          and all(mesh.parent == rig for mesh in meshes.values()))
    for name, previous in hidden.items():
        meshes[name].hide_render = previous
    return ok



def inspect_model(profile, rig, meshes, check, report, roundtrip):
    bpy.context.view_layer.objects.active = rig
    validate.apply_frame(rig, None, 1)
    frame_name = "glb" if roundtrip else "blend"
    sockets = profile["sockets"]
    by_id = {obj.get("socket_id"): obj for obj in bpy.context.scene.objects if "socket_id" in obj}
    errors = []
    for name, spec in sockets.items():
        obj = by_id.get(name)
        if (obj is None or obj.type != "EMPTY" or obj.parent != rig or obj.parent_type != "BONE"
                or obj.parent_bone != spec["bone"] or obj.get("socket_bone") != spec["bone"]
                or obj.get("socket_group") != spec["group"] or obj.get("socket_fit") != spec["fit"]
                or obj.get("geometry_revision") != profile["profile"]
                or obj.get("coverable_part_ids") != ",".join(spec["covers"])):
            errors.append(name)
    max_rest = max((matrix_error(obj.matrix_world, socket_matrix(sockets[name], profile))
                    for name, obj in by_id.items() if name in sockets), default=1.0)
    check(frame_name + "_socket_rest", not errors and by_id.keys() == sockets.keys()
          and max_rest <= TOLERANCE_M, f"nodes={len(by_id)} max matrix error={max_rest:.2e} errors={errors}")

    part_bones = profile.get("part_bones", build.PART_BONE)
    weights_ok = all(
        obj.get("body_part_id") == name and obj.get("body_part_bone") == part_bones[name]
        and all(len([g for g in vertex.groups if g.weight > 0]) == 1 and
                all(obj.vertex_groups[g.group].name == part_bones[name] and abs(g.weight - 1) < 1e-5
                    for g in vertex.groups if g.weight > 0)
                for vertex in obj.data.vertices)
        for name, obj in meshes.items()
    )
    check(frame_name + "_coverage_ids", weights_ok and set(meshes) == set(profile["parts"]),
          f"{len(meshes)} independent segment IDs, each with its expected rigid bone")

    face_err = face_surface_error(profile, meshes["head"])
    uv = meshes["head"].data.uv_layers.active
    check(frame_name + "_face_plane", face_err is not None and face_err <= TOLERANCE_M and uv is not None,
          f"25 rays over FACE, maximum depth error={face_err} m, UV={uv is not None}")

    site_errors = check_landing_sites(profile, meshes)
    check(frame_name + "_landing_sites", not site_errors,
          f"surface rays and interior grip points for {len(sockets)} sockets, errors={site_errors}")

    if not roundtrip:
        check("independent_coverage", check_segment_visibility(profile, rig, meshes),
              "coverage candidates hide by object without hiding the head or armature")
        bone_errors = []
        for spec in profile["bones"]:
            bone = rig.data.bones[spec["name"]]
            if ((bone.head_local - Vector(spec["head"])).length > TOLERANCE_M
                    or (bone.tail_local - Vector(spec["tail"])).length > TOLERANCE_M
                    or bone.use_deform != spec["deform"]):
                bone_errors.append(bone.name)
        check("baseline_rest_bones", not bone_errors, f"{len(profile['bones'])} rest pivots and deform flags, errors={bone_errors}")

        bad_meshes = []
        for name, obj in meshes.items():
            bm = bmesh.new()
            bm.from_mesh(obj.data)
            if any(not e.is_manifold or not e.is_contiguous for e in bm.edges) or bm.calc_volume(signed=True) <= 0:
                bad_meshes.append(name)
            bm.free()
        check("closed_block_segments", not bad_meshes, f"closed and outward-oriented, errors={bad_meshes}")

    if errors or by_id.keys() != sockets.keys():
        return
    rests = {name: obj.matrix_world.copy() for name, obj in by_id.items()}
    max_motion_error = 0.0
    worst_gap = (-1.0, "", 0, "")
    worst_collision = (0.0, "", 0, "")
    ground_range = [0.0, 0.0]
    samples = report.setdefault("_socket_animation_samples", {})
    max_clip_error = 0.0
    for action, count in CLIPS.items():
        frames = range(1, count + 1) if not roundtrip else (1, (count + 1) // 2, count)
        probe_frames = (1, (count + 1) // 2, count)
        action_start = round(bpy.data.actions[action].frame_range[0])
        for frame in frames:
            validate.apply_frame(rig, action, frame + action_start - 1)
            transforms = validate.bone_matrices(rig)
            for name, obj in by_id.items():
                expected = rig.matrix_world @ transforms[sockets[name]["bone"]] @ rig.matrix_world.inverted() @ rests[name]
                max_motion_error = max(max_motion_error, matrix_error(obj.matrix_world, expected))
                if frame not in probe_frames:
                    continue
                key = f"{action}:{frame}:{name}"
                if roundtrip:
                    max_clip_error = max(max_clip_error, matrix_error(obj.matrix_world, Matrix(samples[key])))
                else:
                    samples[key] = matrix_rows(obj.matrix_world)
            if roundtrip:
                continue
            parts = {name: profile["parts"][name] for name in build.PART_BONE}
            for pair, gap in validate.joint_separations(parts, transforms).items():
                worst_gap = max(worst_gap, (gap, action, frame, pair))
            classified = report["poses"]["T_POSE"]["joint_insertion_mm"]
            for pair, depth in validate.part_penetrations(parts, transforms).items():
                a, b = pair.split("|")
                if pair in classified or build.PART_BONE[a] == build.PART_BONE[b]:
                    continue
                permitted = validate.TOLERATED.get((a, b), validate.TOLERATED.get((b, a), 0))
                if (a, b) in validate.JOINTS or (b, a) in validate.JOINTS:
                    continue
                worst_collision = max(worst_collision, (depth - permitted, action, frame, pair))
            z = min(v.z for n in ("foot_l", "foot_r") for v in validate.evaluated_vertices(meshes[n]))
            ground_range = [min(ground_range[0], z), max(ground_range[1], z)]
    check(frame_name + "_socket_motion", max_motion_error <= TOLERANCE_M,
          f"bone-relative transform, max matrix error={max_motion_error:.2e}")
    if roundtrip:
        check("glb_animation_match", max_clip_error <= TOLERANCE_M,
              f"{len(samples)} matched Blender/GLB socket transforms, max matrix error={max_clip_error:.2e}")
        del report["_socket_animation_samples"]
    else:
        stats = {"frames": sum(CLIPS.values()), "worst_joint_gap_m": worst_gap,
                 "worst_collision_over_allowance_m": worst_collision, "foot_min_z_range_m": ground_range}
        report["numbers"]["clip_sweep"] = stats
        check("clip_joint_sweep", worst_gap[0] <= 0.025 and worst_collision[0] < 0.005, json.dumps(stats))
    validate.apply_frame(rig, None, 1)


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if args == ["build"]:
        build.main(PROFILE, RUN, prepare, finish_model)
    elif args in (["validate"], ["validate", "--skip-renders"]):
        if not validate.main(PROFILE, RUN, prepare, inspect_model):
            raise RuntimeError("MANNEQUIN-GEO-02 validation failed. See validation.json.")
    else:
        raise ValueError("Pass -- build or -- validate [--skip-renders]")
