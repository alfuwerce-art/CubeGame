# D1 шаг 2 (часть A) — правило зон со спавнерами Alpha (статическое доказательство)

**Дата:** 2026-11-10. **Исполнитель:** DeepSeek Harness. **Ветка:** `deepseek/m0-content` @ `ae5803b`.
**STATUS: `READY_FOR_REVIEW`** — правило зон зафиксировано; позиции точек спавна всё ещё требуют
исполнения `Chunk_generateObjects` (идут в фоне, см. §5).

## 1. Было → стало

| Что | Было | Стало |
|---|---|---|
| Где в мире вообще есть спавнеры Alpha | неизвестно (популяция ставилась «по контракту» на любую зону) | `AlphaPopulationRules`: `Chunk_generateObjects` (`0x5AF170`) вызывается из `WorldInfo_generateBiomeContent` (`0x5E4850`) **только для типов записи зоны 1 и 7** (call-site `WorldInfo.cpp:3266` проверяет `local_1304[6]` против `0x00000001` и `0x00000007`) — остальные типы зон спавнеров не несут |
| Диагностика | пустая зона без объяснения | `ExplainEmptyZone(type)` → `NO_ALPHA_SPAWNERS zone type N (only 1 and 7 carry them)` |

## 2. Файлы

| Файл | Назначение |
|---|---|
| `Assets/CubeGame/Features/Creatures/AlphaPopulationRules.cs` | константы типов, `CarriesSpawnerObjects`, `SpawnerZoneTypes`, `ExplainEmptyZone`, `Validate` |
| `Assets/CubeGame/Tests/Creatures/EditMode/AlphaPopulationRulesTests.cs` | 2 теста |

## 3. Фактические прогоны

| Прогон | Результат |
|---|---|
| Адресный EditMode, `CubeGame.Tests.Creatures.EditMode` | `123/123 passed`, 0 failed (`AlphaOutput/d1b_editmode.xml`) |
| Полные EditMode/PlayMode | EditMode `541` (535 passed, 0 failed, 6 skipped), PlayMode `57/57` (0 failed), `error CS` 0 |

## 4. PARTIAL / UNKNOWN

1. **Смысл типа зоны 7** — `UNKNOWN` (тип 1 — поселение/город по прежним данным; 7 не расшифрован).
2. **Позиции точек спавна** — не получены: карта точек заполняется в генерации мира, дамп требует
   исполнения зоны типа 1/7 до `Chunk_generateObjects`; опыт запущен (см. §5).
3. **Плотность/лимит популяции** — в Alpha не найдены (было известно ранее).

## 5. Что происходит дальше (D1 шаг 2, часть B)

Запущен опыт: оракул генерирует регион (509,509), находит зону типа 1 или 7 и исполняет
`WorldInfo_generateBiomeContent` с хуком на `0x5AF170`, чтобы снять аргумент и карту точек спавна.
Генерация региона в Python-эмуляторе долгая (минуты-десятки минут); скрипты:
`.scratch/tmp/scan_one_region.py`, `.scratch/tmp/probe_spawn_points.py` (локально, в Git не входят).
Пока опыт не дал точек, `IWorldPopulationSource` продолжает ставить существ по `CUBEGAME_CONTRACT`
(позиции), а виды/уровни/веса — по исполненной таблице Alpha.
