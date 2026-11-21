# CubeGame — контракт задачи
Заполнять только нужные поля, не пересказывать весь проект.

```yaml
task_id: ""
goal: ""
source: ""              # утверждённый GDD / решение владельца / ADR
base_sha: ""
branch: ""
allowed_paths: []
does_not_touch: []
acceptance_checks: []
runtime_player_scenario: ""  # при изменении игры
expected_artifacts: []
status: PLANNED
```

В результате: `TASK / SHA / FILES / TESTS / PLAYER / EVIDENCE / BLOCKERS`. `PASS` каждого теста подтверждается командой и логом. `READY_FOR_OWNER_REVIEW` не равен `ACCEPTED`.
