"""Strict, minimal x86-32 interpreter for running small pure functions of ``Cube.exe`` offline.

Why (universal pipeline, static route): ``creature_generateAppearance`` (VA 0x43F7C0) is the
table "creature kind -> model slots, part scales, pitches and offsets". The Ghidra text loses
the destination of every ``vec3_copy`` (thiscall, destination in ECX), so the offsets cannot be
read from the decompilation without guessing. Executing the shipped bytes answers it exactly.

Design rules (no silent guessing):
* only the instructions this code actually uses are implemented; anything else raises
  ``EmuError`` with the address and bytes, so an unsupported path can never yield a result;
* code and constant data are read from the PE image read-only; writes go to a sparse overlay;
* imported functions (IAT slots) are only callable when the caller registers a Python stub
  for that slot (e.g. ``rand``); an unregistered import raises;
* a step limit stops runaway loops.

Supported: integer ALU (add/or/adc/sbb/and/sub/xor/cmp/test/inc/dec/neg/not), mov/movzx/movsx/
lea/xchg/push/pop/cdq/idiv/div/imul/mul/shl/shr/sar, jcc/jmp/call/ret (incl. jump tables), the
x87 load/store subset (fld/fst/fstp/fild/fldz/fld1/fchs) and SSE scalar moves (movss/movd) used
for copying float constants. Flags: CF, ZF, SF, OF.
"""
from __future__ import annotations

import struct

MASK = 0xFFFFFFFF
REG32 = ["eax", "ecx", "edx", "ebx", "esp", "ebp", "esi", "edi"]


class EmuError(Exception):
    pass


class ImportCall(Exception):
    pass


def _s32(v):
    v &= MASK
    return v - (1 << 32) if v & 0x80000000 else v


def _f32(bits):
    return struct.unpack("<f", struct.pack("<I", bits & MASK))[0]


def _b32(value):
    return struct.unpack("<I", struct.pack("<f", value))[0]


def _sx(v, bits):
    v &= (1 << bits) - 1
    return v - (1 << bits) if v & (1 << (bits - 1)) else v


class Memory:
    """Sparse little-endian byte memory over a read-only PE image."""

    def __init__(self, image):
        self.image = image
        self.over = {}
        self.writes = []          # (va, size) log, used by callers to see what was written

    def read(self, va, n):
        out = bytearray()
        for i in range(n):
            a = (va + i) & MASK
            if a in self.over:
                out.append(self.over[a])
            else:
                try:
                    out += self.image.read(a, 1)
                except Exception as e:  # noqa: BLE001 — unmapped read is a hard error
                    raise EmuError(f"read of unmapped address 0x{a:08X}") from e
        return bytes(out)

    def write(self, va, data):
        for i, b in enumerate(data):
            self.over[(va + i) & MASK] = b
        self.writes.append((va & MASK, len(data)))

    def u(self, va, size):
        return int.from_bytes(self.read(va, size), "little")

    def w(self, va, size, value):
        self.write(va, (value & ((1 << (8 * size)) - 1)).to_bytes(size, "little"))


