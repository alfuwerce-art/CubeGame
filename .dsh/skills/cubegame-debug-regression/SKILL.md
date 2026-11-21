---
name: cubegame-debug-regression
description: "Diagnose and repair reproducible Unity, C#, game runtime or test failures through root cause tracing and focused regression checks."
---
# Debug + regression

1. Reproduce the **observed** problem with exact scene/build, logs, SHA and steps. If no repro, label `UNREPRODUCED` and do not guess cause.
2. Read relevant code, owners and call graph, `docs/architecture/DEPENDENCY_MAP.md`, actual Unity version, source tests. Prefer LSP if available, otherwise ripgrep/PowerShell and compile output; never fabricate class names.
3. Distinguish scene/view defects, Core state/logic defects, Input/UI routing, serialization and tools/asset metadata defects.
4. Form one or two falsifiable hypotheses. Add minimal test or logging; no new framework, no replacement system to hide bug.
5. Fix root cause within task scope; preserve data migrations, deterministic generation, ID stability and previous accepted behavior.
6. Run targeted EditMode/PlayMode then affected regression; inspect actual XML, test count and exit code. When user-facing: build/open Windows Player and record concrete scenario.
7. Do independent diff review and note side effects, tests skipped, needed producer visual approval.

Return `SYMPTOM / REPRO / ROOT_CAUSE_EVIDENCE / PATCH / TESTS / PLAYER / REGRESSIONS / SHA / BLOCKERS`. `PARTIAL` if any mandatory check fails.
