# CubeGame — ACTIVE_STAGE

**Дата решения:** 2026-11-03. **Межчатовая память:** [PROJECT_MEMORY.md](../PROJECT_MEMORY.md). **Рабочее название:** CubeGame (имя игры не утверждено).
**Ответственные:** DeepSeek Harness — исполнитель; ChatGPT — архитектура и ревью; продюсер — игровые и визуальные решения.
**Активный этап:** **`S02-CORE-FOUNDATION`** — `AUTHORIZED / IN_PROGRESS` (решение продюсера 2026-11-03), ветка `stage/s02-core-foundation`, ТЗ [S02_CORE_FOUNDATION.md](stages/S02_CORE_FOUNDATION.md).
**Предыдущий этап:** `S00-R02` закрыт продюсером 2026-11-03 со статусом **`ACCEPTED`** (ограниченная приёмка — см. ниже). Гейт D41 «исследование до переноса новых систем» выполнен.
**D42:** [числа Alpha как референсы](decisions/D42-alpha-numbers-as-references.md) — статус **`ACCEPTED`** (решение продюсера 2026-11-03, утверждено независимо от OQ-23). До закрытия OQ-23 конкретные скорости в прототипе помечаются **`PLACEHOLDER_NOT_ALPHA`**; это не отменяет утверждение D42 и не является окончательным балансом.
**Архитектура:** модульный монолит [ADR-0001](architecture/ADR-0001-modular-monolith.md) утверждён; числовые/технические параметры — по D42.

## S02-CORE-FOUNDATION — АКТИВНЫЙ ЭТАП

**Статус:** `AUTHORIZED / IN_PROGRESS`; по завершении партии — `READY_FOR_REVIEW` (не `ACCEPTED`).
**Ветка:** `stage/s02-core-foundation` (от проверенного `stage/s01-unity-pilot` @ `a8842783378bf2621e6a39a50e1f9ee2398cf245`).
**ТЗ:** [S02_CORE_FOUNDATION.md](stages/S02_CORE_FOUNDATION.md). **Отчёт:** [S02_REPORT.md](stages/S02_REPORT.md).
**Объём первой партии:** модули A — Progression, B — Character Stats, C — Items Foundation, D — Persistence Foundation. Чистая логика отделена от Unity-представления (ADR-0001).
**Запрещено в S02:** переделывать генерацию мира, полная боевая система, AI/NPC, питомцы, подземелья, квесты, модели/анимации, URP, управление Alpha через DSH, объявление неизвестных чисел Alpha восстановленными, merge в `main`.
**Blender / URP / Pipeline:** не требуются и не подключаются в этой партии.

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
**Фактическая проверка 2026-11-03 (см. [S02_REPORT.md](stages/S02_REPORT.md)):** результат прогона фиксируется в отчёте S02; заявления прошлых сессий (EditMode 10/10, PlayMode 2/2) считаются `REPORTED`, пока не подтверждены текущим прогоном.
**URP:** отдельный обратимый trial возможен после фиксации baseline; в S02 не подключается.
**Unity Pipeline (экспериментальный):** DEFERRED / OPTIONAL; автоматически не устанавливать.
**Blender:** отсутствует, не требуется до S05.

## Правила

- Новая сессия `cubegame-dev`, права `workspace-write` + `ask`, без `danger-full-access` по умолчанию.
- Исследование A…L с достоверными источниками, вопросами и тестами; приоритет источников — локальные факты Alpha, реконструированный код, 2013-материалы, поздние wiki с разделением версий.
- Числа Alpha — референсы (D42): реализация начинается с эталонных таблиц, тесты — с эталонных значений; `UNKNOWN` → `PLACEHOLDER_NOT_ALPHA`, а не догадка.
- Не копировать закрытые исходные ассеты/декомпилированный C++ в публичный проект, не изменять оригинальные файлы Alpha.
- Git: отдельные ветки, фактические PASS/FAIL и `READY_FOR_REVIEW`; пользователь разрешает merge отдельно. Обычный push рабочей ветки разрешён; force-push и merge в `main` — нет.
