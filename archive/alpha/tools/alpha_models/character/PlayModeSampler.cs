// S05A-1 pilot — PlayMode sampler (runtime assembly).
//
// Records real Update() frames of the assembled hierarchy, checks that the animator moves parts and
// that the weapon keeps a constant offset in the hand, writes PilotOutput/playmode.* and then hands
// control back through OnFinished.
//
// S05A-1-REVIEW-FIX-02:
//  * the verdict requires at least RequiredFrames collected frames (a short run fails);
//  * the report carries the session id issued by RunPlayMode, so a stale report cannot be mistaken
//    for a fresh one;
//  * OnFinished is invoked at most once, and the sampler never calls EditorApplication.Exit itself
//    (the editor side defers that until the play-mode transition has finished).
//
// S05A-2 (FIX-07/FIX-08):
//  * this file is the ONLY writer of PilotOutput/playmode.txt and playmode.json. The lifecycle writes
//    playmode_lifecycle.* and playmode_verdict.* and reads this report back verbatim, so the sampler's
//    diagnostics can no longer be erased by a verdict downgrade;
//  * the two-handed grip residual is recorded on every attack frame instead of being declared and never
//    filled (the attack branch used to fail no matter what the animation did);
//  * numbers are formatted with the invariant culture (a ru-RU machine used to print "не число");
//  * in attack mode the sampler advances the attack clip deterministically over the run (see Update).
//
// Kept in the runtime assembly so Unity can actually add the component: MonoBehaviours that live in
// an Editor assembly cannot be instantiated at runtime.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CubeGame.CharacterPilot;
using UnityEngine;

public class PlayModeSampler : MonoBehaviour
{
    public static Action<int> OnFinished;

    /// <summary>Frames the run must collect before it may report PASS.</summary>
    public int RequiredFrames = 40;

    /// <summary>
    /// Frames the sampler actually collects. Normally equal to RequiredFrames; the `-pilotFrames N`
    /// test hook lowers it so a prematurely finished run must report FAIL.
    /// </summary>
    public int FrameLimit = 40;

    /// <summary>Session id issued by RunPlayMode; copied verbatim into the report.</summary>
    public string SessionId = "";

    /// <summary>Test hook: report completion twice, so the lifecycle duplicate guard can be exercised.</summary>
    public bool TriggerDoubleFinish;

    /// <summary>When true the sampler validates the two-handed grip instead of the walk cycle.</summary>
    public bool AttackMode;

    /// <summary>
    /// World-space tolerance for the left palm on the secondary grip. The palm is solved onto the grip
    /// every frame, so the residual is numerical; 0.02 world units = 2% of one voxel (1 voxel = 1 unit),
    /// i.e. clearly below the smallest visible offset, while still catching a lost/broken solve.
    /// </summary>
    public const float GripTolerance = 0.02f;

    int _frames;
    Transform _head, _hand, _sword;
    readonly List<Vector3> _heads = new List<Vector3>();
    readonly List<Vector3> _hands = new List<Vector3>();
    readonly List<Vector3> _sockets = new List<Vector3>();
    readonly List<float> _gripErrors = new List<float>();
    ProceduralCharacterAnimator _animator;
    readonly List<Vector3> _tips = new List<Vector3>();
    bool _finished;

