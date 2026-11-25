"""Derive the unadorned mannequin from ElfRanger_Visual in its own bind pose."""
from __future__ import annotations

import argparse
import json
import math

import arm_surface
import block_head
import female_torso
import model
import visual_rework as ranger

SOURCE = model.OUT / "RangerMannequin"
FEMALE_SOURCE = model.OUT / "FemaleMannequin"
STEP = .5
GRIP = (42.6, 47.45, .1)
ROD_RADIUS = 1.57
PALM_INNER = 50.5
PALM_OUTER = 52.0
EAR_SCALE = .5
EAR_ANCHOR = (15.5,72.5,8.5)
HEAD_SCALE = .81
HEAD_BASE = 59.0
EAR_STEP = STEP*EAR_SCALE*HEAD_SCALE
# Torso cross sections from the pelvis to the collar, in centimeters.
TORSO_PROFILE = (
    (27.0,10.2,6.40,-.50),(30.5,9.30,6.30,-.35),(33.0,8.65,6.28,-.20),
    (38.5,8.85,6.50,-.10),(43.25,9.45,6.60,-.10),(46.0,11.45,6.68,.05),
    (51.0,12.1,6.72,.12),(54.25,11.65,6.65,.10),
)
# Bottom, top and outer edge of each paired abdominal row, in centimeters.
ABDOMEN_ROWS = ((26.0,31.0,6.0),(31.5,37.0,5.75),(37.5,42.5,5.75))
# Bottom, top, lateral half-width, rear edge and front edge of each leg block.
LEG_BLOCKS = (
    (4.5,7.0,2.5,-2.5,2.5),(7.0,9.5,3.0,-3.0,2.75),
    (9.5,12.0,3.25,-3.5,3.0),(12.0,14.5,3.5,-3.75,3.25),
    (14.5,17.0,3.5,-3.0,4.0),(17.0,19.5,3.75,-3.5,4.0),
    (19.5,22.5,4.25,-4.0,4.5),
)
# Outward distance, lower edge and upper edge of the swept auricle.
EAR_OUTLINE = (
    (13,62.1,74.6),(15,61.6,78.1),(17,62.1,80.6),(19.8,63.3,82.35),
    (23,65.6,83.7),(26,67.85,84.85),(29,71.15,86.0),(32,75.35,87.35),
    (34.4,79.95,88.35),(36,84.0,89.1),(37,86.8,89.45),(37.5,88.05,89.5),
)
EAR_BORDER = (tuple((u,top) for u,_,top in EAR_OUTLINE)+tuple(
    (u,bottom) for u,bottom,_ in reversed(EAR_OUTLINE))+(EAR_OUTLINE[0][::2],))
# Two raised folds terminate in the bowl or join the outer lip.
ANTIHELIX = ((17.35,68.1),(17.8,71.2),(19.35,73.75),(22.3,75.15),
             (24.4,75.9),(25.4,74.75),(25.1,72.35))
UPPER_FOLD = ((25.0,75.15),(26.45,77.1),(27.55,79.35),(29.05,80.85))

# In the original T-pose X is wrist-to-fingers, -Y faces into the palm and
# +Z is the index edge. Coordinates are in the original centimeters.
DIGIT_TEMPLATE = {
    "Index": ((42.12, 51.11, 3.06), (44.54, 50.99, 3.13), (46.02, 50.88, 3.12), (47.27, 50.78, 3.09)),
    "Middle": ((42.61, 51.18, 1.06), (45.22, 51.10, 1.06), (46.82, 50.99, 1.02), (48.18, 50.86, 1.00)),
    "Ring": ((42.52, 51.12, -.92), (44.92, 51.02, -.98), (46.44, 50.90, -1.00), (47.70, 50.78, -1.02)),
    "Pinky": ((42.00, 50.99, -2.79), (43.98, 50.85, -2.93), (45.25, 50.73, -2.98), (46.35, 50.60, -3.00)),
    "Thumb": ((39.28, 50.92, 2.36), (40.22, 50.65, 4.04), (41.77, 50.47, 4.88), (43.18, 50.35, 5.16)),
}


