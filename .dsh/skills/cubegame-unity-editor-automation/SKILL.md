---
name: cubegame-unity-editor-automation
description: "Create narrow reusable Unity Editor/CLI operations for one authorized change."
---
# cubegame-unity-editor-automation
1. Confirm task scope, actual Unity/CLI version and current project state.
2. Prefer existing Editor APIs; build narrowly scoped scripts with dry run and affected-file list.
3. Avoid editing gameplay runtime from Editor-only tools.
4. Run import/compile/targeted tests and verify actual results; visible changes require Player evidence.
5. Report exact command, inputs, outputs, rollback, SHA and blockers.
