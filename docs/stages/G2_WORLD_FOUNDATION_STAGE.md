# G2 — WORLD FOUNDATION (ТЗ этапа)

**Этап:** G2 дорожной карты ([ROADMAP_DRAFT](../planning/ROADMAP_DRAFT.md))
**Предыдущий этап:** [G1 TECHNICAL FOUNDATION](G1_TECHNICAL_FOUNDATION_STAGE.md)
**Review-ветка:** `claude/g2-world-foundation`
**База:** `deepseek/g1-00-runtime-baseline-candidate` @ `cb76aab` (OPTION C: P0–P7 runtime = основа CubeGame)
**Unity:** `6000.3.27f2`
**Статус:** `READY_FOR_REVIEW` (агент не выставляет `ACCEPTED`)

---

## 1. Цель этапа

Перевести уже работающий мир из состояния «Alpha-derived world implementation» в состояние
**«CubeGame extensible procedural world foundation»**, не переписывая работающие системы:

- генератор высот, климат, зоны, реки, дороги, пещеры, чанки, стриминг с выгрузкой, floating origin,
  world modifications, save/load, размещение окружения, render distance, растительность и структуры
  **переиспользуются** (REUSE BEFORE REWRITE);
- появляется **собственный** слой CubeGame: именованные климатические оси, data-driven биомы, бюджет мира,
  стратегия дистанций, правила растительности отдельно от моделей, конвейер размещения структур,
  seams для городов/данжей, debug overlay, воспроизводимые измерения, явная версия генерации в сейве,
  контракт сущностей для G3.

G2 **не** делает финальный визуал, все биомы, города, данжи, деревья, сезоны, NPC AI, квесты, UI и не чистит
весь legacy-код.

---

## 2. Правила этапа

1. Alpha-зависимости не являются blocker: `.cubdec`, WAV, AlphaFont, atlas, generated tables, content staging
   и legacy parity-код остаются до появления соответствующих production-систем.
2. Новые CubeGame-системы не должны **добавлять** новые зависимости от Alpha-контента. Временное
   использование существующего допустимо и помечается (`TEMPORARY_RUNTIME`).
3. Вопрос «совпадает ли это с Alpha?» больше не главный. Главные вопросы: работает ли для CubeGame,
   расширяемо ли, можно ли добавлять биомы/структуры/правила, переживает ли мир save, масштабируется ли
   streaming, контролируема ли производительность.
4. `REUSE BEFORE REWRITE`: существующие классы генерации/стриминга/сейва сохраняются и оборачиваются, а не
   заменяются.
5. Никакой новой игровой логики, влияющей на баланс/бой/инвентарь, этап не вводит.

---

## 3. Work packages

| ID | Пакет | Выход |
|---|---|---|
| G2-A | Multi-seed validation | автоматический regression gate: `same seed + same generation profile = same world` |
| G2-B | Climate / biome foundation | `ClimateAxes`, `BiomeCatalog`, профили surface/vegetation/spawn/structure/weather |
| G2-C | Render distance / world budget | явная модель бюджета: visible/retained chunks, vertex/vegetation/NPC/object/zone budget + измерение |
| G2-D | LOD / distance strategy | near/mid/far bands с явными правилами (vegetation, structures, simulation, fog) |
| G2-E | Vegetation foundation | правила «где можно» отдельно от «какая модель рисуется» + временная привязка моделей |
| G2-F | Structure placement foundation | candidate → validation → placement → persistent id → runtime state |
| G2-G | City / dungeon seams | reservation sites, NPC anchors, road stubs, dungeon entrance → instance id |
| G2-H | World debug tools | dev overlay: seed, chunk/zone/region, biome, climate, height, streaming, budget, structures, spawns |
| G2-I | Performance measurement | recorder: generation time, chunks/sec, vertices, chunks, plants, NPC, GC, frame time, spikes, JSON |
| G2-J | Save compatibility | явная версия генерации + fingerprint профиля в `.world`, отказ при смене профиля |
| G3 seam | Entity contract | `EntityDefinition` / `EntityVisualRecipe` / registry без знания о фабрике моделей |

---

## 4. Требования по пакетам

### G2-A — multi-seed gate

- минимум 8 seed разных типов (малые, простые, дефолтный 666666, высокоэнтропийные, границы `uint`),
  без отбора «красивых»;
- для каждого seed: повторный расчёт даёт тот же результат; разные seed дают разные миры;
- digest покрывает таблицу seed'ов, высоты, горный маска/rock, климат, региональные записи и зонную сетку;
- отдельно проверяется генерация зоны (воксели) на воспроизводимость между экземплярами генератора;
- 3 seed заморожены как golden-значения: случайное изменение алгоритма ломает suite;
- gate чувствителен к изменению на один воксель.

### G2-B — climate / biome

- именованные оси: temperature, humidity, rareness, relief, region level, surface, water;
- биом = данные: `ClimateEnvelope` + `SurfaceProfile` + `VegetationProfile` + `SpawnProfile` +
  `StructureRuleIds` + `WeatherCompatibility`;
- детерминированное разрешение биома (priority → specificity → id) с fallback-биомом;
- добавление биома не требует правок кода разрешения;
- структурная валидация всех перекрёстных ссылок.

### G2-C — budget

