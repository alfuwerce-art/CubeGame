import { existsSync, readFileSync, readdirSync, statSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
const skillsRoot = path.join(root, '.dsh', 'skills');
const required = [
  'AGENTS.md', 'PROJECT_MEMORY.md', 'docs/ACTIVE_STAGE.md',
  'docs/CONCEPT.md', 'docs/GDD.md', 'docs/ROADMAP.md',
  'docs/agents/SKILLS_INDEX.md', 'docs/agents/HARNESS_ARCHITECTURE.md',
  'docs/agents/DEEPSEEK_HARNESS_SETUP.md', 'docs/design/DECISIONS.md',
  'docs/architecture/NAME_REGISTRY.md', 'docs/architecture/DEPENDENCY_MAP.md',
  'docs/QUALITY_ATTRIBUTES.md', 'docs/art/ART_DIRECTION.md'
];
let errors = [];
for (const rel of required) if (!existsSync(path.join(root, rel))) errors.push('MISSING ' + rel);
let valid = 0;
if (!existsSync(skillsRoot)) errors.push('MISSING skills directory');
else for (const name of readdirSync(skillsRoot)) {
  const skillPath = path.join(skillsRoot, name, 'SKILL.md');
  if (!statSync(path.join(skillsRoot, name)).isDirectory()) continue;
  if (!existsSync(skillPath)) { errors.push('MISSING ' + name + '/SKILL.md'); continue; }
  const text = readFileSync(skillPath, 'utf8');
  const front = /^---\r?\n([\s\S]*?)\r?\n---\r?\n/.exec(text);
  if (!front) { errors.push('INVALID FRONTMATTER ' + name); continue; }
  const n = /^name:\s*(\S+)\s*$/m.exec(front[1])?.[1];
  const d = /^description:\s*.+$/m.test(front[1]);
  if (n !== name || !/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(name) || !d) {
    errors.push('INVALID NAME/DESCRIPTION ' + name + ': ' + (n ?? 'unknown')); continue;
  }
  // Only relative markdown links to local files. Do not chase external URLs or inline code.
  for (const m of text.matchAll(/\]\((\.{1,2}\/[^)\s#]+)(?:#[^)]*)?\)/g)) {
    if (!existsSync(path.resolve(path.dirname(skillPath), m[1])))
      errors.push('BROKEN LINK ' + name + ' → ' + m[1]);
  }
  valid++;
}
console.log(JSON.stringify({ status: errors.length ? 'FAIL' : 'PASS', skillsValidated: valid, documentsChecked: required.length, errors }, null, 2));
if (valid < 9) errors.push('Expected at least 9 project skills');
if (errors.length) process.exitCode = 1;
