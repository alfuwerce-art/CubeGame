"""Bake ONE humanoid's explicit actions from the existing offline Alpha oracle.

No runtime inputs, source binaries, appearance data or baked poses are published. All generated
files MUST be under .scratch. This is a pose library, not authorization of gameplay abilities.
Unknown durations, missing parts, static actions and unsettled recovery are BLOCKED, not guessed.

python -m tools.alpha_models.universal.action_baker --job JOB --actions 57,67,58 --out .scratch/...
"""
from __future__ import annotations
import argparse
import collections
import hashlib
import json
import math
from pathlib import Path
from concurrent.futures import ProcessPoolExecutor, as_completed

from tools.alpha_models.anim import mat4
from tools.alpha_models.anim.alpha_clip import pose_error, decompose, compose
from tools.alpha_models.universal import alpha_oracle, anim_states as states, creature_rig as cr
from tools.alpha_models.universal import alpha_static, drawmodel_emu as de, param_walk as pw
from tools.alpha_models.universal import weapon_locomotion

ROOT = Path(__file__).resolve().parents[3]
DEFAULT_EXE = ROOT / '.scratch/alphatest2/Cube.exe'
DEFAULT_SIZES = ROOT / '.scratch/animsys/registry_sizes.json'


def local_output(path):
    p = Path(path).resolve()
    if not p.is_relative_to(ROOT / '.scratch'):
        raise ValueError('ALPHA_OUTPUT_MUST_BE_IN_SCRATCH')
    p.mkdir(parents=True, exist_ok=True)
    return p


def validate_bake(bake, parts, rest):
    """Independent structural/motion/end checks, in model voxels/degrees (not Unity units)."""
    frames = bake['frames']
    if len(frames) < 3 or frames[0]['t'] != 0 or frames[-1]['t'] <= bake['duration_ms']:
        raise ValueError('ACTION_INCOMPLETE_TIMELINE')
    peak_pos = peak_rot = 0.0
    prev = -1
    for f in frames:
        if f['t'] <= prev:
            raise ValueError('ACTION_TIME_NOT_INCREASING')
        prev = f['t']
        if f.get('unsupported_draw_sites'):raise ValueError('UNSUPPORTED_NATIVE_DRAW')
        if (set(f['joints'])-set(parts)) or (not bake.get('dynamic_parts') and set(f['joints']) != set(parts)):
            raise ValueError(f"ACTION_PART_SET_CHANGED t={f['t']} parts={list(f['joints'])}")
        for n in parts:
            if n not in f['joints']:continue
            m = f['joints'][n]
            if len(m) != 16 or not all(math.isfinite(x) for x in m):
                raise ValueError(f'ACTION_NONFINITE {n}')
            # decomposition additionally rejects singular/non-affine frames
            trs = decompose(m)
            if mat4.max_abs_diff(m, compose(*trs)) > .001:
                raise ValueError(f'ACTION_NOT_TRS {n}')
            pos, rot, _ = pose_error(m, rest[n])
            peak_pos, peak_rot = max(peak_pos, pos), max(peak_rot, rot)
    if set(frames[-1]['joints'])!=set(parts):raise ValueError('ACTION_RECOVERY_PARTS_NOT_RESTORED')
    end = [pose_error(frames[-1]['joints'][n], rest[n]) for n in parts]
    max_pos, max_rot = max(e[0] for e in end), max(e[1] for e in end)
    if max_pos > .001 or max_rot > .05:
        raise ValueError(f'ACTION_RECOVERY_NOT_SETTLED pos={max_pos} rot={max_rot}')
    if peak_pos < .001 and peak_rot < .05:
        raise ValueError('ACTION_STATIC')
    active=active_window_motion(frames,bake['duration_ms'])
    switches = []
    for a, b in zip(frames, frames[1:]):
        if a['state'] != b['state']:
            es = [pose_error(a['joints'][n], b['joints'][n]) for n in parts if n in a['joints'] and n in b['joints']]
            switches.append({'t': b['t'], 'jointly_visible_parts':len(es),
                'max_pos': max((e[0] for e in es),default=0), 'max_rot': max((e[1] for e in es),default=0)})
    return {'frames': len(frames), 'peak_pos_vox': peak_pos, 'peak_rot_deg': peak_rot,
            'end_pos_vox': max_pos, 'end_rot_deg': max_rot, 'raw_state_switches': switches, **active}


