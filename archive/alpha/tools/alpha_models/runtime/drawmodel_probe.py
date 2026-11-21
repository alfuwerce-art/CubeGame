"""Attach to a running Cube.exe and dump the real creature pointer and its model indices.

Breakpoint = ``Creature_drawModel`` RVA ``0x12A42``::

    00412A40  8b 07                 mov  eax,[edi]               ; eax = *this = creature
    00412A42  0f bf 88 8c 00 00 00  movsx ecx, word ptr [eax+0x8c]

At that instruction the registers are meaningful and stable::

    eax = creature pointer      (reloaded by `mov eax,[edi]` right before)
    edi = this                  (the per-frame draw-model object)
    esi = param_4               (the global model vector; `mov esi,[ebp+0x14]` in the prologue)

The probe reads the eight ``int16`` model indices at ``creature+0x8c..+0x9a``, the raw
appearance fields, and resolves each index through the model vector (``[esi+4]`` is the
vector data, ``[esi+8]`` its end) into a model pointer.

It is read-only apart from the single INT3 byte it places and restores in the debuggee.

WOW64 facts this relies on (validated by ``self_check``):
  * ``DEBUG_EVENT`` is 8-byte aligned, so the exception record starts at offset 16;
  * a 64-bit debugger sees INT3 in 32-bit code as ``STATUS_WX86_BREAKPOINT``
    (``0x4000001F``) and single-step as ``0x4000001E``.

Detach is clean: the original byte is restored, the in-flight single-step is completed
(trap flag cleared) before detaching, and ``DebugSetProcessKillOnExit(FALSE)`` is set so the
debuggee is not killed when the probe exits.
"""
from __future__ import annotations

import ctypes
import ctypes.wintypes as wt
import json
import struct
import sys

PROCESS_ALL_ACCESS = 0x1F0FFF
THREAD_ALL_ACCESS = 0x1F03FF
DBG_CONTINUE = 0x00010002
EXCEPTION_BREAKPOINT = 0x80000003
WX86_BREAKPOINT = 0x4000001F
SINGLE_STEP = 0x80000004
WX86_SINGLE_STEP = 0x4000001E
EXCEPTION_EVENT = 1

# RVA of `movsx ecx, word ptr [eax+0x8c]` inside Creature_drawModel (see model_read_scan).
MODEL_READ_RVA = 0x12A42
DEFAULT_MAX_HITS = 40


class EXCEPTION_DEBUG_INFO(ctypes.Structure):
    _fields_ = [("ExceptionCode", wt.DWORD), ("ExceptionFlags", wt.DWORD),
                ("ExceptionRecord", ctypes.c_void_p), ("ExceptionAddress", ctypes.c_ulonglong),
                ("NumberParameters", wt.DWORD), ("pad", wt.DWORD),
                ("ExceptionInformation", ctypes.c_ulonglong * 15), ("dwFirstChance", wt.DWORD)]


class DEBUG_EVENT(ctypes.Structure):
    # On WOW64 the union is 8-byte aligned, so the exception record starts at offset 16
    # (after an explicit pad dword), not offset 12.
    _fields_ = [("dwDebugEventCode", wt.DWORD), ("dwProcessId", wt.DWORD),
                ("dwThreadId", wt.DWORD), ("alignment_pad", wt.DWORD),
                ("info", EXCEPTION_DEBUG_INFO)]


class WOW64_CONTEXT(ctypes.Structure):
    _fields_ = [("ContextFlags", wt.DWORD),
                ("Dr0", wt.DWORD), ("Dr1", wt.DWORD), ("Dr2", wt.DWORD), ("Dr3", wt.DWORD),
                ("Dr6", wt.DWORD), ("Dr7", wt.DWORD), ("FloatSave", ctypes.c_byte * 112),
                ("SegGs", wt.DWORD), ("SegFs", wt.DWORD), ("SegEs", wt.DWORD), ("SegDs", wt.DWORD),
                ("Edi", wt.DWORD), ("Esi", wt.DWORD), ("Ebx", wt.DWORD), ("Edx", wt.DWORD),
                ("Ecx", wt.DWORD), ("Eax", wt.DWORD), ("Ebp", wt.DWORD), ("Eip", wt.DWORD),
                ("SegCs", wt.DWORD), ("EFlags", wt.DWORD), ("Esp", wt.DWORD), ("SegSs", wt.DWORD),
                ("ExtendedRegisters", ctypes.c_byte * 512)]


