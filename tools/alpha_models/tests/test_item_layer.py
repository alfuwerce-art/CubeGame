"""D71 / M0-00B: item models, equipment slots, attach points and item-dependent actions.

Pure tests (no Alpha) cover the law arithmetic, the branch predicates, the contract schema and the
catalog verifier on a synthetic database. Alpha-dependent tests are skipped unless a local research
copy of the game is present (``.scratch/alphatest2/`` or ``$env:CUBEGAME_ALPHA_EXE`` + $env:CUBEGAME_ALPHA_DB);
they never launch the game, only execute its bytes read-only.
"""
from __future__ import annotations

import hashlib
import json
import os
import sqlite3
import struct
import tempfile
import unittest
from pathlib import Path

from tools.alpha_models.cub_format import VoxelModel, encode_model, obfuscate
from tools.alpha_models.universal import creature_rig as cr
from tools.alpha_models.universal import item_attach as ia
from tools.alpha_models.anim import humanoid_rig

ROOT = Path(__file__).resolve().parents[3]
EXE = Path(os.environ.get("CUBEGAME_ALPHA_EXE", ROOT / ".scratch/alphatest2/Cube.exe"))
DB = Path(os.environ.get("CUBEGAME_ALPHA_DB", ROOT / ".scratch/alphatest2/data1.db"))
if not EXE.exists():
    EXE = Path(r"D:\Games\Cube world\Cube.exe")
if not DB.exists():
    DB = Path(r"D:\Games\Cube world\data1.db")
SIZES = ROOT / ".scratch/catalog/registry_sizes.json"
HAVE_ALPHA = EXE.exists() and DB.exists()

from tools.alpha_models.catalog import item_layer as il          # noqa: E402
from tools.alpha_models.catalog import verify_catalog_import as vci  # noqa: E402


class LawMathTests(unittest.TestCase):
    def test_anchor_is_linear_in_size(self):
        self.assertEqual(ia.CENTER.anchor((2, 4, 6)), (1.0, 2.0, 3.0))
        self.assertEqual(ia.WEAPON_IN_HAND_GENERIC.anchor((3, 9, 25)), (1.5, 4.5, 4.0))
        self.assertEqual(ia.WEAPON_IN_HAND["staff"].anchor((8, 8, 35)), (4.0, 4.0, 10.0))
        self.assertEqual(ia.WEAPON_IN_HAND["bow"].anchor((1, 9, 25)), (0.5, 7.5, 12.5))
        self.assertEqual(ia.WEAPON_IN_HAND["crossbow"].anchor((5, 16, 34)), (2.5, 0.0, 17.0))
        self.assertEqual(ia.WEAPON_IN_HAND["boomerang"].anchor((2, 18, 18)), (1.0, 2.0, 4.0))
        self.assertEqual(ia.WEAPON_IN_HAND["fist"].anchor((10, 11, 7)), (5.0, 5.5, 3.5))
        bracelet = ia.WEAPON_IN_HAND["bracelet"]
        self.assertEqual(bracelet.anchor((10, 3, 10)), (5.0, 2.0, 5.0))
        self.assertEqual(bracelet.scale, 0.6)
        self.assertEqual(ia.OFFHAND_IN_HAND["shield"].anchor((14, 6, 12)), (7.0, 3.0, 6.0))

    def test_bad_size_is_refused(self):
        with self.assertRaises(ValueError):
            ia.CENTER.anchor((2, 4))

    def test_coefficient_check_catches_a_wrong_law(self):
        law = ia.WEAPON_IN_HAND_GENERIC
        ok = [[0.5, 0, 0], [0, 0.5, 0], [0, 0, 0]]
        bad = [[0.5, 0, 0], [0, 0.5, 0], [0, 0, 0.5]]      # a centre law, i.e. the back branch
        self.assertEqual(ia.coefficient_problems(law, ok), [])
        self.assertTrue(any("ANCHOR_COEFF_MISMATCH" in p for p in ia.coefficient_problems(law, bad)))


