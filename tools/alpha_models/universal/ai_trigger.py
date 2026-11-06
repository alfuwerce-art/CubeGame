"""П1 (S05B-A, D60): which actions Alpha's creature AI launches for one kind — by executing bytes.

The combat AI tick ``cube::CombatBehavior::vfunc_0`` (VA 0x42CB20) chooses the next action of a
creature that has a target. Its attack-choice region starts at the kind dispatch (VA 0x42D87F,
``mov eax, [edi+0x64]``) and ends at the common exits 0x42EAEF / 0x42EB01 / 0x42EAEA. Instead of
transcribing it, this module EXECUTES that region with ``drawmodel_emu.EmuCPU`` on a creature built
by Alpha's own ``creature_generateAppearance`` (appearance flags ``+0x7e`` select the branch) and
records every value written to the action byte ``creature+0x68`` together with the VA that wrote it.

Inputs that come from the world (distance to the target, rand(), resource +0x170, ability
availability, combo counter +0x70, previous action) are swept explicitly: they are the CONDITIONS
of the result, never guessed values. The external pending order (frame local [esp+0x3c], an order
pushed from outside the AI, VA 0x42D861) is held at 0 — it is not an AI decision.

Calls the region makes are executed for real (Alpha code) except:
  rand (IAT 0x6FC468)              -> scripted sequence (input)
  ability_isUsable 0x43E5A0        -> scripted answer (input; depends on skills of the creature)
  libm thunks                      -> exact maths (drawmodel_emu.LIBM_STUBS)
Anything else that cannot be executed stops the run and is reported (PARTIAL), never guessed.

python -m tools.alpha_models.universal.ai_trigger --kind 0x1e [--out .scratch/...]
"""
from __future__ import annotations

import argparse
import itertools
import json
import struct
from pathlib import Path

from tools.alpha_models.anim import pe_bytes
from tools.alpha_models.universal import alpha_oracle as ao
from tools.alpha_models.universal import alpha_static as st
from tools.alpha_models.universal import drawmodel_emu as de

ROOT = Path(__file__).resolve().parents[3]
DEFAULT_EXE = ROOT / '.scratch/alphatest2/Cube.exe'

REGION_START = 0x42D87F              # kind dispatch of the attack choice
REGION_EXITS = (0x42EAEF, 0x42EB01, 0x42EAEA)
RAND_IAT = 0x6FC468                  # `mov ebx, [0x6FC468]; call ebx`  (msvcr rand)
ABILITY_IS_USABLE = 0x43E5A0         # thiscall(creature, ability_id) -> al
ACTION = 0x68
TIMER = 0x6C
TARGET = de.HEAP_BASE + 0x8000       # target creature struct
MISC = de.HEAP_BASE + 0x10000

# frame locals of vfunc_0 used by the region (esp-relative, from the disassembly)
F_DIST2 = 0x1C                       # f32 squared distance to the target (world units)
F_TARGET = 0x20                      # target creature pointer
F_ORDER = 0x3C                       # external pending order (held 0)
F_PTR_2C, F_PTR_30 = 0x2C, 0x30      # helper objects the region passes on (scratch buffers)

KNOWN_KIND_BRANCHES = {              # kind compares inside the region (address -> kind)
    0x42D882: 0x74, 0x42DEC3: 0x65, 0x42E25E: 0x68, 0x42E29C: 0x19, 0x42E9B1: 0x68,
}


class RegionStop(Exception):
    pass


def _rand_stub(seq):
    it = iter(seq)

    def stub(cpu):
        try:
            cpu.r["eax"] = next(it) & 0x7FFF
        except StopIteration:
            raise RegionStop("RAND_SEQUENCE_EXHAUSTED")
    return stub


def _ability_stub(answer, log):
    def stub(cpu):
        log.append(cpu.arg(0))
        cpu.r["eax"] = (cpu.r["eax"] & ~0xFF) | (1 if answer else 0)
    return stub


