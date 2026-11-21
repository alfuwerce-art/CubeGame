"""Alpha voxel marker / hair-tint rule (D61 COLOR/TINT-FIX). Standard library only.

Established from the original client (Cube.exe SHA256 pinned by alpha_static.verify_exe):

1. Load flag. Every model is loaded by ``VoxelModel_load_from_file_or_buffer`` (0x4E7290,
   ``thiscall(src, size, flag)``); the flag is stored at ``model+0x55`` and passed to
   ``VoxelModel_set_data`` (0x4E7650) as ``noRemap``. ``load_flags()`` reads the flag pushed at each
   load call site of the asset loader (``push 0`` / ``push 1``). Hair, eyes and "-random" items
   are loaded with flag 0; heads, bodies, hands, feet, creatures with flag 1.
2. Markers. With flag 0, ``set_data`` collects the coordinates of exact (255,0,0), (0,255,0),
   (0,0,255) voxels into three lists (``model+4/+0x10/+0x1c``).
3. Transparency. ``VoxelModel_build_mesh`` (0x4E7870) skips a voxel when
   ``match_transparent_color(voxel, model+0x55)`` (0x4E71D0) is true. Executed on the real bytes
   (``transparent_by_alpha_code``): strict (flag 1) -> only (0,0,0); non-strict (flag 0) -> (0,0,0)
   and the three exact marker colours. Neighbour faces are tested strictly (exact (0,0,0) or out of
   the grid), so a face next to a marker voxel is NOT emitted: markers are invisible but not holes.
   Check: captured player hair mesh (session2.jsonl, model+0x55 = 0, marker lists 298/4/10 =
   human-hair-m01) has 2244 vertices in Alpha; this rule gives exactly 2244 (the old "every
   non-black voxel" rule gives 4296).
4. Hair tint. ``Creature_drawModel`` draws the hair block (draw site 0x420A38) after
   ``render_setUniform30(colour)`` (0x448280) with colour = appearance hair bytes
   ``creature+0x7A..0x7C`` / 255 (appearance = creature+0x78, ``hair_rgb`` = raw[2:5]); when the
   appearance flag ``creature+0x7E & 0x400`` is clear the colour is first multiplied component-wise
   by the creature colour passed to drawModel (param_6: (1,1,1,1) x status effects). The same
   uniform carries that creature colour for every other part, so it is a per-draw colour factor.
   Neutral review/rendering: hair vertex colour x hair_rgb/255; other parts x 1.
5. Shader (extracted). The D3D9 shaders are embedded in Cube.exe (SM3 bytecode with CTAB). The
   renderer init (0x447E10) creates the world vertex shader from VA 0x6FD928 (vs_3_0) and stores
   register 0x20 at renderer+0x30; render_setUniform30 uploads it with SetVertexShaderConstantF
   (device vtable +0x178). In that shader c32 is ``materialColor`` and the output colour is
   ``L*vertex.rgb*materialColor.rgb + darknessColor*(1-L)``, then ``lerp(.., 1, white)`` (L = clamped
   lighting): the hair colour MULTIPLIES the vertex colour (checked by tests on the real bytes).
6. Hair colour of an NPC (server rule, executed). Server.exe ``generate_entity_appearance``
   (0x40A840) is called with seed = NULL for spawned NPCs; it draws exactly five MSVC ``rand()``:
   hair = (r3, r2, r1) & 0xFF (seed+0x10..0x12), variant seeds s2 = r4 % 100, s3 = r5 % 100. The
   client's creature_generateAppearance copies seed hair into the appearance only for some kinds;
   the others keep the default (255,255,255) of Spawn_initDefaults. Executed for the 15 kinds:
   copy = humans 0x02/0x03, elves 0x00/0x01, lizardmen 0x07/0x08 (random per NPC); fixed white =
   frogmen 0x0D/0x0E (hair drawn in its model colours), cats and runners (no hair block). Server and
   client appearances are identical for the same seeds (60/60 checks).
"""
from __future__ import annotations

import struct

