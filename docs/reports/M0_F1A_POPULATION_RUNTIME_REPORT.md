# M0-F1A — восстановленное население Alpha и runtime поведения NPC

**Дата:** 2026-11-10. **Исполнитель:** Claude Code. **Ветка:** `claude/m0-f1-population-runtime` от
`integration/m0-all @ 1c6560c8f3e28b4a49f9a435327cd3fcb6e767a5` (worktree `D:\Projects\CubeGame-claude`).
**STATUS: `READY_FOR_REVIEW`**, не `ACCEPTED`. Игра Alpha не запускалась: всё по байтам `Cube.exe`
(sha256 `84a7a132…30bf`), исполнению кода в оракуле M0-02 и декомпиляции.

## 1. Итог

| Задача | Результат |
|---|---|
| Настоящий источник вида существа | **новый** `AlphaCreatureKindSelector` — восстановленный `0x5F56C0`, сверен с исполненным машинным кодом на 3150 входах (0 расхождений) |
| `0x5F56C0` ↔ `AlphaScatterTree` | адрес тот же, **соответствие не доказано — опровергнуто**: перевод теряет и переставляет id и возвращает список вместо вида (§3). Существующий код не использован, не изменён, дефект передан Codex |
| Runtime lifecycle | `AlphaSpawnRuntime`: окно времени → радиус 200 → создание → смерть → таймер 1 200 000 мс / смена дня → уход 512 → повторная активация |
| Расписание / патруль NPC | `AlphaNpcMovementDriver` (SpawnLocation → цель → прибытие → дом → RandomWalk; или WalkPath) + новый `AlphaRandomWalk` (`0x4DBF20`); подключено в `CreatureAi` раньше `PatrolRoute` |
| Стабильный ID | Alpha-идентичность шаблона «зона + индекс» → существующий `StableEntityId.ForZoneSpawn(zone, 0, index)` |
| Persistence | данные и API готовы (`SnapshotStates`/`RestoreStates`), формат общего сейва **не менялся** → `PERSISTENCE_SCHEMA_HANDOFF` |
| `AlphaSpawnTable` | не используется как список существ; IL-тест проверяет, что ни один тип населения/runtime не обращается к ней |
| `GameBootstrap`, `StreamingWorldView`, провайдеры, `Session.WorldPopulation` | не менялись (зона Codex) |

## 2. Что реально подключаемо (API для интегратора)

```csharp
// 1. Источник вида: климат террейна (исполненные порты) + высота первого несплошного вокселя колонки.
Func<int,int,int[]> candidates = AlphaCreatureKindSelector.FromClimate(
    (x, z) => new AlphaClimateSample(terrain.Humidity(x, z), terrain.Temperature(x, z),
                                     terrain.RareCellTerm(x, z), terrain.Mountain(x, z)),
    (x, z) => FirstNonSolidHeight(x, z));                       // как ScatterHeight в AlphaZoneContentProvider
var population = AlphaRecoveredPopulation.WithKindSelector(seed, candidates, groundHeight);   // IWorldPopulationSource + IAlphaSpawnTemplateSource

// 2. Runtime шаблонов (вместо канала 0 Session.WorldPopulation — тот убивает навсегда).
var runtime = new AlphaSpawnRuntime();
runtime.AddSource(population, AlphaSpawnRuntime.CreatureChannel);
runtime.Created   += t => SpawnCombatantAndVisual(t.Id, t.Definition, t.Data);      // позиция = шаблона
runtime.Despawned += (t, reason) => RemoveCombatantAndVisual(t.Id);                 // не смерть
// загрузка/выгрузка зон — вместе со стримингом мира:
runtime.SetLoadedZones(zonesAroundHero);   // или LoadZone / UnloadZone
// каждый тик сервера:
runtime.Tick(elapsedRealMs, clock.WorldTimeMs, AlphaGameDay.Of(clock), heroPositions, id => CurrentPosition(id));
// смерть существа (EntityDied):
runtime.NotifyDeath(died.Entity, AlphaGameDay.Of(clock));
// сейв / загрузка (после согласования схемы):
List<AlphaSpawnTemplateState> state = runtime.SnapshotStates();   runtime.RestoreStates(state);

// 3. Движение без цели:
ai.AttachAlphaMovement(AlphaNpcMovementDriver.ForTemplate(t.Data, () => clock.WorldTimeMs));
```

## 3. `0x5F56C0` и почему отдельный селектор

