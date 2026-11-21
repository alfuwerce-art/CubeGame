using CubeGame.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CubeGame.Gameplay.EditorTools
{
    /// <summary>
    /// Rebuilds the single game scene (Assets/Scenes/Pilot.unity) with the M0-01 <see cref="GameBootstrap"/>
    /// as its entry point. The S01 PilotBootstrap class stays in the code base but is no longer placed in the scene.
    /// Run: Unity.exe -batchmode -quit -projectPath &lt;repo&gt; -executeMethod CubeGame.Gameplay.EditorTools.GameSceneBuilder.Build
    /// </summary>
    public static class GameSceneBuilder
    {
        [MenuItem("CubeGame/Build game scene (M0-01)")]
        public static void Build()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(PilotSceneBuilder.MaterialPath);
            if (material == null)
            {
                Debug.LogError($"[GameScene] BATCH_FAIL: terrain material missing at {PilotSceneBuilder.MaterialPath}");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }

                return;
            }

            // ProjectSettings are shared between agents (D86 §3): this builder does not change PlayerSettings.
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var lightObject = new GameObject("Sun");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.color = new Color(1f, 0.97f, 0.92f);
            lightObject.transform.rotation = Quaternion.Euler(50f, -35f, 0f);

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 600f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.45f, 0.62f, 0.82f);
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<CubeGame.Presentation.CameraRig.OrbitCameraController>();
            cameraObject.transform.position = new Vector3(128f, 40f, 112f);

            var bootstrapObject = new GameObject("GameBootstrap");
            var bootstrap = bootstrapObject.AddComponent<GameBootstrap>();
            var serialized = new SerializedObject(bootstrap);
            serialized.FindProperty("terrainMaterial").objectReferenceValue = material;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, PilotSceneBuilder.ScenePath))
            {
                Debug.LogError($"[GameScene] BATCH_FAIL: cannot save {PilotSceneBuilder.ScenePath}");
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                }

                return;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[GameScene] SCENE_SAVED {PilotSceneBuilder.ScenePath} with GameBootstrap");
        }
    }
}
