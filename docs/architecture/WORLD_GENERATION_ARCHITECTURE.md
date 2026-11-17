# WORLD GENERATION ARCHITECTURE (CubeGame, G2)

**Статус:** описано по факту кода baseline `cb76aab` + G2 Foundation
**Unity:** `6000.3.27f2` · **Review-ветка:** `claude/g2-world-foundation`
**Связанные документы:** [G2 audit](../planning/G2_WORLD_FOUNDATION_AUDIT.md),
[G2 stage](../stages/G2_WORLD_FOUNDATION_STAGE.md), [G2 test matrix](../planning/G2_WORLD_TEST_MATRIX.md)

Документ описывает **существующую** генерацию (то, что переиспользуется) и **новый слой G2** (то, что
добавлено). Он не описывает, как мир «должен» выглядеть по GDD.

---

## 1. Слои

```
┌──────────────────────────────────────────────────────────────────────────────┐
│ Presentation (Unity)                                                        │
│  GameBootstrap (+ .WorldFoundation)  StreamingWorldView  WorldDebugOverlay   │
│  WorldMetricsProbe  AlphaVegetationMeshes  GameVisualSink                     │
├──────────────────────────────────────────────────────────────────────────────┤
│ Features (engine-free)                                                      │
│  Features/World            генератор: климат, регионы, зоны, воксели, дельты │
│  Features/World/Foundation G2: биомы, бюджет, дистанции, правила, структуры,  │
│                            seams, debug snapshot, метрики, identity, entity  │
│  Features/Creatures        контент зоны, population, spawn runtime           │
│  Features/Session          GameSession, WorldPopulation, часы, события       │
│  Features/Persistence      AlphaProfileStore, GameSlotStore, форматы сейва   │
├──────────────────────────────────────────────────────────────────────────────┤
│ Content (engine-free)                                                       │
│  AlphaWorldScale, AlphaStructures, AlphaVegetationPlanner, ZoneContentPlanner│
│  Generated/*.g.cs (Alpha-таблицы, TEMPORARY_RUNTIME)                        │
├──────────────────────────────────────────────────────────────────────────────┤
│ Core (engine-free)                                                          │
│  DeterministicHash, PilotHeightField (legacy), combat/items/progression math │
└──────────────────────────────────────────────────────────────────────────────┘
```

Правило: `Core`/`Content`/`Features` не ссылаются на UnityEngine (asmdef `noEngineReferences: true` для
`CubeGame.Features`). G2-слой полностью engine-free, поэтому проверяется EditMode-тестами и пригоден для
серверной/инструментальной переиспользования.

---

## 2. Координатные системы

| Единица | Размер | Где |
|---|---|---|
| воксель (voxel) | 1 блок = 1 Unity unit | всё |
| чанк | 32×32 вокселя | streaming/меш (CubeGame) |
| зона | 256×256 вокселей | генерация, стриминг зон, контент |
| landmark cell | 8×8 зон = 2 048 вокселей | климат/landmark-правила Alpha |
| регион | 64×64 зоны = 16 384 вокселя | региональные записи, дельты |
| мир | 1024×1024 региона = 2^24 вокселей | верхняя граница |

Два класса констант описывают одно и то же: `WorldLayout` (Features/World, используется транспортом и
генерацией) и `AlphaWorldScale` (Content, используется размещением контента). Числа совпадают; расхождение
— технический долг G1 (`двойная система координат`), G2 его не переписывает, но фиксирует, что новые
G2-правила работают через `WorldLayout`/`AlphaWorldScale` только через их константы, без «сырых» чисел.

Старт: `WorldLayout.StartVoxel = 32800·256 + 128` (центр стартовой зоны; G0/OQ-22 `PARTIAL`), регион (512, 512),
level 1.

Floating origin: абсолютные координаты — `double` воксели, Transform держит `world − Origin`
(`StreamingWorldView.Origin`, rebase при 512 units). Ни один G2-класс не хранит мировую позицию во float.

---

## 3. Seed и RNG

| Уровень | Механизм | Файл |
|---|---|---|
| master seed мира | `uint` в `.session`/`.world`, дефолт `666666` | `GameBootstrap.DefaultSeed`, `AlphaProfileStore` |
| таблица seed'ов | `srand(master)` → `rand()%100000` в 74 слота по Alpha-смещениям | `AlphaWorldSeeds` |
| кубические имена | `WorldSeedTable` (**G2**) — те же значения, роли названы | `Foundation/WorldSeedTable.cs` |
| регион/зона/климат | свой CRT-поток на зону: `srand(worldSeed + zz·0x10000 + zx)`; климат: `srand(rx + 0x108A + rz·0x400 + seed·3)` | `AlphaZoneGenerator`, `AlphaClimateGrid` |
| шум рельефа | целочисленный value-noise (детерминированный, без `Math.Random`) | `AlphaNoise` |
| CubeGame-размещение | FNV-подобный integer hash от `(seed, zone, rule, slot)` | `DeterministicHash` |

