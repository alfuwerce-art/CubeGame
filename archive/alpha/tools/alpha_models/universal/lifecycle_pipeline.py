"""One-human lifecycle evidence/POC, layered on the established oracle/baker.

Generated rules, golden cases and native poses stay in .scratch. Native helper/input-tail
execution proves this limited input context, not all GUI/resource/world/AI launch conditions.
"""
import argparse,collections,hashlib,json,shutil,struct
from pathlib import Path
from concurrent.futures import ProcessPoolExecutor,as_completed
from tools.alpha_models.universal import alpha_oracle,anim_states,creature_rig as cr,native_lifecycle as nl
from tools.alpha_models.universal.action_baker import local_output,DEFAULT_EXE,DEFAULT_SIZES,joints,validate_bake


def specimen(oracle,klass):
    c,app=oracle.creature(2);c[0x5c]|=1;c[0x140]=klass;c[0x141]=0
    anim_states.equip(c,0xaa8,3,15)
    return c,app


def at(c,action,t):
    out=bytearray(c);out[0x68]=action;struct.pack_into('<i',out,0x6c,t);return out


def first_true(fn,limit=20000):
    if fn(0):return 0
    if not fn(limit):return -1
    lo,hi=0,limit
    while hi-lo>1:
        mid=(lo+hi)//2
        if fn(mid):hi=mid
        else:lo=mid
    return hi


def rules_for(oracle,klass):
    c,app=specimen(oracle,klass);rules=[];golden=[]
    for action in (0,57,58,67,79):
        current=at(c,action,0)
        timing=anim_states.action_timings(oracle.image,current,action,(3,15))['attack_timing']
        threshold=first_true(lambda t:nl.advance_held(oracle.image,at(c,action,t))['selected'])
        initial=nl.select_attack(oracle.image,current);late=nl.select_attack(oracle.image,at(c,action,20000))
        select_change=first_true(lambda t:nl.select_attack(oracle.image,at(c,action,t))!=initial) if initial!=late else -1
        idle=-1 if action==0 else first_true(lambda t:nl.idle_tail(oracle.image,at(c,action,t))['action']!=action)
        rules.append({'action':action,'primaryAtMs':threshold,'primaryNext':initial,'primaryLateNext':late,
                      'selectChangeMs':select_change,'idleAtMs':idle,'strikeEndMs':timing,
                      'interruptReadyAtZero':nl.ready_to_start(oracle.image,current)})
        times={0,1,10000,10001,20000,timing}
        for b in (threshold,select_change,idle):
            if b>=0:times.update((max(0,b-1),b,b+1))
        for t in sorted(times):
            for primary in (False,True):
                for secondary in (False,True):
                    for blocked in (False,True):
                        raw=at(c,action,t);struct.pack_into('<i',raw,0x128,int(blocked))
                        selected=False
                        if primary:
                            result=nl.advance_held(oracle.image,raw);raw=result['creature'];selected=result['selected']
                        result=nl.idle_tail(oracle.image,raw,secondary)
                        golden.append({'profile':f'class_{klass}_2h','action':action,'timerMs':t,'primary':primary,
                            'secondary':secondary,'blocked':blocked,'expectedAction':result['action'],
                            'expectedTimerMs':result['timer_ms'],'expectedSelected':selected})
    return {'id':f'class_{klass}_2h','classId':klass,'weaponSubtype':15,'offset70':0,
            'evidence':'VERIFIED_ALPHA','scope':'native primary helper + input-tail; no GUI, resources, AI or world effects',
            'rules':rules},golden


def native_clip_task(payload):
    out,action=payload;oracle=alpha_oracle.load(str(DEFAULT_EXE),str(DEFAULT_SIZES))
    c,app=specimen(oracle,1 if action!=79 else 4);res=cr.resolve(2,app,equipment=('weapon',))
    if action==79:
        # A legal latched standing state can have identical poses. Native input-tail excludes 79.
        b=anim_states.bake_action(oracle,2,79,weapon=(3,15),creature_bytes=c,duration_ms=16,tail_ms=1800)
        first=b['frames'][0];b['frames']=[first,{**first,'t':16}]
        clip=anim_states.baked_clip(b,2,res.family_id,'held_079',res.parts)
        clip['source'].update(category='HELD_STATE',action_end_ms=0,recovery_start_ms=0,
            classification_evidence='class-4 input binding; native input-tail keeps 79 beyond 10000ms; standing pose is static',
            activation_evidence='PARTIAL: class binding and ready predicate known; full resource/world gates not ported')
    else:
        expiry=first_true(lambda t:nl.idle_tail(oracle.image,at(c,action,t))['action']!=action)
        if expiry<1:raise ValueError('NATIVE_IDLE_EXIT_UNPROVEN')
        b=anim_states.bake_action(oracle,2,action,weapon=(3,15),creature_bytes=c,duration_ms=expiry-1,
                                tail_ms=1800,native_timer_carry=True)
        clip=anim_states.baked_clip(b,2,res.family_id,f'native_{action:03d}',res.parts)
        rest,_=joints(oracle,b['idle'],2);clip['validation']=validate_bake(b,res.parts,rest)
        clip['source'].update(category='NATIVE_LIFECYCLE',native_idle_at_ms=expiry,
                             timer_policy='native input-tail preserves timer on idle; renderer window is separate',
                             grip_end_ms=b['timings']['attack_timing'])
    rest,sizes=joints(oracle,b['idle'],2)
    for p in clip['parts']:p['captured_size']=sizes[p['name']]
    clip['source'].update(action=action,context_class=c[0x140],two_handed_attack=action in (57,58,67),
                          runtime_capture=False,exe_sha256=hashlib.sha256(Path(DEFAULT_EXE).read_bytes()).hexdigest())
    dest=Path(out)/(clip['name']+'.alphaclip.json');dest.write_text(json.dumps(clip),encoding='utf8')
    return {'action':action,'clip':clip['name'],'file':dest.name,'status':'EMULATED_ALPHA_CODE',
            'category':clip['source']['category'],'sha256':hashlib.sha256(dest.read_bytes()).hexdigest()}