Функция берёт `(x, z, level, mode)`, сэмплирует `0x5C4DD0` (влажность), `0x5C4800` (температура), `0x5EF040`,
`0x5EFFA0(x,z,0)`, строит список и возвращает **один** вид `list[rand() % n]`, при пустом — `0x3C`.
Место вызова сетки: `0x5EB462 push 0` (mode), `0x5EB48C call`, `0x5EB491 mov [edi+0x2C],eax` (вид шаблона);
`level` — высота первого вокселя воздуха/воды колонки (декомпиляция `WorldInfo.cpp` ≈4010–4024).
Все вызовы функции (E8-скан): `0x5EB48C`, `0x5D92A2`, `0x5D92F0` — последние два внутри генератора
шаблонов `0x5D9460`. Вызовов из размещения растительности нет.

Сравнение `AlphaScatterTree.g.cs` с исполненным кодом (тест `TheExistingScatterTranslationIsNotEquivalent…`):

| Ветка | Исполненный код | `AlphaScatterTree` |
|---|---|---|
| сухо, `h < 0.2` | …`0x17, 0x23` | без `0x23`, плюс лишние `0x97` и `0x6A,0x39,0x56,0x19` |
| `h ≥ 0.8, t ≥ 0.6` | …`0x16, 0x17` | без `0x17`, плюс лишние |
| `h ≥ 0.8, t < 0.2` | …`0x24, 0x63` | без `0x63`, плюс лишние |
| `0.2 ≤ h < 0.8` | …`0x58, 0x64, 0x5A`…`0x97` | без `0x64`, плюс `0x6A…` |
| `level ≤ 3` | `0x15, 0x2E, [0x2F], 0x6A, 0x39, 0x56, 0x19` | то же, но этот хвост добавляется и при `level > 3` |
| `mode ≠ 0` | `2, 4, 7, 9, 0xB, 0xF, 0x33, 0x30, 0x4C` | без `2, 4, 7, 9` |
| итог | один вид `list[rand()%n]` | весь список |

Причина: механический перевод не учёл общий `push_back(param_4)` после ветвей и раннее `goto` в конец.

## 4. Provenance правил

| Правило | Значение | Статус | Источник |
|---|---|---|---|
| вид дикого шаблона | `0x5F56C0`, `list[rand()%n]`, пусто → `0x3C` | `EMULATED_ALPHA_CODE` | `creature_kind_evidence.py`, 3150 случаев |
| входы селектора | влажность/температура/`0x5EF040`/`0x5EFFA0` | порты сверены с исполнением ранее (M0-02) | `AlphaTerrainGenerator` |
| сетка 3×3, уровень, стая | без изменений D1 | `ALPHA_CODE_STATIC` / `EMULATED_ALPHA_CODE` | `spawn_pipeline_evidence.py` |
| радиус активации | 200 (`+0x08`, конструктор `0x5ADB29`) | `ALPHA_CODE_STATIC` | D1 |
| окно времени | 06:00–24:00 для враждебных шаблонов сетки, концы включительно | `ALPHA_CODE_STATIC` | `0x5EB83E/45`, `0x60FBD4/E8` |
| таймер смерти | 1 200 000 мс реального времени | `ALPHA_CODE_STATIC` | `0x596C30`, `0x60DF9F` |
| респаун по смене дня | день смерти ≠ текущий день | `ALPHA_CODE_STATIC` | `0x60FC1A…23` |
| уход | ≥ 512 от всех игроков, шаблон остаётся, таймер не трогается | `ALPHA_DECOMPILED` | D1 |
| позиция при респауне | позиция шаблона | `ALPHA_DECOMPILED` | D1 |
| ID | зона + индекс шаблона → `ForZoneSpawn` | Alpha-идентичность `ALPHA_CODE_STATIC` (`0x596C3A`), формат ID — существующий контракт CubeGame | |
| день | счётчик `+0x800160`, рост при переходе времени (`0x60C6C0`) | `ALPHA_CODE_STATIC`; привязка к `GameClock` — CubeGame | `AlphaGameDay` |
| SpawnLocation / WalkPath | без изменений D2 | `ALPHA_DECOMPILED` + байтовые константы | D2 |
| RandomWalk | поводок 10 ед. `(dx²+dz²)·65536 ≤ 0x640000`, направление ×10, таймер `rand()%5000+3000` | константы — байты (`0x4DBFFE`, `0x4DC004`, `0x4DC1B2`, `0x4DC1BD`, `0x6FCD94`), поток управления `ALPHA_DECOMPILED` | `creature_kind_evidence.py` |
| радиус прибытия SpawnLocation | 2.0 | `CUBEGAME_CONTRACT` | |
| дом без расписания | позиция шаблона | `CUBEGAME_CONTRACT` | |
| состояние шаблона при выгрузке зоны | хранится по ID, не тикает | `CUBEGAME_CONTRACT` (Alpha `UNKNOWN`) | |
| поток `rand()` | собственный детерминированный | `CUBEGAME_CONTRACT` | |
| `PatrolRoute`, `FallbackPopulationPolicy` | не менялись | `CUBEGAME_FALLBACK` / `CUBEGAME_CONTRACT` | |

