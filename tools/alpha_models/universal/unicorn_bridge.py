"""Run an oracle's emulated state on unicorn (native QEMU x86) instead of the pure-Python interpreter.

The Python emulator (x86emu/drawmodel_emu) stays the reference: it builds the state (synthetic world, heap, stack,
stubs, imports) and this bridge continues from it, typically ~1000x faster. What is carried over:
  * memory: the PE image (raw sections), the image overlay (`mem.over`, writes into globals) and every mapped region;
  * registers: general purpose, eflags bits, xmm (128-bit); the x87 stack must be empty (true at call boundaries);
  * FS segment over the synthetic TEB (MSVC SEH frames read fs:[0]) via a flat GDT;
  * stubs (`cpu.stubs`: VA -> (fn, cleanup)), hooks (`cpu.hooks`: VA -> fn) and IAT imports (`cpu.iat`) — each is a
    unicorn code hook at its VA that calls the same Python function through a small CPU facade.
A stub that leaves a float on the facade's x87 list is returned in ST0 through a code cave `fld qword [scratch]; ret`.
Status of results: EMULATED_ALPHA_CODE, the same as the Python interpreter (only the executor changes).
"""
from __future__ import annotations

import struct

from unicorn import Uc, UcError, UC_ARCH_X86, UC_MODE_32, UC_HOOK_CODE, UC_HOOK_MEM_INVALID
from unicorn.x86_const import (
    UC_X86_REG_EAX, UC_X86_REG_EBX, UC_X86_REG_ECX, UC_X86_REG_EDX, UC_X86_REG_ESI, UC_X86_REG_EDI,
    UC_X86_REG_EBP, UC_X86_REG_ESP, UC_X86_REG_EIP, UC_X86_REG_EFLAGS, UC_X86_REG_GDTR, UC_X86_REG_FS,
    UC_X86_REG_DS, UC_X86_REG_ES, UC_X86_REG_SS, UC_X86_REG_CS, UC_X86_REG_XMM0,
)

MASK = 0xFFFFFFFF
PAGE = 0x1000
REGS = {"eax": UC_X86_REG_EAX, "ebx": UC_X86_REG_EBX, "ecx": UC_X86_REG_ECX, "edx": UC_X86_REG_EDX,
        "esi": UC_X86_REG_ESI, "edi": UC_X86_REG_EDI, "ebp": UC_X86_REG_EBP, "esp": UC_X86_REG_ESP}
TRAMPOLINES = 0x7E000000      # one 16-byte slot per IAT import
CAVE = 0x7E100000             # fld qword [CAVE + 0x100]; ret
GDT = 0x7E200000


def _page_down(v):
    return v & ~(PAGE - 1)


def _page_up(v):
    return (v + PAGE - 1) & ~(PAGE - 1)


class _Regs:
    def __init__(self, uc):
        self.uc = uc

    def __getitem__(self, k):
        return self.uc.reg_read(REGS[k]) & MASK

    def __setitem__(self, k, v):
        self.uc.reg_write(REGS[k], v & MASK)


class _Xmm:
    def __init__(self, uc):
        self.uc = uc

    def __getitem__(self, i):
        return self.uc.reg_read(UC_X86_REG_XMM0 + i)

    def __setitem__(self, i, v):
        self.uc.reg_write(UC_X86_REG_XMM0 + i, v)


class UcMem:
    """The RegionMemory API (read/write/u/w) over unicorn memory."""

    def __init__(self, uc):
        self.uc = uc

    def read(self, va, n):
        return bytes(self.uc.mem_read(va & MASK, n))

    def write(self, va, data):
        self.uc.mem_write(va & MASK, bytes(data))

    def u(self, va, size):
        return int.from_bytes(self.read(va, size), "little")

    def w(self, va, size, value):
        self.write(va, (value & ((1 << (8 * size)) - 1)).to_bytes(size, "little"))


