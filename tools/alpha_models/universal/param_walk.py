"""Parametric Alpha locomotion: the walk formulas of ``Creature_drawModel`` re-implemented.

Approach B of docs/architecture/UNIVERSAL_ASSET_ANIMATION_PIPELINE.md §4, now implemented for the
*locomotion layer* (drawModel state 0, action 0: walking and standing). The formulas below were
read from the reconstruction of ``Creature_drawModel`` (VA 0x4128F0; line numbers refer to the
local, non-published ``.scratch/CubeWorld-Reversal/cube/control/GameController.cpp``) and checked
frame by frame against captured Alpha matrices of three body plans (human 0x02, WhiteCat 0x20,
PlainRunner 0x3f; report docs/research/alpha2013/PARAMETRIC_WALK_SYSTEM.md).

There are no per-creature animation data in Alpha: every creature's joint frames are a function of

* its APPEARANCE (``creature+0x78``): part offsets, part scales, ``pitch_tail``, ``angle_58``,
  ``flags`` bit 0 ("quadruped swing") — read statically by ``alpha_static``;
* its RUNTIME state: walk phase ``creature+0x118c``, walk amplitude ``creature+0x1188``, the x1.25
  boost (input/AI acceleration + velocity, ``uclip._boost``) and a smoothed head lean.

Core quantities (GameController.cpp:2214-2420), ``c = sin(phase + pi/2)``::

    w      = amp * 0.7*pi * c          (x1.25 boost, x0.75 if flags&1)        swing [rad]
    d0     =  1.5 * deg(w)    e4 = -1.5 * deg(w)                              limb pitch
    bob    = 4 * |w|                                                         body/hand lift
    lift_l = 4 * amp * c      lift_r = 4 * amp * (1 - c)                     foot lift
    flags&1 == 0 ("biped"): hands translate on a circle of radius R = hand.x*(1+amp)
    flags&1 == 1 ("quadruped"): hands pitch about the shoulder like feet, at a fixed position

Every part is a fixed chain of ``mat4_*`` calls on one of three base frames (placement ``P``,
``P`` lifted by ``bob``); the chains are in ``CHAINS`` below. Matrices use the Alpha row-vector
convention of ``anim.mat4`` (D3DX ``MultMatrixLocal``: each call pre-multiplies), so the joint
frame is ``J = O_n-1 * ... * O_1 * base`` without the final anchor translate — exactly the ``J``
stored in ``.alphaclip.json`` keys.

Scope and honesty rules:
* this is ``RECONSTRUCTED_FROM_EVIDENCE``; clips generated here are never ``TRANSFERRED_FROM_ALPHA``;
* only parameters whose effect was exercised by a capture are ``VERIFIED`` (``PARAM_DOMAIN``);
  ``check_domain`` lists every appearance value outside that domain — such creatures need a
  capture before the formula is trusted for them (see ``coverage``);
* attacks, the 73 drawModel action states, swimming/flying/riding and equipment layers are NOT
  modelled — they stay on the capture path (approach A).
"""
from __future__ import annotations

import math

from tools.alpha_models.anim import mat4

FORMAT = "cubegame.param_walk/1"
SWING_K = 2.1991148          # 0.7*pi, GameController.cpp:2240
NECK_PIVOT = 3.0             # head chain translate(0,0,-3) ... translate(0,0,3), GameController.cpp:6241/6253


# --------------------------------------------------------------------------- matrices (row-vector)
def rot_x(deg):
    a = math.radians(deg)
    c, s = math.cos(a), math.sin(a)
    return [1.0, 0, 0, 0, 0, c, s, 0, 0, -s, c, 0, 0, 0, 0, 1.0]


def rot_y(deg):
    a = math.radians(deg)
    c, s = math.cos(a), math.sin(a)
    return [c, 0, -s, 0, 0, 1.0, 0, 0, s, 0, c, 0, 0, 0, 0, 1.0]


def rot_z(deg):
    a = math.radians(deg)
    c, s = math.cos(a), math.sin(a)
    return [c, s, 0, 0, -s, c, 0, 0, 0, 0, 1.0, 0, 0, 0, 0, 1.0]


