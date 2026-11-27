import test from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync,mkdirSync,writeFileSync,rmSync,readFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
import { start,check } from './task-gate.mjs';
function fixture(fn){
 const root=mkdtempSync(path.join(tmpdir(),'cg-gate-'));
 const g=(...args)=>execFileSync('git',args,{cwd:root,encoding:'utf8',stdio:['ignore','pipe','pipe']}).trim();
 const w=(p,s)=>{mkdirSync(path.dirname(path.join(root,p)),{recursive:true});writeFileSync(path.join(root,p),s);};
 try{
 g('init','-b','review/test');g('config','user.email','test@example.invalid');g('config','user.name','Test');g('remote','add','origin','https://github.com/alfuwerce-art/CubeGame.git');
 for(const p of ['AGENTS.md','PROJECT_MEMORY.md','docs/ACTIVE_STAGE.md','docs/art/ART_DIRECTION.md','docs/art/REFERENCE_PROTOCOL.md','docs/art/ASSET_ACCEPTANCE.md','.agents/skills/cubegame-art-validation/SKILL.md','.agents/skills/cubegame-voxel-asset/SKILL.md','model/base.bin','model/torso.bin','evidence/report.txt'])w(p,'baseline '+p);
 g('add','.');g('commit','-m','base');
 const t={task_id:'test',goal:'torso',source:'owner',kind:'art',base_sha:g('rev-parse','HEAD'),branch:'review/test',engine:'UE test',project_path:root,allowed_paths:['model/torso.bin','evidence/'],context_files:['docs/art/ART_DIRECTION.md','docs/art/REFERENCE_PROTOCOL.md','docs/art/ASSET_ACCEPTANCE.md','.agents/skills/cubegame-art-validation/SKILL.md','.agents/skills/cubegame-voxel-asset/SKILL.md'],skills:['cubegame-voxel-asset','cubegame-art-validation'],protected_files:['model/base.bin'],references:['front'],acceptance_checks:['shape']};
 const result={status:'READY_FOR_OWNER_REVIEW',technical_status:'PASS',visual_status:'PASS',evidence:['evidence/report.txt'],checks:[{name:'shape',status:'PASS',evidence:['evidence/report.txt']}]};
 fn({root,w,g,t,result});
 }finally{rmSync(root,{recursive:true,force:true});}
}
test('local allowed change with complete evidence',()=>fixture(({root,w,t,result})=>{const r=start(root,t);w('model/torso.bin','new');assert.equal(check(root,t,r,result).status,'PASS');}));
test('out of scope untracked or committed change',()=>fixture(({root,w,g,t,result})=>{const r=start(root,t);w('other.txt','x');assert.throws(()=>check(root,t,r,result),/Out-of-scope/);g('add','other.txt');g('commit','-m','outside');assert.throws(()=>check(root,t,r,result),/Out-of-scope/);}));
test('protected and context changes rejected',()=>fixture(({root,w,t,result})=>{const r=start(root,t);w('model/base.bin','x');assert.throws(()=>check(root,t,r,result),/Protected file changed/);w('model/base.bin','baseline model/base.bin');w('AGENTS.md','x');assert.throws(()=>check(root,t,r,result),/Context changed/);}));
test('missing visual skill, unsafe path, dirty baseline rejected',()=>fixture(({root,w,t})=>{assert.throws(()=>start(root,{...t,skills:['cubegame-voxel-asset']}),/Art validation/);assert.throws(()=>start(root,{...t,allowed_paths:['../outside']}),/Unsafe path/);w('other.txt','x');assert.throws(()=>start(root,t),/Dirty workspace/);}));
test('technical pass cannot substitute visual or missing evidence',()=>fixture(({root,t,result})=>{const r=start(root,t);assert.throws(()=>check(root,t,r,{...result,visual_status:'FAIL'}),/READY requires/);assert.throws(()=>check(root,t,r,{...result,checks:[{name:'shape',status:'PASS',evidence:[]}]}),/PASS without evidence/);assert.throws(()=>check(root,t,r,{...result,evidence:['missing.txt']}));assert.equal(check(root,t,r,{...result,status:'PARTIAL',visual_status:'FAIL'}).status,'PASS');}));
test('contract cannot be modified after baseline',()=>fixture(({root,t,result})=>{const r=start(root,t);assert.throws(()=>check(root,{...t,goal:'other'},r,result),/Contract or receipt mismatch/);}));
test('declared external changes checked against explicit scope',()=>fixture(({root,t,result})=>{const ext=mkdtempSync(path.join(tmpdir(),'cg-external-'));try{writeFileSync(path.join(ext,'protected.bin'),'safe');const x={...t,external_root:ext,external_allowed_paths:['torso.bin'],protected_files:[{root:'external',path:'protected.bin'}]};const r=start(root,x);assert.throws(()=>check(root,x,r,{...result,external_changed_paths:['other.bin']}),/external change out of scope/);writeFileSync(path.join(ext,'protected.bin'),'changed');assert.throws(()=>check(root,x,r,result),/Protected file changed/);}finally{rmSync(ext,{recursive:true,force:true});}}));

