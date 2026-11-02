# S01-PILOT — отчёт по первому Unity-прототипу

**Проект:** CubeGame (рабочее название).
**Этап:** S01-PILOT (D40, ТЗ `docs/stages/S01_UNITY_PILOT.md`).
**Ветка:** `stage/s01-unity-pilot` (от `origin/main` = `b1a7c70`).
**Дата:** 2026-11-02, MSK.
**Статус:** `IN_PROGRESS / TESTS_NOT_RUN` — код и сцена подготовлены, прогон Unity **не выполнен**. Итоговый статус появится после прогона `tools/unity/run_pilot.ps1`.

## 1. Сводка

| Требование S01-PILOT | Состояние |
|---|---|
| P0: официальные Unity Hub + Unity 6.3 LTS + URP | Hub 3.22.5 и Editor 6000.3.27f2 **установлены** (см. §2); URP **не подключён** — требует работы Package Manager, будет добавлен после первого успешного прогона |
| P0: проект в корне репозитория, без вложенного проекта | **сделано**: `Assets/`, `Packages/`, `ProjectSettings/` в корне; `.gitignore` дополнен |
| P1.1: границы `World/Core` ↔ `Presentation` | **сделано**: две сборки `CubeGame.Core` (`noEngineReferences: true`) и `CubeGame.Presentation` |
| P1.2: детерминированный voxel-регион от seed, объединённые меши | **сделано** (код): `PilotHeightField` + `ChunkMeshBuilder` (один меш на чанк 32×32) |
| P1.3: временный герой, движение по земле, коллизия | **сделано** (код): `PlayerMotor` на `CharacterController` + `MeshCollider` чанков |
| P1.4: камера третьего лица (мышь, следование) | **сделано** (код): `OrbitCameraController` |
| P1.5: HUD seed/координаты/FPS | **сделано** (код): `PilotDebugHud` |
| P1.6: без NPC/квестов/боя/сейвов/Blender/Alpha-ассетов | **соблюдено** |
| P2: открывается без ошибок компиляции | **NOT_RUN** — нужен прогон Unity |
| P2: EditMode-тесты (детерминизм) | **NOT_RUN** — тесты написаны (12 кейсов), не запускались |
| P2: PlayMode (герой/камера/земля/коллизия) | **NOT_RUN** — 2 PlayMode-кейса написаны |
| P2: демонстрация + скриншоты своей сцены | **NOT_RUN** — сцена и плеер собираются скриптом |
| P2: профиль (время генерации, FPS, RAM) | **NOT_RUN** — метрики выводятся в HUD и лог плеера |
| P2: ветка и отчёт | **сделано** (ветка + этот отчёт); push не выполнялся (см. R01 §4.2) |

Честная формулировка: **ни один тест ещё не проходил**. Код не компилировался, потому что Unity не может запуститься из песочницы агента (см. §3).

## 2. Что реально установлено (проверено)

