"""Compare terrain_port.Port with the emulated Alpha dataset (base 0x5C5E20, mountain 0x5EFFA0)."""
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[3]))
from tools.alpha_models.world.terrain_oracle import Crt  # noqa: E402
from tools.alpha_models.world.terrain_port import Port  # noqa: E402


def seeds_for(master):
    crt = Crt()
    crt.srand(master)
    s = {0x800188: crt.rand() % 100000}
    for off, n in ((0x800168, 4), (0x80018C, 64), (0x800178, 4), (0x80028C, 4)):
        for i in range(n):
            s[off + 4 * i] = crt.rand() % 100000
    return s


def main():
    data = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
    seeds = seeds_for(data["master_seed"])
    assert seeds[0x800188] == data["world_seed"], "seed table mismatch"
    cells = {(c["rx"], c["rz"]): c for c in data["cells"]}
    port = Port(seeds, cells)
    worst = {"base": 0.0, "mountain": 0.0}
    bad = []
    for s in data["samples"]:
        b = port.base(s["x"], s["z"])
        m = port.mountain(s["x"], s["z"])
        db, dm = abs(b - s["base"]), abs(m - s["mountain"])
        worst["base"] = max(worst["base"], db)
        worst["mountain"] = max(worst["mountain"], dm)
        if db > 0.01 or dm > 1e-4:
            bad.append((s["x"], s["z"], s["base"], b, s["mountain"], m))
    print("samples", len(data["samples"]), "worst", worst, "bad", len(bad))
    for row in bad[:8]:
        print("  x=%d z=%d base alpha=%.5f port=%.5f  mountain alpha=%.6f port=%.6f" % row)


if __name__ == "__main__":
    main()
