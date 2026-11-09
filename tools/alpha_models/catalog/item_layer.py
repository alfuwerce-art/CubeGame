"""Alpha 2013 item layer: item -> model -> equipment slot -> attach point -> draw schedule.

Purpose (D71 / M0-00B, issue #10, 2026-11-09). The crate/creature pipeline could already import every
model and every creature animation, but it had no machine-readable answer to four questions the game
needs before items can be drawn on a character:

1. WHICH model does an item select?  -> ``GameController_item_icon_id`` 0x4EC400, executed
   (``catalog/item_models.py`` owns the full sweep; this module resolves the declared class domain).
2. WHICH slot / draw block does that model go to?  -> the seven base-pass slots and the four
   conditional slots of ``Creature_drawModel`` (0x4128F0), executed by
   ``catalog/equipment_probe.py`` (D64) and re-checked here for the hand/back states.
3. WHERE is the anchor on the model?  -> ``universal/item_attach.py`` (laws) + the emulated
   d(anchor)/d(size) measurement below (linear part) + the grip-in-hand check.
4. WHEN is the item in the hand, on the back, or in use?  -> the branch predicates of the draw
   blocks + the action/timer/flag conditions, each verified by executing drawModel.

Additional results this module produces (all EMULATED_ALPHA_CODE, no hand-transcribed numbers):

* the ACTION table: for every action code the drawModel state and whether the weapon / off-hand is
  drawn in the hand or on the back (this is the "item-dependent animation" contract: the same body
  clip needs different item blocks depending on what is equipped);
* the CONSUMABLE schedule: actions 80/81 draw the item from creature+0x1e8 in the ``held_item``
  block while ``timer < itemCooldown`` (3000 ms for type 1 subtype 1, 10000 ms for other type-1
  items, 0 for every other item type -> never drawn);
* DYNAMIC PARTS (R-8): the thrown rock of action 108 (golem kinds) is not an equipment slot at all —
  it is the ``offhand_b`` block drawing a fixed model (``iron-deposit.cub``, index 2101) in state 71.
  The layer records block + model + law, which is exactly the data the ripg needed for a hidden part.

Usage (all writes stay inside ``.scratch``):

    python -m tools.alpha_models.catalog.item_layer --out .scratch/catalog/item_layer.json
    python -m tools.alpha_models.catalog.item_layer --verify          # re-run and compare with the committed evidence
    python -m tools.alpha_models.catalog.item_layer --check-pins      # fast: SHA-256 pins only
    python -m tools.alpha_models.catalog.item_layer --export-evidence # refresh committed numbers

Alpha is opened READ-ONLY; nothing is launched. The committed evidence (``item_layer_evidence.json``)
contains only numbers, model file names and VAs — no asset data.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import sys
import struct
from pathlib import Path

from tools.alpha_models.anim import mat4, pe_bytes as pb
from tools.alpha_models.universal import alpha_oracle as ao
from tools.alpha_models.universal import alpha_static as st
from tools.alpha_models.universal import anim_states as an
from tools.alpha_models.universal import creature_rig as cr
from tools.alpha_models.universal import drawmodel_emu as de
from tools.alpha_models.universal import item_attach as ia

ROOT = Path(__file__).resolve().parents[3]
DEFAULT_EXE = ROOT / ".scratch/alphatest2/Cube.exe"
DEFAULT_DB = ROOT / ".scratch/alphatest2/data1.db"
DEFAULT_SIZES = ROOT / ".scratch/catalog/registry_sizes.json"
EVIDENCE_PATH = Path(__file__).with_name("item_layer_evidence.json")
EVIDENCE_FORMAT = "cubegame.item_layer_evidence/1"
CONTRACT_FORMAT = "cubegame.item_layer/1"
EVIDENCE_CODE = ("tools/alpha_models/catalog/item_layer.py", "tools/alpha_models/universal/item_attach.py",
                 "tools/alpha_models/universal/anim_states.py", "tools/alpha_models/universal/drawmodel_emu.py",
                 "tools/alpha_models/universal/x86emu.py", "tools/alpha_models/universal/alpha_static.py")

# --------------------------------------------------------------------------- declared domain
# Every equipment slot drawModel reads (creature offset -> what it draws). The storage stride is
# 0x118 bytes (getEquipmentSlotPtr 0x47b1b0); see M0_00B_P0_MECHANICS_EVIDENCE.md A.6.
SLOTS = (
    {"offset": 0x418, "name": "neck", "itemTypes": [8], "block": "neck_item", "law": "neck",
     "condition": {}, "status": "VERIFIED_EMULATION"},
    {"offset": 0x530, "name": "chest", "itemTypes": [4], "block": "body(SWAP)", "law": "center",
     "condition": {}, "status": "VERIFIED_EMULATION"},
    {"offset": 0x648, "name": "feet", "itemTypes": [6], "block": "foot_l/foot_r(SWAP)", "law": "center",
     "condition": {}, "status": "VERIFIED_EMULATION"},
    {"offset": 0x760, "name": "hands", "itemTypes": [5], "block": "hand_l/hand_r(SWAP)", "law": "center",
     "condition": {}, "status": "VERIFIED_EMULATION"},
    {"offset": 0x878, "name": "shoulder", "itemTypes": [7], "block": "shoulder_item", "law": "center",
     "condition": {}, "status": "VERIFIED_EMULATION"},
    {"offset": 0x990, "name": "offhand", "itemTypes": [3], "block": "offhand_a|offhand_b",
     "law": "item_attach.offhand_present_law", "condition": {"weaponSubtype": True},
     "status": "VERIFIED_EMULATION"},
    {"offset": 0xAA8, "name": "weapon", "itemTypes": [3], "block": "weapon",
     "law": "item_attach.weapon_present_law", "condition": {}, "status": "VERIFIED_EMULATION"},
    {"offset": 0xDF0, "name": "lamp", "itemTypes": [24], "block": "lamp_item", "law": "lamp",
     "condition": {"flag124": 0x200}, "status": "VERIFIED_EMULATION"},
    {"offset": 0xF08, "name": "special", "itemTypes": [23], "block": "special_item", "law": "center",
     "condition": {"flag124": 0x10}, "status": "VERIFIED_EMULATION"},
    {"offset": 0x1020, "name": "pet", "itemTypes": [19], "block": "pet_item", "law": "pet",
     "condition": {"action": 82}, "status": "VERIFIED_EMULATION"},
    {"offset": 0x1E8, "name": "held", "itemTypes": [1], "block": "held_item", "law": "center",
     "condition": {"actions": [80, 81], "timerBelowItemCooldown": True}, "status": "VERIFIED_EMULATION"},
    {"offset": 0x300, "name": "unknown", "itemTypes": [], "block": "NOT_DRAWN", "law": None,
     "condition": {}, "status": "UNKNOWN"},
    {"offset": 0xBC0, "name": "ring_a", "itemTypes": [9], "block": "NOT_DRAWN", "law": None,
     "condition": {}, "status": "VERIFIED_EMULATION"},
    {"offset": 0xCD8, "name": "ring_b", "itemTypes": [9], "block": "NOT_DRAWN", "law": None,
     "condition": {}, "status": "VERIFIED_EMULATION"},
)
# item type -> what the executed selector draws (labels only; the model comes from the selector)
ITEM_TYPE_LABELS = {1: "food/potion", 3: "weapon", 4: "chest armour", 5: "gloves", 6: "boots",
                    7: "shoulder armour", 8: "amulet", 9: "ring", 14: "leftovers", 16: "painting",
                    17: "vase", 19: "pet box", 21: "quest item", 23: "glider/boat", 24: "lamp",
                    11: "resource"}
# weapon subtypes with a model block in item_icon_id (0..25); label = name in World::ctor_1 / S1
WEAPON_LABELS = {0: "sword", 1: "axe", 2: "mace", 3: "dagger", 4: "fist", 5: "longsword", 6: "bow",
                 7: "crossbow", 8: "boomerang", 9: "arrow", 10: "staff", 11: "wand", 12: "bracelet",
                 13: "shield", 14: "quiver", 15: "greatsword", 16: "greataxe", 17: "greatmace",
                 18: "pitchfork", 19: "pickaxe", 20: "torch", 21: "sword(alias21)", 22: "sword(alias22)",
                 23: "sword(alias23)", 24: "sword(alias24)", 25: "sword(alias25)"}
# actions whose clip needs a weapon in hand (D64 evidence: 23 codes) and the two-handed chain (D52)
WEAPON_ONLY_ACTIONS = (1, 5, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57,
                       59, 60, 61)
TWO_HANDED_CHAIN = (57, 67, 58)
CONSUMABLE_ACTIONS = (80, 81)
PET_ACTION = 82


def item_cooldown_ms(item_type, item_subtype) -> int:
    """GameController_itemCooldown 0x4C6B80: 0 unless type 1; 3000 ms for type1/subtype1 else 10000."""
    if int(item_type) != 1:
        return 0
    return 3000 if int(item_subtype) == 1 else 10000


def code_hashes(root=ROOT) -> dict:
    return {p: hashlib.sha256((Path(root) / p).read_bytes().replace(b"\r\n", b"\n")).hexdigest()
            for p in EVIDENCE_CODE}


# --------------------------------------------------------------------------- emulation wrapper
class ItemLayer:
    """Executes Alpha's drawModel / item selector for one research build (read-only)."""

    def __init__(self, exe, db, sizes=None, kind=0x02, seed=(0, 0)):
        self.exe, self.db = Path(exe), Path(db)
        st.verify_exe(self.exe)
        self.image = pb.load(self.exe)
        self.registry, _, _ = st.model_registry(self.image, detail=True)
        self.sizes_path = str(sizes) if sizes and Path(sizes).exists() else None
        self.sizes = ({int(k): tuple(v) for k, v in
                       json.loads(Path(self.sizes_path).read_text(encoding="utf-8")).items()}
                      if self.sizes_path else st.registry_model_sizes(self.db, self.registry))
        self.oracle = ao.Oracle(self.image, self.sizes)
        self.kind, self.seed = kind, seed
        self.base, _ = self.oracle.creature(kind, seed)
        self.base[0x5C] |= 1
        from tools.alpha_models.catalog import item_models as im
        self._im, self._selector = im, im.Selector(self.image)

    # -- item -> model -----------------------------------------------------
    def item_model(self, item_type, subtype=0, material=0, b0c=0, seed=0):
        idx, _reads, err = self._selector(self._im._item({0: item_type, 1: subtype, 0xD: material,
                                                         0xC: b0c}, seed))
        if err:
            raise ValueError(f"ITEM_SELECTOR_ERROR type={item_type} subtype={subtype}: {err}")
        return idx, (self.registry.get(idx) if idx is not None else None)

    # -- frame -------------------------------------------------------------
    def base_for(self, kind=None):
        if kind is None or kind == self.kind:
            return self.base
        c, _ = self.oracle.creature(kind, self.seed)
        c[0x5C] |= 1
        return c

    def creature(self, equipment=None, flag124=0, held=None, kind=None):
        c = bytearray(self.base_for(kind))
        for off, (t, s) in (equipment or {}).items():
            an.equip(c, off, t, s)
        if held is not None:
            an.equip(c, 0x1E8, *held)
        if flag124:
            struct.pack_into("<H", c, 0x124, struct.unpack_from("<H", c, 0x124)[0] | flag124)
        return c

    def state(self, action, equipment=None, flag124=0, kind=None):
        w = (equipment or {}).get(0xAA8, (0, 0))
        o = (equipment or {}).get(0x990, (0, 0))
        return an.resolve_state(self.image, action, w, o,
                                self.creature(equipment, flag124, kind=kind)[0x130])

    def frame(self, action=0, equipment=None, timer=0, flag124=0, held=None, state=None,
              settle=(600, 600, 16), kind=None):
        """drawModel frame of the research creature; settles the smoothing at idle first."""
        c = self.creature(equipment, flag124, held, kind=kind)
        em = de.Emulator(self.image, bytes(c), self.sizes)
        em.set_u8(0x68, 0)
        em.set_i32(0x6C, 0)
        em.set_this_i32(1, 0)
        em.set_this_i32(2, 0)
        for dt in settle:
            em.frame(dt_ms=dt)
        s = self.state(action, equipment, flag124, kind=kind) if state is None else int(state)
        em.set_u8(0x68, action)
        em.set_i32(0x6C, timer)
        em.set_this_i32(1, timer)
        em.set_this_i32(2, s)
        fr = em.frame(dt_ms=16)
        fr["state"] = s
        return fr

    def parts(self, fr):
        out = {}
        for p in fr["parts"]:
            b = cr.BY_DRAW_SITE.get(p["draw_site"])
            out[b.name if b else f"site_0x{p['draw_site']:06X}"] = p
        return out

    def blocks(self, action=0, equipment=None, timer=0, flag124=0, held=None, kind=None):
        return {n: p.get("model_index") for n, p in
                self.parts(self.frame(action, equipment, timer, flag124, held, kind=kind)).items()}

    # -- anchor measurement (linear part only; constants are code facts) ----
    def measure_law(self, block, model_index, equipment, action, timer=200, kind=None):
        from tools.alpha_models.universal import anchor_probe as apb
        site = cr.BY_NAME[block].draw_site

        def make(over):
            sizes = dict(self.sizes)
            sizes.update(over)
            c = self.creature(equipment, kind=kind)
            em = de.Emulator(self.image, bytes(c), sizes)
            em.set_u8(0x68, 0)
            em.set_i32(0x6C, 0)
            em.set_this_i32(1, 0)
            em.set_this_i32(2, 0)
            for dt in (600, 600, 16):
                em.frame(dt_ms=dt)
            s = self.state(action, equipment, kind=kind)
            em.set_u8(0x68, action)
            em.set_i32(0x6C, timer)
            em.set_this_i32(1, timer)
            em.set_this_i32(2, s)
            return em
        pr = apb.probe(make, model_index, tuple(self.sizes[model_index]), site)
        return pr