def scale(x, y=None, z=None):
    y = x if y is None else y
    z = x if z is None else z
    return [x, 0, 0, 0, 0, y, 0, 0, 0, 0, z, 0, 0, 0, 0, 1.0]


def chain(ops, base=None):
    """Apply drawModel's mat4_* calls in source order: M = op_n * ... * op_1 * base."""
    m = list(mat4.IDENTITY) if base is None else list(base)
    for op in ops:
        m = mat4.mul(op, m)
    return m


# --------------------------------------------------------------------------- inputs
class WalkState:
    """Runtime inputs of the locomotion layer (one drawModel call)."""

    def __init__(self, phase, amp, boost=False, head_lean_deg=None, on_ground=True, accelerating=False):
        self.phase = float(phase)
        self.amp = float(amp)
        self.boost = bool(boost)
        # None = the steady (fully smoothed) target for this state, see head_lean_target
        self.head_lean_deg = head_lean_deg
        self.on_ground = bool(on_ground)
        # AI-style acceleration along the velocity without the x1.25 boost (creature+0x40 . +0x34 > 0)
        self.accelerating = bool(accelerating)


def head_lean_target(app, boost, accelerating):
    """Target of the smoothed head pitch about the neck (post-switch code, GameController.cpp:5719-5735).

    flags bit0 = 1: no lean. Boost (player run): +10 deg — VERIFIED on the human capture.
    Accelerating without boost (AI): +5 deg — CODE_ONLY (same code pattern as the boost branch).
    Otherwise 0 (VERIFIED: the human decelerating without boost decays to 0).
    """
    if app["flags"] & 1:
        return 0.0
    if boost:
        return 10.0
    return 5.0 if accelerating else 0.0


LEAN_LERP_PER_MS = 0.01      # array_lerp(&f, &1, dt_ms, 0.01): f = 1 - 0.99^dt_ms (GameController.cpp:5766, 1231)


def lean_step(current, target, dt_ms, rate_per_ms=LEAN_LERP_PER_MS):
    """One drawModel smoothing step of a pose-state value (exact form of array_lerp over dt_ms steps)."""
    f = 1.0 - (1.0 - rate_per_ms) ** max(0.0, dt_ms)
    return current + (target - current) * f


def head_lean(app, state):
    if state.head_lean_deg is not None:
        return state.head_lean_deg
    return head_lean_target(app, state.boost, state.accelerating)


def core(app, state):
    """GameController.cpp:2214-2420 — the shared locomotion quantities."""
    c = math.sin(state.phase + math.pi / 2)            # libm_sse2_sin_precise(phase + 1.5707964)
    w = state.amp * SWING_K * c
    if state.boost:
        w *= 1.25
    flags = app["flags"]
    grounded_only = bool(flags & 2) and not state.on_ground
    if grounded_only:
        w = 0.0
    if flags & 1:
        w *= 0.75
    d0 = math.degrees(w) * 1.5
    e4 = -math.degrees(w) * 1.5
    lift_l = state.amp * 4.0 * c
    lift_r = state.amp * 4.0 * (1.0 - c)
    if grounded_only:
        lift_l = lift_r = 0.0
    hx, hy, hz = app["offset_hand"]
    if (flags & 1) == 0:
        r = hx * state.amp + hx
        hand_l = [-math.cos(w) * r, hy - math.sin(w) * r, (hz - 4.1) + state.amp * 6.0]
        hand_r = [math.cos(w) * r, hy + math.sin(w) * r, (hz - 4.1) + state.amp * 6.0]
    else:
        hand_l = [-hx, hy, hz]
        hand_r = [hx, hy, hz]
    hand_l[1] += 2.1 - state.amp * 5.0
    hand_r[1] += 2.1 - state.amp * 5.0
    return {"c": c, "w": w, "d0": d0, "e4": e4, "bob": 4.0 * abs(w), "foot_z": 3.0 * abs(w),
            "lift_l": lift_l, "lift_r": lift_r, "hand_l": hand_l, "hand_r": hand_r}


