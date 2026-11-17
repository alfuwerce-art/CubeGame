# G2-1 — CONTENT BINDING REPORT

**Этап:** [G2-1 Content Binding](../stages/G2_1_CONTENT_BINDING_STAGE.md)
**Baseline:** `claude/g2-world-foundation` @ `d6f84699c5e127ce3823990ebd137b59cf16d09a` (`ACCEPTED`)
**Ветка:** `claude/g2-1-content-binding` · **Unity:** `6000.3.27f2` · **Статус:** `READY_FOR_REVIEW`

---

## 1. Итог

Мир впервые генерирует и сохраняет **собственный CubeGame content** через foundation-системы G2, а Alpha-ассеты
остаются только визуальными плейсхолдерами:

- растительность решает `VegetationPolicy` (биом → профиль → правила → плотность/склон/вода/spacing/clustering),
  идентичность — `veg:<seed>:<zone>:<index>:<rule>`;
- структуры решает `StructurePlacementPipeline`, геометрия — прототипы, идентичность — `structure:…`;
- settlement/dungeon/landmark прототипы реально существуют в мире и имеют якоря/портал/комнату;
- спавны идут через `EntityDefinitionRegistry` + `EntityVisualRecipe`, тела — временные Alpha-модели;
- удаление объекта = существующая world delta (каналы 7/8), поэтому переживает unload/reload и save/load.

## 2. Растительность (G2-1A)

- `CubeGameVegetationProvider` (`cubegame.vegetation.v1`): сетка кандидатов шаг 3 вокселя (7 225 на зону),
  высота/склон/вода — из heightfield зоны (BFS до воды), влажность/температура — из колонки, biome — из климата
  центра зоны.
- Результат в стартовой зоне (seed 666666): **biome.forest**, `vegetation.rules.forest`, 160 принятых растений из
  7 225 кандидатов; профиль ограничивает количество, правила — места.
- Детерминизм: повторная генерация даёт те же id/координаты/типы; другой seed — другой мир; стабильные индексы
  уникальны внутри зоны; растения не появляются в settlement-резерве, на дороге и у входа в данж.
- Визуал: `VegetationModelBinding` (`TEMPORARY_VISUAL_PLACEHOLDER`), Alpha `.cubdec` не менялись.

## 3. Структуры (G2-1B)

- `CubeGameStructureProvider` (`cubegame.structures.v1`) выполняет pipeline на зону и строит прототипы:
  camp (платформа+столбы+маркер), ruin (кольцо камней), tower (4 колонны), cave entrance (кайрн),
  landmark (платформа), settlement и dungeon — выделенными прототипами.
- `village/city/dungeon_entrance` из generic pipeline не размещаются: этими объектами владеют прототипы.
- Spawn-записи: индекс канала 7, `PersistentObjectId`, `EntityDefinitionId`; settle/portal/creatures имеют
  собственные диапазоны индексов (2048+, 3048+, 4048+).
- Состояние: `generated` → при удалении `ObjectRemoved` в канале 7 → `Destroyed`; после перезагрузки мира
  структура остаётся уничтоженной.

## 4. Settlement prototype

`SettlementSeamPlanner` → `SettlementBlueprint` (одна зона, центр, радиус 24):

- 4 building slot (3×3 площадки с перспективой под G5), persistent id `<settlement>:building:<i>`;
- один дорожный stub (−Z, шаг 2 вокселя, 23 клетки);
- 9 NPC/сервисных якорей (центр, таверна, 2 магазина, кузнец, квесты, стража, 2 жителя) с persistent id
  `seam.anchor:…`; якоря спавнятся как `cubegame.npc.merchant` / `cubegame.npc.humanoid`;
- маркер settlement как статический content-объект (канал 7), поэтому его можно «разрушить» и проверить state.

Smoke (Player, seed 666666): `SETTLEMENT=seam.settlement:666666:512:512 slots=4 anchors=9 biome=biome.forest level=1`.

## 5. Dungeon prototype

- Один вход на блок 2×2 региона (`DungeonSeamPlanner.EntranceZone`, чистая целочисленная формула).
- `DungeonPrototype`: вход, шахта вниз, комната 11×11×5 на Y = вход−8, стены из камня, пол, возврат в ту же точку.
- Runtime contract: `TryFindDungeon` (поиск по блокам, генерируется только зона-победитель) → `EnterDungeon`
  (позиция в комнате, `InsideDungeonInstanceId`) → `ExitDungeon` (та же точка входа).
