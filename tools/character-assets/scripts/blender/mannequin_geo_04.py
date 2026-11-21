"""Blond-warrior carrier. All visible surfaces are newly authored integer-grid solids."""
import hashlib
import json
import math
from pathlib import Path
import random
import sys

import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
import mannequin_v3_build as base
import mannequin_geo_02 as sockets
import mannequin_geo_02r as motion
import mannequin_geo_03_pipeline as poses
import mannequin_geo_03_review as studio
import mannequin_v3_validate as anim

ROOT = Path(base.ROOT)
OUT = ROOT / "outputs/prototypes/mannequin_geo_04"
OUT.mkdir(exist_ok=True, parents=True)
GRID = .0125
PROFILE = "MANNEQUIN-GEO-04"
SIDES = (("l", 1), ("r", -1))
PARTS = {}
CARRIERS = dict(base.PART_BONE)
BONES = []
SHAPES = {}


def dump(name, data):
    (OUT / name).write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def skin_material():
    # A repeating 32-texel skin tile. Islands are clusters of two to five texels.
    rng = random.Random(401)
    palette = [(0.83, .56, .425), (.815, .543, .410), (.855, .585, .451), (.80, .538, .412)]
    cells = [0]*1024
    for _ in range(58):
        x,y = rng.randrange(32), rng.randrange(32)
        tone = rng.randrange(1,4)
        for dx in range(rng.choice((1,2,3))):
            for dy in range(rng.choice((1,2))):
                cells[((y+dy)%32)*32+(x+dx)%32] = tone
    tex = bpy.data.images.new("BaseSkin_32", width=32, height=32, alpha=False)
    tex.pixels = [c for k in cells for c in (*palette[k],1)]
    tex.pack()
    mat = base.make_material()
    mat.name = "Base skin · pixel"
    mat.diffuse_color = (*palette[0],1)
    node = mat.node_tree.nodes.new("ShaderNodeTexImage")
    node.image, node.interpolation = tex, "Closest"
    mat.node_tree.links.new(node.outputs["Color"], mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"])
    return mat


def make(name, boxes, mat, side=1):
    rows = []
    for x0,x1,y0,y1,z0,z1 in boxes:
        rows.append(dict(x=(x0+x1)*GRID*side/2, y=(y0+y1)*GRID/2, z=(z0+z1)*GRID/2,
                         sx=(x1-x0)*GRID, sy=(y1-y0)*GRID, sz=(z1-z0)*GRID))
    mesh = base.union_block_mesh(name, rows)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    mesh.materials.append(mat)
    uv = mesh.uv_layers.new(name="PIXEL_SKIN")
    for f in mesh.polygons:
        axis = max(range(3), key=lambda i: abs(f.normal[i]))
        indices = ((1,2),(0,2),(0,1))[axis]
        for loop in f.loop_indices:
            point = mesh.vertices[mesh.loops[loop].vertex_index].co
            uv.data[loop].uv = [point[i]/(GRID*32) for i in indices]
    PARTS[name] = obj
    SHAPES[name] = rows
    obj["body_part_id"],obj["body_part_bone"] = name,CARRIERS[name]


def tier(cx, half, front, back, low, high, cut):
    # Rectilinear corner steps in plan view, with a full-width central band.
    return [(cx-half+cut,cx+half-cut,front,back,low,high),
            (cx-half,cx+half,front+cut,back-cut,low,high)]


def layered(rows):
    return [box for row in rows for box in tier(*row)]


def add_bone(name, head, tail, parent, mirror=1, connected=False):
    conv = lambda v: [v[0]*GRID*mirror, v[1]*GRID, v[2]*GRID]
    BONES.append(dict(name=name,head=conv(head),tail=conv(tail),parent=parent,
                      connected=connected,deform=name!="root"))


def anatomy(mat):
    make("head", layered([(0,18,-23,10,82,85,2),(0,23,-25,16,85,90,2),
                          (0,28,-25,21,90,97,3),(0,32,-25,24,97,133,3),
                          (0,31,-24,23,133,140,3),(0,28,-22,21,140,144,2)]), mat)
    make("neck", layered([(0,8,-7,7,76,86,1)]), mat)
    make("torso_upper", layered([(0,14,-7,7,55,58,1),(0,18,-10,10,58,65,2),(0,20,-11,10,65,70,2),
                                 (0,22,-11,11,70,76,2),(0,23,-11,10,76,78,3),
                                 (0,17,-8,9,78,80,2)]), mat)
    make("torso_pelvis", layered([(0,12,-7,8,42,46,2),(0,18,-10,10,46,54,2),
                                  (0,17,-9,9,54,57,2)]), mat)
    for name,head,tail,parent in (
        ("root",(0,0,0),(0,0,10),None),("pelvis",(0,0,45),(0,0,56),"root"),
        ("spine_01",(0,0,56),(0,0,58),"pelvis"),("spine_02",(0,0,58),(0,0,78),"spine_01"),
        ("neck",(0,0,78),(0,0,83),"spine_02"),("head",(0,0,83),(0,0,144),"neck")):
        add_bone(name,head,tail,parent,connected=name in ("spine_01","spine_02","neck","head"))
    for s,sg in SIDES:
        def part(name, boxes):
            make(f"{name}_{s}",boxes,mat,sg)
        def bone(name,h,t,p,join=False):
            add_bone(name+"_"+s,h,t,p if p in ("pelvis","spine_02") else p+"_"+s,sg,join)
        bone("clavicle",(2,0,77),(23,0,73),"spine_02")
        bone("upperarm",(23,0,73),(35,0,73),"clavicle",True)
        bone("lowerarm",(35,0,73),(47,0,73),"upperarm",True)
        bone("hand",(47,0,73),(62,0,73),"lowerarm",True)
        bone("thigh",(12,0,48),(12,0,22),"pelvis")
        bone("calf",(12,0,22),(12,0,9),"thigh",True)
        bone("foot",(12,0,9),(12,-13,2),"calf",True)
        part("ear",[(31,35,-5,4,100,115),(33,37,-4,4,103,113),
                    (35,38,-3,3,111,115),(35,37,3,5,105,112),(32,35,-4,1,97,102)])
        part("upper_arm",[(20,26,-4,4,69,77),(24,32,-5,5,68,77),(31,36,-3.5,3.5,69.5,75.5)])
        part("forearm",[(34,38,-4,4,69,76),(37,43,-5,5,68,77),(42,48,-4,4,70,76)])
        part("hand",[(46,51,-3.5,3.5,70.5,75.5),(50,55.5,-5.5,5.5,70,76)])
        part("thigh",layered([(12,4,-4,4,20,23,1),(12,6,-6,6,23,25,1),(12,7,-7,7,25,32,1),
                              (11.5,8.5,-8,8,32,44,1),(11,6,-6,7,44,49,1)]))
        part("shin",layered([(12,4,-4,4,8,10,1),(12,5,-5,5,10,14,1),(12,6,-6,6,14,22,1)]))
        part("foot",[(6,18,-4,6,0,8),(2.5,21.5,-13,2,0,6),(4,20,-16,-11,0,4),
                     (7,17,-6,5,6,9.5)])
        for digit,y,knuckle,end in (("index",-4,59,63),("middle",-1.25,60,64.5),
                                   ("ring",1.5,59.5,63.5),("pinky",4,58,61.5)):
            width=1 if digit=="pinky" else 1.2
            for i,a,b in ((1,54.5,knuckle),(2,knuckle,end)):
                n=f"{digit}_{i:02d}"
                bone(n,(a,y,73),(b,y,73),"hand" if i==1 else digit+"_01",i==2)
                CARRIERS[n+"_"+s]=n+"_"+s
                part(n,[(a-.25,b,y-width,y+width,72,74)])
        for i,h,t,boxes in (
            (1,(49.5,-3.5,72.5),(52.5,-6,71),[(49,52,-5,-3,71,74),(50,53,-6.5,-4.5,70,73)]),
            (2,(52.5,-6,71),(56,-7.5,70),[(52,55,-7.5,-5.5,70,72),(54,56.5,-8.5,-6.5,69,71.5)])):
            n=f"thumb_{i:02d}"
            bone(n,h,t,"hand" if i==1 else "thumb_01",i==2)
            CARRIERS[n+"_"+s]=n+"_"+s
            part(n,boxes)


def mount(rig):
    slots = json.loads((ROOT/"outputs/prototypes/mannequin_geo_03/run1/module_sockets.json").read_text())["sockets"]
    targets = {"FACE":(0,-25,112),"NOSE":(0,-25,106),"BEARD":(0,-25,88),"SCALP":(0,0,144),
               "CHEST":(0,-11,72),"WAIST":(0,-10,51),"BACK":(0,11,72),
               "EAR":(32,-1,107),"FEATURE":(31,0,135),"SHOULDER":(27,0,77),"ARM":(40,-5,73),
               "GLOVE":(53,0,76),"LEG":(12,-8,38),"SHIN":(12,-6,18),"FOOT":(12,-15,2),
               "MAIN_HAND":(55,0,69),"OFF_HAND":(55,0,69)}
    result={}
    for key,spec in slots.items():
        tag = key[:-2] if key.endswith(("_L","_R")) else key
        sg = -1 if key.endswith("_R") or key=="MAIN_HAND" else 1
        x,y,z = targets[tag]
        point=Vector((sg*x*GRID,y*GRID,z*GRID))
        direction=spec["orientation"]
        normal=Vector(sockets.NORMALS[direction])
        if direction!="grip":
            hit,point,normal,_=PARTS[spec["part"]].ray_cast(point+normal*.08,-normal,distance=.16)
            assert hit,key
        up=Vector((0,1,0) if direction=="top" else (0,-1,0) if direction=="grip" else (0,0,1))
        right=up.cross(normal).normalized()
        rest=Matrix((right,normal.cross(right),normal)).transposed().to_4x4()
        rest.translation=point+normal*spec.get("offset_m",0)
        b=rig.data.bones[spec["bone"]]
        o=bpy.data.objects.new("SOCKET_"+key,None)
        bpy.context.scene.collection.objects.link(o)
        o.parent,o.parent_type,o.parent_bone=rig,"BONE",b.name
        o.matrix_basis=Matrix.Translation((0,-b.length,0))@b.matrix_local.inverted()@rest
        o.empty_display_size=.018
        o["socket_id"],o["coverable_part_ids"]=key,",".join(spec["covers"])
        result[key]=dict(bone=b.name,part=spec["part"],covers=spec["covers"],rest=sockets.matrix_rows(rest))
    result["FACE"]["overlay_size_m"]=[.600,.405]
    dump("module_sockets.json",result)


def setup():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version=0
    mat=skin_material()
    anatomy(mat)
    rig=base.build_armature({"bones":BONES})
    assert not base.skin(PARTS,rig,CARRIERS)
    rig["geometry_revision"]=PROFILE
    mount(rig)
    bpy.context.scene.render.fps=24
    base.build_actions(rig,PARTS)
    for name,count in sockets.CLIPS.items():
        motion.bake_ground(rig,PARTS,name,count,8)
    source= json.loads((ROOT/"scripts/character/profiles/mannequin_geo_03_male.json").read_text())
    keys=[(f,poses.both_hands(source,state)) for f,state in ((1,"neutral"),(7,"open"),(13,"fist"),(19,"grip"),(25,"neutral"))]
    base.make_action(rig,"grip_test",keys,PARTS)
    editor=poses.both_hands(source,"neutral")
    for s,sg in SIDES:
        for digit in ("index","middle","ring","pinky","thumb"):
            editor[digit+"_01_"+s]=[("Y",sg*(34 if digit!="thumb" else 15))]
            editor[digit+"_02_"+s]=[("Y",sg*(48 if digit!="thumb" else 20))]
        editor["hand_"+s]=[("X",76)]
        editor["upperarm_"+s]=[("Y",sg*79)]
        editor["lowerarm_"+s]=[("Z",-sg*13)]
    base.make_action(rig,"editor_idle",[(1,editor),(25,editor)],PARTS)
    anim.apply_frame(rig,None,1)
    scene=bpy.context.scene
    scene.frame_end=25
    scene.unit_settings.system="METRIC"
    bpy.ops.object.select_all(action="DESELECT")
    rig.show_in_front=False
    for area in bpy.context.screen.areas:
        if area.type=="VIEW_3D":
            space=area.spaces.active
            space.overlay.show_overlays=False
            space.shading.color_type="TEXTURE"
            space.region_3d.view_location=(0,0,.9)
            space.region_3d.view_distance=3.25
    dump("geometry.json",dict(profile=PROFILE,grid_m=GRID,boxes_m=SHAPES,bones=BONES,bindings=CARRIERS))
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/"voxel_base.blend"))
    base.set_export_sampling(scene,8)
    bpy.ops.export_scene.gltf(filepath=str(OUT/"voxel_base.glb"),export_format="GLB",export_extras=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(OUT/"voxel_base.glb"),disable_bone_shape=True)
    rig,=[o for o in bpy.context.scene.objects if o.type=="ARMATURE"]
    meshes={o.name:o for o in bpy.context.scene.objects if o.type=="MESH"}
    assert set(meshes)==set(PARTS)
    assert {b.name for b in rig.data.bones}=={b["name"] for b in BONES}
    assert len([o for o in bpy.context.scene.objects if "socket_id" in o])==25
    assert all(o.modifiers and o.vertex_groups for o in meshes.values())
    assert all(max(abs(n) for n in f.normal)>.9999 for o in meshes.values() for f in o.data.polygons)
    clips={a.name for a in bpy.data.actions}
    assert clips=={"editor_idle","idle","walk","attack","grip_test"}
    feet=[]
    for n in ("idle","walk","attack"):
        action=bpy.data.actions[n]
        for f in range(round(action.frame_range[0]),round(action.frame_range[1])+1):
            anim.apply_frame(rig,n,f)
            feet.append(base.foot_min_z(meshes))
    assert min(feet)>-.001 and max(feet)<.002,(min(feet),max(feet))
    dump("checks.json",dict(glb_roundtrip=True,mesh_count=len(meshes),bones=len(BONES),sockets=25,
                            orthogonal_rest_faces=True,clips=sorted(clips),support_m=[min(feet),max(feet)],
                            sha256=hashlib.sha256((OUT/"voxel_base.glb").read_bytes()).hexdigest()))


