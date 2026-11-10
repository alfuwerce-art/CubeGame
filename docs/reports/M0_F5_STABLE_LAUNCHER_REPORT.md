# M0-F5 — STABLE LAUNCHER + FIRST WINDOWS BUILD (подготовка)

**Ветка:** `deepseek/m0-f5-stable-launcher` (база — F4 review commit `60f24be7b34c0bc60d94ff36b75405b651ee02d8`)
**Статус:** `READY_FOR_REVIEW` (сборка и launcher проверены на реальном запуске, приёмка — за продюсером)
**Canonical:** не двигался (`integration/m0-all` = `0533698`)

## 1. Результат

```text
LAUNCHER_STATUS=PASS
LAUNCHER_PATH=D:\Games\CubeGame\CubeGameLauncher.exe
LAUNCHER_SHA256=21b364895c7720f7c9868bdceff6e038a8f97d888de93a481cfb34ed9e92a297

GAME_BUILD_STATUS=PASS
GAME_BUILD_PATH=D:\Games\CubeGame\releases\60f24be\CubeGame.exe

CURRENT_TARGET=releases\60f24be\CubeGame.exe

DESKTOP_SHORTCUT=PASS
SHORTCUT_PATH=C:\Users\bow\Desktop\CubeGame.lnk
SHORTCUT_TARGET=D:\Games\CubeGame\CubeGameLauncher.exe
SHORTCUT_START_IN=D:\Games\CubeGame

SHORTCUT_TO_LAUNCHER_TO_GAME=PASS
LAUNCHER_IMMUTABLE_TEST=PASS

F5_BRANCH=deepseek/m0-f5-stable-launcher
```

Решение и заморозка: [D91 — STABLE LAUNCHER CONTRACT](../decisions/D91_STABLE_LAUNCHER_CONTRACT.md).
`LAUNCHER_IMMUTABLE = YES`; после `LAUNCHER_STATUS=PASS` binary не пересобирается и не патчится.

## 2. Установка

```text
D:\Games\CubeGame\
    CubeGameLauncher.exe  8192 bytes, SHA-256 21b364895c77...a297
    current.txt           releases\60f24be\CubeGame.exe
    launcher.sha256       21b364895c77...a297
    releases\60f24be\     CubeGame.exe 667 136 bytes
                          UnityPlayer.dll 36 799 920 bytes
                          CubeGame_Data\, MonoBleedingEdge\, D3D12\, UnityCrashHandler64.exe
                          155 files, 92 396 341 bytes (88,12 MB)
```

Ярлык: `C:\Users\bow\Desktop\CubeGame.lnk`, target `D:\Games\CubeGame\CubeGameLauncher.exe`,
Start in `D:\Games\CubeGame`. Desktop получен через `[Environment]::GetFolderPath('Desktop')`.

## 3. Launcher

