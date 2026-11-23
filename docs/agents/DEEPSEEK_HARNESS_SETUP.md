# DeepSeek Harness — CubeGame
Workspace D:\Projects\CubeGame. DSH Desktop 0.2.2-rc.1, preset cubegame-dev, модель deepseek-official/deepseek-flash — по локальному отчёту. Права danger-full-access и approval: never — выбор владельца, не менять; это не песочница.

## Старт
Проверить Git/рабочую ветку, прочитать AGENTS, Memory, Active Stage; использовать [общий Harness](HARNESS_ARCHITECTURE.md) и [индекс](SKILLS_INDEX.md). Выбирать навыки по операциям, не все подряд. Canon .dsh/skills; проверить зеркало Codex командой sync-skills.mjs --check. Запустить preflight, task-gate start/check, реальные проверки и представить evidence. Для локального запуска см. [контракт](HARNESS_TASK_TEMPLATE.md).

## Из прежних локальных отчётов
9 skills обнаружены; validator/preflight/hook tests PASS по отчёту. Unity CLI, Editor 6000.3.27f2, компиляция, EditMode, PlayMode, Windows build/boot-smoke TOOLING-08 — PASS по отчёту; полная играбельность не проверена. Последние unity_ci.ps1 и отчёт не опубликованы. Реальное подключение DSH PreToolUse hook UNKNOWN. Новый task-gate и загрузка обновлённых навыков локально NOT_VERIFIED.

Не изменять профиль/плагины/права вне задачи. [Unity CLI](UNITY_CLI_PROTOCOL.md), [Multi-agent](MULTI_AGENT_WORKFLOW.md).
