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
        bool _humanoid;
        // universal rigs: per-part motion range over the real walk frames (anatomy-neutral)
        Vector3[] _posMin, _posMax; Quaternion[] _rot0; float[] _rotRange;
        readonly List<string> _notAvailable = new List<string>();

        void Start()
        {
            Finished = false; Verdict = "RUNNING";
            // Batch PlayMode runs uncapped (2000+ fps), which would leave the clips almost
            // unplayed. Fix the simulated frame step to 1/60 s: Time.deltaTime == 1/60 every frame.
            Time.captureDeltaTime = 1f / 60f;
            _player = GetComponent<AlphaClipPlayer>(); _rig = GetComponent<AlphaRig>();
            _player.LoadClips();
            _problems.AddRange(_player.Problems);
            // The humanoid rig keeps the full S05A-2 contract (walk + attack + idle are mandatory).
            // A universal creature rig must have a walk; attack/idle are run only if they exist
            // (absent ones are reported as NOT_AVAILABLE, never treated as played).
            _humanoid = _rig.rigId == AlphaRig.RigId;
            foreach (var n in new[] { walkClip, attackClip, idleClip })
                if (_player.Get(n) == null)
                {
                    if (_humanoid || n == walkClip) _problems.Add($"CLIP_MISSING {n}");
                    else _notAvailable.Add(n);
                }
            if (_notAvailable.Contains(attackClip)) attackClip = null;
            if (_notAvailable.Contains(idleClip)) idleClip = null;
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
                if (fl >= 0)
                {
                    float z = _rig.parts[fl].localPosition.z;
                    _footMin = Mathf.Min(_footMin, z); _footMax = Mathf.Max(_footMax, z);
                }
                TrackMotion();
                _walkPhaseTravel = _player.Cursor;
                if (++_walkFramesDone >= walkFrames)
                {
                    if (attackClip != null) { _phase = 1; _player.Play(attackClip); }
                    else if (idleClip != null) { _phase = 2; _player.Play(idleClip); }
                    else Finish();
                }
            }
            else if (_phase == 1)
            {
                _attackFrames++;
                int w = _rig.IndexOf("weapon");
                var tip = _rig.meshes[w].TransformPoint(AlphaSpace.GridToUnity(new Vector3(_rig.sizes[w].x * 0.5f, _rig.sizes[w].y * 0.5f, _rig.sizes[w].z))) / AlphaTransferConsts.RootScale;
                if (_haveTip) { float step = (tip - _prevTip).magnitude; _tipTravel += step; _tipMaxStep = Mathf.Max(_tipMaxStep, step); }
                _prevTip = tip; _haveTip = true;
                _attackCursorEnd = _player.Cursor;
                if (_player.Cursor >= clip.Data.duration || _attackFrames >= maxAttackFrames)
                {
                    if (idleClip != null) { _phase = 2; _player.Play(idleClip); } else Finish();
                }
            }
            else
            {
                if (++_idleFrames >= 5) Finish();
            }
        }

        void TrackMotion()
        {
            int n = _rig.parts.Length;
            if (_posMin == null)
            {
                _posMin = new Vector3[n]; _posMax = new Vector3[n]; _rot0 = new Quaternion[n]; _rotRange = new float[n];
                for (int i = 0; i < n; i++) { _posMin[i] = _posMax[i] = _rig.parts[i].localPosition; _rot0[i] = _rig.parts[i].localRotation; }
            }
            for (int i = 0; i < n; i++)
            {
                var p = _rig.parts[i].localPosition;
                _posMin[i] = Vector3.Min(_posMin[i], p); _posMax[i] = Vector3.Max(_posMax[i], p);
                _rotRange[i] = Mathf.Max(_rotRange[i], Quaternion.Angle(_rot0[i], _rig.parts[i].localRotation));
            }
        }

        /// <summary>D65: motion the clip itself promises (max part displacement in voxels, rotation in
        /// degrees over its keys) — sampled from the clip, so it holds for TRANSFERRED clips as well.</summary>
        static void ClipMotion(AlphaClip clip, out float maxRotDeg, out float maxMoveVox)
        {
            maxRotDeg = 0f; maxMoveVox = 0f;
            int n = 32, parts = clip.PartCount;
            if (parts == 0 || !(clip.Data.duration > 0f)) return;
            var poses = new PartPose[parts];
            var firstPos = new Vector3[parts]; var firstRot = new Quaternion[parts];
            for (int i = 0; i <= n; i++)
            {
                clip.Sample(clip.Data.duration * i / n, poses);
                for (int p = 0; p < parts; p++)
                {
                    if (i == 0) { firstPos[p] = poses[p].position; firstRot[p] = poses[p].rotation; continue; }
                    maxMoveVox = Mathf.Max(maxMoveVox, (poses[p].position - firstPos[p]).magnitude);
                    maxRotDeg = Mathf.Max(maxRotDeg, Quaternion.Angle(firstRot[p], poses[p].rotation));
                }
            }
        }

        void CheckPose(AlphaClip clip)
        {
            var expect = new PartPose[clip.PartCount];
            clip.Sample(_player.Cursor, expect);
            var visible=new bool[clip.PartCount];clip.SampleVisibility(_player.Cursor,visible);
            for (int p = 0; p < clip.PartCount; p++)
            {
                int r=_rig.IndexOf(clip.Data.parts[p]);
                if(_rig.IsVisible(r)!=visible[p])_problems.Add("VISIBILITY_MISMATCH "+clip.Data.parts[p]);
                if(!visible[p])continue;
                var t = _rig.parts[r];
                _maxPosErr = Mathf.Max(_maxPosErr, (t.localPosition - expect[p].position).magnitude);
                _maxRotErr = Mathf.Max(_maxRotErr, Quaternion.Angle(t.localRotation, expect[p].rotation));
            }
        }

        void Finish()
        {
            float secs = Time.realtimeSinceStartup - _t0;
            var attack = attackClip != null ? _player.Get(attackClip) : null;
            float footSwing = _footMax - _footMin;
            if (_walkFramesDone < walkFrames) _problems.Add($"WALK_FRAMES {_walkFramesDone}<{walkFrames}");
            // Humanoid contract (S05A-2, unchanged): the left foot must swing >= 4 voxels forward.
            // Universal creature rigs: anatomy differs (a runner's tall foot mostly ROTATES, 0.8 vox
            // forward in Alpha's own data), so the check is "the walk is not frozen": in real
            // PlayMode frames some part turns >= 10 deg or moves >= 1 voxel (the runtime pose is
            // separately required to equal the clip pose at every frame, see CheckPose).
            float bestRot = 0f, bestMove = 0f; string bestPart = "-";
            if (_posMin != null)
                for (int i = 0; i < _rotRange.Length; i++)
                {
                    float mv = (_posMax[i] - _posMin[i]).magnitude;   // part localPosition is in voxels (root carries the 0.1 scale)
                    if (_rotRange[i] > bestRot) { bestRot = _rotRange[i]; bestPart = _rig.partNames[i]; }
                    bestMove = Mathf.Max(bestMove, mv);
                }
            if (_humanoid) { if (footSwing < 4f) _problems.Add($"WALK_FEET_STATIC swing={footSwing:0.00}vox"); }
            else
            {
                // D65: the frozen-walk rule compares the RUNTIME with what the played clip itself promises.
                // A clip whose own poses barely move (Alpha's walk of head-only creatures: 0.6 voxel) is a
                // faithful transfer, reported as WALK_STATIC_BY_CLIP instead of failing; a clip that moves
                // while the rig does not stays a failure.
                var own = _player.Get(walkClip);
                float clipRot = 0f, clipMove = 0f;
                if (own != null) ClipMotion(own, out clipRot, out clipMove);
                bool clipStatic = clipRot < 10f && clipMove < 1f;
                if (clipStatic) _log.AppendLine($"WALK_STATIC_BY_CLIP clip_rot_deg={clipRot:0.0} clip_move_vox={clipMove:0.00} (Alpha's own walk is nearly static)");
                else if (bestRot < 10f && bestMove < 1f) _problems.Add($"WALK_STATIC max_part_rot={bestRot:0.0}deg max_part_move={bestMove:0.00}vox clip_rot={clipRot:0.0} clip_move={clipMove:0.00}");
            }
            if (attack != null && _attackCursorEnd < attack.Data.duration) _problems.Add($"ATTACK_NOT_COMPLETED cursor={_attackCursorEnd:0} < {attack.Data.duration:0}");
            if (attackClip != null && _tipTravel < 30f) _problems.Add($"ATTACK_BLADE_STATIC tipTravel={_tipTravel:0.0}vox");
            if (_maxPosErr > 1e-3f || _maxRotErr > 0.05f) _problems.Add($"RUNTIME_POSE_MISMATCH pos={_maxPosErr} rot={_maxRotErr}");
            if (idleClip != null && _idleFrames < 5 && _problems.Count == 0) _problems.Add("IDLE_NOT_REACHED");
            Verdict = _problems.Count == 0 ? "PASS" : "FAIL";
            _log.AppendLine($"PLAYMODE_RESULT={Verdict}");
            _log.AppendLine($"frames_total={_total} simulated_dt=1/60 real_seconds={secs:0.00}");
            _log.AppendLine($"walk_frames={_walkFramesDone} walk_phase_travel_rad={_walkPhaseTravel:0.00} foot_l_swing_vox={(_footMax >= _footMin ? footSwing : 0f):0.00} max_part_rot_deg={bestRot:0.0} ({bestPart}) max_part_move_vox={bestMove:0.00}");
            _log.AppendLine($"attack_frames={_attackFrames} attack_cursor_end_ms={_attackCursorEnd:0} attack_duration_ms={(attack != null ? attack.Data.duration : -1):0} blade_tip_travel_vox={_tipTravel:0.0} blade_tip_max_step_vox={_tipMaxStep:0.00}");
            _log.AppendLine($"idle_frames={_idleFrames}");
            _log.AppendLine($"rig={_rig.rigId} humanoid_contract={_humanoid} not_available=[{string.Join(",", _notAvailable)}]");
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
