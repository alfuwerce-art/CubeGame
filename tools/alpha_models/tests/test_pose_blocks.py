"""Tests for the state-block model and accessor classification (S05A2 review fix).

The reviewed bug was a block-boundary heuristic that let the last jump-table entry run to the end
of the function. These tests pin the corrected rules with synthetic images, so a regression fails
here without needing the Alpha binary (which is not in the repository).
"""

from __future__ import annotations

import struct
import unittest

from tools.alpha_models.anim import alpha_writes as aw
from tools.alpha_models.anim import pe_bytes, pose_table as pt
from tools.alpha_models.tests.test_alpha_write_scan import TEXT_VA, build_pe32


def jump_to(source_va: int, target_va: int) -> bytes:
    return b"\xe9" + struct.pack("<i", (target_va - (source_va + 5)) & 0xFFFFFFFF if target_va > source_va
                                  else (target_va - (source_va + 5)))


class IntervalTests(unittest.TestCase):
    def test_bodies_are_clamped_by_the_switch_exit(self):
        entries = [0x1000, 0x1100, 0x2000]
        intervals = pt.build_case_intervals(entries, join=0x1800, end_va=0x3000, jumps=[])
        self.assertEqual(intervals[0x1000], (0x1000, 0x1100))
        # the second body must stop at the exit, not run to end_va (the reviewed bug)
        self.assertEqual(intervals[0x1100], (0x1100, 0x1800))
        # an entry at/after the exit owns no body of its own
        self.assertEqual(intervals[0x2000], (0x2000, 0x2000))

    def test_regression_old_model_would_swallow_the_tail(self):
        entries = [0x1000, 0x1100]
        intervals = pt.build_case_intervals(entries, join=0x1800, end_va=0x9000, jumps=[])
        self.assertEqual(intervals[0x1100][1], 0x1800)
        self.assertNotEqual(intervals[0x1100][1], 0x9000)

    def test_inner_jumps_do_not_truncate_a_body(self):
        # a case body starts with a conditional whose else-branch jumps to a shared label further
        # away; cutting there dropped 117 of 292 real sites, so the model must keep the body
        entries = [0x1000, 0x1400]
        jumps = [(0x1010, 0x2000)]
        intervals = pt.build_case_intervals(entries, join=0x1800, end_va=0x3000, jumps=jumps)
        self.assertEqual(intervals[0x1000], (0x1000, 0x1400))

    def test_join_point_is_the_cluster_start_of_the_common_target(self):
        # three breaks to TEXT+0x800 and one to TEXT+0x7FC: the exit is the cluster start
        code = bytearray(b"\xcc" * 0x900)
        for offset, target_offset in ((0x10, 0x800), (0x20, 0x800), (0x30, 0x800), (0x40, 0x7FC)):
            source = TEXT_VA + offset
            code[offset:offset + 5] = jump_to(source, TEXT_VA + target_offset)
        image = pe_bytes.load_from_bytes(build_pe32(bytes(code)))
        join = pt.find_join_point(image, TEXT_VA, TEXT_VA + len(code))
        self.assertEqual(join, TEXT_VA + 0x7FC)


class AccessClassificationTests(unittest.TestCase):
    def _site(self, store: bytes) -> aw.WriteSite:
        code = (b"\x6a\x00" + b"\x8d\x8c\x24" + struct.pack("<i", 0x10))
        call_va = TEXT_VA + len(code)
        rel = (aw.ARRAY_ELEM_STRIDE4 - (call_va + 5)) & 0xFFFFFFFF
        blob = code + b"\xe8" + struct.pack("<I", rel) + store + b"\xcc" * 8
        image = pe_bytes.load_from_bytes(build_pe32(blob))
        sites = aw.scan_call_sites(image, TEXT_VA, TEXT_VA + len(blob))
        self.assertEqual(len(sites), 1)
        return sites[0]

    def test_store_marks_a_write(self):
        self.assertEqual(self._site(b"\xc7\x00" + struct.pack("<f", -100.0)).access, "write")

    def test_load_marks_a_read(self):
        self.assertEqual(self._site(b"\xf3\x0f\x10\x00").access, "read")

    def test_store_hoisted_behind_the_next_argument_setup_is_still_a_write(self):
        # push/lea of the next call sits between the call and its store
        site = self._site(b"\x6a\x02\x8d\x8c\x24" + struct.pack("<i", 0x40)
                          + b"\xc7\x00" + struct.pack("<f", 45.0))
        self.assertEqual(site.access, "write")

    def test_pointer_passed_on_is_unknown_not_a_write(self):
        self.assertEqual(self._site(b"\x8b\xc8").access, "unknown")

    def test_reads_and_writes_sum_to_the_site_count(self):
        sites = [self._site(b"\xf3\x0f\x10\x00"), self._site(b"\xc7\x00" + struct.pack("<f", 1.0))]
        self.assertEqual(sum(1 for s in sites if s.access in ("read", "write")), 2)


if __name__ == "__main__":
    unittest.main()
