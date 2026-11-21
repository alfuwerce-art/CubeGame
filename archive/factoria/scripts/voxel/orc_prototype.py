"""Factoria voxel humanoid prototype: no third-party dependencies.

Builds a NEW ORIGINAL modular voxel orc for evaluating body parts, detail
hierarchy and GLB interoperability. This does NOT remodel an existing mesh,
fit a skeleton, or generate facial emotions.

Usage: python scripts/voxel/orc_prototype.py --out outputs/prototypes
"""
from __future__ import annotations
import argparse
import json
import math
import random
import struct
from pathlib import Path

PARTS = [
    "pelvis", "torso", "head", "eyes", "brows", "mouth", "beard", "hair",
    "upper_arm_L", "forearm_L", "hand_L", "upper_arm_R", "forearm_R", "hand_R",
    "thigh_L", "shin_L", "foot_L", "thigh_R", "shin_R", "foot_R",
]
COLORS = {
    "skin": (96, 137, 91), "skin_light": (130, 167, 110), "skin_dark": (57, 91, 65),
    "armor": (88, 96, 109), "armor_light": (125, 139, 149), "armor_dark": (54, 64, 75),
    "leather": (84, 59, 45), "leather_light": (129, 88, 59), "leather_dark": (54, 39, 32),
    "bone": (223, 208, 170), "eye": (226, 189, 102), "pupil": (38, 38, 33),
    "beard": (43, 36, 30), "beard_light": (75, 55, 40), "mouth": (37, 40, 37),
    "metal": (161, 157, 138), "dark": (32, 39, 37),
}

# +Y is UP, +Z is face-forward. GLB units are meters.
class Model:
    def __init__(self):
        self.cubes = {p: [] for p in PARTS}

    def box(self, part, center, size, color):
        if part not in self.cubes:
            raise ValueError(part)
        if any(s <= 0 for s in size):
            raise ValueError(size)
        self.cubes[part].append((tuple(center), tuple(size), COLORS[color]))

    def scatter(self, part, origin, bounds, count, sizes, colors, seed):
        rng = random.Random(seed)
        for _ in range(count):
            center = tuple(origin[a] + rng.uniform(-bounds[a], bounds[a]) for a in range(3))
            dims = [rng.choice(sizes) for _ in range(3)]
            self.box(part, center, dims, rng.choice(colors))


