r"""PREP-01: Blender headless smoke test Rigify + skinning + экспорт.

Сценарий (всё на собственной процедурной тестовой фигуре, без ассетов CubeGame):
  1. включение штатного аддона Rigify и проверка его доступности через API;
  2. добавление human metarig, генерация control rig (bpy.ops.pose.rigify_generate);
  3. построение собственного манекена: по одному боксу на каждую deform-кость
     сгенерированного рига (собственная геометрия, созданная скриптом);
  4. Parent with Automatic Weights + Armature Modifier;
  5. проверки весов (vertex groups, сумма весов, вершины без весов);
  6. контрольные позы через control bones и через deform-кости, замер смещения
     вершин из depsgraph (числовое доказательство деформации);
  7. рендер evidence-изображений (rest / posed) в outputs/blender;
  8. сохранение .blend и экспорт FBX, повторный импорт FBX в пустую сцену;
  9. отчёт JSON в outputs/reports/prep01_rigify_skinning.json.

Запуск:
  "D:\programs\Blender\5.2\blender.exe" --background --factory-startup \
      --python-exit-code 1 --python scripts\blender\prep01_rigify_skinning.py

Скрипт ничего не пишет вне D:\Projects\CubeGame Factory\outputs.
"""

import json
import math
import os
import sys
import time
import traceback

import bpy
import bmesh
import addon_utils
from mathutils import Vector

PROJECT_ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT_DIR = os.path.join(PROJECT_ROOT, "outputs", "blender")
REP_DIR = os.path.join(PROJECT_ROOT, "outputs", "reports")
BLEND_PATH = os.path.join(OUT_DIR, "prep01_rigify_skinned.blend")
FBX_PATH = os.path.join(OUT_DIR, "prep01_rigify_skinned.fbx")
RENDER_REST = os.path.join(OUT_DIR, "prep01_rest_pose.png")
RENDER_POSED = os.path.join(OUT_DIR, "prep01_control_pose.png")
REPORT_PATH = os.path.join(REP_DIR, "prep01_rigify_skinning.json")

report = {
    "stage": "PREP-01/rigify_skinning",
    "blender_version": bpy.app.version_string,
    "python_version": sys.version.split()[0],
    "background": bpy.app.background,
    "stages": {},
    "checks": {},
    "numbers": {},
    "issues": [],
    "artifacts": {
        "blend": BLEND_PATH,
        "fbx": FBX_PATH,
        "render_rest": RENDER_REST,
        "render_posed": RENDER_POSED,
    },
}
t_start = time.time()


def stage(name, ok, detail=""):
    report["stages"][name] = {"ok": bool(ok), "detail": detail}
    print(f"STAGE {name}: {'OK' if ok else 'FAIL'} {detail}")
    return ok


for d in (OUT_DIR, REP_DIR):
    os.makedirs(d, exist_ok=True)

# ---------------------------------------------------------------- 0. clean ---
bpy.ops.wm.read_factory_settings(use_empty=True)

# ------------------------------------------------------------- 1. Rigify ----
# ВАЖНО (проверено на 5.2.4): addon_utils.enable("rigify") регистрирует операторы, но
# оставляет PropertyGroup без динамических параметров — bpy.ops.pose.rigify_generate()
# падает с "'RigifyParameters' object has no attribute 'make_custom_pivot'".
# Рабочий путь — официальный оператор включения аддона.
rigify_ready = False
previous = list(addon_utils.check("rigify"))
try:
    if not previous[1]:
        bpy.ops.preferences.addon_enable(module="rigify")
    rigify_ready = addon_utils.check("rigify")[1]
    detail = f"before={previous} after={list(addon_utils.check('rigify'))}"
except Exception as exc:  # pragma: no cover
    detail = f"{type(exc).__name__}: {exc}"
    report["issues"].append(f"rigify enable failed: {detail}")
stage("enable_rigify", rigify_ready, detail)

metarig_ops = sorted(n for n in dir(bpy.ops.object) if "metarig" in n)
rigify_pose_ops = sorted(n for n in dir(bpy.ops.pose) if "rigify" in n)
report["checks"]["available_metarig_ops"] = metarig_ops
report["checks"]["available_rigify_pose_ops"] = rigify_pose_ops

