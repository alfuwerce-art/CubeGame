# D87 — сверка DeepSeek: общая папка, сведение веток, состояние шва P0

**Дата:** 2026-11-09, 21:01–21:20 МСК. **Агент:** DeepSeek Harness. **Каталог:** `D:\Projects\CubeGame`.
**Ветка:** `integration/m0-all` (== `integration/m0`), HEAD на момент отчёта — см. §6.
**Статус:** `READY_FOR_REVIEW`, `ACCEPTED` не присваивается.

## 1. Что проверено фактически (а не по документам)

| Проверка | Результат |
|---|---|
| `git status` обоих каталогов перед переходом | чисто, untracked нет |
| Ветки и remote | `claude/d85-gameplay-unity` @ `de62ec6` = `origin`; `deepseek/d85-visual-unity` @ `70b2326` = `origin` |
| Общий предок двух веток | `f0d750c` |
| Прогноз конфликта до слияния (`git merge-tree --write-tree`) | ровно 1 файл: `Assets/CubeGame/Features.meta` (add/add, разные GUID) |
| Фактическое сведение | выполнено в двух потоках одновременно: Claude — `integration/m0-team` (`66699aa`, + D86 из `planning/d85-direct-unity-two-agents`) и `integration/m0-all` (`93c31a6`, слияние с DeepSeek-срезом); DeepSeek — координационные документы поверх (`522b01b`, `4419fb9`) |
| Объединённое дерево содержит обе стороны | да: `Features/Session/GameSession.cs`, `Features/World/AlphaZoneGenerator.cs`, `Presentation/Gameplay/GameBootstrap.cs` (Claude) и `AlphaModels/Runtime/AlphaActorView.cs`, `Content/AlphaContentDatabase.cs` (DeepSeek) — на месте |
| Разрешённые конфликты | нет неразрешённых (`git ls-files --unmerged` пуст) |
| Unity-прогон объединённого дерева | **НЕ ВЫПОЛНЕН**: `It looks like another Unity instance is running with this project open. Multiple Unity instances cannot open the same project. Project: D:/Projects/CubeGame` — слот занят другим агентом (см. журнал) |
| Процессы | в 21:05 Unity не запущен, но запущены процессы `codex` (несколько), значит в общем каталоге работают минимум три агента одновременно |

## 2. Что создано в этом пакете

- `docs/decisions/D87-single-shared-folder-git-scheme.md` — новое решение: общая папка вместо двух worktree, зоны владения, Git-схема, слот Unity, откат.
- `docs/planning/M0_TRANSFER_MASTER_MATRIX.md` — сводная матрица полного переноса M0-01…M0-09: что реализовано Claude, что DeepSeek, фактический статус, владелец, следующее действие, блокеры; разделы «Дубли и риски» и «Очередь».
- `docs/agents/AGENT_COORDINATION.txt` — CLAIM/REPORT-записи DeepSeek (файл одновременно правит Codex, его шапка и правила — авторские; содержимое DeepSeek добавлено дописыванием в конец).
- строка D87 в `PROJECT_MEMORY.md` и `docs/ACTIVE_STAGE.md`.

Кода в этом пакете нет: проверка объединённой сборки требует слота Unity, который занят.

## 3. Шов P0 — что мешает первому играбельному срезу (проверено чтением кода)

1. **Граф сборок.** `CubeGame.Gameplay` (Claude) ссылается на `Core`, `Persistence`, `Presentation`, `Features`; `CubeGame.Presentation.Visual` (DeepSeek) — на `Core`, `Content`, `AlphaModels`, `Features.Creatures`. Ребер `Gameplay → Visual` и `Visual → Features` **нет**. Поэтому контракт `IEntityVisualSink` (в `CubeGame.Features.Contracts`) сегодня не может быть реализован в визуальном слое.
   Нужны ровно два ребра asmdef: `CubeGame.Presentation.Visual → CubeGame.Features` и `CubeGame.Gameplay → CubeGame.Presentation.Visual` (цикла нет: `Features` не ссылается на Visual). Это общий/сериализуемый файл — по D87 §3 только через CLAIM, владелец asmdef — Claude.
