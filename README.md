# CubeGame

**Рабочее название** проекта на Unity 6000.3.27f2 LTS. Референс — Cube World Alpha 2013; первый производственный фокус — максимально близкое воспроизведение Alpha, затем расширение до собственной voxel action RPG. **Это не финальное название и не опубликованная игра.**

## С чего начинать работу

1. [AGENTS.md](AGENTS.md) — правила и **карта документов с пояснением, что агент читает самостоятельно по задаче**; [PROJECT_MEMORY.md](PROJECT_MEMORY.md) — оперативный контекст.
2. [docs/ACTIVE_STAGE.md](docs/ACTIVE_STAGE.md) — **единственный актуальный статус и техническое разрешение**. Последнее документированное направление: S05A-2 IN_PROGRESS / REVIEW OPEN; S05B NOT_AUTHORIZED; проверяйте по самому файлу.
3. [AGENTS.md](AGENTS.md) — **единые правила для Claude, DeepSeek, Codex и ChatGPT**. [CLAUDE.md](CLAUDE.md) — короткий загрузчик этих правил.
4. [GDD](docs/game-design/GDD_DRAFT.md) — игровая концепция, принятые D59–D66, открытые механики.
5. [Решения D01–D36](docs/planning/DECISIONS_001-036.md) и [поздние Dxx](docs/decisions/) — канон по мере приёмки.

**Память и история:** все агенты обновляют оперативную память текущего этапа, только GPT чистит её и ведёт историю; каждый новый этап/подэтап и самостоятельная функция/механика требуют нового чата, остановки фиксируются в [чекпоинте](docs/agents/CURRENT_CHECKPOINT.md).  [PROJECT_MEMORY.md](PROJECT_MEMORY.md) — оперативная передача текущего этапа между чатами и агентами; [docs/STAGE_HISTORY.md](docs/STAGE_HISTORY.md) — краткие итоги прошлых этапов и ссылки на доказательства. По завершении принятого этапа его подробности убираются из оперативной памяти после записи истории.

## Пять проектных документов

| Направление | Файл | Статус |
|---|---|---|
| **Game Design Document** | [GDD](docs/game-design/GDD_DRAFT.md) + [план завершения](docs/game-design/GDD_COMPLETION_PLAN.md) | GDD частично утверждён; редакционный план READY_FOR_REVIEW |
| **Правила ИИ** | [AGENTS.md](AGENTS.md) + [проект объединения](docs/agents/UNIFIED_AGENT_RULES_DRAFT.md) | AGENTS действует; единый регламент READY_FOR_REVIEW |
| **Пайплайны производства** | [PRODUCTION_PIPELINE_DRAFT](docs/production/PRODUCTION_PIPELINE_DRAFT.md) | READY_FOR_REVIEW; не авторизует этапы |
| **Art Direction** | [ART_DIRECTION_DRAFT](docs/art/ART_DIRECTION_DRAFT.md) | текст READY_FOR_REVIEW; визуальные эталоны не утверждены |
| **Управление контекстом** | [SESSION_CONTEXT_PLAN](docs/agents/SESSION_CONTEXT_PLAN.md) | READY_FOR_REVIEW; до утверждения действует MULTI_AGENT_WORKFLOW |

**ROADMAP**: [ROADMAP_DRAFT](docs/planning/ROADMAP_DRAFT.md) — этапы продукта. **MULTI_AGENT_WORKFLOW**: [действующий протокол](docs/agents/MULTI_AGENT_WORKFLOW.md) — последовательная работа ИИ в одной папке `D:\Projects\CubeGame`.

## Правила статуса

- **DRAFT** — обсуждение/предложение; не авторизует реализацию.
- **READY_FOR_REVIEW** — исполнитель закончил свой объём, ждёт независимой проверки; не значит ACCEPTED.
- **ACCEPTED** — продюсер принял решение/результат; **merge в main** только после прямого разрешения.
- Изучение исходных ассетов Alpha проводится локально; закрытые материалы не публикуются.

Пакет пяти направлений подготовлен к поэтапной приёмке в ветке `docs/gdd-first-draft-20261106`, [PR #2](https://github.com/alfuwerce-art/CubeGame/pull/2). PR остаётся DRAFT и не объединён; подготовка документов сама по себе не утверждает новые игровые механики, Art Direction или Unity-этапы.