class MODULEENTRY32(ctypes.Structure):
    _fields_ = [("dwSize", wt.DWORD), ("th32ModuleID", wt.DWORD), ("th32ProcessID", wt.DWORD),
                ("GlblcntUsage", wt.DWORD), ("ProccntUsage", wt.DWORD),
                ("modBaseAddr", ctypes.c_void_p), ("modBaseSize", wt.DWORD),
                ("hModule", wt.HMODULE), ("szModule", ctypes.c_wchar * 256),
                ("szExePath", ctypes.c_wchar * 260)]


def self_check() -> dict:
    """Validate the hard-won WOW64 layout assumptions without touching any process."""
    return {
        "debug_event_size": ctypes.sizeof(DEBUG_EVENT),
        "exception_record_offset": DEBUG_EVENT.info.offset,
        "exception_record_is_16": DEBUG_EVENT.info.offset == 16,
        "wow64_context_eax_offset": WOW64_CONTEXT.Eax.offset,
        "wow64_context_esp_offset": WOW64_CONTEXT.Esp.offset,
    }


_k32 = None


def _kernel32():
    global _k32
    if _k32 is None:
        k = ctypes.WinDLL("kernel32", use_last_error=True)
        k.OpenProcess.restype = wt.HANDLE
        k.OpenProcess.argtypes = (wt.DWORD, wt.BOOL, wt.DWORD)
        k.ReadProcessMemory.argtypes = (wt.HANDLE, ctypes.c_void_p, ctypes.c_void_p,
                                        ctypes.c_size_t, ctypes.POINTER(ctypes.c_size_t))
        k.WriteProcessMemory.argtypes = (wt.HANDLE, ctypes.c_void_p, ctypes.c_void_p,
                                         ctypes.c_size_t, ctypes.POINTER(ctypes.c_size_t))
        k.CreateToolhelp32Snapshot.restype = wt.HANDLE
        k.CreateToolhelp32Snapshot.argtypes = (wt.DWORD, wt.DWORD)
        k.Module32FirstW.argtypes = (wt.HANDLE, ctypes.POINTER(MODULEENTRY32))
        k.Module32NextW.argtypes = (wt.HANDLE, ctypes.POINTER(MODULEENTRY32))
        k.DebugActiveProcess.argtypes = (wt.DWORD,)
        k.DebugActiveProcessStop.argtypes = (wt.DWORD,)
        k.DebugSetProcessKillOnExit.argtypes = (wt.BOOL,)
        k.WaitForDebugEvent.argtypes = (ctypes.POINTER(DEBUG_EVENT), wt.DWORD)
        k.ContinueDebugEvent.argtypes = (wt.DWORD, wt.DWORD, wt.DWORD)
        k.OpenThread.restype = wt.HANDLE
        k.OpenThread.argtypes = (wt.DWORD, wt.BOOL, wt.DWORD)
        k.Wow64GetThreadContext.argtypes = (wt.HANDLE, ctypes.POINTER(WOW64_CONTEXT))
        k.Wow64SetThreadContext.argtypes = (wt.HANDLE, ctypes.POINTER(WOW64_CONTEXT))
        k.CloseHandle.argtypes = (wt.HANDLE,)
        _k32 = k
    return _k32


def _read(handle, address, size, k=None):
    if not address:
        return None
    k = k or _kernel32()
    buffer = ctypes.create_string_buffer(size)
    got = ctypes.c_size_t()
    if not k.ReadProcessMemory(handle, ctypes.c_void_p(address), buffer, size, ctypes.byref(got)):
        return None
    if got.value != size:
        return None
    return buffer.raw[:got.value]


def _u32(handle, address, k=None):
    raw = _read(handle, address, 4, k=k)
    return struct.unpack("<I", raw)[0] if raw else None


