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
  ability_isUsable 0x43E5A0        -> EXECUTED for real (resource +0x170 vs cost, cooldown map +0x139c);
                                      the cooldown map starts empty (no ability used yet). A scripted
                                      answer is only used when ``ability`` is passed explicitly.
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


# D64: MSVCR110 ``operator new`` (thunk 0x68D668 -> IAT 0x6FC3EC). Golems, Mammoth, RuneGiant, Saurian and
# Rockling allocate an object (thrown projectile) in their attack branch. Executed as a real allocation from
# a dedicated zeroed region (bump allocator, 16-byte aligned); the AI code then fills it itself.
OPERATOR_NEW_THUNK = 0x68D668
ALLOC_BASE, ALLOC_SIZE = de.HEAP_BASE + 0x40000, 0x40000


OPERATOR_DELETE_THUNK = 0x68D66E      # MSVCR110 operator delete (IAT 0x6FC3F0)
MEMMOVE_IAT = 0x6FC3F4                # MSVCR110 memmove, called directly through the IAT (vector growth)


def _memmove_stub(cpu):
    """memmove(dst, src, n) — exact semantics (overlap-safe), returns dst."""
    dst, src, n = (cpu.mem.u(cpu.r["esp"] + 4 * i, 4) for i in (1, 2, 3))
    if n > 0x100000:
        raise RegionStop(f"MEMMOVE_SIZE {n}")
    if n:
        cpu.mem.write(dst, cpu.mem.read(src, n))
    cpu.r["eax"] = dst


def _new_stub():
    state = {"next": ALLOC_BASE}

    def stub(cpu):
        size = cpu.mem.u(cpu.r["esp"] + 4, 4)
        if not 0 < size <= 0x10000 or state["next"] + size > ALLOC_BASE + ALLOC_SIZE:
            raise RegionStop(f"OPERATOR_NEW_SIZE {size}")
        if not getattr(cpu, "_alloc_mapped", False):
            cpu.mem.map(ALLOC_BASE, ALLOC_SIZE)
            cpu._alloc_mapped = True
        cpu.r["eax"] = state["next"]
        state["next"] = (state["next"] + size + 15) & ~15
    return stub


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


def run_region(image, creature, *, dist2, resource, rand_seq=(1,) * 8, ability=None, counter=0,
               prior_action=0, timer=1_000_000, target_size=None, cooldowns=(), max_steps=400_000, delta_ms=16):
    """Execute the region once. Returns {"action", "writes": [(va, value)], "abilities", "error"}."""
    rand_target = image.u32(RAND_IAT)
    abilities = []
    stubs = dict(de.LIBM_STUBS)
    stubs[rand_target] = (_rand_stub(rand_seq), 0)
    stubs[OPERATOR_NEW_THUNK] = (_new_stub(), 0)
    stubs[image.u32(MEMMOVE_IAT)] = (_memmove_stub, 0)      # `call [IAT]` lands on the unbound IAT value
    stubs[OPERATOR_DELETE_THUNK] = (lambda cpu: None, 0)     # free: the emulator never reuses memory
    if ability is not None:
        stubs[ABILITY_IS_USABLE] = (_ability_stub(ability, abilities), 4)
    cpu = de.EmuCPU(image, stubs=stubs, max_steps=max_steps)
    cpu.mem.map(de.STACK_BASE, de.STACK_SIZE)
    cre = cpu.mem.map(de.HEAP_BASE, 0x2000, bytes(creature))
    tgt = cpu.mem.map(TARGET, 0x2000)
    misc = cpu.mem.map(MISC, 0x4000)
    # empty std::list sentinel for the buff list +0x1178 (chooseAttack walks it)
    struct.pack_into("<II", misc, 0x800, MISC + 0x800, MISC + 0x800)
    struct.pack_into("<I", cre, 0x1178, MISC + 0x800)
    # std::map<int ability, int remaining_ms> of cooldowns at +0x139c (MSVC tree: node = left,
    # parent, right, color, isnil, key +0x10, value +0x14). Empty = no ability used yet; a listed
    # ability is on cooldown (value != 0) — a WORLD input, swept, never inferred.
    head = MISC + 0x900
    struct.pack_into("<III", misc, 0x900, head, head, head)
    misc[0x90C], misc[0x90D] = 1, 1
    nodes = []
    for i, ab_id in enumerate(sorted(cooldowns)):
        n = MISC + 0x940 + 0x20 * i
        nodes.append(n)
        struct.pack_into("<IIIBBxxii", misc, n - MISC, head, head, head, 1, 0, ab_id, 1000)
    if nodes:                               # chain as a right spine: sorted keys, valid BST
        root = nodes[0]
        struct.pack_into("<III", misc, 0x900, nodes[0], root, nodes[-1])   # head: leftmost, root, rightmost
        for i, n in enumerate(nodes):
            nxt = nodes[i + 1] if i + 1 < len(nodes) else head
            par = nodes[i - 1] if i else head
            struct.pack_into("<III", misc, n - MISC, head, par, nxt)
    struct.pack_into("<II", cre, 0x139C, head, len(nodes))
    if ability is None:
        cpu.hooks[ABILITY_IS_USABLE] = lambda c: abilities.append(c.mem.u(c.r["esp"] + 4, 4))
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
    # D64: vfunc_0's frame pointer; [ebp+0x10] = 3rd argument (frame time, ms), read after a mage spell
    # (actions 30..33) is chosen (0x42DDE2). A world input like the others: explicit, default 16 ms.
    ebp = de.STACK_BASE + 0x90000
    cpu.r["ebp"] = ebp
    cpu.mem.write(ebp + 0x10, struct.pack("<i", delta_ms))
    cpu.mem.write(ebp + 0x08, struct.pack("<I", de.HEAP_BASE))
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


