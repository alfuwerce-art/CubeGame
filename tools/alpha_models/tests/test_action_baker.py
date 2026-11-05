"""Synthetic refusal/contract tests; no Alpha binaries, matrices or assets in fixtures."""
import copy
import math
import tempfile
import unittest
from unittest.mock import patch, MagicMock
from tools.alpha_models.anim import mat4
from tools.alpha_models.universal import action_baker as ab, anim_states as st
from tools.alpha_models.transfer.prepare_unity import unity_clip, weapon_handle


def sample():
    rest={'body':mat4.translate(0,0,0)}
    b={'state':2,'timings':{'attack_timing':32},'duration_ms':32,'dt_ms':16,
       'weapon':[3,15],'offhand':[0,0], 'frames':[
        {'t':0,'action':57,'state':2,'joints':rest},
        {'t':32,'action':57,'state':2,'joints':{'body':mat4.translate(2,3,4)}},
        {'t':48,'action':0,'state':0,'joints':rest},
        {'t':80,'action':0,'state':0,'joints':rest}]}
    return copy.deepcopy(b),rest


class ActionBakeTests(unittest.TestCase):
    def test_handle_geometry_finds_guard_from_width(self):
        class Model:
            width=5;depth=5;height=12
            def rgb_at(self,x,y,z):
                return (1,2,3) if (x==2 and y==2) or (z==7 and y==2) else (0,0,0)
        self.assertEqual(weapon_handle(Model()),(0.0,7.0))

    def test_ambiguous_handle_geometry_refuses(self):
        class Model:
            width=1;depth=1;height=12
            def rgb_at(self,x,y,z):return (1,2,3)
        with self.assertRaisesRegex(ValueError,'GUARD_UNPROVEN'):weapon_handle(Model())

    def test_complete_motion_and_recovery(self):
        b,r=sample(); v=ab.validate_bake(b,['body'],r)
        self.assertGreater(v['peak_pos_vox'],5)
        self.assertEqual(v['end_pos_vox'],0)
        self.assertEqual(v['raw_state_switches'][0]['t'],48)

    def test_missing_part_refuses_instead_of_dropping_key(self):
        b,_=sample(); b['frames'][1]['joints']={}
        with self.assertRaisesRegex(ValueError,'ACTION_PARTS_MISSING'):
            st.baked_clip(b,2,'synthetic','action_057',['body'])

    def test_empty_bake_refuses(self):
        b,_=sample(); b['frames']=[]
        with self.assertRaisesRegex(ValueError,'ACTION_NO_KEYS'):
            st.baked_clip(b,2,'synthetic','action_057',['body'])

    def test_provenance_and_timing_survive_unity_conversion(self):
        b,_=sample(); c=st.baked_clip(b,2,'synthetic','action_057',['body'])
        c['parts'][0]['captured_size']=[1,1,1];c['source']['action']=57
        u=unity_clip(c,['body'])
        self.assertEqual(u['status'],'EMULATED_ALPHA_CODE')
        self.assertEqual(u['actionCode'],57)
        self.assertEqual(u['actionEndMs'],32)
        self.assertEqual(u['recoveryStartMs'],48)
        self.assertTrue(u['weaponOnBack'])
        self.assertEqual(len(u['keys']),4)

    def test_unsettled_recovery_refuses(self):
        b,r=sample();b['frames'][-1]['joints']['body']=mat4.translate(.01,0,0)
        with self.assertRaisesRegex(ValueError,'ACTION_RECOVERY_NOT_SETTLED'):ab.validate_bake(b,['body'],r)

    def test_static_action_refuses(self):
        b,r=sample()
        for f in b['frames']: f['joints']=r
        with self.assertRaisesRegex(ValueError,'ACTION_STATIC'):ab.validate_bake(b,['body'],r)

    def test_static_held_pose_cannot_pass_from_return_to_idle_only(self):
        b,r=sample()
        b['frames'][0]['joints']={'body':mat4.translate(2,3,4)}
        with self.assertRaisesRegex(ValueError,'ACTION_STATIC_ACTIVE_WINDOW'):ab.validate_bake(b,['body'],r)

    def test_nonfinite_matrix_refuses(self):
        b,r=sample();b['frames'][1]['joints']['body'][0]=float('nan')
        with self.assertRaisesRegex(ValueError,'ACTION_NONFINITE'):ab.validate_bake(b,['body'],r)

    def test_shear_refuses(self):
        b,r=sample();b['frames'][1]['joints']['body'][1]=.5
        with self.assertRaisesRegex(ValueError,'ACTION_NOT_TRS'):ab.validate_bake(b,['body'],r)

    def test_changed_part_set_refuses(self):
        b,r=sample();b['frames'][1]['joints']['extra']=mat4.translate(0,0,0)
        with self.assertRaisesRegex(ValueError,'ACTION_PART_SET_CHANGED'):ab.validate_bake(b,['body'],r)

    def test_time_duplicate_refuses(self):
        b,r=sample();b['frames'][1]['t']=0
        with self.assertRaisesRegex(ValueError,'ACTION_TIME_NOT_INCREASING'):ab.validate_bake(b,['body'],r)

    def test_no_recovery_refuses(self):
        b,r=sample();b['frames']=b['frames'][:2]
        with self.assertRaisesRegex(ValueError,'ACTION_INCOMPLETE_TIMELINE'):ab.validate_bake(b,['body'],r)

    def test_output_cannot_escape_scratch(self):
        with self.assertRaisesRegex(ValueError,'ALPHA_OUTPUT_MUST_BE_IN_SCRATCH'):ab.local_output(tempfile.gettempdir())

    def test_unknown_timing_has_no_one_second_fallback(self):
        oracle=MagicMock(); oracle.creature.return_value=(bytearray(0x2000),{})
        for bad in (0,-1,-2147483648,'ERROR unavailable',40000):
            with self.subTest(duration=bad), patch.object(st.de,'Emulator'), patch.object(st,'action_timings',return_value={'attack_timing':bad}):
                with self.assertRaisesRegex(ValueError,'ACTION_DURATION_UNPROVEN'):st.bake_action(oracle,2,57)

    def test_invalid_step_refuses_before_running_binary(self):
        with self.assertRaisesRegex(ValueError,'INVALID_BAKE_STEP'):st.bake_action(None,2,57,dt_ms=0)

if __name__=='__main__':unittest.main()