# --------------------------------------------------------------------------- part chains
def joints(app, state, parts):
    """Joint frames J (row-vector, without the anchor) for the requested universal block names."""
    k = core(app, state)
    bob = mat4.translate(0.0, 0.0, k["bob"])          # base frame of self+0x3b / self+0x4b
    quad = bool(app["flags"] & 1)
    out = {}
    for name in parts:
        if name == "body":       # GameController.cpp:6096-6105
            out[name] = chain([mat4.translate(*app["offset_body"]),
                               rot_z(math.degrees(k["w"] * 0.5)),
                               scale(app["scale_body"])], bob)
        elif name in ("head", "hair"):   # 6229-6256 / 6286-6311
            hx, hy, hz = app["offset_head"]
            out[name] = chain([mat4.translate(hx, hy, hz - k["bob"] * 0.5),
                               mat4.translate(0.0, 0.0, -NECK_PIVOT),
                               rot_x(head_lean(app, state)),
                               mat4.translate(0.0, 0.0, NECK_PIVOT),
                               scale(app["scale_head"])], bob)
        elif name == "tail":     # 6199-6215
            out[name] = chain([mat4.translate(*app["offset_body"]),
                               mat4.translate(*app["offset_tail"]),
                               rot_x(app["pitch_tail"]),
                               rot_z(-math.degrees(k["w"])),
                               scale(app["scale_tail"]),
                               scale(app["scale_body"])], bob)
        elif name in ("hand_l", "hand_r"):   # 7139-7229
            left = name == "hand_l"
            pitch = k["e4"] if left else k["d0"]
            ops = [rot_x(pitch)] if quad else []
            ops.append(mat4.translate(*(k["hand_l"] if left else k["hand_r"])))
            if quad:
                ops.append(rot_x(pitch))
            s = app["scale_hand"]
            ops.append(scale(s) if left else scale(-s, s, s))
            out[name] = chain(ops, bob)
        elif name == "foot_l":   # 7302-7323
            fx, fy, fz = app["offset_foot"]
            out[name] = chain([rot_x(app["angle_58"] + k["d0"]),
                               mat4.translate(-fx, fy, k["foot_z"] + fz + k["lift_l"]),
                               scale(app["scale_foot"]),
                               rot_x(k["d0"])])
        elif name == "foot_r":   # 7343-7363
            fx, fy, fz = app["offset_foot"]
            out[name] = chain([rot_x(k["e4"]),
                               mat4.translate(fx, fy, k["foot_z"] + fz + k["lift_r"]),
                               scale(app["scale_foot"]),
                               rot_x(app["angle_58"] + k["e4"])])
        else:
            raise KeyError(f"part {name!r} has no locomotion formula (block not modelled)")
    return out


MODELLED_PARTS = ("body", "head", "hair", "tail", "hand_l", "hand_r", "foot_l", "foot_r")


