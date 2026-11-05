"""Universal Alpha creature rig: draw blocks, rig resolution and motion families.

Finding behind this module (universal pipeline research, 2026-11-05): Alpha has ONE animation
routine for every creature, ``Creature_drawModel`` (VA 0x4128F0). A creature does not carry a
skeleton; it carries an *appearance* (``creature+0x78``, 0xAC bytes) with eight model slots
(head, hair, hand, foot, body, tail, two more) plus part scales, angles and offsets. Each part
is drawn by a fixed *draw block* of drawModel (one ``render_uploadModelViewMatrices`` +
``GameController_draw_mesh`` pair). Hands and feet blocks run twice (left/right; the right hand
mirrored). Therefore:

* the PART SET of a creature = the draw blocks of its non-empty slots (+ equipment);
* the ANCHOR rule of a part belongs to the draw block, not to the creature;
* the JOINT motion depends only on the appearance parameters (not on model sizes, because model
  sizes enter drawModel only through the final anchor translate) and on the creature's runtime
  state (walk phase/amplitude, action, state, timers, equipment);
* creatures whose part set and motion parameters are equal have equal joints, so a clip
  captured on one of them is valid for all of them (a *motion family*).

Evidence levels used below:
  VERIFIED  — draw block seen in a runtime capture with the predicted slot AND the anchor read
              from the block's code;
  CODE_ONLY — anchor read from code, block not yet seen in a capture;
  UNVERIFIED — block exists (call site found) but its slot/anchor is not established.
"""
from __future__ import annotations

import hashlib
import json
from dataclasses import dataclass

from tools.alpha_models.universal import alpha_static as st


@dataclass(frozen=True)
class DrawBlock:
    name: str           # universal part name (block name, not anatomy: a cat's front paw is hand_l)
    draw_site: int      # VA of `call GameController_draw_mesh`
    upload_site: int    # VA of `call render_uploadModelViewMatrices`
    slot: int           # drawModel self index of the model pointer (mesh == self[slot])
    anchor: str         # anchor rule id (see anchor())
    mirrored: bool
    status: str         # VERIFIED | CODE_ONLY | UNVERIFIED
    evidence: str


BLOCKS = (
    DrawBlock("body", 0x41FD96, 0x41FD6F, 0x31, "center", False, "VERIFIED",
              "GameController.cpp:6108 translate(-size/2); captured: human, cat 0x20, plainrunner 0x3f"),
    DrawBlock("tail", 0x42042E, 0x42041F, 0x35, "tail", False, "VERIFIED",
              "GameController.cpp:6212-6219 translate(-sx/2,-sy,-sz/2) after scale(tail)*scale(body); "
              "captured: cat 0x20"),
    DrawBlock("head", 0x4206C2, 0x4206B7, 0x2F, "head", False, "VERIFIED",
              "GameController.cpp:6256-6270 translate(-sx/2,-sy/2,-sz/2); kind 0x65: -sy*0.2; "
              "captured: human, cat 0x20"),
    DrawBlock("hair", 0x420A38, 0x420A2D, 0x30, "center", False, "VERIFIED",
              "GameController.cpp:6327; captured: human (matrix == head)"),
    DrawBlock("weapon", 0x421A01, 0x4219DE, 0x37, "weapon", False, "VERIFIED",
              "GameController.cpp:6528-6663 (humanoid_rig.weapon_anchor); captured: human"),
    DrawBlock("hand_l", 0x423214, 0x4231F1, 0x33, "center", False, "VERIFIED",
              "GameController.cpp:7162 scale(s) translate(-size/2); captured: human, cat 0x20"),
    DrawBlock("hand_r", 0x4235B1, 0x4235A6, 0x33, "center", True, "VERIFIED",
              "GameController.cpp:7229 scale(-s,s,s) translate(-size/2); captured: human, cat 0x20"),
    DrawBlock("foot_l", 0x423AFA, 0x423AD7, 0x36, "center", False, "VERIFIED",
              "GameController.cpp:7337 translate(-size/2); captured: human, cat 0x20, plainrunner 0x3f"),
    DrawBlock("foot_r", 0x423D1F, 0x423CFC, 0x36, "center", False, "VERIFIED",
              "GameController.cpp:7373 translate(-size/2); captured: human, cat 0x20, plainrunner 0x3f"),
    # call sites whose slot is read in the block prologue (mov ecx,[edi+slot*4]) but never seen
    # drawn in a capture yet: their anchors are NOT established -> such creatures are queued.
    DrawBlock("neck_item", 0x4200EF, 0x4200C8, 0x3A, "unknown", False, "UNVERIFIED", "slot 0x3a (equipment +0x418)"),
    DrawBlock("shoulder_item", 0x42028C, 0x420265, 0x39, "unknown", False, "UNVERIFIED", "slot 0x39 (equipment +0x878)"),
    DrawBlock("slot98_a", 0x420CD2, 0x420CC3, 0x32, "unknown", False, "UNVERIFIED", "slot 0x32 (appearance +0x20)"),
    DrawBlock("slot98_b", 0x420DE1, 0x420DD2, 0x32, "unknown", False, "UNVERIFIED", "slot 0x32 (appearance +0x20)"),
    DrawBlock("offhand_a", 0x421DFA, 0x421DD7, 0x38, "unknown", False, "UNVERIFIED", "slot 0x38 (equipment +0x990)"),
    DrawBlock("offhand_b", 0x42231B, 0x4222F8, 0x38, "unknown", False, "UNVERIFIED", "slot 0x38 (equipment +0x990)"),
    DrawBlock("slot9a_a", 0x42377A, 0x42376B, 0x34, "unknown", False, "UNVERIFIED", "slot 0x34 (appearance +0x22)"),
    DrawBlock("slot9a_b", 0x4238C5, 0x4238B6, 0x34, "unknown", False, "UNVERIFIED", "slot 0x34 (appearance +0x22)"),
)
BY_DRAW_SITE = {b.draw_site: b for b in BLOCKS}
BY_NAME = {b.name: b for b in BLOCKS}