def extend_digit(points,length):
    root = points[0]
    return tuple(tuple(a+(b-a)*length for a,b in zip(root,p)) for p in points)


DIGITS = {
    name: extend_digit(points,1.10 if name=="Thumb" else 1.20)
    for name,points in DIGIT_TEMPLATE.items()
}
# A single section size for all three rigid segments of each digit.
FINGER_SIZE = {
    "Index": (1.70, 1.63),
    "Middle": (1.73, 1.65),
    "Ring": (1.67, 1.62),
    "Pinky": (1.42, 1.40),
    "Thumb": (1.71, 1.65),
}

# Absolute curl angles about the handle and converged positions along it.
# The index anchors the thumb, which crosses its radial/dorsal edge.
CURL = {
    "Index": ((18, 63, 118), (2.82, 2.49, 2.32)),
    "Middle": ((21, 77, 134), (.90, .83, .79)),
    "Ring": ((21, 72, 129), (-.96, -.82, -.76)),
    "Pinky": ((13, 54, 119), (-2.54, -2.24, -2.06)),
}
THUMB_DIRECTIONS = ((1.177,.374,1.426), (1.582,-.047,.558), (1.384,-.114,-.378))


def grip_paths():
    paths = {}
    for digit, rest in DIGITS.items():
        path = [rest[0]]
        for i, (a,b) in enumerate(zip(rest,rest[1:])):
            length = math.dist(a,b)
            x,y,z = path[-1]
            if digit == "Thumb":
                axis = ranger.unit(THUMB_DIRECTIONS[i])
                path.append(tuple(p+length*d for p,d in zip(path[-1],axis)))
            else:
                degrees, rows = CURL[digit]
                angle = math.radians(degrees[i])
                row = rows[i]
                travel = math.sqrt(length*length-(row-z)**2)
                path.append((x+travel*math.cos(angle),y-travel*math.sin(angle),row))
        paths[digit] = path
    return {"center":GRIP, "radius":ROD_RADIUS, "length":20, "rest":DIGITS, "grip":paths}


def skeleton():
    model.skeleton()
    original = list(model.BONES)
    model.BONES.clear()
    model.BONE_INDEX.clear()
    for name, position, parent in original:
        if name.startswith(("Cape", *DIGITS)):
            continue
        if name.startswith("Grip_"):
            side = name[-1]
            sign = 1 if side == "L" else -1
            position = (sign * GRIP[0], *GRIP[1:])
        model.bone(name, position, parent)
    for sign, side in ((1, "L"), (-1, "R")):
        for digit, points in DIGITS.items():
            parent = f"Hand_{side}"
            for i, (x, y, z) in enumerate(points[:3], 1):
                name = f"{digit}{i}_{side}"
                model.bone(name, (sign*x, y, z), parent)
                parent = name