# ----------------------------------------------------------- 2. metarig -----
metarig = None
try:
    bpy.ops.object.select_all(action="DESELECT")
    if hasattr(bpy.ops.object, "armature_human_metarig_add"):
        bpy.ops.object.armature_human_metarig_add()
        metarig = bpy.context.active_object
    else:
        # Резервный путь: аддон может публиковать метариги как ассеты библиотеки.
        for op_name in metarig_ops:
            if "human" in op_name:
                getattr(bpy.ops.object, op_name)()
                metarig = bpy.context.active_object
                break
    ok = metarig is not None and metarig.type == "ARMATURE"
    stage("add_human_metarig", ok, f"object={getattr(metarig, 'name', None)} bones={len(metarig.data.bones) if ok else 0}")
    report["numbers"]["metarig_bones"] = len(metarig.data.bones) if ok else 0
except Exception as exc:
    report["issues"].append(f"metarig add failed: {type(exc).__name__}: {exc}")
    stage("add_human_metarig", False, str(exc))
    ok = False

# ------------------------------------------------------ 3. generate rig -----
rig = None
if ok:
    errors = []
    generated = False
    for label, enter_pose in (("object_mode", False), ("pose_mode", True)):
        try:
            bpy.ops.object.select_all(action="DESELECT")
            metarig.select_set(True)
            bpy.context.view_layer.objects.active = metarig
            if enter_pose:
                bpy.ops.object.mode_set(mode="POSE")
            bpy.ops.pose.rigify_generate()
            generated = True
            break
        except Exception as exc:
            errors.append(f"{label}: {type(exc).__name__}: {exc}")
        finally:
            if bpy.context.mode != "OBJECT":
                try:
                    bpy.ops.object.mode_set(mode="OBJECT")
                except Exception:
                    pass

    rig = bpy.data.objects.get("rig")
    if rig is None:
        rig = next((o for o in bpy.data.objects if o.type == "ARMATURE" and o is not metarig), None)
    deform = [b.name for b in rig.data.bones if b.use_deform] if rig else []
    controls = [b.name for b in rig.data.bones if not b.use_deform] if rig else []
    ok = generated and rig is not None and len(deform) > 20
    stage("rigify_generate", ok, f"via={errors or 'object_mode'} rig={getattr(rig, 'name', None)} "
                                 f"deform={len(deform)} control={len(controls)}")
    if rig:
        report["numbers"]["generated_bones_total"] = len(rig.data.bones)
        report["numbers"]["generated_deform_bones"] = len(deform)
        report["numbers"]["generated_control_bones"] = len(controls)
        report["checks"]["deform_bone_examples"] = deform[:12]
        report["checks"]["control_bone_examples"] = controls[:12]
    else:
        for e in errors:
            report["issues"].append(f"rigify_generate {e}")

# --------------------------------------------------------- 4. mannequin -----
WIDTHS = (  # (подстрока в имени кости, полуширина X, полуглубина Y)
    ("eye", 0.014, 0.014),
    ("finger", 0.011, 0.011),
    ("thumb", 0.012, 0.012),
    ("palm", 0.035, 0.020),
    ("hand", 0.040, 0.026),
    ("forearm", 0.045, 0.045),
    ("upper_arm", 0.052, 0.052),
    ("shoulder", 0.045, 0.040),
    ("head", 0.100, 0.095),
    ("neck", 0.070, 0.065),
    ("breast", 0.045, 0.045),
    ("pelvis", 0.115, 0.085),
    ("spine", 0.120, 0.090),
    ("thigh", 0.078, 0.078),
    ("shin", 0.062, 0.062),
    ("foot", 0.050, 0.045),
    ("toe", 0.045, 0.040),
    ("heel", 0.030, 0.030),
)


def width_for(bone_name):
    low = bone_name.lower().replace("def-", "")
    for token, wx, wz in WIDTHS:
        if token in low:
            return wx, wz
    return 0.030, 0.030


def count_components(obj):
    """Число связных компонент меша (для доказательства единой оболочки)."""
    bm2 = bmesh.new()
    bm2.from_mesh(obj.data)
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


def box_verts(center, basis, hx, hy, hz):
    local = [
        (-hx, -hy, -hz), (hx, -hy, -hz), (hx, hy, -hz), (-hx, hy, -hz),
        (-hx, -hy, hz), (hx, -hy, hz), (hx, hy, hz), (-hx, hy, hz),
    ]
    return [center + basis @ Vector(p) for p in local]


