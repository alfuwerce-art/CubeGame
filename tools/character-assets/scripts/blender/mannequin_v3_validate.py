r"""MANNEQUIN-RIG-01: validate the block mannequin rig and render the review images.

Checks (all measured, nothing assumed):
  1. part list / bone list / hierarchy / geometry integrity;
  2. rigid skinning: every part has exactly one vertex group at weight 1.0, and the
     evaluated deformation equals the analytic bone transform (max deviation);
  3. joints: oriented-box SAT between all part pairs in T-pose and in every test pose
     -> signed separation (negative = penetration, mm);
  4. pose semantics: idle / walk / attack produce the expected limb positions;
  5. GLB round-trip: re-import the exported file into an empty scene;
  6. renders: front / side / 3-4 T-pose, front silhouette (alpha), idle / walk / attack.

Run:
    & 'D:\\programs\\Blender\\5.2\\blender.exe' --background --factory-startup `
        --python-exit-code 1 --python scripts\\blender\\mannequin_v3_validate.py
"""
from __future__ import annotations

import hashlib
import json
import math
import os
import sys
from pathlib import Path
import time

import bpy
from mathutils import Vector

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
PROFILE = os.path.join(ROOT, "scripts", "character", "profiles", "mannequin_v3_male.json")
RUN = os.path.join(ROOT, "outputs", "prototypes", "mannequin_v3_rig", "run1")
BLEND = os.path.join(RUN, "mannequin_v3_rig.blend")
GLB = os.path.join(RUN, "mannequin_v3_male_rig.glb")
REPORT = os.path.join(RUN, "validation.json")
RENDERS = os.path.join(RUN, "renders")

EXPECTED_BONES = ["root", "pelvis", "spine_01", "spine_02", "neck", "head",
                  "clavicle_l", "upperarm_l", "lowerarm_l", "hand_l",
                  "clavicle_r", "upperarm_r", "lowerarm_r", "hand_r",
                  "thigh_l", "calf_l", "foot_l", "thigh_r", "calf_r", "foot_r"]
EXPECTED_PARTS = ["head", "neck", "torso_upper", "torso_pelvis",
                  "upper_arm_l", "forearm_l", "hand_l",
                  "upper_arm_r", "forearm_r", "hand_r",
                  "thigh_l", "shin_l", "foot_l",
                  "thigh_r", "shin_r", "foot_r", "ear_l", "ear_r"]
PART_BONE = {
    "torso_pelvis": "pelvis", "torso_upper": "spine_02", "neck": "neck", "head": "head",
    "ear_l": "head", "ear_r": "head",
    "upper_arm_l": "upperarm_l", "forearm_l": "lowerarm_l", "hand_l": "hand_l",
    "upper_arm_r": "upperarm_r", "forearm_r": "lowerarm_r", "hand_r": "hand_r",
    "thigh_l": "thigh_l", "shin_l": "calf_l", "foot_l": "foot_l",
    "thigh_r": "thigh_r", "shin_r": "calf_r", "foot_r": "foot_r",
}
DESIGNED_INSERTIONS = None  # filled from the bone hierarchy: joint insertions are by design
# block pairs that touch while a limb hangs or lunges next to the torso; a small
# corner overlap is hidden inside the neighbour block (tolerance in meters)
TOLERATED = {
    ("forearm_l", "torso_upper"): 0.030, ("forearm_r", "torso_upper"): 0.030,
    ("thigh_l", "torso_upper"): 0.045, ("thigh_r", "torso_upper"): 0.045,
    ("hand_l", "torso_pelvis"): 0.025, ("hand_r", "torso_pelvis"): 0.025,
    ("forearm_l", "torso_pelvis"): 0.025, ("forearm_r", "torso_pelvis"): 0.025,
}
# adjacent segment pairs whose joint must stay connected in every pose
JOINTS = [
    ("neck", "head"), ("torso_upper", "neck"), ("torso_pelvis", "torso_upper"),
    ("torso_upper", "upper_arm_l"), ("torso_upper", "upper_arm_r"),
    ("upper_arm_l", "forearm_l"), ("forearm_l", "hand_l"),
    ("upper_arm_r", "forearm_r"), ("forearm_r", "hand_r"),
    ("torso_pelvis", "thigh_l"), ("thigh_l", "shin_l"), ("shin_l", "foot_l"),
    ("torso_pelvis", "thigh_r"), ("thigh_r", "shin_r"), ("shin_r", "foot_r"),
]
POSE_FRAMES = [("T_POSE", None, 1), ("IDLE", "idle", 1), ("WALK_CONTACT", "walk", 1),
               ("WALK_PASS", "walk", 5), ("ATTACK_WINDUP", "attack", 5),
               ("ATTACK_STRIKE", "attack", 9), ("ATTACK_RECOVER", "attack", 13)]