# appearance slot -> draw blocks it produces (order = drawModel draw order)
SLOT_BLOCKS = {
    "body": ("body",), "tail": ("tail",), "head": ("head",), "hair": ("hair",),
    "hand": ("hand_l", "hand_r"), "foot": ("foot_l", "foot_r"),
    "slot_98": ("slot98_a", "slot98_b"), "slot_9a": ("slot9a_a", "slot9a_b"),
}
EQUIPMENT_BLOCKS = {"weapon": ("weapon",), "offhand": ("offhand_a", "offhand_b"),
                    "shoulder": ("shoulder_item",), "neck": ("neck_item",)}
DRAW_ORDER = [b.name for b in sorted(BLOCKS, key=lambda b: b.draw_site)]


def anchor(rule, size, kind=None, on_back=None, subtype=None):
    """Grid point that sits on the joint (row-vector maths: M = T(-anchor) * J * P)."""
    sx, sy, sz = size
    if rule == "center":
        return (sx / 2.0, sy / 2.0, sz / 2.0)
    if rule == "tail":
        return (sx / 2.0, float(sy), sz / 2.0)
    if rule == "head":
        if kind == 0x65:
            return (sx / 2.0, sy * 0.2, sz / 2.0)
        return (sx / 2.0, sy / 2.0, sz / 2.0)
    if rule == "weapon":
        from tools.alpha_models.anim import humanoid_rig
        if on_back is None:
            raise ValueError("weapon anchor needs on_back")
        return humanoid_rig.weapon_anchor(size, on_back, subtype)
    raise ValueError(f"anchor rule {rule!r} is not established")


@dataclass
class RigResolution:
    kind: int
    parts: list            # block names in draw order
    models: dict           # block name -> registry index (the slot value), or 'equipment:<x>'
    family_id: str
    motion_params: dict
    exceptions: list       # reasons this kind cannot be processed automatically
    notes: list

    @property
    def supported(self):
        return not self.exceptions

    def to_json(self):
        return {"kind": f"0x{self.kind:02x}", "parts": self.parts, "models": self.models,
                "family_id": self.family_id, "supported": self.supported,
                "exceptions": self.exceptions, "notes": self.notes}


def family_signature(parts, motion_params, kind):
    """Equal signature <=> drawModel computes equal joint frames for equal runtime state."""
    sig = {"parts": sorted(parts), "motion": motion_params,
           "kind_branch": st.KIND_SPECIAL_BRANCHES.get(kind) and kind}
    return json.dumps(sig, sort_keys=True)


def family_id(parts, motion_params, kind):
    h = hashlib.sha1(family_signature(parts, motion_params, kind).encode()).hexdigest()[:10]
    return f"alpha_creature/{h}"


def resolve(kind, appearance, equipment=()):
    """Rig of one creature from its decoded appearance (static) and optional equipment slots."""
    parts, models, exceptions, notes = [], {}, [], []
    for slot_name, blocks in SLOT_BLOCKS.items():
        idx = appearance["slots"].get(slot_name, -1)
        if idx is None or idx < 0:
            continue
        for b in blocks:
            parts.append(b)
            models[b] = idx
    for eq in equipment:
        for b in EQUIPMENT_BLOCKS[eq]:
            parts.append(b)
            models[b] = f"equipment:{eq}"
    parts.sort(key=DRAW_ORDER.index)
    for p in parts:
        blk = BY_NAME[p]
        if blk.status == "UNVERIFIED":
            exceptions.append(f"UNVERIFIED_DRAW_BLOCK {p} (slot 0x{blk.slot:02x}): anchor/slot not established")
    if kind in st.KIND_SPECIAL_BRANCHES:
        exceptions.append(f"KIND_SPECIFIC_CODE 0x{kind:02x}: {st.KIND_SPECIAL_BRANCHES[kind]}")
    if not parts:
        exceptions.append("NO_PARTS appearance has no model slot")
    if appearance["slots"].get("hair", -1) >= 0 and appearance["slots"].get("head", -1) < 0:
        notes.append("hair without head")
    mp = st.motion_params(appearance)
    # the motion family is a property of the creature's BODY (appearance); equipment blocks are a
    # layer on top (weapon on the back / in the hand) and do not change the body joints
    body_parts = [p for p in parts if not str(models[p]).startswith("equipment:")]
    return RigResolution(kind, parts, models, family_id(body_parts, mp, kind), mp, exceptions, notes)


def predicted_blocks_match(resolution, captured_sites):
    """Compare the statically predicted part set with the draw sites of a capture record."""
    captured = []
    unknown = []
    for site in captured_sites:
        b = BY_DRAW_SITE.get(site)
        if b is None:
            unknown.append(f"0x{site:06X}")
        else:
            captured.append(b.name)
    return {"predicted": resolution.parts, "captured": captured, "unknown_sites": unknown,
            "match": sorted(captured) == sorted(resolution.parts) and not unknown}