def build(out):
    out=local_output(out);oracle=alpha_oracle.load(str(DEFAULT_EXE),str(DEFAULT_SIZES))
    old=Path('.scratch/animsys/human_actions_extended');manifest=json.loads((old/'actions.json').read_text())
    manifest['actions']=[e for e in manifest['actions'] if e['action'] not in (22,23,26,71,72,79)]
    for e in manifest['actions']:
        if e['status']=='EMULATED_ALPHA_CODE':shutil.copyfile(old/e['file'],out/e['file'])
    dynamic=Path('.scratch/lifecycle/dynamic');dm=json.loads((dynamic/'actions.json').read_text())
    if dm['BAKE_RESULT']!='PASS':raise ValueError('DYNAMIC_BAKE_NOT_FINISHED')
    for e in dm['actions']:
        if e['status']!='EMULATED_ALPHA_CODE':raise ValueError(f'DYNAMIC_NOT_VERIFIED {e}')
        shutil.copyfile(dynamic/e['file'],out/e['file']);manifest['actions'].append(e)
    shutil.copyfile(dynamic/'weapon_reference.json',out/'weapon_reference.json')
    profiles=[];cases=[]
    for klass in (1,4):
        profile,golden=rules_for(oracle,klass);profiles.append(profile);cases.extend(golden)
    spec={'format':'cubegame.native_lifecycle/1','rig':manifest['rig'],'profiles':profiles,
          'bindings':{'57':'native_057','58':'native_058','67':'native_067','79':'held_079'},
          'golden':cases,'status':'EMULATED_ALPHA_CODE','source':'native 445410,445020,49C272..49C2E6; context offset70=0, item1020=0'}
    (out/'lifecycle.json').write_text(json.dumps(spec,indent=1),encoding='utf8')
    manifest['lifecycle_sha256']=hashlib.sha256((out/'lifecycle.json').read_bytes()).hexdigest()
    with ProcessPoolExecutor(max_workers=4) as pool:
        for f in as_completed([pool.submit(native_clip_task,(str(out),a)) for a in (57,58,67,79)]):
            entry=f.result();manifest['actions'].append(entry);print('NATIVE_CLIP',entry['clip'],flush=True)
    manifest['actions'].sort(key=lambda e:(e['action'],e['clip']))
    manifest['actions']=[e if e['action'] not in (51,80) else {**e,'status':'BLOCKED','reason':
       'action 51: AI branch for kind 0x65 (Dragon), physics-driven; no humanoid launch proved' if e['action']==51 else
       'action 80: consumable context at +0x1e8 and its model missing; cooldown 3000/10000 is not attack_timing'} for e in manifest['actions']]
    manifest['counts']=dict(collections.Counter(e['status'] for e in manifest['actions']))
    supported=[e for e in manifest['actions'] if e['status']=='EMULATED_ALPHA_CODE']
    manifest['coverage']={'unique_pose_actions':len({e['action'] for e in supported}),
        'motion_actions':sum(e.get('category','MOTION')=='MOTION' for e in supported),
        'held_states':sum(e.get('category')=='HELD_STATE' for e in supported),
        'native_lifecycle_variants':sum(e.get('category')=='NATIVE_LIFECYCLE' for e in supported),
        'note':'native variants overlap existing motion action IDs; pose support does not prove activation gates'}
    manifest['BAKE_RESULT']='PASS';manifest['weapon_reference_sha256']=hashlib.sha256((out/'weapon_reference.json').read_bytes()).hexdigest()
    (out/'actions.json').write_text(json.dumps(manifest,indent=1),encoding='utf8')
    print('LIFECYCLE_BUILD=PASS',manifest['counts'],'native_golden',len(cases),flush=True)


if __name__=='__main__':
    ap=argparse.ArgumentParser(description=__doc__.split('\n')[0]);ap.add_argument('--out',default='.scratch/lifecycle/library')
    build(ap.parse_args().out)
