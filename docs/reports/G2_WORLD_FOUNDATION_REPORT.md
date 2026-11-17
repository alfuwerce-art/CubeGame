# G2 — WORLD FOUNDATION REPORT

**Этап:** [G2 World Foundation](../stages/G2_WORLD_FOUNDATION_STAGE.md)
**Ветка:** `claude/g2-world-foundation`
**База:** `deepseek/g1-00-runtime-baseline-candidate` @ `cb76aab`
**Unity:** `6000.3.27f2`
**Статус:** `READY_FOR_REVIEW`

---

## 1. Что сделано в одном абзаце

Существующий мир не переписывался: тот же генератор высот/климата/зон/рек/дорог/пещер, тот же streaming с
выгрузкой и floating origin, те же дельты и сейв. Поверх них появился собственный слой CubeGame: именованные
климатические оси, data-driven таблица биомов, явная модель бюджета мира (объясняющая наблюдаемое «мягкое
переполнение» 1 681 запрошенного чанка против ≈1 398 доступных), стратегия дистанций near/mid/far, правила
растительности отдельно от моделей, конвейер размещения структур с persistent id и состоянием, seams для
городов/данжей, контракт сущностей для G3, debug overlay, воспроизводимые измерения производительности и
явная версия генерации в `.world` с отказом при смене профиля. Multi-seed regression gate фиксирует 8 seed и
3 golden-значения.

## 2. Что переиспользовано (без переписывания)

| Система | Файл | Статус G2 |
|---|---|---|
| таблица seed'ов Alpha | `Features/World/AlphaWorldSeeds.cs` | KEEP, обёрнута именами (`WorldSeedTable`) |
| климат-ячейки, регионы, landmark-колонки | `AlphaClimateGrid`, `AlphaRegionGenerator` | KEEP |
| террейн/высоты/горная маска | `AlphaTerrainGenerator` | KEEP |
| зоны, пещеры, реки, дороги, вырубленный камень | `AlphaZoneGenerator`, `AlphaZoneFeatures` | KEEP |
| меш чанка, вода, коллайдер | `ChunkMesher`, `StreamingWorldView` | KEEP + бюджет/метрики |
| политика окна/выгрузки/вершин | `WorldStreamingPolicy` | KEEP (числа из его формулы) |
| дельты и discovered | `WorldDeltaStore`, `WorldState` | KEEP + канал структур |
| контент зоны/растительность/структуры | `ZoneContentPlanner`, `AlphaVegetationPlanner`, `AlphaStructures` | KEEP как временный путь |
| population/NPC/боссы/данжи | `D3ZonePopulation`, `AlphaRecoveredPopulation`, `TownNpcPopulation`, `AlphaDungeonGenerator` | KEEP |
| сейв персонажа/сессии/мира | `GameSlotStore`, `AlphaProfileStore`, форматы v1 | KEEP + опциональный fingerprint |
| тестовая инфраструктура | EditMode/PlayMode, NUnit XML | KEEP |

## 3. Что изменено и добавлено

### 3.1 Новый engine-free слой (Features/World/Foundation)

| Файл | Назначение |
|---|---|
| `WorldGenerationIdentity.cs` | версия профиля генерации, fingerprint `(id, seed, профиль)`, правило совместимости |
| `WorldSeedTable.cs` | именованные seed-оси поверх проверенной Alpha-таблицы (значения не менялись) |
| `WorldClimateModel.cs` | `ClimateAxes` (temperature/humidity/rareness/relief/level/surface/water) + адаптер генератора |
| `BiomeCatalog.cs` | климатический конверт, surface/vegetation/spawn/weather профили, 11 биомов, детерминированное разрешение |
| `WorldStreamingBudget.cs` | модель бюджета, оценка доступных чанков, измерение и статус (`WithinBudget/SoftOver/Over`) |
| `WorldDistanceStrategy.cs` | near/mid/far/offscreen: vegetation, structures, simulation, plant cap, fog |
| `VegetationRuleSet.cs` | правила «где можно» (биом, склон, высота над водой, влажность, температура, вода, spacing, clustering, density) + политика и временная привязка моделей |
| `StructureFoundation.cs` | 8 kinds, правила, детерминированные кандидаты, валидация, persistent id, состояние, удаление через дельту |
| `WorldRegionSeams.cs` | settlement (зона/центр/радиус/якоря/дороги) и dungeon entrance (instance id, level/stars) |
| `EntityDefinitionContract.cs` | `EntityDefinition`/`EntityVisualRecipe`/registry/`ResolvedEntitySpawn` (G3 seam) |
| `WorldPerformanceMetrics.cs` | recorder + snapshot + детерминированный JSON |
| `WorldDebugSnapshot.cs` | факты debug-инструмента, сборка вне Unity |
| `WorldFoundationValidation.cs` | агрегированная проверка всех перекрёстных ссылок |

