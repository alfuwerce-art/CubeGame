# G0 — EXISTING WORK REUSE AUDIT

**Дата:** 2026-11-17
**Этап:** [G0 — DEVELOPMENT PIVOT / BASELINE FREEZE](../ACTIVE_STAGE.md)
**Аудируемое дерево:** `integration/m0-all` @ `4e36f1539d787e6a03f85d1f9fc92efcb1b64a79`, проверено в отдельном worktree на ветке `deepseek/g0-development-pivot`
**Unity:** `6000.3.27f2` (`D:\Unity\Hub\Editor\6000.3.27f2\Editor\Unity.exe`)

---

## 0. Зачем этот документ

После pivot (2026-11-17) нужно ответить на один вопрос по каждой уже сделанной системе:

> Это работает и полезно собственной CubeGame — или это обязательство копировать Alpha?

Документ фиксирует состояние систем, доказательства, ограничения, Alpha-зависимости и решение: `KEEP / REWORK / RESEARCH / RETIRE`, а также этап, к которому система относится.

**Правило чтения:** наличие класса или зелёного теста не означает работающую систему в игре. Везде разделяются:

- `CODE EXISTS` — код есть;
- `RUNTIME WIRED` — код вызывается из реального игрового пути (`GameBootstrap`/`GameSession`);
- `TESTED` — есть автоматическая проверка;
- `PLAYER` — подтверждено в Windows Player (по отчёту; в G0 заново не перепроверялось).

---

## 1. Важное ограничение аудита: что именно лежит в canonical

Каноническая ветка содержит runtime уровня **F1–F3** плюс документы D92/P7-A/roadmap. Работа P0–P7 **не влита**:

| Ветка | Коммит | Впереди canonical | Объём |
|---|---|---|---|
| `codex/m0-p1-world-streaming` | `0763c83` | 54 | 122 файла, +10 887 / −1 274 |
| `codex/m0-p2-environment` | `85d4e13` | — (влита в P3) | — |
| `codex/m0-p3-population-ai` | `f265108` | 62 | 148 файлов, +12 493 / −1 347 |
| `codex/m0-p4-action-combat` | `f0d2b71` | 65 | 166 файлов, +13 331 / −1 784 |
| `codex/m0-p5-character-items-progression` | `01930ff` | 77 | 192 файла, +16 717 / −1 889 |
| `codex/m0-p6-alpha-menu-hud` | `fb4e0d5` | 83 | 203 файла, +19 324 / −1 889 |
| `codex/m0-p7-quests-cities-dungeons` | `85ba64a` | 104 | 278 файлов, +29 197 / −2 024 |

Цепь P1 → P2 → P3 → P4 → P5 → P6 → P7 линейна (`merge-base --is-ancestor` = true по всей цепочке). Общая точка расхождения canonical и P7 — `0533698`.

Следствия:

1. Классы `AlphaLoot`, `AlphaShop`, `AlphaCrafting`, `AlphaWallet`, `AlphaProfileStore`, `ActionDispatcher`, UI Toolkit HUD (`GameBootstrap.AlphaHud/AlphaMenus/Interface`), audio (`GameAudio`, `AlphaWave`), render-distance/vertex-budget/fog (P1), vegetation `/7` (P2), dungeon carver/quests (P7) **в canonical отсутствуют**. Отчёты P0–P7 описывают код review-веток, а не это дерево.
2. Поэтому вердикты ниже даны по canonical и отдельно помечают, что существует только в review-ветках.
3. Выбор базовой линии для G1 (canonical или цепь P7) — **первая задача G1**, см. §12 и [G1 ТЗ](../stages/G1_TECHNICAL_FOUNDATION_STAGE.md).

Объём проверенного дерева: **274 `.cs` / 55 267 строк** в `Assets/` (плюс 72 тестовых файла).

---

## 2. Сводка вердиктов

