"""PREP-01 диагностика №6: какие контролы Rigify реально деформируют меш.

Rigify: FK-контролы (upper_arm_fk.L и т.д.) дают эффект только при IK_FK=1,
поэтому проверяются оба режима плюс IK-таргеты и управление корпусом/головой.
"""

import json
import math

import bpy

out = {}
bpy.ops.wm.open_mainfile(filepath=r"D:\Projects\CubeGame Factory\outputs\blender\prep01_rigify_skinned.blend")
rig = bpy.data.objects.get("rig")
mesh = next(o for o in bpy.data.objects if o.type == "MESH" and o.name.startswith("CG_PREP01"))


def eval_coords():
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    dg.update()
    ev = mesh.evaluated_get(dg)
    me = ev.to_mesh()
    co = [v.co.copy() for v in me.vertices]
    ev.to_mesh_clear()
    return co


def reset():
    for pb in rig.pose.bones:
        pb.rotation_mode = "XYZ"
        pb.rotation_euler = (0.0, 0.0, 0.0)
        pb.rotation_quaternion = (1.0, 0.0, 0.0, 0.0)
        pb.location = (0.0, 0.0, 0.0)
    for name in ("upper_arm_parent.L", "upper_arm_parent.R", "thigh_parent.L", "thigh_parent.R"):
        pb = rig.pose.bones.get(name)
        if pb is not None and "IK_FK" in pb.keys():
            pb["IK_FK"] = 0.0
    bpy.context.view_layer.update()


def disp():
    return round(max((a - b).length for a, b in zip(rest, eval_coords())), 4)


reset()
rest = eval_coords()

results = {}

# 1) IK targets: перенос рук/ног
reset()
for name, loc in (("hand_ik.L", (-0.25, -0.35, 0.35)), ("hand_ik.R", (0.1, -0.2, 0.25)),
                  ("foot_ik.R", (0.0, -0.25, 0.1))):
    pb = rig.pose.bones.get(name)
    if pb:
        pb.location = loc
results["ik_targets_location"] = disp()

# 2) FK-режим + вращение FK-контролов
reset()
for name in ("upper_arm_parent.L", "thigh_parent.R"):
    pb = rig.pose.bones.get(name)
    if pb is not None and "IK_FK" in pb.keys():
        pb["IK_FK"] = 1.0
for name, axis, deg in (("upper_arm_fk.L", 0, 50.0), ("forearm_fk.L", 0, -60.0),
                        ("thigh_fk.R", 0, 35.0), ("shin_fk.R", 0, -55.0)):
    pb = rig.pose.bones.get(name)
    if pb:
        pb.rotation_mode = "XYZ"
        e = list(pb.rotation_euler)
        e[axis] += math.radians(deg)
        pb.rotation_euler = e
results["fk_mode_rotation"] = disp()

# 3) Корпус и голова
reset()
for name, axis, deg in (("torso", 2, 25.0), ("head", 2, 25.0), ("head", 0, 15.0)):
    pb = rig.pose.bones.get(name)
    if pb:
        pb.rotation_mode = "XYZ"
        e = list(pb.rotation_euler)
        e[axis] += math.radians(deg)
        pb.rotation_euler = e
results["torso_head_rotation"] = disp()

reset()
out["trials"] = results
out["best"] = max(results, key=results.get) if results else None
out["ik_fk_props"] = {n: (rig.pose.bones[n]["IK_FK"] if n in rig.pose.bones and "IK_FK" in rig.pose.bones[n].keys() else None)
                      for n in ("upper_arm_parent.L", "thigh_parent.R")}
print("PREP01_DIAG6_JSON=" + json.dumps(out, ensure_ascii=False, indent=2))
