"""Unified entity contract + transfer planner (S05A2-UNIVERSAL-CONTINUE-01).

Synthetic tests (TEST_DATA_NOT_ALPHA) use invented appearances and kind numbers, so they prove the
logic is structural (blocks, flags, evidence records) and never keyed by a creature name. Local
evidence tests run only when the static table exists in .scratch (never in Git).
"""
from __future__ import annotations

import copy
import json
import os
import tempfile
import unittest
from pathlib import Path

from tools.alpha_models.universal import contract as ct
from tools.alpha_models.universal import creature_rig as cr
from tools.alpha_models.universal import param_walk as pw
from tools.alpha_models.universal import transfer_planner as tp

ROOT = Path(__file__).resolve().parents[3]
STATIC = ROOT / ".scratch" / "universal" / "static" / "alpha_static.json"
CLIPS = ROOT / ".scratch" / "universal" / "clips"
HAVE_STATIC = STATIC.exists()

NO_SLOTS = {"head": -1, "hair": -1, "hand": -1, "foot": -1, "body": -1, "tail": -1, "slot_98": -1, "slot_9a": -1}


def app(flags=0, **slots):
    a = copy.deepcopy(pw.SYNTHETIC_APPEARANCES["biped"])
    a.update(flags=flags, scale_weapon=1.0, scale_40=1.0, slots=dict(NO_SLOTS, **slots))
    return a


def table(kinds):
    """Synthetic static table: kinds {0xNN: appearance}; every slot index i -> model 'm<i>.cub' 4x4x4."""
    reg, sizes, out = {}, {}, {}
    for k, a in kinds.items():
        out[f"0x{k:02x}"] = {"appearance": a, "name_hint": f"Synthetic{k:02x}", "slot_variants": {}}
        for idx in a["slots"].values():
            if idx >= 0:
                reg[str(idx)] = f"m{idx}.cub"
                sizes[f"m{idx}.cub"] = [4, 4, 4]
    return {"format": "cubegame.alpha_static/1", "kinds": out, "registry": reg, "model_sizes": sizes,
            "registry_stats": {"indexed": len(reg)}}


BIPED = app(0, body=1, head=2, hair=3, hand=4, foot=5)
QUAD = app(1, body=11, head=12, hand=14, foot=15, tail=16)
LEGS = app(0, body=21, foot=25)
ODD = app(0, body=31, foot=35, slot_98=38, slot_9a=39, tail=36)       # mid limbs + wings + tail, no head


def describe(kind, a, t=None, **kw):
    t = t or table({kind: a})
    return ct.describe(kind, t["kinds"][f"0x{kind:02x}"], t, None, **kw)


class StructureTests(unittest.TestCase):
    def test_category_from_structure_not_name(self):
        self.assertEqual(describe(0x90, BIPED)["category"], "BIPED_WITH_FORE_LIMBS")
        self.assertEqual(describe(0x91, QUAD)["category"], "QUADRUPED")
        self.assertEqual(describe(0x92, LEGS)["category"], "LEGS_ONLY")
        self.assertEqual(describe(0x93, ODD)["category"], "WINGED")
        self.assertEqual(describe(0x94, app(0, body=41))["category"], "LIMBLESS")

    def test_name_hint_does_not_change_any_decision(self):
        t = table({0x90: QUAD})
        a = ct.describe(0x90, t["kinds"]["0x90"], t, None)
        t["kinds"]["0x90"]["name_hint"] = "HumanMale"          # a misleading name must not matter
        b = ct.describe(0x90, t["kinds"]["0x90"], t, None)
        for key in ("category", "body_plan", "family_id", "readiness", "channels"):
            self.assertEqual(a[key], b[key], key)

    def test_any_part_count_and_symmetric_mirror_pairs(self):
        for kind, a, n in ((0x90, BIPED, 7), (0x91, QUAD, 7), (0x92, LEGS, 3), (0x93, ODD, 8)):
            e = describe(kind, a)
            self.assertEqual(len(e["parts"]), n, e["body_plan"])
            by = {p["name"]: p for p in e["parts"]}
            for p in e["parts"]:
                self.assertIn(p["role"], ct.BLOCK_ROLES.values())
                if p["mirror_of"]:
                    self.assertEqual(by[p["mirror_of"]]["mirror_of"], p["name"])
                    self.assertEqual(by[p["mirror_of"]]["role"], p["role"])
                self.assertIn(p["unity_anchor"], ct.UNITY_ANCHOR_RULES)

    def test_odd_plan_is_walkable_by_formula_without_new_code(self):
        e = describe(0x93, ODD)
        self.assertEqual(e["readiness"], "AUTO_READY", e["reasons"])
        self.assertEqual(e["channels"]["walk"]["formula"], "FORMULA_VERIFIED")
        self.assertEqual(e["topology"]["wings"], 2)
        self.assertEqual(e["topology"]["mid_limbs"], 2)

    def test_equal_appearance_equal_family_across_kind_numbers(self):
        self.assertEqual(describe(0x90, QUAD)["family_id"], describe(0x95, QUAD)["family_id"])
        self.assertNotEqual(describe(0x90, QUAD)["family_id"], describe(0x90, BIPED)["family_id"])


