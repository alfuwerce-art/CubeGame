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
        public int[] visible; // v3: one 0/1 per part; m contains ONLY visible matrices
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
        public int actionCode = -1, drawState;
        public float actionEndMs, recoveryStartMs;
        public string category = "MOTION";
        public float nativeIdleAtMs, gripEndMs;
        public bool twoHandedAttack;
        public string visibilityMode = "STATIC_ALL";
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
        readonly bool[][] _visible;
        readonly int _weaponPivotPart;
        readonly Vector3 _weaponGripOffset;

        AlphaClip(AlphaClipData data, PartPose[][] poses, bool[][] visible)
        {
            Data=data;_poses=poses;_visible=visible;
            _weaponPivotPart=data.twoHandedAttack && data.weaponOnBack ? Array.IndexOf(data.parts,"weapon") : -1;
            if(_weaponPivotPart>=0)_weaponGripOffset=new Vector3(0,4-data.capturedSizes[3*_weaponPivotPart+2]*.5f,0);
        }

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
            if (string.IsNullOrEmpty(d.name)) problems.Add("CLIP_NO_NAME");
            if (float.IsNaN(d.duration) || float.IsInfinity(d.duration) || d.duration < 0) problems.Add($"CLIP_DURATION_INVALID {tag}");
            if(d.category!="MOTION"&&d.category!="HELD_STATE"&&d.category!="NATIVE_LIFECYCLE")
                problems.Add("CLIP_CATEGORY_UNKNOWN "+tag);
            if(d.category=="HELD_STATE"&&(d.loop||d.param!="time_ms"||d.actionCode<0||d.actionEndMs!=0||d.recoveryStartMs!=0))
                problems.Add("HELD_TIMELINE_INVALID "+tag);
            if(d.category=="NATIVE_LIFECYCLE"&&(d.loop||d.param!="time_ms"||d.actionCode<0||!(d.gripEndMs>0)
                ||!(d.actionEndMs<d.nativeIdleAtMs)||!(d.gripEndMs<d.nativeIdleAtMs)
                ||d.recoveryStartMs!=d.nativeIdleAtMs||!(d.nativeIdleAtMs<d.duration)))
                problems.Add("NATIVE_TIMELINE_INVALID "+tag);
            if (problems.Count > 0) return null;
            int n = d.parts.Length;
            if (new HashSet<string>(d.parts).Count != n) problems.Add($"CLIP_DUPLICATE_PART {tag}");
            if (d.keys.Length > 1 && !(d.duration > 0)) problems.Add($"CLIP_DURATION_INVALID {tag}");
            if (d.capturedSizes == null || d.capturedSizes.Length != 3 * n) problems.Add($"CLIP_SIZES_BAD {tag}");
            bool dynamic=d.visibilityMode=="EXPLICIT_NATIVE_DRAW";
            if(!dynamic && !string.IsNullOrEmpty(d.visibilityMode) && d.visibilityMode!="STATIC_ALL")problems.Add("CLIP_VISIBILITY_MODE_UNKNOWN "+tag);
            var poses = new PartPose[d.keys.Length][];
            var visible=new bool[d.keys.Length][];
            float prevX = float.NegativeInfinity;
            for (int k = 0; k < d.keys.Length; k++)
            {
                var key = d.keys[k];
                if (key == null || float.IsNaN(key.x) || float.IsInfinity(key.x) || key.x < 0)
                { problems.Add($"CLIP_TIME_INVALID {tag} key={k}"); return null; }
                visible[k]=new bool[n];int matrixCount=0;
                if(dynamic && (key.visible==null || key.visible.Length!=n)){problems.Add("CLIP_VISIBILITY_MISSING "+tag);return null;}
                if(!dynamic && key.visible!=null && key.visible.Length>0){problems.Add("CLIP_UNDECLARED_VISIBILITY "+tag);return null;}
                for(int p=0;p<n;p++)
                {
                    if(dynamic && key.visible[p]!=0 && key.visible[p]!=1){problems.Add("CLIP_VISIBILITY_INVALID "+tag);return null;}
                    visible[k][p]=!dynamic||key.visible[p]==1;if(visible[k][p])matrixCount++;
                }
                if (key.m == null || key.m.Length != 16 * matrixCount) { problems.Add($"CLIP_KEY_SIZE {tag} key={k}"); return null; }
                if (!(key.x > prevX)) problems.Add($"CLIP_KEYS_NOT_INCREASING {tag} key={k}");
                prevX = key.x;
                poses[k] = new PartPose[n];
                int offset=0;
                for (int p = 0; p < n; p++)
                {
                    if(!visible[k][p])continue; // absent native draw: no fabricated pose
                    for (int i = 0; i < 16; i++)
                        if (float.IsNaN(key.m[offset + i]) || float.IsInfinity(key.m[offset + i]))
                        { problems.Add($"CLIP_NAN {tag} key={k} part={d.parts[p]}"); return null; }
                    var u = AlphaSpace.ToUnity(key.m, offset);offset+=16;
                    if (!AlphaSpace.Decompose(u, out var pos, out var rot, out var scl))
                    { problems.Add($"CLIP_NOT_TRS {tag} key={k} part={d.parts[p]}"); return null; }
                    poses[k][p] = new PartPose { position = pos, rotation = rot, scale = scl };
                }
            }
            if (d.keys[d.keys.Length - 1].x > d.duration) problems.Add($"CLIP_KEY_BEYOND_DURATION {tag}");
            if (!d.loop && d.keys.Length > 1 && d.keys[d.keys.Length - 1].x != d.duration) problems.Add($"CLIP_END_KEY_MISSING {tag}");
            return problems.Count > 0 ? null : new AlphaClip(d, poses,visible);
        }

        /// <summary>Exact Alpha joint matrix of a key (no interpolation) for verification.</summary>
        public float[] KeyMatrix(int key, int part)
        {
            if(!_visible[key][part])throw new InvalidOperationException("HIDDEN_PART_HAS_NO_MATRIX");
            var m = new float[16];
            int offset=0;for(int p=0;p<part;p++)if(_visible[key][p])offset+=16;
            Array.Copy(Data.keys[key].m, offset, m, 0, 16);
            return m;
        }

        public PartPose KeyPose(int key, int part) => _visible[key][part]?_poses[key][part]:throw new InvalidOperationException("HIDDEN_PART_HAS_NO_POSE");
        public bool KeyVisible(int key,int part)=>_visible[key][part];

        public void SampleVisibility(float x,bool[] output)
        {
            if(Data.loop && Data.keys.Length>1)x=Mathf.Repeat(x,Data.duration);
            int lo=0,hi=Data.keys.Length;
            while(lo+1<hi){int mid=(lo+hi)/2;if(Data.keys[mid].x<=x)lo=mid;else hi=mid;}
            if(Data.loop && x<Data.keys[0].x)lo=Data.keys.Length-1;
            Array.Copy(_visible[lo],output,PartCount);
        }

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
                if(!_visible[a][p]){output[p]=default;continue;}
                if(!_visible[b][p]){output[p]=_poses[a][p];continue;}
                var pa = _poses[a][p]; var pb = _poses[b][p];
                // Fixed-back storage must interpolate a swinging weapon around the actual grip.
                // Lerp of model-centre translations opens an artificial hand gap between exact keys.
                if(p==_weaponPivotPart)
                {
                    pa.position+=pa.rotation*Vector3.Scale(pa.scale,_weaponGripOffset);
                    pb.position+=pb.rotation*Vector3.Scale(pb.scale,_weaponGripOffset);
                }
                output[p] = new PartPose
                {
                    position = Vector3.LerpUnclamped(pa.position, pb.position, u),
                    rotation = Quaternion.Slerp(pa.rotation, pb.rotation, u),
                    scale = Vector3.LerpUnclamped(pa.scale, pb.scale, u),
                };
                if(p==_weaponPivotPart)
                    output[p].position-=output[p].rotation*Vector3.Scale(output[p].scale,_weaponGripOffset);
            }
        }
    }
}