| Компонент | Версия | Путь | Проверка |
|---|---|---|---|
| Unity Hub | 3.22.5 (Unity Technologies) | `D:\Unity Hub\Unity Hub.exe` | `FileVersion 3.22.5`; `Get-AuthenticodeSignature` = Valid, `CN=Unity Technologies SF` |
| Unity Editor | 6000.3.27f2 (LTS, revision 22188f149405) | `D:\Unity\Hub\Editor\6000.3.27f2\Editor\Unity.exe` | `Unity.exe -version` → `6000.3.27f2`; подпись Valid, `CN=Unity Technologies SF` |
| Установщик Hub | `UnityHubSetup-x64.exe`, 193 082 648 Б, SHA-256 `C0C2040C…EB2C8CCD` | `.unity-install\` (в `.gitignore`) | официальный CDN Unity |
| Установщик Editor | `UnitySetup64-6000.3.27f2.exe`, 4 236 703 648 Б, SHA-256 `EE89D934…CED33AAA7` | `.unity-install\` | официальный `download.unity3d.com` |

Установка выполнялась молчаливыми ключами NSIS (`/S /D=...`) с подтверждением UAC продюсером. Системные настройки, антивирус и TLS не изменялись; Blender не устанавливался.

## 3. Блокер: Unity не запускается из песочницы агента

Проверено фактически:

```
$ Unity.exe -batchmode -quit -nographics
CreateDirectory 'C:/Users/bow/AppData/LocalLow/Unity' failed: Отказано в доступе.
CreateDirectory 'C:/Users/bow/AppData/Local/Unity/Caches' failed: ...
[Licensing::Module] Channel LicenseClient-bow doesn't exist
exit=1
```

и отдельная проверка записи: создание каталога в `%LOCALAPPDATA%\Unity` → «Отказано в доступе».

Причины: (1) песочница `workspace-write` запрещает запись вне репозитория, а Unity обязательно пишет кэши и лицензию в профиль пользователя; (2) лицензия Unity на этом ПК пока не активирована (нужен вход в Unity ID).

Решение (согласовано с продюсером): прогон выполняет продюсер одним скриптом `tools/unity/run_pilot.ps1`, агент анализирует логи, XML-результаты и скриншот. Инструкция — `docs/stages/S01_RUN_INSTRUCTIONS.md`.

## 4. Что написано (файлы)

| Файл | Роль |
|---|---|
| `Assets/CubeGame/Core/Generation/DeterministicHash.cs` | целочисленный детерминированный хеш (без float, без `System.Random`) |
| `Assets/CubeGame/Core/Generation/PilotWorldLayout.cs` | константы пилота (чанк 32, высоты 8…96, 4 октавы) |
| `Assets/CubeGame/Core/Generation/PilotHeightField.cs` | `seed + (x,z) -> высота` (PLACEHOLDER_NOT_ALPHA) |
| `Assets/CubeGame/Core/Rendering/MeshData.cs` | движко-независимый меш + fingerprint |
| `Assets/CubeGame/Core/Rendering/ChunkMeshBuilder.cs` | только видимые грани: верх + бока по разнице высот |
| `Assets/CubeGame/Core/Rendering/PilotPalette.cs` | плоская палитра по высоте (без текстур) |
| `Assets/CubeGame/Presentation/Rendering/PilotWorldView.cs` | чанк = GameObject + меш + `MeshCollider` |
| `Assets/CubeGame/Presentation/Rendering/UnityMeshFactory.cs` | загрузка `MeshData` в `Mesh` |
| `Assets/CubeGame/Presentation/Player/PlayerMotor.cs` | WASD/Shift/Space, гравитация, `CharacterController` |
| `Assets/CubeGame/Presentation/Camera/OrbitCameraController.cs` | орбита мышью, pitch −15…80°, дистанция 8 |
| `Assets/CubeGame/Presentation/Hud/PilotDebugHud.cs` | seed, координаты блока и чанка, FPS, счётчик чанков |
| `Assets/CubeGame/Presentation/PilotBootstrap.cs` | сборка сцены из кода, аргументы `-cubeSeed/-cubeChunks/-cubeScreenshot/-cubeFrames` |
| `Assets/CubeGame/Presentation/Shaders/PilotVertexColor.shader` | собственный unlit-шейдер с вершинными цветами (Built-in RP) |
| `Assets/CubeGame/Editor/PilotSceneBuilder.cs` | создание `Assets/Scenes/Pilot.unity` + материала, build settings |
| `Assets/CubeGame/Editor/PilotBuild.cs` | сборка Windows-плеера в `.unity-install\build\Pilot` |
| `Assets/CubeGame/Tests/EditMode/*` | детерминизм высот (5 кейсов) и мешей (5 кейсов) |
| `Assets/CubeGame/Tests/PlayMode/*` | 2 кейса: чанки с коллайдерами; персонаж встаёт на землю |
| `tools/unity/run_pilot.ps1` | прогон: лицензия → импорт → EditMode → PlayMode → сцена → плеер → скриншот |

Соблюдено: ни одного файла, шейдера, текстуры или строки кода из Alpha 2013 и из декомпиляции Reversal; геометрия и палитра — собственная.

## 5. Следующие шаги

1. Продюсер: вход в Unity Hub (лицензия) и запуск `tools/unity/run_pilot.ps1`.
2. Агент: разобрать `results\editmode.xml`/`playmode.xml`, логи, скриншот; заполнить §1–§6 фактическими PASS/FAIL.
3. При успешном импорте — добавить URP (`com.unity.render-pipelines.universal`), URP-вариант шейдера, создать `UniversalRenderPipelineAsset` и зафиксировать версии пакетов.
4. Затем — измерения и заполнение профиля (время генерации 8×8 чанков, FPS/кадр, RAM), перечень ручных проверок `TC-*`.
