// AlphaTransferPlayMode.cs — enters PlayMode on the built scene with AlphaPlayModeProbe and exits
// only after Unity is back in EditMode (a synchronous Exit inside playModeStateChanged crashed the
// S05A-1 pilot with 0xC0000005). Exit code: 0 = probe PASS, 1 = FAIL, 3 = timeout.
//   Unity.exe -batchmode -projectPath <P> -executeMethod CubeGame.AlphaTransfer.Editor.AlphaTransferPlayMode.Run
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CubeGame.AlphaTransfer.Editor
{
    public static class AlphaTransferPlayMode
    {
        const string ScenePath = "Assets/AlphaTransfer/AlphaTransferScene.unity";
        static double _deadline;
        static bool _exitScheduled;
        static int _exitCode = 3;

        public static void Run()
        {
            if (!File.Exists(ScenePath)) { Debug.LogError("PLAYMODE_RESULT=FAIL scene missing — run AlphaTransferEditor.Run first"); EditorApplication.Exit(1); return; }
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var rig = Object.FindFirstObjectByType<AlphaRig>();
            if (rig == null) { Debug.LogError("PLAYMODE_RESULT=FAIL no AlphaRig in scene"); EditorApplication.Exit(1); return; }
            if (rig.GetComponent<AlphaPlayModeProbe>() == null) rig.gameObject.AddComponent<AlphaPlayModeProbe>();
            EditorSceneManager.SaveScene(scene);
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
                _exitCode = AlphaPlayModeProbe.Verdict == "PASS" ? 0 : 1;
                _exitScheduled = true;   // the actual Exit happens on the next update tick
            }
        }

        static void Tick()
        {
            if (_exitScheduled && !EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.update -= Tick;
                EditorApplication.playModeStateChanged -= OnState;
                Debug.Log($"PLAYMODE_EXIT code={_exitCode} verdict={AlphaPlayModeProbe.Verdict}");
                EditorApplication.Exit(_exitCode);
                return;
            }
            if (EditorApplication.timeSinceStartup > _deadline)
            {
                Debug.LogError("PLAYMODE_RESULT=FAIL timeout");
                EditorApplication.update -= Tick;
                EditorApplication.Exit(3);
            }
        }
    }
}
