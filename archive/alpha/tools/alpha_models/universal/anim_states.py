"""Alpha animation state resolver: creature action -> drawModel state (and timer).

Found with a hardware write watch on the player's drawModel ``this`` (``write_watch.py``): the
creature renderer (block VA 0x4B4EE5-0x4B532F, inside the function the decompilation calls
``iterated_lerp_toward1``) runs every frame, BEFORE ``Creature_drawModel``:

    this[1] (creature+0x149c) = creature+0x6c      # action timer, ms
    this[2] (creature+0x14a0) = f(action creature+0x68, weapon +0xaa8/+0xaa9,
                                  offhand +0x990/+0x991, creature+0x130)

``f`` is a jump table over 110 action codes with equipment conditions. It has no calls; this
module EXECUTES those exact bytes (``drawmodel_emu.EmuCPU``) for every input combination instead
of transcribing the table, so the mapping is Alpha's own.
"""
from __future__ import annotations

import itertools
import struct

from tools.alpha_models.universal import drawmodel_emu as de

BLOCK_START = 0x4B4EF6          # movzx eax, byte [esi+0x68]
BLOCK_END = 0x4B532F            # common exit after the state store
STATE_OFFSET = 0x14A0           # creature+0x14a0 == drawModel this[2]
TIMER_OFFSET = 0x149C           # creature+0x149c == drawModel this[1] (= creature+0x6c)
THIS_OFFSET = 0x1498            # the drawModel `this` lives inside the creature struct
INPUT_FIELDS = {"action": 0x68, "weapon_type": 0xAA8, "weapon_subtype": 0xAA9,
                "offhand_type": 0x990, "offhand_subtype": 0x991, "flag_130": 0x130}


def resolve_state(image, action, weapon=(0, 0), offhand=(0, 0), flag_130=0):
    cpu = de.EmuCPU(image, max_steps=10_000)
    cpu.mem.map(de.STACK_BASE, de.STACK_SIZE)
    cre = cpu.mem.map(de.HEAP_BASE, 0x2000)
    cre[0x68] = action & 0xFF
    cre[0xAA8], cre[0xAA9] = weapon
    cre[0x990], cre[0x991] = offhand
    cre[0x130] = flag_130 & 0xFF
    struct.pack_into("<i", cre, STATE_OFFSET, -1)
    cpu.r["esi"] = de.HEAP_BASE
    cpu.r["esp"] = de.STACK_BASE + 0x80000
    cpu.eip = BLOCK_START
    while cpu.eip != BLOCK_END:
        cpu.step()
    return struct.unpack_from("<i", cre, STATE_OFFSET)[0]


def state_table(image, actions=range(256), weapon_types=(0, 3), weapon_subtypes=range(0, 26),
                offhands=((0, 0), (4, 0), (4, 14), (3, 14), (3, 20), (3, 0)), flags_130=(0, 1)):
    """{action: {state: [conditions...]}} over the equipment combinations that matter."""
    out = {}
    for a in actions:
        seen = {}
        for wt, ws, oh, fl in itertools.product(weapon_types, weapon_subtypes, offhands, flags_130):
            if wt == 0 and ws:
                continue
            s = resolve_state(image, a, (wt, ws), oh, fl)
            seen.setdefault(s, []).append({"weapon": [wt, ws], "offhand": list(oh), "flag_130": fl})
        out[a] = seen
    return out


# --------------------------------------------------------------------------- action timings
# thiscall(creature) functions drawModel itself consults for phase boundaries (ms):
TIMING_FUNCS = {
    "attack_timing": (0x43D1A0, ()),        # combat_getAttackTiming(): action end / strike moment
    "windup": (0x43CAA0, (0xFFFFFFFF,)),    # combat_getAttackWindup(-1 = current action)
    "reload": (0x447310, (0xFFFFFFFF,)),    # combat_getReloadFrames(-1)
    "stamina": (0x444270, (0xFFFFFFFF,)),   # combat_getStaminaCost(-1)
}


