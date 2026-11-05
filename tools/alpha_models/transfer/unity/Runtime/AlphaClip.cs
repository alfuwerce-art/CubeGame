// AlphaClip.cs — clip data (as written by tools/alpha_models/transfer/prepare_unity.py), parsing
// with validation, and sampling. Keys hold Alpha JOINT frames J (row-vector, Alpha axes); a part
// model is drawn with Alpha matrix T(-anchor(size)) * J, i.e. in Unity: part transform = U(J) and
// the mesh child sits at -GridToUnity(anchor). Interpolation: lerp position/scale, slerp rotation.
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CubeGame.AlphaTransfer
{
    [Serializable]
    public class AlphaClipKey
    {
        public float x;
        public float[] m;
    }

    [Serializable]
    public class AlphaClipData
    {
        public string name;
        public string status;
        public string rig;
        public string param;          // "time_ms" | "walk_phase"
        public bool loop;
        public float duration;
        public float phaseRate;       // rad/s (walk), 0 for time clips
        public bool weaponOnBack;
        public string[] parts;
        public int[] capturedSizes;
        public AlphaClipKey[] keys;
        public string provenance;
        public string validation;
    }

    public struct PartPose
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
    }

    public sealed class AlphaClip
    {
        public const string TransferredStatus = "TRANSFERRED_FROM_ALPHA";
        public AlphaClipData Data { get; }
        public int PartCount => Data.parts.Length;
        readonly PartPose[][] _poses;   // [key][part]

        AlphaClip(AlphaClipData data, PartPose[][] poses) { Data = data; _poses = poses; }

        public int PartIndex(string part) => Array.IndexOf(Data.parts, part);

        /// <summary>Parse and validate. Problems are returned, never silently ignored.</summary>
        public static AlphaClip Parse(string json, string expectedRig, List<string> problems)
        {
            AlphaClipData d;
            try { d = JsonUtility.FromJson<AlphaClipData>(json); }
            catch (Exception e) { problems.Add($"CLIP_JSON_INVALID {e.Message}"); return null; }
            if (d == null) { problems.Add("CLIP_JSON_EMPTY"); return null; }
            string tag = d.name ?? "<unnamed>";
            if (d.rig != expectedRig) problems.Add($"CLIP_RIG_MISMATCH {tag} rig={d.rig} expected={expectedRig}");
            if (d.param != "time_ms" && d.param != "walk_phase") problems.Add($"CLIP_PARAM_UNKNOWN {tag} {d.param}");
            if (d.parts == null || d.parts.Length == 0) problems.Add($"CLIP_NO_PARTS {tag}");
            if (d.keys == null || d.keys.Length == 0) problems.Add($"CLIP_NO_KEYS {tag}");
            if (string.IsNullOrEmpty(d.status)) problems.Add($"CLIP_NO_STATUS {tag}");
            if (problems.Count > 0) return null;
            int n = d.parts.Length;
            if (d.capturedSizes == null || d.capturedSizes.Length != 3 * n) problems.Add($"CLIP_SIZES_BAD {tag}");
            var poses = new PartPose[d.keys.Length][];
            float prevX = float.NegativeInfinity;
            for (int k = 0; k < d.keys.Length; k++)
            {
                var key = d.keys[k];
                if (key.m == null || key.m.Length != 16 * n) { problems.Add($"CLIP_KEY_SIZE {tag} key={k}"); return null; }
                if (!(key.x > prevX)) problems.Add($"CLIP_KEYS_NOT_INCREASING {tag} key={k}");
                prevX = key.x;
                poses[k] = new PartPose[n];
                for (int p = 0; p < n; p++)
                {
                    for (int i = 0; i < 16; i++)
                        if (float.IsNaN(key.m[16 * p + i]) || float.IsInfinity(key.m[16 * p + i]))
                        { problems.Add($"CLIP_NAN {tag} key={k} part={d.parts[p]}"); return null; }
                    var u = AlphaSpace.ToUnity(key.m, 16 * p);
                    if (!AlphaSpace.Decompose(u, out var pos, out var rot, out var scl))
                    { problems.Add($"CLIP_NOT_TRS {tag} key={k} part={d.parts[p]}"); return null; }
                    poses[k][p] = new PartPose { position = pos, rotation = rot, scale = scl };
                }
            }
            if (d.loop && d.keys[d.keys.Length - 1].x > d.duration) problems.Add($"CLIP_KEY_BEYOND_DURATION {tag}");
            return problems.Count > 0 ? null : new AlphaClip(d, poses);
        }

        /// <summary>Exact Alpha joint matrix of a key (no interpolation) for verification.</summary>
        public float[] KeyMatrix(int key, int part)
        {
            var m = new float[16];
            Array.Copy(Data.keys[key].m, 16 * part, m, 0, 16);
            return m;
        }

        public PartPose KeyPose(int key, int part) => _poses[key][part];

        public void Sample(float x, PartPose[] output)
        {
            var keys = Data.keys;
            int last = keys.Length - 1;
            if (last == 0) { Array.Copy(_poses[0], output, PartCount); return; }
            int a, b; float u;
            if (Data.loop)
            {
                x = Mathf.Repeat(x, Data.duration);
                if (x < keys[0].x || x >= keys[last].x)
                {
                    float span = keys[0].x + Data.duration - keys[last].x;
                    float local = x >= keys[last].x ? x - keys[last].x : x + Data.duration - keys[last].x;
                    a = last; b = 0; u = span > 0 ? local / span : 0f;
                    Blend(a, b, u, output);
                    return;
                }
            }
            else
            {
                if (x <= keys[0].x) { Array.Copy(_poses[0], output, PartCount); return; }
                if (x >= keys[last].x) { Array.Copy(_poses[last], output, PartCount); return; }
            }
            int lo = 0, hi = last;
            while (hi - lo > 1) { int mid = (lo + hi) / 2; if (keys[mid].x <= x) lo = mid; else hi = mid; }
            a = lo; b = hi;
            u = (x - keys[a].x) / (keys[b].x - keys[a].x);
            Blend(a, b, u, output);
        }

        void Blend(int a, int b, float u, PartPose[] output)
        {
            for (int p = 0; p < PartCount; p++)
            {
                var pa = _poses[a][p]; var pb = _poses[b][p];
                output[p] = new PartPose
                {
                    position = Vector3.LerpUnclamped(pa.position, pb.position, u),
                    rotation = Quaternion.Slerp(pa.rotation, pb.rotation, u),
                    scale = Vector3.LerpUnclamped(pa.scale, pb.scale, u),
                };
            }
        }
    }
}
