---
name: cubegame-unity-editor-automation
description: "Create small repeatable Unity Editor or CLI scripts to inspect and modify authorized CubeGame content without inventing editor APIs or restarting asset factory."
---
# Unity Editor scripting & CLI

## Before touching anything
- Read `AGENTS.md`, `ACTIVE_STAGE.md`, approved task, [Unity CLI protocol](../../../docs/agents/UNITY_CLI_PROTOCOL.md), [QA](../../../docs/QUALITY_ATTRIBUTES.md).
- Check local Unity version from `ProjectSettings/ProjectVersion.txt`; expected editor has been 6000.3.27f2, not permission to upgrade. Find *actual* installed CLI and Editor.
- Run `git status` and `node tools/harness/preflight.mjs`. Use isolated branch, no writes to current gameplay outside `allowed_paths`.

## Script workflow
1. Specify one operation (e.g., validate materials, missing prefabs, names, import settings, scene references).
2. Prefer existing Unity editor APIs / batch mode / current scripts. Author one narrowly scoped C# Editor script **only when needed**; keep editor code out of Windows runtime.
3. Make operation idempotent where possible. Include **dry-run and listing of affected asset paths**. Require explicit confirmation if mass mutation / delete is involved.
4. Execute via verified Unity 6.3 editor or CLI, not imagined commands; inspect import/compile logs and exit statuses separately.
5. Run checks appropriate to the change, capture actual test XML (nonzero tests), runtime/Player verification for visible changes.
6. Show diff, list created/modified assets, rollback plan, timings and `NOT_RUN` when environment unavailable.
7. Do **not** enable Unity MCP by default: compare documented CLI and tool capabilities first.

## Exit
`SCRIPT/COMMAND / INPUT / OUTPUT / BEFORE-AFTER / DIFF / CHECKS / BLOCKERS / READY_FOR_OWNER_REVIEW`. Never equate successful script execution with completed playable feature.