- бюджет считается из render distance / view radius / keep radius, а не задаётся «магическим» числом;
- видно, что текущее окно 41×41 = 1681 чанк не влезает в vertex budget и даёт ~1 398 чанков;
- измерение сравнивается с бюджетом: `WithinBudget` / `SoftOver` / `Over`, hard overage — ошибка;
- существующее поведение при отсутствии бюджета не меняется.

### G2-D — distance strategy

- near/mid/far/offscreen без дыр и пересечений;
- для каждой полосы: vegetation, structures, creature simulation, cap растений, fog start/end;
- fog заканчивается внутри видимого радиуса.

### G2-E — vegetation

- правила не упоминают модели: biome, slope, height above water, moisture, temperature, water distance,
  spacing, clustering, density;
- детерминированные density roll и выбор kind (integer hash);
- привязка kind → текущая Alpha-модель помечена `TEMPORARY_RUNTIME` и не влияет на правила;
- все CubeGame kinds имеют placeholder-привязку (нет «дыр» в данных).

### G2-F — structures

- kinds: city, village, camp, ruin, tower, cave entrance, dungeon entrance, landmark;
- конвейер: детерминированный candidate на зону → валидация (биом, level, footprint, flatness,
  вода, spacing, шаблон) → placement;
- ничего не размещается «силой»: при плохом footprint — отказ с причиной;
- persistent id: `<seed>:<zone>:<rule>:<slot>`, парсится обратно, переживает unload/reload;
- «уничтожено» использует существующий `WorldDeltaKind.ObjectRemoved` на отдельном канале структур;
- runtime state — закрытый набор переходов.

### G2-G — seams

- settlement: регион → зона → центр, радиус, биом, level, набор NPC-якорей (центр, таверна, магазины,
  кузнец, квесты, стража, жители) и 4 дорожных stub'а, всё с persistent id;
- dungeon entrance: зона → позиция, `DungeonInstanceId`, level/stars;
- планировщики детерминированы и не строят геометрию.

### G2-H — debug tools

- overlay (F9 / `-cubeDebugOverlay`) показывает: seed, generator id, fingerprint, позицию, chunk/zone/region,
  биом, климат, высоту, воду, streaming (loaded/pending/zones/vertices/plants/npc), статус бюджета,
  distance band, perf, ближайшие кандидаты структур, спавны, проблемы;
- сбор данных отделён от отрисовки (snapshot можно получить в тесте и в Player-логе).

### G2-I — performance

- recorder: generation time, chunks/sec, mean/max chunk build, vertices, active/cached chunks, zones,
  vegetation, NPC, objects, GC, frame mean/p95/max, spikes;
- детерминированный JSON (фиксированный порядок ключей) для сравнения прогонов;
- `-cubeWorldMetrics <path>` пишет файл сессии из Player.

### G2-J — save compatibility

- основной принцип: неизменённый мир восстанавливается из seed, изменения игрока — отдельно;
- генератор идентифицируется id + версией (`.../9`) **и** fingerprint профиля генерации;
- fingerprint пишется в `.world` (опциональное поле, старые сейвы читаются как `LegacyWorld`);
- при смене профиля — явный отказ `GeneratorMismatch` с причиной, а не «тихо другой мир»;
- bump любой версии в `WorldGenerationProfile` меняет fingerprint всех новых миров.

### G3 seam

- мир получает `EntityDefinition` (идентичность, поведение, spawn profile) и `EntityVisualRecipe`
  (source kind, model key, material key, rig/animation package, scale);
- мир не знает, откуда модель (imported / external generated / Alpha placeholder);
- адаптер текущего runtime: `SpawnDefinition` → `EntityDefinition` с `TemporaryAlpha` recipe;
- стабильный appearance seed из stable id.

---

## 5. Что не входит в G2

- финальные биомы, деревья, трава, вода, погода, сезоны;
- production города/деревни/данжи и их геометрия;
- замена Alpha assets (`.cubdec`, WAV, шрифты, atlas, generated tables);
- очистка legacy parity-кода;
- NPC AI, квесты, боевые механики, UI/UX;
- переписывание сейв-формата и миграции старого формата символов;
- оптимизация «до смерти»: сначала измерение, потом решение.

---

## 6. Проверки и Definition of Done

| Проверка | Критерий |
|---|---|
| Компиляция | `error CS = 0` |
| EditMode | все применимые тесты PASS (включая G2 suite) |
| PlayMode | все применимые сценарии PASS |
| Windows Player | собирается, smoke-сценарий проходит |
| Multi-seed gate | PASS, 8 seed, golden-значения зафиксированы |
| Foundation validation | `WorldFoundationValidation.Check()` пуст |
| Save | старый сейв читается; смена профиля → явный отказ |
| Runtime | combat/inventory/save не сломаны |
| Документация | stage + report + архитектура + тест-матрица |

Smoke-сценарий этапа:

`launch → create/load world → move through multiple chunks → stream world → observe biome/climate →
modify world → spawn/interact with NPC → combat → loot → save → exit → load → continue`.

---

## 7. Риски

| Риск | Митигация |
|---|---|
| Стоимость полной генерации зоны (~3 с) удлиняет EditMode | зонный digest только для 2 seed, основной gate — по колонкам/регионам |
| Alpha-кэш отсутствует в чистом checkout | G2-тесты не требуют Alpha-контента |
| Новый код случайно меняет существующий streaming | бюджет/метрики подключаются только при явном вызове, дефолтное поведение сохраняется |
| Смена поколения дизайн-данных ломает старые миры | fingerprint + версия профиля + отказ вместо тихой регенерации |
