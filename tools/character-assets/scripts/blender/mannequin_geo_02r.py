"""Build and review the GEO-02R mannequin using the Factoria Blender pipeline."""

import itertools
import json
import math
from pathlib import Path
import sys

import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
import mannequin_geo_02 as geo
import mannequin_v3_build as build
import mannequin_v3_validate as validate

PROFILE = geo.PROFILE.with_name("mannequin_geo_02r_male.json")
RUN = geo.ROOT / "outputs/prototypes/mannequin_geo_02r/run1"
CLIPS = {**geo.CLIPS, "grip_test": 25, "attack_grip": 15}
HAND_STATES = {1: "neutral", 7: "open", 13: "fist", 19: "grip", 25: "release"}
FINGER_AXIS = (0, 1, 0)
GROUND_M = .00032


def block(row, side=1):
    name, x, y, z, sx, sy, sz = row
    return dict(source_block=name, x=side*x, y=y, z=z, sx=sx, sy=sy, sz=sz)


def add_fingers(profile, side, sign):
    for digit, spec in profile["fingers"].items():
        points = [Vector((sign*x, y, z)) for x, y, z in spec["points"]]
        parent = "hand_" + side
        for i, (head, tail) in enumerate(zip(points, points[1:])):
            name = f"{digit}_{i+1:02d}_{side}"
            profile["bones"].append(dict(name=name, head=list(head), tail=list(tail),
                                         parent=parent, connected=i>0, deform=True))
            longitudinal = (tail-head).normalized()
            across = Vector(FINGER_AXIS)
            across = (across-longitudinal*longitudinal.dot(across)).normalized()
            up = longitudinal.cross(across)
            frame = Matrix((longitudinal, across, up)).transposed().to_4x4()
            frame.translation = head
            # Allow shallow insertion at knuckles, including at the base of each digit.
            overlap = (.0025 if digit != "thumb" else .004) if i == 0 else .0025
            length = (tail-head).length
            tip = .0015 if i == 0 else 0
            profile["parts"][name] = [block((digit, (length + tip - overlap)/2, 0, 0,
                                            length + overlap + tip,
                                            spec["width_m"][i], spec["depth_m"][i]))]
            profile["part_frames"][name] = frame
            profile["part_bones"][name] = name
            profile["part_bevel_m"][name] = .001
            parent = name


def prepare(revision):
    baseline = json.loads(PROFILE.with_name(revision["parent_profile"]).read_text(encoding="utf-8"))
    profile = geo.prepare(baseline)
    profile.update(revision)
    profile["part_bones"] = dict(build.PART_BONE)
    profile["part_frames"] = {}
    profile["part_bevel_m"] = {}
    profile["parts"]["torso_pelvis"] = [block(row) for row in revision["pelvis"]]
    slots = profile["sockets"]
    slots["NOSE"] = dict(group="NOSE", bone="head", part="head", block="head_skull",
                         at=(0,-.5,-.15), orientation="front", fit="rigid_socket", covers=[])
    for side, sign in (("l", 1), ("r", -1)):
        for name, rows in revision["parts_left"].items():
            profile["parts"][name + "_" + side] = [block(row, sign) for row in rows]
        profile["part_bevel_m"]["ear_" + side] = 0.0
        profile["part_bevel_m"]["thigh_" + side] = 0.0
        add_fingers(profile, side, sign)
        label = side.upper()
        ear = slots["EAR_" + label]
        ear.update(part="head", block="head_skull", at=(sign*.5,.025,-.11))
        hand = "hand_" + side
        digits = [name for name in profile["part_frames"] if name.endswith("_" + side)]
        slots["GLOVE_" + label] = dict(group="GLOVES", bone=hand, part=hand, block="hand",
                                      at=(0,0,.5), orientation="top", fit="segmented_rigid",
                                      covers=[hand, *digits])
        slots["FEATURE_" + label] = dict(group="FEATURES", bone="head", part="head", block="head_skull",
                                        at=(sign*.5,0,.26), orientation="left" if sign>0 else "right",
                                        fit="rigid_socket", covers=[])
        center = list(revision["grip_fixture"]["center_left_m"])
        center[0] *= sign
        palm, = profile["parts"][hand]
        grip = slots["OFF_HAND" if sign>0 else "MAIN_HAND"]
        grip["at"] = [(center[i]-palm[a])/palm["s"+a] for i,a in enumerate(geo.AXES)]
        grip["external_grip"] = True
    profile["socket_groups"] = geo.GROUPS | {"GLOVES", "NOSE", "FEATURES"}
    return profile


