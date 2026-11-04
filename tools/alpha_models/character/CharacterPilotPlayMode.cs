// S05A-1-REVIEW-FIX-02 — PlayMode lifecycle and pre-play validation.
//
// FIX-04: RunPlayMode refuses to enter PlayMode when the assembly is broken, and a PASS now requires
//         a clean build, a bound animator, the eight required parts, >= 40 real frames, passing
//         animation/socket checks and a normal Unity process exit.
// FIX-05: the crash (0xC0000005) happened because EditorApplication.Exit() was called synchronously
//         inside the playModeStateChanged(EnteredEditMode) callback, i.e. in the middle of the
//         editor's own play-mode transition ("PlayMode ended, exiting with code 0" -> "Crash!!!" in
//         the log). The exit is now deferred to a later EditorApplication.update tick after the
//         transition finished, the handler unsubscribes itself and runs at most once.
//
// Command-line test hooks (documented, used only by the negative-test harness):
//   -pilotFrames N        sampler collects N frames instead of 40 (a value < 40 must FAIL)
//   -pilotSkipAnimator    BuildCharacter omits ProceduralCharacterAnimator (must FAIL)

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class CharacterPilotVerifier
{
    const int RequiredPlayModeFrames = 40;
    const int ExitDelayTicks = 10;

    static readonly string[] RequiredParts =
    {
        "body", "head", "hair", "hand_l", "hand_r", "foot_l", "foot_r", "sword",
    };

    static string PlayModeSessionId = "";
    static bool ExitPumpInstalled;

    // ------------------------------------------------------------------
    // FIX-04: prerequisites
    // ------------------------------------------------------------------

    /// <summary>Everything that must hold before PlayMode may be entered.</summary>
    internal static List<string> ValidatePlayModePrerequisites(AssemblyBuild build)
    {
        var problems = new List<string>();
        if (build == null) return new List<string> { "playmode: assembly build is null" };
        if (build.problems.Count > 0)
            problems.AddRange(build.problems.Select(p => "build: " + p));

        foreach (var id in RequiredParts.Where(id => build.parts == null || !build.parts.ContainsKey(id)))
            problems.Add($"playmode: required part '{id}' is missing");

        if (build.animator == null)
            problems.Add("playmode: ProceduralCharacterAnimator is missing");
        else if (!build.animator.IsBound)
            problems.Add("playmode: ProceduralCharacterAnimator is not bound to the eight parts");

        return problems;
    }

    /// <summary>The sampler's own verdict, including the frame count it actually collected.</summary>
    internal static List<string> ValidatePlayModeResult(int frames, int requiredFrames,
        IReadOnlyList<string> animationProblems)
    {
        var problems = new List<string>();
        if (frames < requiredFrames)
            problems.Add($"playmode: only {frames} frames were processed, {requiredFrames} required");
        if (animationProblems != null) problems.AddRange(animationProblems);
        return problems;
    }

    // ------------------------------------------------------------------
    // entry point
    // ------------------------------------------------------------------

    [MenuItem("CubeGame Pilot/Run PlayMode animation check")]
    public static void RunPlayMode()
    {
        Directory.CreateDirectory(OutputDir);
        CleanOutputs("playmode", "report_exception");
        PlayModeSessionId = Guid.NewGuid().ToString("N");
        var failures = new List<string>();
        try
        {
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var build = BuildCharacter(false);
            // `-pilotFrames N` only lowers how many frames the sampler collects; the requirement
            // stays 40, so a short run is reported as FAIL by the sampler itself.
            var frameLimit = CommandLineInt("-pilotFrames", RequiredPlayModeFrames);
            failures.AddRange(ValidatePlayModePrerequisites(build));

            if (failures.Count > 0)
            {
                WritePlayModeFailure("FAIL", 0, RequiredPlayModeFrames, failures);
                throw new Exception("PLAYMODE_RESULT=FAIL - assembly is not playable, see PilotOutput/playmode.txt");
            }

            AddPlayModeCameraAndLight();
            build.animator.AnimateInPlayMode = true;

            var sampler = build.root.AddComponent<PlayModeSampler>();
            sampler.SessionId = PlayModeSessionId;
            sampler.RequiredFrames = RequiredPlayModeFrames;
            sampler.FrameLimit = frameLimit;
            sampler.enabled = true;

            var exitCode = 1;
            PlayModeSampler.OnFinished = code =>
            {
                exitCode = code;
                Debug.Log($"PlayMode sampling finished with exit code {code}");
                RequestEditorExitAfterTransition(code);
            };

            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            Debug.Log($"playmode session {PlayModeSessionId}: parts={build.parts.Count} " +
                      $"requiredFrames={RequiredPlayModeFrames} frameLimit={frameLimit}");
            EditorApplication.isPlaying = true;
        }
        catch (Exception e)
        {
            if (failures.Count == 0)
            {
                failures.Add("exception: " + e.Message);
                WritePlayModeException(e);
            }
            throw;
        }
    }

    static void AddPlayModeCameraAndLight()
    {
        var camera = new GameObject("PilotCamera").AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.10f, 0.10f, 0.13f, 1f);
        var light = new GameObject("PilotLight").AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1f;
        light.transform.rotation = Quaternion.Euler(35f, -35f, 0f);
    }

    // ------------------------------------------------------------------
    // FIX-05: safe exit
    // ------------------------------------------------------------------

    /// <summary>
    /// Called once PlayMode has fully ended. Exit is requested from a later editor update tick, never
    /// from inside the play-mode transition callback (that caused STATUS_ACCESS_VIOLATION).
    /// </summary>
    static void RequestEditorExitAfterTransition(int exitCode)
    {
        if (ExitPumpInstalled) return;
        ExitPumpInstalled = true;
        var ticks = ExitDelayTicks;
        void Pump()
        {
            if (ticks-- > 0) return;
            EditorApplication.update -= Pump;
            Debug.Log($"pilot exit pump: leaving editor with code {exitCode}");
            EditorApplication.Exit(exitCode);
        }
        EditorApplication.update += Pump;
    }

    static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode) return;
        // unsubscribe immediately: the handler must never run twice
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        var resultPath = Path.Combine(OutputDir, "playmode.txt");
        var text = File.Exists(resultPath) ? File.ReadAllText(resultPath) : "";
        var passed = text.Contains("PLAYMODE_RESULT=PASS") && text.Contains(PlayModeSessionId);
        var code = passed ? 0 : 1;
        Debug.Log($"PlayMode ended (session {PlayModeSessionId}), reported result: " +
                  $"{(passed ? "PASS" : "FAIL")}; requesting exit code {code}");
        RequestEditorExitAfterTransition(code);
    }

    // ------------------------------------------------------------------
    // reports
    // ------------------------------------------------------------------

    static void WritePlayModeFailure(string verdict, int frames, int requiredFrames,
        List<string> problems)
    {
        var json = new StringBuilder();
        json.AppendLine("{");
        json.AppendLine($"  \"result\": {Q(verdict)},");
        json.AppendLine($"  \"session\": {Q(PlayModeSessionId)},");
        json.AppendLine($"  \"frames\": {frames},");
        json.AppendLine($"  \"requiredFrames\": {requiredFrames},");
        json.AppendLine($"  \"unityVersion\": {Q(Application.unityVersion)},");
        json.AppendLine($"  \"problems\": [{string.Join(",", problems.Select(Q))}]");
        json.AppendLine("}");
        File.WriteAllText(Path.Combine(OutputDir, "playmode.json"), json.ToString(), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(OutputDir, "playmode.txt"),
            $"PLAYMODE_RESULT={verdict}\nsession={PlayModeSessionId}\nframes={frames}/{requiredFrames}\n" +
            string.Join("\n", problems) + "\n", new UTF8Encoding(false));
        Debug.Log($"PLAYMODE_RESULT={verdict} (pre-play checks)");
    }

    static void WritePlayModeException(Exception e)
    {
        try
        {
            Directory.CreateDirectory(OutputDir);
            File.WriteAllText(Path.Combine(OutputDir, "playmode_exception.txt"), e.ToString(),
                new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(OutputDir, "playmode.txt"),
                $"PLAYMODE_RESULT=EXCEPTION\nsession={PlayModeSessionId}\n{e}\n", new UTF8Encoding(false));
        }
        catch (Exception nested)
        {
            Debug.LogError("cannot write playmode exception report: " + nested);
        }
    }

    internal static int CommandLineInt(string option, int fallback)
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == option && int.TryParse(args[i + 1], out var value)) return value;
        }
        return fallback;
    }

    internal static bool CommandLineFlag(string flag) =>
        Environment.GetCommandLineArgs().Any(a => a == flag);
}