def action_timings(image, creature_template, action, weapon=None):
    """Call Alpha's combat timing functions for one action on a creature struct (bytes)."""
    out = {}
    for name, (va, args) in TIMING_FUNCS.items():
        run = de.DrawModelRun(image)
        cpu = de.EmuCPU(image, stubs=run.stubs, max_steps=200_000)
        cpu.mem.map(de.STACK_BASE, de.STACK_SIZE)
        cre = cpu.mem.map(de.HEAP_BASE, 0x2000, creature_template)
        misc = cpu.mem.map(de.Layout.MISC, 0x1000)
        struct.pack_into("<II", misc, 0x800, de.Layout.MISC + 0x800, de.Layout.MISC + 0x800)
        struct.pack_into("<I", cre, 0x1178, de.Layout.MISC + 0x800)
        cre[0x68] = action & 0xFF
        if weapon is not None:
            equip(cre, 0xAA8, *weapon)
        try:
            v = cpu.run(va, args=args, ecx=de.HEAP_BASE)
            out[name] = struct.unpack("<i", struct.pack("<I", v))[0]
        except Exception as e:  # noqa: BLE001
            out[name] = f"ERROR {str(e)[:60]}"
    return out


# --------------------------------------------------------------------------- state survey
def _joints(frame):
    """{draw_site: J} with J = M * P^-1 (anchor kept: compared against the same model sizes)."""
    from tools.alpha_models.anim import mat4
    P = frame["placement"]
    Pi = mat4.inverse_affine(P)
    return {p["draw_site"]: mat4.mul(p["matrix"], Pi) for p in frame["parts"]}


def survey_action(image, creature_template, this_template, sizes, action, duration_ms, dt_ms=16,
                  settle_frames=40, weapon=None):
    """Run drawModel through one action from a settled idle pose; per part motion statistics.

    Returns {"state": s, "frames": n, "parts": {site: {max_pos, max_rot_deg, t_peak_ms}},
             "ends_at_rest": bool, "track": [(t, {site: J})...] (sparse)}.
    """
    from tools.alpha_models.anim.alpha_clip import pose_error
    em = de.Emulator(image, creature_template, sizes, this_template)
    if weapon is not None:
        em.creature[0xAA8], em.creature[0xAA9] = weapon
    em.set_u8(0x68, 0)
    em.set_f32(0x1188, 0.0)
    for _ in range(settle_frames):                       # settle smoothing arrays at idle
        idle = em.frame(dt_ms=dt_ms)
    rest = _joints(idle)
    em.set_u8(0x68, action)
    stats, track, t = {}, [], 0
    state = None
    while t <= duration_ms:
        em.set_i32(0x6C, t)
        # the renderer's resolver (this file) sets this[1]/this[2] before drawModel each frame
        struct.pack_into("<i", em.this, 4, t)
        s = resolve_state(image, action, (em.creature[0xAA8], em.creature[0xAA9]),
                          (em.creature[0x990], em.creature[0x991]), em.creature[0x130])
        struct.pack_into("<i", em.this, 8, s)
        state = s
        fr = em.frame(dt_ms=dt_ms)
        j = _joints(fr)
        for site, m in j.items():
            if site not in rest:
                stats.setdefault(site, {"max_pos": 0.0, "max_rot_deg": 0.0, "t_peak_ms": 0, "appears": True})
                continue
            e = pose_error(m, rest[site])
            st = stats.setdefault(site, {"max_pos": 0.0, "max_rot_deg": 0.0, "t_peak_ms": 0})
            if e[0] + e[1] / 10 > st["max_pos"] + st["max_rot_deg"] / 10:
                st["t_peak_ms"] = t
            st["max_pos"] = max(st["max_pos"], e[0])
            st["max_rot_deg"] = max(st["max_rot_deg"], e[1])
        if t % 64 == 0:
            track.append((t, j))
        t += dt_ms
    return {"action": action, "state": state, "parts": stats, "track": track}