# --------------------------------------------------------------------------- formula terms
# Every piece of the locomotion formula is a TERM with an evidence level:
#   VERIFIED  — reproduced a captured Alpha frame (<= 1e-3 vox, <= 0.01 deg) on >= 1 kind;
#   CODE_ONLY — read from drawModel and implemented, but no capture exercised a non-zero value;
#   UNKNOWN   — the parameter is used by drawModel in state 0 but its chain is not established
#               (the decompilation lost the target of the arrayElem accesses) -> not implemented.
# A kind needs a new capture only for terms that are not VERIFIED; one capture of ANY kind that
# exercises a term verifies it for every kind (the formula is shared, the value is appearance data).
TERMS = {
    "core_swing":     ("VERIFIED", "human 0x02, WhiteCat 0x20, PlainRunner 0x3f; amp 0.0-0.193, boost on/off"),
    "body":           ("VERIFIED", "3 kinds: offset_body, yaw w/2, bob 4|w|"),
    "head":           ("VERIFIED", "human, WhiteCat: offset_head, bob/2, scale_head"),
    "hair":           ("VERIFIED", "human: same chain as head"),
    "tail":           ("VERIFIED", "WhiteCat: offset_body+offset_tail, pitch_tail -30, yaw -w, scale_tail*scale_body"),
    "hand_biped":     ("VERIFIED", "human (flags bit0 = 0): circle R = hand.x*(1+amp)"),
    "hand_quadruped": ("VERIFIED", "WhiteCat (flags bit0 = 1): double shoulder pitch e4/d0"),
    "foot":           ("VERIFIED", "3 kinds: hip pitch d0/e4 x2, lift, 3|w|, scale_foot 0.98/1.0/1.5"),
    "head_lean_boost": ("VERIFIED", "human player: +10 deg, smoothing 1-0.99^dt_ms (fit 9.5-10.5 /s)"),
    "head_lean_ai":   ("VERIFIED", "NPC humans 0x02 (AI, accelerating along the velocity): settled lean "
                                  "+5.000 deg on 1578 frames of cap_npc_human/survey1 (2026-11-05), smoothing "
                                  "one-step p95 0.0002 deg; predicted from the code pattern before the capture"),
    "foot_angle_58":  ("CODE_ONLY", "rotX(angle_58 + d0) first on foot_l, last on foot_r (7306/7357)"),
    "airborne_bit1":  ("CODE_ONLY", "flags bit1: swing and lift = 0 while creature+0x5c bit0 is clear"),
    "root_angle_48":  ("UNKNOWN", "rotX(angle_48 + lean) on the root, compensated on the head (5960, 6243); "
                                 "which base frames include it is not established"),
    "hand_rest_angles": ("UNKNOWN", "angle_4c/50/54 seed smoothed hand rotation arrays in state 0 "
                                   "(2190-2197, mirrored on the left); chain not established"),
}


def terms_used(app, parts):
    """Formula terms a creature with this appearance and part set exercises while walking."""
    used = ["core_swing"]
    quad = bool(app["flags"] & 1)
    for p in parts:
        if p in ("body", "head", "hair", "tail"):
            used.append(p)
        elif p in ("hand_l", "hand_r"):
            used.append("hand_quadruped" if quad else "hand_biped")
        elif p in ("foot_l", "foot_r"):
            used.append("foot")
    if not quad and ("head" in parts or "hair" in parts):
        used += ["head_lean_boost", "head_lean_ai"]
    if any(p in parts for p in ("foot_l", "foot_r")) and float(app["angle_58"]) != 0.0:
        used.append("foot_angle_58")
    if app["flags"] & 2:
        used.append("airborne_bit1")
    if float(app["angle_48"]) != 0.0:
        used.append("root_angle_48")
    if any(p in parts for p in ("hand_l", "hand_r")) and any(
            float(app[a]) != 0.0 for a in ("angle_4c", "angle_50", "angle_54")):
        used.append("hand_rest_angles")
    return sorted(set(used), key=list(TERMS).index)


def classify(app, parts, kind_exceptions=()):
    """Walk-formula readiness of one creature.

    FORMULA_VERIFIED        every exercised term is VERIFIED -> walk without a capture;
    FORMULA_CODE_ONLY_TERMS some terms are CODE_ONLY -> usable (RECONSTRUCTED), one spot capture of
                            any kind exercising the term upgrades it for all kinds;
    NEEDS_RESEARCH          an UNKNOWN term, a non-modelled part or kind-specific code.
    """
    reasons = list(kind_exceptions)
    for p in parts:
        if p not in MODELLED_PARTS:
            reasons.append(f"PART_NOT_MODELLED {p}")
    used = terms_used(app, parts)
    unknown = [t for t in used if TERMS[t][0] == "UNKNOWN"]
    code_only = [t for t in used if TERMS[t][0] == "CODE_ONLY"]
    if reasons or unknown:
        return {"status": "NEEDS_RESEARCH", "terms": used, "unknown_terms": unknown,
                "code_only_terms": code_only, "reasons": reasons}
    return {"status": "FORMULA_CODE_ONLY_TERMS" if code_only else "FORMULA_VERIFIED", "terms": used,
            "unknown_terms": [], "code_only_terms": code_only, "reasons": []}