    void Awake()
    {
        foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (t.name == "part_head") _head = t;
            else if (t.name == "part_hand_r") _hand = t;
            else if (t.name == "part_sword") _sword = t;
        }
        _animator = FindFirstObjectByType<ProceduralCharacterAnimator>();
        if (!AttackMode || _animator == null) return;
        // The attack is measured on the attack pose only: the sampler owns the phase (see Update), and a
        // second Time.time-driven pose from the animator's own Update would fight with it.
        _animator.AnimateInPlayMode = false;
        _animator.TwoHandedAttack = true;
    }

    void Update()
    {
        if (_finished) return;
        if (AttackMode)
        {
            // S05A-2: the attack clip is 0.4 s long (Alpha-derived phase timing), but 40 real Update()
            // frames of a batch-mode run cover an uncontrolled, machine-dependent slice of it (the first
            // measured run covered only 0.26 units of blade travel out of the 0.35 expected). The metric
            // therefore has to be defined on the clip, not on wall-clock frames: the sampler advances the
            // attack by exactly one full cycle over the run and measures every resulting pose.
            if (_animator != null) _animator.Sample((float)_frames / FrameLimit);
            // Every frame's residual is recorded, not just the last one: a solve that works at idle but
            // drifts during the strike is exactly what the two-hand contract must catch.
            _gripErrors.Add(_animator != null ? _animator.LeftPalmToSecondaryGrip() : float.NaN);
        }
        if (_head != null) _heads.Add(_head.position);
        if (_hand != null) _hands.Add(_hand.position);
        if (_sword != null && _hand != null) _sockets.Add(_hand.InverseTransformPoint(_sword.position));
        if (_sword != null) _tips.Add(_sword.TransformPoint(0f, 30f, 0f));
        _frames++;
        if (_frames < FrameLimit) return;
        _finished = true;

        var problems = new List<string>();
        if (_heads.Count == 0 || _hands.Count == 0 || _sockets.Count == 0)
            problems.Add("PlayMode sampler could not find head/hand/sword parts");
        if (AttackMode)
        {
            // D58: an attack is judged by what the attack does — the blade sweeps, the left palm stays
            // on the secondary grip, and the weapon stays fixed in the right hand.
            if (_animator == null) problems.Add("attack: animator not found in PlayMode");
            problems.AddRange(Moved("blade tip", _tips, 0.35f));
            problems.AddRange(Moved("right hand", _hands, 0.02f));
            if (_gripErrors.Count == 0)
                problems.Add("attack: animator did not report the two-hand grip");
            else
            {
                var anyNaN = false;
                var worst = 0f;
                foreach (var e in _gripErrors)
                {
                    if (float.IsNaN(e) || float.IsInfinity(e)) { anyNaN = true; continue; }
                    if (e > worst) worst = e;
                }
                if (anyNaN)
                    problems.Add("attack: two-hand grip distance is NaN (the animator lost its grip " +
                                 "binding in PlayMode)");
                else if (worst > GripTolerance)
                    problems.Add($"attack: left palm left the secondary grip by {Num(worst)}");
                else
                    Debug.Log($"[playmode] attack grip contact ok over {_gripErrors.Count} frames " +
                              $"(worst {Num(worst)})");
            }
        }
        else
        {
            problems.AddRange(Moved("head", _heads, 0.002f));
            problems.AddRange(Moved("hand", _hands, 0.02f));
        }
        problems.AddRange(SocketSteady(_sockets));
        if (_frames < RequiredFrames)
            problems.Add($"only {_frames} frames were processed, {RequiredFrames} required");

        var result = problems.Count == 0 ? "PASS" : "FAIL";
        WriteReport(result, problems);
        Debug.Log($"PLAYMODE_RESULT={result} frames={_frames} session={SessionId}");
        OnFinished?.Invoke(problems.Count == 0 ? 0 : 1);
        if (TriggerDoubleFinish)
        {
            Debug.Log("[lifecycle] test hook: reporting sampler completion a second time");
            OnFinished?.Invoke(problems.Count == 0 ? 0 : 1);
        }
    }

    /// <summary>
    /// Writes the sampler-owned report. Invariant culture only: on a ru-RU machine the default formatting
    /// printed "не число" for NaN and "0,2581" for numbers, which makes the report machine-dependent.
    /// </summary>
    void WriteReport(string result, List<string> problems)
    {
        Directory.CreateDirectory("PilotOutput");
        var json = new StringBuilder();
        json.AppendLine("{");
        json.AppendLine($"  \"result\": \"{result}\",");
        json.AppendLine($"  \"session\": \"{SessionId}\",");
        json.AppendLine($"  \"mode\": \"{(AttackMode ? "attack" : "walk")}\",");
        json.AppendLine($"  \"frames\": {_frames},");
        json.AppendLine($"  \"requiredFrames\": {RequiredFrames},");
        json.AppendLine($"  \"unityVersion\": \"{Application.unityVersion}\",");
        json.AppendLine($"  \"headTravel\": \"{Num(Travel(_heads))}\",");
        json.AppendLine($"  \"handTravel\": \"{Num(Travel(_hands))}\",");
        if (AttackMode)
        {
            json.AppendLine($"  \"bladeTipTravel\": \"{Num(Travel(_tips))}\",");
            json.AppendLine($"  \"worstGripError\": \"{Num(WorstGrip())}\",");
            json.AppendLine($"  \"gripSamples\": {_gripErrors.Count},");
        }
        json.AppendLine($"  \"problems\": [{string.Join(",", problems.ConvertAll(p => "\"" + p.Replace("\"", "'") + "\""))}]");
        json.AppendLine("}");
        File.WriteAllText("PilotOutput/playmode.json", json.ToString());
        var text = new StringBuilder();
        text.AppendLine($"PLAYMODE_RESULT={result}");
        text.AppendLine($"session={SessionId}");
        text.AppendLine($"frames={_frames}/{RequiredFrames}");
        foreach (var problem in problems) text.AppendLine(problem);
        File.WriteAllText("PilotOutput/playmode.txt", text.ToString());
    }

    float WorstGrip()
    {
        var worst = 0f;
        foreach (var e in _gripErrors)
        {
            if (float.IsNaN(e) || float.IsInfinity(e)) return float.NaN;
            if (e > worst) worst = e;
        }
        return worst;
    }

    static string Num(float value) =>
        float.IsNaN(value) ? "NaN" :
        float.IsInfinity(value) ? "inf" :
        value.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);

    static List<string> Moved(string label, List<Vector3> frames, float minimum)
    {
        var problems = new List<string>();
        if (frames.Count < 2)
        {
            problems.Add($"{label}: not enough samples");
            return problems;
        }
        var travel = Travel(frames);
        if (travel < minimum)
            problems.Add($"{label}: does not move in PlayMode (travel {Num(travel)} < {Num(minimum)})");
        return problems;
    }

    static float Travel(List<Vector3> frames)
    {
        var min = frames[0];
        var max = frames[0];
        foreach (var f in frames) { min = Vector3.Min(min, f); max = Vector3.Max(max, f); }
        return (max - min).magnitude;
    }

    /// <summary>The weapon must keep a fixed offset in the hand's local space.</summary>
    static List<string> SocketSteady(List<Vector3> localOffsets)
    {
        var problems = new List<string>();
        if (localOffsets.Count < 2)
        {
            problems.Add("weapon socket check needs at least two frames");
            return problems;
        }
        var reference = localOffsets[0];
        for (int i = 1; i < localOffsets.Count; i++)
        {
            var delta = localOffsets[i] - reference;
            if (delta.magnitude > 1e-3f)
            {
                problems.Add($"sword drifted in the hand at frame {i}: {Fmt(delta)}");
                break;
            }
        }
        return problems;
    }

    static string Fmt(Vector3 v) =>
        string.Format(System.Globalization.CultureInfo.InvariantCulture,
            "({0:0.###},{1:0.###},{2:0.###})", v.x, v.y, v.z);
}
