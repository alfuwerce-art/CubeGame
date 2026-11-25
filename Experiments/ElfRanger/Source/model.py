"""Author and export the CubeGame Elf Ranger from integer-grid surface volumes."""
from __future__ import annotations

import collections
import json
import math
import pathlib
import struct
import zlib

from motion import make_clips

ROOT = pathlib.Path(__file__).resolve().parents[1]
OUT = ROOT / "Staging"
EXPORT = ROOT / "Export"
SCALE = 0.01

PALETTE = {
    "skin": "#eeb18c", "skin_shadow": "#d88369", "ear": "#ea9276",
    "ear_fold": "#b96959", "lip": "#94524b", "nose": "#f4b692",
    "hair": "#c9c0cb", "silver": "#e0d7df", "hair_mid": "#b2a6b6",
    "hair_dark": "#8f8397", "brow": "#796879", "lash": "#302238",
    "white": "#f6eae9", "iris": "#9226c3", "violet": "#572278",
    "pupil": "#291338", "glint": "#fcf4fc",
    "cloth": "#35303b", "seam": "#45404d", "coal": "#27232d",
    "lining": "#201d26", "wool": "#89828d", "wool_light": "#aaa2ab",
    "gold": "#cc9034", "gold_dark": "#a16627", "gold_light": "#ecc168",
    "plum": "#713248", "wine": "#572237", "cape_shadow": "#431e2e",
    "cape_light": "#833951", "leather": "#6f4436", "leather_light": "#985d42",
    "leather_dark": "#432d2c", "sole": "#352728", "silver_metal": "#c3b5b3",
    "silver_shadow": "#827473",
}
SHADE_COUNT = 4
ATLAS_WIDTH = 64
ATLAS_HEIGHT = 16
FACE = (
    ((1, 0, 0), ((1, 0, 0), (1, 1, 0), (1, 1, 1), (1, 0, 1))),
    ((-1, 0, 0), ((0, 0, 1), (0, 1, 1), (0, 1, 0), (0, 0, 0))),
    ((0, 1, 0), ((0, 1, 1), (1, 1, 1), (1, 1, 0), (0, 1, 0))),
    ((0, -1, 0), ((0, 0, 0), (1, 0, 0), (1, 0, 1), (0, 0, 1))),
    ((0, 0, 1), ((0, 0, 1), (1, 0, 1), (1, 1, 1), (0, 1, 1))),
    ((0, 0, -1), ((1, 0, 0), (0, 0, 0), (0, 1, 0), (1, 1, 0))),
)
MODULES = {}
BONES = []
BONE_INDEX = {}


def mix(a, b, t):
    return a + (b - a) * t


def clamp(t):
    return max(0, min(1, t))


def smooth(t):
    t = clamp(t)
    return t * t * (3 - 2 * t)


def bone(name, position, parent=None):
    BONE_INDEX[name] = len(BONES)
    BONES.append((name, position, parent))


def skeleton():
    bone("Root", (0, 0, 0))
    bone("Pelvis", (0, 27, 0), "Root")
    bone("Spine", (0, 35, 0), "Pelvis")
    bone("Chest", (0, 46, 0), "Spine")
    bone("Neck", (0, 55, 0), "Chest")
    bone("Head", (0, 61, 0), "Neck")
    bone("CapeTop", (0, 50, -9), "Chest")
    bone("CapeMiddle", (0, 36, -12), "CapeTop")
    bone("CapeHem", (0, 23, -16), "CapeMiddle")
    for s, side in ((1, "L"), (-1, "R")):
        bone(f"Clavicle_{side}", (s * 5, 51, 0), "Chest")
        bone(f"UpperArm_{side}", (s * 14, 51, 0), f"Clavicle_{side}")
        bone(f"Forearm_{side}", (s * 27, 51, 0), f"UpperArm_{side}")
        bone(f"Hand_{side}", (s * 38, 51, 0), f"Forearm_{side}")
        bone(f"Grip_{side}", (s * 43, 50, 1.5), f"Hand_{side}")
        for finger, (y, z, a, b, *_dimensions) in fingers().items():
            bone(f"{finger}1_{side}", (s * a, y, z), f"Hand_{side}")
            bone(f"{finger}2_{side}", (s * b, y, z), f"{finger}1_{side}")
        bone(f"Thigh_{side}", (s * 6, 26, 0), "Pelvis")
        bone(f"Calf_{side}", (s * 6, 15, 0.5), f"Thigh_{side}")
        bone(f"Foot_{side}", (s * 6, 5, 0), f"Calf_{side}")
        bone(f"Toe_{side}", (s * 6, 2, 5), f"Foot_{side}")


