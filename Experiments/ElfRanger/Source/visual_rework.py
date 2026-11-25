"""Model the Elf Ranger visual candidate against the four owner reference views."""
from __future__ import annotations

import math

import model

OUTPUT = model.OUT / "VisualRework"
NAME = "ElfRanger_Visual"
COLORS = model.PALETTE | {
    "skin": "#f6b391", "skin_shadow": "#da967f", "ear": "#efa586",
    "ear_fold": "#bd6f61", "lip": "#9d5047", "nose": "#f3ae8b",
    "silver": "#ddd0d8", "hair": "#c7b8c3", "hair_mid": "#ae9eac",
    "hair_dark": "#a092a1", "brow": "#89707d", "lash": "#261822",
    "white": "#eee5e8", "iris": "#861cac", "violet": "#44155a",
    "pupil": "#210b33", "glint": "#e0aaff",
    "cloth": "#554956", "seam": "#6b5a67", "coal": "#423742",
    "lining": "#30282f", "wool": "#8e828a", "wool_light": "#aaa0a8",
    "gold": "#cf9639", "gold_dark": "#9c652a", "gold_light": "#e5ae55",
    "plum": "#824454", "wine": "#703646", "cape_shadow": "#54323b",
    "cape_light": "#905365", "leather": "#825444", "leather_light": "#a36c55",
    "leather_dark": "#513733", "sole": "#3b2928", "silver_metal": "#d2c1c7",
    "silver_shadow": "#8c7880",
    "hair_ridge": "#d2c3ce", "hair_occlusion": "#887684",
    "skin_light": "#fac2a0", "skin_half": "#efa888",
    "iris_lower": "#a122c4", "iris_highlight": "#c35ae3",
    "panel": "#5b4d58", "panel_forward": "#645460", "stitch": "#806573",
    "pouch_edge": "#a1745b", "cape_thread": "#854453", "wool_shadow": "#655a63",
}


def head_and_ears(*, with_face=True):
    skin = model.Volume("Head", model.fixed("Head"))

    def complexion(x, y, z):
        if z < 15.5:
            return "skin_shadow" if y < 70 else "skin_half"
        if y < 61 or abs(x) > 14:
            return "skin_half"
        return "skin_light" if y > 70 and abs(x) < 6 else "skin"

    skin.bevel((0, 77, 3), (16, 18, 16.8), 2.7, complexion)
    if with_face:
        face(skin)

    ears = model.Volume("Ears", model.fixed("Head"))
    for side in (-1, 1):
        def ear_shape(x, y, z):
            t = (side*x-14.4)/22.6
            if not 0 <= t < 1:
                return False
            bottom = 65.5+12.0*t**1.16
            front = 10.9-6.4*t + max(0, y-bottom-2)*.13
            depth = 4.65*(1-t)+.8
            return bottom<y<79.3-.55*t and front-depth<z<front+.85

        def ear_color(x, y, z):
            t = (side*x-14.4)/22.6
            if y>77.0 or t>.91:
                return "skin_light"
            if y<69+8*t and z>5:
                return "ear_fold"
            return "ear" if z>4.5 else "skin_half"

        ears.fill(-37, 37, 64, 81, 0, 15, ear_shape, ear_color)


