// AlphaPlayModeProbe.cs — runs in real PlayMode frames: walk -> attack (to the end) -> idle, and
// checks every frame that the rig shows exactly the clip pose for the player's cursor (the runtime
// path), that the walk moves the feet, and that the attack runs windup -> strike -> hold.
// Report: <project>/TransferOutput/playmode.txt|json (written by THIS component only).
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace CubeGame.AlphaTransfer
{
    public class AlphaPlayModeProbe : MonoBehaviour
    {
        public string walkClip = "walk";
        public string attackClip = "attack_2h_a57";
        public string idleClip = "idle";
        public int walkFrames = 90;
        public int maxAttackFrames = 600;
        public static bool Finished;
        public static string Verdict = "NOT_RUN";

        AlphaClipPlayer _player; AlphaRig _rig;
        int _phase, _frames, _total;
        readonly List<string> _problems = new List<string>();
        readonly StringBuilder _log = new StringBuilder();
        float _maxPosErr, _maxRotErr;
        float _footMin = float.MaxValue, _footMax = float.MinValue, _walkPhaseTravel;
        Vector3 _prevTip; bool _haveTip; float _tipTravel, _tipMaxStep;
        float _attackCursorEnd; int _attackFrames, _walkFramesDone, _idleFrames;
        float _t0;

        void Start()
        {
            Finished = false; Verdict = "RUNNING";
            // Batch PlayMode runs uncapped (2000+ fps), which would leave the clips almost
            // unplayed. Fix the simulated frame step to 1/60 s: Time.deltaTime == 1/60 every frame.
            Time.captureDeltaTime = 1f / 60f;
            _player = GetComponent<AlphaClipPlayer>(); _rig = GetComponent<AlphaRig>();
            _player.LoadClips();
            _problems.AddRange(_player.Problems);
            foreach (var n in new[] { walkClip, attackClip, idleClip })
                if (_player.Get(n) == null) _problems.Add($"CLIP_MISSING {n}");
            _t0 = Time.realtimeSinceStartup;
            if (_problems.Count > 0) { Finish(); return; }
            _player.Play(walkClip);
        }

        void LateUpdate()
        {
            if (Finished) return;
            _total++;
            var clip = _player.Current;
            CheckPose(clip);
            if (_phase == 0)
            {
                int fl = _rig.IndexOf("foot_l");
                float z = _rig.parts[fl].localPosition.z;
                _footMin = Mathf.Min(_footMin, z); _footMax = Mathf.Max(_footMax, z);
                _walkPhaseTravel = _player.Cursor;
                if (++_walkFramesDone >= walkFrames) { _phase = 1; _player.Play(attackClip); }
            }
            else if (_phase == 1)
            {
                _attackFrames++;
                int w = _rig.IndexOf("weapon");
                var tip = _rig.meshes[w].TransformPoint(AlphaSpace.GridToUnity(new Vector3(_rig.sizes[w].x * 0.5f, _rig.sizes[w].y * 0.5f, _rig.sizes[w].z))) / AlphaTransferConsts.RootScale;
                if (_haveTip) { float step = (tip - _prevTip).magnitude; _tipTravel += step; _tipMaxStep = Mathf.Max(_tipMaxStep, step); }
                _prevTip = tip; _haveTip = true;
                _attackCursorEnd = _player.Cursor;
                if (_player.Cursor >= clip.Data.duration || _attackFrames >= maxAttackFrames) { _phase = 2; _player.Play(idleClip); }
            }
            else
            {
                if (++_idleFrames >= 5) Finish();
            }
        }

        void CheckPose(AlphaClip clip)
        {
            var expect = new PartPose[clip.PartCount];
            clip.Sample(_player.Cursor, expect);
            for (int p = 0; p < clip.PartCount; p++)
            {
                var t = _rig.parts[_rig.IndexOf(clip.Data.parts[p])];
                _maxPosErr = Mathf.Max(_maxPosErr, (t.localPosition - expect[p].position).magnitude);
                _maxRotErr = Mathf.Max(_maxRotErr, Quaternion.Angle(t.localRotation, expect[p].rotation));
            }
        }

        void Finish()
        {
            float secs = Time.realtimeSinceStartup - _t0;
            var attack = _player.Get(attackClip);
            float footSwing = _footMax - _footMin;
            if (_walkFramesDone < walkFrames) _problems.Add($"WALK_FRAMES {_walkFramesDone}<{walkFrames}");
            if (footSwing < 4f) _problems.Add($"WALK_FEET_STATIC swing={footSwing:0.00}vox");
            if (attack != null && _attackCursorEnd < attack.Data.duration) _problems.Add($"ATTACK_NOT_COMPLETED cursor={_attackCursorEnd:0} < {attack.Data.duration:0}");
            if (_tipTravel < 30f) _problems.Add($"ATTACK_BLADE_STATIC tipTravel={_tipTravel:0.0}vox");
            if (_maxPosErr > 1e-3f || _maxRotErr > 0.05f) _problems.Add($"RUNTIME_POSE_MISMATCH pos={_maxPosErr} rot={_maxRotErr}");
            if (_idleFrames < 5 && _problems.Count == 0) _problems.Add("IDLE_NOT_REACHED");
            Verdict = _problems.Count == 0 ? "PASS" : "FAIL";
            _log.AppendLine($"PLAYMODE_RESULT={Verdict}");
            _log.AppendLine($"frames_total={_total} simulated_dt=1/60 real_seconds={secs:0.00}");
            _log.AppendLine($"walk_frames={_walkFramesDone} walk_phase_travel_rad={_walkPhaseTravel:0.00} foot_l_swing_vox={footSwing:0.00}");
            _log.AppendLine($"attack_frames={_attackFrames} attack_cursor_end_ms={_attackCursorEnd:0} attack_duration_ms={(attack != null ? attack.Data.duration : -1):0} blade_tip_travel_vox={_tipTravel:0.0} blade_tip_max_step_vox={_tipMaxStep:0.00}");
            _log.AppendLine($"idle_frames={_idleFrames}");
            _log.AppendLine($"runtime_pose_max_pos_err={_maxPosErr:0.000000} runtime_pose_max_rot_err_deg={_maxRotErr:0.0000}");
            _log.AppendLine($"problems={_problems.Count}");
            foreach (var p in _problems) _log.AppendLine("  - " + p);
            var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "TransferOutput"));
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "playmode.txt"), _log.ToString());
            Debug.Log(_log.ToString());
            Finished = true;
            Time.captureDeltaTime = 0f;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }

    public static class AlphaTransferConsts
    {
        public const float RootScale = 0.1f;
    }
}
