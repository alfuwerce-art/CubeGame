"""Execute Alpha's kind-5 tree growth on an empty field at valley coordinates."""

import argparse
import hashlib
import json
import struct
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[3]))
from tools.alpha_models.universal.drawmodel_emu import RETURN_VA, STACK_BASE, STACK_SIZE  # noqa: E402
from tools.alpha_models.universal.unicorn_bridge import UnicornRun  # noqa: E402
from tools.alpha_models.world.dungeon_oracle import apply_static_init  # noqa: E402
from tools.alpha_models.world.terrain_oracle import Oracle, WORLD  # noqa: E402


def trace(exe, edge):
    oracle = Oracle(exe, 666666, fast_noise=True)
    apply_static_init(oracle)
    cpu = oracle.cpu
    zone = oracle.alloc(0xC8)
    oracle.call(0x6240F0, [], ecx=zone)
    zx = zz = 32812
    cpu.mem.write(zone + 0x60, struct.pack("<2i", zx, zz))
    field = cpu.mem.u(zone + 0xA8, 4)
    temperature, humidity = 0.5979705452919006, 0.7230933904647827
    empty_column = struct.pack("<3f20x", 0.0, temperature, humidity)
    cpu.mem.write(field, empty_column * (256 * 256))
    x, z, y, radius, height, seed = (
        (8399881, 8399881, 76, 7, 14, 4167882435) if edge else (8399918, 8399900, 64, 10, 20, 2214495820)
    )
    draw_sites = []
    voxels = {}

    def record_write(current):
        px, pz, py, ptr = struct.unpack("<3iI", current.mem.read(current.r["esp"] + 4, 16))
        if zx * 256 <= px < (zx + 1) * 256 and zz * 256 <= pz < (zz + 1) * 256:
            voxels[px, pz, py] = current.mem.u(ptr + 3, 1)

    cpu.hooks[0x44E7A0] = record_write
    cpu.fpu.clear()
    cpu.r["esp"] = STACK_BASE + STACK_SIZE - 0x100
    for arg in (zone, 5, height, radius, y, z, x, RETURN_VA):
        cpu.push(arg)
    cpu.r["ecx"] = WORLD
    cpu.eip = 0x5DF960
    oracle.crt.srand(seed)
    run = UnicornRun(cpu, oracle.image)
    cpu.mem = run.mem
    rand = oracle.crt.rand

    def record_rand():
        draw_sites.append(hex(run.last_return))
        return rand()

    oracle.crt.rand = record_rand
    run.run(until=RETURN_VA)
    geometry = b"".join(struct.pack("<3iB", *point, flags) for point, flags in sorted(voxels.items()))
    return {
        "status": "EMULATED_ALPHA_CODE",
        "position": [x, z, y],
        "radius": radius,
        "height": height,
        "crt_in": seed,
        "crt_out": oracle.crt.state,
        "draw_count": len(draw_sites),
        "draw_sites": draw_sites,
        "voxel_count": len(voxels),
        "voxel_hash": hashlib.sha256(geometry).hexdigest(),
    }


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("exe", type=Path)
    parser.add_argument("--edge", action="store_true")
    options = parser.parse_args()
    print(json.dumps(trace(options.exe, options.edge), indent=2))
