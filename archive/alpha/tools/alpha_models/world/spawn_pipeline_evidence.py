"""D1/D2 REVIEW-FIX: byte-level evidence of Alpha's creature spawn pipeline (Cube.exe, read-only).

What this proves, and how:

* ``cube::Spawn`` (0x10F0 bytes, vftable 0x71DBEC) is the creature TEMPLATE. Its constructor is 0x5ADB20.
  Every ``call 0x5ADB20`` in ``.text`` is enumerated (E8 rel32 scan) -> the complete list of places where
  the client creates world creatures. For each site the immediates written to faction (+0x28), kind (+0x2C)
  and class (+0x30) right after the call are collected.
* The template -> creature condition, the respawn timer and the kill write are verified by exact bytes.
* Two per-kind tables are EXECUTED with the M0-02 oracle (Alpha's own machine code):
  0x444020 level range of a wild creature kind, 0x443F40 pack size range of a kind.
* The lists filled by 0x5C9B30 / 0x5CA0A0 are attached to NPC classes 0x80 / 0x81 / 0x83 (call sites verified);
  their records are 0x118-byte ITEM records -> vendor stock, not creature spawn descriptors.

Nothing of Alpha is copied: only addresses, opcodes and numbers. Usage:

    python -m tools.alpha_models.world.spawn_pipeline_evidence            # write JSON + C#
    python -m tools.alpha_models.world.spawn_pipeline_evidence --check    # verify no drift
"""
from __future__ import annotations

import argparse
import bisect
import hashlib
import json
import os
import re
import struct
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
sys.path.insert(0, str(ROOT))
CUBE_SHA256 = "84a7a132a84d4282338e7ea45a1940d32066d64e39cbafed8f2418d5a6dc30bf"
OUT_JSON = ROOT / "tools/alpha_content/data/spawn_pipeline_evidence.json"
OUT_CS = ROOT / "Assets/CubeGame/Content/Generated/AlphaSpawnPipeline.g.cs"
FORMAT = "cubegame.spawn_pipeline_evidence/1"
SPAWN_CTOR = 0x5ADB20
KIND_COUNT = 0xA0

# Functions that contain a Spawn constructor call: start VA -> neutral label (the decompiler's guessed names
# such as "Chunk_generateObjects" or "World_generateTreeRecursive" are NOT used as semantics).
SITE_FUNCTIONS = [
    (0x5AF170, "village_ruins_generator_0x5AF170"),
    (0x5CC510, "generator_0x5CC510"),
    (0x5D6040, "generator_0x5D6040"),
    (0x5D9460, "generator_0x5D9460"),
    (0x5DC6E0, "generator_0x5DC6E0"),
    (0x5E4850, "zone_generator_0x5E4850"),
    (0x5F0CE0, "generator_0x5F0CE0"),
    (0x5F9F70, "end"),
]

