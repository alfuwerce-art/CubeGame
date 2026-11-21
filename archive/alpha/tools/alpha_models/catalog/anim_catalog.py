"""D64: catalog of every animation path of Alpha 2013 (what exists in the code, where it is triggered,
how the pose is computed and how it reaches Unity). Read-only on Cube.exe; output under .scratch.

Parts (each from the shipped bytes, decompilation used only for FUNCTION NAMES/boundaries):

1. ``action_writers`` — every instruction that stores the creature action byte (``creature+0x68``):
   ``mov byte [reg+0x68], imm8|r8`` decoded with capstone over the whole .text, grouped by enclosing
   function. A writer is classified by its region: creature AI attack choice (0x42D87F..0x42EAEF and
   the shared ``CombatBehavior`` helpers), player input helpers, everything else = an event path
   (hit/death/interaction...). The VALUE written is reported when it is an immediate.
2. ``action_states`` — action code -> drawModel state for every equipment combination
   (``anim_states.state_table``: the renderer's resolver block executed, 256 actions).
3. ``render_paths`` — every call of ``render_uploadModelViewMatrices`` (VA 0x4482A0) grouped by
   function: Creature_drawModel is the creature path; other functions are separate model renderers
   (dropped items, projectiles, UI/billboards, world). Each gets an adapter route.

python -m tools.alpha_models.catalog.anim_catalog --out .scratch/catalog/anim_catalog.json
"""
from __future__ import annotations

import argparse
import bisect
import collections
import json
import re
import sys
from pathlib import Path

import capstone

from tools.alpha_models.anim import pe_bytes as pb
from tools.alpha_models.runtime.pose_capture import find_call_sites
from tools.alpha_models.universal import alpha_static as st
from tools.alpha_models.universal import anim_states as an

ROOT = Path(__file__).resolve().parents[3]
DEFAULT_EXE = ROOT / ".scratch/alphatest2/Cube.exe"
DEFAULT_DECOMP = ROOT / ".scratch/CubeWorld-Reversal/cube"
UPLOAD_VA = 0x4482A0
DRAWMODEL = (0x4128F0, 0x423E00)
AI_REGION = (0x42CB20, 0x42EB10)                 # CombatBehavior::vfunc_0 (attack choice inside)
ATTACK_CHOICE = (0x42D87F, 0x42EB02)             # the region ai_trigger / trigger_catalog execute
AI_HELPERS = {0x4455D7: "CombatBehavior helper (weapon attack pick)", 0x4458A0: "CombatBehavior helper"}
PLAYER_INPUT = (0x445020, 0x445410, 0x49C272)   # S05A-2 input helpers (contract trigger_scope)

# routes of the non-creature render paths (adapter per mechanism)
RENDER_ROUTES = {
    "Creature_drawModel": "CREATURE_ORACLE (drawmodel_emu: all states, kinds, equipment)",
    "GameController_renderDroppedItem": "STATIC_MODEL + Alpha transform (yaw = item[8]*90 deg, scale = item[9]/model.sx)",
}


def function_starts(decomp):
    starts = {}
    pat = re.compile(r"^/\* Global::([A-Za-z0-9_:~<>]+) @ ([0-9a-f]+) \*/", re.M)
    for f in Path(decomp).rglob("*.cpp"):
        for m in pat.finditer(f.read_text(encoding="utf-8", errors="replace")):
            starts[int(m.group(2), 16)] = m.group(1)
    keys = sorted(starts)
    return keys, starts


def enclosing(keys, starts, va):
    i = bisect.bisect_right(keys, va) - 1
    return (keys[i], starts[keys[i]]) if i >= 0 else (None, None)


def action_writers(image, keys, starts):
    text = next(s for s in image.sections if s.name == ".text")
    lo = image.image_base + text.virtual_address
    md = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_32)
    md.skipdata = True
    out = []
    for ins in md.disasm(image.read(lo, text.virtual_size), lo):
        if ins.mnemonic != "mov" or not ins.op_str.startswith("byte ptr ["):
            continue
        m = re.match(r"byte ptr \[(e[a-z]{2}) \+ 0x68\], (.+)$", ins.op_str)
        if not m or m.group(1) in ("ebp", "esp"):
            continue
        fva, fname = enclosing(keys, starts, ins.address)
        src = m.group(2)
        val = int(src, 16) if src.startswith("0x") else (int(src) if src.isdigit() else None)
        if ATTACK_CHOICE[0] <= ins.address < ATTACK_CHOICE[1] or ins.address in AI_HELPERS:
            cls = "CREATURE_AI_ATTACK_CHOICE"          # executed by trigger_catalog
        elif AI_REGION[0] <= ins.address < AI_REGION[1]:
            cls = "CREATURE_AI_OTHER"                  # same AI tick, outside the executed region
        elif fva in PLAYER_INPUT or ins.address in PLAYER_INPUT:
            cls = "PLAYER_INPUT"
        else:
            cls = "OTHER_PATH"
        out.append({"va": f"0x{ins.address:06X}", "function_hint": fname, "function_va": f"0x{fva:06X}" if fva else None,
                    "value": val, "source": src, "class": cls})
    return out


