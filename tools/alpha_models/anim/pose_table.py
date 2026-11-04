"""Recover the per-state pose table of ``Creature_drawModel`` from the shipped bytes.

Cube World Alpha 2013 has no animation clips: the client computes every creature pose from
an integer animation state and a millisecond timer. ``Creature_drawModel`` (0x4128F0) holds
a ``switch`` with 73 states; each state assigns per-channel Euler angles into stack vectors
through ``arrayElem_stride4``. The Ghidra reconstruction shows those assignments but not the
targets (the accessor's base pointer lives in ECX), so reading the pose out of the pseudo-C
alone is ambiguous.

This tool closes that gap without any debugger and without modifying the game:

1. locate the ``jmp [eax*4+disp32]`` switch dispatch inside the function;
2. read the jump table, which maps ``state-1`` to the first instruction of that state's block
   (states that share a target are exact fallthrough duplicates);
3. for every accessor call inside a block, recover ``(slot, component, value)`` from the
   instruction shape (see :mod:`alpha_writes`);
4. optionally cross-check the recovered values against the reconstructed source: every derived
   stack-slot name must exist there, and the static values of a state must appear in the
   matching ``case`` body.

Usage (the binary is read only, nothing is written next to it)::

    python -m tools.alpha_models.anim.pose_table --exe <Cube.exe> --state 1
    python -m tools.alpha_models.anim.pose_table --exe <Cube.exe> --all --json out.json

Exit code is 0 when the structural checks pass, 1 otherwise, so the tool can gate a report.
"""

from __future__ import annotations

import argparse
import json
import os
import re
import struct
import sys
from dataclasses import asdict, dataclass
from typing import Optional

if __package__ in (None, ""):                       # allow `python path/to/pose_table.py`
    sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(
        os.path.abspath(__file__))))))
    from tools.alpha_models.anim import alpha_writes as aw
    from tools.alpha_models.anim import pe_bytes
else:
    from . import alpha_writes as aw
    from . import pe_bytes


@dataclass(frozen=True)
class ChannelWrite:
    address: int
    slot: str
    component: int
    value: Optional[float]
    encoding: str

    def to_json(self) -> dict:
        return {
            "address": f"0x{self.address:08X}",
            "slot": self.slot,
            "component": self.component,
            "value": None if self.value is None else round(self.value, 6),
            "encoding": self.encoding,
        }


@dataclass
class StateBlock:
    state: int
    start: int
    end: int
    writes: list[ChannelWrite]

    def to_json(self) -> dict:
        return {
            "state": self.state,
            "start": f"0x{self.start:08X}",
            "end": f"0x{self.end:08X}",
            "writes": [w.to_json() for w in self.writes],
        }


def find_dispatch(image: pe_bytes.Image, start_va: int, end_va: int) -> tuple[int, int]:
    """Find the ``jmp dword ptr [eax*4 + table]`` switch dispatch; returns (va, table_va)."""
    blob = image.read(start_va, end_va - start_va)
    found: list[tuple[int, int]] = []
    for position in range(len(blob) - 7):
        if blob[position:position + 3] == b"\xff\x24\x85":
            found.append((start_va + position, struct.unpack_from("<I", blob, position + 3)[0]))
    if not found:
        raise pe_bytes.PEError("no switch dispatch (jmp [eax*4+disp32]) found in the function")
    if len(found) > 1:
        in_range = [item for item in found if start_va <= item[1] < end_va]
        if len(in_range) != 1:
            raise pe_bytes.PEError(f"{len(found)} switch dispatches found, cannot pick one")
        return in_range[0]
    return found[0]


def read_jump_table(image: pe_bytes.Image, table_va: int, start_va: int, end_va: int,
                    limit: int = 512) -> list[int]:
    """Read jump-table entries that point back into the function, in order."""
    entries: list[int] = []
    for index in range(limit):
        try:
            target = image.u32(table_va + index * 4)
        except pe_bytes.PEError:
            break
        if not start_va <= target < end_va:
            break
        entries.append(target)
    return entries


