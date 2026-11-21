"""Synthetic visibility corruption tests; native-code checks run only with the local Alpha copy."""
import copy,json,struct,unittest
from pathlib import Path
from tools.alpha_models.anim import mat4
from tools.alpha_models.universal import anim_states as st,action_baker as ab,native_lifecycle as nl
from tools.alpha_models.transfer.prepare_unity import unity_clip
from tools.alpha_models.tests.test_action_baker import sample


def sparse():
    b,r=sample();b['dynamic_parts']=True
    b['frames'][0]['joints']['weapon']=mat4.translate(0,0,0)
    for f in b['frames'][2:]:f['joints']['weapon']=mat4.translate(0,0,0)
    c=st.baked_clip(b,2,'synthetic','action_022',['body','weapon'])
    for p in c['parts']:p['captured_size']=[1,1,1]
    return b,c


class DynamicContractTests(unittest.TestCase):
    def test_hidden_matrix_is_absent_and_not_zero_filled(self):
        _,c=sparse();u=unity_clip(c,['weapon','body'])
        self.assertEqual(c['format'],'cubegame.alpha_clip/3')
        self.assertEqual(c['keys'][1]['visible'],[1,0]);self.assertEqual(set(c['keys'][1]['m']),{'body'})
        self.assertEqual(u['keys'][1]['visible'],[0,1]);self.assertEqual(len(u['keys'][1]['m']),16)
        self.assertEqual(len(u['keys'][0]['m']),32)

    def test_dynamic_missing_parts_are_explicit(self):
        b,c=sparse();rest=b['frames'][-1]['joints']
        self.assertGreater(ab.validate_bake(b,['body','weapon'],rest)['active_motion_vox'],0)

    def test_static_missing_part_remains_error(self):
        b,_=sparse();b['dynamic_parts']=False
        with self.assertRaisesRegex(ValueError,'PARTS_MISSING'):st.baked_clip(b,2,'s','a',['body','weapon'])

    def test_unsupported_draw_is_not_hidden_silently(self):
        b,_=sparse();b['frames'][1]['unsupported_draw_sites']=['TEST_SITE']
        with self.assertRaisesRegex(ValueError,'UNSUPPORTED_NATIVE_DRAW'):st.baked_clip(b,2,'s','a',['body','weapon'])

    def test_extra_part_is_not_an_invented_optional_slot(self):
        b,_=sparse();b['frames'][1]['joints']['extra']=mat4.translate(0,0,0)
        with self.assertRaisesRegex(ValueError,'UNEXPECTED_NATIVE_PART'):st.baked_clip(b,2,'s','a',['body','weapon'])

    def test_visibility_masks_and_sparse_payload_must_agree(self):
        _,c=sparse()
        for kind in ('missing','short','invalid','extra_matrix','missing_matrix'):
            with self.subTest(kind=kind):
                d=copy.deepcopy(c);k=d['keys'][1]
                if kind=='missing':del k['visible']
                if kind=='short':k['visible']=[1]
                if kind=='invalid':k['visible']=[1,2]
                if kind=='extra_matrix':k['m']['weapon']=mat4.translate(0,0,0)
                if kind=='missing_matrix':del k['m']['body']
                with self.assertRaisesRegex(ValueError,'CLIP_VIS'):unity_clip(d,['body','weapon'])

    def test_nonfinite_and_invalid_time_are_refused(self):
        _,c=sparse()
        for kind in ('nan','duplicate','negative','beyond_end'):
            with self.subTest(kind=kind):
                d=copy.deepcopy(c)
                if kind=='nan':d['keys'][1]['m']['body'][0]=float('nan')
                if kind=='duplicate':d['keys'][1]['x']=0
                if kind=='negative':d['keys'][1]['x']=-1
                if kind=='beyond_end':d['keys'][1]['x']=1000
                with self.assertRaisesRegex(ValueError,'CLIP_'):unity_clip(d,['body','weapon'])

    def test_undeclared_and_unknown_visibility_refused(self):
        _,c=sparse();c['source']['visibility_mode']='STATIC_ALL'
        with self.assertRaisesRegex(ValueError,'UNDECLARED'):unity_clip(c,['body','weapon'])
        c['source']['visibility_mode']='typo'
        with self.assertRaisesRegex(ValueError,'MODE_UNKNOWN'):unity_clip(c,['body','weapon'])

    def test_dynamic_recovery_must_restore_parts(self):
        b,_=sparse();del b['frames'][-1]['joints']['weapon']
        with self.assertRaisesRegex(ValueError,'RECOVERY_PARTS_NOT_RESTORED'):
            ab.validate_bake(b,['body','weapon'],{'body':mat4.translate(0,0,0),'weapon':mat4.translate(0,0,0)})


@unittest.skipUnless(Path(ab.DEFAULT_EXE).exists() and Path(ab.DEFAULT_SIZES).exists(),'local Alpha only')
class NativeEvidenceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        from tools.alpha_models.universal import alpha_oracle
        from tools.alpha_models.universal.lifecycle_pipeline import specimen
        cls.o=alpha_oracle.load(str(ab.DEFAULT_EXE),str(ab.DEFAULT_SIZES));cls.template,_=specimen(cls.o,1)
    def test_native_idle_is_strict_10s_and_preserves_timer(self):
        for t,expected in ((1382,57),(10000,57),(10001,0)):
            c=bytearray(self.template);c[0x68]=57;struct.pack_into('<i',c,0x6c,t)
            r=nl.idle_tail(self.o.image,c)
            self.assertEqual((r['action'],r['timer_ms']),(expected,t))
            self.assertEqual(nl.idle_tail(self.o.image,c,True)['action'],57)
    def test_latched_79_is_not_rejected_for_static_pose(self):
        c=bytearray(self.template);c[0x68]=79;struct.pack_into('<i',c,0x6c,20000)
        self.assertEqual(nl.idle_tail(self.o.image,c)['action'],79)
    def test_consumable_cooldown_is_contextual(self):
        c=bytearray(self.template)
        for subtype,expected in ((1,3000),(2,10000)):
            c[0x1e8]=1;c[0x1e9]=subtype
            self.assertEqual(nl.item_cooldown(self.o.image,c),expected)
        c[0x1e8]=0;self.assertEqual(nl.item_cooldown(self.o.image,c),0)
    def test_primary_threshold_late_restart_and_blocked_gate(self):
        c=bytearray(self.template);c[0x68]=57
        for t,selected,next_action in ((1381,False,57),(1382,True,67),(1582,True,57)):
            struct.pack_into('<i',c,0x6c,t);r=nl.advance_held(self.o.image,c)
            self.assertEqual((r['selected'],r['action']),(selected,next_action))
        struct.pack_into('<i',c,0x128,1)
        self.assertFalse(nl.advance_held(self.o.image,c)['selected'])


@unittest.skipUnless(Path('.scratch/lifecycle/library/actions.json').exists(),'local generated library only')
class LibraryRegressionTests(unittest.TestCase):
    def test_original_93_payloads_unchanged(self):
        old=Path('.scratch/animsys/human_actions_extended');new=Path('.scratch/lifecycle/library')
        supported=[e for e in json.loads((old/'actions.json').read_text())['actions'] if e['status']=='EMULATED_ALPHA_CODE']
        self.assertEqual(len(supported),93)
        for e in supported:
            with self.subTest(clip=e['clip']):self.assertEqual((old/e['file']).read_bytes(),(new/e['file']).read_bytes())


if __name__=='__main__':unittest.main()
