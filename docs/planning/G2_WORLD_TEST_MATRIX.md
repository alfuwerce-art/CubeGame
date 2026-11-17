# G2 — WORLD TEST MATRIX

**Этап:** [G2 World Foundation](../stages/G2_WORLD_FOUNDATION_STAGE.md)
**Ветка:** `claude/g2-world-foundation` · **База:** `cb76aab`
**Unity:** `6000.3.27f2`

Матрица фиксирует, **что именно доказывает** каждый тест G2. Статусы заполняются по фактическому прогону
(см. [G2 report](../reports/G2_WORLD_FOUNDATION_REPORT.md)); «PASS» здесь означает выполненный прогон, а не
намерение.

## 1. EditMode — G2 unit / foundation

Файл: `Assets/CubeGame/Tests/FullM0/EditMode/G2WorldFoundationTests.cs`
(категория: не требует Alpha-кэша).

| ID | Тест | Что доказывает | Статус |
|---|---|---|---|
| G2-U01 | `GenerationProfileIsVersionedAndHashed` | профиль генерации версионирован и хешируется стабильно | PASS |
| G2-U02 | `GeneratorVersionIsParsedFromTheIdSuffix` | версия генератора читается из id (`.../9`), мусор → 0 | PASS |
| G2-U03 | `FingerprintDependsOnGeneratorSeedAndProfile` | fingerprint зависит от генератора, seed и профиля | PASS |
| G2-U04 | `CompatibilityAcceptsMatchingAndLegacyIdentities` | совместимый сейв читается, старый (без fingerprint) — `LegacyWorld` | PASS |
| G2-U05 | `CompatibilityRefusesAChangedProfileOrGenerator` | смена профиля/генератора/битый fingerprint → отказ с причиной | PASS |
| G2-U06 | `WorldFileRecordsTheGenerationFingerprint` | `.world` реально пишет fingerprint, сейв читается обратно | PASS |
| G2-U07 | `NamedSeedTableMatchesTheVerifiedAlphaTable` | именованные seed-оси равны проверенной Alpha-таблице | PASS |
| G2-U08 | `ClimateAxesClampAndDescribeWater` | климатические оси клампятся, вода/высота считаются верно | PASS |
| G2-U09 | `BiomeCatalogIsValidAndCoversTheClimateSpace` | таблица биомов структурно валидна и покрывает климат | PASS |
| G2-U10 | `BiomeResolutionIsDeterministicAndPriorityAware` | разрешение биома: priority → specificity → детерминизм | PASS |
| G2-U11 | `StreamingBudgetExplainsTheCurrentWindow` | бюджет объясняет 1681 запрошенный чанк и ≈1398 доступных | PASS |
| G2-U12 | `BudgetEvaluatorSeparatesSoftAndHardOverages` | soft/hard перебор различаются; hard — ошибка | PASS |
| G2-U13 | `DistanceStrategyTilesTheViewAndKeepsFogInsideIt` | near/mid/far без дыр; fog внутри радиуса; vegetation/sim выключаются | PASS |
| G2-U14 | `VegetationRulesRejectSlopeWaterAndDrySites` | правила отклоняют склон/воду/сухость; water-набор принимает дно | PASS |
| G2-U15 | `VegetationDensityAndKindAreDeterministic` | density roll и выбор kind детерминированы, оба исхода достижимы | PASS |
| G2-U16 | `VegetationSpacingAndClusterLimitsAreEnforced` | spacing и cluster-лимит соблюдаются | PASS |
| G2-U17 | `VegetationDecisionReportsBudgetAndRule` | бюджет зоны и выбранное правило возвращаются в решении | PASS |
| G2-U18 | `EveryVegetationKindHasATemporaryModelBinding` | у каждого CubeGame kind есть временная модель-плейсхолдер | PASS |
| G2-U19 | `StructurePipelineIsDeterministicWithStableIds` | кандидаты и id стабильны; разные seed → разные id; village/dungeon есть | PASS |
| G2-U20 | `StructurePipelineDoesNotForceAPlacement` | плохой footprint/биом/level не размещаются, причины возвращаются | PASS |
| G2-U21 | `StructureRuntimeStateTransitionsAreClosed` | набор переходов состояния структур закрыт | PASS |
| G2-U22 | `StructureRemovalSurvivesThroughTheWorldDeltaStore` | удаление структуры идёт через существующую дельту, ключи уникальны | PASS |
| G2-U23 | `SettlementSeamIsDeterministicAndCarriesAnchorsAndRoads` | settlement детерминирован, есть якоря/дороги, level-гейт работает | PASS |
| G2-U24 | `DungeonSeamPublishesAStableInstance` | вход в данж и instance id стабильны, level/stars валидны | PASS |
| G2-U25 | `EntityDefinitionContractValidatesAndAdaptsSpawns` | контракт G3 валиден; спавн адаптируется без знания о фабрике | PASS |
| G2-U26 | `PerformanceRecorderProducesReproducibleNumbers` | метрики (mean/p95/max/spikes/chunks per second/JSON) воспроизводимы | PASS |
| G2-U27 | `DebugSnapshotShowsWorldFacts` | snapshot содержит seed/chunk/zone/biome/height/budget, есть unavailable-путь | PASS |
| G2-U28 | `FoundationValidationIsClean` | агрегированная проверка всех ссылок пуста | PASS |

## 2. EditMode — multi-seed gate (G2-A)

Файл: `Assets/CubeGame/Tests/FullM0/EditMode/G2WorldDeterminismGateTests.cs`
(не требует Alpha-кэша; зонный тест — самый тяжёлый в наборе).

