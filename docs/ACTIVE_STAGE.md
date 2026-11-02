# CubeGame — ACTIVE_STAGE

**Дата решения:** 2026-11-03. **Рабочее название:** CubeGame (имя игры не утверждено).
**Ответственные:** DeepSeek Harness — исполнитель; ChatGPT — архитектура и ревью; продюсер — игровые и визуальные решения.
**Актуальный приоритет:** [D41](decisions/D41-research-before-port.md): **углублённый разбор Alpha до переноса новых игровых систем**; [D42](decisions/D42-alpha-numbers-as-references.md): числа Alpha — **проверенные референсы**, первая версия CubeGame стремится к паритету, изменение баланса — отдельное решение.
**Архитектура:** модульный монолит [ADR-0001](architecture/ADR-0001-modular-monolith.md) утверждён; числовые/технические параметры — по D42.

## S00-DEEP-RESEARCH (R02) — ГЛАВНЫЙ ЭТАП

**Статус:** `READY_FOR_REVIEW`, партии 1–2 опубликованы в `stage/s00-deep-research` (`677f836`), слияние в `main` не выполнялось.
**Все 12 пакетов A…L рассмотрены** и имеют явный статус (`docs/research/alpha2013/RESEARCH_COVERAGE.md`), условие gate из ТЗ выполнено.
**ТЗ:** [S00_DEEP_RESEARCH.md](stages/S00_DEEP_RESEARCH.md); [команда DeepSeek](stages/DEEPSEEK_RESEARCH_TASK.md).
**Отчёт:** [R02_REPORT.md](research/alpha2013/R02_REPORT.md); **открытые вопросы:** [OPEN_QUESTIONS.md](research/alpha2013/OPEN_QUESTIONS.md).
**Источники:** [REFERENCE_ATLAS](research/alpha2013/REFERENCE_ATLAS.md); [SOURCE_REGISTER](research/alpha2013/SOURCE_REGISTER.md); [UNITY_REUSE_MAP](architecture/UNITY_REUSE_MAP.md); [SUBSYSTEM_TO_UNITY_MAP](research/alpha2013/SUBSYSTEM_TO_UNITY_MAP.md).
**Оригинал:** `D:\Games\Cube world` read-only; клиент неофициальный и русифицированный; точная подлинность не доказана.
**Контролируемые запуски Alpha:** разрешены продюсером, выполнены 3 запуска на копии установки — [S00_RUNTIME_TESTS.md](stages/S00_RUNTIME_TESTS.md) (`RUNTIME_VERIFIED`: max HP 268, порог XP 50, Berserker; три целевых теста — по ручному протоколу).
**Предыдущие работы:** G0 `a0af8aa` (`stage/s00-alpha-g0`); R01 `73893b6` (`stage/s00-static-r01`) — READY_FOR_REVIEW; часть выводов R01 исправлена в R02 (единицы, `ZoneTile`, «сетевой стриминг»).

## S01-PILOT — СОЗДАН, ОЖИДАЕТ ПУБЛИКАЦИИ И РЕВЬЮ

**Статус:** REPORTED_UNITY_PASS / READY_FOR_REVIEW / LATEST_PUSH_PENDING.
DeepSeek сообщает: Unity Editor `6000.3.27f2`, лицензия активна, EditMode 10/10, PlayMode 2/2, сцена/Windows player/скриншот готовы. Эти результаты пока не проверены независимо в последнем коммите.
**Разрешено:** опубликовать локальный коммит `a884278` в `stage/s01-unity-pilot`, без merge/force push. На GitHub последний проверенный HEAD был `b5d881b`.
**URP:** разрешён отдельный обратимый технический trial после фиксации рабочего пилота.
**Unity Pipeline (экспериментальный):** DEFERRED / OPTIONAL; автоматически не устанавливать.
**Blender:** отсутствует, не требуется до S05.

## Правила

- Новая сессия `cubegame-dev`, права `workspace-write` + `ask`, без `danger-full-access` по умолчанию.
- Исследование A…L с достоверными источниками, вопросами и тестами; приоритет источников — локальные факты Alpha, реконструированный код, 2013-материалы, поздние wiki с разделением версий.
- Не начинать новые игровые системы Unity до исследовательских спецификаций и их приёмки.
- Не копировать закрытые исходные ассеты/декомпилированный C++ в публичный проект, не изменять оригинальные файлы Alpha.
- Git: отдельные ветки, фактические PASS/FAIL и `READY_FOR_REVIEW`; пользователь разрешает merge отдельно.
