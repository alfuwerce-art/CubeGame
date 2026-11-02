# CubeGame

**[PROJECT_MEMORY.md — единая межчатовая память ChatGPT и DeepSeek](PROJECT_MEMORY.md)**

Для нового чата: прочитать `PROJECT_MEMORY.md` и `docs/ACTIVE_STAGE.md` перед вопросами/изменениями. Модель не открывает GitHub автоматически; нужно дать ссылку или указание в настройках проекта.

Рабочее название проекта; финальное название игры не утверждено.

- [AGENTS.md](AGENTS.md) — правила для AI-исполнителей.
- [Текущий этап](docs/ACTIVE_STAGE.md) — статус работ и следующий шаг.
- [36 утверждённых решений](docs/planning/DECISIONS_001-036.md).
- [DeepSeek Harness — настройка](docs/agents/DEEPSEEK_HARNESS_SETUP.md).
- [DeepSeek Harness — статус](docs/agents/DEEPSEEK_SETUP_STATUS.md) — локальный пресет установлен; Unity отсутствует, S00-G0 ещё не завершён.
- [Задача S00-G0](docs/stages/S00_G0_DEEPSEEK_TASK.md).
- [Архитектурный ADR-0001](docs/architecture/ADR-0001-modular-monolith.md).

Сейчас: D40 открыл параллельные S00-R01 (static-first исследование Alpha) и S01-PILOT (минимальный Unity prototype). Для пилота сперва нужна установка Unity; Blender не требуется.

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

Пилот Unity 6.3 выполнен по отчёту агента; последний коммит пока требуется запушить в рабочую ветку для ревью. Этот пилот не означает переноса всех систем Alpha. URP — отдельная техническая миграция, экспериментальный Unity Pipeline — необязателен.
