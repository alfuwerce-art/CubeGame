using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CubeGame.EditorTools
{
    /// <summary>
    /// Builds the pilot standalone player used for the automated demo run and screenshots.
    /// Run: Unity.exe -batchmode -quit -projectPath &lt;repo&gt; -executeMethod CubeGame.EditorTools.PilotBuild.BuildStandalone
    /// </summary>
    public static class PilotBuild
    {
        public const string OutputDirectory = ".unity-install/build/Pilot";
        public const string ExecutableName = "CubeGamePilot.exe";

        [MenuItem("CubeGame/Build pilot player")]
        public static void BuildStandalone()
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string scenePath = Path.Combine(projectRoot, PilotSceneBuilder.ScenePath);
            if (!File.Exists(scenePath))
            {
                Debug.LogError($"[PilotBuild] scene missing: {scenePath}");
                BatchMode.Fail("pilot scene is missing; run PilotSceneBuilder.Build first");
                return;
            }

            string outputPath = Path.Combine(projectRoot, OutputDirectory);
            Directory.CreateDirectory(outputPath);

            string executable = Path.Combine(outputPath, ExecutableName);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { PilotSceneBuilder.ScenePath },
                locationPathName = executable,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            Debug.Log($"[PilotBuild] result={summary.result} size={summary.totalSize}B " +
                      $"errors={summary.totalErrors} warnings={summary.totalWarnings} " +
                      $"time={summary.totalTime} output={executable}");

            if (summary.result != BuildResult.Succeeded)
            {
                Debug.LogError("[PilotBuild] build failed");
                BatchMode.Fail($"build result={summary.result} errors={summary.totalErrors}");
                return;
            }

            Debug.Log($"[PilotBuild] BUILD_SUCCEEDED output={executable} " +
                      $"size={(File.Exists(executable) ? new FileInfo(executable).Length : 0)}B");
        }
    }
}
