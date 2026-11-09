"""Unified entity / part / animation contract of the universal Alpha -> Unity pipeline.

One contract describes ANY creature the static tables can resolve, without hardcoding a creature
name, a limb count or the humanoid input rules (S05A2-UNIVERSAL-CONTINUE-01, 2026-11-06):

  entity      kind, structural category, body plan, motion family, locomotion mode flags
  parts       draw blocks in draw order: role, mirror pair, anchor rule + its evidence, slot, model
  channels    walk / idle / actions / equipment / visibility / lifecycle, each with a source
              (provenance), a status and the exact reason when it is not available
  readiness   AUTO_READY | NEEDS_RESEARCH | BLOCKED (+ reasons) for the transfer planner

Everything here is derived from data that already exists: the static appearance table
(alpha_static.json, local .scratch), the draw-block table (creature_rig.BLOCKS), the walk-formula
terms (param_walk.TERMS), family clips on disk and action profiles below. No Alpha run.

Action profiles are EVIDENCE records, not body-plan rules: a profile states which specimens its
poses and triggers were verified on. Another creature never inherits a profile because it "looks
humanoid"; it gets ``BLOCKED`` with the reason, even when its drawModel family is identical
(poses compatible, trigger not proven). Animal/monster AI action rules need their own evidence.
"""
from __future__ import annotations

import collections
import glob
import json
import os

from tools.alpha_models.universal import creature_rig as cr
from tools.alpha_models.universal import param_walk as pw

FORMAT = "cubegame.entity_contract/1"

# --------------------------------------------------------------------------- provenance
# Main provenance classes (never mixed; a channel/clip carries exactly one of them).
PROVENANCE = ("TRANSFERRED_FROM_ALPHA",       # real matrices Alpha sent to the GPU, repeatability checked
              "EMULATED_ALPHA_CODE",          # original drawModel/resolver bytes executed by the oracle
              "RECONSTRUCTED_FROM_EVIDENCE",  # re-implemented formula, checked against captures/emulation
              "UNITY_TRANSITION",             # Unity pilot policy (crossfades), not an Alpha transition
              "BLOCKED")                      # not available; a reason is mandatory
# Auxiliary markers already used by the project; allowed only where they are explicit.
AUX_PROVENANCE = ("TEST_DATA_NOT_ALPHA", "PLACEHOLDER_NOT_ALPHA", "ORIGINAL_NOT_ALPHA")
CLIP_PROVENANCE = ("TRANSFERRED_FROM_ALPHA", "EMULATED_ALPHA_CODE", "RECONSTRUCTED_FROM_EVIDENCE")

# Clip categories (playback semantics, independent of the creature):
CLIP_CATEGORIES = {
    "LOOP_LOCOMOTION":  "cyclic clip parameterised by walk phase (loop)",
    "STATIC_POSE":      "single settled pose (idle)",
    "MOTION":           "one-shot timed action: entry -> action -> recovery",
    "HELD_STATE":       "latched pose held until cancelled (no duration timer)",
    "NATIVE_LIFECYCLE": "one-shot action whose lifetime is owned by a verified native input profile",
}
VISIBILITY_MODES = ("STATIC_ALL", "EXPLICIT_NATIVE_DRAW")

READINESS = ("AUTO_READY", "NEEDS_RESEARCH", "BLOCKED")

# --------------------------------------------------------------------------- part roles
# Structural role of a draw block. Names are BLOCK names (a cat's front paw is hand_l); the role
# says what the block does in drawModel, not what the creature calls it.
BLOCK_ROLES = {
    "body": "core", "head": "head", "hair": "head_attachment", "tail": "tail",
    "hand_l": "fore_limb", "hand_r": "fore_limb", "foot_l": "hind_limb", "foot_r": "hind_limb",
    "slot98_a": "mid_limb", "slot98_b": "mid_limb", "slot9a_a": "wing", "slot9a_b": "wing",
    "weapon": "equipment", "offhand_a": "equipment", "offhand_b": "equipment",
    "shoulder_item": "equipment", "neck_item": "equipment", "quiver": "equipment",
    "quiver_offhand": "equipment", "quiver_item": "equipment",
    "special_item": "equipment", "held_item": "equipment", "pet_item": "equipment", "lamp_item": "equipment",
}
MIRROR_PAIRS = {"hand_l": "hand_r", "hand_r": "hand_l", "foot_l": "foot_r", "foot_r": "foot_l",
                "slot98_a": "slot98_b", "slot98_b": "slot98_a", "slot9a_a": "slot9a_b", "slot9a_b": "slot9a_a",
                }   # D64: offhand_b is NOT a mirror of offhand_a (separate block for subtypes 4/12/20)