COOLDOWN_CASES = {"no_cooldown": (), "cooldown_0x36": (0x36,)}


def sweep(image, creature, prior_actions=(0,), ability_cases=(None,), cooldown_cases=COOLDOWN_CASES):
    """All recorded outcomes over the input grid; closure over prior actions it produces."""
    out, seen_prior, queue = [], set(), list(prior_actions)
    while queue:
        prior = queue.pop(0)
        if prior in seen_prior:
            continue
        seen_prior.add(prior)
        for (dname, d2), res, (rname, rs), ab, cnt, (cdn, cds) in itertools.product(
                DIST_CASES.items(), RESOURCE_CASES, RAND_CASES.items(), ability_cases, (0, 1),
                cooldown_cases.items()):
            r = run_region(image, creature, dist2=d2, resource=res, rand_seq=rs, ability=ab,
                           counter=cnt, prior_action=prior, cooldowns=cds)
            cond = {"distance": dname, "resource_170": res, "rand": rname,
                    "ability_usable": "executed" if ab is None else ab,
                    "counter_70": cnt, "prior_action": prior, "cooldowns": cdn}
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


# creature fields copied from a live full dump (observed NPC state); pointers are NOT copied
OBSERVED_FIELDS = ((0x60, 1), (0x140, 2), (0x170, 4), (0x174, 12), (0x418, 0x118 * 7))
# header-only captures (creature_hdr, 0x180 bytes): no equipment slots
HEADER_FIELDS = ((0x60, 1), (0x140, 2), (0x170, 4), (0x174, 12))


def analyse_observed(image, kind, creature_full, seed=(0, 0), label="observed", fields=OBSERVED_FIELDS):
    """П1 on a kind's own appearance plus the class/equipment/stat fields of a captured NPC."""
    app = st.AppearanceEmulator(image).run(kind, *seed)
    raw = app["raw"]
    c = bytearray(0x2000)
    c[0x78:0x78 + len(raw)] = raw
    struct.pack_into("<I", c, 0x64, kind)
    for off, fmt, v in ao.RUNTIME_DEFAULTS:
        struct.pack_into(fmt, c, off, v)
    for off, n in fields:
        if off + n > len(creature_full):
            raise ValueError(f"OBSERVED_FIELD_NOT_CAPTURED +0x{off:x}")
        c[off:off + n] = creature_full[off:off + n]
    c[0x5C] |= 1
    flags = struct.unpack_from("<H", c, 0x7E)[0]
    res = sweep(image, c)
    return {"format": "cubegame.ai_trigger/1", "kind": f"0x{kind:02x}", "seed": list(seed), "template": label,
            "observed": {"faction_60": c[0x60], "class_140": c[0x140], "spec_141": c[0x141],
                         "weapon_aa8": [c[0xAA8], c[0xAA9]], "offhand_990": [c[0x990], c[0x991]]},
            "appearance_flags_7e": f"0x{flags:04x}", **summarize(res)}


