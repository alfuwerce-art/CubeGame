"""Live census of the creatures the ORIGINAL Alpha client actually renders (S05B-2 / D67-D69).

Why: D67-D69 require the original-model catalogue to come from the running game, with exact
IDs, not from our pipeline. The game's own ``Creature_drawModel`` entry (gate 0x412A42, the
first ``movsx ecx,word [eax+0x8c]``; eax = creature) is hit once per drawn creature per frame,
so a short debug session yields every creature on screen with:

  kind (+0x64, u32), appearance model indices (+0x8c..+0x9a, 8 x int16), action byte (+0x68),
  action time (+0x6c), appearance flags (+0x7e), flags (+0x124), class/spec (+0x140/+0x141),
  hp (+0x16c), level (+0x190), world position (+0x10, 3 x int64 fixed point).

Read-only apart from the INT3 byte the existing ``drawmodel_probe.run_probe`` writes and always
restores. Run only against the local research copy in ``.scratch`` (D54/D55/D67).

Output JSON: one record per distinct creature pointer, with the observed action bytes and the
number of draws, plus a per-kind summary. Provenance: ``LIVE_ALPHA_OBSERVED`` (rendered by the
original client in the game world at the time of capture).
"""
from __future__ import annotations

import argparse
import json
import struct
import time

from tools.alpha_models.runtime import drawmodel_probe as dp


def _decode_pos(raw):
    xr, zr, yr = struct.unpack("<3q", raw)
    return [round(xr / 4294967296.0, 3), round(yr / 65536.0, 3), round(zr / 4294967296.0, 3)]


def read_creature(handle, ptr):
    r = lambda off, n: dp._read(handle, ptr + off, n)
    rec = {}
    raw = r(0x64, 4)
    rec["kind"] = None if raw is None else struct.unpack("<I", raw)[0]
    raw = r(0x68, 1)
    rec["action"] = None if raw is None else raw[0]
    raw = r(0x6C, 4)
    rec["action_time"] = None if raw is None else round(struct.unpack("<f", raw)[0], 1)
    raw = r(0x7E, 2)
    rec["flags_7e"] = None if raw is None else struct.unpack("<H", raw)[0]
    raw = r(0x124, 2)
    rec["flags_124"] = None if raw is None else struct.unpack("<H", raw)[0]
    raw = r(0x140, 2)
    rec["class_spec"] = None if raw is None else [raw[0], raw[1]]
    raw = r(0x16C, 4)
    rec["hp"] = None if raw is None else round(struct.unpack("<f", raw)[0], 1)
    raw = r(0x190, 4)
    rec["level"] = None if raw is None else struct.unpack("<i", raw)[0]
    raw = r(0x10, 0x18)
    rec["pos"] = None if raw is None else _decode_pos(raw)
    rec["models_8c_9a"] = dp._i16s(handle, ptr + 0x8C)
    return rec


def summarise(creatures: dict) -> dict:
    kinds = {}
    for ptr, c in creatures.items():
        k = f"0x{c['kind']:02x}" if isinstance(c.get("kind"), int) else "?"
        s = kinds.setdefault(k, {"count": 0, "actions_seen": set(), "model_sets": set()})
        s["count"] += 1
        s["actions_seen"].update(c["actions_seen"])
        s["model_sets"].add(tuple(c.get("models_8c_9a") or ()))
    return {k: {"count": v["count"], "actions_seen": sorted(v["actions_seen"]),
                "model_sets": [list(m) for m in sorted(v["model_sets"])]}
            for k, v in sorted(kinds.items())}


def run(pid, hits, out, label=""):
    import ctypes
    k = dp._kernel32()
    handle = k.OpenProcess(dp.PROCESS_ALL_ACCESS, False, pid)
    creatures = {}
    t0 = time.time()

    def on_hit(entry):
        ptr = int(entry["creature"]["ptr"], 16)
        rec = read_creature(handle, ptr)
        c = creatures.setdefault(f"0x{ptr:08X}", {**rec, "draws": 0, "actions_seen": set()})
        c["draws"] += 1
        if rec.get("action") is not None:
            c["actions_seen"].add(rec["action"])
        c["pos"] = rec.get("pos")

    try:
        dp.run_probe(pid, max_hits=hits, on_hit=on_hit)
    finally:
        k.CloseHandle(handle)
    for c in creatures.values():
        c["actions_seen"] = sorted(c["actions_seen"])
    doc = {"format": "cubegame.alpha_live_census/1", "provenance": "LIVE_ALPHA_OBSERVED",
           "label": label, "pid": pid, "hits": hits, "seconds": round(time.time() - t0, 2),
           "captured_at": time.strftime("%Y-%m-%dT%H:%M:%S"),
           "creatures": creatures, "kinds": summarise(creatures)}
    with open(out, "w", encoding="utf-8") as fh:
        json.dump(doc, fh, indent=1)
    return doc


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("pid", type=int)
    ap.add_argument("out")
    ap.add_argument("--hits", type=int, default=400)
    ap.add_argument("--label", default="")
    a = ap.parse_args()
    doc = run(a.pid, a.hits, a.out, a.label)
    print(f"CENSUS creatures={len(doc['creatures'])} seconds={doc['seconds']}")
    for k, v in doc["kinds"].items():
        print(f"  kind={k} n={v['count']} actions={v['actions_seen']} models={v['model_sets'][:2]}")


if __name__ == "__main__":
    main()