def _i16s(handle, address, count=8, k=None):
    raw = _read(handle, address, count * 2, k=k)
    return list(struct.unpack("<%dh" % count, raw)) if raw else None


def exe_base(pid, k=None):
    k = k or _kernel32()
    snap = k.CreateToolhelp32Snapshot(0x8, pid)
    entry = MODULEENTRY32()
    entry.dwSize = ctypes.sizeof(MODULEENTRY32)
    try:
        if k.Module32FirstW(snap, ctypes.byref(entry)):
            while True:
                if entry.th32ProcessID == pid:
                    return entry.modBaseAddr, entry.szModule
                if not k.Module32NextW(snap, ctypes.byref(entry)):
                    break
    finally:
        k.CloseHandle(snap)
    return None, None


def capture(handle, ctx, k=None) -> dict:
    """Build a plain dict from the registers at the breakpoint (no process writes)."""
    creature = ctx.Eax
    self_ptr = ctx.Edi
    vector = ctx.Esi
    entry = {
        "eip": f"0x{ctx.Eip:08X}",
        "esp": f"0x{ctx.Esp:08X}",
        "regs": {
            "eax_creature": f"0x{creature:08X}",
            "edi_this": f"0x{self_ptr:08X}",
            "esi_model_vector": f"0x{vector:08X}",
            "ecx": f"0x{ctx.Ecx:08X}",
            "edx": f"0x{ctx.Edx:08X}",
            "ebx": f"0x{ctx.Ebx:08X}",
            "ebp": f"0x{ctx.Ebp:08X}",
        },
        "creature": {"ptr": f"0x{creature:08X}"},
    }
    entry["creature"]["indices_8c_9a"] = _i16s(handle, creature + 0x8C, k=k)
    entry["creature"]["ids_14_22"] = _i16s(handle, creature + 0x14, k=k)
    entry["creature"]["ids_2c_22"] = _i16s(handle, creature + 0x2C + 0x14, k=k)
    begin = _u32(handle, vector + 4, k=k)
    end = _u32(handle, vector + 8, k=k)
    entry["model_vector"] = {
        "ptr": f"0x{vector:08X}",
        "begin": None if begin is None else f"0x{begin:08X}",
        "end": None if end is None else f"0x{end:08X}",
        "count": None if (begin is None or end is None) else (end - begin) // 4,
    }
    resolved = {}
    for slot, idx in enumerate(entry["creature"]["indices_8c_9a"] or []):
        if idx < 0 or begin is None:
            resolved[slot] = {"index": idx, "model_ptr": None}
            continue
        ptr = _u32(handle, begin + idx * 4, k=k)
        resolved[slot] = {"index": idx, "model_ptr": None if ptr is None else f"0x{ptr:08X}"}
    entry["model_vector"]["resolved_by_slot"] = resolved
    return entry