report = {"stage": "MANNEQUIN-RIG-01/validation", "blender": bpy.app.version_string,
          "stages": {}, "numbers": {}, "poses": {}, "artifacts": {}, "issues": []}
t_all = time.time()


def stage(name, ok, detail=""):
    report["stages"][name] = {"ok": bool(ok), "detail": detail}
    print(f"STAGE {name}: {'PASS' if ok else 'FAIL'} {detail}")


# --------------------------------------------------------------- oriented boxes ----
def obb_from_box(box, M):
    c = Vector((box["x"], box["y"], box["z"]))
    half = Vector((box["sx"] / 2.0, box["sy"] / 2.0, box["sz"] / 2.0))
    rot = M.to_3x3()
    axes = [rot.col[i].normalized() for i in range(3)]
    return M @ c, axes, half


def sat_separation(a, b):
    """Signed distance along the best separating axis; negative = penetration depth."""
    ca, aa, ha = a
    cb, ab, hb = b
    axes = list(aa) + list(ab) + [x.cross(y) for x in aa for y in ab]
    d = cb - ca
    sep = -1e18
    for ax in axes:
        if ax.length < 1e-8:
            continue
        ax = ax.normalized()
        ra = sum(abs(ax.dot(aa[i])) * ha[i] for i in range(3))
        rb = sum(abs(ax.dot(ab[i])) * hb[i] for i in range(3))
        sep = max(sep, abs(ax.dot(d)) - ra - rb)
    return sep


def part_penetrations(parts_boxes, bones_M):
    out = {}
    names = sorted(parts_boxes)
    for i, a in enumerate(names):
        for b in names[i + 1:]:
            pen = 0.0
            for box_a in parts_boxes[a]:
                oa = obb_from_box(box_a, bones_M[PART_BONE[a]])
                for box_b in parts_boxes[b]:
                    ob = obb_from_box(box_b, bones_M[PART_BONE[b]])
                    sep = sat_separation(oa, ob)
                    if sep < 0.0:
                        pen = max(pen, -sep)
            if pen > 0.0005:
                out[f"{a}|{b}"] = pen
    return out


def joint_separations(parts_boxes, bones_M):
    out = {}
    for a, b in JOINTS:
        best = 1e9
        for box_a in parts_boxes[a]:
            oa = obb_from_box(box_a, bones_M[PART_BONE[a]])
            for box_b in parts_boxes[b]:
                ob = obb_from_box(box_b, bones_M[PART_BONE[b]])
                best = min(best, sat_separation(oa, ob))
        out[f"{a}|{b}"] = best
    return out


def bone_matrices(rig):
    return {pb.name: pb.matrix @ pb.bone.matrix_local.inverted() for pb in rig.pose.bones}


def evaluated_vertices(obj):
    deps = bpy.context.evaluated_depsgraph_get()
    ev = obj.evaluated_get(deps)
    mesh = ev.to_mesh()
    mw = ev.matrix_world
    pts = [mw @ v.co for v in mesh.vertices]
    ev.to_mesh_clear()
    return pts


