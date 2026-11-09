# M0 — мастер-матрица полного переноса Alpha → Unity (общая сверка команды)

**Дата сверки:** 2026-11-09. **Основание:** D87 (общая папка), D86 (полный перенос M0 разрешён), D85, D72, D66 (дорога M0-00…M0-09).
**Каталог:** `D:\Projects\CubeGame`, ветка `integration/m0`. **Исходная Alpha:** `D:\Games\Cube world` — только чтение.
**Статус документа:** рабочая матрица координации, не приёмка. `ACCEPTED` присваивает только продюсер.

## 0. Как читать статусы

| Статус | Значение |
|---|---|
| `IMPLEMENTED` | код в общей ветке, есть прогон Unity с артефактом |
| `PARTIAL` | работает часть механики; отсутствующее перечислено явно |
| `NOT_STARTED` | кода нет |
| `BLOCKED` | нет доказательств Alpha или ждёт решения продюсера |
| `CLAIMED:<агент>` | файлы/задача закреплены за агентом |

**Важно:** все числа прогонов ниже — из отчётов веток **до** слияния (`claude/d85-gameplay-unity` @ `de62ec6`, `deepseek/d85-visual-unity` @ `70b2326`). После слияния в `integration/m0` они перепроверяются одним общим прогоном; расхождение фиксируется отдельным REPORT в `docs/agents/AGENT_COORDINATION.txt`.

## 1. Фактическая база

| Что | Значение |
|---|---|
| Общий предок двух веток | `f0d750c` (D85 memory) |
| Claude-срез | `de62ec6` — M0-01 + M0-02 (a–d), EditMode 344 (2 skipped), PlayMode 22 |
| DeepSeek-срез | `70b2326` — M0-01/02c/03c/04b/05c/06/07/08a/09c (контент и визуал), EditMode 356, PlayMode 46 |
| Прогноз конфликта слияния | 1 файл: `Assets/CubeGame/Features.meta` (add/add, разные GUID папки) |
| Unity | 6000.3.27f2, `D:\Unity\Hub\Editor\6000.3.27f2` |
| Приёмка S05B-2 / M0 | не выполнена; работа агентов = `READY_FOR_REVIEW` |

## 2. Матрица подсистем

### M0-01 — единство игры (сессия, часы, меню, сохраняемость)

| Аспект | Claude | DeepSeek | Статус | Владелец | Следующее действие |
|---|---|---|---|---|---|
| GameSession/GameClock 30 Гц, отказы с причиной | `Features/Session/*` | — | `IMPLEMENTED` | Claude | перевести `PlayerMotor` на часы (M0-03B) |
| Меню → новый мир → игра, Esc-пауза, выход | `Presentation/Gameplay/GameBootstrap.cs`, `Assets/Scenes/Pilot.unity` | — | `IMPLEMENTED` (временный IMGUI) | Claude | заменить на UI M0-08A |
| Сейв-слот `CUBEGAME_SESSION` v1 + отказы при повреждении | `Features/Persistence/*` | — | `IMPLEMENTED` | Claude | сейв v2 — только после решения (M0-00D) |
| Контракты для визуального слоя (`IEntityVisualSink`, `SpawnDefinition`, `WorldDelta`, `NpcStateSnapshot`) | `Features/Contracts/*` | ожидает | `PARTIAL` — контракт есть, связки со сценой нет | Claude → DeepSeek | подтвердить контракт, подключить `AlphaActorView` |
| S05 runtime в основном проекте, загрузка локального кэша без Python | — | `AlphaModels/**`, `AlphaModels.Import/**` | `IMPLEMENTED` | DeepSeek | подключить к сцене через контракт |
| Граф сборок Features/Presentation/Core | asmdef | `Content`, `Features.AI`, `Features.Creatures` asmdef | `PARTIAL` — после слияния граф один | Claude (общие asmdef) | проверить компиляцию объединённого графа |

### M0-02 — мир (генерация, стриминг, дельты, содержимое зон)

