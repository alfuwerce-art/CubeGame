// AlphaTransferSelfTests.cs — synthetic self-tests (TEST_DATA_NOT_ALPHA: no Alpha numbers here).
//   -executeMethod CubeGame.AlphaTransfer.Editor.AlphaTransferSelfTests.Run
// Writes <project>/TransferOutput/selftest.txt with SELFTEST_RESULT=PASS|FAIL cases=N failed=M.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CubeGame.AlphaTransfer.Editor
{
    public static class AlphaTransferSelfTests
    {
        static readonly List<(string name, Func<string> run)> Cases = new List<(string, Func<string>)>();

        static void Add(string name, Func<string> run) => Cases.Add((name, run));
        static string Expect(bool cond, string msg) => cond ? null : msg;
        static bool Near(Vector3 a, Vector3 b, float eps = 1e-4f) => (a - b).magnitude <= eps;

        static float[] RowRotZ(float deg)
        {
            float c = Mathf.Cos(deg * Mathf.Deg2Rad), s = Mathf.Sin(deg * Mathf.Deg2Rad);
            return new float[] { c, s, 0, 0, -s, c, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 };
        }

        static float[] RowRotX(float deg)
        {
            float c = Mathf.Cos(deg * Mathf.Deg2Rad), s = Mathf.Sin(deg * Mathf.Deg2Rad);
            return new float[] { 1, 0, 0, 0, 0, c, s, 0, 0, -s, c, 0, 0, 0, 0, 1 };
        }

        static float[] RowScale(float x, float y, float z) => new float[] { x, 0, 0, 0, 0, y, 0, 0, 0, 0, z, 0, 0, 0, 0, 1 };

        static string ClipJson(string rig, string[] parts, float[][] keys, float[] xs, bool loop = false, float duration = 100f, string param = "time_ms")
        {
            var d = new AlphaClipData
            {
                name = "synthetic", status = "TEST_DATA_NOT_ALPHA", rig = rig, param = param, loop = loop,
                duration = duration, parts = parts, capturedSizes = new int[parts.Length * 3],
                keys = new AlphaClipKey[keys.Length], provenance = "{}", validation = "{}",
            };
            for (int i = 0; i < keys.Length; i++) d.keys[i] = new AlphaClipKey { x = xs[i], m = keys[i] };
            return JsonUtility.ToJson(d);
        }

        static float[] Concat(params float[][] ms)
        {
            var o = new List<float>();
            foreach (var m in ms) o.AddRange(m);
            return o.ToArray();
        }

        static void Register()
        {
            Cases.Clear();
            Add("alpha_translation_maps_y_z_swap", () =>
            {
                var u = AlphaSpace.ToUnity(AlphaSpace.Translate(new Vector3(1, 2, 3)));
                return Expect(Near(u.GetColumn(3), new Vector3(1, 3, 2)), $"got {u.GetColumn(3)}");
            });
            Add("alpha_unity_roundtrip_exact", () =>
            {
                var a = AlphaSpace.Mul(AlphaSpace.Mul(RowScale(2, 2, 2), RowRotZ(37)), AlphaSpace.Translate(new Vector3(4, -5, 6)));
                return Expect(AlphaSpace.MaxAbsDiff(AlphaSpace.ToAlpha(AlphaSpace.ToUnity(a)), a) < 1e-6f, "roundtrip differs");
            });
            Add("alpha_x_axis_turns_to_forward", () =>
            {
                // Alpha row-vector RotZ(90): x axis -> +Y (Alpha forward) -> Unity +Z
                var u = AlphaSpace.ToUnity(RowRotZ(90));
                return Expect(Near(u.MultiplyVector(Vector3.right), Vector3.forward), $"got {u.MultiplyVector(Vector3.right)}");
            });
            Add("decompose_keeps_mirror_on_x", () =>
            {
                var a = AlphaSpace.Mul(RowScale(-1.5f, 1.5f, 1.5f), RowRotZ(20));
                var u = AlphaSpace.ToUnity(a);
                if (!AlphaSpace.Decompose(u, out var p, out var r, out var s)) return "decompose failed";
                var back = Matrix4x4.TRS(p, r, s);
                float d = 0; for (int i = 0; i < 16; i++) d = Mathf.Max(d, Mathf.Abs(back[i] - u[i]));
                return Expect(s.x < 0 && d < 1e-5f, $"scale={s} diff={d}");
            });
            Add("decompose_rejects_shear", () =>
            {
                var u = Matrix4x4.identity; u[0, 1] = 0.5f;
                return Expect(!AlphaSpace.Decompose(u, out _, out _, out _), "shear accepted");
            });
            Add("parse_rejects_wrong_rig", () =>
            {
                var p = new List<string>();
                var c = AlphaClip.Parse(ClipJson("other_rig", new[] { "body" }, new[] { AlphaSpace.Translate(Vector3.zero) }, new[] { 0f }), AlphaRig.RigId, p);
                return Expect(c == null && p.Exists(x => x.StartsWith("CLIP_RIG_MISMATCH")), string.Join(";", p));
            });
            Add("parse_rejects_bad_key_size", () =>
            {
                var p = new List<string>();
                var c = AlphaClip.Parse(ClipJson(AlphaRig.RigId, new[] { "body", "head" }, new[] { AlphaSpace.Translate(Vector3.zero) }, new[] { 0f }), AlphaRig.RigId, p);
                return Expect(c == null && p.Exists(x => x.StartsWith("CLIP_KEY_SIZE")), string.Join(";", p));
            });
            Add("parse_rejects_nan", () =>
            {
                var m = AlphaSpace.Translate(Vector3.zero); m[13] = float.NaN;
                var p = new List<string>();
                var json = ClipJson(AlphaRig.RigId, new[] { "body" }, new[] { m }, new[] { 0f });
                var c = AlphaClip.Parse(json, AlphaRig.RigId, p);
                return Expect(c == null && p.Count > 0, "NaN accepted");
            });
            Add("parse_rejects_non_increasing_keys", () =>
            {
                var p = new List<string>();
                var k = AlphaSpace.Translate(Vector3.zero);
                var c = AlphaClip.Parse(ClipJson(AlphaRig.RigId, new[] { "body" }, new[] { k, k }, new[] { 5f, 5f }), AlphaRig.RigId, p);
                return Expect(c == null && p.Exists(x => x.StartsWith("CLIP_KEYS_NOT_INCREASING")), string.Join(";", p));
            });
            Add("parse_rejects_shear_matrix", () =>
            {
                var m = AlphaSpace.Translate(Vector3.zero); m[1] = 0.7f;
                var p = new List<string>();
                var c = AlphaClip.Parse(ClipJson(AlphaRig.RigId, new[] { "body" }, new[] { m }, new[] { 0f }), AlphaRig.RigId, p);
                return Expect(c == null && p.Exists(x => x.StartsWith("CLIP_NOT_TRS")), string.Join(";", p));
            });
            Add("sample_midpoint_and_clamp", () =>
            {
                var p = new List<string>();
                var c = AlphaClip.Parse(ClipJson(AlphaRig.RigId, new[] { "body" },
                    new[] { AlphaSpace.Translate(Vector3.zero), AlphaSpace.Translate(new Vector3(10, 0, 0)) }, new[] { 0f, 100f }), AlphaRig.RigId, p);
                if (c == null) return string.Join(";", p);
                var o = new PartPose[1];
                c.Sample(50f, o); if (!Near(o[0].position, new Vector3(5, 0, 0))) return $"mid {o[0].position}";
                c.Sample(500f, o); return Expect(Near(o[0].position, new Vector3(10, 0, 0)), $"clamp {o[0].position}");
            });
            Add("sample_loop_wraps_last_to_first", () =>
            {
                var p = new List<string>();
                var c = AlphaClip.Parse(ClipJson(AlphaRig.RigId, new[] { "body" },
                    new[] { AlphaSpace.Translate(Vector3.zero), AlphaSpace.Translate(new Vector3(10, 0, 0)) }, new[] { 0f, 1f }, true, 2f, "walk_phase"), AlphaRig.RigId, p);
                if (c == null) return string.Join(";", p);
                var o = new PartPose[1];
                c.Sample(1.5f, o); if (!Near(o[0].position, new Vector3(5, 0, 0))) return $"wrap {o[0].position}";
                c.Sample(2.5f, o); return Expect(Near(o[0].position, new Vector3(5, 0, 0)), $"repeat {o[0].position}");
            });
            Add("anchor_rules", () =>
            {
                var a = AlphaRig.Anchor("center", new Vector3Int(10, 8, 10), false);
                var b = AlphaRig.Anchor("weapon", new Vector3Int(3, 13, 37), false);
                var c = AlphaRig.Anchor("weapon", new Vector3Int(3, 13, 37), true);
                return Expect(Near(a, new Vector3(5, 4, 5)) && Near(b, new Vector3(1.5f, 6.5f, 4f)) && Near(c, new Vector3(1.5f, 6.5f, 18.5f)), $"{a} {b} {c}");
            });
            Add("voxel_mesh_counts_exposed_faces", () =>
            {
                var bytes = new byte[12 + 2 * 3];
                BitConverter.GetBytes(2).CopyTo(bytes, 0); BitConverter.GetBytes(1).CopyTo(bytes, 4); BitConverter.GetBytes(1).CopyTo(bytes, 8);
                bytes[12] = 200; bytes[15] = 100;
                var r = VoxelMesh.Build(bytes, "t");
                return Expect(r.problem == null && r.faces == 10 && r.mesh.vertexCount == 40, $"faces={r.faces} problem={r.problem}");
            });
            Add("voxel_mesh_rejects_bad_length", () =>
            {
                var bytes = new byte[12 + 5];
                BitConverter.GetBytes(2).CopyTo(bytes, 0); BitConverter.GetBytes(1).CopyTo(bytes, 4); BitConverter.GetBytes(1).CopyTo(bytes, 8);
                return Expect(VoxelMesh.Build(bytes, "t").problem != null, "accepted");
            });
            Add("voxel_mesh_axis_order", () =>
            {
                // one voxel at grid (x=0, y=1 depth, z=0) in a 1x2x1 grid -> Unity z in [1,2]
                var bytes = new byte[12 + 2 * 3];
                BitConverter.GetBytes(1).CopyTo(bytes, 0); BitConverter.GetBytes(2).CopyTo(bytes, 4); BitConverter.GetBytes(1).CopyTo(bytes, 8);
                bytes[12 + 3] = 255;
                var r = VoxelMesh.Build(bytes, "t");
                return Expect(r.mesh != null && Mathf.Approximately(r.mesh.bounds.min.z, 1f) && Mathf.Approximately(r.mesh.bounds.max.y, 1f), $"{r.mesh?.bounds}");
            });
            Add("rig_scene_roundtrip_incl_mirror", () =>
            {
                var go = new GameObject("t"); go.transform.localScale = Vector3.one * 0.1f;
                try
                {
                    var rig = go.AddComponent<AlphaRig>();
                    rig.partNames = new[] { "hand_r", "weapon" };
                    rig.parts = new Transform[2]; rig.meshes = new Transform[2];
                    rig.sizes = new[] { new Vector3Int(5, 6, 4), new Vector3Int(3, 13, 37) };
                    rig.anchors = new[] { "center", "weapon" };
                    for (int i = 0; i < 2; i++)
                    {
                        rig.parts[i] = new GameObject("p" + i).transform; rig.parts[i].SetParent(go.transform, false);
                        rig.meshes[i] = new GameObject("mesh").transform; rig.meshes[i].SetParent(rig.parts[i], false);
                    }
                    rig.SetWeaponOnBack(false);
                    var j0 = AlphaSpace.Mul(AlphaSpace.Mul(RowScale(-1.1f, 1.1f, 1.1f), RowRotZ(33)), AlphaSpace.Translate(new Vector3(3, -2, 7)));
                    var j1 = AlphaSpace.Mul(RowRotX(-70), AlphaSpace.Translate(new Vector3(5, 1, -2)));
                    var p = new List<string>();
                    var c = AlphaClip.Parse(ClipJson(AlphaRig.RigId, rig.partNames, new[] { Concat(j0, j1) }, new[] { 0f }), AlphaRig.RigId, p);
                    if (c == null) return string.Join(";", p);
                    var o = new PartPose[2]; c.Sample(0, o); rig.Apply(o, new[] { 0, 1 });
                    float d = 0;
                    for (int i = 0; i < 2; i++)
                        d = Mathf.Max(d, AlphaSpace.MaxAbsDiff(rig.ReadAlphaModelMatrix(i), AlphaSpace.Mul(AlphaSpace.Translate(-rig.AnchorOf(i)), i == 0 ? j0 : j1)));
                    return Expect(d < 1e-4f, $"max diff {d}");
                }
                finally { UnityEngine.Object.DestroyImmediate(go); }
            });
            Add("rig_validate_detects_nonflat", () =>
            {
                var go = new GameObject("t");
                try
                {
                    var rig = go.AddComponent<AlphaRig>();
                    var other = new GameObject("o").transform; other.SetParent(go.transform);
                    rig.partNames = new[] { "body" }; rig.parts = new[] { new GameObject("p").transform };
                    rig.parts[0].SetParent(other);
                    rig.meshes = new[] { new GameObject("m").transform }; rig.meshes[0].SetParent(rig.parts[0]);
                    rig.sizes = new[] { Vector3Int.one }; rig.anchors = new[] { "center" };
                    return Expect(rig.Validate().Exists(x => x.StartsWith("RIG_PART_NOT_FLAT")), "not detected");
                }
                finally { UnityEngine.Object.DestroyImmediate(go); }
            });
            Add("look_pitch_matches_alpha_formula", () =>
            {
                var go = new GameObject("t");
                try
                {
                    var rig = go.AddComponent<AlphaRig>();
                    rig.partNames = new[] { "head" };
                    rig.parts = new[] { new GameObject("part_head").transform }; rig.parts[0].SetParent(go.transform, false);
                    rig.meshes = new[] { new GameObject("mesh").transform }; rig.meshes[0].SetParent(rig.parts[0], false);
                    rig.sizes = new[] { new Vector3Int(18, 16, 16) }; rig.anchors = new[] { "center" };
                    float s = 1.01f, pitch = -7.5f;
                    var j = AlphaSpace.Mul(AlphaSpace.Mul(RowScale(s, s, s), RowRotZ(25)), AlphaSpace.Translate(new Vector3(0.5f, 1, 9)));
                    var player = go.AddComponent<AlphaClipPlayer>();
                    var asset = new TextAsset(ClipJson(AlphaRig.RigId, rig.partNames, new[] { j }, new[] { 0f }));
                    player.clipAssets = new[] { asset };
                    player.LoadClips();
                    player.lookPitchDeg = pitch;
                    if (!player.Play("synthetic")) return string.Join(";", player.Problems);
                    var exp = AlphaSpace.Mul(AlphaSpace.Mul(AlphaSpace.Mul(AlphaSpace.Translate(new Vector3(0, 0, 3f / s)), RowRotX(pitch)), AlphaSpace.Translate(new Vector3(0, 0, -3f / s))), j);
                    exp = AlphaSpace.Mul(AlphaSpace.Translate(-rig.AnchorOf(0)), exp);
                    float d = AlphaSpace.MaxAbsDiff(rig.ReadAlphaModelMatrix(0), exp);
                    return Expect(d < 1e-4f, $"pitch layer differs from Alpha formula by {d}");
                }
                finally { UnityEngine.Object.DestroyImmediate(go); }
            });
        }

        public static void Run()
        {
            Register();
            var sb = new StringBuilder();
            int failed = 0;
            foreach (var (name, run) in Cases)
            {
                string err;
                try { err = run(); } catch (Exception e) { err = "EXCEPTION " + e.Message; }
                if (err != null) failed++;
                sb.AppendLine($"{(err == null ? "PASS" : "FAIL")} {name}{(err == null ? "" : " :: " + err)}");
            }
            var verdict = failed == 0 ? "PASS" : "FAIL";
            sb.Insert(0, $"SELFTEST_RESULT={verdict} cases={Cases.Count} failed={failed} data=TEST_DATA_NOT_ALPHA\n");
            var outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "TransferOutput"));
            Directory.CreateDirectory(outDir);
            File.WriteAllText(Path.Combine(outDir, "selftest.txt"), sb.ToString());
            Debug.Log(sb.ToString());
            if (failed != 0) throw new Exception($"SELFTEST_RESULT={verdict} failed={failed}");
        }
    }
}
