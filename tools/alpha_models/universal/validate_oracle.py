"""Replay full pre-draw Alpha captures through the existing emulator, without changing inputs.

The reference is the actual uploaded part matrices, not the formula or baked clips. A PASS
validates the emulator on the observed actions only; unobserved actions remain EMULATED_ALPHA_CODE.
Outputs are local .scratch evidence. No Alpha launch or writes to the original installation.
"""
import argparse
import collections
import json
import struct
import math
from pathlib import Path
from tools.alpha_models.anim import mat4
from tools.alpha_models.universal import alpha_oracle, anim_states, drawmodel_emu as de
from tools.alpha_models.universal.action_baker import local_output, DEFAULT_EXE, DEFAULT_SIZES


def validate(captures, out, exe=DEFAULT_EXE, sizes=DEFAULT_SIZES, kind=2):
    out=Path(out);local_output(out.parent)
    oracle=alpha_oracle.load(str(exe),str(sizes))
    stats=collections.defaultdict(lambda:{'frames':0,'max_abs':0.0})
    problems=[];n=0;worst=0.0
    for path in captures:
        for idx,line in enumerate(Path(path).read_text(encoding='utf8').splitlines()):
            r=json.loads(line)
            if not r.get('creature_full') or r.get('kind') != kind:continue
            key=f"kind={r['kind']} state={r['state']} action={r['fields']['type_68']}"
            try:
                if r.get('buffs_empty') is False:raise ValueError('BUFF_LIST_NOT_CAPTURED')
                c=bytes.fromhex(r['creature_full']);th=bytes.fromhex(r['this_full'])
                s=anim_states.resolve_state(oracle.image,c[0x68],(c[0xaa8],c[0xaa9]),(c[0x990],c[0x991]),c[0x130])
                if s != struct.unpack_from('<i',th,8)[0]:raise ValueError(f'RESOLVER_MISMATCH {s}')
                em=de.Emulator(oracle.image,c,oracle.sizes,th)
                # Full dumps do not capture vecA/vecB pointed to by the call arguments. Their
                # fixed16 camera offset is recoverable from the captured placement translation.
                # Matching that INPUT is essential: centring a distant NPC at zero changes f32
                # rounding of every upload, amplified by inverse placement scale. No poses are fed back.
                camera=tuple(round(r['placement'][12+i]*65536)-struct.unpack_from('<q',c,0x10+8*i)[0] for i in range(2))
                actual=em.frame(dt_ms=r['dt_ms'],count_flags=r.get('count_flags',0),camera_xy_fixed=camera)
                if mat4.max_abs_diff(actual['placement'],r['placement'])>1e-5:
                    raise ValueError('CAMERA_PLACEMENT_RECONSTRUCTION_FAILED')
                cap={int(p['draw_site'],16):p['matrix'] for p in r['parts']}
                generated={p['draw_site']:p['matrix'] for p in actual['parts']}
                if set(cap)!=set(generated):raise ValueError('DRAW_SITE_SET_MISMATCH')
                if not all(math.isfinite(v) for matrices in (cap,generated) for m in matrices.values() for v in m):
                    raise ValueError('NONFINITE_MATRIX')
                pi=mat4.inverse_affine(actual['placement']);pci=mat4.inverse_affine(r['placement'])
                err=max(mat4.max_abs_diff(mat4.mul(generated[p],pi),mat4.mul(cap[p],pci)) for p in cap)
                if err>1e-4:raise ValueError(f'MATRIX_MISMATCH {err}')
                stats[key]['frames']+=1;stats[key]['max_abs']=max(stats[key]['max_abs'],err)
                worst=max(worst,err);n+=1
            except Exception as e:problems.append(f'{path}:{idx+1} {key} {type(e).__name__}: {e}')
    if n==0:problems.append('NO_CAPTURED_FRAMES')
    result={'ORACLE_VALIDATE':'PASS' if not problems else 'FAIL','frames':n,'max_abs':worst,
            'threshold':1e-4,'by_action_state':dict(stats),'problems':problems,
            'scope':'only captured action/state combinations; generated library remains EMULATED_ALPHA_CODE'}
    out.write_text(json.dumps(result,indent=1),encoding='utf8')
    print(json.dumps(result,indent=1))
    return 0 if not problems else 1


def main():
    ap=argparse.ArgumentParser(description=__doc__.split('\n')[0]);ap.add_argument('--capture',action='append',required=True)
    ap.add_argument('--out',required=True);ap.add_argument('--exe',default=str(DEFAULT_EXE));ap.add_argument('--sizes',default=str(DEFAULT_SIZES))
    a=ap.parse_args();return validate(a.capture,a.out,a.exe,a.sizes)

if __name__=='__main__':raise SystemExit(main())
