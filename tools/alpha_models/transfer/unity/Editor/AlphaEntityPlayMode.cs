// AlphaEntityPlayMode.cs — entity-contract checks for ANY creature of the universal pipeline.
//   Check: EditMode. Builds nothing; loads the transfer scene, Data/contract.json and the clip library
//          and validates them against each other -> TransferOutput/entity_contract.txt
//          (ENTITY_CONTRACT_RESULT=PASS|FAIL). Run after AlphaTransferEditor.Run.
//   Run:   PlayMode. The one AlphaAnimationController (mode taken from the contract) drives the rig
//          with the parametric walk; AlphaEntityProbe checks walk -> stop -> idle -> action/refusal.
//          -> TransferOutput/entity_playmode.txt (ENTITY_PLAYMODE_RESULT). Exit 0 PASS, 1 FAIL, 3 timeout.
// Same safe lifecycle as the other PlayMode entry points (exit only after EnteredEditMode, fresh session).
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CubeGame.AlphaTransfer.Editor
{
    public static class AlphaEntityPlayMode
    {
        const string ClipScene = "Assets/AlphaTransfer/AlphaTransferScene.unity";
        const string EntityScene = "Assets/AlphaTransfer/AlphaEntityScene.unity";
        const string SpecPath = "Assets/AlphaTransfer/Data/locomotion.json";
        const string ContractPath = "Assets/AlphaTransfer/Data/contract.json";
        const string RegistryPath = "Assets/AlphaTransfer/Data/actions.json";
        static double _deadline;
        static bool _exitScheduled, _timedOut;
        static int _exitCode = 3;
        static string _session;

        [Serializable] class Registry { public string[] requiredClips; }

        static string OutDir => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "TransferOutput"));

        // ------------------------------------------------------------------ EditMode contract check
        public static void Check()
        {
            var problems = new List<string>();
            var s = new StringBuilder();
            int exit = 1;
            try
            {
                if (!File.Exists(ClipScene)) throw new Exception("scene missing — run AlphaTransferEditor.Run first");
                if (!File.Exists(ContractPath)) throw new Exception("contract.json missing");
                AssetDatabase.Refresh();
                EditorSceneManager.OpenScene(ClipScene);
                var rig = Object.FindFirstObjectByType<AlphaRig>();
                var player = rig != null ? rig.GetComponent<AlphaClipPlayer>() : null;
                if (rig == null || player == null) throw new Exception("rig/clip player missing");
                var contract = AlphaEntityContract.Parse(File.ReadAllText(ContractPath), problems);
                if (contract != null)
                {
                    player.LoadClips(); problems.AddRange(player.Problems);
                    var clips = player.ClipNames.Select(player.Get).ToList();
                    problems.AddRange(contract.Validate(rig, clips, File.Exists(RegistryPath)));
                    s.AppendLine($"kind={contract.kind} category={contract.category} body_plan={contract.bodyPlan} rig={contract.rig}");
                    s.AppendLine($"parts={string.Join(",", contract.parts.Select(p => p.name + ":" + p.role + (string.IsNullOrEmpty(p.mirrorOf) ? "" : "<->" + p.mirrorOf)))}");
                    s.AppendLine($"controller_mode={contract.controllerMode} actions={contract.actionsStatus} profile={contract.actionProfile} trigger={contract.actionTrigger}");
                    if (contract.LocomotionOnly) s.AppendLine("actions_blocked_reason=" + contract.actionsReason);
                    s.AppendLine($"weapon_layer={contract.weaponLayer} walk_sources={string.Join("+", contract.walkSources ?? new string[0])} idle_sources={string.Join("+", contract.idleSources ?? new string[0])}");
                    var byCat = clips.GroupBy(c => AlphaEntityContract.CategoryOf(c.Data) + "/" + c.Data.status).OrderBy(g => g.Key);
                    s.AppendLine("clips=" + string.Join(" ", byCat.Select(g => $"{g.Key}:{g.Count()}")));
                }
                exit = problems.Count == 0 ? 0 : 1;
            }
            catch (Exception e) { problems.Add("EXCEPTION " + e.Message); }
            var verdict = problems.Count == 0 ? "PASS" : "FAIL";
            s.Insert(0, $"ENTITY_CONTRACT_RESULT={verdict}\nproblems={problems.Count}\n" + string.Join("", problems.Select(p => "  - " + p + "\n")));
            Directory.CreateDirectory(OutDir);
            File.WriteAllText(Path.Combine(OutDir, "entity_contract.txt"), s.ToString());
            Debug.Log(s.ToString());
            if (exit != 0) throw new Exception($"ENTITY_CONTRACT_RESULT={verdict}");
        }

        // ------------------------------------------------------------------ PlayMode
        public static void Run()
        {
            _exitScheduled = false; _exitCode = 3; _timedOut = false;
            _session = Guid.NewGuid().ToString("N");
            AlphaEntityProbe.Finished = false; AlphaEntityProbe.Verdict = "NOT_RUN";
            if (!File.Exists(ClipScene) || !File.Exists(SpecPath) || !File.Exists(ContractPath))
            { Debug.LogError("ENTITY_PLAYMODE_RESULT=FAIL scene, locomotion.json or contract.json missing"); EditorApplication.Exit(1); return; }
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.OpenScene(ClipScene);
            var rig = Object.FindFirstObjectByType<AlphaRig>();
            if (rig == null) { Debug.LogError("ENTITY_PLAYMODE_RESULT=FAIL no AlphaRig"); EditorApplication.Exit(1); return; }
            foreach (var c in rig.GetComponents<AlphaPlayModeProbe>()) Object.DestroyImmediate(c);
            foreach (var c in rig.GetComponents<AlphaLocomotionProbe>()) Object.DestroyImmediate(c);
            foreach (var c in rig.GetComponents<AlphaControllerProbe>()) Object.DestroyImmediate(c);
            foreach (var c in rig.GetComponents<AlphaNativeProbe>()) Object.DestroyImmediate(c);
            foreach (var c in rig.GetComponents<AlphaNativeDriver>()) Object.DestroyImmediate(c);
            foreach (var c in rig.GetComponents<AlphaClipPlayer>()) { c.autoPlay = false; c.playing = ""; }
            var driver = rig.GetComponent<AlphaLocomotionDriver>();
            if (driver == null) driver = rig.gameObject.AddComponent<AlphaLocomotionDriver>();
            driver.specAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(SpecPath);
            var controller = rig.GetComponent<AlphaAnimationController>();
            if (controller == null) controller = rig.gameObject.AddComponent<AlphaAnimationController>();
            controller.requiredClips = File.Exists(RegistryPath)
                ? JsonUtility.FromJson<Registry>(File.ReadAllText(RegistryPath)).requiredClips ?? new string[0] : new string[0];
            var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-controllerFps");
            int fps = at >= 0 && at + 1 < args.Length ? int.Parse(args[at + 1]) : 60;
            if (fps != 30 && fps != 60 && fps != 120) throw new ArgumentException("controllerFps must be 30,60,120");
            var probe = rig.gameObject.AddComponent<AlphaEntityProbe>();
            probe.fps = fps; probe.session = _session; probe.capture = true;
            EditorSceneManager.SaveScene(scene, EntityScene);
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorApplication.playModeStateChanged += OnState;
            EditorApplication.update += Tick;
            _deadline = EditorApplication.timeSinceStartup + 600.0;
            EditorApplication.isPlaying = true;
        }

        static void OnState(PlayModeStateChange s)
        {
            if (s != PlayModeStateChange.EnteredEditMode) return;
            string report = Path.Combine(OutDir, "entity_playmode.txt");
            bool fresh = File.Exists(report) && File.ReadAllText(report).Contains("session=" + _session);
            _exitCode = _timedOut ? 3 : AlphaEntityProbe.Finished && AlphaEntityProbe.Verdict == "PASS" && fresh ? 0 : 1;
            _exitScheduled = true;
        }

        static void Tick()
        {
            if (_exitScheduled && !EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.update -= Tick;
                EditorApplication.playModeStateChanged -= OnState;
                Debug.Log($"ENTITY_PLAYMODE_EXIT code={_exitCode} verdict={AlphaEntityProbe.Verdict}");
                EditorApplication.Exit(_exitCode);
                return;
            }
            if (EditorApplication.timeSinceStartup > _deadline)
            {
                Debug.LogError("ENTITY_PLAYMODE_RESULT=FAIL timeout");
                _timedOut = true; _deadline = double.PositiveInfinity;
                if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying = false;
                else { _exitCode = 3; _exitScheduled = true; }
            }
        }
    }
}
