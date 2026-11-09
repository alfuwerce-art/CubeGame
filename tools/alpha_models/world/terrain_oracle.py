"""Execute Alpha's world-generation helpers from Cube.exe bytes (M0-02 research, U1/U2).

Status of numbers produced here: EMULATED_ALPHA_CODE — Alpha's own machine code on a synthetic world struct.
Nothing is guessed: unimplemented instructions, unmapped reads and unstubbed imports raise.

Functions:
  0x5D7A70 climate cell (thiscall world; rx, rz)            -> int* cell (0x1C bytes)
  0x5C5E20 terrain_generateColumnColor (thiscall; x, z, p3) -> float in ST0 (base height)
  0x4C0EF0 perlinNoise2D_cosInterp (cdecl double, double)   -> float in ST0

Imports: srand/rand are the MSVC CRT LCG, operator new is a bump allocator on a synthetic heap; pure
libm maths uses the drawModel stubs (IEEE double). Any other import raises with its name.

Usage: python -I terrain_oracle.py <Cube.exe> <master_seed> <out.json>
"""
from __future__ import annotations

import json
import struct
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[3]))
from tools.alpha_models.anim.pe_bytes import load  # noqa: E402
from tools.alpha_models.universal.drawmodel_emu import EmuCPU, LIBM_STUBS, STACK_BASE, STACK_SIZE  # noqa: E402
from tools.alpha_models.universal.x86emu import EmuError, MASK, REG32  # noqa: E402

WORLD = 0x30000000
WORLD_SIZE = 0x00900000
HEAP = 0x40000000
HEAP_SIZE = 0x01000000
START_VOXEL = 32800 * 256 + 128


TEB = 0x7FFD0000


class WorldCPU(EmuCPU):
    """EmuCPU + SHLD/SHRD (Alpha 16.16 world coordinates) + FS segment over a synthetic TEB (SEH frames)."""

    fs_active = False

    def step(self):
        if self.mem.read(self.eip, 1)[0] == 0x64:
            self.eip += 1
            op = self.mem.read(self.eip, 1)[0]
            if op in (0xA1, 0xA3):                       # mov eax, fs:[moffs] / mov fs:[moffs], eax
                self.steps += 1
                self.eip += 1
                addr = TEB + self.imm(4)
                if op == 0xA1:
                    self.r["eax"] = self.mem.u(addr, 4)
                else:
                    self.mem.w(addr, 4, self.r["eax"])
                return
            self.fs_active = True
            try:
                return super().step()
            finally:
                self.fs_active = False
        return super().step()

    def modrm(self):
        mod, reg, kind, val = super().modrm()
        if self.fs_active and kind == "mem":
            val = (val + TEB) & MASK
        return mod, reg, kind, val

    def _exec0f(self, size, rep, start):
        save = self.eip
        op2 = self.fetch(1)[0]
        if op2 in (0xA4, 0xA5, 0xAC, 0xAD) and size == 4:
            _, reg, kind, val = self.modrm()
            count = (self.fetch(1)[0] if op2 in (0xA4, 0xAC) else self.r["ecx"]) & 31
            if count == 0:
                return
            dest = self.rd(kind, val, 4)
            src = self.r[REG32[reg]]
            if op2 in (0xA4, 0xA5):            # shld
                res = ((dest << count) | (src >> (32 - count))) & MASK
                self.cf = bool((dest >> (32 - count)) & 1)
            else:                              # shrd
                res = ((dest >> count) | (src << (32 - count))) & MASK
                self.cf = bool((dest >> (count - 1)) & 1)
            self.wr(kind, val, 4, res)
            self.zf = res == 0
            self.sf = bool(res >> 31)
            self.pf = bin(res & 0xFF).count("1") % 2 == 0
            self.of = False
            return
        self.eip = save
        return super()._exec0f(size, rep, start)


class Crt:
    def __init__(self):
        self.state = 1

    def srand(self, seed):
        self.state = seed & MASK

    def rand(self):
        self.state = (self.state * 214013 + 2531011) & MASK
        return (self.state >> 16) & 0x7FFF


def import_slots(image):
    """IAT slot VA -> 'dll!name' from the PE import directory (read-only)."""
    data = image.data
    pe = struct.unpack_from("<I", data, 0x3C)[0]
    opt = pe + 24
    import_rva = struct.unpack_from("<I", data, opt + 96 + 8)[0]
    base = image.image_base
    slots = {}
    desc = base + import_rva
    while True:
        oft, _, _, name_rva, ft = struct.unpack("<5I", image.read(desc, 20))
        if name_rva == 0:
            break
        dll = image.read(base + name_rva, 64).split(b"\0")[0].decode("ascii", "replace")
        lookup = oft or ft
        i = 0
        while True:
            entry = struct.unpack("<I", image.read(base + lookup + 4 * i, 4))[0]
            if entry == 0:
                break
            if entry & 0x80000000:
                name = f"#{entry & 0xFFFF}"
            else:
                name = image.read(base + entry + 2, 96).split(b"\0")[0].decode("ascii", "replace")
            slots[base + ft + 4 * i] = f"{dll}!{name}"
            i += 1
        desc += 20
    return slots


