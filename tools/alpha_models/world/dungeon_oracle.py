"""Execute Alpha's dungeon generator World_populateRegionDecorations (0x5CC510) inside the real zone pass (P7-B).
Runs WorldInfo_generateBiomeContent (0x5E4850) for a zone whose record is type 3 or 5 exactly like zone_oracle.py
(same verified substitutions), lets it call 0x5CC510 at 0x5E84C3/0x5E84F7 and stops when that call returns.
Dumped (status EMULATED_ALPHA_CODE):
  entry   arguments (zone, region, x, z, subtype, flag), CRT state on entry and on return;
  spawns  every cube::Spawn constructed inside the call (0x5ADB20): position 16.16 (+0x10/+0x18/+0x20), role +0x28,
          kind +0x2C, level +0x34, quality +0x58, flags +0x7A, master +0x10E8;
  voxels  zone columns whose voxel words changed during the call (before/after).
Carves stay argument-only stubs as in zone_oracle.py (they run before the dungeon and draw no rand()).
Speed: the zone pass before the dungeon costs ~23 min in the Python emulator, so the emulator state on entry to
0x5CC510 can be saved once (--save-entry) and resumed (--from-entry); --layout stops right after the room loop
(0x5CE200) and dumps the 27 room records, the room boxes (0x5EF8F0) and the boss cell (|4 at 0x5CE1AE).
Usage: python -I dungeon_oracle.py <Cube.exe> <master_seed> <zone_x> <zone_z> <out.json>
                                   [--save-entry F | --from-entry F [--unicorn]] [--layout]
"""
import json
import struct
import sys
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[3]))
from tools.alpha_models.world.compare_landmarks import column_lookup  # noqa: E402
from tools.alpha_models.world.compare_port import seeds_for  # noqa: E402
from tools.alpha_models.world.landmark_port import LandmarkPort  # noqa: E402
from tools.alpha_models.world.region_oracle import dump_region  # noqa: E402
from tools.alpha_models.world.terrain_oracle import Oracle, WORLD  # noqa: E402
from tools.alpha_models.universal.x86emu import EmuError  # noqa: E402

# First instruction after the river pass (jge target at 0x5E8483), right before the zone-type checks that lead to
# World_populateRegionDecorations (call @0x5E84C3, only zone types 3/5) and to object/creature placement.
import pickle

DUNGEON = 0x5CC510
LAYOUT_DONE = 0x5CE200      # first instruction after the 3x3x3 room loops
ROOM_BOX = 0x5EF8F0         # thiscall(dungeon; origin*, size*, bool) per present room
BOSS_CELL = 0x5CE1A9        # call voxelVolume_at(x, y, z) whose cell gets |4 (boss room centre)
CPU_SKIP = {"mem", "stubs", "hooks", "iat", "image", "raw_import_targets", "trace_calls"}


def save_state(o, path, extra):
    cpu = o.cpu
    state = {k: v for k, v in vars(cpu).items() if k not in CPU_SKIP and not callable(v)}
    mem = {"regions": [(b, e, bytes(d)) for b, e, d in cpu.mem.regions], "over": dict(cpu.mem.over)}
    with open(path, "wb") as f:
        pickle.dump({"cpu": state, "mem": mem, "crt": o.crt.state, "heap_top": o.heap_top, "extra": extra}, f)


def load_state(o, path):
    with open(path, "rb") as f:
        snap = pickle.load(f)
    for k, v in snap["cpu"].items():
        setattr(o.cpu, k, v)
    o.cpu.mem.regions = [(b, e, bytearray(d)) for b, e, d in snap["mem"]["regions"]]
    o.cpu.mem.over = dict(snap["mem"]["over"])
    o.cpu.mem.writes = []
    o.crt.state = snap["crt"]
    o.heap_top = snap["heap_top"]
    return snap["extra"]
RETURNS = (0x5E84C8, 0x5E84FC)
SPAWN_CTOR = 0x5ADB20


class Stop(Exception):
    pass


def snapshot(o, zone):
    field = o.cpu.mem.u(zone + 0xA8, 4)
    cols = {}
    for lz in range(256):
        for lx in range(256):
            cell = field + (lx + lz * 256) * 0x20
            bottom, s8, ptr, n = struct.unpack("<iiIi", o.cpu.mem.read(cell + 0x10, 16))
            vox = struct.unpack(f"<{n}I", o.cpu.mem.read(ptr, 4 * n)) if n > 0 and ptr else ()
            cols[(lx, lz)] = (bottom, vox)
    return cols


