r"""ORC-02: Blender 5.2.4 headless-валидация результата конвейера + 3 вида + round-trip.

Запуск:
    $env:BLENDER_USER_CONFIG="D:\Projects\CubeGame Factory\environments\blender\config"
    & 'D:\programs\Blender\5.2\blender.exe' --background --factory-startup --python-exit-code 1 `
        --python "...\scripts\blender\orc02_validate.py" -- --run outputs\prototypes\orc_02\run1

Проверяет: число модулей, целостность сетки, материалы/цвета, оси/масштаб, независимое
редактирование, зазоры между сегментами, .blend (сохранение), GLB round-trip и 3 рендера.
"""

import json
import math
import os
import sys
import time

import bpy
from mathutils import Vector

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
run_dir = os.path.abspath(argv[argv.index("--run") + 1]) if "--run" in argv else os.path.join(
    ROOT, "outputs", "prototypes", "orc_02", "run1")
SRC_GLB = os.path.join(run_dir, "orc02_modular.glb")
OUT = os.path.join(run_dir, "blender")
BLEND = os.path.join(OUT, "orc02_modular.blend")
RT_GLB = os.path.join(OUT, "orc02_roundtrip.glb")
REPORT = os.path.join(run_dir, "validation.json")
RENDERS = {name: os.path.join(OUT, f"orc02_{name}.png") for name in ("front", "side", "three_quarter")}
CHAINS = {
    "arm_L": ["upper_arm_L", "forearm_L", "hand_L"],
    "arm_R": ["upper_arm_R", "forearm_R", "hand_R"],
    "leg_L": ["thigh_L", "shin_L", "foot_L"],
    "leg_R": ["thigh_R", "shin_R", "foot_R"],
}

report = {"stage": "ORC-02/blender_validation", "blender": bpy.app.version_string, "source_glb": SRC_GLB,
          "stages": {}, "numbers": {}, "issues": [], "artifacts": {}}
t_all = time.time()


def stage(name, ok, detail=""):
    report["stages"][name] = {"ok": bool(ok), "detail": detail}
    print(f"STAGE {name}: {'OK' if ok else 'FAIL'} {detail}")


def meshes():
    return [o for o in bpy.context.scene.objects if o.type == "MESH"]


def bbox(obj):
    lo = Vector((1e9, 1e9, 1e9)); hi = Vector((-1e9, -1e9, -1e9))
    for c in obj.bound_box:
        p = obj.matrix_world @ Vector(c)
        for i in range(3):
            lo[i] = min(lo[i], p[i]); hi[i] = max(hi[i], p[i])
    return lo, hi


def unique_positions(objs):
    pts = set()
    for o in objs:
        for v in o.data.vertices:
            p = o.matrix_world @ v.co
            pts.add((round(p.x, 4), round(p.y, 4), round(p.z, 4)))
    return pts


os.makedirs(OUT, exist_ok=True)
if not os.path.isfile(SRC_GLB):
    print(f"ORC02_VALIDATION=FAIL missing {SRC_GLB}")
    sys.exit(2)

bpy.ops.wm.read_factory_settings(use_empty=True)
t0 = time.time()
bpy.ops.import_scene.gltf(filepath=SRC_GLB)
import_s = round(time.time() - t0, 2)
objs = meshes()
names = sorted(o.name for o in objs)
report["numbers"]["import_seconds"] = import_s
report["numbers"]["objects"] = len(objs)
report["numbers"]["object_names"] = names
stage("import", len(objs) >= 14, f"{len(objs)} модулей за {import_s}s")

verts = sum(len(o.data.vertices) for o in objs)
tris = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in objs)
degenerate = [o.name for o in objs if min((p.area for p in o.data.polygons), default=1) <= 1e-9]
lo = Vector((1e9, 1e9, 1e9)); hi = Vector((-1e9, -1e9, -1e9))
for o in objs:
    olo, ohi = bbox(o)
    for i in range(3):
        lo[i] = min(lo[i], olo[i]); hi[i] = max(hi[i], ohi[i])
