# D85 / Claude Code — Реальный игровой контур Unity

**Основание:** D85, D72, D71; общий исходный SHA рабочих веток указывается в координационном handoff.
**Рабочая директория:** D:\Projects\CubeGame, без новых worktree. **Результат:** исполняемая Unity-сцена + C# + EditMode/PlayMode; не исследовательский отчёт.

## Перед кодом

1. Прочесть PROJECT_MEMORY.md, docs/ACTIVE_STAGE.md, AGENTS.md, D85, D72, M0_00C_INTEGRATION_ARCHITECTURE_DRAFT.md, M0-00A аудит/зависимости, D83, П1–П6. Зафиксировать git status, branch, active Unity процессы, сохранить чужие untracked и текущие изменения; без destructive reset/force push.
2. Перейти в **свою ветку, созданную от единого D85 base**. Не изменять DeepSeek worktree или его ветку. Перед изменениями показать продюсеру «было → станет» в соответствии с AGENTS.md; уже утверждённый объём D85 не требует повторного разрешения на сам старт.
3. Сначала опубликовать в GitHub **минимальный C# interface/event contract** для визуального слоя: entity/kind ID, transform, equipment visual state, ActionStarted/PhaseChanged/Resolved, locomotion, времени GameClock. Не навязывать DeepSeek внутренний S05 controller. Не дублировать его код. Приложить тест/компиляцию контракта и SHA, оставить handoff в issue.

## Код и границы файлов

**Claude владеет** Assets/CubeGame/Features/**, Assets/CubeGame/Presentation/Gameplay/**, игровой сборкой/Bootstrap/Assets/Scenes/Pilot.unity (совместную сцену DeepSeek не редактирует). Уточнить реальные пути/asmdef по HEAD, не переписывать принятый Core S02–S04 и их публичные API. В Features — нет UnityEngine, Presentation связывает Core/Features и Unity.

**DeepSeek владеет** Assets/CubeGame/AlphaModels/** и AlphaModels.Import/**. Claude использует только опубликованный визуальный интерфейс/данные и не копирует S05 риг/клипы самостоятельно.

## Первый рабочий вертикальный срез — последовательность

1. GameSession + GameClock/фиксированный тик, lifecycle в существующем Unity-проекте, управляемый PlayerMotor и камера на сцене, отключение временного дублирующего демо. Сохранить существующий базовый Unity Pilot как живую сцену.
2. Реальный персонаж Core: CharacterSheet/HP/ресурсы и состояние в Features. Команды move/start action, детерминированные фазы и отказ с причиной; игровой clock — единственный источник симуляционного времени.
3. Связать S03/S04: инвентарь/equipment через безопасный адаптер без миграции enum, атака в заданном strike window, HitResolver/DamageResolver → изменение HP цели, hit/death outcome, события. Анимация не должна менять урон напрямую.
4. Дать DeepSeek-интерфейсу визуальные события (спавн персонажа, движение, меч в руке, ActionStarted). Если визуальный runtime ещё не слит — независимый mock/stub с явным статусом PLACEHOLDER, заменить при совместной интеграции.
5. Unity PlayMode сквозной сценарий: появление игрока → движение → экипировка тестового меча → старт атаки → один разрешённый hit → HP манекена падает на значение Core → смерть/возврат состояния. Сохранить логи/кадры/точные пути.

## Дальше по D85, без переписывания с нуля

Мир/seed/дозагрузка чанков и постоянные дельты → NPC spawn/AI с действием/целью → инвентарь/взаимодействия → доступный save/load → дальнейшие подтверждённые механики из M0 roadmap. Неподтверждённое в Alpha маркировать UNKNOWN. Полную миграцию v1→v2, переименование Core enum, реальное 13-slot хранилище и изменение принятых S02–S04 делать только после решения M0-00D.

## Проверка / handoff

- Прежде всего изменённые Unity EditMode и PlayMode, реальный результат Unity Editor; Python/Mono не заменяют Unity.
- Хранить отчёт по каждому сценарию: Alpha reference, Unity observed, delta, статус ALPHA_PARITY_PASS/UNITY_TECH_PASS/PARTIAL/BLOCKED.
- Не менять чужие файлы, не запускать конкурирующий Unity на общем ресурсе; коммит/пуш в собственную ветку, отчёт и короткие воспроизводимые команды в issue.
- Финал текущего пакета — READY_FOR_REVIEW, доступная пользователю играемая сцена, никаких ACCEPTED/main.
