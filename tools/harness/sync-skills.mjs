import { readdirSync, readFileSync, mkdirSync, writeFileSync, existsSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const src=path.join(root,'.dsh','skills'), dst=path.join(root,'.agents','skills');
const mode=process.argv[2];
if(!['--check','--write'].includes(mode)){console.error('Use --check or --write');process.exit(2);}
const dirs=p=>existsSync(p)?readdirSync(p,{withFileTypes:true}).filter(x=>x.isDirectory()).map(x=>x.name).sort():[];
const names=dirs(src), errors=[];
for(const name of names){
 const from=path.join(src,name,'SKILL.md'), to=path.join(dst,name,'SKILL.md');
 const body=readFileSync(from);
 if(mode==='--write'){mkdirSync(path.dirname(to),{recursive:true});writeFileSync(to,body);}
 if(!existsSync(to)||!body.equals(readFileSync(to)))errors.push('Missing or drifted '+name);
}
for(const name of dirs(dst))if(!names.includes(name))errors.push('Unexpected mirror '+name+'; review manually');
if(names.length!==9)errors.push('Expected 9 canonical skills; update registry intentionally');
console.log(JSON.stringify({status:errors.length?'FAIL':'PASS',skills:names.length,errors},null,2));
if(errors.length)process.exitCode=1;