| Аспект | Claude | DeepSeek | Статус | Владелец |
|---|---|---|---|---|
| Рельеф Alpha: климат-клетки, база, маска гор | `Features/World/Alpha*.cs`, оракул `tools/alpha_models/world/terrain_oracle.py` | — | `IMPLEMENTED` (≤2.3·10⁻³ вокселя на 1320 точках) | Claude |
| Воксельные зоны 256×256, цвета, пещеры, вода озёр | `AlphaZoneGenerator`, `ChunkMesher` | — | `PARTIAL`: вода озёр `RECONSTRUCTED` (не перепроверена после правки констант), обычные скалы равнин `RECONSTRUCTED` | Claude |
| Ориентиры регионов, сетка зон, скалы, дороги, реки | `generateRegion`-порт | — | `IMPLEMENTED` (36 864 записи сетки — 0 расхождений) | Claude |
| Стриминг чанков, выгрузка, плавающее начало координат | `StreamingWorldView` | — | `IMPLEMENTED` | Claude |
| Дельты мира и сохранение региона | `WorldDeltaStore`, `slotN.world.sav` | — | `IMPLEMENTED` | Claude |
| Содержимое зоны: `IZoneContentProvider` | контракт | `DelegateContentZoneSource`, `AlphaVegetationPlanner` | `PARTIAL` — контракт есть, поставщик не зарегистрирован в игре | Claude → DeepSeek |
| W-18 климат → объекты, 77 id | — | `AlphaScatterTree`, `AlphaScatterSelector`, `AlphaClimateField` | `PARTIAL`: `TRANSLATED_FROM_CODE`, живой сверки нет | DeepSeek |
| Размещение структур/домов/данжей/декора | — | `AlphaStructures`, `AlphaDungeons`, `AlphaDungeonChests` | `PARTIAL` (детерминированное размещение, правила данжа) | DeepSeek |
| Биомы Alpha (базовые) | климат-клетки мира | W-18 климат | `PARTIAL` — единая биомная модель ещё не сведена | Claude + DeepSeek |

### M0-03 — персонаж

| Аспект | Claude | DeepSeek | Статус | Владелец |
|---|---|---|---|---|
| Ввод, камера, `PlayerMotor` | `Presentation/Gameplay/*` | — | `PARTIAL`: мотор на `Time.deltaTime`, скорости `PLACEHOLDER_NOT_ALPHA` (OQ-23) | Claude |
| Создание персонажа (раса/класс/внешность) | меню вводит имя/класс/seed | `AlphaPlayerVisualBuilder` (раса, волосы, оружие, левая рука, броня) | `PARTIAL` — UI создания нет | Claude |
| Визуал героя: модель, риг, idle/walk/attack | капсула-заглушка | `AlphaActorView`, `AlphaRig`, 94 клипа человека | `PARTIAL` — не подключено к сцене | DeepSeek → Claude |
| HP/XP/класс в состоянии героя | Core `CharacterSheet`, `ProgressionState` | — | `PARTIAL` — Core есть, в сцене не связан полностью | Claude |
| Тинт волос по D61 | — | `AlphaHairTint`-правило в контенте | `IMPLEMENTED` | DeepSeek |

### M0-04 — предметы и экипировка

| Аспект | Claude | DeepSeek | Статус | Владелец |
|---|---|---|---|---|
| База предметов, статы, S04 слоты | `Core/Items`, `Core/Equipment` (ACCEPTED) | — | `IMPLEMENTED` | Claude (не менять) |
| Модели оружия/левой руки/удерживаемых/брони в руке | — | `AlphaWeaponLayer`, `AlphaOffhandLayer`, `AlphaHeldItemLayer`, `AlphaEquipmentLayer` (26+10 якорей, 32 модели брони) | `IMPLEMENTED` | DeepSeek |
| Правила «тип → слот», расходники (кулдаун 3с/10с) | — | `AlphaItemSlots`, `AlphaConsumableRules` | `PARTIAL`: эффект расходника B-U1 `OPEN` | DeepSeek |
| Иконки, атлас | — | `atlas.png` + `AlphaIconAtlas` (42 иконки) | `IMPLEMENTED` | DeepSeek |
| Инвентарь/UI, подбор, крафт | — | таблицы имён/типов | `NOT_STARTED` (UI) / `BLOCKED` (рецепты OQ-11) | Claude |
| 24 типа / 13 слотов / сейв v2 в Core | — | — | `BLOCKED` до M0-00D | продюсер |

