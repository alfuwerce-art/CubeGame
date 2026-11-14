"""Reference port of the 0x5CC510 contents stage (0x5D06F2…, stage 4b), built against dungeon_oracle.py traces.

Runs stages 1–4a with the verified ports, then the cell pass of 4b. Every rand() is compared with the oracle's
rand log by draw index; the first difference prints Alpha's call site at that draw. Branches that are not ported
yet stop the run with their address (NotPorted), so the output says how far the port is exact.
Usage: python dungeon_contents_port.py <entry.pkl> <trace.json> <subtype> <flag> <crt_in>
"""
import json
import struct
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[3]))
sys.path.insert(0, str(Path(__file__).resolve().parent))
import dungeon_columns_port as cp  # noqa: E402
import dungeon_layout_port as d  # noqa: E402
import dungeon_pre_port as pre  # noqa: E402
from tools.alpha_models.world.terrain_port import noise  # noqa: E402

N = 22


class NotPorted(Exception):
    pass


class Mismatch(Exception):
    pass


class CheckedCrt(cp.CountingCrt):
    """CRT that checks every draw against the oracle log (index = draws so far + offset)."""

    def __init__(self, state, log, offset):
        super().__init__(state)
        self.log = log
        self.offset = offset
        self.site = "?"

    def rand(self):
        v = super().rand()
        i = self.offset + self.draws - 1
        if i >= len(self.log):
            raise Mismatch(f"draw {i}: Alpha made no more draws (port site {self.site})")
        if self.log[i][1] != self.state:
            raise Mismatch(f"draw {i}: port site {self.site}, Alpha site {hex(self.log[i][0])}")
        return v


def fill_cuboid(W, x, y, z, size, flag):
    """0x59ABA0: covered air (0xC0) over the box; with `flag`, faces bite into the walls by noise (no rand())."""
    sx, sy, sz = size
    for ix in range(sx):
        for iy in range(sy):
            for iz in range(sz - 1, -1, -1):
                X, Y, Z = x + ix, y + iy, z + iz
                W.write(X, Y, Z, [0, 0, 0, 0xC0])
                if not flag:
                    continue
                if ix == 0 and noise(Y * 0.1 + X * 13, Z * 0.2) > 0.5:
                    W.write(X - 1, Y, Z, [0, 0, 0, 0xC0])
                if iy == 0 and noise(X * 0.1 + Y * 13, Z * 0.2) > 0.5:
                    W.write(X, Y - 1, Z, [0, 0, 0, 0xC0])
                if ix == sx - 1 and noise(Y * 0.1 + X * 13, Z * 0.2) > 0.5:
                    W.write(X + 1, Y, Z, [0, 0, 0, 0xC0])
                if iy == sy - 1 and noise(X * 0.1 + Y * 13, Z * 0.2) > 0.5:
                    W.write(X, Y + 1, Z, [0, 0, 0, 0xC0])


def solid(t):
    return t not in (0, 2)


def contents(world, vol, rot, mirror, subtype, flag, x0, y0, base, pal_b, rng, run):
    W = world

    def cell(x, y, z):
        if not (0 <= x < N and 0 <= y < N and 0 <= z < N):
            return (1, 0)
        c = vol[pre.at(vol, rot, mirror, x, y, z)]
        return (c[0], c[1])

    def r(site):
        rng.site = site
        return rng.rand()

    if subtype == 5:
        raise NotPorted("0x5D0705 subtype-5 mound")
    for cx in range(N):
        for cy in range(N):
            for cz in range(N):
                t, fl = cell(cx, cy, cz)
                if t not in (3, 4):
                    continue
                WX, WY, WZ = x0 + 10 * cx, y0 + 10 * cy, base + 10 * cz
                fill_cuboid(W, WX, WY, WZ, (10, 10, 10), subtype in (1, 2, 4))
                if subtype in (0, 1, 3) and t != 4 and not (fl & 1):
                    # four wall niches with a light (0x5D0BC5…0x5D1270); positions follow the bytes
                    zn = WZ + 2
                    niches = (((0, -1), (WX + 5, WY - 3), (WX + 4, WY - 2)),
                              ((0, 1), (WX + 5, WY + 13), (WX + 4, WY + 10)),
                              ((-1, 0), (WX - 3, WY + 5), (WX - 2, WY + 4)),
                              ((1, 0), (WX + 13, WY + 5), (WX + 10, WY + 4)))
                    for side, ((dx, dy), probe, cut) in enumerate(niches):
                        if r(f"niche{side}") & 1 and cell(cx + dx, cy + dy, cz)[0] == 0                                 and not solid(W.type(probe[0], probe[1], zn)):
                            fill_cuboid(W, cut[0], cut[1], zn, (2, 2, 4), False)
                            run.objects.append((4, cut, zn))     # light record (0x5A0E00, type 4)
                if fl & 1:
                    raise NotPorted("0x5D12A1 stairs")
                if subtype == 4 and t == 4:
                    continue
                if cell(cx, cy, cz - 1)[0] != 3:
                    run.fill(0x5D1856, WX, WY, WZ, (10, 10, 1), pal_b, run_scale[0], 0)
                    if subtype in (0, 3) and (fl & 2):
                        raise NotPorted("0x5D1879 carpet")
                if t != 4:
                    if fl & 1:
                        raise NotPorted("0x5D3611 stair cell contents")
                    if not flag and r("light40") % 40 == 0:
                        r("light-orient")
                        r("light-4000")
                        # 0x5D1C15 → 0x5D360B
                        raise NotPorted("0x5D360B after the type-7 object")
                    raise NotPorted("0x5D1C1A furniture")
                # 0x5D3AC3: master of the dungeon on the |4 cell
                if fl & 4:
                    raise NotPorted("0x5D3ACD master spawn")


run_scale = [0.0]


def main():
    entry, trace_path = sys.argv[1], sys.argv[2]
    subtype, flag, crt = int(sys.argv[3]), int(sys.argv[4]), int(sys.argv[5])
    trace = json.loads(Path(trace_path).read_text())
    log = trace["rand"]
    snap = cp.Snapshot(entry)
    fields, zx, zz = snap.fields()
    x0, y0 = zx * 256 + 32, zz * 256 + 32
    vol, rot, mirror, pal, state = pre.run(subtype, flag, crt)
    states = [st for _, st in log]
    offset = states.index(state) + 1
    rng = CheckedCrt(state, log, offset)
    world = cp.World(fields)
    pal_a = [pal[0x9c], pal[0x9d], pal[0x9e]]
    pal_b = [pal[0x34], pal[0x35], pal[0x36]]
    scale = struct.unpack("<f", bytes(pal[0x70:0x74]))[0]
    run_scale[0] = scale
    run, info = cp.columns(world, vol, rot, mirror, subtype, x0, y0, pal_a, [pal[0], pal[1], pal[2]], scale, rng)
    run.objects = []
    print("columns done:", len(run.fills), "fills, draw", offset + rng.draws, info)
    try:
        contents(world, vol, info["rot"], mirror, subtype, flag, x0, y0, info["base"], pal_b, rng, run)
        print("contents finished, draws", offset + rng.draws, "of", len(log))
    except (NotPorted, Mismatch) as e:
        print(type(e).__name__, e, "| draws matched up to", offset + rng.draws - (1 if isinstance(e, Mismatch) else 0),
              "of", len(log))


if __name__ == "__main__":
    main()
