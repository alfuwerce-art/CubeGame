"""Validate the actual carrier surfaces and their Blender/GLB animation."""

import itertools
import math

import bmesh
import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree
from mathutils.kdtree import KDTree

import mannequin_geo_03 as carrier
import mannequin_geo_03_pipeline as pipeline

BODY_ORIGINAL = tuple(carrier.rigging.PART_BONE)
TOLERANCE_M = 2e-5
RAY_DIRECTIONS = [Vector(p).normalized() for p in ((1, .371, .217), (-.43, 1, .613), (.71, -.29, 1))]


def current_model():
    meshes = {obj.name: obj for obj in bpy.context.scene.objects if obj.type == "MESH"}
    rig, = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
    sockets = {obj["socket_id"]: obj for obj in bpy.context.scene.objects if "socket_id" in obj}
    return rig, meshes, sockets


def pose(rig, clip=None, frame=0):
    if clip is None:
        carrier.posing.apply_frame(rig, None, 1)
        return
    action = bpy.data.actions[clip]
    carrier.previous.set_action(rig, action)
    absolute = action.frame_range[0] + frame
    bpy.context.scene.frame_set(math.floor(absolute), subframe=absolute % 1)
    bpy.context.view_layer.update()


def positions(meshes, triangulate=False):
    graph = bpy.context.evaluated_depsgraph_get()
    result = {}
    for name, obj in meshes.items():
        ev = obj.evaluated_get(graph)
        data = ev.to_mesh()
        points = [ev.matrix_world @ v.co for v in data.vertices]
        triangles = []
        if triangulate:
            data.calc_loop_triangles()
            triangles = [tuple(t.vertices) for t in data.loop_triangles]
        result[name] = (points, triangles)
        ev.to_mesh_clear()
    return result


def point_delta(a, b):
    def one_way(source, target):
        tree = KDTree(len(target))
        for i, point in enumerate(target):
            tree.insert(point, i)
        tree.balance()
        return max(tree.find(point)[2] for point in source)
    return max(one_way(a, b), one_way(b, a))


def bone_data(rig, names):
    return {name: (tuple(b.head_local), tuple(b.tail_local), b.parent.name if b.parent else None,
                   b.use_connect, b.use_deform, tuple(tuple(row) for row in b.matrix_local))
            for name in names for b in (rig.data.bones[name],)}


def base_rotations(rig, names):
    snapshots = {}
    for clip, count in pipeline.BASE_CLIPS.items():
        for frame in range(count):
            pose(rig, clip, frame)
            snapshots[clip, frame] = [rig.pose.bones[name].rotation_quaternion.copy() for name in names]
    return snapshots


def check_topology(rig, meshes):
    bad = []
    for name, obj in meshes.items():
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1e-6)
        manifold = (all(edge.is_manifold and edge.is_contiguous for edge in bm.edges)
                    and bm.calc_volume(signed=True) > 0
                    and all(face.calc_area() > 1e-9 for face in bm.faces))
        bm.free()
        if not manifold:
            bad.append((name, "surface"))
        armatures = [modifier.object for modifier in obj.modifiers if modifier.type == "ARMATURE"]
        names = [group.name for group in obj.vertex_groups]
        if armatures != [rig] or any(
            not v.groups or not 1 <= len(v.groups) <= 2
            or abs(sum(g.weight for g in v.groups) - 1) > 1e-5
            or any(names[g.group] not in rig.data.bones or g.weight <= 0 for g in v.groups)
            for v in obj.data.vertices
        ):
            bad.append((name, "skin"))
    return bad


def seam_indices(meshes, profile):
    pairs = list((part.format(side=side), i, other.format(side=side), j)
                 for part, i, other, j in carrier.SEAMS
                 for side in (("l", "r") if "{side}" in part else ("",)))
    pairs.extend((f"{digit}_01_{side}", -1, f"{digit}_02_{side}", 0)
                 for digit in profile["fingers"] for side, _ in carrier.SIDES)

    def indices(obj, section):
        n = obj["section_size"]
        start = (section % obj["section_count"]) * n
        return slice(start, start+n)
    return [(a, indices(meshes[a], i), b, indices(meshes[b], j)) for a, i, b, j in pairs]