## 5. Изменённые файлы

| Файл | Было → стало |
|---|---|
| `tools/alpha_models/world/creature_kind_evidence.py` | не было → исполнение `0x5F56C0`, байты места вызова, 3 вызова, константы RandomWalk |
| `tools/alpha_content/data/creature_kind_evidence.json` | не было → результат |
| `Assets/CubeGame/Content/Generated/AlphaCreatureKindCases.g.cs` | не было → 3150 исполненных случаев, 15 списков |
| `Features/Creatures/AlphaCreatureKindSelector.cs` | не было → селектор |
| `Features/Creatures/AlphaSpawnRuntime.cs` | не было → `AlphaSpawnTemplateData`, `IAlphaSpawnTemplateSource`, `AlphaSpawnRuntime`, `AlphaSpawnTemplateState`, `AlphaGameDay` |
| `Features/Creatures/AlphaNpcBehaviour.cs` | не было → `AlphaRandomWalk`, `AlphaNpcMovementDriver` |
| `Features/Creatures/AlphaRecoveredPopulation.cs` | вид только из внешней функции → + фабрика `WithKindSelector` со списком кандидатов `0x5F56C0`, + `TemplatesForZone` (окно/радиус); старый конструктор без изменений |
| `Features/AI/CreatureAi.cs` | без цели: `PatrolRoute` или CubeGame-блуждание → сначала `AlphaMovement`, если подключён |
| `Tests/Creatures/EditMode/AlphaPopulationRuntimeTests.cs` | не было → 21 тест |
| этот отчёт | не было |

## 6. Тесты

| Прогон | Результат |
|---|---|
| `python -m tools.alpha_models.world.creature_kind_evidence --check` | `CREATURE_KIND_EVIDENCE=PASS cases=3150 lists=15 drift=False problems=[]` (байты места вызова, 3 вызова, константы RandomWalk) |
| `python -m tools.alpha_models.world.spawn_pipeline_evidence --check` | `SPAWN_PIPELINE_EVIDENCE=PASS drift=False problems=[]` (D1 не затронут) |
| Unity 6000.3.27f2 EditMode, весь проект (`AlphaOutput/f1a_editmode.xml`) | **660 total / 658 passed / 0 failed / 2 skipped**, `error CS` 0. База `1c6560c`: 639 / 637 / 0 / 2. Пропуски — прежние (`FullM0ZoneOracleTests`, нет локальных дампов `.scratch/m0`) |
| из них новые (`AlphaCreatureKindSelectorTests` 6, `AlphaSpawnRuntimeTests` 10, `AlphaNpcBehaviourTests` 5) | 21 / 21 passed |
| PlayMode | **не запускался**: правки не касаются сцены и `GameBootstrap`; runtime в сцену не подключён — PlayMode имеет смысл после интеграции Codex (§8 п. 7) |

Покрытие задачи: выбор вида (все 3150 исполненных случаев, пустой список → `0x3C`), стабильный ID, окно времени,
радиус 200, уход 512, таймер 1 200 000 мс, респаун по смене дня, unload/reload и новая сессия (тот же ID, то же
состояние смерти), расписание SpawnLocation, WalkPath (продвижение и пауза), RandomWalk (поводок, таймер),
приоритет драйвера Alpha над `PatrolRoute` в `CreatureAi`, IL-проверка неиспользования `AlphaSpawnTable` и `AlphaScatterTree`.

По ходу: два исправления компиляции до зелёного прогона — `CreatureAi` берёт цель через `TargetX/TargetZ`
(сборка AI не ссылается на `CubeGame.Features`, asmdef не менялся); новый конструктор заменён фабрикой
`WithKindSelector`, т.к. давал неоднозначность с вызовом `new AlphaRecoveredPopulation(seed, null)` в существующем тесте.

