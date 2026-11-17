"""Execute Alpha's 0x5AD320 on a synthetic 17x17 platform at the lake zone's first prop candidate."""

import argparse
import json
import struct
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[3]))
from tools.alpha_models.universal.drawmodel_emu import RETURN_VA, STACK_BASE, STACK_SIZE  # noqa: E402
from tools.alpha_models.universal.unicorn_bridge import UnicornRun  # noqa: E402
from tools.alpha_models.world.dungeon_oracle import apply_static_init  # noqa: E402
from tools.alpha_models.world.terrain_oracle import Oracle, WORLD  # noqa: E402


def trace(exe):
    oracle = Oracle(exe, 666666, fast_noise=True)
    apply_static_init(oracle)
    cpu = oracle.cpu
    zone = oracle.alloc(0xC8)
    oracle.call(0x6240F0, [], ecx=zone)
    zx, zz = 32795, 32804
    cpu.mem.write(zone + 0x60, struct.pack("<2i", zx, zz))
    field = cpu.mem.u(zone + 0xA8, 4)
    x, z = 8395638, 8398007
    voxel = oracle.alloc(4)
    cpu.mem.w(voxel, 4, 0x240F0F0F)
    for px in range(x - 8, x + 9):
        for pz in range(z - 8, z + 9):
            cell = field + ((px - zx * 256) + (pz - zz * 256) * 256) * 0x20
            cpu.mem.write(cell + 0x10, struct.pack("<2iIi", 99, 91, voxel, 1))

    position = oracle.alloc(24)
    cpu.mem.write(position, struct.pack("<3q", (x << 16) + 32768, (z << 16) + 32768, 100 << 16))
    # 0x5AD320 uses this terrain sample only for its gate. The footprint checks read the real zone field.
    cpu.stubs[0x5F9340] = (lambda current: current.fpu.append(0.5), 12)
    cpu.fpu.clear()
    cpu.r["esp"] = STACK_BASE + STACK_SIZE - 0x100
    for arg in (position, zone, RETURN_VA):
        cpu.push(arg)
    cpu.r["ecx"] = WORLD
    cpu.eip = 0x5AD320
    crt_in = 1923529121
    oracle.crt.srand(crt_in)
    run = UnicornRun(cpu, oracle.image)
    cpu.mem = run.mem
    rand = oracle.crt.rand
    draw_sites = []

    def record_rand():
        draw_sites.append(run.last_return)
        return rand()

    oracle.crt.rand = record_rand
    run.run(until=RETURN_VA)
    begin, end = struct.unpack("<II", cpu.mem.read(zone + 0x0C, 8))
    objects = []
    for ptr in range(begin, end, 0x188):
        kind, px, pz, y, yaw, width, depth, height = struct.unpack("<i4x3qi3f", cpu.mem.read(ptr, 48))
        objects.append([kind, px / 65536, y / 65536, pz / 65536, yaw, width, depth, height])
    return {
        "placed": bool(run.cpu.r["eax"] & 0xFF),
        "objects": objects,
        "crt_in": crt_in,
        "crt_out": oracle.crt.state,
        "draw_sites": [hex(site) for site in draw_sites],
    }


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("exe", type=Path)
    print(json.dumps(trace(parser.parse_args().exe), indent=2))
