"""Capture the REAL per-part model matrices Alpha uploads while drawing one creature.

Why this exists (S05A-2, D57/D58): the static route state -> channel -> part could not be
closed because the per-state pose code writes stack channels whose consumer is hard to name.
This tool skips the channel question entirely: inside ``Creature_drawModel`` (VA 0x4128F0)
every body part ends with

    render_uploadModelViewMatrices(partMatrix, view, proj)   ; 27 call sites
    GameController_draw_mesh()  (thiscall, ECX = mesh)       ; 27 call sites

so breaking on those call instructions yields, per drawn part, the final 4x4 matrix Alpha
itself sends to the GPU and the mesh object it is applied to. Together with the creature's
animation fields (state, walk phase, attack time) this IS the original animation, sampled.

Breakpoints (INT3, restored on every exit path):
  * ``0x412A42`` (``movsx ecx,word [eax+0x8c]``) — start of each drawModel body; eax=creature,
    edi=this. Used to gate: the 54 part breakpoints are armed only while the target
    creature is being drawn, so the game keeps a usable frame rate.
  * 27 ``call render_uploadModelViewMatrices`` sites — [esp] = matrix pointer.
  * 27 ``call GameController_draw_mesh`` sites — ECX = mesh object.

Output: JSON lines, one record per drawModel invocation of the target creature::

    {"t": <perf seconds>, "creature": "0x..", "this_hdr": [...], "fields": {...},
     "parts": [{"upload_site": "0x..", "draw_site": "0x..", "matrix": [16 floats],
                "mesh": "0x..", "mesh_hdr": [...]}, ...]}

The process is only read, apart from the INT3 bytes, which are always restored. Run only
against the local research copy in ``.scratch`` (D54/D55/D57).
"""
from __future__ import annotations

import argparse
import ctypes
import json
import struct
import sys
import time

from tools.alpha_models.runtime import drawmodel_probe as dp

IMAGE_BASE = 0x400000
DRAWMODEL_START = 0x4128F0
DRAWMODEL_END = 0x423F20
GATE_VA = 0x412A42
UPLOAD_FN = 0x4482A0
DRAWMESH_FN = 0x4E6DF0
# GameController_renderCreature: `mov ecx,[edi+0x8006d0]` — edi = game controller, the dword is
# the LOCAL PLAYER creature pointer (S05A2-D58, HUMANOID_MOTION_D58.md section 2.3).
PLAYER_READ_VA = 0x4A6B93
PLAYER_OFFSET = 0x8006D0

# creature fields read for every sample: (name, offset, struct format)
CREATURE_FIELDS = [
    ("type_68", 0x68, "<B"),
    ("time_6c", 0x6C, "<f"),
    ("flags_7e", 0x7E, "<H"),
    ("flags_124", 0x124, "<H"),
    ("b_140", 0x140, "<B"),
    ("b_141", 0x141, "<B"),
    ("amp_1188", 0x1188, "<f"),
    ("phase_118c", 0x118C, "<f"),
    ("f_1190", 0x1190, "<f"),
]
CREATURE_DUMP = (0x0, 0x180)       # raw creature header kept for later field discovery
THIS_DUMP = (0x0, 0x100)           # drawModel `this` (state self[2]; model slots self[0x2f..0x3a])
# drawModel model slots (this + 4*idx): the mesh drawn by every part is one of these pointers, so
# `mesh == self[slot]` identifies WHICH model a draw call renders independent of the call site.
# Slot <- source (Creature_drawModel prologue, GameController.cpp:2118-2200; equipment overrides):
MODEL_SLOTS = {
    0x2F: "head",      # creature+0x8c (appearance+0x14)
    0x30: "hair",      # creature+0x8e (appearance+0x16)
    0x33: "hand",      # creature+0x90 (appearance+0x18) | equipment +0x760 (gloves)
    0x36: "foot",      # creature+0x92 (appearance+0x1a) | equipment +0x648 (boots)
    0x31: "body",      # creature+0x94 (appearance+0x1c) | equipment +0x530 (chest)
    0x35: "tail",      # creature+0x96 (appearance+0x1e)
    0x32: "slot_98",   # creature+0x98 (appearance+0x20)
    0x34: "slot_9a",   # creature+0x9a (appearance+0x22)
    0x37: "weapon",    # equipment +0xaa8
    0x38: "offhand",   # equipment +0x990
    0x39: "shoulder",  # equipment +0x878
    0x3A: "neck",      # equipment +0x418
}
KIND_OFFSET = 0x64                 # creature kind (creature_generateAppearance(kindOut=creature+100))
MESH_DUMP = (0x0, 0x60)            # mesh object header (sizes, buffers)
# Creature_drawModel prologue: push ebp; mov ebp,esp; and esp,-8; sub esp,0x1730; push esi;
# push edi; mov edi,ecx; mov [esp+0x20],edi.  So in the body, the decompiler's local_1720
# (= this) lives at [esp+0x20] and local_X lives at esp + 0x1740 - X.
FRAME_THIS_OFFSET = 0x20
FRAME_LOCAL_BASE = 0x1740


