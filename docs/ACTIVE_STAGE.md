# ACTIVE STAGE — G1 TECHNICAL FOUNDATION

**Canonical branch:** `integration/m0-all` · **G1 baseline branch:** `deepseek/g1-00-runtime-baseline-candidate`
**Status:** `ACTIVE` — G1-00 (Runtime Baseline Selection) завершён, baseline выбран: **OPTION C**
**Решение продюсера:** 2026-11-17 — CubeGame разрабатывается как **собственная игра**; полный перенос Cube World Alpha 2013 прекращён как active target ([CR-001](planning/DECISIONS_001-036.md#cr-001--parity-больше-не-является-целью-первой-версии-2026-11-17)).
**Рабочая дорожная карта:** [ROADMAP_DRAFT.md](planning/ROADMAP_DRAFT.md) (коммит `4e36f1539d787e6a03f85d1f9fc92efcb1b64a79`)
**Отчёты:** [G1-00 baseline selection](reports/G1_00_RUNTIME_BASELINE_SELECTION_REPORT.md) · [G0 pivot](reports/G0_DEVELOPMENT_PIVOT_REPORT.md)

---

## 0. Baseline G1 (зафиксирован 2026-11-17)

| Параметр | Значение |
|---|---|
| Основа | полный runtime цепи P0–P7 (`0533698..85ba64a`, 104 линейных коммита, 278 файлов, +29 197/−2 024) |
| Candidate-ветка | `deepseek/g1-00-runtime-baseline-candidate` (= canonical + pivot-документы + P0–P7) |
| Код | 328 `.cs` / 72 400 строк; код побайтово равен P7-tip |
| Компиляция | `error CS = 0`, 38 warnings |
| EditMode | **853/859** с Alpha-кэшем (0 fail, 6 skip) · 845/859 без кэша (1 content-presence fail, 11 skip) |
| PlayMode | **75/75** с кэшем · 39/75 без кэша (3 content-presence fail, 33 skip) |
| Player | сборка `errors=0`; сценарии new/load/continue/attack/UI/save — PASS в обоих режимах |
| Решение | **OPTION C**: P0–P7 — база; перед слиянием в canonical выполняется ограниченный cleanup C1–C4 |

Перед слиянием candidate в canonical обязательны C1–C4 из [G1-00 отчёта §12](reports/G1_00_RUNTIME_BASELINE_SELECTION_REPORT.md). До этого canonical `integration/m0-all` остаётся исторической базой F1–F3.

---

## 1. Что мы делаем сейчас

CubeGame — самостоятельная 3D action RPG с кубическим voxel-миром, процедурной генерацией, живыми NPC, боем, RPG-прогрессией и собственным визуалом. Название игры не утверждено.

Что **не** изменилось этим pivot:

- GDD и утверждённые игровые решения сохраняются: [DECISIONS_001-036.md](planning/DECISIONS_001-036.md);
- идея мира, NPC, городов, квестов, развития, моделей, анимаций, графики и будущих систем не пересобирается;
- ранее сделанный код, тесты, исследования и отчёты не выбрасываются.

Что изменилось: **способ разработки и порядок этапов**. Alpha 2013 больше не является спецификацией CubeGame. Она остаётся источником исследований, алгоритмов, формул и инженерных референсов, когда существует конкретный вопрос CubeGame.

---

## 2. Старый M0

`M0 ALPHA FULL UNITY PORT` переведён в статус:

`LEGACY / RESEARCH BASE / NOT A RELEASE TARGET`

- P7 / P8 / P9 / P10 автоматически **не продолжаются**.
- Alpha parity backlog ([M0_ALPHA_PARITY_BACKLOG.md](planning/M0_ALPHA_PARITY_BACKLOG.md)) больше **не** blocking registry продукта; это исторический исследовательский реестр.
- Все старые отчёты, decompilation evidence, таблицы, фикстуры и тесты сохраняются как доказательная база.
- Исторические документы M0 не переписываются задним числом; на них стоит пометка `LEGACY`.

---

## 3. Порядок этапов

| Этап | Смысл | Документ |
|---|---|---|
| **G0** | pivot, reuse map, baseline freeze | этот файл, [G0 reuse audit](planning/G0_EXISTING_WORK_REUSE_AUDIT.md) |
| **G1** | Technical Foundation: устойчивая платформа CubeGame | [G1_TECHNICAL_FOUNDATION_STAGE.md](stages/G1_TECHNICAL_FOUNDATION_STAGE.md) |
| **G2** | World Foundation: генерация как технология CubeGame | [G2_WORLD_FOUNDATION_AUDIT.md](planning/G2_WORLD_FOUNDATION_AUDIT.md) |
| **G3** | Character & Creature Factory | [M1](stages/M1_MODEL_ANIMATION_EVOLUTION_STAGE.md) как технический референс |
| G4–G8 | combat, NPC/living world, RPG/economy, core gameplay prototype, vertical slice | [Roadmap](planning/ROADMAP_DRAFT.md) |
| G9–G12 | production: world/visual, characters, content/quests, UI/UX/audio/VFX | [Roadmap](planning/ROADMAP_DRAFT.md) |
| G13–G15 | Alpha / Beta / Release Candidate | [Roadmap](planning/ROADMAP_DRAFT.md) |

Ближайшая цель после G0 — **G1 Technical Foundation**, затем G2 World Foundation и G3 Character/Creature Factory как параллельные крупные направления.

---

## 4. Что запрещено без отдельного решения продюсера

- продолжать P7/P8/P9/P10 старого M0;
- закрывать Alpha parity backlog ради самого backlog;
- восстанавливать Alpha UI / audio / VFX / save format ради parity;
- добиваться побайтового parity с Cube World;
- удалять результаты старого M0, reverse-engineering evidence и старые отчёты;
- переписывать GDD и утверждённые игровые решения;
- начинать проект с нуля или выбрасывать рабочий код за то, что он появился при Alpha port.

Главный принцип: `REUSE BEFORE REWRITE`.

---

## 5. Технический baseline

Зафиксирован в [G1-00 отчёте](reports/G1_00_RUNTIME_BASELINE_SELECTION_REPORT.md): ветка `deepseek/g1-00-runtime-baseline-candidate` (canonical + pivot-документы + полный runtime P0–P7), Unity `6000.3.27f2`, `error CS = 0`, EditMode 853/859, PlayMode 75/75, Windows Player собирается и проходит сценарии new/load/continue/attack/UI/save в двух режимах (с Alpha-кэшем и без).

Важно: canonical `integration/m0-all` всё ещё содержит runtime F1–F3. P0–P7 интегрирован в candidate, но **не слит** в canonical: слияние выполняется после ограниченного cleanup C1–C4 ([§12 G1-00 отчёта](reports/G1_00_RUNTIME_BASELINE_SELECTION_REPORT.md)). Работать следует от candidate.

---

## 6. Где искать Alpha research

- [docs/research/alpha2013/](research/alpha2013/) — исследования, спецификации поведения, каталоги, таблицы, инструменты;
- [docs/reports/](reports/) — отчёты P0–P7, F1–F4, D-серии, аудиты;
- [docs/planning/M0_ALPHA_PARITY_BACKLOG.md](planning/M0_ALPHA_PARITY_BACKLOG.md) — что осталось незакрытым в старом M0;
- `tools/alpha_*` — извлечение и верификация данных Alpha (только локально, не публикуется).

Исследование Alpha разрешено только под конкретный вопрос CubeGame, в формате:

`CUBEGAME PROBLEM → CURRENT IMPLEMENTATION → ALPHA RESEARCH → EXTERNAL RESEARCH → OPTIONS → CUBEGAME DECISION → IMPLEMENTATION → PLAYTEST`

Не является задачей: «нашли ещё неизвестную Alpha-функцию → надо её восстановить». Если функция не нужна нашей игре, она не blocker.

---

## 7. Definition of Done для любой системы

`SPEC/DESIGN → IMPLEMENTED → INTEGRATED → TESTED → WINDOWS PLAYER → SAVE/LOAD (если применимо) → PERFORMANCE (если применимо) → PLAYTEST → PRODUCER REVIEW`

Статусы: `RESEARCH` · `PROTOTYPE` · `INTEGRATED` · `READY_FOR_PRODUCER_REVIEW` · `ACCEPTED` · `REWORK` · `DEFERRED`.

Старые маркеры `ALPHA_CODE_STATIC / EMULATED_ALPHA_CODE / ALPHA_DECOMPILED / CUBEGAME_CONTRACT / PLACEHOLDER_NOT_ALPHA / PARTIAL / UNKNOWN` остаются допустимыми для **legacy и research** материалов и больше не являются глобальными blockers CubeGame.

`ACCEPTED` ставит только продюсер.

---

## 8. Предыдущие этапы

Предыдущая схема (D92 → M0 ALPHA FULL UNITY PORT, P0–P7) — `LEGACY`, см. [M0_ALPHA_FULL_UNITY_PORT_STAGE.md](stages/M0_ALPHA_FULL_UNITY_PORT_STAGE.md). Историю статусов F1–F4 и P0–P7 смотреть в [PROJECT_MEMORY.md](../PROJECT_MEMORY.md) и в отчётах `docs/reports/M0_*`.
