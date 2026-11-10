# M0-F4 PRESENTATION

**Status:** ACTIVE  
**Canonical base:** `integration/m0-all` после принятого M0-F3  
**Plan:** D90  
**Parity debts:** [M0 Alpha Parity Backlog](../planning/M0_ALPHA_PARITY_BACKLOG.md)

## Цель

Подключить presentation-слой к уже работающей M0-игре: звук и полноценные игровые интерфейсы должны позволять пользоваться системами F1–F3 без временного debug/IMGUI-представления.

Это всё ещё перенос и технический baseline Alpha. Новый художественный интерфейс CubeGame проектируется позже отдельным этапом.

## F4-A — AUDIO

1. Исправить RIFF/WAV parsing для всего каталога 93 звуков.
2. Подключить runtime loading/playback без публикации оригинальных Alpha assets.
3. Связать подтверждённые игровые события с audio events.
4. Проверить lifecycle, отсутствие дублей и ошибки при streaming/save/load.

## F4-B — HUD

1. Игровой HUD для уже существующих ресурсов/состояний героя.
2. Боевой feedback и необходимые уведомления.
3. Не дублировать gameplay state в presentation-слое.

## F4-C — INVENTORY / SKILLS / SERVICES

1. Игровой inventory UI поверх существующего item/equipment runtime.
2. Skills/specialization UI поверх F3 progression.
3. Доступ к trade/services/crafting без временного debug UI.
4. Отказы и транзакции должны оставаться атомарными как в F3.

## F4-D — MAP / JOURNAL / ICONS

1. Карта поверх существующего world state.
2. Quest journal поверх F3 MissionJournal.
3. Иконки предметов/состояний и минимально необходимый feedback.
4. UI не должен менять правила gameplay только ради отображения.

## F4-E — INTEGRATION / REGRESSION

Проверить единый сценарий:
`new world → character → travel → combat → loot → inventory → city → trade/services → crafting → quest/journal → map → save → exit → load → continue`.

Обязательно:
- полный EditMode;
- полный PlayMode;
- presentation PlayMode/сквозной тест;
- `error CS = 0`;
- документированный список `PARTIAL/UNKNOWN/PLACEHOLDER`;
- сверка с [Alpha parity backlog](../planning/M0_ALPHA_PARITY_BACKLOG.md).

## Не входит

- художественная переработка моделей/анимаций;
- S06 render/environment evolution;
- финальный новый дизайн интерфейсов S07;
- новые RPG-механики M2;
- молчаливое «исправление» неизвестных правил Alpha собственными догадками.

## Exit criterion

Все уже реализованные M0 gameplay-системы доступны через игровой presentation layer; 93 WAV корректно обрабатываются техническим контуром; временный debug UI больше не является обязательным способом пользоваться ключевыми системами. Этап заканчивается `READY_FOR_PRODUCER_ACCEPTANCE`, после чего открывается M0-F5 FINAL.
