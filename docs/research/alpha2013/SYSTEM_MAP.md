# S00-R01 — карта подсистем Alpha 2013

**Проект:** CubeGame (рабочее название).
**Этап:** S00-R01 (D40).
**Метки:** `ALPHA_DIRECT` — наблюдалось в запущенном клиенте (S00-G0); `BINARY_STATIC` — установлено чтением файлов Alpha; `RECONSTRUCTED_SOURCE` — из декомпилированного дерева Reversal (не первоисточник, возможны ошибки); `SECONDARY` — внешние описания; `UNKNOWN`.

**Граница документа:** это **собственная карта CubeGame**, а не копия чужого дерева. Имена подсистем и факты переписаны своими словами; тела функций, структуры и исходный код Reversal в репозиторий не переносятся.

## 1. Слои клиента

| Слой | Состав | Доказательство |
|---|---|---|
| Игровая логика (`cube::`) | `World`, `Zone`, `ZoneTile`, `Field`, `WorldMap`, `Dungeon`, `House`, `Creature`, дерево `Behavior`, `Database`, `GameController`, `Controller`, `QuestText`, `Speech`, `Sprite` | `BINARY_STATIC` (RTTI `Cube.exe`) |
| Движок (`plasma::`) | оконный слой, D3D9-рендер, шрифты, виджеты, объектный поток `.plx` | `BINARY_STATIC` (RTTI, `.plx`-теги) |
| Отражение/биндинги (`abstr::`) | `Function`, `Machine`, `HelperCmpClass`, `HelperIdentityClass` | `BINARY_STATIC` |
| Сторонние библиотеки | SQLite 3.7.15.2 (статически), zlib 1.2.3, FreeImage, XAudio2, MSVC 2012 CRT | `BINARY_STATIC` (DLL в каталоге), `RECONSTRUCTED_SOURCE` (версии) |

Практический вывод для ADR-0001: естественная граница «мир/логика ↔ представление» в оригинале проходила ровно между `cube::` и `plasma::`. Это независимое подтверждение выбранного в ADR-0001 разделения `World/Core` и `Presentation`.

## 2. Подсистема мира

| Элемент | Роль | Метка |
|---|---|---|
| `World` | корневой объект мира: активные чанки, сущности рядом с игроком, подписи объектов, сериализация состояния сущностей | `BINARY_STATIC` + `RECONSTRUCTED_SOURCE` |
| `Zone` | крупная область мира: 64×64 плиток `ZoneTile` + 64 объекта `Region` + сетка полей `Field` | `RECONSTRUCTED_SOURCE` |
| `ZoneTile` | плитка зоны, шаг 32 блока (см. `GENERATION_MOVEMENT_SPEC.md`) | `RECONSTRUCTED_SOURCE` |
| `Field` | поле на зону (65 536 записей по 32 Б) — основа для шума высот/биомов | `RECONSTRUCTED_SOURCE` |
| `Region` | сетка 64×64 ячеек по 4 Б (высота/признак) | `RECONSTRUCTED_SOURCE` |
| `WorldMap` | карта регионов, сохраняется в `Save\map_<имя>.db` по ключам `reg<X>_<Y>` | `RECONSTRUCTED_SOURCE` + `SAVE_OBSERVED` |
| `Dungeon`, `House`, `Spawn` | генерация структур и точек появления | `RECONSTRUCTED_SOURCE` |

Функциональный индекс (по Reversal, `RECONSTRUCTED_SOURCE`), полезный для будущих партий R01:

- шум/высоты: `WorldInfo_sampleTerrainHeight`, `Terrain_sampleHeightAtWorldXY`, `Terrain_sampleHeightNoise`, `terrainNoiseDensityAt`;
- координаты: `world_getColumnAtFixedPos`, `world_getRegionCell64`, `packWorldCoord64`, `VoxelChunk_cellAt`, `terrainNoiseDensityAt`;
- генерация контента: `WorldInfo_generateBiomeContent`, `World_populateRegionDecorations`, `World_generateTreeRoot`, `World_generateVegetationCluster`, `World_generateWaterOrPathFeature`, `World_carveTerrainFeatureA/B`;
- структуры: `World_computeStructurePlacement`, `World_resolveStructureFootprint`, `dungeon_splitRegion_recursive`;
- сохранение: `World_serializeCellData`, `deserializeRegionChunk`.

## 3. Подсистема персонажа и управления

| Элемент | Роль | Метка |
|---|---|---|
| `Creature` | общий класс живых существ (игрок, NPC, монстр): уровень, класс, специализация, урон, баффы, кулдауны | `BINARY_STATIC` (класс) + `RECONSTRUCTED_SOURCE` (поля) |
| `Controller` | ввод и управление | `BINARY_STATIC` |
| `GameController` | верхний игровой контроллер: состояние приложения, мир, UI, аудио | `BINARY_STATIC` |
| дерево `Behavior` | `CombatBehavior`, `CompanionBehavior`, `RandomWalkBehavior`, `WalkPathBehavior`, `LookAtPlayerBehavior`, `SequentialBehavior`, `RandomInteractionBehavior`, `SpawnLocationBehavior` | `BINARY_STATIC` |
| `Speech`, `QuestText`, `QuestTextNode` | тексты NPC/квестов | `BINARY_STATIC` |

Поля `Creature`, помеченные в Reversal как подтверждённые смещения (`RECONSTRUCTED_SOURCE`, использовать только как гипотезу для будущих спецификаций):