def intentional_pairs(profile):
    pairs = [("head", "neck"), ("head", "ear_l"), ("head", "ear_r"), ("neck", "torso_upper"),
             ("torso_upper", "torso_pelvis")]
    for side, _ in carrier.SIDES:
        pairs.extend((a.format(s=side), b.format(s=side)) for a, b in (
            ("torso_upper", "upper_arm_{s}"), ("upper_arm_{s}", "forearm_{s}"),
            ("forearm_{s}", "hand_{s}"), ("torso_pelvis", "thigh_{s}"),
            ("thigh_{s}", "shin_{s}"), ("shin_{s}", "foot_{s}"),
        ))
        for digit in profile["fingers"]:
            pairs.extend(((f"hand_{side}", f"{digit}_01_{side}"),
                          (f"{digit}_01_{side}", f"{digit}_02_{side}")))
    return {frozenset(pair) for pair in pairs}


def overlaps(surfaces, forbidden):
    bounds = {key: pipeline.bounds(points) for key, (points, _) in surfaces.items()}
    trees = {}

    def tree(key):
        if key not in trees:
            points, triangles = surfaces[key]
            trees[key] = BVHTree.FromPolygons(points, triangles, all_triangles=True)
        return trees[key]

    results = []
    for a, b in forbidden:
        lo1, hi1 = bounds[a]
        lo2, hi2 = bounds[b]
        if any(min(p, q) - max(r, s) <= 1e-5 for p, q, r, s in zip(hi1, hi2, lo1, lo2)):
            continue
        ab = tree(a).overlap(tree(b))
        samples = []
        for key, other, axis in ((a, b, 0), (b, a, 1)):
            points, faces = surfaces[key]
            candidates = points if not ab else [points[i] for i in
                                                  {v for hit in ab for v in faces[hit[axis]]}]
            if ab:
                candidates.extend(sum((points[i] for i in face), Vector()) / 3
                                  for face in [faces[i] for i in {hit[axis] for hit in ab}])
            for point in candidates:
                closest, normal, _, distance = tree(other).find_nearest(point)
                if closest is None or distance <= 1e-5 or (point - closest).dot(normal) >= -1e-5:
                    continue
                exits = 0
                for direction in RAY_DIRECTIONS:
                    hit, facing, _, _ = tree(other).ray_cast(point, direction)
                    exits += hit is not None and facing.dot(direction) > 0
                if exits >= 2:
                    samples.append(distance)
        if ab or samples:
            results.append((a, b, round(max(samples, default=0)*1000, 4)))
    return results


def radius_of_triangle(points, half_length):
    """Distance to an axis parallel to local Y, clipped to the shaft end planes."""
    polygon = points
    for sign in (1, -1):
        clipped = []
        for end, start in zip(polygon, polygon[-1:] + polygon[:-1]):
            inside_start, inside_end = sign*start.y <= half_length, sign*end.y <= half_length
            if inside_start != inside_end:
                clipped.append(start.lerp(end, (sign*half_length - start.y) / (end.y-start.y)))
            if inside_end:
                clipped.append(end)
        polygon = clipped
    if not polygon:
        return math.inf
    polygon = [Vector((p.x, p.z)) for p in polygon]
    winding, distances = [], []
    for a, b in zip(polygon, polygon[1:] + polygon[:1]):
        edge = b - a
        winding.append(edge.cross(-a))
        t = min(1, max(0, -a.dot(edge) / edge.length_squared)) if edge.length_squared > 1e-14 else 0
        distances.append((a + edge*t).length)
    if (min(winding) >= -1e-9 and max(winding) > 1e-9
            or max(winding) <= 1e-9 and min(winding) < -1e-9):
        return 0
    return min(distances)


def measure_grip(profile, surfaces, sockets, clip, side):
    spec = profile["grip"]
    main, off = (sockets[key].matrix_world for key in ("MAIN_HAND", "OFF_HAND"))
    shape = (off if side == "l" else main).copy()
    radius, length = spec["radius_m"], spec["length_m"]
    alignment = None
    if clip in ("grip_small", "grip_large"):
        variant = spec["variants"][clip.split("_")[1]]
        dx, dy, dz = Vector(variant["center"]) - Vector(spec["center"])
        shape.translation += shape.to_3x3() @ Vector(((-1 if side == "l" else 1)*dx, -dy, dz))
        radius = variant["radius_m"]
    elif clip == "two_hand_grip":
        axis = main.to_3x3().col[1].normalized()
        second_axis = off.to_3x3().col[1].normalized()
        delta = off.translation - main.translation
        alignment = {"radial_mm": round(delta.cross(axis).length*1000, 5),
                     "axis_error": round(1 - abs(axis.dot(second_axis)), 7),
                     "separation_mm": round(delta.length*1000, 5)}
        shape = main.copy()
        shape.translation = (main.translation + off.translation) * .5
        length = .445
    inverse = shape.inverted()
    gaps = {}
    for part in (f"hand_{side}", *(f"{digit}_{number:02d}_{side}"
                                 for digit in profile["fingers"] for number in (1, 2))):
        points, triangles = surfaces[part]
        local = [inverse @ point for point in points]
        gaps[part] = 1000 * (min(radius_of_triangle([local[i] for i in tri], length / 2)
                                 for tri in triangles) - radius)
    fingers = {digit: round(min(gaps[f"{digit}_{i:02d}_{side}"] for i in (1, 2)), 4)
               for digit in profile["fingers"]}
    return fingers, round(min(gaps.values()), 4), alignment