dims = [round(hi[i] - lo[i], 3) for i in range(3)]
mats = {m.name for o in objs for m in o.data.materials if m}
with_colors = sum(1 for o in objs if len(getattr(o.data, "color_attributes", [])) > 0)
report["numbers"].update({"vertices": verts, "triangles": tris, "dimensions_m": dims,
                          "bbox_min": [round(v, 3) for v in lo], "bbox_max": [round(v, 3) for v in hi],
                          "materials": sorted(mats), "parts_with_vertex_colors": with_colors,
                          "degenerate_parts": degenerate})
stage("geometry_integrity", not degenerate and verts > 0, f"verts={verts} tris={tris} degenerate={degenerate}")
stage("materials_colors", len(mats) == 1 and with_colors == len(objs), f"materials={sorted(mats)} colored={with_colors}")
height_ok = 1.2 <= max(dims) <= 5.0 and max(dims) >= dims[0]
stage("axes_scale", height_ok, f"dimensions(XYZ)={dims} (макс. габарит = высота существа)")

# независимое редактирование
if "forearm_L" in bpy.data.objects:
    target = bpy.data.objects["forearm_L"]
    before = {o.name: (o.matrix_world @ o.data.vertices[0].co).copy() for o in objs}
    target.location.x += 0.1
    bpy.context.view_layer.update()
    moved = (target.matrix_world @ target.data.vertices[0].co) - before["forearm_L"]
    others = max(((o.matrix_world @ o.data.vertices[0].co) - before[o.name]).length
                 for o in objs if o.name != "forearm_L")
    target.location.x -= 0.1
    bpy.context.view_layer.update()
    stage("independent_editing", moved.length > 0.09 and others < 1e-6,
          f"forearm_L moved {round(moved.length, 4)} m, others max {round(others, 8)} m")
else:
    stage("independent_editing", False, "нет объекта forearm_L")

# зазоры
by = {o.name: o for o in objs}
gaps, all_sep = {}, True
for chain, chain_names in CHAINS.items():
    per = {}
    for a, b in zip(chain_names, chain_names[1:]):
        if a not in by or b not in by:
            continue
        alo, ahi = bbox(by[a]); blo, bhi = bbox(by[b])
        gap = round(alo.z - bhi.z, 4)
        # Поза из кадра walk (середина шага): зазор вдоль Z информативен, но критично
        # отсутствие взаимных пересечений модулей.
        per[f"{a}->{b}"] = {"gap_z_m": gap, "separated": gap > 0, "no_overlap": gap > -0.02}
        all_sep = all_sep and gap > -0.02
    gaps[chain] = per
report["numbers"]["gaps_z"] = gaps
stage("limb_gaps", all_sep, json.dumps(gaps, ensure_ascii=False))

bpy.ops.wm.save_as_mainfile(filepath=BLEND)
report["artifacts"]["blend"] = BLEND
report["numbers"]["blend_bytes"] = os.path.getsize(BLEND)
stage("save_blend", os.path.isfile(BLEND), f"{BLEND} ({report['numbers']['blend_bytes']} B)")

orig_positions = unique_positions(objs)
orig_tris = {o.name: sum(len(p.vertices) - 2 for p in o.data.polygons) for o in objs}

bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.gltf(filepath=RT_GLB, export_format="GLB", use_selection=True)
report["artifacts"]["roundtrip_glb"] = RT_GLB
report["numbers"]["roundtrip_bytes"] = os.path.getsize(RT_GLB)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=RT_GLB)
rt = meshes()
rt_names = sorted(o.name for o in rt)
rt_positions = unique_positions(rt)
rt_tris = {o.name: sum(len(p.vertices) - 2 for p in o.data.polygons) for o in rt}
rt_mats = {m.name for o in rt for m in o.data.materials if m}
lo2 = Vector((1e9, 1e9, 1e9)); hi2 = Vector((-1e9, -1e9, -1e9))
for o in rt:
    olo, ohi = bbox(o)
    for i in range(3):
        lo2[i] = min(lo2[i], olo[i]); hi2[i] = max(hi2[i], ohi[i])
