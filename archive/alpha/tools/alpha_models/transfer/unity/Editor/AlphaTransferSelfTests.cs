// AlphaTransferSelfTests.cs — synthetic self-tests (TEST_DATA_NOT_ALPHA: no Alpha numbers here).
//   -executeMethod CubeGame.AlphaTransfer.Editor.AlphaTransferSelfTests.Run
// Writes <project>/TransferOutput/selftest.txt with SELFTEST_RESULT=PASS|FAIL cases=N failed=M.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CubeGame.AlphaTransfer.Editor
{
    public static class AlphaTransferSelfTests
    {
        static readonly List<(string name, Func<string> run)> Cases = new List<(string, Func<string>)>();

        static void Add(string name, Func<string> run) => Cases.Add((name, run));
        static string Expect(bool cond, string msg) => cond ? null : msg;
        static bool Near(Vector3 a, Vector3 b, float eps = 1e-4f) => (a - b).magnitude <= eps;

        static float[] RowRotZ(float deg)
        {
            float c = Mathf.Cos(deg * Mathf.Deg2Rad), s = Mathf.Sin(deg * Mathf.Deg2Rad);
            return new float[] { c, s, 0, 0, -s, c, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 };
        }

        static float[] RowRotX(float deg)
        {
            float c = Mathf.Cos(deg * Mathf.Deg2Rad), s = Mathf.Sin(deg * Mathf.Deg2Rad);
            return new float[] { 1, 0, 0, 0, 0, c, s, 0, 0, -s, c, 0, 0, 0, 0, 1 };
        }

        static float[] RowScale(float x, float y, float z) => new float[] { x, 0, 0, 0, 0, y, 0, 0, 0, 0, z, 0, 0, 0, 0, 1 };

        static string ClipJson(string rig, string[] parts, float[][] keys, float[] xs, bool loop = false, float duration = 100f, string param = "time_ms")
        {
            var d = new AlphaClipData
            {
                name = "synthetic", status = "TEST_DATA_NOT_ALPHA", rig = rig, param = param, loop = loop,
                duration = duration, parts = parts, capturedSizes = new int[parts.Length * 3],
                keys = new AlphaClipKey[keys.Length], provenance = "{}", validation = "{}",
            };
            for (int i = 0; i < keys.Length; i++) d.keys[i] = new AlphaClipKey { x = xs[i], m = keys[i] };
            return JsonUtility.ToJson(d);
        }

        static float[] Concat(params float[][] ms)
        {
            var o = new List<float>();
            foreach (var m in ms) o.AddRange(m);
            return o.ToArray();
        }

        // Isolated synthetic controller: one body, no Alpha assets or extracted poses.
        static GameObject ControllerRig(out AlphaAnimationController controller)
        {
            var go = new GameObject("synthetic_controller");
            var rig=go.AddComponent<AlphaRig>(); rig.rigId="alpha_creature/synthetic";
            var p=new GameObject("body").transform;p.parent=go.transform;
            var m=new GameObject("mesh").transform;m.parent=p;
            rig.partNames=new[]{"body"};rig.parts=new[]{p};rig.meshes=new[]{m};
            rig.sizes=new[]{Vector3Int.one};rig.anchors=new[]{"center"};
            var loco=go.AddComponent<AlphaLocomotionDriver>();loco.amplitude=0;
            var spec=new AlphaLocomotionSpec {format=AlphaLocomotionSpec.Format,rig=rig.rigId,status="FORMULA_VERIFIED",
                parts=rig.partNames,terms=new string[0],code_only_terms=new string[0],unknown_terms=new string[0],
                @params=new AlphaLocomotionParams {size=new[]{1f,1f,2f}}};
            loco.specAsset=new TextAsset(JsonUtility.ToJson(spec));
            var d=new AlphaClipData { name="action_001",status="EMULATED_ALPHA_CODE",rig=rig.rigId,param="time_ms",
                duration=500,actionCode=1,drawState=1,actionEndMs=100,recoveryStartMs=116,weaponOnBack=true,
                parts=rig.partNames,capturedSizes=new[]{1,1,1},provenance="TEST_DATA_NOT_ALPHA",validation="{}",
                keys=new[]{new AlphaClipKey{x=0,m=AlphaSpace.Translate(Vector3.zero)},
                    new AlphaClipKey{x=100,m=AlphaSpace.Translate(Vector3.right)},
                    new AlphaClipKey{x=116,m=AlphaSpace.Translate(Vector3.zero)},new AlphaClipKey{x=500,m=AlphaSpace.Translate(Vector3.zero)}} };
            go.AddComponent<AlphaClipPlayer>().clipAssets=new[]{new TextAsset(JsonUtility.ToJson(d))};
            controller=go.AddComponent<AlphaAnimationController>();controller.requiredClips=new[]{"action_001"};
            return go;
        }

        // Synthetic entity contracts (TEST_DATA_NOT_ALPHA): structure only, any body plan.
        static AlphaEntityContract SynthContract(string rig, bool actions, params (string name, string role, string mirror, string anchor)[] parts)
        {
            return new AlphaEntityContract {
                format=AlphaEntityContract.Format, kind="0xff", category="TEST", bodyPlan="plan/test", rig=rig,
                controllerMode=actions?AlphaEntityContract.ModeActions:AlphaEntityContract.ModeLocomotionOnly,
                actionProfile=actions?"test_profile":"", actionTrigger=actions?"player_input":"",
                actionsStatus=actions?"AVAILABLE":"BLOCKED", actionsReason=actions?"":"NO_VERIFIED_ACTION_TRIGGER (test)",
                weaponLayer="NONE", transitions="UNITY_TRANSITION", readiness="AUTO_READY",
                walkSources=new[]{"RECONSTRUCTED_FROM_EVIDENCE"}, idleSources=new[]{"RECONSTRUCTED_FROM_EVIDENCE"},
                allowedClipProvenance=(string[])AlphaEntityContract.ClipProvenance.Clone(),
                allowedCategories=actions?new[]{"MOTION","HELD_STATE","NATIVE_LIFECYCLE","LOOP_LOCOMOTION","STATIC_POSE"}:new[]{"LOOP_LOCOMOTION","STATIC_POSE"},
                parts=parts.Select(p=>new AlphaEntityPart{name=p.name,role=p.role,mirrorOf=p.mirror,anchor=p.anchor}).ToArray() };
        }

        static AlphaRig SynthRig(GameObject go, string rig, params (string name, string anchor)[] parts)
        {
            var r=go.AddComponent<AlphaRig>();r.rigId=rig;
            r.partNames=parts.Select(p=>p.name).ToArray();r.anchors=parts.Select(p=>p.anchor).ToArray();
            r.parts=new Transform[parts.Length];r.meshes=new Transform[parts.Length];r.sizes=new Vector3Int[parts.Length];
            for(int i=0;i<parts.Length;i++){r.parts[i]=new GameObject(parts[i].name).transform;r.parts[i].parent=go.transform;
                r.meshes[i]=new GameObject("mesh").transform;r.meshes[i].parent=r.parts[i];r.sizes[i]=Vector3Int.one;}
            return r;
        }

        static string ContractRoundTrip(AlphaEntityContract c, out AlphaEntityContract parsed)
        {
            var problems=new List<string>();parsed=AlphaEntityContract.Parse(JsonUtility.ToJson(c),problems);
            return problems.Count==0?null:string.Join(";",problems);
        }

        static void Register()
        {
            Cases.Clear();
            Add("standalone_action_clears_previous_native_lifetime",()=>
            {
                var go=ControllerRig(out var c);
                try{
                    if(!c.Init())return string.Join(";",c.Problems);
                    c.nativeLifecycle=true;
                    if(!c.PlayAction("action_001")||c.nativeLifecycle)return "native lifetime leaked into standalone action";
                    for(int i=0;i<180&&c.InAction;i++)c.Step(1f/60);
                    return Expect(!c.InAction,"standalone action no longer completes after native owner");
                }finally{UnityEngine.Object.DestroyImmediate(go);}
            });
            Add("weapon_interpolation_keeps_grip_between_rotating_keys",()=>
            {
                var a=Matrix4x4.TRS(new Vector3(0,10,0),Quaternion.identity,Vector3.one);
                var b=Matrix4x4.TRS(new Vector3(-10,0,0),Quaternion.Euler(0,0,90),Vector3.one);
                var d=JsonUtility.FromJson<AlphaClipData>(ClipJson("synthetic",new[]{"weapon"},
                    new[]{AlphaSpace.ToAlpha(a),AlphaSpace.ToAlpha(b)},new[]{0f,100f}));
                d.twoHandedAttack=true;d.weaponOnBack=true;d.capturedSizes=new[]{3,3,28};
                var problems=new List<string>();var clip=AlphaClip.Parse(JsonUtility.ToJson(d),"synthetic",problems);
                if(clip==null)return string.Join(";",problems);
                var pose=new PartPose[1];clip.Sample(50,pose);
                var grip=pose[0].position+pose[0].rotation*new Vector3(0,-10,0);
                return Expect(grip.magnitude<1e-4f,"interpolation detached weapon from grip: "+grip);
            });
            // ---------------------------------------------------------------- entity contract
            var quad=new[]{("body","core","","center"),("tail","tail","","tail"),("head","head","","center"),
                ("hand_l","fore_limb","hand_r","center"),("hand_r","fore_limb","hand_l","center"),
                ("foot_l","hind_limb","foot_r","center"),("foot_r","hind_limb","foot_l","center")};
            var quadRig=quad.Select(p=>(p.Item1,p.Item4)).ToArray();
            Add("entity_contract_quadruped_locomotion_only_valid",()=>
            {
                var go=new GameObject("e");try{var rig=SynthRig(go,"alpha_creature/q",quadRig);
                    var err=ContractRoundTrip(SynthContract("alpha_creature/q",false,quad),out var c);if(err!=null)return err;
                    var v=c.Validate(rig,new AlphaClip[0],false);return Expect(v.Count==0,string.Join(";",v));}
                finally{UnityEngine.Object.DestroyImmediate(go);}
            });
            Add("entity_contract_three_part_plan_valid",()=>
            {
                var plan=new[]{("body","core","","center"),("foot_l","hind_limb","foot_r","center"),("foot_r","hind_limb","foot_l","center")};
                var go=new GameObject("e");try{var rig=SynthRig(go,"alpha_creature/r",plan.Select(p=>(p.Item1,p.Item4)).ToArray());
                    var err=ContractRoundTrip(SynthContract("alpha_creature/r",false,plan),out var c);if(err!=null)return err;
                    var v=c.Validate(rig,new AlphaClip[0],false);return Expect(v.Count==0,string.Join(";",v));}
                finally{UnityEngine.Object.DestroyImmediate(go);}
            });
            Add("entity_contract_rejects_action_clip_on_locomotion_only_entity",()=>
            {
                var go=new GameObject("e");try{var rig=SynthRig(go,"alpha_creature/q",quadRig);
                    ContractRoundTrip(SynthContract("alpha_creature/q",false,quad),out var c);
                    var d=JsonUtility.FromJson<AlphaClipData>(ClipJson("alpha_creature/q",new[]{"body"},new[]{AlphaSpace.Translate(Vector3.zero),AlphaSpace.Translate(Vector3.zero)},new[]{0f,100f}));
                    d.status="EMULATED_ALPHA_CODE";d.name="action_057";var probs=new List<string>();
                    var clip=AlphaClip.Parse(JsonUtility.ToJson(d),"alpha_creature/q",probs);
                    var v=c.Validate(rig,new[]{clip},false);
                    return Expect(v.Any(x=>x.StartsWith("CONTRACT_CLIP_CATEGORY_NOT_ALLOWED")),"humanoid-style action accepted on a locomotion-only body plan");}
                finally{UnityEngine.Object.DestroyImmediate(go);}
            });
            Add("entity_contract_rejects_unknown_provenance",()=>
            {
                var go=new GameObject("e");try{var rig=SynthRig(go,"alpha_creature/q",quadRig);
                    ContractRoundTrip(SynthContract("alpha_creature/q",false,quad),out var c);
                    var d=JsonUtility.FromJson<AlphaClipData>(ClipJson("alpha_creature/q",new[]{"body"},new[]{AlphaSpace.Translate(Vector3.zero),AlphaSpace.Translate(Vector3.zero)},new[]{0f,6.28f},true,6.28f,"walk_phase"));
                    d.status="ORIGINAL_NOT_ALPHA";var probs=new List<string>();
                    var clip=AlphaClip.Parse(JsonUtility.ToJson(d),"alpha_creature/q",probs);
                    var v=c.Validate(rig,new[]{clip},false);
                    return Expect(v.Any(x=>x.StartsWith("CONTRACT_CLIP_PROVENANCE_NOT_ALLOWED")),"non-Alpha provenance accepted");}
                finally{UnityEngine.Object.DestroyImmediate(go);}
            });
            Add("entity_contract_blocked_actions_need_reason",()=>
            {
                var c=SynthContract("alpha_creature/q",false,quad);c.actionsReason="";
                return Expect(ContractRoundTrip(c,out _)?.Contains("CONTRACT_BLOCKED_ACTIONS_NEED_REASON")==true,"silent BLOCKED accepted");
            });
            Add("entity_contract_actions_mode_needs_profile_and_registry",()=>
            {
                var c=SynthContract("alpha_creature/q",true,quad);c.actionProfile="";
                if(ContractRoundTrip(c,out _)?.Contains("CONTRACT_ACTIONS_WITHOUT_EVIDENCED_PROFILE")!=true)return "action mode without profile accepted";
                var go=new GameObject("e");try{var rig=SynthRig(go,"alpha_creature/q",quadRig);
                    ContractRoundTrip(SynthContract("alpha_creature/q",true,quad),out var ok);
                    return Expect(ok.Validate(rig,new AlphaClip[0],false).Any(x=>x.StartsWith("CONTRACT_ACTION_REGISTRY_MISMATCH")),"action mode without registry accepted");}
                finally{UnityEngine.Object.DestroyImmediate(go);}
            });
            Add("entity_contract_rejects_part_set_anchor_and_mirror_errors",()=>
            {
                var go=new GameObject("e");try{
                    var rig=SynthRig(go,"alpha_creature/q",quadRig.Where(p=>p.Item1!="tail").ToArray());
                    ContractRoundTrip(SynthContract("alpha_creature/q",false,quad),out var c);
                    if(!c.Validate(rig,new AlphaClip[0],false).Any(x=>x.StartsWith("CONTRACT_PART_SET_MISMATCH")))return "missing part accepted";
                    var bad=quad.Select(p=>p.Item1=="hand_r"?("hand_r","fore_limb","foot_l","center"):p).ToArray();
                    var rig2=SynthRig(new GameObject("e2"){hideFlags=HideFlags.None},"alpha_creature/q",quadRig);
                    try{ContractRoundTrip(SynthContract("alpha_creature/q",false,bad),out var c2);
                        if(!c2.Validate(rig2,new AlphaClip[0],false).Any(x=>x.StartsWith("CONTRACT_MIRROR_PAIR_BROKEN")))return "broken mirror pair accepted";
                        var wrongAnchor=quad.Select(p=>p.Item1=="tail"?("tail","tail","","center"):p).ToArray();
                        ContractRoundTrip(SynthContract("alpha_creature/q",false,wrongAnchor),out var c3);
                        return Expect(c3.Validate(rig2,new AlphaClip[0],false).Any(x=>x.StartsWith("CONTRACT_ANCHOR_MISMATCH")),"wrong anchor accepted");}
                    finally{UnityEngine.Object.DestroyImmediate(rig2.gameObject);}}
                finally{UnityEngine.Object.DestroyImmediate(go);}
            });
            Add("entity_contract_rejects_foreign_rig",()=>
            {
                var go=new GameObject("e");try{var rig=SynthRig(go,"alpha_creature/other",quadRig);
                    ContractRoundTrip(SynthContract("alpha_creature/q",false,quad),out var c);
                    return Expect(c.Validate(rig,new AlphaClip[0],false).Any(x=>x.StartsWith("CONTRACT_RIG_MISMATCH")),"foreign rig accepted");}
                finally{UnityEngine.Object.DestroyImmediate(go);}
            });
            Add("controller_locomotion_only_inits_without_actions_and_refuses_requests",()=>
            {
                var go=ControllerRig(out var c);
                try{
                    go.GetComponent<AlphaClipPlayer>().clipAssets=new TextAsset[0];c.requiredClips=new string[0];
                    c.locomotionOnly=true;c.actionsBlockedReason="NO_VERIFIED_ACTION_TRIGGER (test)";
                    if(!c.Init())return string.Join(";",c.Problems);
                    if(c.PlayAction("action_001")||c.PlayNativeAction("native_057"))return "locomotion-only controller accepted an action";
                    for(int i=0;i<30;i++)c.Step(1f/60);
                    return Expect(c.Problems.Count==0&&c.Phase==AlphaControllerPhase.Locomotion&&!c.InAction,"locomotion-only controller left locomotion: "+string.Join(";",c.Problems));
                }finally{UnityEngine.Object.DestroyImmediate(go);}
            });
            Add("controller_locomotion_only_rejects_action_library_and_missing_reason",()=>
            {
                var go=ControllerRig(out var c);
                try{
                    c.requiredClips=new string[0];c.locomotionOnly=true;c.actionsBlockedReason="x";
                    if(c.Init()||!c.Problems.Any(p=>p.StartsWith("CONTROLLER_LOCOMOTION_ONLY_HAS_ACTIONS")))return "action library accepted in locomotion-only mode";
                    var go2=ControllerRig(out var c2);
                    try{go2.GetComponent<AlphaClipPlayer>().clipAssets=new TextAsset[0];c2.requiredClips=new string[0];c2.locomotionOnly=true;
                        return Expect(!c2.Init()&&c2.Problems.Contains("CONTROLLER_LOCOMOTION_ONLY_WITHOUT_REASON"),"silent locomotion-only mode accepted");}
                    finally{UnityEngine.Object.DestroyImmediate(go2);}
                }finally{UnityEngine.Object.DestroyImmediate(go);}
            });
            Add("controller_missing_required_action_fails_closed",()=>
            {
                var go=ControllerRig(out var c);
                try {c.requiredClips=new[]{"action_999"};return Expect(!c.Init()&&!c.Initialized,"missing action accepted");}
                finally {UnityEngine.Object.DestroyImmediate(go);}
            });
            Add("controller_empty_library_fails_closed",()=>
            {
                var go=ControllerRig(out var c);
                try {go.GetComponent<AlphaClipPlayer>().clipAssets=new TextAsset[0];return Expect(!c.Init(),"empty library accepted");}
                finally {UnityEngine.Object.DestroyImmediate(go);}
            });
            Add("controller_rejects_invalid_clock",()=>
            {
                var go=ControllerRig(out var c);
                try {if(!c.Init())return string.Join(";",c.Problems);c.Step(float.NaN);return Expect(c.Problems.Contains("CONTROLLER_INVALID_CLOCK"),"NaN clock accepted");}
                finally {UnityEngine.Object.DestroyImmediate(go);}
            });
            foreach(int fps in new[]{30,60,120})
            {
                int rate=fps;
                Add("controller_full_cycle_single_clock_"+rate,()=>
                {
                    var go=ControllerRig(out var c);
                    try
                    {
                        if(!c.Init()||!c.PlayAction("action_001"))return string.Join(";",c.Problems);
                        float dt=1f/rate;int n=0;bool sawAction=false,sawRecovery=false;
                        while(c.InAction && ++n<rate*3)
                        {
                            if(c.Phase==AlphaControllerPhase.Action)
                            {
                                float before=c.actions.Cursor;c.Step(dt);
                                if(Mathf.Abs(c.actions.Cursor-before-dt*1000f)>1e-3f)return "action clock advanced twice or wrong units";
                                sawAction=true;
                            }
                            else c.Step(dt);
                            sawRecovery|=c.Phase==AlphaControllerPhase.Recovery;
                        }
                        if(c.InAction||!sawAction||!sawRecovery)return "sequence incomplete";
                        if(c.Problems.Count>0)return string.Join(";",c.Problems);
                        if(go.GetComponent<AlphaRig>().parts[0].localPosition.magnitude>1e-4f)return "did not return to idle";
                        float expected=c.entrySeconds+.5f+c.exitSeconds;
                        return Expect(Mathf.Abs(n*dt-expected)<=4*dt,$"wall time differs {n*dt} expected~{expected}");
                    }
                    finally {UnityEngine.Object.DestroyImmediate(go);}
                });
            }
            Add("alpha_translation_maps_y_z_swap", () =>
            {
                var u = AlphaSpace.ToUnity(AlphaSpace.Translate(new Vector3(1, 2, 3)));
                return Expect(Near(u.GetColumn(3), new Vector3(1, 3, 2)), $"got {u.GetColumn(3)}");
            });
            Add("alpha_unity_roundtrip_exact", () =>
            {
                var a = AlphaSpace.Mul(AlphaSpace.Mul(RowScale(2, 2, 2), RowRotZ(37)), AlphaSpace.Translate(new Vector3(4, -5, 6)));
                return Expect(AlphaSpace.MaxAbsDiff(AlphaSpace.ToAlpha(AlphaSpace.ToUnity(a)), a) < 1e-6f, "roundtrip differs");
            });
            Add("alpha_x_axis_turns_to_forward", () =>
            {
                // Alpha row-vector RotZ(90): x axis -> +Y (Alpha forward) -> Unity +Z
                var u = AlphaSpace.ToUnity(RowRotZ(90));
                return Expect(Near(u.MultiplyVector(Vector3.right), Vector3.forward), $"got {u.MultiplyVector(Vector3.right)}");
            });
            Add("decompose_keeps_mirror_on_x", () =>
            {
                var a = AlphaSpace.Mul(RowScale(-1.5f, 1.5f, 1.5f), RowRotZ(20));
                var u = AlphaSpace.ToUnity(a);
                if (!AlphaSpace.Decompose(u, out var p, out var r, out var s)) return "decompose failed";
                var back = Matrix4x4.TRS(p, r, s);
                float d = 0; for (int i = 0; i < 16; i++) d = Mathf.Max(d, Mathf.Abs(back[i] - u[i]));
                return Expect(s.x < 0 && d < 1e-5f, $"scale={s} diff={d}");
            });
            Add("decompose_rejects_shear", () =>
            {
                var u = Matrix4x4.identity; u[0, 1] = 0.5f;
                return Expect(!AlphaSpace.Decompose(u, out _, out _, out _), "shear accepted");
            });
            Add("parse_rejects_wrong_rig", () =>
            {
                var p = new List<string>();
                var c = AlphaClip.Parse(ClipJson("other_rig", new[] { "body" }, new[] { AlphaSpace.Translate(Vector3.zero) }, new[] { 0f }), AlphaRig.RigId, p);
                return Expect(c == null && p.Exists(x => x.StartsWith("CLIP_RIG_MISMATCH")), string.Join(";", p));
            });
            Add("parse_rejects_bad_key_size", () =>
            {
                var p = new List<string>();
                var c = AlphaClip.Parse(ClipJson(AlphaRig.RigId, new[] { "body", "head" }, new[] { AlphaSpace.Translate(Vector3.zero) }, new[] { 0f }), AlphaRig.RigId, p);
                return Expect(c == null && p.Exists(x => x.StartsWith("CLIP_KEY_SIZE")), string.Join(";", p));
            });
            Add("parse_rejects_nan", () =>
            {
                var m = AlphaSpace.Translate(Vector3.zero); m[13] = float.NaN;
                var p = new List<string>();
                var json = ClipJson(AlphaRig.RigId, new[] { "body" }, new[] { m }, new[] { 0f });
                var c = AlphaClip.Parse(json, AlphaRig.RigId, p);
                return Expect(c == null && p.Count > 0, "NaN accepted");
            });
            Add("parse_rejects_non_increasing_keys", () =>
            {
                var p = new List<string>();
                var k = AlphaSpace.Translate(Vector3.zero);
                var c = AlphaClip.Parse(ClipJson(AlphaRig.RigId, new[] { "body" }, new[] { k, k }, new[] { 5f, 5f }), AlphaRig.RigId, p);
                return Expect(c == null && p.Exists(x => x.StartsWith("CLIP_KEYS_NOT_INCREASING")), string.Join(";", p));
            });
            Add("parse_rejects_shear_matrix", () =>
            {
                var m = AlphaSpace.Translate(Vector3.zero); m[1] = 0.7f;
                var p = new List<string>();
                var c = AlphaClip.Parse(ClipJson(AlphaRig.RigId, new[] { "body" }, new[] { m }, new[] { 0f }), AlphaRig.RigId, p);
                return Expect(c == null && p.Exists(x => x.StartsWith("CLIP_NOT_TRS")), string.Join(";", p));
            });
            Add("sample_midpoint_and_clamp", () =>
            {
                var p = new List<string>();
                var c = AlphaClip.Parse(ClipJson(AlphaRig.RigId, new[] { "body" },
                    new[] { AlphaSpace.Translate(Vector3.zero), AlphaSpace.Translate(new Vector3(10, 0, 0)) }, new[] { 0f, 100f }), AlphaRig.RigId, p);
                if (c == null) return string.Join(";", p);
                var o = new PartPose[1];
                c.Sample(50f, o); if (!Near(o[0].position, new Vector3(5, 0, 0))) return $"mid {o[0].position}";
                c.Sample(500f, o); return Expect(Near(o[0].position, new Vector3(10, 0, 0)), $"clamp {o[0].position}");
            });
            Add("sample_loop_wraps_last_to_first", () =>
            {
                var p = new List<string>();
                var c = AlphaClip.Parse(ClipJson(AlphaRig.RigId, new[] { "body" },
                    new[] { AlphaSpace.Translate(Vector3.zero), AlphaSpace.Translate(new Vector3(10, 0, 0)) }, new[] { 0f, 1f }, true, 2f, "walk_phase"), AlphaRig.RigId, p);
                if (c == null) return string.Join(";", p);
                var o = new PartPose[1];
                c.Sample(1.5f, o); if (!Near(o[0].position, new Vector3(5, 0, 0))) return $"wrap {o[0].position}";
                c.Sample(2.5f, o); return Expect(Near(o[0].position, new Vector3(5, 0, 0)), $"repeat {o[0].position}");
            });
            Add("anchor_rules", () =>
            {
                var a = AlphaRig.Anchor("center", new Vector3Int(10, 8, 10), false);
                var b = AlphaRig.Anchor("weapon", new Vector3Int(3, 13, 37), false);
                var c = AlphaRig.Anchor("weapon", new Vector3Int(3, 13, 37), true);
                return Expect(Near(a, new Vector3(5, 4, 5)) && Near(b, new Vector3(1.5f, 6.5f, 4f)) && Near(c, new Vector3(1.5f, 6.5f, 18.5f)), $"{a} {b} {c}");
            });
            // ---- universal creature rig (synthetic; TEST_DATA_NOT_ALPHA) ----
            Add("anchor_tail_rule", () =>
            {
                var t = AlphaRig.Anchor("tail", new Vector3Int(4, 5, 10), true);
                bool threw = false;
                try { AlphaRig.Anchor("unknown", new Vector3Int(4, 5, 10), true); } catch (ArgumentException) { threw = true; }
                return Expect(Near(t, new Vector3(2f, 5f, 5f)) && threw, $"tail {t} unknownThrew={threw}");
            });
            Add("creature_rig_accepts_own_family_rejects_others", () =>
            {
                const string fam = "alpha_creature/0123456789";
                var ok = new List<string>(); var bad = new List<string>(); var hum = new List<string>();
                var k = new[] { AlphaSpace.Translate(Vector3.zero) };
                var c1 = AlphaClip.Parse(ClipJson(fam, new[] { "tail" }, k, new[] { 0f }), fam, ok);
                var c2 = AlphaClip.Parse(ClipJson("alpha_creature/ffffffffff", new[] { "tail" }, k, new[] { 0f }), fam, bad);
                var c3 = AlphaClip.Parse(ClipJson(AlphaRig.RigId, new[] { "body" }, k, new[] { 0f }), fam, hum);
                return Expect(c1 != null && c2 == null && c3 == null && bad.Exists(x => x.StartsWith("CLIP_RIG_MISMATCH"))
                              && hum.Exists(x => x.StartsWith("CLIP_RIG_MISMATCH")), $"{c1 != null} {c2 == null} {c3 == null} {string.Join(";", ok)}");
            });
            Add("creature_rig_tail_roundtrip", () =>
            {
                var go = new GameObject("t"); go.transform.localScale = Vector3.one * 0.1f;
                try
                {
                    var rig = go.AddComponent<AlphaRig>();
                    rig.rigId = "alpha_creature/0123456789";
                    rig.partNames = new[] { "tail" };
                    rig.parts = new[] { new GameObject("p").transform }; rig.parts[0].SetParent(go.transform, false);
                    rig.meshes = new[] { new GameObject("mesh").transform }; rig.meshes[0].SetParent(rig.parts[0], false);
                    rig.sizes = new[] { new Vector3Int(4, 5, 10) };
                    rig.anchors = new[] { "tail" };
                    rig.SetWeaponOnBack(true);
                    var j = AlphaSpace.Mul(AlphaSpace.Mul(RowScale(1.1f, 1.1f, 1.1f), RowRotX(-30)), AlphaSpace.Translate(new Vector3(0, -8, -1)));
                    var p = new List<string>();
                    var c = AlphaClip.Parse(ClipJson(rig.rigId, rig.partNames, new[] { j }, new[] { 0f }), rig.rigId, p);
                    if (c == null) return string.Join(";", p);
                    var o = new PartPose[1]; c.Sample(0, o); rig.Apply(o, new[] { 0 });
                    float d = AlphaSpace.MaxAbsDiff(rig.ReadAlphaModelMatrix(0), AlphaSpace.Mul(AlphaSpace.Translate(-rig.AnchorOf(0)), j));
                    return Expect(d < 1e-4f && Near(rig.AnchorOf(0), new Vector3(2, 5, 5)), $"max diff {d} anchor {rig.AnchorOf(0)}");
                }
                finally { UnityEngine.Object.DestroyImmediate(go); }
            });
            Add("voxel_mesh_counts_exposed_faces", () =>
            {
                var bytes = new byte[12 + 2 * 3];
                BitConverter.GetBytes(2).CopyTo(bytes, 0); BitConverter.GetBytes(1).CopyTo(bytes, 4); BitConverter.GetBytes(1).CopyTo(bytes, 8);
                bytes[12] = 200; bytes[15] = 100;
                var r = VoxelMesh.Build(bytes, "t");
                return Expect(r.problem == null && r.faces == 10 && r.mesh.vertexCount == 40, $"faces={r.faces} problem={r.problem}");
            });
            Add("voxel_mesh_rejects_bad_length", () =>
            {
                var bytes = new byte[12 + 5];
                BitConverter.GetBytes(2).CopyTo(bytes, 0); BitConverter.GetBytes(1).CopyTo(bytes, 4); BitConverter.GetBytes(1).CopyTo(bytes, 8);
                return Expect(VoxelMesh.Build(bytes, "t").problem != null, "accepted");
            });
            Add("voxel_mesh_axis_order", () =>
            {
                // one voxel at grid (x=0, y=1 depth, z=0) in a 1x2x1 grid -> Unity z in [1,2]
                var bytes = new byte[12 + 2 * 3];
                BitConverter.GetBytes(1).CopyTo(bytes, 0); BitConverter.GetBytes(2).CopyTo(bytes, 4); BitConverter.GetBytes(1).CopyTo(bytes, 8);
                bytes[12 + 3] = 255;
                var r = VoxelMesh.Build(bytes, "t");
                return Expect(r.mesh != null && Mathf.Approximately(r.mesh.bounds.min.z, 1f) && Mathf.Approximately(r.mesh.bounds.max.y, 1f), $"{r.mesh?.bounds}");
            });
            Add("rig_scene_roundtrip_incl_mirror", () =>
            {
                var go = new GameObject("t"); go.transform.localScale = Vector3.one * 0.1f;
                try
                {
                    var rig = go.AddComponent<AlphaRig>();
                    rig.partNames = new[] { "hand_r", "weapon" };
                    rig.parts = new Transform[2]; rig.meshes = new Transform[2];
                    rig.sizes = new[] { new Vector3Int(5, 6, 4), new Vector3Int(3, 13, 37) };
                    rig.anchors = new[] { "center", "weapon" };
                    for (int i = 0; i < 2; i++)
                    {
                        rig.parts[i] = new GameObject("p" + i).transform; rig.parts[i].SetParent(go.transform, false);
                        rig.meshes[i] = new GameObject("mesh").transform; rig.meshes[i].SetParent(rig.parts[i], false);
                    }
                    rig.SetWeaponOnBack(false);
                    var j0 = AlphaSpace.Mul(AlphaSpace.Mul(RowScale(-1.1f, 1.1f, 1.1f), RowRotZ(33)), AlphaSpace.Translate(new Vector3(3, -2, 7)));
                    var j1 = AlphaSpace.Mul(RowRotX(-70), AlphaSpace.Translate(new Vector3(5, 1, -2)));
                    var p = new List<string>();
                    var c = AlphaClip.Parse(ClipJson(AlphaRig.RigId, rig.partNames, new[] { Concat(j0, j1) }, new[] { 0f }), AlphaRig.RigId, p);
                    if (c == null) return string.Join(";", p);
                    var o = new PartPose[2]; c.Sample(0, o); rig.Apply(o, new[] { 0, 1 });
                    float d = 0;
                    for (int i = 0; i < 2; i++)
                        d = Mathf.Max(d, AlphaSpace.MaxAbsDiff(rig.ReadAlphaModelMatrix(i), AlphaSpace.Mul(AlphaSpace.Translate(-rig.AnchorOf(i)), i == 0 ? j0 : j1)));
                    return Expect(d < 1e-4f, $"max diff {d}");
                }
                finally { UnityEngine.Object.DestroyImmediate(go); }
            });
            Add("rig_validate_detects_nonflat", () =>
            {
                var go = new GameObject("t");
                try
                {
                    var rig = go.AddComponent<AlphaRig>();
                    var other = new GameObject("o").transform; other.SetParent(go.transform);
                    rig.partNames = new[] { "body" }; rig.parts = new[] { new GameObject("p").transform };
                    rig.parts[0].SetParent(other);
                    rig.meshes = new[] { new GameObject("m").transform }; rig.meshes[0].SetParent(rig.parts[0]);
                    rig.sizes = new[] { Vector3Int.one }; rig.anchors = new[] { "center" };
                    return Expect(rig.Validate().Exists(x => x.StartsWith("RIG_PART_NOT_FLAT")), "not detected");
                }
                finally { UnityEngine.Object.DestroyImmediate(go); }
            });
            Add("look_pitch_matches_alpha_formula", () =>
            {
                var go = new GameObject("t");
                try
                {
                    var rig = go.AddComponent<AlphaRig>();
                    rig.partNames = new[] { "head" };
                    rig.parts = new[] { new GameObject("part_head").transform }; rig.parts[0].SetParent(go.transform, false);
                    rig.meshes = new[] { new GameObject("mesh").transform }; rig.meshes[0].SetParent(rig.parts[0], false);
                    rig.sizes = new[] { new Vector3Int(18, 16, 16) }; rig.anchors = new[] { "center" };
                    float s = 1.01f, pitch = -7.5f;
                    var j = AlphaSpace.Mul(AlphaSpace.Mul(RowScale(s, s, s), RowRotZ(25)), AlphaSpace.Translate(new Vector3(0.5f, 1, 9)));
                    var player = go.AddComponent<AlphaClipPlayer>();
                    var asset = new TextAsset(ClipJson(AlphaRig.RigId, rig.partNames, new[] { j }, new[] { 0f }));
                    player.clipAssets = new[] { asset };
                    player.LoadClips();
                    player.lookPitchDeg = pitch;
                    if (!player.Play("synthetic")) return string.Join(";", player.Problems);
                    var exp = AlphaSpace.Mul(AlphaSpace.Mul(AlphaSpace.Mul(AlphaSpace.Translate(new Vector3(0, 0, 3f / s)), RowRotX(pitch)), AlphaSpace.Translate(new Vector3(0, 0, -3f / s))), j);
                    exp = AlphaSpace.Mul(AlphaSpace.Translate(-rig.AnchorOf(0)), exp);
                    float d = AlphaSpace.MaxAbsDiff(rig.ReadAlphaModelMatrix(0), exp);
                    return Expect(d < 1e-4f, $"pitch layer differs from Alpha formula by {d}");
                }
                finally { UnityEngine.Object.DestroyImmediate(go); }
            });
        }

        public static void Run()
        {
            Register();
            var sb = new StringBuilder();
            int failed = 0;
            foreach (var (name, run) in Cases)
            {
                string err;
                try { err = run(); } catch (Exception e) { err = "EXCEPTION " + e.Message; }
                if (err != null) failed++;
                sb.AppendLine($"{(err == null ? "PASS" : "FAIL")} {name}{(err == null ? "" : " :: " + err)}");
            }
            var verdict = failed == 0 ? "PASS" : "FAIL";
            sb.Insert(0, $"SELFTEST_RESULT={verdict} cases={Cases.Count} failed={failed} data=TEST_DATA_NOT_ALPHA\n");
            var outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "TransferOutput"));
            Directory.CreateDirectory(outDir);
            File.WriteAllText(Path.Combine(outDir, "selftest.txt"), sb.ToString());
            Debug.Log(sb.ToString());
            if (failed != 0) throw new Exception($"SELFTEST_RESULT={verdict} failed={failed}");
        }
    }
}
