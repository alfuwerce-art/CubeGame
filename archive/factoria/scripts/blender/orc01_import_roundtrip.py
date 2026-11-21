r"""ORC-01: импорт GLB в Blender 5.2.4, проверки, .blend, round-trip GLB, 3 рендера.

Запуск (headless, изолированный конфиг лаборатории):
    $env:BLENDER_USER_CONFIG="D:\Projects\CubeGame Factory\environments\blender\config"
    & 'D:\programs\Blender\5.2\blender.exe' --background --factory-startup --python-exit-code 1 `
        --python "D:\Projects\CubeGame Factory\scripts\blender\orc01_import_roundtrip.py"

Проверяет:
  1. импорт GLB (ось Z-up после конвертации, метры);
  2. 20 независимых объектов-модулей, материалы и вертексные цвета;
  3. отсутствие вырожденных граней и NaN-координат;
  4. независимое редактирование: сдвиг forearm_L не двигает остальные модули;
  5. зазоры между сегментами конечностей (по bounding box, ось Z);
  6. сохранение .blend и повторное открытие (отдельным шагом-скриптом);
  7. экспорт GLB и повторный импорт: объекты, геометрия, материалы, габариты;
  8. три ортографических рендера: front / side / 3-4 (Cycles, CPU).
"""

import json
import math
import os
import time

import bpy
from mathutils import Vector

PROJECT_ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC_GLB = os.path.join(PROJECT_ROOT, "outputs", "prototypes", "orc_01", "generated", "orc_modular_v01.glb")
OUT_DIR = os.path.join(PROJECT_ROOT, "outputs", "prototypes", "orc_01", "blender")
REP_DIR = os.path.join(PROJECT_ROOT, "outputs", "prototypes", "orc_01", "reports")
BLEND_PATH = os.path.join(OUT_DIR, "orc01_modular.blend")
RT_GLB = os.path.join(OUT_DIR, "orc01_roundtrip.glb")
REPORT = os.path.join(REP_DIR, "blender_roundtrip.json")
RENDERS = {
    "front": os.path.join(OUT_DIR, "orc01_front.png"),
    "side": os.path.join(OUT_DIR, "orc01_side.png"),
    "three_quarter": os.path.join(OUT_DIR, "orc01_three_quarter.png"),
}
LIMB_CHAINS = {
    "arm_L": ["upper_arm_L", "forearm_L", "hand_L"],
    "arm_R": ["upper_arm_R", "forearm_R", "hand_R"],
    "leg_L": ["thigh_L", "shin_L", "foot_L"],
    "leg_R": ["thigh_R", "shin_R", "foot_R"],
}

report = {
    "stage": "ORC-01/blender_import_roundtrip",
    "blender": bpy.app.version_string,
    "source_glb": SRC_GLB,
    "stages": {},
    "numbers": {},
    "issues": [],
    "artifacts": {"blend": BLEND_PATH, "roundtrip_glb": RT_GLB, "renders": RENDERS},
}
t_all = time.time()


def stage(name, ok, detail=""):
    report["stages"][name] = {"ok": bool(ok), "detail": detail}
    print(f"STAGE {name}: {'OK' if ok else 'FAIL'} {detail}")


def scene_meshes():
    return [o for o in bpy.context.scene.objects if o.type == "MESH"]


def mesh_stats(obj):
    me = obj.data
    verts = len(me.vertices)
    tris = sum(len(p.vertices) - 2 for p in me.polygons)
    min_area = None
    for poly in me.polygons:
        if min_area is None or poly.area < min_area:
            min_area = poly.area
    nan = any(any(math.isnan(c) for c in v.co) for v in me.vertices)
    color_layers = [a.name for a in me.color_attributes] if hasattr(me, "color_attributes") else []
    return {
        "vertices": verts,
        "triangles": tris,
        "min_face_area": round(min_area, 8) if min_area is not None else None,
        "has_nan": nan,
        "color_attributes": color_layers,
        "materials": [m.name if m else None for m in me.materials],
    }


def world_bbox(objs):
    lo = Vector((1e9, 1e9, 1e9))
    hi = Vector((-1e9, -1e9, -1e9))
    for obj in objs:
        for corner in obj.bound_box:
            p = obj.matrix_world @ Vector(corner)
            for i in range(3):
                lo[i] = min(lo[i], p[i])
                hi[i] = max(hi[i], p[i])
    return lo, hi