### M0-05 — бой

| Аспект | Claude | DeepSeek | Статус | Владелец |
|---|---|---|---|---|
| Формулы урона, митигация, RNG, HP | `Core/Combat` (ACCEPTED) | — | `IMPLEMENTED` | Claude (не менять) |
| Атака в окне strike, hit → изменение HP, death | частично в S04; в новой сессии не связано | `AlphaActionTiming` (windup/actionEnd/recovery) | `PARTIAL` — главный незакрытый шов | Claude + DeepSeek |
| Библиотеки действий существ | — | 129 из 130 боевых видов (900 клипов), `EMULATED_ALPHA_CODE` | `IMPLEMENTED` | DeepSeek |
| VFX, снаряды, динамические части | — | — | `BLOCKED` (R-3, R-8) | DeepSeek |
| Оружейные действия NPC, RuneGiant | — | — | `BLOCKED` (R-1/R-2/R-9) | DeepSeek |
| Способности/магия по классам | — | — | `NOT_STARTED` | Claude |

### M0-06 — существа и NPC

| Аспект | Claude | DeepSeek | Статус | Владелец |
|---|---|---|---|---|
| Контракты существ, спавн по seed, стабильные ID | — | `Features/Creatures/*` | `IMPLEMENTED` | DeepSeek |
| Базовый AI (угроза, зелья A/B, додж, регенерация) | — | `Features/AI/*` (31 мс) | `IMPLEMENTED` | DeepSeek |
| Визуал 156/156 видов (геометрия + ходьба/покой) | — | `AlphaStagedVisuals` + валидатор | `IMPLEMENTED` | DeepSeek |
| Спавн в игровом мире, offscreen-состояние, переход регионов | — | спавн-слой есть | `PARTIAL` — в мир игры не подключён | DeepSeek → Claude |
| Питомцы/боссы/приручение/езда | — | реестр 10 боссов, константы питомцев | `BLOCKED`/`UNKNOWN` | DeepSeek |
| Таблица «уровень/биом → вид» | — | — | `UNKNOWN` | DeepSeek |

### M0-07 — контент и взаимодействия (города, данжи, торговля, задания)

| Аспект | Claude | DeepSeek | Статус | Владелец |
|---|---|---|---|---|
| Дома/город/данж: шаблоны и размещение | — | `AlphaStructures`, `AlphaDungeons`, `AlphaDungeonChests` | `PARTIAL` | DeepSeek |
| Сервисные категории NPC (Trade/Craft/Customize/Rest/Smelt) | — | `AlphaNpcServices` | `PARTIAL` — контракт без правил | DeepSeek |
| Торговля: цены, ассортимент | — | — | `BLOCKED` (F-U5) | Claude |
| Задания/события Alpha | — | — | `BLOCKED`/`UNKNOWN` (J-U4/U5/U7) | Claude |
| Взаимодействия с объектами (открыть/сломать) | — | объекты мира (67) | `PARTIAL` | DeepSeek |

### M0-08 — интерфейс и feedback

| Аспект | Claude | DeepSeek | Статус | Владелец |
|---|---|---|---|---|
| Меню/пауза (временный IMGUI) | `GameBootstrap` | — | `PARTIAL` | Claude |
| HUD, инвентарь, журнал, карта | — | иконки/атлас как данные | `NOT_STARTED` | Claude |
| Звук | — | — | `BLOCKED` (нет доказательств Alpha) | DeepSeek |
| Feedback попаданий | — | — | `BLOCKED` (VFX R-3) | DeepSeek |

