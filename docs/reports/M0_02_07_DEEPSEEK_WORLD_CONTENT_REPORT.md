# M0-02/M0-07 — мир и контент Alpha: объекты, имена, навыки, дома/данжи (партия DeepSeek, №4)

**TASK:** D86 (issue #13), M0-02C/M0-07A в зоне DeepSeek: подтверждённые таблицы мира (объекты домов/города, имена предметов, навыки), шаблоны построек и данжей с измеренными габаритами, детерминированное размещение структур по клетке, приёмка через EditMode.

**STATUS: `READY_FOR_REVIEW` — не `ACCEPTED`.** `main` не тронут, ассеты Alpha не публиковались, сцена/генератор Claude не изменялись.

**Ветка:** `deepseek/d85-visual-unity` (после `1924ec4`). **Worktree:** `D:\Projects\CubeGame-deepseek`. **Unity:** 6000.3.27f2.

---

## 1. Было → стало

| Что | Было | Стало |
|---|---|---|
| Объекты домов/города | только в `docs/research` и `.scratch` raw | коммитируемое evidence-файл `tools/alpha_content/data/world_ctor_evidence.json` (67 объектов, 133 имени предметов, 27 навыков; ids/names only) |
| Использование в игре | отсутствовало | `Assets/CubeGame/Content/AlphaWorldContent.cs`: lookup, категории (структура/мебель/контейнер/ловушка/сервис/декор) с явной пометкой `NAME_PREFIX_CUBEGAME_CONTRACT` |
| Постройки | в реестре как «MISSING» | `AlphaStructureTemplates`: дом 3×3×4 (стили 1…5), комната данжа 3×3×3 (шаг 7); габариты `DIRECT`, интерьер `UNKNOWN` |
| Размещение | алгоритм W-16 только описан | `AlphaStructurePlacementPlanner`: детерминированные кандидаты на клетку, 4 попытки, 4 поворота, проверка свободного футпринта через предикат мира; без свободного места — явная проблема, не принудительная постройка |
| Данжи (M0-07B) | только размеры комнат в evidence | `AlphaDungeonRules` (комната 3×3×3, шаг 7, сундук ровно 10 предметов, имена входов) + `AlphaDungeonLayoutPlanner`: детерминированный связный скелет на решётке 5×5 с L-коридорами; статус `PARTIAL` (порядок вырезания коридоров/содержимое комнат неизвестны) |
| **W-18 климат→объекты** | `UNKNOWN`, таблица не выгружена | **`TRANSLATED_FROM_CODE`**: дерево `WorldInfo_scatterObjectsInArea @ 0x005f56c0` механически транслировано в C# (`AlphaScatterTree`, 77 id, условия декомпилятора сохранены, goto→`goto End`), `AlphaScatterSelector` вызывается с `humidity/temperature/cell/spacing/level/mode`. Подземная и «сухая» ветки совпали с ранее доказанными списками; **живой Alpha-проверки нет** — статус не `VERIFIED_ALPHA` |
| **Размещение растительности** | не было | `AlphaVegetationPlanner`: селектор W-18 → детерминированные позиции в клетке; ставятся только подготовленные виды, отсутствующий вид даёт `SCATTER_KIND_NOT_STAGED` (без подмены); позиции — `CUBEGAME_CONTRACT` (`World_placeObjectWithSpacing` не портирован) |
| **Шов с климатом мира** | не было | `IAlphaClimateGrid` (humidity/temperature/region-cell) + `AlphaScatterField`: выбор и размещение прямо из климатических сеток игры; отсутствие сетки — ошибка, а не климат по умолчанию |
| **Сундуки данжа** | не было | `AlphaDungeonChests.Plan`: точки сундуков по комнатам сцены данжа, якорь — `CUBEGAME_CONTRACT` (позиции Alpha не восстановлены); правило «ровно 10 предметов» (`AlphaDungeonRules.ChestItemCount`) сохраняется |
| Тесты | — | +4 EditMode (344/344 всего); PlayMode без регрессий (34/34) |

---

## 2. Файлы

| Файл | Назначение |
|---|---|
| `tools/alpha_content/build_world_evidence.py` | дистилляция raw-выгрузки `World::ctor_1` в коммитируемое evidence (объекты/предметы/навыки), `--check` |
| `tools/alpha_content/data/world_ctor_evidence.json` | 67 объектов, 133 имени `(type,subtype)`, 27 навыков; провенанс: источник, символ, правило парности |
| `tools/alpha_content/build_content_tables.py` | эмитит `AlphaWorldObjectDef[]`, `AlphaItemNameDef[]`, `AlphaSkillDef[]`; `--check` |
| `Assets/CubeGame/Content/Generated/AlphaContentTables.g.cs` | те же таблицы с SHA входных файлов |
| `Assets/CubeGame/Content/AlphaWorldContent.cs` | lookup объектов/имён/навыков, категории объектов, сервисные объекты, валидация 67/133/27 |
| `Assets/CubeGame/Content/AlphaStructures.cs` | `AlphaStructureTemplateDef`, `AlphaStructureTemplates` (дом 3×3×4, данж 3×3×3, шаг 7), `AlphaStructurePlacementPlanner` (детерминизм, повороты, свободный футпринт), валидация |
| `Assets/CubeGame/Content/AlphaDungeons.cs` | `AlphaDungeonRules` (размер комнаты, шаг, 10 предметов в сундуке, входы), `AlphaDungeonLayoutPlanner` (связный скелет с L-коридорами, детерминизм по seed), статус `PARTIAL` |
| `Assets/CubeGame/Content/AlphaNpcServices.cs` | `AlphaServiceKind` (Trade/Craft/Customize/Rest/Smelt) и `AlphaNpcServices`: сервисная мебель Alpha → категория взаимодействия; `Status` явно помечает `CUBEGAME_CONTRACT`, торговые правила F-U5 `UNKNOWN` |
| `Assets/CubeGame/Tests/World/PlayMode/WorldContentPlayModeTests.cs` | сквозной контент-сценарий: размещение дома, план данжа, сервисы, спавн существ+декора с Alpha-визуалами, тик, despawn/respawn со стабильным id |
| `tools/alpha_content/extract_scatter_table.py` + `data/scatter_evidence.json` + `Content/Generated/AlphaScatterEvidence.g.cs` | извлечение W-18: 6 веток, 64 id, условия декомпилятора дословно |
| `tools/alpha_content/scatter_tree_translate2.py` + `Content/Generated/AlphaScatterTree.g.cs` | механическая трансляция всего дерева `scatterObjectsInArea` в C# (77 id, goto→`goto End`) |
| `Assets/CubeGame/Content/AlphaScatterSelector.cs` | фасад выбора W-18: `Select(humidity, temperature, cell, spacing, level, mode)`, статус `TRANSLATED_FROM_CODE` |
| `Assets/CubeGame/Content/AlphaVegetationPlanner.cs` | детерминированное размещение объектов W-18 в клетке, только подготовленные виды, отчёт о несопоставленных id |
| `Assets/CubeGame/Content/AlphaScatterEvidenceValidation.cs` | проверка извлечённых данных (известные виды, непустые ветки, статус PARTIAL) |
| `Assets/CubeGame/Tests/AlphaModels/EditMode/AlphaModelsEditModeTests.cs` | `AlphaWorldContentTests`: имена Door/Chest/Workbench/FireTrap, LifePotion (1,1), HangGlider (0x17/0), SkillClimbing, сервисы ≥8, шаблоны и размещение (детерминизм, отказ при занятом футпринте, неизвестный шаблон) |
| `Assets/CubeGame/AlphaModels.Import/Editor/AlphaModelsImport.cs` | в проверку кэша добавлены отчёты мира/структур |

---

## 3. Фактические прогоны

| Прогон | Результат |
|---|---|
| EditMode | `total=352 passed=352 failed=0` (`AlphaOutput/editmode.xml`) |
| PlayMode | `total=36 passed=36 failed=0` (`AlphaOutput/playmode.xml`) |
| Кадр мира | `RunPreview` → `world_content.png`: дом 3×3×4, 4 комнаты данжа + 3 коридорных набора, 6 объектов растительности по W-18; `ALPHA_PREVIEW_RESULT=PASS actors=156 images=290 problems=0` |
| Выгрузка таблиц мира | `WORLD_CTOR_EVIDENCE=PASS objects=67 items=133 skills=27` |
| Генерация контента | `ALPHA_CONTENT_TABLES=PASS … worldObjects=67 itemNames=133 skills=27` |

---

## 4. ALPHA_REFERENCE + DELTA

| Данные | Доказательство | Статус |
|---|---|---|
| 67 объектов домов/города (Door, Window, Chest, MarketStand1-3, Furnace, Workbench, CustomizationBench, BedTable, SleepingMat, RuneStone, Artifact, StreetLight…) | `World::ctor_1` `0x0058eb00`, парность insert→string (M0-00B review, AI-17) | `DIRECT` (имена), `AMBIGUOUS` флаги сохранены |
| 133 имени `(type, subtype)` (зелья, фляга/вода, дельтаплан/лодка, лампа, мана-куб, еда…) | тот же конструктор | `DIRECT` (типы), подтипы оружия `AMBIGUOUS` |
| 27 навыков (PetTaming, Riding, Climbing, HangGliding, Swimming, BoatDriving, Ability*) | тот же конструктор | `DIRECT` |
| Дом 3×3×4, стиль 1…5 | W-17 `Chunk_generateObjects 005af170` | `DIRECT` размер, `UNKNOWN` интерьер/правила |
| Комната данжа 3×3×3, шаг 7, L-коридоры | Q-16 | `PARTIAL` |
| Размещение структур | W-16 алгоритм `WorldInfo_placeStructure 005f0ce0` (`DIRECT` алгоритм), правила «что и где» | `CUBEGAME_CONTRACT` + предикат мира |

---

## 5. PARTIAL / UNKNOWN

- **W-16**: правила «что и где ставить» не восстановлены — планировщик не выбирает места сам, а спрашивает `isFootprintFree`.
- **W-18**: таблица «климат → объекты» (~660 строк) не выгружена — растительность/биомный спавн по-прежнему `UNKNOWN`, не выдумывается.
- **Интерьер дома, разложение 12-байтной клетки (U4), мебель `0x88d…0x8ef`** — `UNKNOWN`.
- **Данжи**: только габарит комнаты и шаг; свет/факелы (J-U8), входы, сундуки — `PARTIAL`.
- **W-18**: дерево транслировано механически (`TRANSLATED_FROM_CODE`): условия и списки id взяты из декомпилированной функции, `goto LAB_005f6ce4` сохранён как `goto End`. Подземная и сухая ветки совпали с ранее доказанными списками, добавлена **сверка транслята с извлечёнными группами** (EditMode: каждый id подземной ветки есть в `AlphaScatterEvidence`, все id дерева — известные виды каталога), но **живой проверки нет** — нельзя маркировать `VERIFIED_ALPHA`.
- **Сервисы NPC (торговцы/кузнецы)**: объекты-рабочие места известны (Workbench/Anvil/Furnace/MarketStand…), но правила сервиса/ассортимент — `UNKNOWN` (F-U5).
- **Категории объектов** — CubeGame-контракт по именам (Alpha хранит только имена), явно помечено.

---

## 6. Handoff

Claude получает: таблицы имён/объектов/навыков (только чтение), шаблоны и планировщик структур с предикатом мира — [D86_DEEPSEEK_HANDOFF_M0_02](D86_DEEPSEEK_HANDOFF_M0_02.md).

Следующая партия DeepSeek — M0-08 (VFX/аудио/иконки/UI-данные) и M0-07B/C (данж-шаблоны, сервисы) после появления данных; M0-02A/B остаются за Claude (генератор мира).
