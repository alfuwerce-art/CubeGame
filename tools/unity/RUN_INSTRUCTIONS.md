# Unity — проверки CubeGame
Рабочий проект: `D:\Projects\CubeGame`. Версию редактора всегда брать из `ProjectSettings/ProjectVersion.txt` (по последнему отчёту 6000.3.27f2).

Для проверок: компиляция → EditMode XML → PlayMode XML → Windows build → Player smoke → игровой сценарий. Успех загрузки меню не доказывает работоспособность геймплея.

По локальному отчёту DeepSeek написал `tools/unity/unity_ci.ps1` с `-Step compile|editmode`, `-NoPlayMode`, `-SmokePlayer`. Этот файл **ещё не опубликован в GitHub**: сначала проверить `Test-Path tools/unity/unity_ci.ps1`, diff и перенести по отдельному контролируемому коммиту. Не подменять отсутствующий скрипт устаревшей инструкцией.

Для запуска использовать [Unity CLI Protocol](../../docs/agents/UNITY_CLI_PROTOCOL.md). Проверять реальный итог по логам, а не одному коду выхода.
