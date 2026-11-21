"""Compare landmark_port.LandmarkPort (base, mountain) with landmark_oracle.py samples, grouped by landmark type."""
import json
import sys
from collections import defaultdict
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[3]))
from tools.alpha_models.world.compare_port import seeds_for  # noqa: E402
from tools.alpha_models.world.landmark_port import LandmarkPort  # noqa: E402


def column_lookup(regions):
    table = {}
    for r in regions:
        for c in r["columns"]:
            cx, cz = c["i"] // 8, c["i"] % 8
            table[(r["rx"] * 8 + cx, r["rz"] * 8 + cz)] = {
                "x": c["x"], "z": c["z"], "radius": c["f16"], "base": c["f20"], "type": c["type"]}

    def lookup(x, z):
        return table.get((x >> 11, z >> 11))
    return lookup


def main():
    data = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
    cells = {(c["rx"], c["rz"]): c for c in data["cells"]}
    port = LandmarkPort(seeds_for(data["master_seed"]), cells, column_lookup(data["regions"]))
    worst = defaultdict(lambda: [0.0, 0.0, 0])
    shown = 0
    for s in data["samples"]:
        b = port.base(s["x"], s["z"])
        m = port.mountain(s["x"], s["z"])
        w = worst[s["type"]]
        db, dm = abs(b - s["base"]), abs(m - s["mountain"])
        w[0], w[1], w[2] = max(w[0], db), max(w[1], dm), w[2] + 1
        if (db > 0.01 or dm > 1e-4) and shown < 12:
            shown += 1
            print(f"type {s['type']} x={s['x']} z={s['z']} base alpha={s['base']:.4f} port={b:.4f} mountain alpha={s['mountain']:.6f} port={m:.6f}")
    for t in sorted(worst):
        print(f"type {t:2d}: samples {worst[t][2]:3d} worst |dbase|={worst[t][0]:.2e} |dmountain|={worst[t][1]:.2e}")


if __name__ == "__main__":
    main()