# Anchor rules the Unity rig implements (AlphaRig.Anchor). "head" is "center" for every kind except
# Dragon 0x65, whose head block is translated by (-sx/2, -sy*0.2, -sz/2) (drawModel, GameController.cpp
# 6256-6270; creature_rig.anchor). D69: the Unity mesh MUST use the same anchor the clip joints were
# decomposed with (alpha_oracle.joints uses creature_rig.anchor(kind=...)), otherwise the head is shifted.
UNITY_ANCHOR_RULES = ("center", "tail", "wing", "weapon", "neck", "lamp", "pet", "head_0x65")  # + by_subtype


def unity_anchor(rule, kind=None):
    if rule == "head":
        return "head_0x65" if kind == 0x65 else "center"
    return rule


# --------------------------------------------------------------------------- equipment layers
# Weapon layer: a verified layer needs BOTH grip blocks (hand_l + hand_r) on a non-quadruped
# body plan (flags bit0 clear: hands are not walking limbs) and a verified subtype.
VERIFIED_WEAPON_SUBTYPES = (15, 16, 17)       # two-handed; humanoid_rig.TWO_HANDED_SUBTYPES


def weapon_layer_support(parts, appearance, subtype=None):
    reasons = []
    if not ("hand_l" in parts and "hand_r" in parts):
        reasons.append("NO_GRIP_BLOCKS hand_l+hand_r")
    if appearance["flags"] & 1:
        reasons.append("FORE_LIMBS_ARE_WALKING_LIMBS (flags bit0)")
    if subtype is not None and int(subtype) not in VERIFIED_WEAPON_SUBTYPES:
        reasons.append(f"WEAPON_SUBTYPE_NOT_VERIFIED {subtype}")
    return reasons


# --------------------------------------------------------------------------- action profiles
# Evidence-scoped action profiles. `specimens` = (kind, seed_variant) the poses AND triggers were
# verified on. `pose_family_compatible` lets the planner report that another kind of the same
# drawModel family could reuse the poses once ITS trigger is proven — it never imports them.
ACTION_PROFILES = {
    "alpha_actions/humanoid_two_handed_catalog": {
        "status": "EMULATED_ALPHA_CODE",
        "specimens": [{"kind": 0x02, "seed_variant": [0, 0]}],
        # drawModel motion family the poses were verified on (pinned evidence, checked for drift
        # against the static table by `transfer_planner validate`)
        "evidence_families": ["alpha_creature/e66d96be5a"],
        "trigger": "player_input",
        "trigger_scope": "class 1/4 primary helper + input-tail (0x445020, 0x445410, 0x49C272); "
                         "GUI/resource/world gates PARTIAL",
        "requires_parts": ["hand_l", "hand_r", "weapon"],
        "equipment": {"weapon": {"type": 3, "subtypes": list(VERIFIED_WEAPON_SUBTYPES)}},
        # equipment the specimen's library was baked with (fixed-back weapon geometry must match)
        "specimen_equipment": {"weapon": {"model": "iron-greatsword.cub", "subtype": 15}},
        "categories": ["MOTION", "HELD_STATE", "NATIVE_LIFECYCLE"],
        # order of required clips for the controller probe: the verified primary chain first
        "primary_chain": [57, 58, 67],
        # action ids whose clips carry the verified two-hand grip contract / native visibility events
        "grip_checked_actions": [57, 58, 67],
        "dynamic_part_actions": [22, 23, 26, 71, 72],
        "blocked_examples": {"51": "AI branch of another kind (0x65); humanoid launch not proven",
                             "80": "consumable context/model not prepared"},
        "authorization": "D51/D57 (one humanoid, S05A-2)",
        "evidence": "docs/research/alpha2013/ALPHA_ACTION_LIFECYCLE.md",
    },
}
# S05B-A / D60: creature-AI profiles come from the П1 evidence file (ai_trigger.py executes the
# attack-choice region of Alpha's AI tick for every appearance variant of each kind). One profile per
# kind, specimen = that kind only (D60 rule 2); clips stay EMULATED_ALPHA_CODE (rule 1); the live
# capture result is a separate field (live_check). Equipment: none verified (rule 3).
NPC_EVIDENCE_PATH = os.path.join(os.path.dirname(__file__), "npc_action_evidence.json")


EVIDENCE_PROBLEMS = []   # evidence records refused while building profiles (no profile -> BLOCKED)


