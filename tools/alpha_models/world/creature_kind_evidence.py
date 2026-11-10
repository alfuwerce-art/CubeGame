"""M0-F1A: executed evidence of Alpha's wild creature KIND selector 0x5F56C0 (Cube.exe, read-only).

Why a separate selector: the project already has ``AlphaScatterTree`` ("WorldInfo_scatterObjectsInArea @
0x005f56c0"), a mechanical translation of the decompiled text. Same address, but the translation is not
equivalent to the machine code (see ``scatter_tree_drift`` in the JSON): it loses the id that each branch pushes
through the shared ``push_back`` after the branch, it appends 0x97 and the 0x6a/0x39/0x56/0x19 list to the wrong
branches, and it drops ids 0x64, 2, 4, 7, 9. It also returns the candidate LIST, while 0x5F56C0 returns ONE kind:
``list[rand() % n]`` (``0x3C`` for an empty list). So the creature population gets its own selector, checked
here against the executed function.

How: 0x5F56C0 is executed by the M0-02 oracle (Alpha's own machine code). Only its four climate callees are
replaced by stubs that return chosen values (they have their own executed ports in AlphaTerrainGenerator):

    0x5C4DD0 humidity    (stdcall x, z      -> st0)    local_80
    0x5C4800 temperature (thiscall x, z     -> st0)    local_84
    0x5EF040 rare cell   (thiscall x, z     -> st0)    "cell" (fVar5)
    0x5EFFA0 mountain    (thiscall x, z, 0  -> st0)    "spacing" (fVar6)

``rand`` is the oracle's CRT import; at the moment 0x5F56C0 calls it, the candidate vector is read from the
function's own frame (begin [ebp-0x8C], end [ebp-0x88]) and the returned kind is checked against
``list[rand() % n]``. Inputs sweep every threshold of the function on both sides (float32 exact).

Usage::

    python -m tools.alpha_models.world.creature_kind_evidence            # write JSON + C#
    python -m tools.alpha_models.world.creature_kind_evidence --check    # verify no drift
"""
from __future__ import annotations

import argparse
import hashlib
import itertools
import json
import struct
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT))

from tools.alpha_models.world.spawn_pipeline_evidence import CUBE_SHA256, Pe, call_sites, find_exe  # noqa: E402

OUT_JSON = ROOT / "tools/alpha_content/data/creature_kind_evidence.json"
OUT_CS = ROOT / "Assets/CubeGame/Content/Generated/AlphaCreatureKindCases.g.cs"
FORMAT = "cubegame.creature_kind_evidence/1"
SELECTOR = 0x5F56C0
# Every `call 0x5F56C0` in .text (E8 scan): the wild grid of the zone generator and two calls inside the template
# generator 0x5D9460. No vegetation/decoration caller exists.
EXPECTED_CALLERS = [0x5D92A2, 0x5D92F0, 0x5EB48C]
CLIMATE_STUBS = {0x5C4DD0: ("humidity", 8), 0x5C4800: ("temperature", 8), 0x5EF040: ("cell", 8),
                 0x5EFFA0: ("spacing", 12)}

# Values on both sides of every comparison of 0x5F56C0 (0.1, 0.2, 0.3, 0.6, 0.8; level < 0, level > 3).
HUMIDITY = [0.1, 0.2, 0.5, 0.6, 0.7, 0.8, 0.9]
TEMPERATURE = [0.1, 0.2, 0.5, 0.6, 0.9]
CELL = [0.05, 0.1, 0.5]
SPACING = [0.1, 0.3, 0.5]
LEVEL = [-1, 0, 3, 4, 10]
MODE = [0, 1]