# --------------------------------------------------------------------------- phase speed
# Creature update (GameController.cpp:102185-102196, on ground): phase += |v_xy| * dt * 1.5 * sqrt(X).
# The sqrt argument was lost by the decompiler; the captures give dphi/(|v| dt) = 1.37 (size.x 0.96,
# human and WhiteCat) and 1.22 (size.x 1.2, PlainRunner) = 1.5*sqrt(0.8/size.x) within 0.3 %.
# Status: INFERRED (two distinct sizes); used only to keep Unity's feet from sliding, not for poses.
def phase_rate(app, speed_xy):
    return 1.5 * speed_xy * math.sqrt(0.8 / app["size"][0])


# --------------------------------------------------------------------------- clip / spec export
def generate_walk_clip(app, kind, family_id, parts, amp, boost=False, keys=72, accelerating=False,
                       speed_xy=None, source=None):
    """A ``cubegame.alpha_clip/2`` walk clip computed by the formula (status RECONSTRUCTED_FROM_EVIDENCE).

    Same key layout as uclip clips (x = phase in [0, 2pi), m = J per part), so the existing
    Binding Generator / Unity importer play it unchanged. Refuses NEEDS_RESEARCH creatures.
    """
    from tools.alpha_models.universal import creature_rig as cr
    verdict = classify(app, parts)
    if verdict["status"] == "NEEDS_RESEARCH":
        raise ValueError(f"walk formula not established for this creature: {verdict}")
    state = WalkState(0.0, amp, boost, accelerating=accelerating)
    out = []
    for i in range(keys):
        x = 2.0 * math.pi * i / keys
        state.phase = x
        out.append({"x": x, "m": joints(app, state, parts)})
    rate = phase_rate(app, speed_xy) if speed_xy else None
    return {
        "format": "cubegame.alpha_clip/2",
        "rig": family_id,
        "rig_kind_source": f"0x{kind:02x}",
        "name": "walk",
        "status": "RECONSTRUCTED_FROM_EVIDENCE",
        "param": "walk_phase",
        "loop": True,
        "axes": "alpha_row_vector: X right, Y forward, Z up; 1 unit = 1 model voxel",
        "parts": [{"name": n, "anchor": cr.BY_NAME[n].anchor, "mirrored": cr.BY_NAME[n].mirrored,
                   "draw_site": f"0x{cr.BY_NAME[n].draw_site:06X}", "slot": f"0x{cr.BY_NAME[n].slot:02X}"}
                  for n in parts],
        "source": {"generator": FORMAT, "kind": f"0x{kind:02x}", "amplitude_creature_1188": amp,
                   "walk_boost_x1_25": bool(boost), "accelerating": bool(accelerating),
                   "terms": verdict["terms"], "code_only_terms": verdict["code_only_terms"],
                   "phase_rate_rad_per_s": rate, "phase_rate_status": "INFERRED" if rate else None,
                   "weapon_on_back": True, **(source or {})},
        "duration": 2.0 * math.pi,
        "keys": out,
    }


SPEC_FIELDS = ("flags", "size", "scale_head", "scale_body", "scale_hand", "scale_foot", "scale_tail",
               "pitch_tail", "angle_58", "offset_body", "offset_head", "offset_hand", "offset_foot",
               "offset_tail")


def locomotion_spec(app, kind, family_id, parts):
    """Parameters the Unity runtime (AlphaParamWalk.cs) needs; no geometry, no captured poses."""
    verdict = classify(app, parts)
    return {"format": FORMAT, "kind": f"0x{kind:02x}", "rig": family_id, "parts": list(parts),
            "status": verdict["status"], "terms": verdict["terms"],
            "code_only_terms": verdict["code_only_terms"], "unknown_terms": verdict["unknown_terms"],
            "params": {k: app[k] for k in SPEC_FIELDS}}


