# CubeGame — ACTIVE_STAGE

**Дата решения:** 2026-11-03. **Межчатовая память:** [PROJECT_MEMORY.md](../PROJECT_MEMORY.md). **Рабочее название:** CubeGame (имя игры не утверждено).
**Ответственные:** DeepSeek Harness — исполнитель; ChatGPT — архитектура и ревью; продюсер — игровые и визуальные решения.
**Актуальный приоритет:** [D41](decisions/D41-research-before-port.md): **углублённый разбор Alpha до переноса новых игровых систем**.
**D42:** [числа Alpha как референсы](decisions/D42-alpha-numbers-as-references.md) — статус **`PROPOSED / PENDING_PRODUCER_APPROVAL`**, не `ACCEPTED`. Техническое правило текущей разработки (подтверждено архитектором): до закрытия OQ-23 скорости в прототипе помечаются **`PLACEHOLDER_NOT_ALPHA`**; это не утверждение окончательного баланса.
**Архитектура:** модульный монолит [ADR-0001](architecture/ADR-0001-modular-monolith.md) утверждён; числовые/технические параметры — по D42.

## S00-DEEP-RESEARCH (R02) — ГЛАВНЫЙ ЭТАП

**Статус:** `READY_FOR_REVIEW`, не `ACCEPTED`. Ветка `stage/s00-deep-research` опубликована: исходный HEAD `f5f3984`; review-fix правки (FIX-01…FIX-06) — отдельные коммиты после него. Слияние в `main` не выполнялось.
**Синхронизация:** ветка создана от `origin/main` = `bde0fd6`; позднее `origin/main` ушёл вперёд до `4dcafde` (PROJECT_MEMORY + разрешение автоматического push), эти коммиты влиты в `stage/s00-deep-research` merge-коммитом без потери исследовательских коммитов. Merge в `main` не выполнялся; локальная `main` отстаёт — это не влияет на исследовательскую ветку.
**Все 12 пакетов A…L рассмотрены** и имеют явный статус (`docs/research/alpha2013/RESEARCH_COVERAGE.md`), условие gate из ТЗ выполнено; часть пакетов — `PARTIAL`, это не приёмка механик.
**ТЗ:** [S00_DEEP_RESEARCH.md](stages/S00_DEEP_RESEARCH.md); [команда DeepSeek](stages/DEEPSEEK_RESEARCH_TASK.md).
**Отчёт:** [R02_REPORT.md](research/alpha2013/R02_REPORT.md); **открытые вопросы:** [OPEN_QUESTIONS.md](research/alpha2013/OPEN_QUESTIONS.md); **тесты:** [VALIDATION_CASES.md](research/alpha2013/VALIDATION_CASES.md); **визуальный каталог:** [VISUAL_CATALOG.md](research/alpha2013/VISUAL_CATALOG.md).
**Источники:** [REFERENCE_ATLAS](research/alpha2013/REFERENCE_ATLAS.md); [SOURCE_REGISTER](research/alpha2013/SOURCE_REGISTER.md); [UNITY_REUSE_MAP](architecture/UNITY_REUSE_MAP.md); [SUBSYSTEM_TO_UNITY_MAP](research/alpha2013/SUBSYSTEM_TO_UNITY_MAP.md).
**Оригинал:** `D:\Games\Cube world` read-only; клиент неофициальный и русифицированный; точная подлинность не доказана. Запуски — только из копии установки.
**Контролируемые запуски Alpha:** выполнены 3 запуска на копии установки — [S00_RUNTIME_TESTS.md](stages/S00_RUNTIME_TESTS.md). `RUNTIME_VERIFIED`: max HP 268, порог XP 50, Berserker (`spec=0`). `NOT_VERIFIED`: масштаб движения, HP после level-up, блок щитом. Новых запусков в review-fix не было; протокол — [S00_RUNTIME_PROTOCOL.md](stages/S00_RUNTIME_PROTOCOL.md), нужно отдельное разрешение продюсера.
**Предыдущие работы:** G0 `a0af8aa` (`stage/s00-alpha-g0`); R01 `73893b6` (`stage/s00-static-r01`) — READY_FOR_REVIEW; часть выводов R01 исправлена в R02 (единицы, `ZoneTile`, «сетевой стриминг»).

## S01-PILOT — ОПУБЛИКОВАН, ОЖИДАЕТ РЕВЬЮ

**Статус:** REPORTED_UNITY_PASS / READY_FOR_REVIEW / LATEST_PUSH_CONFIRMED_GITHUB (не независимое тестирование).
DeepSeek сообщает: Unity Editor `6000.3.27f2`, лицензия активна, EditMode 10/10, PlayMode 2/2, сцена/Windows player/скриншот готовы. Последний коммит опубликован в GitHub, но указанные результаты пока не проверены ChatGPT независимым запуском Unity. S00-review-fix не менял Unity-проект.
**Подтверждено GitHub:** `stage/s01-unity-pilot` HEAD `a8842783378bf2621e6a39a50e1f9ee2398cf245`. Push уже выполнен. Merge/force push в main не разрешены.
**URP:** разрешён отдельный обратимый технический trial после фиксации рабочего пилота.
**Unity Pipeline (экспериментальный):** DEFERRED / OPTIONAL; автоматически не устанавливать.
**Blender:** отсутствует, не требуется до S05.

## Правила

- Новая сессия `cubegame-dev`, права `workspace-write` + `ask`, без `danger-full-access` по умолчанию.
- Исследование A…L с достоверными источниками, вопросами и тестами; приоритет источников — локальные факты Alpha, реконструированный код, 2013-материалы, поздние wiki с разделением версий.
- Не начинать новые игровые системы Unity до исследовательских спецификаций и их приёмки.
- Не копировать закрытые исходные ассеты/декомпилированный C++ в публичный проект, не изменять оригинальные файлы Alpha.
- Git: отдельные ветки, фактические PASS/FAIL и `READY_FOR_REVIEW`; пользователь разрешает merge отдельно. Обычный push рабочей ветки разрешён; force-push и merge в `main` — нет.
