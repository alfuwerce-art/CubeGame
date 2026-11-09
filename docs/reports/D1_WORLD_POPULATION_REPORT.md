# D1 — заселение мира Alpha (волна 1, пакет DeepSeek)

**Дата:** 2026-11-09. **Исполнитель:** DeepSeek Harness. **Ветка:** `deepseek/m0-content`
(каталог `D:\Projects\CubeGame-deepseek`, план [M0_D88_FULL_WORK_PLAN](../planning/M0_D88_FULL_WORK_PLAN.md) §3 D1).
**STATUS: `READY_FOR_REVIEW`** — закрыта доказательная часть (таблица спавна исполнена из Alpha);
позиции/плотность — `CUBEGAME_CONTRACT` до исполнения `Chunk_generateObjects`.

## 1. Было → стало

| Что | Было | Стало |
|---|---|---|
| N-19 «уровень/биом → вид» | `PARTIAL (GAP)`, в игре «первый вид библиотеки» | таблица спавна **исполнена**: `World_generateMonsterSpawnList` (`0x5C9B30`) и `World_generateNpcSpawnList` (`0x5CA0A0`) запущены оракулом на 5 seed'ах → 171 дескриптор монстров + 310 дескрипторов NPC, из них собрана коммит-таблица `AlphaSpawnTable` (вид, уровень, вес) |
| Источник популяции | `IWorldPopulationSource` не реализован | `AlphaWorldPopulation` (зона → `SpawnDefinition`): вид по исполненной таблице, детерминизм от seed, стабильные `SpawnIndex`, `BehaviourKey`, провенанс |
| Стык asmdef | `CubeGame.Features.Creatures` не видел `Features.Contracts` | добавлено ребро `Creatures → Features` (цикла нет: `Features` ссылается только на Core) |

## 2. Что именно исполнено (EMULATED_ALPHA_CODE)

Инструмент: `tools/alpha_models/world/spawn_oracle.py` (тот же оракул, что исполнил генератор мира в M0-02:
`tools/alpha_models/world/terrain_oracle.py` + `tools/alpha_models/universal/x86emu.py`). Функция Alpha
крутится нативно (включая CRT `rand()`), наблюдается только `GameController_addItemToInventory`
(`0x46EBE0`) — его аргумент записи 0x118 байт копируется в отчёт.

Доказанные поля записи (сверено с аргументами вызова построителей `0x5F51E0`/`0x5F8AD0`):

| Поле | Смещение | Смысл | Доказательство |
|---|---|---|---|
| type | `+0x00` | 2 = дескриптор монстра, 3 = дескриптор NPC | тип ставит вызывающий код перед добавлением |
| kind | `+0x01` | вид существа (номер Alpha) | совпадает с видом, выбранным построителем |
| weight | `+0x04` | вес записи (`rand()%100` у монстров) | равен случайному значению, сгенерированному перед записью |
| stars | `+0x0C` | у NPC — звёзды (0, редко 1 ≈ 1 %), у монстров — **не звёзды** | у монстров флаг ~50/50 при 5 seed'ах → `PARTIAL`, в игре 0 |
| level | `+0x10` (2 байта) | уровень записи = аргумент построителя | совпало с `p1` каждого вызова построителя |

Артефакты:

- `tools/alpha_content/data/spawn_descriptors.json` — сырые записи по seed'ам `666666, 1, 4242, 999983, 31337`
  (`SPAWN_ORACLE_RESULT=PASS seeds=5 problems=0`);
- `Assets/CubeGame/Content/Generated/AlphaSpawnTable.g.cs` — 132 строки монстров и 267 строк NPC,
  генерируется `python -m tools.alpha_content.build_spawn_table` (есть `--check`).

## 3. Файлы