| Категория | Кол-во систем | Смысл |
|---|---|---|
| `KEEP` | 22 | работает, фундамент CubeGame, трогаем минимально |
| `REWORK` | 19 | полезно, но привязано к Alpha parity/placeholder-архитектуре |
| `RESEARCH` | 11 | знание и референс, не production-код |
| `RETIRE` | 9 | новой игре не нужно, дальше не развивать |

Главный вывод: **большая часть инженерной работы переиспользуема**. Не переиспользуемы: Alpha-специфичный UI, Alpha-специфичный audio/VFX parity, второй AI-стек, pilot-наследие и часть Alpha-таблиц как продуктовые данные.

---

## 3. CORE

### 3.1 Состояние машины состояний сеанса — `KEEP` → G1

- Файлы: `Features/Session/GameSession.cs` (851), `GameClock.cs` (135), `GameEventBus.cs` (128).
- Состояния: `Boot / MainMenu / LoadingWorld / InGame / Paused / Saving / Unloading / Quit` с таблицей разрешённых переходов и отказом при недопустимом переходе (`LastRejection`).
- Тесты: `FullM0ClockAndSessionTests` (9), `FullM0GameLoopPlayModeTests` (2), `CoreFoundationPlayModeTests` (4).
- Доказано: явные переходы, отказ вместо «тихого» перехода, сохранение состояния до/после save.
- Ограничения: `GameSession` 851 строка смешивает состояние, бой, прогрессию, инвентарь и миссии.
- Alpha-зависимости: тик 30 Гц помечен `PLACEHOLDER`; стартовое время мира и `InnDawn` — Alpha-числа.
- Техдолг: крупный класс; нет разделения на сервисы.
- Почему KEEP: это уже работающий каркас жизненного цикла игры, а не Alpha-специфика.

### 3.2 Контракты и стабильные ID — `KEEP` → G1

- Файлы: `Features/Contracts/Ids.cs` (278), `GameplayEvents.cs` (361), `WorldContracts.cs` (91).
- `StableEntityId(Kind|60-bit payload)`, отдельные ID-space для actor/world-object/vegetation/item/structure, типизированные события (16 типов `IGameEvent`).
- Тесты: используются массово (популяция, стриминг, сейвы), включая явные проверки несовместимости ID-space.
- Alpha-зависимости: `HeroKind = 0x02`, action id — Alpha-числа.
- Почему KEEP: это ровно то, что нужно собственной игре; менять нечего.

### 3.3 Bootstrap / точка входа — `REWORK` → G1

- Файл: `Presentation/Gameplay/GameBootstrap.cs` (1 876 строк) + `GameBootstrap.GameplayLoop.cs` (360).
- `RUNTIME WIRED`: единственная сцена `Assets/Scenes/Pilot.unity`, единственный gameplay-entry.
- Что делает: разбор аргументов, создание слотов сейва, камера/свет, загрузка или создание мира, построение мира (`BuildWorldViews`), герой, визуал, снаряды, население, ввод, IMGUI-меню/HUD/инвентарь/карта/журнал, сохранение, выход.
- Тесты: покрыт косвенно через PlayMode-сценарии; прямого теста на bootstrap нет.
- Ограничения: один MonoBehaviour владеет миром, населением, боем, UI и сохранением; 100 вызовов `GUI.*` внутри того же файла; 19 мест прямого `Input.*` в проекте.
- Alpha-зависимости: `DefaultSeed 666666`, `HeroAttackAction 57`, `TrainingTargetKind 0x1C`, `GeneratorId /6`.
- Техдолг: god class; отсутствует композиционный корень/DI; нет input abstraction.
- Почему REWORK: последовательность запуска и lifecycle корректны и должны сохраниться, но владение UI/вводом/миром нужно разделить. Разделение на partial-файлы уже существует в P6-ветке и может быть переиспользовано как форма.

### 3.4 Камера и ввод — `REWORK`

