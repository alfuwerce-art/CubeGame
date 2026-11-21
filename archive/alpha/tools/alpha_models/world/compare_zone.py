"""Compare zone_port.ZonePort with zone_oracle.py output (voxel columns of an executed generateBiomeContent)."""
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[3]))
from tools.alpha_models.world.color_port import ColorPort  # noqa: E402
from tools.alpha_models.world.compare_landmarks import column_lookup  # noqa: E402
from tools.alpha_models.world.compare_port import seeds_for  # noqa: E402
from tools.alpha_models.world.landmark_port import LandmarkPort  # noqa: E402
from tools.alpha_models.world.terrain_oracle import Crt  # noqa: E402
from tools.alpha_models.world.region_port import RegionPort, zone_grid  # noqa: E402
from tools.alpha_models.world.terrain_oracle import START_VOXEL  # noqa: E402
from tools.alpha_models.world.zone_port import ZoneFeatures, ZonePort  # noqa: E402


def unpack(v):
    return [v & 0xFF, (v >> 8) & 0xFF, (v >> 16) & 0xFF, (v >> 24) & 0xFF]


def main():
    data = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
    limit = int(sys.argv[2]) if len(sys.argv) > 2 else None
    master = data["master_seed"]
    zx, zz = data["zone"]
    seeds = seeds_for(master)
    cells = {(c["rx"], c["rz"]): c for c in data["cells"]}
    land = LandmarkPort(seeds, cells, column_lookup(data["regions"]))
    crt = Crt()
    crt.srand((seeds[0x800188] + zz * 0x10000 + zx) & 0xFFFFFFFF)
    port = ZonePort(land, ColorPort(seeds, land), crt.rand)
    full = limit is None and data["columns_processed"] >= 65536
    processed = None if full else (data["columns_processed"] - 1 if limit is None else limit)
    fields = port.generate(zx, zz, processed)
    if full:
        # passes after the column loop need the zone grid record and the region orientation (generateRegion port)
        rx, rz = zx >> 6, zz >> 6
        region = RegionPort(LandmarkPort(seeds, cells, lambda x, z: None), seeds[0x800188]).generate(rx, rz)
        rec = zone_grid(region)[(zx & 63, zz & 63)]
        features = ZoneFeatures(port, fields, zx, zz, rec, (float(START_VOXEL), float(START_VOXEL)), region["orientation"])
        if "carves_skipped" in data:
            features.record_carves = []
        features.run()
        if features.record_carves is not None:
            same = features.record_carves == data["carves_skipped"]
            print("carve calls: alpha", len(data["carves_skipped"]), "port", len(features.record_carves), "identical" if same else "DIFFERENT")
            if not same:
                print("  alpha", data["carves_skipped"][:4])
                print("  port ", features.record_carves[:4])
        print("zone record", rec, "orientation", region["orientation"], "candidates", features.candidates[:8])
    oracle = {(c["lx"], c["lz"]): c for c in data["columns"]}
    bad = shown = 0
    for (x, z), f in fields.items():
        a = oracle[(x - zx * 256, z - zz * 256)]
        av = [unpack(v) for v in a["vox"]]
        diffs = []
        if a["bottom"] != f.bottom:
            diffs.append(f"bottom {a['bottom']}!={f.bottom}")
        if len(av) != len(f.voxels):
            diffs.append(f"count {len(av)}!={len(f.voxels)}")
        else:
            for i, (u, v) in enumerate(zip(av, f.voxels)):
                if u[3] != v[3] or max(abs(u[k] - v[k]) for k in range(3)) > 1:
                    diffs.append(f"y{a['bottom'] + i}: {u}!={v}")
                    break
        if abs(a["t"] - f.t) > 1e-5 or abs(a["h"] - f.h) > 1e-5:
            diffs.append("climate")
        if diffs:
            bad += 1
            if shown < 12:
                shown += 1
                print(f"({x - zx * 256},{z - zz * 256}): " + "; ".join(diffs[:3]))
    print("columns compared", len(fields), "mismatching", bad, "cave/landmark spawns", len(port.spawns))


if __name__ == "__main__":
    main()
