---
name: cubegame-doc-audit
description: Use for reviewing or synchronizing CubeGame documents, decisions, roadmap and agent rules without changing Unity gameplay code.
---
# CubeGame — documentation audit

1. Проверить `AGENTS.md`, `PROJECT_MEMORY.md`, `docs/ACTIVE_STAGE.md`, `docs/CONCEPT.md`, `docs/GDD.md`, `docs/ROADMAP.md`, действующий Decision Registry и D93/D95.
2. Исторические Alpha/M0 и Factoria-материалы помечать историческими, не возобновлять закрытый конвейер. `UNDECIDED` нельзя превратить в `APPROVED`.
3. Сопоставить цель → GDD → ADR → Roadmap → ACTIVE_STAGE → AGENTS → памя́ть; перечислить точные противоречия и источники, предложить минимум изменений.
4. Отдельная ревью-ветка, `git status`, `node tools/harness/preflight.mjs`; без игровых `.cs`, `.unity`, Unity build и удаления истории, если это не разрешено.
5. Проверить ссылки, ссылки на статус PR/ветку, Markdown и diff; не выдавать тестов Unity, которых не запускали.
6. Подготовить список исправлений, конфликтов на решение владельца, SHA, PR и `READY_FOR_OWNER_REVIEW`.