# --------------------------------------------------------------------------- baking (oracle -> clip)
ITEM_SIZE = 0x118


def equip(creature, offset, item_type, subtype, level=1):
    """Write a minimal item struct (type, subtype, level at +0x10) into an equipment slot.
    Level 0 makes Alpha's stat formulas divide by zero (attack timings become INT_MIN)."""
    creature[offset:offset + ITEM_SIZE] = bytes(ITEM_SIZE)
    if item_type:
        creature[offset] = item_type & 0xFF
        creature[offset + 1] = subtype & 0xFF
        struct.pack_into("<i", creature, offset + 0x10, level)


def bake_action(oracle, kind, action, weapon=(0, 0), offhand=(0, 0), dt_ms=16, tail_ms=700, seed=(0, 0),
                creature_bytes=None, this_bytes=b"", duration_ms=None, dynamic_parts=False,native_timer_carry=False):
    """Run Alpha's renderer resolver + drawModel through idle -> action -> back to idle.

    Returns {"state", "timings", "duration_ms", "frames": [{"t", "action", "state", "joints"}]} with
    joints = T(anchor_rule(size)) * M * P^-1 using the RIG's fixed anchor rules (weapon: back), so a
    Unity rig with those rules reproduces Alpha's model matrices exactly for every frame, whatever
    anchor Alpha used internally (e.g. weapon in hand during the strike)."""
    from tools.alpha_models.anim import mat4
    from tools.alpha_models.universal import creature_rig as cr
    if not isinstance(dt_ms, int) or dt_ms <= 0 or tail_ms < 0:
        raise ValueError("INVALID_BAKE_STEP")
    if creature_bytes is None:
        c, _ = oracle.creature(kind, seed)
        c[0x5C] |= 1
    else:
        c = bytearray(creature_bytes)
    equip(c, 0xAA8, *weapon)
    equip(c, 0x990, *offhand)
    em = de.Emulator(oracle.image, bytes(c), oracle.sizes, this_bytes)
    em.set_u8(0x68, 0)
    em.set_f32(0x1188, 0.0)
    for dt in (600, 600, 16):                     # settle the smoothing at idle
        idle = em.frame(dt_ms=dt)
    timings = action_timings(oracle.image, bytes(em.creature), action, weapon)
    if duration_ms is None:
        duration_ms = timings["attack_timing"]
    if not isinstance(duration_ms, int) or not 0 < duration_ms <= 30000:
        raise ValueError(f"ACTION_DURATION_UNPROVEN action={action} timings={timings}")
    frames, t, state = [], 0, None
    sample_times = sorted(set(range(0, duration_ms + tail_ms + 1, dt_ms)) | {duration_ms, duration_ms + tail_ms})
    if native_timer_carry:sample_times=sorted(set(sample_times)|{duration_ms+1})
    if dynamic_parts:
        boundaries=[duration_ms,duration_ms//2]
        for names in (('windup',),('windup','reload'),('windup','reload','stamina')):
            if all(isinstance(timings[n],int) and timings[n]>=0 for n in names):
                boundaries.append(sum(timings[n] for n in names))
        if isinstance(timings.get('windup'),int):boundaries.append(timings['windup']+300)
        sample_times=sorted(set(sample_times)|{v+d for v in boundaries for d in (-1,0,1) if 0<=v+d<=duration_ms+tail_ms})
    previous_t = -dt_ms
    for t in sample_times:
        a = action if t <= duration_ms else 0
        em.set_u8(0x68, a)
        timer=t if a or native_timer_carry else t-duration_ms
        em.set_i32(0x6C,timer)
        struct.pack_into("<i", em.this, 4,timer)
        s = resolve_state(oracle.image, a, weapon, offhand, em.creature[0x130]) if a else 0
        struct.pack_into("<i", em.this, 8, s)
        if a:
            state = s
        fr = em.frame(dt_ms=t - previous_t)
        previous_t = t
        Pi = mat4.inverse_affine(fr["placement"])
        joints = {};unsupported=[]
        for p in fr["parts"]:
            b = cr.BY_DRAW_SITE.get(p["draw_site"])
            size = oracle.sizes.get(p.get("model_index"))
            if b is None or size is None or b.anchor == "unknown":
                unsupported.append(p['draw_site'])
                continue
            rule = b.anchor
            if b.name in cr.EQUIPMENT_ANCHOR_BY_SUBTYPE:       # D64: subtype-dependent fixed anchors
                sub = (offhand if b.name.startswith("offhand") else weapon)[1]
                rule = cr.equipment_anchor(b.name, sub)
                if rule == "center" and b.anchor == "weapon":
                    rule = "weapon"
            anc = cr.anchor(rule, size, kind=kind, on_back=True)
            joints[b.name] = mat4.mul(mat4.mul(mat4.translate(*anc), p["matrix"]), Pi)
        frames.append({"t": t, "action": a, "state": s, "joints": joints,"unsupported_draw_sites":unsupported})
    return {"state": state, "timings": timings, "duration_ms": duration_ms, "frames": frames,
            "idle": idle, "dt_ms": dt_ms, "weapon": list(weapon), "offhand": list(offhand), "kind": kind,
            "dynamic_parts":dynamic_parts}


def baked_clip(bake, kind, family_id, name, parts):
    """cubegame.alpha_clip/2 time clip (status EMULATED_ALPHA_CODE) from bake_action output."""
    from tools.alpha_models.universal import creature_rig as cr
    keys = []
    dynamic=bake.get('dynamic_parts',False)
    for f in bake["frames"]:
        if f.get('unsupported_draw_sites'):raise ValueError(f"UNSUPPORTED_NATIVE_DRAW {f['unsupported_draw_sites']}")
        if set(f['joints'])-set(parts):raise ValueError('UNEXPECTED_NATIVE_PART')
        if not dynamic and not all(p in f["joints"] for p in parts):
            raise ValueError(f"ACTION_PARTS_MISSING t={f['t']} expected={parts} actual={list(f['joints'])}")
        key={"x":float(f['t']),"m":{p:f['joints'][p] for p in parts if p in f['joints']}}
        if dynamic:key['visible']=[int(p in f['joints']) for p in parts]
        keys.append(key)
    if len(keys) < 2:
        raise ValueError("ACTION_NO_KEYS")
    recovery = next((f["t"] for f in bake["frames"] if f["action"] == 0), None)
    return {
        "format": "cubegame.alpha_clip/3" if dynamic else "cubegame.alpha_clip/2", "rig": family_id, "rig_kind_source": f"0x{kind:02x}", "name": name,
        "status": "EMULATED_ALPHA_CODE", "param": "time_ms", "loop": False,
        "axes": "alpha_row_vector: X right, Y forward, Z up; 1 unit = 1 model voxel",
        "parts": [{"name": n, "anchor": cr.BY_NAME[n].anchor, "mirrored": cr.BY_NAME[n].mirrored,
                   "draw_site": f"0x{cr.BY_NAME[n].draw_site:06X}", "slot": f"0x{cr.BY_NAME[n].slot:02X}"} for n in parts],
        "source": {"generator": "alpha_oracle drawModel emulation", "state": bake["state"],
                   "timings": bake["timings"], "action_end_ms": bake["duration_ms"],
                   "recovery_start_ms": recovery, "sample_dt_ms": bake.get("dt_ms"),
                   "weapon": bake.get("weapon"), "offhand": bake.get("offhand"),
                   "weapon_on_back": True,
                   "visibility_mode":"EXPLICIT_NATIVE_DRAW" if dynamic else "STATIC_ALL",
                   "anchor_policy": "fixed back anchor absorbs Alpha hand/back changes into J; no pose edits"},
        "duration": keys[-1]["x"] if keys else 0.0,
        "keys": keys,
    }