def hand_pose(profile, state, side):
    signs = {"X": 1, "Y": 1 if side=="l" else -1, "Z": 1 if side=="l" else -1}
    flex = profile["grip_fixture"]["flex_deg"]
    fist = profile["grip_fixture"].get("fist_deg", {})
    result = {}
    for digit, spec in profile["fingers"].items():
        for i in range(2):
            if state == "grip":
                rot = flex[digit][i]
            elif state == "fist":
                rot = fist.get(digit, [[("Y",70)], [("Y",77)]])[i]
            elif state == "open":
                spread =  -10 if digit == "thumb" else spec["points"][0][1]*64
                rot = [("Z",spread),("Y",-6)] if i==0 else [("Y",-5)]
            else:
                rot = []
            result[f"{digit}_{i+1:02d}_{side}"] = [(axis, angle*signs[axis]) for axis,angle in rot]
    return result


def set_action(rig, action):
    rig.animation_data.action = action
    rig.animation_data.action_slot = action.slots[0]


def bake_ground(rig, objects, name, count, samples_per_frame):
    action = bpy.data.actions[name]
    set_action(rig, action)
    pelvis = rig.pose.bones["pelvis"]
    pelvis_to_local = pelvis.bone.matrix_local.to_3x3().inverted()
    keys = {}
    for sample in range((count-1)*samples_per_frame+1):
        f = 1 + sample/samples_per_frame
        bpy.context.scene.frame_set(int(f), subframe=f%1)
        height = build.foot_min_z(objects)
        keys[f] = pelvis.location.copy() + pelvis_to_local @ Vector((0,0,GROUND_M-height))
    for f, location in keys.items():
        pelvis.location = location
        pelvis.keyframe_insert("location", frame=f, group="pelvis")
    bag = action.layers[0].strips[0].channelbag(action.slots[0])
    for curve in bag.fcurves:
        if curve.data_path == pelvis.path_from_id("location"):
            for key in curve.keyframe_points:
                key.interpolation = "LINEAR"
            curve.update()
    build.report["actions"][name]["ground_bake_samples"] = len(keys)


def finish(profile, rig, objects, run_dir):
    for action, length in geo.CLIPS.items():
        bake_ground(rig, objects, action, length, profile["export_sample_factor"])
    keys = [(frame, {**hand_pose(profile, label, "l"), **hand_pose(profile, label, "r")})
            for frame,label in HAND_STATES.items()]
    build.make_action(rig, "grip_test", keys, objects)
    hold = bpy.data.actions["attack"].copy()
    hold.name = "attack_grip"
    hold.use_fake_user = True
    set_action(rig, hold)
    grip_frames = build.report["actions"]["attack"]["frames"]
    for f in grip_frames:
        bpy.context.scene.frame_set(f)
        for name, rotations in hand_pose(profile, "grip", "r").items():
            build.set_rot(rig, name, rotations)
            rig.pose.bones[name].keyframe_insert("rotation_quaternion", frame=f, group=name)
    build.report["actions"]["attack_grip"] = {**build.report["actions"]["attack"], "finger_keys":grip_frames}
    rig.animation_data.action = None
    bpy.context.scene.frame_set(1)
    build.clear_pose(rig)
    bpy.context.view_layer.update()
    geo.finish_model(profile, rig, objects, run_dir)