def run_probe(pid, max_hits=DEFAULT_MAX_HITS, on_hit=None, out=None, kernel=None,
              base=None, module=None):
    """Run the debug loop. Returns (hits, events). ``on_hit(dict)`` is called per capture.

    ``kernel`` may be a fake for unit tests; ``base``/``module`` may be supplied to bypass
    module enumeration. The INT3 byte is always restored in ``finally`` — including when
    ``DebugActiveProcess`` fails or an exception escapes the debug loop — and the detach
    calls are only made when the debugger was actually attached.
    """
    k = kernel or _kernel32()
    handle = k.OpenProcess(PROCESS_ALL_ACCESS, False, pid)
    if not handle:
        raise RuntimeError(f"OpenProcess failed: {ctypes.get_last_error()}")
    if base is None:
        base, module = exe_base(pid, k=k)
    if base is None:
        k.CloseHandle(handle)
        raise RuntimeError("Cube.exe module not found in the process")
    target = base + MODEL_READ_RVA
    original = _read(handle, target, 1, k=k)
    if original is None or original[0] != 0x0F:
        k.CloseHandle(handle)
        raise RuntimeError(f"unexpected byte at 0x{target:08X} (expected 0x0F movsx/movzx opcode)")

    written = ctypes.c_size_t()
    hits, seen = [], []
    attached = False
    armed = True
    pending_step = False
    event = DEBUG_EVENT()
    try:
        k.WriteProcessMemory(handle, ctypes.c_void_p(target), b"\xcc", 1, ctypes.byref(written))
        if not k.DebugActiveProcess(pid):
            raise RuntimeError(f"DebugActiveProcess failed: {ctypes.get_last_error()}")
        attached = True
        while True:
            if not k.WaitForDebugEvent(ctypes.byref(event), 4000):
                break
            if event.dwDebugEventCode != EXCEPTION_EVENT:
                k.ContinueDebugEvent(event.dwProcessId, event.dwThreadId, DBG_CONTINUE)
                continue
            record = event.info
            code = record.ExceptionCode
            address = record.ExceptionAddress & 0xFFFFFFFF
            if len(seen) < 8:
                seen.append(f"{code:#x}@{address:#x}")
            if code in (EXCEPTION_BREAKPOINT, WX86_BREAKPOINT) and address == target and armed:
                thread = k.OpenThread(THREAD_ALL_ACCESS, False, event.dwThreadId)
                context = WOW64_CONTEXT()
                context.ContextFlags = 0x10007
                if k.Wow64GetThreadContext(thread, ctypes.byref(context)):
                    entry = capture(handle, context, k=k)
                    hits.append(entry)
                    if on_hit:
                        on_hit(entry)
                    k.WriteProcessMemory(handle, ctypes.c_void_p(target), original, 1,
                                         ctypes.byref(written))
                    context.Eip = target
                    context.EFlags |= 0x100
                    k.Wow64SetThreadContext(thread, ctypes.byref(context))
                    armed = False
                    pending_step = True
                k.CloseHandle(thread)
                k.ContinueDebugEvent(event.dwProcessId, event.dwThreadId, DBG_CONTINUE)
                continue
            if code in (SINGLE_STEP, WX86_SINGLE_STEP) and pending_step:
                thread = k.OpenThread(THREAD_ALL_ACCESS, False, event.dwThreadId)
                context = WOW64_CONTEXT()
                context.ContextFlags = 0x10007
                if k.Wow64GetThreadContext(thread, ctypes.byref(context)):
                    context.EFlags &= ~0x100
                    k.Wow64SetThreadContext(thread, ctypes.byref(context))
                k.CloseHandle(thread)
                pending_step = False
                if len(hits) < max_hits:
                    k.WriteProcessMemory(handle, ctypes.c_void_p(target), b"\xcc", 1,
                                         ctypes.byref(written))
                    armed = True
                else:
                    k.ContinueDebugEvent(event.dwProcessId, event.dwThreadId, DBG_CONTINUE)
                    break
                k.ContinueDebugEvent(event.dwProcessId, event.dwThreadId, DBG_CONTINUE)
                continue
            k.ContinueDebugEvent(event.dwProcessId, event.dwThreadId, DBG_CONTINUE)
    finally:
        # Always restore the original instruction byte, even on abnormal termination.
        try:
            k.WriteProcessMemory(handle, ctypes.c_void_p(target), original, 1,
                                 ctypes.byref(written))
        except Exception:
            pass
        if attached:
            try:
                k.DebugSetProcessKillOnExit(False)
            except Exception:
                pass
            try:
                k.DebugActiveProcessStop(pid)
            except Exception:
                pass
        try:
            k.CloseHandle(handle)
        except Exception:
            pass
    if out:
        with open(out, "w", encoding="utf-8") as fh:
            json.dump({"hits": hits, "events": seen, "module": module,
                       "base": f"0x{base:08X}", "target": f"0x{target:08X}"}, fh, indent=1)
    return hits, seen


def main():
    if len(sys.argv) < 3:
        raise SystemExit(f"usage: {sys.argv[0]} <pid> <out.json> [max_hits]")
    pid = int(sys.argv[1])
    out = sys.argv[2]
    max_hits = int(sys.argv[3]) if len(sys.argv) > 3 else DEFAULT_MAX_HITS
    hits, seen = run_probe(pid, max_hits=max_hits, out=out)
    print(f"hits={len(hits)} events={seen}")


if __name__ == "__main__":
    main()