def fixed(name):
    return lambda x, y, z: [(name, 1.0)]


def limb(a, b, fraction):
    t = smooth(fraction)
    return [(a, 1 - t), (b, t)]


class Volume:
    def __init__(self, name, weights):
        self.cells = {}
        self.open_faces = set()
        self.weights = weights
        MODULES[name] = self

    def fill(self, x0, x1, y0, y1, z0, z1, predicate, material):
        for z in range(math.floor(z0), math.ceil(z1)):
            for y in range(math.floor(y0), math.ceil(y1)):
                for x in range(math.floor(x0), math.ceil(x1)):
                    p = x + 0.5, y + 0.5, z + 0.5
                    if predicate(*p):
                        self.cells[x, y, z] = material(*p) if callable(material) else material

    def bevel(self, center, half, r, material):
        def contains(x, y, z):
            d = (max(0, abs(v - c) - (h - r)) for v, c, h in zip((x, y, z), center, half))
            return sum(k * k for k in d) <= r * r

        (x, y, z), (a, b, c) = center, half
        self.fill(x - a, x + a, y - b, y + b, z - c, z + c, contains, material)

    def sweep(self, points, material, exponent=2.0):
        """Follow a stepped lock or fabric roll through successive ellipsoid sections."""
        for a, b in zip(points, points[1:]):
            length = math.dist(a[:3], b[:3])
            for i in range(max(1, math.ceil(length * 1.5)) + 1):
                t = i / max(1, math.ceil(length * 1.5))
                x, y, z, rx, ry, rz = [mix(h, j, t) for h, j in zip(a, b)]
                self.fill(x-rx, x+rx, y-ry, y+ry, z-rz, z+rz,
                          lambda u,v,w: sum(abs(p/q)**exponent for p,q in
                                           ((u-x,rx), (v-y,ry), (w-z,rz))) <= 1,
                          material)

    def stitch_diagonals(self):
        """Close corners where two occupied cells meet along a lone grid edge."""
        pairs=((0,1),(0,2),(1,2))
        while True:
            added={}
            for p in self.cells:
                for a,b in pairs:
                    neighbor=list(p)
                    neighbor[a]+=1
                    edge_a=tuple(neighbor)
                    if edge_a in self.cells:
                        continue
                    for step in (-1,1):
                        neighbor[b]=p[b]+step
                        far=tuple(neighbor)
                        if far not in self.cells:
                            continue
                        other=list(p)
                        other[b]+=step
                        edge_b=tuple(other)
                        if edge_b in self.cells:
                            continue
                        if edge_a not in added and edge_b not in added:
                            added[edge_a]=self.cells[far]
            if not added:
                return
            self.cells.update(added)


