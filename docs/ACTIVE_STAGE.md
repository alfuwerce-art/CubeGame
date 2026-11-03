# CubeGame — ACTIVE_STAGE

**Дата решения:** 2026-11-03. **Межчатовая память:** [PROJECT_MEMORY.md](../PROJECT_MEMORY.md). **Рабочее название:** CubeGame (имя игры не утверждено).
**Ответственные:** DeepSeek Harness — исполнитель; ChatGPT — архитектура и ревью; продюсер — игровые и визуальные решения.
**Активный этап:** **`S03-COMBAT-CORE`** — партия выполнена, статус **`READY_FOR_REVIEW`** (не `ACCEPTED`); авторизация [D44](decisions/D44-s02-accepted-s03-authorized.md), ветка `stage/s03-combat-core`, ТЗ [S03_COMBAT_CORE.md](stages/S03_COMBAT_CORE.md), отчёт [S03_REPORT.md](stages/S03_REPORT.md).
**Предыдущий этап:** **`S02-CORE-FOUNDATION` → `ACCEPTED`** (решение продюсера 2026-11-03, ревью ChatGPT `PASS`); `S00-R02` закрыт `ACCEPTED` ранее.
**D42:** [числа Alpha как референсы](decisions/D42-alpha-numbers-as-references.md) — статус **`ACCEPTED`** (решение продюсера 2026-11-03, утверждено независимо от OQ-23). До закрытия OQ-23 конкретные скорости в прототипе помечаются **`PLACEHOLDER_NOT_ALPHA`**; это не отменяет утверждение D42 и не является окончательным балансом.
**Архитектура:** модульный монолит [ADR-0001](architecture/ADR-0001-modular-monolith.md) утверждён; числовые/технические параметры — по D42.

## S03-COMBAT-CORE — ПАРТИЯ ВЫПОЛНЕНА, READY_FOR_REVIEW

**Статус:** `READY_FOR_REVIEW` (не `ACCEPTED`). Авторизация — [D44](decisions/D44-s02-accepted-s03-authorized.md), 2026-11-03.
**Ветка:** `stage/s03-combat-core` от принятого коммита S02 (`1df7413`); последний проверенный опубликованный HEAD — **`a6735301ecebb19717e7fa0948f866681d3e8205`** (коммит синхронизации памяти — после него). Merge в `main` не выполнялся.
**ТЗ:** [S03_COMBAT_CORE.md](stages/S03_COMBAT_CORE.md). **Отчёт:** [S03_REPORT.md](stages/S03_REPORT.md).
**Фактические результаты (Unity 6000.3.27f2, выполнено DeepSeek):** после **S03-REVIEW-FIX-01** — компиляция PASS без `error CS`/`warning CS`; EditMode **251/251 passed**, PlayMode **10/10 passed**, failed=0. Первая партия дала 232/10; REVIEW-FIX-01 добавил +19 EditMode. Регрессия S01 (10/10 EditMode, 2/2 PlayMode) и S02 (все фикстуры) внутри прогона. Эталонные скрипты `alpha_curves/items/combat --check` — PASS (exit 0); `git diff --check` — чисто.
**S03-REVIEW-FIX-01 (2026-11-03):** (FIX-01) `AbsorbedDamage = min(raw, blockFraction·armor)`, расчётная защита вынесена в `DefenseCapacity`, сумма absorbed+final = raw; (FIX-02) `AttackIdMultiplierApplied` только при найденном множителе; неизвестный ID не меняет урон; (FIX-03) `BlockThreshold` отвергает уровень < 1 (и через `RollBlock`). Публичные контракты `MitigationResult`/`CombatOutcome` расширены полем `DefenseCapacity`; порядок RNG и изменение HP не менялись; статус митигации остаётся `PARTIAL`. Подробности — [S03_REPORT.md](stages/S03_REPORT.md) §5a.
**Реализовано:** `Core/Combat` — формулы урона, разброс, проверка попадания, митигация, пул здоровья, результат удара, резолвер; `Presentation/Combat/CombatDemo` — минимальная демонстрация удара. Ядро без UnityEngine (`noEngineReferences: true`).
**Границы достоверности:** `REFERENCE_VERIFIED` — урон оружия/`levelFactor`, попадание, порог блока; `STRONGLY_SUPPORTED` — разброс урона; `PARTIAL` — множители ID атак (только по явному флагу), модификатор шанса по подтипу оружия, митигация и её порядок, `blockFraction`; `UNKNOWN` — базовая формула `chance` (OQ-01).
**Сохранённые ограничения:** OQ-01/OQ-02/OQ-24/OQ-25/OQ-26 не закрыты; OQ-22/OQ-23 — как в S00/S02. Способности, комбо, тайминги, threat/poise, элементальные проки, анимации/VFX, AI — вне партии.