### 3.2 Presentation / runtime

| Файл | Изменение |
|---|---|
| `Presentation/Gameplay/World/WorldDebugOverlay.cs` | новый dev-overlay (F9 / `-cubeDebugOverlay`), 10 Гц, без UI Toolkit |
| `Presentation/Gameplay/World/WorldMetricsProbe.cs` | кадры/GC/счётчики, `-cubeWorldMetrics <path>`, JSON сессии |
| `Presentation/Gameplay/GameBootstrap.WorldFoundation.cs` | сборка G2-контекста: биомы, бюджет, дистанции, recorder, overlay, probe, identity, seams/структуры зоны |
| `Presentation/Gameplay/World/StreamingWorldView.cs` | `ApplyBudget`, `AttachMetrics`, счётчики зон, лимит растительности, замер чанка/зоны; **при отсутствии бюджета поведение прежнее** |
| `Features/World/AlphaTerrainGenerator.cs` | счётчики кэшей (зоны/регионы/климат) для диагностики |
| `Features/Persistence/SessionSaveData.cs` | опциональное поле `generationFingerprint` в `.world` |
| `Features/Persistence/AlphaProfileStore.cs` | запись fingerprint, отказ при смене профиля |
| `Features/Persistence/GameSlotStore.cs` | то же для legacy paired-slot API |

## 4. Multi-seed gate (G2-A)

- 8 seed разных типов: `1, 2, 7, 666666, 424242, 0xDEADBEEF, 123456789, 4294967295`.
- Digest (FNV-1a 64) покрывает: таблицу seed'ов, 33 колонки террейна (surface/rock/mountain/base/temperature/
  humidity/rareness), 3 региона (level/variant/orientation/climate/64×64 zone records/64 landmark columns).
- Проверено: повторный расчёт даёт тот же digest; все 8 seed дают разные миры; два взаимно используемых
  генератора не влияют друг на друга; изменение на 1 воксель ломает gate; зона (воксели + spawns) воспроизводится
  между экземплярами генератора, другой seed даёт другую зону.
- Заморожены golden-значения (этот build):
  - seed `1` → `16933456655643791213`
  - seed `666666` → `15266482576104709386`
  - seed `4294967295` → `640857990642059632`

## 5. World generation architecture

Полное описание: [WORLD_GENERATION_ARCHITECTURE](../architecture/WORLD_GENERATION_ARCHITECTURE.md).
Кратко: климат → регион (landmark-колонки + зонная сетка) → колонка террейна (base/mountain/ridge) → зона
(поверхность, камень, пещеры, вода озёр, фичи) → чанк 32×32 → контент зоны. Каждый уровень кэшируется и
детерминированно восстанавливается. Две координатные системы (`WorldLayout`, `AlphaWorldScale`) фиксированы как
существующий G1-долг; G2 использует их константы, а не «сырые» числа.

## 6. Streaming budget

`WorldStreamingBudget` (render distance 50, view radius 20):

| Показатель | Значение |
|---|---|
| запрошенных чанков окна | 1 681 (41×41) |
| удерживаемых чанков | 1 681 |
| vertex budget | 12 582 912 (формула `0.01·0x1800000`) |
| измеренных вершин на чанк | 8 998 (baseline G1-00) |
| доступно чанков по бюджету | ≈1 398 |
| статус окна | `SoftOver` (объясняет наблюдавшееся «мягкое переполнение») |