def face(skin):
    skin.bevel((0, 67.2, 19.25), (1.25, 1.24, 1.15), .75, "nose")
    details = model.Volume("Face", model.fixed("Head"))
    details.bevel((0, 64.5, 20.25), (2.55, .35, .79), .30, "lip")
    for side in (-1, 1):
        def eye_shape(x, y, z):
            u = side*x
            left_corner = max(0, 6.3-u)
            right_corner = max(0, u-13.2)
            top = 79.0+.24*(u-7.5)-.36*right_corner
            bottom = 68.7+.29*left_corner+.58*right_corner
            return 5.45<u<14.8 and bottom<y<top and 18.75<z<21.3-.23*right_corner

        def eye_color(x, y, z):
            u = side*x
            top = 79.0+.24*(u-7.5)-.36*max(0, u-13.2)
            if y > top-1.9:
                return "lash"
            if 6.7<u<11.9:
                if 7.6<u<8.65 and 73.6<y<75.4:
                    return "iris_highlight"
                if 7.9<u<9.8 and y>71.1:
                    return "pupil"
                if y>74.1 or u>10.5:
                    return "violet"
                return "iris_lower" if y<70.6 else "iris"
            return "white" if y<75.8 else "silver_metal"

        details.fill(-16, 16, 68, 81, 18, 21, eye_shape, eye_color)
        details.fill(-17,17,70,80,18,22,
                     lambda x,y,z:15<side*x<16.49 and 18.6<z<21.1 and 70.6<y<77.6,
                     lambda x,y,z:"lash" if y>76.5 else
                     ("white" if z>20.0 else ("violet" if y>74 else "iris")))
        details.fill(
            -16, 16, 79, 86, 19, 22,
            lambda x,y,z: 5.05 < side*x < 14.8 and
            abs(y-(80.6+.26*side*x))<1.02 and z<21.1,
            lambda x,y,z: "hair_mid" if y>82.0+.23*side*x else "brow",
        )


def unit(v):
    length = math.sqrt(sum(x*x for x in v))
    return tuple(x/length for x in v)


def cross(a, b):
    ax, ay, az = a
    bx, by, bz = b
    return ay*bz-az*by, az*bx-ax*bz, ax*by-ay*bx


def dot(a, b):
    return sum(x*y for x,y in zip(a,b))


def hair_ribbon(volume, path, facing, tone):
    """Voxelize a flattened lock along its tangent, with deeper tones along the edges."""
    for left, right in zip(path, path[1:]):
        p, q = left[:3], right[:3]
        direction = unit(tuple(b-a for a,b in zip(p,q)))
        side = unit(cross(direction,facing))
        outward = unit(cross(side,direction))
        count = math.ceil(math.dist(p,q)/.72)
        for i in range(count+1):
            t = i/count
            center = tuple(model.mix(a,b,t) for a,b in zip(p,q))
            width = model.mix(left[3],right[3],t)
            thickness = model.mix(left[4],right[4],t)
            radii = tuple(abs(s)*width+abs(n)*thickness+abs(v)*1.85
                          for s,n,v in zip(side,outward,direction))

            def contains(x,y,z):
                d = tuple(v-c for v,c in zip((x,y,z),center))
                return (abs(dot(d,side)/width)**3.6+abs(dot(d,outward)/thickness)**3.2+
                        abs(dot(d,direction)/1.85)**2.8) <= 1

            def color(x,y,z):
                d = tuple(v-c for v,c in zip((x,y,z),center))
                depth = dot(d,outward)/thickness
                edge = abs(dot(d,side)/width)
                if depth<-.36 or edge>.84:
                    return "hair_mid" if tone=="silver" else "hair_dark"
                if depth>.58:
                    return "silver" if tone!="hair_mid" and edge<.68 else "hair_ridge"
                return tone

            x,y,z = center
            a,b,c = radii
            volume.fill(x-a,x+a,y-b,y+b,z-c,z+c,contains,color)


