// ReviewSceneBuilder.cs — builds and checks the S05B visual review scene.
//
//   Build        (batch)  scene Assets/Review/S05B_VisualReview.unity + material  -> ReviewOutput/build.txt
//   Check        (batch)  EditMode: every specimen built with the runtime code, every source sampled
//                         (all keys / 64 walk phases), provenance rules, contact sheets PNG -> review_check.txt
//   RunPlayMode  (batch)  PlayMode smoke tour of the real viewer (all specimens, modes, sources, lineup)
//                         -> review_playmode.txt
//   OpenScene    (GUI)    opens the scene in the interactive editor (open_review.ps1); "-reviewPlay" enters PlayMode
//
// Markers: REVIEW_BUILD_RESULT, REVIEW_CHECK_RESULT, REVIEW_PLAYMODE_RESULT (=PASS|FAIL), exit 0 only on PASS.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CubeGame.AlphaTransfer.Review;

namespace CubeGame.AlphaTransfer.Review.Editor
{
    public static class ReviewSceneBuilder
    {
        public const string ScenePath = "Assets/Review/S05B_VisualReview.unity";
        const string MatPath = "Assets/Review/Generated/ReviewVoxel.mat";
        static string OutDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ReviewOutput"));
        static readonly string[] AllowedStatus = { "TRANSFERRED_FROM_ALPHA", "EMULATED_ALPHA_CODE", "RECONSTRUCTED_FROM_EVIDENCE" };
        static readonly string[] ActionStatus = { "TRANSFERRED_FROM_ALPHA", "EMULATED_ALPHA_CODE" };

        // ------------------------------------------------------------------ build
        [MenuItem("CubeGame/Review/Rebuild review scene")]
        public static void BuildMenu() { BuildScene(new List<string>()); EditorSceneManager.OpenScene(ScenePath); }