class LawTableTests(unittest.TestCase):
    def test_every_weapon_subtype_has_a_declared_law(self):
        self.assertEqual(sorted(ia.WEAPON_SUBTYPE_LAW), list(range(26)))
        for sub, family in ia.WEAPON_SUBTYPE_LAW.items():
            self.assertIn(family, ia.WEAPON_IN_HAND, sub)
        for sub, family in ia.OFFHAND_SUBTYPE_LAW.items():
            self.assertIn(family, ia.OFFHAND_IN_HAND, sub)

    def test_two_handed_subtypes_use_the_generic_in_hand_law(self):
        self.assertEqual(tuple(ia.TWO_HANDED_SUBTYPES), (15, 16, 17))
        for sub in ia.TWO_HANDED_SUBTYPES:
            self.assertIs(ia.weapon_law(sub), ia.WEAPON_IN_HAND_GENERIC, sub)
            self.assertEqual(ia.weapon_law(sub).const[2], 4.0)

    def test_dual_subtypes_are_the_ones_drawn_in_the_left_hand(self):
        self.assertEqual(tuple(ia.DUAL_SUBTYPES), (4, 12, 20))
        for sub in ia.DUAL_SUBTYPES:
            self.assertFalse(ia.offhand_on_back(0, sub), sub)   # held even at idle

    def test_offhand_laws_agree_with_the_accepted_rig_table(self):
        """D64's rule names are the coefficient vectors of the in-hand laws (no rewrite of D64)."""
        rules = {"center": (0.5, 0.5, 0.5), "tail": (0.5, 1.0, 0.5), "pet": (0.5, 0.5, 0.0)}
        # subtype 0 in the D64 table is the golem's thrown rock (state 71), not an in-hand law
        for sub in (4, 12, 20):
            rule = cr.EQUIPMENT_ANCHOR_BY_SUBTYPE["offhand_b"][sub]
            self.assertEqual(ia.offhand_law(sub, 0).coeff, rules[rule], f"offhand subtype {sub}")
        self.assertEqual(cr.EQUIPMENT_ANCHOR_BY_SUBTYPE["offhand_b"][0], "center")

    def test_two_handed_in_hand_anchor_matches_the_accepted_humanoid_rig(self):
        for sub in (15, 16, 17):
            for size in ((3, 13, 37), (5, 9, 25)):
                self.assertEqual(ia.weapon_law(sub).anchor(size), humanoid_rig.weapon_anchor(size, False, sub))
        # the accepted rig still refuses unverified subtypes: this module does not rewrite it
        with self.assertRaises(ValueError):
            humanoid_rig.weapon_anchor((3, 9, 25), False, 0)

    def test_boomerang_main_changes_the_offhand_law(self):
        self.assertIs(ia.offhand_law(8, 8), ia.OFFHAND_IN_HAND["boomerang_main"])
        self.assertIs(ia.offhand_law(8, 0), ia.OFFHAND_IN_HAND["generic"])


class BranchPredicateTests(unittest.TestCase):
    def test_back_actions_are_the_drawmodel_list(self):
        self.assertEqual(set(ia.ON_BACK_ACTIONS), set(humanoid_rig.WEAPON_ON_BACK_ACTIONS))

    def test_weapon_branch(self):
        self.assertTrue(ia.weapon_on_back(0, 0))
        self.assertFalse(ia.weapon_on_back(57, 0))
        self.assertTrue(ia.weapon_on_back(80, 0))            # drinking: weapon on the back
        self.assertTrue(ia.weapon_on_back(57, 0, flag_124=0x10))
        self.assertFalse(ia.weapon_on_back(0, 4))            # fist: in hand even at idle
        self.assertFalse(ia.weapon_on_back(0, 12))           # bracelet
        self.assertIs(ia.weapon_present_law(0, 0), ia.WEAPON_ON_BACK)
        self.assertIs(ia.weapon_present_law(57, 15), ia.WEAPON_IN_HAND_GENERIC)

    def test_offhand_branch(self):
        self.assertTrue(ia.offhand_on_back(0, 13))           # shield worn on the back
        self.assertFalse(ia.offhand_on_back(57, 13))         # shield in the left hand in an attack
        self.assertFalse(ia.offhand_on_back(0, 20))          # torch held at idle
        self.assertFalse(ia.offhand_on_back(0, 4))
        self.assertFalse(ia.offhand_on_back(0, 12))
        self.assertTrue(ia.offhand_on_back(0, 3))            # second dagger worn on the back
        self.assertIs(ia.offhand_present_law(57, 13), ia.OFFHAND_IN_HAND["shield"])

    def test_consumable_codes_are_in_the_back_set(self):
        for action in il.CONSUMABLE_ACTIONS:
            self.assertIn(action, ia.ON_BACK_ACTIONS)


