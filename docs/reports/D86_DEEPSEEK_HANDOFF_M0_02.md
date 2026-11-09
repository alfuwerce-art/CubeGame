# D86 — handoff DeepSeek → Claude: мир и контент (M0-02/M0-07)

**Дата:** 2026-11-09. **Ветка:** `deepseek/d85-visual-unity`. **Отчёт:** [M0_02_07_DEEPSEEK_WORLD_CONTENT_REPORT](M0_02_07_DEEPSEEK_WORLD_CONTENT_REPORT.md). **Статус:** `READY_FOR_REVIEW`.

## 1. Что доступно генератору мира (только чтение)

```csharp
AlphaWorldContent.Objects / ItemNames / Skills        // 67 / 133 / 27 из World::ctor_1
AlphaWorldContent.TryObject(id, out def);             // имя объекта (Door, Chest, MarketStand1…)
AlphaWorldContent.TryItemName(type, subtype, out def);// LifePotion (1,1), HangGlider (0x17,0), Boat (0x17,1)
AlphaWorldContent.TrySkill(id, out def);              // SkillClimbing (2), SkillHangGliding (3)…
AlphaWorldContent.CategoryOf(id);                     // StructurePart/Furniture/Container/Trap/Service/Decor
AlphaWorldContent.ServiceObjects();                   // рабочие места торговцев/крафта (8+)
```

## 2. Постройки и размещение

```csharp
AlphaStructureTemplates.TryGet(AlphaStructureTemplates.HouseId, out var house);  // 3x3x4, styles 1..5
AlphaStructureTemplates.TryGet(AlphaStructureTemplates.DungeonRoomId, out var room); // 3x3x3, step 7

var placements = AlphaStructurePlacementPlanner.Plan(
    worldSeed, regionX, regionZ, cellX, cellZ,
    templateIds: new[] { AlphaStructureTemplates.HouseId },
    isFootprintFree: (x, z, w, d) => myHeightField.IsFlatAndFree(x, z, w, d),
    problems: myProblems);
foreach (var p in placements) InstantiateHouse(p.StableId, p.TemplateId, p.X, p.Z, p.Rotation);
```

- Планировщик **не решает**, где ставить дом: он даёт детерминированных кандидатов и просит предикат мира. Нет свободного футпринта — `STRUCTURE_NO_FREE_FOOTPRINT`, постройка не форсируется.
- Стабильный id: `structure:<region>:<cell>:<template>:<slot>` — годится для персистентности (сохраняйте в своём `WorldDeltaStore`).
- Поворот 0…3, размеры берутся из шаблона; интерьер/расстановка мебели — **не** реализованы (`UNKNOWN`).

### Данжи (M0-07B, PARTIAL)

```csharp
var layout = AlphaDungeonLayoutPlanner.Plan(worldSeed, roomCount: 6, originX, originZ, problems);
// layout.Rooms (3x3x3, шаг 7), layout.Corridors (L-образные клетки), layout.Status = PARTIAL
AlphaDungeonRules.ChestItemCount;   // 10 — сундук данжа по evidence
AlphaDungeonRules.EntranceNames;    // overground-dungeon01 / underground-dungeon01
```
Порядок вырезания коридоров, содержимое комнат, свет/факелы и таблицы встреч — `UNKNOWN`; планировщик даёт связный скелет, а не «Alpha-данж».

### Климат → объекты (W-18, TRANSLATED_FROM_CODE)

```csharp
// humidity = GameController_sampleHumidityGrid, temperature = sampleTemperatureGrid,
// cell = World_getRegionCellPtr, spacing = World_placeObjectWithSpacing, level = уровень, mode = (char)param_4
var selection = AlphaScatterSelector.Select(humidity, temperature, cell, spacing, level, mode);
PlaceObjects(selection.Rule, selection.ObjectKinds);   // status: TRANSLATED_FROM_CODE, не VERIFIED_ALPHA
```
Внутри — механический транслят `WorldInfo_scatterObjectsInArea @ 0x005f56c0` (`AlphaScatterTree`, 77 id, goto сохранён). Подземная (`level<0`) и сухая (`cell<=0.1`) ветки совпали с ранее доказанными списками; живого сравнения с Alpha ещё не было, поэтому не выдавайте результат за проверенный паритет. Сырые 6 групп — `AlphaScatterEvidence`.

```csharp
// Готовое размещение растительности в клетке (W-18 + детерминированные позиции):
var placements = AlphaVegetationPlanner.Plan(seed, regionX, regionZ, cellX, cellZ,
    humidity, temperature, cellValue, spacing, level, mode,
    stagedKinds, problems);   // ставит только подготовленные виды; иначе SCATTER_KIND_NOT_STAGED
// Альтернативно: точки сундуков данжа (правило «10 предметов» доказано, позиции — CUBEGAME_CONTRACT)
var chests = AlphaDungeonChests.Plan(layout, chestsPerDungeon, seed, regionX, regionZ, problems);
// chests[i].StableId / RoomIndex / X / Y / Z
```

Если климат у вас уже в сетках (humidity/temperature/region cell), не считайте его заново:

```csharp
var field = new AlphaScatterField(myClimateGrid, spacing);   // IAlphaClimateGrid: Humidity/Temperature/RegionCell
var selection = field.SelectAt(x, z, level);                  // → AlphaScatterSelection (TRANSLATED_FROM_CODE)
var placements = field.PlaceCell(seed, regionX, regionZ, cellX, cellZ, level, stagedKinds, problems);
// AlphaScatterField(null, ...) бросает ArgumentNullException: отсутствие климата — ошибка, не «по умолчанию»
```

## 3. Сервисы и взаимодействия (M0-07C, данные)

```csharp
AlphaNpcServices.TryServiceFor(objectId, out var service);   // MarketStand1 → Trade, Anvil → Craft, Furnace → Smelt, BedTable → Rest, CustomizationBench → Customize
AlphaNpcServices.All();                                      // все сервисные объекты с категорией
AlphaNpcServices.Status;                                     // явно: категория — CUBEGAME_CONTRACT, торговые правила/цены UNKNOWN (F-U5)
```
Категория выведена из имён объектов Alpha (сами имена — доказательство), правила торговли/крафта не выдумываются.

## 4. Что нельзя утверждать

- W-18 (климат→объекты) не выгружена: растительность и биомный спавн по-прежнему `UNKNOWN`; не подставляйте случайные объекты как «Alpha».
- Правила размещения W-16 не восстановлены: политика (равнина/уровень/расстояния) — ваша, документируйте как `CUBEGAME_CONTRACT`.
- Сервисы торговцев: рабочие места известны, но ассортимент/наценки (`F-U5`) — `UNKNOWN`.
- Категории объектов — CubeGame-контракт по именам, не поле Alpha.

## 5. Проверки

`AlphaOutput/editmode.xml` — 345/345 (+тест сервисов и трансляции W-18); `AlphaOutput/playmode.xml` — 36/36 (включая сквозной контент-сценарий `WorldContentPlayModeTests`). Таблицы воспроизводимы: `python -m tools.alpha_content.build_world_evidence --check`, `build_content_tables --check`, `build_staged_visuals --check`, `scatter_tree_translate2 --check`.