def head():
    face = Volume("Head", fixed("Head"))
    def skin(x, y, z):
        if z > 10 and 64 < y < 70 and 11 < abs(x) < 15:
            return "skin_shadow" if abs(x) > 14 else "skin"
        return "skin"
    face.bevel((0, 76.4, 0.4), (18.1, 16.4, 14.3), 3.5, skin)
    face.bevel((0, 68.4, 14.25), (1.7, 1.4, 1.55), 1.0, "nose")

    details = Volume("Face", fixed("Head"))
    details.bevel((0, 64.9, 14.75), (2.4, .52, .55), .40, "lip")
    for s in (1, -1):
        # Sculpted eyelid, coloured pixels on an opaque continuous eye volume.
        def eye_mat(x, y, z):
            u = s*x
            if y > 77.1 or (u > 13.5 and y > 75.5):
                return "lash"
            if 7.0 < u < 11.7:
                if 8.0 < u < 9.5 and 73.3 < y < 75.2:
                    return "glint"
                if 8.5 < u < 10.9 and y > 72.9:
                    return "pupil"
                return "violet" if y > 75.6 or u > 11 else "iris"
            return "white"
        details.bevel((s*9.45, 74.3, 14.65), (5.15, 4.65, 1.1), .8, eye_mat)
        details.fill(-16, 16, 78, 84, 14, 17,
                     lambda x,y,z: 4.9 < s*x < 15 and abs(y-(80.0+0.19*s*x)) < 1.0
                     and z < (16.1 if s*x<13 else 15.6), "brow")

    ears = Volume("Ears", fixed("Head"))
    for s in (1, -1):
        def ear_shape(x,y,z):
            u = (s*x - 16) / 20
            if not 0 <= u < 1:
                return False
            return (abs(y-(72.0+6.2*u)) <= 6.5*(1-u)**.75+.45
                    and abs(z-(1.5+1.0*u)) <= 3.5*(1-u)+.62)
        def ear_mat(x,y,z):
            u = (s*x - 16) / 20
            if z > 2.5 and 0.17 < u < .88 and y < 76.1+2.2*u:
                return "ear_fold" if u < .36 and y < 73.8 else "ear"
            return "skin"
        ears.fill(-36,36,64,81,-3,7,ear_shape,ear_mat)


def hairstyle():
    hair = Volume("Hair", fixed("Head"))
    hair.fill(-22,22,63,109,-22,17,
              lambda x,y,z: (abs(x)/23)**2.6+(abs(y-87)/20)**2.4+(abs(z+3)/18)**2.5<1
              and (z < 8 or y>85 or abs(x)>15+y*.02)
              and (y > 74 or z < -6 or abs(x)>17), "hair_dark")
    hair.fill(-23,23,61,85,-21,-7,
              lambda x,y,z: (abs(x)/22.4)**4+(abs(y-75)/14.1)**4
              +(abs(z+13)/7.4)**4<1 and z<-10-.065*abs(x), "hair_mid")

    # Back masses fan out from an off-centre crown and terminate at different lengths.
    for i in range(11):
        u = (i-5)/5
        hair.sweep([
            (3*u+2, 103-4*abs(u), -6, 4, 4, 5),
            (13*u+1, 96-5*abs(u), -15+3*abs(u), 5, 5, 6),
            (22*u, 84-1*abs(u), -19+5*abs(u), 4.5, 6, 5),
            (23*u-1, 72+3*abs(u)+(i%3)*2, -20+6*abs(u), 4, 6, 4),
            (22*u, 58+9*abs(u)+(i%4)*2, -16+6*abs(u), 1.8, 3, 2.8)],
            ("hair", "hair_mid", "silver", "hair")[i%4], 3.1)
    for s in (1,-1):
        for i in range(4):
            hair.sweep([
                (s*(12-i*2), 100+i, 8-i*4, 5, 3, 5),
                (s*(21+i*.4), 93-i*1.7, 9-i*5.6, 4.4, 4.5, 4.5),
                (s*(25+i*.55), 86-i*1.9, 4-i*4.8, 3.2, 3.2, 4),
                (s*(27+i*.3), 87-i*3.2, 2-i*4, 2, 1.6, 2.6)],
                ("silver","hair","hair_mid","hair")[i], 3.0)
        for i in range(4):
            z=13-i*4.7
            hair.sweep([
                (s*(13+i*1.7), 91-i, z, 4, 4, 4),
                (s*(18-i*.12), 82-i*.75, z+1, 3.3, 4, 3.6),
                (s*(19+i*.7), 73-i*.6, z, 3.2, 4.8, 3.5),
                (s*(18+i*1.1), 63-i*1.8+(1 if s==1 else 0), z-3, 2.4, 3.1, 2.7)],
                ("silver","hair","hair_mid","hair")[i], 3.2)

    crown = [
        [(5,91,10,5,5,6),(9,103,6,6,5,7),(3,109,1,7,4,7),(-7,110,-1,6,3.5,6),(-12,114,1,2.2,3.2,3)],
        [(-4,91,13,4.5,5,4),(-1,101,12,5,4.5,5),(-8,103,9,5.5,4,6),(-17,99,6,5,3.4,5),(-23,102,5,2,3,3)],
        [(8,90,10,5,5,5),(16,100,7,5.5,4,5),(21,97,1,5,3,4),(24,98,-2,2.2,2,2.2)],
        [(6,104,-8,5,4,6),(13,104,-13,5,4,5),(19,99,-13,4,3,4),(23,98,-10,3,2,2.7)],
        [(-1,100,14,4,4,4),(-7,94,16,4,5,4),(-9,86,17,3.2,4.8,3.2),(-10,83,16,2.8,2.2,2.7)],
        [(5,98,14,4.5,4,4),(10,94,16,4.1,5,4),(13,87,17,3.1,5,3.2),(13,84,16,2.7,2,2.7)],
        [(-9,98,10,5.2,4,4),(-15,94,13,4,5,4),(-17,85,14,3,5,3),(-18,79,13,2.4,2.4,2.5)],
    ]
    for i,points in enumerate(crown):
        hair.sweep(points, ("silver","hair","hair_mid","hair","silver","hair","silver")[i], 3.0)


