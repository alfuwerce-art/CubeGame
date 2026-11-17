using System;
using System.Collections.Generic;
using System.IO;
using CubeGame.AlphaModels;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace CubeGame.EditorTools
{
    /// <summary>
    /// M0 runtime recovery R0/R2/R3 — delivers the local Alpha content into a Windows Player build.
    ///
    /// The transferred Alpha content (actors, item models, appearance parts, clips, sounds, icon atlas) is a
    /// local cache that must never be committed. A Player build therefore received none of it and showed
    /// capsules and cubes. This class copies the runtime categories of the local cache
    ///     &lt;project&gt;/.scratch/content  (+ AlphaOutput/icons)
    /// straight into the built Player
    ///     &lt;Player&gt;_Data/StreamingAssets/CubeGame/AlphaContent
    /// which is exactly <c>Application.streamingAssetsPath</c>/CubeGame/AlphaContent at runtime. Nothing is
    /// written under Assets/, so nothing can be committed by accident and Unity does not import 2 500 files.
    /// The Player folder is self-contained afterwards: it can be moved to another disk or machine.
    ///
    /// Build gate (R2): a Windows Player build fails before it starts when the local cache lacks a required
    /// category, and fails after it when the staged copy is incomplete. Development tooling can opt out
    /// explicitly with CUBEGAME_BUILD_WITHOUT_ALPHA_CONTENT=1; such a build is marked and is not a release.
    /// </summary>
    public sealed class AlphaContentStaging : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public const string BypassVariable = "CUBEGAME_BUILD_WITHOUT_ALPHA_CONTENT";
        public const string SourceVariable = AlphaContentSource.EnvVar;

        /// <summary>Folders of the cache that the runtime reads. Research folders (projects, jobs, scripts, logs) stay local.</summary>
        static readonly string[] RuntimeFolders =
        {
            AlphaContentSource.ActorsFolder,
            AlphaContentInventory.ItemsFolder,
            AlphaContentInventory.AppearanceFolder,
            AlphaContentInventory.SoundsFolder,
            AlphaContentSource.EnvironmentFolder,
            AlphaContentInventory.GuiFolder,
        };

        static readonly HashSet<string> RuntimeExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".cubdec", ".json", ".wav", ".png",
        };

        public int callbackOrder => 0;

        public static bool Bypassed => Environment.GetEnvironmentVariable(BypassVariable) == "1";

        public static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        /// <summary>The local cache a build stages from: CUBEGAME_ALPHA_CONTENT, else &lt;project&gt;/.scratch/content.</summary>
        public static string SourceRoot
        {
            get
            {
                string overridden = Environment.GetEnvironmentVariable(SourceVariable);
                return !string.IsNullOrEmpty(overridden) ? Path.GetFullPath(overridden) : AlphaContentSource.EditorCacheRoot;
            }
        }

        public static string IconSourceDirectory => Path.Combine(ProjectRoot, "AlphaOutput", "icons");

        /// <summary>StreamingAssets/CubeGame/AlphaContent of a built Windows player (next to its executable).</summary>
        public static string PlayerContentRoot(string executablePath)
        {
            string directory = Path.GetDirectoryName(Path.GetFullPath(executablePath));
            string data = Path.Combine(directory, Path.GetFileNameWithoutExtension(executablePath) + "_Data");
            return Path.Combine(data, "StreamingAssets", "CubeGame", "AlphaContent");
        }

        /// <summary>What a build would stage, counted on the local cache (icons counted from AlphaOutput/icons).</summary>
        public static AlphaContentInventory ScanSource()
        {
            AlphaContentInventory inventory = AlphaContentInventory.Scan(SourceRoot);
            if (inventory.iconAtlas == 0
                && File.Exists(Path.Combine(IconSourceDirectory, AlphaContentInventory.IconAtlasImage))
                && File.Exists(Path.Combine(IconSourceDirectory, AlphaContentInventory.IconAtlasIndex)))
            {
                inventory.iconAtlas = 1;
            }

            return inventory;
        }

        static bool IsWindowsPlayer(BuildReport report) =>
            report.summary.platform == BuildTarget.StandaloneWindows64 || report.summary.platform == BuildTarget.StandaloneWindows;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (!IsWindowsPlayer(report)) return;
            if (Bypassed)
            {
                Debug.LogWarning($"[AlphaContent] CONTENT_GATE_BYPASSED {BypassVariable}=1 — this Player is built WITHOUT Alpha content " +
                                 "and is not a release build");
                return;
            }

            AlphaContentInventory source = ScanSource();
            List<string> missing = source.MissingForRelease();
            if (missing.Count > 0)
            {
                string message = $"build FAIL: Alpha content missing — {string.Join("; ", missing)} (source {SourceRoot}; " +
                                 $"icons {IconSourceDirectory}). Stage the local cache first, or set {BypassVariable}=1 for a " +
                                 "non-release development build.";
                Debug.LogError("[AlphaContent] CONTENT_GATE_FAIL " + message);
                throw new BuildFailedException(message);
            }

            Debug.Log($"[AlphaContent] CONTENT_GATE_SOURCE_OK {source.Summary()} source={SourceRoot}");
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (!IsWindowsPlayer(report) || Bypassed) return;
            if (report.summary.result == BuildResult.Failed || report.summary.result == BuildResult.Cancelled) return;
            string target = PlayerContentRoot(report.summary.outputPath);
            AlphaContentInventory staged = Stage(SourceRoot, IconSourceDirectory, target);
            List<string> missing = staged.MissingForRelease();
            if (missing.Count > 0)
            {
                string message = $"build FAIL: staged Alpha content incomplete — {string.Join("; ", missing)} (target {target})";
                Debug.LogError("[AlphaContent] CONTENT_GATE_FAIL " + message);
                throw new BuildFailedException(message);
            }

            Debug.Log($"[AlphaContent] CONTENT_STAGED {staged.Summary()} target={target}");
        }

        /// <summary>
        /// Mirrors the runtime categories of <paramref name="sourceRoot"/> (+ the icon atlas) into
        /// <paramref name="targetRoot"/>, removes what the source no longer has, writes manifest.json and
        /// returns the inventory counted on the target.
        /// </summary>
        public static AlphaContentInventory Stage(string sourceRoot, string iconDirectory, string targetRoot)
        {
            Directory.CreateDirectory(targetRoot);
            var kept = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string folder in RuntimeFolders)
            {
                string from = Path.Combine(sourceRoot, folder);
                if (!Directory.Exists(from)) continue;
                foreach (string file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
                {
                    if (!RuntimeExtensions.Contains(Path.GetExtension(file))) continue;
                    string relative = file.Substring(sourceRoot.Length).TrimStart('\\', '/');
                    CopyIfChanged(file, Path.Combine(targetRoot, relative));
                    kept.Add(Path.GetFullPath(Path.Combine(targetRoot, relative)));
                }
            }

            // R3: the icon atlas travels with the content root, the runtime reads <root>/icons.
            string iconsFrom = Directory.Exists(Path.Combine(sourceRoot, AlphaContentInventory.IconsFolder))
                ? Path.Combine(sourceRoot, AlphaContentInventory.IconsFolder)
                : iconDirectory;
            foreach (string name in new[] { AlphaContentInventory.IconAtlasImage, AlphaContentInventory.IconAtlasIndex })
            {
                string file = iconsFrom != null ? Path.Combine(iconsFrom, name) : null;
                if (file == null || !File.Exists(file)) continue;
                string to = Path.Combine(targetRoot, AlphaContentInventory.IconsFolder, name);
                CopyIfChanged(file, to);
                kept.Add(Path.GetFullPath(to));
            }

            string manifestPath = Path.GetFullPath(Path.Combine(targetRoot, AlphaContentInventory.ManifestFile));
            foreach (string file in Directory.GetFiles(targetRoot, "*", SearchOption.AllDirectories))
            {
                string full = Path.GetFullPath(file);
                if (!kept.Contains(full) && !string.Equals(full, manifestPath, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(full);
                }
            }

            AlphaContentInventory staged = AlphaContentInventory.Scan(targetRoot);
            staged.stagedUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
            staged.sourceCommit = Environment.GetEnvironmentVariable("CUBEGAME_BUILD_COMMIT") ?? "";
            File.WriteAllText(manifestPath, staged.ToJson());
            return staged;
        }

        static void CopyIfChanged(string from, string to)
        {
            var source = new FileInfo(from);
            var target = new FileInfo(to);
            if (target.Exists && target.Length == source.Length && target.LastWriteTimeUtc == source.LastWriteTimeUtc) return;
            Directory.CreateDirectory(Path.GetDirectoryName(to));
            File.Copy(from, to, true);
            File.SetLastWriteTimeUtc(to, source.LastWriteTimeUtc);
        }

        [MenuItem("CubeGame/Alpha content/Report local cache")]
        public static void ReportSource()
        {
            AlphaContentInventory source = ScanSource();
            List<string> missing = source.MissingForRelease();
            Debug.Log($"[AlphaContent] SOURCE {source.Summary()} root={SourceRoot} " +
                      (missing.Count == 0 ? "RELEASE_READY" : "MISSING " + string.Join("; ", missing)));
        }
    }
}
