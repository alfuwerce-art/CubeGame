# CubeGame — ACTIVE_STAGE

**Рабочее название:** CubeGame; итоговое название игры не утверждено.  
**Основной разработчик:** DeepSeek Harness, пресет `cubegame-dev`.  
**Архитектура:** модульный монолит утверждён ([ADR-0001](architecture/ADR-0001-modular-monolith.md)); остальная архитектура по-прежнему требует испытаний.  
**Новое решение:** [D40](decisions/D40-static-first-unity-pilot.md) — **разрешены два параллельных трека**.

## Трек A — S00-R01 (статический анализ Alpha)

**Статус:** `READY_FOR_REVIEW` по отчёту локального агента, ветка `stage/s00-static-r01` уже на GitHub (HEAD `73893b6`); ревью ещё не закрыто.  
**ТЗ:** [S00-R01](stages/S00_STATIC_R01.md).  
**Источник:** `D:\Games\Cube world` (read-only), [CubeWorld-Reversal](https://github.com/qad3n/CubeWorld-Reversal).  
**Отчёт G0:** DeepSeek ранее опубликовал `stage/s00-alpha-g0`, commit `a0af8aa90de7ede6493420846cc152ad9fe30a87`, статус `G0_PASS / READY_FOR_REVIEW`; подтверждённый запуск/сохранение по отчёту, но официальная подлинность и полный аудит не закрыты. **Не называть полный S00 завершённым.**

## Трек B — S01-PILOT (первая Unity-сцена)

**Статус:** `IN_PROGRESS / CORE_CHECK_PASS / UNITY_RUN_NOT_RUN` по отчёту агента; Unity CLI обнаружил Unity 6.3 LTS и 6.6. Ветка `stage/s01-unity-pilot` уже на GitHub (HEAD `b5d881b`); требуется лицензия и реальный прогон.  
**ТЗ:** [S01-PILOT](stages/S01_UNITY_PILOT.md).  
**Техническая гипотеза:** Unity 6.3 LTS + URP. Unity Hub/Editor уже установлены по отчёту агента. Unity CLI `1.0.0-beta.13` **видит** `6000.3.27f2` и `6000.6.7f1`; `unity editors path 6000.3.27f2` возвращает `D:\\Unity\\Hub\\Editor\\6000.3.27f2`. Для пилота использовать только 6.3, выполнить лицензию и тесты. [Протокол CLI](agents/UNITY_CLI_PROTOCOL.md).  
**Blender не установлен и пока не требуется.** Он нужен при S05A: модель → старая анимация → новая анимация → проверка в Unity → отдельное утверждение каждой модели.

## Ограничения

- Новая сессия `workspace-write / ask`; прежняя `danger-full-access / never` не подходит.
- Не копировать платные Alpha-ресурсы, оригинальные игровые бинарники или декомпилированный код в публичный CubeGame. Старые отчёты G0 в другой ветке, аккуратно читать.
- Не запускать полноценный перенос всех механик, мультплеер, LLM NPC, Blender production, платный сервер.
- Изменения в разных ветках с разными отчётами, `READY_FOR_REVIEW`, без самовольного merge.

## Следующее действие

Выдать DeepSeek [следующую задачу](stages/NEXT_DEEPSEEK_TASK.md): немедленно начать S00-R01, отдельно согласовать установку Unity, затем S01-PILOT. Сохранить все данные других проектов и untracked `plugins/`.
