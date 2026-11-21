"""Unreal Engine 5.8 Editor Python, read-only asset list probe.

RESEARCH_ONLY: must run in an isolated, explicitly authorized UE project.
Not a CubeGame Unity asset importer. Not tested on the user's Windows.
"""
import json

try:
    import unreal
except ImportError as exc:
    raise SystemExit("Requires Unreal Editor Python environment; plain Python is unsupported") from exc


def main():
    # No asset load/save/mutation. This is an Editor-only API.
    asset_paths = unreal.EditorAssetLibrary.list_assets(
        "/Game", recursive=True, include_folder=False
    )
    summary = {
        "status": "RESEARCH_READ_ONLY",
        "root": "/Game",
        "count": len(asset_paths),
        "first_paths": sorted(str(p) for p in asset_paths)[:25],
        "changed_assets": 0,
    }
    unreal.log("CUBEGAME_UNREAL_PROBE " + json.dumps(summary, ensure_ascii=False))
    return summary


if __name__ == "__main__":
    main()
