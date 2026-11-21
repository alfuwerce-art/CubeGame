"""PREP-01 диагностика №4: единая оболочка манекена (Voxel Remesh) перед Automatic Weights.

Гипотеза: Bone Heat Weighting тотально падает на 160 отдельных закрытых боксах,
но работает на связной оболочке. Проверяет: число связных компонент до/после
voxel remesh, веса после auto weights, фактическую деформацию при повороте кости.
"""

import json
import math

import bmesh
import bpy
from mathutils import Vector

out = {}
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.preferences.addon_enable(module="rigify")
bpy.ops.object.armature_human_metarig_add()
bpy.ops.pose.rigify_generate()
rig = bpy.data.objects.get("rig")
out["deform_bones"] = sum(1 for b in rig.data.bones if b.use_deform)

WIDTHS = (
    ("eye", 0.03), ("finger", 0.02), ("thumb", 0.02), ("palm", 0.04),
    ("hand", 0.045), ("forearm", 0.05), ("upper_arm", 0.055), ("shoulder", 0.05),
    ("head", 0.10), ("neck", 0.07), ("breast", 0.05), ("pelvis", 0.115),
    ("spine", 0.12), ("thigh", 0.08), ("shin", 0.065), ("foot", 0.055),
    ("toe", 0.05), ("heel", 0.04),
)


def half_width(name):
    low = name.lower().replace("def-", "")
    for token, w in WIDTHS:
        if token in low:
            return w
    return 0.04


mesh = bpy.data.meshes.new("DIAG4")
bm = bmesh.new()
for bone in rig.data.bones:
    if not bone.use_deform:
        continue
    mat = bone.matrix_local
    basis = mat.to_3x3()
    center = mat @ Vector((0.0, bone.length * 0.5, 0.0))
    h = max(half_width(bone.name), 0.035)
    hy = max(bone.length, 0.01) * 0.55
    local = [(-h, -hy, -h), (h, -hy, -h), (h, hy, -h), (-h, hy, -h),
             (-h, -hy, h), (h, -hy, h), (h, hy, h), (-h, hy, h)]
    vs = [bm.verts.new(center + basis @ Vector(p)) for p in local]
    for idx in ((0, 1, 2, 3), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)):
        bm.faces.new([vs[i] for i in idx])
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
bm.to_mesh(mesh)
bm.free()
obj = bpy.data.objects.new("DIAG4_Mannequin", mesh)
bpy.context.scene.collection.objects.link(obj)
obj.select_set(True)
bpy.context.view_layer.objects.active = obj


def components(o):
    bm2 = bmesh.new()
    bm2.from_mesh(o.data)
    seen = set()
    comps = 0
    for v in bm2.verts:
        if v.index in seen:
            continue
        comps += 1
        stack = [v]
        while stack:
            cur = stack.pop()
            if cur.index in seen:
                continue
            seen.add(cur.index)
            for e in cur.link_edges:
                other = e.other_vert(cur)
                if other.index not in seen:
                    stack.append(other)
    bm2.free()
    return comps


out["verts_before_remesh"] = len(obj.data.vertices)
out["components_before_remesh"] = components(obj)

mod = obj.modifiers.new("VoxelRemesh", type="REMESH")
mod.mode = "VOXEL"
mod.voxel_size = 0.03
bpy.ops.object.modifier_apply(modifier=mod.name)
out["verts_after_remesh"] = len(obj.data.vertices)
out["components_after_remesh"] = components(obj)

bpy.ops.object.select_all(action="DESELECT")
obj.select_set(True)
rig.select_set(True)
bpy.context.view_layer.objects.active = rig
try:
    out["parent_result"] = str(bpy.ops.object.parent_set(type="ARMATURE_AUTO"))
except Exception as exc:
    out["parent_result"] = f"{type(exc).__name__}: {exc}"

unweighted = sum(1 for v in obj.data.vertices if sum(g.weight for g in v.groups) < 1e-6)
groups_used = 0
for vg in obj.vertex_groups:
    idx = vg.index
    if any(any(g.group == idx and g.weight > 1e-6 for g in v.groups) for v in obj.data.vertices):
        groups_used += 1
out["groups"] = len(obj.vertex_groups)
out["groups_with_weights"] = groups_used
out["unweighted_vertices"] = unweighted


def eval_coords(o):
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    dg.update()
    ev = o.evaluated_get(dg)
    me = ev.to_mesh()
    co = [v.co.copy() for v in me.vertices]
    ev.to_mesh_clear()
    return co


pb = rig.pose.bones.get("DEF-upper_arm.L")
rest = eval_coords(obj)
if pb:
    pb.rotation_mode = "XYZ"
    pb.rotation_euler[0] += math.radians(60.0)
    moved = max((a - b).length for a, b in zip(rest, eval_coords(obj)))
    out["max_displacement_m"] = moved
    pb.rotation_euler[0] -= math.radians(60.0)
    bpy.context.view_layer.update()
out["result"] = "PASS" if unweighted == 0 and groups_used > 20 and out.get("max_displacement_m", 0) > 0.02 else "FAIL"
print("PREP01_DIAG4_JSON=" + json.dumps(out, ensure_ascii=False, indent=2))