class CooldownTests(unittest.TestCase):
    def test_item_cooldown_rule(self):
        self.assertEqual(il.item_cooldown_ms(1, 1), 3000)
        self.assertEqual(il.item_cooldown_ms(1, 2), 10000)
        self.assertEqual(il.item_cooldown_ms(1, 0), 10000)
        for other in (3, 11, 24, 0):
            self.assertEqual(il.item_cooldown_ms(other, 1), 0, other)


class _StubLayer:
    """Minimal layer for schema tests: no emulator, no Alpha."""
    kind, seed = 0x02, (0, 0)
    sizes = {500: (3, 9, 25)}

    def item_model(self, item_type, subtype=0, material=0, b0c=0, seed=0):
        if item_type == 3:
            return 500 + subtype, f"weapon-{subtype}.cub"
        return 900 + item_type, f"item-{item_type}-{subtype}.cub"

    def state(self, action, equipment=None, flag124=0, kind=None):
        return {57: 2, 80: 51, 81: 52, 82: 53}.get(action, 0)


class ContractTests(unittest.TestCase):
    def test_contract_schema(self):
        c = il.build_contract(_StubLayer(), dynamic=[])
        self.assertEqual(c["format"], "cubegame.item_layer/1")
        self.assertEqual(len(c["weaponClasses"]), 26)
        self.assertEqual(len(c["offhandClasses"]), 10)
        self.assertEqual(len(c["slots"]), len(il.SLOTS))
        ids = [a["id"] for a in c["actions"]]
        self.assertEqual(sorted(ids), [57, 80, 81, 82])
        act = {a["id"]: a for a in c["actions"]}
        self.assertFalse(act[57]["weapon"] == "ON_BACK")     # attack: weapon in hand
        self.assertTrue(act[80]["heldItem"])
        self.assertTrue(act[82]["petItem"])
        self.assertTrue(act[57]["twoHandedChain"])
        self.assertEqual(c["consumable"]["actions"], [80, 81])
        self.assertEqual(c["twoHanded"]["subtypes"], [15, 16, 17])
        self.assertEqual(c["dual"]["subtypes"], [4, 12, 20])
        self.assertEqual(c["dynamicParts"], [])
        self.assertTrue(c["unknowns"])
        great = [w for w in c["weaponClasses"] if w["subtype"] == 15][0]
        self.assertTrue(great["twoHanded"])
        self.assertEqual(great["inHand"]["const"], [0.0, 0.0, 4.0])

    def test_unknown_subtype_is_refused_not_guessed(self):
        with self.assertRaises(KeyError):
            ia.weapon_law(99)
        with self.assertRaises(KeyError):
            ia.offhand_law(99)


class CatalogVerifierTests(unittest.TestCase):
    """The verifier on a synthetic DB/manifest: every check must be able to FAIL."""

    @classmethod
    def setUpClass(cls):
        cls.tmp = tempfile.TemporaryDirectory()
        cls.dir = Path(cls.tmp.name)
        model = VoxelModel(1, 1, 1, bytes((10, 20, 30)))
        cls.blob = obfuscate(encode_model(model))
        cls.sha = hashlib.sha256(cls.blob).hexdigest()
        cls.db = cls.dir / "data1.db"
        con = sqlite3.connect(cls.db)
        con.execute("create table blobs (key text primary key, value blob)")
        con.execute("insert into blobs values (?, ?)", ("cube.cub", cls.blob))
        con.commit()
        con.close()
        cls.record = {"name": "cube.cub", "route": "CREATURE_PART", "sha256": cls.sha,
                      "verdict": "PASS", "problem": "",
                      "prefab": "Assets/AlphaCatalog/Generated/CREATURE_PART/cube.prefab",
                      "vertices": 24, "faces": 6, "solid": 1, "markers": 0,
                      "markerTransparent": False}

    @classmethod
    def tearDownClass(cls):
        cls.tmp.cleanup()

    def _manifest(self, records):
        path = self.dir / "catalog_import.json"
        path.write_text(json.dumps({"records": records}), encoding="utf-8")
        return path

    def test_pass_and_every_detects_drift(self):
        man = self._manifest([self.record])
        res = vci.verify(man, self.db, limit=1)
        self.assertEqual(res["result"], "PASS", res["problems"])

        bad = dict(self.record, faces=7)
        self.assertIn("FACES_MISMATCH", " ".join(vci.verify(self._manifest([bad]), self.db, limit=1)["problems"]))

        bad = dict(self.record, sha256="0" * 64)
        self.assertIn("BLOB_SHA256_DRIFT", " ".join(vci.verify(self._manifest([bad]), self.db, limit=1)["problems"]))

        bad = dict(self.record, verdict="FAIL", problem="boom")
        self.assertIn("VERDICT_NOT_PASS", " ".join(vci.verify(self._manifest([bad]), self.db, limit=1)["problems"]))

        bad = dict(self.record, route="MYSTERY")
        self.assertIn("UNKNOWN_ROUTE", " ".join(vci.verify(self._manifest([bad]), self.db, limit=1)["problems"]))

    def test_duplicate_and_marker_rule(self):
        man = self._manifest([self.record, dict(self.record)])
        self.assertIn("DUPLICATE_NAME", " ".join(vci.verify(man, self.db, limit=2)["problems"]))
        # a flag-0 model whose only voxel is a marker colour: Alpha draws nothing (faces == 0)
        marker_model = VoxelModel(1, 1, 1, bytes((255, 0, 0)))
        blob = obfuscate(encode_model(marker_model))
        con = sqlite3.connect(self.db)
        con.execute("insert into blobs values (?, ?)", ("marker.cub", blob))
        con.commit()
        con.close()
        stats_faces = 0
        rec = {"name": "marker.cub", "route": "CREATURE_PART", "sha256": hashlib.sha256(blob).hexdigest(),
               "verdict": "PASS", "problem": "", "prefab": "/CREATURE_PART/marker.prefab",
               "vertices": stats_faces * 4, "faces": stats_faces, "solid": 0, "markers": 1,
               "markerTransparent": True}
        res = vci.verify(self._manifest([rec]), self.db, limit=1)
        self.assertEqual(res["result"], "PASS", res["problems"])