def render_paths(image, keys, starts):
    text = next(s for s in image.sections if s.name == ".text")
    lo = image.image_base + text.virtual_address
    calls = find_call_sites(image.read, lo, lo + text.virtual_size, UPLOAD_VA)
    by_fn = collections.defaultdict(list)
    for c in calls:
        fva, fname = enclosing(keys, starts, c)
        by_fn[(fva, fname)].append(f"0x{c:06X}")
    out = []
    for (fva, fname), sites in sorted(by_fn.items(), key=lambda x: x[0][0] or 0):
        out.append({"function": fname, "function_va": f"0x{fva:06X}" if fva else None, "upload_calls": len(sites),
                    "sites": sites[:40], "route": RENDER_ROUTES.get(fname, "RESEARCH_ADAPTER (non-creature renderer)")})
    return out


def action_states(image):
    tab = an.state_table(image, actions=range(256), weapon_types=(0, 3), weapon_subtypes=range(0, 26),
                         offhands=((0, 0), (3, 13), (3, 0)), flags_130=(0, 1))
    out = {}
    for a, states in tab.items():
        real = {s: c for s, c in states.items() if s != -1}
        if not real:
            continue
        out[str(a)] = {str(s): {"conditions": len(c), "example": c[0]} for s, c in sorted(real.items())}
    return out


def _pose_route(states):
    real = [s for s in states if s != "0"]
    if not states:
        return "NO_DRAWMODEL_STATE"
    if not real:
        return "STATE_0_ONLY (locomotion pose; the action has no pose of its own)"
    return "DRAWMODEL_STATE " + ",".join(real) + " (EMULATED_ALPHA_CODE via oracle bake)"


def build(exe=DEFAULT_EXE, decomp=DEFAULT_DECOMP, triggers=None):
    st.verify_exe(exe)
    image = pb.load(exe)
    keys, starts = function_starts(decomp)
    writers = action_writers(image, keys, starts)
    states = action_states(image)
    paths = render_paths(image, keys, starts)
    by_action = collections.defaultdict(lambda: {"writers": [], "classes": set()})
    for w in writers:
        if w["value"] is not None:
            e = by_action[w["value"]]
            e["writers"].append(w["va"])
            e["classes"].add(w["class"])
    trig = json.loads(Path(triggers).read_text(encoding="utf-8"))["kinds"] if triggers else {}
    ai_kinds = collections.defaultdict(list)
    for k, r in trig.items():
        for a in (r.get("actions") or {}):
            ai_kinds[int(a)].append(k)
    actions = {}
    for a in sorted(set(int(x) for x in states) | set(by_action) | set(ai_kinds)):
        e = by_action.get(a, {"writers": [], "classes": set()})
        actions[str(a)] = {"drawmodel_states": states.get(str(a), {}),
                           "immediate_writers": e["writers"][:12], "writer_classes": sorted(e["classes"]),
                           "ai_launch_kinds": sorted(ai_kinds.get(a, [])),
                           "pose": _pose_route(states.get(str(a), {}))}
    return {"format": "cubegame.alpha_anim_catalog/1", "decision": "D64",
            "metrics": {"action_writer_instructions": len(writers),
                        "actions_with_own_pose": sum(1 for v in actions.values() if v["pose"].startswith("DRAWMODEL_STATE")),
                        "writers_by_class": dict(collections.Counter(w["class"] for w in writers)),
                        "actions_with_drawmodel_state": len(states),
                        "actions_launched_by_ai_any_kind": len(ai_kinds),
                        "render_functions": len(paths)},
            "actions": actions, "action_writers": writers, "render_paths": paths}


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--exe", default=str(DEFAULT_EXE))
    ap.add_argument("--decomp", default=str(DEFAULT_DECOMP))
    ap.add_argument("--triggers", default=str(ROOT / ".scratch/catalog/trigger_catalog.json"))
    ap.add_argument("--out", required=True)
    a = ap.parse_args(argv)
    out = Path(a.out).resolve()
    if not out.is_relative_to(ROOT / ".scratch"):
        raise SystemExit("ALPHA_OUTPUT_MUST_BE_IN_SCRATCH")
    res = build(a.exe, a.decomp, a.triggers if Path(a.triggers).exists() else None)
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(res, indent=1), encoding="utf-8")
    print(json.dumps({"ANIM_CATALOG": "DONE", **res["metrics"]}, indent=1))
    return 0


if __name__ == "__main__":
    sys.exit(main())