class _Facade:
    """What oracle stubs/hooks touch: mem, r, xmm, fpu (list, results only), push/pop, arg, steps."""

    def __init__(self, uc, mem):
        self.uc = uc
        self.mem = mem
        self.r = _Regs(uc)
        self.xmm = _Xmm(uc)
        self.fpu = []
        self.steps = 0

    @property
    def eip(self):
        return self.uc.reg_read(UC_X86_REG_EIP)

    def push(self, v):
        esp = (self.r["esp"] - 4) & MASK
        self.r["esp"] = esp
        self.mem.w(esp, 4, v)

    def pop(self):
        esp = self.r["esp"]
        v = self.mem.u(esp, 4)
        self.r["esp"] = esp + 4
        return v

    def arg(self, i):
        return self.mem.u(self.r["esp"] + 4 + 4 * i, 4)


class UnicornRun:
    def __init__(self, cpu, image):
        """cpu: the oracle's EmuCPU after its state is set up (registers, regions, stubs, hooks, iat)."""
        if cpu.fpu:
            raise ValueError("x87 stack must be empty when switching executors")
        uc = Uc(UC_ARCH_X86, UC_MODE_32)
        self.uc = uc
        self.mem = UcMem(uc)
        self.cpu = _Facade(uc, self.mem)
        self.stop_exc = None
        self.last_return = 0          # return address of the import/stub being executed (call-site tracing)
        # PE image
        lo = image.image_base
        hi = max(lo + s.virtual_address + max(s.virtual_size, s.raw_size) for s in image.sections)
        uc.mem_map(lo, _page_up(hi - lo))
        for s in image.sections:
            raw = image.data[s.raw_offset:s.raw_offset + min(s.raw_size, s.virtual_size or s.raw_size)]
            uc.mem_write(lo + s.virtual_address, bytes(raw))
        for va, b in cpu.mem.over.items():
            uc.mem_write(va, bytes([b]))
        # regions (stack, world, heap, TEB, section tails); tails overlap the image mapping -> write only
        for base, end, buf in cpu.mem.regions:
            if lo <= base < lo + _page_up(hi - lo):
                uc.mem_write(base, bytes(buf))
                continue
            uc.mem_map(_page_down(base), _page_up(end) - _page_down(base))
            uc.mem_write(base, bytes(buf))
        # registers
        for k, reg in REGS.items():
            uc.reg_write(reg, cpu.r[k] & MASK)
        flags = 0x2 | (cpu.cf << 0) | (getattr(cpu, "pf", False) << 2) | (cpu.zf << 6) | (cpu.sf << 7) \
            | (getattr(cpu, "df", False) << 10) | (cpu.of << 11)
        uc.reg_write(UC_X86_REG_EFLAGS, flags)
        for i, v in enumerate(cpu.xmm):
            uc.reg_write(UC_X86_REG_XMM0 + i, v)
        uc.reg_write(UC_X86_REG_EIP, cpu.eip)
        self._setup_fs(cpu)
        # code cave for float results
        uc.mem_map(CAVE, PAGE)
        uc.mem_write(CAVE, bytes([0xDD, 0x05]) + struct.pack("<I", CAVE + 0x100) + bytes([0xC3]))
        # imports: IAT slots -> trampolines
        uc.mem_map(TRAMPOLINES, _page_up(16 * max(1, len(cpu.iat))))
        self.calls = {}
        for i, (slot, fn) in enumerate(sorted(cpu.iat.items())):
            t = TRAMPOLINES + 16 * i
            uc.mem_write(t, b"\xC3")
            uc.mem_write(slot, struct.pack("<I", t))
            self.calls[t] = ("iat", fn, 0)
        for va, (fn, cleanup) in cpu.stubs.items():
            self.calls[va] = ("stub", fn, cleanup)
        self.hooks = dict(cpu.hooks)
        for va in set(self.calls) | set(self.hooks):
            uc.hook_add(UC_HOOK_CODE, self._on_code, begin=va, end=va)
        uc.hook_add(UC_HOOK_MEM_INVALID, self._on_invalid)

    def _setup_fs(self, cpu):
        teb = 0x7FFD0000
        uc = self.uc
        uc.mem_map(GDT, PAGE)

        def desc(base, limit, access, flags=0xC):
            return struct.pack("<Q", (limit & 0xFFFF) | ((base & 0xFFFFFF) << 16) | (access << 40)
                               | (((limit >> 16) & 0xF) << 48) | (flags << 52) | (((base >> 24) & 0xFF) << 56))
        gdt = b"\0" * 8 + desc(0, 0xFFFFF, 0x9B) + desc(0, 0xFFFFF, 0x93) + desc(teb, 0xFFF, 0x93, 0x4)
        uc.mem_write(GDT, gdt)
        uc.reg_write(UC_X86_REG_GDTR, (0, GDT, len(gdt) - 1, 0))
        # Once a GDT is loaded every segment is resolved through it: give CS/SS/DS/ES the flat descriptors,
        # otherwise the null selectors produce shifted addresses (seen as stack writes near 0).
        uc.reg_write(UC_X86_REG_CS, 1 << 3)
        uc.reg_write(UC_X86_REG_SS, 2 << 3)
        uc.reg_write(UC_X86_REG_DS, 2 << 3)
        uc.reg_write(UC_X86_REG_ES, 2 << 3)
        uc.reg_write(UC_X86_REG_FS, 3 << 3)

    def _on_invalid(self, uc, access, address, size, value, user):
        self.stop_exc = RuntimeError(f"unicorn: invalid memory access 0x{address:08X} (size {size}) "
                                     f"at eip 0x{uc.reg_read(UC_X86_REG_EIP):08X}")
        return False

    def _on_code(self, uc, address, size, user):
        try:
            hook = self.hooks.get(address)
            if hook is not None:
                hook(self.cpu)
            call = self.calls.get(address)
            if call is None:
                return
            kind, fn, cleanup = call
            self.cpu.fpu = []
            if kind == "iat":
                # The interpreter runs `call [IAT]` without pushing a return address (args at esp+0) and via_iat
                # pops the stdcall bytes itself; here `call` reached the trampoline, so take the return first.
                ret = self.cpu.pop()
                self.last_return = ret
                result = fn(self.cpu)
                uc.reg_write(UC_X86_REG_EAX, (result or 0) & MASK)
                self._resume(ret, 0)
            else:
                self.last_return = self.mem.u(self.cpu.r["esp"], 4)
                fn(self.cpu)
                ret = self.cpu.pop()
                self._resume(ret, cleanup)
        except BaseException as e:  # noqa: BLE001 — oracle hooks stop runs by raising
            self.stop_exc = e
            uc.emu_stop()

    def _resume(self, ret, cleanup):
        self.cpu.r["esp"] = self.cpu.r["esp"] + cleanup
        if self.cpu.fpu:
            if len(self.cpu.fpu) != 1:
                raise RuntimeError("stub left more than one x87 value")
            self.mem.write(CAVE + 0x100, struct.pack("<d", float(self.cpu.fpu[0])))
            self.cpu.push(ret)
            self.uc.reg_write(UC_X86_REG_EIP, CAVE)
        else:
            self.uc.reg_write(UC_X86_REG_EIP, ret)

    def run(self, until=0xFFFFFFF0, count=0):
        """Runs until a hook raises (re-raised here), `until` is reached or an error occurs."""
        try:
            self.uc.emu_start(self.uc.reg_read(UC_X86_REG_EIP), until, count=count)
        except UcError as e:
            if self.stop_exc is None:
                raise RuntimeError(f"unicorn: {e} at eip 0x{self.uc.reg_read(UC_X86_REG_EIP):08X}") from e
        if self.stop_exc is not None:
            raise self.stop_exc
