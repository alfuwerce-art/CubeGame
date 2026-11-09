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

## 3. Что нельзя утверждать

- W-18 (климат→объекты) не выгружена: растительность и биомный спавн по-прежнему `UNKNOWN`; не подставляйте случайные объекты как «Alpha».
- Правила размещения W-16 не восстановлены: политика (равнина/уровень/расстояния) — ваша, документируйте как `CUBEGAME_CONTRACT`.
- Сервисы торговцев: рабочие места известны, но ассортимент/наценки (`F-U5`) — `UNKNOWN`.
- Категории объектов — CubeGame-контракт по именам, не поле Alpha.

## 4. Проверки

`AlphaOutput/editmode.xml` — 337/337 (+2 теста мира/структур); `AlphaOutput/playmode.xml` — 26/26. Таблицы воспроизводимы: `python -m tools.alpha_content.build_world_evidence --check` и `python -m tools.alpha_content.build_content_tables --check`.