Runtime-бюджет применяется к `StreamingWorldView`: vertex limit не поднимается выше бюджета, растения
перестают добавляться при исчерпании vegetation budget. Без `ApplyBudget` поведение прежнее.

## 7. Biome / climate architecture

- `ClimateAxes` — именованные оси поверх проверенных чисел (`0x5C4800` temperature, `0x5C4DD0` humidity,
  `0x5EF040` rareness, `0x5EFFA0` relief) + level/surface/water.
- `BiomeCatalog.Default()` — 11 биомов: water, volcanic, highlands, snow, tundra, desert, rainforest, forest,
  grassland, rock barrens, temperate (fallback). Каждый ссылается на surface/vegetation/spawn профили,
  structure rules и weather compatibility.
- Разрешение: matches → priority → specificity → ordinal id, fallback гарантирован.
- Проверки: структурная валидация, покрытие климатических проб, вода не разрешается в «сухой» биом,
  перекрёстные ссылки на structure rules и vegetation rule sets.

## 8. Vegetation foundation

- `VegetationRuleSet` — 5 наборов (sparse/grassland/forest/desert/water), 15 правил; правило знает только
  климат/рельеф/воду/плотность/spacing/clustering.
- `VegetationPolicy` — детерминированные density roll (1/10000, integer hash) и выбор kind; spacing и cluster
  лимиты; бюджет зоны.
- `VegetationModelBinding` — `TEMPORARY_RUNTIME`: 10 CubeGame kinds → текущие Alpha-модели (плейсхолдеры).
  Правила этот слой не читают; замена ассетов не меняет ни одного правила.
- Текущий рендер (`AlphaVegetationMeshes`) не изменён.

## 9. Structure foundation

- Kinds: city, village, camp, ruin, tower, cave entrance, dungeon entrance, landmark.
- 7 правил по умолчанию: биомы, level, flatness, высота над водой, spacing, шаблон (размер — W-17 evidence).
- Конвейер: хеш `(seed, zone, rule, slot)` → footprint probe → валидация → placement → persistent id.
- Причины отказа кодируются (`StructureRejection`), ничего не размещается «силой».
- Удаление: `WorldDeltaKind.ObjectRemoved` на канале структур (7), каждый объект зоны имеет уникальный
  delta-key (12-битный индекс от rule/slot); коллизия индексов репортится.
- Состояние: `candidate/generated/discovered/destroyed/cleared` с закрытым набором переходов.

## 10. City / dungeon seams

- `SettlementSite`: регион → зона → центр/радиус/биом/level, 9 NPC-якорей (центр, таверна, 2 магазина, кузнец,
  квесты, стража, 2 жителя), 4 дорожных stub'а, все с persistent id; один кандидат на регион, level 1…6.
- `DungeonEntranceSite`: зона → позиция, `DungeonInstanceId` (блок 2×2 региона), level 1…12, stars 1…5.
- Геометрия не строится; это точки расширения G5/G11.

## 11. Debug tools

`WorldDebugOverlay` (F9, `-cubeDebugOverlay`) показывает: seed, generator id, fingerprint, позицию,
chunk/zone/region, биом, климат, высоту/воду, streaming (chunks/pending/zones/vertices/plants/npc/structures),
статус бюджета с причинами перебора, distance band, perf-строку, ближайшие кандидаты структур, спавны,
проблемы foundation. Сбор данных (`WorldDebugSnapshot`) не зависит от Unity.

## 12. Save / versioning

- Правило: неизменённый мир восстанавливается из seed, изменения игрока — отдельными дельтами.
- Identity = generator id (`cubegame.alpha_terrain/9`) + fingerprint профиля генерации
  (algorithm/climate/biomes/vegetation/structures/seams).
- `.world` пишет `generationFingerprint` (опциональное поле); старые сейвы без поля читаются (`LegacyWorld`).
- Смена любого компонента профиля → fingerprint другой → `GeneratorMismatch` с причиной вместо тихой
  регенерации другого мира.