class ReadinessTests(unittest.TestCase):
    def test_kind_specific_code_needs_research(self):
        k = next(iter(sorted(__import__("tools.alpha_models.universal.alpha_static", fromlist=["x"]).KIND_SPECIAL_BRANCHES)))
        e = describe(k, BIPED)
        self.assertEqual(e["readiness"], "NEEDS_RESEARCH")
        self.assertTrue(any(r.startswith("KIND_SPECIFIC_CODE") for r in e["reasons"]))

    def test_unresolved_model_is_blocked(self):
        t = table({0x90: LEGS})
        del t["registry"]["25"]
        e = ct.describe(0x90, t["kinds"]["0x90"], t, None)
        self.assertEqual(e["readiness"], "BLOCKED")
        self.assertTrue(any(r.startswith("MODEL_INDEX_UNRESOLVED") for r in e["reasons"]))

    def test_model_missing_in_db_is_blocked(self):
        t = table({0x90: LEGS})
        t["model_sizes"]["m25.cub"] = None
        self.assertEqual(ct.describe(0x90, t["kinds"]["0x90"], t, None)["readiness"], "BLOCKED")

    def test_no_parts_is_blocked(self):
        self.assertEqual(describe(0x90, app(0))["readiness"], "BLOCKED")


class ActionAndEquipmentTests(unittest.TestCase):
    """The real profile is pinned to the real humanoid family; here it is re-pinned to the synthetic
    biped family (TEST_DATA_NOT_ALPHA) so the rules can be exercised without Alpha data."""

    def setUp(self):
        self.pid = next(iter(ct.ACTION_PROFILES))
        self.saved = copy.deepcopy(ct.ACTION_PROFILES)
        ct.ACTION_PROFILES[self.pid]["evidence_families"] = [cr.resolve(0x90, BIPED).family_id]
        self.spec = ct.ACTION_PROFILES[self.pid]["specimens"][0]
        self.sk = self.spec["kind"]

    def tearDown(self):
        ct.ACTION_PROFILES.clear()
        ct.ACTION_PROFILES.update(self.saved)

    def test_only_the_evidenced_specimen_has_actions(self):
        t = table({self.sk: BIPED, 0x96: BIPED, 0x97: QUAD})
        sf = ct.specimen_families(t)
        e = ct.describe(self.sk, t["kinds"][f"0x{self.sk:02x}"], t, None, spec_families=sf)
        self.assertEqual(e["channels"]["actions"]["status"], "AVAILABLE")
        self.assertEqual(e["channels"]["actions"]["profile"], self.pid)
        same_family = ct.describe(0x96, t["kinds"]["0x96"], t, None, spec_families=sf)
        self.assertEqual(same_family["channels"]["actions"]["status"], "BLOCKED")
        self.assertIn("POSES_FAMILY_COMPATIBLE_TRIGGER_UNPROVEN", same_family["channels"]["actions"]["reasons"][0])
        quad = ct.describe(0x97, t["kinds"]["0x97"], t, None, spec_families=sf)
        self.assertEqual(quad["channels"]["actions"]["status"], "BLOCKED")
        self.assertTrue(quad["channels"]["actions"]["reasons"][0].startswith("NO_VERIFIED_ACTION_TRIGGER"))

    def test_other_seed_of_the_specimen_kind_is_not_the_specimen(self):
        self.assertIsNone(ct.profile_for_specimen(self.sk, [7, 3]))
        self.assertEqual(ct.profile_for_specimen(self.sk, self.spec["seed_variant"]), self.pid)

    def test_humanoid_input_rules_never_reach_a_quadruped(self):
        t = table({self.sk: QUAD})            # even the specimen kind number with another body plan
        e = ct.describe(self.sk, t["kinds"][f"0x{self.sk:02x}"], t, None)
        self.assertEqual(e["channels"]["actions"]["status"], "BLOCKED")
        self.assertTrue(any("SPECIMEN_APPEARANCE_DIFFERS" in r for r in e["channels"]["actions"]["reasons"]))
        u = ct.unity_contract(e)
        self.assertEqual(u["controllerMode"], "LOCOMOTION_ONLY")
        self.assertTrue(u["actionsReason"])
        self.assertFalse(set(u["allowedCategories"]) & {"MOTION", "HELD_STATE", "NATIVE_LIFECYCLE"})

    def test_weapon_layer_structural_support(self):
        self.assertEqual(ct.weapon_layer_support(cr.resolve(0x90, BIPED).parts, BIPED, 15), [])
        self.assertIn("FORE_LIMBS_ARE_WALKING_LIMBS (flags bit0)", ct.weapon_layer_support(cr.resolve(0x91, QUAD).parts, QUAD))
        self.assertIn("NO_GRIP_BLOCKS hand_l+hand_r", ct.weapon_layer_support(cr.resolve(0x92, LEGS).parts, LEGS))
        self.assertIn("WEAPON_SUBTYPE_NOT_VERIFIED 3", ct.weapon_layer_support(cr.resolve(0x90, BIPED).parts, BIPED, 3))

    def test_weapon_request_on_unsupported_plan_needs_research(self):
        e = describe(0x91, QUAD, equipment={"weapon": {"model": "x.cub", "subtype": 15}})
        self.assertEqual(e["readiness"], "NEEDS_RESEARCH")
        self.assertEqual(ct.unity_contract(e)["weaponLayer"], "BLOCKED")

    def test_action_baker_refuses_non_specimen_before_touching_alpha(self):
        from tools.alpha_models.universal import action_baker
        out = ROOT / ".scratch" / "tmp" / "test_baker_refusal"
        with self.assertRaisesRegex(ValueError, "ACTION_PROFILE_REQUIRED"):
            action_baker.bake_job({"kind": "0x20", "seed_variant": [0, 0]}, [57], out, exe="missing.exe")
        with self.assertRaisesRegex(ValueError, "PROFILE_REQUIRES_VERIFIED_WEAPON"):
            action_baker.bake_job({"kind": f"0x{self.sk:02x}", "seed_variant": self.spec["seed_variant"],
                                   "equipment": {"weapon": {"subtype": 3}}}, [57], out, exe="missing.exe")


