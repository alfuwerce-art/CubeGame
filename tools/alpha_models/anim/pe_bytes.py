"""Minimal 32-bit PE reader + x86 call-site scanner (standard library only).

Purpose (S05A-2 / D58): the Ghidra reconstruction of Cube.exe decompiles the tiny
accessor helpers (``arrayElem_stride4`` / ``arrayElem_stride8``) with an *uninitialised*
``self`` base pointer, because the base arrives in ECX (thiscall). The decompiled text
therefore cannot say which array a given ``*arrayElem_stride4(0) = -100.0f`` write goes
to. This module recovers that information from the shipped bytes instead of guessing:

  * every ``E8 rel32`` call whose target is one of the known helper addresses is located
    inside a virtual-address range of the image;
  * the instruction immediately before the call (the ``push imm32`` that carries the
    element index) and the instruction before it (``mov ecx, ...`` that carries the base)
    are decoded with a strict, tiny pattern matcher;
  * a base expressed as ``[ebp-0xNNN]`` is translated into the decompiler's own local
    name (``local_0NNN``), which is what lets a data write be tied to a decompiled
    variable.

Only two-opcode patterns are decoded, and every candidate is validated (call target,
push opcode, mov-ecx opcode). Anything that does not match is reported as unmatched
instead of being guessed, so a wrong answer cannot silently look right.

The tool never writes to the image; it opens the file read-only.
"""

from __future__ import annotations

import struct
from dataclasses import dataclass
from typing import Iterable, Optional


class PEError(Exception):
    """Raised when the file is not a parseable 32-bit PE image."""


@dataclass(frozen=True)
class Section:
    """A PE section; ``virtual_address`` is an RVA, callers work with absolute VAs."""

    name: str
    virtual_address: int
    virtual_size: int
    raw_offset: int
    raw_size: int

    def contains(self, rva: int) -> bool:
        return self.virtual_address <= rva < self.virtual_address + self.virtual_size


@dataclass(frozen=True)
class Image:
    path: str
    image_base: int
    data: bytes
    sections: tuple[Section, ...]

    def section_for(self, va: int) -> Optional[Section]:
        rva = va - self.image_base
        for section in self.sections:
            if section.contains(rva):
                return section
        return None

    def offset_of(self, va: int) -> int:
        section = self.section_for(va)
        if section is None:
            raise PEError(f"virtual address 0x{va:08X} is not inside any section")
        delta = (va - self.image_base) - section.virtual_address
        if delta >= section.raw_size:
            raise PEError(f"virtual address 0x{va:08X} is beyond the raw data of {section.name!r}")
        return section.raw_offset + delta

    def read(self, va: int, size: int) -> bytes:
        offset = self.offset_of(va)
        chunk = self.data[offset:offset + size]
        if len(chunk) != size:
            raise PEError(f"cannot read {size} bytes at 0x{va:08X} (file truncated)")
        return chunk

    def u32(self, va: int) -> int:
        return struct.unpack_from("<I", self.read(va, 4))[0]

    def i32(self, va: int) -> int:
        return struct.unpack_from("<i", self.read(va, 4))[0]


def load(path: str) -> Image:
    with open(path, "rb") as handle:
        return load_from_bytes(handle.read(), path)


def load_from_bytes(data: bytes, path: str = "<memory>") -> Image:
    """Parse a PE32 image from memory (also used by the unit tests)."""
    if len(data) < 0x40 or data[:2] != b"MZ":
        raise PEError("not an MZ executable")
    pe_offset = struct.unpack_from("<I", data, 0x3C)[0]
    if data[pe_offset:pe_offset + 4] != b"PE\0\0":
        raise PEError("missing PE signature")
    machine, section_count = struct.unpack_from("<HH", data, pe_offset + 4)
    if machine != 0x014C:
        raise PEError(f"expected i386 (0x014C), found 0x{machine:04X}")
    optional_size = struct.unpack_from("<H", data, pe_offset + 20)[0]
    optional = pe_offset + 24
    magic = struct.unpack_from("<H", data, optional)[0]
    if magic != 0x010B:
        raise PEError(f"expected PE32 optional header (0x010B), found 0x{magic:04X}")
    image_base = struct.unpack_from("<I", data, optional + 28)[0]
    sections = []
    table = optional + optional_size
    for index in range(section_count):
        entry = table + index * 40
        raw_name = data[entry:entry + 8].rstrip(b"\0")
        virtual_size, virtual_address, raw_size, raw_offset = struct.unpack_from("<IIII", data, entry + 8)
        sections.append(Section(raw_name.decode("ascii", "replace"), virtual_address,
                                virtual_size, raw_offset, raw_size))
    return Image(path, image_base, data, tuple(sections))