mannequin = None
if rig:
    try:
        mesh = bpy.data.meshes.new("CG_PREP01_Mannequin")
        bm = bmesh.new()
        count = 0
        for bone in rig.data.bones:
            if not bone.use_deform:
                continue
            hx, hz = width_for(bone.name)
            # минимум 0.035 — иначе voxel remesh теряет тонкие сегменты (пальцы)
            hx = max(hx, 0.035)
            hz = max(hz, 0.035)
            hy = max(bone.length, 0.01) * 0.55  # лёгкое перекрытие соседних сегментов
            mat = bone.matrix_local
            basis = mat.to_3x3()
            center = mat @ Vector((0.0, bone.length * 0.5, 0.0))
            vs = [bm.verts.new(v) for v in box_verts(center, basis, hx, hy, hz)]
            for idx in ((0, 1, 2, 3), (4, 5, 6, 7), (0, 1, 5, 4),
                        (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)):
                bm.faces.new([vs[i] for i in idx])
            count += 1
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        bm.to_mesh(mesh)
        bm.free()
        mannequin = bpy.data.objects.new("CG_PREP01_Mannequin", mesh)
        bpy.context.scene.collection.objects.link(mannequin)
        report["numbers"]["mannequin_segments"] = count
        report["numbers"]["mannequin_vertices_raw"] = len(mesh.vertices)

        # Отдельные закрытые боксы ломают Bone Heat Weighting (проверено: 0 весов на 1280
        # вершинах). Склеиваем их в одну связную оболочку штатным voxel remesh.
        bpy.ops.object.select_all(action="DESELECT")
        mannequin.select_set(True)
        bpy.context.view_layer.objects.active = mannequin
        remesh = mannequin.modifiers.new("VoxelRemesh", type="REMESH")
        remesh.mode = "VOXEL"
        remesh.voxel_size = 0.03
        bpy.ops.object.modifier_apply(modifier=remesh.name)
        report["numbers"]["mannequin_vertices"] = len(mannequin.data.vertices)
        report["numbers"]["mannequin_components"] = count_components(mannequin)
        stage("build_mannequin", count > 0 and report["numbers"]["mannequin_components"] == 1,
              f"segments={count} verts_raw={report['numbers']['mannequin_vertices_raw']} "
              f"verts_remeshed={report['numbers']['mannequin_vertices']} "
              f"components={report['numbers']['mannequin_components']}")
    except Exception as exc:
        report["issues"].append(f"mannequin build failed: {type(exc).__name__}: {exc}")
        stage("build_mannequin", False, str(exc))

