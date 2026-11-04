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
// Kept in the runtime assembly so Unity can actually add the component: MonoBehaviours that live in
// an Editor assembly cannot be instantiated at runtime.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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

    int _frames;
    Transform _head, _hand, _sword;
    readonly List<Vector3> _heads = new List<Vector3>();
    readonly List<Vector3> _hands = new List<Vector3>();
    readonly List<Vector3> _sockets = new List<Vector3>();
    bool _finished;

    void Awake()
    {
        foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (t.name == "part_head") _head = t;
            else if (t.name == "part_hand_r") _hand = t;
            else if (t.name == "part_sword") _sword = t;
        }
    }

    void Update()
    {
        if (_finished) return;
        if (_head != null) _heads.Add(_head.position);
        if (_hand != null) _hands.Add(_hand.position);
        if (_sword != null && _hand != null) _sockets.Add(_hand.InverseTransformPoint(_sword.position));
        _frames++;
        if (_frames < FrameLimit) return;
        _finished = true;

        var problems = new List<string>();
        if (_heads.Count == 0 || _hands.Count == 0 || _sockets.Count == 0)
            problems.Add("PlayMode sampler could not find head/hand/sword parts");
        problems.AddRange(Moved("head", _heads, 0.002f));
        problems.AddRange(Moved("hand", _hands, 0.02f));
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

    void WriteReport(string result, List<string> problems)
    {
        Directory.CreateDirectory("PilotOutput");
        var json = new StringBuilder();
        json.AppendLine("{");
        json.AppendLine($"  \"result\": \"{result}\",");
        json.AppendLine($"  \"session\": \"{SessionId}\",");
        json.AppendLine($"  \"frames\": {_frames},");
        json.AppendLine($"  \"requiredFrames\": {RequiredFrames},");
        json.AppendLine($"  \"unityVersion\": \"{Application.unityVersion}\",");
        json.AppendLine($"  \"headTravel\": \"{Travel(_heads).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}\",");
        json.AppendLine($"  \"handTravel\": \"{Travel(_hands).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}\",");
        json.AppendLine($"  \"problems\": [{string.Join(",", problems.ConvertAll(p => "\"" + p.Replace("\"", "'") + "\""))}]");
        json.AppendLine("}");
        File.WriteAllText("PilotOutput/playmode.json", json.ToString());
        File.WriteAllText("PilotOutput/playmode.txt",
            $"PLAYMODE_RESULT={result}\nsession={SessionId}\nframes={_frames}/{RequiredFrames}\n" +
            string.Join("\n", problems) + "\n");
    }

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
            problems.Add($"{label}: does not move in PlayMode (travel {travel:0.####} < {minimum})");
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
