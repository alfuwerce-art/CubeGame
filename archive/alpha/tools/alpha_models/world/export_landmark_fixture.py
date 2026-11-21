"""Pack region + landmark oracle outputs into the compact C# test fixture (numbers only, no Alpha assets)."""
import json
import sys
from pathlib import Path


def columns(region):
    out = []
    for c in region["columns"]:
        w = c["words"]
        out.append({"i": c["i"], "type": c["type"], "sub": w[0], "id": w[1], "level": w[2], "cls": w[3],
                    "x": c["x"], "z": c["z"], "radius": c["f16"], "base": c["f20"]})
    return out


def main():
    scratch = Path(sys.argv[1])
    out = Path(sys.argv[2])
    regions = []
    for name in ("regions_center.json", "regions_far.json", "regions_water.json"):
        for r in json.loads((scratch / name).read_text(encoding="utf-8")):
            regions.append({"rx": r["rx"], "rz": r["rz"], "level": r["level"], "variant": r["variant"],
                            "orientation": r["orientation"], "columns": columns(r)})
    land = json.loads((scratch / "landmarks_512.json").read_text(encoding="utf-8"))
    fixture = {
        "status": "EMULATED_ALPHA_CODE",
        "source": "generateRegion 0x5DA280 (region_oracle.py), base 0x5C5E20 / mountain 0x5EFFA0 with landmark "
                  "columns present (landmark_oracle.py); regions generated in isolation",
        "master_seed": land["master_seed"],
        "regions": regions,
        "samples": [{"x": s["x"], "z": s["z"], "type": s["type"], "base": s["base"], "mountain": s["mountain"]}
                    for s in land["samples"]],
    }
    out.write_text(json.dumps(fixture, separators=(",", ":")), encoding="utf-8")
    print("regions", len(regions), "samples", len(fixture["samples"]), "bytes", out.stat().st_size)


if __name__ == "__main__":
    main()
