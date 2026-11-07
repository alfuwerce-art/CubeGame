// AlphaEntityProbe.cs — PlayMode check of ANY entity through the one AlphaAnimationController, driven
// by its entity contract (Data/contract.json). No creature name, part name or limb count is used.
// Scenario in real PlayMode frames at a fixed step (Time.captureDeltaTime = 1/fps):
//   walk   formula locomotion at the steady amplitude until the phase covered >= one full cycle
//   stop   amplitude to 0 at the controller's stop rate (UNITY_TRANSITION / PLACEHOLDER_NOT_ALPHA)
//   idle   standing pose must be stable
//   action ACTIONS mode: the first required action runs entry -> action -> recovery -> exit -> idle;
//          LOCOMOTION_ONLY: every action request is refused, nothing breaks, the rig stays in locomotion
// Every locomotion frame: rig transforms == formula pose (<= 1e-3 vox, <= 0.05 deg), all parts finite.
// Report: TransferOutput/entity_playmode.txt (ENTITY_PLAYMODE_RESULT=PASS|FAIL), optional frames.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace CubeGame.AlphaTransfer
{
    public class AlphaEntityProbe : MonoBehaviour
    {
        public int fps = 60;
        public string session = "";
        public bool capture = true;
        public float walkAmplitude = 0.19f;
        public float idleSeconds = 0.5f;
        public string refusedProbeAction = "action_probe_not_in_library";
        public static bool Finished;
        public static string Verdict = "NOT_RUN";

        AlphaAnimationController _c; AlphaLocomotionDriver _d; AlphaRig _r; AlphaEntityContract _contract;
        readonly List<string> _problems = new List<string>();
        readonly List<string> _shots = new List<string>();
        int _frame, _stage, _stageFrames;
        float _phaseStart, _maxPos, _maxRot, _idleDrift, _walkRot, _walkMove, _actionSeconds;
        Vector3[] _min, _max, _idlePos; Quaternion[] _rot0, _idleRot;
        string _action; bool _actionStarted, _refused;
        string[] _stageNames = { "walk", "stop", "idle", "action", "after" };

        void Start()
        {
            Finished = false; Verdict = "RUNNING";
            Time.captureDeltaTime = 1f / fps;
            _c = GetComponent<AlphaAnimationController>(); _d = GetComponent<AlphaLocomotionDriver>(); _r = GetComponent<AlphaRig>();
            var path = Path.Combine(Application.dataPath, "AlphaTransfer", "Data", "contract.json");
            if (!File.Exists(path)) { _problems.Add("CONTRACT_MISSING"); Finish(); return; }
            _contract = AlphaEntityContract.Parse(File.ReadAllText(path), _problems);
            if (_c == null || _d == null) _problems.Add("CONTROLLER_OR_DRIVER_MISSING");
            if (_problems.Count > 0) { Finish(); return; }
            _c.externalClock = true;
            _c.locomotionOnly = _contract.LocomotionOnly;
            _c.actionsBlockedReason = _contract.actionsReason;
            if (!_c.Init()) { _problems.AddRange(_c.Problems.Select(p => "CONTROLLER " + p)); Finish(); return; }
            _problems.AddRange(_contract.Validate(_r, _c.actions.ClipNames.Select(n => _c.actions.Get(n)),
                File.Exists(Path.Combine(Application.dataPath, "AlphaTransfer", "Data", "actions.json"))));
            if (_problems.Count > 0) { Finish(); return; }
            _d.followAmplitude = false; _d.boost = false; _d.accelerating = false; _d.headLeanDeg = 0f;
            _d.amplitude = walkAmplitude; _phaseStart = _d.phase;
            int n = _r.parts.Length;
            _min = new Vector3[n]; _max = new Vector3[n]; _rot0 = new Quaternion[n];
            for (int i = 0; i < n; i++) { _min[i] = _max[i] = _r.parts[i].localPosition; _rot0[i] = _r.parts[i].localRotation; }
            if (!_contract.LocomotionOnly) _action = _c.requiredClips.FirstOrDefault(a => a.StartsWith("action_"));
        }

        void Next() { _stage++; _stageFrames = 0; }

        void Update()
        {
            if (Finished || _c == null || !_c.Initialized) return;
            float dt = 1f / fps;
            _frame++; _stageFrames++;
            if (_frame > fps * 120) { _problems.Add("SCENARIO_TIMEOUT stage=" + _stageNames[Mathf.Min(_stage, 4)]); Finish(); return; }
            switch (_stage)
            {
                case 0: // walk
                    _c.Step(dt); CheckFormula(); Track();
                    if (_d.phase - _phaseStart >= 2f * Mathf.PI) Next();
                    break;
                case 1: // stop (controller-owned amplitude ramp)
                    _d.amplitude = Mathf.MoveTowards(_d.amplitude, 0f, _c.stopAmplitudePerSecond * dt);
                    _c.Step(dt); CheckFormula();
                    if (_d.amplitude == 0f && Mathf.Abs(_d.headLeanDeg) < 1e-4f) { Next(); SnapshotIdle(); }
                    break;
                case 2: // idle must be stable
                    _c.Step(dt); CheckFormula(); IdleDrift();
                    if (_stageFrames >= Mathf.CeilToInt(idleSeconds * fps)) Next();
                    break;
                case 3: // action / refusal
                    if (_contract.LocomotionOnly)
                    {
                        _refused = !_c.PlayAction(refusedProbeAction) && !_c.PlayAction("action_057") && !_c.PlayNativeAction("native_057");
                        if (!_refused) _problems.Add("LOCOMOTION_ONLY_ACCEPTED_ACTION");
                        _c.Step(dt);
                        if (_c.Phase != AlphaControllerPhase.Locomotion || _c.Problems.Count > 0)
                            _problems.Add("LOCOMOTION_ONLY_LEFT_LOCOMOTION " + string.Join(";", _c.Problems));
                        Next();
                    }
                    else
                    {
                        if (!_actionStarted)
                        {
                            if (_action == null || !_c.PlayAction(_action)) { _problems.Add("ACTION_NOT_STARTED " + _action); Finish(); return; }
                            _actionStarted = true;
                        }
                        _c.Step(dt); _actionSeconds += dt;
                        if (_c.Problems.Count > 0) { _problems.AddRange(_c.Problems); Finish(); return; }
                        if (!_c.InAction && _c.Phase == AlphaControllerPhase.Locomotion) Next();
                    }
                    break;
                case 4: // back in locomotion: the formula owns the pose again
                    _c.Step(dt); CheckFormula();
                    if (_stageFrames >= 10) { Finish(); return; }
                    break;
            }
            Finite();
            if (capture && (_frame % Mathf.Max(1, fps / 6) == 0 || _stageFrames == 1)) Capture();
        }

        void CheckFormula()
        {
            if (_c.Phase != AlphaControllerPhase.Locomotion) return;
            foreach (var name in _d.PartNames)
            {
                int i = _r.IndexOf(name);
                var u = AlphaSpace.ToUnity(_d.Joint(name));
                AlphaSpace.Decompose(u, out var pos, out var rot, out _);
                _maxPos = Mathf.Max(_maxPos, (_r.parts[i].localPosition - pos).magnitude);
                _maxRot = Mathf.Max(_maxRot, Quaternion.Angle(_r.parts[i].localRotation, rot));
                if (!_r.IsVisible(i)) _problems.Add("LOCOMOTION_PART_HIDDEN " + name);
            }
        }

        void Track()
        {
            for (int i = 0; i < _r.parts.Length; i++)
            {
                var lp = _r.parts[i].localPosition;
                _min[i] = Vector3.Min(_min[i], lp); _max[i] = Vector3.Max(_max[i], lp);
                _walkRot = Mathf.Max(_walkRot, Quaternion.Angle(_rot0[i], _r.parts[i].localRotation));
                _walkMove = Mathf.Max(_walkMove, (_max[i] - _min[i]).magnitude);
            }
        }

        void SnapshotIdle()
        {
            _idlePos = _r.parts.Select(p => p.localPosition).ToArray();
            _idleRot = _r.parts.Select(p => p.localRotation).ToArray();
        }

        void IdleDrift()
        {
            for (int i = 0; i < _r.parts.Length; i++)
                _idleDrift = Mathf.Max(_idleDrift, (_r.parts[i].localPosition - _idlePos[i]).magnitude
                    + Quaternion.Angle(_r.parts[i].localRotation, _idleRot[i]) * 0.01f);
        }

        void Finite()
        {
            foreach (var t in _r.parts)
            {
                var p = t.localPosition; var q = t.localRotation;
                if (!(float.IsFinite(p.x) && float.IsFinite(p.y) && float.IsFinite(p.z) && float.IsFinite(q.w)))
                { _problems.Add("NONFINITE_POSE " + t.name); Finish(); return; }
            }
        }

        void Capture()
        {
            var camera = Camera.main;
            if (camera == null) camera = Object.FindFirstObjectByType<Camera>();
            if (camera == null) { if (!_problems.Contains("CAPTURE_CAMERA_MISSING")) _problems.Add("CAPTURE_CAMERA_MISSING"); return; }
            var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "TransferOutput", "entity_frames"));
            Directory.CreateDirectory(dir);
            var rt = new RenderTexture(360, 360, 24); var previous = RenderTexture.active;
            var previousTarget = camera.targetTexture; var tex = new Texture2D(360, 360, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, 360, 360), 0, 0); tex.Apply();
                string file = $"frame_{_frame:000000}.png"; File.WriteAllBytes(Path.Combine(dir, file), tex.EncodeToPNG());
                _shots.Add($"{file}\t{_stageNames[Mathf.Min(_stage, 4)]}\t{_c.Phase}\t{_c.CurrentAction ?? "locomotion"}\tamp={_d.amplitude:0.000}");
            }
            finally { camera.targetTexture = previousTarget; RenderTexture.active = previous; Destroy(rt); Destroy(tex); }
        }

        void Finish()
        {
            if (Finished) return;
            if (_contract != null && _stage < 4 && !_problems.Any()) _problems.Add("SCENARIO_INCOMPLETE stage=" + _stage);
            if (_maxPos > 1e-3f || _maxRot > .05f) _problems.Add($"RUNTIME_POSE_MISMATCH pos={_maxPos} rot={_maxRot}");
            // D65: compare the runtime with what the data promises (head-only creatures barely move in Alpha
            // itself); a motionless rig while the formula moves stays a failure.
            float expRot = 0f, expMove = 0f;
            if (_d != null && _d.Spec != null)
                // the amplitude of the WALK stage (at Finish the driver's amplitude is already ramped to 0)
                AlphaParamWalk.ExpectedMotion(_d.Spec.@params, walkAmplitude,
                    new List<string>(_d.PartNames).ToArray(), out expRot, out expMove);
            bool formulaStatic = expRot < 10f && expMove < 1f;
            if (_contract != null && _stage >= 1 && !formulaStatic && _walkRot < 10f && _walkMove < 1f)
                _problems.Add($"WALK_STATIC rot={_walkRot} move={_walkMove} formula_rot={expRot:0.0} formula_move={expMove:0.00}");
            if (_idleDrift > 1e-4f) _problems.Add($"IDLE_NOT_STABLE {_idleDrift}");
            Verdict = _problems.Count == 0 ? "PASS" : "FAIL";
            var s = new StringBuilder();
            s.AppendLine($"ENTITY_PLAYMODE_RESULT={Verdict}"); s.AppendLine("session=" + session);
            if (_contract != null)
            {
                s.AppendLine($"kind={_contract.kind} category={_contract.category} body_plan={_contract.bodyPlan} rig={_contract.rig} parts={_r.parts.Length}");
                s.AppendLine($"controller_mode={_contract.controllerMode} action_profile={_contract.actionProfile} trigger={_contract.actionTrigger}");
                s.AppendLine($"actions_status={_contract.actionsStatus} reason={_contract.actionsReason}");
                s.AppendLine($"walk_sources={string.Join("+", _contract.walkSources ?? new string[0])} idle_sources={string.Join("+", _contract.idleSources ?? new string[0])} transitions={_contract.transitions}");
            }
            s.AppendLine($"frames={_frame} simulated_fps={fps} phase_travel_rad={(_d != null ? _d.phase - _phaseStart : 0):0.000}");
            s.AppendLine($"walk_max_part_rot_deg={_walkRot:0.00} walk_max_part_move_vox={_walkMove:0.00} idle_drift={_idleDrift:0.000000}");
            if (formulaStatic && _stage >= 1)
                s.AppendLine($"WALK_STATIC_BY_FORMULA formula_rot_deg={expRot:0.0} formula_move_vox={expMove:0.00} (Alpha's own walk is nearly static)");
            s.AppendLine($"runtime_formula_pose_max_pos_err_vox={_maxPos:0.000000} rot_err_deg={_maxRot:0.000000}");
            s.AppendLine($"action={_action ?? "-"} action_seconds={_actionSeconds:0.000} locomotion_only_refused={_refused}");
            s.AppendLine("events=" + (_c == null ? "" : string.Join(",", _c.Events)));
            s.AppendLine("frames_captured=" + _shots.Count);
            s.AppendLine("problems=" + _problems.Count); foreach (var p in _problems) s.AppendLine("  - " + p);
            var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "TransferOutput")); Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "entity_playmode.txt"), s.ToString());
            if (capture) File.WriteAllLines(Path.Combine(dir, "entity_frames.tsv"), _shots);
            Debug.Log(s.ToString());
            Finished = true; Time.captureDeltaTime = 0;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}
