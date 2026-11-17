# CubeGame — PROJECT MEMORY

**Updated:** 2026-11-17
**Canonical branch:** `integration/m0-all`
**Текущее направление:** [D93 — CubeGame как собственная игра](docs/decisions/D93-development-pivot-own-game.md) (2026-11-17)
**Рабочая карта:** [ROADMAP_DRAFT](docs/planning/ROADMAP_DRAFT.md) · **Текущий этап:** [ACTIVE_STAGE — G2 WORLD FOUNDATION](docs/ACTIVE_STAGE.md)

## 0. G2 — WORLD FOUNDATION (2026-11-17)

`READY_FOR_REVIEW` в ветке `claude/g2-world-foundation` (база — принятый G1 baseline `cb76aab`). Мир переиспользован
как основа: генератор высот/климата/зон/рек/дорог/пещер, streaming с выгрузкой, floating origin, дельты и сейв не
переписывались. Добавлен собственный слой CubeGame:

- **G2-A** multi-seed gate: 8 seed, 3 golden digest, воспроизводимость зоны между экземплярами генератора.
- **G2-B** `ClimateAxes` + data-driven `BiomeCatalog` (11 биомов; surface/vegetation/spawn/structure/weather профили).
- **G2-C** `WorldStreamingBudget`: 1 681 запрошенный чанк против ≈1 398 доступных при 12 582 912 вершинах — наблюдаемое
  «мягкое переполнение» объяснено и измеряется; бюджет применяется в runtime.