def unique_world_positions(objs):
    """Уникальные позиции вершин: Blender при экспорте разбивает вершины по нормалям,
    поэтому сравнивать сырой счётчик нельзя — сравниваем саму геометрию."""
    pts = set()
    for obj in objs:
        for v in obj.data.vertices:
            p = obj.matrix_world @ v.co
            pts.add((round(p.x, 4), round(p.y, 4), round(p.z, 4)))
    return pts


def snapshot_positions(objs):
    dg = bpy.context.evaluated_depsgraph_get()
    dg.update()
    snap = {}
    for obj in objs:
        ev = obj.evaluated_get(dg)
        me = ev.to_mesh()
        snap[obj.name] = [ev.matrix_world @ v.co for v in me.vertices][:16]
        ev.to_mesh_clear()
    return snap


def gap_checks(objs):
    """Зазор между сегментами конечностей по Z (Blender: Z — up)."""
    by_name = {o.name: o for o in objs}
    result = {}
    for chain, names in LIMB_CHAINS.items():
        per_chain = {}
        for upper, lower in zip(names, names[1:]):
            a, b = by_name.get(upper), by_name.get(lower)
            if not a or not b:
                continue
            (alo, ahi) = world_bbox([a])
            (blo, bhi) = world_bbox([b])
            gap = round(alo.z - bhi.z, 4)
            per_chain[f"{upper}->{lower}"] = {"gap_z_m": gap, "separated": gap > 0}
        result[chain] = per_chain
    return result


os.makedirs(OUT_DIR, exist_ok=True)
os.makedirs(REP_DIR, exist_ok=True)

# --------------------------------------------------------------- 1. import ---
bpy.ops.wm.read_factory_settings(use_empty=True)
t0 = time.time()
bpy.ops.import_scene.gltf(filepath=SRC_GLB)
import_seconds = round(time.time() - t0, 2)
meshes = scene_meshes()
names = sorted(o.name for o in meshes)
report["numbers"]["import_seconds"] = import_seconds
report["numbers"]["object_count"] = len(meshes)
report["numbers"]["object_names"] = names
stage("import_glb", len(meshes) == 20, f"{len(meshes)} mesh objects, {import_seconds}s")

# ------------------------------------------------- 2. stats / integrity ------
total_v = total_t = 0
per_part = {}
for obj in meshes:
    st = mesh_stats(obj)
    total_v += st["vertices"]
    total_t += st["triangles"]
    per_part[obj.name] = st
    if st["has_nan"]:
        report["issues"].append(f"{obj.name}: NaN coordinates")
lo, hi = world_bbox(meshes)
report["numbers"]["total_vertices"] = total_v
report["numbers"]["total_triangles"] = total_t
report["numbers"]["bbox_min"] = [round(v, 4) for v in lo]
report["numbers"]["bbox_max"] = [round(v, 4) for v in hi]
report["numbers"]["dimensions"] = [round(hi[i] - lo[i], 4) for i in range(3)]
report["numbers"]["per_part"] = per_part
report["numbers"]["material_count"] = len({m for st in per_part.values() for m in st["materials"] if m})
report["numbers"]["color_attribute_parts"] = sum(1 for st in per_part.values() if st["color_attributes"])

min_area = min((st["min_face_area"] for st in per_part.values()), default=None)
degenerate = [n for n, st in per_part.items() if (st["min_face_area"] or 0) <= 1e-8]
stage("geometry_integrity", total_v == 1688 and total_t == 2532 and not degenerate,
      f"verts={total_v} tris={total_t} min_face_area={min_area} degenerate={degenerate}")
stage("materials_and_colors",
      report["numbers"]["material_count"] == 1 and report["numbers"]["color_attribute_parts"] == 20,
      f"materials={report['numbers']['material_count']} parts_with_vertex_colors={report['numbers']['color_attribute_parts']}")

# axes: glTF Y-up → Blender Z-up; рост существа должен быть по Z
dims = report["numbers"]["dimensions"]
stage("axes_and_scale", abs(dims[2] - 3.13) < 0.01 and abs(dims[0] - 2.09) < 0.01 and abs(dims[1] - 0.9357) < 0.01,
      f"dimensions(XYZ)={dims} (ожидается 2.09 × 0.9357 × 3.13)")

