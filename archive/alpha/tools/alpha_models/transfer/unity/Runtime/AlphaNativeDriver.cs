// Isolated native input adapter on the established animation controller.
using System;
using System.Collections.Generic;
using UnityEngine;
namespace CubeGame.AlphaTransfer
{
    [RequireComponent(typeof(AlphaAnimationController))]
    public class AlphaNativeDriver:MonoBehaviour
    {
        public TextAsset specAsset;
        public string profileId="class_1_2h";
        public bool primaryHeld,secondaryHeld,blocked,manualClock;
        public int Action {get;private set;}
        public double TimerMs {get;private set;}=200; // TEST_ADAPTER initial idle age, not an Alpha rule
        public AlphaNativeSpec Spec {get;private set;}
        public List<string> Events {get;}=new List<string>();
        public List<string> Problems {get;}=new List<string>();
        AlphaAnimationController _c;
        public bool Init()
        {
            if(Spec!=null)return Problems.Count==0;
            try{
                _c=GetComponent<AlphaAnimationController>();_c.externalClock=true;
                if(!_c.Init())throw new ArgumentException(string.Join(";",_c.Problems));
                Spec=AlphaNativeSpec.Parse(specAsset.text,GetComponent<AlphaRig>().rigId);Spec.Profile(profileId);
                foreach(var p in Spec.profiles)foreach(var r in p.rules)
                    if(r.action!=0&&Clip(r.action)==null)throw new ArgumentException("NATIVE_BINDING_MISSING "+r.action);
            }catch(Exception e){Problems.Add(e.Message);return false;}return true;
        }
        string Clip(int action)
        {
            foreach(var n in _c.requiredClips){var d=_c.actions.Get(n).Data;
                if(d.actionCode==action&&(d.category=="NATIVE_LIFECYCLE"||d.category=="HELD_STATE"))return n;}
            return null;
        }
        // Known class-4 binding only. Full ability activation gates remain PARTIAL.
        public bool ActivateHeldForTest()
        {
            if(!Init()||profileId!="class_4_2h"||Action!=0||_c.locomotion.Problems.Count>0)return false;
            Action=79;TimerMs=0;Events.Add("TEST_ADAPTER_ACTIVATION:79 resource/world gates PARTIAL");
            return _c.PlayNativeAction(Clip(79));
        }
        public void Step(float dt)
        {
            if(Spec==null&&!Init())return;
            if(float.IsNaN(dt)||float.IsInfinity(dt)||dt<0){Problems.Add("NATIVE_CLOCK_INVALID");return;}
            try{
                TimerMs+=dt*1000.0;
                var o=AlphaNativeInput.Decide(Spec.Profile(profileId),Action,(int)Math.Floor(TimerMs+1e-4),primaryHeld,secondaryHeld,blocked);
                if(o.selected){Action=o.action;TimerMs=0;Events.Add("NATIVE_PRIMARY:"+Action);_c.PlayNativeAction(Clip(Action));_c.Step(0);}
                else if(o.action!=Action){Events.Add("NATIVE_IDLE:"+Action+" timer="+TimerMs.ToString("F3"));Action=o.action;_c.NativeIdle();_c.Step(dt);}
                else _c.Step(dt);
                if(_c.Problems.Count>0)Problems.AddRange(_c.Problems);
            }catch(Exception e){Problems.Add(e.Message);}
        }
        void Start(){Init();}
        void Update(){if(!manualClock)Step(Time.deltaTime);}
    }
}
