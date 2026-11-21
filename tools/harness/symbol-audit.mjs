#!/usr/bin/env node
// Read-only, intentionally approximate C# symbol index. The compiler/LSP remain authoritative.
import { readdirSync, readFileSync, existsSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const scannedRoots = ['Assets', 'src', 'Source', 'Runtime', 'Editor']
  .map(x => path.join(root, x)).filter(existsSync);
const omit = new Set(['.git','Library','Temp','obj','bin','.scratch','node_modules','Logs','Builds']);
const full = process.argv.includes('--json');
let inspected = 0, symbols = [], errors = [];

function scan(dir) {
  let entries;
  try { entries = readdirSync(dir, { withFileTypes: true }); }
  catch (e) { errors.push({ path: path.relative(root, dir), problem: e.code }); return; }
  for (const ent of entries) {
    const p = path.join(dir, ent.name);
    if (ent.isDirectory()) { if (!omit.has(ent.name)) scan(p); continue; }
    if (!ent.isFile() || !ent.name.endsWith('.cs')) continue;
    let source;
    try { source = readFileSync(p, 'utf8'); } catch (e) { errors.push({ path: path.relative(root, p), problem: e.code }); continue; }
    inspected++;
    // This is a coarse index, not a real C# parser: attributes, comments and nested namespaces can fool regex.
    const ns = /\bnamespace\s+([A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)/.exec(source)?.[1] ?? null;
    for (const m of source.matchAll(/\b(?:public|internal|private|protected|partial|sealed|static|abstract|readonly|new|\s)*\b(class|struct|interface|enum|record)\s+([A-Za-z_]\w*)\b/g)) {
      symbols.push({ kind: m[1], name: m[2], namespaceApprox: ns, path: path.relative(root, p).split(path.sep).join('/'), confidence: 'CANDIDATE_REGEX' });
    }
  }
}
for (const p of scannedRoots) scan(p);
symbols.sort((a,b) => a.name.localeCompare(b.name) || a.path.localeCompare(b.path));
const duplicates = [...new Set(symbols.map(s=>s.name).filter((v,i,a)=>a.indexOf(v)!==i))];
const out = { status: 'READ_ONLY_HEURISTIC', scannedRoots: scannedRoots.map(p=>path.relative(root,p)),
  filesInspected: inspected, symbolCandidates: symbols.length, duplicateNameCandidates: duplicates, errors };
if (full) out.symbols = symbols;
else out.preview = symbols.slice(0, 25);
console.log(JSON.stringify(out,null,2));
if (errors.length) process.exitCode = 1;
