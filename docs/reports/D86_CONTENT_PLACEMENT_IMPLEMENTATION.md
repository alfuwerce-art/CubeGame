# D86 — DeepSeek #13: реальное размещение игрового контента в мире Unity (M0-02/M0-06/M0-07)

**Дата:** 2026-11-09. **Исполнитель:** DeepSeek Harness. **Ветка:** `deepseek/d85-visual-unity`.
**Commit:** `86ef3756e48f59bea78f46e9b0c8af64c294578e` (`3883c68` — основная реализация, `86ef375` — ограничение диагностики и финальные прогоны).
**Статус:** `READY_FOR_REVIEW` (не ACCEPTED, приёмка — за продюсером). **Issue:** #13.
**Связанные отчёты:** [W-18: принадлежность адреса 0x005EFFA0](D86_W18_ADDRESS_VERIFICATION.md),
предыдущий [M0-02/07 отчёт](M0_02_07_DEEPSEEK_WORLD_CONTENT_REPORT.md).

## 1. Что появилось (кратко)

Рабочий механизм, который получает от мира seed/зону/климат/высоты и **создаёт настоящие объекты Unity**:
растительность и объекты окружения, постройки, существа/NPC — с реальными моделями/анимациями/AI из
существующего реестра, стабильными идентификаторами, выгрузкой/загрузкой без дубликатов, учётом
удалённых объектов и состояния NPC, и с корректной работой при смещении floating origin.

| Файл | Что это |
|---|---|
| `Assets/CubeGame/Content/AlphaWorldScale.cs` | единый масштаб Alpha: зона 256, регион 16 384 вокселя, мир 2²⁴, клетка 2 048; типизированные ключи `WorldRegionKey/WorldZoneKey/WorldCellKey`, `WorldVoxelPoint` (double) |
| `Assets/CubeGame/Content/WorldContentContracts.cs` | контракты содержимого: `IContentZoneSource` (вход от мира), `ZoneClimate`, `ContentPlacement`, `ContentPlan` |
| `Assets/CubeGame/Content/DelegateContentZoneSource.cs` | рабочий адаптер мира на делегатах (шов для Claude, проверен тестами) |
| `Assets/CubeGame/Features/Creatures/ZoneContentPlanner.cs` | планировщик зоны: W-18 растительность + постройки + существа, детерминированно, с высотами мира |
| `Assets/CubeGame/Features/Creatures/WorldContentStreamer.cs` | runtime: загрузка/выгрузка зоны, стабильные id, состояние NPC, floating origin, `MarkRemoved` |
| `Assets/CubeGame/Content/AlphaVegetationPlanner.cs` | + `PlanZone` (позиции внутри зоны), исправлен масштаб клетки/региона |
| `Assets/CubeGame/Content/AlphaStructures.cs` | + `PlanZone`, исправлен масштаб региона/клетки |
| `Assets/CubeGame/Content/AlphaClimateField.cs` | исправлен масштаб клетки в `PlaceCell` |
| `Assets/CubeGame/Features/Creatures/CreatureSpawner.cs` | + `PlanZone`, `SpawnPlan`, `Despawn(id)`, `SharedStore`, floating origin |
| `Assets/CubeGame/Features/Creatures/CreatureAgent.cs` | абсолютные координаты в `double`, локальные в `Transform`; `SetWorldOrigin` |
| `Assets/CubeGame/Features/Creatures/CreatureContracts.cs` | + `SpawnTable.ZoneSpawnId` |
| `Assets/CubeGame/Tests/Creatures/EditMode/ContentPlacementEditModeTests.cs` | 13 EditMode-тестов |
| `Assets/CubeGame/Tests/World/PlayMode/WorldContentStreamerPlayModeTests.cs` | 4 PlayMode-теста (реальные GameObject) |

Существующие `CreatureSpawner`, `CreatureAgent`, `AlphaActorView`, `AlphaActorLibrary`, таблицы контента
**переиспользованы**, а не переписаны; визуальные модели по-прежнему не управляют боем, экономикой и сейвами.

## 2. Единая система координат (критическая часть)

**Было.** `AlphaContentTables.RegionBlocks = 64` читалось как «размер региона в вокселях», а
`RegionCells = 8` — как «клетка 8 вокселей». В `AlphaVegetationPlanner`, `AlphaClimateField`,
`AlphaStructures.Plan`, `CreatureSpawnPlanner.PlanSlots` координаты считались как
`regionX * 64 + cellX * 8 + local`: объект, который должен стоять в 16 384-воксельном регионе, попадал
почти в начало мира — ошибка масштаба ×256 (и, как следствие, неверная зона для каждого объекта).