| Смысл | Смещение | Тип |
|---|---|---|
| признак состояния (используется в формулах урона/скорости) | `0x060` | u8 |
| stun/stagger | `0x128` | i32 |
| класс: 1 Warrior, 2 Ranger, 3 Mage, 4 Rogue | `0x140` | u8 |
| специализация | `0x141` | u8 |
| «poise» (равновесие) | `0x174` | f32 |
| базовый урон | `0x178` | f32 |
| перезарядка | `0x17c` | i32 |
| уровень | `0x190` | i32 |
| «звёздность» монстра | `0x1a8` | u8 |
| ранги навыков (11 шт.) | `0x1138` | i32[11] |
| список баффов | `0x1178` | ptr |
| карта кулдаунов | `0x139c` | std::map |

Замечание: это **не** готовая спецификация боя. Числовые формулы (урон, Power, скорость) в R01 не проверялись и в CubeGame не переносятся до отдельной партии с тестами (`UNKNOWN`).

## 4. Подсистема данных

| Элемент | Роль | Метка |
|---|---|---|
| `Database` | обёртка SQLite для `data*.db` и `Save/*.db` | `BINARY_STATIC` + `SAVE_OBSERVED` |
| `data1.db` | 2857 моделей `.cub` | `BINARY_STATIC` |
| `data2.db` | 93 звука `.wav` | `BINARY_STATIC` |
| `data3.db` | 104 иконки `.png` | `BINARY_STATIC` |
| `data4.db` | 2 словаря локализации (`de`, `en`) | `BINARY_STATIC` |

Подробности — `FILE_FORMATS.md`.

## 5. Подсистема представления

- `plasma::D3D9Engine` / `plasma::Display` — окно, Direct3D 9, режимы экрана. `SECONDARY`/`BINARY_STATIC`: каталог содержит `XAudio2_8.dll` и `FreeImage.dll`, отдельного D3D9-DLL нет — используется системный.
- Виджеты `cube::*Widget` — весь UI написан на собственном движке виджетов (`StartMenuWidget`, `OptionsWidget`, `InventoryWidget`, `SkillWidget`, `MapOverlayWidget`, `CharacterWidget`, `CharacterPreviewWidget`, `WorldPreviewWidget`, `BlueprintPreviewWidget`, `ObjectivesWidget`, `StatisticsWidget`, `SpeechWidget`, `SpriteWidget`, `ChatWidget`, `EnchantWidget`, `AdaptionWidget`, `VoxelWidget`).
- Три-четырёхлицевой камера/рендер персонажа реализуется через `CharacterPreviewWidget`/`CharacterStyleWidget` (создание героя) — `BINARY_STATIC` (наличие классов) + `ALPHA_DIRECT` (экран создания героя наблюдался в S00-G0).

Для CubeGame это означает: UI Alpha целиком самописный, а не Unity-подобный. Переносить его структуру не нужно; в пилоте достаточно отладочного HUD (см. S01-PILOT, P1.5).

## 6. Генерация мира: что подтверждено, что нет

| Утверждение | Статус |
|---|---|
| Мир — воксельный, регион/зона/чанк иерархичны | `RECONSTRUCTED_SOURCE` (высокая уверенность: классы + константы конструкторов) |
| Позиция хранится в fixed-point 16.16 (1 блок = 65536 единиц) | `RECONSTRUCTED_SOURCE` (деление на `0x10000` в `world_getColumnAtFixedPos`) |
| Мир ограничен 65 536 × 65 536 блоков | `RECONSTRUCTED_SOURCE` (проверка `< 0x10000` в `world_getRegionCell64`) |
| Высоты — комбинация двух 2D-шумов Перлина (частоты ~0.01 и ~0.001) с модуляцией амплитуды | `RECONSTRUCTED_SOURCE` (низкая/средняя уверенность: псевдокод деградирован) |
| Базовая подложка высот берётся из ячейки 256 блоков | `RECONSTRUCTED_SOURCE` (имя `world_getChunkCell256` + чтение `+0xc`) |
| Точные константы, seed-механизм, распределение биомов | `UNKNOWN` |
| Визуально подтверждённый вид участка мира | `ALPHA_DIRECT` только на уровне скриншотов G0 (локально, вне репозитория) |

## 7. Требования к CubeGame, вытекающие из карты

1. Граница `World/Core` ↔ `Presentation` соответствует оригиналу → оставляем как в ADR-0001.
2. Мир CubeGame должен адресоваться целочисленными блоками с детерминированной функцией «seed+координаты → содержимое»; хранение позиции во float допустимо только на уровне представления.
3. Ресурсы CubeGame — свои; форматы `.cub`/`.plx` не реализуем, импорт закрытых ассетов не планируем.
4. Подсистемы боя/прокачки/квестов в пилот не входят (D40 п. 9).

## 8. Ограничения этого документа

- Все сведения `RECONSTRUCTED_SOURCE` получены из псевдокода Ghidra с потерянными параметрами (в телах видны обращения к неинициализированным `column`, `world`), поэтому трактовать их как «точную формулу Alpha» нельзя.
- Динамическая проверка (запуск клиента) в R01 **не выполнялась**: G0 уже подтвердил запуск, а R01 по ТЗ — статическая партия. Все утверждения о поведении камеры/движения остаются `UNKNOWN` (см. `GENERATION_MOVEMENT_SPEC.md`, §4).