def active_window_motion(frames, duration_ms):
    """A static held pose plus a hand/back switch is not a verified moving action clip."""
    active=[f for f in frames if f['t']<=duration_ms]
    pos=rot=0.0;first={}
    for f in active[1:]:
        for n,m in f['joints'].items():
            baseline=first.setdefault(n,active[0]['joints'].get(n,m))
            e=pose_error(m,baseline);pos=max(pos,e[0]);rot=max(rot,e[1])
    if pos<=.001 and rot<=.05:
        raise ValueError('ACTION_STATIC_ACTIVE_WINDOW; held pose/input-dependent action not verified as animation')
    return {'active_motion_vox':pos,'active_rotation_deg':rot}


def joints(oracle, frame, kind):
    pi = mat4.inverse_affine(frame['placement'])
    out, sizes = {}, {}
    for p in frame['parts']:
        b = cr.BY_DRAW_SITE.get(p['draw_site'])
        size = oracle.sizes.get(p.get('model_index'))
        if b is None or size is None or b.anchor == 'unknown':
            raise ValueError(f"UNSUPPORTED_DRAW_PART {p['draw_site']}")
        a = cr.anchor(b.anchor, size, kind=kind, on_back=True)
        out[b.name] = mat4.mul(mat4.mul(mat4.translate(*a), p['matrix']), pi)
        sizes[b.name] = list(size)
    return out, sizes


def _bake_one(payload):
    exe, sizes, kind, seed, weapon, parts, family, exe_hash, dt_ms, out, action = payload
    oracle = alpha_oracle.load(str(exe), str(sizes))
    c, _ = oracle.creature(kind, seed)
    name = f'action_{action:03d}'
    entry = {'action': action, 'clip': name, 'status': 'BLOCKED'}
    dest = Path(out) / f'{name}.alphaclip.json'
    if dest.exists():
        dest.unlink()  # a previous successful clip must not mask a new refusal
    try:
        entry['state'] = states.resolve_state(oracle.image, action, weapon)
        entry['timings'] = states.action_timings(oracle.image, c, action, weapon)
        if not 1 <= entry['state'] <= 73:
            raise ValueError('NO_ACTION_DRAW_STATE')
        bake = states.bake_action(oracle, kind, action, weapon=weapon, seed=seed, dt_ms=dt_ms, tail_ms=1800,
                                 dynamic_parts=action in (22,23,26,71,72))
        rest, captured_sizes = joints(oracle, bake['idle'], kind)
        check = validate_bake(bake, parts, rest)
        clip = states.baked_clip(bake, kind, family, name, parts)
        for p in clip['parts']:
            p['captured_size'] = captured_sizes[p['name']]
        clip['source'].update({'action': action, 'exe_sha256': exe_hash, 'seed_variant': list(seed),
            'runtime_capture': False, 'two_handed_attack': action in (57,58,67),
            'duration_policy': 'native combat_getAttackTiming observation window; gameplay lifetime not inferred'})
        clip['validation'] = check
        dest.write_text(json.dumps(clip), encoding='utf8')
        entry.update(status='EMULATED_ALPHA_CODE', file=dest.name, validation=check,
            duration_ms=clip['duration'], action_end_ms=bake['duration_ms'],
            sha256=hashlib.sha256(dest.read_bytes()).hexdigest())
    except Exception as e:
        entry['reason'] = f'{type(e).__name__}: {e}'
    return entry