def _typed_equipment_observed(key, e):
    """REVIEW-FIX-01: the evidence carries an explicit boolean; text is never parsed."""
    v = e.get("equipment_observed")
    if not isinstance(v, bool):
        raise ValueError(f"npc_action_evidence {key}: equipment_observed must be a boolean, got {v!r}")
    return v


def _creature_ai_profiles(path=NPC_EVIDENCE_PATH):
    if not os.path.exists(path):
        return {}
    with open(path, encoding="utf-8") as fh:
        ev = json.load(fh)
    out = {}
    for k, e in ev.get("kinds", {}).items():
        if e.get("status") != "TRIGGER_EMULATED":
            continue
        launched = {int(a): int(va, 16) for a, va in e["launched_actions"].items()}
        try:
            eq_observed = _typed_equipment_observed(k, e)
        except ValueError as ex:
            EVIDENCE_PROBLEMS.append(str(ex))
            continue
        out[f"alpha_actions/creature_ai_{e['name_hint'].lower()}_{k}"] = {
            "status": "EMULATED_ALPHA_CODE",
            "specimens": [{"kind": int(k, 16), "seed_variant": [0, 0]}],
            "seeds_checked": e["seeds_checked"],
            "evidence_families": [e["family_id"]],
            "trigger": "creature_ai",
            "trigger_scope": (f"CombatBehavior::vfunc_0 attack choice {ev['region']} executed; appearance "
                              f"flags {e['appearance_flags_7e']}; {e['equipment']}; "
                              f"state: {e['observed_state'] or 'default creature of the kind'}; "
                              "target/world gates PARTIAL"),
            "requires_parts": [],
            "equipment": {},
            "specimen_equipment": {},
            "equipment_observed": eq_observed,
            "categories": ["MOTION"],
            "primary_chain": sorted(launched),
            "launched_actions": launched,
            "grip_checked_actions": [],
            "dynamic_part_actions": [],
            "blocked_examples": {},
            "live_check": e.get("live_check", "NOT_CAPTURED"),
            "authorization": "D60 (S05B-A)",
            "evidence": "docs/research/alpha2013/NPC_ACTION_TRIGGERS.md; " + os.path.basename(path),
        }
    return out


ACTION_PROFILES.update(_creature_ai_profiles())

# D64 / B-1: every other kind's AI launches, executed with the NPC state as an explicit CONDITION
# (catalog/trigger_catalog.py -> catalog/npc_trigger_evidence.json). A D60 profile of the same kind
# keeps precedence. The profile carries the UNARMED launches (bakeable without a weapon layer); the
# weapon-conditional launches are listed, and need the weapon layer of that rig before baking.
TRIGGER_EVIDENCE_PATH = os.path.join(os.path.dirname(__file__), "..", "catalog", "npc_trigger_evidence.json")


def _d64_ai_profiles(path=TRIGGER_EVIDENCE_PATH):
    if not os.path.exists(path):
        return {}
    with open(path, encoding="utf-8") as fh:
        ev = json.load(fh)
    have = {s["kind"] for p in ACTION_PROFILES.values() for s in p["specimens"]}
    out = {}
    for k, e in ev.get("kinds", {}).items():
        kind = int(k, 16)
        if kind in have or e.get("status") != "TRIGGER_EMULATED_CONDITIONAL" or not e.get("unarmed_actions")                 or not e.get("family_id"):
            continue
        launched = {int(a): int(v["write_vas"][0], 16) for a, v in e["unarmed_actions"].items()}
        name = (e.get("name_hint") or "kind").lower()
        out[f"alpha_actions/creature_ai_d64_{name}_{k}"] = {
            "status": "EMULATED_ALPHA_CODE",
            "specimens": [{"kind": kind, "seed_variant": [0, 0]}],
            "seeds_checked": [[0, 0]],
            "evidence_families": [e["family_id"]],
            "trigger": "creature_ai",
            "trigger_scope": (f"CombatBehavior::vfunc_0 attack choice {ev['region']} executed over "
                              f"{ev['condition_domains']['count']} NPC states (class x spec x weapon); unarmed "
                              "launches listed with their class/spec conditions; target/world gates PARTIAL"),
            "requires_parts": [],
            "equipment": {},
            "specimen_equipment": {},
            "equipment_observed": False,
            "categories": ["MOTION"],
            "primary_chain": sorted(launched),
            "launched_actions": launched,
            "launch_conditions": {int(a): v["class_spec"] for a, v in e["unarmed_actions"].items()},
            "weapon_conditional_actions": sorted(int(a) for a in e.get("weapon_actions", {})
                                                 if a not in e["unarmed_actions"]),
            "grip_checked_actions": [],
            "dynamic_part_actions": [],
            "blocked_examples": {},
            "live_check": "NOT_CAPTURED",
            "authorization": "D64 (method) / D63 (scope)",
            "evidence": "docs/reports/S05B2_CLAUDE_FULL_RESEARCH_REPORT.md; catalog/npc_trigger_evidence.json",
        }
    return out


