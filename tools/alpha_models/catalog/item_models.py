"""Item -> model index of Alpha 2013, by EXECUTING ``GameController_item_icon_id`` (VA 0x4EC400).

Finding (D64): the function the decompilation calls ``item_icon_id`` is Alpha's item -> MODEL
selector: thiscall on the shared model vector (``self+4`` = begin, the same ``std::vector<Model*>``
``GameController_load_game_assets`` fills and ``Creature_drawModel`` indexes with the appearance
slots), argument = item struct (0x118 bytes). ``Creature_drawModel`` calls it for the seven
equipment slots (creature+0x418 .. +0xaa8, stride 0x118) and uses the result INSTEAD of the
appearance model of the same draw block (gloves -> hands, boots -> feet, chest -> body), or draws
it in the equipment-only blocks (weapon, off-hand, shoulder, neck). ``GameController_renderDroppedItem``
uses the same vector for items lying in the world.

Method: the function is executed in ``drawmodel_emu.EmuCPU`` with a synthetic vector whose element
``i`` holds the marker ``MODEL_BASE + i``, so the returned pointer IS the registry index (0 = no
model). The item domain is explored depth-first and ONLY over bytes the code actually read under the
current assignment (read-driven branching): a byte that is never read cannot change the result,
a byte that is read is swept over its whole declared domain. Nothing is transcribed by hand.

Item byte meaning is taken from the call ``GameController_item_name_string_id(item[1], item+4,
item[0xc], item[0xd])`` (VA 0x51BE60): +0 type, +1 subtype, +4 u32 seed (used only as ``seed % n``),
+0xc clamped to 0..4 (rarity-like), +0xd material. Names are hints; the sweep is by offset.

Domains (declared, reported in the output): type 0..255, subtype 0..63, material 0..63,
+0xc 0..7, seed 0..SEED_PERIOD-1 where SEED_PERIOD = 660 = lcm(11, 6, 5, 4) — the moduli used by
the selector (checked: any other modulus would show as a period mismatch, ``seed_period_check``).

python -m tools.alpha_models.catalog.item_models --out .scratch/catalog/item_models.json
"""
from __future__ import annotations

import argparse
import json
import struct
import sys
from concurrent.futures import ProcessPoolExecutor
from pathlib import Path

from tools.alpha_models.anim import pe_bytes as pb
from tools.alpha_models.universal import alpha_static as st
from tools.alpha_models.universal import drawmodel_emu as de

ROOT = Path(__file__).resolve().parents[3]
DEFAULT_EXE = ROOT / ".scratch/alphatest2/Cube.exe"

ITEM_ICON_ID_VA = 0x4EC400
ITEM_SIZE = 0x118
VEC, ARR, ITEM = 0x30000000, 0x31000000, 0x32000000
N_SLOTS = 4096
MODEL_BASE = 0x07000000
SEED_PERIOD = 660
FIELDS = (("type", 0, range(256)), ("subtype", 1, range(64)), ("material", 0xD, range(64)),
          ("b0c", 0xC, range(8)))
FIELD_OF = {off: name for name, off, _ in FIELDS}
SEED_OFFSETS = (4, 5, 6, 7)

# drawModel equipment slots (creature offset -> drawModel self index -> block) — GameController.cpp
# 2111-2123 (decompilation of Creature_drawModel prologue) and creature_rig.BLOCKS
EQUIPMENT_SLOTS = {
    0x418: {"slot": 1, "self_index": 0x3A, "block": "neck_item", "replaces": None},
    0x530: {"slot": 2, "self_index": 0x31, "block": "body", "replaces": "body"},
    0x648: {"slot": 3, "self_index": 0x36, "block": "foot_l/foot_r", "replaces": "foot"},
    0x760: {"slot": 4, "self_index": 0x33, "block": "hand_l/hand_r", "replaces": "hand"},
    0x878: {"slot": 5, "self_index": 0x39, "block": "shoulder_item", "replaces": None},
    0x990: {"slot": 6, "self_index": 0x38, "block": "offhand_a/offhand_b", "replaces": None},
    0xAA8: {"slot": 7, "self_index": 0x37, "block": "weapon", "replaces": None},
}

_ARR = b"".join(struct.pack("<I", MODEL_BASE + i) for i in range(N_SLOTS))