**Стало.** Число 64 — это **количество зон в регионе**, а не воксели. Семантика разведена:

| Величина | Значение | Доказательство |
|---|---|---|
| Зона | 256×256 вокселей | исполненный `WorldInfo_generateBiomeContent` (M0-02b) |
| Зон в регионе | 64 | `0x4000 / 0x100`; `RegionBlocks` = 64 |
| Регион | 16 384 вокселя | `voxel >> 14` в функциях рельефа |
| Клетка (landmark cell) | 8 зон = 2 048 вокселей | `voxel >> 11`, `RegionCells = 8` |
| Мир | 1024 региона = 65 536 зон = 2²⁴ = 16 777 216 вокселей | `WorldBlocks = 65 536` |
| Старт Alpha | зона (32800, 32800) → регион (512,512) → воксель 8 396 928 | OQ-22 PARTIAL, сохранённая позиция героя |

Все четыре планировщика переведены на `AlphaWorldScale`; в `AlphaWorldScale.Validate()` есть проверки
констант **и** сверка с сгенерированной таблицей (если таблица снова начнёт читаться как воксели — тест упадёт).

Координаты хранятся как `double` воксели (`WorldVoxelPoint`), в Unity `Vector3` получается только
**относительно локального начала**: `LocalX = (float)(X - originX)`. `CreatureState` уже хранил `double X/Y/Z` —
теперь и `Transform` больше не получает абсолютные 8.4 млн (это был реальный дефект: float на 8.4·10⁶ имеет
шаг ≈0.5 вокселя).

## 3. Runtime: как мир наполняется объектами

```
IContentZoneSource (seed, ClimateOf(cell), TrySurfaceVoxel(x,z), IsFlatAndFree(x,z,w,d), IsRemoved(id))
        │
        ▼
ZoneContentPlanner.Plan(zone, source, settings)  ── чистая функция, без Unity ──► ContentPlan
   1) растительность/объекты: W-18 выбор + зональные позиции (AlphaVegetationPlanner.PlanZone)
   2) декор: только если вызывающий явно дал набор и W-18 ничего не выбрал (помечено NOT_ALPHA)
   3) существа/NPC: CreatureSpawnPlanner.PlanZone (существующие правила, стабильные id)
   4) постройки: AlphaStructurePlacementPlanner.PlanZone + предикат мира «площадка свободна/ровная»
   каждая позиция получает Y = верхний твёрдый воксель + 1 (или помечается CONTENT_NO_SURFACE и не ставится)
        │
        ▼
WorldContentStreamer.LoadZone(zone)
   ├─ растительность/существа → CreatureSpawner.SpawnOne (AlphaActorView, анимации, существующий AI, state store)
   ├─ постройки → GameObject с ContentStructureView (габарит из шаблона W-17, меш — явный placeholder)
   └─ запоминает id зоны; повторный LoadZone → false, дубликатов нет
WorldContentStreamer.UnloadZone(zone)
   ├─ существа → CreatureSpawner.Despawn(id) (состояние сохраняется в CreatureStateStore)
   ├─ постройки → Destroy
   └─ записи зоны удаляются; план при повторной загрузке тот же (детерминизм)
WorldContentStreamer.MarkRemoved(id)  → объект исчезает и больше не планируется (Removed + Source.IsRemoved)
WorldContentStreamer.SetWorldOrigin(x,z) → все Transform = абсолютная позиция − origin
```

Что это даёт по пунктам задания §3:

1. растительность рассчитывается (`PlanZone` + W-18); 2. постройки и объекты окружения — тоже;
3. существа/NPC создаются из существующего реестра (`CreatureSpawnPlanner`/`CreatureSpawner`);
4. к ним подключаются модели/анимации/AI через `AlphaActorView` + `ICreatureAi` (инъекция игры);
5. идентификаторы стабильны (`spawnz:<zone>:<zone>:<slot>`, `scatter:z:…`, `structure:z:…`);
6. при выгрузке ресурсы освобождаются, состояние уходит в `CreatureStateStore` (игровой слой его сериализует);
7. повторная загрузка не создаёт дубликатов и уважает удалённое.

## 4. Публичный API для Claude (интеграция)

Ничего из файлов Claude не изменено и не требуется изменять: контракт — только чтение мира.

