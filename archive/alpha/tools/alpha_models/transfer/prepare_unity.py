"""Prepare an isolated Unity project for the Alpha animation transfer (S05A-2, local only).

Writes into ``<project>/Assets/AlphaTransfer/Data`` (inside ``.scratch`` — never committed):

* ``models/<part>.cubdec`` — de-obfuscated voxel payloads for the chosen models (read-only from
  ``data1.db``, opened with ``mode=ro&immutable=1``);
* ``rig.json``  — part -> model, grid size, anchor rule, weapon subtype;
* ``clips/<name>.json`` — clips converted from ``*.alphaclip.json`` into the flat JsonUtility
  layout the Unity runtime reads (matrices stay in Alpha joint space; Unity converts them).

Usage::

    python -m tools.alpha_models.transfer.prepare_unity --db <data1.db> --project <UnityProject>
        --clips <dir with *.alphaclip.json> --models <models.json>

``models.json`` maps rig parts to ``.cub`` keys, e.g. the S05A-1 humanoid with a two-handed sword::

    {"variant": "s05a1_human_greatsword", "weapon_subtype": 15,
     "parts": {"body": "body1.cub", "head": "human-head-m01.cub", "hair": "human-hair-m01.cub",
               "weapon": "iron-greatsword.cub", "hand_l": "glove.cub", "hand_r": "glove.cub",
               "foot_l": "foot.cub", "foot_r": "foot.cub"}}
"""
from __future__ import annotations

import argparse
import glob
import json
import math
import os
import sqlite3
import sys
from pathlib import Path

from tools.alpha_models.anim import humanoid_rig as rig

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from cub_format import deobfuscate, parse_decoded  # noqa: E402


def exposed_faces(model):
    solid = set()
    for z in range(model.height):
        for y in range(model.depth):
            for x in range(model.width):
                c = model.rgb_at(x, y, z)
                if c and c != (0, 0, 0):
                    solid.add((x, y, z))
    n = 0
    for (x, y, z) in solid:
        for d in ((1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1)):
            if (x + d[0], y + d[1], z + d[2]) not in solid:
                n += 1
    return n


def weapon_handle(model):
    """Current sword: the contiguous narrow base ends at the first wider guard slice.

    Coordinates are Alpha grid coordinates; return actual voxel extents, not a fitted animation.
    Refuse an ambiguous shape instead of blessing a hand anywhere on an infinite blade axis.
    """
    sections=[]
    for z in range(model.height):
        xy=[(x,y) for y in range(model.depth) for x in range(model.width)
            if model.rgb_at(x,y,z) not in (None,(0,0,0))]
        if xy: sections.append((z,(min(x for x,y in xy),max(x for x,y in xy),min(y for x,y in xy),max(y for x,y in xy))))
    if not sections: raise ValueError('WEAPON_HANDLE_EMPTY')
    first,bb=sections[0]
    for z,b in sections[1:]:
        if b[0]<bb[0] or b[1]>bb[1] or b[2]<bb[2] or b[3]>bb[3]:
            if z-first<2 or not first<=4<z: raise ValueError('WEAPON_GRIP_NOT_IN_HANDLE')
            return float(first),float(z)
    raise ValueError('WEAPON_HANDLE_GUARD_UNPROVEN')


