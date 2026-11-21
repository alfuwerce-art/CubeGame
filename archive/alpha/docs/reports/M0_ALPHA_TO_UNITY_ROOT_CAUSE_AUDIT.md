# M0 — ALPHA → UNITY ROOT CAUSE AUDIT

**Status:** `READY_FOR_PRODUCER_REVIEW`
**Дата:** 2026-11-10
**Ветка:** `review/m0-alpha-unity-root-cause`
**Аудит:** диагностика, без изменения runtime-кода. Ни один этап не считается ACCEPTED.

---

## 0. TL;DR (вывод до деталей)

Оригинальная Cube World Alpha самодостаточна: `Cube.exe` на старте читает `data1–4.db` + `.plx`
из собственного каталога и из **одного и того же** набора данных строит terrain, модели, звуки и UI.

CubeGame построен иначе и именно в этом месте разрывается цепочка:

1. **Terrain** реконструирован процедурно и само́достаточен (код + шейдер закоммичены) — он работает и в Player build.
2. **Все бинарные Alpha-ассеты** (156 акторов, 1554 модели предметов, 180 частей внешности, 93 WAV) **не стали Unity-ассетами**. Они выгружены в локальный файловый кэш `.scratch/content` (и `AlphaOutput/`), который **исключён из Git и не попадает в сборку**.
3. Runtime читает их по путям файловой системы (`AlphaContentSource.Resolve()`). В **Editor** путь `<project>/.scratch/content` существует → модели загружаются. В **Player build** ни одного из путей нет → `ALPHA_CONTENT_NOT_CONFIGURED` → герой = **capsule**, существа = **cube**, лут = **placeholder-cube**, здания = **primitive-cube**, звук = тишина.
4. **Magenta** — runtime-placeholder-материалы создаются через `Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit")`; в Built-in RP со stripped-шейдерами оба возвращают `null` → `new Material(null)` → розовый/магента.
5. **Тесты это не ловят**: визуальные тесты делают `Assert.Ignore`/early-`return`, когда контент-кэш не готов, а `GameVisualSink` складывает пропажу моделей в мягкий список `Problems` и рисует placeholder, не роняя ни один тест. Ни один тест не запускается против **Player build** и не проверяет **отрисованный кадр**.

Правило «не публиковать Alpha assets» было реализовано самым тупым способом — «не класть их в `Assets/` вообще, читать из внешней папки в рантайме»; в результате локальные данные есть, но **build их не получает**.

---

## 1. Инвентаризация `D:\Projects`

| Папка | Назначение | Содержимое | Используется CubeGame? | Read-only? | Важность |
|---|---|---|---|---|---|
| `D:\Projects\CubeGame` | Единственный рабочий репозиторий | `.git`, `Assets/`, `docs/`, `.scratch/`, `AlphaOutput/`, `.unity-install/`, `Library/`, `Packages/`, `ProjectSettings/`, `tools/` | Да (текущий checkout, HEAD = `deepseek/m0-f5-stable-launcher` `44550b8`) | Нет (рабочий) | КРИТИЧНО |
| `D:\Projects\CubeGame-claude` | Изолированный working copy агента Claude (D87/D89) | собственный `.git`, `.scratch/`, `AlphaOutput/`, `Assets/`, `Library/` | Косвенно (история) | Нет | СПРАВОЧНО |
| `D:\Projects\CubeGame-deepseek` | Изолированный working copy агента DeepSeek | собственный `.git`, `.scratch/`, `AlphaOutput/`, `Assets/` | Косвенно (история) | Нет | СПРАВОЧНО |
| `D:\Projects\dsh-acl-recovery` | Артефакты восстановления ACL песочницы DSH | JSON-бэкапы ACL | Нет | Нет | НЕРЕЛЕВАНТНО |
| `D:\Projects\референсы для CudeGame` | **REFERENCE-ONLY** визуальные референсы | `графика/`, `древо навыков/`, `как в целом игра должна выглядеть/`, PNG (гоблин/дворф/орк/скелет/эльф и др.) | Только как референс | **ДА (строго)** | СПРАВОЧНО |

Вне `D:\Projects`, но в области задачи:

| Путь | Назначение | Read-only? | Важность |
|---|---|---|---|
| `D:\Games\Cube world` | **Оригинальная Alpha 2013**: `Cube.exe` (3.9 MB), `data1.db` (20.9 MB), `data2.db` (39.6 MB), `data3.db`, `data4.db`, `*.plx` (start/gui/help/quest-tag/cursor), `Save/`, `patch/`, `Redist/` | **ДА (строго)** | ИСТОЧНИК ИСТИНЫ |
| `D:\Games\CubeGame` | Launcher + релизы сборок | Нет | КРИТИЧНО (сборка F5) |

### Ключевые находки инвентаризации