class Oracle:
    @staticmethod
    def _noise_stub(cpu):
        """perlinNoise2D_cosInterp replaced by its port (identical results on 300 executed samples, see
        compare_port.py); cdecl (double x, double y) -> ST0. Only used with fast_noise=True to make long
        functions such as generateRegion tractable in the Python interpreter."""
        from tools.alpha_models.world.terrain_port import noise
        x, y = struct.unpack("<2d", cpu.mem.read(cpu.r["esp"] + 4, 16))
        cpu.fpu.append(noise(x, y))

    def __init__(self, exe, master_seed, extra_stubs=None, fast_noise=False):
        self.image = load(exe)
        self.crt = Crt()
        self.heap_top = HEAP
        stubs = dict(LIBM_STUBS)
        stubs.update(extra_stubs or {})
        self.cpu = WorldCPU(self.image, stubs=stubs, max_steps=50_000_000)
        self.cpu.mem.map(STACK_BASE, STACK_SIZE)
        self.cpu.mem.map(WORLD, WORLD_SIZE)
        self.cpu.mem.map(HEAP, HEAP_SIZE)
        self.cpu.mem.map(TEB, 0x1000)
        # Zero-filled tails of sections (.bss): virtual size beyond the raw data, as the Windows loader maps them.
        for sec in self.image.sections:
            if sec.virtual_size > sec.raw_size:
                start = self.image.image_base + sec.virtual_address + sec.raw_size
                self.cpu.mem.map(start, sec.virtual_size - sec.raw_size)
        self.imports = import_slots(self.image)
        self._install_imports()
        if fast_noise:
            self.cpu.stubs[0x4C0EF0] = (self._noise_stub, 0)
            self.cpu.max_steps = 400_000_000
        self.seed_world(master_seed)

    def _install_imports(self):
        # `skip` = bytes between esp and the first argument (0 for `call [IAT]`, 4 inside a thunk stub).
        def srand(cpu, skip):
            self.crt.srand(cpu.mem.u(cpu.r["esp"] + skip, 4))
            return 0

        def rand(cpu, skip):
            return self.crt.rand()

        def new(cpu, skip):
            return self.alloc(cpu.mem.u(cpu.r["esp"] + skip, 4))

        def delete(cpu, skip):
            return 0   # freeing synthetic heap memory does not influence results

        known = {"srand": srand, "rand": rand, "??2@YAPAXI@Z": new, "??3@YAXPAX@Z": delete}

        def noop(cpu, skip):
            return cpu.r["eax"]

        def resolve(slot):
            name = self.imports[slot].split("!")[1]
            if name in known:
                return known[name]
            if "_Container_base0" in name:
                # std::_Container_base0 members are empty in the release STL (no iterator debugging).
                return noop

            def fail(cpu, skip):
                raise EmuError(f"unstubbed import {self.imports[slot]} (slot 0x{slot:08X})")
            return fail

        def via_iat(fn):
            return lambda cpu: fn(cpu, 0)

        def via_thunk(fn):
            def stub(cpu):
                cpu.r["eax"] = fn(cpu, 4) & MASK
            return stub

        for slot in self.imports:
            self.cpu.iat[slot] = via_iat(resolve(slot))

        lo, hi = 0x68D000, 0x68E000
        blob = self.image.read(lo, hi - lo)
        for i in range(len(blob) - 6):
            if blob[i] == 0xFF and blob[i + 1] == 0x25:
                slot = int.from_bytes(blob[i + 2:i + 6], "little")
                if slot in self.imports and (lo + i) not in self.cpu.stubs:
                    self.cpu.stubs[lo + i] = (via_thunk(resolve(slot)), 0)

    def seed_world(self, master):
        """World seed table exactly as crt_stl.cpp 11431-11459 (srand(master); rand()%100000 ...)."""
        self.crt.srand(master)
        w = self.cpu.mem.w
        w(WORLD + 0x800164, 4, master)
        w(WORLD + 0x800188, 4, self.crt.rand() % 100000)
        for i in range(4):
            w(WORLD + 0x800168 + 4 * i, 4, self.crt.rand() % 100000)
        # loops of 20,2,2,2,2,8,6,6,10,4,2 values are contiguous: 64 values from +0x80018C
        for i in range(64):
            w(WORLD + 0x80018C + 4 * i, 4, self.crt.rand() % 100000)
        for i in range(4):
            w(WORLD + 0x800178 + 4 * i, 4, self.crt.rand() % 100000)
        for i in range(4):  # +0x80028C (2) and +0x800294 (2), contiguous
            w(WORLD + 0x80028C + 4 * i, 4, self.crt.rand() % 100000)
        # Spawn point (world+0x8000F0/F4, floats in voxels) = CubeGame start: centre of zone 32800 (saved Alpha hero
        # zone 32800.5, OQ-22 PARTIAL). Selects the "start region" branch of the climate cell (0x5D7A70).
        self.cpu.mem.write(WORLD + 0x8000F0, struct.pack("<2f", START_VOXEL, START_VOXEL))

    def alloc(self, size):
        va = self.heap_top
        self.heap_top += (size + 15) & ~15
        if self.heap_top > HEAP + HEAP_SIZE:
            raise EmuError("synthetic heap exhausted")
        self.cpu.mem.write(va, bytes(size))
        return va

    def i32(self, va):
        v = self.cpu.mem.u(va, 4)
        return v - (1 << 32) if v & 0x80000000 else v

    def f32(self, va):
        return struct.unpack("<f", self.cpu.mem.read(va, 4))[0]

    def call(self, va, args, ecx=WORLD):
        self.cpu.fpu = []
        self.cpu.steps = 0
        return self.cpu.run(va, args=args, ecx=ecx)

    def call_float(self, va, args, ecx=WORLD):
        self.call(va, args, ecx)
        if not self.cpu.fpu:
            raise EmuError(f"0x{va:08X} returned no x87 value")
        return self.cpu.fpu[-1]

    def cell(self, rx, rz):
        ptr = self.call(0x5D7A70, [rx, rz])
        return {
            "rx": rx, "rz": rz,
            "anchor_x": self.i32(ptr), "anchor_z": self.i32(ptr + 4),
            "flag": self.cpu.mem.u(ptr + 8, 1),
            "f3": self.f32(ptr + 12), "f4": self.f32(ptr + 16),
            "id": self.i32(ptr + 20), "base": self.i32(ptr + 24),
        }

    def noise(self, x, y):
        args = list(struct.unpack("<2I", struct.pack("<d", x))) + list(struct.unpack("<2I", struct.pack("<d", y)))
        return self.call_float(0x4C0EF0, args, ecx=0)