test('only explicit docs tasks may edit context',()=>fixture(({root,w,t,result})=>{const x={...t,kind:'docs',allowed_paths:['AGENTS.md'],editable_context_files:['AGENTS.md']};assert.throws(()=>start(root,{...x,kind:'art'}),/Only authorized docs/);const r=start(root,x);w('AGENTS.md','approved doc edit');assert.equal(check(root,x,r,result).status,'PASS');}));

test('animation gate integrated with actual protected export and generic receipt',()=>fixture(({root,w,g,t,result})=>{
 for(const p of ['.agents/skills/cubegame-rig-animation/SKILL.md','docs/agents/animation/QUALITY_PROTOCOL.md','docs/agents/animation/WORK_MODES.md','docs/agents/animation/TOOL_SELECTION.md','docs/agents/animation/REVIEW_LAB.md','docs/agents/animation/SOURCE_SELECTION.md','docs/agents/animation/EDITOR_RECOVERY.md'])w(p,'context');
 w('model/baseline.json',JSON.stringify({tracks:{finger:[0],body:[3]}}));
 g('add','.');g('commit','-m','animation baseline');
 const x={...t,kind:'animation',base_sha:g('rev-parse','HEAD'),skills:['cubegame-art-validation','cubegame-rig-animation'],context_files:[...t.context_files,'.agents/skills/cubegame-rig-animation/SKILL.md','docs/agents/animation/QUALITY_PROTOCOL.md','docs/agents/animation/WORK_MODES.md','docs/agents/animation/TOOL_SELECTION.md','docs/agents/animation/REVIEW_LAB.md','docs/agents/animation/SOURCE_SELECTION.md','docs/agents/animation/EDITOR_RECOVERY.md'],protected_files:['model/base.bin','model/baseline.json'],animation:{checkpoint:'restore baseline',gameplay_required:true,requirements:[{id:'A',owner_request:'fingers',mode:'EDIT',target:'Run',check:'shape',preserve:'body'}],snapshots:[{id:'run',baseline:'model/baseline.json',allowed_keys:['finger']}]}};
 assert.throws(()=>start(root,{...x,skills:[...x.skills,'cubegame-voxel-asset']}),/Animation skill lock/);
 const receipt=start(root,x);w('evidence/candidate.json',JSON.stringify({tracks:{finger:[1],body:[3]}}));
 const v={status:'PASS',observation:'frame 5 side view actual observed motion',evidence:['evidence/report.txt']};
 const y={...result,animation:{requirements:[{id:'A',...v}],domains:Object.fromEntries(['technical','motion','visual','gameplay','regression'].map(d=>[d,v])),snapshots:[{id:'run',candidate:'evidence/candidate.json'}]}};
 assert.equal(check(root,x,receipt,y).status,'PASS');
 w('evidence/candidate.json',JSON.stringify({tracks:{finger:[1],body:[4]}}));
 assert.throws(()=>check(root,x,receipt,y),/protected tracks changed/);
}));