ACTION_PROFILES.update(_d64_ai_profiles())


def _merge_d64_into_d60(path=TRIGGER_EVIDENCE_PATH):
    """D69 ADD_MISSING: a D60 profile was built from ONE observed NPC state (the default creature), so
    it lacks the unarmed launches the executed AI region produces in the other NPC states (class/spec
    conditions, D64 evidence). They are added to the D60 profile (which keeps its specimen, chain and
    precedence) only when the D64 record is for the same motion family. Returns {profile: added}."""
    if not os.path.exists(path):
        return {}
    with open(path, encoding="utf-8") as fh:
        ev = json.load(fh)["kinds"]
    added = {}
    for pid, prof in ACTION_PROFILES.items():
        if prof.get("trigger") != "creature_ai" or not prof.get("authorization", "").startswith("D60"):
            continue
        e = ev.get(f"0x{prof['specimens'][0]['kind']:02x}") or {}
        if e.get("status") != "TRIGGER_EMULATED_CONDITIONAL" or e.get("family_id") not in prof["evidence_families"]:
            continue
        extra = {int(a): int(v["write_vas"][0], 16) for a, v in (e.get("unarmed_actions") or {}).items()
                 if int(a) not in prof["launched_actions"]}
        if not extra:
            continue
        prof["launched_actions"] = {**prof["launched_actions"], **extra}
        prof["primary_chain"] = list(prof["primary_chain"]) + sorted(extra)
        prof["launch_conditions"] = {int(a): v["class_spec"] for a, v in e["unarmed_actions"].items()
                                     if int(a) in extra}
        prof["d69_added_actions"] = sorted(extra)
        prof["authorization"] = prof["authorization"] + "; D69 + D64 conditional unarmed launches"
        added[pid] = sorted(extra)
    return added


D69_MERGED_LAUNCHES = _merge_d64_into_d60()

# Trigger domains. Only player_input of the specimen above is evidenced. Creature AI launches of
# non-player kinds are an explicit open domain: they must never default to the humanoid rules.
TRIGGER_DOMAINS = {
    "player_input": "native input helpers of the player creature (class/spec/weapon dependent)",
    "creature_ai": "CombatBehavior/AI update of NPC kinds: NOT reconstructed for any kind",
}


def profile_for_specimen(kind, seed_variant):
    """Profile id whose verified specimen is exactly (kind, seed_variant), else None."""
    for pid, prof in ACTION_PROFILES.items():
        if any(s["kind"] == kind and list(s["seed_variant"]) == list(seed_variant) for s in prof["specimens"]):
            return pid
    return None


def specimen_families_from_appearances(resolve_appearance):
    """profile id -> family ids, with ``resolve_appearance(kind, seed) -> appearance`` (emulator)."""
    return {pid: {cr.resolve(s["kind"], resolve_appearance(s["kind"], tuple(s["seed_variant"]))).family_id
                  for s in prof["specimens"]} for pid, prof in ACTION_PROFILES.items()}


def unity_contract(entity):
    """Flat contract for Unity (JsonUtility): what the rig/controller must enforce at runtime."""
    ch = entity["channels"]
    acts = ch["actions"]
    w = ch["equipment"]["weapon"]
    return {
        "format": FORMAT, "kind": entity["kind"], "category": entity["category"],
        "bodyPlan": entity["body_plan"], "rig": entity["family_id"],
        "parts": [{"name": p["name"], "role": p["role"], "mirrorOf": p["mirror_of"] or "",
                   "anchor": p["unity_anchor"], "mirrored": p["mirrored"]} for p in entity["parts"]],
        "controllerMode": controller_mode(entity),
        "gameUsage": ch.get("game_usage", {}).get("policy", "CREATURE"),
        "gameUsageBasis": ch.get("game_usage", {}).get("basis") or "",
        "gameUsageReason": ch.get("game_usage", {}).get("reason") or "",
        "actionProfile": acts.get("profile") or "", "actionTrigger": acts.get("trigger") or "",
        "actionsStatus": acts["status"], "actionsReason": "; ".join(acts["reasons"]),
        "walkSources": [s["provenance"] for s in ch["walk"]["sources"]],
        "idleSources": [s["provenance"] for s in ch["idle"]["sources"]],
        "weaponLayer": ("SUPPORTED" if w["status"] == "SUPPORTED" else "BLOCKED") if w["requested"] else "NONE",
        "allowedClipProvenance": list(CLIP_PROVENANCE),
        "allowedCategories": (ACTION_PROFILES[acts["profile"]]["categories"] if acts.get("profile") else [])
                             + ["LOOP_LOCOMOTION", "STATIC_POSE"],
        "transitions": "UNITY_TRANSITION", "readiness": entity["readiness"],
    }