# (va, exact bytes, meaning)
BYTE_CHECKS = {
    "template_ctor": [
        (0x5ADB23, "c706ecdb7100", "mov [esi],0x71DBEC            Spawn vftable"),
        (0x5ADB29, "c7460800004843", "mov [esi+8],200.0f            activation radius default"),
        (0x5ADB30, "c7462801000000", "mov [esi+0x28],1              faction default"),
        (0x5ADB37, "c7462c04000000", "mov [esi+0x2c],4              kind default"),
        (0x5ADB44, "c7463401000000", "mov [esi+0x34],1              level default"),
        (0x5ADB4B, "c7463800000000", "mov [esi+0x38],0              respawn timer"),
        (0x5ADB52, "c7463c00000000", "mov [esi+0x3c],0              day of death"),
    ],
    "template_to_creature_condition": [
        (0x60FBC1, "8b4744", "mov eax,[edi+0x44]            window end"),
        (0x60FBC4, "394740", "cmp [edi+0x40],eax            window start == end -> no window"),
        (0x60FBC7, "7428", "je                            skip the time-of-day test"),
        (0x60FBCF, "e83c1de6ff", "call 0x471910                 world time of day (+0x80015C)"),
        (0x60FBD4, "3b4740", "cmp eax,[edi+0x40]"),
        (0x60FBD7, "0f8c56080000", "jl next template              time < window start"),
        (0x60FBE8, "3b4744", "cmp eax,[edi+0x44]"),
        (0x60FBEB, "0f8f42080000", "jg next template              time > window end"),
        (0x60FBF7, "8d4748", "lea eax,[edi+0x48]            template entity id"),
        (0x60FBFB, "e800f4e1ff", "call 0x42F000                 live creature of this id"),
        (0x60FC02, "0f852b080000", "jne next template             a creature already exists"),
        (0x60FC08, "394738", "cmp [edi+0x38],eax(0)         respawn timer"),
        (0x60FC0B, "7d05", "jge"),
        (0x60FC0D, "894738", "mov [edi+0x38],0              clamp a negative timer"),
        (0x60FC12, "7415", "je spawn                      timer == 0"),
        (0x60FC1A, "8b473c", "mov eax,[edi+0x3c]            day of death"),
        (0x60FC1D, "3b8160018000", "cmp eax,[ecx+0x800160]        current day"),
        (0x60FC23, "0f840a080000", "je next template              same day and timer running"),
        (0x60FC64, "f30f104708", "movss xmm0,[edi+8]            activation radius vs player distance"),
    ],
    "respawn_timer": [
        (0x60DF9F, "294838", "sub [eax+0x38],ecx            timer -= frame milliseconds"),
        (0x596C30, "c74038804f1200", "mov [eax+0x38],1200000        written when the creature dies"),
        (0x596C3A, "8b8bb8010000", "mov ecx,[ebx+0x1b8]           creature -> template index in its zone"),
        (0x60C6C0, "ff8760018000", "inc [edi+0x800160]            day counter"),
    ],
    "wild_grid": [
        (0x5EAF9E, "2503000080", "and eax,0x80000003            rand()&3 == 0 -> cell skipped"),
        (0x5EAFAA, "0f84e50b0000", "je next cell"),
        (0x5EAFB3, "b90a000000", "mov ecx,10                    rand()%10 jitter"),
        (0x5EAFC0, "6bf655", "imul esi,esi,0x55             cell step 85"),
        (0x5EAFC3, "83c618", "add esi,0x18                  cell offset 24"),
        (0x5EAFDA, "6bff55", "imul edi,edi,0x55"),
        (0x5EAFEB, "83c718", "add edi,0x18"),
        (0x5EB34D, "e8ce27fcff", "call 0x5ADB20                 template of the cell"),
        (0x5EB48C, "e82fa20000", "call 0x5F56C0                 kind by climate"),
        (0x5EB7B2, "e86988e5ff", "call 0x444020                 level range of the kind"),
        (0x5EB7DC, "895634", "mov [esi+0x34],edx            level = min + rand()%(max-min+1)"),
        (0x5EB838, "837e2801", "cmp [esi+0x28],1              hostile template"),
        (0x5EB83E, "c7464000974901", "mov [esi+0x40],21600000       window start 06:00"),
        (0x5EB845, "c74644005c2605", "mov [esi+0x44],86400000       window end 24:00"),
        (0x5EB87A, "e8c186e5ff", "call 0x443F40                 pack range of the kind"),
        (0x5EB8D9, "e84222fcff", "call 0x5ADB20                 template of a pack member"),
        (0x5EBADD, "e8aeca0000", "call 0x5F8590                 kind of the pack member"),
        (0x5EBB35, "895734", "mov [edi+0x34],edx            pack member level"),
        (0x5EBB68, "f30f114754", "movss [edi+0x54],xmm0         yaw"),
        (0x5EBBAE, "83f803", "cmp eax,3                     3 cells per axis"),
    ],
    "random_interaction_day_window": [
        (0x4DB404, "3d00974901", "cmp eax,21600000              world time < 06:00 -> night set"),
        (0x4DB409, "7c0d", "jl"),
        (0x4DB411, "3d806def04", "cmp eax,82800000              world time < 23:00 -> day set"),
        (0x4DB416, "7c02", "jl"),
    ],
    "vendor_classes": [
        (0x5BCC7A, "c6463084", "class 0x84"),
        (0x5BCE42, "c6463080", "class 0x80"),
        (0x5BCE53, "e8d8cc0000", "call 0x5C9B30                 item list of class 0x80"),
        (0x5BD01E, "c6463082", "class 0x82"),
        (0x5BD1FA, "c6463081", "class 0x81"),
        (0x5BD20B, "e890ce0000", "call 0x5CA0A0                 item list of class 0x81"),
        (0x5BD3D6, "c6463083", "class 0x83"),
        (0x5BD3E7, "e8b4cc0000", "call 0x5CA0A0                 item list of class 0x83"),
    ],
}

