# Контракт задачи для ИИ — CubeGame

Перед кодом исполнитель фиксирует, не придумывая за владельца:

```yaml
task_id: TASK-...
goal: # один проверяемый результат
source_decision: # GDD / Dxx / ADR / прямое разрешение владельца
canonical_sha: # реально проверенный base SHA
branch: # отдельная review/work ветка
allowed_paths: [] # ограниченная область записи
forbidden_actions: [] # ограничения сверх AGENTS/D95
acceptance_checks: [] # какие команды и сценарии проверят результат
expected_artifacts: [] # файл / сцена / билд / отчёт / screenshot
visual_review_required: false # true для видимого игрового результата
status: PLANNED
```

Никто не должен превращать `PLANNED` в право работать над другой системой. Scope проверяется человеком и техническими средствами по возможности. Не принимать самосоставленный контракт за разрешение владельца.

**Отчёт:** `TASK_ID`, `BRANCH`, `SHA`, `FILES`, `CHECKS+RESULTS`, `WINDOWS_PLAYER`, `BLOCKERS`, `EVIDENCE`, `READY_FOR_OWNER_REVIEW` или `PARTIAL`. `ACCEPTED` и merge — решение владельца.

Новая задача — новая сессия или явный handoff; передавать короткий снимок состояния, а не всю переписку.

## Дополнительные поля для D96 (при релевантности)

```yaml
skill: cubegame-feature-planning # другой из SKILLS_INDEX по задаче
requires: [] # реально существующие системы/контракты
supplies: [] # что создаёт задача
does_not_touch: [] # запрещённые соседние модули
actual_symbols_checked: [] # exact name -> source code path
test_oracle: [] # независимое доказательство
tool_versions: [] # только реальные версии DSH/Unity/Blender/UE
runtime_or_editor_only: runtime # не путать редактор и игру
```

[Name Registry](../architecture/NAME_REGISTRY.md), [Dependency Map](../architecture/DEPENDENCY_MAP.md), [Quality Attributes](../QUALITY_ATTRIBUTES.md). Сохранять только поля, реально нужные задаче.
