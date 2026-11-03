# CubeGame — ACTIVE_STAGE

**Дата решения:** 2026-11-03. **Межчатовая память:** [PROJECT_MEMORY.md](../PROJECT_MEMORY.md). **Рабочее название:** CubeGame (имя игры не утверждено).
**Ответственные:** DeepSeek Harness — исполнитель; ChatGPT — архитектура и ревью; продюсер — игровые и визуальные решения.
**Активный этап:** **`S04-EQUIPMENT-COMBAT`** — партия выполнена, статус **`READY_FOR_REVIEW`** (не `ACCEPTED`); авторизация [D45](decisions/D45-s03-accepted-s04-authorized.md), ветка `stage/s04-equipment-combat`, ТЗ [S04_EQUIPMENT_COMBAT.md](stages/S04_EQUIPMENT_COMBAT.md), отчёт [S04_REPORT.md](stages/S04_REPORT.md).
**Предыдущий этап:** **`S03-COMBAT-CORE` → `ACCEPTED`** (решение продюсера 2026-11-03, ревью ChatGPT `PASS`); `S02-CORE-FOUNDATION` и `S00-R02` приняты ранее.
**D42:** [числа Alpha как референсы](decisions/D42-alpha-numbers-as-references.md) — статус **`ACCEPTED`** (решение продюсера 2026-11-03, утверждено независимо от OQ-23). До закрытия OQ-23 конкретные скорости в прототипе помечаются **`PLACEHOLDER_NOT_ALPHA`**; это не отменяет утверждение D42 и не является окончательным балансом.
**Архитектура:** модульный монолит [ADR-0001](architecture/ADR-0001-modular-monolith.md) утверждён; числовые/технические параметры — по D42.

## S04-EQUIPMENT-COMBAT — ПАРТИЯ ВЫПОЛНЕНА, READY_FOR_REVIEW

**Статус:** `READY_FOR_REVIEW` (не `ACCEPTED`). Авторизация — [D45](decisions/D45-s03-accepted-s04-authorized.md), 2026-11-03.
**Ветка:** `stage/s04-equipment-combat` от принятого коммита S03 (`70a0658`); последний проверенный опубликованный HEAD — **`369c32695ee1bbe21f0efe95994da5c931e1412e`** (S04-REVIEW-FIX-01; коммит синхронизации памяти — после него). Merge в `main` не выполнялся.
**ТЗ:** [S04_EQUIPMENT_COMBAT.md](stages/S04_EQUIPMENT_COMBAT.md). **Отчёт:** [S04_REPORT.md](stages/S04_REPORT.md).
**Фактические результаты (Unity 6000.3.27f2, выполнено DeepSeek):** после **S04-REVIEW-FIX-01** — компиляция PASS без `error CS`/`warning CS`; EditMode **296/296 passed**, PlayMode **17/17 passed**, failed=0. Новых тестов: **+36 EditMode** (Loadout 16, Stats 10, Integration 10), **+7 PlayMode**; REVIEW-FIX-01 добавил **+8 EditMode**. Регрессии S01 (10/10 + 2/2), S02 и S03 внутри прогона. Эталонные скрипты `alpha_curves/items/combat --check` — PASS; `git diff --check` — чисто. Артефакты: `.unity-install/results/rf04_editmode.xml`, `.unity-install/results/rf04_playmode.xml`.
**S04-REVIEW-FIX-01 (2026-11-03):** (FIX-01) `EquipmentLoadout.Slots` публикует `ReadOnlyCollection` вместо внутреннего массива — приведение к `EquipmentSlot[]` невозможно, запись через `IList` бросает `NotSupportedException`; (FIX-02) `EquipmentStatSummary` делает защитную копию `PerItem` и публикует неизменяемое представление. Формулы, RNG, баланс, формат сохранений и правила экипировки не менялись; статусы OQ сохранены; D46 и ветка `stage/s05-asset-conversion-plan` не затрагивались. Подробности — [S04_REPORT.md](stages/S04_REPORT.md) §5a.
**Реализовано:** `Core/Equipment` — слоты `MainHand/OffHand/Chest/Gloves/Boots/Shoulders`, `EquipmentLoadout` (совместимость, замена, запрет дублирования экземпляра, атомарность), `EquipmentStats` (урон/Armor/BlockValue раздельно), `EquipmentCombatAdapter`; в `CombatAttack` добавлен аддитивный `ItemWeaponDamageBonus` (учитывается в `DamageResolver` до разброса, RNG не изменён); `Presentation/Equipment/EquipmentCombatDemo` — смена оружия/брони и разница ударов при одном seed.
**Границы достоверности:** `REFERENCE_VERIFIED` — статы предметов (S02) и формулы урона/HP (S02/S03); `PARTIAL` — аддитивный вклад оружия (структура E2), `TargetArmor ← Σ BlockValue` (E3); `UNKNOWN` — OQ-25 (HP из экипировки), OQ-01; правила двуручного оружия/щита/классов не утверждены и не реализованы (S04-OQ-01, S04-OQ-02).
**Сохранения:** формат v1 не изменён; контракт будущих слотов экипировки (`ItemInstanceId` + `slotId`, миграция v1→v2) зафиксирован в [S04_REPORT.md](stages/S04_REPORT.md) §6.
**Запрещено в S04:** инвентарь и drag-and-drop, модели, анимации, VFX, AI, сложные хитбоксы, URP, запуск Alpha, изменение баланса, merge в `main`, force push, самостоятельное присвоение `ACCEPTED`.

