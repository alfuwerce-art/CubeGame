# CubeGame — PROJECT MEMORY

**Updated:** 2026-11-11  
**Canonical branch:** `integration/m0-all`  
**Current plan:** [D92 — full Alpha-to-Unity port](docs/decisions/D92-m0-full-alpha-unity-port.md)  
**Current stage:** [M0 ALPHA FULL UNITY PORT](docs/ACTIVE_STAGE.md) — ACTIVE / REWORKED

## 0. D92 — переоценка M0

Продюсер отклонил старый критерий, при котором наличие отдельных подсистем и зелёных тестов трактовалось как перенос игры. D90 как активная схема исполнения superseded.

Прежние F1–F4 сохраняются как инженерные checkpoint'ы и полезный код, но не означают parity соответствующих Alpha-систем.

Текущий implementation checkpoint: `codex/m0-alpha-code-parity-p1` @ `c1f9f758598d90d76974d352d1a3689ceb04c8cc` — `PARTIAL`.

## 1. Главная цель M0

Полностью перенести одиночную Cube World Alpha 2013 на Unity как связанную игру. Исходный код недоступен, поэтому перенос выполняется через reverse engineering `Cube.exe` и данных Alpha.

Обязательная цепочка по системам:
`original code/data → recovered behavior → Unity runtime → linked systems → Windows Player → comparison → producer review`.

Финальный loop:
`launch → menu → character → world → movement/streaming → environment → NPC/creatures → combat → loot/items → city/services → dungeon/boss → quests/progression → HUD/inventory/map → audio/VFX → save/load/continue`.

Непринятые `UNKNOWN/PARTIAL/PLACEHOLDER_NOT_ALPHA/CUBEGAME_CONTRACT` на обязательном основном пути блокируют закрытие соответствующей системы и M0.

## 2. Alpha parity backlog

[M0 Alpha Parity Backlog](docs/planning/M0_ALPHA_PARITY_BACKLOG.md) теперь является **blocking registry** для обязательного Alpha single-player baseline: долг нельзя просто отложить в M1/M2, если без него M0 перестаёт быть переносом оригинальной игры.

## 3. Исторические инженерные checkpoint'ы

### M0-F4 PRESENTATION

Кандидат на базе canonical `05336984`. Основной `GameBootstrap` использует UI Toolkit для создания мира и героя, HUD, экипировки, навыков, услуг, карты и журнала. Панели читают состояние сеанса и вызывают операции F1–F3. Исправлены метаданные 32 WAV, Unity декодировала все 93 локальных WAV. Воспроизведение подключено к живым событиям и освобождает клипы при выходе. Полный EditMode 755 PASS / 0 FAIL / 2 прежних SKIP. Полный PlayMode 73 PASS / 0 FAIL / 0 SKIP, включая сквозной F4. Ошибок компиляции 0. Точные условия части звуков, оригинальные маркеры и полный Alpha-журнал остаются в parity backlog. [Отчёт F4](docs/reports/M0_F4_PRESENTATION_REPORT.md)

### M0-F3 GAMEPLAY LOOP

`ACCEPTED` продюсером 10 ноября 2026. Canonical `integration/m0-all` переведён fast-forward на `3cd79b4366dda4ff0d2410be22ee11d084eefaf1`. F3 выполнен на базе принятого F2 `4b94808`. Pilot связывает развитие и экипировку, бой и лут, городские сделки и услуги, рецепт Life Potion, региональную охоту и сундуки в одной сессии. Кошелёк, кубы маны, журнал и смещение календаря сохраняются в разрешённых необязательных полях `.session` v1. Открытие сундука использует существующую `.world` дельту. C-29 подключён к маршрутам снарядов существ, реестр P-01 собирается. EditMode 754 PASS, 0 FAIL, 2 SKIP. PlayMode 70 PASS, 0 FAIL, сквозной сценарий F3 PASS. Ошибок компиляции 0. Полный ассортимент, генерация NPC и миссий, формулы наград и состав сундуков остаются `PARTIAL` / `UNKNOWN`. [Отчёт F3](docs/reports/M0_F3_GAMEPLAY_LOOP_REPORT.md)

### M0-F2 INTEGRATION CHECKPOINT

`ACCEPTED` продюсером 10 ноября 2026 на `4b9480836beeba859340938486f4200d16c1549e`. GitHub подтвердил fast-forward `integration/m0-all` с `d21e5fa` на этот коммит. EditMode 744 PASS, 0 FAIL, 2 SKIP. PlayMode 69 PASS, 0 FAIL. Ошибок компиляции 0. Сквозной PlayMode-сценарий F1+F2 проверяет мир, внешность, движение, бой, лут, streaming и save → exit → load. Маршрут атак существ и статус P-01 переданы в F3. [Отчёт](docs/reports/M0_F2_INTEGRATION_REPORT.md)


M0-F1 `READY_FOR_REVIEW` в `codex/m0-f1-world-runtime`, база canonical `1c6560c`, F1A Claude `9e50915`. Подключены recovered population, игровое время, респаун и драйвер движения, единый D3-маршрут `/6`, город, данжи и сундуки. Существующие форматы сейва читаются. Коллайдер следует voxel edit при очереди новых чанков. В PlayMode проверены stable ID и дельты через unload/reload и save/exit/load. Полный EditMode 664 passed / 0 failed / 2 skipped, PlayMode 60 passed / 0 failed / 0 skipped, `error CS = 0`. Каноническое слияние после ревью продюсера. [Отчёт M0-F1](docs/reports/M0_F1_WORLD_FIX_REPORT.md)

### M0-F2A CHARACTER RUNTIME

