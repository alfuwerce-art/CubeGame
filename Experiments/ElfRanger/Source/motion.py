"""Local-space animation curves for the Elf Ranger's authored skeleton."""
from __future__ import annotations

import math


FPS = 30
TAU = math.tau
RAD = math.pi / 180
LEFT_RIGHT = ((1, "L"), (-1, "R"))


def clamp(t, lo=0, hi=1):
    return max(lo, min(hi, t))


def ease(t):
    t = clamp(t)
    return t*t*(3-2*t)


def axis(axis_index, deg):
    h = deg*RAD/2
    xyz = [0.0, 0.0, 0.0]
    xyz[axis_index] = math.sin(h)
    return (*xyz, math.cos(h))


def mul(p, q):
    a,b,c,d = p
    e,f,g,h = q
    return (d*e+a*h+b*g-c*f, d*f-a*g+b*h+c*e, d*g+a*f-b*e+c*h, d*h-a*e-b*f-c*g)


def euler(rx=0, ry=0, rz=0):
    return mul(mul(axis(1,ry),axis(0,rx)),axis(2,rz))


def lerp(a,b,t):
    return a+(b-a)*t


def pose():
    return {}, {}


def rot(p, joint, x=0, y=0, z=0):
    p[0][joint] = euler(x,y,z)


def offset(p, joint, x=0, y=0, z=0):
    p[1][joint] = x,y,z


def fingers(p, s, side, grip):
    for index,f in enumerate(("Index","Middle","Ring","Pinky")):
        rot(p,f"{f}1_{side}",0,0,-s*(13+7*index/3)*grip)
        rot(p,f"{f}2_{side}",0,0,-s*(23+8*index/3)*grip)
    rot(p,f"Thumb1_{side}",s*10*grip,s*12*grip,-s*12*grip)
    rot(p,f"Thumb2_{side}",0,s*19*grip,-s*10*grip)


def arm(p,s,side,swing=0,out=13,elbow=17,roll=0,wrist=0,grip=.35):
    rot(p,f"UpperArm_{side}",swing,roll,-s*(90-out))
    rot(p,f"Forearm_{side}",0,-s*elbow)
    rot(p,f"Hand_{side}",s*wrist,0,s*5)
    fingers(p,s,side,grip)


def leg(p,s,side,fore,height,hip_delta,bend=0,foot_pitch=0):
    """Solve the ankle position in the sagittal plane with a forward knee."""
    down = 21+hip_delta-height
    length1,length2=math.hypot(11,.5),math.hypot(10,.5)
    dist=clamp(math.hypot(down,fore),.1,length1+length2-.001)
    aim=math.atan2(fore,down)
    upper=aim+math.acos(clamp((length1**2+dist**2-length2**2)/(2*length1*dist),-1,1))
    inner=math.acos(clamp((length1**2+length2**2-dist**2)/(2*length1*length2),-1,1))
    ref1=math.atan2(.5,11)
    ref2=math.atan2(-.5,10)
    a=-(upper-ref1)/RAD
    b=(math.pi-inner+ref2-ref1)/RAD
    rot(p,f"Thigh_{side}",a,0,s*bend)
    rot(p,f"Calf_{side}",b)
    rot(p,f"Foot_{side}",foot_pitch-a-b,0,-s*bend)
    rot(p,f"Toe_{side}",-max(0,foot_pitch)*.6)


def idle(t):
    p=pose()
    k=math.sin(TAU*t)
    dip=-1.2+.22*k
    offset(p,"Pelvis",x=.16*k,y=dip)
    rot(p,"Pelvis",0,.8*k,1.1)
    rot(p,"Spine",-1.1*k,0,-.65)
    rot(p,"Chest",-1.2-1.2*k,.8*k)
    rot(p,"Head",.8*k,2.5*math.sin(TAU*t-.3),-.8)
    rot(p,"CapeTop",-1.2*k)
    rot(p,"CapeMiddle",.9*math.sin(TAU*t-.5))
    rot(p,"CapeHem",1.2*math.sin(TAU*t-1))
    for s,side in LEFT_RIGHT:
        arm(p,s,side,swing=-5+s*3+1.7*k,out=16+s*2,elbow=14+s*3,grip=.28-.06*k)
        leg(p,s,side,fore=s*.65,height=0,hip_delta=dip)
    return p


