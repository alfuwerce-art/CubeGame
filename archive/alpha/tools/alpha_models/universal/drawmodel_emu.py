"""Offline execution of Alpha's ``Creature_drawModel`` (VA 0x4128F0) on synthetic creatures.

Why: every Alpha animation (walk, idle, 73 action states, tails, wings, equipment) is computed by
this ONE routine from the creature struct, the drawModel ``this`` (state, timer, smoothed pose
arrays) and the frame time; there are no animation files. The decompilation loses the targets of
most pose writes (``arrayElem_stride4`` without ``this``), so instead of re-deriving every branch
by hand this module EXECUTES the shipped machine code in the strict interpreter ``x86emu`` and
records what Alpha would upload to the GPU: one matrix per drawn part.

Status of poses produced here: ``EMULATED_ALPHA_CODE`` — Alpha's own code on inputs we build.
A state is ``TRANSFERRED_FROM_ALPHA``-equivalent only after an emulated frame matches a runtime
capture of the same inputs (``validate``); otherwise it stays EMULATED (not verified).

Rules (inherited from x86emu, no guessing):
* only implemented instructions run; anything else raises ``EmuError`` with the address;
* reads of memory nobody provided raise (unmapped) — every input is explicit;
* functions with side effects outside the creature (Direct3D uploads, draw calls, shader
  uniforms) are replaced by recording stubs listed in ``STUBS`` with their stack cleanup;
  pure maths library calls (``libm_sse2_*``) are replaced by Python ``math`` (IEEE double).
"""
from __future__ import annotations

import math
import struct

from tools.alpha_models.universal.x86emu import CPU, EmuError, MASK, _b32, _f32, _s32, _sx, REG32

DRAWMODEL_VA = 0x4128F0
UPLOAD_VA = 0x4482A0          # render_uploadModelViewMatrices(this=renderer; matrix, view, proj)
DRAWMESH_VA = 0x4E6DF0        # GameController_draw_mesh(this=mesh)
FRAME_LOCAL_BASE = 0x1740     # local_X lives at body_esp + 0x1740 - X (pose_capture.py)

# synthetic address space (nothing here overlaps the PE image at 0x400000-0x7xxxxx)
STACK_BASE, STACK_SIZE = 0x0F000000, 0x00100000
HEAP_BASE = 0x20000000
RETURN_VA = 0xDEAD0000
# drawModel calls getDword0x1c(renderer) -> IDirect3DDevice9 and then vtable+0xE4 (SetRenderState)
# around the mirrored right hand; the synthetic renderer points at a fake device whose vtable
# sends that slot to a recording stub and every other slot to an unmapped address (raises).
FAKE_DEVICE_SETRENDERSTATE = 0xDEAD1000


def f2b(v):
    return struct.unpack("<I", struct.pack("<f", v))[0]


class RegionMemory:
    """Flat bytearray regions (stack/heap) over the read-only PE image; image writes overlay."""

    def __init__(self, image):
        self.image = image
        self.regions = []         # (base, end, bytearray)
        self.over = {}
        self.writes = []

    def map(self, base, size, data=None):
        buf = bytearray(size)
        if data:
            buf[:len(data)] = data
        self.regions.append((base, base + size, buf))
        return buf

    def _region(self, va, n):
        for b, e, buf in self.regions:
            if b <= va and va + n <= e:
                return b, buf
        return None, None

    def read(self, va, n):
        va &= MASK
        b, buf = self._region(va, n)
        if buf is not None:
            return bytes(buf[va - b:va - b + n])
        out = bytearray()
        for i in range(n):
            a = (va + i) & MASK
            if a in self.over:
                out.append(self.over[a])
            else:
                try:
                    out += self.image.read(a, 1)
                except Exception as e:  # noqa: BLE001
                    raise EmuError(f"read of unmapped address 0x{a:08X}") from e
        return bytes(out)

    def write(self, va, data):
        va &= MASK
        b, buf = self._region(va, len(data))
        if buf is not None:
            buf[va - b:va - b + len(data)] = data
            return
        for i, x in enumerate(data):
            a = (va + i) & MASK
            # a write into the PE image (globals) is kept in the overlay; unmapped writes raise
            if a not in self.over:
                try:
                    self.image.read(a, 1)
                except Exception as e:  # noqa: BLE001
                    raise EmuError(f"write to unmapped address 0x{a:08X}") from e
            self.over[a] = x

    def u(self, va, size):
        return int.from_bytes(self.read(va, size), "little")

    def w(self, va, size, value):
        self.write(va, (value & ((1 << (8 * size)) - 1)).to_bytes(size, "little"))


