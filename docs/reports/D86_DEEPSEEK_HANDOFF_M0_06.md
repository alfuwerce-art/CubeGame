# D86 — handoff DeepSeek → Claude: существа, спавн и AI (M0-06)

**Дата:** 2026-11-09. **Ветка:** `deepseek/d85-visual-unity`. **Отчёт:** [M0_06_DEEPSEEK_CREATURES_REPORT](M0_06_DEEPSEEK_CREATURES_REPORT.md).
**Статус:** `READY_FOR_REVIEW`; общая сцена, Core, сейвы и UI не менялись.

## 1. Модули

| Модуль | asmdef | Роль |
|---|---|---|
| `Assets/CubeGame/Features/Creatures` | `CubeGame.Features.Creatures` | контракты существ, спавн, агент, визуальная привязка |
| `Assets/CubeGame/Features/AI` | `CubeGame.Features.AI` | поведение `CreatureAi : ICreatureAi` |
| `Assets/CubeGame/AlphaModels` | `CubeGame.AlphaModels` | Alpha-визуал (`AlphaActorView`) |
| `Assets/CubeGame/Content` | `CubeGame.Content` | виды/действия/фракции из подтверждённых таблиц |

Зависимости: `AI → Creatures → {Core, Content, AlphaModels}`. Обратной зависимости нет — Claude может подключать модули в любом порядке.

## 2. Что вызвать из GameSession/мира

```csharp
// 1. Спавн в регионе: набор допустимых видов даёт генератор мира (таблица «уровень→вид» Alpha не расшифрована).
var spawner = go.AddComponent<CreatureSpawner>();
spawner.Seed = worldSeed; spawner.RegionX = rx; spawner.RegionZ = rz; spawner.CellX = cx; spawner.CellZ = cz;
spawner.AiFactory = () => new CubeGame.Features.AI.CreatureAi();   // модуль AI сам себя не регистрирует
spawner.ActorLibrary = AlphaActorLibrary.Shared;
spawner.Spawn();

// 2. Часы игры — единственные:
void Update() => spawner.TickAll(gameClock.DeltaSeconds);

// 3. Игрок как цель AI (регистрируется один раз, снимается при уничтожении):
CreatureTargets.Register(playerTargetSource);   // ICreatureTargetSource: TargetId, Position, Faction, Alive

// 4. Боевые последствия — на стороне игры:
agent.ActionRequested += (creature, actionCode, source) => combat.RequestAttack(creature.State, actionCode);

// 5. Инвентарь NPC (зелья) и тайминги действия:
agent.LifePotionQuery   = state => inventory.Has(state, type: 1, subtype: 1);
agent.LifePotionConsume = state => inventory.Consume(state, type: 1, subtype: 1);
agent.ActionDurationQuery = state => combat.GetAttackTiming(state.ActionId);

// 6. Выгрузка региона:
int removed = spawner.DespawnBeyond(playerPosition);   // blob сохраняется в spawner.Store
```

`CreatureAgent` **не** применяет урон, не пишет HP, не начисляет лут/XP — это делает игра по событию `ActionRequested`.

## 3. Постоянное состояние

- `CreatureState` сериализуется `CreatureStateStore` (заголовок `M0NPC`, версия 1, threat-строки). Формат — `CUBEGAME_CONTRACT`, не байтовый Alpha.
- Идентичность: `SpawnTable.SpawnId(regionX, regionZ, cellX, cellZ, slot)`; после выгрузки/загрузки тот же id.
- Сейв мира: добавьте blob-и в свой `SaveGame` через `CreatureStateStore.SerializeState/TryParseState` (версию менять только с миграцией).

## 4. Что AI запрашивает и чего не делает

- Цель: верхушка `ThreatTable`, иначе ближайший враждебный `ICreatureTargetSource` в радиусе 16 (`CUBEGAME_CONTRACT`).
- Доступные действия: `CreatureAgent.AvailableActions` = библиотека актора (например, у дракона 37/40/42/43), иначе профиль вида; при пустом наборе AI пишет `IN_RANGE_NO_ACTION_EVIDENCE` и не выдумывает удар.
- Пассивные объекты (26 видов, фракция 6) не ходят и не действуют (`PASSIVE_OBJECT`).
- Стан блокирует тик; додж — только по роллу игры (`NextRoll`), не босс, дистанция <2.

## 5. Ограничения, которые нельзя замалчивать

- Таблица спавна «уровень/биом → вид» и популяция Alpha не восстановлены: набор видов передаёт генератор мира; `OccupancyPercent`, `DespawnRadius`, радиус агрессии — `CUBEGAME_CONTRACT`.
- База HP вида не восстановлена: используется `CharacterStats.CalcCreatureMaxHealth(100, level, stars)` — при появлении данных заменить в одном месте (`CreatureSpawner.SpawnOne`).
- Визуалы подготовлены для **93 видов** (67 боевых, включая человека/эльфа/дракона, и все 26 пассивных объектов). Список «вид → вариант» — `AlphaStagedVisuals` (коммит), сами варианты — в локальном кэше. Остальные 63 боевых вида дают `SPAWN_VISUAL MISSING_VISUAL_CONTENT` и существуют без модели (AI работает). Пассивные объекты корректно живут как `STATIC_OBJECT` (PlayMode-тест).
- `CreatureSpawner.GroundHeight(x, z)` — передайте высоту из генератора мира, чтобы существа вставали на поверхность; без хука используется Y из плана.
- Симуляция вне экрана не заявлена: только blob сохранения.

## 6. Проверки

`AlphaOutput/editmode.xml` — 333/333, в т.ч. 18 новых тестов существ; `AlphaOutput/playmode.xml` — 25/25, в т.ч. 3 PlayMode с реальными существами и драконом 0x65. Локальные артефакты не в Git.
