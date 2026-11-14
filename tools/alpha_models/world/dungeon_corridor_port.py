import json, sys
import dungeon_layout_port as d

LINKS = {
    "flag1": [[1,1,1,1,2,1],[1,1,1,0,1,1],[1,1,1,2,1,1],[0,1,1,0,0,1],[2,1,1,2,0,1]],
    "scheme0": [[1,0,1,0,0,1],[1,0,1,2,0,1],[0,0,1,0,1,1],[0,1,1,1,1,1],[0,1,1,0,2,1],[1,1,1,1,2,1],[1,1,1,2,1,1],[2,1,1,2,2,1]],
    "scheme1": [[1,0,1,1,1,1],[1,0,1,2,0,1],[1,1,1,0,1,1],[0,1,1,0,0,1],[0,1,1,0,2,1],[0,2,1,1,2,1],[1,2,1,2,2,1],[2,2,1,2,1,1]],
    "scheme2": [[1,0,1,0,0,1],[1,0,1,2,0,1],[2,0,1,2,1,1],[0,0,1,0,1,1],[0,1,1,1,1,1],[0,1,1,0,2,1],[1,1,1,1,2,1],[1,1,1,2,1,1],[2,1,1,2,2,1]],
    "sub2": [[1,0,2,1,2,1],[1,2,1,0,2,1],[1,2,1,2,2,1],[0,2,1,0,1,1],[2,2,1,2,1,1],[2,1,1,2,0,1],[2,0,1,0,0,0],[0,0,0,0,1,0],[0,1,0,0,2,0],[0,1,0,1,1,0]],
    "sub45": [[1,1,2,1,2,1],[1,2,1,0,2,1],[1,2,1,2,2,1],[0,2,1,0,1,1],[2,2,1,2,1,1],[2,1,1,2,0,1],[2,0,1,0,0,0],[0,0,0,0,1,0],[0,1,0,0,2,0],[0,1,0,1,1,0]],
}


def tdiv(a, b):
    q = abs(a) // abs(b)
    return q if (a >= 0) == (b > 0) else -q


def line(vol, s, e):
    dx, dy, dz = e[0] - s[0], e[1] - s[1], e[2] - s[2]
    m = max(abs(dx), abs(dy))
    n = m if m > abs(dz) else abs(dz)
    if n == 0:
        return
    prev = s[2]
    for t in range(n + 1):
        x = s[0] + tdiv(dx * t, n)
        y = s[1] + tdiv(dy * t, n)
        z = s[2] + tdiv(dz * t, n)
        c = d.cell(vol, x, y, z)
        if c is not None:
            vol[c][0] = 3
        if z < prev:
            c = d.cell(vol, x, y, z + 1)
            if c is not None: vol[c][0] = 3
            c = d.cell(vol, x, y, z)
            if c is not None: vol[c][1] |= 1
        if z > prev:
            c = d.cell(vol, x, y, z - 1)
            if c is not None: vol[c][0] = 3
            c = d.cell(vol, x, y, z - 1)
            if c is not None: vol[c][1] |= 1
        prev = z


