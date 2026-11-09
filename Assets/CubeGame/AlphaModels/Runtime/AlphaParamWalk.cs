// AlphaParamWalk.cs — the Alpha locomotion formulas (Creature_drawModel, state 0) evaluated at runtime.
//
// Mirror of tools/alpha_models/universal/param_walk.py (the reference implementation and the
// documentation of every formula: docs/research/alpha2013/PARAMETRIC_WALK_SYSTEM.md). Alpha keeps
// no per-creature walk animation: every joint frame J is a function of the creature's appearance
// parameters (offsets, scales, pitch_tail, angle_58, flags bit 0) and of the runtime walk phase /
// amplitude / boost / head lean. Output matrices are Alpha row-vector joint frames J (without the
// part anchor), the same quantity stored in .alphaclip keys, so AlphaSpace/AlphaRig apply them.
//
// Status of poses produced here: RECONSTRUCTED_FROM_EVIDENCE (never TRANSFERRED_FROM_ALPHA).
// Terms marked UNKNOWN in the spec (angle_48, hand rest angles) are not implemented; the Python
// side refuses such creatures before a spec is written.
using System;
using UnityEngine;

namespace CubeGame.AlphaTransfer
{
    [Serializable]
    public class AlphaLocomotionParams
    {
        public int flags;
        public float[] size = new float[3];
        public float scale_head = 1f, scale_body = 1f, scale_hand = 1f, scale_foot = 1f, scale_tail = 1f,
                     scale_34 = 1f, scale_44 = 1f;
        public float pitch_tail, angle_48, angle_4c, angle_50, angle_54, angle_58, angle_5c;
        public float[] offset_body = new float[3], offset_head = new float[3], offset_hand = new float[3],
                       offset_foot = new float[3], offset_tail = new float[3], offset_a0 = new float[3];
    }

    [Serializable]
    public class AlphaLocomotionSpec
    {
        public int weaponSubtype = -1;
        public float weaponScale = 1f;
        public string weaponStatus;
        public string format;
        public string kind;
        public string rig;
        public string status;
        public string[] parts;
        public string[] terms;
        public string[] code_only_terms;
        public string[] unknown_terms;
        public AlphaLocomotionParams @params;

        public const string Format = "cubegame.param_walk/1";

        public static AlphaLocomotionSpec Parse(string json, System.Collections.Generic.List<string> problems)
        {
            AlphaLocomotionSpec s;
            try { s = JsonUtility.FromJson<AlphaLocomotionSpec>(json); }
            catch (Exception e) { problems.Add("LOCOMOTION_JSON_INVALID " + e.Message); return null; }
            if (s == null || s.@params == null) { problems.Add("LOCOMOTION_JSON_EMPTY"); return null; }
            if (s.format != Format) problems.Add($"LOCOMOTION_FORMAT {s.format} != {Format}");
            if (s.status == "NEEDS_RESEARCH") problems.Add("LOCOMOTION_NEEDS_RESEARCH formula not established for this creature");
            if (s.unknown_terms != null && s.unknown_terms.Length > 0) problems.Add("LOCOMOTION_UNKNOWN_TERMS " + string.Join(",", s.unknown_terms));
            return s;
        }
    }

    public struct AlphaWalkInput
    {
        public float phase;          // creature+0x118c (rad)
        public float amp;            // creature+0x1188
        public bool boost;           // x1.25 (input/AI acceleration + velocity)
        public float headLeanDeg;    // smoothed head pitch (see HeadLeanTarget / LeanStep)
        public bool onGround;        // creature+0x5c bit 0 (only matters for flags bit 1)
    }

    public static class AlphaParamWalk
    {
        public const float SwingK = 2.1991148f;          // 0.7*pi
        public const float NeckPivot = 3f;
        public const float LeanLerpPerMs = 0.01f;         // array_lerp(.., dt_ms, 0.01)

        public static readonly string[] ModelledParts = { "body", "head", "hair", "tail", "hand_l", "hand_r", "foot_l", "foot_r",
                                                          "slot9a_a", "slot9a_b", "slot98_a", "slot98_b" };

        // ------------------------------------------------------------------ row-vector helpers
        public static float[] RotX(float deg)
        {
            float a = deg * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a);
            return new float[] { 1, 0, 0, 0, 0, c, s, 0, 0, -s, c, 0, 0, 0, 0, 1 };
        }