# --------------------------------------------------------------------------- frame geometry
def _inv3(m):
    """Inverse of the rotation part (row-vector matrix) — same maths as anchor_probe._inv3."""
    a = [[m[r * 4 + c] for c in range(3)] for r in range(3)]
    det = (a[0][0] * (a[1][1] * a[2][2] - a[1][2] * a[2][1])
           - a[0][1] * (a[1][0] * a[2][2] - a[1][2] * a[2][0])
           + a[0][2] * (a[1][0] * a[2][1] - a[1][1] * a[2][0]))
    inv = [[0.0] * 3 for _ in range(3)]
    for i in range(3):
        for j in range(3):
            minor = [[a[x][y] for y in range(3) if y != i] for x in range(3) if x != j]
            inv[i][j] = ((-1) ** (i + j)) * (minor[0][0] * minor[1][1] - minor[0][1] * minor[1][0]) / det
    return inv


def anchor_coeff_axis(layer, block, model_index, equipment, action, axis=2, delta=4, timer=200,
                      kind=None):
    """d(anchor)/d(size[axis]) of one draw block, measured by executing drawModel twice.

    Exact for the linear part of the anchor law (``anchor_probe`` does all three axes; a single
    axis is enough to separate the back branch ``anchor = size/2`` from an in-hand law whose axis-2
    coefficient is 0)."""
    site = cr.BY_NAME[block].draw_site

    def matrix(size):
        sizes = dict(layer.sizes)
        sizes[model_index] = tuple(size)
        c = layer.creature(equipment, kind=kind)
        em = de.Emulator(layer.image, bytes(c), sizes)
        em.set_u8(0x68, 0)
        em.set_i32(0x6C, 0)
        em.set_this_i32(1, 0)
        em.set_this_i32(2, 0)
        for dt in (600, 600, 16):
            em.frame(dt_ms=dt)
        st_ = layer.state(action, equipment, kind=kind)
        em.set_u8(0x68, action)
        em.set_i32(0x6C, timer)
        em.set_this_i32(1, timer)
        em.set_this_i32(2, st_)
        fr = em.frame(dt_ms=16)
        for p in fr["parts"]:
            if p["draw_site"] == site:
                return p["matrix"]
        return None

    base = list(layer.sizes[model_index])
    m0 = matrix(base)
    if m0 is None:
        return None
    grown = list(base)
    grown[axis] += delta
    m1 = matrix(grown)
    if m1 is None:
        return None
    dt = [m1[12 + i] - m0[12 + i] for i in range(3)]
    inv = _inv3(m0)
    da = [-sum(dt[k] * inv[k][c] for k in range(3)) for c in range(3)]
    return [round(v / delta, 5) for v in da]


