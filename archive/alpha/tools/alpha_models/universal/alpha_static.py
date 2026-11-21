"""Static extraction of the Alpha 2013 creature -> model -> rig parameters (no Alpha run).

Everything here is read from the shipped ``Cube.exe`` bytes (read-only) and, for display names
only, from the local decompilation text. Universal pipeline, stage "Model Scanner / Entity
Resolver" (docs/architecture/UNIVERSAL_ASSET_ANIMATION_PIPELINE.md).

Sources (all VAs of the local research copy, SHA256 checked by the caller):

* ``Spawn_initDefaults`` (VA 0x428750, thiscall ECX=appearance) — default appearance;
* ``creature_generateAppearance`` (VA 0x43F7C0, cdecl ``(uint *kind, appearance *, seed *)``) —
  156-way switch "creature kind -> model slots, part scales, pitches, offsets, flags". Both are
  EXECUTED by the strict interpreter ``x86emu`` instead of read from the decompilation, because
  the decompilation loses the destination of every ``vec3_copy`` (offset vectors);
* ``GameController_load_game_assets`` (VA 0x4EE230) — the model registry: for every
  ``VoxelModel_load_from_file_or_buffer`` call the pushed ``"<name>.cub"`` string and the index
  pushed to ``vector_at_stride4`` (the ``std::vector<Model*>`` that ``Creature_drawModel`` indexes
  with the appearance slots). Decoded with strict instruction patterns; anything else is
  reported as unmatched instead of guessed.

Verified against runtime (``pose_capture`` creature header, 2026-11-05): the emulated appearance
equals the live ``creature+0x78`` struct byte-for-byte (0xAC bytes, padding byte 5 excepted) for
kind 0x20 (WhiteCat) and kind 0x2 (human male, seed variant 0/0).

CLI::

    python -m tools.alpha_models.universal.alpha_static scan --exe <Cube.exe> --out table.json
        [--db data1.db] [--decomp <CubeWorld-Reversal/cube>]
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import sqlite3
import struct
import sys
from bisect import bisect_left
from pathlib import Path

from tools.alpha_models.anim import pe_bytes as pb
from tools.alpha_models.runtime.pose_capture import find_call_sites
from tools.alpha_models.universal.x86emu import CPU, EmuError

# The only Cube.exe build every VA in this package was read from (the local research copy in
# .scratch\alphatest2 is byte-identical to D:\Games\Cube world\Cube.exe). Any other build is refused:
# addresses must never be applied blindly to a different executable.
EXPECTED_EXE_SHA256 = "84a7a132a84d4282338e7ea45a1940d32066d64e39cbafed8f2418d5a6dc30bf"


class UnknownBuild(Exception):
    pass


def verify_exe(path):
    got = sha256_file(path)
    if got != EXPECTED_EXE_SHA256:
        raise UnknownBuild(f"Cube.exe SHA256 {got} != {EXPECTED_EXE_SHA256}: addresses not verified for this build")
    return got

INIT_DEFAULTS_VA = 0x428750
GENERATE_APPEARANCE_VA = 0x43F7C0
RAND_IAT = 0x6FC468
LOAD_ASSETS_VA = 0x4EE230
LOAD_ASSETS_SPAN = 0x60000
VOXEL_LOAD_VA = 0x4E7290
VECTOR_AT_VA = 0x468C60
KIND_MAX = 0x9B                 # last explicit `case` of creature_generateAppearance
APPEARANCE_SIZE = 0xAC          # creature+0x78 .. creature+0x124
SEED_SWEEP = 60                 # seed[2]/seed[3] values tried per kind (lcm of the % divisors)

# Slot order of appearance+0x14 (i16[8]) and where Creature_drawModel stores the model pointer.
SLOTS = [  # (name, appearance offset, creature offset, drawModel self index)
    ("head", 0x14, 0x8C, 0x2F),
    ("hair", 0x16, 0x8E, 0x30),
    ("hand", 0x18, 0x90, 0x33),
    ("foot", 0x1A, 0x92, 0x36),
    ("body", 0x1C, 0x94, 0x31),
    ("tail", 0x1E, 0x96, 0x35),
    ("slot_98", 0x20, 0x98, 0x32),
    ("slot_9a", 0x22, 0x9A, 0x34),
]
# Float fields. Names are given only where Creature_drawModel's use proves the meaning
# (GameController.cpp reconstruction line in brackets); the rest keep their offset as name.
SCALES = [  # appearance offset -> name
    (0x24, "scale_head"),      # head block mat4_scaleAxes(local_16d8) [6262]
    (0x28, "scale_body"),      # tail/neck blocks mat4_scaleAxes(local_16bc) [6224, 6165]
    (0x2C, "scale_hand"),      # local_1698
    (0x30, "scale_foot"),      # local_168c
    (0x34, "scale_34"),        # local_16f8
    (0x38, "scale_weapon"),    # local_1694; humanoid capture: = weapon scale 0.95 (S05A-2)
    (0x3C, "scale_tail"),      # tail block mat4_scaleAxes(local_169c) [6223]
    (0x40, "scale_40"),
    (0x44, "scale_44"),
]
PITCHES = [(0x48 + 4 * i, f"angle_{0x48 + 4 * i:02x}") for i in range(7)]
PITCHES[6] = (0x60, "pitch_tail")   # tail block mat4_rotateX(creature+0xd8) [6220]
OFFSETS = [  # vec3 fields
    (0x64, "offset_body"),     # local_172c.. (head block uses body offset z/2) [6248]
    (0x70, "offset_head"),
    (0x7C, "offset_hand"),     # walk formula uses creature+0xf4..0xfc [2414-2434]
    (0x88, "offset_foot"),     # creature+0x100..0x108 [2453-2458]
    (0x94, "offset_tail"),     # creature+0x10c..0x114 -> tail block translate [6217]
    (0xA0, "offset_a0"),
]
# drawModel branches on these kinds explicitly (creature+0x64 compared in the function body):
# 0x57 and 0x60 at 6 sites, 0x65 at 2 sites (head anchor sy*0.2, aim vector). Such kinds can
# never share a motion family with others without their own evidence.
KIND_SPECIAL_BRANCHES = {0x57: "drawModel kind!=0x57 guards (6 sites)",
                         0x60: "drawModel kind!=0x60 guards (6 sites)",
                         0x65: "drawModel head anchor (sx/2, sy*0.2, sz/2) + aim vector"}


def sha256_file(path):
    h = hashlib.sha256()
    with open(path, "rb") as fh:
        for chunk in iter(lambda: fh.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


# --------------------------------------------------------------------------- appearance
class AppearanceEmulator:
    """Runs Spawn_initDefaults + creature_generateAppearance for one kind / seed variant."""

    KIND, ITEM, SEED = 0x10000000, 0x10001000, 0x10002000

    def __init__(self, image):
        self.image = image

    def run(self, kind, s2=0, s3=0, hair=(0, 0, 0)):
        rand_calls = []

        def rand(cpu):
            v = len(rand_calls)
            rand_calls.append(v)
            return v

        cpu = CPU(self.image, iat_stubs={RAND_IAT: rand})
        cpu.mem.w(self.KIND, 4, kind)
        cpu.mem.write(self.ITEM, b"\xab" * 0x100)      # sentinel: shows unwritten bytes
        cpu.call(INIT_DEFAULTS_VA, ecx=self.ITEM)
        defaults = cpu.mem.read(self.ITEM, APPEARANCE_SIZE)
        cpu.mem.write(self.SEED, b"\x00" * 0x40)
        cpu.mem.w(self.SEED, 4, 99)                     # race 99 -> keep *kind as given
        cpu.mem.w(self.SEED + 8, 4, s2)
        cpu.mem.w(self.SEED + 12, 4, s3)
        cpu.mem.write(self.SEED + 0x10, bytes(hair))
        cpu.call(GENERATE_APPEARANCE_VA, args=(self.KIND, self.ITEM, self.SEED))
        raw = cpu.mem.read(self.ITEM, APPEARANCE_SIZE)
        # issue #7: an explicitly activated VISUAL CORRECTION PROPOSAL (off by default; not Alpha parity)
        from tools.alpha_models.universal import visual_correction as vc
        raw = vc.apply(cpu.mem.u(self.KIND, 4), raw)
        return {"raw": raw, "defaults": defaults, "rand_calls": len(rand_calls),
                "kind_after": cpu.mem.u(self.KIND, 4), "steps": cpu.steps}


def decode_appearance(raw):
    out = {"flags": struct.unpack_from("<H", raw, 6)[0],
           "hair_rgb": list(raw[2:5]),
           "size": list(struct.unpack_from("<3f", raw, 8)),
           "slots": {n: struct.unpack_from("<h", raw, off)[0] for n, off, _, _ in SLOTS}}
    for off, n in SCALES + PITCHES:
        out[n] = round(struct.unpack_from("<f", raw, off)[0], 6)
    for off, n in OFFSETS:
        out[n] = [round(v, 6) for v in struct.unpack_from("<3f", raw, off)]
    return out


def motion_params(dec):
    """The appearance fields that drive Creature_drawModel's pose maths (everything except model
    choices and colours). Two kinds with equal motion params and part set get equal joints."""
    keep = {k: v for k, v in dec.items() if k not in ("slots", "hair_rgb")}
    return keep


def kind_table(image, kinds=range(0, KIND_MAX + 1), sweep=SEED_SWEEP):
    emu = AppearanceEmulator(image)
    table = {}
    for kind in kinds:
        entry = {"kind": kind}
        try:
            base = emu.run(kind)
        except EmuError as e:
            entry["error"] = str(e)
            table[kind] = entry
            continue
        dec = decode_appearance(base["raw"])
        entry["default_branch"] = base["raw"] == base["defaults"][:APPEARANCE_SIZE] or \
            _only_seed_fields_changed(base["raw"], base["defaults"])
        entry["uses_rand"] = base["rand_calls"] > 0
        entry["appearance"] = dec
        entry["appearance_sha1"] = hashlib.sha1(base["raw"]).hexdigest()[:12]
        # variant sweep: which slots / params depend on the seed
        slot_values = {n: set() for n, *_ in SLOTS}
        motion_sigs = set()
        for s in range(sweep):
            for which in (2, 3):
                r = emu.run(kind, s2=s if which == 2 else 0, s3=s if which == 3 else 0)
                d = decode_appearance(r["raw"])
                for n, v in d["slots"].items():
                    slot_values[n].add(v)
                motion_sigs.add(json.dumps(motion_params(d), sort_keys=True))
        entry["slot_variants"] = {n: sorted(v) for n, v in slot_values.items() if v != {-1}}
        entry["motion_param_variants"] = len(motion_sigs)
        table[kind] = entry
    return table


def _only_seed_fields_changed(raw, defaults):
    # the default branch still writes size (+8..+0x13); everything else must equal defaults
    diff = [i for i in range(APPEARANCE_SIZE) if raw[i] != defaults[i]]
    return all(8 <= i < 0x14 or i in (2, 3, 4) for i in diff)


# --------------------------------------------------------------------------- model registry
def _cstr(image, va, limit=96):
    try:
        b = image.read(va, limit)
    except Exception:  # noqa: BLE001
        return None
    z = b.find(b"\0")
    if z <= 0:
        return None
    s = b[:z]
    return s.decode("ascii") if all(32 <= c < 127 for c in s) else None


def _decode_vector_index(image, call_va):
    """Index pushed for ``call vector_at_stride4``: strict backwards decode of
    ``push imm32|imm8 ; mov ecx, reg ; [mov dword|byte [ebp-4], imm] ; call``."""
    for tail, opcode in ((7, b"\xc7\x45\xfc"), (4, b"\xc6\x45\xfc"), (0, None)):
        p = call_va - tail
        if opcode is not None and image.read(p, 3) != opcode:
            continue
        m = image.read(p - 2, 2)
        if m[0] != 0x8B or (m[1] & 0xC0) != 0xC0 or ((m[1] >> 3) & 7) != 1:
            continue
        a = image.read(p - 7, 5)
        if a[0] == 0x68:
            return int.from_bytes(a[1:5], "little")
        b = image.read(p - 4, 2)
        if b[0] == 0x6A:
            return b[1]
    return None


def _decode_inline_index(blob):
    """Last ``8b 0b [6a 01] 8b 89 disp32`` / ``8b 49 disp8`` / ``8b 09`` in the window (strict)."""
    found = None
    for i in range(len(blob) - 8):
        if blob[i:i + 2] != bytes.fromhex("8b0b"):
            continue
        j = i + 2
        if blob[j:j + 2] in (bytes.fromhex("6a01"), bytes.fromhex("6a00")):
            # push <load flag> (1 = markers, 0 = no marker colours: hair/eyes, D61); both seen
            j += 2
        if blob[j:j + 2] == bytes.fromhex("8b89"):
            disp = int.from_bytes(blob[j + 2:j + 6], "little")
        elif blob[j:j + 2] == bytes.fromhex("8b49"):
            disp = blob[j + 2]
        elif blob[j:j + 2] == bytes.fromhex("8b09"):    # mov ecx,[ecx] -> element 0
            disp = 0
        else:
            continue
        if disp % 4 == 0:
            found = disp // 4
    return found


def _load_flag(blob):
    """Load flag (3rd argument of VoxelModel_load_from_file_or_buffer, stored at model+0x55): the
    last ``push 0|1`` immediately before the call (``6a 00|01`` followed by at most the
    ``mov ecx/edx`` + ``push reg`` pair the compiler places between them). None if not decodable."""
    for i in range(len(blob) - 2, max(len(blob) - 24, 0) - 1, -1):
        if blob[i] == 0x6A and blob[i + 1] in (0, 1):
            return blob[i + 1]
    return None


def model_registry(image, detail=False):
    """index -> .cub name of Alpha's shared model vector (creature parts, items, props).

    ``GameController_load_game_assets`` is ONE straight-line function (the decompilation shows the
    exception-state counter ``local_8`` increasing monotonically through all 2557 loads), so the
    load order is the address order. ``VoxelModel_load_from_file_or_buffer`` loads INTO the
    vector element, therefore when an index is loaded twice the LATER load is what Alpha keeps
    (D64 / B-3: index 1300 = orc-head-m01.cub, then orc-head.cub). Overwritten loads are reported.

    detail=True also returns {index: {"name", "load_flag", "call_va", "overwritten": [...]}}."""
    lo, hi = LOAD_ASSETS_VA, LOAD_ASSETS_VA + LOAD_ASSETS_SPAN
    loads = find_call_sites(image.read, lo, hi, VOXEL_LOAD_VA)
    vcalls = sorted(find_call_sites(image.read, lo, hi, VECTOR_AT_VA))
    registry, info = {}, {}
    stats, prev = {"loads": len(loads), "member_slot": 0, "no_name": 0, "several_names": 0,
                   "several_vector_calls": 0, "undecoded_index": 0, "conflicts": 0,
                   "overwritten_loads": 0}, lo
    for call in loads:
        blob = image.read(prev, call - prev)
        names = []
        for i in range(len(blob) - 6):
            if blob[i] == 0x68 and blob[i + 5] in (0x8D, 0x50):
                s = _cstr(image, int.from_bytes(blob[i + 1:i + 5], "little"))
                if s and s.endswith(".cub"):
                    names.append(s)
        vs = vcalls[bisect_left(vcalls, prev):bisect_left(vcalls, call)]
        prev = call
        if not names:
            stats["no_name"] += 1
            continue
        if len(names) > 1:
            stats["several_names"] += 1
            continue
        idx = None
        if not vs:
            # inlined vector access: `mov ecx,[ebx]` (vector begin) ; `push 0|1` (load flag) ;
            # `mov ecx,[ecx+disp32|disp8]` -> element disp/4 of the SAME vector
            idx = _decode_inline_index(blob)
            if idx is None:
                stats["member_slot"] += 1
                continue
            stats["inline_index"] = stats.get("inline_index", 0) + 1
        else:
            last = vs[-1]
            if len(vs) > 1:
                # a generator loop runs before the load (sphinx01.cub, VA 0x51BCC2): only the LAST
                # vector call feeds the load, and only if no other call sits between them
                stats["several_vector_calls"] += 1
                tail = image.read(last + 5, call - last - 5)
                if 0xE8 in tail or len(tail) > 24:
                    stats["undecoded_index"] += 1
                    continue
            idx = _decode_vector_index(image, last)
            if idx is None:
                stats["undecoded_index"] += 1
                continue
        rec = {"name": names[0], "load_flag": _load_flag(blob), "call_va": f"0x{call:06X}", "overwritten": []}
        if idx in info:
            stats["conflicts"] += 1
            stats["overwritten_loads"] += 1
            rec["overwritten"] = info[idx]["overwritten"] + [{"name": info[idx]["name"], "call_va": info[idx]["call_va"]}]
        info[idx] = rec
        registry[idx] = names[0]
    stats["indexed"] = len(registry)
    if detail:
        return registry, stats, info
    return registry, stats


# --------------------------------------------------------------------------- names (hint only)
def kind_names(decomp_dir, image=None):
    """Kind -> display name from the decompiled name map (World.cpp). HINT, not evidence."""
    path = Path(decomp_dir) / "world" / "World.cpp"
    if not path.exists():
        return {}
    text = path.read_text(encoding="utf-8", errors="replace")
    names = {}
    pat = re.compile(r"local_30 = \(undefined4 \*\)(0x[0-9a-f]+|\d+);\s*\n\s*World_map_find_or_insert_int"
                     r"\(&local_30\);\s*\n\s*u16string_assignPtrLen\((L\"([^\"]+)\"|&DAT_([0-9a-f]+)),")
    for m in pat.finditer(text):
        kind = int(m.group(1), 0)
        name = m.group(3)
        if name is None and image is not None:
            try:
                raw = image.read(int(m.group(4), 16), 64)
                name = raw.decode("utf-16-le").split("\0")[0]
            except Exception:  # noqa: BLE001
                name = None
        names.setdefault(kind, name)
    return names


# --------------------------------------------------------------------------- db sizes
def model_sizes(db_path, names):
    """Voxel grid size (x, y, z) of every named model from data1.db (read-only)."""
    sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
    from cub_format import deobfuscate, parse_decoded  # noqa: E402
    con = sqlite3.connect(f"file:{db_path}?mode=ro&immutable=1", uri=True)
    out = {}
    for n in sorted(set(names)):
        row = con.execute("select value from blobs where key=?", (n,)).fetchone()
        if row is None:
            out[n] = None
            continue
        m = parse_decoded(deobfuscate(row[0]))
        out[n] = [m.width, m.depth, m.height]
    return out


def registry_model_sizes(db_path, registry):
    """{index: [x, y, z]} for EVERY registry model (equipment included), from the .cub header only
    (first three int32 of the deobfuscated blob; no voxel data is converted). Read-only."""
    sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
    from cub_format import deobfuscate  # noqa: E402
    con = sqlite3.connect(f"file:{db_path}?mode=ro&immutable=1", uri=True)
    out = {}
    for idx, name in registry.items():
        row = con.execute("select value from blobs where key=?", (name,)).fetchone()
        if row is not None:
            out[int(idx)] = list(struct.unpack_from("<3i", deobfuscate(row[0]), 0))
    return out


def scan(exe, db=None, decomp=None):
    verify_exe(exe)
    image = pb.load(exe)
    table = kind_table(image)
    registry, reg_stats, reg_detail = model_registry(image, detail=True)
    names = kind_names(decomp, image) if decomp else {}
    used = set()
    for e in table.values():
        for vals in e.get("slot_variants", {}).values():
            used.update(v for v in vals if v >= 0)
    sizes = model_sizes(db, [registry[i] for i in used if i in registry]) if db else {}
    return {
        "format": "cubegame.alpha_static/1",
        "exe_sha256": sha256_file(exe),
        "kinds": {f"0x{k:02x}": {**v, "name_hint": names.get(k)} for k, v in table.items()},
        "registry_stats": reg_stats,
        "registry": {str(k): v for k, v in sorted(registry.items())},
        # D64: load flag (model+0x55, D61 marker rule) and loads a later load of the same index replaced
        "registry_load": {str(k): {"load_flag": v["load_flag"], "call_va": v["call_va"],
                                   "overwritten": [o["name"] for o in v["overwritten"]]}
                          for k, v in sorted(reg_detail.items())},
        "model_sizes": sizes,
        "unresolved_slot_indices": sorted(i for i in used if i not in registry),
    }


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    sub = ap.add_subparsers(dest="cmd", required=True)
    s = sub.add_parser("scan")
    s.add_argument("--exe", default=r"D:\Projects\CubeGame\.scratch\alphatest2\Cube.exe")
    s.add_argument("--db", default=r"D:\Projects\CubeGame\.scratch\alphatest2\data1.db")
    s.add_argument("--decomp", default=r"D:\Projects\CubeGame\.scratch\CubeWorld-Reversal\cube")
    s.add_argument("--out", required=True)
    a = ap.parse_args(argv)
    res = scan(a.exe, a.db if os.path.exists(a.db) else None,
               a.decomp if os.path.isdir(a.decomp) else None)
    os.makedirs(os.path.dirname(os.path.abspath(a.out)), exist_ok=True)
    with open(a.out, "w", encoding="utf-8") as fh:
        json.dump(res, fh, indent=1)
    print(json.dumps({"STATIC_SCAN": "DONE", "kinds": len(res["kinds"]),
                      "registry": res["registry_stats"],
                      "unresolved_slot_indices": len(res["unresolved_slot_indices"])}, indent=1))
    return 0


if __name__ == "__main__":
    sys.exit(main())