### M0-09 — сквозная сборка и приёмка

| Аспект | Claude | DeepSeek | Статус | Владелец |
|---|---|---|---|---|
| Сквозной сценарий: создать мир → герой → путешествие → бой → торговля → задания → сейв/загрузка | частично (мир/сессия/сейв) | частично (существа/контент) | `PARTIAL` — нет единой цепочки | Claude (интегратор сцены) |
| Витрина контента, манифест, профиль производительности, churn-тест | — | `AlphaContentShowcase`, `AlphaContentManifest`, 0.172 мс/кадр, 200 spawn/destroy | `IMPLEMENTED` (как доказательство) | DeepSeek |
| Итоговая сборка и отчёт | — | — | `NOT_STARTED` | все |
| Личная приёмка | — | — | только продюсер | продюсер |

## 3. Дубли и риски интеграции

1. **Две независимые реализации части M0-01.** У Claude — `GameSession`/`GameClock`/меню; у DeepSeek — загрузка S05 runtime в основной проект и `AlphaActorView`. Смыслового дубля нет, но точка стыка одна — визуальный контракт. Риск: два источника истины по времени/позиции актора, если DeepSeek начнёт считать время у себя.
2. **Две «модели мира».** Claude владеет рельефом/чанками/дельтами; DeepSeek — климатом W-18, размещением содержимого и структурами. Риск: содержимое, поставленное по W-18, должно попадать на высоту из `WorldState.SurfaceAt`, а не в собственную сетку.
3. **Единицы мира.** В Alpha зона 256 вокселей, регион 64 зоны; DeepSeek-контент использует `AlphaWorldScale`. Любая константа масштаба в контенте должна читаться из общего места, а не дублироваться.
4. **asmdef.** Корневой `Features` (Claude) и вложенные `Features.AI`/`Features.Creatures` (DeepSeek) должны давать один непротиворечивый граф; проверить компиляцией после слияния.
5. **Сцена.** `Assets/Scenes/Pilot.unity` — единственный редактор Claude. DeepSeek даёт prefab/adapter и тестовую сцену, сцену игры не правит.
6. **Пакеты/ProjectSettings/Library.** Один слот Unity; одновременные импорты запрещены (D87 §6).

## 4. Очередь следующих пакетов (предложение)

**P0 — первый общий играбельный срез (цель продюсера):** в `Pilot.unity` герой — реальная модель HumanMale с мечом, idle/walk/attack-клипы, камера и управление, враг из DeepSeek-спавна, удар через `Core/Combat` с фактическим уменьшением HP, сохранение и загрузка этой же сцены.
Шов: `IEntityVisualSink` → `AlphaActorView`; `AlphaActionTiming` → окно удара; владелец сцены Claude, визуальные компоненты DeepSeek.

**P1 — мир и содержимое:** регистрация `IZoneContentProvider` в игре (деревья W-18, декор, структуры), спавн существ на `WorldState.SurfaceAt`, проверка «выгрузка региона не теряет существ».
**P2 — предметы/инвентарь:** подключение `AlphaItemSlots`/иконок к UI Claude, экипировка в сейв (после решения M0-00D).
**P3 — NPC/торговля/задания:** правила F-U5, J-U4/U5 — сначала доказательства Alpha, затем код.
**P4 — регрессия и сборка:** сквозной сценарий M0-09, профиль FPS/памяти, отчёт и личная приёмка.

## 5. Что нельзя делать без продюсера

- Менять принятый `Core` S02–S04, `main`, сейв v1 → v2, 24 типа/13 слотов.
- Удалять дубли/чужие реализации, worktree `D:\Projects\CubeGame-deepseek`, ветки.
- Публиковать оригинальные/производные ассеты Alpha.
- Присваивать `ACCEPTED`, объявлять Alpha-parity без доказательства.
