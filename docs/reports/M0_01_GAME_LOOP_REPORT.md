# M0-01 / пакет I — единый игровой цикл, часы, сейв-слот и контракт для DeepSeek

**Исполнитель:** Claude Code, D86, issue #12. **Ветка:** `claude/d85-gameplay-unity` (база `f0d750c`).
**Статус:** `READY_FOR_REVIEW` (не `ACCEPTED`). SHA — в сообщении коммита/issue.

## Что реально работает в Unity

| Механика | Где | Проверка |
|---|---|---|
| Главное меню → новый мир (имя, seed, герой, класс, слот) → игра | `Presentation/Gameplay/GameBootstrap.cs`, сцена `Assets/Scenes/Pilot.unity` | PlayMode `FullM0GameLoopPlayModeTests` |
| Автомат сессии Boot/MainMenu/LoadingWorld/InGame/Paused/Saving/Unloading/Quit, отказ с причиной | `Features/Session/GameSession.cs` | EditMode `FullM0SessionTests` |
| Игровые часы: фиксированный тик 30 Гц, мс игры, пауза; одинаковые тики при 30/60/120 Гц и рваных кадрах | `Features/Session/GameClock.cs` | EditMode `FullM0ClockTests` |
| Esc-пауза: остановка часов и ввода; сохранить / сохранить и выйти / выйти без сохранения / выход из игры | `GameBootstrap` | PlayMode |
| Сохранение слота: персонаж — прежний Core `CUBEGAME_SAVE` v1 (без изменений), рядом `CUBEGAME_SESSION` v1 (мир, seed, генератор, тик, позиция/имя героя); порванная пара, повреждение, обрезка, чужая версия, чужой генератор → отказ | `Features/Persistence/*` | EditMode `FullM0PersistenceTests` (память + реальный диск) |
| Загрузка: тот же мир/seed/класс/время, герой на сохранённой позиции; выход в меню уничтожает мир и игрока, повтор 3× без дублей | `GameBootstrap` | PlayMode |
| Контракт для визуального/контентного слоя DeepSeek: ID, события, `IEntityVisualSink`, `SpawnDefinition`, `WorldDelta`, `NpcStateSnapshot`, `SaveVersion` | `Features/Contracts/*` | EditMode (шина событий, ID) |
| Граф сборок: `Features` без UnityEngine и только → Core; Presentation не знает Features | asmdef | EditMode `FullM0AssemblyGraphTests` |

## Unity (6000.3.27f2, batchmode, артефакты в `.scratch/m0/m0_01/`)

- До изменений: EditMode **296/296**.
- После: EditMode **314/314** (FullM0 18/18), PlayMode **19/19** (FullM0 2/2), exit 0, ошибок компиляции нет.
- Лог сценария: мир seed 424242 — 64 чанка, ~492k вершин, ~430 мс; save tick=23 → меню → load tick=23.

## Alpha-ссылки и отклонения

- Тик 30 Гц — решение CubeGame (M0-00C A-2), `PLACEHOLDER_NOT_ALPHA`; длительности Alpha в мс считаются от `GameClock.TimeMs`.
- Генератор `cubegame.pilot_heightfield/1` — пилот S01, **не мир Alpha**; заменяется в M0-02, старые сейвы с другим генератором будут явно отклоняться.
- Скорости/прыжок `PlayerMotor` — прежние `PLACEHOLDER_NOT_ALPHA` (OQ-23); мотор пока на `Time.deltaTime`, перевод на часы — M0-03B.
- Меню — временный IMGUI; игровой API (`StartNewWorld/LoadWorld/SaveCheckpoint/ExitToMenu`) остаётся для UI M0-08.
- Тело героя — капсула `PLACEHOLDER`, пока визуальный слой DeepSeek не подключён через `IEntityVisualSink`.

## Без изменений

Core S02–S04 (enum, 6 слотов, `SaveGame` v1), asmdef `Core/Persistence/Presentation/Editor`, `PilotBootstrap.cs`, `PilotSceneBuilder.cs`, ProjectSettings/Packages, файлы DeepSeek.

## Блокеры / открыто

- Сейв v2 (экипировка, мир, сущности в одном формате) — нужен отдельный документ решения продюсера; до него сессия хранится отдельным файлом.
- Скриншот/сборка игрока в этом пакете не делались (только batchmode-тесты).

## Следующее

M0-02: генератор мира по иерархии Alpha + чанки/стриминг/дельты (`WorldDelta`), затем M0-03 мотор на `GameClock`, M0-04 инвентарь/экипировка.