# --------------------------------------------------------------------------- validation vs Alpha
SCOPE_FLAGS_124 = 0x40       # creature+0x124 bits inside the locomotion scope (boost input only)
# drawModel smooths its pose-state arrays (body/hand rotations of action states) toward their
# targets with 1 - 0.99^dt_ms; after leaving an out-of-scope state (or at a capture start, whose
# history is unknown) the residue decays below 1e-4 of its start value within 1 s. Frames inside
# that window are compared and REPORTED separately ("settling"), not hidden and not thresholded.
SETTLE_S = 1.0

def validate_capture(frames, *, lean_from_capture=True):
    """Formula vs every usable state-0/action-0 frame of a capture (uclip.Frame list).

    The head lean is a smoothed runtime value; with ``lean_from_capture`` the per-frame lean is the
    one-step prediction ``lean_step(previous captured lean, target, dt)`` (so smoothing is tested,
    not copied); without it the steady target is used (errors appear only in transitions).
    """
    from tools.alpha_models.anim.alpha_clip import decompose, pose_error
    from tools.alpha_models.universal import uclip, alpha_static as st
    per_part, lean_err = {}, []
    lean_by_target = {}
    prev = {}            # creature -> (position in that creature's record sequence, t, measured lean)
    seq = {}
    used = out_of_scope = 0
    since, settling = {}, {}
    for f in frames:
        n = seq[f.creature] = seq.get(f.creature, -1) + 1
        res = uclip.frame_resolution(f)
        if not uclip.usable(f, res) or f.state != 0 or f.action != 0 or f.flags_124 & ~SCOPE_FLAGS_124:
            # creature+0x124 bits other than the boost input (0x40) switch drawModel paths that are
            # not part of the locomotion formula (e.g. 0x1 sitting/riding, 0x4 turn lean): not compared
            out_of_scope += 1
            prev.pop(f.creature, None)
            since.pop(f.creature, None)
            continue
        t0 = since.setdefault(f.creature, f.t)
        app = st.decode_appearance(f.appearance)
        captured = uclip.joints(f)
        parts = [p for p in captured if p in MODELLED_PARTS]
        lean = None
        if "head" in captured and not (app["flags"] & 1):
            _, q, _ = decompose(captured["head"])
            measured = math.degrees(2 * math.atan2(q[1], q[0]))
            target = head_lean_target(app, f.boost, f.accelerating)
            p = prev.get(f.creature)
            if lean_from_capture and p is not None and n - p[0] == 1 and 0 < f.t - p[1] < 0.05:
                lean = lean_step(p[2], target, (f.t - p[1]) * 1000.0)
                lean_err.append(abs(lean - measured))
                if abs(measured - target) < 1e-3:
                    lean_by_target[target] = lean_by_target.get(target, 0) + 1
            prev[f.creature] = (n, f.t, measured)
            if lean is None and lean_from_capture:      # no consecutive history: smoothing not testable
                parts = [p for p in parts if p not in ("head", "hair")]
        model = joints(app, WalkState(f.phase, f.amp, f.boost, head_lean_deg=lean,
                                      accelerating=f.accelerating), parts)
        if f.t - t0 < SETTLE_S:
            for p in parts:
                e = pose_error(model[p], captured[p])
                w = settling.setdefault(p, [0, 0.0, 0.0])
                settling[p] = [w[0] + 1, max(w[1], e[0]), max(w[2], e[1])]
            continue
        used += 1
        for p in parts:
            per_part.setdefault(p, []).append(pose_error(model[p], captured[p]))
    summary = {}
    for p, errs in per_part.items():
        rot = sorted(e[1] for e in errs)
        summary[p] = {"frames": len(errs), "max_pos_vox": max(e[0] for e in errs),
                      "max_rot_deg": rot[-1], "p95_rot_deg": rot[int(0.95 * (len(rot) - 1))],
                      "max_scale": max(e[2] for e in errs)}
    allp = [v for v in summary.values()]
    return {"frames": used, "out_of_scope_frames": out_of_scope,
            "settling": {p: {"frames": v[0], "max_pos_vox": v[1], "max_rot_deg": v[2]} for p, v in settling.items()},
            "head_lean_settled_frames_by_target": {f"{k:g}": v for k, v in sorted(lean_by_target.items())},
            "parts": summary,
            "max_pos_vox": max((v["max_pos_vox"] for v in allp), default=None),
            "max_rot_deg": max((v["max_rot_deg"] for v in allp), default=None),
            "head_lean_one_step": ({"frames": len(lean_err), "max_deg": max(lean_err),
                                    "p95_deg": sorted(lean_err)[int(0.95 * (len(lean_err) - 1))]}
                                   if lean_err else None)}