def socket_landing(sockets, specs, meshes):
    worst = 0
    missed = []
    for key, anchor in sockets.items():
        if key in ("MAIN_HAND", "OFF_HAND"):
            continue
        spec = specs[key]
        obj = meshes[spec["part"]]
        local = obj.matrix_world.inverted() @ anchor.matrix_world
        direction = local.to_3x3().col[2].normalized()
        location = local.translation
        hit, point, _, _ = obj.ray_cast(location + direction*.02, -direction, distance=.05)
        if not hit:
            missed.append(key)
        else:
            worst = max(worst, (point - (location - direction*spec.get("offset_m", 0))).length)
    return missed, worst


def face_landing(sockets, meshes, spec):
    face = meshes["head"]
    uv = face.data.uv_layers.get("MANNEQUIN_UV") or face.data.uv_layers.active
    face_frame = face.matrix_world.inverted() @ sockets["FACE"].matrix_world
    width, height = spec["face_size_m"]
    errors = []
    for u, v in itertools.product((-.5, -.25, 0, .25, .5), repeat=2):
        center = face_frame @ Vector((u*width, v*height, 0))
        normal = face_frame.to_3x3().col[2].normalized()
        hit, point, facing, polygon = face.ray_cast(center + normal*.02, -normal, distance=.05)
        if not hit or not uv or facing.dot(normal) < .9999:
            errors.append([u, v, "missing planar face or UV"])
        elif abs((center-point).dot(normal) - spec["offset_m"]) > TOLERANCE_M:
            errors.append([u, v, "offset"])
        else:
            for i in face.data.polygons[polygon].loop_indices:
                vertex = face.data.vertices[face.data.loops[i].vertex_index].co
                expected = Vector((.5 + vertex.x*.72, .028 + (vertex.z - 1.17)*.72))
                coord = uv.uv[i].vector
                if (coord - expected).length > TOLERANCE_M:
                    errors.append([u, v, "UV mapping"])
                    break
    return errors


def clip_semantics(rig, meshes):
    centers = {}
    for name, clip, frame in (("idle", "idle", 0), ("walk", "walk", 0), ("pass", "walk", 4),
                              ("windup", "attack", 4), ("strike", "attack", 8)):
        pose(rig, clip, frame)
        centers[name] = {part: carrier.posing.centroid(verts)
                         for part, (verts, _) in positions(meshes).items()}
    idle, walk, passing, windup, strike = (centers[k] for k in ("idle", "walk", "pass", "windup", "strike"))
    return {
        "idle_arms_down": all(idle[f"hand_{s}"].z < 1.0 for s, _ in carrier.SIDES),
        "idle_upright": idle["head"].z > idle["neck"].z > idle["torso_upper"].z,
        "idle_hands_beside_body": all(abs(idle[f"hand_{s}"].x) < .45 for s, _ in carrier.SIDES),
        "walk_opposite_arm": walk["hand_l"].y > walk["hand_r"].y + .02,
        "walk_left_foot_ahead": walk["foot_l"].y < walk["foot_r"].y - .05,
        "walk_pass_lift": passing["foot_r"].z > passing["foot_l"].z + .02,
        "attack_reach": windup["hand_r"].y > .25 and strike["hand_r"].y < -.25,
        "attack_swing": windup["hand_r"].y - strike["hand_r"].y > .60,
        "attack_lunge": abs(strike["foot_l"].y - strike["foot_r"].y) > .15,
    }


def grip_cases():
    yield from (("grip_test", 18, side) for side, _ in carrier.SIDES)
    yield from (("attack_grip", i, "r") for i in range(15))
    yield from ((clip, frame, side) for clip in ("two_hand_grip", "grip_small", "grip_large")
                for frame in (0, 4, 8) for side, _ in carrier.SIDES)


def samples():
    yield None, 0
    for clip, count in pipeline.CLIPS.items():
        for i in range(2*(count-1) + 1):
            yield clip, i/2