def render():
    bpy.ops.wm.open_mainfile(filepath=str(OUT/"voxel_base.blend"))
    rig=bpy.data.objects["mannequin_v3_rig"]
    scene=bpy.context.scene
    camera,floor=studio.make_stage(scene,False,bpy.data.objects["head"].data.materials[0])
    scene.render.resolution_x,scene.render.resolution_y=640,768
    scene.cycles.samples=10
    for label,action,frame,direction,center,scale in [
        ("EDITOR_FRONT","editor_idle",1,(0,-1,.015),(0,0,.9),2.01),
        ("EDITOR_34","editor_idle",1,(.63,-1,.20),(0,0,.9),2.02),
        ("FRONT",None,1,(0,-1,0),(0,0,.9),2.13),("BACK",None,1,(0,1,0),(0,0,.9),2.13),
        ("LEFT",None,1,(1,0,0),(0,0,.9),2.13),("RIGHT",None,1,(-1,0,0),(0,0,.9),2.13),
        ("WALK","walk",1,(.68,-1,.18),(0,0,.90),2.12),("ATTACK","attack",9,(.68,-1,.18),(0,0,.90),2.12),
        ("HEAD_FRONT",None,1,(0,-1,0),(0,0,1.41),1.28),
        ("EAR_SIDE",None,1,(1,-.28,.12),(.40,0,1.33),.40),
        ("FEET_FRONT",None,1,(0,-1,.12),(0,-.04,.135),.79),
        ("FEET_SIDE",None,1,(1,-.09,.16),(.15,-.035,.12),.50),
        ("THREE_QUARTER",None,1,(.63,-1,.2),(0,0,.9),2.20),
        ("IDLE","idle",1,(.63,-1,.2),(0,0,.9),2.02),
        ("HAND_GRIP","grip_test",19,(-.6,-.7,-1),(-.685,-.01,.905),.36),
        ("HEAD",None,1,(.5,-1,.17),(0,0,1.41),1.34),
        ("HAND_OPEN","grip_test",7,(-.6,-.7,-1),(-.685,-.01,.905),.36),
        ("HAND_FIST","grip_test",13,(-.6,-.7,-1),(-.68,-.01,.90),.36),
        ("FOOT",None,1,(.6,-1,.32),(0,-.035,.12),.85)]:
        anim.apply_frame(rig,action,frame)
        camera.data.ortho_scale=scale
        camera.location=Vector(center)+Vector(direction).normalized()*6
        studio.aim(camera,center)
        floor.hide_render=label.startswith("HAND") or label=="HEAD"
        scene.render.filepath=str(OUT/(label+".png"))
        bpy.ops.render.render(write_still=True)


if __name__=="__main__":
    if "--render" in sys.argv:
        render()
    else:
        setup()