# (name, start, end, little-endian immediate bytes, meaning) -> the immediate must occur in the range.
IMMEDIATE_CHECKS = [
    ("walk_path_pause_span", 0x58CFE0, 0x58D630, "a00f0000", "rand()%4000"),
    ("walk_path_pause_base", 0x58CFE0, 0x58D630, "d0070000", "+2000 ms"),
    ("spawn_location_world_time", 0x4DFAC0, 0x4E0050, "5c018000", "world time of day +0x80015C"),
    ("inn_sleep_wake_time", 0x60C510, 0x612000, "80858001", "25 200 000 ms (07:00) after the day is advanced"),
]


class Pe:
    def __init__(self, path: Path):
        self.data = path.read_bytes()
        e = struct.unpack_from("<I", self.data, 0x3C)[0]
        count = struct.unpack_from("<H", self.data, e + 6)[0]
        opt = struct.unpack_from("<H", self.data, e + 20)[0]
        self.base = struct.unpack_from("<I", self.data, e + 24 + 28)[0]
        self.sections = []
        o = e + 24 + opt
        for _ in range(count):
            name = self.data[o:o + 8].rstrip(b"\0").decode()
            vsize, va, rsize, raw = struct.unpack_from("<IIII", self.data, o + 8)
            self.sections.append((name, self.base + va, max(vsize, rsize), raw))
            o += 40

    def read(self, va: int, n: int) -> bytes:
        for _, start, size, raw in self.sections:
            if start <= va < start + size:
                return self.data[raw + va - start: raw + va - start + n]
        raise ValueError(hex(va))

    def text(self):
        for name, start, size, raw in self.sections:
            if name == ".text":
                return start, self.data[raw: raw + size]
        raise ValueError(".text")


def find_exe(explicit: str | None) -> Path:
    candidates = [explicit, os.environ.get("CUBEGAME_ALPHA_EXE"), ROOT / ".scratch/alphatest2/Cube.exe",
                  Path("D:/Projects/CubeGame/.scratch/alphatest2/Cube.exe")]
    for c in candidates:
        if c and Path(c).exists():
            return Path(c)
    raise FileNotFoundError("Cube.exe (local research copy) not found; pass --exe")


def call_sites(pe: Pe, target: int) -> list:
    start, text = pe.text()
    sites, i = [], 0
    while True:
        i = text.find(b"\xe8", i)
        if i < 0:
            break
        if i + 5 <= len(text) and start + i + 5 + struct.unpack_from("<i", text, i + 1)[0] == target:
            sites.append(start + i)
        i += 1
    return sites


_FIELD = re.compile(rb"\xc7[\x40-\x47](\x28|\x2c)(....)|\xc6[\x40-\x47]\x30(.)", re.S)