Ни один слой не использует `System.Random`, время, адрес памяти или плавающую арифметику для выбора
позиции объекта.

---

## 4. Стадии генерации (существующие, переиспользуются)

```
климат-ячейка (регион)            AlphaClimateGrid.Get(rx, rz)
        │  anchor, rare flag, field3/4, base height
        ▼
регион                            AlphaRegionGenerator.Generate
   ├── level/variant/orientation (0x5A4710 и rand-правила)
   ├── 64 landmark columns (8×8): тип, радиус, base, level
   └── 64×64 зонная сетка: type/sub/id/level/class (города = type 1)
        ▼
колонка террейна                  AlphaTerrainGenerator.Column
   ├── Base(x,z)  (0x5C5E20): климат-взвешивание, октавы, landmark-эффекты
   ├── Mountain(x,z) (0x5EFFA0): горная маска
   └── ridge-термы, dome Canyon/Peak, rock flag, surface = int(height)
        ▼
зона 256×256                      AlphaZoneGenerator.Generate
   ├── base grid 257×257 (с бордюром)
   ├── 65 536 колонок (Parallel.For): surface/stone/sand/water material, цвета
   ├── пещеры: roof/floor/ceiling noise, вода ниже y=1, cover flag
   ├── озеро (Lake landmark), стена камня, скальные цвета
   ├── cave content candidates (потребляют CRT-поток зоны)
   ├── AlphaZoneFeatures: carved rock, roads, rivers
   └── IZoneContentProvider (см. §6) — структуры/растительность/спавны
        ▼
чанк 32×32                        ChunkVoxels.Sample + ChunkMesher.Build
```

Каждый уровень кэшируется: климат — `ConcurrentDictionary` с вытеснением за 5 регионов; регионы —
`Dictionary` с вытеснением за 3 региона; зоны — LRU-подобный кэш (24 записи, при streaming — по keep-radius
зон). Все кэши пересоздают данные детерминированно, поэтому выгрузка не меняет мир.

---

## 5. Streaming (существующий, G2 добавляет бюджет и метрики)

`StreamingWorldView` + `WorldStreamingPolicy.Alpha`:

| Параметр | Значение |
|---|---|
| окно | 41×41 чанк (view radius 20) |
| keep | радиус 20 (выгрузка за пределами) |
| vertex budget | `renderDistance · 0.01 · 0x1800000` (50 → 12 582 912) |
| порядок | по расстоянию до центра, затем X, Z |
| зоны | порядок по расстоянию до центра в пределах 768 вокселей (3 зоны) |
| воркеры | `ProcessorCount − 1` на чанк-меши; 1 задача на зону |
| лимит установки | 4 чанка/кадр в не-Alpha окне, `int.MaxValue` в Alpha-окне с задержкой 5 мс |
| выгрузка зон | раз в 1000 мс за пределами keep-radius |
| меш | 1 quad на открытую грань, отдельный water-меш, отдельный vegetation-меш |
| коллайдер | `MeshCollider` на terrain-меше, перестраивается при правках |
| floating origin | rebase при 512 units |

**G2-C** (`WorldStreamingBudget`) объясняет наблюдаемое «мягкое переполнение»: 1 681 чанк окна при
~8 998 вершинах на чанк требует ≈15.1 M вершин, лимит даёт ≈1 398 чанков. Бюджет теперь считается из
render distance и измеряется (`WorldBudgetMeasurement`/`WorldBudgetEvaluator`: `WithinBudget`/`SoftOver`/`Over`).

**G2-D** (`WorldDistanceStrategy`) добавляет near/mid/far/offscreen: где ещё есть растительность, где только
структуры, где симулируются существа и где начинается fog. Это политика, а не геометрия: классический mesh-LOD
для воксельного мира не применяется, потому что чанк уже плоский (quad-set) и его отсекает vertex budget.

---

## 6. Контент мира (объекты, NPC, структуры)

| Источник | Что даёт | Кэш/поток |
|---|---|---|
| `AlphaZoneContentProvider` (IZoneContentProvider) | зона → `ZoneContentPlanner` (W-18 vegetation, creature slots, structure templates) → воксели домов + `SpawnDefinition` | внутри генерации зоны, поток зоны |
| `D3ZonePopulation` | content-spawns зоны → активные сущности | по активным зонам игрока |
| `AlphaRecoveredPopulation` | Alpha spawn templates: wild grid 3×3, radius 200, despawn 512 | по активным зонам |
| `TownNpcPopulation` | городские NPC | по активным зонам |
| `AlphaMissionBossPopulation` | боссы региональных миссий | по регионам миссий |
| `AlphaDungeonGenerator` (P7) | данж-зоны 3/5 | по зоне |
| `AlphaLandmarkPatrol*` | патрули landmark-колонн | по зоне |

