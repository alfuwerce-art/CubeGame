# D86 M0-06 — существа, спавн и AI Alpha в основном Unity (партия DeepSeek, №2)

**TASK:** D86 (issue #13), M0-06A–06D в зоне DeepSeek: контракты существ из M0-00-пилота → production-модули, детерминированный спавн со стабильными ID, Alpha-AI (31 мс, угроза, зелья, додж, регенерация), связка с визуальным слоем AlphaActorView.

**STATUS: `READY_FOR_REVIEW` — не `ACCEPTED`.** Продюсер не принимал, ChatGPT контракт не ревьюил, `main` не тронут, ассеты Alpha не публиковались.

**Ветка:** `deepseek/d85-visual-unity` (после `da4c422`). **Worktree:** `D:\Projects\CubeGame-deepseek`. **Unity:** 6000.3.27f2.

---

## 1. Было → стало

| Что | Было | Стало |
|---|---|---|
| Контракты существ | только в `tools/m0_pilots/Npc/NpcContracts.cs` (не компилируются в Unity) | `Assets/CubeGame/Features/Creatures/CreatureContracts.cs` — единственный источник: часы AI, угроза, зелья, додж, регенерация, spawn id, blob состояния |
| AI | в пилоте не было поведенческого контроллера, только правила | `Assets/CubeGame/Features/AI/CreatureAi.cs` — состояния Idle/Wander/Chase/Attack/Dead, цель по угрозе/рескану, запрос действия только из доказанного набора вида/актора |
| Спавн | схема в пилоте, без размещения | `CreatureSpawnPlanner` + `CreatureSpawner`: детерминированный план из seed, звёзды по документированным шансам, стабильные id `region:cell:slot`, деспавн с сохранением blob и восстановлением |
| Существо в мире | не было | `CreatureAgent`: state + AI + AlphaActorView, движение, анимация, событие `ActionRequested` для боевого слоя игры (HP/урон не его) |
| Визуал вида | сопоставления не было | `CreatureVisualCatalog`: только реально подготовленные варианты; отсутствующий вид даёт `MISSING_VISUAL_CONTENT`, а не подмену модели |
| Тесты | пилотный консольный стенд | 18 EditMode + 3 PlayMode в основном проекте; полный прогон **333/333** и **25/25** |

---

## 2. Файлы

| Файл | Назначение |
|---|---|
| `Assets/CubeGame/Features/Creatures/CreatureContracts.cs` | `CreatureFaction`, `CreatureState`, `AiClock` (31 мс), `ThreatTable` (×0.9, сброс >20 с, рескан 3 с), `CreatureRules` (ветки зелий A/B, додж, регенерация 10 %/с, питомцы), `SpawnTable` (30 строк, шансы 1 %/0.1 %/0.01 %, кламп 4, стабильный id), `CreatureStateStore` (`M0NPC|1|…`, threat-строки, отказ при битом blob/версии) |
| `Assets/CubeGame/Features/Creatures/CreatureAiPort.cs` | `ICreatureAi` + `ICreatureAiWorld` — шов AI↔мир без цикла asmdef |
| `Assets/CubeGame/Features/Creatures/CreatureAgent.cs` | сущность в мире: `ICreatureAiWorld`, движение, визуал, `ActionRequested`, делегаты инвентаря (зелья/длительность действия) |
| `Assets/CubeGame/Features/Creatures/CreatureSpawner.cs` | `CreatureSpawnPlanner` (детерминированный хэш, занятость 70 %, уровень 1…30, звёзды), `CreatureSpawner` (стабильные id, деспавн с сохранением, восстановление, `TickAll`) |
| `Assets/CubeGame/Features/Creatures/CreatureVisualCatalog.cs` | вид → подготовленный Alpha-актор (0x02/0x1c/0x20/0x3f/0x65), честные `MISSING_VISUAL_CONTENT` |
| `Assets/CubeGame/Features/AI/CreatureAi.cs` | поведение: угроза/рескан, зелья до выбора цели, преследование, атака только из `world.AvailableActions`, помечивание недостижимой цели, додж по роллу игры |
| `Assets/CubeGame/Tests/Creatures/EditMode/CreatureTests.cs` (18) | часы, угроза, зелья, додж, регенерация, id, blob round-trip/повреждение, пассивные объекты, AI (погоня/атака/стан/пассив/недостижимость), детерминизм спавна, отказ от подмены визуала |
| `Assets/CubeGame/Tests/Creatures/PlayMode/CreaturePlayModeTests.cs` (3) | спавн реальных существ → погоня/атака, стабильный id, blob при деспавне и восстановление; дракон 0x65 играет только `action_037`; пассивный объект не двигается и не действует |

---

## 3. Фактические прогоны

| Прогон | Результат |
|---|---|
| EditMode (весь проект) | `total=333 passed=333 failed=0` — `AlphaOutput/editmode.xml` |
| PlayMode (весь проект) | `total=25 passed=25 failed=0` — `AlphaOutput/playmode.xml` |
| Кэш контента | 5 акторов READY (human 94 клипа/93 действия, dragon 5/4, cat/runner/wolf formula-walk) |

Команды — те же, что в [M0-01 отчёте](M0_01_DEEPSEEK_RUNTIME_REPORT.md) §3 (`-runTests -testPlatform EditMode|PlayMode`).

---

## 4. ALPHA_REFERENCE + DELTA

| Механика | Доказательство | Как реализовано |
|---|---|---|
| Тик AI 31 мс | M0-00B N-04 `DIRECT` | `AiClock.TickMs = 31`, EditMode |
| Угроза ×0.9/тик, сброс >20 с, рескан 3 с | N-06/N-07 `DIRECT` (live_check=NOT_CAPTURED) | `ThreatTable`, EditMode |
| Зелье A: HP<25 %, действие завершено, LifePotion (1,1); зелье B: союзник, HP<50 %, вне боя | M0-00B FINAL + prebatch `DIRECT` | `CreatureRules`, EditMode; инвентарь решает игра через делегат |
| Додж `rand()%10==0`, не босс, дистанция <2, 600 мс | N-08 `DIRECT` | `CreatureRules.TryDodge`, ролл — `ICreatureAiWorld.NextRoll` (детерминированный) |
| Регенерация фракции 5: +10 %/с | N-12 `DIRECT` | `CreatureRules.RegenerateFriendly` |
| Пассивные объекты (26 видов, фракция 6) без ходьбы и действий | D67/D69 | `CreatureKinds.IsPassiveObject`, AI `PASSIVE_OBJECT`, PlayMode |
| Набор действий вида | `npc_action_evidence` (15 видов) + библиотека актора (D69/D83) | `world.AvailableActions`: библиотека актора, иначе профиль вида; иначе действие не запрашивается |
| Стабильные ID | M0-00 `CUBEGAME_CONTRACT` | `SpawnTable.SpawnId` = region/cell/slot, blob `M0NPC/1` |
| Таблица спавна 30 строк, звёзды | схема `DIRECT`, формула ролла `PARTIAL` | `SpawnTable` константы; ролл помечен `PARTIAL` |

---

## 5. PARTIAL / UNKNOWN

- **Таблица «уровень/биом → вид» (N-19) не расшифрована**: `CreatureSpawnPlanner` принимает набор допустимых видов от генератора мира; веса биомов не выдумываются.
- **Ролл звёзд** — по документированным шансам, точное выражение Alpha не восстановлено (`PARTIAL`).
- **Популяция/деспавн/реактивация Alpha не найдены**: `DespawnRadius` и `OccupancyPercent` — `CUBEGAME_CONTRACT`.
- **Симуляция вне экрана не реализована**: сохраняется/восстанавливается blob (как Alpha), непрерывная симуляция — расширение CubeGame, не заявлено.
- **База HP существа**: используется принятая формула Core `CharacterStats.CalcCreatureMaxHealth(base=100, level, stars)`; базовая HP по видам Alpha не восстановлена (`PARTIAL`).
- **Инвентарь/зелья**: агент отдаёт запросы через делегаты; фактическое списание и лечение — за игрой (`B-U1` открыт).
- **Визуалы**: в кэше 5 вариантов; 130 боевых видов без подготовленного актора получают `MISSING_VISUAL_CONTENT` (без подмены). Массовая пересборка каталога не выполнялась (D86).
- **Боссы/питомцы (06E)**: список боссов перенесён, поведение питомцев и RuneGiant не реализованы (`BLOCKED` по R-9).

---

## 6. Блокеры

Нет блокеров для этой партии. Для M0-07 (города/данжи/торговцы) нужны данные шаблонов построек и сервисов; для M0-02 — таблица климат→объекты (`W-18`) и порядок шума рельефа (`W-04`), которые остаются `UNKNOWN` и должны маркироваться, а не заполняться догадками.

## 7. Handoff

API для Claude (подключение к GameSession/боевому слою) — [D86_DEEPSEEK_HANDOFF_M0_06](D86_DEEPSEEK_HANDOFF_M0_06.md). Следующая партия DeepSeek — M0-02/M0-07 content (биомы, растительность, постройки, города/данжи) и M0-04B предметный визуальный слой.