```csharp
// Features/Creatures
public sealed class WorldContentStreamer : MonoBehaviour
{
    public uint Seed;                                  // seed мира
    public IContentZoneSource Source;                  // адаптер мира (ниже)
    public AlphaActorLibrary ActorLibrary;             // AlphaActorLibrary.Shared по умолчанию
    public Func<ICreatureAi> AiFactory;                // фабрика AI игры
    public ContentPlanSettings Settings;               // null → ContentPlanSettings.FromLibrary(ActorLibrary)

    public bool LoadZone(WorldZoneKey zone);           // false, если зона уже загружена
    public bool UnloadZone(WorldZoneKey zone);
    public int  LoadRing(WorldZoneKey centre, int radius);
    public int  UnloadOutside(WorldZoneKey centre, int radius);
    public void SetWorldOrigin(double originX, double originZ);
    public void Tick(float deltaSeconds);              // тикает только существ
    public bool MarkRemoved(string stableId);          // убито/подобрано/использовано
    public ContentPlan PlanFor(WorldZoneKey zone);     // план без создания объектов
    public bool IsLoaded(WorldZoneKey zone);
    public bool IsSpawned(string stableId);
    public CreatureStateStore NpcStates { get; }       // состояние выгруженных NPC для сейва игры
    public HashSet<string> Removed { get; }            // удалённый контент (для дельт мира)
    public int LoadedZoneCount, LoadedObjectCount;
    public List<string> Problems;                      // причины, а не «тихие» подстановки
}

// Content
public interface IContentZoneSource
{
    uint Seed { get; }
    ZoneClimate ClimateOf(WorldCellKey cell);          // Humidity/Temperature/CellValue/Spacing/Level/Type
    bool TrySurfaceVoxel(int voxelX, int voxelZ, out int topSolidY);  // верхний твёрдый воксель
    bool IsFlatAndFree(int voxelX, int voxelZ, int width, int depth); // площадка под постройку
    bool IsRemoved(string stableId);                   // сохранённые удаления/смерти
}
```

Готовый адаптер к уже существующему у Claude `WorldState`/`IWorldGenerator` (это **весь** код интеграции):

```csharp
var source = new DelegateContentZoneSource {
    Seed          = session.Seed,
    Climate       = cell => ClimateOfCell(world.Region(cell).Climate),      // RegionInfo.Climate
    Surface       = (x, z) => world.SurfaceAt(x, z),                       // верхний твёрдый воксель
    FootprintFree = (x, z, w, d) => world.IsFlatAndFree(x, z, w, d),       // ровно + свободно
    Removed       = id => worldDeltas.IsRemoved(id),                       // ObjectRemoved/EntityDied
};
var content = gameObject.AddComponent<WorldContentStreamer>();
content.Source = source;
content.Seed = session.Seed;
content.AiFactory = () => new CreatureAi();
// по событиям стриминга мира:
content.SetWorldOrigin(world.OriginX, world.OriginZ);
content.LoadRing(world.HeroZone, radiusZones: 2);
content.UnloadOutside(world.HeroZone, radiusZones: 3);
```

Ограничение (важное): `Surface`/`Climate` вызываются на потоке **планирования** (главный поток в текущей
реализации); зональный план — лёгкий (десятки объектов), но решение «считать в фоне» остаётся за интегратором.

## 5. W-18: проверка адреса `0x005EFFA0`

Подробно — [D86_W18_ADDRESS_VERIFICATION](D86_W18_ADDRESS_VERIFICATION.md). Итог:

* `0x005EFFA0` — **одна и та же функция** для обеих сторон, но это **маска рельефа** (perlin + smoothstep +
  поправки по типу клетки), а не алгоритм размещения объектов;
* вызовы: рельеф (`WorldInfo.cpp:1655`, `:2519`) и W-18 (`WorldInfo.cpp:7262`, где значение используется
  только в сравнении `0.3f < fVar6` → `WorldInfo.cpp:7311`);
* у Claude та же функция исполнена как маска гор (300 точек, `2.1·10⁻⁷`) — это доказательство **исполнением**;
* поэтому `fVar6` в W-18 — значение маски («на горном рельефе добавляется объект `0x2f`»), а не шаг между
  объектами. Порт самой политики размещения (плотность, отталкивание, `rotateAndPlace`) остаётся `UNKNOWN`;
* код и документы DeepSeek исправлены: `spacing` не называют «непортированным алгоритмом размещения», позиции
  помечены `CUBEGAME_CONTRACT`; шов `AlphaScatterField(grid, (x,z) => mask)` готов принять исполненную маску.

## 6. Проверки (реальные прогоны)

Unity 6000.3.27f2, `D:\Projects\CubeGame-deepseek`:

