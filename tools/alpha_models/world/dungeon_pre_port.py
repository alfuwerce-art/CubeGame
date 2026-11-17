import json, struct, sys
import dungeon_layout_port as d
import dungeon_corridor_port as c

N = 22


def mapped(rot, mirror, x, y):
    if rot == 1: x, y = N - y - 1, x
    elif rot == 2: x, y = N - x - 1, N - y - 1
    elif rot == 3: x, y = y, N - x - 1
    if mirror: y = N - y - 1
    return x, y


def at(vol, rot, mirror, x, y, z):
    x, y = mapped(rot, mirror, x, y)
    return d.cell(vol, x, y, z)


def run(subtype, flag, crt):
    rooms, boxes, boss, _ = d.layout(subtype, flag, crt)
    vol = d.layout.volume
    r = d.Crt(_)
    links = c.LINKS[c.scheme_key(subtype, flag, crt)]
    for l in links:
        a = rooms[d.idx(*l[:3])]; b = rooms[d.idx(*l[3:])]
        if a["present"] and b["present"]:
            oa = c.box_of(l[:3], a); ob = c.box_of(l[3:], b)
            c.corridor(vol, oa[0], oa[1], ob[0], ob[1], r)
    # entrance: room A of the first link (rotation still 0)
    a0 = links[0][:3]; v = rooms[d.idx(*a0)]["v"]
    cc = d.cell(vol, 7 * a0[0] + 2 + v[0], 7 * a0[1] + 1 + v[1], 7 * a0[2] + 3 + v[2])
    if cc is not None: vol[cc][0] = 4
    rot = r.rand() & 3
    mirror = 1 if (r.rand() & 1) == 0 else 0
    pal = bytearray(0xB0)
    pal[2] = r.rand() % 50 + 20      # -0x2bda
    pal[1] = r.rand() % 50 + 20      # -0x2bdb
    pal[0] = r.rand() % 50 + 20      # -0x2bdc
    base = r.rand() % 50
    if subtype == 3:
        base = r.rand() % 50 + 100
    pal[0x9e] = (r.rand() % 50 + 50 + base) & 0xFF
    pal[0x9d] = (r.rand() % 50 + 50 + base) & 0xFF
    pal[0x9c] = (r.rand() % 50 + 50 + base) & 0xFF
    pal[0x36] = (r.rand() % 50 + 50 + base) & 0xFF
    pal[0x35] = (r.rand() % 50 + 50 + base) & 0xFF
    pal[0x34] = (r.rand() % 50 + 50 + base) & 0xFF
    scale = 0x3ba3d70a
    if subtype in (1, 2):
        pal[0x9e] = r.rand() % 50 + 50; pal[0x9d] = r.rand() % 50 + 50; pal[0x9c] = r.rand() % 20 + 50
        pal[0x36] = r.rand() % 50 + 50; pal[0x35] = r.rand() % 50 + 50; pal[0x34] = r.rand() % 20 + 50
        scale = 0x3ac49ba6
    elif subtype == 4:
        pal[0x9e] = r.rand() % 50 + 20; pal[0x9d] = r.rand() % 50 + 80; pal[0x9c] = r.rand() % 50 + 100
        pal[0x36] = r.rand() % 50 + 20; pal[0x35] = r.rand() % 50 + 80; pal[0x34] = r.rand() % 50 + 100
        scale = 0x3b23d70a
    elif subtype == 5:
        pal[0x9c], pal[0x9d], pal[0x9e] = 0xff, 0xd2, 0x82
        pal[0x34], pal[0x35], pal[0x36] = 0xc8, 0xb4, 0x50
    pal[0x70:0x74] = struct.pack("<I", scale)
    if subtype in (0, 1, 2, 3):
        for x in range(N):
            for y in range(N):
                h = 1
                if subtype != 2 and r.rand() % 6 == 0:
                    h = r.rand() % 3 + 2
                z = N - 1
                while z >= 0:
                    t = vol[at(vol, rot, mirror, x, y, z)][0]
                    if t != 4 and t != 0:
                        break
                    z -= 1
                if z < 0:
                    continue
                top = min(z + h, N - 1)
                for zz in range(z + 1, top + 1):
                    vol[at(vol, rot, mirror, x, y, zz)][0] = 2
    return vol, rot, mirror, pal, r.state


PAL_OFFSETS = [0, 1, 2, 0x34, 0x35, 0x36, 0x9c, 0x9d, 0x9e, 0x70, 0x71, 0x72, 0x73]

if __name__ == "__main__":
    ok = 0
    data = json.load(open(sys.argv[1]))["cases"]
    for case in data:
        vol, rot, mirror, pal, crt = run(case["subtype"], case["flag"], case["crt_in"])
        alpha = bytes.fromhex(case["volume"]["cells"])
        mine = bytes(b for cc in vol for b in cc)
        apal = bytes.fromhex(case["colors"])
        probs = []
        if (rot, mirror) != (case["volume"]["rotation"], case["volume"]["mirror"]):
            probs.append(f"rot/mirror alpha {case['volume']['rotation'], case['volume']['mirror']} port {rot, mirror}")
        bad = [i // 2 for i in range(len(mine)) if mine[i] != alpha[i]]
        if bad: probs.append(f"cells {len(bad)} first {bad[0]} alpha {alpha[bad[0]*2:bad[0]*2+2].hex()} port {mine[bad[0]*2:bad[0]*2+2].hex()}")
        pb = [hex(o) for o in PAL_OFFSETS if apal[o] != pal[o]]
        if pb: probs.append(f"palette offsets {pb} alpha {[apal[o] for o in PAL_OFFSETS]} port {[pal[o] for o in PAL_OFFSETS]}")
        if crt != case["crt_out"]: probs.append("crt")
        if probs:
            print("FAIL", case["subtype"], case["flag"], case["crt_in"], *probs)
        else:
            ok += 1
    print(ok, "/", len(data))