- Файлы: `Presentation/Camera/OrbitCameraController.cs` (93), `Presentation/Player/PlayerMotor.cs` (186), `Core/Movement/AlphaMovement.cs` (503).
- Состояние: работает, но камера помечена `PLACEHOLDER_NOT_ALPHA`, нет коллизий/zoom; ввод размазан по 19 точкам, биндинги захардкожены.
- Почему REWORK: input abstraction и границы симуляции прямо входят в G1; камера — в G12/G1.

### 3.5 DI/композиция — `REWORK` → G1

- Состояние: DI нет; композиция вручную в `Awake`; глобальный `AlphaActorLibrary.Shared` используется и из Features.
- Почему REWORK: G1 требует явных границ сервисов; статический синглтон связывает игровые системы с локальным Alpha-кэшем.

---

## 4. WORLD

Подробный разбор — в отдельном документе: [G2_WORLD_FOUNDATION_AUDIT.md](G2_WORLD_FOUNDATION_AUDIT.md). Здесь — сжатые вердикты.

| Система | Состояние | Вердикт | Этап |
|---|---|---|---|
| Детерминированный seed + таблица seeds | `RUNTIME WIRED`, oracle-тесты | `KEEP` | G2 |
| Terrain / base / mountain | `RUNTIME WIRED`, отклонение ≤1e-3 вокселя от исполненной Alpha | `KEEP` | G2 |
| Voxel-column модель, зоны 256, регионы 64 зон | `RUNTIME WIRED`, тесты | `KEEP` | G2 |
| Climate (2 поля + rare flag) | `PARTIAL`, семантика полей не восстановлена | `REWORK` | G2 |
| Biome / landmark rules (14 типов) | `RUNTIME WIRED`, но биомы — побочный эффект Alpha landmark-логики | `REWORK` | G2 |
| Rivers / lake water / roads (voxel-пассы) | oracle-проверено | `KEEP` | G2 |
| Визуализация воды | `PLACEHOLDER_NOT_ALPHA` (цвет/альфа, уровень 0 — гипотеза) | `REWORK` | G9 |
| Caves | `RUNTIME WIRED` | `KEEP` | G9 |
| Chunking 32 + mesh/collider | `RUNTIME WIRED`, PlayMode | `KEEP` | G2 |
| Streaming load/unload + floating origin | `RUNTIME WIRED`, unload проверен тестом | `KEEP` | G2 |
| Render distance / vertex budget / LOD / fog | **отсутствует в canonical** (только P1-ветка) | `REWORK` | G2/G9 |
| Кэши регионов/климата | неограниченный рост, eviction без теста | `REWORK` | G2 |
| Persistent world modifications (`WorldDeltaStore`) | `RUNTIME WIRED`, save/load PlayMode | `KEEP` | G1/G2 |
| World object placement foundation | `RUNTIME WIRED`, детерминированные ID | `KEEP` | G2 |
| Vegetation / trees / grass | в canonical геометрии нет; `/7` только в P2-ветке | `RESEARCH` | G9 |
| Houses (структуры) | реконструкция 3×3×4, не Alpha-геометрия | `REWORK` | G9 |
| Cities / settlements | расстановка `CUBEGAME_CONTRACT` | `REWORK` | G9 |
| Dungeons | 5×5 random-walk скелет, `PLACEHOLDER` | `REWORK` | G9 |
| Две параллельные системы координат (`WorldLayout` vs `AlphaWorldScale`) | дублирование констант и `FloorDiv` | `REWORK` (свести) | G2 |
| `WorldContentStreamer` (второй загрузчик контента) | не подключён к игре | `RETIRE` | G2 |
| Pilot height field (`PilotHeightField`, `ChunkMeshBuilder`, `PilotWorldView`) | не используется игрой | `RETIRE` | — |
| Alpha oracle-фикстуры и oracle-тесты | 8 JSON, без Alpha-ассетов | `KEEP` | G2 |