        public static float[] RotY(float deg)
        {
            float a = deg * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a);
            return new float[] { c, 0, -s, 0, 0, 1, 0, 0, s, 0, c, 0, 0, 0, 0, 1 };
        }

        public static float[] RotZ(float deg)
        {
            float a = deg * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a);
            return new float[] { c, s, 0, 0, -s, c, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 };
        }

        public static float[] Scale(float x, float y, float z) => new float[] { x, 0, 0, 0, 0, y, 0, 0, 0, 0, z, 0, 0, 0, 0, 1 };
        public static float[] Scale(float s) => Scale(s, s, s);
        static float[] T(float x, float y, float z) => AlphaSpace.Translate(new Vector3(x, y, z));
        static float[] T(float[] v) => T(v[0], v[1], v[2]);

        /// <summary>drawModel's mat4_* calls in source order on a base: M = op_n * ... * op_1 * base.</summary>
        public static float[] Chain(float[] baseM, params float[][] ops)
        {
            var m = baseM;
            foreach (var op in ops) m = AlphaSpace.Mul(op, m);
            return m;
        }

        static readonly float[] Identity = { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 };

        // ------------------------------------------------------------------ runtime helpers
        public static float HeadLeanTarget(AlphaLocomotionParams p, bool boost, bool accelerating)
        {
            if ((p.flags & 1) != 0) return 0f;
            if (boost) return 10f;                   // VERIFIED (human)
            return accelerating ? 5f : 0f;           // +5 CODE_ONLY
        }

        public static float LeanStep(float current, float target, float dtMs, float ratePerMs = LeanLerpPerMs)
        {
            float f = 1f - Mathf.Pow(1f - ratePerMs, Mathf.Max(0f, dtMs));
            return current + (target - current) * f;
        }

        /// <summary>Walk phase speed (rad/s) for a horizontal speed in Alpha units/s — INFERRED (see param_walk.py).</summary>
        public static float PhaseRate(AlphaLocomotionParams p, float speedXY) => 1.5f * speedXY * Mathf.Sqrt(0.8f / p.size[0]);

        // ------------------------------------------------------------------ the formula
        public static float[] Joint(AlphaLocomotionParams p, AlphaWalkInput s, string part)
        {
            float c = Mathf.Sin(s.phase + Mathf.PI * 0.5f);
            float w = s.amp * SwingK * c;
            if (s.boost) w *= 1.25f;
            bool grounded = (p.flags & 2) != 0 && !s.onGround;
            if (grounded) w = 0f;
            bool quad = (p.flags & 1) != 0;
            if (quad) w *= 0.75f;
            float wDeg = w * Mathf.Rad2Deg;
            float d0 = 1.5f * wDeg, e4 = -1.5f * wDeg;
            float liftL = grounded ? 0f : s.amp * 4f * c;
            float liftR = grounded ? 0f : s.amp * 4f * (1f - c);
            float bob = 4f * Mathf.Abs(w);
            float footZ = 3f * Mathf.Abs(w);
            var liftOnly = T(0, 0, bob);
            var bobBase = Chain(Identity, T(0, 0, bob), RotX(p.angle_48));      // root pitch (angle_48)
            float hx0 = p.offset_hand[0], hz0 = p.offset_hand[2];

            switch (part)
            {
                case "body":
                    return Chain(bobBase, T(p.offset_body), RotZ(wDeg * 0.5f), Scale(p.scale_body));
                case "head":
                case "hair":
                    return Chain(bobBase, T(p.offset_head[0], p.offset_head[1], p.offset_head[2] - bob * 0.5f),
                        T(0, 0, -NeckPivot), RotX(-p.angle_48 + s.headLeanDeg), T(0, 0, NeckPivot), Scale(p.scale_head));
                case "tail":
                    return Chain(bobBase, T(p.offset_body), T(p.offset_tail), RotX(p.pitch_tail), RotZ(-wDeg),
                        Scale(p.scale_tail), Scale(p.scale_body));
                case "hand_l":
                case "hand_r":
                {
                    bool left = part == "hand_l";
                    float hx = p.offset_hand[0], hy = p.offset_hand[1], hz = p.offset_hand[2];
                    float x, y, z;
                    if (!quad)
                    {
                        float r = hx * s.amp + hx;
                        x = (left ? -1f : 1f) * Mathf.Cos(w) * r;
                        y = hy + (left ? -1f : 1f) * Mathf.Sin(w) * r;
                        z = (hz - 4.1f) + s.amp * 6f;
                    }
                    else { x = left ? -hx : hx; y = hy; z = hz; }
                    y += 2.1f - s.amp * 5f;
                    float pitch = left ? e4 : d0;
                    var sc = left ? Scale(p.scale_hand) : Scale(-p.scale_hand, p.scale_hand, p.scale_hand);
                    // hand rest angles: left (a4c, -a50, -a54), right (a4c, a50, a54)
                    float rx = p.angle_4c, ry = left ? -p.angle_50 : p.angle_50, rz = left ? -p.angle_54 : p.angle_54;
                    // D64: hands do NOT receive the root pitch angle_48 (drawModel oracle, InsectGuard 0x36:
                    // with bobBase the hands were off by exactly angle_48 = -45 deg; with the lift only: 0.0)
                    return quad ? Chain(liftOnly, RotX(pitch), T(x, y, z), RotZ(rz), RotY(ry), RotX(pitch), RotX(rx), sc)
                                : Chain(liftOnly, T(x, y, z), RotZ(rz), RotY(ry), RotX(rx), sc);
                }
                case "foot_l":
                    return Chain(Identity, RotX(p.angle_58 + d0),
                        T(-p.offset_foot[0], p.offset_foot[1], footZ + p.offset_foot[2] + liftL),
                        Scale(p.scale_foot), RotX(d0));
                case "foot_r":
                    return Chain(Identity, RotX(e4),
                        T(p.offset_foot[0], p.offset_foot[1], footZ + p.offset_foot[2] + liftR),
                        Scale(p.scale_foot), RotX(p.angle_58 + e4));
                case "slot9a_a":
                case "slot9a_b":
                {
                    bool left = part == "slot9a_a";
                    float wing = Mathf.Cos(s.phase * 0.5f) * s.amp * 90f - 45f;
                    var v = p.offset_a0;
                    return Chain(liftOnly, T(left ? -v[0] : v[0], v[1], v[2]), RotX(p.angle_5c), RotY(left ? wing : -wing),
                        RotZ(-90f), RotX(90f), Scale(p.scale_44));
                }
                case "slot98_a":
                case "slot98_b":
                {
                    bool left = part == "slot98_a";
                    float r = hx0 * s.amp + hx0;     // recompute the hand point exactly like case hand_*
                    float hx, hy, hz;
                    if (!quad)
                    {
                        hx = (left ? -1f : 1f) * Mathf.Cos(w) * r;
                        hy = p.offset_hand[1] + (left ? -1f : 1f) * Mathf.Sin(w) * r;
                        hz = (hz0 - 4.1f) + s.amp * 6f;
                    }
                    else { hx = left ? -hx0 : hx0; hy = p.offset_hand[1]; hz = hz0; }
                    hy += 2.1f - s.amp * 5f;
                    float sx = left ? (hx0 * -0.3f + hx) * 0.5f : (hx + hx0 * 0.3f) * 0.5f;
                    float sy = (hy - 8f) * 0.25f, sz = (hz + hz0 + 12f) * 0.5f;
                    return Chain(bobBase, T((hx + sx) * 0.5f, (hy + sy) * 0.5f, (hz + sz) * 0.5f), Scale(p.scale_34));
                }
                default:
                    throw new ArgumentException($"part {part} has no locomotion formula");
            }
        }

        public static bool IsModelled(string part) => Array.IndexOf(ModelledParts, part) >= 0;

        /// <summary>D65: the motion the walk FORMULA itself promises for these parts at this amplitude
        /// (max part displacement in voxels and max part rotation in degrees over one cycle). Probes use it
        /// to tell "the clip/formula is nearly static in Alpha" (e.g. head-only creatures: 0.6 voxel) from
        /// "the runtime is frozen while the data moves" — the latter stays a failure.</summary>
        public static void ExpectedMotion(AlphaLocomotionParams p, float amp, string[] parts,
                                          out float maxRotDeg, out float maxMoveVox)
        {
            maxRotDeg = 0f; maxMoveVox = 0f;
            if (parts == null || parts.Length == 0) return;
            const int samples = 64;
            var firstPos = new Vector3[parts.Length]; var firstRot = new Quaternion[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                if (!IsModelled(parts[i])) continue;
                var u = AlphaSpace.ToUnity(Joint(p, new AlphaWalkInput { phase = 0f, amp = amp, onGround = true }, parts[i]));
                AlphaSpace.Decompose(u, out firstPos[i], out firstRot[i], out _);
            }
            for (int s = 1; s <= samples; s++)
            {
                float phase = 2f * Mathf.PI * s / samples;
                for (int i = 0; i < parts.Length; i++)
                {
                    if (!IsModelled(parts[i])) continue;
                    var u = AlphaSpace.ToUnity(Joint(p, new AlphaWalkInput { phase = phase, amp = amp, onGround = true }, parts[i]));
                    AlphaSpace.Decompose(u, out var pos, out var rot, out _);
                    maxMoveVox = Mathf.Max(maxMoveVox, (pos - firstPos[i]).magnitude);
                    maxRotDeg = Mathf.Max(maxRotDeg, Quaternion.Angle(firstRot[i], rot));
                }
            }
        }
    }
}