def hair():
    volume = model.Volume("Hair", model.fixed("Head"))
    sections = (
        (53,7,-18,7), (60,15,-18,12), (70,23,-14,17),
        (83,26,-8,22), (97,26,-4,24), (107,20,-5,21), (113,9,0,12),
    )

    def scalp(x,y,z):
        a,b = next((a,b) for a,b in zip(sections,sections[1:]) if a[0]<=y<=b[0])
        t=(y-a[0])/(b[0]-a[0])
        w, center, depth = (model.mix(i,j,t) for i,j in zip(a[1:],b[1:]))
        if y<94 and z>11 and abs(x)<16.8:
            return False
        if 68<y<81 and z>1 and abs(x)>16:
            return False
        return (abs(x)/w)**2.6+(abs(z-center)/depth)**2.3<1

    volume.fill(-28,28,53,113,-34,24,scalp,
                lambda x,y,z: "hair_occlusion" if z>7 and y<97 else
                ("hair" if y>93 else "hair_mid"))

    crown = [
        ([(-13.0,115.1,5,1.8,1.5),(-11.9,112.4,9,3.4,2.3),(-2,111.6,12,4.2,2.9),
          (5,109.3,13,4.6,3.5),(9,105,13,4.5,3.4),(7.5,99,16,3.0,2.3)],"silver"),
        ([(-12,111,1,2.3,2),(-6,109,-3,4.6,2.7),(4,108,-5,5.7,4.0),
          (11,101,-9,5.6,3.9),(10,98,-15,4.2,2.5)],"hair"),
        ([(5,105,15,4,3),(1,103,19,5.4,3),(-8,101,20,5,3.1),(-18,98,19,4.5,3.1),
          (-23,99,16,3.5,2.1),(-24,102,14,1.6,1.2)],"silver"),
        ([(7,105,5,4.8,3.2),(15,104,1,5.6,3.1),(21,101,-7,5.4,2.9),
          (24,102,-13,3.6,2.0),(24,105,-16,1.6,1.4)],"hair"),
        ([(2,100,18,4.6,2.8),(-3,97,23,4.1,2.65),(-4.6,86.4,25,2.65,2.0)],"silver"),
        ([(-7,98,18,4,3),(-12,95,23,4.2,2.7),(-12.8,86.0,25,3.4,2.2)],"hair_ridge"),
        ([(-11,95,15,4,2.8),(-16,89,23,3.7,2.9),(-17,80,23,2.6,2.3)],"silver"),
        ([(10,100,16,3.7,2.6),(10.3,95,22,4.7,2.8),(10.0,87,25,3.4,2.45)],"silver"),
        ([(16,97,15,4.3,2.8),(16,91,22.5,3.8,2.85),(17,83.0,23,2.8,2.55)],"hair_ridge"),
        ([(19,90,16,3.6,2.7),(20,84,20,3.1,2.3),(20.4,75,17.5,2.4,2.1),
          (19.6,65,15,2.1,1.8)],"hair"),
        ([(-18,89,17,3.4,2.5),(-20,82,19.7,3.05,2.8),(-19.8,72,16.5,2.5,2.4),
          (-17.5,63,16,1.8,1.5)],"hair_ridge"),
    ]
    for path, tone in crown:
        hair_ribbon(volume,path,(0,.35,1),tone)

    for s in (-1,1):
        side_layers = (
            ([(11,105,-2,4.7,3.8),(17,102,-9,5.2,3.5),(23,97,-20,5.2,3.45),
              (26,95,-25,4.1,2.7),(25,98,-28,2.45,1.6)],"silver"),
            ([(13,100,12,5,3.3),(21,97,7,5.25,3.8),(25.5,92,-3,5.6,3.7),
              (27.0,90,-17,4.9,3.15),(25.8,93,-23,2.1,1.55)],"hair"),
            ([(18,94,8,5.3,3.3),(24,91,4,5.1,3.7),(27,85,-7,5.45,3.4),
              (29,84,-20,4.3,3.5),(28,85,-27,2.45,1.9)],"hair_ridge"),
            ([(22,86,-2,4.5,3.6),(24.5,82,-11,5,3.9),(26.5,76,-23,5.4,3.4),
              (25.5,76,-32.0,3.7,2.6),(24.2,79,-33,2.1,1.45)],"silver"),
            ([(19,76,-8,4.1,3.1),(22,72,-15,5.0,3.6),(23,68,-26,4.25,3.5),
              (21.5,70,-32.5,2.6,2)],"hair_mid"),
            ([(17,71,-7,4.3,3.5),(19,65,-16,4.1,3.65),(19,62,-24,3.75,3.2),
              (16.5,63,-28.7,2.2,1.9)],"hair"),
            ([(14,64,-10,4.2,3.0),(16,57,-16,4.0,3.1),(14,54,-23,3.0,2.5),
              (12,55,-26,1.9,1.6)],"hair_mid"),
        )
        for i,(path,tone) in enumerate(side_layers):
            mirrored=[(s*(x-1.35*model.smooth((x-23)/6)),
                       y+(.8 if s==1 and i%3==0 else 0),z,w,d) for x,y,z,w,d in path]
            hair_ribbon(volume,mirrored,(s,.44,-.15),tone)
        hair_ribbon(volume,[(s*20,88,11,3.35,2.3),(s*20,79,8.5,2.75,2.6),
                            (s*19.8,70,5.0,2.85,2.55),(s*18,63,5,1.85,1.9)],
                    (s,.15,.28),"hair")
        hair_ribbon(volume,[(s*21,77,-6,3.8,2.7),(s*21.5,70,-5,3.5,3.05),
                            (s*20,64,-6.5,2.9,2.7),(s*18.8,59,-9,1.5,1.7)],
                    (s,0,-.22),"hair_mid")

    cascades = [
        ([(-11,110,-12,2.6,2.6),(-4,109,-17,4.4,3.7),(6,108,-20,4.5,3.75),
          (15,105,-22,4.2,3.4),(21,106.3,-23,2.9,2.3),(22,109,-22,1.6,1.55)],"silver"),
        ([(-18,101,-15,3.84,2.65),(-16,104,-20,4.0,3.8),(-10,103,-23,4.1,3.55),
          (-3,99,-24,3.9,3.0),(3,96,-24.5,2.8,1.8)],"hair_ridge"),
        ([(6,105,-14,3.6,3.0),(13,102,-20,4.1,3.4),(20,98,-24,4.4,3.0),
          (23,97,-27,2.85,2.18),(25,99.5,-27,1.65,1.55)],"hair"),
        ([(-4,108,-9,5.0,3.7),(-8,101,-18,5.6,4.2),(-11,93,-25,5,4.0),
          (-15,87,-29,4.35,3.3),(-19,88,-29,2.1,2.05)],"silver"),
        ([(3,109,-8,4.1,3.9),(1,102,-19,4.5,4.6),(0,94,-25,4.6,4.1),
          (2,86,-30,3.65,3.45),(5,86,-31,2.3,1.8)],"hair_ridge"),
        ([(7,104,-10,5.1,4.4),(10,97,-18,5.6,4.7),(13,91,-25,5.1,4.0),
          (18,88,-30,3.65,3.2),(22,91,-30,2.3,1.7)],"hair"),
        ([(-17,96,-18,4.9,3.7),(-19,88,-26,4.8,4.1),(-21,80,-31,4.45,3.8),
          (-23,78,-31,2.55,2.0)],"hair"),
        ([(-10,93,-23,4.6,4.2),(-11,86,-30,4.3,4.1),(-11,76,-34,4.35,3.7),
          (-8,74,-34,2.4,2.2)],"silver"),
        ([(-2,94,-24,4.1,4.2),(-3,86,-29,4.4,4.6),(-3,79,-33.2,4.45,4),
          (-1,76,-34.1,2.6,2.35)],"hair"),
        ([(6,92,-24,4.5,4.5),(8,85,-31,5.1,4.4),(10,77,-34,4.6,3.5),
          (14,76,-32.8,2.65,2.15)],"hair_ridge"),
        ([(16,87,-23,4.6,4.3),(18,81,-29,4.25,4.2),(18,72,-32.5,4.05,3.6),
          (21,71,-30,2.3,1.9)],"hair_mid"),
        ([(-21,81,-22,4.4,4.1),(-22,74,-28,4.2,3.6),(-19,66,-31,4.0,3.25),
          (-18,63.5,-29.0,2.4,1.9)],"hair"),
        ([(-12,78,-24,4.2,4.1),(-12,69,-29,4.0,4.1),(-10,61,-29,3.2,3.05),
          (-9,58,-27.8,1.7,1.6)],"hair_mid"),
        ([(-5,76,-25,3.7,3.5),(-5,68,-30,4.2,3.95),(-4,60,-29,3.4,3.3),
          (-2,53,-25,1.6,1.7)],"hair_ridge"),
        ([(3,78,-25,4.3,4.0),(5,70,-31,4.25,4.2),(6,61,-29,3.8,3.2),
          (7,55,-25,1.85,1.57)],"hair"),
        ([(12,78,-23,4.3,4.15),(14,69,-29,4.3,3.9),(14,63,-29.3,3.55,3.28),
          (15,60,-27.2,1.8,1.82)],"silver"),
    ]
    for path,tone in cascades:
        hair_ribbon(volume,path,(0,.18,-1),tone)


