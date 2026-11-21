// One same-human demo in real PlayMode frames. Native state clocks are separate from
// renderer strike windows; crossfades and locomotion input remain UNITY_TRANSITION.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace CubeGame.AlphaTransfer
{
    [DefaultExecutionOrder(1100)]
    public class AlphaNativeProbe:MonoBehaviour
    {
        public int fps=60;public string session;public bool capture;
        public static bool Finished;public static string Verdict="NOT_RUN";
        AlphaNativeDriver _n;AlphaAnimationController _c;AlphaRig _r;
        int _stage,_frame,_steps,_changes,_hidden;float _age,_pos,_rot,_scale,_grip,_radial,_outside,_jump,_clock;
        bool _walk,_recovery,_held,_cancel,_dynamicDone,_tapped;float _walkStart;
        readonly HashSet<int> _seen=new HashSet<int>();readonly List<string> _errors=new List<string>();
        readonly List<string> _log=new List<string>();readonly List<string> _shots=new List<string>();
        void Start()
        {
            Finished=false;Verdict="RUNNING";Time.captureDeltaTime=1f/fps;
            _n=GetComponent<AlphaNativeDriver>();_c=GetComponent<AlphaAnimationController>();_r=GetComponent<AlphaRig>();
            _n.manualClock=true;
            if(!_n.Init()){_errors.AddRange(_n.Problems);Finish();return;}
            _c.locomotion.followAmplitude=false;_c.locomotion.amplitude=.19f;_walkStart=_c.locomotion.phase;
        }
        void LateUpdate()
        {
            if(Finished)return;float dt=1f/fps;_age+=dt;_frame++;_steps++;
            int before=_n.Action;double timer=_n.TimerMs;var previous=Snapshot();
            if(_stage==0&&_age>2){_walk=Mathf.Abs(_c.locomotion.phase-_walkStart)>Mathf.PI*2;Next();}
            if(_stage==1){_c.locomotion.amplitude=Mathf.MoveTowards(_c.locomotion.amplitude,0,1.2f*dt);
                if(_c.locomotion.amplitude==0){_n.primaryHeld=true;Next();}}
            if(_stage==2&&_n.Action==57){_n.primaryHeld=false;_tapped=true;}
            if(_stage==2&&_n.Action==57&&_n.TimerMs>2000){
                if(_c.Phase!=AlphaControllerPhase.Recovery)_errors.Add("STRIKE_END_NOT_SEPARATE_FROM_NATIVE_STATE");else _recovery=true;}
            if(_stage==2&&_tapped&&_n.Action==0&&_c.Phase==AlphaControllerPhase.Locomotion&&_n.TimerMs>10350){_n.primaryHeld=true;Next();}
            if(_stage==3&&_n.Action==58){_n.primaryHeld=false;Next();}
            if(_stage==4&&_n.Action==0&&_c.Phase==AlphaControllerPhase.Locomotion){
                _n.profileId="class_4_2h";if(!_n.ActivateHeldForTest())_errors.Add("HELD_ACTIVATION_FAILED");Next();}
            if(_stage==5&&_age>11){_held=_n.Action==79;_n.primaryHeld=true;Next();}
            if(_stage==6&&_n.Action==57){_n.primaryHeld=false;_cancel=true;Next();}
            if(_stage==7&&_n.Action==0&&_c.Phase==AlphaControllerPhase.Locomotion){
                _c.nativeLifecycle=false;if(!_c.PlayAction("action_026"))_errors.Add("DYNAMIC_ACTION_REJECTED");Next();}
            if(_stage==8){
                bool old=_r.IsVisible(_r.IndexOf("weapon"));_c.Step(dt);bool now=_r.IsVisible(_r.IndexOf("weapon"));
                if(old!=now)_changes++;if(!now)_hidden++;
                if(!_c.InAction&&_age>1){_dynamicDone=_changes>=2&&_hidden>0;Next();}}
            else if(_stage<8)_n.Step(dt);
            if(_n.Action!=before){
                _log.Add($"frame={_frame} from={before} timer_before={timer:F3} to={_n.Action} timer_after={_n.TimerMs:F3}");
                if(_n.TimerMs==0){float j=Jump(previous);_jump=Mathf.Max(_jump,j);}
                if(before!=0&&_n.Action==0&&_n.TimerMs<10001)_errors.Add("EARLY_NATIVE_IDLE");
            }
            if(_n.Action!=0)_seen.Add(_n.Action);
            CheckPose();CheckGrip();
            if(_stage<8&&_n.Action!=0&&_c.actions.Current.Data.category=="NATIVE_LIFECYCLE")
                _clock=Mathf.Max(_clock,(float)Math.Abs(_c.actions.Cursor-_n.TimerMs));
            if(capture&&ShouldCapture())Capture();
            if(_n.Problems.Count>0||_c.Problems.Count>0){_errors.AddRange(_n.Problems);_errors.AddRange(_c.Problems);Finish();return;}
            if(_stage==9){Finish();return;}
            if(_frame>fps*90){_errors.Add("SCENARIO_TIMEOUT stage="+_stage);Finish();}
        }
        void Next(){_log.Add($"stage={_stage} elapsed={_age:F3}");_stage++;_age=0;}
        PartPose[] Snapshot(){var p=new PartPose[_r.parts.Length];for(int i=0;i<p.Length;i++)p[i]=new PartPose{
            position=_r.parts[i].localPosition,rotation=_r.parts[i].localRotation,scale=_r.parts[i].localScale};return p;}
        float Jump(PartPose[] p){float v=0;for(int i=0;i<p.Length;i++)if(_r.IsVisible(i))v=Mathf.Max(v,Vector3.Distance(p[i].position,_r.parts[i].localPosition));return v;}
        void CheckPose()
        {
            var expected=new PartPose[_r.parts.Length];var visible=new bool[expected.Length];
            bool formula=_c.Phase==AlphaControllerPhase.Locomotion||_c.Phase==AlphaControllerPhase.ExitingAction||_c.Phase==AlphaControllerPhase.Stopping;
            if(formula){foreach(var name in _c.locomotion.PartNames){int i=_r.IndexOf(name);
                AlphaSpace.Decompose(AlphaSpace.ToUnity(_c.locomotion.Joint(name)),out var p,out var q,out var s);
                expected[i]=new PartPose{position=p,rotation=q,scale=s};visible[i]=true;}}
            else{var c=_c.actions.Current;var p=new PartPose[c.PartCount];var v=new bool[p.Length];c.Sample(_c.actions.Cursor,p);c.SampleVisibility(_c.actions.Cursor,v);
                for(int i=0;i<p.Length;i++){int r=_r.IndexOf(c.Data.parts[i]);expected[r]=p[i];visible[r]=v[i];}}
            if(_c.IsBlending){for(int i=0;i<expected.Length;i++)if(visible[i]&&_c.BlendFromVisible[i]){
                var from=_c.BlendFrom[i];var e=expected[i];expected[i]=new PartPose{
                    position=Vector3.Lerp(from.position,e.position,_c.BlendWeight),rotation=Quaternion.Slerp(from.rotation,e.rotation,_c.BlendWeight),
                    scale=Vector3.Lerp(from.scale,e.scale,_c.BlendWeight)};}}
            for(int i=0;i<expected.Length;i++){
                if(_r.IsVisible(i)!=visible[i])_errors.Add("VISIBILITY_MISMATCH "+_r.partNames[i]);if(!visible[i])continue;
                var t=_r.parts[i];float p=Vector3.Distance(t.localPosition,expected[i].position),q=Quaternion.Angle(t.localRotation,expected[i].rotation),s=Vector3.Distance(t.localScale,expected[i].scale);
                if(float.IsNaN(p)||float.IsNaN(q)||float.IsNaN(s))_errors.Add("NONFINITE_POSE");
                _pos=Mathf.Max(_pos,p);_rot=Mathf.Max(_rot,q);_scale=Mathf.Max(_scale,s);
            }
        }
        void CheckGrip()
        {
            if(!_c.InAction||_c.IsBlending||_c.actions.Current.Data.gripEndMs<=0||_c.actions.Cursor>_c.actions.Current.Data.gripEndMs)return;
            int w=_r.IndexOf("weapon"),r=_r.IndexOf("hand_r"),l=_r.IndexOf("hand_l");var mesh=_r.meshes[w];var z=_r.sizes[w];
            Vector3 Local(Vector3 p)=>_r.transform.InverseTransformPoint(p);
            Vector3 Grid(float h)=>new Vector3(z.x*.5f,h,z.y*.5f);
            var grip=Local(mesh.TransformPoint(Grid(4)));var axis=Local(mesh.TransformPoint(Grid(5)))-grip;float s=axis.magnitude;axis/=s;
            var rel=Local(_r.parts[l].position)-grip;float along=Vector3.Dot(rel,axis);
            _grip=Mathf.Max(_grip,Vector3.Distance(Local(_r.parts[r].position),grip));_radial=Mathf.Max(_radial,(rel-axis*along).magnitude);
            _outside=Mathf.Max(_outside,Mathf.Max((_r.weaponHandleMinZ-4)*s-along,along-(_r.weaponHandleMaxZ-4)*s));
        }
        bool ShouldCapture()=>_frame%Mathf.Max(1,fps/10)==0&&(_stage<=2&&(_age<3||_age>10)||_stage==3&&_age<3||_stage==5&&_age<.6f||_stage==6||_stage==7&&_age<1.5f||_stage==8);
        void Capture()
        {
            var camera=Camera.main??UnityEngine.Object.FindFirstObjectByType<Camera>();if(camera==null){_errors.Add("NO_CAMERA");return;}
            string dir=Path.GetFullPath(Path.Combine(Application.dataPath,"..","TransferOutput","native_frames"));Directory.CreateDirectory(dir);
            var rt=new RenderTexture(480,480,24);var prev=RenderTexture.active;var target=camera.targetTexture;var tex=new Texture2D(480,480,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,480,480),0,0);tex.Apply();
                string file=$"frame_{_frame:000000}.png";File.WriteAllBytes(Path.Combine(dir,file),tex.EncodeToPNG());
                int action=_stage==8?26:_n.Action;double timer=_stage==8?_c.actions.Cursor:_n.TimerMs;
                string visible=_r.IsVisible(_r.IndexOf("weapon"))?"on":"hidden";
                _shots.Add($"{file}\t{_c.Phase}\taction={action} stage={_stage} weapon={visible}\t{timer:F1}");}
            finally{camera.targetTexture=target;RenderTexture.active=prev;Destroy(rt);Destroy(tex);}
        }
        void Finish()
        {
            if(!_walk||!_recovery||!_held||!_cancel||!_dynamicDone||!_seen.IsSupersetOf(new[]{57,67,58,79}))_errors.Add("SCENARIO_INCOMPLETE");
            if(_pos>1e-3f||_rot>.05f||_scale>1e-4f||_jump>.05f)_errors.Add("RUNTIME_POSE_OR_HANDOVER");
            if(_clock>.5f)_errors.Add("NATIVE_SOURCE_CLOCK_DESYNC");
            if(_grip>.05f||_radial>2.5f||_outside>1e-3f)_errors.Add("GRIP_LIMIT");
            Verdict=_errors.Count==0?"PASS":"FAIL";
            var t=new StringBuilder();t.AppendLine($"NATIVE_LIFECYCLE_PLAYMODE_RESULT={Verdict}\nsession={session}\nfps={fps} frames={_frame} stage={_stage}");
            t.AppendLine($"walk={_walk} recovery_while_native_active={_recovery} held_over_10s={_held} primary_cancel={_cancel} dynamic={_dynamicDone} visibility_changes={_changes} hidden_frames={_hidden}");
            t.AppendLine($"pose_pos={_pos:F6} rot={_rot:F6} scale={_scale:F6} handover_jump={_jump:F6} primary_grip={_grip:F6} secondary_radial={_radial:F6} outside_handle={_outside:F6} clock_error_ms={_clock:F6}");
            t.AppendLine("problems="+_errors.Count);foreach(var e in _errors)t.AppendLine(e);foreach(var e in _log)t.AppendLine(e);
            if(_n!=null)foreach(var e in _n.Events)t.AppendLine(e);
            string dir=Path.GetFullPath(Path.Combine(Application.dataPath,"..","TransferOutput"));Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir,"native_lifecycle_playmode.txt"),t.ToString());if(capture)File.WriteAllLines(Path.Combine(dir,"native_frames.tsv"),_shots);
            Debug.Log(t.ToString());Finished=true;Time.captureDeltaTime=0;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying=false;
#endif
        }
    }
}
