"""MANNEQUIN-RIG-01: build the male V3 block mannequin + minimal humanoid rig.

Reads `scripts/character/profiles/mannequin_v3_male.json` (generated from the approved
V3 reference measurements), creates 18 rigid block parts, a 20-bone humanoid armature
with pivots on block seams, rigid skinning (one bone per part, weight 1.0), three test
actions (idle / walk / attack), and writes .blend / .glb / build report.

Run (headless):
    & 'D:\\programs\\Blender\\5.2\\blender.exe' --background --factory-startup `
        --python-exit-code 1 --python scripts\\blender\\mannequin_v3_build.py

This is a prototype for rig validation, not a production character.
"""
from __future__ import annotations

import hashlib
import json
import math
import os
import sys
import time

import bpy
import bmesh
from mathutils import Matrix, Quaternion, Vector

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
PROFILE = os.path.join(ROOT, "scripts", "character", "profiles", "mannequin_v3_male.json")
RUN = os.path.join(ROOT, "outputs", "prototypes", "mannequin_v3_rig", "run1")
BLEND = os.path.join(RUN, "mannequin_v3_rig.blend")
GLB = os.path.join(RUN, "mannequin_v3_male_rig.glb")
REPORT = os.path.join(RUN, "build_report.json")
HIERARCHY_TXT = os.path.join(RUN, "rig_hierarchy.txt")

BEVEL_OFFSET = 0.004

# part -> deform bone (rigid skinning: exactly one bone per part, weight 1.0)
PART_BONE = {
    "torso_pelvis": "pelvis",
    "torso_upper": "spine_02",
    "neck": "neck",
    "head": "head",
    "ear_l": "head",
    "ear_r": "head",
    "upper_arm_l": "upperarm_l",
    "forearm_l": "lowerarm_l",
    "hand_l": "hand_l",
    "upper_arm_r": "upperarm_r",
    "forearm_r": "lowerarm_r",
    "hand_r": "hand_r",
    "thigh_l": "thigh_l",
    "shin_l": "calf_l",
    "foot_l": "foot_l",
    "thigh_r": "thigh_r",
    "shin_r": "calf_r",
    "foot_r": "foot_r",
}

AXES = {"X": Vector((1, 0, 0)), "Y": Vector((0, 1, 0)), "Z": Vector((0, 0, 1))}

report = {
    "stage": "MANNEQUIN-RIG-01/build",
    "status": "IN_PROGRESS",
    "blender": bpy.app.version_string,
    "profile": os.path.relpath(PROFILE, ROOT).replace("\\", "/"),
    "profile_sha256": None,
    "parts": {},
    "bones": [],
    "actions": {},
    "artifacts": {},
    "issues": [],
}


def log(msg):
    print(f"[build] {msg}")


def part_mesh(name, boxes):
    """One cube mesh per block, all blocks of a part in a single mesh object."""
    verts, faces = [], []
    for b in boxes:
        cx, cy, cz = b["x"], b["y"], b["z"]
        hx, hy, hz = b["sx"] / 2.0, b["sy"] / 2.0, b["sz"] / 2.0
        base = len(verts)
        for sx, sy, sz in ((-1, -1, -1), (1, -1, -1), (1, 1, -1), (-1, 1, -1),
                           (-1, -1, 1), (1, -1, 1), (1, 1, 1), (-1, 1, 1)):
            verts.append((cx + sx * hx, cy + sy * hy, cz + sz * hz))
        for q in ((0, 1, 2, 3), (7, 6, 5, 4), (0, 4, 5, 1), (1, 5, 6, 2),
                  (2, 6, 7, 3), (3, 7, 4, 0)):
            faces.append(tuple(base + i for i in q))
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.validate()
    mesh.update()
    return mesh


