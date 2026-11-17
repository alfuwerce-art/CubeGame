# CubeGame — проектные документы

**Рабочее название:** CubeGame; окончательное название игры не утверждено.
**Дата обновления:** 2026-11-17.
**Статус:** CubeGame разрабатывается как **собственная игра**. Реализация идёт с 2026-11-02 и продолжается; формулировка прежних версий этого файла «реализация ещё не начата / текущая фаза — архитектурный разбор» **недействительна**.

## 1. Рабочая дорожная карта

- [ROADMAP_DRAFT.md](ROADMAP_DRAFT.md) — **действующая дорожная карта CubeGame** (G0…G15). Файл сохранил историческое имя `*_DRAFT`, но содержательно это рабочая карта: решение продюсера 2026-11-17, коммит `4e36f1539d787e6a03f85d1f9fc92efcb1b64a79`.
- [ACTIVE_STAGE](../ACTIVE_STAGE.md) — что делается прямо сейчас. Текущий основной фокус — **G3 HUMANOID CHARACTER FACTORY**, начиная с G3-0 Humanoid Race Mannequins.

## 2. Текущий этап и его документы

- [G3_HUMANOID_CHARACTER_FACTORY_STAGE.md](../stages/G3_HUMANOID_CHARACTER_FACTORY_STAGE.md) — исполнимый порядок G3-0…G3-6.
- [D94](../decisions/D94-g3-humanoid-modular-appearance.md) — утверждённое решение: общая библиотека внешности игрока/NPC, простой детерминированный race-based randomizer, существа по фактической потребности.
- [M1_MODEL_ANIMATION_EVOLUTION_STAGE.md](../stages/M1_MODEL_ANIMATION_EVOLUTION_STAGE.md) — архитектурный reference Factory/rig/animation, синхронизированный с D94.
- [G2_WORLD_FOUNDATION_AUDIT.md](G2_WORLD_FOUNDATION_AUDIT.md) — принятый фундамент мира, на чьи entity seams подключается G3.
- [G0_EXISTING_WORK_REUSE_AUDIT.md](G0_EXISTING_WORK_REUSE_AUDIT.md) и [G1_RUNTIME_REUSE_MATRIX.md](G1_RUNTIME_REUSE_MATRIX.md) — история выбора текущего runtime baseline.

## 3. Главный источник игровых требований

- [DECISIONS_001-036.md](DECISIONS_001-036.md) — **утверждено продюсером**: 36 игровых и организационных решений. Идея игры, мир, NPC, города, квесты, развитие, модели, анимации, графика и будущие системы остаются в силе.

**Оговорка о методе (2026-11-17).** Решения D01, D14 и D20 формулируют первый крупный результат как «максимально близкое соответствие Cube World Alpha 2013». Это была формулировка **способа разработки и критерия приёмки**, а не отдельная игровая механика. Решением продюсера от 2026-11-17 полный Alpha port прекращён как active target, поэтому критерий parity из D01/D14/D20 больше не действует как обязательный. Это оформлено записью [CR-001](DECISIONS_001-036.md#cr-001--parity-больше-не-является-целью-первой-версии-2026-11-17) в самом реестре: исходный текст решений сохранён, к трём затронутым строкам добавлена пометка `superseded: CR-001`, остальные 33 решения не изменялись. См. также [D93](../decisions/D93-development-pivot-own-game.md).

## 4. Архитектура

- [ADR-0001 — модульный монолит](../architecture/ADR-0001-modular-monolith.md) — **утверждено**.
- [ARCHITECTURE_PROPOSAL.md](ARCHITECTURE_PROPOSAL.md) — предложение архитектуры v0.2, не утверждено целиком; ADR-0001 и действующие решения имеют приоритет.
- [UNITY_REUSE_MAP.md](../architecture/UNITY_REUSE_MAP.md) — какие подсистемы закрывает Unity 6.3, а что пишем сами.
- [UNIVERSAL_ENTITY_CONTRACT.md](../architecture/UNIVERSAL_ENTITY_CONTRACT.md), [UNIVERSAL_ASSET_ANIMATION_PIPELINE.md](../architecture/UNIVERSAL_ASSET_ANIMATION_PIPELINE.md) — архитектурные контракты, используемые текущим G3.
- [WORKFLOW_PROTOCOL_DRAFT.md](WORKFLOW_PROTOCOL_DRAFT.md) — исторический черновик протокола работы, не действующий регламент.

## 5. Legacy: старый M0 (не active target)

`M0 ALPHA FULL UNITY PORT` — `LEGACY / RESEARCH BASE / NOT A RELEASE TARGET`.

- [M0_ALPHA_FULL_UNITY_PORT_STAGE.md](../stages/M0_ALPHA_FULL_UNITY_PORT_STAGE.md) — ТЗ старого M0 (историческое).
- [M0_ALPHA_PARITY_BACKLOG.md](M0_ALPHA_PARITY_BACKLOG.md) — незакрытые детали Alpha; теперь исследовательский реестр, а не blocking registry.
- [M0_D88_FULL_WORK_PLAN.md](M0_D88_FULL_WORK_PLAN.md), [M0_FULL_ALPHA_INTEGRATION.md](M0_FULL_ALPHA_INTEGRATION.md), [M0_00_FULL_TRANSFER_MAP.md](M0_00_FULL_TRANSFER_MAP.md), [M0_TRANSFER_MASTER_MATRIX.md](M0_TRANSFER_MASTER_MATRIX.md), [M0_TEAM_TRANSFER_MATRIX.md](M0_TEAM_TRANSFER_MATRIX.md), [M0_REMAINING_PLAN_D90.md](M0_REMAINING_PLAN_D90.md) — исторические планы и матрицы.

Эти документы сохраняются как evidence. Они не задают порядок активной разработки и не должны читаться как текущие задачи. Отчёты P0–P7 и F1–F4 остаются в [docs/reports](../reports/).

## 6. Исследования Alpha

- [docs/research/alpha2013/](../research/alpha2013/) — карта архитектуры Alpha, спецификации поведения, каталоги моделей/анимаций/предметов, инструменты верификации.
- [S00_ALPHA_RESEARCH.md](../stages/S00_ALPHA_RESEARCH.md), [S00_DEEP_RESEARCH.md](../stages/S00_DEEP_RESEARCH.md) — исторические ТЗ исследования.
- Alpha 2013 — исследовательский и инженерный референс, а не спецификация CubeGame. Исследование выполняется только под конкретный вопрос CubeGame (формат цикла — в [ACTIVE_STAGE](../ACTIVE_STAGE.md) §6).

## 7. Основание

GitHub — источник актуальных версий документов. При расхождении документов приоритет: решение продюсера → `ACTIVE_STAGE` → дорожная карта → утверждённый реестр решений → ТЗ активного этапа → исторические документы.
