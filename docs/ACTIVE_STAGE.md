# ACTIVE STAGE — M0-F1 WORLD FIX

**Canonical plan:** [D90](decisions/D90-m0-five-stage-finish-plan.md) / [M0 remaining plan](planning/M0_REMAINING_PLAN_D90.md)  
**Branch:** `integration/m0-all`  
**Status:** `READY_TO_ASSIGN` — план утверждён, результат не ACCEPTED.

## Текущее состояние

Последний сведённый код до D90-cleanup: `fce38e0`.

Claude D1/D2 review-fix — `READY_FOR_REVIEW`:
- прежняя D1 spawn-table отменена как неверная интерпретация торгового ассортимента;
- восстановлены `cube::Spawn`, 35 spawn construction sites, wild 3×3 rules, lifecycle/respawn;
- восстановлены `SpawnLocationBehavior` и `WalkPathBehavior`;
- runtime в `GameBootstrap` ещё не подключён.

Codex D3–D6 audit — `READY_FOR_REVIEW`:
- города/данжи/сундуки не в основном runtime route;
- два владельца ground loot и origin drift;
- projectiles = data/math без attack→hit runtime;
- 93 sounds cataloged, playback отсутствует;
- 32 WAV metadata entries parsed неверно.

## M0-F1 задачи

1. Подключить recovered D1/D2 к реальному миру и игровому времени.
2. Исправить два известных PlayMode world-collider fail.
3. Подключить city/dungeon/chest providers в один world-content route.
4. Проверить unload/reload, stable IDs, persistence и отсутствие дублей.
5. Прогнать полный EditMode + PlayMode и оформить `M0_F1_WORLD_FIX_REPORT.md`.

## Тестовая база

После merge Claude+Codex:
- EditMode: **639 total / 637 passed / 0 failed / 2 skipped / error CS 0**.
- PlayMode после merge не повторён.
- Последний полный PlayMode Claude до merge: **57 total / 54 passed / 2 failed / 1 skipped**.
- Известные fail: `TerrainEditSurvivesSaveExitAndLoad`, `VoxelEditBuildsColliderAndSurvivesSaveLoad`.

## Не делать сейчас

- Не начинать художественную переработку CubeGame поверх незакрытого M0.
- Не возвращаться к старой D71–D89 схеме распределения агентов.
- Не считать historical READY_FOR_REVIEW как ACCEPTED.
- Не публиковать Alpha assets.
