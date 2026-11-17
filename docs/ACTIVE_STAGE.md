# ACTIVE STAGE — G2 WORLD FOUNDATION

**Canonical branch:** `integration/m0-all` · **G1 baseline branch:** `deepseek/g1-00-runtime-baseline-candidate`
**Review branch:** `claude/g2-world-foundation` (база `cb76aab`)
**Статус:** `READY_FOR_REVIEW` — агент не выставляет `ACCEPTED`, решение за продюсером.

**ТЗ этапа:** [G2_WORLD_FOUNDATION_STAGE.md](stages/G2_WORLD_FOUNDATION_STAGE.md)
**Отчёт:** [G2_WORLD_FOUNDATION_REPORT.md](reports/G2_WORLD_FOUNDATION_REPORT.md)
**Архитектура:** [WORLD_GENERATION_ARCHITECTURE.md](architecture/WORLD_GENERATION_ARCHITECTURE.md)
**Тест-матрица:** [G2_WORLD_TEST_MATRIX.md](planning/G2_WORLD_TEST_MATRIX.md)
**Входной аудит:** [G2_WORLD_FOUNDATION_AUDIT.md](planning/G2_WORLD_FOUNDATION_AUDIT.md)

---

## 1. Что изменено

Существующий мир (P0–P7 runtime из G1-00) **переиспользован**: генератор высот/климата/зон/рек/дорог/пещер,
streaming с выгрузкой, floating origin, world modifications и сейв не переписывались. Поверх появился
собственный слой CubeGame:

| Пакет | Результат |
|---|---|
| G2-A | multi-seed regression gate: 8 seed, 3 golden digest, зонная воспроизводимость |
| G2-B | `ClimateAxes` + data-driven `BiomeCatalog` (11 биомов, surface/vegetation/spawn/structure/weather) |
| G2-C | `WorldStreamingBudget`: 1 681 запрошенный чанк против ≈1 398 доступных (объяснение soft overflow), применение в runtime |
| G2-D | `WorldDistanceStrategy`: near/mid/far/offscreen (vegetation, structures, simulation, fog) |
| G2-E | `VegetationRuleSet` + `VegetationPolicy` (где можно) отдельно от `VegetationModelBinding` (что рисуется) |
| G2-F | конвейер структур: candidate → validation → placement → persistent id → состояние → удаление через дельту |
| G2-G | `SettlementSite` (якоря NPC + дорожные stub'ы) и `DungeonEntranceSite` (instance id) |
| G2-H | `WorldDebugOverlay` (F9 / `-cubeDebugOverlay`) + engine-free `WorldDebugSnapshot` |
| G2-I | `WorldPerformanceRecorder` + `WorldMetricsProbe` (`-cubeWorldMetrics <path>`, JSON) |
| G2-J | версия профиля генерации + `generationFingerprint` в `.world`, отказ при смене профиля |
| G3 seam | `EntityDefinition`/`EntityVisualRecipe`/registry — мир не знает о фабрике моделей |

## 2. Проверки

| Проверка | Результат |
|---|---|
| Компиляция | `error CS = 0` |
| G2 EditMode suite | 34 PASS / 0 FAIL / 0 SKIP |
| Полный EditMode (с Alpha-кэшем) | 893 total / 887 passed / 0 failed / 6 skipped |
| Полный EditMode (без кэша) | 893 / 879 passed / 1 известный content-presence fail / 11 skipped |
| G2 PlayMode | 1 PASS / 0 FAIL |
| Полный PlayMode (с кэшем) | 76 total / 76 passed / 0 failed / 0 skipped |
| Windows Player | собран (`BUILD_SUCCEEDED`, errors=0), smoke new + load PASS, метрики записаны |
| Foundation validation | `WorldFoundationValidation.Check()` пуст (покрыто тестом) |

## 3. Что осталось вне этапа

- финальные биомы, растительность, вода, погода, сезоны (G9);
- production города/данжи и их геометрия (G5/G11);
- подключение правил растительности и структур к фактическому runtime-представлению (следующий work package);
- замена Alpha-ассетов и чистка legacy parity-кода.

## 4. Предыдущий этап

[G1 TECHNICAL FOUNDATION](stages/G1_TECHNICAL_FOUNDATION_STAGE.md) — baseline `deepseek/g1-00-runtime-baseline-candidate`
@ `cb76aab` (OPTION C: P0–P7 runtime = основа CubeGame), EditMode 853/859 с Alpha-кэшем, PlayMode 75/75,
Windows Player собран. G1-00 отчёт: [G1_00_RUNTIME_BASELINE_SELECTION_REPORT](reports/G1_00_RUNTIME_BASELINE_SELECTION_REPORT.md).
