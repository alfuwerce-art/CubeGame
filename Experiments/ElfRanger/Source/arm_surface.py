"""Faceted arm surfaces joined to the existing shoulder and wrist boundaries."""
from __future__ import annotations

import math


SHOULDER_CUT = 16.5
WRIST_CUT = 38.0

# X, center Y/Z, and half widths Y/Z in centimeters. Broad facets connect
# these sections. Extra sections around the elbow carry the existing weights.
ARM_SECTIONS = (
    (18.55,51.10,.04,3.05,3.03),
    (20.35,51.12,.10,3.02,3.03),
    (22.10,51.12,.17,3.03,2.96),
    (25.00,51.02,-.04,2.22,2.24),
    (26.85,50.98,-.03,2.05,2.18),
    (28.05,50.95,.12,2.12,2.22),
    (30.00,51.07,.16,2.36,2.59),
    (31.75,51.06,.16,2.20,2.63),
    (34.30,51.12,.08,1.66,2.43),
    (36.85,51.23,.02,1.03,2.40),
)


def voxel_rim(cut_x, tiles, step):
    # Follow the directed boundary of the occupied YZ tiles. Keeping each
    # lattice vertex allows the loft to share the original mesh's edges.
    edges = {}
    for y,z in tiles:
        for neighbor,first,last in (
            ((y,z-1),(y,z),(y+1,z)), ((y+1,z),(y+1,z),(y+1,z+1)),
            ((y,z+1),(y+1,z+1),(y,z+1)), ((y-1,z),(y,z+1),(y,z)),
        ):
            if neighbor not in tiles:
                if first in edges:
                    raise ValueError("Limb cut has a self-touching boundary")
                edges[first]=last
    start = min(edges)
    rim = [start]
    point = edges.pop(start)
    while point!=start:
        rim.append(point)
        point = edges.pop(point)
    if edges:
        raise ValueError("Limb cut contains more than one boundary")
    return tuple((cut_x,y*step,z*step) for y,z in rim)


def octagon(section):
    x,y,z,hy,hz = section
    c = .37*min(hy,hz)
    ring = ((-hy+c,-hz),(hy-c,-hz),(hy,-hz+c),(hy,hz-c),
            (hy-c,hz),(-hy+c,hz),(-hy,hz-c),(-hy,-hz+c))
    return tuple((x,y+a,z+b) for a,b in ring)


def rim_progress(rim):
    y_min,y_max = min(p[1] for p in rim),max(p[1] for p in rim)
    z_min,z_max = min(p[2] for p in rim),max(p[2] for p in rim)
    y_mid,z_mid = (y_min+y_max)/2,(z_min+z_max)/2
    start = min(range(len(rim)),key=lambda i:math.atan2(rim[i][2]-z_mid,rim[i][1]-y_mid)%math.tau)
    rim = rim[start:]+rim[:start]
    closed = rim+(rim[0],)
    perimeter = [0.]
    for a,b in zip(closed,closed[1:]):
        perimeter.append(perimeter[-1]+math.dist(a,b))
    return closed,[v/perimeter[-1] for v in perimeter]


def connect_boundary(near,far):
    near,s = rim_progress(near)
    far,t = rim_progress(far)
    a=b=0
    while a<len(near)-1 or b<len(far)-1:
        if b==len(far)-1 or (a<len(near)-1 and s[a+1]<=t[b+1]):
            yield near[a],near[a+1],far[b]
            a+=1
        else:
            yield near[a],far[b+1],far[b]
            b+=1


def loft_between_rims(start, sections, end):
    yield from connect_boundary(start,sections[0])
    for near,far in zip(sections,sections[1:]):
        for i in range(len(near)):
            j = (i+1)%len(near)
            yield near[i],near[j],far[j],far[i]
    yield from connect_boundary(sections[-1],end)


def replace_voxel_arms(skin,step):
    start,end = round(SHOULDER_CUT/step),round(WRIST_CUT/step)
    sections = tuple(octagon(section) for section in ARM_SECTIONS)
    faces = []
    for side,shoulder_x,wrist_x in ((1,start-1,end),(-1,-start,-end-1)):
        cuts = ((shoulder_x,SHOULDER_CUT,side),(wrist_x,WRIST_CUT,-side))
        rims = []
        for x,u,n in cuts:
            tiles = {(y,z) for a,y,z in skin.cells if a==x}
            skin.open_faces.update(((x,y,z),(n,0,0)) for y,z in tiles)
            rims.append(voxel_rim(u,tiles,step))
        for face in loft_between_rims(rims[0],sections,rims[1]):
            ordered = face if side==1 else tuple(reversed(face))
            faces.append(tuple((side*x,y,z) for x,y,z in ordered))
    skin.cells = {p:tone for p,tone in skin.cells.items()
                  if not start<=(p[0] if p[0]>=0 else -p[0]-1)<end}
    return faces