def torso_weights(x,y,z):
    if y < 37:
        return limb("Pelvis", "Spine", (y-28)/8)
    return limb("Spine", "Chest", (y-37)/9)


def torso():
    body=Volume("BaseBody", torso_weights)
    body.bevel((0,40,0),(11.5,13,6.5),3,"skin_shadow")
    neck=Volume("Neck",fixed("Neck"))
    neck.bevel((0,56,0),(6,5,5.5),2,"skin_shadow")
    tunic=Volume("TorsoClothing",torso_weights)
    def top_material(x,y,z):
        a=abs(x)
        if z>6 and 5.4<a<7.2 and y<47:
            return "gold_light" if y%7>5 else "gold_dark"
        if z>7 and a<5.8 and 33<y<46:
            for center in (36.2, 42.1):
                if min(abs(y-center-x*.73),abs(y-center+x*.73))<.69:
                    return "leather_light" if y>center-.5 else "leather"
        if z>5 and a>7 and a<11:
            return "seam"
        return "cloth"
    tunic.fill(-16,16,26,54,-10,10,
               lambda x,y,z:(abs(x)/(11.8+3.6*smooth((y-30)/20)))**5
               +(abs(z)/(7.5+1.3*smooth((y-30)/13)))**5<1
               and y<53-0.12*abs(x),top_material)

    tails=Volume("CoatTails", lambda x,y,z: limb("Pelvis",f"Thigh_{'L' if x>0 else 'R'}",
                                                .3*smooth((29-y)/15)))
    def skirt(x,y,z):
        q=(30-y)/15
        width=12.6+5.4*q
        opening=1+3*q
        radial=(abs(x)/width)**4 + (abs(z)/(8.0+3.0*q))**4
        return .37<radial<1 and (z<3 or abs(x)>opening)
    def skirt_material(x,y,z):
        opening=1+3*(30-y)/15
        if y<17 or (z>4 and abs(x)<opening+1.4):
            return "gold" if y%2 or abs(x)%4>1 else "gold_light"
        if z>5 and y<21 and int(abs(x))%6==1:
            return "gold_dark"
        return "seam" if z>4 and abs(x)%5<1.1 else "cloth"
    tails.fill(-19,19,15,31,-12,12,skirt,skirt_material)

    scarf=Volume("Scarf",fixed("Chest"))
    for level in range(3):
        stations=[]
        for i in range(25):
            angle=-math.pi+i*math.tau/24
            stations.append((math.sin(angle)*(7.0+1.24*level),
                             57.5-level*2.05-1.45*math.cos(angle),
                             math.cos(angle)*(7.3+1.02*level),
                             1.35+level*.3,1.38,1.55+level*.2))
        scarf.sweep(stations,("wool","wool_light","wool")[level],2.8)

    belt=Volume("BeltAccessories",fixed("Pelvis"))
    def belt_band(x,y,z):
        return .59<(abs(x)/14.1)**5+(abs(z)/9.8)**5<1.06
    belt.fill(-15,15,26,31,-11,11,belt_band,lambda x,y,z:"leather" if 27<y<30 else "leather_dark")
    belt.bevel((0,28.5,9.5),(4.65,3.5,1.5),.6,"silver_shadow")
    belt.fill(-5,5,25,32,10,12,
              lambda x,y,z: abs(x)<4.5 and abs(y-28.5)<3.5
              and (abs(x)>3.0 or abs(y-28.5)>2.0 or y>28 and y<29 and x<.5), "silver_metal")
    for s in (1,-1):
        belt.bevel((s*10.2,24.2,8.2),(3.2,5.2,2.8),1.1,"leather")
        belt.bevel((s*10.2,26.7,10.1),(3.3,2.0,1.5),.65,"leather_light")
        belt.bevel((s*10.2,25.0,11.3),(1.05,1.05,.55),.42,"gold_dark")
        belt.bevel((s*13.3,28.6,3.5),(.9,2.55,1.25),.55,"gold_dark")


