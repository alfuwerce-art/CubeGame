# S01-PILOT — отчёт по первому Unity-прототипу

**Проект:** CubeGame (рабочее название).
**Этап:** S01-PILOT (D40, ТЗ `docs/stages/S01_UNITY_PILOT.md`).
**Ветка:** `stage/s01-unity-pilot`.
**Дата:** 2026-11-03, MSK.
**Статус:** `READY_FOR_REVIEW` — все проверки этапа выполнены реальным запуском Unity
6000.3.27f2: импорт/компиляция, 10/10 EditMode, 2/2 PlayMode, сцена, Windows-плеер,
запуск плеера со скриншотом. Единственный незакрытый пункт — URP (см. §7).

## 1. Сводка

| Требование S01-PILOT | Состояние | Доказательство |
|---|---|---|
| P0: Unity 6.3 LTS установлена | **PASS** | `unity editors path 6000.3.27f2` → `D:\Unity\Hub\Editor\6000.3.27f2`; `Unity.exe` запускается |
| P0: лицензия Unity Personal активна | **PASS** | `unity license status --json`: `active=true`, `signedIn=true`, `licenses=Unity Personal, Asset Store` |
| P0: проект в корне репозитория, без вложенного проекта | **PASS** | `Assets/`, `Packages/`, `ProjectSettings/` в корне; `.gitignore` дополнен |
| P0: зафиксирована точная версия Editor | **PASS** | `ProjectSettings/ProjectVersion.txt` = `6000.3.27f2`, в логе `Initialize engine version: 6000.3.27f2 (22188f149405)` |
| P1.1: границы `Core` ↔ `Presentation` | **PASS** | `CubeGame.Core.asmdef` с `noEngineReferences: true`; `CubeGame.Presentation` отдельно |
| P1.2: детерминированный voxel-регион, объединённые меши | **PASS** | EditMode 10/10; 8×8 чанков = 486 088 вершин, один меш на чанк 32×32 |
| P1.3: герой, движение, коллизия | **PASS** | PlayMode `PlayerStandsOnGeneratedTerrain`; HUD в плеере: `block 128,61,128` на поверхности |
| P1.4: камера третьего лица | **PASS** (визуально) | скриншот плеера: вид от третьего лица, капсула на террейне |
| P1.5: HUD seed/координаты/FPS | **PASS** | скриншот: `seed 666666`, `block 128, 61, 128`, `chunk 4, 4`, `fps 168.4 (5.9 ms)` |
| P1.6: без NPC/квестов/боя/сейвов/Blender/Alpha-ассетов | **PASS** | в проекте нет таких систем и ассетов |
| P2: открывается без ошибок компиляции | **PASS** | импорт exit=0, в логе нет `error CS` / `Scripts have compiler errors` |
| P2: EditMode-тесты (детерминизм) | **PASS** | `editmode.xml`: total=10, passed=10, failed=0 |
| P2: PlayMode (герой/камера/земля/коллизия) | **PASS** | `playmode.xml`: total=2, passed=2, failed=0 |
| P2: демонстрация и скриншот своей сцены | **PASS** | `Pilot.unity` (10 092 Б), `pilot_run.png` 1920×1080, 188 847 Б |
| P2: профиль (время генерации, FPS, RAM) | **частично** | генерация 461.2 мс; FPS 168.4 инстант / 31.2 в среднем за 120 кадров; RAM не измерена |
| P2: ветка и отчёт | **PASS** | эта ветка и этот отчёт |

## 2. Окружение (проверено фактически)

| Компонент | Значение | Как проверено |
|---|---|---|
| Unity CLI | `1.0.0-beta.13` | `unity --version` |
| Editor для проекта | `6000.3.27f2` (revision `22188f149405`) | `unity editors path 6000.3.27f2`, лог импорта |
| Путь Editor | `D:\Unity\Hub\Editor\6000.3.27f2\Editor\Unity.exe` | `Test-Path`, запуск |
| Лицензия | Unity Personal, `active=true`, `signedIn=true`, oauth | `unity license status --json` |
| Unity 6.6 | CLI показывает `6000.6.7f1` в `editors --installed`; в `D:\Unity\Hub\Editor` каталога 6.6 нет | `unity editors --installed`; **не использовалась** |
| Unity CLI update | доступен beta.15, **не обновлялся** | уведомление CLI |

Проект: `Pipeline=false` (`unity pipeline list`) — пакет Pipeline не установлен, см. §8.

## 3. Что реально выполнено