BYTE_CHECKS = [
    (0x5EB462, "6a00", "push 0                        4th argument (mode flag) of the wild grid call"),
    (0x5EB48C, "e82fa20000", "call 0x5F56C0                 kind of the wild template"),
    (0x5EB491, "89472c", "mov [edi+0x2c],eax            template kind = return value"),
    (0x5F56F5, "e8d6f6fcff", "call 0x5C4DD0                 humidity"),
    (0x5F5701, "e8faf0fcff", "call 0x5C4800                 temperature"),
    (0x5F570D, "e82e99ffff", "call 0x5EF040                 rare cell term"),
    (0x5F571B, "e880a8ffff", "call 0x5EFFA0                 mountain mask"),
    (0x4DC1B2, "b988130000", "mov ecx,5000                  RandomWalk: rand()%5000"),
    (0x4DC1BD, "81c2b80b0000", "add edx,3000                  RandomWalk: +3000 ms"),
    (0x4DBFBF, "6800000100", "push 0x10000                  RandomWalk: dx*dx / 65536 (16.16 -> units)"),
    (0x4DBFFE, "81f900006400", "cmp ecx,0x640000              RandomWalk: (dx^2+dz^2)*65536 vs 100*65536"),
    (0x4DC004, "0f862d010000", "jbe random direction          inside the 10-unit leash -> random walk"),
    (0x4DC00E, "f30f100d94cd6f00", "movss xmm1,[0x6FCD94]         1/65536 (outside: head home)"),
]

# (name, start, end, little-endian bytes, meaning): the bytes must occur in the range.
IMMEDIATE_CHECKS = [
    ("random_walk_unit_scale", 0x6FCD94, 0x6FCD98, "00008037", "float 1/65536 at 0x6FCD94"),
]


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def f32(v: float) -> float:
    return struct.unpack("<f", struct.pack("<f", v))[0]