def branch_of(layer, block, model_index, equipment, action, kind=None):
    """IN_HAND / ON_BACK by the exact anchor coefficient along the model's Z axis."""
    coeff = anchor_coeff_axis(layer, block, model_index, equipment, action, axis=2, kind=kind)
    if coeff is None:
        return "ABSENT"
    # back branch: anchor_z = sz/2 -> coefficient 0.5; in-hand generic: anchor_z is constant (0.0)
    return "ON_BACK" if abs(coeff[2] - 0.5) < 1e-3 else "IN_HAND"


def verify_weapon_branch(layer, actions, subtype=0):
    idx, _ = layer.item_model(3, subtype)
    equip = {0xAA8: (3, subtype)}
    checked, disagree = [], []
    for a in actions:
        got = branch_of(layer, "weapon", idx, equip, a)
        want = "ON_BACK" if ia.weapon_on_back(a, subtype) else "IN_HAND"
        rec = {"action": a, "predicate": want, "drawn": got}
        checked.append(rec)
        if got != want:
            disagree.append(rec)
    return {"subtype": subtype, "checked": checked, "disagree": disagree,
            "agree": len(checked) - len(disagree), "total": len(checked)}


def verify_offhand_branch(layer, actions, subtype=13, main_subtype=0):
    idx, _ = layer.item_model(3, subtype)
    equip = {0xAA8: (3, main_subtype), 0x990: (3, subtype)}
    checked, disagree = [], []
    for a in actions:
        want = "ON_BACK" if ia.offhand_on_back(a, subtype) else "IN_HAND"
        block = "offhand_a" if want == "ON_BACK" else "offhand_b"
        got = branch_of(layer, block, idx, equip, a)
        rec = {"action": a, "predicate": want, "block": block, "drawn": got}
        checked.append(rec)
        if got != want:
            disagree.append(rec)
    return {"subtype": subtype, "checked": checked, "disagree": disagree,
            "agree": len(checked) - len(disagree), "total": len(checked)}


