"""Byte-level extraction of ``arrayElem_stride4`` write sites from Cube.exe.

The Ghidra reconstruction of the 2013 Alpha client decompiles the tiny accessor
``arrayElem_stride4(int index) { return self + index*4; }`` with an uninitialised ``self``:
the base pointer actually arrives in ECX, so the decompiled bodies show
``*arrayElem_stride4(0) = -100.0f`` without saying *which* stack vector is written.
Every limb-pose assignment in ``Creature_drawModel`` goes through exactly this accessor,
so the missing base makes the decompiled pose code ambiguous.

This module recovers the missing half from the shipped machine code. MSVC 2012 emits a
fixed three-instruction shape for these writes::

    push <index>            ; 6A ib  or  68 id
    lea  ecx, [esp+disp32]  ; 8D 8C 24 id   (or 8D 4D ib for ebp-relative)
    call arrayElem_stride4  ; E8 rel32
    mov  dword ptr [eax], <value>   ; C7 00 id   (or F3 0F 11 00 for a float in xmm0)

The scanner matches that shape exactly, so a recorded site is a real write with a known
element index and a known stack slot, not a guess. ``frame_size`` (from the prologue)
converts the esp-relative slot into the ebp-relative offset that the decompiler prints as
``local_XXXX`` / ``auStack_XXXX``, which is how a write can be tied back to a named
variable in the reconstructed source.
"""

from __future__ import annotations

import struct
from dataclasses import dataclass
from typing import Optional

from . import pe_bytes

#: ``arrayElem_stride4`` in Cube.exe 0x400000-based image (Ghidra label from RENAMES.tsv).
ARRAY_ELEM_STRIDE4 = 0x00428970
#: ``arrayElem_stride8`` — same idiom with an 8-byte stride.
ARRAY_ELEM_STRIDE8 = 0x004120B0
#: ``Creature_drawModel`` (the creature pose + part render function).
CREATURE_DRAW_MODEL = 0x004128F0
#: Function that follows ``Creature_drawModel``; used as the scan boundary.
CREATURE_DRAW_MODEL_END = 0x00423F20


@dataclass(frozen=True)
class WriteSite:
    """One ``*arrayElem_stride4(i) = value`` site recovered from code bytes."""

    call_va: int
    index: int
    slot_kind: str      # "esp" | "ebp" | "absolute"
    slot_disp: int      # displacement as encoded
    value: Optional[float]
    value_va: Optional[int]
    value_encoding: str  # "imm32" | "xmm0" | "none"

    def slot_va_relative(self, frame_size: int, pushed: int = 4, bias: int = 0) -> Optional[int]:
        """ebp-relative offset of the written slot, or None when not stack based.

        At the ``lea`` the callee's argument is already pushed, and the callee cleans it
        with ``ret 4``, so ``esp == ebp - frame_size - pushed - bias`` at that instant.
        The decompiler names a slot ``local_<hex offset>`` for ``[ebp-0x<offset>]``.
        """
        if self.slot_kind == "ebp":
            return self.slot_disp if self.slot_disp < 0 else None
        if self.slot_kind == "esp":
            return -(frame_size + pushed + bias) + self.slot_disp
        return None

    def local_name(self, frame_size: int, bias: int = 0) -> str:
        offset = self.slot_va_relative(frame_size, bias=bias)
        if offset is None:
            return "?"
        return f"local_{-offset:04x}"

    def describe(self, frame_size: int, bias: int = 0) -> str:
        index = self.index
        value = "?" if self.value is None else f"{self.value:g}"
        slot = f"{self.slot_kind}{self.slot_disp:+#x}"
        return (f"0x{self.call_va:08X}  {self.local_name(frame_size, bias):<14} "
                f"slot={slot:<12} index={index}  value={value:<12} ({self.value_encoding})")


def decode_frame_size(image: pe_bytes.Image, function_va: int) -> int:
    """Read the frame allocation from an MSVC 2012 prologue.

    Two shapes occur in this build:

    * ``push ebp / mov ebp,esp / sub esp,imm`` — a plain frame;
    * ``push ebp / mov ebp,esp / and esp,-8 / mov eax,imm32 / call __chkstk / sub esp,eax``
      — a stack-probed frame with 8-byte alignment (this is what ``Creature_drawModel`` uses).

    The second shape breaks an exact ``esp == ebp - frame`` identity, because ``and esp,-8``
    may drop up to 4 more bytes; callers therefore probe both plausible alignments and keep
    the one that reproduces decompiler names (see :func:`resolve_slot_bias`).
    """
    prologue = image.read(function_va, 32)
    if prologue[:3] != b"\x55\x8b\xec":
        raise pe_bytes.PEError(f"function 0x{function_va:08X} does not start with push ebp/mov ebp,esp")
    position = 3
    while position < len(prologue) - 6:
        opcode = prologue[position]
        if opcode == 0x81 and prologue[position + 1] == 0xEC:
            return struct.unpack_from("<I", prologue, position + 2)[0]
        if opcode == 0x83 and prologue[position + 1] == 0xEC:
            return struct.unpack_from("<b", prologue, position + 2)[0]
        if opcode == 0x83 and prologue[position + 1] == 0xE4:      # and esp, -8
            position += 3
            continue
        if opcode == 0xB8 and prologue[position + 5] == 0xE8:      # mov eax, imm32 ; call __chkstk
            return struct.unpack_from("<I", prologue, position + 1)[0]
        position += 1
    raise pe_bytes.PEError(f"function 0x{function_va:08X} has no recognised frame allocation")