2. **Контракт уже достаточен.** `IEntityVisualSink` содержит `OnEntitySpawned/Despawned/Moved/EquipmentChanged/ActionStarted/ActionPhaseChanged/ActionResolved/HitResolved/EntityDied/OnFrame(gameTimeMs, alpha)`. `AlphaActorView` даёт `Initialize(AlphaActorDefinition)`, `ApplyLocomotion(AlphaLocomotionState)`, `TryPlayAction(code, out reason)`, `Tick(dt)`, `TryGetActionTiming(code)`. Соответствие событий и вызовов однозначное, менять контракт не требуется.
3. **Чего не хватает в контракте (не блокер, но нужно решение):**
   - `KindId → AlphaActorId` (строка актора в локальном кэше). Таблица `AlphaStagedVisuals` даёт режим/клипы, но функцию «вид → id актора» нужно зафиксировать в одном месте (Content), иначе сцена и контент разойдутся.
   - `WorldPosition → позиция в сцене` (floating origin и масштаб) — это владение Claude (`StreamingWorldView.ToScene`); визуальный слой должен получать уже преобразованную позицию или делегат, а не считать её сам.
   - `EquipmentVisualChanged` использует `VisualEquipmentSlot`, а домен — 6 слотов S04; адаптер отображения — DeepSeek, но правило соответствия должно быть в одном месте (сейчас `AlphaItemSlots`).
4. **Порядок исполнения.** `OnFrame(gameTimeMs, alpha)` — единственный источник времени для визуала (`AlphaAnimationController.externalClock = true`), собственных `Time.deltaTime` в визуальном слое быть не должно.

## 4. Найденные конфликты координации (требуют решения, не кода)

1. **Три агента в одном каталоге одновременно.** Claude создал `integration/m0-team`/`integration/m0-all`, Codex — свою шапку координационного файла и `docs/planning/M0_TEAM_TRANSFER_MATRIX.md`, DeepSeek — D87, свою матрицу и записи в журнале. Ветки `integration/m0` и `integration/m0-all` уже разъезжались (исправлено: обе на `4419fb9`).
2. **Две матрицы задач** (`M0_TRANSFER_MASTER_MATRIX.md` — DeepSeek, `M0_TEAM_TRANSFER_MATRIX.md` — Codex, не закоммичена). Нужен один владелец; предложение: подсистемная матрица DeepSeek как основная, Codex-файл — как карта владения файлами.
3. **Три предложения Git-схемы**: `integration/m0` (DeepSeek), `codex/m0-full-alpha-team` (Codex), фактическая `integration/m0-all` (Claude). Фактически работает последняя; её и надо подтвердить продюсеру.
4. **Слот Unity занят.** Запуск второго редактора невозможен физически; нужен явный `RELEASE unity` от держателя слота либо согласованное окно.
5. **Push не выполнен.** Общая ветка не опубликована; по задаче push делает назначенный интегратор. Локально коммиты не потеряются, но внешнего ревью по SHA пока нет.

## 5. Что осталось неподтверждённым

- Компиляция объединённого дерева и объединённый набор EditMode/PlayMode-тестов (слот занят).
- Что визуальный слой DeepSeek не тянет за собой решения боя/HP при подключении (проверяется только кодом, тест — после слота).
- Полнота переноса M0 по подсистемам — это матрица, а не приёмка.

## 6. SHA и следующий шаг

- `522b01b` — координационные документы DeepSeek (D87, матрица, журнал).
- `4419fb9` — уточнение D87 (фактическая ветка `integration/m0-all`, двухпоточное сведение); `integration/m0` и `integration/m0-all` указывают на него.
- Следующий шаг DeepSeek: реализовать `AlphaVisualSink` в `Assets/CubeGame/Presentation/Visual/` после согласования двух ребер asmdef, затем прогнать адресные EditMode/PlayMode в освободившемся слоте Unity и показать первый срез «герой с мечом + действие + hit/HP» вместе с Claude.
