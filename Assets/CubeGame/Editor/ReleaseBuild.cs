using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CubeGame.EditorTools
{
    /// <summary>
    /// Builds the Windows x86_64 release player from the active scene list exactly as it is
    /// committed: no gameplay, scene or setting change is made here.
    /// Run: Unity.exe -batchmode -quit -projectPath &lt;repo&gt;
    ///        -executeMethod CubeGame.EditorTools.ReleaseBuild.BuildWindows64
    /// The output folder comes from the CUBEGAME_RELEASE_DIR environment variable and falls
    /// back to .unity-install/build/Release. The player is always produced as CubeGame.exe so
    /// that the stable launcher contract (current.txt -> releases/&lt;build-id&gt;/CubeGame.exe)
    /// does not depend on the product name.
    /// </summary>
    public static class ReleaseBuild
    {
        public const string OutputDirectoryVariable = "CUBEGAME_RELEASE_DIR";
        public const string ExecutableName = "CubeGame.exe";
        public const string DefaultOutputDirectory = ".unity-install/build/Release";

        [MenuItem("CubeGame/Build Windows release")]
        public static void BuildWindows64()
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);

            string[] scenes = EnabledScenes();
            if (scenes.Length == 0)
            {
                BatchMode.Fail("no enabled scenes in EditorBuildSettings");
                return;
            }

            for (int index = 0; index < scenes.Length; index++)
            {
                if (!File.Exists(Path.Combine(projectRoot, scenes[index])))
                {
                    BatchMode.Fail($"enabled scene missing on disk: {scenes[index]}");
                    return;
                }
            }

            string outputDirectory = Environment.GetEnvironmentVariable(OutputDirectoryVariable);
            if (string.IsNullOrEmpty(outputDirectory))
            {
                outputDirectory = Path.Combine(projectRoot, DefaultOutputDirectory);
            }

            outputDirectory = Path.GetFullPath(outputDirectory);
            Directory.CreateDirectory(outputDirectory);

            string executable = Path.Combine(outputDirectory, ExecutableName);
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = executable,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            Debug.Log($"[ReleaseBuild] scenes={string.Join(",", scenes)} output={executable} " +
                      $"unity={Application.unityVersion}");

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            Debug.Log($"[ReleaseBuild] result={summary.result} size={summary.totalSize}B " +
                      $"errors={summary.totalErrors} warnings={summary.totalWarnings} " +
                      $"time={summary.totalTime}");

            if (summary.result != BuildResult.Succeeded)
            {
                BatchMode.Fail($"build result={summary.result} errors={summary.totalErrors} " +
                               $"warnings={summary.totalWarnings}");
                return;
            }

            string dataDirectory = Path.Combine(outputDirectory, Path.GetFileNameWithoutExtension(ExecutableName) + "_Data");
            long executableSize = File.Exists(executable) ? new FileInfo(executable).Length : 0;
            bool playerComplete = Directory.Exists(dataDirectory) &&
                                  File.Exists(Path.Combine(outputDirectory, "UnityPlayer.dll"));

            Debug.Log($"[ReleaseBuild] BUILD_SUCCEEDED output={executable} size={executableSize}B " +
                      $"data={dataDirectory} complete={playerComplete}");

            if (!playerComplete)
            {
                BatchMode.Fail("player is incomplete: CubeGame_Data or UnityPlayer.dll is missing");
            }
        }

        private static string[] EnabledScenes()
        {
            var scenes = new System.Collections.Generic.List<string>();
            EditorBuildSettingsScene[] configured = EditorBuildSettings.scenes;
            for (int index = 0; index < configured.Length; index++)
            {
                EditorBuildSettingsScene scene = configured[index];
                if (scene != null && scene.enabled && !string.IsNullOrEmpty(scene.path))
                {
                    scenes.Add(scene.path);
                }
            }

            return scenes.ToArray();
        }
    }
}