---

## 5. ENVIRONMENT

- **Деревья:** в canonical нет ни геометрии, ни плотностной модели. `AlphaVegetationPlanner` (`Content/`), 6 объектов на зону, помечен «перевод, а не проверенная Alpha-политика», и **отключён** для `/6` (`D3ZonePopulation.cs:38-43`). P2-ветка содержит `/7` и 25–27 тыс. растений — в canonical этого нет.
  → `RESEARCH` → G9. Для CubeGame нужна собственная модель плотности/биомов, а не перенос селектора.
- **Grass/flowers/water plants:** то же; декор присутствует в P2-ветке.
  → `RESEARCH` → G9.
- **Rocks / world objects:** размещение объектов и ID-space работают (`AlphaZoneContentProvider`, `AlphaWorldContent` 67 имён). Речные камни и часть зонных объектов — `PARTIAL`.
  → `KEEP` (foundation) + `REWORK` (содержимое) → G2/G9.
- **Structures (дома):** `AlphaHouseBuilder` — 3×3×4 блок с дверью в 1 воксель, явно `RECONSTRUCTED_FROM_DIMENSIONS`.
  → `REWORK` → G9.
- **Water:** уровень 0 — гипотеза (`U3/Q-2`), цвет/альфа — placeholder.
  → `REWORK` → G9.

Alpha-зависимость environment: селектор видов опирается на staged Alpha-kinds из локального кэша (`AlphaVisualCatalog`, `CreatureVisualCatalog`), поэтому без кэша зоны остаются без растительности/объектов.

---

## 6. NPC / POPULATION / AI

| Система | Состояние | Вердикт | Этап |
|---|---|---|---|
| Population streaming framework (`WorldPopulation` + каналы + активация зон) | `RUNTIME WIRED`, стабильные ID, 9 EditMode + PlayMode | `KEEP` | G5 |
| Alpha recovered wild-grid (`AlphaRecoveredPopulation`, `AlphaWildSpawnRules`, `AlphaCreatureKindSelector`, `AlphaCreatureKindCases.g.cs` 3 189 строк) | `RUNTIME WIRED`, 3 150 исполненных кейсов | `REWORK` | G5 |
| Spawn lifecycle (radius 200 / despawn 512 / таймер 1 200 000 мс / смена дня) + Snapshot/Restore | `RUNTIME WIRED`, тесты | `KEEP` (константы пере-тюнить) | G5 |
| `AlphaSpawnTable` (товары торговцев), `AlphaPopulationRules`, `AlphaWorldPopulation`, `FallbackPopulationPolicy` | не в runtime; данные поняты неверно (D1/D2) | `RETIRE` | G5 |
| Второй AI-стек: `CreatureAgent`, `CreatureSpawner`, `WorldContentStreamer`, `PatrolRoute` | `CODE EXISTS`, в сцену не входит | `RETIRE` | G5 |
| `CreatureAi` + `CreatureCombatDriver` + `AlphaNpcBehaviour` | `RUNTIME WIRED`; состояния idle/wander/notice/chase/attack | `REWORK` | G5 |
| Navigation / pathfinding | **отсутствует**: движение по прямой + подстановка Y из земли | `RESEARCH` | G5 |
| Городские сервисы (`TownNpcPopulation`, `AlphaCityContentProvider`, панели торговли/трактира/тренеров) | `RUNTIME WIRED`; 6 точек вокруг объекта, ассортимент 3 записи | `REWORK` | G5/G6 |
| Расписания и маршруты NPC (`AlphaNpcMovement` Schedule/WalkPath) | код исполняет списки, но данные пустые (`AlphaRecoveredPopulation.cs:149-163`) | `REWORK` | G5 |
| Фракции | только бинарная проверка `Hostile == Hostile` | `REWORK` | G5 |
| Персистентность состояния NPC | через отрицательные дельты, ограничение «только текущий день» | `REWORK` | G1/G5 |

