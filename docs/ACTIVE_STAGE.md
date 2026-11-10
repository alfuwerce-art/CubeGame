# ACTIVE STAGE — M0 ALPHA FULL UNITY PORT

**Decision:** [D92 — full Alpha-to-Unity port](decisions/D92-m0-full-alpha-unity-port.md)  
**Stage:** [M0 ALPHA FULL UNITY PORT](stages/M0_ALPHA_FULL_UNITY_PORT_STAGE.md)  
**Status:** `ACTIVE / REWORKED`  
**Canonical branch remains:** `integration/m0-all`  
**Documentation review branch:** `review/m0-full-unity-port-rework`

## Решение продюсера 2026-11-11

M0 полностью переработан. Цель — перенести Cube World Alpha 2013 как цельную одиночную игру на Unity. Старый подход «подсистема существует + тест проходит = этап готов» больше не используется.

D90 F1–F5 и прежние F1–F4 результаты сохраняются как инженерные checkpoint'ы и доказательная база, но **не доказывают parity соответствующей системы**.

## Текущий implementation checkpoint

`codex/m0-alpha-code-parity-p1` @ `c1f9f758598d90d76974d352d1a3689ceb04c8cc` — `PARTIAL`.

Подтверждено:
- восстановлен порядок menu/character/world state по Alpha code;
- исправлен конфликт Space vs inventory;
- частично восстановлен streaming priority.

Не закрыто:
- полный streaming/render distance;
- деревья/трава/цветы/world-object pipeline;
- корректная классификация actor/world-object ID;
- NPC/creature AI;
- action→animation→hit→damage sync;
- Alpha HUD/inventory/map;
- city/dungeon/quest/content parity;
- полный Alpha gameplay loop.

## Обязательный метод

Для каждого системного контура:

`FIND ORIGINAL CODE → TRACE → DATA/STATE/RNG → SPEC → UNITY IMPLEMENTATION → RUNTIME INTEGRATION → WINDOWS PLAYER → COMPARE → PRODUCER REVIEW`.

`UNKNOWN`, `PARTIAL`, `PLACEHOLDER_NOT_ALPHA` и непринятый `CUBEGAME_CONTRACT` на обязательном основном пути блокируют PASS системы.

## Источники

- [D92](decisions/D92-m0-full-alpha-unity-port.md)
- [Stage matrix](stages/M0_ALPHA_FULL_UNITY_PORT_STAGE.md)
- [Alpha parity backlog](planning/M0_ALPHA_PARITY_BACKLOG.md)
- [P1 code-parity report](reports/M0_ALPHA_CODE_PARITY_P1_REPORT.md)
- [Runtime recovery report](reports/M0_ALPHA_RUNTIME_RECOVERY_REPORT.md)
- [Root-cause audit](reports/M0_ALPHA_TO_UNITY_ROOT_CAUSE_AUDIT.md)
