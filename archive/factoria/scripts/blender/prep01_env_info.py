"""PREP-01: сведения о версиях встроенных аддонов и окружении Blender.

Печатает версии ядра, Rigify и FBX-экспортёра, пути скриптов, состояние
пользовательского конфигурационного каталога лаборатории.
"""

import json
import os
import sys

import addon_utils
import bpy

out = {
    "blender_version": bpy.app.version_string,
    "python_version": sys.version.split()[0],
    "system_info": bpy.app.build_platform.decode() if isinstance(bpy.app.build_platform, bytes) else str(bpy.app.build_platform),
    "build_hash": bpy.app.build_hash.decode() if isinstance(bpy.app.build_hash, bytes) else str(bpy.app.build_hash),
    "build_date": bpy.app.build_date.decode() if isinstance(bpy.app.build_date, bytes) else str(bpy.app.build_date),
    "scripts_dir": bpy.utils.resource_path("LOCAL"),
    "user_config_dir": os.environ.get("BLENDER_USER_CONFIG"),
    "addons": {},
}
for name in ("rigify", "io_scene_fbx", "io_scene_gltf2", "bl_pkg", "pose_library"):
    info = {"found": False}
    mod = next((m for m in addon_utils.modules() if m.__name__ == name), None)
    if mod is not None:
        bl_info = getattr(mod, "bl_info", {})
        info = {
            "found": True,
            "version": list(bl_info.get("version", [])),
            "file": getattr(mod, "__file__", None),
            "enabled": list(addon_utils.check(name))[1],
        }
    out["addons"][name] = info

out["fbx_export_available"] = hasattr(bpy.ops.export_scene, "fbx")
out["fbx_import_available"] = hasattr(bpy.ops.import_scene, "fbx")
print("PREP01_ENV_JSON=" + json.dumps(out, ensure_ascii=False, indent=2))
