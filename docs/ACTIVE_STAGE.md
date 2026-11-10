# ACTIVE STAGE — M0-F4 PRESENTATION

**Canonical branch:** `integration/m0-all` · **База:** `05336984`

**Review branch:** `codex/m0-f4-presentation` · **Status:** `READY_FOR_PRODUCER_ACCEPTANCE`

- M0-F3 принят продюсером и включён в canonical на `3cd79b4366dda4ff0d2410be22ee11d084eefaf1`.
- ТЗ этапа: [M0-F4 PRESENTATION](stages/M0_F4_PRESENTATION_STAGE.md).
- Все хвосты неполного переноса Alpha: [M0 Alpha Parity Backlog](planning/M0_ALPHA_PARITY_BACKLOG.md).
- Подключены декодирование и воспроизведение локального аудио, HUD, инвентарь, навыки, услуги, карта, журнал и уведомления поверх F1–F3.
- Unity 6000.3.27f2. EditMode 755 PASS / 0 FAIL / 2 прежних SKIP. PlayMode 73 PASS / 0 FAIL / 0 SKIP, сквозной F4 PASS. Ошибок компиляции 0. [Отчёт F4](reports/M0_F4_PRESENTATION_REPORT.md)

## Предыдущий этап — M0-F3 GAMEPLAY LOOP

**Status:** `ACCEPTED` 10 ноября 2026 · **Commit:** `3cd79b4366dda4ff0d2410be22ee11d084eefaf1`

- Развитие, бой/лут, торговля/услуги, Life Potion, охота, сундуки и persistence работают в одном игровом сеансе.
- EditMode 754 PASS / 0 FAIL / 2 SKIP; PlayMode 70/70; compilation errors 0.
- Известные `PARTIAL/UNKNOWN/CUBEGAME_CONTRACT/PLACEHOLDER` перенесены в parity backlog и не считаются забытыми.
- [Отчёт F3](reports/M0_F3_GAMEPLAY_LOOP_REPORT.md)

---

## Предыдущий этап — M0-F2 INTEGRATION CHECKPOINT

Продюсер присвоил `ACCEPTED` коммиту `4b9480836beeba859340938486f4200d16c1549e` 10 ноября 2026. `integration/m0-all` переведена на этот коммит fast-forward. EditMode 744 PASS, 0 FAIL, 2 SKIP. PlayMode 69 PASS, 0 FAIL. Ошибок компиляции 0. [Отчёт интеграции](reports/M0_F2_INTEGRATION_REPORT.md)

---

# Предыдущий этап — M0-F2B COMBAT RUNTIME

**Review branch:** `claude/m0-f2b-combat-runtime` (база F2A `5f197f9` + DeepSeek evidence `08ae4e3`) · **Status:** `READY_FOR_REVIEW`

- Атака героя выбирает один путь по оружию и действию Alpha: melee, летящий снаряд (лук/арбалет/бумеранг/жезл/браслет — числа из байтов), объём посоха или луч до 200 блоков (`0x1C/0x5E/0x5F`, бомба 10). Fallback в melee нет.
- Попадание всегда через `CombatSystem.ResolveHit`: владелец не поражается, один hit на снаряд/взмах, удаление ровно один раз, корректно через смену начала координат и save/load (снаряды не сохраняются).
- Масштаб вида — одно правило для героя и NPC (эмулированная матрица размещения `Creature_drawModel`); hit box — размер вида Alpha.
- EditMode 746/744/0/2, PlayMode 68/68, `error CS = 0`. `PARTIAL`: формулы урона Alpha для дальнего боя, подруливание бумеранга, способности/бомба без ввода, dodge `UNKNOWN`. [Отчёт M0-F2B](reports/M0_F2B_COMBAT_RUNTIME_REPORT.md)

---

# Предыдущий этап — M0-F2A CHARACTER RUNTIME

**Review branch:** `claude/m0-f2a-character-runtime` (база M0-F1 `748d650`) · **Status:** `READY_FOR_REVIEW`

- Внешность героя из сейва (раса, пол, лицо, причёска, цвет волос) определяет модель в сцене; экипировка рисуется моделями, выбранными по байтам предмета.
- Один владелец визуала героя и один владелец лута; лут и герой корректны после смены начала координат; визуал восстанавливается после save → exit → load.
- EditMode 679/677/0/2, PlayMode 62/62, `error CS = 0`. `PARTIAL`: OrcFemale, блоки наплечников/амулета/лампы, относительный рост видов. [Отчёт M0-F2A](reports/M0_F2A_CHARACTER_RUNTIME_REPORT.md)
- Параллельно (Codex, F2B): ranged/projectiles/abilities — вне F2A.

---

# Предыдущий этап — M0-F1 WORLD FIX

**Canonical plan:** [D90](decisions/D90-m0-five-stage-finish-plan.md) / [M0 remaining plan](planning/M0_REMAINING_PLAN_D90.md)  
**Branch:** `integration/m0-all`  
**Review branch:** `codex/m0-f1-world-runtime`

**Status:** `READY_FOR_REVIEW` в ветке ревью. Каноническое слияние следует за проверкой продюсера.

## Результат M0-F1

- В Pilot подключены recovered population, жизненный цикл, игровое время и драйвер движения из F1A Claude `9e50915`.
- В новом генераторе `/6` единый D3-маршрут формирует город и данжи с сундуками. Коллайдер перестраивается при voxel edit. Дельты объектов, вокселей и таймер респауна прошли streaming и save/exit/load.
- Полный EditMode **664 passed, 0 failed, 2 skipped**. Полный PlayMode **60 passed, 0 failed, 0 skipped**. `error CS = 0`. Оба прежних падения коллайдера исправлены.
- Расписания естественных городских NPC и часть генерации Alpha остаются `PARTIAL`. [Отчёт M0-F1](reports/M0_F1_WORLD_FIX_REPORT.md)

## Входное состояние D90

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

## Поставленные задачи M0-F1

1. Подключить recovered D1/D2 к реальному миру и игровому времени.
2. Исправить два известных PlayMode world-collider fail.
3. Подключить city/dungeon/chest providers в один world-content route.
4. Проверить unload/reload, stable IDs, persistence и отсутствие дублей.
5. Прогнать полный EditMode + PlayMode и оформить `M0_F1_WORLD_FIX_REPORT.md`.

## Тестовая база до M0-F1

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
