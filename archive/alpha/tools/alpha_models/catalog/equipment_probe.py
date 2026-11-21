"""D64: how Alpha draws EQUIPMENT on a creature — established by executing Creature_drawModel.

For every equipment slot drawModel reads (creature+0x418 neck, +0x530 chest, +0x648 feet, +0x760
hands, +0x878 shoulder, +0x990 off-hand, +0xaa8 weapon, +0xdf0 lamp, +0xf08 special, +0x1020) an item
is put into the slot of an otherwise unchanged creature, drawModel is executed (``alpha_oracle``) and
the drawn parts are compared with the unequipped frame:

* SWAP    — the same draw site draws the item model instead of the appearance model (chest -> body,
            gloves -> hands, boots -> feet): the rig block is unchanged, only the mesh changes;
* ATTACH  — a NEW draw site appears (weapon, off-hand, shoulder, neck, lamp, glider ...). Its anchor
            rule is then derived with ``anchor_probe`` (the model size is changed in the emulator and
            the matrix response measured): ``d_anchor_d_size`` is the exact linear anchor law and
            ``rotation_changes_with_size`` must be False for a transferable joint frame;
* NONE    — drawModel does not draw this slot for this kind/state (e.g. ring slots).

Status of everything here: EMULATED_ALPHA_CODE (Alpha's own drawModel executed).

python -m tools.alpha_models.catalog.equipment_probe --out .scratch/catalog/equipment_probe.json
"""
from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

from tools.alpha_models.anim import mat4
from tools.alpha_models.universal import alpha_oracle as ao
from tools.alpha_models.universal import anchor_probe as apb
from tools.alpha_models.universal import anim_states as an
from tools.alpha_models.universal import creature_rig as cr
from tools.alpha_models.universal import drawmodel_emu as de

ROOT = Path(__file__).resolve().parents[3]
DEFAULT_EXE = ROOT / ".scratch/alphatest2/Cube.exe"
DEFAULT_SIZES = ROOT / ".scratch/catalog/registry_sizes.json"

# (slot offset, item type, subtype, label); subtypes from the item_models sweep (type 3 sub 13 =
# shield models iron-shield*, sub 6 = bow, sub 0 = sword; type 23 sub 0 = glider)
PROBES = [
    (0x418, 8, 0, "neck: amulet"),
    (0x530, 4, 0, "chest armour"),
    (0x648, 6, 0, "boots"),
    (0x760, 5, 0, "gloves"),
    (0x878, 7, 0, "shoulder armour"),
    (0x990, 3, 13, "off-hand: shield"),
    (0x990, 3, 0, "off-hand: sword"),
    (0xAA8, 3, 0, "weapon: sword (one-handed)"),
    (0xAA8, 3, 15, "weapon: greatsword (two-handed)"),
    (0xAA8, 3, 6, "weapon: bow"),
    (0xAA8, 3, 13, "weapon slot: shield"),
    (0xDF0, 24, 0, "lamp: lantern (flag +0x124 & 0x200)"),
    (0xF08, 23, 0, "special: glider (flag +0x124 & 0x10)"),
    (0xF08, 23, 1, "special: boat (flag +0x124 & 0x10)"),
    (0x1E8, 1, 1, "item in use +0x1e8: potion during action 80"),
    (0x1E8, 11, 1, "item in use +0x1e8: resource during action 81"),
    (0x1020, 19, 0, "slot +0x1020: pet item during action 82"),
    # offhand_b (0x42231B): drawn only when BOTH hands hold the same subtype 4 / 12 / 20 (executed sweep
    # of main x off-hand subtypes, D64) — fist weapons, bracelets, torches
    (0x990, 3, 4, "off-hand: fist with main fist (offhand_b)", {0xAA8: (3, 4)}),
    (0x990, 3, 12, "off-hand: bracelet with main bracelet (offhand_b)", {0xAA8: (3, 12)}),
]
# drawModel draws these slots only under a creature condition (GameController.cpp 5997, 7167, 6409, 6437)
CONDITIONS = {
    (0xF08, None): {0x124: ("<H", 0x10)},
    (0xDF0, None): {0x124: ("<H", 0x200)},
    (0x1E8, 1): {0x68: ("<B", 80), 0x6C: ("<i", 0)},
    (0x1E8, 11): {0x68: ("<B", 81), 0x6C: ("<i", 0)},
    (0x1020, None): {0x68: ("<B", 82), 0x6C: ("<i", 0)},
}


def _frame(oracle, c, frames=(600, 600, 16)):
    em = de.Emulator(oracle.image, bytes(c), oracle.sizes)
    fr = None
    for dt in frames:
        fr = em.frame(dt_ms=dt)
    return fr


def _parts(fr):
    return {p["draw_site"]: p for p in fr["parts"]}


