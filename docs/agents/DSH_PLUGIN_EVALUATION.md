# DeepSeek Harness — аудит возможностей и кандидатов плагинов

**Статус:** `RECOMMENDATIONS_ONLY`; на пользовательском ПК ничего не установлено и не включено. DSH Desktop из прежнего контекста: `0.2.1-rc.2` — **перепроверить реальную версию**. Имена ниже сверены с официальным [пакетным каталогом](https://github.com/deepseek-ai/deepseek-harness/blob/master/packages/README.md), но состав версии master **может не совпадать** с установленной rc.

## Приоритет P0 — проверить, не установлено ли уже

| Компонент | Официальный пакет | Цель | Проверка |
|---|---|---|---|
| Локальные skills | `dsh-skill`, `dsh-skill-filesystem`, `dsh-tool-skill` | загружать `.dsh/skills/<name>/SKILL.md` по запросу | каталог, успешная загрузка каждого skill, frontend YAML |
| PowerShell sandbox | `dsh-pwsh-sandbox`, `dsh-tool-pwsh`, `dsh-sandbox-policy` | выполнение Git/Node/Unity на Windows | тест записи только в workspace и блокировки за его пределами |
| Filesystem and search | `dsh-tool-fs`, `dsh-tool-fs-search` | чтение / ограниченное редактирование проекта | не трогать чужие пути, секреты, .git |
| Plugin manager | `dsh-plugin-manager` | посмотреть список bundles в конкретном профиле | audit only; установка выполняется отдельно с approval |
| Git + tests | штатный `pwsh`, локальные Git/Unity CLI | реальная сборка и тесты без лишнего MCP | CLI команды, XML и exit code, не «PASS по выводу модели» |

P0 **не означает, что нужно установить всё подряд**. Некоторые уже входят в базовую композицию DSH. Проверить доступность, имя и совместимость в Plugins → текущий профиль или через встроенный менеджер, не дублировать плагины.

## P1 — дополнительные возможности для больших кодовых баз

| Компонент | Официальный пакет | Зачем | Ограничения |
|---|---|---|---|
| LSP | `dsh-lsp`, `dsh-lsp-stdio`, `dsh-tool-lsp` | точные C#/C++ definitions, references, API navigation | нужны отдельно установленные language servers; без них `LSP_UNAVAILABLE` |
| Субагент | `dsh-subagent`, `dsh-subagent-spawn-in-process`, `dsh-tool-subagent` | независимый аудит и небольшие read-only исследования | использовать лишь DeepSeek или Codex; бюджет модели и доступы; один писатель на изменяемый файл |
| Hook bridge | `dsh-hooks-claude-code` | опциональные PreToolUse/PostToolUse проверки | несовместимость версий возможна; явный `configPath`; хуки не активны только от наличия JSON |
| MCP manager/client | `dsh-mcp-manager`, `dsh-mcp-client` | подключать строго нужный инструмент | сначала доказать пользу относительно CLI/скрипта; отдельная конфигурация/права |

Рекомендованная минимальная связка для CubeGame: **project skills + sandbox PowerShell + filesystem/search + git + Unity CLI + проверочный Node скрипт**. LSP добавлять после успешного smoke-теста C#; субагента — после определения стратегии экономии контекста. MCP — только под задачу.

## P2 — инструменты 3D и альтернативного движка (вручную разрешать)

- **Blender как локальная программа** + Python background scripting; сначала проверить версию и воспроизводимый импорт/рендер одного объекта. Сторонние навыки [EL4CTEO/blender-skills](https://github.com/EL4CTEO/blender-skills) — изучить и выборочно адаптировать, **не** массово устанавливать сторонние пакеты/скрипты.
- **Unreal Engine 5.8**: официальные Python Editor Scripting, Geometry Scripting, Control Rig/IK Rig. Это **эксперимент вне действующего Unity runtime**, а не DSH plug-in. [ТЗ](../research/UNREAL_SCRIPTING_SPIKE.md).
- Unity CLI уже описан в [UNITY_CLI_PROTOCOL](UNITY_CLI_PROTOCOL.md); внешний Unity MCP добавлять только если скрипты/CLI не могут выполнить измеримую задачу.
- GitHub MCP из внешнего источника нужен только при отсутствии штатного Git через PowerShell и необходимости API; GitHub write-права и токены минимальные, не печатать секреты.
- Плагины с разрешением на полную автономную запись вне workspace, автоматическую установку системного ПО и неаудированные marketplace bundles — **не активировать**.

## Подключение — отдельное действие на Windows

1. Проверить `dsh --version` или точное значение в Desktop. Зафиксировать `DSH_VERSION` и список `bundles` для **`cubegame-dev`**.
2. Скопировать настройки пресета и убедиться, что нет пользовательских незакоммиченных изменений. Не менять глобальные профили.
3. Для каждого кандидата выяснить, доступен ли он в установленной версии, какие права получает, какой ввод/вывод принимает и нужен ли restart. Начать с `disabled`/dry-run, когда поддерживается.
4. Подключать **по одному** с разрешения владельца; провести smoke и негативные тесты, сверить логи до/после.
5. Зафиксировать `AVAILABLE / INSTALLED / ACTIVE / TESTED / BLOCKED` отдельно: это разные факты.
6. Обновить `docs/agents/experience/` только по реальным измерениям. Цель — измеримое качество DeepSeek Harness и Codex на задачах CubeGame, без использования Claude.

## Проверенные справочники

- https://github.com/deepseek-ai/deepseek-harness/blob/master/packages/skill/skill-filesystem/README.md
- https://github.com/deepseek-ai/deepseek-harness/blob/master/packages/shell/README.md
- https://github.com/deepseek-ai/deepseek-harness/blob/master/packages/lsp/README.md
- https://github.com/deepseek-ai/deepseek-harness/blob/master/packages/subagent/README.md
- https://github.com/deepseek-ai/deepseek-harness/blob/master/packages/mcp/README.md
- https://github.com/deepseek-ai/deepseek-harness/blob/master/packages/boot/plugin-manager/README.md

## Результат первичного ревью официального base bundle

Согласно текущему `dsh-base`, для базовых профилей **уже предусмотрены** файловые инструменты, platform PowerShell, sandbox, skills и subagents. Начальный этап — диагностировать их в имеющемся Desktop, **не ставить дубликаты**. Хуки `dsh-hooks-claude-code` относятся к **experimental**; абсолютное право/запрет обеспечивают sandbox и права, не скрипт, который может завершиться fail-open.

[Windows first run](WINDOWS_DSH_FIRST_RUN.md) · [dsh-base](https://github.com/deepseek-ai/deepseek-harness/blob/master/packages/bundle/base/README.md) · [hooks experimental](https://github.com/deepseek-ai/deepseek-harness/blob/master/packages/experimental/hooks-claude-code/README.md).

## Разрешённые агентные провайдеры

Для CubeGame — только **DeepSeek Harness и Codex**. Не устанавливать и не активировать `dsh-subagent-claude-code`, Claude Code SDK или любые подключаемые Claude credentials. Пакет `dsh-hooks-claude-code`, если когда-либо понадобится, — технический bridge **формата хуков**, а не обращение к Claude; пока experimental/неактивен. Приоритет — штатные permissions/sandbox и проверки Git.
