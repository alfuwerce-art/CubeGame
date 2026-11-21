# Первый запуск DeepSeek Harness для CubeGame — безопасный порядок

**Статус:** READY_FOR_USER_DIAGNOSTIC; удалённого доступа к Windows у ChatGPT нет. Скрипт только готов к запуску пользователем, не утверждать, что он уже выполнялся или что DSH установлен/настроен.

## 0. Проверка состояния Git

Базовые PR ещё находятся в цепочке **#18 → #19 → #20**, пока они не объединены. Нельзя считать, что работа в `D:\Projects\CubeGame` уже содержит 9 skills. Не запускать `git switch` или `git reset --hard`, пока не проверен `git status`.

В PowerShell:

```powershell
git -C 'D:\Projects\CubeGame' status --short --branch
git -C 'D:\Projects\CubeGame' branch --show-current
Get-Command dsh,node,git -ErrorAction SilentlyContinue | Select-Object Name,CommandType
```

Если `Get-Command dsh` ничего не показал, это **не** доказательство отсутствия DSH Desktop: пользователь запускает графическое приложение, у которого CLI может не быть на PATH.

## 1. Read-only диагностика

После получения `tools/harness/windows/Inspect-Dsh.ps1` из проверенной ревью-ветки:

```powershell
& '.\tools\harness\windows\Inspect-Dsh.ps1' -ProjectRoot 'D:\Projects\CubeGame'
```

Скрипт не меняет права и конфигурации, не читает секреты и не печатает содержимое профилей. **Если скрипт отсутствует локально, сначала проверить PR/рабочую ветку; не скачивать и не запускать непроверенную команду из чата.**

## 2. Безопасный просмотр подготовленных навыков

9 skills лежат в `.dsh/skills/<skill>/SKILL.md` **только в ревью-ветке #20** (7 добавлены в #20 поверх 2 из #19). На рантайме выбирать директорию, где лежат эти файлы.

Владелец решил использовать **только существующее рабочее пространство** `D:\Projects\CubeGame`, без дополнительного каталога и worktree. Факт: установлен DSH Desktop `0.2.2-rc.1`, команды `node` и `dsh` не найдены в PATH, каталог `.dsh/skills` отсутствует в текущей ветке `codex/character-production-contracts`. В этой ветке старые AGENTS/ACTIVE_STAGE ещё описывают Factoria; это **не** текущий утверждённый режим работы.

**Сначала только навыки и read-only discovery**, без перезаписи старых документов:

```powershell
cd 'D:\Projects\CubeGame'
git status --short --branch
# Если есть изменения — не переключать ветку, сначала разобраться.
git switch -c local/dsh-setup-20261121
git restore --source=origin/review/ai-game-development-suite-20261121 --worktree -- .dsh/skills
Get-ChildItem '.dsh\skills' -Directory | Select-Object Name
git status --short --branch
```

`git switch -c` создаёт лишь новую **ветку в том же каталоге**, оставляя исходный `codex/character-production-contracts` неизменным. `git restore --worktree` копирует **только** каталог навыков из PR #20; файлы проекта, AGENTS и память не перезаписываются, а новые файлы будут видны как untracked. Ожидается **9** навыков. Для первой проверки использовать только обзор каталога — не разрешать производство игровых ассетов и кодирование до синхронизации текущих AGENTS/ACTIVE_STAGE и зависимых документов. Ветка `local/dsh-setup-20261121` не каноническая и не требует push.


## 3. Проверка в DSH Desktop

1. Открыть DeepSeek Harness Desktop → уже существующую рабочую папку `D:\Projects\CubeGame`.
2. Зафиксировать фактическую версию из **About DeepSeek Harness** (не ориентироваться на README из master как на установленную версию).
3. Проверить **только обнаружение имён девяти skills** в каталоге и загрузку одного без исполнения. Не запускать `cubegame-doc-audit` до обновления AGENTS/ACTIVE_STAGE и поддерживающих документов.
4. Дать сначала **read-only** задачу: `Прочитай AGENTS, ACTIVE_STAGE и CONCEPT. Расскажи список доступных project skills, не меняй файлы.`
5. Затем проверить возможности PowerShell и sandbox в UI. Не повышать права без необходимости.
6. Плагины, отсутствующие в установленной версии, сначала искать через локальный менеджер; устанавливать только после отдельного решения и проверки совместимости, пакетов и лицензий.

## 4. Проверки скриптов и прав

В **существующей папке CubeGame** после выборочного переноса `tools/harness` в отдельной рабочей ветке и установки Node:

```powershell
node --test tools/harness/hook-pretool.test.mjs
node tools/harness/validate-project-knowledge.mjs
node tools/harness/preflight.mjs
```

`preflight` проверяет текущую ветку. Его отчёт не должен считать legacy `integration/m0-all` допустимой веткой записи. Хуки **не активированы** этой командой.

## 5. Почему нельзя сразу включать hook bridge

Официальная реализация `@deepseek-ai/dsh-hooks-claude-code` расположена в **`packages/experimental/hooks-claude-code`**, а не `packages/hooks/...`. Плагин читает один `configPath` на процесс, может затрагивать все сессии и при неудачном запуске command hook может продолжить выполнение (fail-open). Не подключать его в общем Desktop без отдельной изолированной конфигурации и реального отрицательного теста. В первую очередь использовать разрешения/sandbox и Git branch protection.

Источник: https://github.com/deepseek-ai/deepseek-harness/blob/master/packages/experimental/hooks-claude-code/README.md

## 6. Что передать на следующий шаг

Достаточно вывода `Inspect-Dsh.ps1`, версии Desktop из меню About и списка найденных skills. Не присылать полный `cordis.patch.yml`, JSON настроек, ключи, токены или API credentials.

На основе этих фактов выбирается конкретный `plugin manager` action и безопасный способ включения нужных инструментов для `cubegame-dev`.

## Выбор моделей и провайдеров

Настраивать только **DeepSeek Harness** и при необходимости **Codex** как второго исполнителя/проверяющего. Claude Code/Claude SDK не устанавливать и не подключать. `dsh-hooks-claude-code` не является Claude-агентом: это совместимость с форматом хуков, при этом экспериментальный мост сейчас оставляем отключённым.

## После успешного обнаружения навыков (D97)

Установленная версия DSH Desktop `0.2.2-rc.1` подтвердила `9/9` skills и штатный `skill`. Полный доступ (`danger-full-access`, `never`) выбран владельцем, не менять. Следующий этап — [DSH_LOCAL_SETUP_03](DSH_LOCAL_SETUP_03.md): локальный аудит и изоляция инструментов Rogalik для пресета, перенос `tools/harness`, синхронизация актуальной документации. Никаких новых worktree/workspace.
