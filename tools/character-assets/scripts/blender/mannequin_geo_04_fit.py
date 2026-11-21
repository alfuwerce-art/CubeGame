"""Exercise detachable editor modules on the imported GEO-04 GLB in a disposable scene."""
import json
from pathlib import Path
import sys

import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
from mannequin_geo_04 import OUT, dump
import mannequin_v3_validate as poses

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(OUT/"voxel_base.glb"),disable_bone_shape=True)
rig, = [o for o in bpy.context.scene.objects if o.type=="ARMATURE"]
slots=json.loads((OUT/"module_sockets.json").read_text())
poses.apply_frame(rig,None,1)
local={}
proxies=[]
for key,spec in slots.items():
    anchor=bpy.data.objects["SOCKET_"+key]
    bone=rig.pose.bones[spec["bone"]]
    local[key]=(rig.matrix_world@bone.matrix).inverted()@anchor.matrix_world
    proxy=bpy.data.objects.new("Probe_"+key,None)
    bpy.context.scene.collection.objects.link(proxy)
    proxy.parent=anchor
    proxies.append((key,proxy))
    covered=[bpy.data.objects[part] for part in spec["covers"]]
    for obj in covered:
        obj.hide_render=True
    assert all(o.hide_render==(o in covered) for o in bpy.context.scene.objects if o.type=="MESH")
    for obj in covered:
        obj.hide_render=False

face=bpy.data.objects["head"]
frame=face.matrix_world.inverted()@bpy.data.objects["SOCKET_FACE"].matrix_world
errors=[]
for u in (-.5,-.25,0,.25,.5):
    for v in (-.5,-.25,0,.25,.5):
        origin=frame@Vector((u*.600,v*.405,.025))
        normal=frame.to_3x3().col[2].normalized()
        hit,point,n,_=face.ray_cast(origin,-normal,distance=.05)
        assert hit and n.dot(normal)>.999, (u,v,tuple(origin),tuple(normal),tuple(n),str(frame),str(face.matrix_world),str(bpy.data.objects["SOCKET_FACE"].matrix_world))
        errors.append(abs((origin-point).dot(normal)-.0265))
assert max(errors)<.0001,max(errors)
peak=0
for clip in ("editor_idle","walk","attack","grip_test"):
    action=bpy.data.actions[clip]
    for frame in (round(action.frame_range[0]),round(sum(action.frame_range)/2),round(action.frame_range[1])):
        poses.apply_frame(rig,clip,frame)
        for key,proxy in proxies:
            expected=rig.matrix_world@rig.pose.bones[slots[key]["bone"]].matrix@local[key]
            peak=max(peak,max(abs(a-b) for ra,rb in zip(proxy.matrix_world,expected) for a,b in zip(ra,rb)))
assert peak<.0001,peak
dump("editor_fit.json",dict(socket_probes=len(proxies),animated_samples=12,maximum_matrix_error=peak,
                          hide_restore_slots=len(slots),face_overlay_m=[.600,.405],face_samples=25,
                          maximum_plane_error_m=max(errors)))