@dataclass(frozen=True)
class CallSite:
    call_va: int
    target_va: int
    index: Optional[int]
    base: Optional["BaseRef"]
    raw: bytes

    @property
    def call_offset(self) -> int:
        return self.call_va - self.target_va

    def describe(self) -> str:
        index = "?" if self.index is None else str(self.index)
        base = self.base.describe() if self.base else "?"
        return f"call@0x{self.call_va:08X} -> 0x{self.target_va:08X} index={index} base={base}"


@dataclass(frozen=True)
class BaseRef:
    """Where the hidden ECX base pointer was loaded from."""

    kind: str          # "ebp" | "esp" | "absolute"
    displacement: int

    def describe(self) -> str:
        if self.kind == "absolute":
            return f"[0x{self.displacement & 0xFFFFFFFF:08X}]"
        return f"[{self.kind}{self.displacement:+#x}]"

    def local_name(self) -> Optional[str]:
        """Decompiler-style name for an ebp-relative slot (``local_<offset>`` / ``auStack_<offset>``)."""
        if self.kind != "ebp" or self.displacement >= 0:
            return None
        return f"local_{-self.displacement:04x}"


def _decode_push_imm32(data: bytes, call_offset_in_blob: int, blob_va: int) -> Optional[tuple[int, int]]:
    """Decode ``push imm32`` that must end exactly at the call; returns (value, start_va)."""
    start = call_offset_in_blob - 5
    if start < 0:
        return None
    if data[start] != 0x68:
        return None
    value = struct.unpack_from("<i", data, start + 1)[0]
    return value, blob_va + start


def _decode_mov_ecx(data: bytes, end_offset: int, blob_va: int) -> Optional[tuple[BaseRef, int]]:
    """Decode a ``mov ecx, <mem>`` that ends exactly at ``end_offset``.

    Accepted encodings (MSVC 2012 float-pointer code):
      8B 4D disp8                 mov ecx, [ebp+disp8]
      8B 8D disp32                mov ecx, [ebp+disp32]
      8B 0D disp32                mov ecx, [abs32]
      8B 4C 24 disp8              mov ecx, [esp+disp8]
      8B 8C 24 disp32             mov ecx, [esp+disp32]
    """
    for size, matcher in (
        (3, _match_mov_ecx_3),
        (6, _match_mov_ecx_6),
        (7, _match_mov_ecx_7),
    ):
        start = end_offset - size
        if start < 0:
            continue
        parsed = matcher(data, start)
        if parsed is not None:
            return parsed, blob_va + start
    return None


def _match_mov_ecx_3(data: bytes, start: int):
    if data[start] == 0x8B and data[start + 1] == 0x4D:
        return BaseRef("ebp", struct.unpack_from("<b", data, start + 2)[0])


def _match_mov_ecx_6(data: bytes, start: int):
    if data[start] == 0x8B and data[start + 1] == 0x4C and data[start + 2] == 0x24:
        return BaseRef("esp", struct.unpack_from("<b", data, start + 3)[0])
    if data[start] == 0x8B and data[start + 1] == 0x0D:
        return BaseRef("absolute", struct.unpack_from("<I", data, start + 2)[0])
    if data[start] == 0x8B and data[start + 1] == 0x8D:
        return BaseRef("ebp", struct.unpack_from("<i", data, start + 2)[0])


def _match_mov_ecx_7(data: bytes, start: int):
    if data[start] == 0x8B and data[start + 1] == 0x8C and data[start + 2] == 0x24:
        return BaseRef("esp", struct.unpack_from("<i", data, start + 3)[0])


def scan_calls(image: Image, start_va: int, end_va: int, targets: Iterable[int],
               context_before: int = 16, context_after: int = 2) -> list[CallSite]:
    """Find ``E8 rel32`` calls to any of ``targets`` inside [start_va, end_va)."""
    target_set = {t & 0xFFFFFFFF for t in targets}
    start_offset = image.offset_of(start_va)
    end_offset = image.offset_of(end_va - 1) + 1
    blob = image.data[start_offset:end_offset]
    sites: list[CallSite] = []
    for position in range(0, len(blob) - 4):
        if blob[position] != 0xE8:
            continue
        call_va = start_va + position
        rel = struct.unpack_from("<i", blob, position + 1)[0]
        target = (call_va + 5 + rel) & 0xFFFFFFFF
        if target not in target_set:
            continue
        window_start = max(0, position - context_before)
        window = blob[window_start:position + 5 + context_after]
        push = _decode_push_imm32(blob[:position + 5], position + 5, start_va)
        index = push[0] if push else None
        mov_start = push[1] if push else call_va
        mov_end_offset = (mov_start - start_va)
        mov = _decode_mov_ecx(blob, mov_end_offset, start_va)
        base = mov[0] if mov else None
        sites.append(CallSite(call_va, target, index, base, bytes(window)))
    return sites


def format_hex(blob: bytes, group: int = 1) -> str:
    parts = []
    for index in range(0, len(blob), group):
        parts.append(blob[index:index + group].hex())
    return " ".join(parts)
