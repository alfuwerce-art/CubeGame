# D86 M0-01 — Alpha runtime в основном Unity-проекте (партия DeepSeek)

**TASK:** D86 (issue #13) — первая производственная партия в зоне DeepSeek: перенести принятый S05-runtime в основной Unity-проект, подключить локальный кэш Alpha-данных, собрать подтверждённые контентные таблицы и проверить всё реальными EditMode/PlayMode прогонами.

**STATUS: `READY_FOR_REVIEW` — не `ACCEPTED`.** Продюсер визуально не принимал, ChatGPT контракт не ревьюил; `main` не тронут, ассеты Alpha в Git не попали.

**Исполнитель:** DeepSeek Harness. **Worktree:** `D:\Projects\CubeGame-deepseek` (D71, новых worktree не создавалось).
**Ветка:** `deepseek/d85-visual-unity`. **База:** `f0d750c` (D72 + D83 + П1–П6) + перенос D86-документов из `planning/d85-direct-unity-two-agents` (чери-пик `8c6c263`, `298f0e0`, `0157f05`, `5e463a1`).
**Unity:** 6000.3.27f2 (`D:\Unity\Hub\Editor\6000.3.27f2`), пакеты проекта не менялись.

---

## 1. Было → стало

| Что | Было (на базе f0d750c) | Стало |
|---|---|---|
| S05 runtime | C# пилота жил в `tools/alpha_models/transfer/unity/Runtime` и попадал в игру только копированием в изолированный проект | Один источник истины — модуль `Assets/CubeGame/AlphaModels` (asmdef `CubeGame.AlphaModels`), `git mv` без копий |
| Данные моделей | Unity-проект пилота `Assets/AlphaTransfer/Data` со ссылками на `TextAsset`/ассеты сцены | Runtime читает локальный кэш `<root>/actors/<id>/` (`.scratch/content`, env `CUBEGAME_ALPHA_CONTENT`, `persistentDataPath`); в Git — ни одного `.cubdec`/клипа |
| Точка входа для игры | нет (пилотные сцены/viewer) | `AlphaActorView` + `AlphaActorLibrary` + `AlphaLocomotionState` — визуальный адаптер под игру |
| Контент | числа лежали в research-файлах отчётов | `Assets/CubeGame/Content` + генератор `tools/alpha_content/build_content_tables.py` из подтверждённых evidence-JSON (24 типа, 20 подтипов, 16 способностей, 156 видов, 13 слотов, 101 действие) |
| Проверка | изолированные пилоты | реальные EditMode **315/315** и PlayMode **22/22** в основном проекте + рендер ключевых кадров **12 PNG** |
| Материалы | `Shader.Find` в Editor | материалы в `Assets/CubeGame/AlphaModels/Resources` (нужны для player build), создаются Editor-методом |

---

## 2. Что реализовано (файлы)

### Runtime (`Assets/CubeGame/AlphaModels`, asmdef `CubeGame.AlphaModels`)

| Файл | Назначение |
|---|---|
| `Runtime/VoxelMesh.cs`, `AlphaSpace.cs`, `AlphaRig.cs`, `AlphaClip.cs`, `AlphaClipPlayer.cs`, `AlphaParamWalk.cs`, `AlphaLocomotionDriver.cs`, `AlphaAnimationController.cs`, `AlphaEntityContract.cs` | принятый S05 runtime, перенесён `git mv` без изменения логики; `AlphaClipPlayer.SetClipJson` и `AlphaLocomotionDriver.SetSpecJson` добавляют загрузку документов из кэша вместо `TextAsset` |
| `Runtime/AlphaContentSource.cs` | поиск/разбор локального кэша, `AlphaActorDefinition` (rig + clips + contract + locomotion + actions), точные коды проблем (`ACTOR_MODEL_MISSING`, `ACTOR_NO_CLIPS`, `ACTOR_MODEL_OUTSIDE_ACTOR`, …) |
| `Runtime/AlphaRigBuilder.cs` | сборка рига в рантайме: меши из `.cubdec`, якоря, tint, hair-материал, проверка `expectedVertices` и контракта |
| `Runtime/AlphaActorView.cs` | **визуальный API для игры**: `Initialize`, `ApplyLocomotion`, `TryPlayAction(code, out reason)`, `Tick(dt)`, `Status()`; не меняет HP/деньги/инвентарь |
| `Runtime/AlphaActorLibrary.cs` | кэш загруженных акторов, `AlphaActorLibrary.Configure(source)` для тестов/сборки |
| `Runtime/AlphaContentDiagnostics.cs` | отчёт о кэше без исключений (`ALPHA_CONTENT_STATUS=…`) |
| `Runtime/AlphaVisualResources.cs` + `Shaders/VoxelShaded.shader` | материалы/шейдер, `_Tint` = Alpha `uniform30` |
| `Resources/AlphaVoxel.mat`, `AlphaVoxelHair.mat` | материалы для player build (созданы Editor-методом, закоммичены) |

### Content (`Assets/CubeGame/Content`, asmdef `CubeGame.Content`, без зависимостей от UnityEngine)

| Файл | Назначение |
|---|---|
| `Generated/AlphaContentTables.g.cs` | сгенерированные таблицы + SHA-256 входных evidence-файлов в заголовке |
| `AlphaContentDatabase.cs` | lookup/валидация: 24 типа предметов, 20 подтипов оружия, 16 способностей, 12 типов регионов, 156 видов, 14 слотов, 101 действие |
| `AlphaCreatureCatalog.cs` | роли `PassiveObject` 26 / `Combat` 130, семейство движения, действия вида; `TryActionClipName` не даёт выдать несуществующее действие за игровое |
| `AlphaItemRules.cs` | 13+1 слот, двуручные 15/16/17, расходники 80/81, «в руке/за спине» по действию |

### Editor (`Assets/CubeGame/AlphaModels.Import`, asmdef Editor-only)

| Файл | Назначение |
|---|---|
| `Editor/AlphaModelsImport.cs` | `CubeGame/Alpha Models/{Create Visual Resources, Validate Local Content Cache, Render Key Frames}` + batch-методы `RunCreateVisualResources`, `RunValidateContent`, `RunPreview` |

### Инструменты

| Файл | Назначение |
|---|---|
| `tools/alpha_content/build_content_tables.py` | генератор таблиц из evidence; `--check` запрещает расхождение |
| `tools/alpha_content/stage_local_content.py` | раскладка `Assets/AlphaTransfer/Data` конвейера → `<root>/actors/<variant>/` (локально) |

### Тесты

| Файл | Что проверяет |
|---|---|
| `Assets/CubeGame/Tests/AlphaModels/EditMode/AlphaModelsEditModeTests.cs` (19) | мешер/маркеры/битые payload’ы, Alpha↔Unity матрицы, якоря (включая `head_0x65`), разбор клипов и негативные кейсы, таблицы (Boots/Gloves r3, 26/130, двуручные, расходники, регионы), загрузка кэша (human/dragon/whitecat), точные ошибки битого актора и защита от выхода за пределы каталога |
| `Assets/CubeGame/Tests/AlphaModels/PlayMode/AlphaActorPlayModeTests.cs` (5) | человек ходит формулой и проигрывает `action_003` с возвратом в Locomotion; поза при 30 и 120 Гц совпадает; якорь оружия рука↔спина; существо без профиля отказывает в действиях; дракон 0x65 играет `action_037` с якорем головы D83 |

---

## 3. Фактические прогоны (артефакты)

| Прогон | Команда (сокращённо) | Результат |
|---|---|---|
| EditMode | `Unity.exe -batchmode -projectPath D:\Projects\CubeGame-deepseek -runTests -testPlatform EditMode` | `total=315 passed=315 failed=0` (`AlphaOutput/editmode.xml`) |
| PlayMode | то же с `-testPlatform PlayMode` | `total=22 passed=22 failed=0` (`AlphaOutput/playmode.xml`) |
| Материалы | `-executeMethod CubeGame.AlphaModels.Import.AlphaModelsImport.RunCreateVisualResources` | exit 0, `AlphaOutput/resources.txt` |
| Кэш контента | `…RunValidateContent` | `ALPHA_CONTENT_RESULT=PASS`, 5 акторов READY, `ALPHA_TABLES_RESULT=PASS` (`AlphaOutput/content_status.txt`) |
| Кадры | `…RunPreview` | `ALPHA_PREVIEW_RESULT=PASS actors=5 images=12 problems=0` (`AlphaOutput/preview.txt`, PNG в `AlphaOutput/preview`) |
| Таблицы | `python -m tools.alpha_content.build_content_tables --check` | `ALPHA_CONTENT_TABLES=PASS` |
| Раскладка кэша | `python -m tools.alpha_content.stage_local_content` | `STAGE_CONTENT_RESULT=PASS actors=5` |

Проверенные акторы (локальный кэш, в Git не входит):

```
d86_human_0x02_greatsword   rig=alpha_creature/e66d96be5a parts=8 clips=94 actions=93 ACTIONS
d86_dragon_0x65             rig=alpha_creature/95251fe7e5 parts=7 clips=5  actions=4  ACTIONS (D83 head_0x65)
plainrunner_0x3f_formula_vs_alpha parts=3 clips=2 LOCOMOTION_ONLY
whitecat_0x20_formula_vs_alpha    parts=7 clips=2 LOCOMOTION_ONLY
wolf_0x1c_formula_only            parts=7 clips=1 LOCOMOTION_ONLY
```

---

## 4. ALPHA_REFERENCE + DELTA

| Элемент | Источник Alpha / статус | Проверка |
|---|---|---|
| Геометрия `.cub` → меш | D61 marker-правило, D65; `VERIFIED` | `VoxelMesh` тесты (6/10/14 граней), `expectedVertices` в рантайме |
| Матрицы/пространство | row-vector D3D9, swap Y/Z; `DIRECT` | EditMode round-trip, `Decompose` с отрицательным детерминантом |
| Якоря частей | `drawModel` блоки; center/tail/wing/neck/lamp/pet/head_0x65/weapon | EditMode (числа из принятых отчётов), PlayMode (рука ↔ спина) |
| Ходьба | `param_walk` формулы, `FORMULA_VERIFIED` (D59) | PlayMode 30↔120 Гц, изменение позы |
| Действия человека | `action_003` и др. `EMULATED_ALPHA_CODE` (D53/D59) | PlayMode таймлайн и возврат в Locomotion |
| Действия дракона | 37/40/42/43 `EMULATED_ALPHA_CODE`, `dragon_0x65_v1` (D83) | PlayMode `action_037`, якорь `head_0x65` |
| Типы/подтипы/способности/виды | M0-00 confirmed tables, D65/D69 evidence | EditMode counts + r3 Boots/Gloves + 26 passive |
| Анимации существ без профиля | `LOCOMOTION_ONLY` с причиной | PlayMode отказ `ACTIONS_BLOCKED` |

---

## 5. PARTIAL / UNKNOWN (не выдаётся за Alpha)

- **Масштаб скоростей (OQ-23 OPEN):** `speedXY` — исследовательский вход, `PLACEHOLDER_NOT_ALPHA`; визуальная ходьба от него зависит, физика — нет.
- **Амплитуда ходьбы в игре:** follower амплитуды помечен `PLACEHOLDER_NOT_ALPHA` в принятом контроллере; игра должна передавать амплитуду прямо.
- **Масштаб оружия:** кадры показывают крупный greatsword относительно тела; это следствие принятых матриц `drawModel`/клипов, отдельного пересчёта в этой партии не делалось (`PARTIAL`, вопрос к визуальной приёмке).
- **Эффекты/снаряды (R-3), динамические части (R-8), RuneGiant (R-9):** не в этой партии (`BLOCKED` в реестре M0-00).
- **Действия с оружием у NPC (R-1/R-2):** остаются `DEFERRED_TO_M0`; клипы человека содержат оружие за спиной (`weaponOnBack=true` во всех загруженных клипах) — якорь рука↔спина проверен API, но не позой удара.
- **Контент мира (M0-02), AI/спавн (M0-06), города/данжи (M0-07), VFX/аудио (M0-08)** в этой партии только подготовлены таблицами, кода ещё нет.
- **Публикация:** ни один исходный/производный ассет Alpha не закоммичен; `.cubdec`, клипы, PNG и логи — в `.scratch`/`AlphaOutput` (оба в `.gitignore`).

---

## 6. Блокеры

Нет блокеров для M0-01. Для следующих партий заранее известны: `W-18` (таблица климат→объекты), `W-04` (порядок шума рельефа), `F-U5` (торговые наценки), `J-U4/U5` (раскладка mission), `R-1/R-2/R-3/R-8/R-9` — по ним нужны либо адресные эксперименты, либо явный `UNKNOWN`/`PARTIAL` в отчёте, а не догадки.

---

## 7. Следующий модуль

M0-02/M0-06 (world content suppliers + creatures/spawn/AI в зоне DeepSeek) — контракты уже опубликованы в [handoff](D86_DEEPSEEK_HANDOFF_M0_01.md). Ветка не сливается в `main`, `S05B-2` не переоткрывается, принятые S02–S04 не изменялись.
