"""Alpha animation oracle: Alpha's own drawModel, executed offline, on any creature kind.

Builds a creature from the static tables (kind -> appearance by executing
``creature_generateAppearance``; model sizes of the full registry) and runs
``drawmodel_emu.Emulator`` frame by frame. It is the REFERENCE every Python/C# formula is checked
against, for kinds that were never captured: the emulator itself was verified against runtime
captures to float precision (docs/research/alpha2013/ALPHA_ANIMATION_SYSTEM.md).

Joint frames are returned exactly like ``uclip`` clips (``J = T(anchor) * M * P^-1``) for parts
whose anchor rule is established; other parts are returned raw (model matrix + placement).
"""
from __future__ import annotations

import json
import math
import struct

from tools.alpha_models.anim import mat4
from tools.alpha_models.anim.alpha_clip import pose_error
from tools.alpha_models.universal import alpha_static as st
from tools.alpha_models.universal import creature_rig as cr
from tools.alpha_models.universal import drawmodel_emu as de


RUNTIME_DEFAULTS = ((0x190, "<i", 1), (0x174, "<f", 1.0), (0x178, "<f", 100.0), (0x17C, "<f", 1.0))


class Oracle:
    def __init__(self, image, registry_sizes):
        self.image = image
        self.sizes = {int(k): tuple(v) for k, v in registry_sizes.items()}
        self.appearance = st.AppearanceEmulator(image)

    def creature(self, kind, seed=(0, 0)):
        raw = self.appearance.run(kind, *seed)["raw"]
        c = bytearray(0x2000)
        c[0x78:0x78 + len(raw)] = raw
        struct.pack_into("<I", c, 0x64, kind)
        # runtime stat defaults (values of a freshly spawned creature in the full-dump captures):
        # level 1; +0x174 / +0x17c multipliers 1.0 and +0x178 = 100.0 — with zeros Alpha's
        # attack-speed formulas divide by zero and every action timing becomes INT_MIN
        for off, fmt, v in RUNTIME_DEFAULTS:
            struct.pack_into(fmt, c, off, v)
        return c, st.decode_appearance(raw)

    def emulator(self, kind, seed=(0, 0), on_ground=True, fields=None):
        c, app = self.creature(kind, seed)
        if on_ground:
            c[0x5C] |= 1                   # creature+0x5c bit 0: standing on ground
        for off, (fmt, v) in (fields or {}).items():
            struct.pack_into(fmt, c, off, v)
        return de.Emulator(self.image, bytes(c), self.sizes), app

    def joints(self, frame, kind):
        out, raw = {}, {}
        P = frame["placement"]
        Pi = mat4.inverse_affine(P)
        for p in frame["parts"]:
            b = cr.BY_DRAW_SITE.get(p["draw_site"])
            name = b.name if b else f"site_0x{p['draw_site']:06X}"
            size = self.sizes.get(p.get("model_index"))
            if b is None or b.anchor in ("unknown",) or size is None or b.anchor == "weapon":
                raw[name] = {"matrix": p["matrix"], "model_index": p.get("model_index"), "size": size}
                continue
            a = cr.anchor(b.anchor, size, kind=kind, on_back=True)
            out[name] = mat4.mul(mat4.mul(mat4.translate(*a), p["matrix"]), Pi)
        return out, raw

    def walk_frame(self, kind, phase, amp, on_ground=True, settle=(600, 600, 16), seed=(0, 0)):
        """Steady locomotion pose (state 0) at a given phase/amplitude; smoothing settled.

        drawModel smooths pose arrays with 1-0.99^dt_ms per frame; two 600 ms frames leave
        0.99^1200 = 6e-6 of the initial value, then one ordinary 16 ms frame is sampled."""
        em, app = self.emulator(kind, seed, on_ground,
                                {0x1188: ("<f", amp), 0x118C: ("<f", phase)})
        for dt in settle:
            fr = em.frame(dt_ms=dt)
            em.set_f32(0x1188, amp)          # drawModel does not advance phase/amp (game logic does)
            em.set_f32(0x118C, phase)
        j, raw = self.joints(fr, kind)
        return j, raw, app


def compare_formula(oracle, kind, samples=((0.0, 0.0), (0.8, 0.19), (2.2, 0.19), (4.0, 0.12), (5.5, 0.06)),
                    on_ground=(True, False)):
    """Max pose error of param_walk.joints vs Alpha's drawModel over phases/amps/ground states."""
    from tools.alpha_models.universal import param_walk as pw
    worst = {"pos": 0.0, "rot": 0.0, "parts": {}, "unmodelled": set(), "samples": 0}
    for og in on_ground:
        for phase, amp in samples:
            j, raw, app = oracle.walk_frame(kind, phase, amp, og)
            worst["unmodelled"].update(raw)
            parts = [p for p in j if p in pw.MODELLED_PARTS]
            model = pw.joints(app, pw.WalkState(phase, amp, on_ground=og, head_lean_deg=0.0), parts)
            for p in parts:
                e = pose_error(model[p], j[p])
                w = worst["parts"].setdefault(p, [0.0, 0.0])
                w[0], w[1] = max(w[0], e[0]), max(w[1], e[1])
                worst["pos"], worst["rot"] = max(worst["pos"], e[0]), max(worst["rot"], e[1])
            worst["samples"] += 1
    worst["unmodelled"] = sorted(worst["unmodelled"])
    return worst


def load(exe, registry_sizes_path):
    from tools.alpha_models.anim import pe_bytes as pb
    with open(registry_sizes_path, encoding="utf-8") as fh:
        return Oracle(pb.load(exe), json.load(fh))