def skin_weights(x, y, z):
    x, y, z = (p*STEP for p in (x, y, z))
    u = abs(x)
    side = "L" if x > 0 else "R"
    if u > 37.8:
        return [(f"Hand_{side}",1.)]
    if u > 34:
        return model.limb(f"Forearm_{side}", f"Hand_{side}", (u-34.85)/2.95)
    if u >= 19.0:
        return model.limb(f"UpperArm_{side}", f"Forearm_{side}", (u-24.7)/4.7)
    if y < 28:
        if y < 8:
            if y < 3.6 and z > 4:
                return model.limb(f"Foot_{side}", f"Toe_{side}", (z-4)/3)
            return model.limb(f"Foot_{side}", f"Calf_{side}", (y-4)/4)
        if y < 18:
            return model.limb(f"Calf_{side}", f"Thigh_{side}", (y-12)/6)
        # The rear shell and the center seam belong to the pelvis. Blend
        # each thigh into it gradually, with the rear transition ending lower.
        rear = model.smooth((-z-1.5)/3.0)
        inner = 1-model.smooth((u-.75)/4.0)
        start = model.mix(model.mix(21.5,18.5,rear),18.0,inner)
        end = model.mix(model.mix(28.0,25.5,rear),23.0,inner)
        thigh = 1-model.smooth((y-start)/(end-start))
        return [("Pelvis",1-thigh),(f"Thigh_{side}",thigh)]
    if y > 51 and u <= 7:
        neck = model.smooth((y-51)/7)
        chest_front = model.smooth((abs(z)-5.5)/.5)
        above_chest = model.smooth((y-53.5)/2.5)
        neck *= 1-chest_front*(1-above_chest)
        return [("Chest",1-neck),("Neck",neck)]
    torso = model.torso_weights(x,y,z)
    # Extend the shoulder blend inward across the underside of each axilla.
    low_shoulder = model.smooth((50.5-y)/4)*model.smooth((y-42)/3)
    underarm = 2.0*low_shoulder*(1-model.smooth((abs(z)-2.75)/2.25))
    clavicle_start = 14.5-underarm
    if u <= clavicle_start:
        return torso

    # Keep the front and rear of the rib cage on the torso rig. Transfer
    # influence to the clavicle and upper arm only across the deltoid.
    shoulder_start = clavicle_start+.6*model.smooth((abs(z)-2.1)/3.8)
    upper = model.smooth((u-shoulder_start)/(19-shoulder_start))
    clavicle = .55*(1-upper)*model.smooth((u-clavicle_start)/2.6)
    body = 1-upper-clavicle
    return [(name,w*body) for name,w in torso]+[(f"Clavicle_{side}",clavicle),(f"UpperArm_{side}",upper)]


def underlayers():
    # Keep the neck and limb bases used by the approved head and arm joins.
    model.torso()
    torso = model.MODULES["BaseBody"]
    torso.cells.clear()
    torso.bevel((0, 29.3, -.65), (12.5, 6.15, 6.65), 2.4, "skin")
    torso.cells = {p:tone for p,tone in torso.cells.items() if p[1]<32}
    torso.cells.update(model.MODULES["Neck"].cells)
    for sign in (-1, 1):
        torso.sweep([(sign*10,51,0,3.45,4.45,4.8), (sign*14.7,50.9,0,4.5,4.05,4.05),
                     (sign*22.5,50.9,0,5.3,3.45,3.45), (sign*27.2,50.8,0,3.8,3.0,3.0),
                     (sign*31.9,50.9,.05,4.6,3.2,3.15), (sign*37.4,51,.14,2.35,2.35,2.65)], "skin", 4)
    torso.stitch_diagonals()
    original = torso.cells
    model.MODULES.clear()
    skin = model.Volume("BaseBody", skin_weights)
    for (x,y,z), color in original.items():
        for i in (0,1):
            for j in (0,1):
                for k in (0,1):
                    skin.cells[2*x+i,2*y+j,2*z+k] = "skin_half" if color=="skin_shadow" else color
    return skin