def make_orc(seed=17):
    m=Model()
    b=m.box
    # A bespoke fantasy orc with segmented, visibly floating hard-surface limbs.
    b('pelvis', (0,1.13,0), (.68,.32,.34),'leather')
    b('pelvis', (0,1.29,.07), (.77,.18,.38),'armor_dark')
    b('torso', (0,1.85,0), (1.06,.93,.49),'skin')
    b('torso', (0,2.03,-.08), (1.18,.63,.51),'armor')
    b('torso', (0,1.53,.23), (.70,.30,.13),'leather')
    b('torso', (-.23,1.83,.27), (.13,.73,.09),'leather_light')
    b('torso', (.23,1.83,.27), (.13,.73,.09),'leather_light')
    b('torso', (0,1.68,.31), (.32,.20,.09),'metal')
    m.scatter('torso',(0,2.02,-.07),(.57,.36,.28),72,[.07,.105,.16,.24],['armor','armor_light','armor_dark'],seed+1)
    # Neck is recessed; head is a big flat-faced cubic volume, no separate jaw bone.
    b('head',(0,2.69,0),(.79,.71,.65),'skin')
    b('head',(0,2.97,-.01),(.86,.19,.69),'skin_dark')
    b('head',(-.48,2.68,0),(.23,.18,.23),'skin_dark')
    b('head',(.48,2.68,0),(.23,.18,.23),'skin_dark')
    b('head',(0,2.66,.39),(.18,.20,.14),'skin_light')
    b('head',(-.21,2.47,.36),(.13,.19,.16),'bone') # tusk
    b('head',(.21,2.47,.36),(.13,.19,.16),'bone')
    for sign in (-1,1):
        b('head',(sign*.17,2.55,.36),(.18,.10,.12),'skin_dark')
    for sign in (-1,1):
        b('eyes',(sign*.22,2.75,.344),(.17,.12,.035),'eye')
        b('eyes',(sign*.22,2.75,.371),(.06,.09,.028),'pupil')
        b('brows',(sign*.22,2.85,.365),(.23,.065,.075),'skin_dark')
    b('mouth',(0,2.49,.344),(.28,.055,.028),'mouth')
    # Beard and hair are optional independent modules, not a separately-animated jaw.
    b('beard',(0,2.41,.28),(.47,.13,.13),'beard')
    for i in range(9):
        x=-.22+.055*i
        length=.11+(.15 if 2<=i<=6 else .05)
        b('beard',(x,2.32-length/2,.30),(.056,length,.08),'beard' if i%2 else 'beard_light')
    for i in range(9):
        b('hair',(-.38+.095*i,3.065,-.08),(.102,.13,.63),'beard')
    # Arms float in 3 parts; each part is independently movable when rigged later.
    for sign,side in [(-1,'L'),(1,'R')]:
        x=sign*.78
        b('upper_arm_'+side,(x,1.95,0),(.39,.53,.43),'skin')
        b('upper_arm_'+side,(x,2.19,-.02),(.53,.21,.52),'armor')
        b('upper_arm_'+side,(x,1.82,.21),(.36,.15,.07),'leather')
        m.scatter('upper_arm_'+side,(x,2.11,-.02),(.19,.11,.21),18,[.07,.10,.14],['armor','armor_light','armor_dark'],seed+5+(sign+1))
        b('forearm_'+side,(x,1.39,.015),(.33,.43,.36),'skin')
        b('forearm_'+side,(x,1.35,.03),(.41,.32,.43),'armor')
        for k in range(4):
            b('forearm_'+side,(x,1.23+k*.09,.254),(.30,.055,.06),'metal' if k%2 else 'armor_dark')
        b('hand_'+side,(x,1.015,.03),(.30,.23,.32),'skin')
        for i in range(3):
            b('hand_'+side,(x + (i-1)*.09,.91,.17),(.075,.15,.13),'skin_dark')
        # Angular unfinished cubes at the gaps, parented to their OWN module.
        # ORC-01 fix: подняты/уменьшены, чтобы между upper_arm и forearm был настоящий зазор.
        for k in range(3):
            b('upper_arm_'+side,(x+(k-1)*.10,1.70,0),(.085,.085,.10),'skin_dark')
        for k in range(2):
            b('forearm_'+side,(x+(k-.5)*.12,1.585,-.05),(.08,.09,.09),'armor_dark')
        # Feet offset toward front (positive Z) for clear silhouette.
        xx=sign*.28
        b('thigh_'+side,(xx,.95,0),(.39,.53,.42),'leather')
        b('thigh_'+side,(xx,1.06,.23),(.40,.27,.11),'armor_dark')
        # ORC-01 fix: голень сдвинута вверх и чуть укорочена — иначе её низ пересекал стопу.
        b('shin_'+side,(xx,.42,-.01),(.34,.38,.36),'skin')
        b('shin_'+side,(xx,.42,.19),(.39,.34,.13),'armor')
        for k in range(3):
            b('shin_'+side,(xx,.29+k*.115,.28),(.34,.052,.05),'armor_light')
        b('foot_'+side,(xx,.105,.19),(.42,.21,.68),'leather_dark')
        b('foot_'+side,(xx,.10,.46),(.42,.09,.13),'metal')
        for k in range(3):
            b('thigh_'+side,(xx+(k-1)*.095,.695,0),(.07,.085,.1),'leather_light')
    return m


def pack_f32(seq):
    return struct.pack('<' + 'f'*len(seq), *seq)

def pack_u32(seq):
    return struct.pack('<' + 'I'*len(seq), *seq)

def align4(data):
    return data + b'\x00'*((-len(data))%4)