Команда (обычный PowerShell, без администратора):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\unity\run_pilot.ps1
```

Итог последнего прогона: **все шаги PASS, exit 0** (`UNITY-INSTALL`-артефакты в
`.unity-install\`, каталог в `.gitignore`).

| Шаг | Время | Доказательство |
|---|---|---|
| Импорт + компиляция | 16.2 с | exit 0, `Initialize engine version: 6000.3.27f2`, `script compilation time 0.647 s`, ошибок CS нет |
| EditMode через Unity CLI | 138.2 с | `total=10 passed=10 failed=0 result=Passed` |
| PlayMode через Unity CLI | 138.2 с | `total=2 passed=2 failed=0 result=Passed` |
| Сборка сцены | 16.2 с | `[PilotScene] SCENE_SAVED Assets/Scenes/Pilot.unity`; файл 10 092 Б |
| Сборка Windows-плеера | 17.2 с | `[PilotBuild] BUILD_SUCCEEDED ... size=667136B`; `CubeGamePilot.exe` + `CubeGamePilot_Data` |
| Запуск плеера | 5.3 с | `[Pilot] screenshot requested ... frames=120 ... fps≈31,2`, `[Pilot] quitting`; PNG 188 847 Б |

EditMode-кейсы (все Passed): `SameSeedProducesIdenticalHeights_TC_GEN_01`,
`DifferentSeedChangesHeights_TC_GEN_02`, `AdjacentChunksShareEdgeHeights_TC_GEN_03`,
`HeightsStayInsideConfiguredRange`, `NegativeCoordinatesAreSupported`,
`SameSeedProducesIdenticalMesh`, `DifferentSeedProducesDifferentMesh`,
`IndicesStayInsideVertexBuffer`, `ChunkEdgesAreContinuousBetweenNeighbours`,
`MeshAreaMatchesChunkSize`.

PlayMode-кейсы: `WorldViewBuildsChunksWithColliders` (0.036 с) — 4 чанка с `MeshRenderer` +
`MeshCollider`; `PlayerStandsOnGeneratedTerrain` (1.153 с) — персонаж падает с +12 блоков и
встаёт на сгенерированный террейн.

## 4. Найденные и исправленные дефекты

### 4.1 Ошибки компиляции (первый реальный импорт)

Первый запуск Unity дал `Scripts have compiler errors`, return code 1:

1. `UnityMeshFactory.Create` присваивал `float[]` в `mesh.vertices/normals/colors` —
   `CS0029` ×3. Исправлено: конвертация в `Vector3[]`/`Color[]` (`Assets/CubeGame/Presentation/Rendering/UnityMeshFactory.cs`).
2. `ScreenCapture` не существовал — в `Packages/manifest.json` не был подключён встроенный
   модуль `com.unity.modules.screencapture` (`CS0103`). Модуль добавлен.
3. Предупреждение `CS0108` (`DestroyObject` скрывает `Object.DestroyObject`) — метод
   переименован в `DestroyTracked` (`Assets/CubeGame/Presentation/Rendering/PilotWorldView.cs`).

### 4.2 Ложные PASS в `tools/unity/run_pilot.ps1`

Скрипт переписан, чтобы PASS подтверждался доказательствами:

- лицензия проверяется через `unity license status --json`, а не по «в логе нет ошибки»;
- `AcceptExitCodes @(0,2)` для тестов убран: PASS = exit 0 **и** разбор NUnit XML
  (`total > 0`, `failed = 0`, `passed > 0`);
- сборка сцены/плеера подтверждается маркерами `SCENE_SAVED` / `BUILD_SUCCEEDED` и файлами,
  а не только exit code;
- добавлен `Assets/CubeGame/Editor/BatchMode.cs`: ошибочные ветки `PilotBuild`/`PilotSceneBuilder`
  теперь завершают batchmode с кодом 1, а не логируют ошибку и выходят с 0;
- версия Editor сверяется дважды: `ProjectVersion.txt` и `Initialize engine version` в логе;
  при несовпадении проект не открывается;
- старый скриншот удаляется перед запуском плеера (иначе прошлый прогон давал PASS);
- пустой `$LASTEXITCODE` после `& Unity.exe` больше не используется: Editor запускается через
  `Start-Process -Wait`, плеер — через `&` с ожиданием маркера `[Pilot] quitting`;
- скрипт сделан **ASCII-only**: PowerShell 5.1 читает UTF-8 без BOM как ANSI, и кириллица
  ломала разбор файла (дважды воспроизведено).

### 4.3 Дефекты, найденные PlayMode-тестом (продуктовые)

1. **Телепорт персонажа в (0,0,0) на первом кадре.** `CharacterController` хранит собственную
   позицию в PhysX; `PlayerMotor.Update` вызывал `Move()` до синхронизации трансформа, и
   контроллер откатывался в (0,0,0). Исправлено: `Physics.SyncTransforms()` после установки позиции
   в `PilotBootstrap.BuildScene` и в тесте.
2. **Мерцание `isGrounded` при высоком FPS.** CharacterController держит skin width 0.08 м;
   при `deltaTime≈0.0001` (batchmode PlayMode ≈10 000 fps) шаг вниз меньше зазора, и
   `isGrounded` становится `false`, хотя персонаж стоит на земле (ломает прыжок/шаг).
   Исправлено: `PlayerMotor.IsGrounded` — сначала `controller.isGrounded`, затем короткий
   downward `Physics.Raycast` (0.25 м) при `verticalSpeed <= 0`. Тест проверяет именно это
   продуктовое состояние, а не сырой флаг PhysX.

Оба дефекта подтверждены логами и повторным прогоном; после исправления PlayMode 2/2 PASS.

## 5. Метрики (реальные, из лога плеера)

```
[Pilot] seed=666666 chunks=64 vertices=486088 gen=461,2ms spawn=(128,61,128)
[Pilot] screenshot requested ... frames=120 time=3,84s fps≈31,2
```

- Генерация 64 чанков (256×256 блоков, 486 088 вершин): **461.2 мс** (~7.2 мс/чанк).
- HUD на скриншоте: **168.4 FPS (5.9 мс/кадр)** — мгновенное значение после прогрева.
- Среднее за первые 120 кадров: **31.2 FPS** — включает старт, загрузку сцены и генерацию,
  поэтому не является установившимся FPS. Для честного профиля нужен отдельный замер
  (например, 600 кадров после прогрева).
- RAM: **не измерена** (нет счётчика в пилоте). Не выдумывается.

## 6. Что написано/изменено

| Файл | Роль |
|---|---|
| `Assets/CubeGame/Core/**` | детерминированная генерация и меши без UnityEngine |
| `Assets/CubeGame/Presentation/**` | мир, герой, камера, HUD, шейдер, загрузка мешей |
| `Assets/CubeGame/Editor/PilotSceneBuilder.cs`, `PilotBuild.cs`, `BatchMode.cs` | сборка сцены/плеера с честным кодом возврата |
| `Assets/Scenes/Pilot.unity`, `Assets/CubeGame/Materials/PilotTerrain.mat` | сгенерированная сцена и материал (закоммичены для воспроизводимости) |
| `Assets/CubeGame/Tests/EditMode/*`, `Tests/PlayMode/*` | 10 + 2 теста |
| `Packages/manifest.json`, `Packages/packages-lock.json` | зафиксированы пакеты; добавлен `com.unity.modules.screencapture` |
| `ProjectSettings/*` | сгенерированные настройки проекта (кроме `ProjectVersion.txt`, он был раньше) |
| `tools/unity/run_pilot.ps1` | строгая проверка без ложных PASS |
| `tools/unity/RUN_INSTRUCTIONS.md` | точная команда и правила чтения результатов |
| `docs/agents/UNITY_PIPELINE_PROPOSAL.md` | предложение по Pipeline (§8) |

Все `.meta` для `Assets/**` сгенерированы Unity и закоммичены, чтобы GUID'ы были стабильны.
Ни одного файла, шейдера, текстуры или строки кода из Alpha 2013 / Reversal не добавлено.

## 7. Отклонения и ограничения

- **URP не подключён.** Пилот работает на Built-in Render Pipeline с собственным шейдером
  `CubeGame/PilotVertexColor`. Причина: добавление `com.unity.render-pipelines.universal`
  меняет рендер-пайплайн и требует отдельного решения/проверки; на результат P2 это не влияет.
  Следующий шаг — согласовать URP отдельно и добавить URP-вариант шейдера.
- **RAM не измерена** (см. §5).
- **Unity 6.6** зарегистрирована в списке CLI, но не использовалась; путь
  `D:\Unity\Hub\Editor\6000.6.7f1` отсутствует. Проект жёстко привязан к `6000.3.27f2`.
- В логе плеера есть `d3d12: Error when writing PSO binary data ...` — предупреждение
  драйвера о кэше PSO, на геймплей и проверки не влияет.
- Локально остался пустой каталог `Assets/CubeGame/Core/Domain` с `.meta` от прошлой сессии;
  в коммит не включён (папка пустая, кода в ней нет).
- `docs/ACTIVE_STAGE.md` на `origin/main` всё ещё говорит `BLOCKED_UNITY_INSTALL`; этот отчёт
  фиксирует фактическое состояние, но статус в ACTIVE_STAGE обновляет продюсер.

## 8. Pipeline: предложение, не установка

Пакет `com.unity.pipeline` **не установлен** (`PIPELINE=NOT_INSTALLED`). Предложение с точными
изменениями (`Packages/manifest.json` + `packages-lock.json`), командой установки, проверкой и
откатом — в [UNITY_PIPELINE_PROPOSAL.md](../agents/UNITY_PIPELINE_PROPOSAL.md). Актуальная
версия пакета по CLI — `0.9.1-exp.1` (experimental), требует явного разрешения продюсера.

## 9. Следующие шаги

1. Продюсер: принять/отклонить отчёт и решить по URP и Pipeline.
2. При согласии — подключить URP на этой же ветке, добавить URP-шейдер, перепроверить
   EditMode/PlayMode и сборку.
3. Добавить честный профиль: 600 кадров после прогрева, RAM, время кадра CPU/GPU.
4. Ручная проверка демонстрации по `tools/unity/RUN_INSTRUCTIONS.md` (WASD/Shift/Space/мышь).