def union_block_mesh(name, boxes):
    """Extract the exterior of intersecting, axis-aligned blocks."""
    axes = ("x", "y", "z")
    bounds = [
        [(round(b[a] - b["s" + a] / 2, 6), round(b[a] + b["s" + a] / 2, 6))
         for a in axes]
        for b in boxes
    ]
    cuts = [sorted({v for box in bounds for v in box[i]}) for i in range(3)]
    occupied = {
        (i, j, k)
        for box in bounds
        for i in range(cuts[0].index(box[0][0]), cuts[0].index(box[0][1]))
        for j in range(cuts[1].index(box[1][0]), cuts[1].index(box[1][1]))
        for k in range(cuts[2].index(box[2][0]), cuts[2].index(box[2][1]))
    }
    faces = []
    for cell in sorted(occupied):
        for axis in range(3):
            u, v = (axis + 1) % 3, (axis + 2) % 3
            for side in (-1, 1):
                neighbor = list(cell)
                neighbor[axis] += side
                if tuple(neighbor) in occupied:
                    continue
                corners = []
                for du, dv in ((0, 0), (1, 0), (1, 1), (0, 1)):
                    point = list(cell)
                    point[axis] += int(side > 0)
                    point[u] += du
                    point[v] += dv
                    corners.append(tuple(cuts[i][point[i]] for i in range(3)))
                faces.append(corners if side > 0 else corners[::-1])

    vertices = dict.fromkeys(point for face in faces for point in face)
    indices = {point: i for i, point in enumerate(vertices)}
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(list(vertices), [], [[indices[p] for p in face] for face in faces])
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.dissolve_limit(bm, angle_limit=0.0001, verts=list(bm.verts),
                             edges=list(bm.edges))
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()
    return mesh


def bevel_mesh(mesh, offset=BEVEL_OFFSET):
    if offset == 0:
        return "bevel_disabled"
    bm = bmesh.new()
    bm.from_mesh(mesh)
    note = "bevel_skipped"
    try:
        bmesh.ops.bevel(bm, geom=list(bm.verts) + list(bm.edges), offset=offset,
                        offset_type="OFFSET", segments=1, profile=0.5, affect="EDGES",
                        clamp_overlap=True)
        note = "bevel_applied"
    except Exception as exc:  # bevel is cosmetic; geometry is still valid without it
        note = f"bevel_skipped: {type(exc).__name__}: {exc}"
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()
    return note


def make_material():
    mat = bpy.data.materials.new("mannequin_v3_gray")
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (0.58, 0.575, 0.565, 1.0)
    bsdf.inputs["Roughness"].default_value = 0.42
    if "Metallic" in bsdf.inputs:
        bsdf.inputs["Metallic"].default_value = 0.0
    return mat


def build_geometry(profile, mat):
    objects = {}
    part_bones = profile.get("part_bones", PART_BONE)
    part_frames = profile.get("part_frames", {})
    for part, boxes in profile["parts"].items():
        mesh_factory = union_block_mesh if profile.get("union_blocks") else part_mesh
        mesh = mesh_factory(part, boxes)
        offset = profile.get("part_bevel_m", {}).get(part, profile.get("bevel_m", BEVEL_OFFSET))
        meshes_note = bevel_mesh(mesh, offset)
        if part in part_frames:
            mesh.transform(part_frames[part])
            mesh.update()
        lo = [min(v.co[i] for v in mesh.vertices) for i in range(3)]
        hi = [max(v.co[i] for v in mesh.vertices) for i in range(3)]
        obj = bpy.data.objects.new(part, mesh)
        obj.data.materials.append(mat)
        bpy.context.scene.collection.objects.link(obj)
        objects[part] = obj
        report["parts"][part] = {
            "bone": part_bones[part],
            "blocks": len(boxes),
            "vertices": len(mesh.vertices),
            "polygons": len(mesh.polygons),
            "bbox": [round(v, 4) for v in lo + hi],
            "mesh_note": meshes_note,
        }
    return objects


