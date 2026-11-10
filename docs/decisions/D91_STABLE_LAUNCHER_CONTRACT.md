# D91 — STABLE LAUNCHER CONTRACT

**Статус:** ДЕЙСТВУЕТ · **Дата:** 10 ноября 2026 · **Этап:** подготовка M0-F5
**LAUNCHER_IMMUTABLE = YES**

## 1. Решение продюсера

Установка CubeGame имеет **один постоянный launcher executable**, который после первой
принятой сборки больше никогда не меняется. Unity player `CubeGame.exe` не может быть
launcher'ом, потому что он пересобирается с каждой версией игры.

Обязательная цепочка запуска:

```text
Desktop shortcut (CubeGame.lnk)
  -> D:\Games\CubeGame\CubeGameLauncher.exe     (immutable)
  -> D:\Games\CubeGame\current.txt              (одна строка, относительный путь)
  -> D:\Games\CubeGame\releases\<build-id>\CubeGame.exe   (Unity player)
```

## 2. Установленная структура

```text
D:\Games\CubeGame\
    CubeGameLauncher.exe      # заморожен, SHA-256 фиксирован
    current.txt               # releases\<build-id>\CubeGame.exe
    launcher.sha256           # SHA-256 launcher binary
    releases\
        <build-id>\
            CubeGame.exe
            UnityPlayer.dll
            CubeGame_Data\
            ... остальные файлы Windows Player
```

Unity build всегда лежит в собственной папке `releases\<build-id>\`; файлы игры не
смешиваются с launcher'ом.

## 3. Зафиксированные факты

| Параметр | Значение |
| --- | --- |
| Путь launcher | `D:\Games\CubeGame\CubeGameLauncher.exe` |
| SHA-256 | `21b364895c7720f7c9868bdceff6e038a8f97d888de93a481cfb34ed9e92a297` |
| Размер | `8192` bytes |
| Тип | Windows GUI executable (`/target:winexe`, AnyCPU, .NET Framework 4.x из состава Windows) |
| Источник | `tools/launcher/CubeGameLauncher.cs` |
| Сборка | `tools/launcher/build_launcher.ps1` (csc.exe 4.0.30319, без Unity/Electron/Python) |
| Ярлык | `%USERPROFILE%\Desktop\CubeGame.lnk` |
| Цель ярлыка | `D:\Games\CubeGame\CubeGameLauncher.exe` |
| Start in | `D:\Games\CubeGame` |

## 4. Контракт поведения launcher

1. Определяет каталог **самого launcher** (не текущий рабочий каталог).
2. Читает `current.txt` из этого каталога.
3. Содержимое — одна строка с относительным путём (`releases\<build-id>\CubeGame.exe`).
4. Проверяет существование целевого executable.
5. Запускает игру, передавая ей собственные аргументы командной строки launcher'а.
6. Working directory процесса игры — каталог Unity build.
7. После успешного запуска launcher завершается с кодом `0` и не ждёт игру.

Ошибки показываются обычным Windows message box и возвращают ненулевой код:

| Код | Причина |
| --- | --- |
| 1 | нет `current.txt` рядом с launcher |
| 2 | `current.txt` пуст, нечитаем или содержит абсолютный/некорректный путь |
| 3 | выбранный в `current.txt` build отсутствует |
| 4 | процесс игры не удалось запустить |

Launcher **не** скачивает обновления, **не** обращается в интернет, **не** изменяет файлы
игры, **не** содержит hardcoded SHA game build и **не** зависит от имени release-папки или
рабочего каталога запуска. Активную версию выбирает только `current.txt`.

## 5. Что разрешено менять

- `releases\<build-id>\` — новые сборки игры добавляются рядом, старые не перезаписывают launcher.
- `current.txt` — единственный переключатель активной версии (`tools/launcher/set_current_release.ps1`).
- `launcher.sha256` — **только** вместе с новым launcher'ом по прямому решению продюсера.

## 6. Что запрещено

После фиксации SHA-256 запрещено:

- пересобирать `CubeGameLauncher.exe`;
- заменять или патчить launcher;
- менять его поведение;
- направлять ярлык на другой executable (в том числе напрямую на Unity build).

Дефект launcher'а исправляется **до** фиксации финального SHA. После отчёта
`LAUNCHER_STATUS=PASS` binary считается замороженным; любое изменение требует нового
прямого решения продюсера и новой версии контракта.

## 7. Проверка

- `tools/launcher/verify_launcher.ps1` — сверяет `launcher.sha256` с binary, печатает
  `LAUNCHER_IMMUTABLE=PASS|FAIL`.
- Immutable-тест: подменить `current.txt` на другой установленный target, запустить launcher,
  убедиться, что выбрана другая цель, вернуть `current.txt` и повторно сверить SHA-256 —
  binary не менялся.
- Smoke test: ярлык → launcher → `current.txt` → Unity `CubeGame.exe`, включая запуск из
  постороннего рабочего каталога и повторный запуск.