def run(profile):
    run_dir = carrier.RUN
    report = {"profile": profile["profile"], "tests": {}, "metrics": {},
              "sha256": {key: carrier.sha256(run_dir / file) for key, file in (
                  ("blend", carrier.BLEND), ("glb", carrier.GLB))}}
    metrics = report["metrics"]

    def check(name, ok, detail):
        report["tests"][name] = {"passed": bool(ok), "detail": detail}
        print(f"{'PASS' if ok else 'FAIL'} {name} {detail}", flush=True)

    baseline = carrier.BASELINE / "mannequin_v3_rig.blend"
    bpy.ops.wm.open_mainfile(filepath=str(baseline))
    old_rig, old_meshes, old_sockets = current_model()
    names = [spec["name"] for spec in carrier.read_json(carrier.PROFILE_DIR / profile["rig_profile"])["bones"]]
    old_bones = bone_data(old_rig, names)
    old_rotations = base_rotations(old_rig, names)
    old_names = {bone.name for bone in old_rig.data.bones}
    old_parts, old_anchors = set(old_meshes), set(old_sockets)

    bpy.ops.wm.open_mainfile(filepath=str(run_dir / carrier.BLEND))
    rig, meshes, sockets = current_model()
    specs = carrier.read_json(run_dir / "module_sockets.json")["sockets"]
    check("inventory", set(meshes) == old_parts and set(sockets) == old_anchors
          and {bone.name for bone in rig.data.bones} == old_names,
          {"parts": len(meshes), "sockets": len(sockets), "bones": len(rig.data.bones)})
    check("base_bind_pose", bone_data(rig, names) == old_bones, "20 rest matrices, head/tail, flags and parents")
    new_rotations = base_rotations(rig, names)
    rotation_error = max(min((a-b).magnitude, (a+b).magnitude)
                         for key in old_rotations for a, b in zip(old_rotations[key], new_rotations[key]))
    check("base_animation", rotation_error <= 1e-7, {"sampled_rotations": 1140, "max_component_norm": rotation_error})
    times = {a.name: tuple(a.frame_range) for a in bpy.data.actions}
    check("clip_intervals", times == {name: (1.0, float(count)) for name, count in pipeline.CLIPS.items()}, times)
    pose(rig)
    error = check_topology(rig, meshes)
    check("clean_weighted_meshes", not error, error)
    rest = pipeline.bounds(v.co for obj in meshes.values() for v in obj.data.vertices)
    check("rest_pose_scale_and_support", abs(rest[1][2] - 1.8) < TOLERANCE_M and abs(rest[0][2]) < TOLERANCE_M, rest)
    missed, error = socket_landing(sockets, specs, meshes)
    check("socket_surface_landing", not missed and error <= TOLERANCE_M, {"missed": missed, "max_m": error})
    face_errors = face_landing(sockets, meshes, specs["FACE"])
    check("face_planar_uv", not face_errors, {"samples": 25, "errors": face_errors})
    ear_swap, error = carrier.previous.test_ear_swap(rig, meshes)
    check("replaceable_ears", ear_swap and error < TOLERANCE_M, {"max_m": error, "yaw_deg": [-28, 0, 28]})
    check("independent_coverage", carrier.geo.check_segment_visibility({"sockets": specs}, rig, meshes),
          "Hide declared body parts by ID while head and rig remain visible")
    semantics = clip_semantics(rig, meshes)
    check("base_clip_semantics", all(semantics.values()), semantics)

    seams = seam_indices(meshes, profile)
    allowed = intentional_pairs(profile)
    forbidden = [(a, b) for a,b in itertools.combinations(meshes, 2) if frozenset((a, b)) not in allowed]
    largest_seam, max_socket = 0, 0
    collisions, grip_results, all_poses, support = [], {}, {}, {}
    cases = set(grip_cases())
    for clip, frame in samples():
        pose(rig, clip, frame)
        surfaces = positions(meshes, triangulate=True)
        all_poses[clip, frame] = {key: points for key, (points, _) in surfaces.items()}
        largest_seam = max(largest_seam, max(point_delta(surfaces[a][0][i], surfaces[b][0][j])
                                           for a,i,b,j in seams))
        collisions.extend([clip, frame, *hit] for hit in overlaps(surfaces, forbidden))
        for side, _ in carrier.SIDES:
            if (clip, frame, side) in cases:
                key = f"{clip}:{frame:g}:{side}"
                grip_results[key] = measure_grip(profile, surfaces, sockets, clip, side)
        for key, anchor in sockets.items():
            expected = rig.matrix_world @ rig.pose.bones[specs[key]["bone"]].matrix @ Matrix(specs[key]["bone_local_matrix"])
            max_socket = max(max_socket, carrier.geo.matrix_error(expected, anchor.matrix_world))
        support[clip, frame] = carrier.rigging.foot_min_z(meshes)
    critical = [item for item in collisions if item[-1] > 1.0]
    metrics["non_neighbour_intersections"] = collisions
    check("interpart_clearance", not critical,
          {"critical_samples_over_1mm": len(critical), "worst": sorted(critical, key=lambda x: -x[-1])[:15]})
    check("deformed_shared_joint_rings", largest_seam <= TOLERANCE_M,
          {"sampled_poses": len(all_poses), "seams": len(seams), "maximum_gap_m": largest_seam})
    check("animated_socket_binding", max_socket <= TOLERANCE_M, {"max_matrix_error": max_socket})
    failures = {key: info for key, info in grip_results.items() if
                info[1] < -1.2 or any(gap > 7 for gap in info[0].values())
                or info[2] and (info[2]["radial_mm"] > .1 or info[2]["axis_error"] > 1e-5)}
    metrics["grip_mm"] = grip_results
    check("finite_shaft_holds", not failures, {"samples": len(grip_results), "errors": failures})

    baseline_walk = []
    for i in range(257):
        pose(rig, "walk", i/16)
        baseline_walk.append(carrier.rigging.foot_min_z(meshes))
    min_ground, max_ground = min(support.values()), max(support.values())
    check("support_and_walk_subframes", min(min_ground, min(baseline_walk)) >= -TOLERANCE_M
          and max(max_ground, max(baseline_walk)) < .001,
          {"pose_min_max_mm": [min_ground*1000, max_ground*1000], "walk_samples": len(baseline_walk),
           "walk_min_max_mm": [min(baseline_walk)*1000, max(baseline_walk)*1000]})

    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(run_dir / carrier.GLB), disable_bone_shape=True, merge_vertices=True)
    rig, meshes, sockets = current_model()
    pose(rig)
    error = check_topology(rig, meshes)
    check("glb_parts_skin_and_inventory", set(meshes) == old_parts and set(sockets) == old_anchors
          and {bone.name for bone in rig.data.bones} == old_names and not error, error)
    fps = bpy.context.scene.render.fps / bpy.context.scene.render.fps_base
    durations = {a.name: (a.frame_range[1]-a.frame_range[0])/fps for a in bpy.data.actions}
    check("glb_action_durations", durations.keys() == pipeline.CLIPS.keys() and all(
        abs(sec - (pipeline.CLIPS[clip]-1)/24) < TOLERANCE_M for clip, sec in durations.items()), durations)
    missed, error = socket_landing(sockets, specs, meshes)
    check("glb_socket_rest", not missed and error <= TOLERANCE_M, {"missed": missed, "max_m": error})
    face_errors = face_landing(sockets, meshes, specs["FACE"])
    check("glb_face_uv", not face_errors, {"errors": face_errors})
    worst_shape, worst_socket = (0, ""), 0
    for (clip, frame), expected in all_poses.items():
        pose(rig, clip, frame*fps/24)
        actual = positions(meshes)
        for key, points in expected.items():
            worst_shape = max(worst_shape, (point_delta(points, actual[key][0]), f"{clip}:{frame}:{key}"))
        for key, anchor in sockets.items():
            socket = rig.matrix_world @ rig.pose.bones[specs[key]["bone"]].matrix @ Matrix(specs[key]["bone_local_matrix"])
            worst_socket = max(worst_socket, carrier.geo.matrix_error(socket, anchor.matrix_world))
    check("glb_pose_and_socket_roundtrip", worst_shape[0] < TOLERANCE_M and worst_socket < TOLERANCE_M,
          {"samples": len(all_poses), "max_vertex_error_m": worst_shape, "max_socket_error": worst_socket})
    imported_walk = []
    for i in range(257):
        pose(rig, "walk", i/16*fps/24)
        imported_walk.append(carrier.rigging.foot_min_z(meshes))
    check("glb_walk_support_roundtrip", min(imported_walk) >= -TOLERANCE_M and max(imported_walk) < .001,
          {"samples": len(imported_walk), "range_mm": [min(imported_walk)*1000, max(imported_walk)*1000], "max_bake_delta_mm": 1000*max(abs(a-b) for a,b in zip(baseline_walk, imported_walk))})
    check("source_hashes_unchanged", report["sha256"] == {
        "blend": carrier.sha256(run_dir / carrier.BLEND), "glb": carrier.sha256(run_dir / carrier.GLB)},
        report["sha256"])
    report["result"] = "PASS" if all(item["passed"] for item in report["tests"].values()) else "FAIL"
    carrier.write_json(run_dir / "validation.json", report)
    if report["result"] != "PASS":
        raise ValueError("GEO-03 validation failed; see validation.json")