# --------------------------------------------------------------------------- evidence file
EVIDENCE_FORMAT = "cubegame.npc_action_evidence/1"


def kind_evidence(image, kind, seeds, family_id, observed=None):
    """П1 of one kind over all its appearance variants; the launch set must not depend on the seed.

    ``observed`` = {"source": str, "creature": bytes, "fields": tuple} — live NPC state to run on
    (class/equipment/stats). Without it the creature is the kind's own default (class 0, unarmed)."""
    per_seed, flags = {}, set()
    for seed in seeds:
        if observed and tuple(seed) == (0, 0):
            rep = analyse_observed(image, kind, observed["creature"], seed=tuple(seed),
                                   label=observed["source"], fields=observed["fields"])
        else:
            rep = analyse_kind(image, kind, tuple(seed))
        if rep["errors"]:
            return {"kind": f"0x{kind:02x}", "status": "PARTIAL", "reason": f"EXECUTION_ERRORS {rep['errors']}"}
        flags.add(rep["appearance_flags_7e"])
        per_seed[tuple(seed)] = {int(a): v["write_vas"] for a, v in rep["actions"].items()}
        if tuple(seed) == (0, 0):
            base = rep
    sets = {json.dumps(sorted((a, v) for a, v in x.items())) for x in per_seed.values()}
    if len(sets) != 1 or len(flags) != 1:
        return {"kind": f"0x{kind:02x}", "status": "PARTIAL", "reason": "LAUNCH_SET_DEPENDS_ON_SEED"}
    launched = per_seed[(0, 0)]
    if any(len(v) != 1 for v in launched.values()):
        return {"kind": f"0x{kind:02x}", "status": "PARTIAL", "reason": "ACTION_WRITTEN_AT_SEVERAL_VAS"}
    conditions = {a: sorted({json.dumps({k: e[k] for k in ("resource_170", "cooldowns", "counter_70")},
                                        sort_keys=True) for e in base["actions"][str(a)]["examples"]})
                  for a in launched}
    return {"kind": f"0x{kind:02x}", "status": "TRIGGER_EMULATED", "family_id": family_id,
            "appearance_flags_7e": flags.pop(), "seeds_checked": [list(x) for x in seeds],
            "launched_actions": {str(a): v[0] for a, v in sorted(launched.items())},
            "condition_examples": {str(a): [json.loads(c) for c in v] for a, v in conditions.items()},
            "observed_state": observed["source"] if observed else None,
            "observed_source": observed.get("descriptor") if observed else None,
            # REVIEW-FIX-01: a typed boolean; equipment counts as observed only when the live record
            # carried the full equipment block (OBSERVED_FIELDS), never by text matching
            "equipment_observed": equipment_observed(observed),
            "equipment": "unarmed (weapon/off-hand slots empty)" + (" — observed" if equipment_observed(observed)
                         else " — NPC equipment of this kind not observed")}


def equipment_observed(observed) -> bool:
    return bool(observed) and tuple(observed["fields"]) == tuple(OBSERVED_FIELDS)


# --------------------------------------------------------------------------- observed live states
# Declared live sources of П1 (local captures in .scratch, never committed). Each descriptor pins the
# capture file by SHA256 and a deterministic record selector, so evidence_drift can re-run П1 on
# exactly the same observed state.
OBSERVED_SOURCES = {
    0x03: {"capture": ".scratch/s05ba/cap/humans_full.jsonl", "record_field": "creature_full", "kind": 3,
           "fields": "OBSERVED_FIELDS",
           "source": "live full dump, HumanFemale NPC, Arulon 2026-11-06 (humans_full.jsonl)"},
    0x3f: {"capture": ".scratch/universal/cap/batch1.jsonl", "record_field": "creature_hdr", "kind": 0x3f,
           "fields": "HEADER_FIELDS", "source": "live capture header, PlainRunner (batch1.jsonl)"},
}


