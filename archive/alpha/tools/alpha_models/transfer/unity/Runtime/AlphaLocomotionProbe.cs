// AlphaLocomotionProbe.cs — PlayMode check of the parametric walk (AlphaLocomotionDriver) in real
// frames. Every frame the rig must show exactly the formula pose of the driver's state (runtime
// path == AlphaParamWalk.Joint), the walk must move, and three scenarios run back to back:
//   1. steady walk at the captured amplitude              (walkFrames)
//   2. amplitude sweep 0.05 -> 0.19 (speed change without new clips — impossible with one clip)
//   3. boost on for flags-bit0 = 0 creatures: head lean follows 1 - 0.99^dt_ms toward +10 deg
// Report: <project>/TransferOutput/locomotion_playmode.txt (LOCOMOTION_PLAYMODE_RESULT=PASS|FAIL).
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace CubeGame.AlphaTransfer
{
    public class AlphaLocomotionProbe : MonoBehaviour
    {
        public int walkFrames = 90, sweepFrames = 60, boostFrames = 60;
        public static bool Finished;
        public static string Verdict = "NOT_RUN";

        AlphaLocomotionDriver _driver; AlphaRig _rig;
        int _frame;
        float _maxPosErr, _maxRotErr, _phaseStart, _leanEnd, _leanExpectedEnd, amplitude0;
        readonly List<string> _problems = new List<string>();
        Vector3[] _min, _max; Quaternion[] _rot0; float[] _rotRange;
        bool _leanApplies;

        void Start()
        {
            Finished = false; Verdict = "RUNNING";
            Time.captureDeltaTime = 1f / 60f;
            _driver = GetComponent<AlphaLocomotionDriver>(); _rig = GetComponent<AlphaRig>();
            if (_driver == null) { _problems.Add("NO_DRIVER"); Finish(); return; }
            _driver.Load();
            _problems.AddRange(_driver.Problems);
            if (_problems.Count > 0) { Finish(); return; }
            _driver.followAmplitude = false;
            amplitude0 = _driver.amplitude;
            _driver.boost = false; _driver.accelerating = false; _driver.headLeanDeg = 0f;
            _phaseStart = _driver.phase;
            _leanApplies = (_driver.Spec.@params.flags & 1) == 0 && (_rig.IndexOf("head") >= 0 || _rig.IndexOf("hair") >= 0);
            int n = _rig.parts.Length;
            _min = new Vector3[n]; _max = new Vector3[n]; _rot0 = new Quaternion[n]; _rotRange = new float[n];
            for (int i = 0; i < n; i++) { _min[i] = _max[i] = _rig.parts[i].localPosition; _rot0[i] = _rig.parts[i].localRotation; }
        }

        void LateUpdate()
        {
            if (Finished) return;
            _frame++;
            // runtime pose == formula pose of the driver's current state
            var s = _driver.Input;
            foreach (var name in _driver.PartNames)
            {
                int i = _rig.IndexOf(name);
                var u = AlphaSpace.ToUnity(_driver.Joint(name));
                AlphaSpace.Decompose(u, out var pos, out var rot, out _);
                _maxPosErr = Mathf.Max(_maxPosErr, (_rig.parts[i].localPosition - pos).magnitude);
                _maxRotErr = Mathf.Max(_maxRotErr, Quaternion.Angle(_rig.parts[i].localRotation, rot));
                var lp = _rig.parts[i].localPosition;
                _min[i] = Vector3.Min(_min[i], lp); _max[i] = Vector3.Max(_max[i], lp);
                _rotRange[i] = Mathf.Max(_rotRange[i], Quaternion.Angle(_rot0[i], _rig.parts[i].localRotation));
            }
            if (_frame <= walkFrames) return;
            if (_frame <= walkFrames + sweepFrames)
            {
                float u = (_frame - walkFrames) / (float)sweepFrames;
                _driver.amplitude = Mathf.Lerp(0.05f, 0.19f, u);
                return;
            }
            // boost switches on after this frame's driver step; the driver's lean starts moving next frame
            if (_frame == walkFrames + sweepFrames + 1) { _driver.boost = true; _leanExpectedEnd = _leanEnd = _driver.headLeanDeg; return; }
            if (_frame <= walkFrames + sweepFrames + boostFrames)
            {
                _leanExpectedEnd = AlphaParamWalk.LeanStep(_leanExpectedEnd,
                    AlphaParamWalk.HeadLeanTarget(_driver.Spec.@params, true, false), Time.deltaTime * 1000f);
                _leanEnd = _driver.headLeanDeg;
                return;
            }
            Finish();
        }

        void Finish()
        {
            var log = new StringBuilder();
            float bestRot = 0f, bestMove = 0f; string bestPart = "-";
            if (_rotRange != null)
                for (int i = 0; i < _rotRange.Length; i++)
                {
                    if (_rotRange[i] > bestRot) { bestRot = _rotRange[i]; bestPart = _rig.partNames[i]; }
                    bestMove = Mathf.Max(bestMove, (_max[i] - _min[i]).magnitude);
                }
            if (_frame < walkFrames + sweepFrames + boostFrames && _problems.Count == 0) _problems.Add($"FRAMES {_frame}");
            // D65: the frozen-walk rule compares the runtime with what the measured data promises. The
            // formula of a head-only creature barely moves in Alpha itself (0.6 voxel): that is a faithful
            // result and is reported, not failed; a formula that moves but a rig that does not stays a FAIL.
            float expRot = 0f, expMove = 0f;
            if (_driver != null && _driver.Spec != null)
                // the steady-walk amplitude (the sweep ends at 0.19, the boost stage does not change it)
                AlphaParamWalk.ExpectedMotion(_driver.Spec.@params, amplitude0,
                    new List<string>(_driver.PartNames).ToArray(), out expRot, out expMove);
            bool formulaStatic = expRot < 10f && expMove < 1f;
            if (formulaStatic)
                log.AppendLine($"WALK_STATIC_BY_FORMULA formula_rot_deg={expRot:0.0} formula_move_vox={expMove:0.00} (Alpha's own walk is nearly static)");
            else if (bestRot < 10f && bestMove < 1f)
                _problems.Add($"WALK_STATIC max_part_rot={bestRot:0.0} max_part_move={bestMove:0.00} formula_rot={expRot:0.0} formula_move={expMove:0.00}");
            if (_maxPosErr > 1e-3f || _maxRotErr > 0.05f) _problems.Add($"RUNTIME_POSE_MISMATCH pos={_maxPosErr} rot={_maxRotErr}");
            float travel = _driver != null ? _driver.phase - _phaseStart : 0f;
            if (travel <= 0f) _problems.Add("PHASE_NOT_ADVANCING");
            if (_leanApplies && Mathf.Abs(_leanEnd - _leanExpectedEnd) > 1e-3f) _problems.Add($"HEAD_LEAN_SMOOTHING {_leanEnd} != {_leanExpectedEnd}");
            if (_leanApplies && _leanEnd < 5f) _problems.Add($"HEAD_LEAN_NOT_RISING {_leanEnd}");
            Verdict = _problems.Count == 0 ? "PASS" : "FAIL";
            log.AppendLine($"LOCOMOTION_PLAYMODE_RESULT={Verdict}");
            log.AppendLine($"frames={_frame} simulated_dt=1/60 rig={_rig.rigId} spec_status={_driver?.Spec?.status}");
            log.AppendLine($"steady_walk_frames={walkFrames} amp_sweep_frames={sweepFrames} (0.05->0.19) boost_frames={boostFrames}");
            log.AppendLine($"phase_travel_rad={travel:0.00} max_part_rot_deg={bestRot:0.0} ({bestPart}) max_part_move_vox={bestMove:0.00}");
            log.AppendLine($"head_lean_applies={_leanApplies} head_lean_end_deg={_leanEnd:0.0000} expected={_leanExpectedEnd:0.0000}");
            log.AppendLine($"runtime_pose_max_pos_err={_maxPosErr:0.000000} runtime_pose_max_rot_err_deg={_maxRotErr:0.0000}");
            log.AppendLine($"problems={_problems.Count}");
            foreach (var p in _problems) log.AppendLine("  - " + p);
            var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "TransferOutput"));
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "locomotion_playmode.txt"), log.ToString());
            Debug.Log(log.ToString());
            Finished = true;
            Time.captureDeltaTime = 0f;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}
