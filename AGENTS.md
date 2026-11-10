# AGENTS.md — CubeGame current rules

## Перед началом любой работы

Прочитать:
1. `PROJECT_MEMORY.md`
2. `docs/ACTIVE_STAGE.md`
3. `docs/decisions/D90-m0-five-stage-finish-plan.md`
4. ТЗ текущего этапа/отчёты, на которые ссылается ACTIVE_STAGE.

GitHub — основной источник актуального состояния. Старые D71–D89 workflow/role документы — история и доказательная база; они не задают текущую схему исполнения.

## Текущая модель работы

- Каноническая ветка: `integration/m0-all`.
- Роли назначаются **по конкретной задаче**, а не закреплены навсегда за Claude/DeepSeek/Codex.
- Один Unity slot.
- Одни и те же файлы параллельно не редактировать.
- Изолированный worktree допустим для независимой задачи, но итог должен быть сведен в canonical branch и проверен.
- Перед публикацией проверить новый remote HEAD; force-push запрещён без прямого решения продюсера.
- Не менять `main`, принятые S02–S04, формат сейва или игровые решения без применимого разрешения.
- Alpha assets не коммитить и не публиковать.

## Доказательность

Использовать явные статусы:
- `ALPHA_CODE_STATIC`
- `EMULATED_ALPHA_CODE`
- `ALPHA_DECOMPILED`
- `CUBEGAME_CONTRACT`
- `PLACEHOLDER_NOT_ALPHA`
- `PARTIAL`
- `UNKNOWN`

Наличие класса или теста не означает runtime integration. Отдельно проверять:
`CODE EXISTS → RUNTIME CONNECTED → TESTED → ALPHA PROVENANCE`.

## Приёмка

Агент завершает задачу максимум статусом `READY_FOR_REVIEW`.
`ACCEPTED` ставит только продюсер.

После завершения утверждённого этапа обновляются:
- `PROJECT_MEMORY.md`
- `docs/ACTIVE_STAGE.md`
- соответствующий отчёт/план.

## Текущий приоритет

D90 / `M0-F3 GAMEPLAY LOOP`:
progression → items/equipment → NPC/economy → quests → persistence и полная Unity-регрессия.