def controller_mode(entity):
    """ACTIONS | LOCOMOTION_ONLY | STATIC_OBJECT (D69: passive objects never walk or act in the game)."""
    ch = entity["channels"]
    if ch.get("game_usage", {}).get("policy") == "STATIC_OBJECT":
        return "STATIC_OBJECT"
    return "ACTIONS" if ch["actions"]["status"] == "AVAILABLE" else "LOCOMOTION_ONLY"


def game_usage(kind):
    """GAME USAGE channel (D69). Research data of the kind (walk formula, baked actions) is NOT
    changed; only what the GAME controller may use."""
    from tools.alpha_models.catalog import spawn_roles
    policy, basis, reason = spawn_roles.game_policy(kind)
    if policy == "STATIC_OBJECT":
        return {"policy": policy, "basis": basis, "reason": reason,
                "walk": "DISABLED_IN_GAME", "actions": "DISABLED_IN_GAME",
                "research_data": "KEPT (walk formula / baked action clips stay in the research library)"}
    return {"policy": "CREATURE", "basis": None, "reason": None, "walk": "ALLOWED", "actions": "BY_PROFILE"}


def _kept_by_visual_correction(pid, kind, prof):
    """Issue #7: an ACTIVE visual correction proposal may keep one profile of its kind, declared with the Alpha
    family it patches (``keeps_profiles``). Triggers do not depend on the corrected offsets and the poses are
    re-baked by the drawModel oracle on the corrected appearance; ``visual_correction.apply`` refuses unless the
    patched fields hold Alpha's documented values. Returns the reason note, or None."""
    from tools.alpha_models.universal import visual_correction as vc
    name = vc.current()
    if name is None:
        return None
    c = vc.get(name)
    base = c.get("keeps_profiles", {}).get(pid)
    if c["kind"] != kind or base is None or base not in prof["evidence_families"]:
        return None
    return f"{c['status']} {name}: profile {pid} kept on the corrected appearance (Alpha family {base})"


def action_profile_for(kind, seed_variant, family_id, parts, specimen_families=None):
    """(profile_id | None, status, reasons) for one entity.

    A profile applies only to its exact specimen (kind + seed) AND only while that specimen still
    resolves to the pinned evidence family: the specimen kind number with another body is refused.
    ``specimen_families`` (profile id -> family ids) defaults to the pinned ``evidence_families``."""
    seed = list(seed_variant)
    compatible = []
    for pid, prof in ACTION_PROFILES.items():
        fams = set((specimen_families or {}).get(pid, ())) | set(prof["evidence_families"])
        if any(s["kind"] == kind and list(s["seed_variant"]) == seed for s in prof["specimens"]):
            kept = _kept_by_visual_correction(pid, kind, prof)
            if family_id not in prof["evidence_families"] and not kept:
                return None, "BLOCKED", [f"SPECIMEN_APPEARANCE_DIFFERS {pid}: family {family_id} is not the "
                                         f"verified {prof['evidence_families']}"]
            missing = [p for p in prof["requires_parts"] if p not in parts and p != "weapon"]
            if missing:
                return None, "BLOCKED", [f"PROFILE_PARTS_MISSING {pid} {missing}"]
            return pid, prof["status"], ([kept] if kept and family_id not in prof["evidence_families"] else [])
        if family_id in fams:
            compatible.append(pid)
    if compatible:
        return None, "BLOCKED", [f"POSES_FAMILY_COMPATIBLE_TRIGGER_UNPROVEN {','.join(compatible)}: "
                                 "same drawModel family as a verified specimen, but this kind's "
                                 "launch context (player race/class or AI) is not evidenced"]
    return None, "BLOCKED", ["NO_VERIFIED_ACTION_TRIGGER: no action profile has evidence for this "
                             "motion family; creature AI launch rules are not reconstructed"]


def profile_family_drift(static_table):
    """Profiles whose specimen no longer resolves to the pinned evidence family (static table)."""
    out = []
    for pid, fams in specimen_families(static_table).items():
        pinned = set(ACTION_PROFILES[pid]["evidence_families"])
        if fams and fams != pinned:
            out.append(f"PROFILE_FAMILY_DRIFT {pid} static={sorted(fams)} pinned={sorted(pinned)}")
    return out


