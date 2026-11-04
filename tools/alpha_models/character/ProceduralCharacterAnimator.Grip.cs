// S05A-2 GRIP (D52) — two-handed attack for the technical humanoid.
//
// Status: ORIGINAL_NOT_ALPHA. The pose is authored by us; it is NOT a transfer of the original animation.
// The original attack could not be recovered: a controlled Alpha session did not produce usable frames,
// and the static analysis of Creature_drawModel did not yield the per-state pose table (see the report).
//
// What the animation guarantees (checked by the verifier, not by eyes only):
//   * the right hand keeps the primary grip on the handle;
//   * during the two-handed phases the left hand is driven to the secondary grip (real world-space solve);
//   * the blade stays clear of the head and torso in every sampled phase;
//   * phase 1 returns to the idle pose (loop closure) and no transform jumps between phases.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace CubeGame.CharacterPilot
{

public partial class ProceduralCharacterAnimator : MonoBehaviour
    {
        public const string TwoHandedClipName = "two_handed_attack_NOT_ALPHA";
        // Alpha timings (RECONSTRUCTED_FROM_EVIDENCE): combat_getAttackWindup@0x0043caa0 = 100 ms,
        // combat_getAttackTiming@0x0043d1a0 contact = 100 ms, combat_getReloadFrames@0x00447310 recovery = 200 ms
        // for ability types 2..4. Poses stay ORIGINAL_NOT_ALPHA; only the phase timing comes from Alpha.
        public const float TwoHandedWindupSeconds = 0.100f;
        public const float TwoHandedContactSeconds = 0.100f;
        public const float TwoHandedRecoverySeconds = 0.200f;
        public const float TwoHandedCycleSeconds = TwoHandedWindupSeconds + TwoHandedContactSeconds + TwoHandedRecoverySeconds;

        /// <summary>Grip data derived from the weapon geometry (voxel space).</summary>
        public Vector3 PrimaryGripVoxel;
        public Vector3 SecondaryGripVoxel;

        /// <summary>Local point of the palm inside the hand part (voxel units).</summary>
        public Vector3 LeftHandGripLocal;

        /// <summary>Enables the two-handed attack instead of the walk cycle.</summary>
        public bool TwoHandedAttack;

        Transform _gripSword, _gripLeftHand;

        public void BindGrip(Transform sword, Transform leftHand, Vector3 primary, Vector3 secondary,
            Vector3 leftHandGripLocal)
        {
            _gripSword = sword;
            _gripLeftHand = leftHand;
            PrimaryGripVoxel = primary;
            SecondaryGripVoxel = secondary;
            LeftHandGripLocal = leftHandGripLocal;
        }

        /// <summary>Attack phase 0..1 mapped to named stages (for the report and the tests).</summary>
        public static string PhaseName(float t)
        {
            t = Mathf.Repeat(t, 1f);
            if (t < 0.25f) return "windup";
            if (t < 0.45f) return "strike";
            if (t < 0.70f) return "contact";
            if (t < 0.95f) return "recovery";
            return "idle";
        }

        /// <summary>Deterministic two-handed attack pose; t = 0 and t = 1 are the same idle pose.</summary>
        void SampleTwoHanded(float t)
        {
            t = Mathf.Repeat(t, 1f);
            // smoothstep-like shaping so the strike is fast and the recovery is slow
            float windup = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.25f));
            float strike = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.25f) / 0.20f));
            float recover = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.70f) / 0.30f));

            // body: lean back during the windup, drive forward on the strike, return on recovery
            float lean = -22f * windup + 40f * strike - 18f * recover;
            _body.localRotation = _bodyHomeRot * Quaternion.Euler(lean * 0.35f, 0f, 0f);
            _body.localPosition = _bodyHome + new Vector3(0f, -0.15f * strike + 0.15f * recover, 0.6f * strike - 0.6f * recover);

            // right arm raises the blade behind the head, then swings down and forward
            float armLift = 70f * windup - 110f * strike + 40f * recover;
            _handRight.localRotation = _handRightHomeRot * Quaternion.Euler(armLift, 0f, 0f);
            _handRight.localPosition = _handRightHome + new Vector3(0f, 1.2f * windup - 2.0f * strike + 0.8f * recover, 0f);

            // head follows the target, not the blade
            _head.localRotation = _headHomeRot * Quaternion.Euler(6f * windup - 14f * strike + 8f * recover, 0f, 0f);

            // feet: a small step on the strike
            _footLeft.localPosition = _footLeftHome + new Vector3(0f, 0f, 0.5f * strike - 0.5f * recover);
            _footRight.localPosition = _footRightHome + new Vector3(0f, 0f, -0.4f * strike + 0.4f * recover);
        }

        /// <summary>
        /// Places the left hand on the secondary grip after the sword has been posed by the right hand.
        /// This is a real constraint solve in world space, not an overlay.
        /// </summary>
        void AttachLeftHandToSecondaryGrip()
        {
            if (_gripSword == null || _gripLeftHand == null) return;
            var target = _gripSword.TransformPoint(SecondaryGripVoxel);
            // align the palm with the handle axis, then move the hand so its palm point hits the target
            _gripLeftHand.rotation = _gripSword.rotation;
            var palmOffset = _gripLeftHand.rotation * Vector3.Scale(LeftHandGripLocal, _gripLeftHand.lossyScale);
            _gripLeftHand.position = target - palmOffset;
        }

        /// <summary>Distance between the left palm and the secondary grip (world units), for the validators.</summary>
        public float LeftPalmToSecondaryGrip()
        {
            if (_gripSword == null || _gripLeftHand == null) return float.NaN;
            return Vector3.Distance(_gripLeftHand.TransformPoint(Vector3.Scale(LeftHandGripLocal, _gripLeftHand.lossyScale)),
                _gripSword.TransformPoint(SecondaryGripVoxel));
        }
    }
}
