# DeepSeek Harness — CubeGame
**Workspace:** `D:\Projects\CubeGame`. **DSH Desktop:** 0.2.2-rc.1. **Preset:** `cubegame-dev`; **модель:** `deepseek-official/deepseek-flash`. Это состояние по локальному отчёту, а не удалённая инспекция ChatGPT.
**Права владельца:** `danger-full-access` и `approval: never`. Не изменять. Сами по себе эти права не ограничивают доступ файловой песочницей.

## Старт каждой задачи
1. `git status`, ветка, remote, HEAD, локальные изменения.
2. Прочитать AGENTS, PROJECT_MEMORY, ACTIVE_STAGE и только релевантные материалы по задаче.
3. Выбрать один skill из [индекса](SKILLS_INDEX.md); выполнить `node tools/harness/preflight.mjs` в рабочей ветке.
4. Работать только в разрешённых файлах. Прогнать проверки и документировать фактическое доказательство; передать SHA/ветку.

## Подтверждённые возможности из локальных отчётов
9 skills обнаружены; validator PASS; preflight PASS; тесты hook-проверки прошли. Unity CLI, Editor 6000.3.27f2, компиляция, EditMode, PlayMode, Windows-сборка и boot-smoke по задаче TOOLING-08 — PASS по отчёту DeepSeek; полная играбельность не проверялась. Последний `tools/unity/unity_ci.ps1` и отчёт пока не опубликованы в GitHub. Реальный DSH PreToolUse hook не подтверждён и не должен считаться активной защитой.

## Границы
Профиль и плагины вне задачи не изменять; не устанавливать инструменты по умолчанию. Инструкции проверки Unity — [UNITY_CLI_PROTOCOL](UNITY_CLI_PROTOCOL.md); распределение исполнителей — [MULTI_AGENT_WORKFLOW](MULTI_AGENT_WORKFLOW.md).

## Unreal-first (текущая задача)
Контракт [U0–U4](../experiments/UNREAL_FIRST_PLAYABLE.md). Настройки `cubegame-dev` не переписывать; отдельный `cubegame-unreal` preset возможен после проверки реальной версии/формата DSH. Сначала проверить установленную UE, Unreal MCP, Editor Python, C++ toolchain и реальное MCP-соединение. Наличие документа или теста hook не означает, что инструмент подключён.

Весь экспериментальный проект — `Unreal/CubeGameTrial/` внутри CubeGame. C++-first: источники `Source/` с осмысленными комментариями и тестами; Editor Python — для повторяемых изменений контента. Blueprint только при объективной пользе. Не менять `Assets/`, `Packages/`, `ProjectSettings/` Unity и глобальные DSH-плагины в рамках этого эксперимента.

MCP включать только локально, после проверки конфигурации и реальной безопасности endpoint. Командные hooks только как дополнительная эвристика. Отчёт `ENGINE_VERSION / TOOLCHAIN / MCP_CALL / C++_BUILD / PYTHON_RUN / PIE / WIN64_BUILD / FPS / RAM / VRAM / BLOCKERS`.