def build_armature(profile):
    arm_data = bpy.data.armatures.new("mannequin_v3_rig")
    rig = bpy.data.objects.new("mannequin_v3_rig", arm_data)
    bpy.context.scene.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    for spec in profile["bones"]:
        eb = arm_data.edit_bones.new(spec["name"])
        eb.head = Vector(spec["head"])
        eb.tail = Vector(spec["tail"])
        eb.roll = 0.0
        eb.use_deform = spec["deform"]
    for spec in profile["bones"]:
        if spec["parent"]:
            eb = arm_data.edit_bones[spec["name"]]
            eb.parent = arm_data.edit_bones[spec["parent"]]
            eb.use_connect = bool(spec["connected"])
    bpy.ops.object.mode_set(mode="OBJECT")
    rig.select_set(False)
    report["bones"] = [dict(s) for s in profile["bones"]]
    return rig


def skin(objects, rig, part_bones=PART_BONE):
    problems = []
    for part, obj in objects.items():
        bone = part_bones[part]
        group = obj.vertex_groups.new(name=bone)
        group.add(list(range(len(obj.data.vertices))), 1.0, "REPLACE")
        mod = obj.modifiers.new("Armature", "ARMATURE")
        mod.object = rig
        mod.use_vertex_groups = True
        obj.parent = rig
        obj.matrix_parent_inverse = Matrix.Identity(4)
        if len(obj.vertex_groups) != 1:
            problems.append(f"{part}: {len(obj.vertex_groups)} vertex groups")
    return problems


# --------------------------------------------------------------------- actions ----
def set_rot(rig, bone, rots):
    """Rotate a bone around armature-space axes (rightmost item is applied first)."""
    pb = rig.pose.bones[bone]
    q = Quaternion()
    for axis, deg in rots:
        q = q @ Quaternion(AXES[axis], math.radians(deg))
    m = pb.bone.matrix_local.to_3x3().to_quaternion()
    pb.rotation_mode = "QUATERNION"
    pb.rotation_quaternion = m.inverted() @ q @ m


def set_loc(rig, bone, world_offset):
    """Translate a bone by an armature-space offset (pose location is bone-local)."""
    pb = rig.pose.bones[bone]
    m = pb.bone.matrix_local.to_3x3().inverted()
    pb.location = m @ Vector(world_offset)


def clear_pose(rig):
    for pb in rig.pose.bones:
        pb.rotation_mode = "QUATERNION"
        pb.rotation_quaternion = Quaternion()
        pb.location = (0.0, 0.0, 0.0)
        pb.scale = (1.0, 1.0, 1.0)


def apply_pose(rig, pose):
    clear_pose(rig)
    for bone, spec in pose.items():
        if bone == "pelvis_loc":
            set_loc(rig, "pelvis", spec)
            continue
        set_rot(rig, bone, spec)


def action_fcurve_count(act):
    try:
        return len(act.fcurves)
    except Exception:
        pass
    total = 0
    for layer in getattr(act, "layers", []):
        for strip in getattr(layer, "strips", []):
            for cb in getattr(strip, "channelbags", []):
                total += len(cb.fcurves)
    return total


def foot_min_z(objects):
    deps = bpy.context.evaluated_depsgraph_get()
    lo = 1e9
    for name in ("foot_l", "foot_r"):
        ev = objects[name].evaluated_get(deps)
        mesh = ev.to_mesh()
        mw = ev.matrix_world
        for v in mesh.vertices:
            lo = min(lo, (mw @ v.co).z)
        ev.to_mesh_clear()
    return lo


def snap_to_ground(rig, objects):
    """Drop/raise the pelvis so the lowest foot vertex sits exactly on z = 0."""
    bpy.context.view_layer.update()
    dz = -foot_min_z(objects)
    set_loc(rig, "pelvis", (0.0, 0.0, dz))
    bpy.context.view_layer.update()
    return dz