## 7. PARTIAL / UNKNOWN

1. Поток `rand()` генератора зоны не воспроизведён: состав и координаты конкретной зоны Alpha не совпадут.
2. Условия клетки сетки, не смоделированные в D1 (пропуски по климату `< 0.2`, маска ориентира, 20 вокселей, высота, подмена на растения по грунту `0xC`, флаг `0x200`), — по-прежнему не смоделированы.
3. Проверка активации 200 — 2D или 3D, не установлено (функция расстояния `0x42C6C0` не разобрана); runtime считает 3D.
4. Сохраняет ли Alpha таймер при выгрузке зоны и в сейве — `UNKNOWN`; принят `CUBEGAME_CONTRACT`.
5. Содержимое расписаний/точек WalkPath для сгенерированной деревни — `PARTIAL` (генератор `0x5AF170` не воспроизведён); runtime исполняет любые переданные списки, сейчас их никто не заполняет.
6. `RandomInteractionBehavior` — известно только дневное окно; точки не исполняются.
7. Длина вектора RandomWalk 10 — направление Alpha; скорость ходьбы остаётся игровой (`PARTIAL`).
8. 22 места создания шаблонов вне сетки (деревня, руины, данжи) не подключены к runtime: у них нет восстановленного источника.
9. Вызовы `0x5F56C0` из `0x5D9460` (`0x5D92A2`, `0x5D92F0`, mode 0/1) — назначение результата (`+0x38` структуры) не разобрано.

## 8. Что сделать Codex при интеграции

1. **`GameBootstrap.StartPopulation`:** заменить `new AlphaWorldPopulation(seed, level, ground)` (сейчас пусто) на
   `AlphaRecoveredPopulation.WithKindSelector(seed, AlphaCreatureKindSelector.FromClimate(...), ground)` и вести канал 0 через
   `AlphaSpawnRuntime`, а не через `Session.WorldPopulation` (там убитые не возвращаются — противоречит Alpha).
   `D3ZonePopulation` (канал 1) можно оставить в `WorldPopulation`.
2. Зоны: синхронизировать `runtime.SetLoadedZones` со стримингом; тик — от `GameClock` (`elapsedRealMs` = реальные
   мс кадра, как в Alpha `0x60DF9F`), день — `AlphaGameDay.Of(clock)`.
3. `OnEntityDied` → `runtime.NotifyDeath(id, day)` вместо `_population.MarkKilled` для канала 0.
4. `Created` → `Combatant.CreateCreature` + `EntitySpawned` (как `OnSpawnAppeared`), затем
   `ai.AttachAlphaMovement(AlphaNpcMovementDriver.ForTemplate(t.Data, () => clock.WorldTimeMs))`;
   `Despawned` → как `OnSpawnGone`. Позиция для проверки 512 — текущая позиция существа.
5. **`PERSISTENCE_SCHEMA_HANDOFF`:** сохранить `runtime.SnapshotStates()` (ID `ulong`, таймер мс, день смерти) в
   сейв мира и вызвать `RestoreStates` до загрузки зон. Вариант без смены схемы: `WorldDeltaKind.EntityStateChanged`
   с `X/Z` = зона, `Y` = канал·4096 + индекс, `Value` = таймер, отдельная запись для дня — решать владельцу сейва.
   Старые дельты «убит навсегда» канала 0 при переходе на runtime надо игнорировать или мигрировать.
6. **Дефект `AlphaScatterTree.g.cs` (W-18):** перевод `0x5F56C0` неверен (§3), а сама функция выбирает вид
   существа шаблона, а не растительность. `ZoneContentPlanner`/`AlphaVegetationPlanner` размещают по ней
   «растительность» с фракцией вида — при подключении runtime это дублирует диких существ. Нужно решение
   владельца маршрута контента: убрать этот слой или перевести на `AlphaCreatureKindSelector.Candidates`.
7. Проверить в PlayMode: существа появляются у героя днём, ночью новые враги не создаются, после смерти
   возвращаются через 20 мин реального времени или на следующий игровой день, уход/возврат героя не плодит дублей.

## 9. SHA

- база: `integration/m0-all` @ `1c6560c8f3e28b4a49f9a435327cd3fcb6e767a5`;
- результат: `claude/m0-f1-population-runtime` @ HEAD_PLACEHOLDER.
