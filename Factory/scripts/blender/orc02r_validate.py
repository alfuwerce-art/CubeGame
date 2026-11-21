r"""ORC-02R: Blender headless-стадия — валидация новой модели, .blend, .glb, 3 вида.

Запуск (вызывается раннером, без кликов):
    $env:BLENDER_USER_CONFIG="D:\Projects\CubeGame Factory\environments\blender\config"
    & 'D:\programs\Blender\5.2\blender.exe' --background --factory-startup --python-exit-code 1 `
        --python "...\scripts\blender\orc02r_validate.py" -- --run outputs\prototypes\orc_02r\run1
"""

import json
import os
import sys
import time

import bpy
from mathutils import Vector

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
RUN = os.path.abspath(argv[argv.index("--run") + 1]) if "--run" in argv else os.path.join(
    ROOT, "outputs", "prototypes", "orc_02r", "run1")
SRC_GLB = os.path.join(RUN, "orc_new.glb")
OUT = os.path.join(RUN, "blender")
BLEND = os.path.join(OUT, "orc_new.blend")
RT_GLB = os.path.join(OUT, "orc_new_roundtrip.glb")
REPORT = os.path.join(RUN, "validation.json")
RENDERS = {name: os.path.join(OUT, f"orc_new_{name}.png") for name in ("front", "side", "three_quarter")}
CHAINS = {
    "arm_L": ["upper_arm_L", "forearm_L", "hand_L"],
    "arm_R": ["upper_arm_R", "forearm_R", "hand_R"],
    "leg_L": ["thigh_L", "shin_L", "foot_L"],
    "leg_R": ["thigh_R", "shin_R", "foot_R"],
    "spine": ["pelvis", "torso", "head"],
}
EXPECTED = ["head", "eyes", "brows", "mouth", "hair", "torso", "pelvis",
            "upper_arm_L", "forearm_L", "hand_L", "upper_arm_R", "forearm_R", "hand_R",
            "thigh_L", "shin_L", "foot_L", "thigh_R", "shin_R", "foot_R",
            "pouch", "weapon_club"]

report = {"stage": "ORC-02R/blender_validation", "blender": bpy.app.version_string,
          "source_glb": SRC_GLB, "stages": {}, "numbers": {}, "issues": [], "artifacts": {}}
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


def tri_count(o):
    return sum(len(p.vertices) - 2 for p in o.data.polygons)


os.makedirs(OUT, exist_ok=True)
if not os.path.isfile(SRC_GLB):
    print(f"ORC02R_VALIDATION=FAIL missing {SRC_GLB}")
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
missing = [n for n in EXPECTED if n not in names]
stage("import", not missing and len(objs) == len(EXPECTED),
      f"{len(objs)} модулей за {import_s}s, отсутствуют: {missing}")

verts = sum(len(o.data.vertices) for o in objs)
tris = sum(tri_count(o) for o in objs)
degenerate = [o.name for o in objs if min((p.area for p in o.data.polygons), default=1) <= 1e-9]
nan = [o.name for o in objs if any(
    any(v != v or abs(v) > 1e6 for v in vert.co) for vert in o.data.vertices)]
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
                          "degenerate_parts": degenerate, "nan_parts": nan})
stage("geometry_integrity", not degenerate and not nan and verts > 0,
      f"verts={verts} tris={tris} degenerate={degenerate} nan={nan}")
stage("materials_colors", len(mats) == 1 and with_colors == len(objs),
      f"materials={sorted(mats)} colored={with_colors}")
height_ok = 1.2 <= max(dims) <= 3.0 and dims[2] >= dims[0] and abs(lo[2]) < 1e-3
stage("axes_scale", height_ok,
      f"dimensions(XYZ)={dims} низ модели z={round(lo[2], 4)} (пол з=0, Y-up→Z-up корректен)")

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

# стыки: зазор по Z + отсутствие пересечения AABB между соседними сегментами
by = {o.name: o for o in objs}
gaps, all_sep, overlaps = {}, True, []
for chain, chain_names in CHAINS.items():
    per = {}
    for a, b in zip(chain_names, chain_names[1:]):
        if a not in by or b not in by:
            continue
        alo, ahi = bbox(by[a]); blo, bhi = bbox(by[b])
        lower, upper = (alo, ahi), (blo, bhi)
        if alo.z > blo.z:
            lower, upper = (blo, bhi), (alo, ahi)
        gap = round(upper[0].z - lower[1].z, 4)
        inter = all(upper[0][i] <= lower[1][i] and lower[0][i] <= upper[1][i]
                    for i in range(3) if i != 2)
        per[f"{a}|{b}"] = {"gap_z_m": gap, "aabb_overlap_xy": bool(inter)}
        if gap < 0:
            all_sep = False
        if inter and gap < 0:
            overlaps.append(f"{a}|{b}")
    gaps[chain] = per
report["numbers"]["joint_gaps"] = gaps
stage("joint_gaps", all_sep and not overlaps,
      f"минимальный зазор ok={all_sep}, пересечения={overlaps}")