def walk_clip(rig, parts, status="TRANSFERRED_FROM_ALPHA", masks=None):
    eye = [1.0, 0, 0, 0, 0, 1.0, 0, 0, 0, 0, 1.0, 0, 0, 0, 0, 1.0]
    keys = [{"x": 0.0, "m": {p: eye for p in parts}}, {"x": 6.28, "m": {p: eye for p in parts}}]
    clip = {"name": "walk", "rig": rig, "status": status, "param": "walk_phase", "loop": True, "duration": 6.28,
            "parts": [{"name": p} for p in parts], "keys": keys, "source": {}}
    if masks:
        clip["source"]["visibility_mode"] = "EXPLICIT_NATIVE_DRAW"
        for k, v in zip(clip["keys"], masks):
            k["visible"] = v
            k["m"] = {p: eye for p, on in zip(parts, v) if on}
    return clip


class ClipContractTests(unittest.TestCase):
    PARTS = ["body", "foot_l", "foot_r"]

    def test_good_clip_passes(self):
        self.assertEqual(ct.validate_clip(walk_clip("alpha_creature/x", self.PARTS), "alpha_creature/x", self.PARTS), [])

    def test_refusals(self):
        cases = {
            "CLIP_RIG_MISMATCH": walk_clip("alpha_creature/y", self.PARTS),
            "CLIP_PROVENANCE_NOT_ALLOWED": walk_clip("alpha_creature/x", self.PARTS, status="ORIGINAL_NOT_ALPHA"),
            "CLIP_PART_SET_MISMATCH": walk_clip("alpha_creature/x", self.PARTS[:2]),
        }
        unknown = walk_clip("alpha_creature/x", self.PARTS)
        unknown.update(loop=False, param="frames")
        cases["CLIP_CATEGORY_UNKNOWN"] = unknown
        bad_mask = walk_clip("alpha_creature/x", self.PARTS, masks=[[1, 1, 1], [1, 0, 1]])
        bad_mask["keys"][1]["m"]["foot_l"] = bad_mask["keys"][0]["m"]["body"]   # hidden part with a matrix
        cases["CLIP_MASK_MATRIX_MISMATCH"] = bad_mask
        for code, clip in cases.items():
            problems = ct.validate_clip(clip, "alpha_creature/x", self.PARTS)
            self.assertTrue(any(p.startswith(code) for p in problems), (code, problems))

    def test_dynamic_visibility_mask_is_valid(self):
        clip = walk_clip("alpha_creature/x", self.PARTS, masks=[[1, 1, 1], [1, 0, 1]])
        self.assertEqual(ct.validate_clip(clip, "alpha_creature/x", self.PARTS), [])

    def test_equipment_layer_only_when_declared(self):
        clip = walk_clip("alpha_creature/x", self.PARTS + ["weapon"])
        self.assertTrue(ct.validate_clip(clip, "alpha_creature/x", self.PARTS))
        self.assertEqual(ct.validate_clip(clip, "alpha_creature/x", self.PARTS, ("weapon",)), [])

    def test_categories(self):
        self.assertEqual(ct.clip_category(walk_clip("r", self.PARTS)), "LOOP_LOCOMOTION")
        self.assertEqual(ct.clip_category({"param": "time_ms", "loop": False, "duration": 0}), "STATIC_POSE")
        self.assertEqual(ct.clip_category({"param": "time_ms", "loop": False, "duration": 9}), "MOTION")
        self.assertEqual(ct.clip_category({"param": "time_ms", "duration": 9, "source": {"category": "HELD_STATE"}}),
                         "HELD_STATE")