M128 = (1 << 128) - 1


def _lanes(v):
    return [(v >> (32 * i)) & MASK for i in range(4)]


def _pack(lanes):
    return sum((x & MASK) << (32 * i) for i, x in enumerate(lanes))


def _d64(bits):
    return struct.unpack("<d", struct.pack("<Q", bits & 0xFFFFFFFFFFFFFFFF))[0]


def _q64(value):
    return struct.unpack("<Q", struct.pack("<d", value))[0]


class EmuCPU(CPU):
    """x86emu.CPU with 128-bit SSE registers, more x87, string ops and VA stubs."""

    def __init__(self, image, stubs=None, max_steps=20_000_000):
        super().__init__(image, max_steps=max_steps)
        self.mem = RegionMemory(image)
        self.xmm = [0] * 8
        self.stubs = dict(stubs or {})
        self.hooks = {}           # VA -> callable(cpu) run before the instruction at VA
        self.df = False

    # ------------------------------------------------------------------ calls into stubs
    def _do_call(self, target, ret):
        if target not in self.stubs and 0x68D000 <= target < 0x68E000:
            try:
                head = self.mem.read(target, 2)
            except EmuError:
                head = b""
            if head == bytes((0xFF, 0x25)):      # jmp [IAT]: an import nobody stubbed -> stop, never guess
                iat = self.mem.u(target + 2, 4)
                raise EmuError(f"unstubbed import thunk 0x{target:08X} (IAT 0x{iat:08X})")
        if target in self.stubs:
            fn, cleanup = self.stubs[target]
            self.push(ret)
            fn(self)
            self.eip = self.pop()
            self.r["esp"] = (self.r["esp"] + cleanup) & MASK
            return
        super()._do_call(target, ret)

    def arg(self, i):
        """i-th stack argument while inside a stub (return address at [esp])."""
        return self.mem.u(self.r["esp"] + 4 + 4 * i, 4)

    # ------------------------------------------------------------------ helpers
    def xr(self, k, v, n=4):
        """Read n bytes from xmm reg (low lanes) or memory as int."""
        if k == "mem":
            return self.mem.u(v, n)
        return self.xmm[v] & ((1 << (8 * n)) - 1)

    def run(self, va, args=(), ecx=None, return_va=RETURN_VA):
        self.r["esp"] = STACK_BASE + STACK_SIZE - 0x100
        for a in reversed(args):
            self.push(a)
        self.push(return_va)
        if ecx is not None:
            self.r["ecx"] = ecx
        self.eip = va
        while self.eip != return_va:
            self.step()
        return self.r["eax"]

    def step(self):
        self.steps += 1
        if self.steps > self.max_steps:
            raise EmuError("step limit exceeded")
        start = self.eip
        if start in self.hooks:
            self.hooks[start](self)
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
            if op in (0xA4, 0xA5, 0xAA, 0xAB):
                return self._string(op, size, rep)
            if op in (0xD8, 0xDA, 0xDC, 0xDE, 0xDF, 0xD9, 0xDD, 0xDB):
                return self._x87x(op, start)
            if op == 0x9E:     # sahf
                ah = (self.r["eax"] >> 8) & 0xFF
                self.cf, self.zf, self.sf = bool(ah & 1), bool(ah & 0x40), bool(ah & 0x80)
                self.pf = bool(ah & 4)
                return
            if op == 0xFC:
                self.df = False
                return
            if op == 0x9F:           # lahf
                ah = (int(self.sf) << 7) | (int(self.zf) << 6) | (int(getattr(self, "pf", False)) << 2) | 2 | int(self.cf)
                self.r["eax"] = (self.r["eax"] & 0xFFFF00FF) | (ah << 8)
                return
            if op in (0xA8, 0xA9):   # test al/eax, imm
                sz = 1 if op == 0xA8 else size
                self._flags_logic(self.get(0, sz) & self.imm(sz), sz)
                return
            if 0x91 <= op <= 0x97:   # xchg eax, r32
                n = REG32[op & 7]
                self.r["eax"], self.r[n] = self.r[n], self.r["eax"]
                return
            self._exec(op, size, rep, start)
        except EmuError:
            raise
        except Exception as e:  # noqa: BLE001
            raise EmuError(f"at 0x{start:08X}: {e!r}") from e

    def _flags_logic(self, res, size):
        r = super()._flags_logic(res, size)
        self.pf = bin(r & 0xFF).count("1") % 2 == 0
        return r

    def alu(self, op, a, b, size):
        r = super().alu(op, a, b, size)
        self.pf = bin(r & 0xFF).count("1") % 2 == 0
        return r

    def cond(self, cc):
        if cc in (10, 11):
            p = getattr(self, "pf", False)
            return p if cc == 10 else not p
        return super().cond(cc)

    def _string(self, op, size, rep):
        sz = 1 if op in (0xA4, 0xAA) else size
        count = self.r["ecx"] if rep else 1
        step = -sz if self.df else sz
        for _ in range(count):
            if op in (0xA4, 0xA5):
                self.mem.w(self.r["edi"], sz, self.mem.u(self.r["esi"], sz))
                self.r["esi"] = (self.r["esi"] + step) & MASK
            else:
                self.mem.w(self.r["edi"], sz, self.get(0, sz))
            self.r["edi"] = (self.r["edi"] + step) & MASK
        if rep:
            self.r["ecx"] = 0

    # ------------------------------------------------------------------ x87 (superset)
    def _fcmp(self, a, b):
        unordered = a != a or b != b
        self.fsw_c = (unordered or a < b, unordered or a == b, unordered)   # C0, C3, C2

    def _x87x(self, op, start):
        b = self.mem.read(self.eip, 1)[0]
        st = self.fpu
        if b >= 0xC0:
            self.eip += 1
            i = b & 7
            if op == 0xD9:
                if b == 0xEE: st.append(0.0); return
                if b == 0xE8: st.append(1.0); return
                if b == 0xE0: st[-1] = -st[-1]; return
                if b == 0xE1: st[-1] = abs(st[-1]); return
                if b == 0xFA: st[-1] = math.sqrt(st[-1]); return
                if b == 0xFE: st[-1] = math.sin(st[-1]); return
                if b == 0xFF: st[-1] = math.cos(st[-1]); return
                if 0xC0 <= b <= 0xC7: st.append(st[-1 - i]); return
                if 0xC8 <= b <= 0xCF: st[-1], st[-1 - i] = st[-1 - i], st[-1]; return
            if op == 0xDD and 0xD8 <= b <= 0xDF:      # fstp st(i)
                top = st.pop()
                if i:
                    st[-i] = top
                return
            if op == 0xDD and 0xD0 <= b <= 0xD7:      # fst st(i)
                st[-1 - i] = st[-1]
                return
            if op in (0xD8, 0xDC, 0xDE) :
                kind = (b >> 3) & 7
                if op == 0xD8:
                    a_, b_ = st[-1], st[-1 - i]
                    if kind in (2, 3):
                        self._fcmp(a_, b_)
                        if kind == 3: st.pop()
                        return
                    st[-1] = self._farith(kind, a_, b_)
                    return
                # DC: st(i) = st(i) op st(0); DE: same then pop (reversed sub/div encodings)
                if op == 0xDE and b == 0xD9:          # fcompp
                    self._fcmp(st[-1], st[-2]); st.pop(); st.pop(); return
                tgt = -1 - i
                kmap = {0: 0, 1: 1, 4: 5, 5: 4, 6: 7, 7: 6}
                st[tgt] = self._farith(kmap[kind], st[tgt], st[-1])
                if op == 0xDE:
                    st.pop()
                return
            if op == 0xDF and b == 0xE0:              # fnstsw ax
                c0, c3, c2 = getattr(self, "fsw_c", (False, False, False))
                sw = (c0 << 8) | (c2 << 10) | (c3 << 14)
                self.r["eax"] = (self.r["eax"] & 0xFFFF0000) | sw
                return
            if op in (0xDB, 0xDF) and (0xE8 <= b <= 0xF7):   # fucomi / fcomi (+p)
                a_, b_ = st[-1], st[-1 - i]
                unordered = a_ != a_ or b_ != b_
                self.zf, self.pf, self.cf = unordered or a_ == b_, unordered, unordered or a_ < b_
                if op == 0xDF:
                    st.pop()
                return
            if op == 0xDD and 0xE0 <= b <= 0xEF:      # fucom(p)
                self._fcmp(st[-1], st[-1 - i])
                if b >= 0xE8: st.pop()
                return
            self._unsupported(start, op)
        _, reg, k, v = self.modrm()
        if op == 0xD9:
            if reg == 0: st.append(_f32(self.mem.u(v, 4))); return
            if reg == 2: self.mem.w(v, 4, f2b(st[-1])); return
            if reg == 3: self.mem.w(v, 4, f2b(st.pop())); return
            if reg == 5: return                        # fldcw
            if reg == 7: self.mem.w(v, 2, 0x027F); return   # fnstcw
        if op == 0xDD:
            if reg == 0: st.append(_d64(self.mem.u(v, 8))); return
            if reg == 2: self.mem.w(v, 8, _q64(st[-1])); return
            if reg == 3: self.mem.w(v, 8, _q64(st.pop())); return
        if op == 0xDB:
            if reg == 0: st.append(float(_s32(self.mem.u(v, 4)))); return
            if reg in (2, 3):
                self.mem.w(v, 4, int(round(st[-1])) & MASK)
                if reg == 3: st.pop()
                return
        if op == 0xDF and reg == 5:
            st.append(float(_sx(self.mem.u(v, 8), 64))); return
        if op == 0xDF and reg == 7:
            self.mem.w(v, 8, int(round(st.pop())) & 0xFFFFFFFFFFFFFFFF); return
        if op in (0xD8, 0xDC):
            val = _f32(self.mem.u(v, 4)) if op == 0xD8 else _d64(self.mem.u(v, 8))
            if reg in (2, 3):
                self._fcmp(st[-1], val)
                if reg == 3: st.pop()
                return
            st[-1] = self._farith(reg, st[-1], val)
            return
        if op == 0xDA:
            st[-1] = self._farith(reg, st[-1], float(_s32(self.mem.u(v, 4))))
            return
        self._unsupported(start, op)

    @staticmethod
    def _farith(kind, a, b):
        if kind == 0: return a + b
        if kind == 1: return a * b
        if kind == 4: return a - b
        if kind == 5: return b - a
        if kind == 6: return a / b
        if kind == 7: return b / a
        raise EmuError(f"x87 arith {kind}")

    # ------------------------------------------------------------------ SSE (128-bit)
    def _exec0f(self, size, rep, start):
        save = self.eip
        op = self.fetch(1)[0]
        X = self.xmm
        if op in (0x10, 0x11):
            _, reg, k, v = self.modrm()
            if rep == 0xF3:            # movss
                if op == 0x10:
                    X[reg] = self.mem.u(v, 4) if k == "mem" else (X[reg] & ~MASK & M128) | (X[v] & MASK)
                elif k == "mem":
                    self.mem.w(v, 4, X[reg] & MASK)
                else:
                    X[v] = (X[v] & ~MASK & M128) | (X[reg] & MASK)
                return
            if rep == 0xF2:            # movsd
                m64 = 0xFFFFFFFFFFFFFFFF
                if op == 0x10:
                    X[reg] = self.mem.u(v, 8) if k == "mem" else (X[reg] & ~m64 & M128) | (X[v] & m64)
                elif k == "mem":
                    self.mem.w(v, 8, X[reg] & m64)
                else:
                    X[v] = (X[v] & ~m64 & M128) | (X[reg] & m64)
                return
            # movups / movupd
            if op == 0x10:
                X[reg] = self.mem.u(v, 16) if k == "mem" else X[v]
            elif k == "mem":
                self.mem.w(v, 16, X[reg])
            else:
                X[v] = X[reg]
            return
        if op in (0x28, 0x29) and rep is None:          # movaps / movapd
            _, reg, k, v = self.modrm()
            if op == 0x28:
                X[reg] = self.mem.u(v, 16) if k == "mem" else X[v]
            elif k == "mem":
                self.mem.w(v, 16, X[reg])
            else:
                X[v] = X[reg]
            return
        if op in (0x12, 0x13, 0x16, 0x17) and rep is None:   # movlps/movhps (and pd)
            _, reg, k, v = self.modrm()
            m64 = 0xFFFFFFFFFFFFFFFF
            hi = op in (0x16, 0x17)
            if op in (0x12, 0x16):
                if k != "mem":
                    if op == 0x16:                   # movlhps
                        X[reg] = (X[reg] & m64) | ((X[v] & m64) << 64)
                    else:                            # movhlps
                        X[reg] = (X[reg] & ~m64 & M128) | (X[v] >> 64)
                    return
                q = self.mem.u(v, 8)
                X[reg] = (X[reg] & m64) | (q << 64) if hi else (X[reg] & ~m64 & M128) | q
            else:
                self.mem.w(v, 8, (X[reg] >> 64) if hi else X[reg] & m64)
            return
        if op in (0x14, 0x15) and rep is None and size == 4:   # unpcklps / unpckhps
            _, reg, k, v = self.modrm()
            a, b = _lanes(X[reg]), _lanes(self.xr(k, v, 16))
            X[reg] = _pack([a[0], b[0], a[1], b[1]] if op == 0x14 else [a[2], b[2], a[3], b[3]])
            return
        if op == 0xC6 and size == 4:                    # shufps
            _, reg, k, v = self.modrm()
            imm = self.imm(1)
            a, b = _lanes(X[reg]), _lanes(self.xr(k, v, 16))
            X[reg] = _pack([a[imm & 3], a[(imm >> 2) & 3], b[(imm >> 4) & 3], b[(imm >> 6) & 3]])
            return
        if op in (0x54, 0x55, 0x56, 0x57) and rep is None:   # and/andn/or/xor ps|pd
            _, reg, k, v = self.modrm()
            b = self.xr(k, v, 16)
            a = X[reg]
            X[reg] = {0x54: a & b, 0x55: (~a & M128) & b, 0x56: a | b, 0x57: a ^ b}[op]
            return
        if op in (0x58, 0x59, 0x5C, 0x5D, 0x5E, 0x5F, 0x51):
            _, reg, k, v = self.modrm()
            fn = {0x58: lambda a, b: a + b, 0x59: lambda a, b: a * b, 0x5C: lambda a, b: a - b,
                  0x5E: lambda a, b: a / b if b else (math.copysign(math.inf, a) if a else math.nan),
                  0x5D: lambda a, b: a if a < b else b, 0x5F: lambda a, b: a if a > b else b,
                  0x51: lambda a, b: math.sqrt(b) if b >= 0 else math.nan}[op]
            if rep == 0xF3:            # ss
                a = _f32(X[reg] & MASK)
                b = _f32(self.xr(k, v, 4))
                X[reg] = (X[reg] & ~MASK & M128) | _b32(fn(a, b))
            elif rep == 0xF2:          # sd
                m64 = 0xFFFFFFFFFFFFFFFF
                a = _d64(X[reg] & m64)
                b = _d64(self.xr(k, v, 8))
                X[reg] = (X[reg] & ~m64 & M128) | _q64(fn(a, b))
            elif size == 4:            # ps
                a, b = _lanes(X[reg]), _lanes(self.xr(k, v, 16))
                X[reg] = _pack([_b32(fn(_f32(x), _f32(y))) for x, y in zip(a, b)])
            else:                      # pd
                m64 = 0xFFFFFFFFFFFFFFFF
                src = self.xr(k, v, 16)
                out = 0
                for i in range(2):
                    out |= _q64(fn(_d64((X[reg] >> (64 * i)) & m64), _d64((src >> (64 * i)) & m64))) << (64 * i)
                X[reg] = out
            return
        if op == 0x5A:                                  # cvtss2sd / cvtsd2ss
            _, reg, k, v = self.modrm()
            m64 = 0xFFFFFFFFFFFFFFFF
            if rep == 0xF3:
                X[reg] = (X[reg] & ~m64 & M128) | _q64(_f32(self.xr(k, v, 4)))
            elif rep == 0xF2:
                X[reg] = (X[reg] & ~MASK & M128) | _b32(_d64(self.xr(k, v, 8)))
            elif size == 4:            # cvtps2pd: low two floats -> two doubles
                lo = self.xr(k, v, 8)
                X[reg] = _q64(_f32(lo & MASK)) | (_q64(_f32(lo >> 32)) << 64)
            else:                      # cvtpd2ps
                src = self.xr(k, v, 16)
                X[reg] = _b32(_d64(src & 0xFFFFFFFFFFFFFFFF)) | (_b32(_d64(src >> 64)) << 32)
            return
        if op == 0x2A:                                  # cvtsi2ss / cvtsi2sd
            _, reg, k, v = self.modrm()
            val = float(_s32(self.rd(k, v, 4)))
            if rep == 0xF3:
                X[reg] = (X[reg] & ~MASK & M128) | _b32(val)
            elif rep == 0xF2:
                m64 = 0xFFFFFFFFFFFFFFFF
                X[reg] = (X[reg] & ~m64 & M128) | _q64(val)
            else:
                self._unsupported(start, 0x0F2A)
            return
        if op in (0x2C, 0x2D):                          # cvtt?ss2si / sd2si
            _, reg, k, v = self.modrm()
            f = _f32(self.xr(k, v, 4)) if rep == 0xF3 else _d64(self.xr(k, v, 8))
            if f != f or abs(f) >= 2 ** 31:
                r = 0x80000000
            else:
                r = int(f) if op == 0x2C else int(round(f))
            self.put(reg, 4, r & MASK)
            return
        if op in (0x2E, 0x2F):                          # (u)comiss / (u)comisd
            _, reg, k, v = self.modrm()
            if size == 2:
                a, b = _d64(X[reg] & 0xFFFFFFFFFFFFFFFF), _d64(self.xr(k, v, 8))
            else:
                a, b = _f32(X[reg] & MASK), _f32(self.xr(k, v, 4))
            un = a != a or b != b
            self.zf, self.pf, self.cf = un or a == b, un, un or a < b
            self.of = self.sf = False
            return
        if op == 0x5B and size == 4 and rep is None:   # cvtdq2ps
            _, reg, k, v = self.modrm()
            X[reg] = _pack([_b32(float(_s32(x))) for x in _lanes(self.xr(k, v, 16))])
            return
        if op == 0x5B and rep == 0xF3:                  # cvttps2dq
            _, reg, k, v = self.modrm()
            X[reg] = _pack([int(_f32(x)) & MASK for x in _lanes(self.xr(k, v, 16))])
            return
        if op in (0x6E, 0x7E) and size == 2:            # movd
            _, reg, k, v = self.modrm()
            if op == 0x6E:
                X[reg] = self.rd(k, v, 4)
            else:
                self.wr(k, v, 4, X[reg] & MASK)
            return
        if op == 0x7E and rep == 0xF3:                  # movq xmm, xmm/m64
            _, reg, k, v = self.modrm()
            X[reg] = self.xr(k, v, 8)
            return
        if op == 0xD6 and size == 2:                    # movq xmm/m64, xmm
            _, reg, k, v = self.modrm()
            if k == "mem":
                self.mem.w(v, 8, X[reg] & 0xFFFFFFFFFFFFFFFF)
            else:
                X[v] = X[reg] & 0xFFFFFFFFFFFFFFFF
            return
        if op in (0x6F, 0x7F) and size == 2:            # movdqa
            _, reg, k, v = self.modrm()
            if op == 0x6F:
                X[reg] = self.xr(k, v, 16)
            elif k == "mem":
                self.mem.w(v, 16, X[reg])
            else:
                X[v] = X[reg]
            return
        if op == 0xC2 and rep == 0xF3:                  # cmpss
            _, reg, k, v = self.modrm()
            pred = self.imm(1)
            a, b = _f32(X[reg] & MASK), _f32(self.xr(k, v, 4))
            r = {0: a == b, 1: a < b, 2: a <= b, 3: a != a or b != b, 4: a != b, 5: not a < b,
                 6: not a <= b, 7: not (a != a or b != b)}[pred]
            X[reg] = (X[reg] & ~MASK & M128) | (MASK if r else 0)
            return
        if 0x40 <= op <= 0x4F:                          # cmovcc
            _, reg, k, v = self.modrm()
            if self.cond(op & 0xF):
                self.put(reg, size, self.rd(k, v, size))
            return
        if op == 0xE6:                                  # cvtdq2pd / cvt(t)pd2dq
            _, reg, k, v = self.modrm()
            m64 = 0xFFFFFFFFFFFFFFFF
            if rep == 0xF3:
                lo = self.xr(k, v, 8)
                X[reg] = _q64(float(_s32(lo & MASK))) | (_q64(float(_s32(lo >> 32))) << 64)
            else:
                src = self.xr(k, v, 16)
                conv = (lambda f: int(f)) if size == 2 else (lambda f: int(round(f)))
                X[reg] = (conv(_d64(src & m64)) & MASK) | ((conv(_d64(src >> 64)) & MASK) << 32)
            return
        if op == 0x18:                                  # prefetch
            self.modrm()
            return
        self.eip = save
        return super()._exec0f(size, rep, start)


