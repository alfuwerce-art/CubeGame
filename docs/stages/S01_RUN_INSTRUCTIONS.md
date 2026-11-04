# S01-PILOT — автоматический прогон Unity (запускает продюсер)

**Почему запускает продюсер, а не агент:** Unity пишет в `%LOCALAPPDATA%\Unity` и `%LOCALAPPDATA%Low\Unity` (кэши, лицензия, данные игрока). В сессии агента эти каталоги закрыты песочницей (`workspace-write`): проверено — создание каталога в `%LOCALAPPDATA%\Unity` возвращает «Отказано в доступе», а запуск `Unity.exe -batchmode` пишет `CreateDirectory 'C:/Users/bow/AppData/LocalLow/Unity' failed`. Агент не отключает песочницу и не включает `danger-full-access`.

## Что сделать один раз перед запуском

1. Открыть Unity Hub (`D:\Unity Hub\Unity Hub.exe`) и войти в Unity ID, активировать Unity Personal (или другую имеющуюся лицензию). Пароль агенту не передаётся.
2. Убедиться, что Hub видит редактор: `Preferences → Installs → Add → D:\Unity\Hub\Editor\6000.3.27f2`.
3. Запустить скрипт:

```powershell
cd D:\Projects\CubeGame
powershell -ExecutionPolicy Bypass -File tools\unity\run_pilot.ps1
```

## Что делает скрипт

| Шаг | Действие | Артефакт |
|---|---|---|
| 0 | проверка лицензии | `.unity-install\logs\0_license.log`; при неактивной лицензии скрипт выходит с кодом 10 |
| 1 | импорт проекта, компиляция скриптов, генерация недостающих `ProjectSettings/*` и `Library/` | `logs\1_project_import.log` |
| 2 | EditMode-тесты | `results\editmode.xml`, `logs\2_tests_editmode.log` |
| 3 | PlayMode-тесты | `results\playmode.xml`, `logs\3_tests_playmode.log` |
| 4 | сборка сцены `Assets/Scenes/Pilot.unity` + материала | `logs\4_scene_build.log` |
| 5 | сборка плеера Windows x64 | `.unity-install\build\Pilot\CubeGamePilot.exe`, `logs\5_player_build.log` |
| 6 | запуск плеера (seed 666666, 8×8 чанков), скриншот и выход | `.unity-install\shots\pilot_run.png`, `logs\6_player_run.log` |
| — | сводка | `.unity-install\run_summary.txt` |

Скрипт **не** устанавливает ПО, не меняет системные настройки, не трогает антивирус и не скачивает ассеты Alpha; он только запускает уже установленный Unity и пишет артефакты в `.unity-install\` (каталог в `.gitignore`).

## Если шаг упал

| Код выхода | Значение | Что делать |
|---|---|---|
| 10 | нет активной лицензии Unity | войти в Unity Hub и повторить |
| 20 | проект не импортировался (обычно ошибки C# или пакеты) | прислать `logs\1_project_import.log` агенту |
| 1 | один из шагов упал | прислать `run_summary.txt` и логи упавших шагов |

После прогона агент читает `results\*.xml`, логи и скриншот, заполняет `docs/stages/S01_PILOT_REPORT.md` фактическими PASS/FAIL и коммитит.