class PlannerTests(unittest.TestCase):
    def setUp(self):
        self.t = table({0x90: BIPED, 0x91: QUAD, 0x92: LEGS, 0x93: ODD, 0x57: BIPED, 0x94: app(0)})

    def test_scan_and_plan_buckets(self):
        s = tp.scan(self.t)
        self.assertEqual(s["metrics"]["kinds"], 6)
        p = tp.plan(s)
        by = {r["kind"]: r for r in p["entities"]}
        self.assertEqual(by["0x93"]["readiness"], "AUTO_READY")
        self.assertEqual(by["0x57"]["readiness"], "NEEDS_RESEARCH")
        self.assertEqual(by["0x94"]["readiness"], "BLOCKED")
        self.assertEqual(by["0x92"]["job"]["locomotion"], "formula")
        self.assertIn("PLACEHOLDER_NOT_ALPHA", by["0x92"]["job"]["speed_status"])
        self.assertNotIn("job", by["0x57"])
        md = tp.plan_markdown(p)
        self.assertIn("| 0x93 |", md)
        self.assertEqual(p["batch"]["authorized"], [["D59", "s05b1"]])
        self.assertIn("NOT_AUTHORIZED", p["batch"]["other"])

    def test_pilot_refuses_kinds_outside_allowlist(self):
        r = tp.pilot(self.t, [0x93], None, ROOT / ".scratch" / "tmp" / "pilot_refusal")
        self.assertEqual(r["PILOT_RESULT"], "REFUSED")
        self.assertIn("NOT_IN_PILOT_ALLOWLIST", r["problems"][0])

    def test_batch_only_d59_s05b1_is_authorized(self):
        self.assertEqual(tp.BATCH_AUTHORIZATIONS, (("D59", "s05b1"),))
        for auth, scope in ((None, None), ("S05B", None), ("D99", "s05b1"), ("D59", None), ("D59", "s05b2"),
                            ("D58", "s05b1"), ("d59", "s05b1")):
            self.assertEqual(tp.batch(auth, scope)["BATCH_RESULT"], "NOT_AUTHORIZED", (auth, scope))
        self.assertEqual(tp.main(["batch", "--authorization", "S05B"]), 3)
        self.assertEqual(tp.main(["batch", "--authorization", "D59", "--scope", "s05b2"]), 3)
        self.assertIsNone(tp.batch_authorized("D59", "s05b1"))

    def test_outputs_must_stay_in_scratch(self):
        with self.assertRaisesRegex(ValueError, "PLANNER_OUTPUT_MUST_BE_IN_SCRATCH"):
            tp.local_out(tempfile.gettempdir())

    def test_validate_flags_inconsistent_job(self):
        with tempfile.TemporaryDirectory() as d:
            Path(d, "bad.json").write_text(json.dumps({"kind": "0x91", "seed_variant": [0, 0],
                                                       "equipment": {"weapon": {"model": "x", "subtype": 15}}}))
            Path(d, "actions.json").write_text(json.dumps({"kind": "0x92", "seed_variant": [0, 0],
                                                           "action_manifest": "m.json"}))
            r = tp.validate(self.t, d, jobs_dir=d)
        self.assertEqual(r["VALIDATE_RESULT"], "FAIL")
        self.assertTrue(any(p.startswith("JOB_WEAPON_NOT_SUPPORTED") for p in r["problems"]))
        self.assertTrue(any(p.startswith("JOB_ACTIONS_WITHOUT_PROFILE") for p in r["problems"]))