# --------------------------------------------------------------------------- Unity data
SYNTHETIC_APPEARANCES = {   # TEST_DATA_NOT_ALPHA: invented values that exercise every branch
    "biped": {"flags": 0x00, "size": [1.0, 1.0, 2.0], "scale_head": 1.1, "scale_body": 0.9, "scale_hand": 1.2,
              "scale_foot": 0.8, "scale_tail": 1.3, "pitch_tail": -20.0, "angle_58": 0.0,
              "offset_body": [0.0, 1.0, -3.0], "offset_head": [0.0, 2.0, 6.0], "offset_hand": [5.0, 1.0, -1.0],
              "offset_foot": [2.0, 0.5, -9.0], "offset_tail": [0.0, -6.0, 1.0]},
}
SYNTHETIC_APPEARANCES["quadruped"] = dict(SYNTHETIC_APPEARANCES["biped"], flags=0x01)
SYNTHETIC_APPEARANCES["airborne_bit1"] = dict(SYNTHETIC_APPEARANCES["biped"], flags=0x02)
SYNTHETIC_APPEARANCES["hip_angle_58"] = dict(SYNTHETIC_APPEARANCES["quadruped"], angle_58=12.0)


def golden_cases():
    """Python reference values for the C# port (AlphaParamWalkCheck GOLDEN). No Alpha data."""
    cases = []
    for an, app in SYNTHETIC_APPEARANCES.items():
        for phase in (0.0, 0.7, 2.0, math.pi, 4.4):
            for amp, boost, lean, ground in ((0.19, False, 0.0, True), (0.12, True, 7.5, True), (0.19, False, 0.0, False)):
                st_ = WalkState(phase, amp, boost, head_lean_deg=lean, on_ground=ground)
                for part, m in joints(app, st_, MODELLED_PARTS).items():
                    cases.append({"name": f"{an}/{part}/ph{phase:.2f}/a{amp}/b{int(boost)}/g{int(ground)}",
                                  "p": {k: app[k] for k in SPEC_FIELDS}, "phase": phase, "amp": amp,
                                  "boost": boost, "lean": lean, "onGround": ground, "part": part, "m": m})
    return {"status": "TEST_DATA_NOT_ALPHA", "cases": cases}


