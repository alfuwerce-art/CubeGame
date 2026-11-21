# DeepSeek Harness Desktop 0.2.2-rc.1 — фактический аудит и корректирующие действия

**Источник:** read-only отчёт агента `CUBEGAME-DSH-SKILLS-SMOKE-01`, присланный владельцем 2026-11-21. **Это заявленные результаты локального агента, не личный удалённый доступ ChatGPT к Windows.**

## Подтверждено отчётом

- Workspace `D:\Projects\CubeGame`, профиль `desktop`, активный пресет `cubegame-dev`, модель `deepseek-official / deepseek-flash`.
- 9/9 проектных skills обнаружены; skill `cubegame-doc-audit` загружен через штатный инструмент.
- Есть `pwsh` (исполняет Windows PowerShell 5.1), file tools, Git через оболочку, subagents. PowerShell 7 и system Node отсутствуют в PATH. DSH Desktop содержит встроенный Node 24.21.0, но не обещано, что он доступен из внешнего PowerShell.
- **Текущая сохранённая сессия: `danger-full-access` + `never`** — это не `workspace-write` + `ask`, заявленные в пресете.
- В CubeGame доступны `rogalik_env_check`, `rogalik_pixel_pipeline` — глобальный профиль смешивает инструменты разных проектов.
- `tools/harness/preflight.mjs` отсутствует в текущем локальном checkout; `.dsh/skills` ещё untracked. Старые `AGENTS.md` и `ACTIVE_STAGE.md` могут противоречить D95/D96.

## Приоритетный порядок

1. **Права живой сессии:** пользователь через DSH Permissions выбирает `workspace-write`. Эквивалент официального механизма (если доступен как команда в этой версии) — `/permission workspace-write`. Этот preset означает `sandbox=workspace-write`, `approval=ask`.
2. **Новые сессии:** в Settings → Permissions установить `workspace-write` как default, создать **новую** сессию, проверить фактические `sandbox` и `approval`. Изменение дефолта не переписывает уже сохранённые сессии.
3. **Сделать резервную копию DSH user profile до ручных YAML правок.** Не публиковать полный `cordis.patch.yml`, JSON сессий или ключи.
4. После действующего ограниченного режима проверить, какие global bundle добавляют rogalik tools, и выделить их в пресет Rogalik. **Не удалять и не отключать их вслепую**: Desktop профиль общий.
5. Выборочно добавить отсутствующие `tools/harness` и соответствующие новые docs через Git; исходная рабочая ветка `local/dsh-setup-20261121`. До обновления текущего `AGENTS.md` и `ACTIVE_STAGE.md` — только задачи на настройку.
6. Отдельно исполнить негативные тесты прав доступа/хуков. `dsh-hooks-claude-code` не подключать (experimental и fail-open); Claude/Claude Code как модель и агент запрещён.

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

https://github.com/deepseek-ai/deepseek-harness/blob/master/packages/interaction/permission-presets/README.md

Фактическая версия пользователя `0.2.2-rc.1`; ветка документации `master` может отличаться. При расхождении GUI/команды с установленной версией **не вручную менять YAML по документации master**.