def apply_frame(rig, action, frame):
    if action is None:
        if rig.animation_data:
            rig.animation_data.action = None
        for pb in rig.pose.bones:
            pb.rotation_mode = "QUATERNION"
            pb.rotation_quaternion = (1.0, 0.0, 0.0, 0.0)
            pb.location = (0.0, 0.0, 0.0)
            pb.scale = (1.0, 1.0, 1.0)
    else:
        act = bpy.data.actions[action]
        if rig.animation_data is None:
            rig.animation_data_create()
        rig.animation_data.action = act
        if hasattr(rig.animation_data, "action_slot") and rig.animation_data.action_slot is None:
            slots = getattr(act, "slots", [])
            if len(slots):
                rig.animation_data.action_slot = slots[0]
    bpy.context.scene.frame_set(frame)
    bpy.context.view_layer.update()


def centroid(points):
    v = Vector((0.0, 0.0, 0.0))
    for p in points:
        v += p
    return v / len(points)


def main(profile_path=PROFILE, run_dir=RUN, prepare_profile=None, inspect_model=None, review_shots=None):
    blend = os.path.join(run_dir, os.path.basename(BLEND))
    glb = os.path.join(run_dir, os.path.basename(GLB))
    report_path = os.path.join(run_dir, os.path.basename(REPORT))
    renders = os.path.join(run_dir, "renders")
    os.makedirs(renders, exist_ok=True)
    with open(profile_path, encoding="utf-8") as fh:
        profile = json.load(fh)
    if prepare_profile:
        profile = prepare_profile(profile)
        report["stage"] = profile["profile"] + "/validation"
    part_bones = profile.get("part_bones", PART_BONE)
    expected_parts = set(part_bones)
    expected_bones = {spec["name"] for spec in profile["bones"]}
    parts_boxes = {name: profile["parts"][name] for name in PART_BONE}

    if not os.path.isfile(blend):
        print("MANNEQUIN_VALIDATION=FAIL missing blend")
        sys.exit(2)
    if inspect_model:
        def artifact_manifest():
            return {name: hashlib.sha256(Path(path).read_bytes()).hexdigest()
                    for name, path in (("blend", blend), ("glb", glb))}
        report["artifacts"]["sha256"] = artifact_manifest()
    bpy.ops.wm.open_mainfile(filepath=blend)
    scene = bpy.context.scene
    rig = bpy.data.objects.get("mannequin_v3_rig")
    meshes = {o.name: o for o in scene.objects if o.type == "MESH"}
    report["numbers"].update({"objects": len(meshes), "object_names": sorted(meshes),
                              "bones": len(rig.data.bones) if rig else 0})

    missing = sorted(expected_parts - meshes.keys())
    stage("part_list", len(meshes) == len(expected_parts) and not missing,
          f"{len(meshes)} parts, missing={missing}")

    verts = sum(len(o.data.vertices) for o in meshes.values())
    polys = sum(len(o.data.polygons) for o in meshes.values())
    lo = Vector((1e9, 1e9, 1e9))
    hi = Vector((-1e9, -1e9, -1e9))
    nan_parts, degenerate = [], []
    for name, obj in meshes.items():
        for v in obj.data.vertices:
            p = obj.matrix_world @ v.co
            if any(math.isnan(c) or abs(c) > 1e4 for c in p):
                nan_parts.append(name)
                break
            for i in range(3):
                lo[i] = min(lo[i], p[i])
                hi[i] = max(hi[i], p[i])
        if min((pl.area for pl in obj.data.polygons), default=1.0) <= 1e-9:
            degenerate.append(name)
    dims = [round(hi[i] - lo[i], 4) for i in range(3)]
    report["numbers"].update({"vertices": verts, "polygons": polys, "dimensions_m": dims,
                              "bbox_min": [round(v, 4) for v in lo],
                              "bbox_max": [round(v, 4) for v in hi]})
    geom_ok = (not nan_parts and not degenerate and abs(lo.z) < 1e-3
               and abs(hi.z - 1.80) < 1e-3 and dims[0] > 1.3 and dims[2] > 1.7)
    stage("geometry", geom_ok,
          f"verts={verts} polys={polys} dims={dims} floor_z={round(lo.z, 4)} "
          f"nan={nan_parts} degenerate={degenerate}")

    bones = [b.name for b in rig.data.bones] if rig else []
    bone_missing = sorted(expected_bones - set(bones))
    stage("bone_list", len(bones) == len(expected_bones) and not bone_missing,
          f"{len(bones)} bones, missing={bone_missing}")

    bad = []
    for spec in profile["bones"]:
        b = rig.data.bones.get(spec["name"])
        if b is None:
            bad.append(f"{spec['name']} missing")
            continue
        parent = b.parent.name if b.parent else None
        if parent != spec["parent"]:
            bad.append(f"{spec['name']} parent {parent} != {spec['parent']}")
        if b.use_connect != bool(spec["connected"]):
            bad.append(f"{spec['name']} connect {b.use_connect}")
    stage("bone_hierarchy", not bad,
          "; ".join(bad) if bad else f"{len(bones)} bones, parents and connections match the profile")

    # -------------------------------------------------------------- rigid skinning --
    skin_bad = []
    for name, obj in meshes.items():
        bone = part_bones[name]
        groups = list(obj.vertex_groups)
        if len(groups) != 1 or groups[0].name != bone:
            skin_bad.append(f"{name}: groups={[g.name for g in groups]}")
        mods = [m for m in obj.modifiers if m.type == "ARMATURE"]
        if len(mods) != 1 or mods[0].object is None or mods[0].object.name != "mannequin_v3_rig":
            skin_bad.append(f"{name}: armature modifier wrong")
        for v in obj.data.vertices:
            if len(v.groups) != 1 or abs(v.groups[0].weight - 1.0) > 1e-5:
                skin_bad.append(f"{name}: vertex {v.index} weights="
                                f"{[round(g.weight, 4) for g in v.groups]}")
                break
    stage("rigid_vertex_groups", not skin_bad, "; ".join(skin_bad[:4]) if skin_bad else
          f"{len(meshes)} parts, exactly one vertex group each, all weights 1.0")

    # ---------------------------------------------- poses: rigid transform + joints --
    bone_parent = {spec["name"]: spec["parent"] for spec in profile["bones"]}

    def chain_distance(a, b, limit=3):
        """Distance in the bone hierarchy between the bones that drive two parts."""
        ba, bb = PART_BONE[a], PART_BONE[b]
        seen = {ba}
        frontier = [ba]
        for step in range(1, limit + 1):
            nxt = []
            for bone in frontier:
                for cand in (bone_parent.get(bone), [k for k, v in bone_parent.items() if v == bone]):
                    items = cand if isinstance(cand, list) else [cand]
                    for item in items:
                        if item == bb:
                            return step
                        if item and item not in seen:
                            seen.add(item)
                            nxt.append(item)
            frontier = nxt
        return 99

    designed_pairs = {}
    tolerated_pairs = {}
    unexpected_pairs = {}
    names = sorted(parts_boxes)
    for i, a in enumerate(names):
        for b in names[i + 1:]:
            if PART_BONE[a] == PART_BONE[b] or chain_distance(a, b) <= 2:
                designed_pairs[(a, b)] = True
            elif (a, b) in TOLERATED:
                tolerated_pairs[(a, b)] = TOLERATED[(a, b)]
            elif (b, a) in TOLERATED:
                tolerated_pairs[(a, b)] = TOLERATED[(b, a)]
            else:
                unexpected_pairs[(a, b)] = True

    rest_pts = {name: [obj.matrix_world @ v.co for v in obj.data.vertices]
                for name, obj in meshes.items()}
    pose_numbers = {}
    for label, action, frame in POSE_FRAMES:
        apply_frame(rig, action, frame)
        Ms = bone_matrices(rig)
        dev_max = 0.0
        min_z = 1e9
        part_centroids = {}
        for name, obj in meshes.items():
            expected = [Ms[part_bones[name]] @ (obj.matrix_world.inverted() @ p) for p in rest_pts[name]]
            actual = evaluated_vertices(obj)
            for e, a in zip(expected, actual):
                dev_max = max(dev_max, (e - a).length)
            min_z = min(min_z, min(p.z for p in actual))
            part_centroids[name] = centroid(actual)
        pens = part_penetrations(parts_boxes, Ms)
        seps = joint_separations(parts_boxes, Ms)
        unexpected, tolerated, designed = {}, {}, {}
        for key, value in pens.items():
            pair = tuple(key.split("|"))
            mm = round(value * 1000, 2)
            if pair in unexpected_pairs:
                unexpected[key] = mm
            elif pair in tolerated_pairs and mm > tolerated_pairs[pair] * 1000:
                unexpected[key] = mm
            else:
                (designed if pair in designed_pairs else tolerated)[key] = mm
        pose_numbers[label] = {
            "frame": frame,
            "action": action,
            "rigid_max_deviation_m": round(dev_max, 8),
            "collisions_mm": unexpected,
            "max_collision_mm": max(unexpected.values()) if unexpected else 0.0,
            "joint_insertion_mm": designed,
            "tolerated_contact_mm": tolerated,
            "joint_separation_mm": {k: round(v * 1000, 2) for k, v in seps.items()},
            "min_z_m": round(min_z, 4),
            "centroids": {k: [round(c.x, 4), round(c.y, 4), round(c.z, 4)]
                          for k, c in part_centroids.items()},
        }
    report["numbers"]["pair_classes"] = {
        "designed_joint_pairs": len(designed_pairs),
        "tolerated_pairs": len(tolerated_pairs),
        "checked_collision_pairs": len(unexpected_pairs),
    }
    report["poses"] = pose_numbers

    dev_ok = all(v["rigid_max_deviation_m"] < 1e-5 for v in pose_numbers.values())
    stage("rigid_deformation", dev_ok,
          "max deviation from the analytic bone transform = "
          f"{max(v['rigid_max_deviation_m'] for v in pose_numbers.values()):.2e} m")

    pen_ok = all(v["max_collision_mm"] < 5.0 for v in pose_numbers.values())
    worst = max(pose_numbers.items(), key=lambda kv: kv[1]["max_collision_mm"])
    stage("part_intersections", pen_ok,
          f"{len(unexpected_pairs)} non-joint block pairs checked; worst pose {worst[0]}: "
          f"{worst[1]['max_collision_mm']} mm "
          f"({'; '.join(f'{k} {v} mm' for k, v in worst[1]['collisions_mm'].items()) or 'no collision'})")

    # the hidden hip insertion must not grow noticeably when the legs rotate
    rest_hip = {k: v for k, v in pose_numbers["T_POSE"]["joint_insertion_mm"].items() if "thigh" in k}
    hip_growth = {}
    for label, p in pose_numbers.items():
        for k, v in p["joint_insertion_mm"].items():
            if k in rest_hip and v - rest_hip[k] > 30.0:
                hip_growth[f"{label}:{k}"] = round(v - rest_hip[k], 2)
    stage("hip_insertion_stable", not hip_growth,
          "joint insertions stay within 30 mm of the rest pose" if not hip_growth
          else json.dumps(hip_growth))

    gap_bad = {}
    for label, p in pose_numbers.items():
        for k, v in p["joint_separation_mm"].items():
            if v > 25.0:
                gap_bad.setdefault(label, {})[k] = v
    stage("joint_connectivity", not gap_bad,
          "all 15 joints stay closed (<= 25 mm gap) in every pose" if not gap_bad
          else json.dumps(gap_bad))

    # ---------------------------------------------------------------- pose semantics --
    c = {label: p["centroids"] for label, p in pose_numbers.items()}
    checks = {}

    def hand_of(label, side):
        return c[label][f"hand_{side}"]

    def foot_of(label, side):
        return c[label][f"foot_{side}"]

    checks["idle_arms_down"] = (hand_of("IDLE", "l")[2] < 1.00 and hand_of("IDLE", "r")[2] < 1.00)
    checks["idle_feet_on_ground"] = (abs(pose_numbers["IDLE"]["min_z_m"]) < 0.02)
    checks["idle_body_upright"] = (c["IDLE"]["head"][2] > c["IDLE"]["neck"][2] > c["IDLE"]["torso_upper"][2])
    checks["idle_hands_beside_body"] = (abs(hand_of("IDLE", "l")[0]) < 0.45 and abs(hand_of("IDLE", "r")[0]) < 0.45)
    checks["walk_contact_left_forward"] = (foot_of("WALK_CONTACT", "l")[1] < foot_of("WALK_CONTACT", "r")[1] - 0.05)
    checks["walk_contact_contralateral_arms"] = (hand_of("WALK_CONTACT", "l")[1] > hand_of("WALK_CONTACT", "r")[1] + 0.02)
    checks["walk_pass_feet_close"] = (abs(foot_of("WALK_PASS", "l")[1] - foot_of("WALK_PASS", "r")[1]) < 0.16)
    checks["walk_pass_swing_foot_lifted"] = (foot_of("WALK_PASS", "r")[2] > foot_of("WALK_PASS", "l")[2] + 0.02)
    checks["walk_contact_grounded"] = (abs(pose_numbers["WALK_CONTACT"]["min_z_m"]) < 0.03)
    checks["attack_windup_hand_back"] = (hand_of("ATTACK_WINDUP", "r")[1] > 0.25)
    checks["attack_strike_hand_forward"] = (hand_of("ATTACK_STRIKE", "r")[1] < -0.25)
    checks["attack_swing_arc"] = (hand_of("ATTACK_WINDUP", "r")[1] - hand_of("ATTACK_STRIKE", "r")[1] > 0.60)
    checks["attack_lunge_foot_spread"] = (abs(foot_of("ATTACK_STRIKE", "l")[1] - foot_of("ATTACK_STRIKE", "r")[1]) > 0.15)
    report["numbers"]["pose_semantics"] = checks
    stage("pose_semantics", all(checks.values()),
          ", ".join(f"{k}={'ok' if v else 'FAIL'}" for k, v in checks.items()))

    if inspect_model:
        inspect_model(profile, rig, meshes, stage, report, roundtrip=False)

    # ------------------------------------------------------------------- round trip --
    rt_ok, rt_detail = False, "not run"
    try:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.gltf(filepath=glb, disable_bone_shape=True)
        scene2 = bpy.context.scene
        rt_meshes = {o.name: o for o in scene2.objects if o.type == "MESH"}
        rt_parts = {n: o for n, o in rt_meshes.items() if n in expected_parts}
        rt_arms = [o for o in scene2.objects if o.type == "ARMATURE"]
        rt_actions = sorted(a.name for a in bpy.data.actions)
        rt_bones = len(rt_arms[0].data.bones) if rt_arms else 0
        weight_bad = []
        for name, obj in rt_parts.items():
            for v in obj.data.vertices:
                ws = [g.weight for g in v.groups if g.weight > 0.0]
                if len(ws) != 1 or abs(ws[0] - 1.0) > 1e-4:
                    weight_bad.append(f"{name}:{v.index}:{ws}")
                    break
        rt_ok = (len(rt_parts) == len(expected_parts) and rt_bones == len(expected_bones) and not weight_bad
                 and len(rt_actions) >= 3)
        if inspect_model:
            inspect_model(profile, rt_arms[0], rt_parts, stage, report, roundtrip=True)
            rt_ok = rt_ok and not (set(rt_meshes) - set(rt_parts))
        rt_detail = (f"parts={len(rt_parts)} extra_meshes={sorted(set(rt_meshes) - set(rt_parts))} "
                     f"bones={rt_bones} skinned_weights_bad={weight_bad[:3]} actions={rt_actions}")
        report["numbers"]["roundtrip"] = {"parts": len(rt_parts), "bones": rt_bones,
                                          "actions": rt_actions, "weight_bad": weight_bad[:3]}
    except Exception as exc:
        rt_detail = f"{type(exc).__name__}: {exc}"
    stage("glb_roundtrip", rt_ok, rt_detail)

    # ---------------------------------------------------------------------- renders --
    if "--skip-renders" in sys.argv:
        stage("renders", True, "skipped by --skip-renders")
    else:
        try:
            render_setup_and_run(blend, renders, review_views=bool(prepare_profile), review_shots=review_shots)
            stage("renders", report["numbers"].get("render_ok", False),
                  json.dumps(report["numbers"].get("render_seconds", {})))
        except Exception as exc:
            report["issues"].append(f"render: {type(exc).__name__}: {exc}")
            stage("renders", False, f"{type(exc).__name__}: {exc}")

    if inspect_model:
        stage("validated_artifact_identity", artifact_manifest() == report["artifacts"]["sha256"],
              "Build hashes are unchanged through validation and rendering")
    report["seconds_total"] = round(time.time() - t_all, 1)
    report["result"] = "PASS" if all(s["ok"] for s in report["stages"].values()) else "FAIL"
    with open(report_path, "w", encoding="utf-8") as fh:
        json.dump(report, fh, indent=2, ensure_ascii=False, sort_keys=True)
    print("MANNEQUIN_VALIDATION=" + report["result"])
    for nm, st in report["stages"].items():
        print(f"MANNEQUIN_STAGE {nm}={'PASS' if st['ok'] else 'FAIL'} :: {st['detail']}")
    print("MANNEQUIN_VALIDATION_REPORT=" + report_path)
    return report["result"] == "PASS"


