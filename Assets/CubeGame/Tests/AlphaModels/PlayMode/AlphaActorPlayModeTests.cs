// D86 M0-01 — PlayMode checks: a real Alpha actor is built from the local content cache, walks with
// the accepted formula, plays verified action clips on an external clock and switches the weapon
// anchor. Content-dependent tests skip with an explicit reason when the cache is not staged.
using System.Collections;
using System.Collections.Generic;
using CubeGame.AlphaModels;
using CubeGame.AlphaTransfer;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CubeGame.AlphaModels.Tests.PlayMode
{
    public class AlphaActorPlayModeTests
    {
        static AlphaActorView Spawn(string id, out AlphaActorDefinition def)
        {
            var lib = new AlphaActorLibrary(AlphaContentSource.Resolve());
            if (!lib.TryGet(id, out def))
                Assert.Ignore("local Alpha content cache is not usable here (" + lib.Diagnosis + ") for " + id);
            var problems = new List<string>();
            var view = AlphaActorView.Spawn(def, null, problems);
            Assert.IsNotNull(view);
            Assert.IsTrue(view.Ready, string.Join("\n", view.Problems));
            return view;
        }

        [UnityTest]
        public IEnumerator HumanWalksAndPlaysAnAlphaAction()
        {
            var view = Spawn("d86_human_0x02_greatsword", out var def);
            try
            {
                Assert.AreEqual(8, view.Rig.parts.Length, "body, head, hair, weapon, two hands, two feet");
                Assert.AreSame(def, view.Definition);
                Assert.AreEqual(AlphaControllerPhase.Locomotion, view.Controller.Phase);

                int headIndex = view.Rig.IndexOf("head");
                var head = view.Rig.parts[headIndex];
                view.ApplyLocomotion(AlphaLocomotionState.Standing);
                for (int i = 0; i < 5; i++) view.Tick(1f / 60f);
                var rest = head.localPosition;

                view.ApplyLocomotion(new AlphaLocomotionState { SpeedXY = 3.7f, Amplitude = 0.19f, OnGround = true });
                int framesBefore = view.Locomotion.FramesApplied;
                for (int i = 0; i < 20; i++) view.Tick(1f / 60f);
                Assert.Greater(view.Locomotion.FramesApplied, framesBefore, "the walk formula must pose the rig every tick");
                Assert.Greater(Vector3.Distance(rest, head.localPosition), 1e-4f, "walking changes the head pose");

                Assert.IsTrue(view.TryPlayAction(3, out var reason), "action 3 is in this kind's verified profile: " + reason);
                // A moving actor first stops (bounded UNITY_TRANSITION), then the action starts.
                int enter = 0;
                while (view.Controller.CurrentAction == null && enter++ < 120) view.Tick(1f / 60f);
                Assert.AreEqual("action_003", view.Controller.CurrentAction);
                for (int i = 0; i < 30; i++) view.Tick(1f / 60f);
                Assert.IsTrue(view.Controller.InAction);
                int guard = 0;
                while (view.Controller.InAction && guard++ < 600) view.Tick(1f / 60f);
                Assert.IsFalse(view.Controller.InAction, "the action must end on its own clip timeline");
                Assert.AreEqual(AlphaControllerPhase.Locomotion, view.Controller.Phase);

                Assert.IsFalse(view.TryPlayAction(22, out var blocked), "action 22 was never launched for kind 0x02");
                StringAssert.Contains("ACTION_NOT_IN_KIND_PROFILE", blocked);
                yield return null;
            }
            finally
            {
                Object.Destroy(view.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator LocomotionPoseIsFrameRateIndependent()
        {
            var a = Spawn("d86_human_0x02_greatsword", out _);
            var b = Spawn("d86_human_0x02_greatsword", out _);
            try
            {
                var state = new AlphaLocomotionState { SpeedXY = 3.7f, Amplitude = 0.19f, OnGround = true };
                a.ApplyLocomotion(state);
                b.ApplyLocomotion(state);
                for (int i = 0; i < 30; i++) a.Tick(1f / 30f);
                for (int i = 0; i < 120; i++) b.Tick(1f / 120f);
                int head = a.Rig.IndexOf("head");
                Assert.AreEqual(a.Locomotion.phase, b.Locomotion.phase, 1e-3f, "the walk phase is time-based, not frame-based");
                Assert.Less(Vector3.Distance(a.Rig.parts[head].localPosition, b.Rig.parts[head].localPosition), 2e-3f,
                    "30 Hz and 120 Hz must land on the same pose after one second");
                yield return null;
            }
            finally
            {
                Object.Destroy(a.gameObject);
                Object.Destroy(b.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator WeaponAnchorSwitchesBetweenHandAndBack()
        {
            var view = Spawn("d86_human_0x02_greatsword", out _);
            try
            {
                int weapon = view.Rig.IndexOf("weapon");
                Assert.GreaterOrEqual(weapon, 0);
                Assert.IsTrue(view.Rig.WeaponOnBack);
                var onBack = view.Rig.meshes[weapon].localPosition;
                view.Rig.SetWeaponOnBack(false);
                Assert.IsFalse(view.Rig.WeaponOnBack);
                var inHand = view.Rig.meshes[weapon].localPosition;
                Assert.Greater(Vector3.Distance(onBack, inHand), 1e-3f, "the hand anchor differs from the back anchor");
                // documented anchors: back = centre of the grid, hand = centre shifted 4 voxels forward
                var size = view.Rig.sizes[weapon];
                Assert.AreEqual(new Vector3(-size.x * 0.5f, -size.z * 0.5f, -size.y * 0.5f), onBack);
                Assert.AreEqual(new Vector3(-size.x * 0.5f, -4f, -size.y * 0.5f), inHand);
                yield return null;
            }
            finally
            {
                Object.Destroy(view.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator CreatureWithoutActionProfileWalksAndRefusesActions()
        {
            var view = Spawn("whitecat_0x20_formula_vs_alpha", out var def);
            try
            {
                Assert.IsTrue(view.Controller.locomotionOnly);
                Assert.IsFalse(view.TryPlayAction(3, out var reason));
                Assert.AreEqual("ACTIONS_BLOCKED", reason);
                var body = view.Rig.parts[view.Rig.IndexOf("body")];
                var rest = body.localPosition;
                view.ApplyLocomotion(new AlphaLocomotionState { SpeedXY = 3.5f, Amplitude = 0.19f, OnGround = true });
                for (int i = 0; i < 15; i++) view.Tick(1f / 30f);
                Assert.Greater(Vector3.Distance(rest, body.localPosition), 1e-4f);
                Assert.AreEqual("LOCOMOTION_ONLY", def.ControllerMode);
                yield return null;
            }
            finally
            {
                Object.Destroy(view.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator DragonPlaysItsTransferredActionWithTheD83HeadAnchor()
        {
            var view = Spawn("d86_dragon_0x65", out var def);
            try
            {
                var head = def.Rig.parts[System.Array.FindIndex(def.Rig.parts, p => p.name == "head")];
                Assert.AreEqual("head_0x65", head.anchor);
                Assert.IsTrue(view.TryPlayAction(37, out var reason), "action 37 was transferred for kind 0x65: " + reason);
                int enter = 0;
                while (view.Controller.CurrentAction == null && enter++ < 120) view.Tick(1f / 60f);
                Assert.AreEqual("action_037", view.Controller.CurrentAction);
                for (int i = 0; i < 20; i++) view.Tick(1f / 60f);
                Assert.IsTrue(view.Clips.FramesApplied > 0);
                yield return null;
            }
            finally
            {
                Object.Destroy(view.gameObject);
            }
        }
    }
}