def export_glb(model, output):
    """One independently named glTF mesh/node per LEGO body module."""
    cube_faces = [(0,2,1),(0,3,2),(4,5,6),(4,6,7),
                  (0,1,5),(0,5,4),(2,3,7),(2,7,6),
                  (0,4,7),(0,7,3),(1,2,6),(1,6,5)]
    # vertex order: x-/y-/z-, x+/y-/z-, x+/y+/z-, x-/y+/z-, ... back
    signs=[(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]
    binary=b''
    views=[]; accessors=[]; meshes=[]; nodes=[]
    count_boxes=0
    def accessor(payload, component, count, typ, minimum=None, maximum=None, target=None, normalized=None):
        nonlocal binary
        offset=len(binary)
        payload=align4(payload)
        binary+=payload
        view={'buffer':0,'byteOffset':offset,'byteLength':len(payload)}
        if target is not None: view['target']=target
        views.append(view)
        a={'bufferView':len(views)-1,'componentType':component,'count':count,'type':typ}
        if normalized is not None: a['normalized']=normalized
        if minimum is not None: a['min']=minimum
        if maximum is not None: a['max']=maximum
        accessors.append(a)
        return len(accessors)-1
    for part,boxes in model.cubes.items():
        if not boxes: continue
        pos=[]; col=[]; idx=[]
        for center,size,color in boxes:
            base=len(pos)//3
            for sx,sy,sz in signs:
                pos.extend((center[0]+sx*size[0]/2,center[1]+sy*size[1]/2,center[2]+sz*size[2]/2))
                col.extend((*color,255))
            for tri in cube_faces: idx.extend((base+tri[0],base+tri[1],base+tri[2]))
        count_boxes+=len(boxes)
        # RGBA8 normalized vertex colors, unlit so exported voxel colors stay readable.
        # ORC-01 fix: normalized=True обязателен по спецификации glTF, иначе Blender
        # читает COLOR_0 как целые числа 0..255 и получает белый цвет.
        px=pos[0::3]; py=pos[1::3]; pz=pos[2::3]
        posid=accessor(pack_f32(pos),5126,len(pos)//3,'VEC3',[min(px),min(py),min(pz)],[max(px),max(py),max(pz)],34962)
        colid=accessor(bytes(col),5121,len(col)//4,'VEC4',target=34962,normalized=True)
        ixid=accessor(pack_u32(idx),5125,len(idx),'SCALAR',[min(idx)],[max(idx)],34963)
        meshes.append({'name':part,'primitives':[{'attributes':{'POSITION':posid,'COLOR_0':colid},'indices':ixid,'material':0,'mode':4}]})
        nodes.append({'name':part,'mesh':len(meshes)-1,'extras':{'body_part':part,'rig_ready':False}})
    doc={'asset':{'version':'2.0','generator':'Factoria procedural voxel prototype v0.1'},
         'extensionsUsed':['KHR_materials_unlit'],
         'scene':0,'scenes':[{'nodes':list(range(len(nodes)))}],'nodes':nodes,'meshes':meshes,
         'materials':[{'name':'voxel_vertex_colors','pbrMetallicRoughness':{'baseColorFactor':[1,1,1,1], 'metallicFactor':0,'roughnessFactor':1},'extensions':{'KHR_materials_unlit':{}},'doubleSided':False}],
         'accessors':accessors,'bufferViews':views,'buffers':[{'byteLength':len(binary)}]}
    j=align4(json.dumps(doc,separators=(',',':')).encode('utf-8')).replace(b'\x00',b' ')
    bn=align4(binary)
    header=struct.pack('<4sII',b'glTF',2,12+8+len(j)+8+len(bn))
    output.write_bytes(header+struct.pack('<I4s',len(j),b'JSON')+j+struct.pack('<I4s',len(bn),b'BIN\x00')+bn)
    return {'box_count':count_boxes,'part_count':len(nodes),'triangle_count':count_boxes*12,'vertex_count':count_boxes*8,'glb_bytes':output.stat().st_size}


def main():
    ap=argparse.ArgumentParser()
    ap.add_argument('--out',type=Path,default=Path('outputs/prototypes'))
    ap.add_argument('--seed',type=int,default=17)
    args=ap.parse_args()
    args.out.mkdir(parents=True,exist_ok=True)
    model=make_orc(args.seed)
    stats=export_glb(model,args.out/'orc_modular_v01.glb')
    manifest={'status':'EXPERIMENTAL_GEOMETRY_ONLY_NOT_GAME_READY',
      'input':'NEW_ORIGINAL_PARAMETRIC_ORC_NOT_EXISTING_MESH',
      'seed':args.seed,'coordinate_system':'Y_UP_Z_FORWARD_METERS',
      'style':'modular voxel hard surface, intentional unconnected limbs',
      'body_parts':{k:len(v) for k,v in model.cubes.items()},
      'has_skeleton':False,'has_animation':False,'has_emotions':False,
      'artist_approved':False,'status_note':'Do not assert visual acceptance or conversion of Alpha/CubeGame assets.',
      'metrics':stats}
    (args.out/'orc_modular_v01_manifest.json').write_text(json.dumps(manifest,indent=2,ensure_ascii=False),encoding='utf-8')
    print(json.dumps(stats,ensure_ascii=False))

if __name__=='__main__': main()