def reference_frames(frames, capture_path, max_frames=400):
    """Captured Alpha joint frames for the Unity ALPHA check (local .scratch data only, D54).

    Head/hair use the lean MEASURED from the captured head (the chain is checked; the smoothing is
    checked separately by validate_capture's one-step prediction).
    """
    import hashlib
    from tools.alpha_models.anim.alpha_clip import decompose
    from tools.alpha_models.universal import uclip, alpha_static as st
    rows, app0, kind, rig = [], None, None, None
    usable, since = [], {}
    for f in frames:
        res = uclip.frame_resolution(f)
        if not uclip.usable(f, res) or f.state != 0 or f.action != 0 or f.flags_124 & ~SCOPE_FLAGS_124:
            since.pop(f.creature, None)
            continue
        # same scope as validate_capture: skip the settling window after an out-of-scope state
        if f.t - since.setdefault(f.creature, f.t) >= SETTLE_S:
            usable.append((f, res))
    step = max(1, len(usable) // max_frames)
    for f, res in usable[::step]:
        app = st.decode_appearance(f.appearance)
        app0, kind, rig = app0 or app, f.kind, res.family_id
        cap = uclip.joints(f)
        parts = [p for p in res.parts if p in MODELLED_PARTS]
        lean = 0.0
        if "head" in cap:
            _, q, _ = decompose(cap["head"])
            lean = math.degrees(2 * math.atan2(q[1], q[0]))
        rows.append({"index": f.index, "phase": f.phase, "amp": f.amp, "boost": f.boost, "lean": lean,
                     "parts": parts, "m": [v for p in parts for v in cap[p]]})
    with open(capture_path, "rb") as fh:
        sha = hashlib.sha256(fh.read()).hexdigest()
    return {"status": "ALPHA_LOCAL_ONLY", "capture": capture_path.replace("\\", "/").split("/")[-1],
            "capture_sha256": sha, "kind": f"0x{kind:02x}", "rig": rig,
            "p": {k: app0[k] for k in SPEC_FIELDS}, "frames": rows}


# --------------------------------------------------------------------------- coverage
def coverage(static_table, captured_kinds=(), captured_families=()):
    """Walk readiness of every kind and motion family of the static table (no Alpha run)."""
    import collections
    from tools.alpha_models.universal import creature_rig as cr
    kinds, fams = [], collections.OrderedDict()
    for k, e in static_table["kinds"].items():
        app = e.get("appearance")
        if not app:
            kinds.append({"kind": k, "status": "NEEDS_RESEARCH", "reasons": ["NO_APPEARANCE"]})
            continue
        res = cr.resolve(int(k, 16), app)
        v = classify(app, res.parts, kind_exceptions=[x for x in res.exceptions])
        variants = e.get("motion_param_variants") or 1
        if isinstance(variants, list):
            variants = len(variants)
        row = {"kind": k, "name_hint": e.get("name_hint"), "family_id": res.family_id, "parts": res.parts,
               "captured": k in captured_kinds or res.family_id in captured_families,"motion_param_variants": variants, **v}
        kinds.append(row)
        f = fams.setdefault(res.family_id, {"kinds": [], "parts": res.parts, "status": v["status"],
                                            "code_only_terms": v["code_only_terms"],
                                            "unknown_terms": v["unknown_terms"], "reasons": v["reasons"],
                                            "captured": False})
        f["kinds"].append(k)
        f["captured"] = f["captured"] or row["captured"]
    by = collections.Counter(r["status"] for r in kinds)
    fam_by = collections.Counter(f["status"] for f in fams.values())
    uncaptured = {fid: f for fid, f in fams.items() if not f["captured"]}
    unc_by = collections.Counter(f["status"] for f in uncaptured.values())
    term_need = collections.Counter(t for f in uncaptured.values() for t in f["code_only_terms"] + f["unknown_terms"])
    def _reason_key(r):
        w = r.split(" ")
        if w[0] in ("PART_NOT_MODELLED", "UNVERIFIED_DRAW_BLOCK") and len(w) > 1:
            return f"{w[0]} {w[1].rstrip('_ab').rstrip('_')}"     # slot9a_a / slot9a_b -> slot9a
        return w[0]
    reason_need = collections.Counter(k for f in uncaptured.values() for k in {_reason_key(r) for r in f["reasons"]})
    return {"format": "cubegame.param_walk_coverage/1",
            "metrics": {"kinds": len(kinds), "families": len(fams), "kinds_by_status": dict(by),
                        "families_by_status": dict(fam_by), "families_captured": len(fams) - len(uncaptured),
                        "uncaptured_families_by_status": dict(unc_by),
                        "uncaptured_families_per_open_term": dict(term_need),
                        "uncaptured_families_per_blocking_reason": dict(reason_need)},
            "terms": {t: {"status": s, "evidence": ev} for t, (s, ev) in TERMS.items()},
            "families": fams, "kinds": kinds}


# Acceptance of a formula-vs-capture validation (all parts except the smoothed head lean, which is
# reported separately by its one-step prediction). float32 Alpha maths vs float64 here: ~3e-4 vox.
VALIDATE_MAX_POS_VOX = 1e-3
VALIDATE_MAX_ROT_DEG = 0.01