def make_action(rig, name, keys, objects):
    """keys: list of (frame, pose dict)."""
    bpy.context.scene.frame_set(1)
    act = bpy.data.actions.new(name)
    act.use_fake_user = True
    rig.animation_data_create()
    rig.animation_data.action = act
    # Blender 4.4+ slotted actions: bind a slot so keyframes land in this action.
    if hasattr(rig.animation_data, "action_slot"):
        try:
            if rig.animation_data.action_slot is None:
                slot = act.slots.new(id_type="OBJECT", name=rig.name)
                rig.animation_data.action_slot = slot
        except Exception as exc:
            report["issues"].append(f"action slot {name}: {type(exc).__name__}: {exc}")
    ground = {}
    for frame, pose in keys:
        bpy.context.scene.frame_set(frame)
        apply_pose(rig, pose)
        ground[frame] = round(snap_to_ground(rig, objects), 4)
        for pb in rig.pose.bones:
            pb.keyframe_insert("rotation_quaternion", frame=frame, group=pb.name)
        rig.pose.bones["pelvis"].keyframe_insert("location", frame=frame, group="pelvis")
    fc = action_fcurve_count(act)
    report["actions"][name] = {"frames": [k[0] for k in keys], "fcurves": fc,
                               "bones": len(rig.pose.bones),
                               "ground_snap_m": ground}
    if fc == 0:
        report["issues"].append(f"action {name} has 0 fcurves")
    return act


def build_actions(rig, objects):
    arm_down = 68.0

    def arms(swing_l, swing_r, down=arm_down, elbow_l=16, elbow_r=16):
        return {
            "upperarm_l": [("X", swing_l), ("Y", down)],
            "upperarm_r": [("X", swing_r), ("Y", -down)],
            "lowerarm_l": [("Z", -elbow_l)], "lowerarm_r": [("Z", elbow_r)],
        }

    idle = {
        "spine_01": [("X", 2)], "spine_02": [("X", 1)], "head": [("X", -1)],
        "thigh_l": [("X", -3)], "thigh_r": [("X", -3)],
        "calf_l": [("X", 6)], "calf_r": [("X", 6)],
        "foot_l": [("X", -3)], "foot_r": [("X", -3)],
    }
    idle.update(arms(-6, -6))
    idle_up = json.loads(json.dumps(idle))
    idle_up.update(arms(-8, -8))
    idle_up["head"] = [("X", -2)]
    idle_up["calf_l"] = [("X", 5)]
    idle_up["calf_r"] = [("X", 5)]
    idle_low = json.loads(json.dumps(idle))
    idle_low.update(arms(-4, -4))
    idle_low["calf_l"] = [("X", 7)]
    idle_low["calf_r"] = [("X", 7)]

    walk_contact_l = {
        "pelvis": [("Z", 5)],
        "spine_01": [("X", 4), ("Z", -4)], "spine_02": [("X", 1)],
        "head": [("X", -2), ("Z", 2)],
        "thigh_l": [("X", -24)], "calf_l": [("X", 8)], "foot_l": [("X", 10)],
        "thigh_r": [("X", 20)], "calf_r": [("X", 26)], "foot_r": [("X", -20)],
    }
    walk_contact_l.update(arms(18, -20, elbow_l=22, elbow_r=30))
    walk_pass_l = {
        "spine_01": [("X", 3)], "head": [("X", -2)],
        "thigh_l": [("X", 0)], "calf_l": [("X", 3)], "foot_l": [("X", -2)],
        "thigh_r": [("X", -12)], "calf_r": [("X", 40)], "foot_r": [("X", -18)],
    }
    walk_pass_l.update(arms(5, -5, elbow_l=20, elbow_r=22))

    def mirror(pose):
        out = {}
        for bone, spec in pose.items():
            if bone.endswith("_l"):
                name = bone[:-2] + "_r"
            elif bone.endswith("_r"):
                name = bone[:-2] + "_l"
            else:
                name = bone
            # mirrored rotation: X keeps the sign, Y/Z flip; the same rule holds for
            # both the body bones and the limb bones because limbs are mirrored in X.
            out[name] = [(ax, -deg if ax in ("Y", "Z") else deg) for ax, deg in spec]
        return out

    attack_ready = {
        "pelvis": [("Z", -5)], "spine_01": [("X", 3), ("Z", -6)],
        "head": [("X", -2), ("Z", 5)],
        "upperarm_l": [("X", -10), ("Y", 58)], "upperarm_r": [("X", -10), ("Y", -58)],
        "lowerarm_l": [("Z", -40)], "lowerarm_r": [("Z", 40)],
        "thigh_l": [("X", -12)], "calf_l": [("X", 14)], "foot_l": [("X", -2)],
        "thigh_r": [("X", 10)], "calf_r": [("X", 12)], "foot_r": [("X", -6)],
    }
    attack_windup = {
        "pelvis": [("Z", -8)], "spine_01": [("X", 5), ("Z", -18)],
        "spine_02": [("Z", -6)], "head": [("X", -2), ("Z", 12)],
        "upperarm_l": [("X", 10), ("Y", 50)], "lowerarm_l": [("Z", -30)],
        "upperarm_r": [("Z", -38)], "lowerarm_r": [("Z", -45)],
        "thigh_l": [("X", -16)], "calf_l": [("X", 18)], "foot_l": [("X", -2)],
        "thigh_r": [("X", 12)], "calf_r": [("X", 16)], "foot_r": [("X", -8)],
    }
    attack_strike = {
        "pelvis": [("Z", 12)], "spine_01": [("X", 6), ("Z", 20)],
        "spine_02": [("X", 2), ("Z", 10)], "head": [("X", 0), ("Z", -14)],
        "upperarm_l": [("Z", 15), ("Y", 55)], "lowerarm_l": [("Z", -20)],
        "upperarm_r": [("Z", 50), ("Y", -20)], "lowerarm_r": [("Z", 25)],
        "thigh_l": [("X", -26)], "calf_l": [("X", 22)], "foot_l": [("X", 2)],
        "thigh_r": [("X", 20)], "calf_r": [("X", 20)], "foot_r": [("X", -14)],
    }
    attack_recover = {
        "pelvis": [("Z", 2)], "spine_01": [("X", 5), ("Z", 2)],
        "head": [("X", -2), ("Z", 2)],
        "upperarm_l": [("X", -2), ("Y", 60)], "upperarm_r": [("X", -10), ("Y", -58)],
        "lowerarm_l": [("Z", -42)], "lowerarm_r": [("Z", 35)],
        "thigh_l": [("X", -14)], "calf_l": [("X", 16)], "foot_l": [("X", -2)],
        "thigh_r": [("X", 10)], "calf_r": [("X", 12)], "foot_r": [("X", -6)],
    }

    make_action(rig, "idle", [(1, idle), (7, idle_up), (13, idle), (19, idle_low), (25, idle)],
                objects)
    make_action(rig, "walk", [
        (1, walk_contact_l), (5, walk_pass_l), (9, mirror(walk_contact_l)),
        (13, mirror(walk_pass_l)), (17, walk_contact_l)], objects)
    make_action(rig, "attack", [
        (1, attack_ready), (5, attack_windup), (9, attack_strike),
        (13, attack_recover), (15, attack_ready)], objects)
    rig.animation_data.action = None
    bpy.context.scene.frame_set(1)
    clear_pose(rig)