```powershell
& 'D:\Unity\Hub\Editor\6000.3.27f2\Editor\Unity.exe' -batchmode -projectPath 'D:\Projects\CubeGame-deepseek' `
  -runTests -testPlatform EditMode `
  -testResults 'D:\Projects\CubeGame-deepseek\AlphaOutput\editmode_content.xml' `
  -logFile 'D:\Projects\CubeGame-deepseek\AlphaOutput\editmode_content.log'

& 'D:\Unity\Hub\Editor\6000.3.27f2\Editor\Unity.exe' -batchmode -projectPath 'D:\Projects\CubeGame-deepseek' `
  -runTests -testPlatform PlayMode `
  -testResults 'D:\Projects\CubeGame-deepseek\AlphaOutput\playmode_content.xml' `
  -logFile 'D:\Projects\CubeGame-deepseek\AlphaOutput\playmode_content.log'
```

Результат (по XML, не по exit code; **без** флага `-quit` — с ним Unity завершается до прогона тестов):

| Прогон | Было | Стало | Итог |
|---|---|---|---|
| EditMode | 356/356 | **369/369**, failed=0 | PASS (13 новых тестов контента) |
| PlayMode | 46/46 | **50/50**, failed=0 | PASS (4 новых сценария) |

Что именно доказывают новые тесты:

| Проверка задания §5 | Тест |
|---|---|
| Корректные мировые координаты у стартовой зоны | `StartZoneSitsInTheCentreRegion`, `WorldScaleMatchesExecutedAlphaLayout` |
| Точность и локальные координаты | `AbsolutePositionsUseDoubleAndStayRelativeToTheOrigin` |
| Размещение на реальной высоте поверхности | `PlacementsStandOnTheReportedSurface`, `ZoneLoadsRealObjectsOnTheReportedSurface` |
| Постройка на подходящем участке | `StructureFootprintAsksTheWorld` (+ отсутствие на непригодном) |
| Повторное создание зоны без изменения состава/id | `ZonePlanIsDeterministicAndUnique`, `NeighbouringZonesDoNotShareIds`, повторный `LoadZone → false` |
| Выгрузка/загрузка с удалёнными и состоянием NPC | `RemovedObjectsAreNotPlannedAgain`, `UnloadKeepsStateAndReloadRestoresTheSameIdentities` |
| Отсутствие дубликатов | `ZoneLoadsRealObjectsOnTheReportedSurface` (второй `LoadZone`), `WorldContentStreamer` (`Spawned`/`Removed`) |
| Стабильность памяти при многократной загрузке/выгрузке | `RepeatedLoadUnloadKeepsTheSceneStable` (10 циклов: 0 объектов, 0 построек, store и диагностика не растут — список `Problems` ограничен `MaxProblems = 512`) |
| Работа при смещении floating origin | `FloatingOriginKeepsTransformsLocalAndWorldDataAbsolute` (±1 000 000 вокселей, абсолютные `double` не меняются) |

Не подтверждено этими тестами: интеграция с живым `GameSession`/`WorldState` Claude (его ветка содержит код,
которого нет в этой ветке) — см. §8; живая сверка W-18 с запущенной Alpha; визуальная приёмка моделей.

## 7. Оставшиеся несовместимости и ограничения

1. **Типы Claude недоступны в этой ветке**: `Features/World`, `Features/Contracts` (`SpawnDefinition`,
   `StableEntityId`, `RegionKey`), `M0_02_WORLD_REPORT.md` существуют только в `claude/d85-gameplay-unity`.
   Поэтому контракт DeepSeek выражен собственными типами (`WorldZoneKey`, `ContentPlacement`, …) + адаптер на
   делегатах; **несовместимого дубликата** `IZoneContentProvider` не создано.
2. **`IZoneContentProvider` Claude** (зона-контент на этапе генерации, воркер-поток, запись вокселей + `SpawnDefinition`)
   не реализован этой веткой: это второй шов (генерация), а сданный механизм — runtime-размещение. Для полной
   картины требуется решение: W-18-растительность можно отдать либо в `IZoneContentProvider` (детерминированно
   вместе с зоной), либо в runtime (как сейчас). Обе части используют одни и те же планы — конфликта id нет,
   но выбрать владельца должен интегратор (предложение: генерация — воксельные объекты/декор, runtime — существа
   и постройки с состоянием).
3. **Единицы шаблонов построек**: `3×3×4` из W-17 трактуются как воксели гaбарита; интерьер/части/поворот
   (`Rotation 0..3`) не восстановлены — меш `PLACEHOLDER_NOT_ALPHA`.