- **G2-D** `WorldDistanceStrategy` (near/mid/far/offscreen: vegetation, structures, simulation, fog).
- **G2-E** правила растительности отделены от моделей (временная привязка — Alpha-плейсхолдеры).
- **G2-F** конвейер структур: candidate → validation → placement → persistent id → состояние → удаление через `.world`.
- **G2-G** city/dungeon seams: settlement site (NPC-якоря, дорожные stub'ы) и dungeon entrance (instance id).
- **G2-H** debug overlay (F9 / `-cubeDebugOverlay`) + engine-free snapshot.
- **G2-I** performance recorder + `-cubeWorldMetrics <path>` (JSON, фиксированный порядок ключей).
- **G2-J** версия профиля генерации + `generationFingerprint` в `.world`; смена профиля даёт явный отказ.
- **G3 seam** `EntityDefinition`/`EntityVisualRecipe`/registry — мир не знает о фабрике моделей.

Проверки: `error CS = 0`; G2 EditMode 34 PASS; полный EditMode 893 total / 879 passed / 1 известный content-presence
fail без Alpha-кэша / 11 skipped; PlayMode и Windows Player — см. отчёт. Документы:
[этап](docs/stages/G2_WORLD_FOUNDATION_STAGE.md) · [отчёт](docs/reports/G2_WORLD_FOUNDATION_REPORT.md) ·
[архитектура](docs/architecture/WORLD_GENERATION_ARCHITECTURE.md) · [тест-матрица](docs/planning/G2_WORLD_TEST_MATRIX.md).

## 0. G1-00 — RUNTIME BASELINE (2026-11-17)

Baseline для дальнейшей разработки — **OPTION C**: полный runtime цепи P0–P7 становится основой CubeGame.

- Candidate-ветка: `deepseek/g1-00-runtime-baseline-candidate` = `integration/m0-all` + pivot-документы + merge `codex/m0-p7-quests-cities-dungeons` (merge-коммит `c613ae6`, код равен P7-tip).
- Цепь: `0533698..85ba64a`, 104 линейных коммита, 278 файлов, +29 197/−2 024. В canonical пока **не слита**: сначала cleanup C1–C4.
- Проверено: `error CS = 0`; EditMode 853/859 (с Alpha-кэшем) и 845/859 (без); PlayMode 75/75 и 39/75 (3 content-presence fail, 33 skip); Windows Player собран (0 errors) и проверены сценарии new/load/continue/attack/UI/save с кэшем и без.
- Alpha-зависимости классифицированы: `RESEARCH_ONLY` / `TEMPORARY_RUNTIME` / `PRODUCT_BLOCKER` / `REMOVE`.
- Performance baseline: стартовый мир 67–357 мс, 25 стартовых чанков, vertex budget 12 582 912, 56 активных NPC в 9 зонах, peak WS 2.4–2.5 ГБ с контентом / 1.2–1.3 ГБ без.
- Parity-конфликт D01/D14/D20 закрыт записью [CR-001](docs/planning/DECISIONS_001-036.md) (текст решений не изменён).
- Отчёт: [G1_00_RUNTIME_BASELINE_SELECTION_REPORT](docs/reports/G1_00_RUNTIME_BASELINE_SELECTION_REPORT.md) · матрица: [G1_RUNTIME_REUSE_MATRIX](docs/planning/G1_RUNTIME_REUSE_MATRIX.md).

## 0a. G0 — DEVELOPMENT PIVOT (2026-11-17)

Решение продюсера: **CubeGame разрабатывается как самостоятельная игра**. Полный перенос Cube World Alpha 2013 прекращён как active target. GDD и утверждённые игровые решения не пересобираются; меняется способ разработки и порядок этапов.

- Alpha 2013 = источник исследований, алгоритмов, формул, инженерного референса. Не спецификация.
- Старый **M0 ALPHA FULL UNITY PORT** → `LEGACY / RESEARCH BASE / NOT A RELEASE TARGET`. P7/P8/P9/P10 не продолжаются.
- Код, тесты, отчёты и decompilation evidence сохраняются. Принцип: `REUSE BEFORE REWRITE`.
- Разбор существующей работы: [G0 reuse audit](docs/planning/G0_EXISTING_WORK_REUSE_AUDIT.md).
- Состояние генерации мира: [G2 world audit](docs/planning/G2_WORLD_FOUNDATION_AUDIT.md).
- Следующий этап: [G1 Technical Foundation](docs/stages/G1_TECHNICAL_FOUNDATION_STAGE.md).
- Baseline и результаты проверок: [G0 report](docs/reports/G0_DEVELOPMENT_PIVOT_REPORT.md).

Документы ниже в этом файле описывают **историю** M0/D90/D92 и остаются как evidence.

## 0a. D92 — переоценка M0 (историческая схема)

Продюсер отклонил старый критерий, при котором наличие отдельных подсистем и зелёных тестов трактовалось как перенос игры. D90 как активная схема исполнения superseded.

Прежние F1–F4 сохраняются как инженерные checkpoint'ы и полезный код, но не означают parity соответствующих Alpha-систем.

Текущий implementation checkpoint: `codex/m0-p6-alpha-menu-hud` @ `fb4e0d5` — P0–P6 выполнены как `PARTIAL` (P6 `READY_FOR_PRODUCER_REVIEW`), ни один блок не `PASS`. Документы и отчёты P0–P6 сведены в canonical 2026-11-14, код остаётся в review-ветках. Следующий блок — P7. Сводная таблица — [ACTIVE_STAGE](docs/ACTIVE_STAGE.md).

## 1. Главная цель M0 (historical, до 2026-11-17)

> `LEGACY` — цель больше не действует. Оставлено как evidence метода.

Полностью перенести одиночную Cube World Alpha 2013 на Unity как связанную игру. Исходный код недоступен, поэтому перенос выполняется через reverse engineering `Cube.exe` и данных Alpha.

Обязательная цепочка по системам:
`original code/data → recovered behavior → Unity runtime → linked systems → Windows Player → comparison → producer review`.

Финальный loop:
`launch → menu → character → world → movement/streaming → environment → NPC/creatures → combat → loot/items → city/services → dungeon/boss → quests/progression → HUD/inventory/map → audio/VFX → save/load/continue`.

Непринятые `UNKNOWN/PARTIAL/PLACEHOLDER_NOT_ALPHA/CUBEGAME_CONTRACT` на обязательном основном пути блокируют закрытие соответствующей системы и M0.

## 2. Alpha parity backlog (historical)

> `LEGACY` — реестр больше не является blocking для продукта; см. [параметры backlog](docs/planning/M0_ALPHA_PARITY_BACKLOG.md).

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