- Проверено тестом: fingerprint действительно записывается в `.world` и сейв читается обратно.

## 13. Performance measurements

Воспроизводимая схема: `WorldPerformanceRecorder` (engine-free) + `WorldMetricsProbe` (Unity) →
`-cubeWorldMetrics <path>`; JSON с фиксированным порядком ключей. Собирается: generation time, chunks/sec,
mean/max chunk build, vertices, active/cached chunks, zones, vegetation, NPC, objects, GC, frame mean/p95/max,
spikes.

Фактические измерения (Windows Player, seed 666666, `-cubeSmokeSeconds`, Alpha-контент упакован в сборку):

| Показатель | New world (25 с, телепорт +400 вокселей) | Load (12 с) |
|---|---|---|
| построение мира | 25 чанков / 116 108 вершин за 79.5 мс | 25 чанков / 182 788 вершин за 384.7 мс |
| чанков построено за сессию | 1 152 (47.0 чанк/с) | 1 126 (101.4 чанк/с) |
| зон сгенерировано | 30 (mean 1 175.8 мс/зона) | 26 (mean 512.6 мс/зона) |
| mean / max построения чанка | 3.699 / 58.4 мс | 3.844 / 45.9 мс |
| активных чанков в конце | 1 127 | 1 126 |
| вершин в конце | 12 592 756 (`SoftOver`, +0.08 %) | 12 587 492 (`SoftOver`) |
| растений | 24 765 | 24 765 |
| NPC | 1 | 1 |
| кадры mean / p95 / max | 30.6 / 24.2 / 15 531.8 мс | 19.0 / 30.0 / 4 228.5 мс |
| spikes (>3× mean) | 4 | 3 |
| GC / working set | 495.8 МБ / 429.2 МБ | 452.6 МБ / 406.5 МБ |

Max frame — стартовая загрузка мира (единичный кадр), не игровой цикл. Метрики полностью описаны в JSON
(`g2_metrics_fresh.json`, `g2_metrics_load.json` в `.unity-install/logs`, gitignored).

## 14. G3 entity seams

`EntityDefinition` + `EntityVisualRecipe` (`VisualSourceKind`: placeholder/imported/external generated/temporary
Alpha), `RigAnimationPackage`, `EntityDefinitionRegistry`, `EntitySpawnContract` (spawn + стабильный appearance
seed). Мир не знает, откуда модель; адаптер текущего runtime превращает `SpawnDefinition` в определение с
`TemporaryAlpha` recipe.

## 15. Тесты

| Прогон | Результат |
|---|---|
| Компиляция (EditMode batch, с кэшем и без) | `error CS = 0` |
| G2 EditMode suite (34 теста) | **34 PASS / 0 FAIL / 0 SKIP** |
| Полный EditMode с Alpha-кэшем | **893 total / 887 passed / 0 failed / 6 skipped** |
| Полный EditMode без Alpha-кэша | 893 total / 879 passed / 1 failed (`SettingsFromLibrarySplitPassiveObjectsFromCreatures`, требует кэш) / 11 skipped / 2 inconclusive |
| G2 PlayMode сценарий | **1 PASS / 0 FAIL** (бюджет, snapshot, overlay, метрики, floating origin) |
| Полный PlayMode с Alpha-кэшем | см. §17 |
| Windows Player | собран, `BUILD_SUCCEEDED`, `errors=0`; smoke new/load PASS |

Тест-матрица: [G2_WORLD_TEST_MATRIX](../planning/G2_WORLD_TEST_MATRIX.md).

## 16. Windows Player

- Сборка: `CubeGame.EditorTools.PilotBuild.BuildStandalone` → `.unity-install/build/Pilot/CubeGamePilot.exe`
  (667 136 B; output 92.77 МБ; `errors=0`; content gate `CONTENT_GATE_SOURCE_OK` с iconAtlas=1, 232 МБ контента
  упаковано в `StreamingAssets/CubeGame/AlphaContent`). Live-установка продюсера **не заменялась**.