4. **Плотность/политика размещения** — `CUBEGAME_CONTRACT` (6 объектов на зону, 4 слота существ, 4 декора,
   70% занятости слотов). Числа — контракт CubeGame, не Alpha.
5. **`spawning` существ** по-прежнему без 30-строчной таблицы уровней Alpha (N-19): уровень/звёзды — существующие
   документированные правила `CreatureSpawnPlanner`.
6. **Мир (terrain) PARTIAL** у Claude (ориентиры/пещеры/вода) — контент это компенсирует корректно (нет высоты →
   нет объекта), но наполнение будет соответствовать текущему рельефу, а не финальному.
7. **Сейв**: `NpcStates`/`Removed` — это `CreatureStateStore`-блоб и множество id; запись в общую схему сейвов
   НЕ добавлена (запрещено без решения продюсера) — интегратор должен сохранить их своим слоем.
8. **Производительность/память**: план зоны ~десятки объектов, тесты быстрые; массовый прогон (сотни зон) не
   профилировался. «Стабильность памяти» доказана по счётчикам объектов/состояний/диагностики, не профайлером Unity.

## 8. Handoff Claude (что сделать при слиянии)

1. Взять файлы из §1 (только `Content/**`, `Features/Creatures/**`, `Tests/…`) — конфликтов с `Features/World`
   нет, `Pilot.unity`/`GameBootstrap`/`Features/Session` не тронуты.
2. Собрать `DelegateContentZoneSource` из `WorldState`/`IWorldGenerator`/`WorldDeltaStore` (§4); `SurfaceAt`
   должен возвращать верхний твёрдый воксель, `IsFlatAndFree` — площадку постройки.
3. На события стриминга вызывать `SetWorldOrigin` + `LoadRing`/`UnloadOutside`; `Tick(dt)` — рядом с тиками NPC.
4. `NpcStates` и `Removed` сохранять/восстанавливать своим persistence-слоем (схему сейвов не менял).
5. Если `0x5EFFA0` доступна по произвольной точке — передать её в `AlphaScatterField` как `spacing`
   (иначе оставить текущее значение и статус `CUBEGAME_CONTRACT`).
6. Отдельно решить п. 7.2 (какой шов владеет растительностью).

## 9. Handoff для Issue #13 (готовый текст выкладки)

> **DeepSeek #13 — runtime размещения контента, `READY_FOR_REVIEW`.**
> Commit `86ef3756e48f59bea78f46e9b0c8af64c294578e`, ветка `deepseek/d85-visual-unity`.
> Сделано: единый масштаб Alpha (`AlphaWorldScale`: зона 256, регион 16 384, мир 2²⁴, клетка 2 048 —
> исправлена ошибка ×256 от чтения `RegionBlocks=64` как вокселей); зональные планировщики
> (`AlphaVegetationPlanner/AlphaStructurePlacementPlanner/CreatureSpawnPlanner.PlanZone`);
> `ZoneContentPlanner`; runtime `WorldContentStreamer` (загрузка/выгрузка зоны, стабильные id,
> `CreatureStateStore`, удалённые объекты, floating origin на `double`); адаптер мира
> `DelegateContentZoneSource`; origin-поддержка в `CreatureAgent`/`CreatureSpawner`.
> Unity 6000.3.27f2: EditMode **369/369**, PlayMode **50/50**, failed=0 (запускать **без** `-quit`).
> W-18: `0x005EFFA0` — маска рельефа (исполнена Claude, 2.1·10⁻⁷), а не алгоритм размещения;
> `fVar6` в W-18 — значение маски; позиции остаются `CUBEGAME_CONTRACT`.
> Отчёты: `docs/reports/D86_CONTENT_PLACEMENT_IMPLEMENTATION.md`, `docs/reports/D86_W18_ADDRESS_VERIFICATION.md`.
> Claude при слиянии: собрать `DelegateContentZoneSource` из `WorldState`/`WorldDeltaStore`, вызывать
> `SetWorldOrigin` + `LoadRing`/`UnloadOutside`, сохранять `NpcStates`/`Removed` своим слоем;
> решить, кто владеет W-18-растительностью — runtime или `IZoneContentProvider`.

## 10. Что НЕ сделано сознательно
Не создавался каталог моделей, не запускались массовые конверсии, не менялись `main`, `Pilot.unity`,
`GameBootstrap`, `Features/World`, `Features/Session`, S02–S04 Core, схема сейвов и файлы Claude. Ассеты
оригинальной Alpha не публиковались; работа только в `D:\Projects\CubeGame-deepseek`, ветка
`deepseek/d85-visual-unity`, без force push.