def cape():
    def cape_weights(x,y,z):
        if y>41:
            return limb("CapeTop","Chest",(y-49)/3)
        if y>29:
            return limb("CapeMiddle","CapeTop",(y-34)/8)
        return limb("CapeHem","CapeMiddle",(y-21)/11)
    v=Volume("Cape",cape_weights)
    def surface(x,y,z):
        q=(52-y)/37
        width=15.5+6*q
        u=abs(x)/width
        if u>1 or y<15+1.5*u+1.0*math.cos(x*.6):
            return False
        depth=-9-9.1*q+4.3*u*u+0.8*math.cos(x*.85)*smooth(q+.25)
        return abs(z-depth)<1.28+.36*u
    def color(x,y,z):
        q=(52-y)/37
        u=abs(x)/(15.5+6*q)
        hem=y-(15+1.5*u+math.cos(x*.6))
        notch=2.8 if abs(x)<1.5 or 7.4<abs(x)<10.4 or abs(x)>17.5 else 0
        if hem<1.7+notch or u>.945 and y<25:
            return "gold_dark" if z>-14 else "gold"
        if u>.88 or math.cos(x*.85)<-.42:
            return "wine"
        return "plum" if math.cos(x*.85)<.55 else "cape_light"
    v.fill(-23,23,14,53,-25,-2,surface,color)
    mantle=Volume("Mantle",fixed("Chest"))
    def mantle_shape(x,y,z):
        radius=(abs(x)/17.8)**2.9+(abs(z)/11.6)**2.4
        rim=49.3-2.1*abs(z)/11-1.2*abs(x)/17
        return .28<radius<1.07 and rim<y<55-3.8*radius and (z<4 or abs(x)>3.7)
    def mantle_color(x,y,z):
        rim=49.3-2.1*abs(z)/11-1.2*abs(x)/17
        return "gold" if y<rim+1.25 else ("wine" if y>50.8 else "plum")
    mantle.fill(-19,19,45,55,-13,13,mantle_shape,mantle_color)
    for s in (1,-1):
        mantle.bevel((s*8.5,50,9.2),(2.25,2.45,1.52),.72,"gold_dark")
        mantle.bevel((s*8.5,50,10.3),(1.8,1.8,.62),.62,"gold_light")
        mantle.bevel((s*8.5,50,10.8),(.8,.8,.45),.45,"leather")


def fingers():
    # y, z, base joint, second joint, fingertip, half height, half depth.
    return {
        "Index": (51.4,3.0,42.6,46.4,49.0,1.22,.86),
        "Middle":(51.3,1.2,43.0,47.0,50.1,1.31,.84),
        "Ring":  (51.1,-.65,42.7,46.4,49.0,1.19,.84),
        "Pinky": (50.9,-2.14,42.4,45.5,47.5,1.06,.65),
        "Thumb": (49.4,3.3,39.7,42.1,44.4,1.42,1.22),
    }


def pin_weights(x,y,z):
    side="L" if x>0 else "R"
    u=abs(x)
    if z>2.4 and y<50.5 and u<44.6:
        name="Thumb"
    else:
        name=min(("Index","Middle","Ring","Pinky"),key=lambda f:abs(z-fingers()[f][1]))
    _,_,start,end,_,_,_=fingers()[name]
    if u<end-1:
        return limb(f"Hand_{side}", f"{name}1_{side}",(u-start+.5)/2.3)
    return limb(f"{name}1_{side}", f"{name}2_{side}",(u-end+1)/2)