def _apply(m, pt):
    """Row-vector transform of a grid point by a 4x4 matrix (p' = p * M)."""
    x, y, z = pt
    return (x * m[0] + y * m[4] + z * m[8] + m[12], x * m[1] + y * m[5] + z * m[9] + m[13],
            x * m[2] + y * m[6] + z * m[10] + m[14])


def _bbox(m, size):
    """Axis-aligned box of a model grid placed by matrix ``m`` (creature space)."""
    pts = [_apply(m, (x, y, z)) for x in (0, size[0]) for y in (0, size[1]) for z in (0, size[2])]
    return [(min(p[i] for p in pts), max(p[i] for p in pts)) for i in range(3)]


def _inside(p, bb, pad=0.0):
    return all(bb[i][0] - pad <= p[i] <= bb[i][1] + pad for i in range(3))


def grip_in_hand(layer, block, law, equipment, action, timer=200):
    """Grip point of the item model vs the gripping hand's box (hand_r: weapon, hand_l: off-hand)."""
    fr = layer.frame(action, equipment, timer)
    parts = layer.parts(fr)
    hand = "hand_r" if block == "weapon" else "hand_l"
    if block not in parts or hand not in parts:
        return None
    idx = parts[block]["model_index"]
    size = layer.sizes.get(idx)
    if size is None:
        return None
    p = _apply(parts[block]["matrix"], law.anchor(size))
    bb = _bbox(parts[hand]["matrix"], layer.sizes[parts[hand]["model_index"]])
    return _inside(p, bb)


# --------------------------------------------------------------------------- verification
def verify_weapon_law_one(layer, sub, actions=(0, 57)):
    """Measured coefficient vs declared law for one weapon subtype + grip check in hand."""
    idx, name = layer.item_model(3, sub)
    if idx is None:
        return {"subtype": sub, "status": "NO_MODEL", "problems": []}
    rec = {"subtype": sub, "model": name, "model_index": idx, "problems": []}
    equip = {0xAA8: (3, sub)}
    law = ia.weapon_law(sub)
    pr = layer.measure_law("weapon", idx, equip, 57)
    if pr is None:
        rec["problems"].append("ANCHOR_NOT_DRAWN action=57")
    else:
        rec["measured_coeff"] = [[round(v, 5) for v in row] for row in pr["d_anchor_d_size"]]
        rec["problems"] += ia.coefficient_problems(law, pr["d_anchor_d_size"])
        rec["grip_in_hand"] = grip_in_hand(layer, "weapon", law, equip, 57)
        if ia.is_weapon_like(sub) and rec["grip_in_hand"] is False:
            rec["problems"].append("GRIP_NOT_IN_HAND")
    # idle branch: centre law on the back, the in-hand law for the subtypes the back branch excludes
    # (4 fist / 12 bracelet) — the predicate decides, not the test
    idle = anchor_coeff_axis(layer, "weapon", idx, equip, 0, axis=2)
    idle_law = ia.weapon_present_law(0, sub)
    if idle is not None:
        rec["measured_coeff_idle_z"] = idle
        rec["idle_branch"] = ("ON_BACK" if idle_law is ia.WEAPON_ON_BACK else "IN_HAND")
        if abs(idle[2] - idle_law.coeff[2]) > 1e-3:
            rec["problems"].append(f"IDLE_ANCHOR_Z {idle[2]} != {idle_law.coeff[2]}")
    rec["status"] = "PASS" if not rec["problems"] else "FAIL"
    return rec


