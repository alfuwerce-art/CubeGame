"""Find the code that WRITES a given address in the running local Alpha copy (hardware watchpoint).

Used to locate the animation-state resolver: the drawModel ``this`` fields state (``this+8``) and
timer (``this+4``) are set outside ``Creature_drawModel``. Debug registers DR0/DR1 of every
thread get a 4-byte write watch; each hit reports the address of the instruction AFTER the
write (x86 data breakpoints are traps). No byte of the process is modified; the registers are
cleared on every exit path and the debugger detaches without killing the game.

    python -m tools.alpha_models.runtime.write_watch <pid> <addr_hex> [<addr2_hex>] --seconds 20
"""
from __future__ import annotations

import argparse
import ctypes
import ctypes.wintypes as wt
import json
import time

from tools.alpha_models.runtime import drawmodel_probe as dp

TH32CS_SNAPTHREAD = 0x4
CTX_DEBUG = 0x00010010          # WOW64_CONTEXT_i386 | CONTEXT_DEBUG_REGISTERS
CTX_FULL = 0x10007 | 0x10


class THREADENTRY32(ctypes.Structure):
    _fields_ = [("dwSize", wt.DWORD), ("cntUsage", wt.DWORD), ("th32ThreadID", wt.DWORD),
                ("th32OwnerProcessID", wt.DWORD), ("tpBasePri", wt.LONG), ("tpDeltaPri", wt.LONG),
                ("dwFlags", wt.DWORD)]


def threads(pid, k):
    k.Thread32First.argtypes = (wt.HANDLE, ctypes.POINTER(THREADENTRY32))
    k.Thread32Next.argtypes = (wt.HANDLE, ctypes.POINTER(THREADENTRY32))
    snap = k.CreateToolhelp32Snapshot(TH32CS_SNAPTHREAD, 0)
    te = THREADENTRY32()
    te.dwSize = ctypes.sizeof(te)
    out = []
    ok = k.Thread32First(snap, ctypes.byref(te))
    while ok:
        if te.th32OwnerProcessID == pid:
            out.append(te.th32ThreadID)
        ok = k.Thread32Next(snap, ctypes.byref(te))
    k.CloseHandle(snap)
    return out


def set_watch(k, tid, addrs):
    th = k.OpenThread(dp.THREAD_ALL_ACCESS, False, tid)
    if not th:
        return False
    ctx = dp.WOW64_CONTEXT()
    ctx.ContextFlags = CTX_DEBUG
    ok = bool(k.Wow64GetThreadContext(th, ctypes.byref(ctx)))
    if ok:
        dr7 = 0
        for i, a in enumerate(addrs[:4]):
            setattr(ctx, f"Dr{i}", a or 0)
            if a:
                dr7 |= (1 << (2 * i)) | (0b01 << (16 + 4 * i)) | (0b11 << (18 + 4 * i))   # write, 4 bytes
        ctx.Dr7 = dr7
        ctx.Dr6 = 0
        ok = bool(k.Wow64SetThreadContext(th, ctypes.byref(ctx)))
    k.CloseHandle(th)
    return ok


def watch(pid, addrs, seconds=20.0, max_hits=200):
    k = dp._kernel32()
    hits = []
    event = dp.DEBUG_EVENT()
    attached = False
    tids = []
    try:
        if not k.DebugActiveProcess(pid):
            raise RuntimeError(f"DebugActiveProcess failed: {ctypes.get_last_error()}")
        attached = True
        tids = threads(pid, k)
        armed = sum(set_watch(k, t, addrs) for t in tids)
        t_end = time.perf_counter() + seconds
        while time.perf_counter() < t_end and len(hits) < max_hits:
            if not k.WaitForDebugEvent(ctypes.byref(event), 500):
                continue
            if event.dwDebugEventCode == dp.EXCEPTION_EVENT and event.info.ExceptionCode in (dp.SINGLE_STEP, dp.WX86_SINGLE_STEP):
                th = k.OpenThread(dp.THREAD_ALL_ACCESS, False, event.dwThreadId)
                ctx = dp.WOW64_CONTEXT()
                ctx.ContextFlags = CTX_FULL
                if k.Wow64GetThreadContext(th, ctypes.byref(ctx)):
                    which = [i for i in range(4) if ctx.Dr6 & (1 << i)]
                    hits.append({"eip_after": f"0x{ctx.Eip:08X}", "dr": which, "eax": f"0x{ctx.Eax:08X}",
                                 "ecx": f"0x{ctx.Ecx:08X}", "edx": f"0x{ctx.Edx:08X}", "esi": f"0x{ctx.Esi:08X}",
                                 "edi": f"0x{ctx.Edi:08X}", "t": time.perf_counter()})
                    ctx.Dr6 = 0
                    k.Wow64SetThreadContext(th, ctypes.byref(ctx))
                k.CloseHandle(th)
            elif event.dwDebugEventCode == 2:          # CREATE_THREAD: arm the new thread too
                set_watch(k, event.dwThreadId, addrs)
            k.ContinueDebugEvent(event.dwProcessId, event.dwThreadId, dp.DBG_CONTINUE)
    finally:
        for t in threads(pid, k) if attached else tids:
            try:
                set_watch(k, t, [0, 0, 0, 0])
            except Exception:
                pass
        # drain: a data breakpoint may have trapped on another thread between the last wait and
        # the clearing above; an un-continued single-step after detach would kill the game
        if attached:
            for _ in range(200):
                if not k.WaitForDebugEvent(ctypes.byref(event), 200):
                    break
                if event.dwDebugEventCode == dp.EXCEPTION_EVENT and event.info.ExceptionCode in (dp.SINGLE_STEP, dp.WX86_SINGLE_STEP):
                    set_watch(k, event.dwThreadId, [0, 0, 0, 0])
                k.ContinueDebugEvent(event.dwProcessId, event.dwThreadId, dp.DBG_CONTINUE)
        if attached:
            try:
                k.DebugSetProcessKillOnExit(False)
                k.DebugActiveProcessStop(pid)
            except Exception:
                pass
    return hits


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("pid", type=int)
    ap.add_argument("addrs", nargs="+")
    ap.add_argument("--seconds", type=float, default=20.0)
    ap.add_argument("--out", default=None)
    a = ap.parse_args(argv)
    base, _ = dp.exe_base(a.pid)
    hits = watch(a.pid, [int(x, 16) for x in a.addrs], a.seconds)
    delta = (base or 0x400000) - 0x400000
    for h in hits:
        h["eip_after_image"] = f"0x{int(h['eip_after'], 16) - delta:08X}"
    sites = {}
    for h in hits:
        sites.setdefault(h["eip_after_image"], 0)
        sites[h["eip_after_image"]] += 1
    print(json.dumps({"WRITE_WATCH_HITS": len(hits), "module_base": f"0x{(base or 0):08X}", "sites": sites}, indent=1))
    if a.out:
        with open(a.out, "w", encoding="utf-8") as fh:
            json.dump(hits, fh, indent=1)


if __name__ == "__main__":
    main()