def site_fields(pe: Pe, site: int, limit: int) -> dict:
    """Immediates written to +0x28 / +0x2C / +0x30 in the bytes after one constructor call.

    A byte-pattern scan of at most 0x300 bytes (up to the next site): it lists constants, it does not prove
    which branch writes them; values computed at run time (kind selectors) are not listed.
    """
    window = pe.read(site + 5, max(0, min(0x300, limit - site - 5)))
    out = {"faction": [], "kind": [], "npc_class": []}
    for m in _FIELD.finditer(window):
        if m.group(1) == b"\x28":
            v = struct.unpack("<I", m.group(2))[0]
            if v < 16:
                out["faction"].append(v)
        elif m.group(1) == b"\x2c":
            v = struct.unpack("<I", m.group(2))[0]
            if v < 0x200:
                out["kind"].append(v)
        else:
            out["npc_class"].append(m.group(3)[0])
    return {k: sorted(set(v)) for k, v in out.items()}


def executed_tables(exe: Path) -> dict:
    from tools.alpha_models.world.terrain_oracle import Oracle
    oracle = Oracle(str(exe), 1)
    buf = oracle.alloc(16)

    def run(va: int) -> list:
        rows = []
        for kind in range(KIND_COUNT):
            oracle.call(va, [kind, buf, buf + 4])
            rows.append([oracle.cpu.mem.u(buf, 4), oracle.cpu.mem.u(buf + 4, 4)])
        return rows

    return {"level_range_0x444020": run(0x444020), "pack_range_0x443F40": run(0x443F40)}