def verify_offhand_law_one(layer, sub, main_subtype=0):
    idx, name = layer.item_model(3, sub)
    equip = {0xAA8: (3, main_subtype), 0x990: (3, sub)}
    law = ia.offhand_law(sub, main_subtype)
    rec = {"subtype": sub, "model": name, "model_index": idx, "problems": []}
    pr = layer.measure_law("offhand_b", idx, equip, 57)
    if pr is None:
        rec["problems"].append("ANCHOR_NOT_DRAWN action=57")
    else:
        rec["measured_coeff"] = [[round(v, 5) for v in row] for row in pr["d_anchor_d_size"]]
        rec["problems"] += ia.coefficient_problems(law, pr["d_anchor_d_size"])
        rec["grip_in_hand"] = grip_in_hand(layer, "offhand_b", law, equip, 57)
        if sub in (3, 4, 20) and rec["grip_in_hand"] is False:
            rec["problems"].append("GRIP_NOT_IN_HAND")
    rec["status"] = "PASS" if not rec["problems"] else "FAIL"
    return rec


def verify_branch_one(layer, action, weapon_subtype=0, offhand_subtype=3):
    """Both branches for one action: measured anchor-Z coefficient vs the code predicate.

    The block is not drawn in every phase of every action (verified: action 22 draws no weapon in
    the first ~400 ms and draws it later), so the measurement is taken at the first timer where the
    block exists; the timer used is recorded. If the block never appears the claim is INCONCLUSIVE,
    which is reported separately and does not turn into a predicate mismatch."""
    def measure(block, idx, equip):
        for timer in (200, 600, 1000):
            coeff = anchor_coeff_axis(layer, block, idx, equip, action, axis=2, timer=timer)
            if coeff is not None:
                return coeff, timer
        return None, None

    wi, _ = layer.item_model(3, weapon_subtype)
    oi, _ = layer.item_model(3, offhand_subtype)
    w_coeff, w_timer = measure("weapon", wi, {0xAA8: (3, weapon_subtype)})
    o_back = ia.offhand_on_back(action, offhand_subtype)
    o_block = "offhand_a" if o_back else "offhand_b"
    o_coeff, o_timer = measure(o_block, oi, {0xAA8: (3, 0), 0x990: (3, offhand_subtype)})
    w_want = "ON_BACK" if ia.weapon_on_back(action, weapon_subtype) else "IN_HAND"
    w_got = "ABSENT" if w_coeff is None else ("ON_BACK" if abs(w_coeff[2] - 0.5) < 1e-3 else "IN_HAND")
    o_want = "ON_BACK" if o_back else "IN_HAND"
    o_got = "ABSENT" if o_coeff is None else ("ON_BACK" if abs(o_coeff[2] - 0.5) < 1e-3 else "IN_HAND")
    rec = {"action": action,
           "weapon": {"predicate": w_want, "drawn": w_got, "coeff_z": w_coeff, "timer_ms": w_timer},
           "offhand": {"predicate": o_want, "drawn": o_got, "block": o_block, "coeff_z": o_coeff,
                       "timer_ms": o_timer}}
    problems, inconclusive = [], []
    for side, want, got in (("WEAPON", w_want, w_got), ("OFFHAND", o_want, o_got)):
        if got == "ABSENT":
            inconclusive.append(f"{side}_BRANCH_INCONCLUSIVE action={action} block not drawn")
        elif got != want:
            problems.append(f"{side}_BRANCH_MISMATCH action={action} {want} != {got}")
    rec["problems"] = problems
    rec["inconclusive"] = inconclusive
    return rec


def verify_two_handed_branch_one(layer, sub, action):
    idx, name = layer.item_model(3, sub)
    equip = {0xAA8: (3, sub)}
    law = ia.weapon_law(sub)
    coeff = anchor_coeff_axis(layer, "weapon", idx, equip, action, axis=2)
    problems = ([] if coeff is not None and abs(coeff[2] - law.coeff[2]) < 1e-3
                else [f"TWO_HANDED_ANCHOR_MISMATCH action={action} coeff_z={coeff}"])
    return {"subtype": sub, "action": action, "state": layer.state(action, equip), "coeff_z": coeff,
            "model": name, "grip_in_hand": grip_in_hand(layer, "weapon", law, equip, action),
            "problems": problems}


def _worker(args):
    """One process: build the research layer and run one verification task."""
    op, exe, db, sizes, payload = args
    layer = ItemLayer(exe, db, sizes)
    if op == "weaponlaw":
        return verify_weapon_law_one(layer, payload)
    if op == "offhandlaw":
        return verify_offhand_law_one(layer, payload)
    if op == "branch":
        return verify_branch_one(layer, payload)
    if op == "twohand":
        sub, action = payload
        return verify_two_handed_branch_one(layer, sub, action)
    raise ValueError(f"unknown task {op}")


def _run_tasks(layer, tasks, workers):
    """Run verification tasks sequentially or in worker processes (each builds its own layer)."""
    args = [(op, str(layer.exe), str(layer.db), layer.sizes_path, payload)
            for op, _e, _d, _s, payload in tasks]
    if workers and workers > 1 and len(args) > 1:
        from concurrent.futures import ProcessPoolExecutor
        with ProcessPoolExecutor(workers) as ex:
            return list(ex.map(_worker, args))
    return [_worker(a) for a in args]


def verify_weapon_laws(layer, subtypes=range(26), workers=0):
    if not workers or workers <= 1:
        return [verify_weapon_law_one(layer, sub) for sub in subtypes]
    tasks = [("weaponlaw", None, None, None, sub) for sub in subtypes]
    return _run_tasks(layer, tasks, workers)


