"""Opt-in VISUAL CORRECTION PROPOSALS for kinds whose original Alpha composition looks broken (issue #7).

Not Alpha parity. Off by default: without an active correction every appearance is byte-identical to
Alpha's own ``Spawn_initDefaults`` + ``creature_generateAppearance`` (the oracle tests keep proving that).
A correction is switched on only for one named proposal, e.g. by a job field
``"visual_correction": "dragon_0x65_v1"`` (``pipeline.prepare_unity``) or by ``with active(name):`` around
library baking; then ``alpha_static.AppearanceEmulator.run`` patches the listed appearance fields of that
one kind after Alpha's generator ran. Everything downstream (drawModel oracle, action clips, walk formula,
entity contract) sees the same corrected appearance, so the parts stay consistent in every clip.

Every clip/specimen made under a correction carries the correction's status: ``VISUAL_CORRECTION_PROPOSAL_NOT_ALPHA``
until the producer approves it, then ``VISUAL_CORRECTION_APPROVED_NOT_ALPHA`` (``dragon_0x65_v1``: D83, 2026-11-09).
Neither ever counts as a transferred Alpha result. Approved corrections listed in ``GAME_DEFAULTS`` are used by
every game job of their kind (``transfer_planner.job_for``); Alpha-parity checks keep running without them.

Dragon 0x65 (root cause, Cube.exe ``creature_generateAppearance`` kind branch at 0x442E91):
Alpha's own data place all four legs with z = +1 (front legs (3, 4, 1) scale 1.5, hind legs (3, -4, 1))
and the tail root at y = -16 behind a body that ends at y = -12. Executed drawModel then draws the
paws 8-10 voxels above the ground plane every finished quadruped stands on (cat 0x1E, human 0x02:
lowest voxel centre z = -12 at rest), the body below its paws, and a ~5-voxel hole between body and tail.
The dragon is never spawned by an immediate kind write in Server.exe/Cube.exe and was never observed
live, so this reads as unfinished Alpha content, not a transfer defect.
The proposal moves only those three offsets, by the composition rules of Alpha's finished quadrupeds:
paws on the shared ground plane at rest, tail root about one voxel inside the body (cat: 0.9).
Head, body, scales, models, anchors and every animation formula are unchanged.
"""
from __future__ import annotations

import contextlib
import struct

STATUS = "VISUAL_CORRECTION_PROPOSAL_NOT_ALPHA"
APPROVED_STATUS = "VISUAL_CORRECTION_APPROVED_NOT_ALPHA"   # producer approved for the game; still not Alpha parity

CORRECTIONS = {
    "dragon_0x65_v1": {
        "kind": 0x65,
        "status": APPROVED_STATUS,
        "approval": {"decision": "D83", "date": "2026-11-09", "by": "producer"},
        # rig family of the corrected appearance (alpha_creature/ccddb0483c without the correction);
        # a game job only accepts action libraries baked under the correction with this family
        "game_family": "alpha_creature/95251fe7e5",
        "issue": "#7",
        # appearance offset -> (field name, Alpha value, proposed value)
        "fields": {
            0x7C: ("offset_hand", (3.0, 4.0, 1.0), (3.0, 4.0, -7.5)),     # front legs: paws on the ground
            0x88: ("offset_foot", (3.0, -4.0, 1.0), (3.0, -4.0, -9.0)),   # hind legs: paws on the ground
            0x94: ("offset_tail", (0.0, -16.0, 4.0), (0.0, -11.0, 4.0)),  # tail root 1 voxel inside the body
        },
        # action profile kept on the corrected appearance: profile -> the Alpha family it was verified on
        "keeps_profiles": {"alpha_actions/creature_ai_d64_dragon_0x65": "alpha_creature/ccddb0483c"},
        "rules": ["lowest voxel centre of every leg at rest on the ground plane z = -12 (as cat 0x1E, human 0x02)",
                  "tail root about one voxel inside the body (closest body/tail voxel centres <= 1.1)",
                  "head, body, scales, models, anchors and formulas unchanged"],
    },
}

# kind -> correction every GAME job of that kind uses (transfer_planner.job_for). Only approved corrections.
# Alpha-parity paths (oracle tests, D69 verification of the D64 library) never read this table.
GAME_DEFAULTS = {0x65: "dragon_0x65_v1"}

_active = None


def game_default(kind):
    """Approved correction a game job of ``kind`` must use, else None."""
    name = GAME_DEFAULTS.get(int(kind, 16) if isinstance(kind, str) else kind)
    if name is not None and get(name).get("status") != APPROVED_STATUS:
        raise ValueError(f"{name} is in GAME_DEFAULTS but not approved")
    return name


def get(name):
    if name not in CORRECTIONS:
        raise KeyError(f"unknown visual correction {name!r}; known: {sorted(CORRECTIONS)}")
    return CORRECTIONS[name]


@contextlib.contextmanager
def active(name):
    """Enable one correction for the block (None = no correction, a no-op)."""
    global _active
    if name is not None:
        get(name)
    prev, _active = _active, name
    try:
        yield
    finally:
        _active = prev


def current():
    return _active


def apply(kind, raw):
    """Appearance bytes of ``kind`` with the active correction applied (unchanged if none applies).

    Refuses to patch when Alpha's own value is not the documented one: a different Cube.exe build or
    a changed generator must never be silently "corrected"."""
    if _active is None:
        return raw
    c = CORRECTIONS[_active]
    if kind != c["kind"]:
        return raw
    out = bytearray(raw)
    for off, (name, alpha, proposed) in c["fields"].items():
        got = tuple(round(v, 6) for v in struct.unpack_from("<3f", out, off))
        if got != alpha:
            raise ValueError(f"{_active}: {name} is {got}, expected Alpha value {alpha} - not patching")
        struct.pack_into("<3f", out, off, *proposed)
    return bytes(out)


def provenance(name):
    c = get(name)
    return {"visual_correction": name, "status": c["status"], "approval": c.get("approval"),
            "issue": c["issue"], "kind": f"0x{c['kind']:02x}",
            "fields": {n: {"alpha": list(a), "proposed": list(p)} for n, a, p in c["fields"].values()},
            "rules": c["rules"]}