## S02-CORE-FOUNDATION — ПРИНЯТ (`ACCEPTED`, 2026-11-03)

**Статус:** **`ACCEPTED`** — решение продюсера 2026-11-03 ([D44](decisions/D44-s02-accepted-s03-authorized.md)); архитектурное ревью ChatGPT — `PASS`. Приёмка ограниченная: приняты модули A–D как техническая основа, а не паритет с Alpha 2013.
**Ветка:** `stage/s02-core-foundation` (от `stage/s01-unity-pilot` @ `a884278`); последний проверенный HEAD — **`626f21ed3d83f258386b35165a567ff52ec8b31c`** (+ коммит закрытия S02). Merge в `main` не выполнялся.
**ТЗ:** [S02_CORE_FOUNDATION.md](stages/S02_CORE_FOUNDATION.md). **Отчёт с фактическими прогонами:** [S02_REPORT.md](stages/S02_REPORT.md).
**Объём принятой партии:** модули A — Progression, B — Character Stats, C — Items Foundation, D — Persistence Foundation. Чистая логика отделена от Unity-представления (ADR-0001).
**Фактические результаты (после REVIEW-FIX-01, Unity 6000.3.27f2, выполнено DeepSeek):** компиляция PASS (без `error CS`/`warning CS`); EditMode **170/170 passed** (включая 10 регрессионных кейсов пилота); PlayMode **6/6 passed** (4 новых smoke + 2 регрессионных пилота); failed=0. Логи/XML — локально в `.unity-install/` (в `.gitignore`): `rf01_editmode.xml`, `rf01_playmode.xml`.
**REVIEW-FIX-01 (2026-11-03):** закрыты дефекты ревью — неполная валидация сохранений, переполнения и зависание прогрессии, переполнение `int` в арифметике seed, две неточности в эталонных таблицах. FIX-01/02/04 — `DONE`; FIX-03 — `DONE` при достоверности `PARTIAL`.
**Сохранённые ограничения S02:** OQ-25 `OPEN`, OQ-26 `OPEN`, seed-арифметика для крайних значений `PARTIAL`, множители типа атаки в S02 не реализованы (`UNKNOWN`).

## S00-DEEP-RESEARCH (R02) — ЭТАП ЗАКРЫТ

