// AlphaControllerProbe.cs — PlayMode check of a full sequence on AlphaAnimationController:
//   idle (formula, amp 0) -> move (amp ramps up, walks) -> stop (amp ramps to 0) -> action clip
//   (baked from Alpha's drawModel) -> recovery (tail of the same clip) -> idle (formula)
// Every frame the rig must equal the active source exactly (formula of the driver state or the
// clip sample at the player cursor); at the two handovers the pose jump must stay below
// `maxHandoverJumpVox` (Alpha's standing pose is the clip's first and last key).
// Report: TransferOutput/controller_playmode.txt, marker CONTROLLER_PLAYMODE_RESULT=PASS|FAIL.
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace CubeGame.AlphaTransfer
{
    public class AlphaControllerProbe : MonoBehaviour
    {
        public string actionClip = "action";
        public int idleFrames = 20, moveFrames = 90, stopFrames = 40, afterFrames = 20, maxActionFrames = 600;
        public float maxHandoverJumpVox = 0.05f;
        public static bool Finished;
        public static string Verdict = "NOT_RUN";

        AlphaAnimationController _c; AlphaRig _rig;
        int _frame, _phase, _actionFrames, _after;
        float _maxPosErr, _maxRotErr, _jumpIn, _jumpOut, _maxActionMove;
        readonly List<string> _problems = new List<string>();
        readonly List<string> _log = new List<string>();
        Vector3[] _prev;

        void Start()
        {
            Finished = false; Verdict = "RUNNING";
            Time.captureDeltaTime = 1f / 60f;
            _c = GetComponent<AlphaAnimationController>(); _rig = GetComponent<AlphaRig>();
            if (_c == null) { _problems.Add("NO_CONTROLLER"); Finish(); return; }
            _c.Init();
            _problems.AddRange(_c.Problems);
            if (_problems.Count > 0) { Finish(); return; }
            _c.locomotion.followAmplitude = false;
            _c.locomotion.amplitude = 0f;
            _prev = new Vector3[_rig.parts.Length];
            Snapshot();
        }

        void Snapshot() { for (int i = 0; i < _rig.parts.Length; i++) _prev[i] = _rig.parts[i].localPosition; }

        float Jump()
        {
            float j = 0f;
            for (int i = 0; i < _rig.parts.Length; i++) j = Mathf.Max(j, (_rig.parts[i].localPosition - _prev[i]).magnitude);
            return j;
        }

        void CheckFormula()
        {
            var s = _c.locomotion.Input;
            foreach (var name in _c.locomotion.PartNames)
            {
                int i = _rig.IndexOf(name);
                AlphaSpace.Decompose(AlphaSpace.ToUnity(AlphaParamWalk.Joint(_c.locomotion.Spec.@params, s, name)), out var p, out var r, out _);
                _maxPosErr = Mathf.Max(_maxPosErr, (_rig.parts[i].localPosition - p).magnitude);
                _maxRotErr = Mathf.Max(_maxRotErr, Quaternion.Angle(_rig.parts[i].localRotation, r));
            }
        }

        void CheckClip()
        {
            var clip = _c.actions.Current;
            var expect = new PartPose[clip.PartCount];
            clip.Sample(_c.actions.Cursor, expect);
            for (int p = 0; p < clip.PartCount; p++)
            {
                var t = _rig.parts[_rig.IndexOf(clip.Data.parts[p])];
                _maxPosErr = Mathf.Max(_maxPosErr, (t.localPosition - expect[p].position).magnitude);
                _maxRotErr = Mathf.Max(_maxRotErr, Quaternion.Angle(t.localRotation, expect[p].rotation));
            }
        }

        void LateUpdate()
        {
            if (Finished) return;
            _frame++;
            var loco = _c.locomotion;
            switch (_phase)
            {
                case 0:   // idle
                    CheckFormula();
                    if (_frame >= idleFrames) { _phase = 1; _log.Add($"move@{_frame}"); }
                    break;
                case 1:   // move: amplitude ramps up and the creature walks
                    CheckFormula();
                    loco.amplitude = Mathf.Min(0.19f, loco.amplitude + 0.01f);
                    if (_frame >= idleFrames + moveFrames) { _phase = 2; _log.Add($"stop@{_frame}"); }
                    break;
                case 2:   // stop: amplitude back to 0 (standing)
                    CheckFormula();
                    loco.amplitude = Mathf.Max(0f, loco.amplitude - 0.01f);
                    if (_frame >= idleFrames + moveFrames + stopFrames && loco.amplitude <= 0f)
                    {
                        Snapshot();
                        if (!_c.PlayAction(actionClip)) { _problems.AddRange(_c.Problems); Finish(); return; }
                        _jumpIn = Jump();                     // standing formula pose -> clip key 0
                        _phase = 3; _log.Add($"action@{_frame}");
                    }
                    break;
                case 3:   // action + recovery (clip)
                    if (_c.InAction)
                    {
                        CheckClip();
                        _maxActionMove = Mathf.Max(_maxActionMove, Jump());
                        Snapshot();
                        if (++_actionFrames > maxActionFrames) { _problems.Add("ACTION_NOT_FINISHED"); Finish(); return; }
                    }
                    else
                    {
                        _jumpOut = Jump();                    // clip last key -> standing formula pose
                        _phase = 4; _log.Add($"idle@{_frame}");
                    }
                    break;
                default:  // idle again
                    CheckFormula();
                    if (++_after >= afterFrames) Finish();
                    break;
            }
        }

        void Finish()
        {
            if (_phase < 4 && _problems.Count == 0) _problems.Add($"SEQUENCE_INCOMPLETE phase={_phase}");
            if (_maxPosErr > 1e-3f || _maxRotErr > 0.05f) _problems.Add($"RUNTIME_POSE_MISMATCH pos={_maxPosErr} rot={_maxRotErr}");
            if (_jumpIn > maxHandoverJumpVox) _problems.Add($"HANDOVER_IN_JUMP {_jumpIn:0.0000}vox");
            if (_jumpOut > maxHandoverJumpVox) _problems.Add($"HANDOVER_OUT_JUMP {_jumpOut:0.0000}vox");
            if (_phase >= 4 && _maxActionMove <= 0f) _problems.Add("ACTION_STATIC");
            Verdict = _problems.Count == 0 ? "PASS" : "FAIL";
            var sb = new StringBuilder();
            sb.AppendLine($"CONTROLLER_PLAYMODE_RESULT={Verdict}");
            sb.AppendLine($"sequence=idle,move,stop,{actionClip},recovery,idle events=[{string.Join(",", _log)}] frames={_frame} simulated_dt=1/60");
            sb.AppendLine($"action_frames={_actionFrames} max_part_step_in_action_vox={_maxActionMove:0.000}");
            sb.AppendLine($"handover_in_jump_vox={_jumpIn:0.000000} handover_out_jump_vox={_jumpOut:0.000000} limit={maxHandoverJumpVox}");
            sb.AppendLine($"runtime_pose_max_pos_err={_maxPosErr:0.000000} runtime_pose_max_rot_err_deg={_maxRotErr:0.0000}");
            sb.AppendLine($"problems={_problems.Count}");
            foreach (var p in _problems) sb.AppendLine("  - " + p);
            var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "TransferOutput"));
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "controller_playmode.txt"), sb.ToString());
            Debug.Log(sb.ToString());
            Finished = true;
            Time.captureDeltaTime = 0f;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}