        static Material EnsureMaterial(List<string> problems)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(MatPath));
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
            var shader = Shader.Find("AlphaTransfer/VoxelShaded");
            if (shader == null) { problems.Add("SHADER_MISSING AlphaTransfer/VoxelShaded"); shader = Shader.Find("Unlit/Color"); }
            if (mat == null) { mat = new Material(shader) { name = "ReviewVoxel" }; AssetDatabase.CreateAsset(mat, MatPath); }
            else mat.shader = shader;
            return mat;
        }

        static void BuildScene(List<string> problems)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var mat = EnsureMaterial(problems);
            var camGo = new GameObject("ReviewCamera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.62f, 0.7f, 0.78f);
            cam.fieldOfView = 30f; cam.nearClipPlane = 0.02f; cam.farClipPlane = 500f;
            var orbit = camGo.AddComponent<ReviewOrbitCamera>();
            orbit.Apply();
            var viewer = new GameObject("ReviewViewer").AddComponent<ReviewViewer>();
            viewer.voxelMaterial = mat;
            viewer.orbit = orbit;
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
        }

        public static void Build()
        {
            var problems = new List<string>();
            int exit = 1;
            try
            {
                Directory.CreateDirectory(OutDir);
                BuildScene(problems);
                var idx = ReviewData.LoadIndex(problems);
                if (idx == null) problems.Add("INDEX_NOT_LOADED");
                exit = problems.Count == 0 ? 0 : 1;
            }
            catch (Exception e) { problems.Add("EXCEPTION " + e); }
            var sb = new StringBuilder($"REVIEW_BUILD_RESULT={(exit == 0 ? "PASS" : "FAIL")} scene={ScenePath}\n");
            foreach (var p in problems) sb.AppendLine("PROBLEM " + p);
            File.WriteAllText(Path.Combine(OutDir, "build.txt"), sb.ToString());
            EditorApplication.Exit(exit);
        }

        // ------------------------------------------------------------------ interactive open
        public static void OpenScene()
        {
            if (!File.Exists(ScenePath)) BuildScene(new List<string>());
            EditorSceneManager.OpenScene(ScenePath);
            if (Environment.GetCommandLineArgs().Contains("-reviewPlay")) EditorApplication.isPlaying = true;
        }

        // ------------------------------------------------------------------ check
        static bool Finite(Vector3 v) => !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) || float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));

        static string CheckPose(ReviewSpecimen s)
        {
            for (int i = 0; i < s.rig.parts.Length; i++)
            {
                var t = s.rig.parts[i];
                if (!s.rig.IsVisible(i)) continue;
                if (!Finite(t.localPosition) || !Finite(t.localScale) || float.IsNaN(t.localRotation.w)) return $"NON_FINITE {s.rig.partNames[i]}";
                if (Mathf.Abs(t.localScale.x * t.localScale.y * t.localScale.z) < 1e-6f) return $"ZERO_SCALE {s.rig.partNames[i]}";
            }
            return null;
        }

        static IEnumerable<float> Cursors(ReviewSpecimen s, ReviewSource src)
        {
            if (src.formula) { for (int i = 0; i < 64; i++) yield return i * Mathf.PI * 2f / 64f; yield break; }
            var c = s.player.Get(src.clip);
            foreach (var k in c.Data.keys) yield return k.x;
            if (c.Data.duration > 0) yield return c.Data.duration * 0.5f + 0.37f;   // between keys (interpolation)
        }

        public static void Check()
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            var problems = new List<string>();
            var lines = new StringBuilder();
            int exit = 1, sources = 0, samples = 0, specimens = 0;
            var tint = new List<string>(); var mirror = new List<string>();
            try
            {
                Directory.CreateDirectory(OutDir);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var mat = EnsureMaterial(problems);
                var idx = ReviewData.LoadIndex(problems);
                if (idx == null) throw new Exception("index not loaded");
                if (idx.kinds.Length != (idx.expectedKinds > 0 ? idx.expectedKinds : 15))
                    problems.Add($"KINDS {idx.kinds.Length} != {(idx.expectedKinds > 0 ? idx.expectedKinds : 15)}");
                var root = new GameObject("CheckRoot").transform;
                foreach (var info in idx.specimens.Where(x => x.prepare == "PASS"))
                {
                    var s = ReviewSpecimen.Build(info, mat, root);
                    specimens++;
                    foreach (var p in s.problems) problems.Add($"{info.id} {p}");
                    var counts = new Dictionary<ReviewMode, int>();
                    foreach (ReviewMode mode in Enum.GetValues(typeof(ReviewMode)))
                    {
                        var list = s.Sources(mode);
                        counts[mode] = list.Count;
                        foreach (var src in list)
                        {
                            sources++;
                            if (!AllowedStatus.Contains(src.status)) problems.Add($"{info.id} STATUS_NOT_ALLOWED {src.label} {src.status}");
                            if (mode == ReviewMode.Action && !ActionStatus.Contains(src.status)) problems.Add($"{info.id} ACTION_STATUS {src.label} {src.status}");
                            foreach (var x in Cursors(s, src))
                            {
                                s.Apply(src, x); samples++;
                                var bad = CheckPose(s);
                                if (bad != null) { problems.Add($"{info.id} {mode} {src.label} x={x}: {bad}"); break; }
                            }
                        }
                    }
                    if (counts[ReviewMode.Idle] == 0) problems.Add($"{info.id} NO_IDLE_SOURCE");
                    if (counts[ReviewMode.Walk] == 0) problems.Add($"{info.id} NO_WALK_SOURCE");
                    var actionNames = new HashSet<string>(s.Sources(ReviewMode.Action).Select(a => a.clip));
                    if (s.actions != null)
                        foreach (var req in s.actions.requiredClips)
                            if (!actionNames.Contains(req)) problems.Add($"{info.id} REQUIRED_ACTION_NOT_PLAYABLE {req}");
                    if (info.controllerMode == "ACTIONS" && s.actions != null && actionNames.Count == 0) problems.Add($"{info.id} ACTIONS_MODE_WITHOUT_CLIPS");
                    // informational: idle mirror symmetry and raw tint markers
                    var idle = s.Sources(ReviewMode.Idle).LastOrDefault();
                    s.Apply(idle, 0f);
                    float worst = 0f; string worstPair = "";
                    if (s.contract?.parts != null)
                        foreach (var p in s.contract.parts.Where(p => !string.IsNullOrEmpty(p.mirrorOf)))
                        {
                            int a = s.rig.IndexOf(p.name), b = s.rig.IndexOf(p.mirrorOf);
                            if (a < 0 || b < 0) { problems.Add($"{info.id} MIRROR_PART_MISSING {p.name}/{p.mirrorOf}"); continue; }
                            var pa = s.rig.parts[a].localPosition; var pb = s.rig.parts[b].localPosition;
                            float d = Mathf.Max(Mathf.Abs(pa.x + pb.x), Mathf.Abs(pa.y - pb.y), Mathf.Abs(pa.z - pb.z));
                            if (d > worst) { worst = d; worstPair = $"{p.name}/{p.mirrorOf}"; }
                        }
                    mirror.Add($"MIRROR_IDLE {info.id} max_delta_vox={worst:0.000} {worstPair} ({idle?.status})");
                    for (int i = 0; i < s.markers.Length; i++)
                        if (s.markers[i][0] + s.markers[i][1] + s.markers[i][2] > 0)
                            tint.Add($"TINT_NOT_APPLIED {info.id} {s.rig.partNames[i]} {s.partSpecs[i].model} R={s.markers[i][0]} G={s.markers[i][1]} B={s.markers[i][2]}");
                    lines.AppendLine($"SPECIMEN {info.id} {info.name} idle={counts[ReviewMode.Idle]} walk={counts[ReviewMode.Walk]} actions={counts[ReviewMode.Action]} " +
                                     $"statuses={string.Join(",", s.Sources(ReviewMode.Idle).Concat(s.Sources(ReviewMode.Walk)).Concat(s.Sources(ReviewMode.Action)).Select(x => x.status).Distinct())}");
                    UnityEngine.Object.DestroyImmediate(s.gameObject);
                }
                problems.AddRange(LayoutProblems());
                Sheets(idx, mat, problems);
                exit = problems.Count == 0 ? 0 : 1;
            }
            catch (Exception e) { problems.Add("EXCEPTION " + e); }
            var sb = new StringBuilder();
            sb.AppendLine($"REVIEW_CHECK_RESULT={(exit == 0 ? "PASS" : "FAIL")} specimens={specimens} sources={sources} samples={samples} problems={problems.Count}");
            foreach (var p in problems) sb.AppendLine("PROBLEM " + p);
            sb.Append(lines);
            foreach (var t in tint) sb.AppendLine(t);
            foreach (var m in mirror) sb.AppendLine(m);
            File.WriteAllText(Path.Combine(OutDir, "review_check.txt"), sb.ToString());
            EditorApplication.Exit(exit);
        }

        // ------------------------------------------------------------------ layout (small Game view)
        public static readonly Vector2Int[] LayoutSizes =
        {
            new Vector2Int(480, 270), new Vector2Int(640, 360), new Vector2Int(800, 450), new Vector2Int(960, 540),
            new Vector2Int(1024, 600), new Vector2Int(1280, 720), new Vector2Int(1366, 768), new Vector2Int(1600, 900),
            new Vector2Int(1920, 1080), new Vector2Int(2560, 1440), new Vector2Int(3840, 2160), new Vector2Int(600, 900)
        };

        public static List<string> LayoutProblems()
        {
            var problems = new List<string>();
            foreach (var sz in LayoutSizes)
                foreach (var open in new[] { true, false })
                {
                    var l = ReviewLayout.Compute(sz.x, sz.y, ReviewLayout.AutoScale(sz.x, sz.y), open);
                    foreach (var p in l.Problems()) problems.Add($"LAYOUT {sz.x}x{sz.y} panel={(open ? "open" : "closed")}: {p}");
                    if (open && l.panel.width < 220f) problems.Add($"LAYOUT {sz.x}x{sz.y}: panel too narrow {l.panel.width}");
                    if (l.timeline.width < 220f) problems.Add($"LAYOUT {sz.x}x{sz.y}: timeline too narrow {l.timeline.width}");
                }
            return problems;
        }

        // ------------------------------------------------------------------ colour before / after (D61)
        // D65: the full catalog has more kinds than one texture allows (16384 px), so the sheet is split
        // into chunks of at most 50 kinds; each chunk is a separate PNG, the content is unchanged.
        static void BeforeAfter(ReviewIndex idx, Material mat)
        {
            var cam = SheetCamera();
            var root = new GameObject("BeforeAfterRoot").transform;
            int n = idx.kinds.Length, cell = 300, chunk = Mathf.Max(1, Mathf.Min(50, 16000 / cell));
            for (int start = 0; start < n; start += chunk)
            {
                int count = Mathf.Min(chunk, n - start);
                var sheet = new Texture2D(count * cell, 2 * cell, TextureFormat.RGB24, false);
                for (int k = 0; k < count; k++)
                {
                    var info = BaseSpecimen(idx, idx.kinds[start + k]);
                    for (int row = 0; row < 2; row++)
                    {
                        bool after = row == 1;
                        var s = ReviewSpecimen.Build(info, mat, root, alphaMarkerRule: after);
                        if (after) s.SetHairColour(ReviewData.DefaultHairColour(idx, s));
                        s.Apply(s.Sources(ReviewMode.Idle).LastOrDefault(), 0f);
                        var b = s.VisibleBounds();
                        // head close-up: upper 55 % of the creature, 3/4 front
                        var head = new Bounds(new Vector3(b.center.x, b.max.y - b.size.y * 0.3f, b.center.z),
                                              new Vector3(b.size.x, b.size.y * 0.6f, b.size.z));
                        Aim(cam, head, 200f, 8f, 1.0f);
                        var tex = Render(cam, cell, cell);
                        sheet.SetPixels(k * cell, (1 - row) * cell, cell, cell, tex.GetPixels());
                        UnityEngine.Object.DestroyImmediate(tex);
                        UnityEngine.Object.DestroyImmediate(s.gameObject);
                    }
                }
                sheet.Apply();
                Save(sheet, n <= chunk ? "colour_before_after.png"
                                       : $"colour_before_after_{start / chunk + 1}.png");
            }
            UnityEngine.Object.DestroyImmediate(cam.gameObject);
            UnityEngine.Object.DestroyImmediate(root.gameObject);
        }

        // ------------------------------------------------------------------ contact sheets (local only)
        const int Cell = 240;

        static Camera SheetCamera()
        {
            var cam = new GameObject("SheetCamera").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.62f, 0.7f, 0.78f);
            cam.fieldOfView = 30f; cam.nearClipPlane = 0.02f; cam.farClipPlane = 500f;
            cam.enabled = false;
            return cam;
        }

        static void Aim(Camera cam, Bounds b, float yaw, float pitch, float margin = 1.15f)
        {
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            float radius = Mathf.Max(b.extents.magnitude, 0.05f) * margin;
            float dist = radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            cam.transform.SetPositionAndRotation(b.center - rot * Vector3.forward * dist, rot);
        }

        static Texture2D Render(Camera cam, int w, int h)
        {
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt; cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
            RenderTexture.active = prev; cam.targetTexture = null; RenderTexture.ReleaseTemporary(rt);
            return tex;
        }

        static void Save(Texture2D t, string name)
        {
            File.WriteAllBytes(Path.Combine(OutDir, name), t.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(t);
        }

        // A review scope may hold a kind only as the player action library (issue #4: HumanMale 0x02 via the
        // regression job); then the sheet uses that specimen instead of failing on a missing base variant.
        static ReviewSpecimenInfo BaseSpecimen(ReviewIndex idx, string kind) =>
            idx.specimens.FirstOrDefault(x => x.kind == kind && x.IsBaseVariant && x.prepare == "PASS")
            ?? idx.specimens.First(x => x.kind == kind && x.prepare == "PASS");

        static ReviewIndex _sheetIndex;

        static void Sheets(ReviewIndex idx, Material mat, List<string> problems)
        {
            _sheetIndex = idx;
            BeforeAfter(idx, mat);
            var cam = SheetCamera();
            var root = new GameObject("SheetRoot").transform;
            // 1. lineup of every kind (seed 0-0), idle and walk, front and side.
            // D65: a single row is unreadable for 141 kinds (each becomes a few pixels), so the lineup is a grid
            // of at most 20 per row; the texture grows with the number of rows (still far below 16384 px).
            int cols = Mathf.Max(1, Mathf.Min(idx.kinds.Length, 20));
            int rows = Mathf.Max(1, Mathf.CeilToInt(idx.kinds.Length / (float)cols));
            int sheetH = Mathf.Clamp(700 * rows / Mathf.Max(1, Mathf.Min(rows, 4)), 700, 4800);
            foreach (var mode in new[] { ReviewMode.Idle, ReviewMode.Walk })
            {
                var specs = new List<ReviewSpecimen>(); var all = new Bounds(); bool first = true;
                for (int k = 0; k < idx.kinds.Length; k++)
                {
                    var info = BaseSpecimen(idx, idx.kinds[k]);
                    var s = ReviewSpecimen.Build(info, mat, root);
                    s.SetHairColour(ReviewData.DefaultHairColour(idx, s));
                    // D69: a STATIC_OBJECT stands still in the walk lineup too (the game never walks it)
                    var m = s.contract != null && s.contract.controllerMode == "STATIC_OBJECT" ? ReviewMode.Idle : mode;
                    var src = s.Sources(m).LastOrDefault(v => v.formula) ?? s.Sources(m).FirstOrDefault();
                    s.Apply(src, m == ReviewMode.Idle ? 0f : 1.0f);
                    var b = s.VisibleBounds();
                    int col = k % cols, row = k / cols;
                    s.transform.position += new Vector3(col * 2.2f - b.center.x, -row * 2.8f, 0);
                    b = s.VisibleBounds();
                    if (first) { all = b; first = false; } else all.Encapsulate(b);
                    specs.Add(s);
                }
                Aim(cam, all, 180f, 6f, 0.62f); Save(Render(cam, 2400, sheetH), $"lineup_{mode.ToString().ToLower()}_front.png");
                Aim(cam, all, 150f, 14f, 0.62f); Save(Render(cam, 2400, Mathf.Max(900, sheetH)), $"lineup_{mode.ToString().ToLower()}_34.png");
                foreach (var s in specs) UnityEngine.Object.DestroyImmediate(s.gameObject);
            }
            // 2. per kind: every variant (idle, 3/4 front), then every action of the action specimens
            foreach (var k in idx.kinds)
            {
                var vars = idx.specimens.Where(s => s.kind == k && s.prepare == "PASS").ToList();
                // review #4: base specimen from four sides (front, right, back, left), idle and walk rows
                var bse = vars.FirstOrDefault(v => v.IsBaseVariant) ?? vars.FirstOrDefault();
                if (bse != null) FourSides(cam, root, mat, bse, $"sides_{bse.id}.png");
                Grid(cam, root, mat, vars.Select(v => (v, (Func<ReviewSpecimen, (ReviewSource, float)>)(s =>
                    (s.Sources(ReviewMode.Idle).LastOrDefault(), 0f)))).ToList(), $"variants_{k}.png", 200f, 10f);
                foreach (var v in vars)
                {
                    var probe = ReviewSpecimen.Build(v, mat, root);
                    var acts = probe.Sources(ReviewMode.Action);
                    UnityEngine.Object.DestroyImmediate(probe.gameObject);
                    if (acts.Count == 0) continue;
                    // 3 frames per action (30 %, 60 %, 100 % of the action window), one row per action
                    var cells = new List<(ReviewSpecimenInfo, Func<ReviewSpecimen, (ReviewSource, float)>)>();
                    foreach (var a in acts)
                        foreach (var f in new[] { 0.3f, 0.6f, 1.0f })
                        {
                            var act = a; var frac = f;
                            cells.Add((v, s =>
                            {
                                var d = s.player.Get(act.clip).Data;
                                float end = d.actionEndMs > 0 ? d.actionEndMs : d.duration;
                                return (act, end * frac);
                            }));
                        }
                    Grid(cam, root, mat, cells, $"actions_{v.id}.png", 160f, 12f, acts.Count > 20 ? 12 : 3);
                }
            }
            UnityEngine.Object.DestroyImmediate(cam.gameObject);
            UnityEngine.Object.DestroyImmediate(root.gameObject);
        }

        // One sheet: columns = yaw 180 (front), 90 (right), 0 (back), 270 (left); rows = idle, walk (two phases).
        // The frame is fixed per specimen (idle bounds), so a part that drifts between views is visible.
        static void FourSides(Camera cam, Transform root, Material mat, ReviewSpecimenInfo info, string name)
        {
            var s = ReviewSpecimen.Build(info, mat, root);
            s.SetHairColour(ReviewData.DefaultHairColour(_sheetIndex, s));
            var idle = s.Sources(ReviewMode.Idle).LastOrDefault();
            var walk = s.Sources(ReviewMode.Walk).LastOrDefault(v => v.formula) ?? s.Sources(ReviewMode.Walk).FirstOrDefault();
            s.Apply(idle, 0f);
            var frame = s.VisibleBounds();
            frame.Expand(frame.size.magnitude * 0.5f);
            var rowsSrc = new List<(ReviewSource src, float x)> { (idle, 0f) };
            // A STATIC_OBJECT never walks in the game (D69); the PNG has no labels, so its research walk data is
            // not drawn here (the interactive viewer still shows it, labelled "disabled in game").
            bool isStatic = s.contract != null && s.contract.controllerMode == "STATIC_OBJECT";
            if (walk != null && !isStatic) { rowsSrc.Add((walk, 1.0f)); rowsSrc.Add((walk, 2.6f)); }
            float[] yaws = { 180f, 90f, 0f, 270f };
            int rows = rowsSrc.Count;
            var sheet = new Texture2D(yaws.Length * Cell, rows * Cell, TextureFormat.RGB24, false);
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < yaws.Length; c++)
                {
                    if (rowsSrc[r].src == null) continue;
                    s.Apply(rowsSrc[r].src, rowsSrc[r].x);
                    Aim(cam, frame, yaws[c], 8f, 0.8f);
                    var tex = Render(cam, Cell, Cell);
                    sheet.SetPixels(c * Cell, (rows - 1 - r) * Cell, Cell, Cell, tex.GetPixels());
                    UnityEngine.Object.DestroyImmediate(tex);
                }
            UnityEngine.Object.DestroyImmediate(s.gameObject);
            sheet.Apply();
            Save(sheet, name);
        }

        static void Grid(Camera cam, Transform root, Material mat, List<(ReviewSpecimenInfo info, Func<ReviewSpecimen, (ReviewSource, float)> pose)> cells,
                         string name, float yaw, float pitch, int cols = 0)
        {
            if (cells.Count == 0) return;
            if (cols <= 0) cols = Mathf.Min(cells.Count, 8);
            int rows = (cells.Count + cols - 1) / cols;
            var sheet = new Texture2D(cols * Cell, rows * Cell, TextureFormat.RGB24, false);
            var bg = Enumerable.Repeat(cam.backgroundColor, sheet.width * sheet.height).ToArray();
            sheet.SetPixels(bg);                         // empty cells: scene background, not grey
            ReviewSpecimen s = null; string built = null; Bounds frame = default;
            for (int i = 0; i < cells.Count; i++)
            {
                if (built != cells[i].info.id)
                {
                    if (s != null) UnityEngine.Object.DestroyImmediate(s.gameObject);
                    s = ReviewSpecimen.Build(cells[i].info, mat, root); built = cells[i].info.id;
                    s.SetHairColour(ReviewData.DefaultHairColour(_sheetIndex, s));
                    s.Apply(s.Sources(ReviewMode.Idle).LastOrDefault(), 0f);
                    frame = s.VisibleBounds();
                    frame.Expand(frame.size.magnitude * 0.5f);
                }
                var (src, x) = cells[i].pose(s);
                s.Apply(src, x);
                Aim(cam, frame, yaw, pitch, 0.8f);
                var tex = Render(cam, Cell, Cell);
                int cx = i % cols, cy = rows - 1 - i / cols;
                sheet.SetPixels(cx * Cell, cy * Cell, Cell, Cell, tex.GetPixels());
                UnityEngine.Object.DestroyImmediate(tex);
            }
            if (s != null) UnityEngine.Object.DestroyImmediate(s.gameObject);
            sheet.Apply();
            Save(sheet, name);
        }

        // ------------------------------------------------------------------ PlayMode smoke
        static double _deadline;
        static bool _exitScheduled;
        static int _exitCode = 3;
        static readonly List<string> _errors = new List<string>();

        public static void RunPlayMode()
        {
            Directory.CreateDirectory(OutDir);
            if (!File.Exists(ScenePath)) { File.WriteAllText(Path.Combine(OutDir, "review_playmode.txt"), "REVIEW_PLAYMODE_RESULT=FAIL scene missing\n"); EditorApplication.Exit(1); return; }
            EditorSceneManager.OpenScene(ScenePath);
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            ReviewViewer.SmokeTour = true; ReviewViewer.SmokeDone = false;
            Application.logMessageReceived += OnLog;
            EditorApplication.playModeStateChanged += OnState;
            EditorApplication.update += Tick;
            _deadline = EditorApplication.timeSinceStartup + 900.0;
            EditorApplication.isPlaying = true;
        }

        static string FirstLine(string s) { s = s ?? ""; int i = s.IndexOf((char)10); return i < 0 ? s : s.Substring(0, i); }

        // every error/exception of the PlayMode session counts (D61: the Unity Search startup exception is
        // removed at its cause - setup_review.ps1 disables startup indexing - not filtered here)
        static void OnLog(string msg, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                _errors.Add($"{type}: {msg} | {FirstLine(stack)}");
        }

        static void OnState(PlayModeStateChange s)
        {
            if (s != PlayModeStateChange.EnteredEditMode) return;
            var log = ReviewViewer.SmokeLog;
            bool pass = ReviewViewer.SmokeDone && log.Count == 0 && _errors.Count == 0 && ReviewViewer.SmokeSpecimens > 0;
            var sb = new StringBuilder($"REVIEW_PLAYMODE_RESULT={(pass ? "PASS" : "FAIL")} specimens={ReviewViewer.SmokeSpecimens} " +
                                       $"sources={ReviewViewer.SmokeSources} frames={ReviewViewer.SmokeFrames} errors={_errors.Count} tour_problems={log.Count} " +
                                       $"marker_toggle_vertices={ReviewViewer.SmokeMarkerToggle}\n");
            foreach (var l in log) sb.AppendLine("PROBLEM " + l);
            foreach (var e in _errors.Take(50)) sb.AppendLine("ERROR " + e);
            File.WriteAllText(Path.Combine(OutDir, "review_playmode.txt"), sb.ToString());
            _exitCode = pass ? 0 : 1;
            _exitScheduled = true;
        }

        static void Tick()
        {
            if (EditorApplication.isPlaying && ReviewViewer.SmokeDone && !_exitScheduled) EditorApplication.isPlaying = false;
            if (_exitScheduled && !EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.update -= Tick;
                EditorApplication.playModeStateChanged -= OnState;
                Application.logMessageReceived -= OnLog;
                ReviewViewer.SmokeTour = false;
                EditorApplication.Exit(_exitCode);
                return;
            }
            if (EditorApplication.timeSinceStartup > _deadline)
            {
                File.WriteAllText(Path.Combine(OutDir, "review_playmode.txt"), "REVIEW_PLAYMODE_RESULT=FAIL timeout\n");
                EditorApplication.update -= Tick;
                EditorApplication.Exit(3);
            }
        }
    }
}
