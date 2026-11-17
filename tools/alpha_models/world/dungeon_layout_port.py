"""Python port of the 0x5CC510 room layout (to 0x5CE200), checked against oracle layout cases."""
import json
import struct
import sys


class Crt:
    def __init__(self, s):
        self.state = s & 0xFFFFFFFF

    def rand(self):
        self.state = (self.state * 214013 + 2531011) & 0xFFFFFFFF
        return (self.state >> 16) & 0x7FFF


N = 22


def cell(vol, x, y, z):
    if 0 <= x < N and 0 <= y < N and 0 <= z < N:
        return ((z * N) + y) * N + x
    return None


def box_rand(r, size, carve, origin=None, vol=None):
    """0x5EF8F0 with rotation 0 / no mirror (true at the layout stage)."""
    sx, sy, sz = size
    if sx < 1 or sy < 1 or sz < 1:
        return
    ox, oy, oz = origin
    for x in range(ox, ox + sx):
        for y in range(oy, oy + sy):
            for z in range(oz, oz + sz):
                c = cell(vol, x, y, z)
                if c is not None:
                    vol[c][0] = 3
    if r.rand() & 1:
        w = r.rand() % sx + 1
        h = r.rand() % sy + 1
        x0, y0 = ox, oy
        if sx - w > 0:
            x0 += r.rand() % (sx - w + 1)
        if sy - h > 0:
            y0 += r.rand() % (sy - h + 1)
        for x in range(x0, x0 + w):
            for y in range(y0, y0 + h):
                c = cell(vol, x, y, oz)
                if c is not None:
                    vol[c][1] |= 2
    if carve and sx > 2 and sy > 2:
        w = r.rand() % (sx - 2) + 1
        h = r.rand() % (sy - 2) + 1
        x0, y0 = ox + 1, oy + 1
        if sx - w > 1:
            x0 += r.rand() % (sx - w - 1)
        if sy - h > 1:
            y0 += r.rand() % (sy - h - 1)
        for x in range(x0, x0 + w):
            for y in range(y0, y0 + h):
                for z in range(oz, oz + sz):
                    c = cell(vol, x, y, z)
                    if c is not None:
                        vol[c][0] = 2


def idx(i, j, k):
    return i * 9 + j * 3 + k


def layout(subtype, flag, crt, extra=0):
    r = Crt(crt)
    # kind lists (subtypes 1/2 draw rand()%3 for the third boss kind)
    if subtype in (1, 2):
        r.rand()
    rooms = [dict(present=0, boss=0, b2=0, special=0, v=[0] * 6) for _ in range(27)]
    for n in range(27):
        r1 = r.rand(); r2 = r.rand(); r3 = r.rand(); r4 = r.rand(); r5 = r.rand(); r6 = r.rand()
        v = rooms[n]["v"]
        v[5] = r1 % 3            # F
        v[4] = r2 % 3 - 1        # E
        v[3] = r3 % 3 - 1        # D
        v[2] = r4 % 3 - 1        # C
        v[1] = r5 & 1            # B
        v[0] = r6 & 1            # A

    def setp(i, j, k, val=1):
        rooms[idx(i, j, k)]["present"] = val

    def even():
        return 1 if (r.rand() & 1) == 0 else 0

    if flag == 0:
        if subtype not in (4, 5):
            if subtype == 2:
                r.rand()
                setp(1, 0, 2); setp(1, 2, 1); setp(2, 2, 1); setp(2, 1, 1); setp(2, 0, 1); setp(0, 2, 1)
                setp(0, 1, 1, even()); setp(0, 0, 0); setp(0, 1, 0); setp(0, 2, 0, even())
                setp(1, 1, 0); rooms[idx(1, 1, 0)]["boss"] = 1
            else:
                c = r.rand() % 3
                setp(1, 0, 1)
                if c == 1:
                    setp(2, 0, 1, even()); setp(1, 1, 1); setp(0, 1, 1); setp(0, 0, 1, even()); setp(0, 2, 1)
                    setp(1, 2, 1); setp(2, 2, 1); rooms[idx(2, 2, 1)]["boss"] = 1; setp(2, 1, 1, even())
                elif c == 2:
                    setp(2, 0, 1, even()); setp(0, 0, 1); setp(0, 1, 1); setp(0, 2, 1, even()); setp(1, 1, 1)
                    setp(2, 1, 1); setp(2, 2, 1, even()); setp(1, 2, 1); rooms[idx(1, 2, 1)]["boss"] = 1
                else:
                    setp(2, 0, 1, even()); setp(0, 0, 1); setp(0, 1, 1); setp(0, 2, 1, even()); setp(1, 1, 1)
                    setp(1, 2, 1, even()); setp(2, 1, 1); setp(2, 2, 1); rooms[idx(2, 2, 1)]["boss"] = 1
        else:
            r.rand()
            setp(1, 1, 2); rooms[idx(1, 1, 2)]["b2"] = 1; rooms[idx(1, 1, 2)]["special"] = 1
            setp(1, 2, 1); setp(2, 2, 1); setp(2, 1, 1); setp(2, 0, 1); setp(0, 2, 1)
            setp(0, 1, 1, even()); setp(0, 0, 0); setp(0, 1, 0); setp(0, 2, 0, even())
            setp(1, 1, 0); rooms[idx(1, 1, 0)]["boss"] = 1
    else:
        setp(1, 1, 1); setp(1, 2, 1); setp(0, 1, 1); setp(2, 1, 1); setp(0, 0, 1); setp(2, 0, 1)

    vol = [[0, 0] for _ in range(N * N * N)]
    for _ in range(extra):
        r.rand()
    boxes, boss = [], []
    for i in range(3):
        for j in range(3):
            for k in range(3):
                room = rooms[idx(i, j, k)]
                if not room["present"]:
                    continue
                v = room["v"]
                if room["special"]:
                    v[0] = v[1] = v[2] = 0
                    v[3], v[4], v[5] = 0, 0, 1
                origin = [7 * i + 2 + v[0], 7 * j + 2 + v[1], 7 * k + 3 + v[2]]
                size = [3 + v[3], 3 + v[4], 1 + v[5]]
                carve = 0
                if not room["special"] and not room["boss"]:
                    carve = 1 if r.rand() % 3 == 0 else 0
                boxes.append({"origin": origin, "size": size, "flag": carve})
                box_rand(r, size, carve, origin, vol)
                if room["boss"]:
                    b = [origin[n] + int(size[n] / 2) for n in range(3)]
                    boss.append(b)
                    c = cell(vol, *b)
                    if c is not None:
                        vol[c][1] |= 4
    layout.volume = vol
    return rooms, boxes, boss, r.state