**Статус:** **`ACCEPTED`** — решение продюсера **2026-11-03** (`S00-R02 CLOSED`). Ветка `stage/s00-deep-research` опубликована; **последний проверенный HEAD `60ac26a89d1235612588726e6b28ae321f3da9ce`**, коммит закрытия этапа — после него. Слияние в `main` не выполнялось.
**Ограничение приёмки:** приняты объём и качество исследования как база для реализации, **а не** доказательство полного соответствия Cube World Alpha 2013. Пакеты A…L остаются `PARTIAL`, кроме явно `DIRECT`/`STRONGLY_SUPPORTED` фрагментов. Открытые вопросы сохранены: OQ-22 `PARTIAL`, OQ-23 `OPEN/BLOCKED`, OQ-24 `PARTIAL`, прочие — по [OPEN_QUESTIONS.md](research/alpha2013/OPEN_QUESTIONS.md). Скорости движения до закрытия OQ-23 — `PLACEHOLDER_NOT_ALPHA`.
**Все 12 пакетов A…L рассмотрены** и имеют явный статус ([RESEARCH_COVERAGE.md](research/alpha2013/RESEARCH_COVERAGE.md)).
**Отчёт:** [R02_REPORT.md](research/alpha2013/R02_REPORT.md); **тесты:** [VALIDATION_CASES.md](research/alpha2013/VALIDATION_CASES.md); **визуальный каталог:** [VISUAL_CATALOG.md](research/alpha2013/VISUAL_CATALOG.md).
**Источники:** [REFERENCE_ATLAS](research/alpha2013/REFERENCE_ATLAS.md); [SOURCE_REGISTER](research/alpha2013/SOURCE_REGISTER.md); [UNITY_REUSE_MAP](architecture/UNITY_REUSE_MAP.md); [SUBSYSTEM_TO_UNITY_MAP](research/alpha2013/SUBSYSTEM_TO_UNITY_MAP.md).
**Оригинал:** `D:\Games\Cube world` read-only; клиент неофициальный и русифицированный; точная подлинность не доказана.
**Контролируемые запуски Alpha:** использованы все 4 (финальная валидация 2026-11-03); **новых запусков без отдельного разрешения продюсера нет**. Среда DSH блокировала foreground/input (error 5), поэтому загрузка мира/персонажа не доведена. `RUNTIME_VERIFIED` (прошлые 3 запуска): max HP 268, порог XP 50, Berserker (`spec=0`). `NOT_VERIFIED/BLOCKED`: OQ-22 позиция, OQ-23 масштаб движения, HP после level-up, блок щитом. Протокол — [S00_RUNTIME_PROTOCOL.md](stages/S00_RUNTIME_PROTOCOL.md).
**Предыдущие работы:** G0 `a0af8aa` (`stage/s00-alpha-g0`); R01 `73893b6` (`stage/s00-static-r01`) — READY_FOR_REVIEW; часть выводов R01 исправлена в R02 (единицы, `ZoneTile`, «сетевой стриминг»).

## S01-PILOT — ОПУБЛИКОВАН, READY_FOR_REVIEW

**Статус:** `READY_FOR_REVIEW` (не `ACCEPTED`). Код используется как техническая основа S02, что **не** означает приёмку пилота.
**Подтверждено GitHub:** `stage/s01-unity-pilot` HEAD `a8842783378bf2621e6a39a50e1f9ee2398cf245`. Merge/force push в main не разрешены.
**Фактическая проверка 2026-11-03 (см. [S02_REPORT.md](stages/S02_REPORT.md)):** импорт PASS, EditMode **10/10**, PlayMode **2/2** — выполнено DeepSeek; независимой проверки ChatGPT не было. Код S01 используется как основа S02/S03, но S01 остаётся `READY_FOR_REVIEW`.
**URP:** отдельный обратимый trial возможен после фиксации baseline; в S02 не подключается.
**Unity Pipeline (экспериментальный):** DEFERRED / OPTIONAL; автоматически не устанавливать.
**Blender:** отсутствует, не требуется до S05.

## Правила

- Новая сессия `cubegame-dev`, права `workspace-write` + `ask`, без `danger-full-access` по умолчанию.
- Исследование A…L с достоверными источниками, вопросами и тестами; приоритет источников — локальные факты Alpha, реконструированный код, 2013-материалы, поздние wiki с разделением версий.
- Числа Alpha — референсы (D42): реализация начинается с эталонных таблиц, тесты — с эталонных значений; `UNKNOWN` → `PLACEHOLDER_NOT_ALPHA`, а не догадка.
- Не копировать закрытые исходные ассеты/декомпилированный C++ в публичный проект, не изменять оригинальные файлы Alpha.
- Git: отдельные ветки, фактические PASS/FAIL и `READY_FOR_REVIEW`; пользователь разрешает merge отдельно. Обычный push рабочей ветки разрешён; force-push и merge в `main` — нет.
