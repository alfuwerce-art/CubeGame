# CubeGame

Самостоятельная 3D action RPG на Unity с процедурными регионами, воксельным окружением и развивающимся миром.

**Единственный рабочий репозиторий игры — CubeGame.** Отдельный конвейер автоматического производства моделей закрыт по [D95](docs/decisions/D95-single-repository-close-model-pipeline.md).

- [GDD](docs/GDD.md), [Roadmap](docs/ROADMAP.md) и [действующие решения](docs/design/DECISIONS.md).
- [Активная задача](docs/ACTIVE_STAGE.md), [память](PROJECT_MEMORY.md), [правила ИИ](AGENTS.md).

## Навигация по разработке через ИИ

- [Карта проекта](docs/CONCEPT.md), [Art Direction](docs/art/ART_DIRECTION.md), [качество](docs/QUALITY_ATTRIBUTES.md).
- [Сравнение подходов из трёх видео](docs/agents/AI_WORKFLOW_COMPARISON.md) и [решение D96](docs/decisions/D96-ai-harness-unreal-research.md).
- [Навыки DeepSeek Harness](docs/agents/SKILLS_INDEX.md), [оценка плагинов](docs/agents/DSH_PLUGIN_EVALUATION.md), [настройка DSH](docs/agents/DEEPSEEK_HARNESS_SETUP.md).
- [Реестр технических имён](docs/architecture/NAME_REGISTRY.md) и [карта зависимостей](docs/architecture/DEPENDENCY_MAP.md).
- [Unreal scripting spike](docs/research/UNREAL_SCRIPTING_SPIKE.md) — отдельное исследование, **не** миграция игры и **не** разрешение на установку.

## Настройка DeepSeek Harness на Windows

[Проверка и первый запуск](docs/agents/WINDOWS_DSH_FIRST_RUN.md): read-only диагностика установленного DSH, обзор девяти навыков, изолированный Git worktree для ревью, тесты без автоматической установки плагинов.

**ИИ-исполнители разработки:** DeepSeek Harness и Codex (без Claude/Claude Code). Использование стороннего hook-формата не добавляет другого агента.