class EvidenceTests(unittest.TestCase):
    def test_committed_evidence_is_complete(self):
        path = il.EVIDENCE_PATH
        if not path.exists():
            self.skipTest("item layer evidence not exported yet")
        ev = json.loads(path.read_text(encoding="utf-8"))
        self.assertEqual(ev["format"], il.EVIDENCE_FORMAT)
        ver = ev["verification"]
        for key in ("weaponLaws", "offhandLaws", "weaponBackPredicate", "offhandBackPredicate",
                    "twoHandedBranch", "consumable", "conditionalSlots", "dynamicParts"):
            self.assertIn(key, ver)
        self.assertTrue(all(r["status"] == "PASS" for r in ver["weaponLaws"]))
        self.assertTrue(all(r["status"] == "PASS" for r in ver["offhandLaws"]))
        self.assertEqual(ver["weaponBackPredicate"]["disagree"], [])
        self.assertEqual(ver["offhandBackPredicate"]["disagree"], [])
        self.assertTrue(ver["consumable"]["ok"])
        self.assertTrue(ver["conditionalSlots"]["ok"])
        ident = [d for d in ver["dynamicParts"] if d["status"] == "PART_IDENTIFIED"]
        self.assertTrue(ident, "golem action 108 must be identified as a block+model record")
        contract = ev["contract"]
        self.assertEqual(len(contract["actions"]), 101)
        self.assertEqual(len(contract["slotClasses"]), 13)


@unittest.skipUnless(HAVE_ALPHA, "local research copy of Alpha not present (no .scratch/alphatest2)")
class AlphaExecutionTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.layer = il.ItemLayer(EXE, DB, SIZES if SIZES.exists() else None)

    def test_item_selector_and_model_names(self):
        idx, name = self.layer.item_model(3, 15)
        self.assertIn("greatsword", name)
        self.assertIn("shield", self.layer.item_model(3, 13)[1])
        self.assertEqual(self.layer.item_model(23, 0)[1], "glider.cub")

    def test_branch_predicate_on_real_frames(self):
        idx, _ = self.layer.item_model(3, 0)
        equip = {0xAA8: (3, 0)}
        self.assertEqual(il.branch_of(self.layer, "weapon", idx, equip, 0), "ON_BACK")
        self.assertEqual(il.branch_of(self.layer, "weapon", idx, equip, 57), "IN_HAND")
        self.assertEqual(il.branch_of(self.layer, "weapon", idx, equip, 80), "ON_BACK")

    def test_two_handed_grip_is_in_the_hand(self):
        law = ia.weapon_law(15)
        self.assertTrue(il.grip_in_hand(self.layer, "weapon", law, {0xAA8: (3, 15)}, 57))

    def test_committed_evidence_matches_the_executed_bytes(self):
        if not il.EVIDENCE_PATH.exists():
            self.skipTest("evidence not exported yet")
        self.assertEqual(il.evidence_drift(), [])


if __name__ == "__main__":
    unittest.main()