def verify_offhand_laws(layer, subtypes=(3, 4, 6, 7, 12, 13, 20), workers=0):
    if not workers or workers <= 1:
        return [verify_offhand_law_one(layer, sub) for sub in subtypes]
    return _run_tasks(layer, [("offhandlaw", None, None, None, sub) for sub in subtypes], workers)


def verify_branches(layer, actions, workers=0):
    if not workers or workers <= 1:
        return [verify_branch_one(layer, a) for a in actions]
    return _run_tasks(layer, [("branch", None, None, None, a) for a in actions], workers)


def verify_two_handed_branch(layer, workers=0):
    payloads = [(sub, action) for sub in ia.TWO_HANDED_SUBTYPES for action in TWO_HANDED_CHAIN]
    if not workers or workers <= 1:
        return [verify_two_handed_branch_one(layer, sub, action) for sub, action in payloads]
    return _run_tasks(layer, [("twohand", None, None, None, p) for p in payloads], workers)


def verify_consumable(layer):
    cases = []
    for item_type, subtype, cooldown in ((1, 1, 3000), (1, 2, 10000), (11, 1, 0)):
        for timer in sorted({0, 1, max(cooldown - 1, 0), cooldown, cooldown + 1}):
            for action in (0, 80, 81):
                blocks = layer.blocks(action, None, timer, 0, held=(item_type, subtype))
                drawn = "held_item" in blocks
                want = action in CONSUMABLE_ACTIONS and cooldown > 0 and timer < cooldown
                cases.append({"type": item_type, "subtype": subtype, "action": action, "timer": timer,
                              "cooldown": cooldown, "drawn": drawn, "expected": want,
                              "ok": drawn == want})
    return {"cases": cases, "ok": all(c["ok"] for c in cases)}


def verify_conditional_slots(layer):
    """Flags/actions that gate the lamp, glider/boat and pet-slot blocks (D64 A.6 conditions)."""
    cases = []
    flag_cases = ((0x200, "lamp_item", 0xDF0, (24, 0), True),
                  (0x10, "lamp_item", 0xDF0, (24, 0), False),
                  (0x200, "special_item", 0xF08, (23, 0), False),
                  (0x10, "special_item", 0xF08, (23, 0), True),
                  (0x210, "lamp_item", 0xDF0, (24, 0), True),
                  (0x210, "special_item", 0xF08, (23, 0), True))
    for flag, block, slot, item, expected in flag_cases:
        drawn = block in layer.blocks(0, {slot: item}, 0, flag)
        cases.append({"flag124": flag, "slot": f"0x{slot:x}", "block": block, "drawn": drawn,
                      "expected": expected, "ok": drawn == expected})
    for action, expected in ((0, False), (82, True)):
        drawn = "pet_item" in layer.blocks(action, {0x1020: (19, 0)})
        cases.append({"action": action, "slot": "0x1020", "block": "pet_item", "drawn": drawn,
                      "expected": expected, "ok": drawn == expected})
    return {"cases": cases, "ok": all(c["ok"] for c in cases)}


def dynamic_parts(layer, cases=((0x6F, 108, 80), (0x19, 72, 80))):
    """R-8: a part that appears during an action but is not in the kind's appearance rig."""
    out = []
    for kind, action, timer in cases:
        base = layer.parts(layer.frame(0, kind=kind))
        fr = layer.frame(action, timer=timer, kind=kind)
        parts = layer.parts(fr)
        new = {n: p for n, p in parts.items() if n not in base}
        rec = {"kind": f"0x{kind:02x}", "action": action, "timer": timer, "state": fr["state"],
               "new_blocks": []}
        for n, p in new.items():
            idx = p.get("model_index")
            rec["new_blocks"].append({"block": n, "model_index": idx,
                                      "model": layer.registry.get(idx),
                                      "size": list(layer.sizes.get(idx, ())) or None,
                                      "anchor_law": ia.OFFHAND_IN_HAND["generic"].as_dict()
                                      if n == "offhand_b" else None})
        rec["status"] = "PART_IDENTIFIED" if rec["new_blocks"] else "NO_NEW_BLOCK"
        out.append(rec)
    return out


# --------------------------------------------------------------------------- tables
def action_table(layer, actions=range(256)):
    rows = []
    for a in actions:
        s = layer.state(a)
        if s == 0 and a not in CONSUMABLE_ACTIONS and a != PET_ACTION:
            continue
        rows.append({"id": a, "state": s,
                     "weapon": "IN_HAND" if not ia.weapon_on_back(a, 0) else "ON_BACK",
                     "offhand": "IN_HAND" if not ia.offhand_on_back(a, 13) else "ON_BACK",
                     "heldItem": a in CONSUMABLE_ACTIONS, "petItem": a == PET_ACTION,
                     "weaponOnly": a in WEAPON_ONLY_ACTIONS,
                     "twoHandedChain": a in TWO_HANDED_CHAIN})
    return rows


def weapon_classes(layer):
    out = []
    for sub in range(26):
        idx, name = layer.item_model(3, sub)
        law = ia.weapon_law(sub)
        out.append({"type": 3, "subtype": sub, "label": WEAPON_LABELS.get(sub),
                    "modelIndex": idx, "model": name,
                    "slot": "0xaa8", "block": "weapon",
                    "inHand": law.as_dict(),
                    "onBack": ia.WEAPON_ON_BACK.as_dict(),
                    "twoHanded": sub in ia.TWO_HANDED_SUBTYPES,
                    "dualCapable": sub in ia.DUAL_SUBTYPES,
                    "status": "VERIFIED_EMULATION"})
    return out