- Smoke: `DUNGEON=dungeon.instance:666666:256:256 zone=(32778,32831) ENTER=PASS EXIT=PASS room=(8391311…8391321,-24)`.

## 6. Landmark / camp / ruin / tower

Прототипы размещаются тем же pipeline (правила, footprint, flatness, biome, level) и получают persistent id
`structure:<seed>:<zone>:<rule>:<slot>`; удаление/состояние — как у остальных структур. Проверено EditMode
(детерминизм, уникальность spawn-индексов, стабильность между генераторами) и population-тестом (removed spawn
исчезает из зоны).

## 7. Entity registry и visual recipe (G2-1C)

`CubeGameEntityCatalog` (5 определений):

| id | family | behaviour | visual recipe |
|---|---|---|---|
| `cubegame.hero.prototype` | player | `cubegame.hero` | `recipe.hero.placeholder` |
| `cubegame.npc.humanoid` | humanoid | `cubegame.villager` | `recipe.npc.humanoid` |
| `cubegame.npc.hostile` | humanoid | `creature_ai` | `recipe.npc.hostile` |
| `cubegame.creature.wild` | beast | `creature_ai` | `recipe.creature.wild` |
| `cubegame.npc.merchant` | humanoid | `cubegame.service_npc` | `recipe.npc.merchant` |

Все `VisualSourceKind.TemporaryAlpha` + `TEMPORARY_VISUAL_PLACEHOLDER`: runtime не знает про `.cubdec`/ComfyUI/Tripo,
получает только `EntityVisualRecipe` + `RigAnimationPackage`. Спавн существа/якоря резолвит definition и логирует
recipe один раз, population использует `EntityDefinitionId` и `Disposition`, а не список legacy behaviour keys.

## 8. Persistence

- Растительность: `WorldDeltaKind.ObjectRemoved` в канале **8** по `StableIndex` (меш чанка фильтрует удалённые).
- Структуры/якоря/портал: тот же вид дельты в канале **7** по `SpawnIndex`.
- Player smoke: fresh → destroy portal + remove plant → save → перезапуск → verify:
  `VERIFY removed_structures=1 removed_plants=1 structures=34 plants=1824 result=PASS`.
- Discovery/«загружено» вычисляется из активных зон (streaming), а не хранится отдельно.

## 9. Streaming

PlayMode сценарий: content world → vegetation/settlement → dungeon enter/exit → destroy structure+plant →
уйти на 6 зон (unload) → вернуться: та же зона, тот же первый plant id, удаления сохранились.
Player smoke дополнительно проверяет это через save/load.

## 10. Debug overlay

Добавлены строки `content zone(...) biome=… vegProfile=… rules=… candidates=… accepted=… removed=…`,
`structures=… rejected=… spawns=…`, пример vegetation id, пример structure id/state, `settlement … slots/anchors/roads`,
`anchor <entityDefinition> recipe=<recipeId> <persistentId>`, `dungeon <instance> entrance=(…) lvl=… inside=…`,
плюс проблемы контентной стадии.

## 11. Производительность: G2 → G2-1

Один и тот же Player-сценарий (`-cubeAutoNew … -cubeTeleport 400 0 -cubeSmokeSeconds 25 -cubeWorldMetrics`):

| Показатель | G2 (`/9`, Alpha content) | G2-1 (`/10`, CubeGame content) | Δ |
|---|---|---|---|
| чанков загружено | 1 127 | 1 419 | +292 |
| вершин | 12 592 756 | 8 465 952 | −33 % |
| растений | 24 765 | 3 363 | −86 % (профили биомов реже Alpha) |
| NPC | 1 | 93 | +92 (spawn profile) |
| mean frame | 30.6 мс | 9.1 мс | −70 % |
| p95 frame | 24.2 мс | 9.0 мс | −63 % |
| max frame | 15 531 мс | 59.3 мс | стартовая загрузка 8.5 мс/чанк |
| chunk rate | 47.0/с | 58.9/с | +25 % |
| zone mean | 1 175.8 мс | 713.1 мс | −39 % |
| chunk mean / max | 3.699 / 58.4 мс | 1.732 / 54.3 мс | −53 % mean |
| GC | 495.8 МБ | 474.1 МБ | −4 % |
| working set | 429.2 МБ | 491.4 МБ | +14 % |

