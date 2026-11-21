# DeepSeek Harness Desktop 0.2.2-rc.1 — фактический аудит и корректирующие действия

**Источник:** read-only отчёт агента `CUBEGAME-DSH-SKILLS-SMOKE-01`, присланный владельцем 2026-11-21. **Это заявленные результаты локального агента, не личный удалённый доступ ChatGPT к Windows.**

## Подтверждено отчётом

- Workspace `D:\Projects\CubeGame`, профиль `desktop`, активный пресет `cubegame-dev`, модель `deepseek-official / deepseek-flash`.
- 9/9 проектных skills обнаружены; skill `cubegame-doc-audit` загружен через штатный инструмент.
- Есть `pwsh` (исполняет Windows PowerShell 5.1), file tools, Git через оболочку, subagents. PowerShell 7 и system Node отсутствуют в PATH. DSH Desktop содержит встроенный Node 24.21.0, но не обещано, что он доступен из внешнего PowerShell.
- **Текущая сохранённая сессия: `danger-full-access` + `never`** — это не `workspace-write` + `ask`, заявленные в пресете.
- В CubeGame доступны `rogalik_env_check`, `rogalik_pixel_pipeline` — глобальный профиль смешивает инструменты разных проектов.
- `tools/harness/preflight.mjs` отсутствует в текущем локальном checkout; `.dsh/skills` ещё untracked. Старые `AGENTS.md` и `ACTIVE_STAGE.md` могут противоречить D95/D96.

## Исправление после решения владельца (D97)

Владелец специально выбрал `danger-full-access` + `approval: never`, **это не ошибка и не задача на исправление**. Не просить переводить сессию на `workspace-write`, не менять Settings Permissions. Полный доступ даёт агенту запись вне workspace, поэтому ограничения AGENTS и Git выступают процессными барьерами, не технической защитой.

## Следующие действия

1. В локальном `cubegame-dev` проверить источник `rogalik_env_check` и `rogalik_pixel_pipeline`. Изолировать **только их для CubeGame**, не отключая общий профиль Desktop, не ломая Rogalik.
2. Создать локальную резервную копию только тех DSH-конфигов, которые нужно менять; не печатать полный YAML или ключи.
3. Выборочно импортировать `tools/harness` и отсутствующие новые документы из PR #20; согласовать изменения старых AGENTS/PROJECT_MEMORY/ACTIVE_STAGE с текущими контрактами вместо безусловного перезаписывания.
4. Выполнить Windows тесты и доказать, что 9 навыков и режим прав остались неизменными. Не утверждать изоляцию, пока не проверены и CubeGame, и Rogalik.
5. Хуки и новые плагины включать только в отдельном проверенном следующем шаге.

[Утверждённое решение D97](../decisions/D97-dsh-local-profile-configuration.md) · [Локальное задание](DSH_LOCAL_SETUP_03.md).

## Проверочный текст для новой DSH сессии

```text
TASK: CUBEGAME-DSH-PERMISSIONS-02
Read-only. Show active workspace, agent preset, model, actual permissions preset,
effective sandbox mode and approval policy from THIS fresh session. Do not edit
configuration or use tools outside the workspace. List project skills count.
```

**Важно:** переключение `workspace-write` не гарантирует безопасность всех host plugins и сторонних MCP, поскольку native plugin code запускается вне файлового sandbox. Поэтому отдельный аудит global bundles обязателен.

## Справка

Официальная документация `dsh-permission-presets` описывает `/permission`, `workspace-write + ask` и `danger-full-access + never`, а также то, что default касается только новых сессий:

https://github.com/deepseek-ai/deepseek-harness/blob/master/packages/core/tools/README.md

Фактическая версия пользователя `0.2.2-rc.1`; ветка документации `master` может отличаться. При расхождении GUI/команды с установленной версией **не вручную менять YAML по документации master**.
