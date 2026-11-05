// AlphaTransferEditor.cs — batch entry points of the Alpha animation transfer pilot (S05A-2).
//
//   Unity.exe -batchmode -quit -projectPath <P> -executeMethod CubeGame.AlphaTransfer.Editor.AlphaTransferEditor.Run
//   Unity.exe -batchmode -quit -projectPath <P> -executeMethod CubeGame.AlphaTransfer.Editor.AlphaTransferEditor.RunSelfTests
//   Unity.exe -batchmode        -projectPath <P> -executeMethod CubeGame.AlphaTransfer.Editor.AlphaTransferEditor.RunPlayMode
//
// Run: builds the rig from Assets/AlphaTransfer/Data (rig.json + models/*.cubdec + clips/*.json),
// saves Assets/AlphaTransfer/AlphaTransferScene.unity and verifies (results: <P>/TransferOutput):
//   GEOMETRY   Unity vertex count == expectedVertices (same rule as Alpha's mesh, see prepare_unity)
//   CLIPS      every clip parses, rig/parts match, status recorded
//   ROUNDTRIP  at every key and part: Unity scene matrix converted back to Alpha == T(-anchor)*J
//   SEAM       loop clips: pose at duration-eps continues into the pose at 0
//   GRIP       attack clips: main hand centre on the grip point, second hand on the handle
//   SHOTS      PNG frames (idle / walk / attack) and contact sheets
// Markers: TRANSFER_RESULT=PASS|FAIL, SELFTEST_RESULT=PASS|FAIL, PLAYMODE_RESULT=PASS|FAIL.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CubeGame.AlphaTransfer.Editor
{
    public static class AlphaTransferEditor
    {
        const string Root = "Assets/AlphaTransfer";
        const string DataDir = Root + "/Data";
        const string GenDir = Root + "/Generated";
        const string ScenePath = Root + "/AlphaTransferScene.unity";
        static string OutDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "TransferOutput"));
        public const float RootScale = AlphaTransferConsts.RootScale;   // 1 voxel = 0.1 Unity units

        // ------------------------------------------------------------------ build
        public static AlphaRigSpec LoadSpec(List<string> problems)
        {
            var path = Path.Combine(DataDir, "rig.json");
            if (!File.Exists(path)) { problems.Add($"RIG_SPEC_MISSING {path}"); return null; }
            var spec = JsonUtility.FromJson<AlphaRigSpec>(File.ReadAllText(path));
            if (spec == null || spec.parts == null || spec.parts.Length == 0) { problems.Add("RIG_SPEC_EMPTY"); return null; }
            bool creature = spec.rig != null && spec.rig.StartsWith(AlphaRig.CreatureRigPrefix) && spec.rig.Length > AlphaRig.CreatureRigPrefix.Length;
            if (spec.rig != AlphaRig.RigId && !creature) problems.Add($"RIG_ID_MISMATCH {spec.rig}");
            bool hasWeapon = spec.parts.Any(p => p.name == "weapon");
            if ((spec.rig == AlphaRig.RigId || hasWeapon) && Array.IndexOf(AlphaRig.TwoHandedSubtypes, spec.weaponSubtype) < 0)
                problems.Add($"WEAPON_SUBTYPE_NOT_VERIFIED {spec.weaponSubtype} (verified: 15,16,17)");
            foreach (var p in spec.parts)
                if (p.anchor != "center" && p.anchor != "weapon" && p.anchor != "tail" && p.anchor != "wing")
                    problems.Add($"ANCHOR_RULE_NOT_ESTABLISHED {p.name} {p.anchor}");
            return spec;
        }

        public static GameObject BuildRig(AlphaRigSpec spec, List<string> problems, Dictionary<string, int> vertexCounts)
        {
            Directory.CreateDirectory(GenDir);
            var shader = Shader.Find("AlphaTransfer/VoxelShaded");
            if (shader == null) { problems.Add("SHADER_MISSING AlphaTransfer/VoxelShaded"); shader = Shader.Find("Unlit/Color"); }
            var mat = new Material(shader) { name = "AlphaVoxel" };
            AssetDatabase.CreateAsset(mat, GenDir + "/AlphaVoxel.mat");

            var go = new GameObject(spec.rig == AlphaRig.RigId ? "AlphaHumanoid" : "AlphaCreature");
            go.transform.localScale = Vector3.one * RootScale;
            var rig = go.AddComponent<AlphaRig>();
            rig.rigId = spec.rig;
            rig.variant = spec.variant;
            rig.weaponSubtype = spec.weaponSubtype;
            int n = spec.parts.Length;
            rig.partNames = new string[n]; rig.parts = new Transform[n]; rig.meshes = new Transform[n];
            rig.sizes = new Vector3Int[n]; rig.anchors = new string[n];
            for (int i = 0; i < n; i++)
            {
                var ps = spec.parts[i];
                var bytes = File.ReadAllBytes(Path.Combine(DataDir, ps.file));
                var built = VoxelMesh.Build(bytes, ps.name + "_" + Path.GetFileNameWithoutExtension(ps.model));
                if (built.problem != null) problems.Add($"MESH {ps.name}: {built.problem}");
                if (built.size != new Vector3Int(ps.size[0], ps.size[1], ps.size[2]))
                    problems.Add($"MESH_SIZE {ps.name} {built.size} != spec");
                if (built.mesh != null)
                {
                    AssetDatabase.CreateAsset(built.mesh, $"{GenDir}/{ps.name}.asset");
                    vertexCounts[ps.name] = built.mesh.vertexCount;
                }
                var part = new GameObject("part_" + ps.name).transform;
                part.SetParent(go.transform, false);
                var meshT = new GameObject("mesh").transform;
                meshT.SetParent(part, false);
                meshT.gameObject.AddComponent<MeshFilter>().sharedMesh = built.mesh;
                meshT.gameObject.AddComponent<MeshRenderer>().sharedMaterial = mat;
                rig.partNames[i] = ps.name; rig.parts[i] = part; rig.meshes[i] = meshT;
                rig.sizes[i] = built.size; rig.anchors[i] = ps.anchor;
            }
            rig.SetWeaponOnBack(true);
            var player = go.AddComponent<AlphaClipPlayer>();
            var clipFiles = Directory.Exists(DataDir + "/clips")
                ? Directory.GetFiles(DataDir + "/clips", "*.json").OrderBy(f => f).ToArray() : new string[0];
            AssetDatabase.Refresh();
            player.clipAssets = clipFiles.Select(f => AssetDatabase.LoadAssetAtPath<TextAsset>(f.Replace('\\', '/'))).ToArray();
            if (player.clipAssets.Length == 0) problems.Add("NO_CLIPS");
            problems.AddRange(rig.Validate());
            return go;
        }

        static (GameObject rig, Camera cam) BuildScene(List<string> problems, Dictionary<string, int> vertexCounts, out AlphaRigSpec spec)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            spec = LoadSpec(problems);
            if (spec == null) return (null, null);
            var go = BuildRig(spec, problems, vertexCounts);
            var camGo = new GameObject("Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.78f, 0.84f, 0.9f);
            cam.fieldOfView = 30f;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Quad);
            ground.name = "ground";
            ground.transform.rotation = Quaternion.Euler(90, 0, 0);
            ground.transform.position = new Vector3(0, -1.45f, 0);   // feet bottom at idle (~ -14.5 vox)
            ground.transform.localScale = Vector3.one * 8f;
            ground.GetComponent<Renderer>().sharedMaterial = new Material(Shader.Find("Unlit/Color")) { color = new Color(0.55f, 0.7f, 0.45f) };
            EditorSceneManager.SaveScene(scene, ScenePath);
            return (go, cam);
        }

        // ------------------------------------------------------------------ Run
        public static void Run()
        {
            var problems = new List<string>();
            var report = new StringBuilder();
            var json = new Dictionary<string, object>();
            int exit = 1;
            try
            {
                Directory.CreateDirectory(OutDir);
                var vertexCounts = new Dictionary<string, int>();
                var (go, cam) = BuildScene(problems, vertexCounts, out var spec);
                if (go == null) throw new Exception("rig not built");
                var rig = go.GetComponent<AlphaRig>();
                var player = go.GetComponent<AlphaClipPlayer>();

                // GEOMETRY
                foreach (var ps in spec.parts)
                {
                    vertexCounts.TryGetValue(ps.name, out int v);
                    bool ok = v == ps.expectedVertices;
                    report.AppendLine($"GEOMETRY {ps.name} model={ps.model} size={ps.size[0]}x{ps.size[1]}x{ps.size[2]} vertices={v} expected={ps.expectedVertices} {(ok ? "OK" : "MISMATCH")}");
                    if (!ok) problems.Add($"GEOMETRY_MISMATCH {ps.name} {v} != {ps.expectedVertices}");
                }

                // CLIPS
                player.LoadClips();
                problems.AddRange(player.Problems);
                foreach (var name in player.ClipNames.ToList())
                {
                    var clip = player.Get(name);
                    report.AppendLine($"CLIP {name} status={clip.Data.status} param={clip.Data.param} keys={clip.Data.keys.Length} duration={clip.Data.duration:0.###} loop={clip.Data.loop} weaponOnBack={clip.Data.weaponOnBack}");
                    if (clip.Data.status != AlphaClip.TransferredStatus)
                        report.AppendLine($"  NOTE status is {clip.Data.status}, not {AlphaClip.TransferredStatus}");
                    var rt = RoundTrip(rig, player, clip);
                    report.AppendLine($"ROUNDTRIP {name} keys={clip.Data.keys.Length} parts={clip.PartCount} max_abs={rt:0.000000}");
                    if (!(rt <= 1e-3f)) problems.Add($"ROUNDTRIP {name} max_abs={rt}");
                    if (clip.Data.loop && clip.Data.keys.Length > 1)
                    {
                        var seam = Seam(rig, player, clip);
                        report.AppendLine($"SEAM {name} {seam.text}");
                        if (!seam.ok) problems.Add($"SEAM {name} {seam.text}");
                    }
                    if (!clip.Data.weaponOnBack && clip.PartIndex("weapon") >= 0)
                    {
                        var grip = Grip(rig, player, clip, report);
                        if (grip != null) problems.Add(grip);
                    }
                }

                // SHOTS
                Shots(rig, player, cam, report);
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
                exit = problems.Count == 0 ? 0 : 1;
            }
            catch (Exception e)
            {
                problems.Add("EXCEPTION " + e);
            }
            var verdict = problems.Count == 0 ? "PASS" : "FAIL";
            report.Insert(0, $"TRANSFER_RESULT={verdict}\nproblems={problems.Count}\n" + string.Join("\n", problems.Select(p => "  - " + p)) + "\n");
            File.WriteAllText(Path.Combine(OutDir, "transfer_report.txt"), report.ToString());
            Debug.Log(report.ToString());
            Debug.Log($"TRANSFER_RESULT={verdict}");
            // With -quit, an explicit EditorApplication.Exit() crashed the editor on shutdown
            // (0xC0000005). Unity's documented way: return normally (exit 0) or throw (exit 1).
            if (exit != 0) throw new Exception($"TRANSFER_RESULT={verdict} problems={problems.Count}");
        }

        /// <summary>Expected Alpha model matrix of a part at a key: T(-anchor(size)) * J.</summary>
        static float[] Expected(AlphaRig rig, AlphaClip clip, int key, int clipPart, int rigPart)
        {
            var j = clip.KeyMatrix(key, clipPart);
            var a = rig.AnchorOf(rigPart);
            return AlphaSpace.Mul(AlphaSpace.Translate(-a), j);
        }

        static float RoundTrip(AlphaRig rig, AlphaClipPlayer player, AlphaClip clip)
        {
            player.lookPitchDeg = 0f;
            player.Play(clip.Data.name);
            float worst = 0f;
            for (int k = 0; k < clip.Data.keys.Length; k++)
            {
                player.ApplyAt(clip.Data.keys[k].x);
                for (int p = 0; p < clip.PartCount; p++)
                {
                    int r = rig.IndexOf(clip.Data.parts[p]);
                    var got = rig.ReadAlphaModelMatrix(r);
                    var exp = Expected(rig, clip, k, p, r);
                    worst = Mathf.Max(worst, AlphaSpace.MaxAbsDiff(got, exp));
                }
            }
            return worst;
        }

        static (bool ok, string text) Seam(AlphaRig rig, AlphaClipPlayer player, AlphaClip clip)
        {
            var d = clip.Data;
            var a = new PartPose[clip.PartCount]; var b = new PartPose[clip.PartCount];
            var steps = new List<float>();
            for (int k = 1; k < d.keys.Length; k++)
                for (int p = 0; p < clip.PartCount; p++)
                    steps.Add(Vector3.Distance(clip.KeyPose(k, p).position, clip.KeyPose(k - 1, p).position));
            steps.Sort();
            float medianStep = steps[steps.Count / 2], maxStep = steps[steps.Count - 1];
            clip.Sample(d.duration - 1e-4f, a);
            clip.Sample(0f, b);
            float jump = 0f, rot = 0f;
            for (int p = 0; p < clip.PartCount; p++)
            {
                jump = Mathf.Max(jump, Vector3.Distance(a[p].position, b[p].position));
                rot = Mathf.Max(rot, Quaternion.Angle(a[p].rotation, b[p].rotation));
            }
            // the wrap segment (last key -> first key + duration) must not move more than the
            // largest ordinary key-to-key step: a seam jump would exceed it
            float wrapSpan = d.keys[0].x + d.duration - d.keys[d.keys.Length - 1].x;
            var c = new PartPose[clip.PartCount];
            clip.Sample(d.keys[d.keys.Length - 1].x, c);
            float wrapMove = 0f;
            for (int p = 0; p < clip.PartCount; p++) wrapMove = Mathf.Max(wrapMove, Vector3.Distance(c[p].position, b[p].position));
            bool ok = jump <= Mathf.Max(1e-3f, maxStep) && wrapMove <= maxStep * 1.5f + 1e-3f;
            return (ok, $"eps_jump_vox={jump:0.0000} eps_rot_deg={rot:0.000} wrap_span={wrapSpan:0.0000} wrap_move_vox={wrapMove:0.000} median_key_step_vox={medianStep:0.000} max_key_step_vox={maxStep:0.000} {(ok ? "CONTINUOUS" : "JUMP")}");
        }

        /// <summary>Distances in voxels: main hand centre to the grip point, second hand to the handle axis.</summary>
        static string Grip(AlphaRig rig, AlphaClipPlayer player, AlphaClip clip, StringBuilder report)
        {
            int w = rig.IndexOf("weapon"), hr = rig.IndexOf("hand_r"), hl = rig.IndexOf("hand_l");
            if (w < 0 || hr < 0 || hl < 0) return "GRIP_PARTS_MISSING";
            player.Play(clip.Data.name);
            float worstMain = 0f, worstSecondRadial = 0f, minAlong = float.MaxValue, maxAlong = float.MinValue;
            var inv = 1f / RootScale;
            for (int k = 0; k < clip.Data.keys.Length; k++)
            {
                player.ApplyAt(clip.Data.keys[k].x);
                var wm = rig.meshes[w];
                var grip = wm.TransformPoint(AlphaSpace.GridToUnity(rig.AnchorOf(w)));
                var axis = wm.TransformDirection(Vector3.up).normalized;     // Alpha grid Z (blade length)
                Vector3 Centre(int i) => rig.meshes[i].TransformPoint(AlphaSpace.GridToUnity(new Vector3(rig.sizes[i].x, rig.sizes[i].y, rig.sizes[i].z) * 0.5f));
                float main = Vector3.Distance(Centre(hr), grip) * inv;
                var rel = Centre(hl) - grip;
                float along = Vector3.Dot(rel, axis) * inv;
                float radial = (rel - axis * Vector3.Dot(rel, axis)).magnitude * inv;
                worstMain = Mathf.Max(worstMain, main);
                worstSecondRadial = Mathf.Max(worstSecondRadial, radial);
                minAlong = Mathf.Min(minAlong, along); maxAlong = Mathf.Max(maxAlong, along);
            }
            // hand grid 5x6x4: the handle passes through the second hand when radial <= 2.5
            bool ok = worstMain <= 0.05f && worstSecondRadial <= 2.5f;
            report.AppendLine($"GRIP {clip.Data.name} main_hand_to_grip_max_vox={worstMain:0.0000} second_hand_radial_max_vox={worstSecondRadial:0.000} second_hand_along_handle_vox=[{minAlong:0.00},{maxAlong:0.00}] {(ok ? "OK" : "FAIL")}");
            return ok ? null : $"GRIP {clip.Data.name} main={worstMain} radial={worstSecondRadial}";
        }

        // ------------------------------------------------------------------ screenshots
        static void Shots(AlphaRig rig, AlphaClipPlayer player, Camera cam, StringBuilder report)
        {
            var dir = Path.Combine(OutDir, "shots");
            Directory.CreateDirectory(dir);
            var views = new (string name, Vector3 dir)[] { ("side", new Vector3(1f, 0.15f, 0.02f)), ("threequarter", new Vector3(0.8f, 0.35f, 0.9f)), ("front", new Vector3(0.02f, 0.12f, 1f)) };
            var frames = new List<(string clip, float x, string label)>();
            foreach (var name in player.ClipNames)
            {
                var c = player.Get(name);
                if (c.Data.keys.Length == 1) frames.Add((name, 0f, "pose"));
                else if (c.Data.param == "walk_phase")
                    for (int i = 0; i < 4; i++) frames.Add((name, c.Data.duration * i / 4f, $"phase{i * 90:000}"));
                else
                    foreach (var ms in new[] { 0f, 300f, 600f, 765f, 793f, 807f, 822f, 900f, 1300f, c.Data.duration })
                        if (ms <= c.Data.duration) frames.Add((name, ms, $"t{ms:0000}"));
            }
            var tiles = new Dictionary<string, List<Texture2D>>();
            foreach (var (clipName, x, label) in frames)
            {
                player.Play(clipName);
                player.ApplyAt(x);
                foreach (var v in views)
                {
                    var target = rig.transform.position + new Vector3(0f, 0.2f, 0f);
                    cam.transform.position = target + v.dir.normalized * 7.5f;
                    cam.transform.LookAt(target);
                    var tex = Render(cam, 360, 360);
                    File.WriteAllBytes(Path.Combine(dir, $"{clipName}_{label}_{v.name}.png"), tex.EncodeToPNG());
                    var sheetKey = $"{clipName}_{v.name}";
                    if (!tiles.ContainsKey(sheetKey)) tiles[sheetKey] = new List<Texture2D>();
                    tiles[sheetKey].Add(tex);
                }
            }
            foreach (var kv in tiles)
            {
                var sheet = Sheet(kv.Value);
                File.WriteAllBytes(Path.Combine(OutDir, $"sheet_{kv.Key}.png"), sheet.EncodeToPNG());
            }
            report.AppendLine($"SHOTS frames={frames.Count} views={views.Length} sheets={tiles.Count} dir={dir}");
        }

        [Serializable]
        class CompareRequest
        {
            public string clip = "attack_2h_a57";
            public float[] times = new float[0];
            public string[] labels = new string[0];
            public float lookPitchDeg;
            public float screenYawDeg = 45f;   // where the character's forward points on screen
            public float distance = 7.5f;
            public float elevationDeg = 90f;   // 90 = straight down
        }

        /// <summary>Render frames for an Alpha <-> Unity visual comparison (TransferOutput/compare_request.json).</summary>
        public static void RenderCompare()
        {
            var problems = new List<string>();
            var reqPath = Path.Combine(OutDir, "compare_request.json");
            var req = JsonUtility.FromJson<CompareRequest>(File.ReadAllText(reqPath));
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var rig = UnityEngine.Object.FindFirstObjectByType<AlphaRig>();
            var player = rig.GetComponent<AlphaClipPlayer>();
            var cam = UnityEngine.Object.FindFirstObjectByType<Camera>();
            player.LoadClips();
            player.lookPitchDeg = req.lookPitchDeg;
            if (!player.Play(req.clip)) throw new Exception("clip not found " + req.clip);
            var tiles = new List<Texture2D>();
            var dir = Path.Combine(OutDir, "compare");
            Directory.CreateDirectory(dir);
            for (int i = 0; i < req.times.Length; i++)
            {
                player.ApplyAt(req.times[i]);
                var target = rig.transform.position + new Vector3(0f, 0.2f, 0f);
                var el = req.elevationDeg * Mathf.Deg2Rad;
                var back = Quaternion.Euler(0f, req.screenYawDeg, 0f) * Vector3.back;
                cam.transform.position = target + (Vector3.up * Mathf.Sin(el) + back * Mathf.Cos(el)) * req.distance;
                var screenUp = Quaternion.Euler(0f, -req.screenYawDeg, 0f) * Vector3.forward;
                cam.transform.LookAt(target, Mathf.Abs(Mathf.Sin(el)) > 0.99f ? screenUp : Vector3.up);
                var tex = Render(cam, 392, 280);
                File.WriteAllBytes(Path.Combine(dir, $"unity_{i:00}_{(i < req.labels.Length ? req.labels[i] : req.times[i].ToString("0"))}.png"), tex.EncodeToPNG());
                tiles.Add(tex);
            }
            File.WriteAllBytes(Path.Combine(OutDir, "compare_unity_sheet.png"), Sheet(tiles).EncodeToPNG());
            Debug.Log($"COMPARE_RENDERED frames={tiles.Count}");
        }

        static Texture2D Render(Camera cam, int w, int h)
        {
            var rt = new RenderTexture(w, h, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            cam.targetTexture = null; RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(rt);
            return tex;
        }

        static Texture2D Sheet(List<Texture2D> tiles)
        {
            int cols = Mathf.Min(5, tiles.Count), rows = (tiles.Count + cols - 1) / cols;
            int w = tiles[0].width, h = tiles[0].height;
            var sheet = new Texture2D(cols * w, rows * h, TextureFormat.RGB24, false);
            var fill = Enumerable.Repeat(new Color32(40, 40, 40, 255), cols * w * rows * h).ToArray();
            sheet.SetPixels32(fill);
            for (int i = 0; i < tiles.Count; i++)
            {
                int cx = i % cols, cy = rows - 1 - i / cols;
                sheet.SetPixels(cx * w, cy * h, w, h, tiles[i].GetPixels());
            }
            sheet.Apply();
            return sheet;
        }
    }
}
