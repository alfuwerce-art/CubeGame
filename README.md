# CubeGame

Рабочее название проекта; финальное название игры не утверждено.

- [AGENTS.md](AGENTS.md) — правила для AI-исполнителей.
- [Текущий этап](docs/ACTIVE_STAGE.md) — статус работ и следующий шаг.
- [36 утверждённых решений](docs/planning/DECISIONS_001-036.md).
- [DeepSeek Harness — настройка](docs/agents/DEEPSEEK_HARNESS_SETUP.md).
- [DeepSeek Harness — статус](docs/agents/DEEPSEEK_SETUP_STATUS.md) — исторический снимок на 2026-11-02; текущий статус этапов — только в [ACTIVE_STAGE](docs/ACTIVE_STAGE.md).
- [Задача S00-G0](docs/stages/S00_G0_DEEPSEEK_TASK.md).
- [Архитектурный ADR-0001](docs/architecture/ADR-0001-modular-monolith.md).

Сейчас: D41 — приоритет исследования Alpha (S00-R02); S01-PILOT опубликован в `stage/s01-unity-pilot` (HEAD `a884278`) и ожидает ревью. S00-R02 — `READY_FOR_REVIEW`, не принят. D42 — `PROPOSED / PENDING_PRODUCER_APPROVAL`; до OQ-23 скорости — `PLACEHOLDER_NOT_ALPHA`.

- [D40 — начало параллельного исследования и Unity-пилота](docs/decisions/D40-static-first-unity-pilot.md).
- [S00-R01: статический анализ](docs/stages/S00_STATIC_R01.md).
- [S01-PILOT: Unity-прототип](docs/stages/S01_UNITY_PILOT.md).
- [Следующее задание DeepSeek](docs/stages/NEXT_DEEPSEEK_TASK.md).

- [Unity CLI — правила DeepSeek](docs/agents/UNITY_CLI_PROTOCOL.md) — обнаружение Editor, тесты, Pipeline и версии.

## Актуальный приоритет: D41 (2026-11-03)

- [Решение D41 — исследовать Alpha до реализации новых систем](docs/decisions/D41-research-before-port.md)
- [Программа полного исследования R02, подсистемы A…L](docs/stages/S00_DEEP_RESEARCH.md)
- [Справочник подтверждённых источников и визуальных референсов](docs/research/alpha2013/REFERENCE_ATLAS.md)
- [Что реализуется средствами Unity, а не копированием Plasma](docs/architecture/UNITY_REUSE_MAP.md)
- [Задание для DeepSeek](docs/stages/DEEPSEEK_RESEARCH_TASK.md)

Пилот Unity 6.3 опубликован по отчёту агента; результаты (EditMode 10/10, PlayMode 2/2) независимо не перепроверялись в этой сессии. Пилот не означает переноса всех систем Alpha. URP — отдельная техническая миграция, экспериментальный Unity Pipeline — необязателен. S00-R02 review-fix: противоречия `stat_calcAttackDamage`/`+0x70`/D42 исправлены, см. [ACTIVE_STAGE](docs/ACTIVE_STAGE.md).