bpy.ops.wm.save_as_mainfile(filepath=BLEND)
report["artifacts"]["blend"] = BLEND
report["numbers"]["blend_bytes"] = os.path.getsize(BLEND)
stage("save_blend", os.path.isfile(BLEND), f"{BLEND} ({report['numbers']['blend_bytes']} B)")

orig_positions = unique_positions(objs)
orig_tris = {o.name: tri_count(o) for o in objs}

bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.gltf(filepath=RT_GLB, export_format="GLB", use_selection=True)
report["artifacts"]["roundtrip_glb"] = RT_GLB
report["numbers"]["roundtrip_bytes"] = os.path.getsize(RT_GLB)

bpy.ops.wm.open_mainfile(filepath=BLEND)
reopen = meshes()
reopen_ok = sorted(o.name for o in reopen) == names
stage("blend_reopen", reopen_ok and len(reopen) == len(objs), f"{len(reopen)} объектов после reopen")

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=RT_GLB)
rt = meshes()
rt_names = sorted(o.name for o in rt)
rt_positions = unique_positions(rt)
rt_tris = {o.name: tri_count(o) for o in rt}
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
                                  "materials_match": rt_mats == mats, "dimensions": rt_dims}
stage("glb_roundtrip", roundtrip_ok, json.dumps(report["numbers"]["roundtrip"], ensure_ascii=False))

# ------------------------------------------------------------------ рендеры ----
try:
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 96
    scene.cycles.use_denoising = True
    scene.render.resolution_x = 900
    scene.render.resolution_y = 1300
    scene.render.film_transparent = False
    scene.render.image_settings.file_format = "PNG"
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "None"
    scene.view_settings.exposure = -0.25

    # фон: мягкий градиент мира
    world = bpy.data.worlds.new("ORC02R_World")
    world.use_nodes = True
    bg = world.node_tree.nodes["Background"]
    bg.inputs[0].default_value = (0.045, 0.05, 0.058, 1.0)
    bg.inputs[1].default_value = 1.0
    scene.world = world

    center = (lo + hi) / 2.0
    height = hi[2] - lo[2]
    target = bpy.data.objects.new("ORC02R_Target", None)
    target.location = (0.0, 0.0, lo[2] + height * 0.52)
    scene.collection.objects.link(target)

    # нейтральный пол с матовым материалом — даёт контактную тень
    floor_mesh = bpy.data.meshes.new("ORC02R_Floor")
    verts = [(-20, -20, 0), (20, -20, 0), (20, 20, 0), (-20, 20, 0)]
    floor_mesh.from_pydata(verts, [], [(0, 1, 2, 3)])
    floor = bpy.data.objects.new("ORC02R_Floor", floor_mesh)
    scene.collection.objects.link(floor)
    floor_mat = bpy.data.materials.new("ORC02R_FloorMat")
    floor_mat.use_nodes = True
    bsdf = floor_mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = (0.10, 0.105, 0.115, 1.0)
    bsdf.inputs["Roughness"].default_value = 0.65
    floor.data.materials.append(floor_mat)

    cam_data = bpy.data.cameras.new("ORC02R_Cam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = height * 1.18
    cam = bpy.data.objects.new("ORC02R_Cam", cam_data)
    scene.collection.objects.link(cam)
    tr = cam.constraints.new(type="TRACK_TO")
    tr.target = target
    tr.track_axis = "TRACK_NEGATIVE_Z"
    tr.up_axis = "UP_Y"
    scene.camera = cam

    for nm, loc, energy, size in (
        ("Key", (-3.0, -4.4, 4.2), 460, 2.6),
        ("Fill", (4.2, -3.4, 2.4), 170, 4.0),
        ("Rim", (1.0, 4.4, 4.0), 320, 2.4),
        ("Top", (0.0, -0.6, 6.0), 110, 5.0),
    ):
        d = bpy.data.lights.new(nm, type="AREA")
        d.energy = energy
        d.size = size
        o = bpy.data.objects.new(nm, d)
        o.location = loc
        c = o.constraints.new(type="TRACK_TO")
        c.target = target
        c.track_axis = "TRACK_NEGATIVE_Z"
        c.up_axis = "UP_Y"
        scene.collection.objects.link(o)

    dist = max(height, 2.0) * 3.2
    views = {"front": (0.0, -dist, target.location.z, "ORTHO"),
             "side": (dist, 0.0, target.location.z, "ORTHO"),
             "three_quarter": (-dist * 0.52, -dist * 0.70, target.location.z + height * 0.22,
                               "PERSP")}
    times = {}
    for nm, (vx, vy, vz, cam_type) in views.items():
        cam_data.type = cam_type
        if cam_type == "ORTHO":
            cam_data.ortho_scale = height * 1.18
        else:
            cam_data.lens = 62.0
        cam.location = (vx, vy, vz)
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
print("ORC02R_VALIDATION=" + report["result"])
for nm, st in report["stages"].items():
    print(f"ORC02R_STAGE {nm}={'PASS' if st['ok'] else 'FAIL'} :: {st['detail']}")
print("ORC02R_REPORT=" + REPORT)