# --------------------------------------------------------------------------- structure
def body_plan_id(parts):
    body = [p for p in parts if BLOCK_ROLES.get(p) != "equipment"]
    return "plan/" + "+".join(sorted(body)) if body else "plan/empty"


def topology(parts):
    roles = collections.Counter(BLOCK_ROLES.get(p, "unknown") for p in parts)
    return {"core": roles["core"], "head": roles["head"], "head_attachment": roles["head_attachment"],
            "tail": roles["tail"], "fore_limbs": roles["fore_limb"], "hind_limbs": roles["hind_limb"],
            "mid_limbs": roles["mid_limb"], "wings": roles["wing"], "equipment": roles["equipment"],
            "unknown": roles["unknown"]}


def structural_category(parts, appearance):
    """Creature type from STRUCTURE (blocks + locomotion flags), never from its name."""
    t = topology(parts)
    if t["wings"]:
        return "WINGED"
    if t["fore_limbs"] and t["hind_limbs"]:
        return "QUADRUPED" if appearance["flags"] & 1 else "BIPED_WITH_FORE_LIMBS"
    if t["hind_limbs"]:
        return "LEGS_ONLY"
    if t["fore_limbs"]:
        return "FORE_LIMBS_ONLY"
    if t["core"] or t["head"]:
        return "LIMBLESS"
    return "EMPTY"


def locomotion_mode(appearance):
    f = appearance["flags"]
    return {"fore_limbs_walk": bool(f & 1), "airborne_capable": bool(f & 2), "flags": f}


def part_records(res, sizes=None):
    out = []
    for p in res.parts:
        b = cr.BY_NAME[p]
        idx = res.models[p]
        out.append({"name": p, "role": BLOCK_ROLES.get(p, "unknown"), "mirror_of": MIRROR_PAIRS.get(p),
                    "mirrored": b.mirrored, "slot": f"0x{b.slot:02X}", "draw_site": f"0x{b.draw_site:06X}",
                    "anchor": b.anchor, "unity_anchor": unity_anchor(b.anchor, res.kind), "block_status": b.status,
                    "anchor_evidence": b.evidence, "model_index": idx,
                    "size": (sizes or {}).get(p)})
    return out


# --------------------------------------------------------------------------- family clips on disk
def family_clips(clips_root, family_id):
    """Clips found in the family directory: name -> {status, category, loop, param, parts}."""
    out = {}
    if not clips_root:
        return out
    d = os.path.join(clips_root, family_id.replace("/", "_"))
    for path in sorted(glob.glob(os.path.join(d, "*.alphaclip.json"))):
        try:
            c = json.load(open(path, encoding="utf-8"))
        except (OSError, ValueError):
            out[os.path.basename(path)] = {"status": "BLOCKED", "reason": "CLIP_UNREADABLE"}
            continue
        out[c.get("name", os.path.basename(path))] = {
            "status": c.get("status"), "rig": c.get("rig"), "loop": c.get("loop"), "param": c.get("param"),
            "category": clip_category(c), "parts": [p["name"] for p in c.get("parts", [])],
            "rig_kind_source": c.get("rig_kind_source"),
            "capture": c.get("source", {}).get("capture"),
            "capture_sha256": c.get("source", {}).get("capture_sha256"),
            "file": os.path.basename(path)}
    return out


def clip_category(clip):
    src = clip.get("source", {})
    if src.get("category"):
        return src["category"]
    if clip.get("loop") and clip.get("param") == "walk_phase":
        return "LOOP_LOCOMOTION"
    if clip.get("param") == "time_ms" and clip.get("duration", 1) == 0:
        return "STATIC_POSE"
    if clip.get("param") == "time_ms" and not clip.get("loop"):
        return "MOTION"
    return "UNKNOWN"


# --------------------------------------------------------------------------- one entity
def specimen_families(static_table):
    """profile id -> set of family ids of its specimens (default appearance of the static table).

    The static table stores the seed (0,0) appearance; specimens with other seeds would need the
    emulator and are reported as unresolved instead of guessed."""
    out = {}
    for pid, prof in ACTION_PROFILES.items():
        fams = set()
        for s in prof["specimens"]:
            e = static_table["kinds"].get(f"0x{s['kind']:02x}")
            if e and e.get("appearance") and list(s["seed_variant"]) == [0, 0]:
                fams.add(cr.resolve(s["kind"], e["appearance"]).family_id)
        out[pid] = fams
    return out