def volumes(profile, transforms, names):
    return {name: [validate.obb_from_box(b, transforms[profile["part_bones"][name]] @
                                         profile["part_frames"].get(name, Matrix.Identity(4)))
                   for b in profile["parts"][name]] for name in names}


def handle_gap(obb, center, axis, radius, length):
    """Distance from a finite cylinder axis to an oriented box, less cylinder radius."""
    point, axes, half = obb
    a = [(center-point).dot(v) for v in axes]
    b = [axis.dot(v) for v in axes]
    def dist_sq(t):
        return sum(max(0,abs(x+t*y)-h)**2 for x,y,h in zip(a,b,half))
    lo,hi = -length/2,length/2
    for _ in range(32):
        u,v = (2*lo+hi)/3,(lo+2*hi)/3
        if dist_sq(u)<dist_sq(v):
            hi=v
        else:
            lo=u
    return math.sqrt(dist_sq((lo+hi)/2))-radius


def posed_hand(profile, rig, side):
    transforms = validate.bone_matrices(rig)
    palm = "hand_"+side
    digits = [name for name in profile["part_frames"] if name.endswith("_"+side)]
    posed = volumes(profile, transforms, [palm, *digits])
    violations = []
    max_seam = 0.0
    for a,b in itertools.combinations(posed,2):
        separation = min(validate.sat_separation(x,y) for x in posed[a] for y in posed[b])
        connected_digit = a.split("_")[0] == b.split("_")[0]
        root_seam = a == palm and "_01_" in b
        if connected_digit or root_seam:
            # Each hinge may enter its socket by roughly one half-section.
            # Compare distal/palm and inter-digit pairs as separate forbidden contacts.
            allowed = .015 if connected_digit else (.017 if b.startswith("thumb") else .0145)
            if separation > .004 or separation < -allowed:
                violations.append((a,b,round(separation*1000,2),"seam"))
            max_seam = max(max_seam, separation)
        elif separation < -.0004:
            violations.append((a,b,round(separation*1000,2),"collision"))
    return posed, violations, max_seam


def grip_contacts(profile, posed, socket):
    spec = profile["grip_fixture"]
    center = socket.matrix_world.translation
    direction = socket.matrix_world.to_3x3().col[1].normalized()
    return {name: min(handle_gap(box,center,direction,spec["radius_m"],spec["length_m"])
                      for box in boxes) for name,boxes in posed.items()}


def test_ear_swap(rig, meshes):
    """Mount two temporary stepped coupons to the same ear IDs as the base ears."""
    proxies = []
    for side, reach in (("l", .075), ("r", .105)):
        anchor = bpy.data.objects["SOCKET_EAR_"+side.upper()]
        source = meshes["ear_"+side]
        source.hide_render = True
        mesh = build.union_block_mesh("QA_EAR_"+side, [
            block(("seat",0,0,reach*.28,.095,.125,reach*.72)),
            block(("tip",0,.03,reach*.60,.055,.106,reach*.48)),
        ])
        proxy = bpy.data.objects.new("QA_EAR_"+side, mesh)
        bpy.context.scene.collection.objects.link(proxy)
        proxy.parent = anchor
        proxies.append((proxy,anchor,source))
    error = 0.0
    valid = True
    for angle in (-28,0,28):
        build.clear_pose(rig)
        build.set_rot(rig,"head",[("Z",angle),("X",12)])
        bpy.context.view_layer.update()
        for proxy, anchor, source in proxies:
            error = max(error, geo.matrix_error(proxy.matrix_world, anchor.matrix_world))
            normal = anchor.matrix_world.to_3x3().col[2].normalized()
            depths = [(proxy.matrix_world @ v.co - anchor.matrix_world.translation).dot(normal)
                      for v in proxy.data.vertices]
            valid &= source.hide_render and min(depths) <= -.005 and max(depths) >= .055
    for proxy,_,source in proxies:
        mesh = proxy.data
        bpy.data.objects.remove(proxy,do_unlink=True)
        bpy.data.meshes.remove(mesh)
        source.hide_render = False
    build.clear_pose(rig)
    bpy.context.view_layer.update()
    return valid, error