Ключевые ограничения (evidence): нет pathfinding, расписания не заполняются, фракционной матрицы нет, позиции/HP живых NPC не сохраняются, таймер респавна сохраняется только в пределах текущего игрового дня.

---

## 7. COMBAT

| Система | Состояние | Вердикт | Этап |
|---|---|---|---|
| Action state machine (`CombatSystem`: windup/strike/recover, action serial, hit ровно на strike) | `RUNTIME WIRED`, `FullM0CombatTests` (23) | `KEEP` | G4 |
| Разрешение урона (`Core/Combat`: `DamageResolver`, `CombatFormulas`, `DamageMitigation`, `HitResolver`, `HealthPool`) | `RUNTIME WIRED`, 23+53+10 тестов | `KEEP` | G4 |
| Смерть/прерывание, один `EntityDied` | `RUNTIME WIRED`, тесты | `KEEP` | G4 |
| Снаряды (`ProjectileSystem` + `ProjectileVisuals`) | `RUNTIME WIRED`, 56 EditMode + 6 PlayMode | `KEEP` (константы — баланс) | G4 |
| Маршрутизация по оружию/action id (`AttackRoute`, `AlphaPrimaryAttack`) | `RUNTIME WIRED`, Alpha action id | `REWORK` | G4 |
| Alpha animation timing (`AlphaActionTimingSource` + `AlphaCombatRuntimeData.g.cs`) | работает, данные из Alpha-клипов | `RESEARCH` | G4 |
| Респавн героя (5 с + стартовая позиция) | `PLACEHOLDER_NOT_ALPHA` | `REWORK` | G4 |
| Способности / блок / dodge / бомба | в canonical не исполняются | `RESEARCH` | G4 |
| Старая библиотека снарядов `Presentation/Visual/Projectiles.cs` | не в runtime-пути | `RETIRE` | G4 |

Отдельно: расширенный диспетчер 110 действий и эффекты способностей существуют только в P4/P5-ветках; в canonical их нет.

---

## 8. RPG / ITEMS / ECONOMY

| Система | Состояние | Вердикт | Этап |
|---|---|---|---|
| Инвентарь и экипировка (`HeroInventory` 515, `EquipmentLoadout`, `ItemInstance`, `ItemStatsCalculator`) | `RUNTIME WIRED`, 10+20+64 теста | `KEEP` | G6 |
| Stats / XP / level (`CharacterStats`, `ProgressionState`, `AlphaMath`) | `RUNTIME WIRED`, 22+19 тестов | `KEEP` (кривые пере-утвердить как баланс) | G6 |
| Навыки/ранги/специализации (`HeroSkillRanks`) | рост и цена работают, эффекты способностей не исполняются | `REWORK` | G6 |
| Лут (`LootRules`: `System.Random`, 11 захардкоженных пар, деньги не падают) | `PLACEHOLDER_NOT_ALPHA` | `REWORK` | G6 |
| Ground loot владелец/сохранение | runtime-only, не сохраняется | `REWORK` | G6 |
| Крафт | только Life Potion | `RESEARCH` | G6 |
| Торговля (`AlphaTradeValue`, TryBuy/TrySell) | цена восстановлена по байтам, ассортимент/ресток нет | `REWORK` | G6 |
| Журнал/миссии (`MissionJournal`) | только региональные охоты | `REWORK` | G11 |
| Классы/навыки/крафт/кошелёк расширенного объёма (P5) | только в P5-ветке | `RESEARCH` (после решения по базовой линии) | G6 |

---

## 9. UI

