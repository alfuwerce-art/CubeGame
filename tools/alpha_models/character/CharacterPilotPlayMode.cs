// S05A-1-REVIEW-FIX-03 — PlayMode lifecycle, event-order contract and safe editor exit.
//
// The previous revision requested the editor exit directly from PlayModeSampler.OnFinished, i.e. while the
// editor was still in PlayMode. A deferred Exit() could therefore run before the editor had left PlayMode.
//
// Contract now enforced here:
//   RunPlayMode (pre-flight checks) -> PlayMode starts -> sampler collects >= 40 frames ->
//   OnFinished(code) records the verdict and requests `EditorApplication.isPlaying = false` ->
//   Unity performs its normal play-mode transition -> on EnteredEditMode the exit is scheduled ->
//   an EditorApplication.update tick confirms the editor is really out of PlayMode -> Exit(code).
//
// Every step appends a marker to PilotOutput/playmode_events.txt (and playmode_events.json). The verdict
// is FAIL if the markers are out of order, duplicated, or if EditMode was reached before the sampler
// finished.
//
// FIX-04 (kept): pre-flight checks refuse to enter PlayMode on a broken assembly.
// FIX-05 (kept): the editor exit is never called from inside a play-mode transition callback.
//
// Test hooks (command line, never used by a normal run):
//   -pilotFrames N            sampler collects N frames (requirement stays 40 -> FAIL)
//   -pilotSkipAnimator        animator omitted (pre-flight FAIL)
//   -pilotDoubleFinish        sampler reports completion twice (must be detected)
//   -pilotDoubleRegister      the state handler is registered twice (must be detected)
//   -pilotExitBeforeEditMode  an exit is requested while still playing (must be refused)
//   -pilotForceExitEarly      PlayMode is stopped before sampling finishes (must FAIL)

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
    const int ExitMaxTicks = 600;      // safety budget: abort with FAIL instead of hanging

    static readonly string[] RequiredParts =
    {
        "body", "head", "hair", "hand_l", "hand_r", "foot_l", "foot_r", "sword",
    };

    // ---- lifecycle state (reset at the start of every run) ----
    static string PlayModeSessionId = "";
    static readonly List<string> PlayModeEvents = new List<string>();
    static bool SamplerFinished;
    static bool DuplicateSamplerFinish;
    static bool DuplicateHandlerRegistration;
    static bool ForbiddenExitAttempt;
    static bool EditModeReached;
    static bool EditorExitExecuted;
    static bool ExitPumpInstalled;
    static int PendingExitCode = 1;
    static int ExitTicksLeft = ExitDelayTicks;
    static int ExitBudgetLeft = ExitMaxTicks;

    // ------------------------------------------------------------------
    // FIX-04: prerequisites
    // ------------------------------------------------------------------

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
    // event order contract
    // ------------------------------------------------------------------

    internal const string MarkSessionStarted = "PLAYMODE_SESSION_STARTED";
    internal const string MarkEntered = "PLAYMODE_ENTERED";
    internal const string MarkSamplerFinished = "SAMPLER_FINISHED";
    internal const string MarkExitRequested = "PLAYMODE_EXIT_REQUESTED";
    internal const string MarkExiting = "PLAYMODE_EXITING";
    internal const string MarkEditModeEntered = "EDITMODE_ENTERED";
    internal const string MarkExitScheduled = "EDITOR_EXIT_SCHEDULED";
    internal const string MarkExitRejected = "EDITOR_EXIT_REJECTED";
    internal const string MarkExitExecuted = "EDITOR_EXIT_EXECUTED";
    internal const string MarkExitAborted = "EDITOR_EXIT_ABORTED";

    /// <summary>
    /// Pure order check for the lifecycle markers. The rule that matters most: the editor may only exit
    /// after EditMode has been entered, and only after the sampler reported completion.
    /// </summary>
    internal static List<string> ValidateEventOrder(IReadOnlyList<string> events)
    {
        var problems = new List<string>();
        if (events == null || events.Count == 0) return new List<string> { "lifecycle: no events recorded" };
        var list = events.ToList();

        int Index(string marker)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i] == marker || list[i].StartsWith(marker + " ", StringComparison.Ordinal)) return i;
            return -1;
        }
        int Count(string marker) => list.Count(e => e == marker || e.StartsWith(marker + " ", StringComparison.Ordinal));

        if (Count(MarkSessionStarted) != 1) problems.Add($"lifecycle: {MarkSessionStarted} must appear exactly once");
        if (Count(MarkSamplerFinished) < 1) problems.Add("lifecycle: sampler never reported completion");
        if (Count(MarkSamplerFinished) > 1) problems.Add("lifecycle: sampler reported completion more than once");
        if (Count(MarkEditModeEntered) != 1) problems.Add($"lifecycle: {MarkEditModeEntered} must appear exactly once");
        if (Count(MarkExitExecuted) > 1) problems.Add("lifecycle: editor exit executed more than once");
        if (Count(MarkExitRejected) > 0) problems.Add("lifecycle: an editor exit was attempted while still in PlayMode");

        int sampler = Index(MarkSamplerFinished);
        int exitRequested = Index(MarkExitRequested);
        int editMode = Index(MarkEditModeEntered);
        int scheduled = Index(MarkExitScheduled);
        int executed = Index(MarkExitExecuted);

        if (editMode >= 0 && sampler < 0)
            problems.Add("lifecycle: PlayMode ended before the sampler completed");
        if (sampler >= 0 && exitRequested >= 0 && exitRequested < sampler)
            problems.Add("lifecycle: exit requested before the sampler finished");
        if (editMode >= 0 && exitRequested >= 0 && editMode < exitRequested)
            problems.Add("lifecycle: EditMode reached before the exit was requested");
        if (executed >= 0 && editMode < 0)
            problems.Add("lifecycle: editor exit executed before EditMode was entered");
        if (executed >= 0 && scheduled >= 0 && executed < scheduled)
            problems.Add("lifecycle: editor exit executed before it was scheduled");
        if (executed >= 0 && executed != list.Count - 1)
            problems.Add("lifecycle: editor exit is not the last recorded event");
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
        ResetLifecycleState();
        PlayModeSessionId = Guid.NewGuid().ToString("N");
        Mark(MarkSessionStarted);

        var failures = new List<string>();
        try
        {
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var build = BuildCharacter(false);
            var frameLimit = CommandLineInt("-pilotFrames", RequiredPlayModeFrames);
            if (CommandLineFlag("-pilotUnbindHair"))
            {
                // Test hook: drop the hair from the binding so IsBound must become false.
                build.animator.Bind(Find(build, "body"), Find(build, "head"), null,
                    Find(build, "hand_l"), Find(build, "hand_r"), Find(build, "foot_l"),
                    Find(build, "foot_r"), Find(build, "sword"));
                Debug.Log($"[lifecycle] test hook: animator unbound (IsBound={build.animator.IsBound})");
            }
            failures.AddRange(ValidatePlayModePrerequisites(build));

            if (failures.Count > 0)
            {
                WritePlayModeFailure("FAIL", 0, RequiredPlayModeFrames, failures);
                WriteEventsReport("FAIL", failures);
                throw new Exception("PLAYMODE_RESULT=FAIL - assembly is not playable, see PilotOutput/playmode.txt");
            }

            AddPlayModeCameraAndLight();
            build.animator.AnimateInPlayMode = true;

            var sampler = build.root.AddComponent<PlayModeSampler>();
            sampler.SessionId = PlayModeSessionId;
            sampler.RequiredFrames = RequiredPlayModeFrames;
            sampler.FrameLimit = frameLimit;
            sampler.TriggerDoubleFinish = CommandLineFlag("-pilotDoubleFinish");
            sampler.enabled = true;

            PlayModeSampler.OnFinished = HandleSamplerFinished;
            RegisterStateHandler(CommandLineFlag("-pilotDoubleRegister"));

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            Debug.Log($"playmode session {PlayModeSessionId}: parts={build.parts.Count} " +
                      $"requiredFrames={RequiredPlayModeFrames} frameLimit={frameLimit}");
            EditorApplication.isPlaying = true;

            if (CommandLineFlag("-pilotExitBeforeEditMode")) SimulateForbiddenEarlyExit();
            if (CommandLineFlag("-pilotForceExitEarly")) ScheduleForcedEarlyPlayModeExit();
        }
        catch (Exception e)
        {
            if (failures.Count == 0)
            {
                failures.Add("exception: " + e.Message);
                WritePlayModeException(e);
                WriteEventsReport("EXCEPTION", failures);
            }
            throw;
        }
    }

    static void ResetLifecycleState()
    {
        PlayModeEvents.Clear();
        SamplerFinished = false;
        DuplicateSamplerFinish = false;
        DuplicateHandlerRegistration = false;
        ForbiddenExitAttempt = false;
        EditModeReached = false;
        EditorExitExecuted = false;
        ExitPumpInstalled = false;
        PendingExitCode = 1;
        ExitTicksLeft = ExitDelayTicks;
        ExitBudgetLeft = ExitMaxTicks;
        PlayModeSampler.OnFinished = null;
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChangedDuplicate;
        EditorApplication.update -= PumpEditorExit;
    }

    static void RegisterStateHandler(bool alsoDuplicate)
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        if (!alsoDuplicate) return;
        // Simulation of a second, accidental registration: the lifecycle must notice the duplicate event.
        EditorApplication.playModeStateChanged += HandlePlayModeStateChangedDuplicate;
    }

    // ------------------------------------------------------------------
    // FIX-06: state machine
    // ------------------------------------------------------------------

    static void HandleSamplerFinished(int code)
    {
        if (SamplerFinished)
        {
            DuplicateSamplerFinish = true;
            Mark("SAMPLER_FINISHED_DUPLICATE");
            return;
        }
        SamplerFinished = true;
        PendingExitCode = code;
        Mark(MarkSamplerFinished, $"code={code}");
        // The sampler only *asks* to leave PlayMode; the editor exit is scheduled after EnteredEditMode.
        if (EditorApplication.isPlaying)
        {
            Mark(MarkExitRequested);
            EditorApplication.isPlaying = false;
        }
        else
        {
            Mark(MarkExitRequested, "already-outside-playmode");
        }
    }

    static void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            Mark(MarkEntered);
            return;
        }
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            Mark(MarkExiting);
            return;
        }
        if (state != PlayModeStateChange.EnteredEditMode) return;

        EditModeReached = true;
        Mark(MarkEditModeEntered);
        // Only our own handler is removed here: a duplicate registration must still be observable,
        // otherwise the duplicate-registration guard could never fire.
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;

        var ordering = ValidateEventOrder(PlayModeEvents);
        if (ordering.Count > 0)
        {
            PendingExitCode = 1;
            foreach (var problem in ordering) Mark("LIFECYCLE_PROBLEM", problem);
        }
        ScheduleEditorExit(PendingExitCode);
    }

    static void HandlePlayModeStateChangedDuplicate(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode) return;
        DuplicateHandlerRegistration = true;
        Mark("STATE_HANDLER_DUPLICATE");
    }

    /// <summary>
    /// Schedules the editor exit. Refuses (and records a failure) unless EditMode has really been entered,
    /// which is exactly the mistake the previous revision made.
    /// </summary>
    static void ScheduleEditorExit(int code)
    {
        if (!EditModeReached || EditorApplication.isPlaying)
        {
            ForbiddenExitAttempt = true;
            Mark(MarkExitRejected, $"isPlaying={EditorApplication.isPlaying} editMode={EditModeReached}");
            PendingExitCode = 1;
            return;
        }
        if (ExitPumpInstalled) return;
        ExitPumpInstalled = true;
        Mark(MarkExitScheduled, $"code={code}");
        EditorApplication.update += PumpEditorExit;
    }

    static void PumpEditorExit()
    {
        if (EditorExitExecuted) return;
        if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            // never exit in the middle of a play-mode transition
            ForbiddenExitAttempt = true;
            Mark(MarkExitAborted, "still inside PlayMode");
            EditorApplication.update -= PumpEditorExit;
            FinishEditorExit(1);
            return;
        }
        ExitTicksLeft--;
        ExitBudgetLeft--;
        if (ExitBudgetLeft <= 0)
        {
            Mark(MarkExitAborted, "delay budget exhausted");
            EditorApplication.update -= PumpEditorExit;
            FinishEditorExit(1);
            return;
        }
        if (ExitTicksLeft > 0) return;
        EditorApplication.update -= PumpEditorExit;
        FinishEditorExit(PendingExitCode);
    }

    static void FinishEditorExit(int code)
    {
        if (EditorExitExecuted) return;
        EditorExitExecuted = true;
        var ordering = ValidateEventOrder(PlayModeEvents);
        var resultPath = Path.Combine(OutputDir, "playmode.txt");
        var text = File.Exists(resultPath) ? File.ReadAllText(resultPath) : "";
        var passed = code == 0 &&
                     text.Contains("PLAYMODE_RESULT=PASS") &&
                     text.Contains(PlayModeSessionId) &&
                     ordering.Count == 0 && !DuplicateSamplerFinish &&
                     !DuplicateHandlerRegistration && !ForbiddenExitAttempt;
        if (!passed)
        {
            code = code == 0 ? 1 : code;
            foreach (var problem in ordering) Mark("LIFECYCLE_PROBLEM", problem);
            var reasons = new List<string>(ordering)
            {
                $"lifecycle: verdict downgraded to FAIL before exit (sampler={SamplerFinished}, " +
                $"duplicateFinish={DuplicateSamplerFinish}, duplicateHandler={DuplicateHandlerRegistration}, " +
                $"forbiddenExit={ForbiddenExitAttempt})",
            };
            WritePlayModeFailure("FAIL", CountFrames(text), RequiredPlayModeFrames, reasons);
        }
        Mark(MarkExitExecuted, $"code={code}");
        WriteEventsReport(passed ? "PASS" : "FAIL", ordering);
        Debug.Log($"pilot exit: leaving editor with code {code} (session {PlayModeSessionId})");
        EditorApplication.Exit(code);
    }

    static int CountFrames(string playmodeText)
    {
        var match = System.Text.RegularExpressions.Regex.Match(playmodeText, @"frames=(\d+)/");
        return match.Success && int.TryParse(match.Groups[1].Value, out var frames) ? frames : 0;
    }

    static void Mark(string marker, string detail = "")
    {
        var line = string.IsNullOrEmpty(detail) ? marker : $"{marker} {detail}";
        PlayModeEvents.Add(line);
        Debug.Log($"[lifecycle] {line} (session {PlayModeSessionId})");
    }

    // Test hook: stop PlayMode before the sampler has collected its frames.
    static void ScheduleForcedEarlyPlayModeExit()
    {
        var ticks = 8;
        void Tick()
        {
            if (ticks-- > 0) return;
            EditorApplication.update -= Tick;
            if (EditorApplication.isPlaying)
            {
                Debug.Log("[lifecycle] test hook: stopping PlayMode before the sampler finished");
                EditorApplication.isPlaying = false;
            }
        }
        EditorApplication.update += Tick;
    }

    // Test hook: try to leave the editor while still in PlayMode.
    internal static void SimulateForbiddenEarlyExit()
    {
        ScheduleEditorExit(0);
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
    // reports
    // ------------------------------------------------------------------

    static void WriteEventsReport(string verdict, IReadOnlyList<string> problems)
    {
        Directory.CreateDirectory(OutputDir);
        var lines = new StringBuilder();
        lines.AppendLine($"session={PlayModeSessionId}");
        lines.AppendLine($"verdict={verdict}");
        foreach (var e in PlayModeEvents) lines.AppendLine(e);
        File.WriteAllText(Path.Combine(OutputDir, "playmode_events.txt"), lines.ToString(), new UTF8Encoding(false));

        var json = new StringBuilder();
        json.AppendLine("{");
        json.AppendLine($"  \"session\": {Q(PlayModeSessionId)},");
        json.AppendLine($"  \"verdict\": {Q(verdict)},");
        json.AppendLine($"  \"events\": [{string.Join(",", PlayModeEvents.Select(Q))}],");
        json.AppendLine($"  \"problems\": [{string.Join(",", problems.Select(Q))}]");
        json.AppendLine("}");
        File.WriteAllText(Path.Combine(OutputDir, "playmode_events.json"), json.ToString(), new UTF8Encoding(false));
    }

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
        Debug.Log($"PLAYMODE_RESULT={verdict} (lifecycle/pre-play checks)");
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
