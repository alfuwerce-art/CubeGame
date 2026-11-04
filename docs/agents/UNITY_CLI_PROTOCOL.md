# CubeGame — Unity CLI: инструкция для DeepSeek

**Дата:** 2026-11-02
**Статус:** ACTIVE tooling protocol. Не является подтверждением успешных Unity-тестов.
**Источник фактов:** команды и вывод PowerShell, предоставленные продюсером, плюс отчёты агента.

## Что уже известно и что НЕ надо повторять

- Unity CLI **работает**: `unity --version` → `1.0.0-beta.13`, `unity --help` показывает команды.
- CLI исполняется из `C:\Users\bow\AppData\Local\Unity\bin\unity.exe` (проверить полный путь `Get-Command unity`).
- **Unity 6.3 LTS зарегистрирована**: `unity editors --installed` показывает `6000.3.27f2`; `unity editors path 6000.3.27f2` → `D:\Unity\Hub\Editor\6000.3.27f2`.
- **Unity 6.6 тоже зарегистрирована**: `6000.6.7f1`; для CubeGame **НЕ использовать** без отдельного утверждения обновления.
- Повторный вызов `unity editors add 'D:\Unity\Hub\Editor\6000.3.27f2'` возвратил «Недопустимое приложение Unity Editor». Это **не блокер**: запись редактора уже присутствует. **Не повторять, не переустанавливать и не перенастраивать install path.**
- CLI предлагает self-update beta.13 → beta.15: **не обновлять автоматически**.
- Unity-пилот `stage/s01-unity-pilot` имеет `ProjectSettings/ProjectVersion.txt` с `6000.3.27f2`.
- Blender пока не установлен и для пилота не требуется.
- Ветки `stage/s00-static-r01` (remote HEAD `73893b6`) и `stage/s01-unity-pilot` (remote HEAD `b5d881b`) уже на GitHub. **Не утверждать, что изменения уже слиты в main.**

## 1. Правила запуска в DSH

1. Сначала `AGENTS.md`, `docs/ACTIVE_STAGE.md`, `docs/stages/S01_UNITY_PILOT.md`, этот документ.
2. Новая сессия только `cubegame-dev`, `workspace-write` + `ask`; не включать `danger-full-access`/never ради Unity.
3. Работать на ветке `stage/s01-unity-pilot` для кода пилота. Перед переключением проверять `git status` и несохранённые файлы, особенно `plugins/` и `.unity-install/`.
4. Unity Editor пишет в `AppData/Local/Unity` и `AppData/LocalLow/Unity`; если sandbox запрещает доступ — **BLOCKED_SANDBOX**, попросить продюсера запустить одобренную команду в обычном PowerShell. Не ослаблять защиту компьютера.
5. Для Unity-аккаунта пользователь сам входит в Hub и активирует Unity Personal. Не запрашивать пароль, ключи/токены не записывать в Git. Проверку лицензии выполнять лишь безопасными командами CLI.
6. Не использовать Unity Cloud, Unity VCS, платные сервисы, сторонние MCP или `unity setup` без отдельного обоснования и разрешения.

## 2. Чтение и диагностика — без изменений состояния

```powershell
cd 'D:\Projects\CubeGame'
git status --short --branch
(Get-Command unity).Source
unity --version
unity editors --installed
unity editors path 6000.3.27f2
unity license --help
unity auth --help
unity projects --help
unity open --help
unity test --help
unity build --help
unity doctor --help
```

Убедиться, что текущая ветка содержит Unity-проект и `ProjectSettings/ProjectVersion.txt` фиксирует 6000.3.27f2. Если лицензия не активна, указать точные UI-действия пользователю для Unity Hub и **не** заявлять успех.

## 3. Реальный пилот: что надо сделать

1. Не менять версию Editor и не открывать проект в 6.6. Для открытия выбрать 6.3 в Hub или через `unity open`, уточнив аргументы `unity open --help`; указать точный путь проекта.
2. Сначала успешный импорт/компиляция проекта в Unity 6.3. Только потом EditMode и PlayMode тесты, сборка сцены и Windows player.
3. Проверить реальный отчёт тестов (XML/JSON) — число test cases >0, количество failed=0, итог passed соответствующий. **Exit code Unity 2 не равен PASS автоматически.**
4. Сборка — проверка файла `CubeGamePilot.exe` и успешного отчёта BuildPipeline; отсутствие строк об ошибке в логах недостаточно.
5. PlayMode/игровая проверка: работает камера, движение персонажа и столкновение с землёй; сохранить скриншоты собственной сцены, реальные FPS и время генерации.
6. В репозитории существует `tools/unity/run_pilot.ps1`; до запуска исправить две логические ошибки: `AcceptExitCodes @(0,2)` в тестах и «лицензия выглядит активной, если ошибка в логе не встретилась». Результаты `CORE_CHECK_PASS 10/10` не путать с фактическими `UNITY_TESTS_PASS`.
7. Скрипт проверен на предмет отсутствия очевидных команд удаления/изменения системных настроек, но сам не запускался ChatGPT на ПК. Сначала запускать после ревью с обычными правами пользователя; `ExecutionPolicy Bypass` не делать режимом по умолчанию.

## 4. Подключение Pipeline только после базового прогона

Unity CLI поддерживает `pipeline`, `status`, `list`, `command`, `recompile`, `test` и `build`.

- Для команд управления запущенным редактором может потребоваться пакет `com.unity.pipeline`.
- Сначала `unity pipeline --help`, `unity pipeline install --help`, `unity status --help`, `unity command --help`.
- Подключать пакет **только после** согласования с продюсером, на ветке пилота и с сохранением/проверкой диффа `Packages/manifest.json` и lock-файла. Установка Pipeline может менять зависимости; это не повод автоматически мигрировать на Unity 6.6.
- Если DSH работает через PowerShell, отдельный MCP для базового тестирования не требуется. `unity skill show` может дать официальную памятку по CLI; `unity skill install`, `unity setup` могут менять клиентские настройки, **без разрешения не запускать**.

## 5. Выходной отчёт

```text
CLI_VERSION=1.0.0-beta.13
EDITOR_REGISTERED_6000_3=YES
EDITOR_6000_3_PATH=D:\Unity\Hub\Editor\6000.3.27f2
EDITOR_REGISTERED_6000_6=YES
PROJECT_EDITOR=6000.3.27f2
UNITY_LICENSE=UNKNOWN|ACTIVE|BLOCKED
UNITY_IMPORT=NOT_RUN|PASS|FAIL
UNITY_EDITMODE=NOT_RUN|PASS|FAIL
UNITY_PLAYMODE=NOT_RUN|PASS|FAIL
PLAYER_BUILD=NOT_RUN|PASS|FAIL
PIPELINE=NOT_INSTALLED|INSTALLED|CONNECTED|FAILED
BLOCKERS=
BRANCH=
COMMIT=
```

Не помечать этап `ACCEPTED`: только `READY_FOR_REVIEW` с фактическими доказательствами.

Официальные материалы:
- https://docs.unity.com/en-us/unity-cli/use-unity-cli
- https://docs.unity.com/en-us/unity-cli/unity-pipeline/unity-pipeline-package
