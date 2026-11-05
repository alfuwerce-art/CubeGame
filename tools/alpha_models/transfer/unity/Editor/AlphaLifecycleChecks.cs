// Synthetic corruption checks plus local golden native helper decisions. No Alpha assets in Git.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CubeGame.AlphaTransfer.Editor
{
    public static class AlphaLifecycleChecks
    {
        public static void Run()
        {
            int count=0;var errors=new List<string>();
            Action<string,bool> check=(n,ok)=>{count++;if(!ok)errors.Add(n);};
            Action<string,Action> refuses=(n,f)=>{bool refused=false;try{f();}catch(ArgumentException){refused=true;}check(n,refused);};
            try{
                var rig=JsonUtility.FromJson<RigId>(File.ReadAllText("Assets/AlphaTransfer/Data/rig.json")).rig;
                string json=File.ReadAllText("Assets/AlphaTransfer/Data/lifecycle.json");
                var spec=AlphaNativeSpec.Parse(json,rig);
                foreach(var g in spec.golden){var o=AlphaNativeInput.Decide(spec.Profile(g.profile),g.action,g.timerMs,g.primary,g.secondary,g.blocked);
                    check($"native {g.profile}/{g.action}/{g.timerMs}/{g.primary}/{g.secondary}/{g.blocked}",
                        o.action==g.expectedAction&&o.timerMs==g.expectedTimerMs&&o.selected==g.expectedSelected);}
                refuses("missing native profile",()=>spec.Profile("absent"));
                refuses("unsupported native action",()=>AlphaNativeInput.Decide(spec.profiles[0],255,0,false,false,false));
                refuses("negative native timer",()=>AlphaNativeInput.Decide(spec.profiles[0],0,-1,false,false,false));
                var bad=JsonUtility.FromJson<AlphaNativeSpec>(json);bad.profiles[0].rules[0].primaryAtMs=-10;
                refuses("corrupt native timing",()=>AlphaNativeSpec.Parse(JsonUtility.ToJson(bad),rig));
                bad=JsonUtility.FromJson<AlphaNativeSpec>(json);bad.profiles[0].rules[0].primaryNext=255;
                refuses("missing native target",()=>AlphaNativeSpec.Parse(JsonUtility.ToJson(bad),rig));
                refuses("wrong rig",()=>AlphaNativeSpec.Parse(json,"wrong"));
                // TEST_DATA_NOT_ALPHA. Reappearance has its own pose, no made-up hidden transform.
                var d=new AlphaClipData{name="sparse",status="TEST_DATA_NOT_ALPHA",rig="synthetic",param="time_ms",duration=30,
                    visibilityMode="EXPLICIT_NATIVE_DRAW",parts=new[]{"body","weapon"},capturedSizes=new[]{1,1,1,1,1,1},
                    keys=new[]{new AlphaClipKey{x=0,visible=new[]{1,1},m=Both(0)},
                        new AlphaClipKey{x=10,visible=new[]{1,0},m=AlphaSpace.Translate(Vector3.zero)},
                        new AlphaClipKey{x=20,visible=new[]{1,1},m=Both(10)},
                        new AlphaClipKey{x=30,visible=new[]{1,1},m=Both(20)}}};
                var p=new List<string>();var clip=AlphaClip.Parse(JsonUtility.ToJson(d),"synthetic",p);
                check("sparse clip parses",clip!=null&&p.Count==0);
                var go=new GameObject("visibility_synthetic");
                try{
                    var r=go.AddComponent<AlphaRig>();r.partNames=d.parts;r.parts=new Transform[2];r.meshes=new Transform[2];
                    for(int i=0;i<2;i++){r.parts[i]=new GameObject(d.parts[i]).transform;r.parts[i].parent=go.transform;
                        r.meshes[i]=GameObject.CreatePrimitive(PrimitiveType.Cube).transform;r.meshes[i].parent=r.parts[i];}
                    var poses=new PartPose[2];var v=new bool[2];
                    foreach(float t in new[]{0f,9.999f,10f,19.999f,20f,25f,30f}){
                        clip.Sample(t,poses);clip.SampleVisibility(t,v);r.Apply(poses,new[]{0,1},v);
                        check("renderer visibility at "+t,r.IsVisible(1)==(t<10||t>=20));
                        if(t>=20)check("reappearance samples native pose "+t,Vector3.Distance(poses[1].position,new Vector3(10+(t-20),0,0))<1e-4f);
                        if(t>=10&&t<20)check("hidden transform untouched "+t,r.parts[1].localPosition.sqrMagnitude<1e-6f);
                    }
                    bool threw=false;try{clip.KeyMatrix(1,1);}catch(InvalidOperationException){threw=true;}
                    check("hidden matrix is absent",threw);
                }finally{UnityEngine.Object.DestroyImmediate(go);}
                string good=JsonUtility.ToJson(d);
                foreach(string kind in new[]{"missing mask","bad mask","hidden matrix","missing visible matrix","nonfinite","bad time","undeclared mask","unknown category","held wrong timing"}){
                    var x=JsonUtility.FromJson<AlphaClipData>(good);
                    switch(kind){case "missing mask":x.keys[1].visible=null;break;case "bad mask":x.keys[1].visible[1]=2;break;
                        case "hidden matrix":x.keys[1].m=Both(0);break;case "missing visible matrix":x.keys[0].m=new float[16];break;
                        case "nonfinite":x.keys[0].m[0]=float.PositiveInfinity;break;case "bad time":x.keys[1].x=0;break;
                        case "undeclared mask":x.visibilityMode="STATIC_ALL";break;
                        case "unknown category":x.category="typo";break;
                        case "held wrong timing":x.category="HELD_STATE";x.actionCode=79;x.actionEndMs=5;break;}
                    var pr=new List<string>();check("reject "+kind,AlphaClip.Parse(JsonUtility.ToJson(x),"synthetic",pr)==null&&pr.Count>0);
                }
                // Every declared asset, including held/native categories, is checked by controller Init.
                var playerAssets=Directory.GetFiles("Assets/AlphaTransfer/Data/clips","*.json");
                var all=new List<AlphaClip>();
                foreach(var path in playerAssets){var pr=new List<string>();var c=AlphaClip.Parse(File.ReadAllText(path),rig,pr);
                    check("parse "+Path.GetFileName(path),c!=null&&pr.Count==0);if(c!=null)all.Add(c);}
                check("five dynamic clips",all.Count(c=>c.Data.visibilityMode=="EXPLICIT_NATIVE_DRAW")==5);
                check("latched held pose",all.Count(c=>c.Data.category=="HELD_STATE")==1);
                check("three native clocks",all.Count(c=>c.Data.category=="NATIVE_LIFECYCLE")==3);
            }catch(Exception e){errors.Add(e.ToString());}
            var text=$"NATIVE_LIFECYCLE_RESULT={(errors.Count==0?"PASS":"FAIL")} cases={count} failed={errors.Count}\n"+string.Join("\n",errors);
            Directory.CreateDirectory("TransferOutput");File.WriteAllText("TransferOutput/native_lifecycle.txt",text);Debug.Log(text);
            EditorApplication.Exit(errors.Count==0?0:1);
        }
        static float[] Both(float x)=>AlphaSpace.Translate(Vector3.zero).Concat(AlphaSpace.Translate(new Vector3(x,0,0))).ToArray();
        [Serializable]class RigId{public string rig;}
    }
}
