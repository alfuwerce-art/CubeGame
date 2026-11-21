import test from 'node:test';
import assert from 'node:assert/strict';
import { evaluateTool } from './hook-pretool.mjs';

const ctx = { branch: 'review/harness-layering-20261121', root: 'C:\\Projects\\CubeGame', cwd: 'C:\\Projects\\CubeGame' };
const shell = command => ({ tool_name: 'bash', tool_input: { command } });

test('allows read-only shell', () => assert.equal(evaluateTool(shell('git status --short'), ctx), null));
test('rejects force push', () => assert.match(evaluateTool(shell('git push --force origin main'), ctx), /разрешения/));
test('rejects reset hard', () => assert.match(evaluateTool(shell('git reset --hard HEAD~1'), ctx), /разрешения/));
test('protects integration branch', () => assert.match(evaluateTool({ tool_name: 'write', tool_input: { file_path: 'AGENTS.md' } }, { ...ctx, branch: 'integration/m0-all' }), /ветке/));
test('rejects write outside workspace', () => assert.match(evaluateTool({ tool_name: 'write', tool_input: { file_path: 'C:\\Other\\file.txt' } }, ctx), /вне/));
test('rejects .git manual edit', () => assert.match(evaluateTool({ tool_name: 'edit', tool_input: { file_path: 'C:\\Projects\\CubeGame\\.git\\config' } }, ctx), /\.git/));
test('allows scoped edit', () => assert.equal(evaluateTool({ tool_name: 'write', tool_input: { file_path: 'docs\\agents\\test.md' } }, ctx), null));