- Локальный кэш переноса (S05): `D:\Projects\CubeGame\.scratch\content\` содержит
  `actors\` (**156**), `items\` (**1554** `.cubdec`), `appearance\` (**180**), `sounds\` (**93** `.wav`),
  `jobs\`, `projects\`.
- В `Assets\` есть только код и **генерация**: `Content\Generated\*.g.cs` — это **таблицы/каталоги/селекторы**
  (AlphaCharacterModels, AlphaCreatureKindCases, AlphaStagedVisuals, AlphaSoundCatalog и т.д.), т.е. **ID и числа,
  а не бинарные модели**.
- В `Assets\` **нет** `Models/`, `Meshes/`, `Textures/`, `StreamingAssets/`. Единственные ресурсные папки —
  `CubeGame\AlphaModels\Resources\` (2 `.mat`) и `CubeGame\Presentation\Gameplay\Resources\` (USS/TSS UI).
- Оригинальная Alpha найдена в `D:\Games\Cube world` (read-only), данные целы.

---

## 2. Цепочка ORIGINAL ALPHA PIPELINE

`Cube.exe` (единый бинарник) → читает из своего каталога `data1..4.db` (архивы Picroma: воксельные модели,
текстуры, звуки, таблицы) и `*.plx` (UI/террейн/плазма-виджеты) → строит runtime-представление в памяти →
отрисовывает собственным рендером (voxel meshes + per-draw tint) → финальный кадр.

Для всех 23 элементов цепочка одна и та же и **замкнута внутри каталога игры**:

```text
data*.db / *.plx (в каталоге игры)
  → loader Cube.exe
  → runtime object (voxel mesh / clip / sound / widget)
  → renderer (Cube.exe)
  → final screen