def torso_surface(skin, *, female=False):
    chest_bottom, chest_top = 43.5,53.0

    def core(x,y,z):
        x,y,z = x*STEP,y*STEP,z*STEP
        if not TORSO_PROFILE[0][0] <= y <= TORSO_PROFILE[-1][0]:
            return False
        for (a,*start),(b,*end) in zip(TORSO_PROFILE,TORSO_PROFILE[1:]):
            if y <= b:
                t = (y-a)/(b-a)
                width,depth,center = (model.mix(p,q,t) for p,q in zip(start,end))
                if female:
                    width = female_torso.waist_width(y,width)
                u = abs(x)
                if u >= width:
                    return False
                curvature = depth*(1-(u/width)**6)**(1/6)
                # The chest front is part of the rib cage surface. The plane
                # rolls into the flanks and collar within the same cross section.
                chest = model.smooth((y-42.5)/1.5)*model.smooth((54.25-y)/1.25)
                side = model.smooth((u-(width-2.6))/2.6)
                front = model.mix(center+curvature,7.35,chest*(1-side))
                return center-curvature < z < front
        return False

    def axilla(x,y,z):
        x,y,z = abs(x*STEP),y*STEP,z*STEP
        span = model.clamp((x-9)/7)
        floor = 42.75+4.6*span**1.4+1.65*(z/4.5)**2
        return 9 <= x < 16 and abs(z) < 4.5 and floor < y < 49.5

    def front_relief(x,y,z):
        u,y,z = abs(x*STEP),y*STEP,z*STEP
        if u < .5:
            return False
        if chest_bottom <= y < chest_top:
            outer = model.mix(8.0,9.5,model.smooth((y-chest_bottom)/2.5))
            return u < outer and 6.5 <= z < 8.0
        return 5.5 <= z < 7.0 and any(
            low <= y < high and u < outer for low,high,outer in ABDOMEN_ROWS
        )

    def torso_tone(x,y,z):
        u,y,z = abs(x*STEP),y*STEP,z*STEP
        if female or z < 5.75:
            return "skin"
        center = u < .5
        if center and chest_bottom <= y < chest_top:
            return "skin_half"
        if ABDOMEN_ROWS[0][0] <= y < ABDOMEN_ROWS[-1][1] and u < 5.75:
            in_row = any(low <= y < high for low,high,_ in ABDOMEN_ROWS)
            if center or not in_row:
                return "skin_half"
        return "skin"

    # Bring the upper pelvis under the torso outline while retaining the leg join.
    def within_pelvis(cell):
        x,y = ((v+.5)*STEP for v in cell[:2])
        if not 23 <= y < 32:
            return True
        leg_join = model.mix(11.5,10.35,model.smooth((y-23)/3))
        width = model.mix(leg_join,8.9,model.smooth((y-29.5)/2.5))
        if female:
            width = min(width,female_torso.waist_width(y,width))
        return abs(x) <= width

    skin.cells = {cell:tone for cell,tone in skin.cells.items() if within_pelvis(cell)}
    skin.fill(-25,25,54,109,-15,15,core,torso_tone)
    skin.fill(-32,32,85,99,-9,9,axilla,"skin")
    if not female:
        skin.fill(-19,19,52,106,11,16,front_relief,"skin")


def beveled_cm(volume, center, half, radius):
    volume.bevel(tuple(v/STEP for v in center),tuple(v/STEP for v in half),radius/STEP,"skin")


def leg_surfaces(skin):
    for side in (-1,1):
        beveled_cm(skin,(side*6.0,22.3,.1),(4.25,4.7,4.4),.75)
    hip_row = round(LEG_BLOCKS[-1][1]/STEP)
    skin.cells = {p:tone for p,tone in skin.cells.items() if p[1]>=hip_row}

    def block_section(x,y,z):
        u,y,z = abs(abs(x*STEP)-6.0),y*STEP,z*STEP
        for low,high,width,rear,front in LEG_BLOCKS:
            if low <= y < high and rear <= z < front:
                corner = STEP if z < rear+STEP or z >= front-STEP else 0.0
                return u < width-corner
        return False

    skin.fill(-21,21,9,hip_row,-8,9,block_section,"skin")
    for side in (-1,1):
        x = side*6.0
        beveled_cm(skin,(x,2.0,1.55),(3.4,2.0,5.55),.65)
        beveled_cm(skin,(x,4.15,.35),(2.8,.85,3.45),.40)


def palms(skin):
    skin.cells = {cell:tone for cell,tone in skin.cells.items() if abs((cell[0]+.5)*STEP)<35.0}

    def palm(x, y, z):
        x,y,z = abs(x*STEP),y*STEP,z*STEP
        if not 35.0 <= x < 42.5:
            return False
        wrist = model.smooth((x-35.0)/3.25)
        inner = model.mix(48.3,PALM_INNER,wrist)
        outer = model.mix(53.7,PALM_OUTER,wrist)
        little_edge = model.mix(2.5,3.7,model.clamp((x-37.3)/3.8))
        thumb_edge = model.mix(2.5,3.72,model.clamp((x-37.1)/3.6))
        return inner < y < outer and -little_edge < z < thumb_edge

    skin.fill(-85,85,96,108,-8,8,palm,"skin")
    skin.stitch_diagonals()