def limbs():
    def arm_weights(x,y,z):
        side="L" if x>0 else "R"
        return limb(f"UpperArm_{side}", f"Forearm_{side}", (abs(x)-24)/6)
    arms=Volume("ArmClothing",arm_weights)
    hands=Volume("Hands",pin_weights)
    pants=Volume("Pants",lambda x,y,z:limb(f"Thigh_{'L' if x>0 else 'R'}",
                                          f"Calf_{'L' if x>0 else 'R'}",(17-y)/4))
    boots=Volume("Boots",lambda x,y,z:limb(f"Foot_{'L' if x>0 else 'R'}",
                                         f"Calf_{'L' if x>0 else 'R'}",(y-6)/4))
    for s in (1,-1):
        def sleeve(x,y,z):
            u=s*x
            r=mix(4.65,3.45,smooth((u-13)/21))
            if u>33:
                r=4.1
            return 11<u<38.4 and (abs(y-50.8)/r)**6+(abs(z)/(r*.94))**6<1
        def uniform(x,y,z):
            u=abs(x)
            if 34.2<u<36.2:
                return "gold" if z>0 or y>52 else "gold_dark"
            if u>33:
                return "leather_dark" if u>36.5 else "seam"
            return "coal" if (u>16 and y<49) else ("seam" if y>52 and int(u)%6<2 else "cloth")
        arms.fill(-39,39,45,57,-6,6,sleeve,uniform)
        hands.sweep([(s*37.3,51,0,1.9,2.75,2.65),(s*40,51,0.05,2.6,2.8,3.25),
                     (s*43,51,.2,1.85,2.42,3.45)],"skin",4)
        for name, (y,z,a,b,c,ry,rz) in fingers().items():
            dy=-.8 if name=="Thumb" else -.12
            dz=1.25 if name=="Thumb" else 0
            hands.sweep([(s*a,y,z,1.65,ry,rz),(s*b,y+dy,z+dz,1.6,ry*.96,rz),
                         (s*(c-.55),y+dy-.15,z+dz,0.86,ry*.84,rz*.83)],
                        "skin" if name!="Thumb" else "nose",3.5)
        pants.bevel((s*6,21,0),(5,8,5.5),1.8,lambda x,y,z:"seam" if z>3 and y>18 else "coal")
        pants.bevel((s*6,12,0),(4.36,5.1,4.4),1.2,"cloth")
        boots.bevel((s*6,5,.7),(5.25,5,5),1.05,"leather_dark")
        boots.bevel((s*6,7.7,.5),(5.65,1.65,5.22),.75,"leather")
        boots.bevel((s*6,3,2.6),(5.28,2.7,6.8),1.3,
                    lambda x,y,z:"leather_light" if z>6 and y>3.5 else "leather_dark")
        boots.bevel((s*6,1,2.3),(5.46,1,7.25),.63,"sole")


def png(path,width,height,pixels):
    def chunk(tag,data):
        return struct.pack(">I",len(data))+tag+data+struct.pack(">I",zlib.crc32(tag+data))
    data=b"".join(b"\0"+bytes(row) for row in pixels)
    path.write_bytes(b"\x89PNG\r\n\x1a\n"+chunk(b"IHDR",struct.pack(">IIBBBBB",width,height,8,2,0,0,0))
                     +chunk(b"IDAT",zlib.compress(data))+chunk(b"IEND",b""))


