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
from dataclasses import asdict, dataclass, field
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
    access: str = "unknown"      # "read" | "write" | "unknown" (see alpha_writes.classify_access)

    def to_json(self) -> dict:
        return {
            "address": f"0x{self.address:08X}",
            "slot": self.slot,
            "component": self.component,
            "value": None if self.value is None else round(self.value, 6),
            "encoding": self.encoding,
            "access": self.access,
        }


@dataclass
class StateBlock:
    state: int
    start: int
    end: int
    writes: list[ChannelWrite]
    shared_with: list[int] = field(default_factory=list)

    def to_json(self) -> dict:
        return {
            "state": self.state,
            "start": f"0x{self.start:08X}",
            "end": f"0x{self.end:08X}",
            "sharedWith": self.shared_with,
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
    join = find_join_point(image, start_va, end_va)
    jumps = collect_unconditional_jumps(image, start_va, end_va)
    intervals = build_case_intervals(entries, join, end_va, jumps)
    blocks: list[StateBlock] = []
    for state, target in enumerate(entries, start=1):
        block_start, block_end = intervals[target]
        writes = [
            ChannelWrite(site.call_va, site.local_name(frame_size, bias), site.index,
                         site.value, site.value_encoding, site.access)
            for site in sites if block_start <= site.call_va < block_end
        ]
        blocks.append(StateBlock(state, block_start, block_end, writes, shared_with=[]))
    # states that share one jump-table target share their body: say so instead of counting twice
    by_start: dict[int, list[int]] = {}
    for block in blocks:
        by_start.setdefault(block.start, []).append(block.state)
    for block in blocks:
        others = [s for s in by_start[block.start] if s != block.state]
        block.shared_with = others
    return blocks, dispatch


# ----------------------------------------------------------------- control-flow model

def find_join_point(image: pe_bytes.Image, start_va: int, end_va: int) -> int:
    """Address right after the switch: where the ``break`` statements jump.

    The jump-table entries alone cannot bound a case body: the *last* entry would otherwise run to
    the end of the function and swallow the shared drawing code (that was the reviewed bug). Every
    ``break`` compiles to ``jmp rel32`` to the switch exit, so the most common in-function jump
    target — clustered, because MSVC splits it into a few nearby addresses — is that exit.
    """
    targets: dict[int, int] = {}
    blob = image.read(start_va, end_va - start_va)
    for position in range(len(blob) - 5):
        if blob[position] != 0xE9:
            continue
        va = start_va + position
        target = (va + 5 + struct.unpack_from("<i", blob, position + 1)[0]) & 0xFFFFFFFF
        if start_va <= target < end_va:
            targets[target] = targets.get(target, 0) + 1
    if not targets:
        raise pe_bytes.PEError("no in-function jumps found: cannot locate the switch join point")
    best = max(targets, key=lambda address: targets[address])
    cluster = [address for address in targets if abs(address - best) <= 0x20]
    return min(cluster)


def collect_unconditional_jumps(image: pe_bytes.Image, start_va: int, end_va: int) -> list[tuple[int, int]]:
    """In-function ``jmp rel32`` instructions as ``(va, target)``, sorted by address."""
    blob = image.read(start_va, end_va - start_va)
    jumps: list[tuple[int, int]] = []
    for position in range(len(blob) - 5):
        if blob[position] != 0xE9:
            continue
        va = start_va + position
        target = (va + 5 + struct.unpack_from("<i", blob, position + 1)[0]) & 0xFFFFFFFF
        if start_va <= target < end_va:
            jumps.append((va, target))
    return sorted(jumps)


def build_case_intervals(entries: list[int], join: int, end_va: int,
                         jumps: list[tuple[int, int]]) -> dict[int, tuple[int, int]]:
    """Map every jump-table entry to the half-open interval that is really its own body.

    The reviewed version bounded a case by the next *greater* jump-table address and let the last
    one run to the end of the function, which swallowed the shared drawing code. Two things are
    corrected here, and both are validated against the reconstruction:

    * every body is clamped by the switch exit (``join``): code after the last ``break`` is shared
      by all states and is not evidence for any single one of them;
    * an entry at or beyond the exit (five fallthrough cases share ``0x41E660``) owns no body,
      because its "body" is exactly that shared tail.

    Deliberately *not* cut at inner jumps: a case body contains its own ``if/else`` jumps and
    gotos to labels that the compiler placed elsewhere, so cutting at the first outward jump
    truncated real bodies (measured: it dropped 117 of 292 stack-decoded sites). With the plain
    clamp the per-state accessor counts reproduce the decompiled case bodies exactly:
    397 counted statements in 67 case bodies, 397 sites, and the remaining 126 statements Ghidra
    prints for case 72 are the shared tail.
    """
    unique = sorted(set(entries))
    intervals: dict[int, tuple[int, int]] = {}
    for entry in unique:
        if entry >= join:
            intervals[entry] = (entry, entry)
            continue
        following = [candidate for candidate in unique if candidate > entry]
        intervals[entry] = (entry, min(following[0] if following else join, join))
    return intervals


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


def write_probe_document(blocks: list[StateBlock], args) -> None:
    """Write the static values of one channel as a probe document for the Unity adapter.

    Only values recovered from the bytes are exported, each with the instruction address it was
    read from, so the Unity side can say exactly where a number came from. The document is
    produced locally: it contains original game values and therefore never goes into Git.
    """
    block = next((b for b in blocks if b.state == args.probe_state), None)
    if block is None:
        raise pe_bytes.PEError(f"state {args.probe_state} does not exist (1..{len(blocks)})")
    moments = [
        {"name": f"write_0x{write.address:08X}", "value": round(write.value, 6),
         "address": f"0x{write.address:08X}", "encoding": write.encoding}
        for write in block.writes
        if write.slot == args.probe_slot and write.component == args.probe_component
        and write.value is not None
    ]
    document = {
        "source": os.path.abspath(args.exe),
        "generator": "tools/alpha_models/anim/pose_table.py",
        "function": "Creature_drawModel@004128F0",
        "state": args.probe_state,
        "blockStart": f"0x{block.start:08X}",
        "slot": args.probe_slot,
        "component": args.probe_component,
        "moments": moments,
    }
    with open(args.probe_out, "w", encoding="utf-8") as handle:
        json.dump(document, handle, indent=1)
    print(f"probe        : {args.probe_out} ({len(moments)} static moments for "
          f"{args.probe_slot}[{args.probe_component}] in state {args.probe_state})")


def counter_lines(sites: list, blocks: list[StateBlock]) -> str:
    """Denominators that the reviewed report mixed up.

    ``total``          every accessor call in the function (decoded or not);
    ``decoded``        calls whose hidden ECX base is a stack slot (only these can be named);
    ``uniqueSites``    decoded sites, counted once;
    ``assignments``    decoded sites inside case bodies, counted once per state that owns the body;
    ``sharedBodies``   case bodies reached from more than one jump-table slot (fallthrough cases).
    """
    decoded = [site for site in sites if site.slot_kind in ("esp", "ebp")]
    in_bodies = [block for block in blocks if block.end > block.start]
    shared = [block for block in blocks if block.shared_with]
    assignments = sum(len(block.writes) for block in blocks)
    by_access = {"read": 0, "write": 0, "unknown": 0}
    for block in blocks:
        for write in block.writes:
            by_access[write.access] = by_access.get(write.access, 0) + 1
    unique_in_bodies = len({write.address for block in blocks for write in block.writes})
    return "\n".join([
        f"accessor calls: {len(sites)}  decoded to a stack slot: {len(decoded)}  "
        f"not attributable (base in a register): {len(sites) - len(decoded)}",
        f"case bodies  : {len(in_bodies)}  states sharing a body: {len(shared)}  "
        f"states without own body: {len(blocks) - len(in_bodies)}",
        f"unique sites inside case bodies: {unique_in_bodies}  state assignments: {assignments}",
        f"in-state access kinds: read={by_access.get('read', 0)} write={by_access.get('write', 0)} "
        f"unknown={by_access.get('unknown', 0)}",
    ])


def format_state(block: StateBlock, limit: Optional[int] = None) -> str:
    shared = f" shared with {block.shared_with}" if block.shared_with else ""
    lines = [f"state {block.state}: 0x{block.start:08X}..0x{block.end:08X} "
             f"({len(block.writes)} accessor sites){shared}"]
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
    parser.add_argument("--probe-out",
                        help="write a Unity probe document with the static values of one channel")
    parser.add_argument("--probe-state", type=int, default=1, help="state used for --probe-out")
    parser.add_argument("--probe-slot", default="local_15fc", help="channel slot used for --probe-out")
    parser.add_argument("--probe-component", type=int, default=0,
                        help="channel component (0=x, 1=y, 2=z) used for --probe-out")
    args = parser.parse_args(argv)

    start, end = aw.CREATURE_DRAW_MODEL, aw.CREATURE_DRAW_MODEL_END
    image = pe_bytes.load(args.exe)
    frame_size = aw.decode_frame_size(image, start)
    bias, frame_size, agreement = aw.resolve_slot_bias(
        image, start, start, end, _source_local_names(args.source, args.first_line, args.last_line))
    blocks, dispatch = collect_states(image, start, end, frame_size, bias)
    all_sites = aw.scan_call_sites(image, start, end)
    join = find_join_point(image, start, end)

    print(f"image        : {args.exe}")
    print(f"function     : 0x{start:08X}..0x{end:08X}  frame=0x{frame_size:X}  slot bias={bias}")
    print(f"dispatch     : 0x{dispatch:08X}   switch join: 0x{join:08X}")
    print(f"states       : {len(blocks)}  (unique case bodies {len({b.start for b in blocks})})")
    if args.source:
        print(f"slot names   : agreement with decompiled locals {agreement:.3f}")
    print(counter_lines(all_sites, blocks))

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
    if args.probe_out:
        write_probe_document(blocks, args)
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
