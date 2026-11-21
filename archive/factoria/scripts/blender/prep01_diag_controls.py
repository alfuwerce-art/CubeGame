"""PREP-01 диагностика №5: фактические имена контрольных костей Rigify 5.2.4 и проверка весов.

Открывает сохранённый outputs/blender/prep01_rigify_skinned.blend и печатает:
  - имена костей, управляющих рукой/ногой (кандидаты на контрольные позы);
  - статистику сумм весов по вершинам;
  - какие из кандидатов реально деформируют меш (поворот на 45–60°).
"""

import json
import math

import bpy

out = {}
bpy.ops.wm.open_mainfile(filepath=r"D:\Projects\CubeGame Factory\outputs\blender\prep01_rigify_skinned.blend")
rig = bpy.data.objects.get("rig")
mesh = next(o for o in bpy.data.objects if o.type == "MESH" and o.name.startswith("CG_PREP01"))
out["rig"] = rig.name if rig else None
out["mesh"] = mesh.name if mesh else None
out["bones_total"] = len(rig.pose.bones)
out["bone_name_samples"] = {
    "arm": sorted(b.name for b in rig.pose.bones if "arm" in b.name.lower())[:24],
    "thigh": sorted(b.name for b in rig.pose.bones if "thigh" in b.name.lower())[:12],
    "hand": sorted(b.name for b in rig.pose.bones if "hand" in b.name.lower())[:12],
}
out["bone_collections"] = [c.name for c in rig.data.collections] if hasattr(rig.data, "collections") else None

sums = []
for v in mesh.data.vertices:
    sums.append(sum(g.weight for g in v.groups))
sums.sort()
out["weights"] = {
    "verts": len(sums),
    "min": round(sums[0], 4),
    "p50": round(sums[len(sums) // 2], 4),
    "max": round(sums[-1], 4),
    "below_0_98": sum(1 for s in sums if s < 0.98),
    "above_1_02": sum(1 for s in sums if s > 1.02),
}
groups_with_weights = set()
for v in mesh.data.vertices:
    for g in v.groups:
        if g.weight > 1e-6:
            groups_with_weights.add(mesh.vertex_groups[g.group].name)
out["groups_with_weights"] = len(groups_with_weights)


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
    bpy.context.view_layer.update()


rest = eval_coords()
trials = {}
for name in sorted({b.name for b in rig.pose.bones}):
    low = name.lower()
    if not any(k in low for k in ("upper_arm", "forearm", "thigh", "shin")):
        continue
    if name.startswith(("DEF-", "MCH-", "ORG-")):
        continue
    if ".L" not in name and ".R" not in name:
        continue
    reset()
    pb = rig.pose.bones.get(name)
    pb.rotation_mode = "XYZ"
    pb.rotation_euler[0] = math.radians(45.0)
    moved = max((a - b).length for a, b in zip(rest, eval_coords()))
    trials[name] = round(moved, 4)
reset()
out["control_bone_rotation_displacement_m"] = trials
print("PREP01_DIAG5_JSON=" + json.dumps(out, ensure_ascii=False, indent=2))
