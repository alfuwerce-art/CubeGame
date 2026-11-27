# Codex — подключение к CubeGame
Открывать Codex в D:\Projects\CubeGame (корень Git), с актуальной утверждённой веткой и отдельной рабочей веткой задачи. Не открывать внешний UE-проект как единственный workspace вместо CubeGame: при разрешённой UE-задаче указать его фактический путь отдельно, проверить открытый редактор/версию и ограничения. Не выполнять автоматический stash/reset/switch поверх чужих изменений.

Codex использует корневой AGENTS.md и репозиторные Skills из .agents/skills (официальный пример: https://developers.openai.com/blog/skills-agents-sdk). В CubeGame .dsh/skills — каноническое содержимое, .agents/skills — синхронизированное зеркало. Проверка: `node tools/harness/sync-skills.mjs --check`. Если Codex не обнаружил навык в интерфейсе — прочитать соответствующий SKILL.md вручную и отметить discovery NOT_VERIFIED. Не загружать все навыки сразу.

## Первый запуск после интеграции
1. В новой сессии Codex открыть корень репозитория, проверить HEAD, AGENTS/Memory/Active Stage и список skills; сверить актуальность локальной копии без уничтожения работы.
2. Проверить Node, `node tools/harness/sync-skills.mjs --check`, `node --test tools/harness/task-gate.test.mjs`, `node tools/harness/validate-project-knowledge.mjs`. В рабочей ветке выполнить preflight.
3. Для конкретной задачи проверить фактический editor/MCP/Python/Control Rig и разрешённые операции. Вызвать read-only probe, а не делать вывод по наличию документа. Не устанавливать внешние skills/plugins автоматически.
4. Создать контракт, выполнить start/check по [Harness](HARNESS_ARCHITECTURE.md), приложить evidence. Настоящий hook Codex здесь не установлен; возможность подключения требует проверки версии и отдельного разрешения на локальную конфигурацию. Не заявлять принудительную защиту UI/MCP.

## Как ставить задачи
Обычный запрос: «Продолжи женский манекен, исправь только грудь, пресс и таз по приложенному референсу. Остальное сохрани». Агент сам находит документы, исходник, ограничения, выбирает skills и выполняет проверки; если сохранённый ассет или референс недоступен, сообщает точный блокер. Для локальной работы не требовать нового ТЗ на каждый элемент. Результат — краткий отчёт + открытая/воспроизводимая сцена по запросу, без самовольного merge.

Статус подключения на ПК владельца: NOT_VERIFIED. Наличие этих файлов в Git не подтверждает локальную загрузку, работу инструмента или улучшение качества.

For animation tasks keep cubegame-rig-animation as the primary skill even for editor launch, MCP, search, report, memory and archive. Do not switch to external docs skills; art-validation is subordinate. For animation tasks read [quality protocol](animation/QUALITY_PROTOCOL.md), resume actual checkpoint and include animation contract/result fields in task-gate. Current workstream (pilot history and next step): [RUN-02](animation/RUN02_CHECKPOINT.md) and [Review Lab](animation/REVIEW_LAB.md). Verify discovery and actual tools; no automatic reset/switch over local work.