class Selector:
    def __init__(self, image):
        self.image = image
        self.runs = 0

    def __call__(self, item):
        """(model index | None, sorted item offsets read, error)."""
        self.runs += 1
        cpu = de.EmuCPU(self.image, max_steps=50_000)
        m = cpu.mem
        m.map(de.STACK_BASE, de.STACK_SIZE)
        v = m.map(VEC, 0x20)
        struct.pack_into("<III", v, 4, ARR, ARR + 4 * N_SLOTS, ARR + 4 * N_SLOTS)
        m.map(ARR, 4 * N_SLOTS, _ARR)
        m.map(ITEM, ITEM_SIZE, bytes(item))
        reads = set()
        orig = m.read

        def rd(va, n):
            if ITEM <= va < ITEM + ITEM_SIZE:
                reads.update(range(va - ITEM, min(va - ITEM + n, ITEM_SIZE)))
            return orig(va, n)
        m.read = rd
        trace = []
        try:
            cpu.r["esp"] = de.STACK_BASE + de.STACK_SIZE - 0x100
            cpu.push(ITEM)
            cpu.push(de.RETURN_VA)
            cpu.r["ecx"] = VEC
            cpu.eip = ITEM_ICON_ID_VA
            while cpu.eip != de.RETURN_VA:
                trace.append(cpu.eip)
                cpu.step()
            r = cpu.r["eax"]
        except Exception as e:  # noqa: BLE001 — reported, never guessed
            self.trace = None
            return None, sorted(reads), f"{type(e).__name__}: {str(e)[:100]}"
        self.trace = hash(tuple(trace))
        if r == 0:
            return None, sorted(reads), None
        if not MODEL_BASE <= r < MODEL_BASE + N_SLOTS:
            return None, sorted(reads), f"RESULT_NOT_A_MODEL 0x{r:08X}"
        return r - MODEL_BASE, sorted(reads), None


def _item(assign, seed=0):
    b = bytearray(ITEM_SIZE)
    for off, val in assign.items():
        b[off] = val
    struct.pack_into("<I", b, 4, seed)
    return b


def explore(image, prefix):
    """All leaves under a fixed field prefix ({offset: value}).

    Reduction rule (path classes): for the next field the code READ, every value of its domain is
    executed; values that run the SAME instruction trace AND return the same model form one class
    and only its first value is explored further (the class keeps all its values). Values whose
    result differs inside a trace class (the field is used arithmetically, e.g. ``+0xc`` clamp or
    ``seed % n``) are explored one by one. This never assumes a field is irrelevant: it is executed
    for every value of the declared domain at its level of the tree."""
    sel = Selector(image)
    leaves, unknown, errors = [], set(), []
    order = [off for _, off, _ in FIELDS]
    domain = {off: list(dom) for _, off, dom in FIELDS}

    def run(assign, seed):
        idx, reads, err = sel(_item(assign, seed))
        return idx, reads, err, sel.trace

    def visit(assign, classes):
        idx, reads, err, _ = run(assign, 0)
        if err:
            errors.append({"assign": {FIELD_OF[o]: v for o, v in assign.items()}, "error": err})
            return
        unknown.update(o for o in reads if o not in FIELD_OF and o not in SEED_OFFSETS)
        pending = [o for o in order if o in reads and o not in assign]
        if pending:
            o = pending[0]
            groups = {}
            for val in domain[o]:
                i2, _, e2, tr = run({**assign, o: val}, 0)
                if e2:
                    errors.append({"assign": {FIELD_OF[k]: v for k, v in {**assign, o: val}.items()}, "error": e2})
                    continue
                groups.setdefault(tr, []).append((val, i2))
            for members in groups.values():
                results = {i2 for _, i2 in members}
                if len(results) == 1:
                    visit({**assign, o: members[0][0]}, {**classes, o: [v for v, _ in members]})
                else:
                    for val, _ in members:
                        visit({**assign, o: val}, {**classes, o: [val]})
            return
        rec = {FIELD_OF[o]: _ranges(classes.get(o, [v])) for o, v in sorted(assign.items())}
        if any(o in reads for o in SEED_OFFSETS):
            per_seed = []
            for s in range(SEED_PERIOD):
                i2, r2, e2, _ = run(assign, s)
                if e2:
                    errors.append({"assign": rec, "seed": s, "error": e2})
                    return
                per_seed.append(i2)
            chk = [run(assign, SEED_PERIOD + s)[0] for s in range(0, SEED_PERIOD, 7)]
            if chk != per_seed[::7]:
                errors.append({"assign": rec, "error": "SEED_PERIOD_MISMATCH"})
                return
            period = next(p for p in range(1, SEED_PERIOD + 1)
                          if SEED_PERIOD % p == 0 and all(per_seed[i] == per_seed[i % p] for i in range(SEED_PERIOD)))
            rec.update({"seed_period": period, "models": per_seed[:period]})
        else:
            rec["models"] = [idx]
        leaves.append(rec)

    visit(dict(prefix), {})
    return {"prefix": {FIELD_OF[o]: v for o, v in prefix.items()}, "leaves": leaves,
            "unknown_reads": sorted(unknown), "errors": errors, "runs": sel.runs}


def explore_type(image, t):
    """Type level: split by subtype when the type reads it (parallel work units)."""
    sel = Selector(image)
    _, reads, _ = sel(_item({0: t}))
    if 1 not in reads:
        r = explore(image, {0: t})
        return {"type": t, **{k: r[k] for k in ("leaves", "unknown_reads", "errors", "runs")}}
    out = {"type": t, "leaves": [], "unknown_reads": set(), "errors": [], "runs": 1}
    for sub in range(64):
        r = explore(image, {0: t, 1: sub})
        out["leaves"] += r["leaves"]
        out["unknown_reads"].update(r["unknown_reads"])
        out["errors"] += r["errors"]
        out["runs"] += r["runs"]
    out["unknown_reads"] = sorted(out["unknown_reads"])
    return out