def run_region(image, creature, *, dist2, resource, rand_seq=(1,) * 8, ability=False, counter=0,
               prior_action=0, timer=1_000_000, target_size=None, max_steps=400_000):
    """Execute the region once. Returns {"action", "writes": [(va, value)], "abilities", "error"}."""
    rand_target = image.u32(RAND_IAT)
    abilities = []
    stubs = dict(de.LIBM_STUBS)
    stubs[rand_target] = (_rand_stub(rand_seq), 0)
    stubs[ABILITY_IS_USABLE] = (_ability_stub(ability, abilities), 4)
    cpu = de.EmuCPU(image, stubs=stubs, max_steps=max_steps)
    cpu.mem.map(de.STACK_BASE, de.STACK_SIZE)
    cre = cpu.mem.map(de.HEAP_BASE, 0x2000, bytes(creature))
    tgt = cpu.mem.map(TARGET, 0x2000)
    misc = cpu.mem.map(MISC, 0x4000)
    # empty std::list sentinel for the buff list +0x1178 (chooseAttack walks it)
    struct.pack_into("<II", misc, 0x800, MISC + 0x800, MISC + 0x800)
    struct.pack_into("<I", cre, 0x1178, MISC + 0x800)
    cre[ACTION] = prior_action & 0xFF
    struct.pack_into("<i", cre, TIMER, timer)
    struct.pack_into("<i", cre, 0x70, counter)
    struct.pack_into("<i", cre, 0x128, 0)
    struct.pack_into("<f", cre, 0x170, resource)
    struct.pack_into("<f", tgt, 0x80, target_size if target_size is not None else
                     struct.unpack_from("<f", cre, 0x80)[0])
    esp = de.STACK_BASE + 0x80000
    cpu.mem.write(esp + F_DIST2, struct.pack("<f", dist2))
    cpu.mem.write(esp + F_TARGET, struct.pack("<I", TARGET))
    cpu.mem.write(esp + F_ORDER, struct.pack("<I", 0))
    cpu.mem.write(esp + F_PTR_2C, struct.pack("<I", MISC + 0x1000))
    cpu.mem.write(esp + F_PTR_30, struct.pack("<I", MISC + 0x1800))
    cpu.r["esp"] = esp
    cpu.r["edi"] = de.HEAP_BASE
    cpu.r["ecx"] = de.HEAP_BASE          # thiscall callees at the dispatch use ecx = creature
    cpu.r["esi"] = de.HEAP_BASE + 0x10
    cpu.eip = REGION_START
    writes, kinds_seen, err = [], [], None
    last = cre[ACTION]
    try:
        while cpu.eip not in REGION_EXITS:
            va = cpu.eip
            if va in KNOWN_KIND_BRANCHES:
                kinds_seen.append(va)
            cpu.step()
            if cre[ACTION] != last:
                last = cre[ACTION]
                writes.append((va, last))
    except (de.EmuError, RegionStop, pe_bytes.PEError) as e:   # never guess past an unknown
        err = f"{type(e).__name__}: {str(e)[:120]}"
    return {"action": cre[ACTION], "writes": writes, "abilities": abilities, "error": err,
            "exit": cpu.eip, "kind_compares": kinds_seen}


# --------------------------------------------------------------------------- sweep
DIST_CASES = {"melee": 0.01, "far": 1.0e7}
RESOURCE_CASES = (0.0, 0.5, 0.79, 0.81, 1.0)
RAND_CASES = {"rand_even": (2,) * 8, "rand_odd": (1,) * 8, "rand_mod10_zero": (10, 1, 2, 3, 0, 1, 2, 3),
              "rand_0..3": (0, 1, 2, 3, 4, 5, 6, 7)}