MARKERS = ((255, 0, 0), (0, 255, 0), (0, 0, 255))
MATCH_TRANSPARENT_VA = 0x4E71D0
BSS_ZERO_COLOUR_VA = 0x76B340      # DAT_0076b340: .bss, zero-initialised -> (0,0,0)
HAIR_DRAW_SITE = 0x420A38
HAIR_COLOUR_OFFSET = 0x7A          # creature+0x7A..0x7C (appearance raw[2:5])
HAIR_UNLIT_FLAG = 0x400            # creature+0x7E: hair colour not multiplied by the creature colour
TINT_STATUS = "VERIFIED_ALPHA_CODE (uniform30 = VS c32 materialColor, multiplies the vertex colour)"
WORLD_VS_VA = 0x6FD928             # vs_3_0 of the world/model pass (CTAB: materialColor c32)
RENDER_INIT_VA = 0x447E10          # stores the materialColor register (0x20) at renderer+0x30
SERVER_GENERATE_VA = 0x40A840      # Server.exe generate_entity_appearance
SERVER_INIT_DEFAULT_VA = 0x406970  # Server.exe CreatureAppearance_initDefault
SERVER_RAND_IAT = 0x5582F4         # Server.exe IAT slot of rand()
HAIR_RULE_RANDOM = "RANDOM_PER_NPC"
HAIR_RULE_FIXED = "FIXED_BY_KIND"
MARKER_STATUS = "VERIFIED_ALPHA_CODE (match_transparent_color executed; captured hair mesh 2244 = 2244)"


def transparent(colour, strict: bool) -> bool:
    """Python mirror of match_transparent_color (verified against the executed bytes in tests)."""
    c = tuple(colour)
    if c == (0, 0, 0):
        return True
    return (not strict) and c in MARKERS


def transparent_by_alpha_code(image, colour, strict: bool) -> bool:
    """Execute the original match_transparent_color bytes on one colour."""
    from tools.alpha_models.universal.x86emu import CPU
    cpu = CPU(image)
    cpu.mem.write(BSS_ZERO_COLOUR_VA, bytes(4))   # .bss beyond the raw image: zero-initialised
    buf = 0x0E000000
    cpu.mem.write(buf, bytes(tuple(colour)) + b"\0")
    return (cpu.call(MATCH_TRANSPARENT_VA, args=(buf, 1 if strict else 0)) & 0xFF) != 0


def _decoded_dims(dec: bytes):
    return struct.unpack_from("<3i", dec, 0)


def mesh_stats(dec: bytes, strict: bool) -> dict:
    """Vertex count of Alpha's build_mesh for a .cubdec payload + marker counts."""
    w, d, h = _decoded_dims(dec)

    def col(x, y, z):
        if not (0 <= x < w and 0 <= y < d and 0 <= z < h):
            return (0, 0, 0)
        o = 12 + ((z * d + y) * w + x) * 3
        return (dec[o], dec[o + 1], dec[o + 2])

    faces = 0
    markers = [0, 0, 0]
    hidden = 0
    for z in range(h):
        for y in range(d):
            for x in range(w):
                c = col(x, y, z)
                if c == (0, 0, 0):
                    continue
                if c in MARKERS:
                    markers[MARKERS.index(c)] += 1
                if transparent(c, strict):
                    hidden += 1
                    continue
                for dx, dy, dz in ((1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1)):
                    if col(x + dx, y + dy, z + dz) == (0, 0, 0):   # neighbour test is always strict
                        faces += 1
    return {"vertices": faces * 4, "faces": faces, "markers": markers, "hidden_voxels": hidden}


def load_flags(image) -> dict:
    """.cub name -> set of load flags pushed at the asset loader's VoxelModel load call sites.

    Call site pattern (all 2557 sites of the loader): ``push imm8(flag)`` ... two single-byte
    register pushes (size, source) ... ``call 0x4E7290``; unrelated instructions may sit between.
    A site whose window does not have exactly that shape is reported, never guessed.
    """
    from tools.alpha_models.universal import alpha_static as st
    lo, hi = st.LOAD_ASSETS_VA, st.LOAD_ASSETS_VA + st.LOAD_ASSETS_SPAN
    loads = st.find_call_sites(image.read, lo, hi, st.VOXEL_LOAD_VA)
    flags, problems, prev = {}, [], lo
    for call in loads:
        blob = image.read(prev, call - prev)
        names = []
        for i in range(len(blob) - 6):
            if blob[i] == 0x68 and blob[i + 5] in (0x8D, 0x50):
                s = st._cstr(image, int.from_bytes(blob[i + 1:i + 5], "little"))
                if s and s.endswith(".cub"):
                    names.append(s)
        prev = call
        flag = _flag_before(image.read(call - 24, 24))
        if not names or flag is None:
            problems.append(f"0x{call:06X} names={names} flag={flag}")
            continue
        flags.setdefault(names[-1], set()).add(flag)
    return {"flags": flags, "problems": problems, "sites": len(loads)}


