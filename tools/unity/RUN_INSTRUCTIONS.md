# Как запустить проверку S01-PILOT

Скрипт: `tools/unity/run_pilot.ps1`. Он проверяет уже установленный Unity, ничего не
устанавливает и не меняет версию Editor.

## Команда

Обычный PowerShell (не нужен администратор):

```powershell
cd 'C:\Users\bow\Desktop\CubeGame'
powershell -NoProfile -ExecutionPolicy Bypass -File tools\unity\run_pilot.ps1
```

`ExecutionPolicy Bypass` действует только на этот запуск и не меняет политику системы.

Если Unity CLI ещё не в PATH, скрипт сам ищет его в
`%LOCALAPPDATA%\Unity\bin\unity.exe`; дополнительная настройка PATH не нужна.

## Что именно проверяется

| Шаг | PASS только если |
|---|---|
| `0_license` | `unity license status --json` вернул `active=true` и `signedIn=true` (Unity Personal) |
| `1_project_import` | Unity 6000.3.27f2 запустился, exit code 0, в логе нет `error CS` / `Scripts have compiler errors` |
| `1_editor_version_check` | в логе `Initialize engine version: 6000.3.27f2`, а `ProjectVersion.txt` совпадает побайтно по версии |
| `2_tests_editmode` | `unity test --mode EditMode` вернул exit 0 **и** NUnit XML: `total > 0`, `failed = 0`, `passed > 0` |
| `2_tests_playmode` | то же для PlayMode |
| `3_scene_build` / `3_scene_artifact` | в логе маркер `SCENE_SAVED`, файл `Assets/Scenes/Pilot.unity` существует |
| `4_player_build` / `4_player_artifact` | маркер `BUILD_SUCCEEDED`, есть `CubeGamePilot.exe` (>100 КБ) и папка `CubeGamePilot_Data` |
| `5_player_run` | в логе плеера `[Pilot] screenshot requested` и `[Pilot] quitting`, скриншот `shots\pilot_run.png` > 1 КБ |

Ложные PASS исключены намеренно:

- exit code Unity/CLI сам по себе не считается PASS;
- тесты проверяются по содержимому NUnit XML, а не по коду возврата (Unity возвращает 2
  при упавших тестах, но 2 бывает и при других сбоях);
- лицензия проверяется через JSON CLI, а не по «в логе нет слова license»;
- сцена и плеер подтверждаются файлами и маркерами, а не только отсутствием ошибок;
- старый скриншот удаляется перед запуском плеера, иначе прошлый прогон дал бы ложный PASS.

## Где результаты

```
.unity-install\run_summary.json      итог по шагам + блок отчёта
.unity-install\logs\                 все логи Unity, CLI и плеера
.unity-install\results\editmode.xml  NUnit XML EditMode
.unity-install\results\playmode.xml  NUnit XML PlayMode
.unity-install\shots\pilot_run.png   скриншот собственной сцены
```

`.unity-install\` в `.gitignore` и в репозиторий не попадает.

## Как читать итог

В конце печатается блок:

```text
CLI_VERSION=1.0.0-beta.13
EDITOR_REGISTERED_6000_3=YES
EDITOR_6000_3_PATH=D:\Unity\Hub\Editor\6000.3.27f2
PROJECT_EDITOR=6000.3.27f2
UNITY_LICENSE=ACTIVE
UNITY_IMPORT=PASS
UNITY_EDITMODE=PASS
UNITY_PLAYMODE=PASS
SCENE_BUILD=PASS
PLAYER_BUILD=PASS
PLAYER_RUN=PASS
PIPELINE=NOT_INSTALLED
BLOCKERS=
BRANCH=stage/s01-unity-pilot
COMMIT=<hash>
```

`exit 0` — все шаги PASS. `exit 1` — хотя бы один FAIL, причина в `BLOCKERS` и в
соответствующем логе. `exit 21` — лицензия не подтверждена (нужен вход в Unity Hub).

## Технические детали, которые не надо «чинить»

- Скрипт намеренно **ASCII-only**. Windows PowerShell 5.1 читает UTF-8 без BOM как ANSI, и
  кириллица в строках ломает разбор файла. Не добавляйте не-ASCII символы в `.ps1`.
- Запуск Editor идёт через `Start-Process -Wait`, потому что прямой `& Unity.exe` в
  PowerShell возвращает управление сразу (GUI-подсистема), и проверка получала бы пустой
  код возврата.
- Плеер запускается через `&` и ожидается по маркеру `[Pilot] quitting` в логе: для GUI-плеера
  `Start-Process` в среде агента зависает, а код возврата недоступен. Факт успеха
  подтверждают скриншот и маркеры в логе.
- Строка `d3d12: Error when writing PSO binary data ...` в логе плеера — предупреждение
  драйвера о кэше PSO, на результаты проверки не влияет.

## Ручная проверка демонстрации (P2)

1. Открыть `C:\Users\bow\Desktop\CubeGame` в Unity 6000.3.27f2.
2. Открыть сцену `Assets/Scenes/Pilot.unity` (или выполнить пункт меню `CubeGame > Build pilot scene`).
3. Play: WASD — движение, Shift — бег, Space — прыжок, ПКМ/курсор — обзор мышью, Esc — освободить курсор.
4. HUD показывает seed, координаты блока и чанка, FPS.
