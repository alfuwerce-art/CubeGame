// Real PlayMode sequence for EVERY required action. Independent expected sampling checks both
// raw Alpha poses and explicit Unity crossfades; missing/static clips or skipped phases fail.
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
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
        float _locoSeconds;                        // seconds the formula-driven locomotion ran (D65)
        float _prevPhase; bool _phaseInit;         // wrapped-phase accumulation (D65)
        float _mainGrip,_secondRadial,_outsideHandle,_bladeTravel;
        Vector3 _lastTip;bool _hasTip;
        bool _entered, _acted, _recovered, _exited, _busyChecked;
        AlphaControllerPhase _lastPhase;
        PartPose[] _prev, _firstAction;
        bool[] _previousVisible,_firstActionVisible;
        int _visibilityChanges,_hiddenFrames;
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
            _names = _c.requiredClips.Where(n=>_c.actions.Get(n).Data.category=="MOTION").ToArray();
            if (_names.Length == 0) { _problems.Add("NO_REQUIRED_REGISTRY"); Finish(); return; }
            _prev = Snapshot();_previousVisible=Visible(); _lastPhase = _c.Phase;
        }

        PartPose[] Snapshot()
        {
            var p = new PartPose[_rig.parts.Length];
            for (int i = 0; i < p.Length; i++) p[i] = new PartPose { position = _rig.parts[i].localPosition,
                rotation = _rig.parts[i].localRotation, scale = _rig.parts[i].localScale };
            return p;
        }

        bool[] Visible(){var v=new bool[_rig.parts.Length];for(int i=0;i<v.Length;i++)v[i]=_rig.IsVisible(i);return v;}
        void Compare(PartPose[] expected,bool[] visible=null)
        {
            for (int i = 0; i < expected.Length; i++)
            {
                if(visible!=null && _rig.IsVisible(i)!=visible[i])_problems.Add("RUNTIME_VISIBILITY_MISMATCH "+_rig.partNames[i]);
                if(visible!=null&&!visible[i])continue;
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
            var visible=new bool[_rig.parts.Length];
            bool formula = _c.Phase == AlphaControllerPhase.Locomotion || _c.Phase == AlphaControllerPhase.Stopping
                || _c.Phase == AlphaControllerPhase.ExitingAction;
            if (formula)
            {
                // D65: the phase is wrapped into [0, 2pi) by the driver, so the travelled angle must be
                // accumulated frame by frame (the old "phase - phaseStart" stopped at one wrap and made a
                // long-stride kind look as if the locomotion never repeated).
                _locoSeconds += Time.deltaTime;
                float ph = _c.locomotion.phase;
                if (!_phaseInit) { _prevPhase = ph; _phaseInit = true; }
                float d = Mathf.Repeat(ph - _prevPhase + Mathf.PI, 2f * Mathf.PI) - Mathf.PI;
                if (d > 0f) _phaseTravel += d;
                _prevPhase = ph;
            }
            if (formula)
            {
                foreach (var name in _c.locomotion.PartNames)
                {
                    int i = _rig.IndexOf(name);
                    AlphaSpace.Decompose(AlphaSpace.ToUnity(_c.locomotion.Joint(name)), out var p,out var r,out var s);
                    expected[i] = new PartPose { position=p,rotation=r,scale=s };
                    visible[i]=true;
                }
            }
            else
            {
                var clip = _c.actions.Current; var source = new PartPose[clip.PartCount];
                clip.Sample(_c.actions.Cursor, source);var mask=new bool[clip.PartCount];clip.SampleVisibility(_c.actions.Cursor,mask);
                for (int i = 0; i < source.Length; i++){int p=_rig.IndexOf(clip.Data.parts[i]);expected[p]=source[i];visible[p]=mask[i];}
            }
            if (_c.IsBlending)
                for (int i = 0; i < expected.Length; i++)
                {
                    if(!visible[i]||!_c.BlendFromVisible[i])continue;
                    var a = _c.BlendFrom[i]; var b = expected[i]; float u = _c.BlendWeight;
                    expected[i] = new PartPose { position=Vector3.Lerp(a.position,b.position,u),
                        rotation=Quaternion.Slerp(a.rotation,b.rotation,u), scale=Vector3.Lerp(a.scale,b.scale,u) };
                }
            Compare(expected,visible);
        }

        void LateUpdate()
        {
            if (Finished) return;
            _frame++; _stageFrames++;
            if (_c.Problems.Count > 0) { _problems.AddRange(_c.Problems); Finish(); return; }
            CheckPose();
            for(int i=0;i<_rig.parts.Length;i++)
            {
                if(_previousVisible[i]!=_rig.IsVisible(i))_visibilityChanges++;
                if(!_rig.IsVisible(i))_hiddenFrames++;
            }
            if(captureFirstAction && _n==0 && _frame%4==0) Capture();
            if (_c.Phase == AlphaControllerPhase.EnteringAction) _entered = true;
            if (_c.Phase == AlphaControllerPhase.Recovery) _recovered = true;
            if (_c.Phase == AlphaControllerPhase.ExitingAction) _exited = true;
            if (_c.Phase == AlphaControllerPhase.Recovery && _lastPhase == AlphaControllerPhase.Action)
                for (int i=0;i<_prev.Length;i++)
                {
                    if(!_previousVisible[i]||!_rig.IsVisible(i))continue;
                    _jump = Mathf.Max(_jump,Vector3.Distance(_prev[i].position,_rig.parts[i].localPosition));
                    _jumpRot = Mathf.Max(_jumpRot,Quaternion.Angle(_prev[i].rotation,_rig.parts[i].localRotation));
                }
            if (_c.Phase == AlphaControllerPhase.Action)
            {
                _acted = true;
                CheckGrip();
                if (_firstAction == null){_firstAction = Snapshot();_firstActionVisible=Visible();}
                for (int i=0;i<_firstAction.Length;i++)
                {
                    if(!_firstActionVisible[i]||!_rig.IsVisible(i))continue;
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
                        if(_c.actions.Current.Data.visibilityMode=="EXPLICIT_NATIVE_DRAW")
                        {
                            if(_visibilityChanges<2||_hiddenFrames==0)_problems.Add("DYNAMIC_VISIBILITY_NOT_EXERCISED "+_names[_n]);
                            _results.Add($"  VISIBILITY transitions={_visibilityChanges} hidden_part_frames={_hiddenFrames}");
                        }
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
                        _visibilityChanges=_hiddenFrames=0;
                    }
                    break;
            }
            _lastPhase=_c.Phase; _prev=Snapshot();_previousVisible=Visible();
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
            // D65: the locomotion must advance as much as the kind's own phase rate implies within the walk
            // time the scenario gave it. Kinds with long strides cover < 2pi in the same scenario (0x15:
            // 5.4 rad); a locomotion that does not advance at all still fails.
            float rate = 0f;
            try { rate = AlphaParamWalk.PhaseRate(_c.locomotion.Spec.@params, _c.locomotion.speedXY); } catch { }
            float expected = rate > 0f ? rate * _locoSeconds : 2f * Mathf.PI;
            float need = Mathf.Min(2f * Mathf.PI, Mathf.Max(0.5f, 0.5f * expected));
            if (_phaseTravel < need)
                _problems.Add($"LOCOMOTION_DID_NOT_REPEAT travel={_phaseTravel:0.00} need={need:0.00} rate={rate:0.000} loco_s={_locoSeconds:0.00}");
            Verdict = _problems.Count == 0 ? "PASS" : "FAIL";
            var s = new StringBuilder();
            s.AppendLine($"CONTROLLER_PLAYMODE_RESULT={Verdict}"); s.AppendLine("session="+session);
            s.AppendLine($"frames={_frame} simulated_fps={fps} actions_completed={_done} required={_names?.Length ?? 0} phase_travel_rad={_phaseTravel}");
            s.AppendLine($"loco_seconds={_locoSeconds:0.00} phase_rate_rad_s={rate:0.000} phase_travel_need_rad={need:0.00}");
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
