"""Compare region_port.zone_grid with the zone grid dumped by region_oracle.py (generateRegion executed)."""
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[3]))
from tools.alpha_models.world.compare_port import seeds_for  # noqa: E402
from tools.alpha_models.world.landmark_port import LandmarkPort  # noqa: E402
from tools.alpha_models.world.region_port import RegionPort, zone_grid  # noqa: E402
from tools.alpha_models.world.terrain_oracle import Oracle  # noqa: E402


def main():
    exe, oracle_json = sys.argv[1], sys.argv[2]
    regions = json.loads(Path(oracle_json).read_text(encoding="utf-8"))
    master = int(sys.argv[3]) if len(sys.argv) > 3 else 666666
    o = Oracle(exe, master)
    seeds = seeds_for(master)
    total = bad = 0
    for r in regions:
        rx, rz = r["rx"], r["rz"]
        cells = {(x, z): o.cell(x, z) for x in range(rx - 4, rx + 5) for z in range(rz - 4, rz + 5)}
        port = RegionPort(LandmarkPort(seeds, cells, lambda x, z: None), seeds[0x800188])
        grid = zone_grid(port.generate(rx, rz))
        alpha = {(z["lx"], z["lz"]): z for z in r["zones"]}
        for key, mine in grid.items():
            a = alpha.get(key, dict(type=0, sub=0, id=0, level=0, cls=0, b2=0, b3=0))
            total += 1
            diffs = [f"{k} {a[k]}!={mine[k]}" for k in ("type", "sub", "id", "level", "cls") if a[k] != mine[k]]
            if a.get("b2") or a.get("b3"):
                diffs.append(f"bytes2/3 {a['b2']},{a['b3']}")
            if diffs:
                bad += 1
                if bad <= 20:
                    print(f"region ({rx},{rz}) zone {key}: " + "; ".join(diffs))
    print("zones compared", total, "mismatching", bad)


if __name__ == "__main__":
    main()
