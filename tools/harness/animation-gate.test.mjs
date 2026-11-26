import test from 'node:test';
import assert from 'node:assert/strict';
import {animationContract,animationResult,compareTracks} from './animation-gate.mjs';
function fixture(){
 const t={acceptance_checks:['visible idle','visible fingers'],protected_files:['baseline.json'],animation:{checkpoint:'restorable candidate',gameplay_required:true,requirements:[{id:'A',owner_request:'idle',mode:'EDIT',target:'Idle',check:'visible idle',preserve:'feet'},{id:'B',owner_request:'fingers',mode:'EDIT',target:'Run',check:'visible fingers',preserve:'body'}],snapshots:[{id:'s',baseline:'baseline.json',allowed_keys:['finger']}]}};
 const v=()=>({status:'PASS',observation:'actual frame and camera',evidence:['evidence.txt']});
 const r={status:'READY_FOR_OWNER_REVIEW',technical_status:'PASS',visual_status:'PASS',checks:t.acceptance_checks.map(name=>({name,status:'PASS'})),animation:{requirements:t.animation.requirements.map(x=>({id:x.id,...v()})),domains:Object.fromEntries(['technical','motion','visual','gameplay','regression'].map(d=>[d,v()])),snapshots:[{id:'s',candidate:'candidate.json'}]}};
 const data={'baseline.json':{tracks:{finger:[0],body:[3],metadata:{length:1}}},'candidate.json':{tracks:{finger:[1],body:[3],metadata:{length:1}}}};
 const run=()=>{animationContract(t);animationResult(t,r,()=>{},p=>data[p]);};
 return {t,r,data,run};
}
test('complete coverage and allowed track change',()=>fixture().run());
test('missing owner requirement rejected',()=>{const f=fixture();f.r.animation.requirements.pop();assert.throws(f.run,/coverage/);});
test('technical PASS cannot hide visual FAIL',()=>{const f=fixture();f.r.animation.domains.visual.status='FAIL';f.r.visual_status='FAIL';assert.throws(f.run,/READY with visual/);});
test('track drift or metadata changes rejected',()=>{const f=fixture();f.data['candidate.json'].tracks.body=[4];assert.throws(f.run,/protected tracks changed/);assert.deepEqual(compareTracks({tracks:{m:{b:2,a:1}}},{tracks:{m:{a:1,b:2}}},[]),[]);});
test('missing snapshot and evidence rejected',()=>{const f=fixture();f.r.animation.snapshots=[];assert.throws(f.run,/missing candidate/);const g=fixture();g.r.animation.domains.visual.evidence=[];assert.throws(g.run,/PASS without evidence/);});
test('gameplay SKIP not allowed when required',()=>{const f=fixture();f.r.animation.domains.gameplay.status='SKIP';assert.throws(f.run,/gameplay/);});
test('unprotected baseline and omitted check rejected',()=>{const f=fixture();f.t.protected_files=[];assert.throws(f.run,/baseline export/);const g=fixture();g.t.animation.requirements.pop();assert.throws(g.run,/every acceptance check/);});
test('partial may honestly report regression and missing visual',()=>{const f=fixture();f.r.status='PARTIAL';f.r.animation.domains.visual.status='NOT_RUN';f.r.visual_status='NOT_RUN';f.r.animation.domains.regression.status='FAIL';f.data['candidate.json'].tracks.body=[9];f.run();});
test('candidate cannot reuse baseline export path',()=>{const f=fixture();f.r.animation.snapshots[0].candidate='baseline.json';assert.throws(f.run,/separate from baseline/);});