def atlas(output, colors):
    palette={name:i for i,name in enumerate(colors)}
    img=[[0]*(ATLAS_WIDTH*3) for _ in range(ATLAS_HEIGHT)]
    for name,index in palette.items():
        rgb=bytes.fromhex(colors[name][1:])
        for shade in range(SHADE_COUNT):
            idx=index*SHADE_COUNT+shade
            ux=(idx%32)*2
            uy=(idx//32)*2
            mod=(0.93, 0.98, 1.0, 1.035)[shade]
            for y in (uy,uy+1):
                for x in (ux,ux+1):
                    img[y][x*3:x*3+3]=[max(0,min(255,round(c*mod))) for c in rgb]
    png(output,ATLAS_WIDTH,ATLAS_HEIGHT,img)
    return palette


def uv_for(color, x, y, z, normal, palette):
    n = ((x//2)*73856093)^((y//3)*19349663)^((z//2)*83492791)
    shade = (0 if normal[1] < 0 else 3 if normal[1] > 0 else 2) if n%5 else 1
    i=palette[color]*SHADE_COUNT+shade
    return ((2*(i%32)+1)/ATLAS_WIDTH,(2*(i//32)+1)/ATLAS_HEIGHT)


class Gltf:
    def __init__(self, output=EXPORT, name="ElfRanger", colors=PALETTE, scale=SCALE):
        self.bin=bytearray()
        self.views=[]
        self.accessors=[]
        self.geom={}
        self.output=output
        self.name=name
        self.scale=scale
        self.texture_name=f"{name}_Color.png"
        output.mkdir(parents=True,exist_ok=True)
        self.palette=atlas(output/self.texture_name,colors)

    def access(self, values, fmt, kind, ctype, target=None, bounds=False):
        values=list(values)
        vf=struct.Struct("<"+fmt)
        self.bin.extend(b"\0"*((-len(self.bin))%4))
        start=len(self.bin)
        self.bin.extend(b"".join(vf.pack(*v) for v in values))
        view={"buffer":0,"byteOffset":start,"byteLength":len(self.bin)-start}
        if target:
            view["target"]=target
        self.views.append(view)
        desc={"bufferView":len(self.views)-1,"componentType":ctype,"count":len(values),"type":kind}
        if bounds:
            columns=list(zip(*values))
            desc["min"]=[min(c) for c in columns]
            desc["max"]=[max(c) for c in columns]
        self.accessors.append(desc)
        return len(self.accessors)-1

    def extra_faces(self,name):
        return ()

    def primitive(self,name,volume,material):
        vertices={}
        skinning={}
        positions,normals,texture,joints,weights,indices=[],[],[],[],[],[]

        def add_face(points,normal,uv,rigid=None):
            face=[]
            for p in points:
                key=(p,normal,uv,rigid)
                idx=vertices.get(key)
                if idx is None:
                    idx=vertices[key]=len(positions)
                    positions.append(tuple(round(t*self.scale,5) for t in p))
                    normals.append(normal)
                    texture.append(uv)
                    if rigid is not None:
                        bone_ids,bone_weights=(BONE_INDEX[rigid],0,0,0),(1.,0.,0.,0.)
                    else:
                        if p not in skinning:
                            binding=[(BONE_INDEX[n],w) for n,w in volume.weights(*p) if w>0.00001]
                            binding+=(4-len(binding))*[(0,0.0)]
                            total=sum(w for _,w in binding)
                            skinning[p]=(tuple(i for i,_ in binding),tuple(w/total for _,w in binding))
                        bone_ids,bone_weights=skinning[p]
                    joints.append(bone_ids)
                    weights.append(bone_weights)
                face.append(idx)
            for left,right in zip(face[1:],face[2:]):
                indices.extend((face[0],left,right))

        boundary=0
        for (x,y,z),color in sorted(volume.cells.items()):
            for normal,corners in FACE:
                a,b,c=normal
                if (x+a,y+b,z+c) in volume.cells or ((x,y,z),normal) in volume.open_faces:
                    continue
                boundary+=1
                uv=uv_for(color,x,y,z,normal,self.palette)
                add_face(((x+i,y+j,z+k) for i,j,k in corners),normal,uv)
        for points,normal,uv,rigid in self.extra_faces(name):
            add_face(points,normal,uv,rigid)
        self.geom[name]={"voxels":len(volume.cells),"surface_quads":boundary,
                         "triangles":len(indices)//3,"vertices":len(vertices),"material":material}
        return {
            "attributes":{
                "POSITION":self.access(positions,"3f","VEC3",5126,34962,True),
                "NORMAL":self.access(normals,"3f","VEC3",5126,34962),
                "TEXCOORD_0":self.access(texture,"2f","VEC2",5126,34962),
                "JOINTS_0":self.access(joints,"4B","VEC4",5121,34962),
                "WEIGHTS_0":self.access(weights,"4f","VEC4",5126,34962),
            },
            "indices":self.access(((i,) for i in indices),"I","SCALAR",5125,34963,True),
            "material":material,
        }

    def build(self, staging=OUT, include_animation=True):
        nodes=[]
        for name,p,parent in BONES:
            pp=BONES[BONE_INDEX[parent]][1] if parent is not None else (0,0,0)
            t=[round((a-b)*self.scale,6) for a,b in zip(p,pp)]
            children=[i for i,(_,_,q) in enumerate(BONES) if q==name]
            nodes.append({"name":name,"translation":t,**({"children":children} if children else {})})
        inverse=[]
        for _,p,_ in BONES:
            x,y,z=[v*self.scale for v in p]
            inverse.append((1,0,0,0,0,1,0,0,0,0,1,0,-x,-y,-z,1))
        ibm=self.access(inverse,"16f","MAT4",5126)

        materials=[]
        primitives=[]
        for name,volume in MODULES.items():
            primitives.append(self.primitive(name,volume,len(materials)))
            materials.append({"name":name,"pbrMetallicRoughness":{
                "baseColorTexture":{"index":0},"metallicFactor":0.0,"roughnessFactor":0.84}})
        mesh_node=len(nodes)
        nodes.append({"name":self.name,"mesh":0,"skin":0})

        anims=[]
        for name,clip in (make_clips(BONES).items() if include_animation else ()):
            times=self.access(((t,) for t in clip["times"]),"f","SCALAR",5126,bounds=True)
            anim={"name":name,"channels":[],"samplers":[]}
            for (joint,channel),values in clip["tracks"].items():
                output=self.access(values,"4f" if channel=="rotation" else "3f",
                                   "VEC4" if channel=="rotation" else "VEC3",5126)
                anim["channels"].append({"sampler":len(anim["samplers"]),
                                         "target":{"node":BONE_INDEX[joint],"path":channel}})
                anim["samplers"].append({"input":times,"output":output,"interpolation":"LINEAR"})
            anims.append(anim)
        doc={
            "asset":{"version":"2.0","generator":"CubeGame Elf Ranger"},
            "scene":0,"scenes":[{"name":"Elf Ranger — T-pose","nodes":[0,mesh_node]}],
            "nodes":nodes,"meshes":[{"name":self.name,"primitives":primitives}],"materials":materials,
            "skins":[{"name":"ElfRangerSkeleton","joints":list(range(len(BONES))),
                      "skeleton":0,"inverseBindMatrices":ibm}],
            **({"animations":anims} if anims else {}),
            "samplers":[{"magFilter":9728,"minFilter":9728,"wrapS":33071,"wrapT":33071}],
            "images":[{"uri":self.texture_name}],"textures":[{"source":0,"sampler":0}],
            "buffers":[{"byteLength":len(self.bin),"uri":f"{self.name}.bin"}],
            "bufferViews":self.views,"accessors":self.accessors,
        }
        folders=(self.output,) if staging is None or staging==self.output else (self.output,staging)
        for path in folders:
            (path/f"{self.name}.bin").write_bytes(self.bin)
            (path/f"{self.name}.gltf").write_text(json.dumps(doc,separators=(",",":")),encoding="utf-8")
            if path!=self.output:
                (path/self.texture_name).write_bytes((self.output/self.texture_name).read_bytes())
        (staging or self.output).joinpath("geometry.json").write_text(json.dumps(self.geom,indent=2),encoding="utf-8")
        print(json.dumps({"bones":len(BONES),"animations":[a["name"] for a in anims],
                          "triangles":sum(m["triangles"] for m in self.geom.values()),
                          "modules":list(MODULES)},indent=2))


if __name__=="__main__":
    EXPORT.mkdir(exist_ok=True)
    OUT.mkdir(exist_ok=True)
    skeleton()
    torso()
    limbs()
    cape()
    head()
    hairstyle()
    for module in ("Cape", "Hair"):
        MODULES[module].stitch_diagonals()
    Gltf().build()
