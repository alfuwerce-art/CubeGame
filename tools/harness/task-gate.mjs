import { execFileSync } from 'node:child_process';
import { readFileSync, writeFileSync, existsSync, realpathSync, statSync } from 'node:fs';
import { createHash } from 'node:crypto';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
const core=['AGENTS.md','PROJECT_MEMORY.md','docs/ACTIVE_STAGE.md'];
const art=['docs/art/ART_DIRECTION.md','docs/art/REFERENCE_PROTOCOL.md','docs/art/ASSET_ACCEPTANCE.md'];
const kinds=new Set(['code','art','animation','docs','research']);
const statuses=new Set(['PASS','FAIL','SKIP','BLOCKED','NOT_RUN']);
const insist=(ok,msg)=>{if(!ok)throw Error(msg);};
const hash=b=>createHash('sha256').update(b).digest('hex');
const git=(root,...args)=>execFileSync('git',args,{cwd:root,encoding:'utf8',stdio:['ignore','pipe','pipe']}).trim();
const gitZ=(root,...args)=>execFileSync('git',args,{cwd:root,encoding:'utf8',stdio:['ignore','pipe','pipe']}).split('\0').filter(Boolean);
const arr=(x,n)=>{insist(Array.isArray(x),n+' must be array');return x;};
const str=(x,n)=>{insist(typeof x==='string'&&x.trim().length>0,n+' required');return x;};
function rel(p, prefix=false){
 str(p,'relative path'); insist(!p.includes('\\')&&!p.includes(':')&&!p.startsWith('/')&&!p.includes('\0'),'Unsafe path '+p);
 const q=prefix&&p.endsWith('/')?p.slice(0,-1):p;
 insist(q.split('/').every(x=>x&&x!=='.'&&x!=='..'&&x.toLowerCase()!=='.git'),'Unsafe path '+p);return p;
}
function inside(root,p){
 rel(p);const abs=path.resolve(root,p);const actual=realpathSync(abs);const r=path.relative(realpathSync(root),actual);
 insist(r&&!r.startsWith('..'+path.sep)&&r!=='..'&&!path.isAbsolute(r),'Path escapes root '+p);
 insist(statSync(actual).isFile(),'Not a file '+p);return actual;
}
function resolveRef(root,t,item){
 const o=typeof item==='string'?{path:item}:item;
 insist(o&&['repo','external'].includes(o.root??'repo'),'Invalid file root');
 const base=o.root==='external'?str(t.external_root,'external_root'):root;
 return inside(base,o.path);
}
function fingerprint(root,t,item){return hash(readFileSync(resolveRef(root,t,item)));}
function scopeList(t,k){return arr(t[k]??[],k).map(x=>rel(x,true));}
function allowed(p,scopes){return scopes.some(s=>s.endsWith('/')?p.startsWith(s):p===s);}
export function validateContract(root,t){
 for(const k of ['task_id','goal','source','kind','base_sha','branch','engine','project_path'])str(t[k],k);
 insist(kinds.has(t.kind),'Invalid kind');
 insist(/^[0-9a-f]{40}$/i.test(t.base_sha),'Full base SHA required');
 insist(!['main','master','integration/m0-all'].includes(t.branch),'Protected branch');
 const scopes=scopeList(t,'allowed_paths'),ext=scopeList(t,'external_allowed_paths');
 insist(scopes.length+ext.length>0,'No allowed paths');
 if(t.external_root){insist(path.isAbsolute(t.external_root)&&existsSync(t.external_root),'External root must exist');const r=realpathSync(t.external_root),g=realpathSync(root);const a=path.relative(g,r),b=path.relative(r,g);const outside=x=>x==='..'||x.startsWith('..'+path.sep)||path.isAbsolute(x);insist(outside(a)&&outside(b),'External root must be isolated from Git workspace');insist(ext.length,'External allowed paths required');}
 else insist(!ext.length,'External root required');
 const ctx=[...new Set([...core,...arr(t.context_files,'context_files')])];
 const skillNames=arr(t.skills,'skills');
 insist(skillNames.length>0,'At least one relevant skill required');
 for(const s of skillNames){insist(/^cubegame-[a-z0-9-]+$/.test(s),'Invalid skill name');const a='.agents/skills/'+s+'/SKILL.md',d='.dsh/skills/'+s+'/SKILL.md';insist(ctx.includes(a)||ctx.includes(d),'Skill not in context_files: '+s);}
 const editable=arr(t.editable_context_files??[],'editable_context_files');
 insist(!editable.length||t.kind==='docs','Only authorized docs tasks may edit context');
 for(const p of editable)insist(ctx.includes(p)&&allowed(p,scopes),'Editable context must be in scope: '+p);
 const visual=['art','animation'].includes(t.kind);
 if(visual){for(const p of art)insist(ctx.includes(p),'Missing visual context '+p);if(t.asset_type==='character')insist(ctx.includes('docs/art/CHARACTER_STYLE_STANDARD.md'),'Character style standard required');insist(skillNames.includes('cubegame-art-validation'),'Art validation skill required');insist(skillNames.includes(t.kind==='art'?'cubegame-voxel-asset':'cubegame-rig-animation'),'Missing production skill');insist(arr(t.references,'references').length>0,'Visual reference required');for(const r of t.references)str(r,'reference');}
 for(const p of ctx)inside(root,p);
 const checks=arr(t.acceptance_checks,'acceptance_checks');
 insist(checks.length&&new Set(checks).size===checks.length,'Nonempty unique acceptance checks required');for(const c of checks)str(c,'acceptance check');
 const protect=arr(t.protected_files,'protected_files');for(const p of protect)resolveRef(root,t,p);
 return {ctx,protect,scopes,ext,visual,editable};
}
export function start(root,t){
 const v=validateContract(root,t);
 insist(git(root,'rev-parse','--show-toplevel').replaceAll('\\','/').toLowerCase()===realpathSync(root).replaceAll('\\','/').toLowerCase(),'Run at repository root');
 insist(/alfuwerce-art[/:]CubeGame(?:\.git)?$/i.test(git(root,'remote','get-url','origin')),'Wrong origin');
 insist(git(root,'symbolic-ref','--short','HEAD')===t.branch,'Wrong branch');
 insist(git(root,'rev-parse','HEAD')===t.base_sha,'Wrong base SHA');
 insist(!git(root,'status','--porcelain','--untracked-files=all'),'Dirty workspace; preserve existing work and isolate task');
 return {schema:1,task_id:t.task_id,contract_sha:hash(JSON.stringify(t)),base_sha:t.base_sha,branch:t.branch,context:v.ctx.map(p=>({path:p,sha:fingerprint(root,t,p)})),protected:v.protect.map(p=>({file:p,sha:fingerprint(root,t,p)}))};
}
export function check(root,t,r,result){
 const v=validateContract(root,t);
 insist(r.schema===1&&r.contract_sha===hash(JSON.stringify(t))&&r.task_id===t.task_id&&r.base_sha===t.base_sha&&r.branch===t.branch,'Contract or receipt mismatch');
 insist(git(root,'symbolic-ref','--short','HEAD')===t.branch,'Branch changed');
 git(root,'merge-base','--is-ancestor',t.base_sha,'HEAD');
 insist(JSON.stringify(r.context.map(x=>x.path))===JSON.stringify(v.ctx),'Context receipt incomplete');
 insist(JSON.stringify(r.protected.map(x=>x.file))===JSON.stringify(v.protect),'Protected receipt incomplete');
 for(const e of r.context)if(!v.editable.includes(e.path))insist(fingerprint(root,t,e.path)===e.sha,'Context changed '+e.path);
 for(const e of r.protected)insist(fingerprint(root,t,e.file)===e.sha,'Protected file changed '+JSON.stringify(e.file));
 insist(/alfuwerce-art[/:]CubeGame(?:\.git)?$/i.test(git(root,'remote','get-url','origin')),'Wrong origin');
 const changed=[...new Set([...gitZ(root,'diff','--name-only','--no-renames','-z',t.base_sha,'--'),...gitZ(root,'diff','--cached','--name-only','--no-renames','-z',t.base_sha,'--'),...gitZ(root,'ls-files','--others','--exclude-standard','-z')])];
 for(const p of changed)insist(allowed(p,v.scopes),'Out-of-scope Git change '+p);
 const ext=arr(result.external_changed_paths??[],'external_changed_paths');
 for(const p of ext){rel(p);insist(allowed(p,v.ext),'Reported external change out of scope '+p);}
 insist(['READY_FOR_OWNER_REVIEW','PARTIAL','BLOCKED'].includes(result.status),'Invalid result status; owner accepts');
 insist(statuses.has(result.technical_status)&&statuses.has(result.visual_status),'Missing technical/visual status');
 const evidence=arr(result.evidence,'evidence');insist(evidence.length>0,'Result evidence required');for(const e of evidence)resolveRef(root,t,e);
 const checks=arr(result.checks,'checks');insist(checks.length===t.acceptance_checks.length&&new Set(checks.map(c=>c.name)).size===checks.length,'Acceptance coverage mismatch');
 for(const c of checks){insist(t.acceptance_checks.includes(c.name)&&statuses.has(c.status),'Invalid criterion '+c.name);const ev=arr(c.evidence,'check evidence');if(c.status==='PASS')insist(ev.length>0,'PASS without evidence '+c.name);for(const e of ev)resolveRef(root,t,e);}
 if(result.status==='READY_FOR_OWNER_REVIEW'){insist(result.technical_status==='PASS'&&(!v.visual||result.visual_status==='PASS'),'READY requires relevant PASS');insist(checks.every(c=>c.status==='PASS'),'READY with incomplete criteria');}
 return {status:'PASS',task_status:result.status,changed,reported_external_changes:ext,limitations:['Receipt checks structure and bytes, not instruction comprehension or evidence truth.','Ignored Git files, unreported external operations, editor memory and visual quality are not independently verified.']};
}
if(process.argv[1]&&fileURLToPath(import.meta.url)===path.resolve(process.argv[1])){
 try{const [mode,contract,receipt,result]=process.argv.slice(2);insist(['start','check'].includes(mode)&&contract&&receipt,'Usage: task-gate.mjs start|check contract.json receipt.json [result.json]');
 const root=process.cwd(),t=JSON.parse(readFileSync(contract,'utf8'));
 if(mode==='start'){insist(!existsSync(receipt),'Receipt already exists; do not replace baseline');const r=start(root,t);writeFileSync(receipt,JSON.stringify(r,null,2),{flag:'wx'});console.log(JSON.stringify({status:'PASS',task_id:r.task_id,receipt},null,2));}
 else console.log(JSON.stringify(check(root,t,JSON.parse(readFileSync(receipt,'utf8')),JSON.parse(readFileSync(result,'utf8'))),null,2));
 }catch(e){console.error('BLOCKED: '+e.message);process.exitCode=2;}
}