def sweep(image, creature, prior_actions=(0,)):
    """All recorded outcomes over the input grid; closure over prior actions it produces."""
    out, seen_prior, queue = [], set(), list(prior_actions)
    while queue:
        prior = queue.pop(0)
        if prior in seen_prior:
            continue
        seen_prior.add(prior)
        for (dname, d2), res, (rname, rs), ab, cnt in itertools.product(
                DIST_CASES.items(), RESOURCE_CASES, RAND_CASES.items(), (False, True), (0, 1)):
            r = run_region(image, creature, dist2=d2, resource=res, rand_seq=rs, ability=ab,
                           counter=cnt, prior_action=prior)
            cond = {"distance": dname, "resource_170": res, "rand": rname, "ability_usable": ab,
                    "counter_70": cnt, "prior_action": prior}
            out.append({"conditions": cond, **r})
            if r["writes"] and not r["error"]:
                nxt = r["writes"][-1][1]
                if nxt not in seen_prior and nxt != prior:
                    queue.append(nxt)
    return out


def summarize(results):
    """{action: {"write_vas": [...], "conditions": [...]}} for launched actions; plus errors."""
    acts, errors, unchanged = {}, {}, 0
    for r in results:
        if r["error"]:
            errors.setdefault(r["error"], 0)
            errors[r["error"]] += 1
            continue
        if not r["writes"]:
            unchanged += 1
            continue
        va, a = r["writes"][-1]
        e = acts.setdefault(a, {"write_vas": set(), "examples": [], "ability_ids": set()})
        e["write_vas"].add(va)
        e["ability_ids"].update(r["abilities"])
        if len(e["examples"]) < 4:
            e["examples"].append(r["conditions"])
    return {"actions": {f"{a}": {"write_vas": sorted(f"0x{v:06X}" for v in e["write_vas"]),
                                  "ability_queries": sorted(e["ability_ids"]),
                                  "examples": e["examples"]} for a, e in sorted(acts.items())},
            "errors": errors, "runs_without_action_write": unchanged, "runs": len(results)}


def analyse_kind(image, kind, seed=(0, 0)):
    app = st.AppearanceEmulator(image).run(kind, *seed)
    raw = app["raw"]
    c = bytearray(0x2000)
    c[0x78:0x78 + len(raw)] = raw
    struct.pack_into("<I", c, 0x64, kind)
    for off, fmt, v in ao.RUNTIME_DEFAULTS:
        struct.pack_into(fmt, c, off, v)
    c[0x5C] |= 1                                   # on ground
    c[0x60] = 1                                    # hostile monster (faction observed in captures)
    flags = struct.unpack_from("<H", c, 0x7E)[0]
    res = sweep(image, c)
    return {"format": "cubegame.ai_trigger/1", "kind": f"0x{kind:02x}", "seed": list(seed),
            "appearance_flags_7e": f"0x{flags:04x}", "region": [f"0x{REGION_START:06X}",
                                                               [f"0x{x:06X}" for x in REGION_EXITS]],
            "held_inputs": {"external_order_esp_3c": 0, "stun_128": 0, "timer_6c": 1_000_000,
                            "faction_60": 1, "on_ground_5c_bit0": 1},
            **summarize(res)}


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--kind", required=True, type=lambda s: int(s, 0))
    ap.add_argument("--seed", default="0,0")
    ap.add_argument("--exe", default=str(DEFAULT_EXE))
    ap.add_argument("--out")
    a = ap.parse_args(argv)
    image = pe_bytes.load(a.exe)
    rep = analyse_kind(image, a.kind, tuple(int(x) for x in a.seed.split(",")))
    text = json.dumps(rep, indent=1)
    if a.out:
        p = Path(a.out).resolve()
        if not p.is_relative_to(ROOT / '.scratch'):
            raise SystemExit("ALPHA_OUTPUT_MUST_BE_IN_SCRATCH")
        p.parent.mkdir(parents=True, exist_ok=True)
        p.write_text(text, encoding="utf-8")
    print(text)


if __name__ == "__main__":
    main()