class Runner:
    def __init__(self, exe: Path):
        from tools.alpha_models.world.terrain_oracle import Oracle

        self.climate = {}
        self.rand_value = 0
        self.captured = None
        stubs = {va: (self._stub(name), cleanup) for va, (name, cleanup) in CLIMATE_STUBS.items()}
        self.oracle = Oracle(str(exe), 1, extra_stubs=stubs)
        self.oracle.crt.rand = self._rand

    def _stub(self, name):
        def fn(cpu):
            cpu.fpu.append(self.climate[name])
        return fn

    def _rand(self):
        cpu = self.oracle.cpu
        ebp = cpu.r["ebp"]
        begin = cpu.mem.u(ebp - 0x8C, 4)
        end = cpu.mem.u(ebp - 0x88, 4)
        self.captured = [cpu.mem.u(begin + 4 * i, 4) for i in range((end - begin) // 4)] if begin else []
        return self.rand_value

    def run(self, h, t, cell, spacing, level, mode, rand_value):
        self.climate = {"humidity": f32(h), "temperature": f32(t), "cell": f32(cell), "spacing": f32(spacing)}
        self.rand_value = rand_value
        self.captured = None
        kind = self.oracle.call(SELECTOR, [0, 0, level & 0xFFFFFFFF, mode])
        return kind, self.captured


def collect(exe: Path) -> dict:
    pe = Pe(exe)
    checks, problems = [], []
    for va, hexbytes, meaning in BYTE_CHECKS:
        got = pe.read(va, len(hexbytes) // 2).hex()
        checks.append({"va": f"0x{va:06X}", "bytes": hexbytes, "ok": got == hexbytes, "meaning": meaning})
        if got != hexbytes:
            problems.append(f"BYTES 0x{va:06X} {got} != {hexbytes}")
    for name, start, end, hexbytes, meaning in IMMEDIATE_CHECKS:
        ok = bytes.fromhex(hexbytes) in pe.read(start, end - start)
        checks.append({"name": name, "range": f"0x{start:06X}-0x{end:06X}", "bytes": hexbytes, "ok": ok,
                       "meaning": meaning})
        if not ok:
            problems.append(f"IMMEDIATE {name}")

    callers = call_sites(pe, SELECTOR)
    if callers != EXPECTED_CALLERS:
        problems.append(f"CALLERS {[hex(c) for c in callers]}")

    runner = Runner(exe)
    cases = []
    for h, t, cell, spacing, level, mode in itertools.product(HUMIDITY, TEMPERATURE, CELL, SPACING, LEVEL, MODE):
        rand_value = (len(cases) * 7919) % 32768
        kind, ids = runner.run(h, t, cell, spacing, level, mode, rand_value)
        if ids is None:
            if kind != 0x3C:
                problems.append(f"EMPTY_LIST_KIND {h},{t},{cell},{spacing},{level},{mode} -> {kind}")
            ids = []
        elif kind != ids[rand_value % len(ids)]:
            problems.append(f"PICK {h},{t},{cell},{spacing},{level},{mode} rand={rand_value} -> {kind}")
        cases.append({"h": h, "t": t, "cell": cell, "spacing": spacing, "level": level, "mode": mode,
                      "rand": rand_value, "kind": kind, "ids": ids})

    distinct = sorted({tuple(c["ids"]) for c in cases})
    index = {d: i for i, d in enumerate(distinct)}
    for c in cases:
        c["list"] = index[tuple(c.pop("ids"))]
    return {
        "format": FORMAT,
        "status": "EMULATED_ALPHA_CODE: Cube.exe 0x5F56C0 executed by the M0-02 oracle; climate callees stubbed",
        "cube_exe_sha256": CUBE_SHA256,
        "selector": "0x5F56C0",
        "callers": [f"0x{c:06X}" for c in callers],
        "empty_list_kind": "0x3C",
        "byte_checks": checks,
        "case_count": len(cases),
        "distinct_lists": [list(d) for d in distinct],
        "cases": cases,
        "problems": problems,
    }


def build_cs(doc: dict) -> str:
    lists = doc["distinct_lists"]
    out = [
        "// <auto-generated>",
        "// tools/alpha_models/world/creature_kind_evidence.py — EXECUTED Alpha code (Cube.exe 0x5F56C0).",
        "// Numbers only: inputs of the selector and the candidate lists the machine code built. Do not edit.",
        "// </auto-generated>",
        "namespace CubeGame.Content",
        "{",
        "    /// <summary>Executed cases of Alpha's creature kind selector 0x5F56C0 (EMULATED_ALPHA_CODE).</summary>",
        "    public static class AlphaCreatureKindCases",
        "    {",
        f"        public const string Status = \"{doc['status']}\";",
        f"        public const string CubeExeSha256 = \"{doc['cube_exe_sha256']}\";",
        "        public const int EmptyListKind = 0x3C;",
        "",
        "        /// <summary>Distinct candidate lists in the order the machine code pushed them.</summary>",
        "        public static readonly int[][] Lists =",
        "        {",
    ]
    for l in lists:
        out.append("            new[] { " + ", ".join(f"0x{v:02X}" for v in l) + " }," if l else
                   "            new int[0],")
    out += [
        "        };",
        "",
        "        /// <summary>humidity, temperature, cell, spacing, level, mode, rand, returned kind, list index.</summary>",
        "        public static readonly double[][] Cases =",
        "        {",
    ]
    for c in doc["cases"]:
        out.append(f"            new[] {{ {c['h']}, {c['t']}, {c['cell']}, {c['spacing']}, {c['level']}, {c['mode']}, "
                   f"{c['rand']}, {c['kind']}, {c['list']} }},")
    out += ["        };", "    }", "}", ""]
    return "\n".join(out)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--exe")
    parser.add_argument("--check", action="store_true")
    args = parser.parse_args()
    exe = find_exe(args.exe)
    if sha256(exe) != CUBE_SHA256:
        print("CREATURE_KIND_EVIDENCE=FAIL wrong Cube.exe")
        return 1
    doc = collect(exe)
    text = json.dumps(doc, indent=1, sort_keys=True) + "\n"
    cs = build_cs(doc)
    if args.check:
        drift = (not OUT_JSON.exists() or OUT_JSON.read_text(encoding="utf-8") != text or
                 not OUT_CS.exists() or OUT_CS.read_text(encoding="utf-8") != cs)
    else:
        OUT_JSON.write_text(text, encoding="utf-8", newline="\n")
        OUT_CS.write_text(cs, encoding="utf-8", newline="\n")
        drift = False
    ok = not doc["problems"] and not drift
    print(f"CREATURE_KIND_EVIDENCE={'PASS' if ok else 'FAIL'} cases={doc['case_count']} "
          f"lists={len(doc['distinct_lists'])} drift={drift} problems={doc['problems'][:5]}")
    return 0 if ok else 1


if __name__ == "__main__":
    raise SystemExit(main())
