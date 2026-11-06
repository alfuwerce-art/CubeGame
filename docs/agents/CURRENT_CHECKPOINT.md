# CubeGame — текущий checkpoint / точка восстановления

**Статус:** TEMPLATE / EMPTY. Здесь нет подтверждённого checkpoint текущей разработки. Текущий исполнитель заполняет файл **после каждой остановки**, передачи или значимого завершённого блока в своей Git-ветке. Не считать шаблон выполненной работой.

```text
CHECKPOINT_STATUS: EMPTY | SAVED | BLOCKED
UPDATED_AT:
STAGE:
TASK_ID:
GOAL:
OWNER_AGENT:
BRANCH:
HEAD_SHA:
WORKTREE_STATUS:
DONE:
FILES_CHANGED:
TESTED:
NOT_RUN:
BLOCKERS:
REMAINING:
NEXT_STEP:
REFERENCES: (TASK, report, commit, Dxx)
```

## Правила

- Все агенты обновляют `PROJECT_MEMORY.md` по текущему этапу; **только GPT** очищает устаревшее и ведёт `docs/STAGE_HISTORY.md`.
- По слову **«продолжаем»** открыть `PROJECT_MEMORY.md`, `docs/ACTIVE_STAGE.md`, `AGENTS.md`, этот checkpoint; сверить BRANCH/HEAD с Git и продолжить NEXT_STEP, только если задача разрешена.
- Новый этап, подэтап или новая самостоятельная функция/механика — новая сессия. Исправления той же функции могут оставаться в текущей.
- Не утверждать `PASS` за `NOT_RUN`, не считать checkpoint разрешением нового этапа. Чужой worktree не чистить без согласования.
- Не публиковать секреты, приватные файлы и закрытые ассеты Alpha. Checkpoint должен быть коротким; доказательства — в отчётах и коммитах.