def render_setup_and_run(blend, renders, review_views=False, review_shots=None):
    bpy.ops.wm.open_mainfile(filepath=blend)
    scene = bpy.context.scene
    rig = bpy.data.objects["mannequin_v3_rig"]

    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 24 if review_shots else (32 if review_views else 64)
    scene.cycles.use_denoising = True
    scene.render.resolution_x = 1000 if review_views else 800
    scene.render.resolution_y = 1200
    scene.render.image_settings.file_format = "PNG"
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "None"
    scene.view_settings.exposure = -0.9

    world = bpy.data.worlds.new("MANNEQUIN_World")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.16, 0.16, 0.165, 1.0)
    world.node_tree.nodes["Background"].inputs[1].default_value = 1.0
    scene.world = world

    target = bpy.data.objects.new("MANNEQUIN_Target", None)
    target.location = (0.0, 0.0, 0.92)
    scene.collection.objects.link(target)

    floor_mesh = bpy.data.meshes.new("MANNEQUIN_Floor")
    floor_mesh.from_pydata([(-30, -30, 0), (30, -30, 0), (30, 30, 0), (-30, 30, 0)], [],
                           [(0, 1, 2, 3)])
    floor = bpy.data.objects.new("MANNEQUIN_Floor", floor_mesh)
    scene.collection.objects.link(floor)
    fmat = bpy.data.materials.new("MANNEQUIN_FloorMat")
    fmat.use_nodes = True
    fb = fmat.node_tree.nodes["Principled BSDF"]
    fb.inputs["Base Color"].default_value = (0.10, 0.10, 0.105, 1.0)
    fb.inputs["Roughness"].default_value = 0.75
    floor.data.materials.append(fmat)

    cam_data = bpy.data.cameras.new("MANNEQUIN_Cam")
    cam = bpy.data.objects.new("MANNEQUIN_Cam", cam_data)
    scene.collection.objects.link(cam)
    tr = cam.constraints.new(type="TRACK_TO")
    tr.target = target
    tr.track_axis = "TRACK_NEGATIVE_Z"
    tr.up_axis = "UP_Y"
    scene.camera = cam

    for nm, loc, energy, size in (("Key", (-2.6, -3.6, 3.6), 420, 2.4),
                                  ("Fill", (3.6, -2.6, 1.9), 150, 3.2),
                                  ("Rim", (0.8, 3.6, 3.2), 260, 2.4),
                                  ("Top", (0.0, -0.4, 5.4), 100, 4.0)):
        d = bpy.data.lights.new(nm, type="AREA")
        d.energy = energy
        d.size = size
        o = bpy.data.objects.new(nm, d)
        o.location = loc
        cc = o.constraints.new(type="TRACK_TO")
        cc.target = target
        cc.track_axis = "TRACK_NEGATIVE_Z"
        cc.up_axis = "UP_Y"
        scene.collection.objects.link(o)

    def shoot(name, location, cam_type="ORTHO", ortho_scale=2.55, lens=70.0,
              transparent=False, hide_floor=False, focus=(0, 0, 0.92)):
        cam_data.type = cam_type
        if cam_type == "ORTHO":
            cam_data.ortho_scale = ortho_scale
        else:
            cam_data.lens = lens
        target.location = focus
        cam.location = location
        scene.render.film_transparent = transparent
        floor.hide_render = hide_floor
        bpy.context.view_layer.update()
        scene.render.filepath = os.path.join(renders, name)
        t = time.time()
        bpy.ops.render.render(write_still=True)
        return round(time.time() - t, 2)

    if review_views:
        apply_frame(rig, None, 1)
        views = {
            "FRONT": (0, -8, 0.92), "BACK": (0, 8, 0.92),
            "LEFT": (8, 0, 0.92), "RIGHT": (-8, 0, 0.92),
            "THREE_QUARTER": (5, -7, 3.3),
        }
        times = {name + ".png": shoot(name + ".png", location, ortho_scale=2.20)
                 for name, location in views.items()}
        times["tpose_front_silhouette.png"] = shoot(
            "tpose_front_silhouette.png", views["FRONT"], ortho_scale=2.20,
            transparent=True, hide_floor=True)
        for label, action, frame in POSE_FRAMES[1:]:
            apply_frame(rig, action, frame)
            name = {"WALK_CONTACT": "WALK", "ATTACK_STRIKE": "ATTACK"}.get(label, label)
            times[name + ".png"] = shoot(name + ".png", views["THREE_QUARTER"], ortho_scale=2.20)
        if review_shots:
            times.update(review_shots(rig, shoot))
        filenames = list(times)
    else:
        times = {}
        apply_frame(rig, None, 1)
        times["front"] = shoot("tpose_front.png", (0.0, -8.0, 0.92))
        times["side"] = shoot("tpose_side.png", (8.0, 0.0, 0.92))
        times["three_quarter"] = shoot("tpose_three_quarter.png", (-2.9, -3.7, 1.62), "PERSP")
        times["front_alpha"] = shoot("tpose_front_silhouette.png", (0.0, -8.0, 0.92),
                                     transparent=True, hide_floor=True)
        for label, action, frame in (("IDLE", "idle", 1), ("WALK_CONTACT", "walk", 1),
                                     ("WALK_PASS", "walk", 5), ("ATTACK_WINDUP", "attack", 5),
                                     ("ATTACK_STRIKE", "attack", 9)):
            apply_frame(rig, action, frame)
            times[label] = shoot(f"pose_{label.lower()}.png", (-2.9, -3.7, 1.62), "PERSP")
            times[label + "_front"] = shoot(f"pose_{label.lower()}_front.png", (0.0, -8.0, 0.92))

        filenames = sorted(os.listdir(renders))
    report["numbers"]["render_seconds"] = times
    report["numbers"]["render_files"] = sorted(filenames)
    report["artifacts"]["renders"] = os.path.relpath(renders, ROOT).replace("\\", "/")
    report["numbers"]["render_ok"] = all(
        os.path.getsize(os.path.join(renders, f)) > 10000 for f in filenames)


if __name__ == "__main__":
    main()



