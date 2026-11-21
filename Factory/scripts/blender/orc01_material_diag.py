"""ORC-01 диагностика: как импортированный GLB выглядит в материале Blender.

Открывает сохранённый .blend и печатает:
  - список материалов и узлы/связи (чтобы понять, подключены ли vertex colors);
  - имена color attributes и средний цвет по нескольким модулям;
  - проверку, что сцена открывается (количество объектов/вершин).
"""

import json

import bpy

BLEND = r"D:\Projects\CubeGame Factory\outputs\prototypes\orc_01\blender\orc01_modular.blend"
bpy.ops.wm.open_mainfile(filepath=BLEND)

meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
out = {
    "objects": len(meshes),
    "vertices": sum(len(o.data.vertices) for o in meshes),
    "blend_reopened": True,
    "materials": {},
    "color_samples": {},
}
for mat in bpy.data.materials:
    nodes = []
    if mat.node_tree:
        for n in mat.node_tree.nodes:
            entry = {"type": n.type, "name": n.name}
            if n.type == "ATTRIBUTE":
                entry["attribute_name"] = getattr(n, "attribute_name", None)
            if n.type == "BSDF_PRINCIPLED":
                entry["base_color"] = list(n.inputs["Base Color"].default_value)
                entry["base_color_linked"] = bool(n.inputs["Base Color"].links)
            nodes.append(entry)
        links = [f"{l.from_node.name}.{l.from_socket.name} -> {l.to_node.name}.{l.to_socket.name}"
                 for l in mat.node_tree.links]
    else:
        links = []
    out["materials"][mat.name] = {"nodes": nodes, "links": links,
                                  "blend_method": getattr(mat, "surface_render_method", None)}

for name in ("head", "torso", "foot_L", "hair"):
    obj = bpy.data.objects.get(name)
    if not obj:
        continue
    me = obj.data
    layer = me.color_attributes[0] if len(me.color_attributes) else None
    if layer is None:
        continue
    acc = [0.0, 0.0, 0.0]
    for i in range(min(64, len(layer.data))):
        c = layer.data[i].color
        acc[0] += c[0]
        acc[1] += c[1]
        acc[2] += c[2]
    n = min(64, len(layer.data))
    out["color_samples"][name] = {
        "attribute": layer.name,
        "domain": layer.domain,
        "data_type": layer.data_type,
        "mean_rgb": [round(v / n, 3) for v in acc],
    }
print("ORC01_MATERIAL_DIAG=" + json.dumps(out, ensure_ascii=False, indent=2))