- Исходник: `tools/launcher/CubeGameLauncher.cs` (C# 5, только BCL + WinForms MessageBox).
- Сборка: `tools/launcher/build_launcher.ps1` → `csc.exe` 4.0.30319 из состава Windows
  (`/target:winexe /platform:anycpu /optimize+`). Unity, Electron, Python, PowerShell как
  runtime не используются; установка дополнительной среды не требуется (.NET Framework 4.x
  входит в Windows 10/11).
- Размер: **8 192 bytes**.
- Каталог launcher'а определяется по расположению самого executable; `current.txt` читается
  из этого каталога; относительный путь разворачивается от него же; absolute path отвергается.
- Working directory процесса игры = каталог Unity build; аргументы launcher'а передаются игре
  с корректным Windows-квотированием; после успешного `Process.Start` launcher выходит с кодом 0.

Проверенные ошибки (каждая — обычный Windows message box, затем ненулевой код):

| Сценарий | Код | Диалог |
| --- | --- | --- |
| нет `current.txt` | 1 | да |
| пустой `current.txt` | 2 | да |
| абсолютный путь в `current.txt` | 2 | да |
| target отсутствует | 3 | да |
| процесс игры не стартовал | 4 | (код предусмотрен, сценарий не воспроизводился) |

## 4. Сборка игры

Добавлен только в F5-ветку: `Assets/CubeGame/Editor/ReleaseBuild.cs` +
`tools/release/build_windows_release.ps1`. Gameplay, сцены и PlayerSettings не менялись;
собирается включённая в `EditorBuildSettings` сцена `Assets/Scenes/Pilot.unity` как есть.

Команда:

```text
powershell -NoProfile -ExecutionPolicy Bypass -File tools\release\build_windows_release.ps1
```

Unity 6000.3.27f2, `-batchmode -quit -nographics -executeMethod CubeGame.EditorTools.ReleaseBuild.BuildWindows64`,
`CUBEGAME_RELEASE_DIR=D:\Games\CubeGame\releases\60f24be`.
Лог: `.unity-install/logs/f5_build_60f24be_20261110_201657.log`.

Ключевые строки лога:

```text
[ReleaseBuild] result=Succeeded size=92396341B errors=0 warnings=19 time=00:00:50.9775955
[ReleaseBuild] BUILD_SUCCEEDED output=D:\Games\CubeGame\releases\60f24be\CubeGame.exe size=667136B data=...\CubeGame_Data complete=True
```

`error CS` — 0. Предупреждения компиляции прежние, не из F5-кода:
`CS0162` (32), `CS0675` (4), `CS0105` (2); 19 warnings в сводке билда — из импорта/сборки.

## 5. Smoke test полного пути пользователя

1. Запуск `C:\Users\bow\Desktop\CubeGame.lnk` → launcher (мгновенно завершился, код 0) →
   `releases\60f24be\CubeGame.exe` (PID 28464), окно `CubeGame Pilot` 1920x1080, `Responding=True`.
2. `Player.log` (`%USERPROFILE%\AppData\LocalLow\CubeGame\CubeGame Pilot\Player.log`): движок 6000.3.27f2,
   `Mono path[0] = D:/Games/CubeGame/releases/60f24be/CubeGame_Data/Managed`,
   `[Game] boot -> MainMenu`, `[Game] world built seed=666666 ... chunks=25 ... level=1`.
   `Exception` — 0, ошибок DLL нет, build не закрывается сам.
3. Меню отрисовано (скриншот `.unity-install/shots/f5_smoke_shortcut.png`): New world / Create
   hero / Saved worlds, панель по центру экрана.
4. Независимость от рабочего каталога: `CubeGameLauncher.exe` запущен с `-WorkingDirectory C:\Windows`
   → игра стартовала так же, launcher вышел с кодом 0.
5. Повторный запуск через ярлык после закрытия игры → снова `CubeGame Pilot`, `Responding=True`,
   `Exception` 0.

## 6. Immutable-тест

1. SHA-256 launcher до теста: `21b3648...a297`.
2. Создан второй release-entry `releases\launcherprobe\Probe.exe` (тестовый executable, не игра),
   `current.txt` переписан на `releases\launcherprobe\Probe.exe`.
3. Запуск установленного launcher из `C:\Windows` → запустился именно Probe
   (`probe_exe=...\launcherprobe\Probe.exe`, `current_directory=...\launcherprobe`),
   launcher binary не менялся.
4. SHA-256 после теста: `21b3648...a297` — совпадает бит-в-бит.
5. `current.txt` возвращён на `releases\60f24be\CubeGame.exe`, игра запустилась снова;
   probe-каталог удалён; `tools/launcher/verify_launcher.ps1` → `LAUNCHER_IMMUTABLE=PASS`.

Итог: смена активного target выполняется только правкой `current.txt`; launcher и ярлык при
обновлении игры не меняются.

## 7. Что осталось неподтверждённым / ограничения

- Launcher не подписан (Authenticode); зависимости кроме .NET Framework 4.x нет.
- Ошибка «процесс не стартовал» (код 4) предусмотрена кодом, но не воспроизводилась.
- Проверка выполнена на текущем ПК пользователя (NVIDIA RTX 4060 Laptop, Windows 11),
  не на чистой машине без .NET Framework.
- Cборка соответствует F4-кандидату `60f24be`; полный игровой цикл, регрессия и визуальный
  review относятся к этапу M0-F5 и здесь не выполнялись.
- `PROJECT_MEMORY.md` / `ACTIVE_STAGE.md` не обновлялись: приёмки этапа ещё нет, F5-работа —
  подготовка инфраструктуры запуска.