def hierarchy_text(profile):
    children = {}
    for spec in profile["bones"]:
        children.setdefault(spec["parent"], []).append(spec["name"])
    lines = []

    def walk(name, depth):
        spec = next(s for s in profile["bones"] if s["name"] == name)
        head = ", ".join(f"{v:+.4f}" for v in spec["head"])
        tail = ", ".join(f"{v:+.4f}" for v in spec["tail"])
        flag = "connected" if spec["connected"] else "offset"
        lines.append(f"{'  ' * depth}{name}  head=({head}) tail=({tail}) [{flag}]")
        for child in children.get(name, []):
            walk(child, depth + 1)

    for root in children.get(None, []):
        walk(root, 0)
    return "\n".join(lines) + "\n"


def set_export_sampling(scene, factor):
    """Increase the export sample rate without changing the saved .blend or clip seconds."""
    scene.render.fps *= factor
    scene.frame_end *= factor
    for action in bpy.data.actions:
        for layer in action.layers:
            for strip in layer.strips:
                for bag in strip.channelbags:
                    for curve in bag.fcurves:
                        for point in curve.keyframe_points:
                            point.co.x *= factor
                            point.handle_left.x *= factor
                            point.handle_right.x *= factor
                        curve.update()


def main(profile_path=PROFILE, run_dir=RUN, prepare_profile=None, finish_model=None):
    t0 = time.time()
    blend = os.path.join(run_dir, os.path.basename(BLEND))
    glb = os.path.join(run_dir, os.path.basename(GLB))
    report_path = os.path.join(run_dir, os.path.basename(REPORT))
    hierarchy = os.path.join(run_dir, os.path.basename(HIERARCHY_TXT))
    os.makedirs(run_dir, exist_ok=True)
    with open(profile_path, "rb") as fh:
        raw_profile = fh.read()
    profile = json.loads(raw_profile)
    report["profile"] = os.path.relpath(profile_path, ROOT).replace(os.sep, "/")
    report["profile_sha256"] = hashlib.sha256(raw_profile).hexdigest()
    if prepare_profile:
        profile = prepare_profile(profile)
        report["stage"] = profile["profile"] + "/build"

    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.frame_start = 1
    scene.frame_end = 25

    mat = make_material()
    objects = build_geometry(profile, mat)
    log(f"parts built: {len(objects)}")

    rig = build_armature(profile)
    log(f"bones built: {len(rig.data.bones)}")

    problems = skin(objects, rig, profile.get("part_bones", PART_BONE))
    report["issues"].extend(problems)
    log(f"skinning applied, problems: {problems}")

    build_actions(rig, objects)
    log(f"actions: {list(report['actions'])}")

    if finish_model:
        finish_model(profile, rig, objects, run_dir)

    with open(hierarchy, "w", encoding="utf-8") as fh:
        fh.write(hierarchy_text(profile))
    report["artifacts"]["hierarchy"] = os.path.relpath(hierarchy, ROOT).replace("\\", "/")
    report["hierarchy_text"] = hierarchy_text(profile)

    bpy.context.view_layer.update()
    bpy.ops.wm.save_as_mainfile(filepath=blend)
    report["artifacts"]["blend"] = os.path.relpath(blend, ROOT).replace("\\", "/")
    report["artifacts"]["blend_bytes"] = os.path.getsize(blend)

    sample_factor = profile.get("export_sample_factor", 1)
    if sample_factor > 1:
        set_export_sampling(scene, sample_factor)
    report["glb_sampling_hz"] = scene.render.fps / scene.render.fps_base
    kwargs = dict(filepath=glb, export_format="GLB", use_selection=False,
                  export_animations=True, export_skins=True, export_apply=False,
                  export_extras=bool(finish_model))
    try:
        bpy.ops.export_scene.gltf(**kwargs)
        export_args = sorted(kwargs)
    except TypeError as exc:
        if finish_model:
            raise
        report["issues"].append(f"gltf export args fallback: {exc}")
        bpy.ops.export_scene.gltf(filepath=glb, export_format="GLB")
        export_args = ["filepath", "export_format"]
    report["artifacts"]["glb"] = os.path.relpath(glb, ROOT).replace("\\", "/")
    report["artifacts"]["glb_bytes"] = os.path.getsize(glb)
    report["export_args"] = export_args

    report["counts"] = {
        "parts": len(objects),
        "bones": len(rig.data.bones),
        "deform_bones": sum(1 for b in rig.data.bones if b.use_deform),
        "vertices": sum(len(o.data.vertices) for o in objects.values()),
        "polygons": sum(len(o.data.polygons) for o in objects.values()),
        "actions": len(report["actions"]),
    }
    report["seconds"] = round(time.time() - t0, 1)
    report["status"] = "BUILD_DONE" if not report["issues"] else "BUILD_DONE_WITH_ISSUES"
    with open(report_path, "w", encoding="utf-8") as fh:
        json.dump(report, fh, indent=2, ensure_ascii=False, sort_keys=True)
    print("MANNEQUIN_BUILD=" + report["status"])
    print("MANNEQUIN_BUILD_REPORT=" + report_path)
    print(json.dumps(report["counts"], ensure_ascii=False))


if __name__ == "__main__":
    main()