- Smoke 1 (новый мир): `-batchmode -cubeSaveDir <dir> -cubeAutoNew slot0 -cubeSeed 666666 -cubeTeleport 400 0
  -cubeSaveCheckpoint -cubeSmokeSeconds 25 -cubeDebugOverlay -cubeWorldMetrics <json>` →
  мир построен, идентичность генерации записана, 1 127 чанков настримлено, биом `biome.forest`,
  бюджет `SoftOver`, метрики записаны, чекпоинт сохранён, чистый выход.
- Smoke 2 (загрузка после перезапуска): `-cubeAutoLoad slot0 -cubeSmokeSeconds 12 -cubeWorldMetrics <json2>` →
  `[Game] alpha profile store loaded migrated slot0` → мир/герой/тик 280 восстановлены, fingerprint совпал,
  чанки настримлены, метрики записаны, чистый выход.
- Боевые/лут-сценарии в Player этим smoke не проверялись (нет гарантированно враждебного существа в стартовой
  зоне — известное ограничение baseline); они покрыты PlayMode F2/F3 сценариями.

## 17. Итоговые результаты прогонов

| Прогон | Команда | Результат |
|---|---|---|
| G2 EditMode | `-runTests -testPlatform EditMode -testFilter G2World` | 34 / 34 PASS |
| Полный EditMode (кэш) | `-runTests -testPlatform EditMode` + `CUBEGAME_ALPHA_CONTENT` | 893 / 887 PASS, 0 FAIL, 6 SKIP, `error CS = 0` |
| G2 PlayMode | `-testFilter G2WorldFoundationPlayModeTests` | 1 / 1 PASS |
| Полный PlayMode (кэш) | `-runTests -testPlatform PlayMode` + `CUBEGAME_ALPHA_CONTENT` | см. ниже |
| Player build | `-executeMethod CubeGame.EditorTools.PilotBuild.BuildStandalone` | `BUILD_SUCCEEDED`, errors=0 |
| Player smoke new | `-cubeAutoNew ... -cubeSmokeSeconds 25` | PASS (save, metrics, quit) |
| Player smoke load | `-cubeAutoLoad ... -cubeSmokeSeconds 12` | PASS (restore, continue) |

Полный PlayMode с кэшем: **76 total / 76 passed / 0 failed / 0 skipped** (включая новый G2-сценарий).

## 18. Blockers и ограничения

- Alpha-контент (`.cubdec`, WAV, шрифты, atlas) остаётся `TEMPORARY_RUNTIME`; G2 не создаёт новых зависимостей.
- Города/данжи — только seams; растительность визуально остаётся Alpha-плейсхолдером.
- Две координатные системы (`WorldLayout`/`AlphaWorldScale`) — G1-долг, зафиксирован.
- Полные зонные oracle-дампы требуют локального `.scratch/m0` (skip в чистом checkout).

## 19. Следующий work package

**G2-1 — Content binding of the foundation** (вход в G3/G5):

1. Подключить `VegetationPolicy` к фактическому размещению растений (заменить временный Alpha-скан на
   CubeGame-правила, сохранив текущие модели как плейсхолдеры).
2. Подключить `StructurePlacementPipeline` к runtime-представлению структур (сейчас runtime использует
   Alpha-путь домов) и к состоянию «уничтожено».
3. City/dungeon seams → прототип одного города и одного данжа на реальных данных (без production-контента).
4. Заполнить `EntityDefinitionRegistry` из текущих species kinds и перевести визуальный слой на recipe-контракт.

---

## Приложение A. Как воспроизвести

```powershell
# EditMode (весь набор)
Unity.exe -batchmode -nographics -projectPath <repo> -runTests -testPlatform EditMode `
          -testResults .unity-install/results/editmode.xml -logFile .unity-install/logs/editmode.log

# Только G2
Unity.exe -batchmode -nographics -projectPath <repo> -runTests -testPlatform EditMode `
          -testFilter G2World -testResults .unity-install/results/g2.xml -logFile .unity-install/logs/g2.log

# Player
Unity.exe -batchmode -nographics -projectPath <repo> -executeMethod CubeGame.EditorTools.PilotBuild.BuildStandalone
```