def main():
    exe, master, zx, zz, out = sys.argv[1], int(sys.argv[2]), int(sys.argv[3]), int(sys.argv[4]), sys.argv[5]
    opts = sys.argv[6:]
    save_entry = opts[opts.index("--save-entry") + 1] if "--save-entry" in opts else None
    from_entry = opts[opts.index("--from-entry") + 1] if "--from-entry" in opts else None
    layout_only = "--layout" in opts
    rx0, rz0 = zx >> 6, zz >> 6
    o = Oracle(exe, master, fast_noise=True)
    o.cpu.max_steps = 4_000_000_000
    o.cpu.mem.w(WORLD + 0xA4, 4, 1)
    o.cpu.mem.w(WORLD + 0xB4, 1, 1)
    o.cpu.stubs[0x4498D0] = ((lambda cpu: cpu.r.__setitem__("eax", 0)), 8)
    cells = {(x, z): o.cell(x, z) for x in range(rx0 - 5, rx0 + 6) for z in range(rz0 - 5, rz0 + 6)}
    seeds = seeds_for(master)
    free = LandmarkPort(seeds, cells, lambda x, z: None)

    def stub(fn):
        def s(cpu):
            x, z = cpu.mem.u(cpu.r["esp"] + 4, 4), cpu.mem.u(cpu.r["esp"] + 8, 4)
            x = x - (1 << 32) if x & 0x80000000 else x
            z = z - (1 << 32) if z & 0x80000000 else z
            cpu.fpu.append(fn(x, z))
        return s

    o.cpu.stubs[0x5C5E20] = (stub(free.base), 12)
    o.cpu.stubs[0x5EFFA0] = (stub(free.mountain), 12)
    regions = []
    for rx in range(rx0 - 1, rx0 + 2):
        for rz in range(rz0 - 1, rz0 + 2):
            o.call(0x5DA280, [rx, rz])
            regions.append(dump_region(o, rx, rz))
    land = LandmarkPort(seeds, cells, column_lookup(regions))
    del o.cpu.stubs[0x5C5E20]
    del o.cpu.stubs[0x5EFFA0]

    # Verify the climate ports (incl. the Forest bonus of 0x5C4800) against the real functions before using them.
    import random
    rng = random.Random(zx * 31 + zz)
    worst_t = worst_h = 0.0
    forest = [c for r in regions for c in r["columns"] if c["type"] == 3]
    for i in range(40):
        if forest and i % 2 == 0:
            c = forest[i % len(forest)]
            x, z = int(c["x"] + rng.uniform(-c["f16"], c["f16"])), int(c["z"] + rng.uniform(-c["f16"], c["f16"]))
        else:
            x, z = (zx << 8) + rng.randrange(256), (zz << 8) + rng.randrange(256)
        worst_t = max(worst_t, abs(o.call_float(0x5C4800, [x, z]) - land.temperature(x, z)))
        worst_h = max(worst_h, abs(o.call_float(0x5C4DD0, [x, z]) - land.humidity(x, z)))
    print("climate port check: worst |dT|", worst_t, "|dH|", worst_h, "forest columns", len(forest), flush=True)

    o.cpu.stubs[0x5C5E20] = (stub(land.base), 12)
    o.cpu.stubs[0x5EFFA0] = (stub(land.mountain), 12)
    o.cpu.stubs[0x5C4800] = (stub(land.temperature), 8)
    o.cpu.stubs[0x5C4DD0] = (stub(land.humidity), 8)
    o.cpu.stubs[0x5EF040] = (stub(land.region_cell), 8)
    o.cpu.stubs[0x5F9340] = (stub(land.sample_terrain_height), 12)
    o.cpu.stubs[0x5989D0] = (stub(land.height_at_xy), 8)
    o.cpu.stubs[0x5F9F70] = (stub(land.rotate_and_place), 12)

    # Colour functions: ports byte-identical to execution (compare_colors.py, 300 samples).
    from tools.alpha_models.world.color_port import ColorPort
    colors = ColorPort(seeds, land)

    def sint(v):
        return v - (1 << 32) if v & 0x80000000 else v

    def args(cpu, n):
        return [cpu.mem.u(cpu.r["esp"] + 4 + 4 * i, 4) for i in range(n)]

    def surface_stub(cpu):
        out, x, z, y, t, h = args(cpu, 6)
        x, z, y = sint(x), sint(z), sint(y)
        t = struct.unpack("<f", struct.pack("<I", t))[0]
        h = struct.unpack("<f", struct.pack("<I", h))[0]
        cpu.mem.write(out, bytes(colors.surface_color(x, z, y, t, h, land.region_cell(x, z))))
        cpu.r["eax"] = out

    def float3_stub(fn):
        def s(cpu):
            out, x, z, y = args(cpu, 4)
            x, z, y = sint(x), sint(z), sint(y)
            rgb = fn(x, z, y, land.humidity(x, z), land.region_cell(x, z))
            cpu.mem.write(out, struct.pack("<3f", *rgb))
            cpu.r["eax"] = out
        return s

    o.cpu.stubs[0x5C56E0] = (surface_stub, 28)

    # carveB 0x5CBE00 / carveA 0x5CB600 (thiscall, 7 args, callee pops 28): ~1e8 emulated instructions each, so here
    # they only record their arguments (they draw no rand()); their shapes are verified by carve_oracle.py.
    carves = []

    def carve_stub(kind):
        def s(cpu):
            a = [sint(v) for v in args(cpu, 6)]
            carves.append({"kind": kind, "cx": a[0], "cz": a[1], "base_h": a[2], "rx": a[3], "rz": a[4], "height": a[5]})
        return s

    o.cpu.stubs[0x5CBE00] = (carve_stub("B"), 28)
    o.cpu.stubs[0x5CB600] = (carve_stub("A"), 28)
    o.cpu.stubs[0x5C7140] = (float3_stub(colors.rock_color), 20)
    o.cpu.stubs[0x5F9620] = (float3_stub(colors.soil_color), 20)

    started = time.time()
    zone_ptr = {}
    entry = {}
    spawns = []

    def on_zone_ctor(cpu):
        zone_ptr.setdefault('zone', cpu.r['ecx'])

    def on_dungeon(cpu):
        a = [sint(v) for v in args(cpu, 6)]
        entry.update({"zone": a[0] & 0xFFFFFFFF, "region": a[1] & 0xFFFFFFFF, "x": a[2], "z": a[3],
                      "subtype": a[4], "flag": a[5] & 0xFF, "crt_in": o.crt.state,
                      })
        entry["before"] = None if layout_only else snapshot(o, zone_ptr["zone"])
        print("dungeon entry", {k: v for k, v in entry.items() if k != "before"}, "s", round(time.time() - started), flush=True)

    def on_spawn(cpu):
        if entry:
            spawns.append(cpu.r['ecx'])

    def on_return(cpu):
        if entry:
            raise Stop()

    boxes = []
    boss_cells = []
    layout = {}

    def on_box(cpu):
        if entry:
            origin, size = cpu.mem.u(cpu.r["esp"] + 4, 4), cpu.mem.u(cpu.r["esp"] + 8, 4)
            boxes.append({"origin": [o.i32(origin + 4 * i) for i in range(3)],
                          "size": [o.i32(size + 4 * i) for i in range(3)], "flag": cpu.mem.u(cpu.r["esp"] + 12, 1)})

    def on_boss_cell(cpu):
        if entry:
            boss_cells.append([o.i32(cpu.r["esp"] + 4 * i) for i in range(3)])

    def on_layout(cpu):
        if entry:
            ebp = cpu.r["ebp"]
            layout["frame"] = cpu.mem.read(ebp - 0x400, 0x400).hex()
            layout["ebp"] = ebp
            layout["crt"] = o.crt.state
            if layout_only:
                raise Stop()

    o.cpu.hooks[0x6240F0] = on_zone_ctor
    o.cpu.hooks[ROOM_BOX] = on_box
    o.cpu.hooks[BOSS_CELL] = on_boss_cell
    o.cpu.hooks[LAYOUT_DONE] = on_layout
    o.cpu.hooks[SPAWN_CTOR] = on_spawn
    for r in RETURNS:
        o.cpu.hooks[r] = on_return

    if from_entry:
        extra = load_state(o, from_entry)
        zone_ptr["zone"] = extra["zone_ptr"]
        on_dungeon(o.cpu)
        print("resumed at dungeon entry", hex(o.cpu.eip), flush=True)
        if "--unicorn" in opts:
            from tools.alpha_models.universal.unicorn_bridge import UnicornRun
            run = UnicornRun(o.cpu, o.image)
            o.cpu.mem = run.mem          # oracle helpers (o.i32, snapshot) now read unicorn memory
            # The 16 MiB bump heap (frees ignored) is too small for the dungeon: continue in a 512 MiB region.
            big, big_size = 0x50000000, 0x20000000
            run.uc.mem_map(big, big_size)
            top = {"va": big}

            def alloc(size):
                va = top["va"]
                top["va"] += (size + 15) & ~15
                if top["va"] > big + big_size:
                    raise EmuError("unicorn heap exhausted")
                run.mem.write(va, bytes(size))
                return va

            o.alloc = alloc
            try:
                run.run()
                print("unicorn run ended without reaching a stop hook")
                raise SystemExit(1)
            except Stop:
                pass
        else:
            o.cpu.max_steps = 10 ** 12
            try:
                while True:
                    o.cpu.step()
            except Stop:
                pass
            except EmuError as e:
                print("stop:", e, "eip", hex(o.cpu.eip))
                raise SystemExit(1)
    else:
        def on_dungeon_entry(cpu):
            if save_entry:
                save_state(o, save_entry, {"zone_ptr": zone_ptr["zone"]})
                print("saved entry state to", save_entry, "s", round(time.time() - started), flush=True)
            on_dungeon(cpu)

        o.cpu.hooks[DUNGEON] = on_dungeon_entry
        try:
            o.call(0x5E4850, [zx, zz])
            print("generateBiomeContent returned without a dungeon call (zone record not type 3/5?)")
            raise SystemExit(2)
        except Stop:
            pass
        except EmuError as e:
            print("stop:", e, "eip", hex(o.cpu.eip), "calls", [hex(c) for c in o.cpu.trace_calls[-8:]])
            raise SystemExit(1)

    if layout_only:
        Path(out).write_text(json.dumps({"status": "EMULATED_ALPHA_CODE", "function": "0x5CC510 rooms (to 0x5CE200)",
                                         "master_seed": master, "zone": [zx, zz],
                                         "entry": {k: v for k, v in entry.items() if k != "before"},
                                         "room_boxes": boxes, "boss_cells": boss_cells, "layout": layout}),
                             encoding="utf-8")
        print("layout:", len(boxes), "rooms,", len(boss_cells), "boss cells", boss_cells, round(time.time() - started), "s")
        return

    zone = zone_ptr['zone']
    after = snapshot(o, zone)
    changed = []
    for key, (bottom, vox) in after.items():
        b0, v0 = entry["before"][key]
        if (bottom, vox) != (b0, v0):
            changed.append({"lx": key[0], "lz": key[1], "bottom_before": b0, "before": list(v0),
                            "bottom": bottom, "vox": list(vox)})
    records = []
    for p in spawns:
        u = lambda off, n=4: o.cpu.mem.u(p + off, n)
        q = lambda off: struct.unpack("<q", o.cpu.mem.read(p + off, 8))[0] / 65536.0
        records.append({"x": q(0x10), "z": q(0x18), "y": q(0x20), "role": o.i32(p + 0x28), "kind": o.i32(p + 0x2C),
                        "level": o.i32(p + 0x34), "quality": u(0x58, 1), "flags": u(0x7A, 2), "master": u(0x10E8, 1)})
    info = {k: v for k, v in entry.items() if k != "before"}
    info["crt_out"] = o.crt.state
    info["zone_level"] = o.i32(zone + 0x80)
    info["zone_quality"] = o.cpu.mem.u(zone + 0x84, 1)
    Path(out).write_text(json.dumps({"status": "EMULATED_ALPHA_CODE", "function": "0x5CC510", "master_seed": master,
                                     "zone": [zx, zz], "entry": info, "spawns": records,
                                     "changed_columns": changed}), encoding="utf-8")
    print("dungeon:", len(records), "spawns,", len(changed), "columns changed,", round(time.time() - started), "s")


if __name__ == "__main__":
    main()