def unity_clip(clip, part_order):
    names = [p["name"] for p in clip["parts"]]
    if sorted(names) != sorted(part_order):
        raise ValueError(f"clip {clip['name']} parts {names} != rig {part_order}")
    keys = []
    mode=clip.get('source',{}).get('visibility_mode','STATIC_ALL')
    if mode not in ('STATIC_ALL','EXPLICIT_NATIVE_DRAW'):raise ValueError('CLIP_VISIBILITY_MODE_UNKNOWN')
    dynamic=mode=='EXPLICIT_NATIVE_DRAW'
    previous=-1
    for k in clip["keys"]:
        if not math.isfinite(k['x']) or k['x']<=previous or k['x']<0 or k['x']>clip['duration']:
            raise ValueError('CLIP_TIME_INVALID')
        previous=k['x']
        flat = []
        visible=None
        if dynamic:
            flags=k.get('visible')
            if flags is None or len(flags)!=len(names) or any(v not in (0,1) for v in flags):raise ValueError('CLIP_VISIBILITY_BAD')
            visible=[flags[names.index(n)] for n in part_order]
            if set(k['m'])!={n for n,v in zip(names,flags) if v}:raise ValueError('CLIP_VISIBLE_MATRIX_MISMATCH')
        elif 'visible' in k:raise ValueError('CLIP_UNDECLARED_VISIBILITY')
        for n in part_order:
            if dynamic and not visible[part_order.index(n)]:continue
            m = k["m"][n]
            if len(m) != 16:
                raise ValueError(f"clip {clip['name']}: matrix of {n} has {len(m)} values")
            values=[float(v) for v in m]
            if not all(math.isfinite(v) for v in values):raise ValueError('CLIP_NONFINITE_MATRIX')
            flat.extend(values)
        key={"x":float(k['x']),"m":flat}
        if dynamic:key['visible']=visible
        keys.append(key)
    src = clip.get("source", {})
    by = {p["name"]: p for p in clip["parts"]}
    return {
        "name": clip["name"], "status": clip["status"], "rig": clip["rig"],
        "param": clip["param"], "loop": bool(clip["loop"]), "duration": float(clip["duration"]),
        "phaseRate": float(src.get("phase_rate_rad_per_s", 0.0)),
        "weaponOnBack": bool(src.get("weapon_on_back", False)),
        "visibilityMode":src.get('visibility_mode','STATIC_ALL'),
        "category":src.get('category','MOTION'),
        "nativeIdleAtMs":float(src.get('native_idle_at_ms',0) or 0),
        "gripEndMs":float(src.get('grip_end_ms',0) or 0),
        "actionCode": int(src.get("action", -1)),
        "drawState": int(src.get("state", 0)),
        "actionEndMs": float(src.get("action_end_ms", 0) or 0),
        "recoveryStartMs": float(src.get("recovery_start_ms", 0) or 0),
        "twoHandedAttack": bool(src.get("two_handed_attack", False)),
        "parts": list(part_order),
        "capturedSizes": [int(v) for n in part_order for v in by[n]["captured_size"]],
        "keys": keys,
        "provenance": json.dumps({k: v for k, v in src.items()}, sort_keys=True),
        "validation": json.dumps(clip.get("validation", {}), sort_keys=True),
    }


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--db", required=True)
    ap.add_argument("--project", required=True)
    ap.add_argument("--clips", required=True)
    ap.add_argument("--models", required=True)
    a = ap.parse_args(argv)
    spec = json.load(open(a.models, encoding="utf-8"))
    data_dir = Path(a.project) / "Assets" / "AlphaTransfer" / "Data"
    (data_dir / "models").mkdir(parents=True, exist_ok=True)
    (data_dir / "clips").mkdir(parents=True, exist_ok=True)
    con = sqlite3.connect(f"file:{a.db}?mode=ro&immutable=1", uri=True)
    sizes, parts_out, problems = {}, [], []
    order = [p.name for p in rig.PARTS]
    for part in rig.PARTS:
        key = spec["parts"].get(part.name)
        if not key:
            problems.append(f"MISSING_MODEL {part.name}")
            continue
        row = con.execute("select value from blobs where key=?", (key,)).fetchone()
        if row is None:
            problems.append(f"MODEL_NOT_IN_DB {part.name} {key}")
            continue
        decoded = deobfuscate(row[0])
        model = parse_decoded(decoded)
        size = (model.width, model.depth, model.height)
        sizes[part.name] = size
        (data_dir / "models" / f"{part.name}.cubdec").write_bytes(decoded)
        parts_out.append({"name": part.name, "model": key, "file": f"models/{part.name}.cubdec",
                          "size": list(size), "anchor": part.anchor, "mirrored": part.mirrored,
                          "expectedVertices": exposed_faces(model) * 4})
    problems += rig.check_model_compatibility(sizes)
    rig_json = {"rig": rig.RIG_ID, "variant": spec.get("variant", "custom"),
                "weaponSubtype": int(spec.get("weapon_subtype", -1)), "parts": parts_out}
    if rig_json["weaponSubtype"] not in rig.TWO_HANDED_SUBTYPES:
        problems.append(f"WEAPON_SUBTYPE_NOT_VERIFIED {rig_json['weaponSubtype']}")
    (data_dir / "rig.json").write_text(json.dumps(rig_json, indent=1), encoding="utf-8")
    written = []
    for path in sorted(glob.glob(os.path.join(a.clips, "*.alphaclip.json"))):
        clip = json.load(open(path, encoding="utf-8"))
        if clip.get("rig") != rig.RIG_ID:
            problems.append(f"CLIP_RIG_MISMATCH {path}")
            continue
        uc = unity_clip(clip, order)
        out = data_dir / "clips" / f"{clip['name']}.json"
        out.write_text(json.dumps(uc), encoding="utf-8")
        written.append(str(out))
    result = "PASS" if not problems else "FAIL"
    print(json.dumps({"PREPARE_RESULT": result, "problems": problems, "rig": rig_json,
                      "clips": written}, indent=1))
    return 0 if result == "PASS" else 1


if __name__ == "__main__":
    sys.exit(main())
