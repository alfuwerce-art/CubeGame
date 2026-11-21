import { execFileSync } from 'node:child_process';
import { existsSync } from 'node:fs';
import path from 'node:path';

const required = ['AGENTS.md', 'PROJECT_MEMORY.md', 'docs/ACTIVE_STAGE.md', 'docs/GDD.md', 'docs/ROADMAP.md', 'docs/decisions/D95-single-repository-close-model-pipeline.md'];
const run = (args, cwd) => execFileSync('git', args, { cwd, encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] }).trim();
const fail = m => { console.error('BLOCKED:', m); process.exitCode = 2; };

try {
  const root = run(['rev-parse', '--show-toplevel'], process.cwd());
  const branch = run(['symbolic-ref', '--quiet', '--short', 'HEAD'], root);
  const remote = run(['remote', 'get-url', 'origin'], root);
  const status = run(['status', '--short'], root);
  const sha = run(['rev-parse', 'HEAD'], root);
  const missing = required.filter(p => !existsSync(path.join(root, p)));
  console.log(JSON.stringify({ project: 'CubeGame', branch, sha, remote, dirty: Boolean(status), changed: status.split('\n').filter(Boolean), missing }, null, 2));
  if (!/alfuwerce-art[/:]CubeGame(?:\.git)?$/i.test(remote)) fail('Неверный origin; ожидался alfuwerce-art/CubeGame');
  if (['main', 'master', 'integration/m0-all'].includes(branch)) fail('Нельзя изменять protected/canonical branch');
  if (missing.length) fail(`Не хватает обязательных файлов: ${missing.join(', ')}`);
  if (status) console.error('NOTICE: Есть незакоммиченные изменения. Нельзя стирать/переключать без согласования.');
} catch (e) { fail(`Git-проверка не удалась: ${e.message}`); }