## S03-COMBAT-CORE — ПРИНЯТ (`ACCEPTED`, 2026-11-03)

**Статус:** **`ACCEPTED`** — решение продюсера 2026-11-03 ([D45](decisions/D45-s03-accepted-s04-authorized.md)); архитектурное ревью ChatGPT — `PASS`, `S03-REVIEW-FIX-01` принят. Приёмка ограниченная: боевое ядро как техническая основа, а не паритет с Alpha 2013.
**Ветка:** `stage/s03-combat-core` от принятого коммита S02 (`1df7413`); последний проверенный HEAD — **`943dea68fbe69e49c53cbb16b7b47c24e500691f`** (+ коммит закрытия S03). Merge в `main` не выполнялся.
**ТЗ:** [S03_COMBAT_CORE.md](stages/S03_COMBAT_CORE.md). **Отчёт:** [S03_REPORT.md](stages/S03_REPORT.md).
**Фактические результаты (Unity 6000.3.27f2, выполнено DeepSeek):** компиляция PASS без `error CS`/`warning CS`; EditMode **251/251 passed**, PlayMode **10/10 passed**, failed=0; регрессии S01 (10/10 + 2/2) и S02 (все фикстуры) внутри прогона; Python-эталоны — PASS.
**S03-REVIEW-FIX-01:** (FIX-01) `AbsorbedDamage = min(raw, blockFraction·armor)`, расчётная защита вынесена в `DefenseCapacity`; (FIX-02) `AttackIdMultiplierApplied` только при найденном множителе; (FIX-03) `BlockThreshold` отвергает уровень < 1 (и через `RollBlock`). Порядок RNG и изменение HP не менялись; статус митигации остаётся `PARTIAL`. Дополнительно исправлен комментарий об инварианте overkill: корректен `MitigationResult.AbsorbedDamage + MitigationResult.FinalDamage = RawDamage`.
**Реализовано:** `Core/Combat` — формулы урона, разброс, проверка попадания, митигация, пул здоровья, результат удара, резолвер, детерминированный RNG; `Presentation/Combat/CombatDemo` — демонстрация удара. Ядро без UnityEngine.
**Сохранённые ограничения:** OQ-01 `OPEN`, OQ-02 `PARTIAL`, OQ-24 `PARTIAL`, OQ-25/OQ-26 `OPEN`, OQ-22/OQ-23 — как в S00/S02. Способности, комбо, тайминги, threat/poise, элементальные проки, анимации/VFX, AI — вне партии.

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
