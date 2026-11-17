# ACTIVE STAGE — G2-1 CONTENT BINDING

**Canonical branch:** `integration/m0-all` · **G2 baseline:** `claude/g2-world-foundation` @ `d6f84699c5e127ce3823990ebd137b59cf16d09a` (`ACCEPTED`)
**Review branch:** `claude/g2-1-content-binding`
**Статус:** `READY_FOR_REVIEW` — агент не выставляет `ACCEPTED`.

**ТЗ этапа:** [G2_1_CONTENT_BINDING_STAGE.md](stages/G2_1_CONTENT_BINDING_STAGE.md)
**Отчёт:** [G2_1_CONTENT_BINDING_REPORT.md](reports/G2_1_CONTENT_BINDING_REPORT.md)
**Предыдущий этап:** [G2 World Foundation](stages/G2_WORLD_FOUNDATION_STAGE.md) · [отчёт](reports/G2_WORLD_FOUNDATION_REPORT.md)
**Архитектура:** [WORLD_GENERATION_ARCHITECTURE.md](architecture/WORLD_GENERATION_ARCHITECTURE.md)
**Тест-матрица:** [G2_WORLD_TEST_MATRIX.md](planning/G2_WORLD_TEST_MATRIX.md)

---

## 1. Что изменено

Foundation G2 теперь **владеет контентом мира**, Alpha-ассеты остаются визуальными плейсхолдерами:

| Пакет | Результат |
|---|---|
| G2-1A | `CubeGameVegetationProvider`: seed → biome → vegetation profile → rules → `VegetationPolicy` → placements → persistent id → временная Alpha-модель |
| G2-1B | `CubeGameStructureProvider`: `StructurePlacementPipeline` → прототипы (camp/ruin/tower/cave/landmark) → spawn-записи → id → state |
| G2-1C | `CubeGameEntityCatalog` (5 CubeGame-native определений) + `CubeGameVisualBindings` (`TEMPORARY_VISUAL_PLACEHOLDER`) |
| Прототипы | settlement (4 слота, дорога, 9 якорей), dungeon (вход + комната + возврат), generic landmark/camp/ruin/tower |
| Runtime | `CubeGameContentRuntime` + `CubeGameStructurePopulation` (канал 7), фильтр удалённых растений в меше чанка (канал 8) |
| Debug | overlay: biome, vegetation profile/rules, candidates/accepted/removed, структуры id/state, settlement/anchors, dungeon, entity definition + recipe |
| Save | удаления — существующие world deltas; content world id = `cubegame.alpha_terrain/10` |
| Automation | `-cubeSmokeContent fresh|verify` — полный Player-сценарий контента с сохранением и проверкой после перезапуска |

## 2. Проверки

| Проверка | Результат |
|---|---|
| Компиляция | `error CS = 0` |
| G2-1 EditMode | 11 / 11 PASS |
| Полный EditMode (с Alpha-кэшем) | 904 total / 898 passed / 0 failed / 6 skipped |
| G2-1 PlayMode | 1 / 1 PASS |
| Полный PlayMode (с Alpha-кэшем) | [см. отчёт §15](reports/G2_1_CONTENT_BINDING_REPORT.md) |
| Windows Player | собран (`BUILD_SUCCEEDED`, errors=0); content smoke fresh/verify PASS |
| Производительность | mean frame 30.6 → 9.1 мс, вершины −33 %, зоны 1176 → 713 мс (тот же сценарий) |

## 3. Важное следствие для версий мира

- Мир `/10` = тот же террейн, но CubeGame-контент; мир `/9` (G2) намеренно не загружается новым клиентом
  (`GeneratorMismatch`), потому что контентная стадия изменилась.
- Исторические Alpha-content тесты PlayMode продолжают работать на явно запрошенном `/9` мире
  (`StartNewWorld(..., generatorId: ...)` — automation-only параметр).

## 4. Вне этапа

Production city/dungeon/vegetation, разрушение вокселей зданий, фракции, расписания NPC, G3 factory, Alpha cleanup.
Следующий рекомендуемый пакет: **G2-2 World object lifecycle & destruction** (см. §16 отчёта).