@unittest.skipUnless(HAVE_STATIC, "local static table (.scratch) not present")
class LocalReferenceTests(unittest.TestCase):
    """The three researched references, from the local static table (no Alpha run)."""

    @classmethod
    def setUpClass(cls):
        cls.t = tp.load_static(STATIC)
        cls.scan = {e["kind"]: e for e in tp.scan(cls.t, str(CLIPS))["entities"]}

    def test_references(self):
        hum, cat, cat2, run = (self.scan[k] for k in ("0x02", "0x20", "0x1e", "0x3f"))
        self.assertEqual((hum["category"], len(hum["parts"])), ("BIPED_WITH_FORE_LIMBS", 7))
        self.assertEqual(hum["channels"]["actions"]["status"], "AVAILABLE")
        self.assertEqual((cat["category"], len(cat["parts"])), ("QUADRUPED", 7))
        self.assertEqual((run["category"], len(run["parts"])), ("LEGS_ONLY", 3))
        self.assertEqual(cat["family_id"], cat2["family_id"])
        for e in (cat, cat2, run):
            self.assertEqual(e["readiness"], "AUTO_READY")
        # D60/A1: only Cat 0x1e has creature-AI trigger evidence; WhiteCat (same family) and the
        # runner stay blocked until their own trigger is proven
        self.assertEqual(cat2["channels"]["actions"]["status"], "AVAILABLE")
        self.assertEqual(cat2["channels"]["actions"]["trigger"], "creature_ai")
        self.assertEqual(ct.unity_contract(cat2)["controllerMode"], "ACTIONS")
        for e in (cat, run):
            self.assertEqual(e["channels"]["actions"]["status"], "BLOCKED")
            self.assertEqual(ct.unity_contract(e)["controllerMode"], "LOCOMOTION_ONLY")
        # cat and runner families carry a captured walk clip and the verified formula
        for e in (cat, run):
            self.assertEqual({s["provenance"] for s in e["channels"]["walk"]["sources"]},
                             {"TRANSFERRED_FROM_ALPHA", "RECONSTRUCTED_FROM_EVIDENCE"})

    def test_human_with_weapon_layer(self):
        e = ct.describe(0x02, self.t["kinds"]["0x02"], self.t, None,
                        equipment={"weapon": {"model": "iron-greatsword.cub", "subtype": 15}})
        self.assertEqual(len(e["parts"]), 8)
        self.assertEqual(ct.unity_contract(e)["weaponLayer"], "SUPPORTED")

    def test_validate_repository_jobs_and_family_clips(self):
        r = tp.validate(self.t, str(CLIPS))
        self.assertEqual(r["VALIDATE_RESULT"], "PASS", r["problems"])

    def test_pinned_profile_family_matches_static_table(self):
        self.assertEqual(ct.profile_family_drift(self.t), [])

    def test_pilot_allowlist_are_ready(self):
        for k in tp.PILOT_ALLOWLIST:
            self.assertEqual(self.scan[f"0x{k:02x}"]["readiness"], "AUTO_READY")


