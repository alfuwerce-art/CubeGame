"""Tests for the byte-level pose extraction (S05A-2).

The Alpha binary itself is not in the repository (it is a third-party game), so these tests
build a tiny synthetic PE32 image containing the exact instruction shapes that MSVC 2012
emits around ``arrayElem_stride4`` calls. That keeps the scanner honest without shipping
any Cube World bytes: every check here is about the decoder's own logic.

Run: ``python -m unittest discover -s tools/alpha_models/tests -t .``
"""

from __future__ import annotations

import struct
import unittest

from tools.alpha_models.anim import alpha_writes, pe_bytes

TEXT_VA = 0x401000


def build_pe32(text: bytes, text_va: int = 0x1000, image_base: int = 0x400000) -> bytes:
    """Return a minimal but well-formed PE32 image with one ``.text`` section."""
    pe_offset = 0x40
    optional_size = 0xE0
    header = bytearray(0x200)
    header[0:2] = b"MZ"
    struct.pack_into("<I", header, 0x3C, pe_offset)
    header[pe_offset:pe_offset + 4] = b"PE\0\0"
    struct.pack_into("<HH", header, pe_offset + 4, 0x014C, 1)      # i386, 1 section
    struct.pack_into("<H", header, pe_offset + 20, optional_size)
    optional = pe_offset + 24
    struct.pack_into("<H", header, optional, 0x010B)               # PE32
    struct.pack_into("<I", header, optional + 28, image_base)
    table = optional + optional_size
    header[table:table + 8] = b".text\0\0\0"
    struct.pack_into("<IIII", header, table + 8, len(text), text_va, len(text), 0x200)
    return bytes(header) + text


class CodeBuilder:
    """Emits the MSVC 2012 argument-setup shape for accessor calls, tracking addresses."""

    def __init__(self, start_va: int = TEXT_VA):
        self.start_va = start_va
        self.chunks: list[bytes] = []

    @property
    def va(self) -> int:
        return self.start_va + sum(len(chunk) for chunk in self.chunks)

    def _push_lea(self, index: int, slot: int) -> bytes:
        return (b"\x6a" + struct.pack("<b", index) +
                b"\x8d\x8c\x24" + struct.pack("<i", slot))

    def accessor_prefix(self, index: int, slot: int) -> bytes:
        """``push idx`` + ``lea ecx,[esp+slot]`` (the argument setup of one accessor call)."""
        return self._push_lea(index, slot)

    def call_bytes(self, call_va: int,
                   target: int = alpha_writes.ARRAY_ELEM_STRIDE4) -> bytes:
        rel = (target - (call_va + 5)) & 0xFFFFFFFF
        return b"\xe8" + struct.pack("<I", rel)

    def store_imm(self, value: float) -> bytes:
        return b"\xc7\x00" + struct.pack("<f", value)

    def store_xmm0(self) -> bytes:
        return b"\xf3\x0f\x11\x00"

    def add(self, blob: bytes) -> "CodeBuilder":
        self.chunks.append(blob)
        return self

    def accessor_write(self, index: int, slot: int, store: bytes) -> "CodeBuilder":
        """Emit ``push/lea/call/store`` and record it, starting at the current address."""
        prefix = self._push_lea(index, slot)
        self.add(prefix)
        self.add(self.call_bytes(self.va))
        self.add(store)
        return self

    def bytes(self) -> bytes:
        return b"".join(self.chunks)


class FrameSizeTests(unittest.TestCase):
    def test_plain_sub_esp_frame(self):
        text = b"\x55\x8b\xec\x81\xec" + struct.pack("<I", 0x200) + b"\xcc" * 32
        image = pe_bytes.load_from_bytes(build_pe32(text))
        self.assertEqual(alpha_writes.decode_frame_size(image, TEXT_VA), 0x200)

    def test_stack_probe_frame_with_alignment(self):
        # push ebp / mov ebp,esp / and esp,-8 / mov eax,0x1730 / call __chkstk
        text = (b"\x55\x8b\xec\x83\xe4\xf8\xb8" + struct.pack("<I", 0x1730) +
                b"\xe8\0\0\0\0" + b"\xcc" * 24)
        image = pe_bytes.load_from_bytes(build_pe32(text))
        self.assertEqual(alpha_writes.decode_frame_size(image, TEXT_VA), 0x1730)

    def test_rejects_non_standard_prologue(self):
        image = pe_bytes.load_from_bytes(build_pe32(b"\x90" * 32))
        with self.assertRaises(pe_bytes.PEError):
            alpha_writes.decode_frame_size(image, TEXT_VA)


