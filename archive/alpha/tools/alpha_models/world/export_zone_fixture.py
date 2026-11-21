"""Export a compact EditMode fixture from a zone_oracle.py dump (voxel columns of executed Alpha code).

Usage: python -I export_zone_fixture.py <dump.json> <out.json> [max_columns] [stride]
Only columns the oracle actually processed are exported (columns_processed − 1, Alpha order x outer, z inner).
"""
import json
import sys
from pathlib import Path


def main():
    data = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
    limit = int(sys.argv[3]) if len(sys.argv) > 3 else 600
    stride = int(sys.argv[4]) if len(sys.argv) > 4 else 1
    full = data["columns_processed"] >= 65536
    processed = 65536 if full else data["columns_processed"] - 1
    by_key = {(c["lx"], c["lz"]): c for c in data["columns"]}
    out = []
    picked = set()

    def take(lx, lz):
        if (lx, lz) in picked or len(out) >= limit:
            return
        picked.add((lx, lz))
        c = by_key[(lx, lz)]
        out.append({"lx": lx, "lz": lz, "t": c["t"], "h": c["h"], "bottom": c["bottom"], "vox": c["vox"]})

    if full:
        # complete zone: every column written by a feature pass (mesa 5, road 11, river water flags exactly 2,
        # river/lake bed 3) first, then a regular sample of the rest
        def featured(c):
            return any((v >> 24) & 0xFF in (2, 3, 5, 11) for v in c["vox"])
        featured_keys = [divmod(n, 256) for n in range(processed) if featured(by_key[divmod(n, 256)])]
        step = max(1, len(featured_keys) // max(1, limit * 2 // 3))
        for key in featured_keys[::step]:
            take(*key)
        for n in range(0, processed, stride):
            take(*divmod(n, 256))
    else:
        for n in range(0, min(processed, len(by_key)), stride):
            take(*divmod(n, 256))
    fixture = {
        "status": data.get("status", "EMULATED_ALPHA_CODE"),
        "source": "zone_oracle.py: Cube.exe generateBiomeContent executed in x86emu up to the first content call (0x5E84C3)",
        "complete_zone": full,
        "master_seed": data["master_seed"],
        "zone": data["zone"],
        "columns": out,
        "carves_skipped": data.get("carves_skipped", []),
    }
    Path(sys.argv[2]).write_text(json.dumps(fixture, separators=(",", ":")), encoding="utf-8")
    print("exported", len(out), "columns")


if __name__ == "__main__":
    main()
