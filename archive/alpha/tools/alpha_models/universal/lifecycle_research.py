"""Survey native input helpers for the existing one-human action registry; no new pose bake.

This is deliberately separate from executable Unity profiles. A helper result does not prove
that the full game can launch that action in this equipment/class/resource/world context.
"""
import argparse,json,struct
from pathlib import Path
from tools.alpha_models.universal import alpha_oracle,anim_states,native_lifecycle as nl
from tools.alpha_models.universal.action_baker import local_output,DEFAULT_EXE,DEFAULT_SIZES
from tools.alpha_models.universal.lifecycle_pipeline import specimen,at


def research(out):
    out=local_output(out);oracle=alpha_oracle.load(str(DEFAULT_EXE),str(DEFAULT_SIZES))
    registry=json.loads(Path('.scratch/animsys/human_actions_extended/actions.json').read_text())
    ids=sorted({e['action'] for e in registry['actions']});rows=[];variants=[]
    for klass in (1,4):
        c,_=specimen(oracle,klass)
        for action in ids:
            row={'class':klass,'action':action,'scope':'primary helper + player idle-tail only; launch/world gates PARTIAL'}
            try:
                timing=anim_states.action_timings(oracle.image,at(c,action,0),action,(3,15))
                row['renderer_timings']=timing
                times={0,10000,10001,20000}
                for value in timing.values():
                    if isinstance(value,int) and 0<value<=30000:times.update((value-1,value,value+1))
                row['samples']=[]
                for t in sorted(times):
                    raw=at(c,action,t);primary=nl.advance_held(oracle.image,raw);tail=nl.idle_tail(oracle.image,raw)
                    held=nl.idle_tail(oracle.image,raw,True)
                    row['samples'].append({'timer_ms':t,'primary_selected':primary['selected'],'primary_action':primary['action'],
                        'primary_timer_ms':primary['timer_ms'],'released_action':tail['action'],'released_timer_ms':tail['timer_ms'],
                        'secondary_held_action':held['action']})
                row['ready_at_zero']=nl.ready_to_start(oracle.image,at(c,action,0))
                row['helper_evidence']='VERIFIED_ALPHA';row['full_lifecycle']='PARTIAL'
            except Exception as e:row['helper_evidence']='PARTIAL';row['reason']=f'{type(e).__name__}: {e}'
            rows.append(row)
    for klass in (1,3,4):
        for spec in (0,1):
            for subtype in (15,16,17):
                c,_=specimen(oracle,klass);c[0x141]=spec;anim_states.equip(c,0xaa8,3,subtype)
                try:variants.append({'class':klass,'spec':spec,'weapon_subtype':subtype,
                    'idle_primary_action':nl.select_attack(oracle.image,at(c,0,10000)),
                    '57_early_next':nl.select_attack(oracle.image,at(c,57,0)),
                    '57_late_next':nl.select_attack(oracle.image,at(c,57,20000)),
                    'scope':'native selector only; not proof of weapon geometry or launch eligibility'})
                except Exception as e:variants.append({'class':klass,'spec':spec,'weapon_subtype':subtype,'reason':str(e)})
    result={'RESEARCH_RESULT':'PASS','action_ids':len(ids),'contexts':len(rows),
        'verified_helpers':sum(r['helper_evidence']=='VERIFIED_ALPHA' for r in rows),
        'rows':rows,'equipment_selector':variants,
        'limits':'No input GUI/resource/world update emulation. Sampled helper rules are not complete ability lifetimes.'}
    (out/'input_survey.json').write_text(json.dumps(result,indent=1),encoding='utf8')
    print('RESEARCH_RESULT=PASS',result['contexts'],'contexts;',result['verified_helpers'],'verified helper contexts')


if __name__=='__main__':
    ap=argparse.ArgumentParser(description=__doc__.split('\n')[0]);ap.add_argument('--out',default='.scratch/lifecycle/research')
    research(ap.parse_args().out)
