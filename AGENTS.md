# AGENTS.md — CubeGame current rules

## Перед началом любой работы

Прочитать:
1. `PROJECT_MEMORY.md`
2. `docs/ACTIVE_STAGE.md`
3. `docs/decisions/D90-m0-five-stage-finish-plan.md`
4. ТЗ текущего этапа/отчёты, на которые ссылается ACTIVE_STAGE.

Для задач эволюционного геймдизайна дополнительно сверять [GDD](docs/game-design/GDD_DRAFT.md), [D91](docs/decisions/D91-document-consolidation-release-batches.md), [Master Roadmap (review)](docs/planning/ROADMAP_CONSOLIDATION_PROPOSAL.md) и утверждённые Dxx; для визуальных задач — [Art Direction](docs/art/ART_DIRECTION_DRAFT.md) и [Production Pipeline](docs/production/PRODUCTION_PIPELINE_DRAFT.md). Эти документы не открывают этапы автоматически. Навигация: [docs/README.md](docs/README.md).

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

## Разграничение полномочий документации

- GDD = требуемый игровой результат; Dxx = доказательство утверждённого решения; Roadmap = последовательность; ACTIVE_STAGE и stage-ТЗ = единственный исполнимый scope; AGENTS/Production = способ работы.
- Не смешивать `DESIGN_APPROVED`, `STAGE_AUTHORIZED`, `TESTED` и `ACCEPTED`.
- D91: сначала полноценный одиночный релиз, сеть позже; кампания D60 — отдельная крупная веха после первого релиза. M0-F5 — техническая приёмка фундамента, а не релизное разрешение.
- D67/D91: художественная приёмка — эталон семейства + партия продюсером, каждый Asset ID проходит техническую проверку; обязательной личной поштучной художественной приёмки нет.
- Старые формулировки D35 о поштучной приёмке, D59 о совместном релизе/сети и устаревшие stage-статусы трактовать по новым явным решениям, не переписывать историю.

## Приёмка

Агент завершает задачу максимум статусом `READY_FOR_REVIEW`.
`ACCEPTED` ставит только продюсер.

После завершения утверждённого этапа обновляются:
- `PROJECT_MEMORY.md`
- `docs/ACTIVE_STAGE.md`
- соответствующий отчёт/план.

## Текущий приоритет

D90 / `M0-F4 PRESENTATION`:
RIFF/WAV и runtime audio → HUD → inventory/skills/services UI → map/journal/icons → presentation integration и полная Unity-регрессия.

Обязательный reference: `docs/planning/M0_ALPHA_PARITY_BACKLOG.md`. Неполные элементы Alpha нельзя молча считать закрытыми только потому, что соответствующий этап принят.
