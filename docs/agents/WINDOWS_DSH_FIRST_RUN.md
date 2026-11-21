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

Рекомендуемый способ без нарушения текущей рабочей копии: сначала из `D:\Projects\CubeGame` выполнить `git fetch origin review/ai-game-development-suite-20261121`, затем создать **отдельный Git worktree** как sibling, если папка свободна:

```powershell
git -C 'D:\Projects\CubeGame' worktree add -b local/harness-eval 'D:\Projects\CubeGame-Harness-Review' 'origin/review/ai-game-development-suite-20261121'
```

Это новая рабочая папка **того же Git-репозитория**, не новый самостоятельный проект Factoria. Не выполнять без просмотра `git status`, версии Git и существующих worktree. Не перезаписывать существующий путь. Дальнейшие тесты/CLI выполнять в новой папке (а не слепо в `D:\Projects\CubeGame`).

## 3. Проверка в DSH Desktop

1. Открыть DeepSeek Harness Desktop → выбранную рабочую папку review worktree.
2. Зафиксировать фактическую версию из **About DeepSeek Harness** (не ориентироваться на README из master как на установленную версию).
3. Проверить наличие skills через модельный каталог или явный вызов `/cubegame-doc-audit`; убедиться, что DSH прочитал полный `SKILL.md`, а не придумал содержание.
4. Дать сначала **read-only** задачу: `Прочитай AGENTS, ACTIVE_STAGE и CONCEPT. Расскажи список доступных project skills, не меняй файлы.`
5. Затем проверить возможности PowerShell и sandbox в UI. Не повышать права без необходимости.
6. Плагины, отсутствующие в установленной версии, сначала искать через локальный менеджер; устанавливать только после отдельного решения и проверки совместимости, пакетов и лицензий.

## 4. Проверки скриптов и прав

В *review worktree* (`D:\Projects\CubeGame-Harness-Review`), после установки Node:

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