# ---------------------------------------- 5. automatic weights + modifier ---
weights_ok = False
if mannequin and rig:
    try:
        bpy.ops.object.select_all(action="DESELECT")
        mannequin.select_set(True)
        rig.select_set(True)
        bpy.context.view_layer.objects.active = rig
        bpy.ops.object.parent_set(type="ARMATURE_AUTO")

        mods = [m for m in mannequin.modifiers if m.type == "ARMATURE"]
        unweighted = 0
        bad_sum = 0
        sums = []
        for v in mannequin.data.vertices:
            s = sum(g.weight for g in v.groups)
            sums.append(s)
            if s < 1e-6:
                unweighted += 1
            elif abs(s - 1.0) > 0.02:
                bad_sum += 1
        sums.sort()
        deform_names = {b.name for b in rig.data.bones if b.use_deform}
        used_groups = {g.name for g in mannequin.vertex_groups} & deform_names
        weights_ok = bool(mods) and mods[0].object is rig and unweighted == 0 and len(used_groups) > 20
        report["numbers"]["vertex_groups_total"] = len(mannequin.vertex_groups)
        report["numbers"]["vertex_groups_deform"] = len(used_groups)
        report["numbers"]["vertices_unweighted"] = unweighted
        report["numbers"]["vertices_weight_sum_off"] = bad_sum
        report["numbers"]["weight_sum_min"] = round(sums[0], 4) if sums else None
        report["numbers"]["weight_sum_p50"] = round(sums[len(sums) // 2], 4) if sums else None
        report["numbers"]["weight_sum_max"] = round(sums[-1], 4) if sums else None
        report["checks"]["armature_modifier_target"] = mods[0].object.name if mods else None
        stage("automatic_weights", weights_ok,
              f"groups={len(mannequin.vertex_groups)} unweighted={unweighted} "
              f"weight_sum_min={report['numbers']['weight_sum_min']} "
              f"sum_off_from_1={bad_sum} modifier={bool(mods)}")
    except Exception as exc:
        report["issues"].append(f"automatic weights failed: {type(exc).__name__}: {exc}")
        stage("automatic_weights", False, f"{type(exc).__name__}: {exc}")


# ------------------------------------------------------ deform evaluation ---
def eval_coords(obj):
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    dg.update()
    ev = obj.evaluated_get(dg)
    me = ev.to_mesh()
    coords = [v.co.copy() for v in me.vertices]
    ev.to_mesh_clear()
    return coords


def reset_pose(arm):
    for pb in arm.pose.bones:
        pb.rotation_mode = "XYZ"
        pb.rotation_euler = (0.0, 0.0, 0.0)
        pb.rotation_quaternion = (1.0, 0.0, 0.0, 0.0)
        pb.location = (0.0, 0.0, 0.0)
        pb.scale = (1.0, 1.0, 1.0)
    bpy.context.view_layer.update()


def pose_by_names(arm, name_list, angle_deg, axis=0):
    used, missing = [], []
    for name in name_list:
        pb = arm.pose.bones.get(name)
        if pb is None:
            missing.append(name)
            continue
        pb.rotation_mode = "XYZ"
        e = list(pb.rotation_euler)
        e[axis] += math.radians(angle_deg)
        pb.rotation_euler = e
        used.append(name)
    bpy.context.view_layer.update()
    return used, missing


def max_disp(a, b):
    if len(a) != len(b):
        return None
    return max((p - q).length for p, q in zip(a, b))


def apply_ik_pose(arm):
    """Поза через контрольные кости (IK-режим Rigify — режим по умолчанию)."""
    applied = []
    for name, loc in (("hand_ik.L", (-0.25, -0.35, 0.35)),
                      ("hand_ik.R", (0.12, -0.22, 0.28)),
                      ("foot_ik.R", (0.0, -0.25, 0.12))):
        pb = arm.pose.bones.get(name)
        if pb is not None:
            pb.location = loc
            applied.append(name)
    bpy.context.view_layer.update()
    return applied


def apply_fk_pose(arm):
    """Поза через FK-контролы (требует переключения IK_FK=1 на parent-костях)."""
    applied = []
    for name in ("upper_arm_parent.L", "thigh_parent.R"):
        pb = arm.pose.bones.get(name)
        if pb is not None and "IK_FK" in pb.keys():
            pb["IK_FK"] = 1.0
            applied.append(f"{name}[IK_FK=1]")
    for name, axis, deg in (("upper_arm_fk.L", 0, 50.0), ("forearm_fk.L", 0, -60.0),
                            ("thigh_fk.R", 0, 35.0), ("shin_fk.R", 0, -55.0)):
        pb = arm.pose.bones.get(name)
        if pb is not None:
            pb.rotation_mode = "XYZ"
            e = list(pb.rotation_euler)
            e[axis] += math.radians(deg)
            pb.rotation_euler = e
            applied.append(name)
    bpy.context.view_layer.update()
    return applied


pose_results = {}
if mannequin and rig and weights_ok:
    try:
        rest = eval_coords(mannequin)
        report["numbers"]["rest_max_abs_z"] = round(max(c.z for c in rest), 4)

        # A. Прямой поворот deform-кости. Ожидаемый результат 0: у Rigify DEF-кости
        #    ведомы constraints от контрольных костей. Фиксируем как факт API.
        reset_pose(rig)
        used, missing = pose_by_names(rig, ["DEF-upper_arm.L"], 45.0, axis=0)
        disp_def = max_disp(rest, eval_coords(mannequin))
        reset_pose(rig)

        # B. Контрольные кости, IK-режим (по умолчанию)
        ik_applied = apply_ik_pose(rig)
        disp_ik = max_disp(rest, eval_coords(mannequin))
        reset_pose(rig)

        # C. Контрольные кости, FK-режим (переключение IK_FK)
        fk_applied = apply_fk_pose(rig)
        disp_fk = max_disp(rest, eval_coords(mannequin))
        reset_pose(rig)

        # Финальная поза для evidence-рендера
        apply_ik_pose(rig)
        apply_fk_pose(rig)
        posed = eval_coords(mannequin)
        disp_final = max_disp(rest, posed)

        pose_results = {
            "deform_bone_direct": {"bones": used, "missing": missing,
                                   "max_displacement_m": round(disp_def, 4),
                                   "note": "0 ожидаемо: DEF-кости Rigify управляются constraints"},
            "ik_controls": {"bones": ik_applied, "max_displacement_m": round(disp_ik, 4)},
            "fk_controls": {"bones": fk_applied, "max_displacement_m": round(disp_fk, 4)},
            "render_pose": {"bones": ik_applied + fk_applied,
                            "max_displacement_m": round(disp_final, 4)},
        }
        report["numbers"]["pose_deform_max_disp_m"] = round(disp_def, 4)
        report["numbers"]["pose_ik_max_disp_m"] = round(disp_ik, 4)
        report["numbers"]["pose_fk_max_disp_m"] = round(disp_fk, 4)
        ok = disp_ik > 0.02 and disp_fk > 0.02
        stage("pose_deformation", ok,
              f"ik={round(disp_ik, 3)}m fk={round(disp_fk, 3)}m def_direct={round(disp_def, 3)}m")
    except Exception as exc:
        report["issues"].append(f"pose test failed: {type(exc).__name__}: {exc}")
        stage("pose_deformation", False, str(exc))
        posed = None
else:
    posed = None

# ------------------------------------------------------------- render -------
render_info = {}
if mannequin:
    try:
        scene = bpy.context.scene
        scene.render.engine = "CYCLES"
        scene.cycles.device = "CPU"
        scene.cycles.samples = 24
        scene.cycles.use_denoising = False
        scene.render.resolution_x = 640
        scene.render.resolution_y = 800
        scene.render.resolution_percentage = 100
        scene.render.image_settings.file_format = "PNG"
        scene.render.film_transparent = False

        world = bpy.data.worlds.new("W")
        if world.node_tree is None:
            world.use_nodes = True
        if world.node_tree and "Background" in world.node_tree.nodes:
            world.node_tree.nodes["Background"].inputs[0].default_value = (0.05, 0.05, 0.06, 1.0)
        scene.world = world

        tmp_material = bpy.data.materials.new("PREP01_MannequinMat")
        if tmp_material.node_tree is None:
            tmp_material.use_nodes = True
        bsdf = tmp_material.node_tree.nodes.get("Principled BSDF") if tmp_material.node_tree else None
        if bsdf:
            bsdf.inputs["Base Color"].default_value = (0.55, 0.62, 0.75, 1.0)
            bsdf.inputs["Roughness"].default_value = 0.55
        mannequin.data.materials.append(tmp_material)

        cam_data = bpy.data.cameras.new("PREP01_Cam")
        cam_data.lens = 45.0
        cam = bpy.data.objects.new("PREP01_Cam", cam_data)
        cam.location = (0.9, -4.1, 1.05)
        cam.rotation_euler = (math.radians(90.0), 0.0, math.radians(12.0))
        scene.collection.objects.link(cam)
        scene.camera = cam

        sun_data = bpy.data.lights.new("PREP01_Sun", type="SUN")
        sun_data.energy = 4.0
        sun = bpy.data.objects.new("PREP01_Sun", sun_data)
        sun.rotation_euler = (math.radians(55.0), math.radians(15.0), math.radians(40.0))
        scene.collection.objects.link(sun)

        t0 = time.time()
        reset_pose(rig)
        scene.render.filepath = RENDER_REST
        bpy.ops.render.render(write_still=True)
        t_rest = round(time.time() - t0, 2)

        t0 = time.time()
        apply_ik_pose(rig)
        apply_fk_pose(rig)
        scene.render.filepath = RENDER_POSED
        bpy.ops.render.render(write_still=True)
        t_posed = round(time.time() - t0, 2)

        import hashlib

        def md5(path):
            with open(path, "rb") as fh:
                return hashlib.md5(fh.read()).hexdigest()

        h_rest = md5(RENDER_REST) if os.path.isfile(RENDER_REST) else None
        h_posed = md5(RENDER_POSED) if os.path.isfile(RENDER_POSED) else None

        render_info = {
            "engine": scene.render.engine,
            "device": scene.cycles.device,
            "samples": scene.cycles.samples,
            "resolution": [scene.render.resolution_x, scene.render.resolution_y],
            "seconds_rest": t_rest,
            "seconds_posed": t_posed,
            "rest_png": RENDER_REST if os.path.isfile(RENDER_REST) else None,
            "posed_png": RENDER_POSED if os.path.isfile(RENDER_POSED) else None,
            "rest_md5": h_rest,
            "posed_md5": h_posed,
        }
        stage("render_evidence",
              h_rest is not None and h_posed is not None and h_rest != h_posed,
              f"rest={t_rest}s posed={t_posed}s distinct_images={h_rest != h_posed}")
    except Exception as exc:
        report["issues"].append(f"render failed: {type(exc).__name__}: {exc}")
        stage("render_evidence", False, f"{type(exc).__name__}: {exc}")

report["numbers"]["render"] = render_info
report["numbers"]["pose_tests"] = pose_results

# -------------------------------------------------------- save .blend -------
if mannequin and rig:
    try:
        bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
        stage("save_blend", os.path.isfile(BLEND_PATH), BLEND_PATH)
        report["numbers"]["blend_size_bytes"] = os.path.getsize(BLEND_PATH)
    except Exception as exc:
        report["issues"].append(f"save_blend failed: {exc}")
        stage("save_blend", False, str(exc))

# -------------------------------------------------------------- FBX --------
fbx_report = {}
if mannequin and rig:
    try:
        fbx_ops = [n for n in dir(bpy.ops.export_scene) if "fbx" in n]
        report["checks"]["fbx_export_ops"] = fbx_ops
        if not fbx_ops:
            addon_utils.enable("io_scene_fbx", default_set=False, persistent=False)
            fbx_ops = [n for n in dir(bpy.ops.export_scene) if "fbx" in n]
        bpy.ops.object.select_all(action="DESELECT")
        mannequin.select_set(True)
        rig.select_set(True)
        bpy.context.view_layer.objects.active = rig
        kwargs = dict(filepath=FBX_PATH, use_selection=True, bake_anim=False,
                      add_leaf_bones=False, mesh_smooth_type="FACE")
        try:
            bpy.ops.export_scene.fbx(**kwargs)
        except TypeError:
            kwargs.pop("mesh_smooth_type", None)
            bpy.ops.export_scene.fbx(**kwargs)
        size = os.path.getsize(FBX_PATH) if os.path.isfile(FBX_PATH) else 0
        stage("export_fbx", size > 0, f"{FBX_PATH} bytes={size}")
        fbx_report["export"] = {"path": FBX_PATH, "bytes": size, "ops": fbx_ops}
    except Exception as exc:
        report["issues"].append(f"export_fbx failed: {type(exc).__name__}: {exc}")
        stage("export_fbx", False, f"{type(exc).__name__}: {exc}")

# ------------------------------------------- FBX round-trip (import back) ---
if os.path.isfile(FBX_PATH):
    try:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=FBX_PATH)
        imported_arms = [o for o in bpy.data.objects if o.type == "ARMATURE"]
        imported_meshes = [o for o in bpy.data.objects if o.type == "MESH"]
        arm = imported_arms[0] if imported_arms else None
        me_obj = imported_meshes[0] if imported_meshes else None
        info = {
            "armatures": len(imported_arms),
            "meshes": len(imported_meshes),
            "bones": len(arm.data.bones) if arm else 0,
            "vertex_groups": len(me_obj.vertex_groups) if me_obj else 0,
        }
        deform_ok = False
        if arm and me_obj:
            rest = eval_coords(me_obj)
            pb = next((b for b in arm.pose.bones if b.name.startswith("DEF-upper_arm")), None)
            if pb is None:
                pb = next((b for b in arm.pose.bones if "upper_arm" in b.name), None)
            if pb:
                pb.rotation_mode = "XYZ"
                pb.rotation_euler[0] += math.radians(45.0)
                moved = max_disp(rest, eval_coords(me_obj))
                info["max_displacement_after_rotation_m"] = moved
                deform_ok = (moved or 0) > 0.02
        fbx_report["import"] = info
        ok = info["armatures"] >= 1 and info["meshes"] >= 1 and deform_ok
        stage("fbx_roundtrip", ok, json.dumps(info, ensure_ascii=False))
    except Exception as exc:
        report["issues"].append(f"fbx roundtrip failed: {type(exc).__name__}: {exc}")
        stage("fbx_roundtrip", False, f"{type(exc).__name__}: {exc}")

report["numbers"]["fbx"] = fbx_report
report["seconds_total"] = round(time.time() - t_start, 1)
report["result"] = "PASS" if all(s["ok"] for s in report["stages"].values()) and report["stages"] else "FAIL"

with open(REPORT_PATH, "w", encoding="utf-8") as fh:
    json.dump(report, fh, ensure_ascii=False, indent=2)

print("PREP01_REPORT=" + REPORT_PATH)
print("PREP01_RESULT=" + report["result"])
for name, st in report["stages"].items():
    print(f"PREP01_STAGE {name}={ 'PASS' if st['ok'] else 'FAIL' } :: {st['detail']}")
if report["issues"]:
    print("PREP01_ISSUES=" + json.dumps(report["issues"], ensure_ascii=False))