if __name__ == "__main__":
    unittest.main()


class HumanoidIndependenceTests(unittest.TestCase):
    """Generic modules may name humanoid action ids / the specimen kind ONLY inside evidence records."""

    EVIDENCE_NAMES = {"ACTION_PROFILES", "PILOT_ALLOWLIST"}
    PATTERN = r"\b(57|58|67|79|0x02)\b|\b(Human|Elf|Cat|Runner|humanoid_rig)\b"

    def _strip_evidence(self, path):
        import ast
        import re
        src = Path(path).read_text(encoding="utf-8")
        tree = ast.parse(src)
        lines = src.splitlines()
        drop = set()
        for node in ast.walk(tree):
            if isinstance(node, (ast.Assign, ast.AnnAssign)):
                targets = node.targets if isinstance(node, ast.Assign) else [node.target]
                if any(isinstance(t, ast.Name) and t.id in self.EVIDENCE_NAMES for t in targets):
                    drop.update(range(node.lineno, node.end_lineno + 1))
            if isinstance(node, ast.Expr) and isinstance(getattr(node, "value", None), ast.Constant) \
                    and isinstance(node.value.value, str):
                drop.update(range(node.lineno, node.end_lineno + 1))   # docstrings
        code = [ln.split("#")[0] for i, ln in enumerate(lines, 1) if i not in drop]
        return [ln for ln in code if re.search(self.PATTERN, ln)]

    def test_contract_and_planner_have_no_humanoid_literals(self):
        here = Path(__file__).resolve().parents[1] / "universal"
        for mod in ("contract.py", "transfer_planner.py"):
            self.assertEqual(self._strip_evidence(here / mod), [], mod)

    def test_unity_generic_components_have_no_part_or_action_literals(self):
        import re
        rt = Path(__file__).resolve().parents[1] / "transfer" / "unity" / "Runtime"
        for f in ("AlphaEntityContract.cs", "AlphaAnimationController.cs", "AlphaEntityProbe.cs"):
            src = (rt / f).read_text(encoding="utf-8")
            # comments and the structural ROLE vocabulary (core/head/tail/... roles) are not part names
            code = [ln for ln in src.splitlines() if not ln.strip().startswith("//") and "fore_limb" not in ln]
            hits = [ln for ln in code if re.search(r'"(hand_l|hand_r|foot_l|foot_r|head|hair|tail|body|weapon)"', ln)]
            self.assertEqual(hits, [], f)
            ids = [ln for ln in code if re.search(r"\b(58|67|79)\b", ln)]
            self.assertEqual(ids, [], f)
            # the only humanoid id allowed is the explicit refusal probe of a locomotion-only entity
            for ln in code:
                if "057" in ln:
                    self.assertIn("PlayAction", ln, f)