def inspect(profile, rig, meshes, check, report, roundtrip):
    geo.inspect_model(profile, rig, meshes, check, report, roundtrip)
    phase = "glb" if roundtrip else "blend"
    actions = set(a.name for a in bpy.data.actions)
    check(phase+"_actions_exact", actions == CLIPS.keys(), str(sorted(actions)))
    if not CLIPS.keys() <= actions:
        return
    seals = {side:bpy.data.objects["SOCKET_"+slot] for side,slot in (("r","MAIN_HAND"),("l","OFF_HAND"))}
    collisions, grip_bad, min_support, max_support = [], [], 1.0, -1.0
    contact_summary = {}
    max_seam = 0.0
    for action, count in CLIPS.items():
        first = round(bpy.data.actions[action].frame_range[0])
        for index in range(1,count+1):
            validate.apply_frame(rig,action,first+index-1)
            if action in {"walk","idle","attack","attack_grip"}:
                z = build.foot_min_z(meshes)
                min_support,max_support = min(z,min_support),max(z,max_support)
            if action not in {"grip_test","attack_grip"}:
                continue
            for side in ("l","r"):
                posed, failures, joint_gap = posed_hand(profile,rig,side)
                max_seam = max(max_seam,joint_gap)
                collisions.extend((action,index,side,*f) for f in failures)
                if action == "grip_test" and index == 19 or action == "attack_grip" and side == "r":
                    gaps = grip_contacts(profile,posed,seals[side])
                    by_digit = {digit:min(gap for name,gap in gaps.items() if name.startswith(digit))
                                for digit in profile["fingers"]}
                    values = {digit:round(gap*1000,2) for digit,gap in by_digit.items()}
                    if index in (1,9,15,19):
                        contact_summary[f"{action}:{index}:{side}"] = values
                    if min(gaps.values()) < -.0012 or any(g > .007 for g in by_digit.values()):
                        grip_bad.append((action,index,side,values,round(min(gaps.values())*1000,2)))
    validate.apply_frame(rig,None,1)
    for side in ("l","r"):
        _, failures, joint_gap = posed_hand(profile,rig,side)
        max_seam = max(max_seam,joint_gap)
        collisions.extend(("T_POSE",1,side,*item) for item in failures)
    report["numbers"][phase+"_finger_qa"] = dict(collisions=collisions, max_seam_m=max_seam,
                                                 contacts_mm=contact_summary, grip_errors=grip_bad)
    check(phase+"_finger_clearance", not collisions, f"80 hand poses + T-pose, first failures={collisions[:12]}")
    check(phase+"_grip_contact", not grip_bad, f"2 static and 15 attack holds, first failures={grip_bad[:3]}")
    check(phase+"_grounded_frames", min_support>=-.0001 and max_support<=.00065,
          f"72 exported integer frames, min/max supporting sole = {min_support:.7f}/{max_support:.7f} m")
    first_walk = round(bpy.data.actions["walk"].frame_range[0])
    validate.apply_frame(rig, "walk", first_walk)
    subframe_z = []
    for frame in range(first_walk, first_walk+CLIPS["walk"]-1):
        for i in range(1,16):
            bpy.context.scene.frame_set(frame, subframe=i/16)
            subframe_z.append(build.foot_min_z(meshes))
    validate.apply_frame(rig, None, 1)
    support = dict(samples=len(subframe_z), min_z_m=min(subframe_z), max_z_m=max(subframe_z))
    report["numbers"][phase+"_walk_subframes"] = support
    check(phase+"_walk_subframes", min(subframe_z)>=-.00002 and max(subframe_z)<=.001,
          json.dumps(support))
    if not roundtrip:
        fitted, err = test_ear_swap(rig,meshes)
        check("replaceable_ears",fitted and err<1e-5,f"two socket proxies, head yaw ±28 deg, max error {err:.2e} m")
        sweep_body(profile,rig,check)