def probe_kind(oracle, kind, seed=(0, 0), with_anchor=True):
    c0, _ = oracle.creature(kind, seed)
    c0[0x5C] |= 1
    base = _parts(_frame(oracle, c0))
    out = []
    for probe in PROBES:
        off, typ, sub, label = probe[:4]
        c = bytearray(c0)
        for o2, (t2, s2) in (probe[4] if len(probe) > 4 else {}).items():
            an.equip(c, o2, t2, s2)
        an.equip(c, off, typ, sub)
        import struct
        cond = CONDITIONS.get((off, typ)) or CONDITIONS.get((off, None)) or {}
        for fo, (fmt, val) in cond.items():
            if fmt == "<H":
                struct.pack_into("<H", c, fo, struct.unpack_from("<H", c, fo)[0] | val)
            else:
                struct.pack_into(fmt, c, fo, val)
        try:
            fr = _frame(oracle, c)
        except Exception as e:  # noqa: BLE001
            out.append({"slot": f"0x{off:x}", "item": [typ, sub], "label": label, "result": "ERROR",
                        "error": f"{type(e).__name__}: {e}"[:160]})
            continue
        now = _parts(fr)
        new = [s for s in now if s not in base]
        swapped = [s for s in now if s in base and now[s].get("model_index") != base[s].get("model_index")]
        gone = [s for s in base if s not in now]
        rec = {"slot": f"0x{off:x}", "item": [typ, sub], "label": label,
               "condition": {f"0x{k:x}": v[1] for k, v in cond.items()},
               "result": "ATTACH" if new else ("SWAP" if swapped else "NONE"),
               "new_sites": [], "swapped_sites": [], "removed_sites": [f"0x{s:06X}" for s in gone]}
        for s in swapped:
            b = cr.BY_DRAW_SITE.get(s)
            rec["swapped_sites"].append({"site": f"0x{s:06X}", "block": b.name if b else None,
                                         "from_model": base[s].get("model_index"), "to_model": now[s].get("model_index")})
        for s in new:
            b = cr.BY_DRAW_SITE.get(s)
            idx = now[s].get("model_index")
            entry = {"site": f"0x{s:06X}", "upload_site": f"0x{now[s]['upload_site']:06X}", "block": b.name if b else None,
                     "block_status": b.status if b else "UNKNOWN_SITE", "model_index": idx,
                     "model_size": list(oracle.sizes.get(idx, ())) or None}
            if with_anchor and idx is not None and idx in oracle.sizes:
                def make(over, _c=c):
                    sizes = dict(oracle.sizes)
                    sizes.update(over)
                    return de.Emulator(oracle.image, bytes(_c), sizes)
                try:
                    pr = apb.probe(make, idx, oracle.sizes[idx], s)
                    entry["anchor"] = {"d_anchor_d_size": [[round(v, 6) for v in row] for row in pr["d_anchor_d_size"]],
                                       "anchor_at_base": [round(v, 5) for v in pr["anchor_at_base"]],
                                       "rotation_changes_with_size": pr["rotation_changes_with_size"]}
                    entry["anchor_rule"] = classify_anchor(pr)
                except Exception as e:  # noqa: BLE001
                    entry["anchor_error"] = f"{type(e).__name__}: {e}"[:160]
            rec["new_sites"].append(entry)
        out.append(rec)
    return {"kind": f"0x{kind:02x}", "seed": list(seed), "probes": out}


def classify_anchor(pr):
    """Name the anchor law if it is one of the established rules; otherwise return the matrix."""
    m = pr["d_anchor_d_size"]
    diag = [round(m[i][i], 4) for i in range(3)]
    off = max(abs(m[i][j]) for i in range(3) for j in range(3) if i != j)
    if off > 1e-4 or pr["rotation_changes_with_size"]:
        return "NONLINEAR_OR_COUPLED"
    rules = {(0.5, 0.5, 0.5): "center", (0.5, 1.0, 0.5): "tail", (0.5, 0.0, 0.5): "wing"}
    return rules.get(tuple(diag), f"linear diag {diag}")


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--exe", default=str(DEFAULT_EXE))
    ap.add_argument("--sizes", default=str(DEFAULT_SIZES))
    ap.add_argument("--kinds", default="0x02,0x1e,0x3f,0x13,0x4c")
    ap.add_argument("--out", required=True)
    a = ap.parse_args(argv)
    out = Path(a.out).resolve()
    if not out.is_relative_to(ROOT / ".scratch"):
        raise SystemExit("ALPHA_OUTPUT_MUST_BE_IN_SCRATCH")
    oracle = ao.load(a.exe, a.sizes)
    res = {"format": "cubegame.alpha_equipment_probe/1", "decision": "D64",
           "kinds": [probe_kind(oracle, int(k, 0)) for k in a.kinds.split(",")]}
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(res, indent=1), encoding="utf-8")
    for k in res["kinds"]:
        print(k["kind"], [(p["label"], p["result"], [(n["block"], n.get("anchor_rule")) for n in p["new_sites"]],
                           [s["block"] for s in p["swapped_sites"]]) for p in k["probes"]])
    return 0


if __name__ == "__main__":
    sys.exit(main())
