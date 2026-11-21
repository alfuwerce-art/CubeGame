"""D86 M0-01: stage the pipeline's Unity data into the local runtime content cache.

The pipeline writes ``<project>/Assets/AlphaTransfer/Data`` (rig.json, models/*.cubdec,
clips/*.json, contract.json, locomotion.json, actions.json). The Unity runtime expects

    <content root>/actors/<variant>/{rig.json,models,clips,contract.json,locomotion.json,actions.json}

This script copies one actor per project directory under that name. Only numbers/geometry from
the local Alpha copy are staged; nothing is written into Git (the cache lives in .scratch).

    python -m tools.alpha_content.stage_local_content \
        --projects .scratch/content/projects --out .scratch/content
"""

from __future__ import annotations

import argparse
import json
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SKIP = {"paramwalk_golden.json", "paramwalk_reference.json", "weapon_reference.json"}


def stage(project: Path, out: Path) -> str:
    data = project / "Assets/AlphaTransfer/Data"
    rig = json.loads((data / "rig.json").read_text(encoding="utf-8"))
    variant = rig.get("variant") or project.name
    target = out / "actors" / variant
    if target.exists():
        shutil.rmtree(target)
    target.mkdir(parents=True)
    for src in data.iterdir():
        if src.is_dir():
            shutil.copytree(src, target / src.name)
        elif src.name not in SKIP:
            shutil.copy2(src, target / src.name)
    return f"{variant} parts={len(rig.get('parts', []))} clips={len(list((target / 'clips').glob('*.json')))}"


def main(argv=None) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--projects", default=str(ROOT / ".scratch/content/projects"))
    ap.add_argument("--out", default=str(ROOT / ".scratch/content"))
    a = ap.parse_args(argv)
    projects = sorted(p for p in Path(a.projects).iterdir() if (p / "Assets/AlphaTransfer/Data/rig.json").exists())
    if not projects:
        print("STAGE_CONTENT_RESULT=FAIL no projects in " + a.projects)
        return 1
    out = Path(a.out)
    lines = [stage(p, out) for p in projects]
    for line in lines:
        print("  " + line)
    print(f"STAGE_CONTENT_RESULT=PASS actors={len(lines)} root={out / 'actors'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
