"""Export the zone grids dumped by region_oracle.py (executed generateRegion) as an EditMode fixture.

Only records that differ from the default (type 0, sub 0, id 0, level 1, class 0) are written.
Usage: python -I export_zone_grid_fixture.py <regions.json> <out.json>
"""
import json
import sys
from pathlib import Path


def main():
    regions = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
    out = []
    for r in regions:
        zones = [{k: z[k] for k in ("lx", "lz", "type", "sub", "id", "level", "cls")} for z in r["zones"]
                 if (z["type"], z["sub"], z["id"], z["level"], z["cls"]) != (0, 0, 0, 1, 0)]
        out.append({"rx": r["rx"], "rz": r["rz"], "zones": zones})
    fixture = {"status": "EMULATED_ALPHA_CODE",
               "source": "region_oracle.py: Cube.exe generateRegion (0x5DA280) executed in x86emu; zone grid region+0x18",
               "master_seed": 666666, "regions": out}
    Path(sys.argv[2]).write_text(json.dumps(fixture, separators=(",", ":")), encoding="utf-8")
    print("regions", len(out), "non-default records", sum(len(r["zones"]) for r in out))


if __name__ == "__main__":
    main()