def _flag_before(win: bytes):
    """imm8 of the last ``6A 00|01`` followed by exactly two ``50..57`` register pushes."""
    for i in range(len(win) - 2, -1, -1):
        if win[i] == 0x6A and win[i + 1] in (0, 1):
            tail = win[i + 2:]
            pushes = sum(1 for k, b in enumerate(tail) if 0x50 <= b <= 0x57 and _is_insn_start(tail, k))
            if pushes == 2:
                return win[i + 1]
    return None


def _is_insn_start(tail: bytes, k: int) -> bool:
    """A 0x50..0x57 byte counts as push-reg only when not inside a known longer encoding of the
    window (mov ecx,[eax]=8B 08; mov ecx,[ecx+disp32]=8B 89 ..; mov byte [ebp-4],imm=C6 45 FC ..)."""
    j = 0
    while j < len(tail):
        if j == k:
            return True
        b = tail[j]
        if b == 0x8B and j + 1 < len(tail):
            mod = tail[j + 1] >> 6
            rm = tail[j + 1] & 7
            j += 2 + (4 if mod == 2 else 1 if mod == 1 else 0) + (1 if rm == 4 and mod != 3 else 0)
        elif b == 0xC6 and j + 1 < len(tail):
            mod = tail[j + 1] >> 6
            j += 2 + (4 if mod == 2 else 1 if mod == 1 else 0) + 1
        elif b == 0x6A:
            j += 2
        elif b == 0xE8:
            j += 5
        else:
            j += 1
    return False


def msvc_rand_stream(state: int):
    """MSVC CRT rand(): state = state*214013 + 2531011; yields (state >> 16) & 0x7FFF."""
    while True:
        state = (state * 214013 + 2531011) & 0xFFFFFFFF
        yield (state >> 16) & 0x7FFF


def server_npc_draw(rands):
    """Server rule for one spawned NPC from five consecutive rand() values (docstring item 6)."""
    r1, r2, r3, r4, r5 = list(rands)[:5]
    return {"hair_rgb": [r3 & 0xFF, r2 & 0xFF, r1 & 0xFF], "seed_variant": [r4 % 100, r5 % 100]}


def server_npc_by_alpha_code(server_image, kind: int, rands):
    """Execute Server.exe generate_entity_appearance (seed NULL) with a scripted rand(); returns the
    decoded appearance and the number of rand() calls."""
    from tools.alpha_models.universal import alpha_static as st
    from tools.alpha_models.universal.x86emu import CPU
    seq, used = list(rands), []

    def rand(cpu):
        used.append(seq[len(used)])
        return used[-1]
    cpu = CPU(server_image, iat_stubs={SERVER_RAND_IAT: rand})
    kind_va, item_va = 0x10000000, 0x10001000
    cpu.mem.w(kind_va, 4, kind)
    cpu.mem.write(item_va, bytes([0xAB]) * 0x100)
    cpu.call(SERVER_INIT_DEFAULT_VA, ecx=item_va)
    cpu.call(SERVER_GENERATE_VA, args=(kind_va, item_va, 0))
    return st.decode_appearance(cpu.mem.read(item_va, st.APPEARANCE_SIZE)), len(used)


def hair_rule(appearance_emulator, kind: int, seed=(0, 0)) -> dict:
    """Does the client copy the spawn hair colour for this kind? Two probe colours through the real
    creature_generateAppearance: followed -> random per NPC; otherwise the fixed kind value."""
    from tools.alpha_models.universal import alpha_static as st
    a = st.decode_appearance(appearance_emulator.run(kind, *seed, hair=(11, 22, 33))["raw"])["hair_rgb"]
    b = st.decode_appearance(appearance_emulator.run(kind, *seed, hair=(44, 55, 66))["raw"])["hair_rgb"]
    if list(a) == [11, 22, 33] and list(b) == [44, 55, 66]:
        return {"rule": HAIR_RULE_RANDOM, "fixed": None}
    if list(a) == list(b):
        return {"rule": HAIR_RULE_FIXED, "fixed": list(a)}
    raise ValueError(f"HAIR_RULE_UNDETERMINED kind=0x{kind:02x} {a} {b}")