def frame_rooms(frame_hex):
    frame = bytes.fromhex(frame_hex)
    base = 0x400 - 0x334
    out = []
    for n in range(27):
        rec = frame[base + 0x1C * n: base + 0x1C * (n + 1)]
        out.append(dict(present=rec[0], boss=rec[1], b2=rec[2], special=rec[3], v=list(struct.unpack("<6i", rec[4:]))))
    return out


def main():
    data = json.load(open(sys.argv[1]))
    ok = 0
    for case in data["cases"]:
        rooms, boxes, boss, crt = layout(case["subtype"], case["flag"], case["crt_in"])
        alpha_rooms = frame_rooms(case["frame"])
        problems = []
        for n in range(27):
            a, m = alpha_rooms[n], rooms[n]
            if (a["present"], a["boss"], a["b2"], a["special"]) != (m["present"], m["boss"], m["b2"], m["special"]):
                problems.append(f"room {n} flags alpha {a['present'],a['boss'],a['b2'],a['special']} port {m['present'],m['boss'],m['b2'],m['special']}")
            elif a["present"] and a["v"] != m["v"]:
                problems.append(f"room {n} v alpha {a['v']} port {m['v']}")
        if boxes != case["room_boxes"]:
            problems.append(f"boxes differ: alpha {case['room_boxes']} port {boxes}")
        if boss != case["boss_cells"]:
            problems.append(f"boss alpha {case['boss_cells']} port {boss}")
        alpha_vol = bytes.fromhex(case["volume"]["cells"])
        mine = bytes(b for c in layout.volume for b in c)
        if alpha_vol != mine:
            diff = [i // 2 for i in range(len(mine)) if mine[i] != alpha_vol[i]]
            problems.append(f"volume differs in {len(diff)} bytes, first cell {diff[0]} alpha {alpha_vol[diff[0]*2:diff[0]*2+2].hex()} port {mine[diff[0]*2:diff[0]*2+2].hex()}")
        if crt != case["crt_out"]:
            problems.append(f"crt alpha {case['crt_out']} port {crt}")
        tag = f"subtype {case['subtype']} flag {case['flag']} crt {case['crt_in']}"
        if problems:
            print("FAIL", tag)
            for p in problems[:6]:
                print("   ", p)
        else:
            ok += 1
            print("OK  ", tag, len(boxes), "rooms")
    print(ok, "/", len(data["cases"]), "cases identical")


if __name__ == "__main__":
    main()