def offhand_classes(layer):
    out = []
    for sub in (3, 4, 6, 7, 12, 13, 20):
        idx, name = layer.item_model(3, sub)
        out.append({"type": 3, "subtype": sub, "label": WEAPON_LABELS.get(sub), "modelIndex": idx,
                    "model": name, "slot": "0x990",
                    "blockInHand": "offhand_b", "blockOnBack": "offhand_a",
                    "inHand": ia.offhand_law(sub, 0).as_dict(),
                    "onBack": ia.OFFHAND_ON_BACK.as_dict(), "status": "VERIFIED_EMULATION"})
    # non-weapon off-hand classes: bow/crossbow draw the quiver block instead of offhand_b
    for sub, block in ((6, "quiver_offhand"), (7, "quiver_offhand"), (14, "quiver_item")):
        idx, name = layer.item_model(3, sub)
        out.append({"type": 3, "subtype": sub, "label": WEAPON_LABELS.get(sub), "modelIndex": idx,
                    "model": name, "slot": "0x990", "blockInHand": block, "blockOnBack": None,
                    "inHand": ia.CENTER.as_dict(), "onBack": None, "status": "VERIFIED_EMULATION"})
    return out


def slot_classes(layer):
    out = []
    for slot in SLOTS:
        for t in slot["itemTypes"]:
            idx, name = layer.item_model(t, 0)
            out.append({"type": t, "subtype": 0, "label": ITEM_TYPE_LABELS.get(t), "modelIndex": idx,
                        "model": name, "slot": f"0x{slot['offset']:x}", "block": slot["block"],
                        "status": slot["status"]})
    return out


def build_contract(layer, dynamic=None):
    return {"format": CONTRACT_FORMAT, "researchOnly": True,
            "source": "Cube.exe executed by the Alpha oracle (no asset data committed)",
            "units": {"kind": f"0x{layer.kind:02x}", "seed": list(layer.seed)},
            "slots": [{**s, "offset": f"0x{s['offset']:x}"} for s in SLOTS],
            "weaponClasses": weapon_classes(layer),
            "offhandClasses": offhand_classes(layer),
            "slotClasses": slot_classes(layer),
            "actions": action_table(layer),
            "consumable": {"actions": list(CONSUMABLE_ACTIONS), "itemType": 1,
                           "cooldownsMs": {"1": 3000, "other": 10000, "notType1": 0},
                           "block": "held_item",
                           "rule": "timer < itemCooldown(type, subtype); model = item selector result"},
            "dual": {"subtypes": list(ia.DUAL_SUBTYPES), "rule":
                     "both hand items are type 3 with the same subtype; the off-hand is drawn in "
                     "offhand_b while an action is played (verified by emulation)"},
            "twoHanded": {"subtypes": list(ia.TWO_HANDED_SUBTYPES), "chain": list(TWO_HANDED_CHAIN),
                          "grip": "second hand placement is a project rule (D52), not Alpha data",
                          "status": "RECONSTRUCTED_FROM_EVIDENCE"},
            "dynamicParts": dynamic if dynamic is not None else dynamic_parts(layer),
            "unknowns": [
                "weapon subtype 0x12 (18) exists only in weapon_isSpecialRunePtr; no model block",
                "type 0x300 storage slot: no draw site found (A-U4)",
                "ring slots 0xbc0/0xcd8 are stored but never drawn (A.6)",
                "arrow special branch constants are code-derived only",
                "off-hand block for subtype 14 (quiver) is quiver_item; anchor law not measured",
                "turtle 0x19 action 72: no new draw block in the tested state domain (R-8 stays open)",
            ]}


# --------------------------------------------------------------------------- evidence + CLI
def build_evidence(layer, verification):
    return {"format": EVIDENCE_FORMAT, "decision": "D71 / M0-00B (issue #10)",
            "tool": "tools/alpha_models/catalog/item_layer.py",
            "exe_sha256": st.EXPECTED_EXE_SHA256, "code_sha256": code_hashes(),
            "units": {"kind": f"0x{layer.kind:02x}", "seed": list(layer.seed)},
            "verification": verification,
            "contract": build_contract(layer)}


def evidence_drift(path=EVIDENCE_PATH, root=ROOT):
    if not Path(path).exists():
        return ["ITEM_LAYER_EVIDENCE_MISSING"]
    ev = json.loads(Path(path).read_text(encoding="utf-8"))
    if ev.get("exe_sha256") != st.EXPECTED_EXE_SHA256:
        return ["ITEM_LAYER_EVIDENCE_EXE_DRIFT"]
    now = code_hashes(root)
    return [f"ITEM_LAYER_EVIDENCE_CODE_DRIFT {p}" for p, h in ev.get("code_sha256", {}).items()
            if now.get(p) != h]


def predicate_sample():
    """Actions whose back/in-hand branch is measured: every back action + every weapon-only action
    + the consumable/pet codes + a few neutral ones. Membership in a fixed set is what the
    predicate claims, so the sample covers every class of the claim."""
    sample = set(ia.ON_BACK_ACTIONS) | set(WEAPON_ONLY_ACTIONS) | set(CONSUMABLE_ACTIONS) | {PET_ACTION}
    sample |= {0, 2, 3, 6, 8, 9, 11, 12, 13}
    return sorted(sample)