def dataset(exe, master, out, count_line=160, count_random=140):
    """Base height (0x5C5E20) and mountain mask (0x5EFFA0) at voxel coordinates around the world centre.

    Region pointers (world+0xBC) are left empty, so per-column landmark rules (types 1/2/4/6/7/13) are not
    triggered: this is Alpha's natural terrain without region landmarks.
    """
    import random

    o = Oracle(exe, master)
    for rx in range(509, 516):
        for rz in range(509, 516):
            o.cell(rx, rz)
    points = []
    z_line = 512 * 0x4000 + 0x2000
    for i in range(count_line):
        points.append((511 * 0x4000 + i * 160, z_line))
    rng = random.Random(master)
    for _ in range(count_random):
        points.append((rng.randrange(510 * 0x4000, 514 * 0x4000), rng.randrange(510 * 0x4000, 514 * 0x4000)))
    samples = []
    for x, z in points:
        base = o.call_float(0x5C5E20, [x, z, 0])
        mountain = o.call_float(0x5EFFA0, [x, z, 0])
        samples.append({"x": x, "z": z, "base": base, "mountain": mountain})
    result = {
        "status": "EMULATED_ALPHA_CODE",
        "source": "Cube.exe 0x5C5E20 / 0x5EFFA0 / 0x5D7A70 executed by tools/alpha_models/world/terrain_oracle.py",
        "units": "x, z in terrain voxels (256 per zone, 0x4000 per region)",
        "master_seed": master,
        "world_seed": o.i32(WORLD + 0x800188),
        "cells": [o.cell(rx, rz) for rx in range(509, 516) for rz in range(509, 516)],
        "samples": samples,
    }
    Path(out).write_text(json.dumps(result, indent=0), encoding="utf-8")
    print("samples", len(samples), "base range", min(s["base"] for s in samples), max(s["base"] for s in samples),
          "mountain range", min(s["mountain"] for s in samples), max(s["mountain"] for s in samples))


def main():
    exe, master, out = sys.argv[1], int(sys.argv[2]), sys.argv[3]
    if len(sys.argv) > 4 and sys.argv[4] == "dataset":
        dataset(exe, master, out)
        return
    o = Oracle(exe, master)
    result = {"master_seed": master, "world_seed": o.i32(WORLD + 0x800188), "status": "EMULATED_ALPHA_CODE"}
    result["noise"] = [{"x": x, "y": y, "v": o.noise(x, y)} for x, y in
                       [(0.0, 0.0), (1.5, 2.25), (345.678, 9012.3), (98584.01, 985.5), (8396800 * 0.01 + 34432.0, 8396800 * 0.01 + 8992.0)]]
    cells = []
    for rx in range(510, 515):
        for rz in range(510, 515):
            cells.append(o.cell(rx, rz))
    result["cells"] = cells
    Path(out).write_text(json.dumps(result, indent=1), encoding="utf-8")
    print(json.dumps({k: result[k] for k in ("world_seed",)}), "noise", [round(n["v"], 6) for n in result["noise"]])
    print("cells", [(c["rx"], c["rz"], c["base"]) for c in cells[:6]])


if __name__ == "__main__":
    main()