def resolve_slot_bias(image: pe_bytes.Image, function_va: int, start_va: int, end_va: int,
                      decompiled_names: set[str]) -> tuple[int, int, float]:
    """Pick the ``esp``-to-``ebp`` bias that reproduces the decompiler's own local names.

    Returns ``(bias, frame_size, agreement)`` where ``agreement`` is the share of *stack*
    slots whose derived ``local_XXXX`` name really occurs in the reconstructed source.
    A bias of 0 means ``esp == ebp - frame``; a bias of 4 means ``and esp,-8`` dropped 4 bytes.
    """
    frame_size = decode_frame_size(image, function_va)
    sites = scan_write_sites(image, start_va, end_va)
    stack_sites = [s for s in sites if s.slot_kind in ("esp", "ebp")]
    if not stack_sites:
        return 0, frame_size, 0.0

    best = (0, frame_size, -1.0)
    for bias in (0, 4):
        hits = 0
        for site in stack_sites:
            if site.slot_kind == "esp":
                offset = -(frame_size + 4) + site.slot_disp - bias
            else:
                offset = site.slot_disp
            if offset >= 0:
                continue
            if f"local_{-offset:04x}" in decompiled_names:
                hits += 1
        agreement = hits / len(stack_sites)
        if agreement > best[2]:
            best = (bias, frame_size, agreement)
    return best


def _find_ecx_load(blob: bytes, call_position: int, window: int = 24
                   ) -> Optional[tuple[int, str, int]]:
    """Find the ECX base-load instruction closest to the call.

    MSVC emits ``push <index>`` / ``lea ecx,[slot]`` in that order, but it is free to
    hoist the *store* that belongs to the previous site in between, so the argument
    setup is not always byte-adjacent to the call. The search therefore walks backwards
    for the nearest ``lea ecx``/``mov ecx`` and then validates the push in front of it.
    """
    for start in range(max(0, call_position - window), call_position):
        end, kind, disp = (start + 7, "esp", None) if blob[start:start + 3] == b"\x8d\x8c\x24" else (0, "", 0)
        if end:
            disp = struct.unpack_from("<i", blob, start + 3)[0]
        elif blob[start:start + 2] == b"\x8d\x4d":
            end, kind = start + 3, "ebp"
            disp = struct.unpack_from("<b", blob, start + 2)[0]
        elif blob[start:start + 2] == b"\x8b\x4d":
            end, kind = start + 3, "ebp"
            disp = struct.unpack_from("<b", blob, start + 2)[0]
        elif blob[start:start + 3] == b"\x8b\x8c\x24":
            end, kind = start + 7, "esp"
            disp = struct.unpack_from("<i", blob, start + 3)[0]
        if not end or end > call_position:
            continue
        # The push must sit immediately before the base load.
        if start >= 2 and blob[start - 2] == 0x6A:
            return struct.unpack_from("<b", blob, start - 1)[0], kind, disp
        if start >= 5 and blob[start - 5] == 0x68:
            return struct.unpack_from("<i", blob, start - 4)[0], kind, disp
        return None
    return None


def _scan_stores(blob: bytes, start: int, end: int) -> tuple[Optional[float], Optional[int], str]:
    """Find the store of the accessor's result between two call instructions.

    Only the two instruction shapes MSVC 2012 actually emits around these calls are
    accepted; anything else is reported as ``none`` instead of being guessed:

    * the store directly follows the call
      (``mov dword [eax],imm32`` / ``movss [eax],xmm0``);
    * the store is scheduled after the *next* site's ``push`` + ``lea ecx`` argument setup,
      which is 9 bytes when the index is a ``push imm8``.

    Byte-by-byte scanning is deliberately not used: past the first instruction the read
    position is no longer instruction-aligned, and a stray 0xE9 byte would be mistaken for
    a jump.
    """
    for offset in (0, 9):
        position = start + offset
        if position + 6 > end:
            continue
        if blob[position] == 0xC7 and blob[position + 1] == 0x00:
            return struct.unpack_from("<f", blob, position + 2)[0], position + 2, "imm32"
        if blob[position:position + 4] == b"\xf3\x0f\x11\x00":
            return None, position, "xmm0"
    return None, None, "none"


def scan_write_sites(image: pe_bytes.Image, start_va: int, end_va: int,
                     accessors: tuple[int, ...] = (ARRAY_ELEM_STRIDE4, ARRAY_ELEM_STRIDE8),
                     ) -> list[WriteSite]:
    """Collect every accessor call site and the value stored into the returned pointer.

    The value of site *N* lives in the gap between call *N* and call *N+1*: the compiler
    stores through ``eax`` (site *N*'s result) and may schedule that store after the
    argument setup of the next call, so gaps — not fixed offsets — are what is scanned.
    """
    start_offset = image.offset_of(start_va)
    end_offset = image.offset_of(end_va - 1) + 1
    blob = image.data[start_offset:end_offset]
    targets = {target & 0xFFFFFFFF for target in accessors}
    calls: list[tuple[int, int]] = []
    for position in range(0, len(blob) - 4):
        if blob[position] != 0xE8:
            continue
        call_va = start_va + position
        rel = struct.unpack_from("<i", blob, position + 1)[0]
        if (call_va + 5 + rel) & 0xFFFFFFFF in targets:
            calls.append((position, call_va))

    sites: list[WriteSite] = []
    for order, (position, call_va) in enumerate(calls):
        shape = _find_ecx_load(blob, position)
        if shape is None:
            continue
        index, slot_kind, slot_disp = shape
        gap_end = calls[order + 1][0] if order + 1 < len(calls) else min(len(blob), position + 40)
        value, value_offset, encoding = _scan_stores(blob, position + 5, gap_end)
        value_va = start_va + value_offset if value_offset is not None else None
        sites.append(WriteSite(call_va, index, slot_kind, slot_disp, value, value_va, encoding))
    return sites


def to_pe(path: str) -> pe_bytes.Image:
    return pe_bytes.load(path)
