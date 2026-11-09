"""Compare region_port.RegionPort with region_oracle.py output (generateRegion executed)."""
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[3]))
from tools.alpha_models.world.compare_port import seeds_for  # noqa: E402
from tools.alpha_models.world.region_port import RegionPort  # noqa: E402
from tools.alpha_models.world.terrain_oracle import Oracle  # noqa: E402
from tools.alpha_models.world.landmark_port import LandmarkPort  # noqa: E402


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
        mine = port.generate(rx, rz)
        for key in ("level", "variant", "orientation"):
            if mine[key] != r[key]:
                print(f"region ({rx},{rz}) {key}: alpha {r[key]} port {mine[key]}")
        for a, m in zip(r["columns"], mine["columns"]):
            total += 1
            w = a["words"]
            alpha = {"type": a["type"], "sub": w[0], "id": w[1], "level": w[2], "cls": w[3],
                     "x": a["x"], "z": a["z"], "radius": a["f16"], "base": a["f20"]}
            diffs = []
            for k in ("type", "sub", "id", "level", "cls"):
                if alpha[k] != m[k]:
                    diffs.append(f"{k} {alpha[k]}!={m[k]}")
            for k in ("x", "z", "radius"):
                if abs(alpha[k] - m[k]) > 0.01:
                    diffs.append(f"{k} {alpha[k]:.3f}!={m[k]:.3f}")
            if abs(alpha["base"] - m["base"]) > 0.01:
                diffs.append(f"base {alpha['base']:.3f}!={m['base']:.3f}")
            if diffs:
                bad += 1
                if bad <= 25:
                    print(f"({rx},{rz}) col {a['i']} cx{a['i'] // 8} cz{a['i'] % 8}: " + "; ".join(diffs))
    print("columns", total, "mismatching", bad)


if __name__ == "__main__":
    main()