| Файл | Назначение |
|---|---|
| `tools/alpha_models/world/spawn_oracle.py` | исполнение двух построителей таблицы спавна + перехват записей |
| `tools/alpha_content/build_spawn_table.py` | агрегация записей → коммит-таблица C# (с `--check`) |
| `tools/alpha_content/data/spawn_descriptors.json` | доказательство: записи по 5 seed'ам |
| `Assets/CubeGame/Content/Generated/AlphaSpawnTable.g.cs` | таблица (вид, уровень, вес) |
| `Assets/CubeGame/Features/Creatures/AlphaWorldPopulation.cs` | `IWorldPopulationSource`: зона → список спавнов |
| `Assets/CubeGame/Tests/Creatures/EditMode/AlphaWorldPopulationTests.cs` | 7 EditMode-тестов |
| `Assets/CubeGame/Features/Creatures/CubeGame.Features.Creatures.asmdef`, `Tests/Creatures/EditMode/…asmdef` | ребро на `CubeGame.Features` (контракты) |

## 4. Фактические прогоны

| Прогон | Результат |
|---|---|
| `python -I tools/alpha_models/world/spawn_oracle.py <Cube.exe> … 666666 1 4242 999983 31337` | `SPAWN_ORACLE_RESULT=PASS seeds=5 problems=0` |
| `python -m tools.alpha_content.build_spawn_table` | `PASS monster_rows=132 npc_rows=267 records={'monster': 171, 'npc': 310}` |
| `python -m tools.alpha_content.build_spawn_table --check` | `PASS drift=False` |
| Unity 6000.3.27f2 EditMode, `CubeGame.Tests.Creatures.EditMode` | `53/53 passed`, 0 failed (`AlphaOutput/pop_editmode2.xml`) |
| Unity 6000.3.27f2 EditMode, весь проект | `461` тестов: 455 passed, 0 failed, 6 skipped (`AlphaOutput/c29_full_editmode.xml`) |
| Unity 6000.3.27f2 PlayMode, весь проект | `57/57 passed`, 0 failed (`AlphaOutput/c29_full_playmode.xml`); ни одного `error CS` в логах |

## 5. PARTIAL / UNKNOWN (не выдаётся за Alpha)

1. **Позиции и число спавнов на зону** — `CUBEGAME_CONTRACT` (`AlphaWorldPopulation.SpawnsPerZone = 6`,
   позиция — детерминированный хэш внутри зоны). Alpha ставит спавн-точки внутри `Chunk_generateObjects`
   (`0x5AF170`), эта функция ещё **не исполнена**.
2. **Биом.** Вызывающий код передаёт построителям `variant = -1` («все варианты»), поэтому исполненная
   таблица — общая, без привязки к биому; соответствие «тип региона/спавн-точки → variant» не установлено.
3. **Звёзды монстров** — `PARTIAL` (см. §2), в игре 0.
4. **Плотность, деспавн, лимит популяции** в Alpha не найдены (было известно ранее).
5. **NPC-таблица** собрана (`AlphaSpawnTable.Npc`, 267 строк), но в игру пока не подключена — NPC-сервисы
   относятся к D3/D2; сейчас в игре используются только строки монстров.

## 6. Что нужно от Claude (C6)

1. Создать источник в композиции: `new AlphaWorldPopulation(seed, zoneLevel, groundHeight)` —
   `zoneLevel` из генератора регионов, `groundHeight` из `WorldState.SurfaceAt` (иначе Y = 0).
2. При регистрации в сцене `SpawnDefinition` → сущность/комбатант/ИИ/визуал остаётся за Claude; мои поля
   `ClassId/SpecializationId` = −1 (R-1 не исполнена), поэтому C-29 даёт безоружную атаку 3.
3. Если нужен NPC-слой (город/торговцы) — брать `AlphaSpawnTable.Npc` и `npcTable: true`.

## 7. Следующий шаг

1. Исполнить `Chunk_generateObjects` (`0x5AF170`) на зоне: получить **точки спавна** (типы `0x80…0x84`),
   их позиции и `variant` → закрыть позиции/плотность/биом (N-19 полностью).
2. Затем D3: постройки, города, данжи, растительность через `WorldContentSource` (один путь размещения).
