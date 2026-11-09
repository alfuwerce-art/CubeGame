"""Execute Alpha generateRegion (0x5DA280) and dump the 64 'voxel column 8' records of a region.

Research oracle for M0-02 region landmarks; values are EMULATED_ALPHA_CODE with two verified substitutions:
perlinNoise2D_cosInterp, terrain base 0x5C5E20 and mountain mask 0x5EFFA0 are replaced by their Python ports,
which equal the executed Alpha functions on 300 samples (compare_port.py) as long as NO landmark columns exist
in the world grid. Each region is therefore generated in a fresh synthetic world ("isolation"): Alpha itself
generates regions in streaming order and later regions see earlier landmarks; CubeGame fixes the order-free
variant (deviation recorded in the M0-02 report).

Region struct 0x15A28: +0xC level, +0x10 variant, +0x14 orientation, +0x14018 + i*0x68 columns.
Usage: python -I region_oracle.py <Cube.exe> <master_seed> <rx0> <rz0> <radius> [out.json]
"""
import json
import struct
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[3]))
from tools.alpha_models.world.terrain_oracle import Oracle, WORLD  # noqa: E402
from tools.alpha_models.world.compare_port import seeds_for  # noqa: E402
from tools.alpha_models.world.landmark_port import LandmarkPort  # noqa: E402
from tools.alpha_models.universal.x86emu import EmuError  # noqa: E402

COLUMN = 0x68


def region_ptr(o, rx, rz):
    return o.cpu.mem.u(WORLD + 0xBC + (rx * 0x400 + rz) * 4, 4)


def dump_region(o, rx, rz):
    r = region_ptr(o, rx, rz)
    if r == 0:
        return None
    out = {"rx": rx, "rz": rz, "level": o.i32(r + 0xC), "variant": o.i32(r + 0x10), "orientation": o.i32(r + 0x14),
           "columns": []}
    for i in range(64):
        c = r + 0x14018 + i * COLUMN
        raw = o.cpu.mem.read(c, COLUMN)
        words = struct.unpack("<26i", raw)
        out["columns"].append({
            "i": i,
            "x": struct.unpack("<q", raw[0:8])[0] / 65536.0,
            "z": struct.unpack("<q", raw[8:16])[0] / 65536.0,
            "f16": struct.unpack("<f", raw[16:20])[0],
            "f20": struct.unpack("<f", raw[20:24])[0],
            "type": words[6],
            "words": list(words[7:26]),
            "floats": list(struct.unpack("<19f", raw[28:104])),
        })
    # 16-byte zone grid at region+0x18, index lx*64 + lz: +0 type, +1 sub, +4 id, +8 level, +0xC class (non-zero only)
    out["zones"] = []
    raw = o.cpu.mem.read(r + 0x18, 4096 * 16)
    for idx in range(4096):
        rec = raw[idx * 16:(idx + 1) * 16]
        if any(rec):
            ztype, zsub = rec[0], rec[1]
            zid, zlevel, zcls = struct.unpack("<iii", rec[4:16])
            out["zones"].append({"lx": idx >> 6, "lz": idx & 63, "type": ztype, "sub": zsub, "b2": rec[2], "b3": rec[3],
                                 "id": zid, "level": zlevel, "cls": zcls})
    return out


def generate_isolated(exe, master, rx, rz):
    o = Oracle(exe, master, fast_noise=True)
    for x in range(rx - 4, rx + 5):
        for z in range(rz - 4, rz + 5):
            o.cell(x, z)
    cells = {}
    for x in range(rx - 4, rx + 5):
        for z in range(rz - 4, rz + 5):
            cells[(x, z)] = o.cell(x, z)
    # landmark-free Alpha terrain (incl. the rare-cell term 0x5EF040): regions are generated in isolation
    port = LandmarkPort(seeds_for(master), cells, lambda x, z: None)

    def base_stub(cpu):
        x, z = cpu.mem.u(cpu.r["esp"] + 4, 4), cpu.mem.u(cpu.r["esp"] + 8, 4)
        cpu.fpu.append(port.base(x - (1 << 32) if x & 0x80000000 else x, z - (1 << 32) if z & 0x80000000 else z))

    def mountain_stub(cpu):
        x, z = cpu.mem.u(cpu.r["esp"] + 4, 4), cpu.mem.u(cpu.r["esp"] + 8, 4)
        cpu.fpu.append(port.mountain(x - (1 << 32) if x & 0x80000000 else x, z - (1 << 32) if z & 0x80000000 else z))

    # Single-player world with an open world database (world+0xA4 != 0) and no stored region blobs (fresh world):
    # with the database open Alpha turns the climate-anchor column into a Village (type 1) and then looks
    # for saved data, which db_getBlobInto (0x4498D0, thiscall, ret 8) reports as absent.
    o.cpu.mem.w(WORLD + 0xA4, 4, 1)
    # world+0xB4 != 0 skips the stored-region loading block (stringstream keys + db reads); nothing is stored anyway.
    o.cpu.mem.w(WORLD + 0xB4, 1, 1)
    o.cpu.stubs[0x4498D0] = ((lambda cpu: cpu.r.__setitem__("eax", 0)), 8)
    o.cpu.stubs[0x5C5E20] = (base_stub, 12)   # thiscall: callee pops 3 args (verified by esp after the real call)
    o.cpu.stubs[0x5EFFA0] = (mountain_stub, 12)
    o.call(0x5DA280, [rx, rz])
    return dump_region(o, rx, rz)


def main():
    exe, master, rx0, rz0, radius = sys.argv[1], int(sys.argv[2]), int(sys.argv[3]), int(sys.argv[4]), int(sys.argv[5])
    regions = []
    for rx in range(rx0 - radius, rx0 + radius + 1):
        for rz in range(rz0 - radius, rz0 + radius + 1):
            try:
                regions.append(generate_isolated(exe, master, rx, rz))
            except EmuError as e:
                print("stop at region", rx, rz, ":", e)
                raise SystemExit(1)
            r = regions[-1]
            types = {}
            for c in r["columns"]:
                types[c["type"]] = types.get(c["type"], 0) + 1
            print(r["rx"], r["rz"], "level", r["level"], "variant", r["variant"], "orient", r["orientation"], "types", types, flush=True)
    if len(sys.argv) > 6:
        Path(sys.argv[6]).write_text(json.dumps(regions), encoding="utf-8")


if __name__ == "__main__":
    main()
