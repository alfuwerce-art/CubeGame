# AGENTS.md — CubeGame current rules

## Перед началом любой работы

Прочитать:
1. `PROJECT_MEMORY.md`
2. `docs/ACTIVE_STAGE.md`
3. `docs/decisions/D92-m0-full-alpha-unity-port.md`
4. `docs/stages/M0_ALPHA_FULL_UNITY_PORT_STAGE.md`
5. релевантные Alpha research/reports и parity backlog.

GitHub — основной источник актуального состояния.

## Текущая цель

M0 = **полный перенос одиночной Cube World Alpha 2013 в Unity как связанной игры**.

Прежние D90 F1–F4 являются инженерными checkpoint'ами, а не доказательством полного переноса мира, боя, UI или gameplay.

## Метод работы

Для каждой системы:

`FIND ORIGINAL CODE → TRACE CALL GRAPH → IDENTIFY DATA/STATE/RNG → SPEC → IMPLEMENT → CONNECT RUNTIME → WINDOWS PLAYER → COMPARE → REVIEW`.

Не реализовывать неизвестную Alpha-механику предположением только ради рабочего стенда.

## Доказательность

Использовать:
- `ALPHA_CODE_STATIC`
- `ALPHA_DECOMPILED`
- `EMULATED_ALPHA_CODE`
- `CUBEGAME_CONTRACT`
- `PLACEHOLDER_NOT_ALPHA`
- `PARTIAL`
- `UNKNOWN`

Но наличие кода или теста не означает parity.

Обязательная лестница:
`ORIGINAL LOGIC UNDERSTOOD → UNITY RUNTIME CONNECTED → SYSTEMS LINKED → WINDOWS PLAYER VERIFIED → PRODUCER REVIEW`.

## Blocking rule

Если на обязательном Alpha runtime-path остаётся:
- `UNKNOWN`;
- существенный `PARTIAL`;
- `PLACEHOLDER_NOT_ALPHA`;
- непринятый `CUBEGAME_CONTRACT`;

система не получает PASS.

Пример: отдельные AI, animation и damage PASS не закрывают combat, пока `decision → action → animation → strike → hit/damage → recovery` не являются одной восстановленной цепочкой.

## Работа с кодом

- Сначала использовать уже сделанное; не переписывать без причины.
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

Агент завершает задачу максимум `READY_FOR_PRODUCER_REVIEW` / `PARTIAL`.

`PASS` системного блока и `ACCEPTED` этапа ставит только продюсер.

После утверждённого изменения этапа синхронизировать:
- `PROJECT_MEMORY.md`
- `docs/ACTIVE_STAGE.md`
- stage/decision/report;
- parity backlog.