def finger_block(a,b,width,thickness,start,end):
    """Return the closed faces of one rigid rectangular phalanx with small bevels."""
    axis = ranger.unit(tuple(v-u for u,v in zip(a,b)))
    across = ranger.unit(ranger.cross(axis,(0,1,0)))
    up = ranger.cross(across,axis)
    width,thickness = width/2,thickness/2
    bevel = .09

    def ring(distance,inset):
        w,h = width-inset,thickness-inset
        corners = ((-h+bevel,-w),(h-bevel,-w),(h,-w+bevel),(h,w-bevel),
                   (h-bevel,w),(-h+bevel,w),(-h,w-bevel),(-h,-w+bevel))
        return [tuple(o+distance*d+y*v+z*w for o,d,v,w in zip(a,axis,up,across)) for y,z in corners]

    rings = (ring(start,bevel),ring(start+bevel,0),ring(end-bevel,0),ring(end,bevel))
    yield tuple(reversed(rings[0]))
    for near,far in zip(rings,rings[1:]):
        for i in range(8):
            j = (i+1)%8
            yield (near[i],near[j],far[j],far[i])
    yield tuple(rings[-1])


def finger_faces():
    for sign,side in ((1,"L"),(-1,"R")):
        for digit,points in DIGITS.items():
            rest = [(sign*x,y,z) for x,y,z in points]
            width,thickness = FINGER_SIZE[digit]
            for i,(a,b) in enumerate(zip(rest,rest[1:]),1):
                start = -.28 if i==1 else .025
                end = math.dist(a,b)+(.11 if i==3 else -.025)
                for vertices in finger_block(a,b,width,thickness,start,end):
                    yield vertices,f"{digit}{i}_{side}"


def path_distance(u,y,path):
    nearest = math.inf
    for (a,b),(c,d) in zip(path,path[1:]):
        dx,dy = c-a,d-b
        t = model.clamp(((u-a)*dx+(y-b)*dy)/(dx*dx+dy*dy))
        nearest = min(nearest,math.hypot(u-a-t*dx,y-b-t*dy))
    return nearest


def ear_profile(u,y):
    start,end = EAR_OUTLINE[0][0],EAR_OUTLINE[-1][0]
    if not start <= u < end:
        return None
    for (a,lo_a,hi_a),(b,lo_b,hi_b) in zip(EAR_OUTLINE,EAR_OUTLINE[1:]):
        if u <= b:
            t = (u-a)/(b-a)
            lower,upper = model.mix(lo_a,lo_b,t),model.mix(hi_a,hi_b,t)
            break
    if not lower < y < upper:
        return None

    t = (u-start)/(end-start)
    distance = path_distance(u,y,EAR_BORDER)
    root_wall = u-(14.7+.3*((y-71)/9)**2)
    rim_width = 1.42+.64*(1-t)
    recess = model.smooth((min(distance,root_wall)-rim_width+.2)/1.45)
    lip = 12.2-5.35*t+.095*(y-(lower+upper)/2)
    lip -= .5*(1-model.smooth(distance/.75))
    bowl_depth = (4.8-.70*t+.52*model.smooth((76-y)/9))*recess

    # The bowl persists between the two shelves and runs up to the tip.
    main_fold = 2.75*(1-model.smooth((path_distance(u,y,ANTIHELIX)-.38)/1.12))
    upper_fold = 2.0*(1-model.smooth((path_distance(u,y,UPPER_FOLD)-.28)/.95))
    fold = max(main_fold,upper_fold)*recess
    rear = lip-(2.05+6.55*(1-t)**.7)*(.78+.22*model.smooth(distance/3))
    front = max(rear+1.05,lip-bowl_depth+fold)
    remaining_depth = lip-front
    tone = "skin_shadow" if remaining_depth>3.6 else "skin_half" if remaining_depth>1.65 else "skin"
    return rear,front,tone


