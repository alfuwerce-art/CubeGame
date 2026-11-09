"""Execute Alpha's spawn-table builders (M0-06B / N-18, N-19) from Cube.exe bytes.

Two client functions fill a spawner's descriptor list; both are executed here with the M0-02 oracle
(`tools/alpha_models/world/terrain_oracle.py`), so the numbers are Alpha's own machine code:

  0x5C9B30 World_generateMonsterSpawnList(this = controller, spawner, arg)
  0x5CA0A0 World_generateNpcSpawnList   (same calling convention)

The whole function runs natively (builders, std::vector growth, rand() from the CRT emulation); only one
observation hook is installed: `GameController_addItemToInventory` (0x46EBE0) is executed normally, and its
0x118-byte record argument is captured. Records are classified by their type byte:

  type 2  creature spawn descriptor: +1 kind, +4 weight (rand()%100), +0xC stars, +0xD level
  type 1, 11, 20, 23, 24  loot/other entries of the same list (kept verbatim, not interpreted)

Status of every number: EMULATED_ALPHA_CODE. Nothing is guessed; an unsupported instruction or an
unstubbed import raises. Usage:

    python -I spawn_oracle.py <Cube.exe> <out.json> <seed> [<seed> ...]
"""
from __future__ import annotations

import json
import struct
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[3]))
from tools.alpha_models.world.terrain_oracle import Oracle  # noqa: E402
from tools.alpha_models.universal.x86emu import EmuError  # noqa: E402

MONSTER_LIST = 0x5C9B30
NPC_LIST = 0x5CA0A0
ADD_TO_LIST = 0x46EBE0
RECORD_BYTES = 0x118
CREATURE_TYPE = 2


def run_list(oracle: Oracle, va: int, records: list) -> None:
    """One list builder on a fresh synthetic spawner; captured records appended to `records`."""
    spawner = oracle.alloc(0x10F0)
    controller = oracle.alloc(0x2000)
    # The list the builder appends to is a std::vector<record> inside the controller: begin == end.
    oracle.cpu.mem.w(controller + 0xF6C, 4, controller + 0x1000)
    oracle.cpu.mem.w(controller + 0xF70, 4, controller + 0x1000)
    captured = []

    def on_append(cpu):
        rec = cpu.mem.u(cpu.r["esp"] + 4, 4)
        captured.append(cpu.mem.read(rec, RECORD_BYTES))

    oracle.cpu.hooks[ADD_TO_LIST] = on_append
    try:
        oracle.call(va, [spawner, 1], ecx=controller)
    finally:
        del oracle.cpu.hooks[ADD_TO_LIST]
    for raw in captured:
        records.append({
            "source": "monster" if va == MONSTER_LIST else "npc",
            "type": raw[0],
            "kind": raw[1],
            "weight": struct.unpack("<I", raw[4:8])[0],
            "stars": raw[0xC],
            "level": raw[0xD],
            "level16": struct.unpack("<H", raw[0x10:0x12])[0],
            "record_sha_prefix": raw[:16].hex(),
        })


def main() -> int:
    if len(sys.argv) < 4:
        print(__doc__)
        return 2
    exe, out = sys.argv[1], sys.argv[2]
    seeds = [int(s) for s in sys.argv[3:]]
    result = {"status": "EMULATED_ALPHA_CODE", "cube": str(exe), "record_bytes": RECORD_BYTES,
              "functions": {"monster": f"0x{MONSTER_LIST:X}", "npc": f"0x{NPC_LIST:X}"}, "seeds": []}
    problems = []
    for seed in seeds:
        oracle = Oracle(exe, seed)
        oracle.cpu.max_steps = 400_000_000
        records = []
        for va in (MONSTER_LIST, NPC_LIST):
            try:
                run_list(oracle, va, records)
            except EmuError as exc:
                problems.append({"seed": seed, "va": f"0x{va:X}", "error": str(exc), "eip": f"0x{oracle.cpu.eip:X}"})
        result["seeds"].append({"seed": seed, "records": records})
        creatures = [r for r in records if r["type"] == CREATURE_TYPE]
        print(f"seed {seed}: records {len(records)} creature-descriptors {len(creatures)} "
              f"levels {sorted({r['level'] for r in creatures})}", flush=True)
    result["problems"] = problems
    result["RESULT"] = "PASS" if not problems else "FAIL"
    Path(out).write_text(json.dumps(result, indent=1), encoding="utf-8")
    print(f"SPAWN_ORACLE_RESULT={result['RESULT']} seeds={len(seeds)} problems={len(problems)}")
    return 0 if result["RESULT"] == "PASS" else 1


if __name__ == "__main__":
    sys.exit(main())
