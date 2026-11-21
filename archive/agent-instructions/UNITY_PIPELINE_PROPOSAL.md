# Предложение: подключить Unity Pipeline к пилоту CubeGame

**Дата:** 2026-11-03, MSK.
**Статус:** PROPOSAL / НЕ УСТАНОВЛЕНО. Нужно решение продюсера.
**Основание:** базовый прогон S01-PILOT уже проходит (см. `docs/stages/S01_PILOT_REPORT.md`);
Pipeline нужен для следующего шага — управления запущенным редактором без перезапуска в
batchmode.

## 1. Зачем

Сейчас каждая проверка — отдельный запуск `Unity.exe -batchmode` (импорт ≈16 с, EditMode ≈138 с,
PlayMode ≈138 с, сцена ≈16 с, сборка ≈18 с). Это надёжно, но:

- нельзя посмотреть состояние уже открытого редактора;
- нельзя выполнить команду/перекомпиляцию в живом редакторе;
- ручная проверка продюсера и автоматическая проверка агента работают раздельно.

`com.unity.pipeline` (Unity Editor Pipeline) позволяет CLI видеть живой Editor и управлять им:
`unity status`, `unity list`, `unity command`, `unity recompile`. Это ускоряет итерации и
убирает необходимость каждый раз открывать/закрывать редактор.

## 2. Что уже проверено (только чтение, без изменений проекта)

```
unity pipeline list
--> CubeGame  Pipeline=false  (пакет не установлен, сервер недоступен)

unity pipeline list-versions
--> 0.9.1-exp.1 (latest), 0.9.0-exp.1, 0.8.0-exp.1, ... 0.2.0-exp.2
```

CLI 1.0.0-beta.13 поддерживает команды `install`, `upgrade`, `list`, `list-versions`,
`cloud-build`, `automation`. В `Packages/manifest.json` проекта пакета нет.

## 3. Что изменится в проекте (точно)

1. `Packages/manifest.json`: добавится одна зависимость

   ```json
   "com.unity.pipeline": "0.9.1-exp.1"
   ```

2. `Packages/packages-lock.json`: пересчитается резолв (появится запись пакета и, возможно,
   его зависимостей).
3. Редактор нужно один раз перезапустить (пакет подключается при старте Editor).
4. Больше ничего: `Assets/`, сцена, сборка плеера и `ProjectSettings` не меняются.
   Требуется проверить дифф после установки — если появятся лишние пакеты, это повод
   остановиться и вернуть изменения.

## 4. Команда установки (только после разрешения)

```powershell
cd 'D:\Projects\CubeGame'
git status --short                      # ветка stage/s01-unity-pilot, рабочее дерево зафиксировано
unity pipeline install --project-path 'D:\Projects\CubeGame' --package-version 0.9.1-exp.1
git diff -- Packages/manifest.json Packages/packages-lock.json
```

Проверка результата:

```powershell
unity pipeline list                 # ожидается Pipeline=true, версия 0.9.1-exp.1
unity status --project-path 'D:\Projects\CubeGame'
unity command --help
```

Откат (если что-то пошло не так): убрать `com.unity.pipeline` из `Packages/manifest.json`,
запустить `unity pipeline list` для проверки, вернуть `packages-lock.json` из Git.

## 5. Риски и ограничения

- Пакет **experimental** (`0.9.1-exp.1`), не LTS-компонент. Может менять API и поведение между
  версиями; для пилота допустимо, для релиза — отдельное решение.
- Установка требует сети (Package Manager) и меняет `packages-lock.json`.
- Не даёт повода обновлять Unity: остаёмся на 6000.3.27f2, Unity 6.6 не используется.
- Не заменяет batchmode-проверку: `run_pilot.ps1` остаётся источником доказательств
  (импорт/тесты/сборка/плеер). Pipeline — дополнительный инструмент итераций.
- Пакет не должен попадать в сборку плеера как runtime-зависимость; после установки нужно
  убедиться, что `PilotBuild` по-прежнему собирается (шаг 4 `run_pilot.ps1`).

## 6. Что нужно от продюсера

Разрешение на одну установку пакета на ветке `stage/s01-unity-pilot` с фиксацией диффа
`Packages/manifest.json` и `packages-lock.json`. Без разрешения пакет не устанавливается,
`PIPELINE=NOT_INSTALLED` в отчёте остаётся корректным статусом.