```

Точная адресация каждой системы (террейн-генератор, `Creature_drawModel`, таблица видов, item_icon_id,
звуковой диспетчер `0x484350`, Plasma UI) восстановлена в предыдущих этапах (D1–D6, S05) и зафиксирована в
`docs/research/alpha2013/` и `docs/reports/`. Места, где доказательство заканчивается, помечены в
`M0_ALPHA_PARITY_BACKLOG.md` (`UNKNOWN_NEEDS_RESEARCH`).

**Что оригинал делает правильно:** данные и их потребитель лежат рядом, один бинарник, ноль внешних путей,
поэтому «исследование → загрузка → отрисовка» всегда согласованы.

---

## 3. Цепочка CUBEGAME UNITY (и точки разрыва)

Для каждого элемента: `Source Alpha data → extractor → converted → Unity asset → runtime resolver →
visual builder → material/shader → renderer → build inclusion → final screen`.

| Элемент | До converted | Unity asset/data | Runtime resolver | Build inclusion | Результат в Player build |
|---|---|---|---|---|---|
| **terrain** | `AlphaTerrainGenerator` (процедурно, эмулирован) | код + `PilotVertexColor.shader` (закоммичен) | `StreamingWorldView` + `PilotTerrain.mat` (в сцене) | ✅ shader через material в сцене | **OK** (геометрия + vertex-color) |
| **террейн palette/цвет** | voxel-цвета из реконструкции | vertex colors | `PilotVertexColor` | ✅ | **PARTIAL** (эмуляция, не `start.plx`) |
| **персонаж (body/head/face/hair)** | `.cubdec` в `.scratch/content` | ❌ не в Unity | `AlphaContentSource` → путь FS | ❌ нет в build | **fallback capsule** |
| **части персонажа/лицо/волосы** | `.cubdec` (`appearance/`) | ❌ | `AlphaPlayerVisualBuilder` | ❌ | capsule |
| **оружие/броня** | `.cubdec` (`items/`) | ❌ | `AlphaWeaponLayer`/`AlphaEquipmentLayer` | ❌ | не рисуется (нет модели) |
| **NPC** | actor `.cubdec` | ❌ | `BuildCreature` | ❌ | cube |
| **существа (quadruped/flying/insect/…)** | actor `.cubdec` | ❌ | `CreatureVisualCatalog` + `BuildCreature` | ❌ | cube |
| **NPC equipment** | не перенесён (R-1) | ❌ | `DEFERRED_TO_M0` | ❌ | отсутствует |
| **анимации** | `clips/*.json` в `.scratch/content` | ❌ | `AlphaActorView`/`AlphaClipPlayer` | ❌ | отсутствуют (нет актора) |
| **деревья/растения/скалы** | scatter/vegetation → `CreatureVisualCatalog` (нужны actors) | ❌ | `WorldContentStreamer` → spawner | ❌ | cube/нет |
| **здания/города** | `AlphaStructurePlacementPlanner` — только footprint | ❌ | `WorldContentStreamer.SpawnStructure` | n/a | **primitive cube (footprint)** |
| **dungeon objects** | планировщик комнат/коридоров | ❌ | `AlphaDungeonLayoutPlanner` | n/a | primitive cubes |
| **сундуки** | `AlphaWorldContent` | ❌ (мебель `PLACEHOLDER_NOT_ALPHA`) | world-object route | n/a | placeholder |
| **предметы (ground loot)** | `.cubdec` (`items/`) | ❌ | `GameVisualSink.BuildGroundModel` | ❌ | placeholder-cube (magenta при null shader) |
| **projectiles** | `.cubdec` | ❌ | `ProjectileVisuals` | ❌ | sphere (magenta при null shader) |
| **HUD/UI** | UI Toolkit (USS/TSS) | ✅ `Gameplay/Resources` | `GameBootstrap.Interface` | ✅ | **OK** (контракт-дизайн) |
| **карта** | `WorldState` + маркеры | ✅ код | `MenusAndMap` | ✅ | OK (символы `PLACEHOLDER`) |
| **иконки** | атлас из `AlphaOutput/icons` | ❌ (`.png` вне Assets) | `AlphaItemIconRenderer` | ❌ | текст вместо иконок |
| **звуки** | `.wav` в `.scratch/content/sounds` (93) | ❌ | `AlphaSoundLibrary`/`GameAudio` | ❌ | тишина |

**Вывод по таблице:** единственная система, дошедшая до финального экрана в Player build «в настоящем виде» —
это terrain + UI Toolkit. Всё, что должно было прийти из перенесённых Alpha-моделей/звуков, физически
**не включено в сборку**, и рантайм честно сообщает об этом (`DATA_MISSING`) и рисует placeholder.

---

## 4. Главная матрица аудита

| Система | Alpha работает | Данные найдены | Extracted | Converted | Unity import | Runtime connected | Render correct | Build included | Current result | Root cause |
|---|---|---|---|---|---|---|---|---|---|---|
| body | ✅ | ✅ | ✅ | ✅ (`.cubdec`) | ❌ | ✅ (resolver есть) | ❌ | ❌ | capsule | BUILD_PACKAGING (нет в build) |
| head | ✅ | ✅ | ✅ | ✅ | ❌ | ✅ | ❌ | ❌ | capsule | BUILD_PACKAGING |
| hair | ✅ | ✅ | ✅ | ✅ | ❌ | ✅ | ❌ | ❌ | capsule | BUILD_PACKAGING |
| face | ✅ | ✅ | ✅ | ✅ | ❌ | ✅ | ❌ | ❌ | capsule | BUILD_PACKAGING |
| weapon | ✅ | ✅ | ✅ | ✅ | ❌ | ✅ | ❌ | ❌ | нет модели | BUILD_PACKAGING |
| armor | ✅ | ✅ | ✅ | ✅ | ❌ | ✅ | ❌ | ❌ | нет модели | BUILD_PACKAGING |
| animation | ✅ | ✅ | ✅ | ✅ (clips json) | ❌ | ✅ | ❌ | ❌ | нет | BUILD_PACKAGING + ANIMATION_GAP |
| material/color | ✅ | ✅ | ✅ | ✅ | ⚠️ (только `.mat`+shader) | ✅ | ⚠️ | ⚠️ | tint потерян на placeholder | MATERIAL_SHADER_GAP (частично) |
| humanoids NPC | ✅ | ✅ | ✅ | ✅ | ❌ | ✅ | ❌ | ❌ | cube | BUILD_PACKAGING |
| quadrupeds | ✅ | ✅ | ✅ | ✅ | ❌ | ✅ | ❌ | ❌ | cube | BUILD_PACKAGING |
| flying | ✅ | ✅ | ✅ | ✅ | ❌ | ✅ | ❌ | ❌ | cube | BUILD_PACKAGING |
| insects | ✅ | ✅ | ✅ | ✅ | ❌ | ✅ | ❌ | ❌ | cube | BUILD_PACKAGING |
| special body types | ✅ | ✅ | ✅ | ✅ | ❌ | ✅ | ❌ | ❌ | cube | BUILD_PACKAGING |
| NPC equipment | ✅ | ✅ | ❌ (R-1) | ❌ | ❌ | ❌ | ❌ | ❌ | отсутствует | EXTRACTION_GAP (DEFERRED) |
| animations (creatures) | ✅ | ✅ | ✅ | ✅ | ❌ | ✅ | ❌ | ❌ | нет | BUILD_PACKAGING |
| terrain | ✅ | ✅ (recon) | ✅ | ✅ (процедурно) | ✅ | ✅ | ✅ | ✅ | **OK** | — |
| voxel palette/material | ✅ | ⚠️ (эмуляция) | ⚠️ | ✅ | ✅ | ✅ | ⚠️ | ✅ | PARTIAL | CONVERSION_GAP (не `start.plx`) |
| trees | ✅ | ✅ (catalog) | ✅ | ✅ | ❌ | ✅ | ❌ | ❌ | нет/cube | BUILD_PACKAGING |
| vegetation | ✅ | ✅ | ✅ | ✅ | ❌ | ✅ | ❌ | ❌ | нет/cube | BUILD_PACKAGING |
| rocks | ✅ | ✅ | ✅ | ✅ | ❌ | ✅ | ❌ | ❌ | cube | BUILD_PACKAGING |
| structures | ✅ | ✅ (footprint) | ⚠️ | ❌ (геометрия не перенесена) | ❌ | ✅ | ❌ | n/a | primitive cube | CONVERSION_GAP (S05 не покрывал здания) |
| cities | ✅ | ✅ (planning) | ⚠️ | ❌ | ❌ | ✅ | ❌ | n/a | primitive cubes | CONVERSION_GAP |
| dungeons | ✅ | ⚠️ (recon) | ⚠️ | ❌ | ❌ | ✅ | ❌ | n/a | primitive cubes | CONVERSION_GAP |
| containers (сундуки) | ✅ | ⚠️ | ⚠️ | ❌ | ❌ | ✅ | ❌ | n/a | placeholder | CONVERSION_GAP |
| UI | ✅ | n/a | ✅ | ✅ (UI Toolkit) | ✅ | ✅ | ✅ | ✅ | **OK** | — |
| icons | ✅ | ⚠️ | ✅ | ✅ (атлас `.png`) | ❌ | ✅ | ❌ | ❌ | текст | BUILD_PACKAGING |
| map | ✅ | n/a | ✅ | ✅ (код) | ✅ | ✅ | ✅ | ✅ | OK | — |
| audio | ✅ | ✅ (93 WAV) | ✅ | ✅ | ❌ | ✅ | ❌ | ❌ | тишина | BUILD_PACKAGING |

---

## 5. MAGENTA — точная причина

Magenta возникает не «потому что материалы сломаны», а из **runtime-материалов с null-шейдером**:

```text
GameVisualSink.GroundPlaceholder            (Assets\CubeGame\Presentation\Gameplay\GameVisualSink.cs:383-384)
ProjectileVisuals.PlaceholderMaterial       (Assets\CubeGame\Presentation\Gameplay\ProjectileVisuals.cs:256-257)
  Shader shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit");
  _placeholderMaterial = new Material(shader) { ... };
```

Цепочка:

1. Проект — **Built-in RP** (`ProjectSettings/GraphicsSettings.asset`: `m_CustomRenderPipeline: {fileID: 0}`),
   пакет URP не подключён → `"Universal Render Pipeline/Lit"` всегда `null`.
2. `ProjectSettings/GraphicsSettings.asset`: `m_AlwaysIncludedShaders:` **пуст** → в сборку попадают только
   шейдеры, на которые ссылается ассет в сцене/Resources. Проект использует только `PilotVertexColor` (через
   `PilotTerrain.mat` в сцене) и `AlphaTransfer/VoxelShaded` (через `AlphaVoxel.mat` в Resources). **`"Standard"`
   нигде как ассет не задействован** → в Player build он stripped → `Shader.Find("Standard")` возвращает `null`.
3. `new Material(null)` → Unity отрисовывает объект внутренним error-шейдером **ярко-розовым (magenta)**.

Когда это видно: выстрел снаряда (`ProjectileVisuals.Placeholder`) и/или выпавший предмет (`GroundPlaceholder`).
`MakePlaceholder` для героя/существа (`CreatePrimitive` без материала) использует встроенный Default-Material —
он **серый**, не magenta; поэтому в кадре одновременно серые capsule/cube и розовые projectiles/loot.

> Отметка: это путь к magenta, доказанный кодом и настройками проекта. Точное подтверждение «Standard
> stripped» в конкретном билде — запуск `-batchmode` с логированием `Shader.Find` (не выполнялся в аудите,
> т.к. это не чинит игру). Альтернативные источники magenta (потерянный material в сцене) исключены: материал
> террейна в сцене назначен и шейдер включён.

**Итог magenta:** `placeholder-объект → материал, созданный кодом → Shader.Find("Standard"/"URP/Lit") → null →
new Material(null) → розовый`. Причина — **shader stripping + Built-in RP + код, полагающийся на Shader.Find**.

---

## 6. CAPSULE / CUBE PLACEHOLDERS — точная причина

Все примитивы создаются в `GameVisualSink` и `WorldContentStreamer`:

| Объект | Где создаётся | Условие | Основной path или fallback |
|---|---|---|---|
| Герой = capsule | `GameVisualSink.MakePlaceholder(hero:true)` `GameVisualSink.cs:759-768` | `BuildHero` вернул null, т.к. `_library.State != Ready` | **fallback** (основной path — `AlphaPlayerVisualBuilder`) |
| Существо/NPC = cube | `GameVisualSink.MakePlaceholder(hero:false)` | `BuildCreature` вернул null (нет актора) | **fallback** |
| Лут = cube | `GameVisualSink.GroundPlaceholder` | `BuildGroundModel` не нашёл `.cubdec` файл | **fallback** |
| Здание/город = cube | `WorldContentStreamer.SpawnStructure` `WorldContentStreamer.cs:209-217` | **всегда** — структура = footprint | **основной path** (намеренный `PLACEHOLDER_NOT_ALPHA`) |
| Снаряд = sphere | `ProjectileVisuals.Placeholder` | нет `.cubdec` модели снаряда | **fallback** |

**Почему основной asset path не срабатывает (для героя/существ/лута/снарядов):**

1. Модель физически существует: `.scratch/content/actors/<id>/models/*.cubdec`, `.scratch/content/items/*.cubdec`.
2. Она **уже успешно импортировалась ранее** — S05/D69-перенос доказан: 156 акторов, 1554 предметов; в Editor
   превью (`AlphaModelsImport.Preview`) и PlayMode-тесты собирают эти модели в меши.
3. Но runtime (`GameVisualSink`) читает её через `AlphaContentSource.Resolve()` по **файловому пути**.
4. В Player build ни один путь не существует → `library.State == NotConfigured` → `BuildHero/BuildCreature`
   возвращают null → `MakePlaceholder`.

**Ответ на главный вопрос раздела:** модель была импортирована и проверена **в Editor**, но не стала
Unity-ассетом и не включена в сборку; `GameBootstrap` и `GameVisualSink` не получают её в Player build, потому
что источник контента — внешний локальный каталог, а не `Resources`/`StreamingAssets`/AssetBundle.

---

## 7. Разрыв S05 ↔ F1–F4

Проверена связь:

```text
S05 generated/imported asset (.scratch/content actors/items/appearance/clips)
  → F2 visual resolver (AlphaPlayerVisualBuilder / CreatureVisualCatalog / GameVisualSink)
  → GameVisualSink
  → GameBootstrap
  → Player build
```

**Связь существует по коду, но разрывается на границе «сборка».** Конкретное место разрыва:

- `AlphaActorLibrary.Shared` = `new AlphaActorLibrary(AlphaContentSource.Resolve())` (`AlphaActorLibrary.cs:17`).
- `AlphaContentSource.FindRoot()` (`AlphaContentSource.cs:115-136`) ищет: explicit root → `CUBEGAME_ALPHA_CONTENT`
  → `<project>/.scratch/content` → `persistentDataPath/CubeGame/AlphaContent`.
- `GameBootstrap.BuildWorldViews()` вызывает `_visuals.Initialise(AlphaActorLibrary.Shared, …)`
  (`GameBootstrap.cs:1020`) **без explicit root**.
- В Editor срабатывает `<project>/.scratch/content` (существует). В Player build — ни один путь не существует,
  т.к. `.scratch/content` не копируется в `persistentDataPath/CubeGame/AlphaContent` и не входит в релиз.

То есть S05 asset pipeline **не подключён к gameplay runtime в том смысле, что его выходные данные не доезжают
до рантайма в собранной игре**. Это не отсутствие классов — классы есть и работают в Editor; это разрыв
**доставки данных в build**.

---

## 8. BUILD против EDITOR

**Подтверждено: Editor и Player build получают разные ресурсы.**

Доказательство из Player build (F5):

- `Player.log` (`%USERPROFILE%\AppData\LocalLow\CubeGame\CubeGame Pilot\Player.log`):
  `DATA_MISSING alpha content cache: ALPHA_CONTENT_NOT_CONFIGURED (set CUBEGAME_ALPHA_CONTENT or place the cache at <project>/.scratch/content)`.
- Релиз `D:\Games\CubeGame\releases\60f24be\` содержит только стандартный Unity build
  (`CubeGame_Data`, `MonoBleedingEdge`, `D3D12`, `CubeGame.exe`, `UnityPlayer.dll`). Каталога `AlphaContent` **нет**.
- `persistentDataPath` (`...\CubeGame Pilot\CubeGame\`) содержит только `saves\` — `AlphaContent` **отсутствует**.

Причины расхождения:

| Механизм | Статус |
|---|---|
| `.scratch/` (локальный кэш контента) | gitignored, не в сборке |
| `AlphaOutput/` (иконки/превью) | gitignored, не в сборке |
| `*.cubdec` / `*.cub` / `*.wav` / `data*.db` | gitignored |
| `Resources/` | содержит только 2 `.mat` (материалы), **не модели** |
| `StreamingAssets/` | **отсутствует** |
| external absolute paths | `AlphaContentSource` использует пути, вычисляемые от `Application.dataPath`/`persistentDataPath`, а не absolute |
| `#if UNITY_EDITOR` / `AssetDatabase` | только в `*Import`/`Editor` (превью/иконки/материалы), не в runtime-пути визуала |
| runtime conversion | `VoxelMesh.Build` из байтов `.cubdec` — нужен файл на диске, в build его нет |

Вывод: **в Editor данные доступны локально, в Player build они не попадают** — подтверждено.

---

## 9. Asset publication policy vs runtime

Принятое правило «не коммитить и не публиковать Alpha assets» было выполнено **архитектурно неверно**:

- данные исследованы ✅;
- конвертеры есть ✅;
- модели локально есть (`.scratch/content`) ✅;
- но build **не получает их вообще** ❌;
- поэтому runtime показывает placeholders ❌.

**Правильная архитектура (описание, без реализации в этом аудите):**

1. Локальные Alpha-ассеты используются для **development/build**, но **не попадают в Git**.
2. Во время сборки (`ReleaseBuild`) копировать подготовленный локальный кэш `AlphaContent` в
   `Assets/StreamingAssets/CubeGame/AlphaContent` (или разворачивать в `persistentDataPath` на первом запуске),
   а рантайм читает его через `Application.streamingAssetsPath`/`persistentDataPath`.
3. `StreamingAssets` при этом остаётся в `.gitignore` (или заполняется только на машине сборки), чтобы исходные
   Alpha-производные не публиковались.
4. Build-шаг делает **жёсткую проверку**: если `AlphaContent` не staged → сборка падает (gate `CONTENT_STAGED`),
   а не собирает «стенд с placeholder».

---

## 10. Сравнение с оригиналом (сценарии)

Оригинал: `D:\Games\Cube world` (read-only, не запускался в рамках аудита — достаточно статики и каталога).
CubeGame: Windows build `D:\Games\CubeGame\releases\60f24be\CubeGame.exe` + `Player.log`.

### Scenario A — новый персонаж возле стартового города

```text
Alpha:  terrain + palette, grass, trees, buildings, NPC models, player model, weapon, animations, shadows, UI
Unity:  terrain geometry = YES
        terrain palette = PARTIAL (эмуляция vertex-color)
        grass = NO
        trees = NO (нет actors в build)
        building geometry = PLACEHOLDER (primitive cube footprint)
        NPC model = NO (fallback cube)
        player model = fallback capsule
        weapon = NO (нет items в build)
        animation = NO (нет clips в build)
        shadows = YES (built-in RP)
        UI = YES (UI Toolkit)
```

### Scenario B — персонаж + оружие + NPC

```text
Alpha:  player model + weapon + NPC model + NPC equipment + animations
Unity:  player model = capsule
        weapon = NO
        NPC model = cube
        NPC equipment = NO (R-1 не перенесён)
        animation = NO
```

### Scenario C — обычный противник (Wolf 0x1c)

```text
Alpha:  wolf voxel model + walk/idle/attack + AI
Unity:  wolf logic = YES (spawn/combat/AI работают)
        wolf model = fallback cube (DATA_MISSING)
        wolf animation = NO
        projectile/эффект = sphere (magenta при null shader)
```

### Scenario D — город / здание / растительность

```text
Alpha:  дома с геометрией, растительность, деревья, улицы
Unity:  house geometry = PLACEHOLDER (cuboid footprint, "interior/parts UNKNOWN")
        vegetation = NO (нужны actors, их нет в build)
        trees = NO
```

---

## 11. Тестовая стратегия — почему сотни PASS, а игра не перенесена

Найденные причины:

1. **Визуальные тесты пропускаются при отсутствии контента.** Паттерн:
   - `Tests\Visual\PlayMode\AlphaVisualPlayModeTests.cs:20,50,82,…` →
     `if (source.State != AlphaContentState.Ready) Assert.Ignore(source.Diagnosis);`
   - `Tests\FullM0\PlayMode\FullM0CombatPlayModeTests.cs:60` →
     `if (AlphaActorLibrary.Shared.State != AlphaContentState.Ready) { … return; }` (ранний выход мимо визуальных assert).
   - `Tests\FullM0\PlayMode\FullM0CharacterRuntimePlayModeTests.cs:61` — аналогично.
2. **`GameVisualSink` превращает пропажу модели в мягкий список `Problems`**, а не в ошибку:
   `Problems.Add("DATA_MISSING …")` (`GameVisualSink.cs:93,337,704`), игра продолжает работать, placeholder рисуется,
   ни один тест не падает.
3. **Тесты проверяют логику/числа/события/persistence** (spawn-таблицы, combat math, ID, save/load), а не
   **отрисованный кадр**. «Visual available» проверяется только при условии готового контента.
4. **Ни один тест не запускается против Player build.** Все PlayMode идут в Editor, где `.scratch/content` есть,
   поэтому модели собираются и «всё зелёное», а расхождение Editor↔build не детектируется.

**Недостающие acceptance gates (предложение, без реализации в аудите):**

- `NO_MAGENTA` — сканирование рендер-текстуры на «розовые» пиксели (error shader).
- `NO_PRIMITIVE_PLACEHOLDER_FOR_KNOWN_ASSET` — если для kind/предмета есть staged модель, примитив недопустим.
- `PLAYER_MODEL_VISIBLE` / `WEAPON_MODEL_VISIBLE` / `NPC_MODEL_VISIBLE` / `CREATURE_MODEL_VISIBLE`.
- `VEGETATION_VISIBLE` / `CITY_VISIBLE`.
- `MATERIALS_VALID` — ни один `Renderer.sharedMaterial.shader` не `null`/error.
- `ANIMATION_VISIBLE`.
- `CONTENT_STAGED` — билд падает, если `AlphaContent` не поставлен в StreamingAssets.
- Screenshot comparison (build vs эталон) + ручная визуальная приёмка продюсера.

---

## 12. Root Cause Tree (реальные причины)

```text
BAD FINAL VISUAL (Player build)
|
+-- ASSET PIPELINE РАЗОРВАН НА ГРАНИЦЕ BUILD
|   +-- Alpha-модели/звуки есть локально: .scratch/content (156 actors, 1554 items, 180 appearance, 93 wav) = YES
|   +-- но они gitignored (.gitignore: .scratch/, *.cubdec, *.wav, AlphaOutput/)
|   +-- и НЕ импортированы в Assets (нет Models/StreamingAssets)
|   +-- runtime читает их по файловым путям (AlphaContentSource.Resolve)
|   |   +-- Editor: <project>/.scratch/content существует → модели ОК
|   |   +-- Player build: ни одного пути нет → ALPHA_CONTENT_NOT_CONFIGURED
|   +-- → hero=capsule, creatures=cube, loot=placeholder, audio=тишина
|
+-- ЗДАНИЯ/ДЕРЕВЬЯ/ДАНЖИ — PLACEHOLDER ПО ДИЗАЙНУ
|   +-- S05 перенёс акторов (существа/игрок), НЕ геометрию структур
|   +-- WorldContentStreamer.SpawnStructure = CreatePrimitive(Cube) = "PLACEHOLDER_NOT_ALPHA"
|   +-- деревья/растительность = actors → тоже не в build
|
+-- MAGENTA
|   +-- placeholder-материалы через Shader.Find("Standard") ?? Shader.Find("URP/Lit")
|   +-- Built-in RP + m_AlwaysIncludedShaders пуст → shader stripped
|   +-- → Shader.Find == null → new Material(null) → розовый
|
+-- ACCEPTANCE PROCESS
    +-- тесты проверяют логику/числа/persistence
    +-- визуальные тесты Assert.Ignore / early-return при отсутствии контента
    +-- GameVisualSink: DATA_MISSING = мягкий Problems, не failure
    +-- нет теста против Player build / отрисованного кадра
    +-- → сотни PASS, а игра визуально не перенесена
```

---

## 13. Классификация дефектов

| # | Дефект | TYPE | SEVERITY | REPAIR COST |
|---|---|---|---|---|
| 1 | Alpha-контент (модели/риги/клипы) не попадает в Player build | BUILD_PACKAGING_GAP | BLOCKER | SMALL–MEDIUM |
| 2 | Звук (93 WAV) не попадает в Player build | BUILD_PACKAGING_GAP | CRITICAL | SMALL |
| 3 | Иконки/атлас не попадают в Player build | BUILD_PACKAGING_GAP | MAJOR | SMALL |
| 4 | Magenta из null-shader placeholder-материалов | MATERIAL_SHADER_GAP | CRITICAL | SMALL |
| 5 | Здания/города/данжи — primitive footprint вместо геометрии | CONVERSION_GAP | MAJOR | LARGE |
| 6 | Деревья/растительность/скалы — нет в build (зависят от actors) | BUILD_PACKAGING_GAP | MAJOR | SMALL (после #1) |
| 7 | NPC equipment (R-1) не перенесён | EXTRACTION_GAP | MAJOR | MEDIUM |
| 8 | Анимации не видны в build (клипы не доставлены) | ANIMATION_GAP | CRITICAL | SMALL (после #1) |
| 9 | Тесты не гейтят отрисованный кадр / Player build | TEST_COVERAGE_GAP | CRITICAL | SMALL–MEDIUM |
| 10 | Архитектура «внешний локальный кэш вместо Unity-ассетов/StreamingAssets» | ARCHITECTURE_GAP | BLOCKER | MEDIUM |
| 11 | Террейн-палитра — эмуляция, не `start.plx` | CONVERSION_GAP | MINOR | RESEARCH_REQUIRED |

---

## 14. WHY ALPHA WORKS / WHY CUBEGAME DOES NOT

1. **Что оригинал делает правильно:** `Cube.exe` и его данные (`data1–4.db`, `*.plx`) лежат в одном каталоге;
   одна программа загружает всё и отрисовывает всё. Нет разделения «данные отдельно / рантайм отдельно», нет
   внешних путей, нет шанса, что «данные исследованы, но build их не видит».

2. **Какие из этих данных/систем у нас уже есть:**
   - Исследование и конвертация: `.cub` → `.cubdec`, риги, клипы, селекторы моделей, каталоги видов, spawn/combat/
     world-правила — всё это выполнено (S05/D1–D6) и зафиксировано в коде-таблицах `Content/Generated/*.g.cs`.
   - Terrain-реконструкция, game loop (F1–F3), UI Toolkit (F4), combat/progression/persistence — работают.
   - Локальный кэш переноса физически существует: 156 акторов, 1554 модели предметов, 180 частей внешности, 93 WAV.

3. **Какие данные действительно отсутствуют:** геометрия зданий/городов/данжей/мебели (S05 перенёс акторов, а не
   структуры; структуры — только footprint), NPC-экипировка R-1, часть оригинальных иконок/Plasma-виджетов,
   точный `start.plx`-террейн. Это настоящие `CONVERSION_GAP`, а не «потерянные» данные.

4. **Какие данные есть, но не используются:** **почти все** — 156 акторов, 1554 предмета, 180 частей внешности,
   93 WAV, атлас иконок. Они есть локально, но не включены в сборку и не видны в Player build.

5. **Где runtime заменяет их placeholder:** `GameVisualSink.MakePlaceholder` (hero=capsule, creature=cube),
   `GameVisualSink.GroundPlaceholder` (loot=cube), `WorldContentStreamer.SpawnStructure` (здание=cube),
   `ProjectileVisuals.Placeholder` (снаряд=sphere). Условие — отсутствие контента/модели в рантайме.

6. **Почему материалы magenta:** `Shader.Find("Standard") ?? Shader.Find("URP/Lit")` в placeholder-материалах
   возвращает null в Built-in RP build со stripped-шейдерами → `new Material(null)` → розовый error-shader.

7. **Почему предыдущие этапы считались успешными:** они проверялись **в Editor**, где `.scratch/content`
   существует, и **тестами логики/чисел/persistence**, а не отрисованным кадром Player build. `GameVisualSink`
   сообщает о пропаже модели мягко (`Problems`/`DATA_MISSING`), а не падением, поэтому «755 EditMode PASS +
   73 PlayMode PASS» не означало «в билде видно Alpha».

8. **Сколько реально осталось до визуально узнаваемой Alpha-базы:** **мало** — блокер не в отсутствии данных,
   а в их доставке. Один шаг (поставить локальный контент в сборку через StreamingAssets + исправить
   null-shader placeholder) возвращает героя, оружие, NPC, существ, предметы, снаряды, звук и анимации сразу.
   Затем отдельно (более трудоёмко) — геометрия зданий/деревьев/данжей.

---

## 15. План восстановления (Vertical Slice first)

```text
R0  Поставить локальный кэш в сборку:
    ReleaseBuild копирует .scratch/content → Assets/StreamingAssets/CubeGame/AlphaContent
    (StreamingAssets остаётся в .gitignore); runtime читает Application.streamingAssetsPath.
    → возвращает героя/существ/предметы/снаряды/звук/анимации в Player build.

R1  Убрать magenta: заменить Shader.Find("Standard"/"URP/Lit") на закоммиченный fallback-шейдер
    (или Resources-материал) в GameVisualSink.GroundPlaceholder и ProjectileVisuals.PlaceholderMaterial.

R2  Build gate CONTENT_STAGED: билд падает, если AlphaContent не staged в StreamingAssets.

R3  Вернуть иконки/атлас в StreamingAssets (тот же механизм, что R0).

R4  Acceptance gates на отрисованный кадр Player build:
    NO_MAGENTA, NO_PRIMITIVE_PLACEHOLDER_FOR_KNOWN_ASSET, PLAYER_MODEL_VISIBLE, WEAPON_MODEL_VISIBLE,
    NPC_MODEL_VISIBLE, CREATURE_MODEL_VISIBLE, VEGETATION_VISIBLE, CITY_VISIBLE, MATERIALS_VALID,
    ANIMATION_VISIBLE + скриншот-сравнение.

R5  Vertical slice: terrain + реальный герой + реальное оружие + 1 реальный NPC + 1 реальный противник +
    дерево/растительность + одно здание + рабочий HUD. Приёмка продюсера (визуально).

R6  Только после R5 — масштабирование на весь контент (156 видов, предметы, города, данжи).

R7  Отдельный трек: геометрия структур/деревьев/данжей (CONVERSION_GAP) — новый перенос S05-structures.
```

---

## 16. Состояние веток (наблюдаемый факт)

- `origin/integration/m0-all` = `0533698` (F3 принят + docs cleanup) — **canonical (GitHub)**.
- `codex/m0-f4-presentation` = `60f24be` — **F4 candidate, НЕ ACCEPTED** (не в canonical).
- `deepseek/m0-f5-stable-launcher` = `44550b8` — F5 launcher + первый Windows build **поверх F4**.
- Локальный checkout: `deepseek/m0-f5-stable-launcher` `44550b8`; локальный `integration/m0-all` = `4b94808`
  (отстаёт от remote `0533698` — локальная ссылка не обновлена, GitHub — источник истины).

Сборка, которую видел продюсер: `D:\Games\CubeGame\releases\60f24be\CubeGame.exe` (F4-код + F5-инфраструктура).
