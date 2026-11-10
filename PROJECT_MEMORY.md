# CubeGame — PROJECT MEMORY

**Updated:** 2026-11-10  
**Canonical branch:** `integration/m0-all`  
**Current plan:** [D90 — M0 five-stage finish](docs/decisions/D90-m0-five-stage-finish-plan.md)  
**Current stage:** [M0-F1 WORLD FIX](docs/ACTIVE_STAGE.md)

## 1. Главная цель M0

Собрать максимально близкий к Cube World Alpha 2013 одиночный игровой цикл в одной Unity-игре, а уже после закрытия M0 переходить к эволюционным улучшениям CubeGame.

Финальный M0 loop:
`new world → character → travel → combat → loot → city → trade → dungeon → boss → quest → save/load → continue`.

## 2. Последний сведённый результат

До D90-cleanup canonical HEAD был `fce38e0`, объединяющий:
- Codex D3–D6 audit `762ac5d`;
- Claude D1/D2 review-fix code `47c5b1a` и его последующее сведение.

Unity после merge:
- EditMode: **639 total / 637 passed / 0 failed / 2 skipped / error CS 0**.
- Полный PlayMode после merge не повторён.
- Последний полный PlayMode Claude до merge: **57 total / 54 passed / 2 failed / 1 skipped**.
- Два известных fail существовали до D1/D2: stale world collider после voxel edit/save/load.

## 3. Ключевые новые факты D1/D2

Claude review-fix: [D1_D2_SPAWN_PATROL_REVIEW_FIX](docs/reports/D1_D2_SPAWN_PATROL_REVIEW_FIX.md).

Отменено старое утверждение DeepSeek, что `0x5C9B30/0x5CA0A0` дают таблицу видов существ: это ассортимент/предметы NPC-магазинов, а `+0x04` — item modifier, не probability weight.

Восстановлено:
- `cube::Spawn` size 0x10F0;
- 35 construction sites в 7 функциях;
- wild population grid 3×3;
- spawn radius 200;
- despawn distance 512;
- death timer 1,200,000 ms или смена игрового дня;
- NPC `SpawnLocationBehavior`;
- NPC `WalkPathBehavior`.

Код разделён на recovered Alpha path и `FallbackPopulationPolicy`.

**Важно:** recovered population/schedule ещё не подключены в `GameBootstrap`. Без kind selector `AlphaWorldPopulation` пуст; ошибочные «расы игрока как существа» больше не спавнятся.

Открыто: exact rand() stream, часть условий wild cells, 22 non-grid construction sites, NPC equipment R-1, spawn timer persistence.

## 4. Ключевые факты D3–D6

Codex audit: [CODEX_D3_D6_INDEPENDENT_AUDIT](docs/reports/CODEX_D3_D6_INDEPENDENT_AUDIT.md).

- D3: дома/W-18 частично подключены; города, данжи и сундуки отсутствуют в основном gameplay route. Dungeon carver — реконструкция CubeGame, не Alpha parity.
- D4: equipment events работают частично; race/sex/appearance не применяются к runtime hero. Ground loot имеет два визуальных владельца и не следует floating origin.
- D5: projectile models/flight data существуют, но `attack → projectile → collision → CombatSystem.ResolveHit` отсутствует.
- D6: 93 sounds cataloged; playback отсутствует. WAV metadata parser ошибается на 32 файлах. Local map/item icons частично подключены.

## 5. D90 — остаток M0

1. **M0-F1 WORLD FIX** — D1/D2 runtime, collider, city/dungeon/chest, persistence.
2. **M0-F2 CHARACTER & COMBAT** — appearance/equipment/loot visual, movement, ranged/projectiles/abilities/VFX.
3. **M0-F3 GAMEPLAY LOOP** — progression, items/crafting, NPC/trade, quests, save/load.
4. **M0-F4 PRESENTATION** — audio/WAV, HUD/inventory/skills/map/journal, feedback.
5. **M0-F5 FINAL** — Windows build, full loop, regressions, visual review.

Старые D71–D89 документы сохраняются как история, но их роли/ветки/очереди **SUPERSEDED BY D90**.

## 6. Неизменные правила

- Alpha original read-only.
- Не публиковать исходные/конвертированные Alpha assets.
- GitHub — source of truth.
- Не выдавать `CUBEGAME_CONTRACT` за Alpha.
- Один Unity slot.
- `ACCEPTED` — только решение продюсера.
