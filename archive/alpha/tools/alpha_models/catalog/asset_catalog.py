"""D64: full asset inventory of Alpha 2013 — every model in data1.db and every model Cube.exe loads.

Sources (read-only; outputs only under .scratch):
* ``data1.db``  — 2857 ``.cub`` blobs (all models that physically exist);
* ``Cube.exe``  — every ``VoxelModel_load_from_file_or_buffer`` call (VA 0x4E7290, 2576 calls):
    - 2557 calls in ``GameController_load_game_assets`` fill the ONE shared model vector
      (``alpha_static.model_registry``: index, load flag, overwritten loads);
    - 19 calls elsewhere pass ``size = 0`` (``push 1 ; push 0``) = load from a LOOSE FILE on disk
      (World build-mode framework, map tiles, skull, build cursor). The distribution has no loose
      ``.cub`` files, so Alpha shows these models EMPTY; the DB holds blobs of the same name;
* bindings that select a registry index at run time:
    - creature appearance slots (``creature_generateAppearance`` executed for all kinds/seeds,
      ``alpha_static`` scan);
    - items (``item_models`` sweep: ``GameController_item_icon_id`` executed over the item domain).

Every model gets independent statuses (GEOMETRY / VISUAL / RIG / ANIMATION / BINDING) and ONE
processing route; nothing is left without a route. Route ids:
  CREATURE_PART        drawn by a creature draw block (appearance slot) -> universal rig/animation
  EQUIPMENT_SWAP       item model that REPLACES the body/hand/foot block model when worn
  EQUIPMENT_ATTACH     item model drawn in an equipment-only block (weapon/off-hand/shoulder/neck/lamp/special)
  ITEM_STATIC          item model never drawn on a creature (inventory icon / dropped item / consumable)
  REGISTRY_STATIC      loaded into the registry, no executed selector reaches it (static geometry)
  FILE_LOADER_ABSENT   loaded from a loose file that the distribution lacks (EMPTY in Alpha);
                       the DB blob of the same name is converted as static geometry, provenance noted
  DB_ONLY              exists in data1.db, no code path of Cube.exe loads it (unused content)
  MISSING_IN_DB        loaded by name but no blob: Alpha keeps the model EMPTY (part not drawn)

python -m tools.alpha_models.catalog.asset_catalog --out-dir .scratch/catalog
"""
from __future__ import annotations

import argparse
import collections
import hashlib
import json
import sqlite3
import struct
import sys
from pathlib import Path

from tools.alpha_models.anim import pe_bytes as pb
from tools.alpha_models.cub_format import CubFormatError, deobfuscate, parse_decoded
from tools.alpha_models.runtime.pose_capture import find_call_sites
from tools.alpha_models.universal import alpha_static as st
from tools.alpha_models.universal import voxel_markers as vm

ROOT = Path(__file__).resolve().parents[3]
DEFAULT_EXE = ROOT / ".scratch/alphatest2/Cube.exe"
DEFAULT_DB = ROOT / ".scratch/alphatest2/data1.db"
FORMAT = "cubegame.alpha_asset_catalog/1"

# item type -> where a creature shows it (drawModel equipment slot usage, GameController.cpp
# 2111-2123 / 5998 / 6412 / 6438 / 7168). The slot is what drawModel reads; the type -> slot
# assignment is checked by executing drawModel with the item in that slot (equipment_probe).
ITEM_TYPE_ROUTE = {
    3: ("EQUIPMENT_ATTACH", "weapon/offhand (+0xaa8/+0x990)"),
    4: ("EQUIPMENT_SWAP", "body (+0x530 chest)"),
    5: ("EQUIPMENT_SWAP", "hand_l/hand_r (+0x760 gloves)"),
    6: ("EQUIPMENT_SWAP", "foot_l/foot_r (+0x648 boots)"),
    7: ("EQUIPMENT_ATTACH", "shoulder_item (+0x878)"),
    8: ("EQUIPMENT_ATTACH", "neck_item (+0x418)"),
    23: ("EQUIPMENT_ATTACH", "special +0xf08 (glider/boat; drawModel flag 0x10)"),
    24: ("EQUIPMENT_ATTACH", "lamp +0xdf0"),
}


def _loose_file_loads(image):
    """Calls of the loader outside load_game_assets: (call VA, .cub literal, size arg)."""
    text = next(s for s in image.sections if s.name == ".text")
    lo = image.image_base + text.virtual_address
    hi = lo + text.virtual_size
    calls = find_call_sites(image.read, lo, hi, st.VOXEL_LOAD_VA)
    out = []
    for c in calls:
        if st.LOAD_ASSETS_VA <= c < st.LOAD_ASSETS_VA + st.LOAD_ASSETS_SPAN:
            continue
        win = image.read(c - 0x60, 0x60)
        name = None
        for i in range(len(win) - 5):
            if win[i] in (0x68, 0xB8, 0xB9, 0xBA, 0xBB, 0xBE, 0xBF):        # push imm32 / mov r32, imm32
                s = st._cstr(image, int.from_bytes(win[i + 1:i + 5], "little"))
                if s and s.endswith(".cub"):
                    name = s
        tail = image.read(c - 18, 18)
        # push size 0 within the 18 bytes before the call (all 19 sites: `push 1 .. push 0 .. lea .. push`)
        size_arg = "file" if b"\x6a\x00" in tail else "unknown"
        out.append({"call_va": f"0x{c:06X}", "name": name, "source": size_arg})
    return out


