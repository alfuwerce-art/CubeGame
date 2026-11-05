// AlphaControllerPlayMode.cs — PlayMode run of the parametric walk: the transfer scene's rig is driven
// by AlphaLocomotionDriver (formula, no clip) and checked by AlphaControllerProbe in real frames.
// Same safe lifecycle as AlphaTransferPlayMode (exit only after EnteredEditMode).
// Exit code: 0 = PASS, 1 = FAIL, 3 = timeout. Saved as a separate scene; the clip scene is untouched.
//   Unity.exe -batchmode -projectPath <P> -executeMethod CubeGame.AlphaTransfer.Editor.AlphaControllerPlayMode.Run
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

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

        public static void Run()
        {
            if (!File.Exists(ClipScene)) { Debug.LogError("CONTROLLER_PLAYMODE_RESULT=FAIL scene missing — run AlphaTransferEditor.Run first"); EditorApplication.Exit(1); return; }
            if (!File.Exists(SpecPath)) { Debug.LogError("CONTROLLER_PLAYMODE_RESULT=FAIL locomotion.json missing"); EditorApplication.Exit(1); return; }
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.OpenScene(ClipScene);
            var rig = Object.FindFirstObjectByType<AlphaRig>();
            if (rig == null) { Debug.LogError("CONTROLLER_PLAYMODE_RESULT=FAIL no AlphaRig"); EditorApplication.Exit(1); return; }
            // the formula drives the rig: no clip player, no clip probe
            foreach (var c in rig.GetComponents<AlphaPlayModeProbe>()) Object.DestroyImmediate(c);
            // the controller drives both sources; the clip player stays, its autoplay is off
            foreach (var c in rig.GetComponents<AlphaClipPlayer>()) { c.autoPlay = false; c.playing = ""; }
            var driver = rig.GetComponent<AlphaLocomotionDriver>();
            if (driver == null) driver = rig.gameObject.AddComponent<AlphaLocomotionDriver>();
            driver.specAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(SpecPath);
            if (rig.GetComponent<AlphaAnimationController>() == null) rig.gameObject.AddComponent<AlphaAnimationController>();
            var probe = rig.GetComponent<AlphaControllerProbe>();
            if (probe == null) probe = rig.gameObject.AddComponent<AlphaControllerProbe>();
            var player = rig.GetComponent<AlphaClipPlayer>();
            string action = null;
            foreach (var a in player.clipAssets) if (a != null && a.name.StartsWith("action_")) { action = a.name; break; }
            if (action == null) { Debug.LogError("CONTROLLER_PLAYMODE_RESULT=FAIL no action_* clip"); EditorApplication.Exit(1); return; }
            probe.actionClip = action;
            EditorSceneManager.SaveScene(scene, LocoScene);
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorApplication.playModeStateChanged += OnState;
            EditorApplication.update += Tick;
            _deadline = EditorApplication.timeSinceStartup + 180.0;
            EditorApplication.isPlaying = true;
        }

        static void OnState(PlayModeStateChange s)
        {
            if (s == PlayModeStateChange.EnteredEditMode)
            {
                _exitCode = AlphaControllerProbe.Verdict == "PASS" ? 0 : 1;
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
                EditorApplication.update -= Tick;
                EditorApplication.Exit(3);
            }
        }
    }
}