def clothing():
    model.torso()
    tunic = model.Volume("TorsoClothing",model.torso_weights)

    def torso_radius(y):
        return 13.2+3.7*model.smooth((y-30)/21)

    def torso_color(x,y,z):
        a=abs(x)
        if z<6.75:
            return "cloth" if y>34 else "lining"
        if 5.7<a<7.75 and 31<y<50:
            return "gold_dark" if a>7.1 else "gold"
        if 7.7<a<9.5 and 32<y<47 and int(y+1)%5==0:
            return "gold"
        if a<6 and 32<y<46:
            if any(min(abs(y-c-x*.7),abs(y-c+x*.7))<.6 for c in (35.5,41.8)):
                return "leather" if y<40 else "leather_light"
            return "lining"
        return "panel" if a<12 or (int(x//3)+int(y//4))%7==0 else "cloth"

    tunic.fill(-18,18,26,54,-10,11,
               lambda x,y,z:(abs(x)/torso_radius(y))**5+(abs(z)/(8.6+model.smooth((y-31)/13)))**4.4<1
               and y<53.7-.105*abs(x),torso_color)

    skirt = model.Volume("CoatTails", lambda x,y,z:model.limb(
        "Pelvis",f"Thigh_{'L' if x>0 else 'R'}",.25*model.smooth((29-y)/15)))

    def skirt_shape(x,y,z):
        t=model.clamp((31-y)/18)
        width=14.8+9.65*t
        depth=9.0+3.35*t
        rim=(abs(x)/width)**4+(abs(z)/depth)**4
        slit=2.0+4.15*t
        hem=13.5+.08*abs(x-4.0)
        return y>hem and .49<rim<1 and (z<3.5 or abs(x)>slit)

    def skirt_color(x,y,z):
        t=model.clamp((31-y)/18)
        a=abs(x)
        slit=2+4.15*t
        hem=13.5+.08*abs(x-4.0)
        if y<hem+1.8 or (z>6 and a<slit+1.6):
            return "gold_light" if y<hem+1 else "gold"
        emblem_y=y-hem
        if z>6 and ((3<emblem_y<5 and (int(a+1)%6 in (0,1))) or
                    (5<emblem_y<7 and int(a+1)%6==1)):
            return "gold"
        if z<8:
            return "cloth"
        return "panel_forward" if (int(a//3)+int(y//5))%7 in (1,2) else "panel"

    skirt.fill(-25,25,13,31,-14,14,skirt_shape,skirt_color)

    scarf=model.Volume("Scarf",model.fixed("Chest"))
    for row, tone in enumerate(("wool_shadow","wool_light","wool")):
        points=[]
        for i in range(27):
            angle=-math.pi+i*math.tau/26
            front=max(0,math.cos(angle))
            points.append((math.sin(angle)*(9.3+row*.95),59.2-row*1.4-(3.65+row*1.1)*front,
                           math.cos(angle)*(7+row*1.2)+1.2,1.7,1.66,1.65))
        scarf.sweep(points,tone,3.0)

    belt=model.Volume("BeltAccessories",model.fixed("Pelvis"))
    belt.fill(-20,20,30,37,-14,14,
              lambda x,y,z:.57<(abs(x)/19.0)**5+(abs(z)/11.4)**5<1.0,
              lambda x,y,z:"leather_dark" if y<31 or y>35 else "leather")
    belt.bevel((0,33.8,10.6),(5.1,3.7,1.53),.66,"leather_dark")
    belt.fill(-6,6,30,38,11,14,
              lambda x,y,z:abs(x)<5 and abs(y-33.8)<3.55 and
              (abs(x)>3.35 or abs(y-33.8)>2.0 or 33.1<y<34.25 and 0<x<4.2),
              lambda x,y,z:"silver_metal" if z>12 else "silver_shadow")
    belt.bevel((0,26.7,11.3),(4.0,3.7,1.15),.7,"leather")
    belt.bevel((0,27.2,12.17),(2.05,1.2,.75),.55,"leather_dark")
    for s in (-1,1):
        def pouch(x,y,z):
            u=s*x-(16.65+.26*(30-y))
            return (abs(u)/3.65)**5+(abs(y-28.7)/5.35)**5+(abs(z-10.7)/3.05)**5<1

        def pouch_color(x,y,z):
            u=s*x-(16.65+.26*(30-y))
            if y>30 and z>11:
                return "leather_light" if abs(u)>2.1 or y>32 else "pouch_edge"
            if z>12 and abs(u)>2.5:
                return "leather_light"
            return "leather"

        belt.fill(-23,23,23,35,7,15,pouch,pouch_color)
        belt.bevel((s*17.4,30.7,13.6),(1.8,1.25,.82),.63,"leather_dark")
        belt.bevel((s*17.5,29.7,14.15),(.73,.77,.48),.4,"gold_dark")


def cape():
    def cape_weights(x,y,z):
        if y>42:
            return model.limb("CapeTop","Chest",(y-49)/3)
        if y>29:
            return model.limb("CapeMiddle","CapeTop",(y-34)/8)
        return model.limb("CapeHem","CapeMiddle",(y-17)/13)

    cape=model.Volume("Cape",cape_weights)

    def profile(x,y):
        t=model.clamp((55-y)/42)
        width=18.7+10.5*t
        u=abs(x)/width
        fold=1.55*math.cos(x*.415+.19*y*.02)+.57*math.cos(x*.87-.2)
        depth=-12.0-11.1*t+(5.6+3*t)*u*u+fold*(.48+.85*t)
        hem=12.1+4.45*u**1.35+.75*math.cos(x*.55)
        return u, depth, hem, fold

    def cape_shape(x,y,z):
        u,depth,hem,_=profile(x,y)
        return u<1 and y>hem and y<54-2.6*u and abs(z-depth)<1.37+.43*u

    def cape_color(x,y,z):
        u,depth,hem,fold=profile(x,y)
        a=abs(x)
        trim=y-hem
        inset=1.6 if a<1.3 or 14.6<a<18.8 else 0
        if a<2.0 and 17.2<y<22:
            return "gold"
        if trim<1.65+inset or (u>.91 and y<25 and trim<3.8):
            return "gold" if z<depth+.4 else "gold_dark"
        if 4.7<trim<6.2 and (a<3.4 or u>.87 or int(a+2)%8==0):
            return "gold_dark"
        if z>depth+.68:
            return "cape_shadow"
        if u<.77 and fold>.74:
            return "cape_light"
        if fold<-.58 or u>.86:
            return "wine"
        return "cape_thread" if int(x//2+y//3)%9==0 else "plum"

    cape.fill(-30,30,11,55,-28,-3,cape_shape,cape_color)
    cape.fill(-12,12,16,51,-25,-8,
              lambda x,y,z:17+.15*abs(x)<y<51 and profile(x,y)[1]+.4<z<-9.4-.06*(51-y),
              "cape_shadow")

    mantle=model.Volume("Mantle",model.fixed("Chest"))
    for front in (False,True):
        def shape(x,y,z):
            xabs=abs(x)
            if front:
                if z<1 or (z>5 and xabs<7.1) or xabs>26:
                    return False
                border=46.4+.24*abs(xabs-12.7)+.125*max(0,xabs-18)
                r=(xabs/26.0)**3.6+(max(0,z-2)/11.0)**2.1
                return .19<r<1.10 and border<y<56.8-3.6*r
            border=39.7+.0184*x*x+.09*max(0,z+10)
            rear=-17.7+.0126*x*x+.09*(y-43.0)
            r=(xabs/25.5)**2.8+(abs(z+2)/14.3)**3
            return z<=2 and (.25<r<1.04 or abs(z-rear)<1.65) and xabs<25.7 and border<y<56.5-3.6*min(r,1)

        def mantle_color(x,y,z):
            a=abs(x)
            border=(46.4+.24*abs(a-12.7)+.125*max(0,a-18)) if front else (
                39.7+.0184*x*x+.09*max(0,z+10))
            if y-border<1.6:
                return "gold" if y-border>.6 else "gold_dark"
            if y-border<3.2 and int(a+.4)%5==1:
                return "gold_dark"
            if y-border<4.55:
                return "plum"
            return "wine" if y>52 else "cape_shadow"

        mantle.fill(-27,27,39,57,-18,13,shape,mantle_color)

    for side in (-1,1):
        hx=side*9.05
        mantle.bevel((hx,52.85,12.2),(2.4,2.6,2.6),.95,"gold_dark")
        mantle.bevel((hx,52.85,14.45),(1.97,2.1,.85),.80,"gold_light")
        mantle.bevel((hx,52.85,15.35),(.96,1.0,.51),.45,"leather_dark")
        mantle.bevel((side*8.85,53.1,15.6),(.61,.72,.54),.46,"gold")


def limbs():
    model.limbs()
    hands=model.MODULES["Hands"]
    hands.cells={p:color for p,color in hands.cells.items() if abs(p[0]+.5)<47.25}
    pants=model.Volume("Pants",lambda x,y,z:model.limb(
        f"Thigh_{'L' if x>0 else 'R'}",f"Calf_{'L' if x>0 else 'R'}",(18-y)/6))
    boots=model.Volume("Boots",lambda x,y,z:model.limb(
        f"Foot_{'L' if x>0 else 'R'}",f"Calf_{'L' if x>0 else 'R'}",(y-7)/4))
    for s in (-1,1):
        pants.bevel((s*8,20,.15),(6.4,8.5,6.45),1.5,"coal")
        pants.bevel((s*8.4,13,-.05),(6.35,5.1,5.6),.9,
                    lambda x,y,z:"cloth" if z>2 else "coal")
        boots.bevel((s*9.1,5.8,-.1),(6.65,5.8,6.05),.99,"leather_dark")
        boots.bevel((s*9.1,9.0,.1),(7.35,2.05,6.5),.65,
                    lambda x,y,z:"leather" if y>8.5 else "leather_dark")
        boots.bevel((s*9.1,2.85,2.9),(7.0,2.85,7.36),.95,
                    lambda x,y,z:"leather" if z>6 and y>3.4 else "leather_dark")
        boots.bevel((s*9.1,.7,2.3),(7.2,.7,8.25),.47,"sole")


def main():
    model.skeleton()
    clothing()
    limbs()
    cape()
    head_and_ears()
    hair()
    for volume in model.MODULES.values():
        volume.stitch_diagonals()
    model.Gltf(output=OUTPUT,name=NAME,colors=COLORS).build(staging=None,include_animation=False)


if __name__=="__main__":
    main()