def _strings_near(image, out):
    """Resolve names of loose loads whose literal is set by string_assign (decompiled pattern): the
    .cub literal nearest BEFORE the call in a 0x80-byte window (push imm32 in that window)."""
    for rec in out:
        if rec["name"]:
            continue
        c = int(rec["call_va"], 16)
        win = image.read(c - 0x80, 0x80)
        for i in range(len(win) - 5):
            if win[i] == 0x68:
                s = st._cstr(image, int.from_bytes(win[i + 1:i + 5], "little"))
                if s and s.endswith(".cub"):
                    rec["name"] = s
    return out


def model_record(name, blob, strict):
    rec = {"in_db": blob is not None}
    if blob is None:
        return rec
    rec["blob_sha256"] = hashlib.sha256(blob).hexdigest()
    dec = deobfuscate(blob)
    try:
        m = parse_decoded(dec)
    except CubFormatError as e:
        rec["geometry"] = {"status": "BLOCKED", "reason": f"DECODE_ERROR {e}"}
        return rec
    px = m.pixels
    cols = collections.Counter(zip(px[0::3], px[1::3], px[2::3]))
    filled = sum(n for c, n in cols.items() if c != (0, 0, 0))
    markers = [cols.get(c, 0) for c in vm.MARKERS]
    rec["geometry"] = {"status": "SUPPORTED" if filled else "EMPTY_GRID", "size": [m.width, m.depth, m.height],
                       "filled_voxels": filled, "colours": len(cols) - (1 if (0, 0, 0) in cols else 0)}
    rec["visual"] = {"marker_voxels_rgb": markers,
                     "marker_rule": ("unknown_load_flag" if strict is None else
                                     ("markers_drawn (flag 1)" if strict else "markers_transparent (flag 0)"))}
    return rec


def build(exe=DEFAULT_EXE, db=DEFAULT_DB, static_path=None, items_path=None):
    st.verify_exe(exe)
    image = pb.load(exe)
    registry, rstats, rinfo = st.model_registry(image, detail=True)
    loose = _strings_near(image, _loose_file_loads(image))
    con = sqlite3.connect(f"file:{db}?mode=ro&immutable=1", uri=True)
    blobs = {k: v for k, v in con.execute("select key, value from blobs")}

    static = json.loads(Path(static_path).read_text(encoding="utf-8")) if static_path else None
    items = json.loads(Path(items_path).read_text(encoding="utf-8")) if items_path else None

    # bindings ---------------------------------------------------------------------------------
    creature_use = collections.defaultdict(list)      # index -> [(kind, slot)]
    if static:
        for k, e in static["kinds"].items():
            for slot, vals in e.get("slot_variants", {}).items():
                for v in vals:
                    if v >= 0:
                        creature_use[v].append(f"{k}:{slot}")
    item_use = {}
    if items:
        for m, rec in items["models"].items():
            types = sorted({it["type"] for it in rec["items"]})
            item_use[int(m)] = {"types": types, "item_variants": rec["item_variants"]}

    names = {}
    for idx, rec in rinfo.items():
        names.setdefault(rec["name"], []).append(("registry", idx, rec))
        for ow in rec["overwritten"]:
            names.setdefault(ow["name"], []).append(("overwritten", idx, ow))
    for rec in loose:
        if rec["name"]:
            names.setdefault(rec["name"], []).append(("loose_file", None, rec))
    all_names = sorted(set(blobs) | set(names))

    models, counts = {}, collections.Counter()
    for n in all_names:
        refs = names.get(n, [])
        reg = [(i, r) for kind_, i, r in refs if kind_ == "registry"]
        flag = reg[0][1]["load_flag"] if reg else (1 if any(k_ == "loose_file" for k_, _, _ in refs) else None)
        rec = {"name": n, **model_record(n, blobs.get(n), None if flag is None else flag == 1)}
        rec["loaders"] = [{"kind": k_, "index": i, "load_flag": r.get("load_flag"), "call_va": r.get("call_va")}
                          for k_, i, r in refs]
        idxs = [i for i, _ in reg]
        cuse = sorted({u for i in idxs for u in creature_use.get(i, [])})
        iuse = {}
        for i in idxs:
            if i in item_use:
                iuse = item_use[i]
        rec["bindings"] = {"registry_index": idxs, "creature_slots": cuse[:40], "creature_slot_count": len(cuse),
                           "items": iuse}
        # route
        if not rec["in_db"]:
            route = "MISSING_IN_DB"
        elif cuse:
            route = "CREATURE_PART"
        elif iuse:
            routes = {ITEM_TYPE_ROUTE.get(t, ("ITEM_STATIC", None))[0] for t in iuse["types"]}
            route = ("EQUIPMENT_ATTACH" if "EQUIPMENT_ATTACH" in routes else
                     "EQUIPMENT_SWAP" if "EQUIPMENT_SWAP" in routes else "ITEM_STATIC")
            rec["bindings"]["equipment_blocks"] = sorted({ITEM_TYPE_ROUTE[t][1] for t in iuse["types"]
                                                          if t in ITEM_TYPE_ROUTE})
        elif reg:
            route = "REGISTRY_STATIC"
        elif any(k_ == "loose_file" for k_, _, _ in refs):
            route = "FILE_LOADER_ABSENT"
        elif any(k_ == "overwritten" for k_, _, _ in refs):
            route = "REGISTRY_OVERWRITTEN"        # loaded, then replaced by a later load of the same index
        else:
            route = "DB_ONLY"
        rec["route"] = route
        rec["status"] = _statuses(rec)
        models[n] = rec
        counts[route] += 1
    geo = collections.Counter(m.get("geometry", {}).get("status", "NONE") for m in models.values())
    return {"format": FORMAT, "decision": "D64", "exe_sha256": st.EXPECTED_EXE_SHA256,
            "db_sha256": st.sha256_file(db),
            "metrics": {"db_models": len(blobs), "exe_load_calls": rstats["loads"] + len(loose),
                        "registry_indices": rstats["indexed"], "registry_overwritten_loads": rstats["overwritten_loads"],
                        "loose_file_loads": len(loose), "names_total": len(all_names),
                        "by_route": dict(sorted(counts.items())), "geometry": dict(geo),
                        "creature_bound_indices": len(creature_use), "item_bound_indices": len(item_use)},
            "registry_stats": rstats, "loose_file_loads": loose, "models": models}