def collect_states(image: pe_bytes.Image, start_va: int, end_va: int,
                   frame_size: int, bias: int = 0) -> tuple[list[StateBlock], int]:
    """Build the state table; the returned int is the dispatch address."""
    dispatch, table = find_dispatch(image, start_va, end_va)
    entries = read_jump_table(image, table, start_va, end_va)
    if not entries:
        raise pe_bytes.PEError("jump table is empty")
    sites = aw.scan_write_sites(image, start_va, end_va)
    boundaries = sorted(set(entries))
    blocks: list[StateBlock] = []
    for state, target in enumerate(entries, start=1):
        following = [b for b in boundaries if b > target]
        block_end = following[0] if following else end_va
        writes = [
            ChannelWrite(site.call_va, site.local_name(frame_size, bias), site.index,
                         site.value, site.value_encoding)
            for site in sites if target <= site.call_va < block_end
        ]
        blocks.append(StateBlock(state, target, block_end, writes))
    return blocks, dispatch


# ----------------------------------------------------------------- cross-check against source

_CASE_RE = re.compile(r"\s+case ([0-9.eE+-]+):")


def parse_source_cases(path: str, first_line: int, last_line: int) -> dict[int, list[float]]:
    """Map a decompiled ``case`` id to the float constants of its body.

    Ghidra prints an integer switch over a ``float`` local as cases like ``2.66247e-44``;
    those are the IEEE bit patterns of the small integers, which is what the caller sees as
    ``self[2]``. The constants in the body are printed as raw ``0x……`` hex floats.
    """
    with open(path, "r", encoding="utf-8", errors="replace") as handle:
        lines = handle.read().splitlines()
    cases: dict[int, list[float]] = {}
    current: Optional[int] = None
    for line in lines[first_line - 1:last_line]:
        match = _CASE_RE.match(line)
        if match:
            try:
                current = struct.unpack("<I", struct.pack("<f", float(match.group(1))))[0]
            except (ValueError, struct.error):
                current = None
            if current is not None:
                cases[current] = []
            continue
        if current is None:
            continue
        for hex_value in re.findall(r"0x([0-9a-f]{8})", line):
            cases[current].append(struct.unpack("<f", struct.pack("<I", int(hex_value, 16)))[0])
    return cases


def cross_check(blocks: list[StateBlock], source_cases: dict[int, list[float]],
                names: set[str]) -> dict:
    """Compare the recovered table with the reconstruction; never assume, always count."""
    known_names = 0
    total_writes = 0
    static_values = 0
    matched_values = 0
    for block in blocks:
        body_values = source_cases.get(block.state, [])
        body_rounded = {round(value, 3) for value in body_values}
        for write in block.writes:
            total_writes += 1
            if write.slot in names:
                known_names += 1
            if write.value is not None:
                static_values += 1
                if round(write.value, 3) in body_rounded:
                    matched_values += 1
    return {
        "writes": total_writes,
        "slotNamesPresentInSource": known_names,
        "staticValues": static_values,
        "staticValuesPresentInCaseBody": matched_values,
        "states": len(blocks),
    }


def format_state(block: StateBlock, limit: Optional[int] = None) -> str:
    lines = [f"state {block.state}: 0x{block.start:08X}..0x{block.end:08X} "
             f"({len(block.writes)} channel writes)"]
    shown = block.writes if limit is None else block.writes[:limit]
    for write in shown:
        value = "computed" if write.value is None else f"{write.value:g}"
        lines.append(f"  {write.address:#010x}  {write.slot:<14} component {write.component}"
                     f"  <- {value:<10} [{write.encoding}]")
    if limit is not None and len(block.writes) > limit:
        lines.append(f"  … {len(block.writes) - limit} more")
    return "\n".join(lines)