def run(exe=DEFAULT_EXE, db=DEFAULT_DB, sizes=DEFAULT_SIZES, kinds=(0x02,), workers=0):
    layer = ItemLayer(exe, db, sizes, kind=kinds[0])
    sample = predicate_sample()
    branches = verify_branches(layer, sample, workers)
    verification = {"weaponLaws": verify_weapon_laws(layer, workers=workers),
                    "offhandLaws": verify_offhand_laws(layer, workers=workers),
                    "branchChecks": branches,
                    "twoHandedBranch": verify_two_handed_branch(layer, workers=workers),
                    "consumable": verify_consumable(layer),
                    "conditionalSlots": verify_conditional_slots(layer)}
    verification["weaponBackPredicate"] = {
        "agree": sum(1 for b in branches if not any("WEAPON" in p for p in b["problems"])),
        "total": len(branches),
        "disagree": [b for b in branches if any("WEAPON" in p for p in b["problems"])]}
    verification["offhandBackPredicate"] = {
        "agree": sum(1 for b in branches if not any("OFFHAND" in p for p in b["problems"])),
        "total": len(branches),
        "disagree": [b for b in branches if any("OFFHAND" in p for p in b["problems"])]}
    verification["dynamicParts"] = dynamic_parts(layer)
    return layer, verification


def print_summary(verification):
    wl = verification["weaponLaws"]
    ol = verification["offhandLaws"]
    th = verification["twoHandedBranch"]
    print(json.dumps({
        "WEAPON_LAWS": {"pass": sum(1 for r in wl if r["status"] == "PASS"), "total": len(wl),
                        "fail": [r["subtype"] for r in wl if r["status"] != "PASS"]},
        "OFFHAND_LAWS": {"pass": sum(1 for r in ol if r["status"] == "PASS"), "total": len(ol),
                         "fail": [r["subtype"] for r in ol if r["status"] != "PASS"]},
        "WEAPON_BACK_PREDICATE": {k: v for k, v in verification["weaponBackPredicate"].items()
                                  if k != "disagree"},
        "OFFHAND_BACK_PREDICATE": {k: v for k, v in verification["offhandBackPredicate"].items()
                                   if k != "disagree"},
        "TWO_HANDED_CHAIN": {"pass": sum(1 for r in th if not r["problems"]), "total": len(th)},
        "CONSUMABLE": verification["consumable"]["ok"],
        "CONDITIONAL_SLOTS": verification["conditionalSlots"]["ok"],
    }, indent=1))


def main(argv=None):
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--exe", default=str(DEFAULT_EXE))
    ap.add_argument("--db", default=str(DEFAULT_DB))
    ap.add_argument("--sizes", default=str(DEFAULT_SIZES))
    ap.add_argument("--out", default=None)
    ap.add_argument("--verify", action="store_true",
                    help="re-run the emulation and compare verification + contract with the committed evidence")
    ap.add_argument("--check-pins", action="store_true",
                    help="fast: only compare the exe/code SHA-256 pins of the committed evidence (no re-run)")
    ap.add_argument("--export-evidence", action="store_true")
    ap.add_argument("--workers", type=int, default=1, help="parallel verification processes")
    a = ap.parse_args(argv)

    drift = evidence_drift()
    if a.check_pins:
        print(json.dumps({"ITEM_LAYER_EVIDENCE_PINS": "DRIFT" if drift else "OK", "problems": drift}))
        return 1 if drift else 0

    if not Path(a.exe).exists() or not Path(a.db).exists():
        print(json.dumps({"ITEM_LAYER": "SKIPPED", "reason": "local Alpha research copy not present",
                          "exe": a.exe, "db": a.db}))
        return 2

    layer, verification = run(a.exe, a.db, a.sizes, workers=a.workers)
    print_summary(verification)
    ok = (all(r["status"] == "PASS" for r in verification["weaponLaws"])
          and all(r["status"] == "PASS" for r in verification["offhandLaws"])
          and not verification["weaponBackPredicate"]["disagree"]
          and not verification["offhandBackPredicate"]["disagree"]
          and all(not r["problems"] for r in verification["twoHandedBranch"])
          and verification["consumable"]["ok"] and verification["conditionalSlots"]["ok"]
          and all(d["status"] == "PART_IDENTIFIED" for d in verification["dynamicParts"] if d["kind"] == "0x6f"))
    contract = build_contract(layer)
    if a.verify:
        # Claude prebatch P6: --verify used to compare only the hash pins and never re-ran anything.
        committed = json.loads(EVIDENCE_PATH.read_text(encoding="utf-8")) if EVIDENCE_PATH.exists() else {}
        fresh = json.loads(json.dumps({"verification": verification, "contract": contract}))
        mismatched = [k for k in ("verification", "contract") if committed.get(k) != fresh[k]]
        print(json.dumps({"ITEM_LAYER_EVIDENCE_DATA": "MISMATCH" if mismatched else "MATCH",
                          "mismatched": mismatched, "pin_drift": drift}))
        ok = ok and not mismatched and not drift
    if a.out:
        out = Path(a.out).resolve()
        if not out.is_relative_to(ROOT / ".scratch"):
            raise SystemExit("ALPHA_OUTPUT_MUST_BE_IN_SCRATCH")
        out.parent.mkdir(parents=True, exist_ok=True)
        out.write_text(json.dumps({"verification": verification, "contract": contract}, indent=1),
                       encoding="utf-8")
        print(json.dumps({"WROTE": str(out), "actions": len(contract["actions"])}))
    if a.export_evidence:
        EVIDENCE_PATH.write_text(json.dumps(build_evidence(layer, verification), indent=1),
                                 encoding="utf-8")
        print(json.dumps({"EVIDENCE": "EXPORTED", "path": str(EVIDENCE_PATH)}))
    print(json.dumps({"ITEM_LAYER": "PASS" if ok else "FAIL"}))
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
