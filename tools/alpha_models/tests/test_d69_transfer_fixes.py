"""D69 targeted checks: passive-object game usage, kind-aware Unity anchors (Dragon 0x65), census tool.

Alpha-dependent checks skip without the local copy in .scratch (never in Git)."""
from __future__ import annotations

import json
import re
import unittest
from pathlib import Path

from tools.alpha_models.catalog import spawn_roles as sr
from tools.alpha_models.universal import batch_runner as br
from tools.alpha_models.universal import contract as ct
from tools.alpha_models.universal import creature_rig as cr

ROOT = Path(__file__).resolve().parents[3]
SERVER = ROOT / ".scratch" / "alphatest2" / "Server.exe"
CUBE = ROOT / ".scratch" / "alphatest2" / "Cube.exe"
STATIC = ROOT / ".scratch" / "catalog" / "alpha_static.json"
HAVE_ALPHA = SERVER.exists() and CUBE.exists()

PROVEN_PASSIVE = {0x78, 0x79, 0x7A, 0x7B, 0x7C, 0x7D, 0x7E, 0x7F, 0x80, 0x82,      # plants
                  0x83, 0x84, 0x85, 0x87, 0x88, 0x89, 0x8A,                          # generated ore deposits
                  0x8C, 0x8F, 0x90}                                                  # scarecrow, vase, bomb
POLICY_ONLY = {0x18, 0x81, 0x86, 0x8B, 0x8D, 0x8E}


def _mirror_unity_anchor(rule, size):
    """Python mirror of AlphaRig.Anchor (C#) for the body rules (kept in sync by the source check below)."""
    sx, sy, sz = size
    return {"center": (sx / 2, sy / 2, sz / 2), "tail": (sx / 2, sy, sz / 2), "wing": (sx / 2, 0.0, sz / 2),
            "head_0x65": (sx / 2, sy * 0.2, sz / 2)}[rule]


class SpawnRoleEvidenceTests(unittest.TestCase):
    def test_committed_evidence_is_pass_and_complete(self):
        ev = sr.load_evidence()
        self.assertEqual(ev["RESULT"], "PASS")
        self.assertEqual(ev["problems"], [])
        kinds = {int(k, 16): v for k, v in ev["passive_kinds"].items()}
        self.assertEqual({k for k, v in kinds.items() if v["basis"] == "ALPHA_CODE_SPAWN"}, PROVEN_PASSIVE)
        self.assertEqual({k for k, v in kinds.items() if v["basis"] != "ALPHA_CODE_SPAWN"}, POLICY_ONLY)
        for k in PROVEN_PASSIVE:      # a proven kind names the exact faction and kind writes
            self.assertTrue(kinds[k]["sites"] and all(s["faction_write"] and s["kind_write"] for s in kinds[k]["sites"]))

    def test_real_creatures_stay_creatures(self):
        # plant MONSTERS are not decorative plants; fish, slimes and the dragon are creatures
        for k in (0x45, 0x46, 0x47, 0x48, 0x49, 0x25, 0x91, 0x96, 0x65, 0x02, 0x6F):
            self.assertEqual(sr.game_policy(k)[0], "CREATURE", hex(k))

    def test_every_decorative_object_is_static(self):
        for k in PROVEN_PASSIVE | POLICY_ONLY:
            policy, basis, reason = sr.game_policy(k)
            self.assertEqual(policy, "STATIC_OBJECT", hex(k))
            self.assertTrue(basis and reason)

    @unittest.skipUnless(HAVE_ALPHA, "needs the local Alpha copy in .scratch")
    def test_bytes_reverified_against_the_binaries(self):
        doc = sr.build(SERVER, CUBE)
        self.assertEqual(doc["RESULT"], "PASS", doc["problems"])
        self.assertEqual(doc["passive_kinds"], sr.load_evidence()["passive_kinds"])

    def test_wrong_bytes_are_reported_not_guessed(self):
        data = bytearray(64)
        with self.assertRaises(ValueError):
            sr.read_va(bytes(data), 0x401000, 4, sections=[(0x400000, 0x100, 0)])


class StaticObjectContractTests(unittest.TestCase):
    def _entity(self, kind, actions_available=True):
        return {"kind": f"0x{kind:02x}", "category": "LIMBLESS", "body_plan": "plan/body", "family_id": "f",
                "parts": [], "readiness": "AUTO_READY",
                "channels": {"actions": {"status": "AVAILABLE" if actions_available else "BLOCKED",
                                         "profile": None, "reasons": []},
                             "equipment": {"weapon": {"status": "SUPPORTED", "requested": False}},
                             "walk": {"sources": []}, "idle": {"sources": []},
                             "game_usage": ct.game_usage(kind)}}

    def test_static_object_mode_wins_over_an_action_profile(self):
        e = self._entity(0x78)          # Bush: had 8 default-branch actions in D65
        self.assertEqual(ct.controller_mode(e), "STATIC_OBJECT")
        u = ct.unity_contract(e)
        self.assertEqual(u["controllerMode"], "STATIC_OBJECT")
        self.assertEqual(u["gameUsage"], "STATIC_OBJECT")
        self.assertIn("0x5360AE", u["gameUsageReason"])

    def test_creature_modes_unchanged(self):
        self.assertEqual(ct.controller_mode(self._entity(0x6F, True)), "ACTIONS")
        self.assertEqual(ct.controller_mode(self._entity(0x6F, False)), "LOCOMOTION_ONLY")

    def test_batch_judge_requires_refused_move(self):
        import tempfile
        with tempfile.TemporaryDirectory() as d:
            ok = {"RESULT": {"verdict": "PASS", "exit": 0}}
            (Path(d) / "entity_playmode.txt").write_text(
                "controller_mode=STATIC_OBJECT\nlocomotion_only_refused=True\nstatic_object=True move_refused=True\n",
                encoding="utf-8")
            self.assertEqual(br.judge_entry(ok, 0, d, required=()), [])
            (Path(d) / "entity_playmode.txt").write_text(
                "controller_mode=STATIC_OBJECT\nlocomotion_only_refused=True\nstatic_object=True move_refused=False\n",
                encoding="utf-8")
            self.assertIn("STATIC_OBJECT_MOVE_NOT_REFUSED", br.judge_entry(ok, 0, d, required=()))