def sculpt_ears():
    ears = model.Volume("Ears",model.fixed("Head"))
    for x in range(26,75):
        for y in range(123,179):
            section = ear_profile((x+.5)*STEP,(y+.5)*STEP)
            if section is None:
                continue
            rear,front,tone = section
            for z in range(math.floor(rear/STEP+.5),math.floor(front/STEP+.5)):
                color = tone if (z+.5)*STEP>front-1.15 else "skin_half"
                ears.cells[x,y,z] = color
    ears.stitch_diagonals()
    # Export on a finer grid so the existing folds shrink with the shell.
    # The offset keeps the attachment point at the side of the head.
    dx,dy,dz = (round((1-EAR_SCALE)*v/(STEP*EAR_SCALE)) for v in EAR_ANCHOR)
    dy += round(HEAD_BASE*(1-HEAD_SCALE)/EAR_STEP)
    ears.cells = {(x+dx,y+dy,z+dz):tone for (x,y,z),tone in ears.cells.items()}
    ears.cells.update({(-x-1,y,z):tone for (x,y,z),tone in ears.cells.items()})


def mannequin_head():
    faces = block_head.build(HEAD_SCALE, HEAD_BASE)
    sculpt_ears()
    return faces


class RangerCopy(model.Gltf):
    def __init__(self, body_faces, head_faces, torso_faces, **kwargs):
        super().__init__(**kwargs)
        self.body_faces = body_faces
        self.head_faces = head_faces
        self.torso_faces = torso_faces

    @staticmethod
    def face_normal(vertices):
        a,b,c = vertices[:3]
        edge_a = tuple(v-u for u,v in zip(a,b))
        edge_b = tuple(v-u for u,v in zip(a,c))
        return ranger.unit(ranger.cross(edge_a,edge_b))

    def extra_faces(self,name):
        if name == "Head":
            for face,tone in self.head_faces:
                normal = self.face_normal(face)
                uv = model.uv_for(tone, 2, 0, 0, (0, 0, 1), self.palette)
                yield tuple(tuple(v/STEP for v in p) for p in face),normal,uv,"Head"
            return
        elif name == "BaseBody":
            for face,cell,tone,direction in self.torso_faces:
                uv = model.uv_for(tone,*cell,direction,self.palette)
                yield face,direction,uv,None
            surfaces = [(face,None) for face in self.body_faces]
            surfaces.extend(finger_faces())
        else:
            return
        for vertices,bone in surfaces:
            normal = self.face_normal(vertices)
            uv = model.uv_for("skin",1,1,1,normal,self.palette)
            yield tuple(tuple(v/STEP for v in p) for p in vertices),normal,uv,bone

    def primitive(self, name, volume, material):
        step = EAR_STEP if name=="Ears" else STEP
        self.scale *= step
        try:
            return super().primitive(name,volume,material)
        finally:
            self.scale /= step


def main(*, female=False):
    output = FEMALE_SOURCE if female else SOURCE
    skeleton()
    skin = underlayers()
    torso_surface(skin,female=female)
    leg_surfaces(skin)
    palms(skin)
    body = arm_surface.replace_voxel_arms(skin,STEP)
    torso = tuple(female_torso.contour_faces(skin,STEP)) if female else ()
    head = mannequin_head()
    # Keep the same node identity and palette for reimport into the native duplicate.
    RangerCopy(body,head,torso,output=output,name=ranger.NAME,
               colors=ranger.COLORS).build(staging=None,include_animation=False)
    (output/"grip.json").write_text(json.dumps(grip_paths(),indent=2),encoding="utf8")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--female",action="store_true",help="Export the female torso variant")
    main(female=parser.parse_args().female)