# --------------------------------------------------------------------------- libm (pure maths)
# msvcr thunks `jmp [IAT]` of __libm_sse2_*_precise: double argument and result in xmm0.
# Identified by their call sites in drawModel (sin(phase + pi/2) @0x412DFF, sqrt of a sum of
# squares @0x423F01, asin of a clamped cross product @0x4132E9, cos paired with sin @0x41358E).
def _libm(fn):
    def stub(cpu):
        x = _d64(cpu.xmm[0] & 0xFFFFFFFFFFFFFFFF)
        try:
            y = fn(x)
        except ValueError:
            y = math.nan
        cpu.xmm[0] = (cpu.xmm[0] & ~0xFFFFFFFFFFFFFFFF & M128) | _q64(y)
    return stub


def _libm2(fn):
    def stub(cpu):
        m64 = 0xFFFFFFFFFFFFFFFF
        x, y = _d64(cpu.xmm[0] & m64), _d64(cpu.xmm[1] & m64)
        try:
            r = fn(x, y)
        except (ValueError, OverflowError):
            r = math.nan
        cpu.xmm[0] = (cpu.xmm[0] & ~m64 & M128) | _q64(r)
    return stub


# names verified from the import table: `jmp [IAT]` thunk -> "_libm_sse2_<fn>_precise"
LIBM_STUBS = {
    0x68D8D4: (_libm2(math.pow), 0),
    0x68D9CE: (_libm(math.tan), 0),
    0x68D9C2: (_libm(math.sin), 0),
    0x68D7A4: (_libm(math.cos), 0),
    0x68D9C8: (_libm(math.sqrt), 0),
    0x68D9BC: (_libm(math.asin), 0),
}