def find_call_sites(image_bytes_reader, start, end, target):
    """Return VAs of every ``E8 rel32`` call to ``target`` inside [start, end)."""
    code = image_bytes_reader(start, end - start)
    sites = []
    for i in range(len(code) - 5):
        if code[i] == 0xE8:
            va = start + i
            rel = int.from_bytes(code[i + 1:i + 5], "little", signed=True)
            if (va + 5 + rel) & 0xFFFFFFFF == target:
                sites.append(va)
    return sites


def static_sites(exe_path):
    """Call sites from the on-disk image (identical bytes to the loaded module)."""
    from tools.alpha_models.anim import pe_bytes as pb
    img = pb.load(exe_path)
    ups = find_call_sites(img.read, DRAWMODEL_START, DRAWMODEL_END, UPLOAD_FN)
    dms = find_call_sites(img.read, DRAWMODEL_START, DRAWMODEL_END, DRAWMESH_FN)
    return ups, dms


class Capture:
    def __init__(self, pid, exe_path, target=None, max_records=400, max_seconds=30.0,
                 out=None, auto_parts=5, kinds=None, per_kind=1):
        self.pid = pid
        self.k = dp._kernel32()
        self.handle = self.k.OpenProcess(dp.PROCESS_ALL_ACCESS, False, pid)
        if not self.handle:
            raise RuntimeError("OpenProcess failed")
        base, _ = dp.exe_base(pid, k=self.k)
        if base is None:
            raise RuntimeError("Cube.exe module not found")
        self.delta = base - IMAGE_BASE
        ups, dms = static_sites(exe_path)
        if len(ups) != 27 or len(dms) != 27:
            raise RuntimeError(f"unexpected call-site count ups={len(ups)} dms={len(dms)}")
        self.gate = GATE_VA + self.delta
        self.upload_sites = {va + self.delta for va in ups}
        self.draw_sites = {va + self.delta for va in dms}
        self.part_sites = self.upload_sites | self.draw_sites
        self.original = {}
        self.player_site = PLAYER_READ_VA + self.delta
        for va in [self.gate, self.player_site, *self.part_sites]:
            b = dp._read(self.handle, va, 1, k=self.k)
            if b is None:
                raise RuntimeError(f"cannot read 0x{va:08X}")
            self.original[va] = b
        if self.original[self.gate] != b"\x0f":
            raise RuntimeError("gate byte mismatch — wrong Cube.exe build?")
        for va in self.part_sites:
            if self.original[va] != b"\xe8":
                raise RuntimeError(f"call byte mismatch at 0x{va:08X}")
        # kind targeting (universal pipeline): capture creatures whose creature+0x64 is in
        # `kinds`; up to `per_kind` distinct creatures of each kind are followed in ONE session.
        self.kinds = set(kinds) if kinds else None
        self.per_kind = per_kind
        self.kind_targets = {}             # creature ptr -> kind (locked once chosen)
        self.kind_of = {}                  # every creature seen -> kind
        self.want_player = target == "player"
        self.player_resolved = False
        self.target = None if target in (None, "player") else target
        self.auto_parts = 0 if self.want_player else auto_parts
        self.max_records = max_records
        self.max_seconds = max_seconds
        self.out = out
        self.armed = set()
        self.records = []
        self.current = None
        self.pending_upload = None
        self.creature_seen = {}

    # --- memory helpers -------------------------------------------------------------
    def _w(self, va, b):
        n = ctypes.c_size_t()
        self.k.WriteProcessMemory(self.handle, ctypes.c_void_p(va), b, 1, ctypes.byref(n))

    def arm(self, va):
        if va not in self.armed:
            self._w(va, b"\xcc")
            self.armed.add(va)

    def disarm(self, va):
        if va in self.armed:
            self._w(va, self.original[va])
            self.armed.discard(va)

    def read(self, va, size):
        return dp._read(self.handle, va, size, k=self.k)

    def floats(self, va, n):
        raw = self.read(va, 4 * n)
        return list(struct.unpack("<%df" % n, raw)) if raw else None

    def u32(self, va):
        return dp._u32(self.handle, va, k=self.k)

    # --- handlers -------------------------------------------------------------------
    def _finish_current(self):
        if self.current is not None and self.current["parts"]:
            self.current.pop("_slots", None)
            self.records.append(self.current)
        self.current = None

    def on_gate(self, ctx):
        creature = ctx.Eax
        self._finish_current()
        idx = dp._i16s(self.handle, creature + 0x8C, k=self.k) or []
        self.creature_seen.setdefault(creature, idx)
        kind_raw = self.read(creature + KIND_OFFSET, 4)
        kind = struct.unpack("<I", kind_raw)[0] if kind_raw else None
        self.kind_of.setdefault(creature, kind)
        if self.kinds is not None and kind in self.kinds and creature not in self.kind_targets:
            if sum(1 for k in self.kind_targets.values() if k == kind) < self.per_kind:
                self.kind_targets[creature] = kind
        if self.target is None and self.auto_parts:
            used = [i for i in idx if i >= 0]
            if len(used) == self.auto_parts:
                self.target = creature
        if creature == self.target or creature in self.kind_targets:
            fields = {}
            for name, off, fmt in CREATURE_FIELDS:
                raw = self.read(creature + off, struct.calcsize(fmt))
                fields[name] = struct.unpack(fmt, raw)[0] if raw else None
            this_raw = self.read(ctx.Edi + THIS_DUMP[0], THIS_DUMP[1])
            cr_raw = self.read(creature + CREATURE_DUMP[0], CREATURE_DUMP[1])
            self.current = {
                "t": time.perf_counter(),
                "creature": f"0x{creature:08X}",
                "kind": kind,
                "this": f"0x{ctx.Edi:08X}",
                "state": struct.unpack("<i", this_raw[8:12])[0] if this_raw else None,
                "indices": idx,
                "fields": fields,
                "this_hdr": this_raw.hex() if this_raw else None,
                "creature_hdr": cr_raw.hex() if cr_raw else None,
                "esp_body": f"0x{ctx.Esp:08X}",
                "frame_check": self.u32(ctx.Esp + FRAME_THIS_OFFSET) == ctx.Edi,
                "placement": None,
                "root": None,
                "parts": [],
            }
            self._esp_body = ctx.Esp
            self._this = ctx.Edi
            for va in self.part_sites:
                self.arm(va)
        else:
            for va in list(self.part_sites):
                self.disarm(va)

    def on_upload(self, ctx, va):
        mptr = self.u32(ctx.Esp)
        if self.current is not None and self.current["placement"] is None:
            # local_14e0 = creature placement (world pos * facing * size/11.2) before any
            # animation; self[0x3b] = root after bob/root rotation. Both read once per draw.
            self.current["placement"] = self.floats(self._esp_body + FRAME_LOCAL_BASE - 0x14E0, 16)
            self.current["root"] = self.floats(self._this + 0x3B * 4, 16)
        self.pending_upload = {
            "upload_site": f"0x{va - self.delta:06X}",
            "matrix": self.floats(mptr, 16) if mptr else None,
        }

    def on_draw(self, ctx, va):
        if self.current is None:
            return
        mesh = ctx.Ecx
        hdr = self.read(mesh + MESH_DUMP[0], MESH_DUMP[1])
        part = dict(self.pending_upload or {})
        part["draw_site"] = f"0x{va - self.delta:06X}"
        part["mesh"] = f"0x{mesh:08X}"
        slots = self.current.get("_slots")
        if slots is None:
            # the slots are filled by the drawModel prologue AFTER the gate, so they are read
            # live at the first draw call of this record (and kept in the record for audit)
            raw = self.read(self._this, THIS_DUMP[1]) or b""
            self.current["this_slots_hdr"] = raw[0xBC:0xF0].hex() if raw else None
            slots = {}
            for idx in MODEL_SLOTS:
                if 4 * idx + 4 <= len(raw):
                    slots.setdefault(struct.unpack_from("<I", raw, 4 * idx)[0], []).append(idx)
            self.current["_slots"] = slots
        hit = slots.get(mesh, [])
        part["slot"] = f"0x{hit[0]:02X}" if len(hit) == 1 else (None if not hit else
                                                                [f"0x{h:02X}" for h in hit])
        part["mesh_hdr"] = hdr.hex() if hdr else None
        self.current["parts"].append(part)
        self.pending_upload = None

    # --- debug loop -----------------------------------------------------------------
    def run(self):
        k = self.k
        attached = False
        event = dp.DEBUG_EVENT()
        step_va = {}
        t_end = time.perf_counter() + self.max_seconds
        try:
            self.arm(self.gate)
            if self.want_player:
                self.arm(self.player_site)
            if not k.DebugActiveProcess(self.pid):
                raise RuntimeError(f"DebugActiveProcess failed: {ctypes.get_last_error()}")
            attached = True
            while len(self.records) < self.max_records and time.perf_counter() < t_end:
                if not k.WaitForDebugEvent(ctypes.byref(event), 2000):
                    continue
                if event.dwDebugEventCode != dp.EXCEPTION_EVENT:
                    k.ContinueDebugEvent(event.dwProcessId, event.dwThreadId, dp.DBG_CONTINUE)
                    continue
                code = event.info.ExceptionCode
                addr = event.info.ExceptionAddress & 0xFFFFFFFF
                if code in (dp.EXCEPTION_BREAKPOINT, dp.WX86_BREAKPOINT) and addr in self.original:
                    th = k.OpenThread(dp.THREAD_ALL_ACCESS, False, event.dwThreadId)
                    ctx = dp.WOW64_CONTEXT()
                    ctx.ContextFlags = 0x10007
                    if k.Wow64GetThreadContext(th, ctypes.byref(ctx)):
                        handled = addr in self.armed
                        if handled:
                            if addr == self.player_site:
                                self.target = self.u32(ctx.Edi + PLAYER_OFFSET)
                                self.player_resolved = True
                            elif addr == self.gate:
                                self.on_gate(ctx)
                            elif addr in self.upload_sites:
                                self.on_upload(ctx, addr)
                            elif addr in self.draw_sites:
                                self.on_draw(ctx, addr)
                        rearm = handled and addr != self.player_site and (
                            addr == self.gate or addr in self.armed_after_gate())
                        # always: put the original byte back and re-execute the instruction
                        if addr in self.armed:
                            self.disarm(addr)
                        else:
                            self._w(addr, self.original[addr])
                        ctx.Eip = addr
                        if rearm:
                            ctx.EFlags |= 0x100
                            step_va[event.dwThreadId] = addr
                        k.Wow64SetThreadContext(th, ctypes.byref(ctx))
                    k.CloseHandle(th)
                    k.ContinueDebugEvent(event.dwProcessId, event.dwThreadId, dp.DBG_CONTINUE)
                    continue
                if code in (dp.SINGLE_STEP, dp.WX86_SINGLE_STEP) and event.dwThreadId in step_va:
                    self._clear_trap(event.dwThreadId)
                    va = step_va.pop(event.dwThreadId)
                    if va == self.gate or self.current is not None:
                        self.arm(va)
                    k.ContinueDebugEvent(event.dwProcessId, event.dwThreadId, dp.DBG_CONTINUE)
                    continue
                if code in (dp.EXCEPTION_BREAKPOINT, dp.WX86_BREAKPOINT):
                    # the loader's attach breakpoint — not ours, harmless to continue
                    k.ContinueDebugEvent(event.dwProcessId, event.dwThreadId, dp.DBG_CONTINUE)
                    continue
                k.ContinueDebugEvent(event.dwProcessId, event.dwThreadId, 0x80010001)
            self._finish_current()
            self._drain(event, step_va)
        finally:
            for va in list(self.original):
                try:
                    self._w(va, self.original[va])
                except Exception:
                    pass
            self.armed.clear()
            if attached:
                try:
                    k.DebugSetProcessKillOnExit(False)
                except Exception:
                    pass
                try:
                    k.DebugActiveProcessStop(self.pid)
                except Exception:
                    pass
            k.CloseHandle(self.handle)
        if self.out:
            import os
            os.makedirs(os.path.dirname(os.path.abspath(self.out)), exist_ok=True)
            with open(self.out, "w", encoding="utf-8") as fh:
                for r in self.records:
                    fh.write(json.dumps(r) + "\n")
        return self.records

    def _clear_trap(self, tid):
        k = self.k
        th = k.OpenThread(dp.THREAD_ALL_ACCESS, False, tid)
        ctx = dp.WOW64_CONTEXT()
        ctx.ContextFlags = 0x10007
        if k.Wow64GetThreadContext(th, ctypes.byref(ctx)):
            ctx.EFlags &= ~0x100
            k.Wow64SetThreadContext(th, ctypes.byref(ctx))
        k.CloseHandle(th)

    def _rewind(self, tid, addr):
        k = self.k
        th = k.OpenThread(dp.THREAD_ALL_ACCESS, False, tid)
        ctx = dp.WOW64_CONTEXT()
        ctx.ContextFlags = 0x10007
        if k.Wow64GetThreadContext(th, ctypes.byref(ctx)):
            ctx.Eip = addr
            ctx.EFlags &= ~0x100
            k.Wow64SetThreadContext(th, ctypes.byref(ctx))
        k.CloseHandle(th)

    def _drain(self, event, step_va):
        """Restore every byte FIRST, then finish in-flight events so nothing is left behind.

        A breakpoint already raised but not yet delivered is rewound (EIP = address, original
        byte back), a pending single-step gets its trap flag cleared. Stops after 400 ms of
        silence with no single-step outstanding.
        """
        k = self.k
        for va in list(self.original):
            self._w(va, self.original[va])
        self.armed.clear()
        deadline = time.perf_counter() + 3.0
        while time.perf_counter() < deadline:
            if not k.WaitForDebugEvent(ctypes.byref(event), 400):
                if not step_va:
                    break
                continue
            status = dp.DBG_CONTINUE
            if event.dwDebugEventCode == dp.EXCEPTION_EVENT:
                code = event.info.ExceptionCode
                addr = event.info.ExceptionAddress & 0xFFFFFFFF
                if code in (dp.EXCEPTION_BREAKPOINT, dp.WX86_BREAKPOINT) and addr in self.original:
                    self._rewind(event.dwThreadId, addr)
                    step_va.pop(event.dwThreadId, None)
                elif code in (dp.SINGLE_STEP, dp.WX86_SINGLE_STEP):
                    self._clear_trap(event.dwThreadId)
                    step_va.pop(event.dwThreadId, None)
                elif code not in (dp.EXCEPTION_BREAKPOINT, dp.WX86_BREAKPOINT):
                    status = 0x80010001
            k.ContinueDebugEvent(event.dwProcessId, event.dwThreadId, status)

    def armed_after_gate(self):
        return self.part_sites if self.current is not None else set()


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("pid", type=int)
    ap.add_argument("out")
    ap.add_argument("--exe", default=r"D:\Projects\CubeGame\.scratch\alphatest2\Cube.exe")
    ap.add_argument("--target", default="player",
                    help="'player' (default: local player via gameController+0x8006D0), "
                         "'auto' (first creature with --auto-parts slots) or a hex pointer")
    ap.add_argument("--auto-parts", type=int, default=5,
                    help="auto-select the first creature with this many model slots (5 = human)")
    ap.add_argument("--kinds", default=None,
                    help="universal pipeline: comma list of creature kinds (creature+0x64), e.g. "
                         "0x20,0x1e; captures up to --per-kind creatures of each kind in one "
                         "session instead of --target")
    ap.add_argument("--per-kind", type=int, default=1)
    ap.add_argument("--records", type=int, default=400)
    ap.add_argument("--seconds", type=float, default=30.0)
    a = ap.parse_args(argv)
    kinds = [int(k, 0) for k in a.kinds.split(",")] if a.kinds else None
    if kinds:
        target = "kinds"
    else:
        target = a.target if a.target == "player" else (None if a.target == "auto" else int(a.target, 16))
    cap = Capture(a.pid, a.exe, target=None if kinds else target,
                  max_records=a.records, max_seconds=a.seconds, out=a.out,
                  auto_parts=0 if kinds else a.auto_parts, kinds=kinds, per_kind=a.per_kind)
    recs = cap.run()
    print(f"POSE_CAPTURE records={len(recs)} player_resolved={cap.player_resolved} target="
          f"{'0x%08X' % cap.target if cap.target else None} creatures_seen={len(cap.creature_seen)}")
    for c, idx in cap.creature_seen.items():
        mark = " TARGET" if c in cap.kind_targets else ""
        kind = cap.kind_of.get(c)
        print(f"  creature 0x{c:08X} kind={kind if kind is None else hex(kind)} indices={idx}{mark}")
    if recs:
        print(f"  parts/record={len(recs[0]['parts'])} state0={recs[0]['state']}")


if __name__ == "__main__":
    main()
