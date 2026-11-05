"""Locate the instructions that read a creature's model-index fields in Creature_drawModel.

Cube World Alpha 2013 has no animation clips; a pose is computed in ``Creature_drawModel``
(``Cube.exe`` RVA ``0x128F0``). The eight body-part model indices are ``int16`` fields at
``creature + 0x8c..+0x9a`` and are read by a run of ``movsx ecx, word ptr [eax+0xNN]``
instructions (the creature pointer is loaded into ``eax`` by ``mov eax,[edi]`` immediately
before each read). The instruction address is the breakpoint used by ``drawmodel_probe``.

This module finds those instructions from the image bytes. It deliberately does *not* assume
a single opcode: ``movsx`` (``0F BF``), ``movzx`` (``0F B7``), the byte variants
(``0F BE``/``0F B6``) and a plain 32-bit ``mov`` (``8B /r``) are all recognised, with full
ModRM/SIB decoding, so a different compiler load shape cannot be silently missed.

The module is pure (no Windows imports) and reads nothing but the bytes it is given.
"""
from __future__ import annotations

from dataclasses import dataclass

# Creature field offsets (int16) that hold the eight model indices.
MODEL_INDEX_FIELDS = (0x8C, 0x8E, 0x90, 0x92, 0x94, 0x96, 0x98, 0x9A)

_REG32 = ("eax", "ecx", "edx", "ebx", "esp", "ebp", "esi", "edi")


@dataclass(frozen=True)
class ModelIndexRead:
    """One instruction that reads a model-index field.

    ``address`` is the virtual address of the first opcode byte. ``op`` is one of
    ``movsx``/``movzx``/``mov``; ``operand`` is the loaded width in bytes (1/2/4).
    ``destination`` is the register the value is loaded into; ``base`` is the register the
    creature pointer lives in (``[base + field]``); ``field`` is the creature offset.
    """

    address: int
    op: str
    operand: int
    destination: str
    base: str
    field: int

    def describe(self) -> str:
        return (f"0x{self.address:08X}  {self.op} {self.destination}, "
                f"{self.operand * 8}-bit ptr [{self.base}+0x{self.field:x}]")


def _decode_ea(blob: bytes, modrm_off: int) -> tuple[int, str | None, int | None]:
    """Decode a ModRM effective address.

    Returns ``(length_after_modrm, base_expr, disp32)``:
      * ``length_after_modrm`` — bytes after the ModRM byte (SIB + displacement);
      * ``base_expr`` — base register name for ``[reg + disp32]``, else ``None``;
      * ``disp32`` — the 32-bit displacement when present (mod == 2), else ``None``.
    """
    modrm = blob[modrm_off]
    mod = (modrm >> 6) & 3
    rm = modrm & 7
    if rm == 4:  # SIB follows
        sib = blob[modrm_off + 1]
        sib_scale = (sib >> 6) & 3
        sib_index = (sib >> 3) & 7
        sib_base = sib & 7
        scale = 1 << sib_scale
        idx = _REG32[sib_index] if sib_index != 4 else ""
        if mod == 0 and sib_base == 5:
            return 5, None, None  # [index*scale + disp32], absolute
        if mod == 1:
            return 2, None, None  # disp8 form
        if mod == 2:
            disp = int.from_bytes(blob[modrm_off + 2:modrm_off + 6], "little")
            parts = [f"{idx}*{scale}" if idx and scale != 1 else idx] if idx else []
            parts.append(_REG32[sib_base])
            return 5, "+".join(parts), disp
        return 1, None, None  # [base] / [base+index*scale], no displacement
    if mod == 0 and rm == 5:
        return 4, None, None  # [disp32] absolute
    if mod == 1:
        return 1, None, None  # [reg + disp8]
    if mod == 2:
        disp = int.from_bytes(blob[modrm_off + 1:modrm_off + 5], "little")
        return 4, _REG32[rm], disp
    return 0, None, None  # [reg]


def scan_model_index_reads(blob: bytes, base_va: int,
                           offsets: tuple[int, ...] = MODEL_INDEX_FIELDS) -> list[ModelIndexRead]:
    """Return every ``[reg + field]`` read of a model-index field inside ``blob``.

    ``blob`` is the raw function bytes; ``base_va`` is the virtual address of ``blob[0]``.
    Only instructions with a 32-bit displacement exactly equal to one of ``offsets`` are
    reported, so a match is a real ``[reg + 0x8c..+0x9a]`` access, not a coincidence.
    """
    wanted = set(offsets)
    found: list[ModelIndexRead] = []
    i = 0
    n = len(blob)
    while i + 2 < n:
        opcode = blob[i]
        op = None
        operand = None
        if opcode == 0x8B:
            op, operand, modrm_off = "mov", 4, i + 1
        elif opcode == 0x0F and i + 3 <= n and blob[i + 1] in (0xBF, 0xB7, 0xBE, 0xB6):
            op = {0xBF: "movsx", 0xB7: "movzx", 0xBE: "movsx", 0xB6: "movzx"}[blob[i + 1]]
            operand = 2 if blob[i + 1] in (0xBF, 0xB7) else 1
            modrm_off = i + 2
        else:
            i += 1
            continue
        length, base, disp = _decode_ea(blob, modrm_off)
        if base is not None and disp is not None and disp in wanted:
            reg = (blob[modrm_off] >> 3) & 7
            found.append(ModelIndexRead(base_va + i, op, operand, _REG32[reg], base, disp))
        i = modrm_off + 1 + length
    return found


def scan_exe_image(image, func_start: int, func_end: int,
                   offsets: tuple[int, ...] = MODEL_INDEX_FIELDS) -> list[ModelIndexRead]:
    """Convenience wrapper: extract the function bytes from a parsed PE image and scan it.

    ``image`` is the result of ``tools.alpha_models.anim.pe_bytes.load`` (it exposes
    ``.data``, ``.image_base`` and ``.sections`` of ``Section`` objects).
    """
    rva = func_start - image.image_base
    for section in image.sections:
        if section.virtual_address <= rva < section.virtual_address + section.virtual_size:
            off = section.raw_offset + (rva - section.virtual_address)
            blob = image.data[off:off + (func_end - func_start)]
            return scan_model_index_reads(blob, func_start, offsets)
    return []


def main():
    import argparse
    from tools.alpha_models.anim.pe_bytes import load
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--exe", required=True)
    parser.add_argument("--start", required=True, help="function start VA, e.g. 0x4128f0")
    parser.add_argument("--end", required=True, help="function end VA, e.g. 0x423f20")
    args = parser.parse_args()
    image = load(args.exe)
    hits = scan_exe_image(image, int(args.start, 16), int(args.end, 16))
    for hit in hits:
        print(hit.describe())
    print(f"total={len(hits)}")


if __name__ == "__main__":
    main()