# ------------------------------------------- 3. independent editability ------
before = snapshot_positions(meshes)
target = bpy.data.objects.get("forearm_L")
moved = False
if target:
    target.location.x += 0.1
    bpy.context.view_layer.update()
    after = snapshot_positions(meshes)
    moved_ok = all((after["forearm_L"][i] - before["forearm_L"][i]).length > 0.09 for i in range(len(before["forearm_L"])))
    others_delta = max((after[n][i] - before[n][i]).length
                       for n in before if n != "forearm_L"
                       for i in range(len(before[n])))
    target.location.x -= 0.1
    bpy.context.view_layer.update()
    moved = moved_ok and others_delta < 1e-6
    report["numbers"]["edit_test_forearm_move_m"] = round(max((after["forearm_L"][i] - before["forearm_L"][i]).length
                                                             for i in range(len(before["forearm_L"]))), 4)
    report["numbers"]["edit_test_others_max_delta_m"] = round(others_delta, 8)
stage("independent_editing", moved,
      f"forearm_L moved, others max delta={report['numbers'].get('edit_test_others_max_delta_m')}m")

# ------------------------------------------------------------- 4. gaps ------
gaps = gap_checks(meshes)
report["numbers"]["gaps_z"] = gaps
all_separated = all(v["separated"] for chain in gaps.values() for v in chain.values())
stage("limb_gaps_visible", all_separated, json.dumps(gaps, ensure_ascii=False))

# ------------------------------------------------------- 5. save .blend ------
# Снимок исходной геометрии ДО сброса сцены (после read_factory_settings ссылки на
# объекты становятся недействительными).
orig_unique = unique_world_positions(meshes)
orig_tris_by_part = {name: st["triangles"] for name, st in per_part.items()}
bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
report["numbers"]["blend_bytes"] = os.path.getsize(BLEND_PATH) if os.path.isfile(BLEND_PATH) else 0
stage("save_blend", os.path.isfile(BLEND_PATH), f"{BLEND_PATH} ({report['numbers']['blend_bytes']} B)")

# ------------------------------------------------ 6. export GLB + reimport --
bpy.ops.object.select_all(action="SELECT")
t0 = time.time()
bpy.ops.export_scene.gltf(filepath=RT_GLB, export_format="GLB", use_selection=True)
export_seconds = round(time.time() - t0, 2)
report["numbers"]["export_seconds"] = export_seconds
report["numbers"]["roundtrip_glb_bytes"] = os.path.getsize(RT_GLB) if os.path.isfile(RT_GLB) else 0
stage("export_glb", os.path.isfile(RT_GLB), f"{RT_GLB} ({report['numbers']['roundtrip_glb_bytes']} B, {export_seconds}s)")

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=RT_GLB)
rt_meshes = scene_meshes()
rt_names = sorted(o.name for o in rt_meshes)
rt_v = sum(len(o.data.vertices) for o in rt_meshes)
rt_t = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in rt_meshes)

rt_lo, rt_hi = world_bbox(rt_meshes)
rt_dims = [round(rt_hi[i] - rt_lo[i], 4) for i in range(3)]
rt_materials = len({m.name for o in rt_meshes for m in o.data.materials if m})
rt_color_parts = sum(1 for o in rt_meshes if hasattr(o.data, "color_attributes") and len(o.data.color_attributes) > 0)
rt_unique = unique_world_positions(rt_meshes)
per_part_tris_match = all(
    sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in rt_meshes if o.name == name)
    == orig_tris_by_part[name] for name in orig_tris_by_part
)

report["numbers"]["roundtrip"] = {
    "object_count": len(rt_meshes),
    "object_names_match": rt_names == names,
    "raw_vertices": rt_v,
    "unique_positions": len(rt_unique),
    "unique_positions_original": len(orig_unique),
    "positions_identical": rt_unique == orig_unique,
    "triangles": rt_t,
    "per_part_triangles_match": per_part_tris_match,
    "dimensions": rt_dims,
    "materials": rt_materials,
    "parts_with_vertex_colors": rt_color_parts,
    "bbox_min": [round(v, 4) for v in rt_lo],
    "bbox_max": [round(v, 4) for v in rt_hi],
}
same = (len(rt_meshes) == len(meshes) and rt_names == names and rt_t == total_t
        and rt_unique == orig_unique and per_part_tris_match
        and rt_materials == report["numbers"]["material_count"] and rt_color_parts == 20
        and all(abs(rt_dims[i] - dims[i]) < 1e-3 for i in range(3)))
