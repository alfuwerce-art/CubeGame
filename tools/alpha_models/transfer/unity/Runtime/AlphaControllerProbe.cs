// Real PlayMode sequence for EVERY required action. Independent expected sampling checks both
// raw Alpha poses and explicit Unity crossfades; missing/static clips or skipped phases fail.
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace CubeGame.AlphaTransfer
{
    [DefaultExecutionOrder(1000)]
    public class AlphaControllerProbe : MonoBehaviour
    {
        public string actionClip; // compatibility; the required registry takes precedence
        public string session;
        public int fps = 60;
        public bool captureFirstAction;
        public int idleFrames = 20, moveFrames = 110, afterFrames = 20;
        public float maxHandoverJumpVox = .05f;
        public static bool Finished;
        public static string Verdict = "NOT_RUN";
        AlphaAnimationController _c;
        AlphaRig _rig;
        string[] _names;
        int _frame, _stage, _n, _stageFrames, _done;
        float _maxPos, _maxRot, _maxScale, _jump, _jumpRot, _motion, _motionRot, _phaseStart, _phaseTravel;
        float _mainGrip,_secondRadial,_outsideHandle,_bladeTravel;
        Vector3 _lastTip;bool _hasTip;
        bool _entered, _acted, _recovered, _exited, _busyChecked;
        AlphaControllerPhase _lastPhase;
        PartPose[] _prev, _firstAction;
        readonly List<string> _problems = new List<string>();
        readonly List<string> _results = new List<string>();
        readonly List<string> _captureLog = new List<string>();

        void Start()
        {
            Finished = false; Verdict = "RUNNING";
            Time.captureDeltaTime = 1f / fps;
            idleFrames = Mathf.CeilToInt(idleFrames*fps/60f);
            moveFrames = Mathf.CeilToInt(moveFrames*fps/60f);
            afterFrames = Mathf.CeilToInt(afterFrames*fps/60f);
            _c = GetComponent<AlphaAnimationController>(); _rig = GetComponent<AlphaRig>();
            if (_c == null) { _problems.Add("NO_CONTROLLER"); Finish(); return; }
            _c.locomotion = GetComponent<AlphaLocomotionDriver>();
            _c.locomotion.followAmplitude = false; _c.locomotion.amplitude = 0;
            if (!_c.Init()) { _problems.AddRange(_c.Problems); Finish(); return; }
            _c.locomotion.Apply();
            _names = _c.requiredClips;
            if (_names.Length == 0) { _problems.Add("NO_REQUIRED_REGISTRY"); Finish(); return; }
            _prev = Snapshot(); _lastPhase = _c.Phase;
        }

        PartPose[] Snapshot()
        {
            var p = new PartPose[_rig.parts.Length];
            for (int i = 0; i < p.Length; i++) p[i] = new PartPose { position = _rig.parts[i].localPosition,
                rotation = _rig.parts[i].localRotation, scale = _rig.parts[i].localScale };
            return p;
        }

        void Compare(PartPose[] expected)
        {
            for (int i = 0; i < expected.Length; i++)
            {
                var t = _rig.parts[i];
                float p = Vector3.Distance(t.localPosition, expected[i].position);
                float r = Quaternion.Angle(t.localRotation, expected[i].rotation);
                float s = Vector3.Distance(t.localScale, expected[i].scale);
                if (float.IsNaN(p) || float.IsNaN(r) || float.IsNaN(s)) { _problems.Add("POSE_NONFINITE"); continue; }
                _maxPos = Mathf.Max(_maxPos,p); _maxRot = Mathf.Max(_maxRot,r); _maxScale = Mathf.Max(_maxScale,s);
            }
        }

        void CheckPose()
        {
            var expected = new PartPose[_rig.parts.Length];
            bool formula = _c.Phase == AlphaControllerPhase.Locomotion || _c.Phase == AlphaControllerPhase.Stopping
                || _c.Phase == AlphaControllerPhase.ExitingAction;
            if (formula)
            {
                foreach (var name in _c.locomotion.PartNames)
                {
                    int i = _rig.IndexOf(name);
                    AlphaSpace.Decompose(AlphaSpace.ToUnity(_c.locomotion.Joint(name)), out var p,out var r,out var s);
                    expected[i] = new PartPose { position=p,rotation=r,scale=s };
                }
            }
            else
            {
                var clip = _c.actions.Current; var source = new PartPose[clip.PartCount];
                clip.Sample(_c.actions.Cursor, source);
                for (int i = 0; i < source.Length; i++) expected[_rig.IndexOf(clip.Data.parts[i])] = source[i];
            }
            if (_c.IsBlending)
                for (int i = 0; i < expected.Length; i++)
                {
                    var a = _c.BlendFrom[i]; var b = expected[i]; float u = _c.BlendWeight;
                    expected[i] = new PartPose { position=Vector3.Lerp(a.position,b.position,u),
                        rotation=Quaternion.Slerp(a.rotation,b.rotation,u), scale=Vector3.Lerp(a.scale,b.scale,u) };
                }
            Compare(expected);
        }

        void LateUpdate()
        {
            if (Finished) return;
            _frame++; _stageFrames++;
            if (_c.Problems.Count > 0) { _problems.AddRange(_c.Problems); Finish(); return; }
            CheckPose();
            if(captureFirstAction && _n==0 && _frame%4==0) Capture();
            if (_c.Phase == AlphaControllerPhase.EnteringAction) _entered = true;
            if (_c.Phase == AlphaControllerPhase.Recovery) _recovered = true;
            if (_c.Phase == AlphaControllerPhase.ExitingAction) _exited = true;
            if (_c.Phase == AlphaControllerPhase.Recovery && _lastPhase == AlphaControllerPhase.Action)
                for (int i=0;i<_prev.Length;i++)
                {
                    _jump = Mathf.Max(_jump,Vector3.Distance(_prev[i].position,_rig.parts[i].localPosition));
                    _jumpRot = Mathf.Max(_jumpRot,Quaternion.Angle(_prev[i].rotation,_rig.parts[i].localRotation));
                }
            if (_c.Phase == AlphaControllerPhase.Action)
            {
                _acted = true;
                CheckGrip();
                if (_firstAction == null) _firstAction = Snapshot();
                for (int i=0;i<_firstAction.Length;i++)
                {
                    _motion = Mathf.Max(_motion,Vector3.Distance(_firstAction[i].position,_rig.parts[i].localPosition));
                    _motionRot = Mathf.Max(_motionRot,Quaternion.Angle(_firstAction[i].rotation,_rig.parts[i].localRotation));
                }
                if (!_busyChecked)
                {
                    float cursor = _c.actions.Cursor;
                    if (_c.PlayAction(_names[_n]) || _c.actions.Cursor != cursor) _problems.Add("BUSY_ACTION_RESTARTED");
                    _busyChecked = true;
                }
            }
            switch (_stage)
            {
                case 0:
                    if (_stageFrames >= idleFrames) { _stage=1; _stageFrames=0; _phaseStart=_c.locomotion.phase; }
                    break;
                case 1:
                    _c.locomotion.amplitude = Mathf.Min(.19f, _c.locomotion.amplitude + .01f);
                    if (_stageFrames >= moveFrames)
                    {
                        _phaseTravel = Mathf.Max(_phaseTravel,_c.locomotion.phase-_phaseStart);
                        var before = Snapshot();
                        if (!_c.PlayAction(_names[_n])) { _problems.Add("ACTION_REQUEST_REFUSED "+_names[_n]); Finish(); return; }
                        // The request is from moving locomotion. It must queue a stop, without a pose write.
                        if (_c.Phase != AlphaControllerPhase.Stopping) _problems.Add("MOVING_REQUEST_DID_NOT_STOP");
                        Compare(before); _stage=2; _stageFrames=0;
                    }
                    break;
                case 2:
                    if (_c.Phase == AlphaControllerPhase.Locomotion)
                    {
                        if (!_entered || !_acted || !_recovered || !_exited || !_busyChecked) _problems.Add("MISSING_ACTION_PHASE "+_names[_n]);
                        if (_motion <= .001f && _motionRot <= .05f) _problems.Add("ACTION_STATIC "+_names[_n]);
                        _results.Add($"ACTION {_names[_n]} status={_c.actions.Current.Data.status} phases=enter,action,recovery,exit,idle motion_vox={_motion:0.0000} rotation_deg={_motionRot:0.0000}");
                        if(_c.actions.Current.Data.twoHandedAttack)
                        {
                            if(_mainGrip>.05f||_secondRadial>2.5f||_outsideHandle>1e-3f||_bladeTravel<30f)
                                _problems.Add($"RUNTIME_GRIP_OR_BLADE {_names[_n]} main={_mainGrip} radial={_secondRadial} outside={_outsideHandle} blade={_bladeTravel}");
                            _results.Add($"  RUNTIME_GRIP main_vox={_mainGrip:0.000000} second_radial_vox={_secondRadial:0.0000} outside_handle_vox={_outsideHandle:0.000000} blade_travel_vox={_bladeTravel:0.000}");
                        }
                        _done++; _stage=3; _stageFrames=0;
                    }
                    else if (_stageFrames > fps * (_c.actions.Get(_names[_n]).Data.duration/1000f+6))
                    { _problems.Add("ACTION_TIMEOUT "+_names[_n]); Finish(); return; }
                    break;
                case 3:
                    if (_stageFrames >= afterFrames)
                    {
                        if (++_n >= _names.Length) { Finish(); return; }
                        _entered=_acted=_recovered=_exited=_busyChecked=false; _firstAction=null;
                        _motion=_motionRot=0; _stage=0; _stageFrames=0;
                        _mainGrip=_secondRadial=_outsideHandle=_bladeTravel=0;_hasTip=false;
                    }
                    break;
            }
            _lastPhase=_c.Phase; _prev=Snapshot();
        }

        void CheckGrip()
        {
            if(!_c.actions.Current.Data.twoHandedAttack)return;
            int w=_rig.IndexOf("weapon"),r=_rig.IndexOf("hand_r"),l=_rig.IndexOf("hand_l");
            if(w<0||r<0||l<0){_problems.Add("GRIP_PART_MISSING");return;}
            var mesh=_rig.meshes[w];var size=_rig.sizes[w];
            Vector3 Grid(float z)=>new Vector3(size.x*.5f,z,size.y*.5f); // grid Y/Z swapped for Unity
            Vector3 Local(Vector3 p)=>_rig.transform.InverseTransformPoint(p);
            var grip=Local(mesh.TransformPoint(Grid(4)));
            var axis=(Local(mesh.TransformPoint(Grid(5)))-grip);float scale=axis.magnitude;
            if(!(scale>1e-6f)||float.IsInfinity(scale)){_problems.Add("GRIP_AXIS_INVALID");return;}axis/=scale;
            var main=Local(_rig.parts[r].position);var rel=Local(_rig.parts[l].position)-grip;
            float along=Vector3.Dot(rel,axis);
            _mainGrip=Mathf.Max(_mainGrip,Vector3.Distance(main,grip));
            _secondRadial=Mathf.Max(_secondRadial,(rel-axis*along).magnitude);
            _outsideHandle=Mathf.Max(_outsideHandle,Mathf.Max((_rig.weaponHandleMinZ-4)*scale-along,along-(_rig.weaponHandleMaxZ-4)*scale));
            var tip=Local(mesh.TransformPoint(Grid(size.z)));
            if(_hasTip)_bladeTravel+=Vector3.Distance(tip,_lastTip);
            _lastTip=tip;_hasTip=true;
        }

        void Capture()
        {
            var camera=Camera.main;
            if(camera==null)camera=Object.FindFirstObjectByType<Camera>();
            if(camera==null){_problems.Add("CAPTURE_CAMERA_MISSING");return;}
            var dir=Path.GetFullPath(Path.Combine(Application.dataPath,"..","TransferOutput","controller_frames"));
            Directory.CreateDirectory(dir);
            var rt=new RenderTexture(480,480,24);var previous=RenderTexture.active;
            var previousTarget=camera.targetTexture;var tex=new Texture2D(480,480,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
                tex.ReadPixels(new Rect(0,0,480,480),0,0);tex.Apply();
                string file=$"frame_{_frame:000000}.png";File.WriteAllBytes(Path.Combine(dir,file),tex.EncodeToPNG());
                _captureLog.Add($"{file}\t{_c.Phase}\t{_c.CurrentAction ?? "locomotion"}\t{_c.actions.Cursor:0.0}");
            }
            finally {camera.targetTexture=previousTarget;RenderTexture.active=previous;Destroy(rt);Destroy(tex);}
        }

        void Finish()
        {
            if (_names == null || _done != _names.Length || _done == 0) _problems.Add("SEQUENCE_INCOMPLETE");
            if (_maxPos > 1e-3f || _maxRot > .05f || _maxScale > 1e-4f) _problems.Add($"RUNTIME_POSE_MISMATCH pos={_maxPos} rot={_maxRot} scale={_maxScale}");
            if (_jump > maxHandoverJumpVox || _jumpRot > .05f) _problems.Add($"HANDOVER_JUMP pos={_jump} rot={_jumpRot}");
            if (_phaseTravel < 2f*Mathf.PI) _problems.Add("LOCOMOTION_DID_NOT_REPEAT");
            Verdict = _problems.Count == 0 ? "PASS" : "FAIL";
            var s = new StringBuilder();
            s.AppendLine($"CONTROLLER_PLAYMODE_RESULT={Verdict}"); s.AppendLine("session="+session);
            s.AppendLine($"frames={_frame} simulated_fps={fps} actions_completed={_done} required={_names?.Length ?? 0} phase_travel_rad={_phaseTravel}");
            s.AppendLine($"runtime_pose_max_pos_err={_maxPos:0.000000} runtime_pose_max_rot_err_deg={_maxRot:0.000000} scale_err={_maxScale:0.000000}");
            s.AppendLine($"recovery_boundary_jump_vox={_jump:0.000000} rotation_deg={_jumpRot:0.000000} limit={maxHandoverJumpVox}");
            s.AppendLine("transition_status=UNITY_TRANSITION (raw clips are unmodified)");
            foreach(var r in _results) s.AppendLine(r);
            s.AppendLine("events="+(_c==null?"":string.Join(",",_c.Events)));
            s.AppendLine("problems="+_problems.Count); foreach(var p in _problems) s.AppendLine("  - "+p);
            var dir=Path.GetFullPath(Path.Combine(Application.dataPath,"..","TransferOutput")); Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir,"controller_playmode.txt"),s.ToString()); Debug.Log(s.ToString());
            if(captureFirstAction)File.WriteAllLines(Path.Combine(dir,"controller_frames.tsv"),_captureLog);
            Finished=true; Time.captureDeltaTime=0;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying=false;
#endif
        }
    }
}
