"""Extract the model-load table from the decompiled client: vector index -> .cub name.

Evidence: cube/control/GameController.cpp (Ghidra headless decompilation of Cube.exe 2013).
The asset loader prints, for every model, the pattern

    uVar5 = string_ctorAppend(local_XXXX,local_2c,"NAME.cub");
    local_8 = 0x<MODEL_ID>;
    vector_at_stride4(0x<VECTOR_INDEX>);
    VoxelModel_load_from_file_or_buffer(uVar5,puVar2,1);

so (VECTOR_INDEX, MODEL_ID, NAME) are recoverable textually. `GameController_item_icon_id`
returns `vectorU32_at(<VECTOR_INDEX>)`, therefore index -> name is exactly what is needed to
answer "which model does item type/subtype X actually use".

No binary is executed; this is a text re-read plus deterministic arithmetic.
"""
import json
import re
import sys
from pathlib import Path

# Defaults resolve inside THIS checkout (were hard-coded to the DeepSeek worktree; Claude prebatch P5).
_ROOT = Path(__file__).resolve().parents[2]
SRC = Path(sys.argv[1]) if len(sys.argv) > 1 else (
    _ROOT / ".scratch" / "CubeWorld-Reversal" / "cube" / "control" / "GameController.cpp")
OUT = Path(sys.argv[2]) if len(sys.argv) > 2 else (
    _ROOT / ".scratch" / "research" / "model_load_table.json")

lines = SRC.read_text(encoding="utf-8", errors="replace").splitlines()

RE_NAME = re.compile(r'string_ctorAppend\([^,]+,[^,]+,"([^"]+\.cub)"\)')
RE_MODELID = re.compile(r"local_8 = (0x[0-9a-fA-F]+|\d+);\s*$")
RE_VECIDX = re.compile(r"vector_at_stride4\((0x[0-9a-fA-F]+|\d+)\)")
RE_LOAD = re.compile(r"VoxelModel_load_from_file_or_buffer")

records = []
i = 0
n = len(lines)
while i < n:
    m = RE_NAME.search(lines[i])
    if not m:
        i += 1
        continue
    name = m.group(1)
    model_id = vec_idx = None
    for k in range(i + 1, min(n, i + 6)):
        ln = lines[k]
        if model_id is None:
            mm = RE_MODELID.search(ln)
            if mm:
                model_id = int(mm.group(1), 16) if mm.group(1).lower().startswith("0x") else int(mm.group(1))
        mv = RE_VECIDX.search(ln)
        if mv and vec_idx is None:
            vec_idx = int(mv.group(1), 16) if mv.group(1).lower().startswith("0x") else int(mv.group(1))
        if RE_LOAD.search(ln):
            if vec_idx is not None:
                records.append({"vector_index": vec_idx, "model_id": model_id,
                                "name": name, "line": i + 1})
            break
    i += 1

by_index = {}
for r in records:
    by_index.setdefault(r["vector_index"], r)

print(f"loader_records={len(records)} unique_index={len(by_index)}")
if records:
    ids = [r["vector_index"] for r in records if r["vector_index"] is not None]
    print(f"index range: 0x{min(ids):x}..0x{max(ids):x}")
    deltas = sorted({(r["model_id"] - r["vector_index"]) for r in records
                     if r["model_id"] is not None and r["vector_index"] is not None})
    print(f"model_id - vector_index distinct deltas: {[hex(d) for d in deltas[:6]]} (count={len(deltas)})")

data = {
    "extractor": "extract_model_load_table.py",
    "source_file": str(SRC),
    "count": len(by_index),
    "index_range": [min(ids), max(ids)] if records else None,
    "table": [{"vector_index": k, "model_id": v["model_id"], "name": v["name"],
               "line": v["line"]} for k, v in sorted(by_index.items())],
}
OUT.parent.mkdir(parents=True, exist_ok=True)
OUT.write_text(json.dumps(data, indent=1, ensure_ascii=False), encoding="utf-8")
print("WROTE", OUT)

tsv = OUT.with_suffix(".tsv")
with tsv.open("w", encoding="utf-8", newline="\n") as f:
    f.write("vector_index\tvector_index_hex\tmodel_id\tmodel_id_hex\tname\tline\n")
    for k, v in sorted(by_index.items()):
        f.write(f"{k}\t0x{k:04x}\t{v['model_id']}\t0x{(v['model_id'] or 0):x}\t{v['name']}\t{v['line']}\n")
print("WROTE", tsv)

# Quick windows around the armour/weapon model blocks used by item_icon_id
for base in (0x464, 0x496, 0x4c8, 0x4fa, 0x52c, 0x55e, 0x590, 0x59a, 0x5b3, 0x5fe, 0x685, 0x6d0):
    hits = [by_index[k] for k in range(base, base + 6) if k in by_index]
    if hits:
        print(f"\nwindow 0x{base:04x}: " + ", ".join(f"0x{h['vector_index']:04x}={h['name']}" for h in hits))
