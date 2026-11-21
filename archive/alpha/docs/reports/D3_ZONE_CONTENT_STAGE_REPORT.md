# D3 — постройки, растительность и существа в стадии контента зоны (волна 1, пакет DeepSeek)

**Дата:** 2026-11-09. **Исполнитель:** DeepSeek Harness. **Ветка:** `deepseek/m0-content`.
**STATUS: `READY_FOR_REVIEW`** — стадия контента зоны подключена к генератору мира; геометрия построек
и точные позиции Alpha остаются `PENDING`/`CUBEGAME_CONTRACT`.

## 1. Было → стало

| Что | Было | Стало |
|---|---|---|
| Стадия контента зоны (`IZoneContentProvider`) | контракт существовал, реализаций не было: генерируемый мир оставался без построек и растительности | `AlphaZoneContentProvider` — реализация стадии: климат зоны → W-18 выбор объектов, слоты существ, шаблоны построек; всё публикуется в `ZoneContentContext.Spawns` как `SpawnDefinition` |
| Связь «мир Claude → контент DeepSeek» | планировщик `ZoneContentPlanner` вызывался только из тестов/витрины | вызов внутри генерации: контекст зоны адаптируется к `IContentZoneSource` (климат из `Temperature/Humidity/RareCellTerm/Mountain`, поверхность из колонок зоны, «площадка свободна» — по четырём углам) |
| Ключи поведения | не заданы | `static_object` (пассивные объекты, фракция 6), `creature_ai` (боевые), `structure_static` (постройка) |
| Честность данных | — | в каждом `SpawnDefinition.Provenance`: `TRANSLATED_FROM_CODE` (W-18) / `CUBEGAME_CONTRACT` (позиции, плотность, геометрия) |

## 2. Файлы

| Файл | Назначение |
|---|---|
| `Assets/CubeGame/Features/Creatures/AlphaZoneContentProvider.cs` | `IZoneContentProvider` + адаптер контекста зоны + `ToSpawn` (размещение → сущность) |
| `Assets/CubeGame/Tests/Creatures/EditMode/AlphaZoneContentProviderTests.cs` | 6 EditMode-тестов: пассивный объект → `static_object`/фракция 6, существо → `creature_ai`, постройка → `structure_static` + `PENDING` в провенансе, регион/позиция, граница `SpawnIndex`, разбиение библиотеки на пассивные и боевые виды |

Использованы существующие модули без изменений: `ZoneContentPlanner`, `AlphaVegetationPlanner` (W-18),
`AlphaStructurePlacementPlanner`, `CreatureSpawnPlanner`, `DelegateContentZoneSource`-контракт
`IContentZoneSource`.

## 3. Фактические прогоны

| Прогон | Результат |
|---|---|
| Unity 6000.3.27f2 EditMode, `CubeGame.Tests.Creatures.EditMode` | `59/59 passed`, 0 failed (`AlphaOutput/d3_editmode3.xml`) |
| Полные EditMode/PlayMode | EditMode `467` (461 passed, 0 failed, 6 skipped), PlayMode `57/57` (0 failed), `error CS` 0 |

Первые прогоны были красными и это исправлено в причине, а не ослаблением теста:
1. `AlphaVegetationPlanner.SelectionStatus` не существует → `Status` (ошибка компиляции, поймана сборкой);
2. пассивный объект, пришедший каналом «растительность», получал фракцию 0 — теперь фракцию решает
   каталог видов (`CreatureKinds.IsPassiveObject`), а не канал размещения.

## 4. PARTIAL / UNKNOWN

1. **Геометрия построек** — `PENDING`: шаблоны домов Alpha (W-17, 3×3×4) не перенесены в воксели; сейчас
   публикуется footprint (позиция, габариты, поворот) с ключом `structure_static`.
2. **Позиции и плотность** — `CUBEGAME_CONTRACT`: Alpha ставит спавн-точки внутри
   `Chunk_generateObjects` (`0x5AF170`) и `GameController_pickSpawnPoint` (`0x4D4DE0`); исполнение этих
   функций — следующий проход оракула (см. D1).
3. **Удаления** — этой стадии не видны (владелец дельт мира — Claude); `IsRemoved` возвращает `false`.
4. **Сундуки/данжи/города как объекты мира** — данные есть (`AlphaDungeons`, `AlphaDungeonChests`,
   `AlphaNpcServices`), но в стадию контента они попадут вместе с геометрией (следующий шаг D3).

## 5. Что нужно от Claude

1. Зарегистрировать провайдера в генерации зон:
   `registrar.Register(new AlphaZoneContentProvider())` (или с явным `ContentPlanSettings`).
   Ключ провайдера `deepseek.zone.content.v1` пишется в провенанс мира — при смене набора провайдеров
   меняется id генератора.
2. При создании сущностей из `SpawnDefinition` учитывать `BehaviourKey`:
   `static_object` → без ИИ и без действий (как пассивные объекты D67/D69),
   `structure_static` → статичный объект/пьедестал без ИИ, `creature_ai` → `CreatureAi`.
3. Порядок провайдеров: этот провайдер ожидает, что проходы рельефа уже выполнены (как в комментарии
   контракта) — регистрировать после них.