class WriteSiteTests(unittest.TestCase):
    def _scan(self, code: CodeBuilder):
        text = code.bytes() + b"\xc2\x04\x00" + b"\xcc" * 8
        image = pe_bytes.load_from_bytes(build_pe32(text))
        return alpha_writes.scan_write_sites(image, TEXT_VA, TEXT_VA + len(text))

    def test_sites_carry_index_slot_and_value(self):
        code = (CodeBuilder()
                .accessor_write(0, 0x10, b"\xc7\x00" + struct.pack("<f", 20.0))
                .accessor_write(2, 0x10, b"\xc7\x00" + struct.pack("<f", -60.0)))
        sites = self._scan(code)
        self.assertEqual(len(sites), 2)
        self.assertEqual((sites[0].index, sites[0].slot_kind, sites[0].slot_disp),
                         (0, "esp", 0x10))
        self.assertAlmostEqual(sites[0].value, 20.0, places=5)
        self.assertEqual(sites[0].value_encoding, "imm32")
        self.assertEqual((sites[1].index, sites[1].slot_disp), (2, 0x10))
        self.assertAlmostEqual(sites[1].value, -60.0, places=5)

    def test_slot_resolves_to_decompiler_local_name(self):
        # esp == ebp - frame - 4 at the lea, so [esp+0x10] is [ebp-0xf4] with frame 0x100.
        site = alpha_writes.WriteSite(TEXT_VA, 0, "esp", 0x10, 1.0, TEXT_VA, "imm32")
        self.assertEqual(site.slot_va_relative(0x100), -0xF4)
        self.assertEqual(site.local_name(0x100), "local_00f4")

    def test_xmm0_store_is_reported_as_computed(self):
        sites = self._scan(CodeBuilder().accessor_write(1, 0x40, b"\xf3\x0f\x11\x00"))
        self.assertEqual(len(sites), 1)
        self.assertIsNone(sites[0].value)
        self.assertEqual(sites[0].value_encoding, "xmm0")

    def test_store_scheduled_after_next_argument_setup(self):
        # Site 1's store is hoisted in front of site 2's call instruction: push/lea of
        # site 2 sits between site 1's call and its store.
        code = CodeBuilder()
        code.add(code.accessor_prefix(0, 0x10))
        code.add(code.call_bytes(code.va))
        code.add(code.accessor_prefix(1, 0x20))
        code.add(code.store_imm(90.0))
        code.add(code.call_bytes(code.va))
        sites = self._scan(code)
        self.assertEqual(len(sites), 2)
        self.assertAlmostEqual(sites[0].value, 90.0, places=5)

    def test_call_to_other_target_is_ignored(self):
        code = CodeBuilder()
        code.add(code.accessor_prefix(0, 0x10))
        code.add(code.call_bytes(code.va, target=0x405000))
        code.add(code.store_imm(1.0))
        self.assertEqual(self._scan(code), [])

    def test_ebp_relative_base(self):
        code = CodeBuilder()
        code.add(b"\x6a\x02" + b"\x8d\x4d\xd8")            # push 2 ; lea ecx,[ebp-0x28]
        call_va = code.va
        code.add(code.call_bytes(call_va))
        code.add(code.store_imm(-30.0))
        sites = self._scan(code)
        self.assertEqual(len(sites), 1)
        self.assertEqual((sites[0].index, sites[0].slot_kind, sites[0].slot_disp),
                         (2, "ebp", -0x28))
        self.assertEqual(sites[0].local_name(0x100), "local_0028")


class ImageTests(unittest.TestCase):
    def test_rejects_non_pe(self):
        with self.assertRaises(pe_bytes.PEError):
            pe_bytes.load_from_bytes(b"not an executable")

    def test_virtual_address_outside_sections(self):
        image = pe_bytes.load_from_bytes(build_pe32(b"\xcc" * 16))
        with self.assertRaises(pe_bytes.PEError):
            image.read(0x900000, 4)

    def test_read_is_relative_to_image_base(self):
        image = pe_bytes.load_from_bytes(build_pe32(b"\x11\x22\x33\x44"))
        self.assertEqual(image.read(TEXT_VA, 2), b"\x11\x22")


if __name__ == "__main__":
    unittest.main()
