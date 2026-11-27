const statuses=new Set(['PASS','FAIL','SKIP','BLOCKED','NOT_RUN']);
const modes=new Set(['SEARCH','CREATE','ADAPT','EDIT','REVIEW']);
const domains=['technical','motion','visual','gameplay','regression'];
const requireIt=(ok,msg)=>{if(!ok)throw Error('Animation: '+msg);};
const nonempty=x=>typeof x==='string'&&x.trim().length>0;
const list=(x,n)=>{requireIt(Array.isArray(x),n+' must be array');return x;};
const unique=(a,n)=>requireIt(new Set(a).size===a.length,n+' duplicate');
const canonical=x=>Array.isArray(x)?x.map(canonical):x!==null&&typeof x==='object'?Object.fromEntries(Object.keys(x).sort().map(k=>[k,canonical(x[k])])):x;
const same=(a,b)=>JSON.stringify(canonical(a))===JSON.stringify(canonical(b));
export function animationContract(t){
 const a=t.animation;requireIt(a&&nonempty(a.checkpoint),'checkpoint and restore recipe required');
 requireIt(typeof a.gameplay_required==='boolean','gameplay_required must be boolean');
 const q=list(a.requirements,'requirements');requireIt(q.length>0,'requirements required');
 for(const r of q){requireIt(r&&['id','owner_request','target','check','preserve'].every(k=>nonempty(r[k]))&&modes.has(r.mode),'invalid requirement');requireIt(t.acceptance_checks.includes(r.check),'unmapped check '+r.id);}
 unique(q.map(r=>r.id),'requirement IDs');unique(q.map(r=>r.check),'requirement checks');requireIt(q.length===t.acceptance_checks.length,'every acceptance check needs requirement ID');
 const s=list(a.snapshots,'snapshots');unique(s.map(x=>x.id),'snapshot IDs');
 for(const x of s){requireIt(nonempty(x.id)&&t.protected_files.some(p=>same(p,x.baseline)),'baseline export must be protected');const k=list(x.allowed_keys,'allowed_keys');requireIt(k.every(nonempty),'invalid allowed key');unique(k,'allowed keys');}
 if(q.some(r=>r.mode==='EDIT'))requireIt(s.length>0||nonempty(a.runtime_only_reason),'EDIT requires track baseline or explicit runtime-only reason');
}
export function compareTracks(before,after,allowed){
 for(const a of [before,after])requireIt(a&&a.tracks&&typeof a.tracks==='object'&&!Array.isArray(a.tracks)&&Object.keys(a.tracks).length>0,'invalid or empty track export');
 const keys=new Set([...Object.keys(before.tracks),...Object.keys(after.tracks)]);
 const changed=[];for(const k of keys){if(!Object.hasOwn(before.tracks,k)||!Object.hasOwn(after.tracks,k)||!same(before.tracks[k],after.tracks[k]))changed.push(k);}
 return changed.filter(k=>!allowed.includes(k));
}
export function animationResult(t,r,resolveEvidence,readExport){
 const a=t.animation,b=r.animation;requireIt(b&&b.domains,'result domains required');
 const ready=r.status==='READY_FOR_OWNER_REVIEW';
 function verdict(v,label){requireIt(v&&statuses.has(v.status)&&nonempty(v.observation),label+' status/observation required');const e=list(v.evidence,label+' evidence');if(v.status==='PASS')requireIt(e.length>0,label+' PASS without evidence');e.forEach(resolveEvidence);}
 const q=list(b.requirements,'result requirements');unique(q.map(x=>x.id),'result IDs');requireIt(q.length===a.requirements.length,'requirement coverage mismatch');
 for(const x of a.requirements){const y=q.find(z=>z.id===x.id);verdict(y,x.id);requireIt(r.checks.find(c=>c.name===x.check)?.status===y.status,'check status mismatch '+x.id);if(ready)requireIt(y.status==='PASS','READY with open requirement '+x.id);}
 requireIt(Object.keys(b.domains).length===domains.length,'five domains required');
 for(const d of domains){const v=b.domains[d];verdict(v,d);if(ready)requireIt(v.status==='PASS'||(d==='gameplay'&&!a.gameplay_required&&v.status==='SKIP'),'READY with '+d+' '+v.status);}
 requireIt(b.domains.technical.status===r.technical_status&&b.domains.visual.status===r.visual_status,'top-level verdict mismatch');
 if(a.gameplay_required)requireIt(b.domains.gameplay.status!=='SKIP','required gameplay cannot SKIP');
 const s=list(b.snapshots,'result snapshots');unique(s.map(x=>x.id),'result snapshot IDs');
 for(const x of s){const p=a.snapshots.find(z=>z.id===x.id);requireIt(p,'unknown snapshot '+x.id);requireIt(!same(p.baseline,x.candidate),'candidate export must be separate from baseline');const changed=compareTracks(readExport(p.baseline),readExport(x.candidate),p.allowed_keys);requireIt(!changed.length||(!ready&&b.domains.regression.status==='FAIL'),'protected tracks changed: '+changed.join(', '));}
 if(ready||b.domains.regression.status==='PASS')requireIt(s.length===a.snapshots.length,'missing candidate snapshot');
}