- **Факт:** в canonical UI — это IMGUI, рисуемый каждый кадр из `GameBootstrap` (`OnGUI`): главное меню, редактор героя, пауза, HUD из трёх строк, инвентарь 4×50, черновик навыков, локальная карта 3×3, панель торговли/трактира/тренера. UI Toolkit, UXML/USS, шрифты и спрайт-атласы отсутствуют.
- **Alpha parity UI в canonical нет вообще** — ни полос HP/MP/XP, ни хотбара, ни портрета, ни прицела. UI Toolkit-реализация HUD/меню существует только в `codex/m0-p6-alpha-menu-hud` (+16.6 тыс. строк) и является Alpha-recreation.
- **Что переиспользуется:** игровой API, который вызывает UI (`StartNewWorld`, `LoadWorld`, `SaveCheckpoint`, `HeroAttack`, `TryInteract`, `TryBuy`, `TrySell`, `TryRest`, `TryApplyProgression`), правило валидации навыков, данные правил редактора героя, чистые хелперы (`MapTileBuilder`, `AlphaItemIcons`, `AlphaIconAtlas`), паттерн «честный отказ», когда модели/иконки недоступны.

Вердикты:

| Элемент | Вердикт | Этап |
|---|---|---|
| IMGUI-меню/HUD/инвентарь/карта/журнал как архитектура UI | `RETIRE` | G12 |
| Игровой API за панелями | `KEEP` | G1/G12 |
| Данные правил редактора героя (`AlphaHeroCreation`) | `RESEARCH` | G3 |
| `MapTileBuilder` | `KEEP` (структура), палитра — заменить | G12 |
| Иконочный пайплайн (`AlphaItemIcons`, `AlphaIconAtlas`) | `REWORK` (перенести в поставляемый контент) | G12 |
| P6-слой UI Toolkit (только review-ветка) | `RESEARCH` (майнинг структуры, не pixel parity) | G12 |

---

## 10. PERSISTENCE

- **Что есть:** три файла на слот (`<slot>.sav`, `.session`, `.world`), путь `persistentDataPath/CubeGame/saves`, атомарная запись `.tmp` → replace с `.bak`, заголовок `HEADER|version|fnv1a64|length`, pair-checksum между `.sav` и `.session`.
- **Форматы:** `CUBEGAME_SAVE` v1 (персонаж), `CUBEGAME_SESSION` v1 (сессия: мир, часы, герой, инвентарь, миссии, кошелёк), `CUBEGAME_WORLD` v1 (дельты мира + discovered).
- **Тесты:** `PersistenceTests` (28: roundtrip, restart, tamper, truncated, newer version, validator, delete), `FullM0PersistenceTests` + `FullM0AssemblyGraphTests` (13), PlayMode save→exit→load.
- **Ограничения:** миграций нет — есть только отказ при несовпадении версии; терпимость к старым слотам достигнута «необязательными полями с дефолтами»; позиции/HP живых NPC и существ, лут на земле, состояние действий и снарядов не сохраняются; ассортимент торговцев не сохраняется; `characterChecksum` обязателен.
- **Alpha-семантика сейва Alpha не переносилась**: формат собственный (это плюс для pivot).

| Элемент | Вердикт | Этап |
|---|---|---|
| Checkpoint-механика (3 файла, checksums, atomic write, pair validation) | `KEEP` | G1 |
| Версионирование/миграции схемы | `REWORK` (сейчас отсутствуют) | G1 |
| `WorldDeltaStore` (воксели, контейнеры, удаления, discovered) | `KEEP` | G1 |
| Персистентность состояния NPC через negative-delta | `REWORK` | G1/G5 |
| `GameSlotStore`/`StartNewWorld` как совместимость автотестов | `REWORK` (техдолг, помечен в legacy backlog) | G1 |

---

## 11. PRESENTATION (audio / VFX / lighting)

- **Audio:** в canonical нет ни одного `AudioSource`/`AudioClip`, нет RIFF/WAV парсера в C#. Есть `AlphaSoundLibrary` — резолвер 93 Alpha-имён в локальные `.wav` (только `File.Exists`), отображает **одно** событие (`mission_complete`), создаётся только в тестах. Реальный audio-стек (`GameAudio`, `AlphaWave`) существует в `codex/m0-f4-presentation`.
  → `AlphaSoundLibrary` = `REWORK` (паттерн «честный отказ» полезен), audio playback = `RESEARCH` → G12.
