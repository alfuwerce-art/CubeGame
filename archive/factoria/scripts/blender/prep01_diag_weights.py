"""PREP-01 диагностика №3: почему Parent with Automatic Weights даёт нулевые веса.

Три случая на одном и том же Rigify-риге:
  A) один куб в области груди (проверка самой операции auto weights);
  B) вытянутый блок, покрывающий фигуру (проверка на «крупноблочной» геометрии);
  C) прежний манекен из 160 отдельных боксов (проверка исходной конструкции).

Печатает количество групп с реальными весами и вершины без весов.
"""

import json

import addon_utils
import bpy
from mathutils import Vector

out = {}
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.preferences.addon_enable(module="rigify")
bpy.ops.object.armature_human_metarig_add()
metarig = bpy.context.active_object
bpy.ops.pose.rigify_generate()
rig = bpy.data.objects.get("rig")
out["rig_bones"] = len(rig.data.bones)
out["rig_deform"] = sum(1 for b in rig.data.bones if b.use_deform)
out["rig_matrix"] = [list(map(float, row)) for row in rig.matrix_world]


def weight_stats(obj):
    nonzero_groups = 0
    for vg in obj.vertex_groups:
        idx = vg.index
        if any(idx in [g.group for g in v.groups] and any(
                g.group == idx and g.weight > 1e-6 for g in v.groups) for v in obj.data.vertices):
            nonzero_groups += 1
    unweighted = sum(1 for v in obj.data.vertices if sum(g.weight for g in v.groups) < 1e-6)
    return {"groups": len(obj.vertex_groups), "groups_with_weights": nonzero_groups,
            "vertices": len(obj.data.vertices), "unweighted": unweighted}


def case(name, obj):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    result = None
    try:
        result = str(bpy.ops.object.parent_set(type="ARMATURE_AUTO"))
    except Exception as exc:
        result = f"{type(exc).__name__}: {exc}"
    out[name] = {"parent_result": result, "stats": weight_stats(obj)}
    obj.select_set(False)


# A: один куб в районе груди
bpy.ops.mesh.primitive_cube_add(size=0.45, location=(0.0, 0.0, 1.15))
A = bpy.context.active_object
A.name = "CASE_A_cube"
case("case_A_small_cube", A)

# B: вытянутый блок на всю фигуру
bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0.0, 0.0, 0.9))
B = bpy.context.active_object
B.name = "CASE_B_block"
B.scale = (0.35, 0.25, 0.9)
bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
case("case_B_whole_block", B)

# C: прежняя конструкция — по боксу на каждую deform-кость
import bmesh

mesh = bpy.data.meshes.new("CASE_C")
bm = bmesh.new()
for bone in rig.data.bones:
    if not bone.use_deform:
        continue
    mat = bone.matrix_local
    basis = mat.to_3x3()
    center = mat @ Vector((0.0, bone.length * 0.5, 0.0))
    hx = hy = 0.05
    hz = max(bone.length, 0.01) * 0.55
    local = [(-hx, -hy, -hz), (hx, -hy, -hz), (hx, hy, -hz), (-hx, hy, -hz),
             (-hx, -hy, hz), (hx, -hy, hz), (hx, hy, hz), (-hx, hy, hz)]
    vs = [bm.verts.new(center + basis @ Vector(p)) for p in local]
    for idx in ((0, 1, 2, 3), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)):
        bm.faces.new([vs[i] for i in idx])
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
bm.to_mesh(mesh)
bm.free()
C = bpy.data.objects.new("CASE_C_boxes", mesh)
bpy.context.scene.collection.objects.link(C)
case("case_C_boxes", C)

print("PREP01_DIAG3_JSON=" + json.dumps(out, ensure_ascii=False, indent=2))
