"""PREP-01 диагностика №2: официальный способ включения Rigify и состояние параметров.

Проверяет:
  - состояние аддона до/после bpy.ops.preferences.addon_enable(module='rigify');
  - какой модуль rigify загружен (bundled 5.2.4 или пользовательская копия);
  - определены ли динамические параметры (make_custom_pivot) до генерации;
  - проходит ли bpy.ops.pose.rigify_generate().
"""

import json
import sys

import addon_utils
import bpy

out = {}
out["check_before"] = list(addon_utils.check("rigify"))
out["in_prefs_before"] = "rigify" in bpy.context.preferences.addons

try:
    res = bpy.ops.preferences.addon_enable(module="rigify")
    out["addon_enable_ops"] = str(res)
except Exception as exc:
    out["addon_enable_ops"] = f"{type(exc).__name__}: {exc}"

out["check_after"] = list(addon_utils.check("rigify"))
out["in_prefs_after"] = "rigify" in bpy.context.preferences.addons
mod = bpy.context.preferences.addons.get("rigify")
out["rigify_module_file"] = getattr(getattr(mod, "module", None), "__file__", None)
out["metarig_ops"] = sorted(n for n in dir(bpy.ops.object) if "metarig" in n)
out["rigify_pose_ops"] = sorted(n for n in dir(bpy.ops.pose) if "rigify" in n)

try:
    from rigify import properties as rigify_props
    out["RigifyParameters_has_pivot"] = hasattr(rigify_props.RigifyParameters, "make_custom_pivot")
    if hasattr(rigify_props.RigifyParameters, "make_custom_pivot"):
        out["pivot_descriptor"] = str(rigify_props.RigifyParameters.make_custom_pivot)
except Exception as exc:
    out["rigify_props_import"] = f"{type(exc).__name__}: {exc}"

bpy.ops.object.armature_human_metarig_add()
metarig = bpy.context.active_object
out["metarig"] = {"name": metarig.name, "bones": len(metarig.data.bones)}
out["metarig_rigify_type"] = metarig.data.get("rigify_rig_basename")
try:
    bpy.ops.pose.rigify_generate()
    out["generate"] = "OK"
except Exception as exc:
    out["generate"] = f"{type(exc).__name__}: {exc}"

rig = bpy.data.objects.get("rig")
if rig:
    out["rig"] = {
        "bones": len(rig.data.bones),
        "deform": sum(1 for b in rig.data.bones if b.use_deform),
    }
out["objects"] = [f"{o.name}:{o.type}" for o in bpy.data.objects]
out["python"] = sys.version.split()[0]
print("PREP01_DIAG2_JSON=" + json.dumps(out, ensure_ascii=False, indent=2))
