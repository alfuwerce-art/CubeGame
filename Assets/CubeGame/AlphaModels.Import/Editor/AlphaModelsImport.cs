// CubeGame.AlphaModels.Import — editor-side preparation and verification (D86 M0-01).
//
// Three batch/Menu entry points:
//   * CreateVisualResources        — writes the voxel materials into Resources (needed by a player build),
//   * ValidateContent              — checks the local Alpha content cache and reports every problem,
//   * Preview                      — renders key frames of every cached actor to AlphaOutput/preview
//                                    (local evidence only; nothing Alpha-derived is imported into Git).
//
// Batch:
//   Unity.exe -batchmode -quit -projectPath <P> \
//     -executeMethod CubeGame.AlphaModels.Import.AlphaModelsImport.RunCreateVisualResources
//     ... RunValidateContent | RunPreview
// Results: <P>/AlphaOutput/content_status.txt, <P>/AlphaOutput/preview.txt, PREVIEW frames.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CubeGame.AlphaModels;
using CubeGame.AlphaTransfer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CubeGame.AlphaModels.Import
{
    public static class AlphaModelsImport
    {
        public const string ResourcesDir = "Assets/CubeGame/AlphaModels/Resources";

        static string OutDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "AlphaOutput"));

        // ---------------------------------------------------------------- materials
        [MenuItem("CubeGame/Alpha Models/Create Visual Resources")]
        public static void CreateVisualResources()
        {
            var shader = Shader.Find(AlphaVisualResources.ShaderName);
            if (shader == null) throw new InvalidOperationException("SHADER_MISSING " + AlphaVisualResources.ShaderName);
            Directory.CreateDirectory(ResourcesDir);
            WriteMaterial(Path.Combine(ResourcesDir, AlphaVisualResources.BaseResourceName + ".mat"), shader, 2000);
            WriteMaterial(Path.Combine(ResourcesDir, AlphaVisualResources.HairResourceName + ".mat"), shader, AlphaRig.HairRenderQueue);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        static void WriteMaterial(string path, Shader shader, int queue)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;
            mat.renderQueue = queue;
            mat.SetColor("_Tint", Color.white);
            mat.SetVector("_LightDir", new Vector4(0.35f, 0.8f, 0.5f, 0f));
            EditorUtility.SetDirty(mat);
        }

        public static void RunCreateVisualResources()
        {
            int exit = 0;
            try { CreateVisualResources(); }
            catch (Exception e)
            {
                exit = 1;
                Write("resources.txt", "ALPHA_RESOURCES_RESULT=FAIL " + e);
            }
            if (exit == 0) Write("resources.txt", "ALPHA_RESOURCES_RESULT=PASS dir=" + ResourcesDir);
            EditorApplication.Exit(exit);
        }

        // ---------------------------------------------------------------- validation
        [MenuItem("CubeGame/Alpha Models/Validate Local Content Cache")]
        public static void ValidateContent()
        {
            var report = AlphaContentDiagnostics.Inspect();
            var text = new StringBuilder();
            text.AppendLine("ALPHA_CONTENT_RESULT=" + (report.Ready ? "PASS" : report.State == AlphaContentState.NotConfigured ? "NOT_CONFIGURED" : "FAIL"));
            text.AppendLine(report.Detail());
            text.AppendLine("ALPHA_TABLES_RESULT=" + (CubeGame.Content.AlphaContentDatabase.Validate().Count == 0 ? "PASS" : "FAIL"));
            text.AppendLine(CubeGame.Content.AlphaContentDatabase.Summary());
            foreach (var p in CubeGame.Content.AlphaContentDatabase.Validate()) text.AppendLine("TABLE_PROBLEM " + p);
            foreach (var p in CubeGame.Content.AlphaCreatureCatalog.Validate()) text.AppendLine("CATALOG_PROBLEM " + p);
            foreach (var p in CubeGame.Content.AlphaItemRules.Validate()) text.AppendLine("ITEM_PROBLEM " + p);
            Write("content_status.txt", text.ToString());
            if (!report.Ready) Debug.LogWarning(text.ToString());
        }

        public static void RunValidateContent()
        {
            int exit = 1;
            try
            {
                ValidateContent();
                var report = AlphaContentDiagnostics.Inspect();
                exit = report.Ready ? 0 : (report.State == AlphaContentState.NotConfigured ? 2 : 1);
            }
            catch (Exception e)
            {
                Write("content_status.txt", "ALPHA_CONTENT_RESULT=FAIL exception=" + e);
            }
            EditorApplication.Exit(exit);
        }

        // ---------------------------------------------------------------- preview
        [MenuItem("CubeGame/Alpha Models/Render Key Frames")]
        public static void Preview()
        {
            var problems = new List<string>();
            var report = AlphaContentDiagnostics.Inspect();
            int images = 0;
            var summary = new StringBuilder();
            summary.AppendLine("ALPHA_PREVIEW root=" + (report.Root ?? "<none>") + " actors=" + report.Actors.Count);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var lib = new AlphaActorLibrary(AlphaContentSource.Resolve());
            var cam = CreateCamera();
            float x = 0f;
            foreach (var id in lib.ActorIds)
            {
                if (!lib.TryGet(id, out var def))
                {
                    problems.Add(id + ": ACTOR_NOT_READY");
                    continue;
                }
                var view = AlphaActorView.Spawn(def, null, problems);
                if (view == null || !view.Ready) { problems.Add(id + ": VIEW_NOT_READY"); continue; }
                view.transform.position = new Vector3(x, 0f, 0f);
                x += 6f;
                images += RenderPose(cam, view, id, "idle", 0.2f, AlphaLocomotionState.Standing, problems);
                images += RenderPose(cam, view, id, "walk", 0.35f,
                    new AlphaLocomotionState { SpeedXY = 3.7f, Amplitude = 0.19f, OnGround = true }, problems);
                var action = FirstAction(def);
                if (action != null)
                {
                    view.TryPlayAction(AlphaActorView.ActionCodeOf(action), out _);
                    images += RenderPose(cam, view, id, "action_" + action, 0.30f, null, problems);
                    view.Controller.Step(6f);            // finish the action so the next actor starts clean
                }
                summary.AppendLine($"  {id}: parts={def.Rig.parts.Length} clips={def.Clips.Count} actions={def.RequiredActions.Length} mode={def.ControllerMode}");
            }
            summary.Append("ALPHA_PREVIEW_RESULT=").Append(problems.Count == 0 ? "PASS" : "FAIL")
                   .Append(" actors=").Append(report.Actors.Count).Append(" images=").Append(images)
                   .Append(" problems=").Append(problems.Count);
            foreach (var p in problems) summary.Append('\n').Append("PROBLEM ").Append(p);
            Write("preview.txt", summary.ToString());
            if (problems.Count > 0) Debug.LogWarning(summary.ToString());
        }

        public static void RunPreview()
        {
            int exit = 1;
            try { Preview(); exit = 0; }
            catch (Exception e) { Write("preview.txt", "ALPHA_PREVIEW_RESULT=FAIL exception=" + e); }
            EditorApplication.Exit(exit);
        }

        static string FirstAction(AlphaActorDefinition def)
        {
            for (int i = 0; i < def.RequiredActions.Length; i++)
                if (def.RequiredActions[i] != null && def.RequiredActions[i].StartsWith("action_")) return def.RequiredActions[i];
            return null;
        }

        static Camera CreateCamera()
        {
            var go = new GameObject("AlphaPreviewCamera");
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.13f, 0.16f, 1f);
            cam.fieldOfView = 35f;
            return cam;
        }

        static int RenderPose(Camera cam, AlphaActorView view, string actor, string pose, float seconds,
            AlphaLocomotionState? locomotion, List<string> problems)
        {
            if (locomotion.HasValue) view.ApplyLocomotion(locomotion.Value);
            int steps = Mathf.Max(1, Mathf.RoundToInt(seconds * 30f));
            for (int i = 0; i < steps; i++) view.Tick(1f / 30f);
            Frame(cam, view);
            Directory.CreateDirectory(Path.Combine(OutDir, "preview"));
            var file = Path.Combine(OutDir, "preview", actor + "_" + pose + ".png");
            const int size = 640;
            var rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            tex.Apply();
            File.WriteAllBytes(file, tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(tex);
            if (new FileInfo(file).Length < 1024) problems.Add(actor + "/" + pose + ": IMAGE_TOO_SMALL");
            return 1;
        }

        static void Frame(Camera cam, AlphaActorView view)
        {
            var bounds = new Bounds(view.transform.position, Vector3.one);
            bool first = true;
            foreach (var filter in view.GetComponentsInChildren<MeshFilter>())
            {
                var renderer = filter.GetComponent<Renderer>();
                if (filter.sharedMesh == null || renderer == null) continue;
                if (first) { bounds = renderer.bounds; first = false; }
                else bounds.Encapsulate(renderer.bounds);
            }
            var center = bounds.center;
            float radius = Mathf.Max(0.5f, bounds.extents.magnitude);
            cam.transform.position = center + new Vector3(-radius * 2.4f, radius * 1.4f, -radius * 2.4f);
            cam.transform.LookAt(center);
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = radius * 20f;
        }

        static void Write(string name, string text)
        {
            Directory.CreateDirectory(OutDir);
            var path = Path.Combine(OutDir, name);
            File.WriteAllText(path, text);
            Debug.Log("[AlphaModelsImport] " + text);
        }
    }
}
