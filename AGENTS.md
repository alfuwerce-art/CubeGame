# AGENTS.md — CubeGame current rules

## Перед началом любой работы

Прочитать:
1. `PROJECT_MEMORY.md`
2. `docs/ACTIVE_STAGE.md`
3. `docs/planning/ROADMAP_DRAFT.md` — действующая дорожная карта (G0…G15)
4. `docs/decisions/D93-development-pivot-own-game.md` — текущее направление
5. ТЗ активного этапа: `docs/stages/G1_TECHNICAL_FOUNDATION_STAGE.md` и связанные документы
6. `docs/planning/G0_EXISTING_WORK_REUSE_AUDIT.md` — что уже сделано и что переиспользуется

GitHub — основной источник актуального состояния.

## Текущая цель

CubeGame — **самостоятельная игра**, которая разрабатывается с 2026-11-17 по дорожной карте G0…G15.

- Полный перенос Cube World Alpha 2013 **не является** active target.
- Старый `M0 ALPHA FULL UNITY PORT` = `LEGACY / RESEARCH BASE / NOT A RELEASE TARGET`. P7–P10 не продолжаются.
- GDD и утверждённые игровые решения ([DECISIONS_001-036](docs/planning/DECISIONS_001-036.md)) не пересобираются.
- Alpha 2013 — источник исследований, алгоритмов и формул, а не спецификация. Её изучают под конкретный вопрос CubeGame.

## Метод работы

Для каждой системы CubeGame:

`ТРЕБОВАНИЕ CUBEGAME → ПРОВЕРИТЬ СУЩЕСТВУЮЩЕЕ → RESEARCH ПРИ НЕОБХОДИМОСТИ → СОБСТВЕННЫЙ DESIGN/SPEC → ПРОТОТИП → ИНТЕГРАЦИЯ → PLAYTEST → ПРИЁМКА`

Alpha research допустим только внутри этого цикла:

`CUBEGAME PROBLEM → CURRENT IMPLEMENTATION → ALPHA RESEARCH → EXTERNAL RESEARCH → OPTIONS → CUBEGAME DECISION → IMPLEMENTATION → PLAYTEST`

Не исследовать Alpha только потому, что функция существует и ещё не восстановлена.

## Доказательность

Категории наследия Alpha:
- `ALPHA_VERIFIED` — намеренно сохраняем подтверждённый алгоритм/формулу/поведение Alpha;
- `ALPHA_ADAPTED` — используем принцип исследования, архитектура и поведение адаптированы;
- `CUBEGAME_NATIVE` — собственная система.

В legacy/research материалах сохраняются исторические маркеры:
`ALPHA_CODE_STATIC`, `ALPHA_DECOMPILED`, `EMULATED_ALPHA_CODE`, `CUBEGAME_CONTRACT`, `PLACEHOLDER_NOT_ALPHA`, `PARTIAL`, `UNKNOWN`.

Наличие класса или теста не доказывает работающую систему в игре. Обязательно разделять:
`CODE EXISTS → RUNTIME CONNECTED → TESTED → WINDOWS PLAYER VERIFIED → PRODUCER REVIEW`.

## Что не является blocker

Открытые пункты [legacy parity backlog](docs/planning/M0_ALPHA_PARITY_BACKLOG.md) не блокируют разработку CubeGame. Блокирует только то, что нужно **собственной игре** на текущем этапе. Незакрытая деталь Alpha, без которой играбельная CubeGame возможна, — не blocker.

Нельзя использовать это правило, чтобы выдать незаконченную систему за готовую: статус системы ставится по её Definition of Done (см. `ACTIVE_STAGE.md` §7).

## Работа с кодом

- Сначала использовать уже сделанное; не переписывать без причины (`REUSE BEFORE REWRITE`).
- Исправлять ошибочные интерпретации старых исследований.
- Не смешивать ID-space actor/world-object/vegetation/item/structure.
- Не писать плагины, editor tools и frameworks вместо игрового runtime.
- Одноразовый диагностический script допустим только для доказательства конкретного факта.
- Один Unity slot.
- Одни и те же файлы параллельно не редактировать.
- Force-push запрещён без прямого решения продюсера.
- Alpha original read-only.
- Alpha assets не коммитить и не публиковать.

## Windows build / пользовательская версия

Editor недостаточен. Видимые и игровые системы проверяются в Windows Player.

Постоянный пользовательский launcher один. После успешной сборки и smoke-test последняя версия заменяет единственную live-версию; старые пользовательские builds не являются отдельными версиями проекта.

## Приёмка

Агент завершает задачу максимум `READY_FOR_REVIEW` / `READY_FOR_PRODUCER_REVIEW`.

`ACCEPTED` этапа ставит только продюсер.

После утверждённого изменения этапа синхронизировать:
- `PROJECT_MEMORY.md`
- `docs/ACTIVE_STAGE.md`
- stage/decision/report.
