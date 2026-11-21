import { execFileSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const PROTECTED = new Set(['main', 'master', 'integration/m0-all']);

function git(args, cwd) {
  try { return execFileSync('git', args, { cwd, encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] }).trim(); }
  catch { return ''; }
}

export function evaluateTool(payload, { branch = '', root = '', cwd = '' } = {}) {
  if (!payload || typeof payload !== 'object') return 'Неверный payload инструмента';
  const name = String(payload.tool_name || '').toLowerCase();
  const input = payload.tool_input && typeof payload.tool_input === 'object' ? payload.tool_input : {};
  const command = ['command', 'cmd', 'script'].filter(k => typeof input[k] === 'string').map(k => input[k]).join('\n');
  const isShell = /(?:bash|pwsh|shell|exec|powershell|terminal|command)/.test(name);
  const fileEdit = /(?:write|edit|patch|create_file|delete_file)/.test(name);
  // Encoded shell commands are not auditable by this lightweight check.
  if (isShell && /(?:-encodedcommand\b|-enc\s|frombase64string\s*\()/i.test(command))
    return 'Закодированная shell-команда требует отдельного разрешения';
  const mutation = fileEdit || (isShell && /(?:git\s+(?:push|reset|clean|switch|checkout|merge|rebase|branch\s+-[dD])|(?:rm|del|erase|rmdir|remove-item|set-content|add-content|move-item|copy-item|mkdir|new-item|\b(?:tee|sed\s+-i)\b)|(?:>>|(?<![<])>(?![>])))/i.test(command));
  if (!mutation) return null;
  if (!root || !cwd) return 'Не удалось подтвердить корень Git workspace';
  if (PROTECTED.has(branch) || !branch) return `Запись запрещена в ветке ${branch || '(detached HEAD)'}`;
  if (isShell) {
    const workdir = input.workdir;
    if (typeof workdir === 'string' && workdir.length) {
      const pathApi = path.win32.isAbsolute(root) ? path.win32 : path;
      const target = pathApi.resolve(cwd, workdir);
      const relative = pathApi.relative(root, target);
      if (relative === '..' || relative.startsWith('..' + pathApi.sep) || pathApi.isAbsolute(relative))
        return 'Рабочий каталог mutating shell находится вне CubeGame workspace';
    }
    const prohibited = [
      /git\s+push\b[^\n]*(?:--force|-f\b|\bmain\b|\bintegration\/m0-all\b)/i,
      /git\s+reset\s+--hard/i,
      /git\s+clean\s+-[^\n]*[fd]/i,
      /git\s+(?:switch|checkout)\b/i,
      /(?:rm\s+-\S*r\S*f|rm\s+-\S*f\S*r|remove-item\b[^\n]*-recurse|rmdir\s+\/s|del\s+\/s)/i,
      /(?:format-volume|clear-disk|diskpart)\b/i,
    ];
    if (prohibited.some(re => re.test(command))) return 'Операция с разрушительным эффектом / переключением ветки требует отдельного разрешения';
  }
  if (fileEdit) {
    const candidate = input.file_path ?? input.path ?? input.filename;
    if (typeof candidate === 'string') {
      const pathApi = /^[A-Za-z]:[\\/]/.test(root) ? path.win32 : path;
      const target = pathApi.resolve(cwd, candidate);
      const relative = pathApi.relative(root, target);
      if (relative === '..' || relative.startsWith('..' + pathApi.sep) || pathApi.isAbsolute(relative)) return 'Изменение файла вне CubeGame workspace запрещено';
      if (relative.split(pathApi.sep).includes('.git')) return 'Прямая запись внутрь .git запрещена';
    }
  }
  return null;
}

async function main() {
  let payload;
  try { payload = JSON.parse(readFileSync(0, 'utf8')); }
  catch { process.stderr.write('CubeGame hook: не удалось разобрать JSON от DSH\n'); process.exitCode = 2; return; }
  const cwd = process.cwd();
  const root = git(['rev-parse', '--show-toplevel'], cwd);
  const branch = git(['symbolic-ref', '--quiet', '--short', 'HEAD'], cwd);
  const reason = evaluateTool(payload, { branch, root, cwd });
  if (reason) { process.stderr.write(`CubeGame pre-tool guard: ${reason}\n`); process.exitCode = 2; }
}

if (process.argv[1] && fileURLToPath(import.meta.url) === path.resolve(process.argv[1])) await main();