def _statuses(rec):
    r = rec["route"]
    g = rec.get("geometry", {}).get("status", "NOT_APPLICABLE" if r == "MISSING_IN_DB" else "NONE")
    return {
        "GEOMETRY": "ALPHA_EMPTY_MODEL" if r == "MISSING_IN_DB" else g,
        "VISUAL": rec.get("visual", {}).get("marker_rule", "NOT_APPLICABLE"),
        "RIG": {"CREATURE_PART": "CREATURE_BLOCK", "EQUIPMENT_SWAP": "BODY_BLOCK_MODEL_SWAP",
                "EQUIPMENT_ATTACH": "EQUIPMENT_BLOCK"}.get(r, "NOT_APPLICABLE"),
        "ANIMATION": {"CREATURE_PART": "VIA_CREATURE_KIND", "EQUIPMENT_SWAP": "VIA_WEARER_BLOCK",
                      "EQUIPMENT_ATTACH": "VIA_WEARER_BLOCK"}.get(r, "NOT_APPLICABLE"),
        "BINDING": {"DB_ONLY": "UNUSED_BY_ALPHA_CODE", "FILE_LOADER_ABSENT": "LOOSE_FILE_ABSENT_EMPTY_IN_ALPHA",
                    "MISSING_IN_DB": "ALPHA_EMPTY_MODEL", "REGISTRY_STATIC": "LOADED_NO_EXECUTED_SELECTOR",
                    "REGISTRY_OVERWRITTEN": "REPLACED_BY_LATER_LOAD"}.get(r, "EXECUTED_SELECTOR"),
    }


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--exe", default=str(DEFAULT_EXE))
    ap.add_argument("--db", default=str(DEFAULT_DB))
    ap.add_argument("--out-dir", default=str(ROOT / ".scratch/catalog"))
    a = ap.parse_args(argv)
    out = Path(a.out_dir).resolve()
    if not out.is_relative_to(ROOT / ".scratch"):
        raise SystemExit("ALPHA_OUTPUT_MUST_BE_IN_SCRATCH")
    out.mkdir(parents=True, exist_ok=True)
    static = out / "alpha_static.json"
    if not static.exists():
        st_main = st.main
        st_main(["scan", "--exe", a.exe, "--db", a.db, "--out", str(static)])
    items = out / "item_models.json"
    if not items.exists():
        from tools.alpha_models.catalog import item_models
        item_models.main(["--exe", a.exe, "--out", str(items)])
    sizes = out / "registry_sizes.json"
    if not sizes.exists():
        reg, _ = st.model_registry(pb.load(a.exe))
        sizes.write_text(json.dumps({str(k): v for k, v in sorted(st.registry_model_sizes(a.db, reg).items())}),
                         encoding="utf-8")
    res = build(a.exe, a.db, static, items)
    (out / "asset_catalog.json").write_text(json.dumps(res, indent=1), encoding="utf-8")
    print(json.dumps({"ASSET_CATALOG": "DONE", **res["metrics"]}, indent=1))
    return 0


if __name__ == "__main__":
    sys.exit(main())
