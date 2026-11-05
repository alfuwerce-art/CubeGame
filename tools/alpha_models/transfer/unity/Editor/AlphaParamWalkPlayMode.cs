// AlphaParamWalkPlayMode.cs — PlayMode run of the parametric walk: the transfer scene's rig is driven
// by AlphaLocomotionDriver (formula, no clip) and checked by AlphaLocomotionProbe in real frames.
// Same safe lifecycle as AlphaTransferPlayMode (exit only after EnteredEditMode).
// Exit code: 0 = PASS, 1 = FAIL, 3 = timeout. Saved as a separate scene; the clip scene is untouched.
//   Unity.exe -batchmode -projectPath <P> -executeMethod CubeGame.AlphaTransfer.Editor.AlphaParamWalkPlayMode.Run
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CubeGame.AlphaTransfer.Editor
{
    public static class AlphaParamWalkPlayMode
    {
        const string ClipScene = "Assets/AlphaTransfer/AlphaTransferScene.unity";
        const string LocoScene = "Assets/AlphaTransfer/AlphaLocomotionScene.unity";
        const string SpecPath = "Assets/AlphaTransfer/Data/locomotion.json";
        static double _deadline;
        static bool _exitScheduled;
        static int _exitCode = 3;

        public static void Run()
        {
            if (!File.Exists(ClipScene)) { Debug.LogError("LOCOMOTION_PLAYMODE_RESULT=FAIL scene missing — run AlphaTransferEditor.Run first"); EditorApplication.Exit(1); return; }
            if (!File.Exists(SpecPath)) { Debug.LogError("LOCOMOTION_PLAYMODE_RESULT=FAIL locomotion.json missing"); EditorApplication.Exit(1); return; }
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.OpenScene(ClipScene);
            var rig = Object.FindFirstObjectByType<AlphaRig>();
            if (rig == null) { Debug.LogError("LOCOMOTION_PLAYMODE_RESULT=FAIL no AlphaRig"); EditorApplication.Exit(1); return; }
            // the formula drives the rig: no clip player, no clip probe
            foreach (var c in rig.GetComponents<AlphaPlayModeProbe>()) Object.DestroyImmediate(c);
            foreach (var c in rig.GetComponents<AlphaClipPlayer>()) c.enabled = false;
            var driver = rig.GetComponent<AlphaLocomotionDriver>();
            if (driver == null) driver = rig.gameObject.AddComponent<AlphaLocomotionDriver>();
            driver.specAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(SpecPath);
            if (rig.GetComponent<AlphaLocomotionProbe>() == null) rig.gameObject.AddComponent<AlphaLocomotionProbe>();
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
                _exitCode = AlphaLocomotionProbe.Verdict == "PASS" ? 0 : 1;
                _exitScheduled = true;
            }
        }

        static void Tick()
        {
            if (_exitScheduled && !EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.update -= Tick;
                EditorApplication.playModeStateChanged -= OnState;
                Debug.Log($"LOCOMOTION_PLAYMODE_EXIT code={_exitCode} verdict={AlphaLocomotionProbe.Verdict}");
                EditorApplication.Exit(_exitCode);
                return;
            }
            if (EditorApplication.timeSinceStartup > _deadline)
            {
                Debug.LogError("LOCOMOTION_PLAYMODE_RESULT=FAIL timeout");
                EditorApplication.update -= Tick;
                EditorApplication.Exit(3);
            }
        }
    }
}
