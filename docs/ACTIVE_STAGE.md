# CubeGame — ACTIVE_STAGE

**Дата решения:** 2026-11-03. **Межчатовая память:** [PROJECT_MEMORY.md](../PROJECT_MEMORY.md). **Рабочее название:** CubeGame (имя игры не утверждено).
**Ответственные:** DeepSeek Harness — исполнитель; ChatGPT — архитектура и ревью; продюсер — игровые и визуальные решения.
**Актуальный приоритет:** [D41](decisions/D41-research-before-port.md): **углублённый разбор Alpha до переноса новых игровых систем**.
**Архитектура:** модульный монолит [ADR-0001](architecture/ADR-0001-modular-monolith.md) утверждён; числовые/технические параметры — на проверке.

## S00-DEEP-RESEARCH (R02) — ГЛАВНЫЙ ЭТАП

**Статус:** AUTHORIZED / NOT_STARTED по доступным данным.
**ТЗ:** [S00_DEEP_RESEARCH.md](stages/S00_DEEP_RESEARCH.md); [команда DeepSeek](stages/DEEPSEEK_RESEARCH_TASK.md).
**Источники:** [REFERENCE_ATLAS](research/alpha2013/REFERENCE_ATLAS.md); [UNITY_REUSE_MAP](architecture/UNITY_REUSE_MAP.md).
**Оригинал:** `D:\Games\Cube world` read-only; клиент неофициальный и русифицированный; точная подлинность не доказана.
**Предыдущие работы:** G0 `a0af8aa` в `stage/s00-alpha-g0`; R01 `73893b6` в `stage/s00-static-r01`, READY_FOR_REVIEW. Не утверждать, что полный S00 закончен.

## S01-PILOT — СОЗДАН, ОЖИДАЕТ ПУБЛИКАЦИИ И РЕВЬЮ

**Статус:** REPORTED_UNITY_PASS / READY_FOR_REVIEW / LATEST_PUSH_CONFIRMED_GITHUB (не независимое тестирование).
DeepSeek сообщает: Unity Editor `6000.3.27f2`, лицензия активна, EditMode 10/10, PlayMode 2/2, сцена/Windows player/скриншот готовы. Последний коммит опубликован в GitHub, но указанные результаты пока не проверены ChatGPT независимым запуском Unity.
**Подтверждено GitHub:** `stage/s01-unity-pilot` HEAD `a8842783378bf2621e6a39a50e1f9ee2398cf245`. Push уже выполнен. Merge/force push в main не разрешены.
**URP:** разрешён отдельный обратимый технический trial после фиксации рабочего пилота.
**Unity Pipeline (экспериментальный):** DEFERRED / OPTIONAL; автоматически не устанавливать.
**Blender:** отсутствует, не требуется до S05.

## Правила

- Новая сессия `cubegame-dev`, права `workspace-write` + `ask`, без `danger-full-access` по умолчанию.
- Исследование A…L с достоверными источниками, вопросами и тестами; приоритет источников — локальные факты Alpha, реконструированный код, 2013-материалы, поздние wiki с разделением версий.
- Не начинать новые игровые системы Unity до исследовательских спецификаций и их приёмки.
- Не копировать закрытые исходные ассеты/декомпилированный C++ в публичный проект, не изменять оригинальные файлы Alpha.
- Git: отдельные ветки, фактические PASS/FAIL и `READY_FOR_REVIEW`; пользователь разрешает merge отдельно.