class CPU:
    def __init__(self, image, iat_stubs=None, max_steps=200000):
        self.mem = Memory(image)
        self.r = {n: 0 for n in REG32}
        self.eip = 0
        self.cf = self.zf = self.sf = self.of = False
        self.fpu = []             # x87 stack (python floats, top = end)
        self.xmm = [0] * 8        # low 32 bits only
        self.iat = dict(iat_stubs or {})   # IAT slot VA -> python callable(cpu) -> eax
        self.steps = 0
        self.max_steps = max_steps
        self.trace_calls = []

    # ------------------------------------------------------------------ registers
    def get(self, idx, size):
        if size == 4:
            return self.r[REG32[idx]]
        if size == 2:
            return self.r[REG32[idx]] & 0xFFFF
        if idx < 4:
            return self.r[REG32[idx]] & 0xFF
        return (self.r[REG32[idx - 4]] >> 8) & 0xFF

    def put(self, idx, size, v):
        if size == 4:
            self.r[REG32[idx]] = v & MASK
        elif size == 2:
            n = REG32[idx]
            self.r[n] = (self.r[n] & 0xFFFF0000) | (v & 0xFFFF)
        elif idx < 4:
            n = REG32[idx]
            self.r[n] = (self.r[n] & 0xFFFFFF00) | (v & 0xFF)
        else:
            n = REG32[idx - 4]
            self.r[n] = (self.r[n] & 0xFFFF00FF) | ((v & 0xFF) << 8)

    def push(self, v):
        self.r["esp"] = (self.r["esp"] - 4) & MASK
        self.mem.w(self.r["esp"], 4, v)

    def pop(self):
        v = self.mem.u(self.r["esp"], 4)
        self.r["esp"] = (self.r["esp"] + 4) & MASK
        return v

    # ------------------------------------------------------------------ decoding
    def fetch(self, n):
        b = self.mem.read(self.eip, n)
        self.eip = (self.eip + n) & MASK
        return b

    def imm(self, size, signed=False):
        v = int.from_bytes(self.fetch(size), "little")
        return _sx(v, 8 * size) & MASK if signed else v

    def modrm(self):
        """Return (mod, reg, rm_kind, rm_value): rm_kind 'reg' -> index, 'mem' -> address."""
        b = self.fetch(1)[0]
        mod, reg, rm = b >> 6, (b >> 3) & 7, b & 7
        if mod == 3:
            return mod, reg, "reg", rm
        if rm == 4:
            sib = self.fetch(1)[0]
            ss, idx, base = sib >> 6, (sib >> 3) & 7, sib & 7
            addr = 0
            if base == 5 and mod == 0:
                addr = self.imm(4)
            else:
                addr = self.r[REG32[base]]
            if idx != 4:
                addr += self.r[REG32[idx]] << ss
        elif rm == 5 and mod == 0:
            addr = self.imm(4)
        else:
            addr = self.r[REG32[rm]]
        if mod == 1:
            addr += _sx(self.fetch(1)[0], 8)
        elif mod == 2:
            addr += _s32(self.imm(4))
        return mod, reg, "mem", addr & MASK

    def rd(self, kind, val, size):
        return self.get(val, size) if kind == "reg" else self.mem.u(val, size)

    def wr(self, kind, val, size, v):
        if kind == "reg":
            self.put(val, size, v)
        else:
            self.mem.w(val, size, v)

    # ------------------------------------------------------------------ flags / ALU
    def _flags_logic(self, res, size):
        bits = 8 * size
        res &= (1 << bits) - 1
        self.cf = self.of = False
        self.zf = res == 0
        self.sf = bool(res >> (bits - 1))
        return res

    def alu(self, op, a, b, size):
        bits = 8 * size
        m = (1 << bits) - 1
        a &= m
        b &= m
        if op in (0, 2):        # add / adc
            c = 1 if (op == 2 and self.cf) else 0
            full = a + b + c
            res = full & m
            self.cf = full > m
            self.of = bool(((a ^ res) & (b ^ res)) >> (bits - 1) & 1)
        elif op in (5, 7, 3):   # sub / cmp / sbb
            c = 1 if (op == 3 and self.cf) else 0
            full = a - b - c
            res = full & m
            self.cf = full < 0
            self.of = bool(((a ^ b) & (a ^ res)) >> (bits - 1) & 1)
        elif op == 1:
            return self._flags_logic(a | b, size)
        elif op == 4:
            return self._flags_logic(a & b, size)
        elif op == 6:
            return self._flags_logic(a ^ b, size)
        else:
            raise EmuError(f"alu op {op}")
        self.zf = res == 0
        self.sf = bool(res >> (bits - 1))
        return res

    def cond(self, cc):
        t = {
            0: self.of, 1: not self.of, 2: self.cf, 3: not self.cf, 4: self.zf, 5: not self.zf,
            6: self.cf or self.zf, 7: not (self.cf or self.zf), 8: self.sf, 9: not self.sf,
            12: self.sf != self.of, 13: self.sf == self.of,
            14: self.zf or (self.sf != self.of), 15: (not self.zf) and (self.sf == self.of),
        }
        if cc not in t:
            raise EmuError(f"condition {cc} (parity) not supported")
        return t[cc]

    # ------------------------------------------------------------------ run
    def call(self, va, args=(), ecx=None, stack_top=0x0FF00000, return_va=0xDEAD0000):
        """Call ``va`` (cdecl/thiscall) and run until it returns to ``return_va``."""
        self.r["esp"] = stack_top
        for a in reversed(args):
            self.push(a)
        self.push(return_va)
        if ecx is not None:
            self.r["ecx"] = ecx
        self.eip = va
        while self.eip != return_va:
            self.step()
        return self.r["eax"]

    def _do_call(self, target, ret):
        if target in self.iat:
            # an import reached through `call [iat]` / `call reg` (reg loaded from the IAT)
            self.r["eax"] = self.iat[target](self) & MASK
            self.eip = ret
            return
        if target & 0xF0000000 == 0xF0000000 and (target & 0x0FFFFFFF) in self.iat:
            self.r["eax"] = self.iat[target & 0x0FFFFFFF](self) & MASK
            self.eip = ret
            return
        self.trace_calls.append(target)
        self.push(ret)
        self.eip = target

    def load_import_ptr(self, va):
        """Reading an IAT slot yields a tagged pseudo-pointer so `call reg` can be resolved."""
        if va in self.iat:
            return 0xF0000000 | va
        return None

    def step(self):
        self.steps += 1
        if self.steps > self.max_steps:
            raise EmuError("step limit exceeded")
        start = self.eip
        op = self.fetch(1)[0]
        size = 4
        rep = None
        while op in (0x66, 0xF3, 0xF2):
            if op == 0x66:
                size = 2
            else:
                rep = op
            op = self.fetch(1)[0]
        try:
            self._exec(op, size, rep, start)
        except EmuError:
            raise
        except Exception as e:  # noqa: BLE001
            raise EmuError(f"at 0x{start:08X}: {e!r}") from e

    def _unsupported(self, start, op):
        raise EmuError(f"unsupported opcode 0x{op:02X} at 0x{start:08X} bytes="
                       f"{self.mem.read(start, 8).hex(' ')}")

    def _exec(self, op, size, rep, start):
        # ALU r/m, r and r, r/m and acc, imm  (00..3D)
        if op < 0x40 and (op & 7) < 6 and op not in (0x0F, 0x26, 0x2E, 0x36, 0x3E, 0x27, 0x2F, 0x37, 0x3F):
            alu_op = op >> 3
            form = op & 7
            if form in (0, 1, 2, 3):
                sz = 1 if form in (0, 2) else size
                _, reg, k, v = self.modrm()
                if form in (0, 1):
                    a, b = self.rd(k, v, sz), self.get(reg, sz)
                    res = self.alu(alu_op, a, b, sz)
                    if alu_op != 7:
                        self.wr(k, v, sz, res)
                else:
                    a, b = self.get(reg, sz), self.rd(k, v, sz)
                    res = self.alu(alu_op, a, b, sz)
                    if alu_op != 7:
                        self.put(reg, sz, res)
                return
            sz = 1 if form == 4 else size
            b = self.imm(sz)
            res = self.alu(alu_op, self.get(0, sz), b, sz)
            if alu_op != 7:
                self.put(0, sz, res)
            return
        if 0x40 <= op <= 0x4F:
            idx = op & 7
            cf = self.cf
            v = self.alu(0 if op < 0x48 else 5, self.get(idx, size), 1, size)
            self.cf = cf
            self.put(idx, size, v)
            return
        if 0x50 <= op <= 0x57:
            self.push(self.r[REG32[op & 7]])
            return
        if 0x58 <= op <= 0x5F:
            self.r[REG32[op & 7]] = self.pop()
            return
        if op == 0x68:
            self.push(self.imm(4))
            return
        if op == 0x6A:
            self.push(self.imm(1, signed=True))
            return
        if op in (0x69, 0x6B):
            _, reg, k, v = self.modrm()
            a = _sx(self.rd(k, v, size), 8 * size)
            b = _sx(self.imm(4 if op == 0x69 else 1), 32 if op == 0x69 else 8)
            res = a * b
            self.put(reg, size, res)
            self.cf = self.of = not (-(1 << 31) <= res < (1 << 31))
            return
        if 0x70 <= op <= 0x7F:
            d = _sx(self.fetch(1)[0], 8)
            if self.cond(op & 0xF):
                self.eip = (self.eip + d) & MASK
            return
        if op in (0x80, 0x81, 0x83):
            sz = 1 if op == 0x80 else size
            _, reg, k, v = self.modrm()
            b = self.imm(1, signed=True) if op == 0x83 else self.imm(sz)
            res = self.alu(reg, self.rd(k, v, sz), b, sz)
            if reg != 7:
                self.wr(k, v, sz, res)
            return
        if op in (0x84, 0x85):
            sz = 1 if op == 0x84 else size
            _, reg, k, v = self.modrm()
            self._flags_logic(self.rd(k, v, sz) & self.get(reg, sz), sz)
            return
        if op in (0x86, 0x87):
            sz = 1 if op == 0x86 else size
            _, reg, k, v = self.modrm()
            a, b = self.rd(k, v, sz), self.get(reg, sz)
            self.wr(k, v, sz, b)
            self.put(reg, sz, a)
            return
        if op in (0x88, 0x89, 0x8A, 0x8B):
            sz = 1 if op in (0x88, 0x8A) else size
            _, reg, k, v = self.modrm()
            if op in (0x88, 0x89):
                self.wr(k, v, sz, self.get(reg, sz))
            else:
                val = self.rd(k, v, sz)
                if k == "mem" and sz == 4:
                    tag = self.load_import_ptr(v)
                    if tag is not None:
                        val = tag
                self.put(reg, sz, val)
            return
        if op == 0x8D:
            _, reg, k, v = self.modrm()
            if k != "mem":
                self._unsupported(start, op)
            self.put(reg, size, v)
            return
        if op == 0x90:
            return
        if op == 0x99:
            self.r["edx"] = MASK if self.r["eax"] & 0x80000000 else 0
            return
        if op == 0x98:
            self.r["eax"] = _sx(self.r["eax"] & 0xFFFF, 16) & MASK
            return
        if op in (0xA1, 0xA3, 0xA0, 0xA2):
            addr = self.imm(4)
            sz = 1 if op in (0xA0, 0xA2) else size
            if op in (0xA1, 0xA0):
                val = self.mem.u(addr, sz)
                tag = self.load_import_ptr(addr) if sz == 4 else None
                self.put(0, sz, tag if tag is not None else val)
            else:
                self.mem.w(addr, sz, self.get(0, sz))
            return
        if 0xB0 <= op <= 0xB7:
            self.put(op & 7, 1, self.imm(1))
            return
        if 0xB8 <= op <= 0xBF:
            self.put(op & 7, size, self.imm(size))
            return
        if op in (0xC1, 0xD1, 0xC0, 0xD0):
            sz = 1 if op in (0xC0, 0xD0) else size
            _, reg, k, v = self.modrm()
            n = (self.imm(1) if op in (0xC1, 0xC0) else 1) & 31
            a = self.rd(k, v, sz)
            bits = 8 * sz
            m = (1 << bits) - 1
            if n == 0:
                return
            if reg == 4:
                res = (a << n) & m
                self.cf = bool((a >> (bits - n)) & 1)
            elif reg == 5:
                res = a >> n
                self.cf = bool((a >> (n - 1)) & 1)
            elif reg == 7:
                sa = _sx(a, bits)
                res = (sa >> n) & m
                self.cf = bool((sa >> (n - 1)) & 1)
            else:
                self._unsupported(start, op)
            self.zf = res == 0
            self.sf = bool(res >> (bits - 1))
            self.of = False
            self.wr(k, v, sz, res)
            return
        if op == 0xC3:
            self.eip = self.pop()
            return
        if op == 0xC2:
            n = self.imm(2)
            self.eip = self.pop()
            self.r["esp"] = (self.r["esp"] + n) & MASK
            return
        if op in (0xC6, 0xC7):
            sz = 1 if op == 0xC6 else size
            _, reg, k, v = self.modrm()
            self.wr(k, v, sz, self.imm(sz))
            return
        if op == 0xC9:            # leave
            self.r["esp"] = self.r["ebp"]
            self.r["ebp"] = self.pop()
            return
        if op == 0xE8:
            rel = _s32(self.imm(4))
            self._do_call((self.eip + rel) & MASK, self.eip)
            return
        if op == 0xE9:
            rel = _s32(self.imm(4))
            self.eip = (self.eip + rel) & MASK
            return
        if op == 0xEB:
            d = _sx(self.fetch(1)[0], 8)
            self.eip = (self.eip + d) & MASK
            return
        if op in (0xF6, 0xF7):
            sz = 1 if op == 0xF6 else size
            _, reg, k, v = self.modrm()
            a = self.rd(k, v, sz)
            if reg == 0:
                self._flags_logic(a & self.imm(sz), sz)
            elif reg == 2:
                self.wr(k, v, sz, ~a)
            elif reg == 3:
                self.wr(k, v, sz, self.alu(5, 0, a, sz))
                self.cf = a != 0
            elif reg in (6, 7) and sz == 4:
                num = (self.r["edx"] << 32) | self.r["eax"]
                if a == 0:
                    raise EmuError("division by zero")
                if reg == 7:
                    sn, sd = _sx(num, 64), _s32(a)
                    q = abs(sn) // abs(sd) * (1 if (sn < 0) == (sd < 0) else -1)
                    rmd = sn - q * sd
                else:
                    q, rmd = divmod(num, a)
                self.r["eax"] = q & MASK
                self.r["edx"] = rmd & MASK
            elif reg in (4, 5) and sz == 4:
                if reg == 5:
                    p = _s32(self.r["eax"]) * _s32(a)
                else:
                    p = self.r["eax"] * a
                self.r["eax"] = p & MASK
                self.r["edx"] = (p >> 32) & MASK
            else:
                self._unsupported(start, op)
            return
        if op == 0xFF:
            _, reg, k, v = self.modrm()
            if reg in (0, 1):
                cf = self.cf
                self.wr(k, v, size, self.alu(0 if reg == 0 else 5, self.rd(k, v, size), 1, size))
                self.cf = cf
            elif reg == 2:
                target = self.rd(k, v, 4)
                if k == "mem":
                    tag = self.load_import_ptr(v)
                    if tag is not None:
                        target = tag
                self._do_call(target, self.eip)
            elif reg == 4:
                self.eip = self.rd(k, v, 4)
            elif reg == 6:
                self.push(self.rd(k, v, 4))
            else:
                self._unsupported(start, op)
            return
        if op == 0x0F:
            return self._exec0f(size, rep, start)
        if op in (0xD9, 0xDD, 0xDB):
            return self._x87(op, start)
        self._unsupported(start, op)

    def _exec0f(self, size, rep, start):
        op = self.fetch(1)[0]
        if 0x80 <= op <= 0x8F:
            rel = _s32(self.imm(4))
            if self.cond(op & 0xF):
                self.eip = (self.eip + rel) & MASK
            return
        if 0x90 <= op <= 0x9F:
            _, reg, k, v = self.modrm()
            self.wr(k, v, 1, 1 if self.cond(op & 0xF) else 0)
            return
        if 0x40 <= op <= 0x4F:                        # cmovcc r, r/m (the source is always read)
            _, reg, k, v = self.modrm()
            val = self.rd(k, v, size)
            if self.cond(op & 0xF):
                self.put(reg, size, val)
            return
        if op in (0xB6, 0xB7, 0xBE, 0xBF):
            src = 1 if op in (0xB6, 0xBE) else 2
            _, reg, k, v = self.modrm()
            val = self.rd(k, v, src)
            if op in (0xBE, 0xBF):
                val = _sx(val, 8 * src) & MASK
            self.put(reg, size, val)
            return
        if op == 0xAF:
            _, reg, k, v = self.modrm()
            res = _sx(self.get(reg, size), 8 * size) * _sx(self.rd(k, v, size), 8 * size)
            self.put(reg, size, res)
            return
        if op in (0x10, 0x11) and rep == 0xF3:       # movss
            _, reg, k, v = self.modrm()
            if op == 0x10:
                self.xmm[reg] = self.mem.u(v, 4) if k == "mem" else self.xmm[v]
            else:
                if k == "mem":
                    self.mem.w(v, 4, self.xmm[reg])
                else:
                    self.xmm[v] = self.xmm[reg]
            return
        if rep == 0xF3 and op in (0x58, 0x59, 0x5C, 0x5E, 0x5D, 0x5F):   # add/mul/sub/div/min/max ss
            _, reg, k, v = self.modrm()
            a = _f32(self.xmm[reg])
            b = _f32(self.mem.u(v, 4) if k == "mem" else self.xmm[v])
            r = {0x58: a + b, 0x59: a * b, 0x5C: a - b, 0x5E: (a / b) if b else float("nan"),
                 0x5D: min(a, b), 0x5F: max(a, b)}[op]
            self.xmm[reg] = _b32(r)
            return
        if rep == 0xF3 and op == 0x2A:               # cvtsi2ss xmm, r/m32
            _, reg, k, v = self.modrm()
            self.xmm[reg] = _b32(float(_s32(self.rd(k, v, 4))))
            return
        if rep == 0xF3 and op == 0x2C:               # cvttss2si r32, xmm/m32
            _, reg, k, v = self.modrm()
            f = _f32(self.mem.u(v, 4) if k == "mem" else self.xmm[v])
            self.put(reg, 4, int(f) & MASK)
            return
        if op in (0x2E, 0x2F) and rep is None:       # (u)comiss xmm, xmm/m32
            _, reg, k, v = self.modrm()
            a = _f32(self.xmm[reg])
            b = _f32(self.mem.u(v, 4) if k == "mem" else self.xmm[v])
            self.zf, self.cf, self.of, self.sf = a == b, a < b, False, False
            return
        if op == 0x5B and rep is None and size == 4:  # cvtdq2ps (low lane is all this code reads)
            _, reg, k, v = self.modrm()
            self.xmm[reg] = _b32(float(_s32(self.mem.u(v, 4) if k == "mem" else self.xmm[v])))
            return
        if op == 0x28 and rep is None:               # movaps xmm, xmm
            _, reg, k, v = self.modrm()
            if k == "reg":
                self.xmm[reg] = self.xmm[v]
                return
        if op in (0x6E, 0x7E) and size == 2:          # 66 0F 6E/7E movd
            _, reg, k, v = self.modrm()
            if op == 0x6E:
                self.xmm[reg] = self.rd(k, v, 4)
            else:
                self.wr(k, v, 4, self.xmm[reg])
            return
        if op == 0x57 and size == 4:                  # xorps xmm,xmm (zeroing idiom)
            _, reg, k, v = self.modrm()
            if k == "reg" and v == reg:
                self.xmm[reg] = 0
                return
        self._unsupported(start, 0x0F00 | op)

    def _x87(self, op, start):
        b = self.mem.read(self.eip, 1)[0]
        if op == 0xD9 and b >= 0xC0:
            self.eip += 1
            if b == 0xEE:
                self.fpu.append(0.0)
            elif b == 0xE8:
                self.fpu.append(1.0)
            elif b == 0xE0:
                self.fpu[-1] = -self.fpu[-1]
            elif 0xC0 <= b <= 0xC7:
                self.fpu.append(self.fpu[-1 - (b & 7)])
            else:
                self._unsupported(start, op)
            return
        if op == 0xDD and 0xD8 <= b <= 0xDF:          # fstp st(i)
            self.eip += 1
            i = b & 7
            top = self.fpu.pop()
            if i:
                self.fpu[-i] = top
            return
        _, reg, k, v = self.modrm()
        if k != "mem":
            self._unsupported(start, op)
        if op == 0xD9 and reg == 0:
            self.fpu.append(struct.unpack("<f", self.mem.read(v, 4))[0])
        elif op == 0xD9 and reg in (2, 3):
            self.mem.write(v, struct.pack("<f", self.fpu[-1]))
            if reg == 3:
                self.fpu.pop()
        elif op == 0xDD and reg == 0:
            self.fpu.append(struct.unpack("<d", self.mem.read(v, 8))[0])
        elif op == 0xDD and reg in (2, 3):
            self.mem.write(v, struct.pack("<d", self.fpu[-1]))
            if reg == 3:
                self.fpu.pop()
        elif op == 0xDB and reg == 0:
            self.fpu.append(float(_s32(self.mem.u(v, 4))))
        else:
            self._unsupported(start, op)
