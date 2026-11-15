"""Trace Alpha's 0x5DC6E0 without launching Cube.exe.

Usage: python landmark_patrol_oracle.py <Cube.exe> <seed> <zone_x> <zone_z> <out.json>
The default runs the zone generator with the terrain oracle substitutions. --composition
starts at 0x5DC6E0 with a synthetic voxel field and a fixed grid of input points.
"""

import argparse
import hashlib
import json
import struct
import sys
from collections import Counter
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[3]))
from tools.alpha_models.universal.drawmodel_emu import RETURN_VA, STACK_BASE, STACK_SIZE  # noqa: E402
from tools.alpha_models.universal.unicorn_bridge import UnicornRun  # noqa: E402
from tools.alpha_models.world.dungeon_oracle import Stop, args, apply_static_init, prepare  # noqa: E402
from tools.alpha_models.world.terrain_oracle import Oracle, WORLD  # noqa: E402


def composition_arguments(oracle, zone_x, zone_z, composition):
    zone = oracle.alloc(0xC8)
    oracle.call(0x6240F0, [], ecx=zone)
    mem = oracle.cpu.mem
    mem.write(zone + 0x60, struct.pack("<2i", zone_x, zone_z))
    if composition <= 5:
        # The camp footprint needs solid ground. Native column reads supply it
        # below Bottom, so this flat field needs no individual voxel buffers.
        field = mem.u(zone + 0xA8, 4)
        mem.write(field, struct.pack("<16x2i8x", 64, 56) * (256 * 256))
    scale = 65536
    x0, z0 = zone_x * 256, zone_z * 256
    points = [
        ((x0 + x) * scale + scale // 2, (z0 + z) * scale + scale // 2, (48 + (x + z) % 13) * scale)
        for x in range(40, 185, 36) for z in range(40, 185, 36)
    ]
    begin = oracle.alloc(len(points) * 24)
    end = begin + len(points) * 24
    mem.write(begin, b"".join(struct.pack("<3q", *point) for point in points))
    vector = oracle.alloc(12)
    mem.write(vector, struct.pack("<3I", begin, end, end))
    landmark_type, landmark_id = {6: (11, 9), 9: (4, 9), 10: (3, 10)}.get(composition, (11, composition))
    landmark = oracle.alloc(0x68)
    centre_x, centre_z, _ = points[len(points) // 2]
    mem.write(landmark, struct.pack("<2q2f5i", centre_x, centre_z, 300.0, 0.0,
                                    landmark_type, 0, landmark_id, 60, 3))
    return zone, landmark, vector, 5


def trace(exe, seed, zone_x, zone_z, composition=None):
    if composition is None:
        oracle, carves = prepare(exe, seed, zone_x, zone_z)
    else:
        oracle, carves = Oracle(exe, seed, fast_noise=True), []
    apply_static_init(oracle)
    cpu = oracle.cpu
    # 0x5D9460 enumerates world+4 before looking for a mission. A head pointing
    # to itself represents the empty actor map of this generation-only run.
    actor_head = oracle.alloc(0x20)
    cpu.mem.write(actor_head, struct.pack("<3I", actor_head, actor_head, actor_head))
    cpu.mem.w(actor_head + 0xD, 1, 1)
    cpu.mem.w(WORLD + 4, 4, actor_head)
    placement = {}
    zone_stages = {}
    entry = {}
    draws = []
    placement_draws = []
    decoration_draws = Counter()
    tree_draws = Counter()
    trees = []
    post_tree_prop = None
    surface_props = []
    boulders = []
    boulder_writes = hashlib.sha256()
    boulder_voxels = set()
    boulder_write_count = 0
    spawns = []
    leaders = {}
    entry_address = 0x5DC6E0
    return_address = 0x5EACEC
    spawned_at = {0x5DE9A9, 0x5DEDC9, 0x5DF1D9}

    def record_placement(_cpu):
        placement["crt_in"] = oracle.crt.state

    def record_entry(current):
        zone, column, points, group_size = args(current, 4)
        begin, end = struct.unpack("<II", current.mem.read(points, 8))
        entry.update({
            "landmark": oracle.i32(column + 0x18),
            "landmark_id": oracle.i32(column + 0x20),
            "landmark_center_fixed_16_16": struct.unpack("<2q", current.mem.read(column, 16)),
            "radius": oracle.f32(column + 0x10),
            "level": oracle.i32(column + 0x24),
            "quality": oracle.i32(column + 0x28),
            "zone_record": list(current.mem.read(zone + 0x78, 2)),
            "group_size_argument": group_size,
            "points_fixed_16_16": list(struct.iter_unpack("<3q", current.mem.read(begin, end - begin))),
            "crt_in": oracle.crt.state,
        })
        if composition is None and "crt_in" not in placement:
            raise RuntimeError("Missing patrol point selection entry at 0x5EAA59")
        print(f"Entered 0x5DC6E0: landmark {entry['landmark']}, {len(entry['points_fixed_16_16'])} points", flush=True)

    def record_spawn(current):
        caller = current.mem.u(current.r["esp"], 4)
        if caller in spawned_at:
            spawns.append((caller - 5, current.r["ecx"]))

    def record_leader(current):
        # The companion copies the leader's ID from EBX. Resolve its index even
        # when the synthetic world has not assigned the persistent IDs yet.
        site, follower = spawns[-1]
        if site != 0x5DF1D4:
            raise ValueError("Companion assignment outside follower construction")
        leaders[follower] = current.r["ebx"]

    def record_return(_cpu):
        if entry:
            raise Stop()

    def record_surface_prop(current):
        if current.mem.u(current.r["esp"], 4) == 0x5E8F43:
            _, position = args(current, 2)
            surface_props.append({"position": struct.unpack("<3q", current.mem.read(position, 24))})

    def record_surface_prop_result(current):
        surface_props[-1]["placed"] = bool(current.r["eax"] & 0xFF)

    def record_boulder_start(current):
        frame = current.r["ebp"]
        zone_stages.update(before_boulders=oracle.crt.state,
                           boulder_temperature=oracle.f32(frame - 0x12FC),
                           boulder_humidity=oracle.f32(frame - 0x1310))

    def record_boulder(current):
        frame = current.r["ebp"]
        boulders.append({
            "center": [current.r["edi"], current.r["eax"]],
            "radii": [oracle.i32(frame - offset) for offset in (0x12FC, 0x12EC, 0x12D0)],
        })

    def record_boulder_base(current):
        boulders[-1]["base"] = oracle.i32(current.r["ebp"] - 0x12D4)

    def record_boulder_write(current):
        nonlocal boulder_write_count
        x, z, y, voxel = struct.unpack("<3iI", current.mem.read(current.r["esp"], 16))
        boulder_writes.update(struct.pack("<3iB", x, z, y, current.mem.u(voxel + 3, 1)))
        boulder_voxels.add((x, z, y))
        boulder_write_count += 1

    def record_tree(current):
        x, z, y, radius, height, kind = struct.unpack("<6i", current.mem.read(current.r["esp"], 24))
        trees.append({
            "position": [x, z, y],
            "radius": radius,
            "height": height,
            "kind": kind,
            "crt_in": oracle.crt.state,
        })

    def record_tree_return(_current):
        if trees and "crt_out" not in trees[-1]:
            trees[-1]["crt_out"] = oracle.crt.state

    def record_post_tree_prop(current):
        nonlocal post_tree_prop
        ptr = current.mem.u(current.r["esp"], 4)
        kind, x, z, y, yaw, width, depth, height = struct.unpack("<i4x3qi3f", current.mem.read(ptr, 48))
        post_tree_prop = {
            "kind": kind,
            "position_fixed_16_16": [x, z, y],
            "yaw": yaw,
            "dimensions": [width, depth, height],
        }

    def require_model_registry(current):
        if current.r["ecx"] == 0:
            caller = current.mem.u(current.r["esp"], 4)
            stage = "in 0x5DC6E0" if entry else "before 0x5DC6E0"
            raise RuntimeError(f"Model registry missing {stage} at return address 0x{caller:08X}")

    cpu.hooks.update({
        0x5E8490: lambda _: zone_stages.update(after_terrain=oracle.crt.state),
        0x5E888E: lambda _: zone_stages.update(after_river_stones=oracle.crt.state),
        0x5AD320: record_surface_prop,
        0x5E8F43: record_surface_prop_result,
        0x5E8FB5: record_boulder_start,
        0x5E911F: record_boulder,
        0x5E9344: record_boulder_base,
        0x5E95B1: record_boulder_write,
        0x5E9655: lambda _: zone_stages.update(after_boulders=oracle.crt.state),
        0x5E9E7D: lambda _: zone_stages.update(before_trees=oracle.crt.state),
        0x5EA7AE: record_tree,
        0x5EA7B3: record_tree_return,
        0x5EA7E7: lambda _: zone_stages.update(after_trees=oracle.crt.state),
        0x5EA9B6: record_post_tree_prop,
        0x5EAA59: record_placement,
        entry_address: record_entry,
        return_address: record_return,
        0x5ADB20: record_spawn,
        0x5DF334: record_leader,
        0x424820: require_model_registry,
    })
    if composition is None:
        arguments = (zone_x, zone_z)
        start = 0x5E4850
    else:
        arguments = composition_arguments(oracle, zone_x, zone_z, composition)
        oracle.crt.srand(seed)
        start = entry_address
    cpu.fpu.clear()
    cpu.r["esp"] = STACK_BASE + STACK_SIZE - 0x100
    for arg in reversed((RETURN_VA, *arguments)):
        cpu.push(arg)
    cpu.r["ecx"] = WORLD
    cpu.eip = start
    run = UnicornRun(cpu, oracle.image)
    cpu.mem = run.mem

    heap_start = 0x80000000
    heap_end = heap_start + 0x20000000
    run.uc.mem_map(heap_start, heap_end - heap_start)
    oracle.heap_top = heap_start

    def alloc(size):
        address = oracle.heap_top
        oracle.heap_top += (size + 15) & ~15
        if oracle.heap_top > heap_end:
            raise RuntimeError("Patrol trace heap exhausted")
        return address  # Unicorn maps the fresh block to zero-filled storage.

    oracle.alloc = alloc
    rand = oracle.crt.rand

    def record_rand():
        value = rand()
        if entry:
            draws.append((run.last_return, oracle.crt.state))
        elif placement:
            placement_draws.append((run.last_return, oracle.crt.state))
        elif "before_trees" in zone_stages:
            tree_draws[run.last_return] += 1
        elif "after_river_stones" in zone_stages:
            decoration_draws[run.last_return] += 1
        return value

    oracle.crt.rand = record_rand
    try:
        run.run(until=RETURN_VA)
    except Stop:
        pass
    else:
        if composition is None or not entry:
            raise ValueError(f"Zone {zone_x}/{zone_z} returned without a landmark patrol call")

    placement["draws"] = placement_draws

    def behavior(ptr):
        sequential = cpu.mem.u(ptr + 0x109C, 4)
        if not sequential or cpu.mem.u(sequential, 4) != 0x703A00:
            raise ValueError(f"Expected SequentialBehavior in spawn at 0x{ptr:08X}")
        head = cpu.mem.u(sequential + 4, 4)
        count = cpu.mem.u(sequential + 8, 4)
        node = cpu.mem.u(head, 4)
        children = []
        for _ in range(count):
            child = cpu.mem.u(node + 8, 4)
            vtable = cpu.mem.u(child, 4)
            record = {"vtable": f"0x{vtable:06X}"}
            if vtable == 0x71A770:
                begin, end = struct.unpack("<II", cpu.mem.read(child + 4, 8))
                record["route_fixed_16_16"] = list(struct.iter_unpack("<3q", cpu.mem.read(begin, end - begin)))
            elif vtable == 0x6FD868:
                record["leader_id"] = struct.unpack("<q", cpu.mem.read(child + 8, 8))[0]
            children.append(record)
            node = cpu.mem.u(node, 4)
        if node != head:
            raise ValueError(f"Invalid SequentialBehavior list in spawn at 0x{ptr:08X}")
        return children

    spawn_indices = {ptr: index for index, (_, ptr) in enumerate(spawns)}
    records = [
        {
            "constructor_call": site,
            "position_fixed_16_16": struct.unpack("<3q", cpu.mem.read(ptr + 0x10, 24)),
            "role": oracle.i32(ptr + 0x28),
            "kind": oracle.i32(ptr + 0x2C),
            "id": struct.unpack("<q", cpu.mem.read(ptr + 0x48, 8))[0],
            "level": oracle.i32(ptr + 0x34),
            "yaw": oracle.f32(ptr + 0x54),
            "quality": cpu.mem.u(ptr + 0x58, 1),
            "flags": cpu.mem.u(ptr + 0x7A, 2),
            "class": cpu.mem.u(ptr + 0x30, 1),
            "specialization": cpu.mem.u(ptr + 0x31, 1),
            "power_scale": oracle.f32(ptr + 0xF60),
            "hands": [list(cpu.mem.read(ptr + offset, 2)) for offset in (0x7B0, 0x8C8)],
            "master": bool(cpu.mem.u(ptr + 0x10E8, 1)),
            "leader_index": spawn_indices[leaders[ptr]] if ptr in leaders else -1,
            "behavior": behavior(ptr),
        }
        for site, ptr in spawns
    ]
    camp_props = []
    if composition is not None:
        begin, end = struct.unpack("<2I", cpu.mem.read(arguments[0] + 0x0C, 8))
        for ptr in range(begin, end, 0x188):
            kind, x, z, y, yaw, width, depth, height = struct.unpack("<i4x3qi3f", cpu.mem.read(ptr, 48))
            camp_props.append({
                "kind": kind, "position_fixed_16_16": [x, z, y], "yaw": yaw, "dimensions": [width, depth, height],
            })
    return {
        "status": "EMULATED_ALPHA_CODE",
        "function": "0x5DC6E0",
        "seed": seed,
        "zone": [zone_x, zone_z],
        "skipped_carves": carves,
        "zone_stages": zone_stages,
        "decoration_draw_calls": dict(sorted(decoration_draws.items())),
        "surface_prop_attempts": surface_props,
        "boulders": boulders,
        "boulder_write_count": boulder_write_count,
        "boulder_write_hash": boulder_writes.hexdigest(),
        "boulder_voxel_count": len(boulder_voxels),
        "boulder_voxel_hash": hashlib.sha256(
            b"".join(struct.pack("<3i", *point) for point in sorted(boulder_voxels))
        ).hexdigest(),
        "tree_draw_calls": dict(sorted(tree_draws.items())),
        "trees": trees,
        "post_tree_prop": post_tree_prop,
        "placement": placement,
        "entry": entry,
        "crt_out": oracle.crt.state,
        "draws": draws,
        "camp_props": camp_props,
        "spawns": records,
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("exe", type=Path)
    parser.add_argument("seed", type=int)
    parser.add_argument("zone_x", type=int)
    parser.add_argument("zone_z", type=int)
    parser.add_argument("output", type=Path)
    parser.add_argument("--composition", type=int, choices=range(11))
    options = parser.parse_args()
    result = trace(options.exe, options.seed, options.zone_x, options.zone_z, options.composition)
    options.output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(f"Patrol {result['entry']['landmark']}: {len(result['spawns'])} spawns, {len(result['draws'])} CRT draws")


if __name__ == "__main__":
    main()
