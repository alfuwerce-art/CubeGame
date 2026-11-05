"""Turn a ``pose_capture`` recording into model-independent Alpha clips, and check them.

Pipeline (S05A-2, D57/D58, research prototype — clips stay in ``.scratch``, never in Git)::

    capture.jsonl  --(load_capture)-->  frames  --(build_*_clip)-->  clip.json  --> Unity

* a *frame* is one ``Creature_drawModel`` call for the player: state/time/action/walk fields
  plus, per rig part, the matrix Alpha uploaded and the drawn mesh size;
* a *clip* stores per key and per part the JOINT frame ``J = T(anchor(size)) * M * P^-1``
  (row-vector, Alpha axes: X right, Y forward, Z up, unit = 1 voxel of the model grid);
* ``status`` is ``TRANSFERRED_FROM_ALPHA`` only when every key is a real captured matrix
  (no fitting, no procedural fill). Interpolation happens only at playback time.

CLI::

    python -m tools.alpha_models.anim.alpha_clip summary  CAPTURE.jsonl
    python -m tools.alpha_models.anim.alpha_clip build    CAPTURE.jsonl --out-dir DIR [--walk]
            [--state 2 --action 57 --name attack_2h_a57] [--idle]
    python -m tools.alpha_models.anim.alpha_clip compare  CAPTURE_A.jsonl CAPTURE_B.jsonl --state 2
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
import os
import struct
import sys
from dataclasses import dataclass, field

from tools.alpha_models.anim import mat4
from tools.alpha_models.anim import humanoid_rig as rig

FORMAT = "cubegame.alpha_clip/1"
TWO_PI = 2.0 * math.pi


# --------------------------------------------------------------------------- capture loading
@dataclass
class PartSample:
    name: str
    size: tuple
    vertices: int
    matrix: list          # Alpha uploaded matrix (world/camera-relative, row-vector)


@dataclass
class Frame:
    index: int
    t: float              # host perf-counter seconds
    state: int            # drawModel this[2]
    time_ms: int          # drawModel this[1] (game ms since the action started)
    action: int           # creature+0x68
    amp: float            # creature+0x1188 (walk amplitude)
    phase: float          # creature+0x118c (walk phase, radians, unwrapped)
    placement: list       # local_14e0: creature placement matrix
    weapon_scale: float   # creature+0xb0 (local_1694, weapon scale)
    accel2: float = 0.0   # |creature+0x40..+0x48|^2 (movement input); >0.1 -> x1.25 "run" factor
    pitch: float = 0.0    # creature+0x58, look pitch in degrees (head/hair/weapon aim layer)
    parts: dict = field(default_factory=dict)
    problems: list = field(default_factory=list)


def _u(fmt, hexstr, off):
    raw = bytes.fromhex(hexstr)
    return struct.unpack_from(fmt, raw, off)[0]


def load_capture(path):
    frames = []
    with open(path, encoding="utf-8") as fh:
        for i, line in enumerate(fh):
            r = json.loads(line)
            this_hdr = r["this_hdr"]
            fr = Frame(
                index=i, t=r["t"],
                state=_u("<i", this_hdr, 8), time_ms=_u("<i", this_hdr, 4),
                action=r["fields"]["type_68"], amp=r["fields"]["amp_1188"] or 0.0,
                phase=r["fields"]["phase_118c"] or 0.0,
                placement=r.get("placement"),
                weapon_scale=_u("<f", r["creature_hdr"], 0xB0) if r.get("creature_hdr") else None,
            )
            if r.get("creature_hdr"):
                acc = struct.unpack_from("<3f", bytes.fromhex(r["creature_hdr"]), 0x40)
                fr.accel2 = sum(v * v for v in acc)
                fr.pitch = struct.unpack_from("<f", bytes.fromhex(r["creature_hdr"]), 0x58)[0]
            if not r.get("frame_check", False):
                fr.problems.append("FRAME_CHECK_FAILED")
            if fr.placement is None:
                fr.problems.append("NO_PLACEMENT")
            for p in r["parts"]:
                site = int(p["draw_site"], 16)
                part = rig.BY_DRAW_SITE.get(site)
                if part is None:
                    fr.problems.append(f"UNMAPPED_SITE {p['draw_site']}")
                    continue
                if part.name in fr.parts:
                    fr.problems.append(f"DUPLICATE_PART {part.name}")
                    continue
                hdr = p.get("mesh_hdr")
                if not hdr or not p.get("matrix"):
                    fr.problems.append(f"NO_MATRIX {part.name}")
                    continue
                size = struct.unpack_from("<3i", bytes.fromhex(hdr), 0x44)
                verts = struct.unpack_from("<i", bytes.fromhex(hdr), 0x3C)[0]
                fr.parts[part.name] = PartSample(part.name, tuple(size), verts, p["matrix"])
            frames.append(fr)
    return frames


# --------------------------------------------------------------------------- pose maths
def joint_frame(part_name, sample, placement, on_back, subtype=None):
    part = rig.BY_NAME[part_name]
    a = rig.anchor_for(part, sample.size, on_back=on_back, subtype=subtype)
    return mat4.mul(mat4.mul(mat4.translate(*a), sample.matrix), mat4.inverse_affine(placement))


def decompose(m):
    """Row-vector affine -> (t, q(w,x,y,z), s) with a negative X scale for mirrored frames."""
    rows = [m[0:3], m[4:7], m[8:11]]
    s = [math.sqrt(sum(v * v for v in r)) for r in rows]
    if mat4.det3(m) < 0:
        s[0] = -s[0]
    r = [[rows[i][j] / s[i] for j in range(3)] for i in range(3)]
    # r is the rotation in row-vector form (rows = rotated basis); column form = transpose
    c = [[r[j][i] for j in range(3)] for i in range(3)]
    tr = c[0][0] + c[1][1] + c[2][2]
    if tr > 0:
        k = math.sqrt(tr + 1.0) * 2
        q = (0.25 * k, (c[2][1] - c[1][2]) / k, (c[0][2] - c[2][0]) / k, (c[1][0] - c[0][1]) / k)
    elif c[0][0] > c[1][1] and c[0][0] > c[2][2]:
        k = math.sqrt(1.0 + c[0][0] - c[1][1] - c[2][2]) * 2
        q = ((c[2][1] - c[1][2]) / k, 0.25 * k, (c[0][1] + c[1][0]) / k, (c[0][2] + c[2][0]) / k)
    elif c[1][1] > c[2][2]:
        k = math.sqrt(1.0 + c[1][1] - c[0][0] - c[2][2]) * 2
        q = ((c[0][2] - c[2][0]) / k, (c[0][1] + c[1][0]) / k, 0.25 * k, (c[1][2] + c[2][1]) / k)
    else:
        k = math.sqrt(1.0 + c[2][2] - c[0][0] - c[1][1]) * 2
        q = ((c[1][0] - c[0][1]) / k, (c[0][2] + c[2][0]) / k, (c[1][2] + c[2][1]) / k, 0.25 * k)
    n = math.sqrt(sum(v * v for v in q))
    return list(m[12:15]), [v / n for v in q], s


def compose(t, q, s):
    w, x, y, z = q
    c = [[1 - 2 * (y * y + z * z), 2 * (x * y - w * z), 2 * (x * z + w * y)],
         [2 * (x * y + w * z), 1 - 2 * (x * x + z * z), 2 * (y * z - w * x)],
         [2 * (x * z - w * y), 2 * (y * z + w * x), 1 - 2 * (x * x + y * y)]]
    r = [[c[j][i] for j in range(3)] for i in range(3)]  # back to row form
    out = []
    for i in range(3):
        out += [r[i][0] * s[i], r[i][1] * s[i], r[i][2] * s[i], 0.0]
    return out + [t[0], t[1], t[2], 1.0]


def slerp(a, b, u):
    d = sum(x * y for x, y in zip(a, b))
    if d < 0:
        b = [-v for v in b]
        d = -d
    if d > 0.9995:
        r = [x + (y - x) * u for x, y in zip(a, b)]
    else:
        th = math.acos(min(1.0, d))
        sa, sb = math.sin((1 - u) * th) / math.sin(th), math.sin(u * th) / math.sin(th)
        r = [x * sa + y * sb for x, y in zip(a, b)]
    n = math.sqrt(sum(v * v for v in r))
    return [v / n for v in r]


def interp(ma, mb, u):
    ta, qa, sa = decompose(ma)
    tb, qb, sb = decompose(mb)
    return compose([x + (y - x) * u for x, y in zip(ta, tb)], slerp(qa, qb, u),
                   [x + (y - x) * u for x, y in zip(sa, sb)])


def pose_error(ma, mb):
    """(translation error in voxels, rotation error in degrees, scale error)."""
    ta, qa, sa = decompose(ma)
    tb, qb, sb = decompose(mb)
    dt = math.sqrt(sum((x - y) ** 2 for x, y in zip(ta, tb)))
    d = abs(sum(x * y for x, y in zip(qa, qb)))
    dr = math.degrees(2 * math.acos(min(1.0, d)))
    ds = max(abs(x - y) for x, y in zip(sa, sb))
    return dt, dr, ds


# --------------------------------------------------------------------------- look-pitch layer
# creature+0x58 (degrees) is the look pitch. Alpha adds it as
#   head/hair: mat4_rotateX(pitch - ch + x) right before translate(0,0,3) * scale  (6253, 6299)
#   weapon in hand: mat4_rotateAxisAngle(pitch*0.5 + ch, X) right before scale (6607, 6785)
# so in the joint frame it is a pure X rotation about the neck (3*scale below the joint) or about
# the grip (the joint itself). Clips are stored at pitch 0: J0 = T(0,0,p)*Rx(-k*pitch)*T(0,0,-p)*J.
# Verified on two captures with pitch 0 and -3.78 deg: residual 0.000 deg head, 0.02 deg weapon.
PITCH_LAYER = {"head": (1.0, 3.0), "hair": (1.0, 3.0), "weapon": (0.5, 0.0)}


def _rx(deg):
    c, s = math.cos(math.radians(deg)), math.sin(math.radians(deg))
    return [1.0, 0, 0, 0, 0, c, s, 0, 0, -s, c, 0, 0, 0, 0, 1.0]


def pitch_layer(name, j, pitch_deg, on_back):
    """Return J with the look-pitch rotation of ``pitch_deg`` added (negative value removes)."""
    if name not in PITCH_LAYER or pitch_deg == 0.0 or (name == "weapon" and on_back):
        return j
    k, pivot = PITCH_LAYER[name]
    s = decompose(j)[2][0]
    corr = mat4.mul(mat4.mul(mat4.translate(0, 0, pivot * abs(s)), _rx(k * pitch_deg)),
                    mat4.translate(0, 0, -pivot * abs(s)))
    return mat4.mul(corr, j)


# --------------------------------------------------------------------------- clip building
def _frame_joints(fr, on_back, subtype=None, normalize_pitch=True):
    out = {}
    for name, ps in fr.parts.items():
        j = joint_frame(name, ps, fr.placement, on_back, subtype)
        out[name] = pitch_layer(name, j, -fr.pitch, on_back) if normalize_pitch else j
    return out


def _usable(fr):
    return not fr.problems and len(fr.parts) == len(rig.PARTS)


def _sizes(frames):
    sizes = {}
    for fr in frames:
        for name, ps in fr.parts.items():
            sizes.setdefault(name, set()).add(ps.size)
    return {k: sorted(v) for k, v in sizes.items()}


def _clip_header(name, param, loop, frames, capture_path, extra):
    first = frames[0]
    sizes = _sizes(frames)
    return {
        "format": FORMAT,
        "rig": rig.RIG_ID,
        "name": name,
        "status": "TRANSFERRED_FROM_ALPHA",
        "param": param,
        "loop": loop,
        "axes": "alpha_row_vector: X right, Y forward, Z up; 1 unit = 1 model voxel",
        "parts": [{"name": p.name, "captured_size": list(sizes[p.name][0]), "anchor": p.anchor,
                   "mirrored": p.mirrored, "alpha_slot": p.alpha_slot,
                   "draw_site": f"0x{p.draw_site:06X}"} for p in rig.PARTS],
        "source": {
            "capture": os.path.basename(capture_path),
            "capture_sha256": _sha256(capture_path),
            "frames_used": len(frames),
            "weapon_scale_creature_b0": first.weapon_scale,
            "look_pitch_deg_range": [min(f.pitch for f in frames), max(f.pitch for f in frames)],
            "look_pitch_normalized_to": 0.0,
            **extra,
        },
    }


def _sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as fh:
        h.update(fh.read())
    return h.hexdigest()


def build_time_clip(frames, capture_path, state, action, name, on_back=False, subtype=None,
                    still_eps=(0.005, 0.05)):
    """Clip over game time of ONE uninterrupted run of ``state``/``action`` (the first run).

    The run is cut where the motion stops (Alpha keeps holding the last pose while the action
    stays active); ``hold_from_ms`` records that point.
    """
    run, started = [], False
    for fr in frames:
        if fr.state == state and fr.action == action and _usable(fr):
            if started and run and fr.time_ms < run[-1].time_ms:
                break  # a new occurrence started
            run.append(fr)
            started = True
        elif started:
            break
    if len(run) < 3:
        raise ValueError(f"state {state}/action {action}: only {len(run)} usable frames")
    joints = [_frame_joints(fr, on_back, subtype) for fr in run]
    last_move = 0
    for i in range(1, len(run)):
        moved = any(
            (lambda e: e[0] > still_eps[0] or e[1] > still_eps[1])(pose_error(joints[i][n], joints[i - 1][n]))
            for n in joints[i])
        if moved:
            last_move = i
    end = min(len(run) - 1, last_move + 1)
    keys = [{"x": float(run[i].time_ms), "m": joints[i]} for i in range(end + 1)]
    clip = _clip_header(name, "time_ms", False, run[:end + 1], capture_path, {
        "drawmodel_state": state, "action_creature_68": action,
        "first_frame_time_ms": run[0].time_ms, "hold_from_ms": run[end].time_ms,
        "frames_in_run": len(run), "weapon_on_back": on_back,
    })
    clip["duration"] = keys[-1]["x"]
    clip["keys"] = keys
    return clip


def _steady_walk(frames, amp_ratio):
    walk = [fr for fr in frames if fr.state == 0 and fr.action == 0 and _usable(fr)]
    if not walk:
        raise ValueError("no walking frames")
    amp_max = max(fr.amp for fr in walk)
    # GameController.cpp:2246-2256: with movement input (|accel|^2 > 0.1, |vel|^2 > 0.1) the walk
    # value is multiplied by 1.25. Steady walking = full amplitude AND input held.
    steady = [fr for fr in walk if fr.amp >= amp_ratio * amp_max and fr.accel2 > 0.1]
    return steady, amp_max


def _walk_keys(steady, phi0):
    keys = sorted(((fr.phase - phi0) % TWO_PI, fr) for fr in steady)
    out, last = [], None
    for x, fr in keys:
        if last is not None and x - last < 1e-4:
            continue
        out.append({"x": x, "m": _frame_joints(fr, on_back=True), "_cycle": int((fr.phase - phi0) // TWO_PI),
                    "_frame": fr.index})
        last = x
    return out


def build_walk_clip(frames, capture_path, name="walk", amp_ratio=0.97):
    """Walk cycle over the walk phase. Keys are REAL captured frames from all steady cycles,
    ordered by phase (the pose is a function of phase while amplitude and input are steady)."""
    steady, amp_max = _steady_walk(frames, amp_ratio)
    if len(steady) < 8:
        raise ValueError("not enough steady walking frames")
    phi0 = steady[0].phase
    if steady[-1].phase < phi0 + TWO_PI:
        raise ValueError("capture does not contain one full steady walk cycle")
    keys = _walk_keys(steady, phi0)
    rate = (steady[-1].phase - steady[0].phase) / (steady[-1].t - steady[0].t)
    clip = _clip_header(name, "walk_phase", True, steady, capture_path, {
        "drawmodel_state": 0, "action_creature_68": 0, "phase_origin": phi0,
        "amplitude_creature_1188": sum(fr.amp for fr in steady) / len(steady),
        "amplitude_max": amp_max, "phase_rate_rad_per_s": rate,
        "cycle_seconds": TWO_PI / rate, "weapon_on_back": True,
        "cycles": len({k["_cycle"] for k in keys}),
        "alpha_formula": "local_16fc = amp * 2.1991148 * sin(phase + pi/2), x1.25 while input held "
                         "(GameController.cpp:2244-2256)",
    })
    clip["duration"] = TWO_PI
    clip["keys"] = [{"x": k["x"], "m": k["m"]} for k in keys]
    clip["_keys_full"] = keys
    clip["_steady_frames"] = steady
    return clip


def build_idle_clip(frames, capture_path, name="idle"):
    idle = [fr for fr in frames if fr.state == 0 and fr.action == 0 and abs(fr.amp) < 1e-6 and _usable(fr)]
    if not idle:
        raise ValueError("no idle frames")
    fr = idle[0]
    clip = _clip_header(name, "time_ms", True, [fr], capture_path,
                        {"drawmodel_state": 0, "action_creature_68": 0, "weapon_on_back": True,
                         "idle_frames_seen": len(idle)})
    clip["duration"] = 0.0
    clip["keys"] = [{"x": 0.0, "m": _frame_joints(fr, on_back=True)}]
    return clip


# --------------------------------------------------------------------------- sampling / checks
def sample(clip, x):
    keys = clip["keys"]
    if len(keys) == 1:
        return keys[0]["m"]
    if clip["loop"]:
        x = x % clip["duration"]
    if x <= keys[0]["x"]:
        if not clip["loop"]:
            return keys[0]["m"]
        a, b = keys[-1], keys[0]
        span = keys[0]["x"] + clip["duration"] - keys[-1]["x"]
        u = (x + clip["duration"] - keys[-1]["x"]) / span
        return {n: interp(a["m"][n], b["m"][n], u) for n in a["m"]}
    for i in range(1, len(keys)):
        if x <= keys[i]["x"]:
            a, b = keys[i - 1], keys[i]
            u = (x - a["x"]) / (b["x"] - a["x"]) if b["x"] > a["x"] else 0.0
            return {n: interp(a["m"][n], b["m"][n], u) for n in a["m"]}
    if clip["loop"]:
        a, b = keys[-1], keys[0]
        span = keys[0]["x"] + clip["duration"] - keys[-1]["x"]
        u = (x - keys[-1]["x"]) / span
        return {n: interp(a["m"][n], b["m"][n], u) for n in a["m"]}
    return keys[-1]["m"]


def worst(errors):
    if not errors:
        return {"n": 0}
    return {"n": len(errors), "max_translation_vox": max(e[0] for e in errors),
            "max_rotation_deg": max(e[1] for e in errors), "max_scale": max(e[2] for e in errors),
            "mean_translation_vox": sum(e[0] for e in errors) / len(errors)}


def validate_walk(clip, steady):
    """Leave-one-cycle-out: keys from the other cycles must predict every frame of the held-out
    cycle (proves the pose is a repeatable function of the phase, not a one-off)."""
    full = clip["_keys_full"]
    cycles = sorted({k["_cycle"] for k in full})
    errs = []
    for c in cycles:
        train = [{"x": k["x"], "m": k["m"]} for k in full if k["_cycle"] != c]
        if len(train) < 8:
            continue
        sub = dict(clip, keys=train)
        for k in full:
            if k["_cycle"] != c:
                continue
            pred = sample(sub, k["x"])
            errs += [pose_error(pred[n], k["m"][n]) for n in k["m"]]
    return {"cycles": len(cycles), **worst(errs),
            "p95_rotation_deg": sorted(e[1] for e in errs)[int(0.95 * (len(errs) - 1))] if errs else None}


def validate_time(clip, frames, on_back=False, skip_first=True):
    """Every other occurrence of the clip's state/action vs the clip at the same game time.

    ``skip_first=False`` checks ALL runs (use it for a capture other than the clip's own)."""
    st, act = clip["source"]["drawmodel_state"], clip["source"]["action_creature_68"]
    first_t = clip["source"]["first_frame_time_ms"]
    errs, runs, cur = [], [], []
    for fr in frames:
        if fr.state == st and fr.action == act and _usable(fr):
            if cur and fr.time_ms < cur[-1].time_ms:
                runs.append(cur)
                cur = []
            cur.append(fr)
        elif cur:
            runs.append(cur)
            cur = []
    if cur:
        runs.append(cur)
    for run in (runs[1:] if skip_first else runs):
        for fr in run:
            if fr.time_ms > clip["duration"]:
                continue
            pred = sample(clip, fr.time_ms)
            real = _frame_joints(fr, on_back=on_back)
            errs += [pose_error(pred[n], real[n]) for n in real]
    # same-time pairs: frames of two different runs with the SAME game time. The attack target
    # pose is a function of time, but Alpha smooths the drawn pose toward it every frame
    # (local_1730 = 0.01 / 0.04, case 2 at GameController.cpp:2639), so a residual that depends
    # on the frame timing and on the pose before the attack (first ~50 ms) is expected.
    pair_errs, pair_errs_blend = [], []
    by_time = [{f.time_ms: f for f in r} for r in runs]
    for i in range(len(runs)):
        for j in range(i + 1, len(runs)):
            for t, f in by_time[i].items():
                g = by_time[j].get(t)
                if g is None or t > clip["duration"]:
                    continue
                ja, jb = _frame_joints(f, on_back), _frame_joints(g, on_back)
                e = [pose_error(ja[n], jb[n]) for n in ja]
                (pair_errs_blend if t < 50 else pair_errs).extend(e)
    checked = max(0, len(runs) - 1) if skip_first else len(runs)
    return {"other_runs": checked, "interpolated": {**worst(errs),
            "p95_rotation_deg": sorted(e[1] for e in errs)[int(0.95 * (len(errs) - 1))] if errs else None},
            "same_time_pairs": worst(pair_errs), "same_time_pairs_blend_in_lt50ms": worst(pair_errs_blend)}


def reconstruct_check(frames, on_back):
    """J round trip: T(-anchor) * J * P must equal the captured matrix (sanity of the maths)."""
    worst_abs = 0.0
    for fr in frames:
        if not _usable(fr):
            continue
        for name, ps in fr.parts.items():
            part = rig.BY_NAME[name]
            a = rig.anchor_for(part, ps.size, on_back=on_back)
            j = joint_frame(name, ps, fr.placement, on_back)
            back = mat4.mul(mat4.mul(mat4.translate(-a[0], -a[1], -a[2]), j), fr.placement)
            worst_abs = max(worst_abs, mat4.max_abs_diff(back, ps.matrix))
    return worst_abs


def write_clip(clip, out_dir):
    os.makedirs(out_dir, exist_ok=True)
    data = {k: v for k, v in clip.items() if not k.startswith("_")}
    path = os.path.join(out_dir, f"{clip['name']}.alphaclip.json")
    with open(path, "w", encoding="utf-8") as fh:
        json.dump(data, fh, indent=1)
    return path


def summary(frames):
    from collections import Counter
    out = {"frames": len(frames), "usable": sum(_usable(f) for f in frames),
           "problems": Counter(p for f in frames for p in f.problems).most_common(10),
           "runs": []}
    prev = None
    for f in frames:
        key = (f.state, f.action, f.amp > 0.05)
        if key != prev:
            out["runs"].append({"from_frame": f.index, "state": f.state, "action": f.action,
                                "walking": f.amp > 0.05, "time_ms": f.time_ms})
            prev = key
    out["sizes"] = {k: [list(s) for s in v] for k, v in _sizes(frames).items()}
    return out


def main(argv=None):
    ap = argparse.ArgumentParser(description="Alpha capture -> clip tool")
    sub = ap.add_subparsers(dest="cmd", required=True)
    s = sub.add_parser("summary")
    s.add_argument("capture")
    b = sub.add_parser("build")
    b.add_argument("capture")
    b.add_argument("--out-dir", required=True)
    b.add_argument("--walk", action="store_true")
    b.add_argument("--idle", action="store_true")
    b.add_argument("--state", type=int)
    b.add_argument("--action", type=int)
    b.add_argument("--name")
    b.add_argument("--subtype", type=int, default=None)
    c = sub.add_parser("compare", help="clip from capture A vs every run in capture B")
    c.add_argument("clip")
    c.add_argument("capture")
    a = ap.parse_args(argv)
    if a.cmd == "compare":
        with open(a.clip, encoding="utf-8") as fh:
            clip = json.load(fh)
        frames = load_capture(a.capture)
        if clip["param"] == "time_ms":
            res = validate_time(clip, frames, on_back=clip["source"]["weapon_on_back"], skip_first=False)
        else:
            steady, _ = _steady_walk(frames, 0.97)
            errs = []
            for fr in steady:
                x = (fr.phase - clip["source"]["phase_origin"]) % TWO_PI
                pred = sample(clip, x)
                real = _frame_joints(fr, on_back=True)
                errs += [pose_error(pred[n], real[n]) for n in real]
            res = {"frames": len(steady), **worst(errs)}
        print(json.dumps({"clip": a.clip, "capture": a.capture, "result": res}, indent=1))
        return 0
    frames = load_capture(a.capture)
    if a.cmd == "summary":
        print(json.dumps(summary(frames), indent=1))
        return 0
    report = {"capture": a.capture, "clips": []}
    if a.idle:
        clip = build_idle_clip(frames, a.capture)
        report["clips"].append({"clip": write_clip(clip, a.out_dir), "keys": 1})
    if a.walk:
        clip = build_walk_clip(frames, a.capture)
        v = validate_walk(clip, clip["_steady_frames"])
        clip["validation"] = {"cross_cycle": v,
                              "reconstruct_max_abs": reconstruct_check(clip["_steady_frames"], True)}
        report["clips"].append({"clip": write_clip(clip, a.out_dir), "keys": len(clip["keys"]),
                                "validation": clip["validation"],
                                "cycle_seconds": clip["source"]["cycle_seconds"]})
    if a.state is not None:
        name = a.name or f"state{a.state}_a{a.action}"
        clip = build_time_clip(frames, a.capture, a.state, a.action, name, on_back=False,
                               subtype=a.subtype)
        run = [f for f in frames if f.state == a.state and f.action == a.action]
        clip["validation"] = {"other_occurrences": validate_time(clip, frames),
                              "reconstruct_max_abs": reconstruct_check(run, False)}
        report["clips"].append({"clip": write_clip(clip, a.out_dir), "keys": len(clip["keys"]),
                                "duration_ms": clip["duration"], "validation": clip["validation"]})
    print(json.dumps(report, indent=1))
    return 0


if __name__ == "__main__":
    sys.exit(main())