def part_colour(part: str, appearance: dict, hair_rgb=None, rule=None) -> dict:
    """Static colour factor of a part for a neutral creature colour. Only the hair block has one.

    ``rule`` = hair_rule(): FIXED_BY_KIND -> the kind's fixed value (verified); RANDOM_PER_NPC -> the
    instance colour from the job (``hair_rgb``: a live sample or a server-rule draw), else identity x1
    with status NOT_SET. Never an invented colour.
    """
    if part != "hair":
        return {"tintMode": "NONE", "tint": [], "tintSource": "no colour upload for this block",
                "tintMultipliesCreatureColour": True, "tintStatus": TINT_STATUS}
    unlit = bool(appearance.get("flags", 0) & HAIR_UNLIT_FLAG)
    rule = rule or {"rule": HAIR_RULE_RANDOM, "fixed": None}
    if rule["rule"] == HAIR_RULE_FIXED:
        tint, src = list(rule["fixed"]), "fixed by kind (client generateAppearance ignores the spawn colour)"
    elif hair_rgb:
        tint, src = list(hair_rgb), "job hair_rgb (instance: live sample or server rand rule)"
    else:
        tint, src = [], "NOT_SET: random per NPC (server rand rule), instance colour not chosen (identity x1)"
    return {"tintMode": "HAIR_" + rule["rule"], "tint": tint, "tintSource": src,
            "tintMultipliesCreatureColour": not unlit, "tintStatus": TINT_STATUS}


def shader_blob(image, va):
    """D3D9 shader bytecode at ``va``: version token .. end token 0x0000FFFF."""
    out = bytearray()
    off = va
    while True:
        tok = image.read(off, 4)
        out += tok
        off += 4
        if struct.unpack("<I", tok)[0] == 0x0000FFFF:
            return bytes(out)
        if len(out) > 0x40000:
            raise ValueError("SHADER_END_NOT_FOUND")


def shader_constants(blob: bytes) -> dict:
    """name -> (register set, register index, count) from the CTAB comment of a D3D9 shader."""
    i = 4
    while i < len(blob):
        tok = struct.unpack_from("<I", blob, i)[0]
        if tok & 0xFFFF == 0xFFFE:                      # comment token, length in dwords
            n = tok >> 16
            body = blob[i + 4:i + 4 + n * 4]
            if body[:4] == b"CTAB":
                ct = body[4:]
                _size, _creator, _ver, count, info = struct.unpack_from("<5I", ct, 0)
                out = {}
                for k in range(count):
                    name_off, rset, ridx, rcnt = struct.unpack_from("<IHHH", ct, info + k * 20)
                    name = ct[name_off:ct.index(b"\0", name_off)].decode("ascii")
                    out[name] = (rset, ridx, rcnt)
                return out
            i += 4 + n * 4
        else:
            i += 4
    return {}


def material_colour_evidence(image) -> dict:
    """The chain uniform30 -> VS c32 materialColor, read from the real bytes (see docstring item 5)."""
    consts = shader_constants(shader_blob(image, WORLD_VS_VA))
    init = image.read(RENDER_INIT_VA, 0x200)
    upload = image.read(0x448280, 0x18)
    return {"vs_version": struct.unpack("<I", image.read(WORLD_VS_VA, 4))[0],
            "materialColor": consts.get("materialColor"), "white": consts.get("white"),
            "init_sets_reg_0x20_at_0x30": bytes.fromhex("c7463020000000") in init,
            "upload_reads_0x30": bytes.fromhex("ff7130") in upload,
            "upload_calls_vtable_0x178": bytes.fromhex("ff9278010000") in upload}


def load_hair_evidence(path=None) -> dict:
    """Live hair colours per kind (cubegame.hair_colour_evidence/1)."""
    import json
    from pathlib import Path
    p = Path(path) if path else Path(__file__).with_name("hair_colour_evidence.json")
    return json.loads(p.read_text(encoding="utf-8"))