**G2-F** вводит собственный конвейер структур: kinds (city/village/camp/ruin/tower/cave/dungeon/landmark),
правила (биом, level, flatness, вода, spacing, шаблон), детерминированный candidate, persistent id
`<seed>:<zone>:<rule>:<slot>`, состояние (`candidate/generated/discovered/destroyed/cleared`) и удаление через
существующий `WorldDeltaKind.ObjectRemoved` на отдельном канале.

**G2-G** добавляет seams: `SettlementSite` (резерв зоны, радиус, биом, level, 9 NPC-якорей, 4 дорожных stub'а)
и `DungeonEntranceSite` (зона, позиция, `DungeonInstanceId`, level/stars). Геометрия городов/данжей — G5/G11.

**G2-E** отделяет правила растительности от моделей: `VegetationRuleSet` (где можно) + `VegetationModelBinding`
(что рисуется, `TEMPORARY_RUNTIME`). Текущий путь отрисовки (`AlphaVegetationMeshes`, Alpha `.cubdec`) не изменён.

---

## 7. Persistence

| Что | Где | Формат |
|---|---|---|
| персонаж | `slot0`/`characters.db` | `CUBEGAME_SAVE` v1 |
| сессия (мир, часы, герой, инвентарь) | `slot0.session` / `characters.db` | `CUBEGAME_SESSION` v1 |
| мир (дельты, discovered, часы) | `slot0.world` / `world_<id>` | `CUBEGAME_WORLD` v1 |
| список миров | `worlds.db` | текстовый индекс |
| identity генерации | `.world` поле `generationFingerprint` (**G2-J**, опциональное) | FNV-1a 64 hex |

Правило: **неизменённый мир восстанавливается из seed, изменения игрока сохраняются отдельно.**

- дельты: `BlockSet`, `ColumnSurfaceSet`, `ObjectRemoved`, `ContainerOpened`, `EntityStateChanged`,
  `discovered` зоны; лимит 1 000 000; дедупликация по ключу; отказ на повреждённом файле;
- стабильные id: `StableEntityId.ForZoneSpawn(zone, channel, index)`; структуры используют свой канал;
- G2-J: при смене `WorldGenerationProfile` (алгоритм/климат/биомы/растительность/структуры/seams) fingerprint
  меняется, и старый мир получает явный `GeneratorMismatch` вместо тихой регенерации. Сейвы без поля читаются
  как `LegacyWorld`.

---

## 8. G2 Foundation — карта файлов

| Файл | Содержимое |
|---|---|
| `Features/World/Foundation/WorldGenerationIdentity.cs` | версия профиля генерации, fingerprint, правило совместимости |
| `Features/World/Foundation/WorldSeedTable.cs` | именованные seed-оси поверх проверенной Alpha-таблицы |
| `Features/World/Foundation/WorldClimateModel.cs` | `ClimateAxes`, `IWorldClimateSource`, адаптер генератора |
| `Features/World/Foundation/BiomeCatalog.cs` | климатический конверт, surface/vegetation/spawn/weather профили, биомы, детерминированное разрешение |
| `Features/World/Foundation/WorldStreamingBudget.cs` | модель бюджета и оценка измерения |
| `Features/World/Foundation/WorldDistanceStrategy.cs` | near/mid/far/offscreen политика |
| `Features/World/Foundation/VegetationRuleSet.cs` | правила «где можно», политика размещения, временная привязка моделей |
| `Features/World/Foundation/StructureFoundation.cs` | kinds, правила, кандидаты, валидация, persistent id, состояние, удаление |
| `Features/World/Foundation/WorldRegionSeams.cs` | settlement/dungeon seams, NPC-якоря, дорожные stub'ы |
| `Features/World/Foundation/EntityDefinitionContract.cs` | `EntityDefinition`/`EntityVisualRecipe`/registry — контракт с G3 |
| `Features/World/Foundation/WorldPerformanceMetrics.cs` | recorder + snapshot + JSON |
| `Features/World/Foundation/WorldDebugSnapshot.cs` | факты для debug overlay |
| `Features/World/Foundation/WorldFoundationValidation.cs` | агрегированная проверка согласованности |
| `Presentation/Gameplay/World/WorldDebugOverlay.cs` | dev overlay (F9 / `-cubeDebugOverlay`) |
| `Presentation/Gameplay/World/WorldMetricsProbe.cs` | кадры/GC/счётчики, `-cubeWorldMetrics <path>` |
| `Presentation/Gameplay/GameBootstrap.WorldFoundation.cs` | сборка G2-контекста в runtime |

Интеграционные правки в существующем коде: `StreamingWorldView` (бюджет/метрики/счётчики зон),
`AlphaTerrainGenerator` (счётчики кэшей), `GameBootstrap` (setup/teardown, CLI), `AlphaProfileStore`/
`GameSlotStore`/`SessionSaveData` (fingerprint).

---

## 9. Известные ограничения (не блокируют G2)
1. Две координатные системы (`WorldLayout` и `AlphaWorldScale`) остаются — G1 debt.
2. `PilotHeightField` (старый pilot-мир) остаётся в Core как legacy, не используется генератором.
3. Alpha-таблицы `Content/Generated/*.g.cs` и `.cubdec`-модели остаются `TEMPORARY_RUNTIME`.
4. У структур нет собственной геометрии: шаблоны W-17 дают размеры, дома строит Alpha-путь.
5. Города/данжи — seams; их содержимое — G5/G11.
6. Вода — уровень `0` (гипотеза), визуал — placeholder; G9.
7. Полные зонные oracle-дампы требуют локального `.scratch/m0` (в чистом checkout эти тесты skip).

---

## 10. G2-1: CONTENT BINDING (CubeGame owns placement)

From G2-1 the world content has a CubeGame owner. The generator id records which content stage produced a world:

| id | content stage |
|---|---|
| `cubegame.alpha_terrain/9` | historical (G2 and older): Alpha trees, ground vegetation, houses, Alpha dungeons |
| `cubegame.alpha_terrain/10` | **current**: same terrain/features, content from `CubeGameVegetationProvider` + `CubeGameStructureProvider` |

### 10.1 Pipeline of a zone (`/10`)

```
terrain passes (unchanged: surface, caves, lakes, carved rock, rivers, roads)
        |
        +--> CubeGameVegetationProvider
        |       biome (BiomeCatalog) -> vegetation profile -> rule set -> VegetationPolicy
        |       candidates on a 3-voxel grid (7 225/zone), height/slope/water (zone heightfield + BFS),
        |       moisture/temperature (column), spacing/cluster/density (integer hash)
        |       -> AlphaPlantPlacement (StableIndex) + VegetationPlacement (persistent id)
        |
        +--> CubeGameStructureProvider
                StructurePlacementPipeline (camp/ruin/tower/cave/landmark)
                SettlementSeamPlanner -> SettlementBlueprint (4 slots, road stub, 9 anchors)
                DungeonSeamPlanner -> DungeonPrototype (entrance + carved 11x11x5 room)
                -> prototype voxels + StructurePlacement + SpawnDefinition (channel 7)
```

### 10.2 Identity and state

| object | persistent id | removal key |
|---|---|---|
| vegetation | `veg:<seed>:<zoneX>:<zoneZ>:<site>:<rule>` | `ObjectRemoved`, channel 8, `StableIndex` |
| structure | `structure:<seed>:<zoneX>:<zoneZ>:<rule>:<slot>` | `ObjectRemoved`, channel 7, `SpawnIndex` |
| settlement | `seam.settlement:<seed>:<regionX>:<regionZ>` | channel 7, marker spawn index |
| anchor | `seam.anchor:<seed>:<regionX>:<regionZ>:<kind>:<index>` | channel 7, anchor spawn index |
| dungeon portal | `portal:<seed>:<zoneX>:<zoneZ>` | channel 7, portal spawn index |
| dungeon instance | `dungeon.instance:<seed>:<blockX>:<blockZ>` | instance id (session) |

`StructurePlacement.SpawnIndex` is assigned by the provider in creation order (structures, settlement marker,
anchors from 2048, portal from 3048, creatures from 4048), so a structure and its anchors never share a delta key.

### 10.3 Runtime

`CubeGameContentRuntime` (Presentation) is the gameplay side: structure states derived from zone content + delta,
vegetation removal, dungeon session (enter/exit to the same entrance), and the content lines of the debug overlay.
`CubeGameStructurePopulation` serves channel 7 to the existing `WorldPopulation`, so streaming, respawn and save
behaviour stay the same as before.

`StreamingWorldView` filters removed plants when it builds the chunk vegetation mesh; the terrain mesh, budget and
vertex handling are unchanged.

### 10.4 Entity identity (G3 seam)

`CubeGameEntityCatalog` defines five CubeGame-native entities; `EntityVisualRecipe` carries the (temporary) Alpha
model key and the rig/animation package. `CubeGameVisualBindings` is the only place that maps a CubeGame definition
to an Alpha species kind, and it is marked `TEMPORARY_VISUAL_PLACEHOLDER` for G3 to replace.