def describe(kind, entry, static_table, clips_root=None, seed_variant=(0, 0), equipment=None,
             spec_families=None):
    """Full contract of one kind of the static table (default appearance)."""
    app = entry.get("appearance")
    k = f"0x{kind:02x}"
    if not app:
        return {"format": FORMAT, "kind": k, "name_hint": entry.get("name_hint"), "readiness": "BLOCKED",
                "reasons": ["NO_APPEARANCE in static table"], "channels": {}}
    weapon = (equipment or {}).get("weapon")
    eq = ("weapon",) if weapon else ()
    res = cr.resolve(kind, app, equipment=eq)
    registry = {int(i): n for i, n in static_table.get("registry", {}).items()}
    msizes = static_table.get("model_sizes", {})
    sizes, geometry_reasons = {}, []
    for p, idx in res.models.items():
        if isinstance(idx, int):
            name = registry.get(idx)
            if name is None:
                geometry_reasons.append(f"MODEL_INDEX_UNRESOLVED {p}:{idx}")
            elif msizes.get(name) is None:
                geometry_reasons.append(f"MODEL_NOT_IN_DB {p}:{name}")
            else:
                sizes[p] = list(msizes[name])
    hard_reasons = [x for x in res.exceptions if x.startswith("NO_PARTS")]
    research_reasons = [x for x in res.exceptions if x not in hard_reasons]
    body_parts = [p for p in res.parts if BLOCK_ROLES.get(p) != "equipment"]

    channels = {}
    # geometry / rig ------------------------------------------------------------------------
    unestablished = [p for p in res.parts if cr.BY_NAME[p].anchor == "unknown"]
    channels["geometry"] = {"status": "BLOCKED" if (geometry_reasons or unestablished or not res.parts) else "READY",
                            "reasons": geometry_reasons + [f"ANCHOR_NOT_ESTABLISHED {p}" for p in unestablished]}
    # walk ------------------------------------------------------------------------------------
    walk_cls = pw.classify(app, body_parts, kind_exceptions=res.exceptions)
    fclips = family_clips(clips_root, res.family_id)
    captured_walk = [n for n, c in fclips.items() if c.get("category") == "LOOP_LOCOMOTION"
                     and c.get("status") == "TRANSFERRED_FROM_ALPHA" and c.get("rig") == res.family_id]
    captured_idle = [n for n, c in fclips.items() if c.get("category") == "STATIC_POSE"
                     and c.get("status") == "TRANSFERRED_FROM_ALPHA" and c.get("rig") == res.family_id]
    sources = []
    if captured_walk:
        src_kind = fclips[captured_walk[0]].get("rig_kind_source")
        sources.append({"source": "family_clip", "provenance": "TRANSFERRED_FROM_ALPHA", "clips": captured_walk,
                        "rig_kind_source": src_kind, "receiver_captured": src_kind == k,
                        "capture": fclips[captured_walk[0]].get("capture"),
                        "capture_sha256": fclips[captured_walk[0]].get("capture_sha256")})
    if walk_cls["status"] == "FORMULA_VERIFIED":
        sources.append({"source": "param_walk_formula", "provenance": "RECONSTRUCTED_FROM_EVIDENCE",
                        "terms": walk_cls["terms"]})
    channels["walk"] = {"status": "AVAILABLE" if sources else "BLOCKED", "sources": sources,
                        "formula": walk_cls["status"],
                        "reasons": [] if sources else (walk_cls["reasons"] + [f"TERM_NOT_VERIFIED {t}" for t in
                                                                              walk_cls["unknown_terms"] + walk_cls["code_only_terms"]]
                                                       or ["NO_WALK_SOURCE"])}
    # idle: captured standing pose, or the formula at amplitude 0 (standing pose of the same code)
    idle_sources = []
    if captured_idle:
        idle_sources.append({"source": "family_clip", "provenance": "TRANSFERRED_FROM_ALPHA", "clips": captured_idle})
    if walk_cls["status"] == "FORMULA_VERIFIED":
        idle_sources.append({"source": "param_walk_formula_amp0", "provenance": "RECONSTRUCTED_FROM_EVIDENCE"})
    channels["idle"] = {"status": "AVAILABLE" if idle_sources else "BLOCKED", "sources": idle_sources,
                        "reasons": [] if idle_sources else ["NO_IDLE_SOURCE"]}
    # actions -------------------------------------------------------------------------------
    if spec_families is None:
        spec_families = specimen_families(static_table)
    pid, astatus, areasons = action_profile_for(kind, seed_variant, res.family_id, res.parts, spec_families)
    channels["actions"] = {"status": "AVAILABLE" if pid else "BLOCKED", "profile": pid,
                           "provenance": astatus if pid else "BLOCKED",
                           "trigger": ACTION_PROFILES[pid]["trigger"] if pid else None, "reasons": areasons}
    # equipment -----------------------------------------------------------------------------
    wreasons = weapon_layer_support(res.parts, app, weapon["subtype"] if weapon else None)
    channels["equipment"] = {"weapon": {"status": "SUPPORTED" if not wreasons else "BLOCKED",
                                        "requested": bool(weapon), "reasons": wreasons,
                                        "verified_subtypes": list(VERIFIED_WEAPON_SUBTYPES)}}
    if weapon and wreasons:
        research_reasons.append("WEAPON_LAYER_NOT_SUPPORTED " + "; ".join(wreasons))
    # visibility / lifecycle ----------------------------------------------------------------
    channels["visibility"] = {"modes": list(VISIBILITY_MODES),
                              "dynamic_parts": "per clip: EXPLICIT_NATIVE_DRAW masks; unknown draw site -> FAIL"}
    channels["game_usage"] = game_usage(kind)
    channels["lifecycle"] = {"status": "AVAILABLE" if pid else "NOT_APPLICABLE",
                             "input_profile": pid and ACTION_PROFILES[pid]["trigger_scope"],
                             "transitions": "UNITY_TRANSITION"}

    # readiness -----------------------------------------------------------------------------
    if hard_reasons or (channels["geometry"]["status"] == "BLOCKED" and not research_reasons):
        readiness, reasons = "BLOCKED", hard_reasons + channels["geometry"]["reasons"]
    elif research_reasons or channels["geometry"]["status"] == "BLOCKED":
        readiness, reasons = "NEEDS_RESEARCH", research_reasons + channels["geometry"]["reasons"]
    elif channels["walk"]["status"] != "AVAILABLE":
        readiness, reasons = "NEEDS_RESEARCH", channels["walk"]["reasons"]
    else:
        readiness, reasons = "AUTO_READY", []
    return {
        "format": FORMAT, "kind": k, "name_hint": entry.get("name_hint"), "seed_variant": list(seed_variant),
        "category": structural_category(res.parts, app), "body_plan": body_plan_id(res.parts),
        "topology": topology(res.parts), "locomotion_mode": locomotion_mode(app),
        "family_id": res.family_id, "parts": part_records(res, sizes),
        "model_variants": max([1] + [len(v) for v in entry.get("slot_variants", {}).values()]),
        "channels": channels, "readiness": readiness, "reasons": reasons,
    }


