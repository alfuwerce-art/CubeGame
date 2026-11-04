// S05A-1 pilot — technical procedural character animation (NOT an Alpha animation).
//
// Cube World Alpha ships no animation clips: the shipped assets contain only *.cub models,
// *.wav, *.png, *.xml and UI *.plx streams, and the client drives creature poses from code
// (state id + bone matrices). The original cycle is therefore not reproduced here; this
// component is an explicitly labelled technical cycle whose only purpose is to prove that
// the assembled hierarchy rotates around sane pivots, keeps its parts connected, keeps the
// weapon attached to the hand and loops cleanly.
//
// Hierarchy contract (see AssemblySpec): body is the root part; head is a child of body,
// hair a child of head, hands and feet are children of body, the sword is a child of the
// right hand. Pivots of limbs sit at the joint, the mesh hangs below/at the pivot.

using UnityEngine;

namespace CubeGame.CharacterPilot
{
    [DisallowMultipleComponent]
    public class ProceduralCharacterAnimator : MonoBehaviour
    {
        public const string ClipName = "technical_walk_NOT_ALPHA";
        public const float CycleSeconds = 1.6f;

        public float Speed = 1f;
        public bool AnimateInPlayMode = true;

        Transform _body, _head, _hair, _handLeft, _handRight, _footLeft, _footRight, _sword;
        Vector3 _bodyHome, _headHome, _handLeftHome, _handRightHome, _footLeftHome, _footRightHome;
        Quaternion _bodyHomeRot, _headHomeRot, _handLeftHomeRot, _handRightHomeRot;

        /// <summary>
        /// True only when all eight parts of the character contract are wired:
        /// body, head, hair, hand_l, hand_r, foot_l, foot_r, sword. PlayMode refuses to run otherwise.
        /// </summary>
        public bool IsBound =>
            _body != null && _head != null && _hair != null &&
            _handLeft != null && _handRight != null &&
            _footLeft != null && _footRight != null && _sword != null;

        void Awake()
        {
            // Entering PlayMode reloads the scene, so the private binding fields are null there.
            if (_body == null) AutoBind();
        }

        /// <summary>Finds the parts by name; used after a scene reload.</summary>
        public void AutoBind()
        {
            Transform Find(string partName)
            {
                foreach (var t in GetComponentsInChildren<Transform>(true))
                    if (t.name == partName) return t;
                return null;
            }
            Bind(Find("part_body"), Find("part_head"), Find("part_hair"), Find("part_hand_l"),
                Find("part_hand_r"), Find("part_foot_l"), Find("part_foot_r"), Find("part_sword"));
        }

        public void Bind(Transform body, Transform head, Transform hair, Transform handLeft,
                         Transform handRight, Transform footLeft, Transform footRight, Transform sword)
        {
            if (body == null || head == null || hair == null || handLeft == null ||
                handRight == null || footLeft == null || footRight == null || sword == null)
            {
                // The contract is all eight parts: a character without hair or weapon is not animateable
                // for this pilot, so the animator stays unbound (IsBound == false) and disabled.
                Debug.LogWarning("ProceduralCharacterAnimator: incomplete binding (need body, head, hair, " +
                                 "hand_l, hand_r, foot_l, foot_r, sword), animation disabled");
                _body = _head = _hair = _handLeft = _handRight = _footLeft = _footRight = _sword = null;
                enabled = false;
                return;
            }
            enabled = true;
            _body = body; _head = head; _hair = hair;
            _handLeft = handLeft; _handRight = handRight;
            _footLeft = footLeft; _footRight = footRight; _sword = sword;

            _bodyHome = _body.localPosition; _headHome = _head.localPosition;
            _handLeftHome = _handLeft.localPosition; _handRightHome = _handRight.localPosition;
            _footLeftHome = _footLeft.localPosition; _footRightHome = _footRight.localPosition;
            _bodyHomeRot = _body.localRotation; _headHomeRot = _head.localRotation;
            _handLeftHomeRot = _handLeft.localRotation; _handRightHomeRot = _handRight.localRotation;
        }

        /// <summary>Deterministic pose at phase <paramref name="t"/> (0..1, wraps).</summary>
        public void Sample(float t)
        {
            if (_body == null || !enabled) return;
            t = Mathf.Repeat(t, 1f);
            float w = t * Mathf.PI * 2f;

            // vertical bob of the whole body, two bounces per cycle
            _body.localPosition = _bodyHome + new Vector3(0f, Mathf.Abs(Mathf.Sin(w)) * 0.35f, 0f);
            _body.localRotation = _bodyHomeRot * Quaternion.Euler(0f, 0f, Mathf.Sin(w) * 2f);

            // head: gentle counter-rotation and a slow independent yaw (head-turn check)
            _head.localRotation = _headHomeRot * Quaternion.Euler(Mathf.Sin(w * 2f) * 3f,
                                                                  Mathf.Sin(w * 0.5f) * 14f,
                                                                  0f);

            // arms swing in opposite phase around the shoulder pivot
            _handLeft.localRotation = _handLeftHomeRot * Quaternion.Euler(Mathf.Sin(w) * 26f, 0f, 0f);
            _handRight.localRotation = _handRightHomeRot * Quaternion.Euler(-Mathf.Sin(w) * 26f, 0f, 0f);

            // legs step: lift and slide, opposite phase
            float leftLift = Mathf.Max(0f, Mathf.Sin(w)) * 0.55f;
            float rightLift = Mathf.Max(0f, -Mathf.Sin(w)) * 0.55f;
            _footLeft.localPosition = _footLeftHome + new Vector3(0f, leftLift, Mathf.Cos(w) * 0.45f);
            _footRight.localPosition = _footRightHome + new Vector3(0f, rightLift, -Mathf.Cos(w) * 0.45f);

            // Hair and sword are children and are deliberately left untouched: their local offsets
            // are the authored placement, and the socket invariant checks that they stay constant.
            // (An earlier version reset the sword offset here, which the socket test caught.)
        }

        void Update()
        {
            if (!AnimateInPlayMode) return;
            Sample(Time.time / CycleSeconds);
        }
    }
}
