r"""PREP-01: минимальный headless smoke test Blender.

Запуск (из корня проекта, изолированный пользовательский каталог конфигурации):
    "D:\programs\Blender\5.2\blender.exe" --background --factory-startup \
        --python-exit-code 1 --python scripts\blender\prep01_bg_smoke.py

Проверяет: background-режим, версия Blender/Python, работа bpy с геометрией,
сохранение .blend в лабораторный outputs. Ничего в CubeGame не пишет.
"""

import json
import os
import sys

import bpy

PROJECT_ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT_BLEND = os.path.join(PROJECT_ROOT, "outputs", "blender", "prep01_bg_smoke.blend")

report = {
    "stage": "PREP-01/bg_smoke",
    "blender_version": bpy.app.version_string,
    "blender_version_tuple": list(bpy.app.version),
    "python_version": sys.version.split()[0],
    "background": bpy.app.background,
    "output_blend": OUT_BLEND,
    "checks": {},
    "issues": [],
}

# --- 1. Чистая сцена -------------------------------------------------------
bpy.ops.wm.read_factory_settings(use_empty=True)
report["checks"]["empty_scene"] = "PASS" if len(bpy.data.objects) == 0 else "FAIL"

# --- 2. Создание собственной геометрии, без внешних ассетов ---------------
bpy.ops.mesh.primitive_cube_add(size=1.0, location=(0.0, 0.0, 0.5))
cube = bpy.context.active_object
cube.name = "PREP01_SmokeCube"
report["checks"]["mesh_created"] = "PASS" if cube.type == "MESH" else "FAIL"
report["checks"]["mesh_vertices"] = len(cube.data.vertices)

# --- 3. Сохранение в лабораторию -----------------------------------------
os.makedirs(os.path.dirname(OUT_BLEND), exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=OUT_BLEND)
report["checks"]["blend_saved"] = "PASS" if os.path.isfile(OUT_BLEND) else "FAIL"
report["checks"]["blend_size_bytes"] = os.path.getsize(OUT_BLEND) if os.path.isfile(OUT_BLEND) else 0

# --- 4. Доступность аддонов, важных для PREP-01 --------------------------
import addon_utils  # noqa: E402  (после инициализации bpy)

addons = {m.__name__: addon_utils.check(m.__name__)[0] for m in addon_utils.modules()}
report["checks"]["rigify_addon_present"] = "rigify" in addons
report["checks"]["rigify_addon_enabled"] = bool(addons.get("rigify"))
report["checks"]["fbx_exporter_ops"] = "export_scene.fbx" if hasattr(bpy.ops.export_scene, "fbx") else "MISSING"

meta_ops = [n for n in dir(bpy.ops.object) if "metarig" in n or "rigify" in n]
report["checks"]["metarig_object_ops"] = meta_ops
pose_ops = [n for n in dir(bpy.ops.pose) if "rigify" in n]
report["checks"]["rigify_pose_ops"] = pose_ops
if "rigify" not in addons:
    report["issues"].append("Rigify addon module not found")

print("PREP01_BG_SMOKE_REPORT_JSON=" + json.dumps(report, ensure_ascii=False))
print("PREP01_BG_SMOKE_RESULT=" + ("PASS" if all(
    v == "PASS" for k, v in report["checks"].items() if isinstance(v, str) and k in
    ("empty_scene", "mesh_created", "blend_saved")
) else "FAIL"))
