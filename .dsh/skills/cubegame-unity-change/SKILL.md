---
name: cubegame-unity-change
description: Use for an authorized Unity code or gameplay change in CubeGame; check scope, architecture, tests and player evidence before review.
---
# CubeGame — Unity feature workflow

1. Прочитать `AGENTS.md`, `PROJECT_MEMORY.md`, `docs/ACTIVE_STAGE.md`, `docs/CONCEPT.md`, соответствующий GDD, ADR и [task contract](../../../docs/agents/HARNESS_TASK_TEMPLATE.md).
2. Убедиться, что текущий активный этап и прямое поручение владельца **разрешают** данную конкретную игровую задачу. D95 закрыл модельный конвейер; никакие старые Factory/Alpha работы не перезапускать.
3. `git status` + source SHA; `node tools/harness/preflight.mjs`; только своя ветка, только разрешённые файлы. Если preflight BLOCKED — остановить изменение.
4. Соблюдать принятый модульный монолит: правила/состояние не принадлежат UI, MonoBehaviour и аниматору. Data-driven слои — отдельный ADR-кандидат; не утверждать автоматически рефакторинг и новые API.
5. Писать минимальный runtime-код по утверждённому scope, без параллельного алгоритма. Проверить совместимость с сохранениями, RNG/seed/ID, NPC, интерфейсом и другими затронутыми функциями.
6. Прогнать *фактические* тесты по задаче, проверить XML тестов, сцены, билд/Windows Player и изображение/геймплей, если требуется. `NOT_RUN` честнее ложного `PASS`.
7. Отдать доказательства и PR; статус максимум `READY_FOR_OWNER_REVIEW`. Владелец решает `ACCEPTED`.