class KindAwareAnchorTests(unittest.TestCase):
    def test_dragon_head_uses_alpha_anchor(self):
        self.assertEqual(ct.unity_anchor("head", 0x65), "head_0x65")
        self.assertEqual(ct.unity_anchor("head", 0x02), "center")
        size = (16, 44, 14)                       # dragon-head.cub
        self.assertEqual(_mirror_unity_anchor(ct.unity_anchor("head", 0x65), size), cr.anchor("head", size, kind=0x65))

    def test_csharp_implements_the_dragon_rule(self):
        cs = (ROOT / "tools/alpha_models/transfer/unity/Runtime/AlphaRig.cs").read_text(encoding="utf-8")
        self.assertRegex(cs, r'rule == "head_0x65"\) return new Vector3\(size\.x \* 0\.5f, size\.y \* 0\.2f, size\.z \* 0\.5f\)')
        for r in ct.UNITY_ANCHOR_RULES:
            self.assertIn(f'"{r}"', cs, r)

    @unittest.skipUnless(STATIC.exists(), "needs the static table in .scratch")
    def test_mesh_anchor_equals_clip_anchor_for_every_kind(self):
        """The Unity mesh anchor must equal the anchor the oracle decomposed the joints with
        (alpha_oracle.joints -> creature_rig.anchor(kind=...)), for every body block of every kind."""
        table = json.loads(STATIC.read_text(encoding="utf-8"))
        sizes = {n: tuple(s) for n, s in table["model_sizes"].items() if s}
        reg = table["registry"]
        checked = 0
        for k, e in table["kinds"].items():
            app = e.get("appearance")
            if not app:
                continue
            kind = int(k, 16)
            res = cr.resolve(kind, app)
            for p in res.parts:
                b = cr.BY_NAME[p]
                if b.anchor not in ("center", "head", "tail", "wing"):
                    continue
                name = reg.get(str(res.models[p]))
                if name not in sizes:
                    continue
                self.assertEqual(_mirror_unity_anchor(ct.unity_anchor(b.anchor, kind), sizes[name]),
                                 cr.anchor(b.anchor, sizes[name], kind=kind), f"{k} {p}")
                checked += 1
        self.assertGreater(checked, 500)


@unittest.skipUnless(HAVE_ALPHA and (ROOT / ".scratch/catalog/registry_sizes.json").exists(), "needs .scratch")
class DragonEndToEndTests(unittest.TestCase):
    """Unity-side data (walk formula joints + the UNITY anchor rule) must rebuild the final per-part model
    matrices Alpha's executed drawModel uploads: M = T(-anchor_unity) * J * P. Before D69 the head of 0x65
    used the centre anchor and this check failed by ~13 grid voxels along the neck."""

    def test_final_matrices_equal_executed_drawmodel(self):
        from tools.alpha_models.anim import mat4
        from tools.alpha_models.anim.alpha_clip import pose_error
        from tools.alpha_models.universal import alpha_oracle as ao
        from tools.alpha_models.universal import param_walk as pw
        o = ao.load(str(CUBE), str(ROOT / ".scratch/catalog/registry_sizes.json"))
        worst = 0.0
        for og in (True, False):
            for phase, amp in ((0.0, 0.0), (0.8, 0.19), (4.0, 0.12)):
                em, app = o.emulator(0x65, (0, 0), og, {0x1188: ("<f", amp), 0x118C: ("<f", phase)})
                for dt in (600, 600, 16):
                    fr = em.frame(dt_ms=dt)
                    em.set_f32(0x1188, amp)
                    em.set_f32(0x118C, phase)
                P = fr["placement"]
                names = {}
                for p in fr["parts"]:
                    b = cr.BY_DRAW_SITE[p["draw_site"]]
                    names[b.name] = (p["matrix"], o.sizes[p["model_index"]])
                J = pw.joints(app, pw.WalkState(phase, amp, on_ground=og, head_lean_deg=0.0), list(names))
                for name, (M, size) in names.items():
                    a = _mirror_unity_anchor(ct.unity_anchor(cr.BY_NAME[name].anchor, 0x65), size)
                    rebuilt = mat4.mul(mat4.mul(mat4.translate(-a[0], -a[1], -a[2]), J[name]), P)
                    e = pose_error(rebuilt, M)
                    worst = max(worst, e[0])
                    self.assertLess(e[0], 1e-3, f"{name} og={og} phase={phase}")
                    self.assertLess(e[1], 1e-2, f"{name} og={og} phase={phase}")
        self.assertIn("head", names)


class CensusToolTests(unittest.TestCase):
    def test_summary_groups_by_kind(self):
        from tools.alpha_models.runtime import creature_census as cc
        s = cc.summarise({"0x1": {"kind": 2, "actions_seen": [0], "models_8c_9a": [1, 2]},
                          "0x2": {"kind": 2, "actions_seen": [3], "models_8c_9a": [1, 2]},
                          "0x3": {"kind": 0x1e, "actions_seen": [0], "models_8c_9a": [5]}})
        self.assertEqual(s["0x02"], {"count": 2, "actions_seen": [0, 3], "model_sets": [[1, 2]]})
        self.assertEqual(s["0x1e"]["count"], 1)


if __name__ == "__main__":
    unittest.main()
