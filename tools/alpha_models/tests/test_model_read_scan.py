"""Tests for tools.alpha_models.runtime.model_read_scan (model-index read scanner)."""
import os
import unittest

from tools.alpha_models.runtime.model_read_scan import (
    MODEL_INDEX_FIELDS,
    scan_exe_image,
    scan_model_index_reads,
)

# movsx ecx, word ptr [eax+0x8c]  -> 0F BF 88 8C 00 00 00
MOVSX_EAX_8C = bytes.fromhex("0fbf888c000000")
# movzx ecx, word ptr [eax+0x8e]  -> 0F B7 88 8E 00 00 00
MOVZX_EAX_8E = bytes.fromhex("0fb7888e000000")
# movsx edx, word ptr [ecx+0x94]  -> 0F BF 91 94 00 00 00
MOVSX_ECX_94 = bytes.fromhex("0fbf9194000000")
# mov eax, [eax+0x98]             -> 8B 80 98 00 00 00
MOV_EAX_98 = bytes.fromhex("8b8098000000")
# movsx ecx, word ptr [esp+0x8c]  -> 0F BF 8C 24 8C 00 00 00 (SIB)
MOVSX_ESP_8C = bytes.fromhex("0fbf8c248c000000")
# movsx ecx, word ptr [eax+0x8d]  -> wrong displacement (0x8d not a model field)
MOVSX_EAX_8D = bytes.fromhex("0fbf888d000000")
# movsx ecx, word ptr [eax+0x5c]  -> disp8 form (0x5c < 0x80), not a model field
MOVSX_EAX_5C_DISP8 = bytes.fromhex("0fbf485c")
# movsx ecx, word ptr [0x8c]      -> absolute [disp32], no base register
MOVSX_ABS_8C = bytes.fromhex("0fbf0d8c000000")


class ScanSynthetic(unittest.TestCase):
    def _read(self, blob, base_va=0x00400000):
        return scan_model_index_reads(blob, base_va)

    def test_movsx_word_is_found(self):
        hits = self._read(MOVSX_EAX_8C)
        self.assertEqual(len(hits), 1)
        h = hits[0]
        self.assertEqual(h.address, 0x00400000)
        self.assertEqual(h.op, "movsx")
        self.assertEqual(h.operand, 2)
        self.assertEqual(h.destination, "ecx")
        self.assertEqual(h.base, "eax")
        self.assertEqual(h.field, 0x8C)

    def test_movzx_variant_is_found(self):
        hits = self._read(MOVZX_EAX_8E)
        self.assertEqual(len(hits), 1)
        self.assertEqual(hits[0].op, "movzx")
        self.assertEqual(hits[0].field, 0x8E)

    def test_different_base_and_dest(self):
        hits = self._read(MOVSX_ECX_94)
        self.assertEqual(len(hits), 1)
        self.assertEqual(hits[0].destination, "edx")
        self.assertEqual(hits[0].base, "ecx")
        self.assertEqual(hits[0].field, 0x94)

    def test_plain_mov_variant_is_found(self):
        hits = self._read(MOV_EAX_98)
        self.assertEqual(len(hits), 1)
        self.assertEqual(hits[0].op, "mov")
        self.assertEqual(hits[0].operand, 4)
        self.assertEqual(hits[0].field, 0x98)

    def test_sib_esp_form_is_found(self):
        hits = self._read(MOVSX_ESP_8C)
        self.assertEqual(len(hits), 1)
        self.assertEqual(hits[0].base, "esp")
        self.assertEqual(hits[0].field, 0x8C)

    def test_wrong_displacement_is_rejected(self):
        self.assertEqual(self._read(MOVSX_EAX_8D), [])

    def test_disp8_form_cannot_reach_model_fields(self):
        # 0x5c as disp8 is a different field and must not be reported as a model-index read
        self.assertEqual(self._read(MOVSX_EAX_5C_DISP8), [])

    def test_absolute_form_is_rejected(self):
        # [0x8c] with no base register is not a creature-pointer read
        self.assertEqual(self._read(MOVSX_ABS_8C), [])

    def test_multiple_instructions_addresses_are_correct(self):
        # each instruction is 7 bytes (0F BF /r + ModRM + disp32)
        blob = MOVSX_EAX_8C + MOVZX_EAX_8E + MOVSX_ECX_94
        hits = self._read(blob, base_va=0x00412A40)
        self.assertEqual([h.address for h in hits], [0x00412A40, 0x00412A47, 0x00412A4E])
        self.assertEqual([h.field for h in hits], [0x8C, 0x8E, 0x94])


class ScanRealImage(unittest.TestCase):
    """Optional integration check against the local Alpha copy (skipped when absent).

    The eight known instruction addresses in the shipped Cube.exe are the load sites of the
    model-index fields (creature+0x8c..+0x9a); the scanner must recover exactly them.
    """

    EXE = r"D:\Projects\CubeGame\.scratch\alphatest2\Cube.exe"
    KNOWN = [
        (0x00412A42, 0x8C), (0x00412A6C, 0x8E), (0x00412A9F, 0x90), (0x00412AD2, 0x92),
        (0x00412B05, 0x94), (0x00412B38, 0x96), (0x00412B6B, 0x9A), (0x00412B9E, 0x98),
    ]

    @unittest.skipUnless(os.path.exists(EXE), "local Alpha copy not present")
    def test_recovers_the_eight_model_index_reads(self):
        from tools.alpha_models.anim.pe_bytes import load
        image = load(self.EXE)
        hits = scan_exe_image(image, 0x004128F0, 0x00423F20)
        # The eight creature-field reads are word loads (movsx ecx,[eax+0xNN]); the function
        # also contains dword stack-local reads (mov eax,[esp+0xNN]) which are not these.
        creature_reads = {(h.address, h.field) for h in hits if h.operand == 2 and h.base != "esp"}
        expected = {(address, field) for address, field in self.KNOWN}
        self.assertEqual(creature_reads, expected)


if __name__ == "__main__":
    unittest.main()