def gait(t,running):
    p=pose()
    q=t*TAU
    contact=.56 if not running else .39
    stride=12.3 if not running else 17.7
    hip=-2.25-.48*math.cos(q*2) if not running else -3.1-1.35*math.cos(q*2)
    if running:
        hip+=max(0,math.sin(q*2-.9))*1.9
    offset(p,"Pelvis",x=.5*math.sin(q),y=hip,z=.38*math.cos(q*2))
    rot(p,"Pelvis",0,4.8*math.cos(q),1.9*math.sin(q))
    rot(p,"Spine",-3 if not running else -8,-3*math.cos(q),-1.9*math.sin(q))
    rot(p,"Chest",-2 if not running else -9,-4.1*math.cos(q),-.7*math.sin(q))
    rot(p,"Neck",4 if not running else 10)
    rot(p,"Head",2+1.4*math.sin(q*2),1.25*math.sin(q),.7*math.sin(q))
    rot(p,"CapeTop",4 if not running else 15,0,2.0*math.sin(q))
    rot(p,"CapeMiddle",4+3*math.sin(2*q-.45) if not running else 8+6*math.sin(2*q-.45))
    rot(p,"CapeHem",2+4*math.sin(2*q-.9) if not running else 6+8*math.sin(2*q-.9))
    for s,side in LEFT_RIGHT:
        u=(t+(0 if s==1 else .5))%1
        if u<contact:
            fore=lerp(stride/2,-stride/2,u/contact)
            rise=0
            foot_pitch=0
        else:
            v=(u-contact)/(1-contact)
            fore=lerp(-stride/2,stride/2,ease(v))
            rise=(4.8 if not running else 9.4)*math.sin(v*math.pi)
            foot_pitch=(7 if not running else 16)*math.sin(TAU*v)
        leg(p,s,side,fore,rise,hip,foot_pitch=foot_pitch)
        arm(p,s,side,(22 if not running else 46)*s*math.cos(q)+2,
            out=15 if not running else 19,elbow=23 if not running else 73,wrist=5,grip=.46)
    return p


#  Per-key values are normalized time, hip height, torso pitch, torso yaw,
#  arm pitch L/R, elbows L/R, ankle forward L/R, ankle lift L/R, cape pitch.
ACTIONS = {
    "Jump":[(0,-1,0,0,-6,-7,18,17,0,0,0,0,1),
            (.21,-6,-14,0,24,24,21,21,1,-1,0,0,4),
            (.39,8,-6,2,-54,-64,42,46,2,-5,11,9,17),
            (.66,16,1,3,-29,-48,54,58,6,-3,24,19,23),
            (1,8,5,0,-19,-23,28,32,3,-4,11,11,15)],
    "Fall":[(0,1,4,1,-20,-17,27,27,4,-4,6,7,17),
            (.42,-.5,6,0,-15,-25,27,26,4,-4,6,6,20),
            (1,1,4,1,-20,-17,27,27,4,-4,6,7,17)],
    "Land":[(0,6,7,0,-17,-21,26,26,3,-3,9,9,15),
            (.36,-6.5,-22,2,25,20,38,33,2,-2,0,0,11),
            (.67,-4,-10,0,8,2,28,23,1,-1,0,0,5),
            (1,-1.2,0,0,-5,-7,17,14,.65,-.65,0,0,0)],
    "Dodge":[(0,-1.2,0,0,-4,-6,18,16,0,0,0,0,0),
             (.19,-5,-12,-12,24,-20,45,52,3,-4,0,0,6),
             (.47,-2,-24,15,-36,-57,63,81,7,-7,3,6,28),
             (.72,-5,-11,12,-24,5,52,42,-3,5,0,1,18),
             (1,-1.2,0,0,-4,-6,18,16,0,0,0,0,1)],
    "AttackLight":[(0,-1.2,0,0,-5,-5,23,21,0,0,0,0,0),
                   (.27,-2.9,-6,-35,13,32,39,50,-2,4,0,0,8),
                   (.48,-3.4,-14,31,-24,-66,53,21,4,-3,0,0,13),
                   (.66,-2.9,-11,23,-31,-41,48,40,4,-3,0,0,9),
                   (1,-1.2,0,0,-5,-6,21,17,0,0,0,0,0)],
    "AttackHeavy":[(0,-1.2,0,0,-6,-7,23,24,0,0,0,0,0),
                   (.35,-4,9,-45,-28,-128,45,66,-4,3,0,0,8),
                   (.50,-3,14,-41,-42,-136,57,74,-4,3,0,0,13),
                   (.65,-5,-25,34,-48,-55,56,21,4,-4,0,0,31),
                   (.79,-5.5,-22,27,-37,8,46,39,4,-4,0,0,20),
                   (1,-1.2,0,0,-6,-6,19,19,0,0,0,0,1)],
    "HitReaction":[(0,-1.2,0,0,-6,-7,17,17,0,0,0,0,0),
                   (.23,-2.9,22,-13,24,18,34,30,-4,3,0,0,-13),
                   (.48,-4,-14,11,10,-39,53,51,-4,3,0,0,10),
                   (1,-1.2,0,0,-6,-7,17,17,0,0,0,0,0)],
    "Death":[(0,-1.2,0,0,-6,-7,17,17,0,0,0,0,0),
             (.25,-3,18,-9,24,-29,30,38,-3,2,0,0,-12),
             (.47,-10,-30,-14,-18,-34,39,48,3,7,0,0,15),
             (.68,-15,-23,-5,-34,-42,39,38,1,5,0,0,18),
             (.9,-19,1,0,-18,-20,16,20,2,1,0,0,3),
             (1,-19,0,0,-18,-20,16,20,2,1,0,0,3)],
}


