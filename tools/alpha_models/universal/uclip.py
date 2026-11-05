"""Universal capture -> clip builder (any creature kind, any verified body plan).

Generalises ``anim/alpha_clip.py`` (which stays the reference path for the humanoid
``alpha_humanoid_v1``) to the universal rig of ``creature_rig.py``:

* a frame is identified by ``kind`` (creature+0x64) and its parts by DRAW SITE; the slot read
  live by ``pose_capture`` (``mesh == self[slot]``) must agree with the block's slot, otherwise
  the frame is rejected (``SLOT_MISMATCH``) — a part can never be silently mislabelled;
* a frame is usable only if its drawn part set equals the STATIC prediction for its appearance
  (``creature_rig.resolve`` on the appearance bytes captured with the frame);
* the clip stores joint frames ``J = T(anchor(size)) * M * P^-1`` exactly like alpha_clip, with
  the rig id = the motion family id, so a clip can only be applied to kinds of the same family.

Status rules (unchanged from S05A-2): ``TRANSFERRED_FROM_ALPHA`` only if every key is a real
captured matrix and the hold-out check passes; otherwise the clip is not written.
"""
from __future__ import annotations

import hashlib
import json
import math
import os
import struct
from dataclasses import dataclass, field

from tools.alpha_models.anim import mat4
from tools.alpha_models.anim.alpha_clip import decompose, interp, pose_error, worst  # noqa: F401
from tools.alpha_models.universal import alpha_static as st
from tools.alpha_models.universal import creature_rig as cr

FORMAT = "cubegame.alpha_clip/2"
TWO_PI = 2.0 * math.pi


@dataclass
class Part:
    name: str
    size: tuple
    vertices: int
    matrix: list


@dataclass
class Frame:
    index: int
    t: float
    kind: int
    creature: str
    state: int
    time_ms: int
    action: int
    amp: float
    phase: float
    boost: bool
    placement: list
    appearance: bytes
    parts: dict = field(default_factory=dict)
    problems: list = field(default_factory=list)


def _boost(h):
    """GameController.cpp:2244-2256: x1.25 walk factor (input/AI acceleration + velocity)."""
    fl124 = struct.unpack_from("<H", h, 0x124)[0]
    b5c = h[0x5C]
    acc = sum(v * v for v in struct.unpack_from("<3f", h, 0x40))
    vel = struct.unpack_from("<f", h, 0x38)[0] ** 2 + struct.unpack_from("<f", h, 0x34)[0] ** 2
    return bool((((fl124 & 1) == 0 or (b5c & 4) == 0) and (fl124 & 0x40)) and acc > 0.1 and vel > 0.1) \
        or h[0x68] == ord("0")


def load_capture(path, kind=None, creature=None):
    frames = []
    with open(path, encoding="utf-8") as fh:
        for i, line in enumerate(fh):
            r = json.loads(line)
            if creature is not None and r.get("creature") != creature:
                continue
            h = bytes.fromhex(r["creature_hdr"])
            this = bytes.fromhex(r["this_hdr"])
            rkind = r.get("kind")
            if rkind is None:   # captures made before kind targeting: creature+0x64 is in the header
                rkind = struct.unpack_from("<I", h, st_kind_offset())[0]
            if kind is not None and rkind != kind:
                continue
            fr = Frame(index=i, t=r["t"], kind=rkind, creature=r["creature"],
                       state=struct.unpack_from("<i", this, 8)[0],
                       time_ms=struct.unpack_from("<i", this, 4)[0],
                       action=r["fields"]["type_68"], amp=r["fields"]["amp_1188"] or 0.0,
                       phase=r["fields"]["phase_118c"] or 0.0, boost=_boost(h),
                       placement=r.get("placement"), appearance=h[0x78:0x78 + st.APPEARANCE_SIZE])
            if not r.get("frame_check", False):
                fr.problems.append("FRAME_CHECK_FAILED")
            if fr.placement is None:
                fr.problems.append("NO_PLACEMENT")
            for p in r["parts"]:
                site = int(p["draw_site"], 16)
                blk = cr.BY_DRAW_SITE.get(site)
                if blk is None:
                    fr.problems.append(f"UNKNOWN_DRAW_SITE {p['draw_site']}")
                    continue
                if blk.status == "UNVERIFIED":
                    fr.problems.append(f"UNVERIFIED_BLOCK {blk.name}")
                    continue
                slot = p.get("slot")
                if slot is not None and slot != f"0x{blk.slot:02X}":
                    fr.problems.append(f"SLOT_MISMATCH {blk.name} {slot} != 0x{blk.slot:02X}")
                    continue
                if blk.name in fr.parts:
                    fr.problems.append(f"DUPLICATE_PART {blk.name}")
                    continue
                hdr = p.get("mesh_hdr")
                if not hdr or not p.get("matrix"):
                    fr.problems.append(f"NO_MATRIX {blk.name}")
                    continue
                hb = bytes.fromhex(hdr)
                fr.parts[blk.name] = Part(blk.name, tuple(struct.unpack_from("<3i", hb, 0x44)),
                                          struct.unpack_from("<i", hb, 0x3C)[0], p["matrix"])
            frames.append(fr)
    return frames