def collect(exe: Path) -> dict:
    pe = Pe(exe)
    sha = hashlib.sha256(pe.data).hexdigest()
    problems = []
    if sha != CUBE_SHA256:
        problems.append(f"CUBE_SHA_MISMATCH {sha}")
    checks = {}
    for group, rows in BYTE_CHECKS.items():
        checks[group] = []
        for va, expected, meaning in rows:
            got = pe.read(va, len(expected) // 2).hex()
            ok = got == expected
            if not ok:
                problems.append(f"BYTES 0x{va:X} expected {expected} got {got}")
            checks[group].append({"va": f"0x{va:X}", "bytes": expected, "meaning": " ".join(meaning.split()), "ok": ok})
    immediates = []
    for name, start, end, imm, meaning in IMMEDIATE_CHECKS:
        count = pe.read(start, end - start).count(bytes.fromhex(imm))
        if count < 1:
            problems.append(f"IMMEDIATE {name} not found")
        immediates.append({"name": name, "range": [f"0x{start:X}", f"0x{end:X}"], "bytes": imm,
                           "meaning": meaning, "occurrences": count})
    sites = call_sites(pe, SPAWN_CTOR)
    starts = [f[0] for f in SITE_FUNCTIONS]
    site_rows = []
    for k, site in enumerate(sites):
        idx = bisect.bisect_right(starts, site) - 1
        if idx < 0 or idx >= len(SITE_FUNCTIONS) - 1:
            problems.append(f"SITE 0x{site:X} outside the known functions")
            continue
        limit = sites[k + 1] if k + 1 < len(sites) else site + 0x300
        row = {"va": f"0x{site:X}", "function": SITE_FUNCTIONS[idx][1]}
        row.update(site_fields(pe, site, limit))
        site_rows.append(row)
    tables = executed_tables(exe)
    return {
        "format": FORMAT,
        "cube_sha256": sha,
        "spawn_ctor": f"0x{SPAWN_CTOR:X}",
        "template_bytes": 0x10F0,
        "byte_checks": checks,
        "immediate_checks": immediates,
        "template_sites": site_rows,
        "template_site_count": len(site_rows),
        "executed_tables": tables,
        "problems": problems,
        "RESULT": "PASS" if not problems else "FAIL",
    }


def build_cs(doc: dict) -> str:
    def ranges(rows, default):
        groups = {}
        for kind, (lo, hi) in enumerate(rows):
            if [lo, hi] != default:
                groups.setdefault((lo, hi), []).append(kind)
        lines = []
        for (lo, hi), kinds in sorted(groups.items()):
            cases = " ".join(f"case 0x{k:02X}:" for k in kinds)
            lines.append(f"                {cases}\n                    min = {lo}; max = {hi}; return;")
        return "\n".join(lines)

    level = doc["executed_tables"]["level_range_0x444020"]
    pack = doc["executed_tables"]["pack_range_0x443F40"]
    sites = "\n".join(
        f"            new AlphaSpawnSiteDef(0x{int(s['va'], 16):X}, \"{s['function']}\", "
        f"new int[] {{ {', '.join(str(v) for v in s['faction'])} }}, "
        f"new int[] {{ {', '.join(f'0x{v:02X}' for v in s['kind'])} }}, "
        f"new int[] {{ {', '.join(f'0x{v:02X}' for v in s['npc_class'])} }}),"
        for s in doc["template_sites"])
    return f"""// <auto-generated>
//   {FORMAT} — generated by tools/alpha_models/world/spawn_pipeline_evidence.py
//   source: Cube.exe sha256 {doc['cube_sha256'][:16]}… (local research copy, read-only)
//   DO NOT EDIT: regenerate with `python -m tools.alpha_models.world.spawn_pipeline_evidence`
// </auto-generated>
namespace CubeGame.Content
{{
    /// <summary>One place in Cube.exe that constructs a creature template (call 0x5ADB20) and the constants written next to it.</summary>
    public readonly struct AlphaSpawnSiteDef
    {{
        public readonly int Address;
        public readonly string Function;
        public readonly int[] Factions;
        public readonly int[] Kinds;
        public readonly int[] NpcClasses;

        public AlphaSpawnSiteDef(int address, string function, int[] factions, int[] kinds, int[] npcClasses)
        {{
            Address = address;
            Function = function;
            Factions = factions;
            Kinds = kinds;
            NpcClasses = npcClasses;
        }}
    }}

    /// <summary>Byte-verified and executed data of Alpha's creature spawn pipeline.</summary>
    public static class AlphaSpawnPipelineData
    {{
        public const string Format = "{FORMAT}";
        public const string SitesStatus = "ALPHA_CODE_STATIC";
        public const string TablesStatus = "EMULATED_ALPHA_CODE";
        public const int KindCount = 0x{KIND_COUNT:X};

        /// <summary>Every `call 0x5ADB20` of the client (complete E8 scan of .text).</summary>
        public static readonly AlphaSpawnSiteDef[] Sites =
        {{
{sites}
        }};

        /// <summary>Cube.exe 0x444020 executed for every kind: level range of a wild creature.</summary>
        public static void LevelRange(int kind, out int min, out int max)
        {{
            switch (kind)
            {{
{ranges(level, [2, 4])}
                default:
                    min = 2; max = 4; return;
            }}
        }}

        /// <summary>Cube.exe 0x443F40 executed for every kind: pack size range (leader included).</summary>
        public static void PackRange(int kind, out int min, out int max)
        {{
            switch (kind)
            {{
{ranges(pack, [1, 1])}
                default:
                    min = 1; max = 1; return;
            }}
        }}
    }}
}}
"""


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--exe")
    ap.add_argument("--check", action="store_true")
    a = ap.parse_args()
    doc = collect(find_exe(a.exe))
    text_json = json.dumps(doc, indent=1) + "\n"
    text_cs = build_cs(doc)
    if a.check:
        drift = (not OUT_JSON.exists() or OUT_JSON.read_text(encoding="utf-8") != text_json
                 or not OUT_CS.exists() or OUT_CS.read_text(encoding="utf-8") != text_cs)
        ok = doc["RESULT"] == "PASS" and not drift
        print(f"SPAWN_PIPELINE_EVIDENCE={'PASS' if ok else 'FAIL'} drift={drift} problems={doc['problems']}")
        return 0 if ok else 1
    OUT_JSON.write_text(text_json, encoding="utf-8", newline="\n")
    OUT_CS.write_text(text_cs, encoding="utf-8", newline="\n")
    print(f"SPAWN_PIPELINE_EVIDENCE={doc['RESULT']} sites={doc['template_site_count']} problems={doc['problems']}")
    return 0 if doc["RESULT"] == "PASS" else 1


if __name__ == "__main__":
    raise SystemExit(main())