stage("glb_roundtrip", same, json.dumps(report["numbers"]["roundtrip"], ensure_ascii=False))

# ------------------------------------------------------------ 7. renders ----
render_info = {}
try:
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 48
    scene.cycles.use_denoising = False
    scene.render.resolution_x = 900
    scene.render.resolution_y = 1200
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False

    world = bpy.data.worlds.new("ORC01_World")
    if world.node_tree is None:
        world.use_nodes = True
    if world.node_tree and "Background" in world.node_tree.nodes:
        world.node_tree.nodes["Background"].inputs[0].default_value = (0.18, 0.19, 0.21, 1.0)
        world.node_tree.nodes["Background"].inputs[1].default_value = 0.9
    scene.world = world

    target_empty = bpy.data.objects.new("ORC01_Target", None)
    target_empty.location = (0.0, 0.0, 1.56)
    scene.collection.objects.link(target_empty)

    cam_data = bpy.data.cameras.new("ORC01_Cam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = 3.7
    cam = bpy.data.objects.new("ORC01_Cam", cam_data)
    scene.collection.objects.link(cam)
    track = cam.constraints.new(type="TRACK_TO")
    track.target = target_empty
    track.track_axis = "TRACK_NEGATIVE_Z"
    track.up_axis = "UP_Y"
    scene.camera = cam

    def add_light(name, loc, energy, size):
        data = bpy.data.lights.new(name, type="AREA")
        data.energy = energy
        data.size = size
        obj = bpy.data.objects.new(name, data)
        obj.location = loc
        track2 = obj.constraints.new(type="TRACK_TO")
        track2.target = target_empty
        track2.track_axis = "TRACK_NEGATIVE_Z"
        track2.up_axis = "UP_Y"
        scene.collection.objects.link(obj)
        return obj

    add_light("Key", (-3.5, -4.0, 4.6), 900, 4.0)
    add_light("Fill", (4.0, -2.5, 2.6), 420, 5.0)
    add_light("Rim", (0.5, 4.5, 3.6), 500, 4.0)

    views = {
        "front": (0.0, -6.0, 1.56),
        "side": (6.0, 0.0, 1.56),
        "three_quarter": (4.3, -4.3, 2.35),
    }
    for name, loc in views.items():
        cam.location = loc
        bpy.context.view_layer.update()
        t0 = time.time()
        scene.render.filepath = RENDERS[name]
        bpy.ops.render.render(write_still=True)
        render_info[name] = {"path": RENDERS[name], "seconds": round(time.time() - t0, 2),
                             "bytes": os.path.getsize(RENDERS[name]) if os.path.isfile(RENDERS[name]) else 0}
    ok = all(os.path.isfile(p) and os.path.getsize(p) > 5000 for p in RENDERS.values())
    stage("render_three_views", ok, json.dumps(render_info, ensure_ascii=False))
except Exception as exc:
    report["issues"].append(f"render failed: {type(exc).__name__}: {exc}")
    stage("render_three_views", False, f"{type(exc).__name__}: {exc}")

report["numbers"]["renders"] = render_info
report["seconds_total"] = round(time.time() - t_all, 1)
report["result"] = "PASS" if report["stages"] and all(s["ok"] for s in report["stages"].values()) else "FAIL"
with open(REPORT, "w", encoding="utf-8") as fh:
    json.dump(report, fh, ensure_ascii=False, indent=2)
print("ORC01_REPORT=" + REPORT)
print("ORC01_RESULT=" + report["result"])
for name, st in report["stages"].items():
    print(f"ORC01_STAGE {name}={'PASS' if st['ok'] else 'FAIL'} :: {st['detail']}")
if report["issues"]:
    print("ORC01_ISSUES=" + json.dumps(report["issues"], ensure_ascii=False))