# --------------------------------------------------------------------------- driver
class DrawModelRun:
    """One drawModel call: builds memory, records uploads, returns per-part matrices."""

    def __init__(self, image, stubs_extra=None):
        self.image = image
        self.parts = []           # dicts: upload_site, matrix, draw_site, mesh
        self.placement = None
        self.root = None
        self._body_esp = None
        self.unknown_calls = []
        self.stubs = {
            UPLOAD_VA: (self._upload, 12),
            DRAWMESH_VA: (self._drawmesh, 0),
            **LIBM_STUBS,
            0x448280: (self._noop("render_setUniform30"), 4),
            0x448FE0: (self._noop("render_setVec3Uniform58"), 4),
            FAKE_DEVICE_SETRENDERSTATE: (self._noop("IDirect3DDevice9::SetRenderState"), 12),
        }
        self.side_calls = []
        self.stubs.update(stubs_extra or {})

    def _noop(self, name):
        def stub(cpu):
            self.side_calls.append(name)
        return stub

    def _upload(self, cpu):
        site = cpu.mem.u(cpu.r["esp"], 4) - 5
        m = [_f32(cpu.mem.u(cpu.arg(0) + 4 * i, 4)) for i in range(16)]
        if self.placement is None:
            # the caller's frame: esp at the call (after the 3 pushes) = stub esp + 4 + 12
            body_esp = self._body_esp
            if body_esp is not None:
                self.placement = [_f32(cpu.mem.u(body_esp + FRAME_LOCAL_BASE - 0x14E0 + 4 * i, 4)) for i in range(16)]
        self.parts.append({"upload_site": site, "matrix": m, "draw_site": None, "mesh": None})

    def _drawmesh(self, cpu):
        site = cpu.mem.u(cpu.r["esp"], 4) - 5
        if self.parts and self.parts[-1]["draw_site"] is None:
            self.parts[-1]["draw_site"] = site
            self.parts[-1]["mesh"] = cpu.r["ecx"]