`READY_FOR_REVIEW` в `claude/m0-f2a-character-runtime`, база F1 `748d650` (canonical `integration/m0-all` = `1c6560c` ещё не содержит F1). Один runtime-путь героя: сохранённые внешность и экипировка → `HeroVisualComposer` → `AlphaPlayerVisualBuilder` → `GameVisualSink` (единственный владелец). Раса/пол/лицо/причёска/цвет волос видны в сцене; лицо и причёска разрешаются через реестр Alpha, модель предмета — исполненным селектором `item_icon_id` по материалу/редкости/seed. 15 видов рисуются общим гуманоидным ригом 0x02 по блоку внешности Alpha; OrcFemale 0x0C — собственным актором (`PARTIAL`). Лут на земле — один владелец, без дрейфа при смене начала координат. Формат сейва не менялся. EditMode 679/677/0/2, PlayMode 62/62, `error CS = 0`. [Отчёт M0-F2A](docs/reports/M0_F2A_CHARACTER_RUNTIME_REPORT.md)

### M0-F2B COMBAT RUNTIME

`READY_FOR_REVIEW` в `claude/m0-f2b-combat-runtime`, база F2A `5f197f9`, сведён evidence DeepSeek `08ae4e3`. Атака героя: оружие → действие → путь по группе обработчика Alpha, без fallback в melee (неизвестное правило — отказ `UNKNOWN`). По байтам `Cube.exe`: лук/арбалет/бумеранг/жезл/браслет выпускают **летящий снаряд** (список `[world]+0x14`, 100/50 бл/с, g 7.5, 5000 мс, бокс `size/2+0.5`) — поправка к выводу DeepSeek «снаряда нет»; луч ≤200 блоков до стены — для `0x1C/0x5E/0x5F`, бомба 0x68 — 10 блоков; посох — объём перед героем. Всё через `CombatSystem.ResolveHit`, один визуальный владелец (`ProjectileVisuals`), снаряды не сохраняются. Масштаб вида: эмулированный `Creature_drawModel` — матрица размещения масштабируется по виду, S05 её вычитал; теперь `ModelScale(эталон 0x02) × placement(kind)/placement(0x02)` для героя и NPC, hit box = размер Alpha 156 видов. EditMode 746/744/0/2, PlayMode 68/68, `error CS = 0`. [Отчёт M0-F2B](docs/reports/M0_F2B_COMBAT_RUNTIME_REPORT.md)

### База до M0-F1

До D90-cleanup canonical HEAD был `fce38e0`, объединяющий:
- Codex D3–D6 audit `762ac5d`;
- Claude D1/D2 review-fix code `47c5b1a` и его последующее сведение.

Unity после merge:
- EditMode: **639 total / 637 passed / 0 failed / 2 skipped / error CS 0**.
- Полный PlayMode после merge не повторён.
- Последний полный PlayMode Claude до merge: **57 total / 54 passed / 2 failed / 1 skipped**.
- Два известных fail существовали до D1/D2: stale world collider после voxel edit/save/load.

## 4. Ключевые факты D1/D2

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

На базе D90 `AlphaWorldPopulation` без селектора был пуст. В кандидате M0-F1 источник `/6` использует проверенный `0x5F56C0`. Драйвер NPC получает реальное время `GameClock`, а данные расписаний деревень остаются `PARTIAL`.

Открыто: exact rand() stream, часть условий wild cells, 22 non-grid construction sites, NPC equipment R-1, spawn timer persistence.

## 5. Ключевые факты D3–D6

Codex audit: [CODEX_D3_D6_INDEPENDENT_AUDIT](docs/reports/CODEX_D3_D6_INDEPENDENT_AUDIT.md).

- D3 на входе D90: дома/W-18 частично подключены. Кандидат M0-F1 добавляет город, данжи и сундуки в gameplay route `/6`. Dungeon carver остаётся реконструкцией CubeGame.
- D4: equipment events работают частично; race/sex/appearance не применяются к runtime hero. Ground loot имеет два визуальных владельца и не следует floating origin.
- D5: projectile models/flight data существуют, но `attack → projectile → collision → CombatSystem.ResolveHit` отсутствует.
- D6: 93 sounds cataloged; playback отсутствует. WAV metadata parser ошибается на 32 файлах. Local map/item icons частично подключены.

## 6. D92 — активная структура M0

См. [M0 ALPHA FULL UNITY PORT](docs/stages/M0_ALPHA_FULL_UNITY_PORT_STAGE.md).

Системные контуры: boot/menu/input; world/streaming; environment/world objects/structures; population/AI; authoritative action/combat/animation; character/items/progression; UI; quests/cities/dungeons; audio/VFX/time; persistence; final parity run.

## 7. D90 — исторический план

1. **M0-F1 WORLD FIX** — D1/D2 runtime, collider, city/dungeon/chest, persistence.
2. **M0-F2 CHARACTER & COMBAT** — appearance/equipment/loot visual, movement, ranged/projectiles/abilities/VFX.
3. **M0-F3 GAMEPLAY LOOP** — progression, items/crafting, NPC/trade, quests, save/load.
4. **M0-F4 PRESENTATION** — audio/WAV, HUD/inventory/skills/map/journal, feedback.
5. **M0-F5 FINAL** — Windows build, full loop, regressions, visual review.

Старые D71–D89 документы сохраняются как история, но их роли/ветки/очереди **SUPERSEDED BY D90**.

## 8. Неизменные правила

- Alpha original read-only.
- Не публиковать исходные/конвертированные Alpha assets.
- GitHub — source of truth.
- Не выдавать `CUBEGAME_CONTRACT` за Alpha.
- Один Unity slot.
- `ACCEPTED` — только решение продюсера.
