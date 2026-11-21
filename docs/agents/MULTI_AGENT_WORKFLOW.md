# Multi-agent workflow — CubeGame

**Status:** active general collaboration rules. Project and scope: [AGENTS](../../AGENTS.md), [PROJECT_MEMORY](../../PROJECT_MEMORY.md), [ACTIVE_STAGE](../ACTIVE_STAGE.md). Model automation pipeline is closed under [D95](../decisions/D95-single-repository-close-model-pipeline.md).

## Roles

The owner sets the task and accepts the result. ChatGPT supports architecture/review. DeepSeek Harness and Codex implement only separately authorized tasks; the owner assigns the executor. Their output is evidence for review, not owner acceptance.

## Workspaces and Git

- Only one project repository: `alfuwerce-art/CubeGame`. Local root: `D:\Projects\CubeGame`.
- Parallel work is allowed only in isolated worktrees/branches with separate Unity caches and ownership of files; never let two agents operate on the same Editor/Library or switch each other's branch.
- Read `PROJECT_MEMORY.md`, `docs/ACTIVE_STAGE.md`, `AGENTS.md` and task-specific contracts before editing.
- Verify `git status`, remote HEAD, diff and report before push. Do not overwrite uncommitted changes, delete remote refs, force-push or merge without the owner's explicit authorization.
- Task handoff: task ID, branch, exact SHA, changed paths, tests and reproduced errors, limitations and blockers, proposed next steps, `READY_FOR_OWNER_REVIEW`.
- A passed test is not a model's visual approval nor `ACCEPTED`. No new feature, production framework or tool installation is implicitly authorized by a successful technical check.

## Closed work

No agent may restart or port the discarded asset factory or its previous approved workflow. Relevant experiments are history, not an approved stage. New content is developed inside CubeGame after an explicit task.