# --------------------------------------------------------------------------- frame API
SLOT_ORDER = ("head", "hair", "hand", "foot", "body", "tail", "slot_98", "slot_9a")   # creature+0x8c..0x9a


class Layout:
    H = HEAP_BASE
    CREATURE, THIS, RENDERER, VIEW, MVEC, MARR, MODELS, MISC = (H, H + 0x10000, H + 0x20000, H + 0x30000,
                                                               H + 0x40000, H + 0x41000, H + 0x50000, H + 0x90000)
    N_MODELS = 3000


class Emulator:
    """Persistent drawModel context for ONE creature: successive frames keep the drawModel ``this``
    (state, timer, smoothed pose arrays) exactly like Alpha does between frames."""

    def __init__(self, image, creature_bytes, model_sizes, this_bytes=b"", equipment_models=None,
                 empty_buffs=True):
        """creature_bytes: creature struct prefix (>= 0x180 bytes; the rest is zero unless set);
        model_sizes: {model index: (sx, sy, sz)} for every index the creature can draw."""
        self.image = image
        L = Layout
        self.cpu = None
        self.creature = bytearray(0x2000)
        self.creature[:len(creature_bytes)] = creature_bytes
        self.this = bytearray(0x1000)
        self.this[:len(this_bytes)] = this_bytes
        self.model_sizes = dict(model_sizes)
        self.equipment_models = dict(equipment_models or {})
        # runtime fields beyond the captured 0x180-byte header, set to explicit defaults:
        #   +0x190 level (int)  — haste/attack-speed formulas (stat_calcHasteBonus) read it;
        #   +0x1178 buff list   — std::list sentinel; empty list = sentinel pointing to itself.
        if struct.unpack_from("<i", self.creature, 0x190)[0] == 0:
            struct.pack_into("<i", self.creature, 0x190, 1)
        if empty_buffs:
            struct.pack_into("<I", self.creature, 0x1178, Layout.MISC + 0x800)

    # creature field helpers
    def set_f32(self, off, v):
        struct.pack_into("<f", self.creature, off, v)

    def set_u8(self, off, v):
        self.creature[off] = v & 0xFF

    def set_i32(self, off, v):
        struct.pack_into("<i", self.creature, off, v)

    def set_this_i32(self, idx, v):
        struct.pack_into("<i", self.this, 4 * idx, v)

    def frame(self, dt_ms=16, count_flags=0, max_steps=2_000_000, camera_xy_fixed=None):
        """Run drawModel once; returns {"parts": [...], "placement": P, "steps": n}."""
        L = Layout
        run = DrawModelRun(self.image)
        cpu = EmuCPU(self.image, stubs=run.stubs, max_steps=max_steps)
        m = cpu.mem
        m.map(STACK_BASE, STACK_SIZE)
        cre = m.map(L.CREATURE, len(self.creature), bytes(self.creature))
        th = m.map(L.THIS, len(self.this), bytes(self.this))
        struct.pack_into("<I", th, 0, L.CREATURE)
        rd = m.map(L.RENDERER, 0x1000)
        struct.pack_into("<I", rd, 0x1C, L.RENDERER + 0x100)
        struct.pack_into("<I", rd, 0x100, L.RENDERER + 0x200)
        struct.pack_into("<I", rd, 0x200 + 0xE4, FAKE_DEVICE_SETRENDERSTATE)
        m.map(L.VIEW, 0x1000)
        misc = m.map(L.MISC, 0x1000)
        # vecA / vecB: the NEGATED camera world x / y (int64 fixed point; drawModel adds them to the
        # creature position). Using -creature x / y puts the creature at the origin: the float32
        # placement stays small and exact (the world position does not change any joint frame).
        for axis, (dst, src) in enumerate(((0x100, 0x10), (0x110, 0x18))):
            v = -struct.unpack_from("<q", self.creature, src)[0] if camera_xy_fixed is None else camera_xy_fixed[axis]
            struct.pack_into("<q", misc, dst, v)
        struct.pack_into("<II", misc, 0x800, L.MISC + 0x800, L.MISC + 0x800)   # empty buff list
        mv = m.map(L.MVEC, 0x100)
        struct.pack_into("<III", mv, 0, 0, L.MARR, L.MARR + 4 * L.N_MODELS)
        ma = m.map(L.MARR, 4 * L.N_MODELS)
        mo = m.map(L.MODELS, 0x100 * (len(self.model_sizes) + 1))
        self.mesh_of = {}
        for j, (idx, size) in enumerate(sorted(self.model_sizes.items())):
            obj = L.MODELS + 0x100 * j
            struct.pack_into("<3i", mo, 0x100 * j + 0x44, *size)
            struct.pack_into("<I", ma, 4 * idx, obj)
            self.mesh_of[obj] = idx
        cpu.hooks[0x412912] = lambda c: setattr(run, "_body_esp", c.r["esp"])
        args = (L.RENDERER, L.VIEW, L.VIEW + 0x40, L.MVEC, int(dt_ms), L.MISC, L.MISC + 0x100,
                L.MISC + 0x110, count_flags, L.MISC + 0x120)
        cpu.run(DRAWMODEL_VA, args=args, ecx=L.THIS)
        # persist what drawModel wrote into its own state (smoothing, timers) and the creature
        self.this[:] = bytes(th)
        struct.pack_into("<I", self.this, 0, 0)
        self.creature[:] = bytes(cre)
        for p in run.parts:
            p["model_index"] = self.mesh_of.get(p["mesh"])
        return {"parts": run.parts, "placement": run.placement, "steps": cpu.steps, "side_calls": run.side_calls}

    @staticmethod
    def registry_sizes(static_table):
        """{model index: (sx, sy, sz)} for every registry model (alpha_static scan output)."""
        sizes = static_table.get("model_sizes", {})
        out = {}
        for k, name in static_table["registry"].items():
            sz = sizes.get(name)
            if sz:
                out[int(k)] = tuple(sz)
        return out

    @property
    def state(self):
        return struct.unpack_from("<i", self.this, 8)[0]

    @property
    def timer(self):
        return struct.unpack_from("<i", self.this, 4)[0]
