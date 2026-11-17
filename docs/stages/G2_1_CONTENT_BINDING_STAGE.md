# G2-1 — CONTENT BINDING (ТЗ и итог этапа)

**Этап:** G2-1 дорожной карты, продолжение принятого [G2 World Foundation](G2_WORLD_FOUNDATION_STAGE.md)
**Baseline:** `claude/g2-world-foundation` @ `d6f84699c5e127ce3823990ebd137b59cf16d09a` (`G2 = ACCEPTED`)
**Ветка:** `claude/g2-1-content-binding`
**Unity:** `6000.3.27f2`
**Статус:** `READY_FOR_REVIEW`

---

## 1. Цель

Сделать новые системы G2 **владельцами размещения и runtime-состояния** мира, оставив Alpha-ассеты только
временными визуальными плейсхолдерами:

`WORLD LOGIC = CubeGame` · `TEMPORARY VISUAL CONTENT = Alpha placeholders`

G2-1 не переделывает принятый foundation и не занимается Alpha parity.

---

## 2. Что сделано

| Пакет | Результат |
|---|---|
| G2-1A | `CubeGameVegetationProvider`: seed → biome → vegetation profile → rule set → `VegetationPolicy` → placements → persistent id → временная модель |
| G2-1B | `CubeGameStructureProvider`: `StructurePlacementPipeline` → прототип геометрии → spawn-запись → persistent id → состояние |
| G2-1C | `CubeGameEntityCatalog` (5 CubeGame-native определений) + `CubeGameVisualBindings` (`TEMPORARY_VISUAL_PLACEHOLDER`) |
| Прототипы | settlement (зона/центр/слоты/дорога/якоря), dungeon (вход + тестовая комната + возврат), landmark/camp/ruin/tower (generic pipeline) |
| Runtime | `CubeGameContentRuntime`: структуры/состояние, удаление растительности, dungeon session, факты для overlay |
| Population | `CubeGameStructurePopulation` на отдельном канале 7 (структуры, якоря, портал, существа по spawn profile) |
| Streaming | vegetation removal учитывается при построении меша чанка (канал 8) |
| Debug | overlay показывает biome, vegetation profile/rule set, candidates/accepted/removed, структуры c id/state, settlement/anchors, dungeon, entity definition + recipe |
| Save | удаления — существующие world deltas (каналы 7/8); версия генерации `/10` |
| Smoke | `-cubeSmokeContent fresh|verify`: vegetation → settlement anchors → dungeon enter/exit → destroy → save → перезапуск → verify |

## 3. Мир и версия

- Новый content world id: `cubegame.alpha_terrain/10` (`WorldRuntimeId` `/9` остаётся историческим).
- `/10` = тот же террейн/фичи, но Alpha vegetation/tree/building content **не** генерируется.
- Старые миры `/9` **явно отклоняются** (GeneratorMismatch): контентная стадия изменилась, тихая регенерация
  «того же мира» с другим содержимым запрещена (правило G2-J).

## 4. Определение готовности (PASS)

- `VegetationPolicy` реально управляет placement − да (кандидаты/принятые/детерминизм/удаление);
- `StructurePlacementPipeline` реально управляет structures − да (pipeline → прототип → spawn → state);
- settlement / dungeon / landmark prototypes работают − да;
- persistent IDs работают − да (стабильны между экземплярами генератора, переживают unload/reload и save/load);
- save/load state работает − да (Player smoke verify);
- streaming unload/reload работает − да (PlayMode + Player smoke);
- `EntityDefinitionRegistry` используется runtime − да (спавн NPC/существ, лог recipe, population);
- visual recipe contract используется − да (`CubeGameVisualBindings`, `EntityVisualRecipe`);
- Alpha-ассеты только плейсхолдеры − да (`TEMPORARY_VISUAL_PLACEHOLDER`);
- debug overlay показывает новые системы − да;
- EditMode/PlayMode/Windows Player/smoke − см. [отчёт](../reports/G2_1_CONTENT_BINDING_REPORT.md).

## 5. Вне этапа

Production city generator, финальные здания, quest content, фракции, расписания NPC, финальные данжи и
растительность, финальные модели, сезоны, полный G3, чистка Alpha-кода.