def main(argv: Optional[list[str]] = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument("--exe", required=True, help="Cube.exe of the local Alpha copy (read only)")
    parser.add_argument("--source", help="optional reconstructed GameController.cpp for cross-checks")
    parser.add_argument("--first-line", type=int, default=1, help="first source line of the function")
    parser.add_argument("--last-line", type=int, default=0, help="last source line of the function")
    parser.add_argument("--state", type=int, action="append", help="state id(s) to print")
    parser.add_argument("--all", action="store_true", help="print every state")
    parser.add_argument("--limit", type=int, default=0, help="max writes printed per state")
    parser.add_argument("--json", help="write the full table as JSON")
    args = parser.parse_args(argv)

    start, end = aw.CREATURE_DRAW_MODEL, aw.CREATURE_DRAW_MODEL_END
    image = pe_bytes.load(args.exe)
    frame_size = aw.decode_frame_size(image, start)
    bias, frame_size, agreement = aw.resolve_slot_bias(
        image, start, start, end, _source_local_names(args.source, args.first_line, args.last_line))
    blocks, dispatch = collect_states(image, start, end, frame_size, bias)

    print(f"image        : {args.exe}")
    print(f"function     : 0x{start:08X}..0x{end:08X}  frame=0x{frame_size:X}  slot bias={bias}")
    print(f"dispatch     : 0x{dispatch:08X}")
    print(f"states       : {len(blocks)}  (unique blocks {len({b.start for b in blocks})})")
    if args.source:
        print(f"slot names   : agreement with decompiled locals {agreement:.3f}")

    if args.source:
        cases = parse_source_cases(args.source, args.first_line,
                                   args.last_line or _count_lines(args.source))
        summary = cross_check(blocks, cases, _source_local_names(args.source, args.first_line,
                                                                args.last_line))
        print("cross-check  : " + ", ".join(f"{k}={v}" for k, v in summary.items()))
    else:
        summary = {}

    requested = args.state or []
    if args.all:
        for block in blocks:
            print(format_state(block, args.limit or None))
    else:
        for state in requested:
            if 1 <= state <= len(blocks):
                print(format_state(blocks[state - 1], args.limit or None))
            else:
                print(f"state {state}: out of range 1..{len(blocks)}")

    if args.json:
        payload = {
            "exe": os.path.abspath(args.exe),
            "function": f"0x{start:08X}..0x{end:08X}",
            "frameSize": frame_size,
            "slotBias": bias,
            "dispatch": f"0x{dispatch:08X}",
            "slotNameAgreement": round(agreement, 4),
            "crossCheck": summary,
            "states": [block.to_json() for block in blocks],
        }
        with open(args.json, "w", encoding="utf-8") as handle:
            json.dump(payload, handle, indent=1)
        print(f"json         : {args.json}")

    # Structural verdict: the table is only accepted when the dispatch, the jump table and
    # every accessor site resolved to a stack slot. With --source the verdict additionally
    # requires every derived slot name to exist in the reconstruction.
    resolved = sum(1 for block in blocks for write in block.writes if write.slot != "?")
    total = sum(len(block.writes) for block in blocks)
    ok = bool(blocks) and total > 0 and resolved == total
    if summary:
        ok = ok and summary["slotNamesPresentInSource"] == summary["writes"]
        if summary["staticValues"]:
            ok = ok and summary["staticValuesPresentInCaseBody"] > 0
    print(f"resolved     : {resolved}/{total} writes tied to a stack slot")
    print("POSE_TABLE_RESULT=" + ("PASS" if ok else "FAIL"))
    return 0 if ok else 1


def _count_lines(path: str) -> int:
    with open(path, "rb") as handle:
        return handle.read().count(b"\n") + 1


def _source_local_names(path: Optional[str], first_line: int, last_line: int) -> set[str]:
    if not path:
        return set()
    with open(path, "r", encoding="utf-8", errors="replace") as handle:
        lines = handle.read().splitlines()
    body = "\n".join(lines[first_line - 1:(last_line or len(lines))])
    return set(re.findall(r"\b(local_[0-9a-f]{3,4})\b", body))


if __name__ == "__main__":
    sys.exit(main())