- **VFX:** нет `ParticleSystem`, VFX Graph, декалей. Есть `CombatFeedback` (чистые кривые, `CUBEGAME_CONTRACT`), hit = pulse масштаба, смерть = наклон 90° и погружение.
  → `CombatFeedback` = `KEEP` (форма будущего VFX-слоя), VFX = `RESEARCH` → G12.
- **Lighting / shaders:** Built-in RP, два шейдера (`AlphaTransfer/VoxelShaded`, `CubeGame/PilotVertexColor`), нет теней, нет skybox, камера чистит в плоский цвет; день/ночь = множитель интенсивности directional light.
  → `RETIRE` текущее состояние / `REWORK` по S06 → G9.
- **Presentation infrastructure, которая переиспользуется:** event-driven visual sinks (`IEntityVisualSink` + `GameEventBus.VisualSinks`), один владелец визуала на сущность с явным `Problems[]`, floating-origin-safe перепозиционирование.
  → `KEEP` → G1/G12.

---

## 12. ASSET PIPELINE / M1 / G3

- **Что работает как код:** `tools/alpha_content` (15 Python, ~2.8 тыс. строк) генерирует `Content/Generated/*.g.cs`; `tools/alpha_models` (114 Python, ~21.7 тыс. строк) — Alpha→Unity transfer pipeline (`pipeline.py`, `batch_runner.py`, rig/clip generation), с SHA-256 проверкой `Cube.exe`.
- **Unity-сторона:** `AlphaModels/**` (~2.4 тыс. строк) — voxel-rig, клипы, контракт сущности `cubegame.entity_contract/1`, слои экипировки/оружия/оффхенда.
- **M1 (Character & Creature Factory):** `EntityRecipe`, `BodyPartDefinition`, `RigArchetypeDefinition`, `AnimationResolver`, `Humanoid/Creature Factory` — **0 совпадений в коде**. Существуют только как спецификация (`docs/stages/M1_MODEL_ANIMATION_EVOLUTION_STAGE.md`, 1 947 строк).
- **ComfyUI:** не интегрирован и не упомянут в коде; единственное упоминание — в roadmap. Вне репозитория лежат scratch-графы.
- **Ближайший существующий предшественник M1:** `AlphaEntityContract` + `AlphaRigSpec` + motion-family теория из `tools/alpha_models/universal/creature_rig.py`.

| Элемент | Вердикт | Этап |
|---|---|---|
| `tools/alpha_content` генераторы (SHA-pinned воспроизводимость) | `KEEP` как evidence-tooling, не продукт | G0/G2 |
| `tools/alpha_models` transfer pipeline | `RESEARCH` (ценнейший вход для G3/G10) | G3/G10 |
| `AlphaModels/**` Unity runtime | `RESEARCH` | G10 |
| `Content/Generated/*.g.cs` (14 файлов, ~7 060 строк Alpha-чисел) | `RETIRE` из продуктовой цепи, сохранить как oracle-фикстуры | G2/G3 |
| Editor-инструменты импорта (`AlphaModelsImport`) | `REWORK` (dev-only, перенести в собственный контент-корень) | G3/G10 |
| M1 спецификация | `RESEARCH` → вход G3 | G3 |
| ComfyUI | `RESEARCH`, не начато | G10 |

---

## 13. Alpha runtime dependencies (сводка)

Полный список — в [G2 audit §7](G2_WORLD_FOUNDATION_AUDIT.md) и [G1 ТЗ](../stages/G1_TECHNICAL_FOUNDATION_STAGE.md). Кратко:

