// AlphaControllerPlayMode.cs — PlayMode run of the parametric walk: the transfer scene's rig is driven
// by AlphaLocomotionDriver (formula, no clip) and checked by AlphaControllerProbe in real frames.
// Same safe lifecycle as AlphaTransferPlayMode (exit only after EnteredEditMode).
// Exit code: 0 = PASS, 1 = FAIL, 3 = timeout. Saved as a separate scene; the clip scene is untouched.
//   Unity.exe -batchmode -projectPath <P> -executeMethod CubeGame.AlphaTransfer.Editor.AlphaControllerPlayMode.Run
using System.IO;
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CubeGame.AlphaTransfer.Editor
{
    public static class AlphaControllerPlayMode
    {
        const string ClipScene = "Assets/AlphaTransfer/AlphaTransferScene.unity";
        const string LocoScene = "Assets/AlphaTransfer/AlphaControllerScene.unity";
        const string SpecPath = "Assets/AlphaTransfer/Data/locomotion.json";
        static double _deadline;
        static bool _exitScheduled;
        static int _exitCode = 3;
        static string _session;
        static bool _timedOut;
        static bool _native;
        public static void RunNative(){_native=true;Run();}

        [Serializable] class Registry { public string[] requiredClips; }

        public static void Run()
        {
            _exitScheduled = false; _exitCode = 3; _timedOut = false;
            _session = Guid.NewGuid().ToString("N");
            AlphaControllerProbe.Finished = false; AlphaControllerProbe.Verdict = "NOT_RUN";
            if (!File.Exists(ClipScene)) { Debug.LogError("CONTROLLER_PLAYMODE_RESULT=FAIL scene missing — run AlphaTransferEditor.Run first"); EditorApplication.Exit(1); return; }
            if (!File.Exists(SpecPath)) { Debug.LogError("CONTROLLER_PLAYMODE_RESULT=FAIL locomotion.json missing"); EditorApplication.Exit(1); return; }
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.OpenScene(ClipScene);
            var rig = Object.FindFirstObjectByType<AlphaRig>();
            if (rig == null) { Debug.LogError("CONTROLLER_PLAYMODE_RESULT=FAIL no AlphaRig"); EditorApplication.Exit(1); return; }
            // the formula drives the rig: no clip player, no clip probe
            foreach (var c in rig.GetComponents<AlphaPlayModeProbe>()) Object.DestroyImmediate(c);
            foreach (var c in rig.GetComponents<AlphaLocomotionProbe>()) Object.DestroyImmediate(c);
            // the controller drives both sources; the clip player stays, its autoplay is off
            foreach (var c in rig.GetComponents<AlphaClipPlayer>()) { c.autoPlay = false; c.playing = ""; }
            var driver = rig.GetComponent<AlphaLocomotionDriver>();
            if (driver == null) driver = rig.gameObject.AddComponent<AlphaLocomotionDriver>();
            driver.specAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(SpecPath);
            var controller = rig.GetComponent<AlphaAnimationController>();
            if (controller == null) controller = rig.gameObject.AddComponent<AlphaAnimationController>();
            var player = rig.GetComponent<AlphaClipPlayer>();
            string action = null;
            foreach (var a in player.clipAssets) if (a != null && a.name.StartsWith("action_")) { action = a.name; break; }
            if (action == null) { Debug.LogError("CONTROLLER_PLAYMODE_RESULT=FAIL no action_* clip"); EditorApplication.Exit(1); return; }
            const string registryPath = "Assets/AlphaTransfer/Data/actions.json";
            if (!File.Exists(registryPath)) { Debug.LogError("CONTROLLER_PLAYMODE_RESULT=FAIL registry missing"); EditorApplication.Exit(1); return; }
            controller.requiredClips = JsonUtility.FromJson<Registry>(File.ReadAllText(registryPath)).requiredClips;
            if (controller.requiredClips == null || controller.requiredClips.Length == 0
                || controller.requiredClips.Any(n => !player.clipAssets.Any(a => a != null && a.name == n)))
            { Debug.LogError("CONTROLLER_PLAYMODE_RESULT=FAIL required action clip missing"); EditorApplication.Exit(1); return; }
            var args = Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, "-controllerFps");
            int fps=at>=0&&at+1<args.Length?int.Parse(args[at+1]):60;
            if (fps != 30 && fps != 60 && fps != 120) throw new ArgumentException("controllerFps must be 30,60,120");
            bool capture=Array.IndexOf(args,"-controllerCapture")>=0;
            if(_native){
                foreach(var p in rig.GetComponents<AlphaControllerProbe>())Object.DestroyImmediate(p);
                var native=rig.gameObject.AddComponent<AlphaNativeDriver>();native.manualClock=true;
                native.specAsset=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/AlphaTransfer/Data/lifecycle.json");
                controller.externalClock=true;
                var probe=rig.gameObject.AddComponent<AlphaNativeProbe>();probe.fps=fps;probe.session=_session;probe.capture=capture;
                AlphaNativeProbe.Finished=false;AlphaNativeProbe.Verdict="NOT_RUN";
            }else{
                var probe=rig.gameObject.AddComponent<AlphaControllerProbe>();probe.actionClip=action;probe.session=_session;
                probe.fps=fps;probe.captureFirstAction=capture;
            }
            EditorSceneManager.SaveScene(scene, _native?"Assets/AlphaTransfer/AlphaNativeScene.unity":LocoScene);
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorApplication.playModeStateChanged += OnState;
            EditorApplication.update += Tick;
            _deadline = EditorApplication.timeSinceStartup + 1200.0;
            EditorApplication.isPlaying = true;
        }

        static void OnState(PlayModeStateChange s)
        {
            if (s == PlayModeStateChange.EnteredEditMode)
            {
                string report = Path.Combine(Application.dataPath,"..","TransferOutput",_native?"native_lifecycle_playmode.txt":"controller_playmode.txt");
                bool fresh = File.Exists(report) && File.ReadAllText(report).Contains("session="+_session);
                bool pass=_native?AlphaNativeProbe.Finished&&AlphaNativeProbe.Verdict=="PASS":AlphaControllerProbe.Finished&&AlphaControllerProbe.Verdict=="PASS";
                _exitCode = _timedOut ? 3 : pass&&fresh ? 0 : 1;
                _exitScheduled = true;
            }
        }

        static void Tick()
        {
            if (_exitScheduled && !EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.update -= Tick;
                EditorApplication.playModeStateChanged -= OnState;
                Debug.Log($"CONTROLLER_PLAYMODE_EXIT code={_exitCode} verdict={AlphaControllerProbe.Verdict}");
                EditorApplication.Exit(_exitCode);
                return;
            }
            if (EditorApplication.timeSinceStartup > _deadline)
            {
                Debug.LogError("CONTROLLER_PLAYMODE_RESULT=FAIL timeout");
                _timedOut = true;
                _deadline = double.PositiveInfinity;
                if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                    EditorApplication.isPlaying = false;
                else { _exitCode=3; _exitScheduled=true; }
            }
        }
    }
}
