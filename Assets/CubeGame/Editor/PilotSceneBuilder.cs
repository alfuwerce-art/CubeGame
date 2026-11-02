using System.IO;
using CubeGame.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CubeGame.EditorTools
{
    /// <summary>
    /// Creates the pilot scene and its material asset from code, so the scene is reproducible and
    /// does not depend on hand-edited YAML or Unity-version-specific serialization.
    /// Run: Unity.exe -batchmode -quit -projectPath &lt;repo&gt; -executeMethod CubeGame.EditorTools.PilotSceneBuilder.Build
    /// </summary>
    public static class PilotSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Pilot.unity";
        public const string MaterialFolder = "Assets/CubeGame/Materials";
        public const string MaterialPath = MaterialFolder + "/PilotTerrain.mat";
        public const string ShaderName = "CubeGame/PilotVertexColor";

        [MenuItem("CubeGame/Build pilot scene")]
        public static void Build()
        {
            EnsureFolder("Assets/Scenes");
            EnsureFolder(MaterialFolder);

            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                Shader shader = Shader.Find(ShaderName);
                if (shader == null)
                {
                    Debug.LogError($"[PilotScene] shader not found: {ShaderName}");
                    return;
                }

                material = new Material(shader) { name = "PilotTerrain" };
                AssetDatabase.CreateAsset(material, MaterialPath);
                Debug.Log($"[PilotScene] created material {MaterialPath}");
            }

            PlayerSettings.productName = "CubeGame Pilot";
            PlayerSettings.companyName = "CubeGame";

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var lightObject = new GameObject("Sun");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.color = new Color(1f, 0.97f, 0.92f);
            lightObject.transform.rotation = Quaternion.Euler(50f, -35f, 0f);

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 600f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.45f, 0.62f, 0.82f);
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<CubeGame.Presentation.CameraRig.OrbitCameraController>();
            cameraObject.transform.position = new Vector3(128f, 40f, 112f);

            var bootstrapObject = new GameObject("PilotBootstrap");
            bootstrapObject.AddComponent<PilotBootstrap>();

            var serializedBootstrap = new SerializedObject(bootstrapObject.GetComponent<PilotBootstrap>());
            serializedBootstrap.FindProperty("seed").longValue = PilotBootstrap.DefaultSeed;
            serializedBootstrap.FindProperty("chunksPerAxis").intValue = CubeGame.Core.Generation.PilotWorldLayout.ViewChunks;
            serializedBootstrap.FindProperty("terrainMaterial").objectReferenceValue = material;
            serializedBootstrap.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                Debug.LogError($"[PilotScene] failed to save scene {ScenePath}");
                return;
            }

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[PilotScene] scene saved: {ScenePath}; material: {MaterialPath}");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string leaf = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
