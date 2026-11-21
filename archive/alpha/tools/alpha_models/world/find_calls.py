"""List E8 rel32 call sites of a target inside a VA range of Cube.exe and dump the bytes before each.

Read-only research helper for M0-02 (terrain base units, U1/U2). Usage:
  python -I find_calls.py <Cube.exe> <target_va> <range_start_va> <range_end_va> [context_bytes]
"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[3]))
from tools.alpha_models.anim.pe_bytes import load  # noqa: E402


def main():
    image = load(sys.argv[1])
    target = int(sys.argv[2], 16)
    start = int(sys.argv[3], 16)
    end = int(sys.argv[4], 16)
    context = int(sys.argv[5]) if len(sys.argv) > 5 else 48
    blob = image.read(start, end - start)
    for i in range(len(blob) - 5):
        if blob[i] != 0xE8:
            continue
        rel = int.from_bytes(blob[i + 1:i + 5], "little", signed=True)
        va = start + i
        if (va + 5 + rel) & 0xFFFFFFFF == target:
            before = image.read(va - context, context)
            after = image.read(va + 5, 24)
            print(f"call @0x{va:08X}")
            print("  before:", before.hex(" "))
            print("  after: ", after.hex(" "))


if __name__ == "__main__":
    main()