def action(name,t):
    right=next((i for i,a in enumerate(ACTIONS[name]) if i and t<=a[0]),len(ACTIONS[name])-1)
    a,b=ACTIONS[name][right-1:right+1]
    hip,lean,yaw,sl,sr,el,er,fl,fr,hl,hr,cape=[lerp(v,w,ease((t-a[0])/(b[0]-a[0])))
                                                for v,w in zip(a[1:],b[1:])]
    p=pose()
    displacement=18*ease((t-.15)/.60) if name=="Dodge" else 0
    offset(p,"Root",x=displacement)
    if name=="Death":
        fall=ease((t-.43)/.51)
        offset(p,"Pelvis",y=lerp(hip,-13.6,fall),z=15*fall)
        rot(p,"Pelvis",-86*fall,-8*fall,6*fall)
    else:
        offset(p,"Pelvis",y=hip)
        rot(p,"Pelvis",lean*.18,yaw*.23)
    rot(p,"Spine",lean*.33,yaw*.29)
    rot(p,"Chest",lean*.51,yaw*.48)
    rot(p,"Head",-lean*.43,-yaw*.32,5*math.sin(t*math.pi) if name in ("Death","HitReaction") else 0)
    rot(p,"CapeTop",cape*.55)
    rot(p,"CapeMiddle",cape*.7)
    rot(p,"CapeHem",cape*.39)
    for s,side in LEFT_RIGHT:
        strike=name.startswith("Attack")
        out=17+(14 if strike else 5)*math.sin(t*math.pi)
        arm(p,s,side,sl if s>0 else sr,out,el if s>0 else er,
            roll=s*(yaw*.21),wrist=8 if strike else 0,grip=.8 if strike else .44)
        if name=="Death":
            fall=ease((t-.43)/.51)
            rot(p,f"Thigh_{side}",lerp(-39,-7,fall),0,s*lerp(8,15,fall))
            rot(p,f"Calf_{side}",lerp(73,8,fall))
            rot(p,f"Foot_{side}",lerp(-33,6,fall))
        else:
            leg(p,s,side,fl if s>0 else fr,hl if s>0 else hr,hip)
    return p


def turn(t,direction):
    p=idle(t)
    angle=direction*90*ease(t)
    rot(p,"Root",0,angle)
    rot(p,"Chest",-3,12*direction*math.sin(t*math.pi))
    rot(p,"Head",0,19*direction*math.sin(t*math.pi))
    rot(p,"CapeMiddle",2,9*direction*math.sin(t*math.pi))
    hip=-1.2-1.6*math.sin(t*math.pi)
    offset(p,"Pelvis",y=hip)
    for s,side in LEFT_RIGHT:
        step=clamp((t-(.11 if s==direction else .48))/.46)
        lift=math.sin(step*math.pi)
        leg(p,s,side,3.7*s*lift,2.9*lift,hip,bend=s*2.8*lift)
        arm(p,s,side,swing=s*8*math.sin(t*math.pi),out=16,elbow=22)
    return p


def make_clips(bones):
    durations={"TPose":1.0,"Idle":2.8,"Walk":1.10,"Run":.73333,
               "TurnLeft":.9,"TurnRight":.9,"Jump":.77,"Fall":.73,"Land":.57,
               "Dodge":.83,"AttackLight":.73,"AttackHeavy":1.13,"HitReaction":.57,"Death":1.80}
    rest={name:p for name,p,_ in bones}
    local={name:tuple(a-b for a,b in zip(p,rest[parent] if parent else (0,0,0)))
           for name,p,parent in bones}
    clips={}
    for clip,duration in durations.items():
        steps=round(duration*FPS)
        frames=[]
        for i in range(steps+1):
            t=i/steps
            if clip=="TPose":
                frame=pose()
            elif clip=="Idle":
                frame=idle(t)
            elif clip in ("Walk","Run"):
                frame=gait(t,clip=="Run")
            elif clip in ("TurnLeft","TurnRight"):
                frame=turn(t,1 if clip=="TurnLeft" else -1)
            else:
                frame=action(clip,t)
            frames.append(frame)
        tracks={}
        for name,_,_ in bones:
            if any(name in r for r,_ in frames) or name=="Root":
                tracks[name,"rotation"]=[r.get(name,(0,0,0,1)) for r,_ in frames]
            if any(name in o for _,o in frames) or name=="Root":
                tracks[name,"translation"]=[tuple((a+b)*.01 for a,b in
                                                  zip(local[name],o.get(name,(0,0,0))))
                                             for _,o in frames]
        clips[clip]={"times":[i/FPS for i in range(steps+1)],"tracks":tracks}
    return clips