def bake_job(job, actions, out, exe=DEFAULT_EXE, sizes=DEFAULT_SIZES, dt_ms=16, workers=1):
    out = local_output(out)
    alpha_static.verify_exe(str(exe))
    oracle = alpha_oracle.load(str(exe), str(sizes))
    kind = int(str(job['kind']), 0)
    if kind != 2:
        raise ValueError('S05A2_ONLY_CURRENT_HUMAN_0X02')
    weapon_spec = job.get('equipment', {}).get('weapon', {})
    weapon = (3, int(weapon_spec['subtype'])) if weapon_spec else (0, 0)
    if weapon not in ((3,15), (3,16), (3,17)):
        raise ValueError('S05A2_REQUIRES_VERIFIED_TWO_HANDED_WEAPON')
    seed = tuple(job.get('seed_variant', (0,0)))
    c, app = oracle.creature(kind, seed)
    res = cr.resolve(kind, app, equipment=('weapon',))
    manifest = {'format': 'cubegame.alpha_actions/1', 'kind': kind, 'rig': res.family_id,
                'weapon': list(weapon), 'offhand': [0,0], 'seed_variant': list(seed),
                'exe_sha256': hashlib.sha256(Path(exe).read_bytes()).hexdigest(),
                'status': 'EMULATED_ALPHA_CODE', 'BAKE_RESULT': 'IN_PROGRESS',
                'action_semantics': 'pose library, not gameplay authorization',
                'actions': []}
    payloads = [(str(exe), str(sizes), kind, seed, weapon, res.parts, res.family_id,
                 manifest['exe_sha256'], dt_ms, str(out), a) for a in actions]
    (out / 'actions.json').write_text(json.dumps(manifest, indent=1), encoding='utf8')
    with ProcessPoolExecutor(max_workers=workers) as pool:
        futures = [pool.submit(_bake_one,p) for p in payloads]
        for f in as_completed(futures):
            entry=f.result()
            manifest['actions'].append(entry)
            manifest['actions'].sort(key=lambda e:e['action'])
            (out / 'actions.json').write_text(json.dumps(manifest, indent=1), encoding='utf8')
            print(f"ACTION {entry['action']} state={entry.get('state')} status={entry['status']} {entry.get('reason','')}", flush=True)
    # The separate weapon layer is checked against the same oracle on runtime locomotion inputs.
    c[0x5c] |= 1
    states.equip(c, 0xaa8, *weapon)
    worst = 0.0; weapon_cases=[]
    for ph, amp, boost in ((0,0,False),(.8,.19,False),(2.2,.19,True),(4,.12,False),(5.5,.06,True)):
        em = de.Emulator(oracle.image, c, oracle.sizes)
        em.set_f32(0x1188, amp); em.set_f32(0x118c, ph)
        if boost:
            em.set_f32(0x40, 1.0); em.set_f32(0x34, 1.0); em.creature[0x124] |= 0x40
        for dt in (600,600,16):
            fr = em.frame(dt_ms=dt)
        actual, _ = joints(oracle, fr, kind)
        candidate = weapon_locomotion.joint(app, pw.WalkState(ph,amp,boost=boost,head_lean_deg=0), weapon[1])
        worst = max(worst, mat4.max_abs_diff(candidate, actual['weapon']))
        weapon_cases.append({'phase':ph,'amp':amp,'boost':boost,'onGround':True,'m':actual['weapon']})
    manifest['weapon_locomotion_oracle_max_abs'] = worst
    if worst > .001:
        raise ValueError(f'WEAPON_LOCOMOTION_ORACLE_MISMATCH {worst}')
    manifest['counts']=dict(collections.Counter(e['status'] for e in manifest['actions']))
    manifest['BAKE_RESULT'] = 'PASS' if manifest['counts'].get('EMULATED_ALPHA_CODE',0) else 'FAIL'
    ref_file=out / 'weapon_reference.json'
    ref_file.write_text(json.dumps({'status':'EMULATED_ALPHA_CODE','cases':weapon_cases}),encoding='utf8')
    manifest['weapon_reference_sha256']=hashlib.sha256(ref_file.read_bytes()).hexdigest()
    (out / 'actions.json').write_text(json.dumps(manifest, indent=1), encoding='utf8')
    if manifest['BAKE_RESULT']!='PASS':raise ValueError('NO_SUPPORTED_ACTIONS')
    return manifest


def main():
    ap = argparse.ArgumentParser(description=__doc__.split('\n')[0])
    ap.add_argument('--job', required=True)
    group=ap.add_mutually_exclusive_group(required=True)
    group.add_argument('--actions');group.add_argument('--survey',help='existing human action survey; enumerates action IDs only')
    ap.add_argument('--out', required=True); ap.add_argument('--exe', default=str(DEFAULT_EXE))
    ap.add_argument('--sizes', default=str(DEFAULT_SIZES)); ap.add_argument('--dt-ms', type=int, default=16)
    ap.add_argument('--workers',type=int,choices=(1,2,3,4),default=1)
    a=ap.parse_args()
    actions = sorted(set(int(x,0) for x in a.actions.split(','))) if a.actions else sorted(int(x) for x in json.loads(Path(a.survey).read_text(encoding='utf8')))
    if not actions or any(not 1 <= x <= 110 for x in actions):
        ap.error('explicit actions must be in 1..110')
    bake_job(json.loads(Path(a.job).read_text(encoding='utf8')), actions, a.out, a.exe, a.sizes, a.dt_ms, a.workers)


if __name__ == '__main__':
    main()