def corridor(vol, A, sA, B, sB, r):
    endAx = A[0] + sA[0]
    if B[0] < endAx and A[0] < B[0] + sB[0]:
        m = B[0] + tdiv(sB[0], 2)
        if m < A[0]: m = A[0]
        if m >= endAx: m = endAx - 1
        if B[1] < A[1]:
            line(vol, (m, A[1], A[2]), (m, B[1] + sB[1], B[2]))
            if (r.rand() & 1) and m > A[0] and m > B[0]:
                line(vol, (m - 1, A[1], A[2]), (m - 1, B[1] + sB[1], B[2]))
            if (r.rand() & 1) and m < A[0] + sA[0] - 1 and m < B[0] + sB[0] - 1:
                line(vol, (m + 1, A[1], A[2]), (m + 1, B[1] + sB[1], B[2]))
        else:
            line(vol, (m, A[1] + sA[1] - 1, A[2]), (m, B[1] - 1, B[2]))
            if (r.rand() & 1) and m > A[0] and m > B[0]:
                line(vol, (m - 1, A[1] + sA[1] - 1, A[2]), (m - 1, B[1] - 1, B[2]))
            if (r.rand() & 1) and m < A[0] + sA[0] - 1 and m < B[0] + sB[0] - 1:
                line(vol, (m + 1, A[1] + sA[1] - 1, A[2]), (m + 1, B[1] - 1, B[2]))
        return
    endAy = A[1] + sA[1]
    if B[1] < endAy and A[1] < B[1] + sB[1]:
        m = B[1] + tdiv(sB[1], 2)
        if m < A[1]: m = A[1]
        if m >= endAy: m = endAy - 1
        if B[0] < A[0]:
            line(vol, (A[0], m, A[2]), (B[0] + sB[0], m, B[2]))
            if (r.rand() & 1) and m > A[1] and m > B[1]:
                line(vol, (A[0], m - 1, A[2]), (B[0] + sB[0], m - 1, B[2]))
            # Alpha compares the second limit against B's x extent (bytes 0x5C55BF)
            if (r.rand() & 1) and m < A[1] + sA[1] - 1 and m < B[0] + sB[0] - 1:
                line(vol, (A[0], m + 1, A[2]), (B[0] + sB[0], m + 1, B[2]))
        else:
            line(vol, (A[0] + sA[0] - 1, m, A[2]), (B[0] - 1, m, B[2]))
            if (r.rand() & 1) and m > A[1] and m > B[1]:
                line(vol, (A[0] + sA[0] - 1, m - 1, A[2]), (B[0] - 1, m - 1, B[2]))
            # bytes 0x5C5682: both limits are x extents
            if (r.rand() & 1) and m < A[0] + sA[0] - 1 and m < B[0] + sB[0] - 1:
                line(vol, (A[0] + sA[0] - 1, m + 1, A[2]), (B[0] - 1, m + 1, B[2]))


def scheme_key(subtype, flag, crt):
    r = d.Crt(crt)
    if subtype in (1, 2): r.rand()
    for _ in range(162): r.rand()
    if flag: return "flag1"
    if subtype in (4, 5): return "sub45"
    if subtype == 2: return "sub2"
    return "scheme%d" % (r.rand() % 3)


def run(subtype, flag, crt):
    rooms, boxes, boss, _ = d.layout(subtype, flag, crt)
    vol = d.layout.volume
    r = d.Crt(_)
    for l in LINKS[scheme_key(subtype, flag, crt)]:
        a = rooms[d.idx(*l[:3])]; b = rooms[d.idx(*l[3:])]
        if not a["present"] or not b["present"]:
            continue
        oa = box_of(l[:3], a); ob = box_of(l[3:], b)
        corridor(vol, oa[0], oa[1], ob[0], ob[1], r)
    return vol, r.state


def box_of(ijk, room):
    v = room["v"]
    i, j, k = ijk
    return [7 * i + 2 + v[0], 7 * j + 2 + v[1], 7 * k + 3 + v[2]], [3 + v[3], 3 + v[4], 1 + v[5]]


if __name__ == "__main__":
    ok = total = 0
    for f in sys.argv[1:]:
        for c in json.load(open(f))["cases"]:
            total += 1
            vol, crt = run(c["subtype"], c["flag"], c["crt_in"])
            mine = bytes(b for cc in vol for b in cc)
            alpha = bytes.fromhex(c["volume"]["cells"])
            bad = [i // 2 for i in range(len(mine)) if mine[i] != alpha[i]]
            good = not bad and crt == c["crt_out"]
            ok += good
            if not good:
                print("FAIL", c["subtype"], c["flag"], c["crt_in"], "cells", len(bad), "crt", crt == c["crt_out"],
                      bad[:3], [(alpha[i*2:i*2+2].hex(), mine[i*2:i*2+2].hex()) for i in bad[:3]])
    print(ok, "/", total)