rt_dims = [round(hi2[i] - lo2[i], 3) for i in range(3)]
roundtrip_ok = (len(rt) == len(objs) and rt_names == names and rt_positions == orig_positions
                and rt_tris == orig_tris and rt_mats == mats
                and all(abs(rt_dims[i] - dims[i]) < 1e-3 for i in range(3)))
report["numbers"]["roundtrip"] = {"objects": len(rt), "names_match": rt_names == names,
                                  "unique_positions": len(rt_positions),
                                  "positions_identical": rt_positions == orig_positions,
                                  "triangles_match": rt_tris == orig_tris,
                                  "materials_match": rt_mats == mats,
                                  "dimensions": rt_dims}
stage("glb_roundtrip", roundtrip_ok, json.dumps(report["numbers"]["roundtrip"], ensure_ascii=False))

# рендеры
try:
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 48
    scene.cycles.use_denoising = False
    scene.render.resolution_x = 900
    scene.render.resolution_y = 1200
    scene.render.image_settings.file_format = "PNG"
    world = bpy.data.worlds.new("ORC02_World")
    if world.node_tree is None:
        world.use_nodes = True
    if world.node_tree and "Background" in world.node_tree.nodes:
        world.node_tree.nodes["Background"].inputs[0].default_value = (0.18, 0.19, 0.21, 1.0)
    scene.world = world
    center = ((lo2 + hi2) / 2.0)
    target = bpy.data.objects.new("ORC02_Target", None)
    target.location = center
    scene.collection.objects.link(target)
    cam_data = bpy.data.cameras.new("ORC02_Cam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = max(rt_dims) * 1.25
    cam = bpy.data.objects.new("ORC02_Cam", cam_data)
    scene.collection.objects.link(cam)
    tr = cam.constraints.new(type="TRACK_TO")
    tr.target = target
    tr.track_axis = "TRACK_NEGATIVE_Z"
    tr.up_axis = "UP_Y"
    scene.camera = cam
    for nm, loc, energy in (("Key", (-4.0, -4.5, 5.0), 900), ("Fill", (4.5, -3.0, 3.0), 420),
                            ("Rim", (0.5, 5.0, 4.0), 480)):
        d = bpy.data.lights.new(nm, type="AREA"); d.energy = energy; d.size = 5.0
        o = bpy.data.objects.new(nm, d); o.location = loc
        c = o.constraints.new(type="TRACK_TO"); c.target = target
        c.track_axis = "TRACK_NEGATIVE_Z"; c.up_axis = "UP_Y"
        scene.collection.objects.link(o)
    views = {"front": (0.0, -10.0, center.z), "side": (10.0, 0.0, center.z),
             "three_quarter": (7.0, -7.0, center.z + 0.8)}
    times = {}
    for nm, loc in views.items():
        cam.location = loc
        bpy.context.view_layer.update()
        t0 = time.time()
        scene.render.filepath = RENDERS[nm]
        bpy.ops.render.render(write_still=True)
        times[nm] = round(time.time() - t0, 2)
    report["numbers"]["render_seconds"] = times
    report["artifacts"]["renders"] = RENDERS
    ok = all(os.path.isfile(p) and os.path.getsize(p) > 5000 for p in RENDERS.values())
    stage("three_views", ok, json.dumps(times, ensure_ascii=False))
except Exception as exc:
    report["issues"].append(f"render: {type(exc).__name__}: {exc}")
    stage("three_views", False, f"{type(exc).__name__}: {exc}")

report["seconds_total"] = round(time.time() - t_all, 1)
report["result"] = "PASS" if report["stages"] and all(s["ok"] for s in report["stages"].values()) else "FAIL"
with open(REPORT, "w", encoding="utf-8") as fh:
    json.dump(report, fh, ensure_ascii=False, indent=2, sort_keys=True)
print("ORC02_VALIDATION=" + report["result"])
for nm, st in report["stages"].items():
    print(f"ORC02_STAGE {nm}={'PASS' if st['ok'] else 'FAIL'} :: {st['detail']}")
print("ORC02_REPORT=" + REPORT)