def _worker(args):
    exe, prefixes = args
    image = pb.load(exe)
    return [explore(image, {int(k): v for k, v in p.items()}) for p in prefixes]


def sweep(exe=DEFAULT_EXE, workers=8):
    """Work units: one per type, split by subtype where the type reads the subtype byte."""
    st.verify_exe(exe)
    image = pb.load(exe)
    sel = Selector(image)
    prefixes = []
    for t in range(256):
        _, reads, _ = sel(_item({0: t}))
        prefixes += [{0: t, 1: sub} for sub in range(64)] if 1 in reads else [{0: t}]
    chunks = [(str(exe), prefixes[i::workers]) for i in range(workers)]
    per_type = {}
    with ProcessPoolExecutor(workers) as ex:
        for res in ex.map(_worker, chunks):
            for r in res:
                t = r["prefix"]["type"]
                e = per_type.setdefault(t, {"type": t, "leaves": [], "unknown_reads": set(), "errors": [], "runs": 0})
                e["leaves"] += r["leaves"]
                e["unknown_reads"].update(r["unknown_reads"])
                e["errors"] += r["errors"]
                e["runs"] += r["runs"]
    out = []
    for t in sorted(per_type):
        e = per_type[t]
        e["unknown_reads"] = sorted(e["unknown_reads"])
        e["leaves"].sort(key=lambda l: json.dumps(l, sort_keys=True))
        out.append(e)
    return out


def summarize(per_type, registry):
    """Compact table: only types that select at least one model; index -> list of item keys."""
    types, by_model = {}, {}
    for r in per_type:
        models = sorted({m for leaf in r["leaves"] for m in leaf["models"] if m is not None})
        if not models and not r["errors"]:
            continue
        # group leaves with the same model list (drop redundant material/b0c rows)
        groups = {}
        for leaf in r["leaves"]:
            key = json.dumps({"models": leaf["models"], "seed_period": leaf.get("seed_period")})
            groups.setdefault(key, []).append({k: v for k, v in leaf.items() if k not in ("models", "seed_period")})
        types[str(r["type"])] = {
            "models": models,
            "selectors": [{**json.loads(k), "when": v[:32], "when_rows": len(v)} for k, v in groups.items()
                          if any(m is not None for m in json.loads(k)["models"])],
            "unknown_reads": r["unknown_reads"], "errors": r["errors"][:5], "error_count": len(r["errors"]),
            "runs": r["runs"]}
        for leaf in r["leaves"]:
            for m in leaf["models"]:
                if m is not None:
                    by_model.setdefault(m, set()).add(json.dumps({k: leaf[k] for k in ("subtype", "material", "b0c")
                                                                  if k in leaf} | {"type": r["type"]}, sort_keys=True))
    return {"format": "cubegame.alpha_item_models/1", "selector_va": f"0x{ITEM_ICON_ID_VA:06X}",
            "domains": {n: [min(d), max(d)] for n, _, d in FIELDS} | {"seed": [0, SEED_PERIOD - 1]},
            "equipment_slots": {f"0x{k:x}": v for k, v in EQUIPMENT_SLOTS.items()},
            "types": types,
            "models": {str(m): {"name": registry.get(m), "items": [json.loads(x) for x in sorted(v)][:16],
                                "item_variants": len(v)}
                       for m, v in sorted(by_model.items())},
            "metrics": {"item_types_with_models": len(types), "distinct_models": len(by_model),
                        "runs": sum(r["runs"] for r in per_type),
                        "errors": sum(len(r["errors"]) for r in per_type)}}


def _ranges(v):
    if v and v[0] is not None and v == list(range(v[0], v[-1] + 1)) and len(v) > 2:
        return f"{v[0]}..{v[-1]}"
    return v


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--exe", default=str(DEFAULT_EXE))
    ap.add_argument("--out", required=True)
    ap.add_argument("--workers", type=int, default=8)
    ap.add_argument("--rerun", action="store_true", help="ignore the cached raw sweep")
    a = ap.parse_args(argv)
    out = Path(a.out).resolve()
    if not out.is_relative_to(ROOT / ".scratch"):
        raise SystemExit("ALPHA_OUTPUT_MUST_BE_IN_SCRATCH")
    raw = out.with_suffix(".raw.json")
    if raw.exists() and not a.rerun:
        per_type = json.loads(raw.read_text(encoding="utf-8"))
    else:
        per_type = sweep(a.exe, a.workers)
        raw.parent.mkdir(parents=True, exist_ok=True)
        raw.write_text(json.dumps(per_type), encoding="utf-8")
    registry, _ = st.model_registry(pb.load(a.exe))
    res = summarize(per_type, registry)
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(res, indent=1), encoding="utf-8")
    print(json.dumps({"ITEM_MODELS": "DONE", **res["metrics"]}))
    return 0


if __name__ == "__main__":
    sys.exit(main())