def st_kind_offset():
    from tools.alpha_models.runtime.pose_capture import KIND_OFFSET
    return KIND_OFFSET


def frame_resolution(fr):
    # equipment is not part of the appearance: take it from what the frame actually drew
    equipment = tuple(e for e, blocks in cr.EQUIPMENT_BLOCKS.items() if any(b in fr.parts for b in blocks))
    return cr.resolve(fr.kind, st.decode_appearance(fr.appearance), equipment=equipment)


def usable(fr, resolution):
    return not fr.problems and sorted(fr.parts) == sorted(resolution.parts)


def joint(fr, name, on_back=True, subtype=None):
    blk = cr.BY_NAME[name]
    ps = fr.parts[name]
    a = cr.anchor(blk.anchor, ps.size, kind=fr.kind, on_back=on_back, subtype=subtype)
    return mat4.mul(mat4.mul(mat4.translate(*a), ps.matrix), mat4.inverse_affine(fr.placement))


def joints(fr):
    return {n: joint(fr, n) for n in fr.parts}


def roundtrip(frames, resolution):
    worst_abs = 0.0
    for fr in frames:
        if not usable(fr, resolution):
            continue
        for name, ps in fr.parts.items():
            blk = cr.BY_NAME[name]
            a = cr.anchor(blk.anchor, ps.size, kind=fr.kind, on_back=True)
            back = mat4.mul(mat4.mul(mat4.translate(-a[0], -a[1], -a[2]), joint(fr, name)), fr.placement)
            worst_abs = max(worst_abs, mat4.max_abs_diff(back, ps.matrix))
    return worst_abs


def _sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as fh:
        h.update(fh.read())
    return h.hexdigest()


def _header(name, param, loop, frames, resolution, capture_path, extra):
    sizes = {}
    for fr in frames:
        for n, ps in fr.parts.items():
            sizes.setdefault(n, set()).add(ps.size)
    multi = {n: sorted(v) for n, v in sizes.items() if len(v) > 1}
    if multi:
        raise ValueError(f"part sizes change inside the clip: {multi}")
    return {
        "format": FORMAT,
        "rig": resolution.family_id,
        "rig_kind_source": f"0x{resolution.kind:02x}",
        "name": name,
        "status": "TRANSFERRED_FROM_ALPHA",
        "param": param,
        "loop": loop,
        "axes": "alpha_row_vector: X right, Y forward, Z up; 1 unit = 1 model voxel",
        "parts": [{"name": n, "captured_size": list(sorted(sizes[n])[0]), "anchor": cr.BY_NAME[n].anchor,
                   "mirrored": cr.BY_NAME[n].mirrored, "draw_site": f"0x{cr.BY_NAME[n].draw_site:06X}",
                   "slot": f"0x{cr.BY_NAME[n].slot:02X}"} for n in resolution.parts],
        "source": {"capture": os.path.basename(capture_path), "capture_sha256": _sha256(capture_path),
                   "kind": f"0x{resolution.kind:02x}", "creature": frames[0].creature,
                   "frames_used": len(frames), "weapon_on_back": True, **extra},
    }


# --------------------------------------------------------------------------- walk
def steady_walk(frames, resolution, amp_ratio=0.97):
    walk = [f for f in frames if f.state == 0 and f.action == 0 and usable(f, resolution)]
    if not walk:
        raise ValueError("no walking frames")
    amp_max = max(f.amp for f in walk)
    if amp_max < 0.05:
        raise ValueError("creature never walks in this capture")
    boost = sorted({f.boost for f in walk if f.amp >= amp_ratio * amp_max})
    # the pose is a function of phase only for constant amplitude AND constant boost factor
    steady = [f for f in walk if f.amp >= amp_ratio * amp_max and f.boost == boost[-1]]
    return steady, amp_max, boost[-1]