def load_observed(desc, root=ROOT):
    """Observed state of a descriptor: first record of ``kind`` carrying ``record_field``.
    Raises ValueError when the capture is missing or its SHA256 differs from a pinned one."""
    import hashlib
    path = Path(root) / desc["capture"]
    if not path.exists():
        raise ValueError(f"OBSERVED_SOURCE_MISSING {desc['capture']}")
    data = path.read_bytes()
    sha = hashlib.sha256(data).hexdigest()
    if desc.get("sha256") and desc["sha256"] != sha:
        raise ValueError(f"OBSERVED_SOURCE_SHA256 {desc['capture']} {sha} != {desc['sha256']}")
    for line in data.decode("utf-8").splitlines():
        r = json.loads(line)
        if r.get("kind") == desc["kind"] and r.get(desc["record_field"]):
            fields = OBSERVED_FIELDS if desc["fields"] == "OBSERVED_FIELDS" else HEADER_FIELDS
            return {"source": desc["source"], "creature": bytes.fromhex(r[desc["record_field"]]), "fields": fields,
                    "descriptor": {**{k: v for k, v in desc.items()}, "sha256": sha}}
    raise ValueError(f"OBSERVED_RECORD_NOT_FOUND {desc['capture']} kind={desc['kind']}")


# fields of a kind record that П1 must reproduce exactly (evidence_drift)
EVIDENCE_COMPARED = ("status", "family_id", "appearance_flags_7e", "seeds_checked", "launched_actions",
                     "condition_examples", "observed_state", "equipment_observed", "equipment", "reason")


def build_evidence(image, coverage, families, root=ROOT):
    """The whole П1 evidence file (cubegame.npc_action_evidence/1) for the coverage kinds."""
    out = {"format": EVIDENCE_FORMAT, "decision": "D60", "tool": "tools/alpha_models/universal/ai_trigger.py",
           "region": "0x42D87F..0x42EAEF (CombatBehavior::vfunc_0)", "kinds": {}}
    for k in sorted(coverage):
        kind = int(k, 16)
        if kind == 0x02:
            out["kinds"][k] = {"kind": k, "status": "EXCLUDED", "reason": "specimen (0x02, seed 0,0) carries the "
                               "player_input profile; its actions are covered by the HumanMale regression"}
            continue
        obs = load_observed(OBSERVED_SOURCES[kind], root) if kind in OBSERVED_SOURCES else None
        out["kinds"][k] = kind_evidence(image, kind, [tuple(s) for s in coverage[k]], families[k], obs)
    return out


def evidence_drift_kind(image, key, e, root=ROOT):
    """Re-run П1 for one evidence record over ALL its declared seeds and its declared observed state."""
    obs = None
    if e.get("observed_source"):
        try:
            obs = load_observed(e["observed_source"], root)
        except ValueError as ex:
            return [f"EVIDENCE_DRIFT {key} {ex}"]
    elif e.get("observed_state"):
        return [f"EVIDENCE_DRIFT {key} OBSERVED_STATE_WITHOUT_SOURCE {e['observed_state']}"]
    if not isinstance(e.get("equipment_observed"), bool):
        return [f"EVIDENCE_DRIFT {key} EQUIPMENT_OBSERVED_NOT_BOOL {e.get('equipment_observed')!r}"]
    got = kind_evidence(image, int(key, 16), [tuple(s) for s in e["seeds_checked"]], e["family_id"], obs)
    got = json.loads(json.dumps(got))
    return [f"EVIDENCE_DRIFT {key} {f}: {e.get(f)!r} != {got.get(f)!r}"
            for f in EVIDENCE_COMPARED if e.get(f) != got.get(f)]


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