def sweep_body(profile, rig, check):
    parts = {name:profile["parts"][name] for name in build.PART_BONE}
    ranges = [
        ("head",[("Z",-30),("X",-15)],[("Z",30),("X",18)]),
        ("lowerarm_l", [("Z",-62)],[("Z",18)]), ("lowerarm_r", [("Z",-18)],[("Z",62)]),
        ("thigh_l",[("X",-27)],[("X",25)]), ("thigh_r",[("X",-27)],[("X",25)]),
        ("calf_l",[("X",2)],[("X",58)]), ("calf_r",[("X",2)],[("X",58)]),
    ]
    worst = (-1.0,"")
    for name, low, high in ranges:
        for angle in (low,high):
            build.apply_pose(rig,{name:angle})
            bpy.context.view_layer.update()
            gaps = validate.joint_separations(parts,validate.bone_matrices(rig))
            worst=max(worst,max((val,pair) for pair,val in gaps.items()))
    build.clear_pose(rig)
    bpy.context.view_layer.update()
    check("body_joint_range",worst[0]<=.025,f"14 body range poses, worst joint separation {worst}")


def make_handle(profile, socket):
    spec = profile["grip_fixture"]
    bpy.ops.mesh.primitive_cylinder_add(vertices=12,radius=spec["radius_m"],depth=spec["length_m"])
    obj = bpy.context.object
    obj.name = "QA_GRIP_CYLINDER"
    obj.parent = socket
    obj.rotation_euler.x = math.pi/2
    mat = bpy.data.materials.new("QA fixture blue")
    mat.use_nodes = True
    mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (.035,.24,.41,1)
    obj.data.materials.append(mat)
    return obj


def shots(rig, shoot):
    profile=prepare(json.loads(PROFILE.read_text(encoding="utf-8")))
    times={}
    def detail(name,focus,direction,scale):
        view=Vector(focus)+Vector(direction)
        filename=name+".png"
        times[filename]=shoot(filename,view,ortho_scale=scale,focus=focus,hide_floor=True)
    validate.apply_frame(rig,None,1)
    detail("HEAD_EAR",(0,-.03,1.482),(3.6,-1.6,1.0),1.04)
    detail("HEAD_FRONT",(0,-.03,1.482),(0,-4.0,.2),1.08)
    detail("FEET",(0,-.022,.18),(1.0,-2.0,.90),.66)
    for name,index in (("HAND_NEUTRAL",1),("HAND_OPEN",7),("HAND_FIST",13),("HAND_GRIP",19),("HAND_RELEASE",25)):
        validate.apply_frame(rig,"grip_test",index)
        focus=(-.642,-.02,.994)
        fixture = make_handle(profile,bpy.data.objects["SOCKET_MAIN_HAND"]) if index==19 else None
        detail(name,focus,(-.53,-.68,-1.2),.355)
        if fixture:
            bpy.data.objects.remove(fixture,do_unlink=True)
    validate.apply_frame(rig,"attack_grip",9)
    fixture=make_handle(profile,bpy.data.objects["SOCKET_MAIN_HAND"])
    times["ATTACK_GRIP.png"]=shoot("ATTACK_GRIP.png",(5,-7,3.3),ortho_scale=2.20)
    bpy.data.objects.remove(fixture,do_unlink=True)
    return times


if __name__ == "__main__":
    args=sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else []
    if args == ["build"]:
        build.main(PROFILE,RUN,prepare,finish)
    elif args in (["validate"],["validate","--skip-renders"]):
        if not validate.main(PROFILE,RUN,prepare,inspect,shots):
            raise RuntimeError("MANNEQUIN-GEO-02R validation failed; see validation.json")
    else:
        raise ValueError("Pass -- build or -- validate [--skip-renders]")