def build_walk_clip(frames, resolution, capture_path, name="walk", amp_ratio=0.97):
    steady, amp_max, boost = steady_walk(frames, resolution, amp_ratio)
    if len(steady) < 8:
        raise ValueError("not enough steady walking frames")
    phi0 = steady[0].phase
    if steady[-1].phase < phi0 + TWO_PI:
        raise ValueError("capture does not contain one full steady walk cycle")
    keys = sorted(({"x": (f.phase - phi0) % TWO_PI, "m": joints(f), "_cycle": int((f.phase - phi0) // TWO_PI),
                    "_frame": f.index} for f in steady), key=lambda k: (k["x"], k["_frame"]))
    # phase speed WHILE steady (consecutive steady frames only; the creature may slow down or
    # stop between steady stretches, which must not dilute the playback rate)
    dphi = dt = 0.0
    for a, b in zip(steady, steady[1:]):
        if b.index - a.index <= 3 and 0 < b.t - a.t < 0.1:
            dphi += b.phase - a.phase
            dt += b.t - a.t
    if dt <= 0:
        raise ValueError("no consecutive steady frames to measure the phase rate")
    rate = dphi / dt
    clip = _header(name, "walk_phase", True, steady, resolution, capture_path, {
        "drawmodel_state": 0, "action_creature_68": 0, "phase_origin": phi0,
        "amplitude_creature_1188": sum(f.amp for f in steady) / len(steady), "amplitude_max": amp_max,
        "walk_boost_x1_25": boost, "phase_rate_rad_per_s": rate, "cycle_seconds": TWO_PI / rate,
        "cycles": len({k["_cycle"] for k in keys}),
    })
    clip["duration"] = TWO_PI
    out, last = [], None
    for k in keys:
        if last is not None and k["x"] - last < 1e-4:
            continue
        out.append({"x": k["x"], "m": k["m"]})
        last = k["x"]
    clip["keys"] = out
    clip["_keys_full"] = keys
    return clip


def _sample_keys(keys, x, period):
    xs = [k["x"] for k in keys]
    import bisect
    i = bisect.bisect_right(xs, x)
    a = keys[i - 1] if i > 0 else keys[-1]
    b = keys[i] if i < len(keys) else keys[0]
    xa = a["x"] if i > 0 else a["x"] - period
    xb = b["x"] if i < len(keys) else b["x"] + period
    u = 0.0 if xb - xa < 1e-12 else (x - xa) / (xb - xa)
    return {n: interp(a["m"][n], b["m"][n], u) for n in a["m"]}


def validate_walk_holdout(clip):
    """Leave-one-cycle-out: every frame of cycle c is predicted from the keys of other cycles."""
    full = clip["_keys_full"]
    cycles = sorted({k["_cycle"] for k in full})
    errs = []
    for c in cycles:
        rest = [k for k in full if k["_cycle"] != c]
        if len(rest) < 8:
            continue
        for k in full:
            if k["_cycle"] != c:
                continue
            pred = _sample_keys(rest, k["x"], TWO_PI)
            errs.extend(pose_error(pred[n], k["m"][n]) for n in k["m"])
    res = worst(errs)
    res["p95_rotation_deg"] = sorted(e[1] for e in errs)[int(0.95 * (len(errs) - 1))] if errs else None
    res["cycles"] = len(cycles)
    return res


def validate_against(clip, frames, resolution):
    """Independent frames (other capture/creature of the same family) vs. the clip."""
    if clip["param"] != "walk_phase":
        raise ValueError("only walk clips")
    phi0 = clip["source"]["phase_origin"]
    steady, _, _ = steady_walk(frames, resolution)
    # phase origins differ between creatures: align by the best global phase shift (one number)
    best = None
    for shift_i in range(0, 72):
        shift = TWO_PI * shift_i / 72
        errs = []
        for f in steady[:: max(1, len(steady) // 60)]:
            pred = _sample_keys(clip["keys"], (f.phase - phi0 + shift) % TWO_PI, TWO_PI)
            jf = joints(f)
            errs.extend(pose_error(pred[n], jf[n]) for n in jf)
        w = worst(errs)
        if best is None or w["max_rotation_deg"] < best[1]["max_rotation_deg"]:
            best = (shift, w)
    return {"phase_shift_rad": best[0], **best[1], "frames": len(steady)}


def write_clip(clip, out_dir):
    os.makedirs(out_dir, exist_ok=True)
    data = {k: v for k, v in clip.items() if not k.startswith("_")}
    path = os.path.join(out_dir, f"{clip['name']}.alphaclip.json")
    with open(path, "w", encoding="utf-8") as fh:
        json.dump(data, fh, indent=1)
    return path