| Зависимость | Механизм | Класс |
|---|---|---|
| Локальный кэш моделей/ригов/клипов (`<root>/actors/<id>/…`) | `System.IO` + `JsonUtility`, резолв через `CUBEGAME_ALPHA_CONTENT` → `<project>/.scratch/content` → `persistentDataPath` | `AlphaContentSource` |
| Модели предметов/внешности (`items/*.cubdec`, `appearance/*.cubdec`) | тот же кэш | `GameVisualSink`, `HeroLoadout` |
| Атлас иконок (`<project>/AlphaOutput/icons/atlas.png|json`) | `File.ReadAllBytes` + `Texture2D.LoadImage` | `AlphaIconAtlas` |
| 93 звука Alpha (`<root>/sounds/*.wav`) | `File.Exists`, воспроизведения нет | `AlphaSoundLibrary` |
| Оригинальная установка `D:\Games\Cube world` | **не читается в runtime**, только build-time инструментами | `tools/alpha_content` |

Следствие: без локального кэша игра **работает** (мир генерируется, стримится, сохраняется), но существа/предметы рисуются placeholder-кубами, а тесты, требующие кэш, честно пропускаются (`Assert.Ignore`).

### Классификация зависимостей

**TEMPORARY DEVELOPMENT DEPENDENCY** — можно оставить локально, помогает разработке, не блокирует продукт:

| Зависимость | Почему временная |
|---|---|
| Локальный кэш `.scratch/content` (156 actors, предметы, appearance) | dev-данные; при отсутствии игра работает на placeholder |
| `<project>/AlphaOutput/icons/atlas.png` | dev-path для иконок; отсутствие деградирует в текст |
| `.scratch/m0` зонные дампы | только oracle-регрессия, не runtime |
| `tools/alpha_*` и `Cube.exe` (build-time) | извлечение и верификация, вне `Assets/`, не в runtime |
| 93 WAV / `AlphaSoundLibrary` | разрешение имён без воспроизведения; не влияет на игру |

**PRODUCTION BLOCKER** — должно быть заменено собственным контентом CubeGame до релиза:

| Зависимость | Почему блокер | Этап замены |
|---|---|---|
| Модели существ/NPC/предметов из Alpha (`.cubdec`, rig/clip JSON) | нельзя публиковать и нельзя поставлять; без них визуал = placeholder-кубы | G3/G10 |
| Атлас иконок, собранный из Alpha GUI | производная от закрытых ассетов | G12 |
| 93 Alpha-звука и их имена | закрытые ассеты; нужен собственный аудио-контент | G12 |
| 14 таблиц `Content/Generated/*.g.cs` (~7 060 строк Alpha-чисел) | продуктовые данные, производные от Alpha | G2/G3/G6 |
| Каталоги имён существ/предметов/звуков из `data*.db` | тот же правовой режим | G2/G3 |
| `AlphaContentStaging` / упаковка кэша (только в P0–P7-ветках) | поставка Alpha-производного контента недопустима | G3 |

**Ничего из этого не блокирует G1**: техническая платформа обязана работать и без кэша (см. [G1 W6](../stages/G1_TECHNICAL_FOUNDATION_STAGE.md)).


---

## 14. Что это значит для дальнейшей работы

1. Фундамент для G1 уже существует: сеанс/состояния, контракты, события, checkpoint-сейв, world delta, event-driven presentation.
2. Для G2 уже существует работающий генератор с oracle-доказательствами; переписывать его ради отличия от Alpha не нужно.
3. Для G3 существует pipeline и теория ригов, но продуктового кода фабрики нет — начинать надо со спецификации в код, а не с масштабирования.
4. Главный технический долг, который нужно снять в G1: god-class bootstrap, отсутствие input abstraction, отсутствие версионирования сейва, статический синглтон Alpha-кэша, второй (мёртвый) AI/контент-стек, две системы координат.
5. Главный организационный долг: определить судьбу цепи P0–P7 (merge / cherry-pick / drop) до начала G1-реализации.