Неконтролируемой регрессии нет; рабочий набор выше из-за 93 NPC и контентных записей зон (структуры/растения).
Причина меньшего числа растений — плотность профилей биомов (forest 160/зону), а не отключение системы.

## 12. Тесты

| Прогон | Результат |
|---|---|
| G2-1 EditMode (11 тестов) | **11 / 11 PASS** |
| Полный EditMode (с Alpha-кэшем) | **904 total / 898 passed / 0 failed / 6 skipped**, `error CS = 0` |
| G2-1 PlayMode | **1 / 1 PASS** |
| Полный PlayMode (с Alpha-кэшем) | **77 total / 77 passed / 0 failed / 0 skipped** |
| Windows Player | собран: `BUILD_SUCCEEDED`, `errors=0` (92.82 МБ output, контент упакован) |
| Player content smoke fresh | `VEGETATION=160`, `SETTLEMENT slots=4 anchors=9`, `DUNGEON ENTER/EXIT=PASS`, `DESTROY=PASS`, `VEG_REMOVE=PASS`, `save=PASS` |
| Player content smoke verify (после перезапуска) | `removed_structures=1 removed_plants=1 result=PASS` |

## 13. Изменения в существующем коде (минимальные)

| Файл | Изменение |
|---|---|
| `Features/World/AlphaTerrainGenerator.cs` | id `/10`, `UsesCubeGameContent`, `TryGetGeneratedZone`, счётчики |
| `Features/World/AlphaZoneGenerator.cs` | для `/10` Alpha tree/ground vegetation не запускаются (решение принимает CubeGame provider) |
| `Features/World/VoxelColumn.cs` (`AlphaZone`) | поля CubeGame content: Vegetation/Structures/StructureSpawns/Settlement/Dungeon/BiomeId/статистика |
| `Features/World/AlphaGroundVegetation.cs` | `AlphaPlantPlacement.StableIndex` (удаление растения) |
| `Features/World/Foundation/StructureFoundation.cs` | `StructurePlacement.SpawnIndex` |
| `Features/Contracts/WorldContracts.cs` | `SpawnDefinition.EntityDefinitionId` / `PersistentObjectId` |
| `Presentation/Gameplay/World/D3ZonePopulation.cs` | для `/10` собирает CubeGame providers |
| `Presentation/Gameplay/GameBootstrap*.cs` | generator id `/10`, entity registry, content runtime/population, overlay content lines, smoke modes |
| `Presentation/Gameplay/World/StreamingWorldView.cs` | фильтр удалённых растений, счётчики контента зоны |
| `Presentation/Gameplay/World/AlphaVegetationMeshes.cs` | фильтр удалённых растений при сборке меша |
| `Presentation/Gameplay/World/CubeGameContentRuntime.cs` | новый runtime-binding контента |
| `Presentation/Gameplay/GameBootstrap.Smoke.cs` | `-cubeSmokeContent fresh/verify`, `TeleportHeroToPosition` |

## 14. Известные ограничения

1. Прототипы — платформы/столбы/кайрны; город/данж/растительность как production-контент — G5/G9/G11.
2. Разрушение структуры меняет **состояние и population**, но не вырезает воксели её прототипа (нужен отдельный
   вид дельты «строение разрушено» — осознанно вне G2-1).
3. Dungeon — одна тестовая комната; instance не персистится как отдельный регион (сессия enter/exit в runtime).
4. Старые миры `/9` намеренно не загружаются (контентная стадия изменилась).
5. Метрика `structures` в JSON метрик пока не заполняется (overlay и тесты показывают структуры напрямую).
6. NPC settlement-якорей используют существующий creature-путь как actor (без расписаний/торговли — G5/G6).

## 15. Итоговый прогон PlayMode

Заполняется после полного прогона.

## 16. Следующий рекомендуемый work package

**G2-2 — World object lifecycle & destruction**: новый вид дельты/операции для разрушаемых структур (воксели
здания исчезают, освобождается footprint), уборка vegetation по площади, «discovered/modified» состояние,
интеграция с `WorldDeltaStore` лимитами и тестами на длинную сессию. Затем — G3 entity factory на этом контракте.