| ID | Тест | Что доказывает | Статус |
|---|---|---|---|
| G2-A01 | `SameSeedOnTwoFreshGeneratorsIsIdentical` | same seed + same build → один и тот же мир | PASS |
| G2-A02 | `EverySeedTypeProducesADifferentWorld` | 8 seed разных типов дают 8 разных миров | PASS |
| G2-A03 | `GoldenDigestsAreFrozenForThisGenerationProfile` | 3 seed заморожены: изменение алгоритма ломает suite | PASS |
| G2-A04 | `DigestDetectsAOneVoxelChange` | gate чувствителен к изменению на 1 воксель | PASS |
| G2-A05 | `GenerationOrderDoesNotDependOnInterleaving` | два генератора, используемые вперемешку, не влияют друг на друга | PASS |
| G2-A06 | `GeneratedZoneVoxelsAreReproducibleAcrossInstances` | воксели зоны воспроизводятся между экземплярами; другой seed → другой мир | PASS |

## 3. PlayMode — runtime integration

| ID | Проверка | Что доказывает | Статус |
|---|---|---|---|
| G2-P01 | `G2WorldFoundationPlayModeTests.EnteringAWorldBuildsTheFoundationContext` | в живом мире бюджет применён к streamer'у, foundation-проверки пусты, snapshot/overlay/метрики дают факты, floating origin не меняет мировую позицию | PASS |
| G2-P02 | существующий PlayMode suite (с Alpha-кэшем) | streaming, floating origin, бой, лут, save/load не сломаны G2 (76/76) | PASS |

## 4. Windows Player

| ID | Проверка | Что доказывает | Статус |
|---|---|---|---|
| G2-W01 | build `PilotBuild.BuildStandalone` | проект собирается в Windows Player | PASS (`BUILD_SUCCEEDED`, errors=0, content gate iconAtlas=1) |
| G2-W02 | smoke `-cubeAutoNew` + `-cubeWorldMetrics` + `-cubeSmokeSeconds` | мир создаётся, стримится (1 127 чанков), метрики пишутся, чекпоинт сохраняется, чистый выход | PASS |
| G2-W03 | smoke `-cubeAutoLoad` | сохранённый мир загружается после перезапуска (герой, тик 280, fingerprint) | PASS |

## 5. Что НЕ покрыто и почему

| Область | Причина |
|---|---|
| Полные зонные oracle-дампы | требуют локального `.scratch/m0`, не входят в репозиторий |
| Alpha-кэш модели/иконки | dev-контент; G2-тесты намеренно от него не зависят |
| Финальные биомы/структуры | вне G2 (G9/G11) |
| Производительность на длинной сессии >15 мин | измеряется прогоном Player, не автотестом |

---

## 6. G2-1 — CONTENT BINDING

Файлы: `Assets/CubeGame/Tests/FullM0/EditMode/G2_1ContentBindingTests.cs`,
`Assets/CubeGame/Tests/FullM0/PlayMode/G2_1ContentBindingPlayModeTests.cs`.
Одна зона генерируется один раз и переиспользуется edit-тестами (тяжёлая часть — генерация зоны).

| ID | Тест | Что доказывает | Статус |
|---|---|---|---|
| G2-1-U01 | `EntityCatalogueIsCubeGameNativeAndBoundToPlaceholders` | 5 CubeGame-native определений, все с `TemporaryAlpha` recipe и placeholder-привязкой | PASS |
| G2-1-U02 | `ContentIdsAreStableAndNamespaced` | persistent id формат/стабильность, каналы 7/8 не совпадают | PASS |
| G2-1-U03 | `VegetationIsPlacedByTheCubeGamePolicy` | биом → профиль → rule set → kind/model; уникальные StableIndex; растения внутри резерва отсутствуют | PASS |
| G2-1-U04 | `VegetationPlacementIsDeterministicAcrossGenerators` | same seed → те же id/координаты/индексы; другой seed → другой мир | PASS |
| G2-1-U05 | `VegetationIsNotGeneratedInsideSettlementOrDungeonReserve` | резервы settlement/данжа исключены из vegetation | PASS |
| G2-1-U06 | `StructuresArePlacedByTheCubeGamePipeline` | структуры с id/spawn index/biome; village/city не из random pipeline | PASS |
| G2-1-U07 | `StructuresAreDeterministicAcrossGenerators` | структуры воспроизводятся между экземплярами генератора | PASS |
| G2-1-U08 | `StructurePopulationServesTheZoneAndSkipsRemovedObjects` | population отдаёт зону, удаление через delta убирает spawn | PASS |
| G2-1-U09 | `SettlementBlueprintReservesAreaSlotsRoadAndAnchors` | 4 слота, дорога, ≥8 якорей, границы резерва | PASS |
| G2-1-U10 | `DungeonPrototypeCarvesATestRoomAndReturnsToTheEntrance` | один вход на блок, комната/стены/пол/шахта, Contains | PASS |
| G2-1-U11 | `RemovedVegetationStaysRemovedAfterRegeneration` | удалённое растение не возвращается после перегенерации | PASS |
| G2-1-P01 | `CubeGameContentIsGeneratedAndStateSurvivesStreaming` | vegetation+s settlement anchors+dungeon enter/exit+destroy+unload/reload+snapshot в живом мире | PASS |
| G2-1-W01 | `-cubeSmokeContent fresh` | Player: 160 растений, 4 слота/9 якорей, вход/выход данжа, удаление портала и растения, save | PASS |
| G2-1-W02 | `-cubeSmokeContent verify` | после перезапуска: removed_structures=1, removed_plants=1 | PASS |