# --------------------------------------------------------------------------- clip checks
def validate_clip(clip, family_id, parts, optional_layers=()):
    """Contract checks of one clip against an entity (rig, parts, provenance, category, masks).

    ``parts`` are the body blocks the clip must animate; ``optional_layers`` are equipment blocks a
    clip MAY add (e.g. the weapon layer of a body plan that supports it), never others."""
    problems = []
    name = clip.get("name", "<unnamed>")
    if clip.get("rig") != family_id:
        problems.append(f"CLIP_RIG_MISMATCH {name} {clip.get('rig')} != {family_id}")
    st = clip.get("status")
    if st not in CLIP_PROVENANCE:
        problems.append(f"CLIP_PROVENANCE_NOT_ALLOWED {name} {st}")
    cat = clip_category(clip)
    if cat not in CLIP_CATEGORIES:
        problems.append(f"CLIP_CATEGORY_UNKNOWN {name} {cat}")
    cparts = [p["name"] for p in clip.get("parts", [])]
    body = [p for p in cparts if p not in optional_layers]
    if sorted(body) != sorted(parts) or len(set(cparts)) != len(cparts):
        problems.append(f"CLIP_PART_SET_MISMATCH {name} {cparts} != {parts} (+ optional {list(optional_layers)})")
    vis = clip.get("source", {}).get("visibility_mode", "STATIC_ALL")
    if vis not in VISIBILITY_MODES:
        problems.append(f"CLIP_VISIBILITY_MODE_UNKNOWN {name} {vis}")
    for i, k in enumerate(clip.get("keys", [])):
        m = k.get("m", {})
        if vis == "EXPLICIT_NATIVE_DRAW":
            v = k.get("visible")
            if not isinstance(v, list) or len(v) != len(cparts):
                problems.append(f"CLIP_MASK_SHAPE {name} key={i}")
                break
            expect = {p for p, on in zip(cparts, v) if on}
            if set(m) != expect:
                problems.append(f"CLIP_MASK_MATRIX_MISMATCH {name} key={i}")
                break
        elif set(m) != set(cparts):
            problems.append(f"CLIP_KEY_PARTS_INCOMPLETE {name} key={i}")
            break
    return problems
