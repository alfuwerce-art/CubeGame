# D86 — Исполняемый план ПОЛНОГО переноса Cube World Alpha 2013 в Unity

**Решение продюсера:** [D86](../decisions/D86-full-m0-unity-transfer-authorized.md) — `FULL_M0_IMPLEMENTATION_AUTHORIZED`, 2026-11-09.
**Полная исходная дорожная карта:** [M0_FULL_ALPHA_INTEGRATION.md](../planning/M0_FULL_ALPHA_INTEGRATION.md) — M0-01…M0-09, с 40 подэтапами вместе с M0-00.
**Контекст:** [M0_00_FULL_TRANSFER_MAP.md](../planning/M0_00_FULL_TRANSFER_MAP.md), M0-00C архитектурный DRAFT в `claude/m0-00c-integration-design`, актуальные S02–S05/D83/П1–П6.
**Статус:** ИСПОЛНЕНИЕ РАЗРЕШЕНО; **код Unity по D86 не объявлен выполненным без фактического коммита и прогона.**

## 0. Правило результата и планирование

Цель — **одна полностью играбельная одиночная игра**, в которой через единый Unity runtime можно создать мир/героя, исследовать процедурный мир, взаимодействовать с NPC, городами, врагами, данжами, предметами, торговцами, заданиями и вернуться после сохранения. Это воспроизведение согласованного функционала Alpha 2013, **не графический/игровой редизайн M1/M2** и не перенос бинарного движка Plasma. Мультиплеер в M0 исключён.

**Не останавливать работу после героя, меча, первой анимации, одного AI, одной деревни или удачного теста**: это промежуточные milestones в полном плане. Исполнение по проверяемым частям, не одна попытка написать весь проект в одном коммите. Все промежуточные срезы накапливаются **в одной Unity-игре**. Не делать дубликатов Core, методов генерации или S05. При блокере неизвестного Alpha-поведения — изолировать, указать PARTIAL/UNKNOWN и продолжить независимые системы, не подменять Alpha факт выдумкой.

Исторические B0–B3 D84 не являются обязательным стартовым шлюзом. Проверять **изменённые механики** EditMode/PlayMode и визуально; полный regression — перед объединением или если тронуты трансфер/контракт.

## 1. Общая база, запуск, ограничения

- Исходная общая точка двух веток D85: `f0d750ce602cf798d1e2c68fda6c41be5a6c408f` (D72+D83+П1–П6). Их ветки `claude/d85-gameplay-unity` и `deepseek/d85-visual-unity`. Новые изменения D86 опубликованы **отдельно** в `planning/d85-direct-unity-two-agents`, их нужно прочитать из GitHub; не предполагать, что D86 есть в старой рабочей ветке.
- Claude — `D:\Projects\CubeGame`, DeepSeek — `D:\Projects\CubeGame-deepseek`. Не создавать дополнительные worktree, не переключать чужие ветки и не удалять чужие untracked.
- Перед локальной работой: git status, git branch, git worktree list, Unity-процессы; при грязном дереве безопасный handoff, без reset/force push. После каждого самостоятельного изменения «было → станет» согласно AGENTS.md; уже утверждённый общий scope D86 не требует повторно утверждать старт.
- Коммиты и push — **только свои ветки**. Обе ветки сводятся контролируемо после проверки конфликтов/Unity smoke; `main` без разрешения продюсера не трогать. Unity Editor/Library и общий scene — эксклюзивный слот.
- Alpha `D:\Games\Cube world` read-only; повторный запуск оригинала согласуется отдельно; локальные исходные/конвертированные ресурсы Alpha — не публичный Git и не готовые для свободного распространения игровые ассеты.
- Принятые S02–S04 нельзя молча переписывать. Утверждён D86-перенос, но спорная миграция Core ItemType/WeaponSubtype, 24 типов/13 слотов и save v2 требует явного совместимого решения M0-00D и миграционных тестов.

## 2. Постоянное разграничение ответственности

**Claude — владение игровым состоянием и единой сценой:**
`Assets/CubeGame/Features/Session/**`, `World/**`, `Character/**`, `Movement/**`, `Items/**`, `Combat/**`, `Quests/**`, `Economy/**`, `Persistence/**`;
`Assets/CubeGame/Presentation/Gameplay/**`, `Presentation/UI/**`, `Assets/Scenes/Pilot.unity`, bootstrap.
Направления: меню/сессия, world seed/terrain/chunks/streaming/deltas, управление/камера/создание героя, доменные предметные операции и боёвка Core, устойчивое состояние NPC, экономика/задания/сейвы, UI, конечная интеграционная сборка. Доменная логика **не** читает визуальные клипы.

**DeepSeek — контент + алгоритмы поведения, изолированные поставщики для мира:**
`Assets/CubeGame/AlphaModels/**`, `AlphaModels.Import/**`, `Content/**`, `Features/AI/**`, `Features/Creatures/**`, `Presentation/Visual/**`, свои editor/test компоненты с непересекающимися именами.
Направления: модели/риг/анимации/крепления/эффекты; подтверждённые Alpha биомы, пропсы/растительность, вода/пещерный контент; размещение структур; реестры существ/NPC/фракций/спавна; AI, навигационные стратегии, special bosses/pets; шаблоны городов/данжей; предметная база/иконки/рецепты/NPC-сервисы; звуки и VFX. **Не** редактировать общую сцену и Bootstrap, не менять HP/деньги/сейвы в визуальном слое.

Сначала зафиксировать минимальный C# контракт внешних данных/событий:
`WorldSeed/RegionKey/ChunkKey`, `StableEntityId/KindId`, `SpawnDefinition`, `ItemDefinition/RawAlphaId`, `Move/Interaction/ActionStarted/Phase/Resolved`, `WorldDelta`, `NPCStateSnapshot`, `SaveVersion`. Соглашения о порядке исполнения и data ownership. **Не вводить дубли типов**. Две части UI и контента связываются только через единый игровой API. Изменения общих asmdef/сейва/Core — последовательно через handoff.

## 3. Модули M0-01…M0-09 — все обязательны

| Модуль | Результат в ОДНОЙ игре | Claude | DeepSeek |
|---|---|---|---|
| M0-01 Единство | Menu → Create/Load world/character → InGame → Save/Exit | session/clock/scene/bootstrap/save v1 API | подготовка реестров/визуального загрузчика и совместимых prefabs |
| M0-02 Мир | процедурная генерация по Alpha seed, исходные биомы, поверхность/вода/пещеры, коллизии, потоковая загрузка, постоянные дельты | world generator/terrain/chunk/streaming/persistence API | climate/biome/placeables/vegetation/water/caves content + structure placement данные |
| M0-03 Персонаж | раса/класс/внешность/начальный город, управление, ходьба/плавание/лазание/глайдер/лодка, XP и ресурсы | character creator state, ввод, камера, мотор, progression/save | модель, риг, одежда, action clips и визуальные состояния перемещения |
| M0-04 Предметы | 24 Alpha типа, инвентарь/лута/экипировка, редкости, крафт, расходники, сохранение | ItemInstance/domain, pickup, equipment, inventory/craft transactions and save | model/attachment/visual rules, item definitions/icons/recipe data |
| M0-05 Бой | оружие/классы/магия Alpha, боевые действия, блок/уклонение, попадания/HP, снаряды, смерть/XP/дроп | action phases, Core S03/S04, hit/HP, ability resources, loot/death | attack clips, grips, weapon actions, projectile/VFX visual runtime |
| M0-06 NPC/мобы | 156 видов: статические vs боевые, спавн по миру, фракции, AI, питомцы, боссы, состояние вне кадра | stable entities, persistent NPC state/offscreen policy, связи боя/мира/интеракций | spawn tables, AI behavior/navigation, creature forms, special bosses/pets |
| M0-07 Контент | города, деревни, NPC-сервисы, данжи, торговля, события и задания Alpha | interaction/quests/economy/wallet/quest save | structures/dungeons/camps/NPC service/loot/quest source data |
| M0-08 Интерфейс | меню/редактор героя/HUD/сумка/навыки/карта/журнал/настройки, аудио | UI presentation, controls, localization/layout/save settings | icons, audio VFX feedback, UI data and visual systems |
| M0-09 Финал | полный игровой цикл, регрессии, производительность, приёмка | integration, full build, golden scenarios, save load, tech review | content coverage, Alpha visual parity, AI/streaming performance, fixes |

Каждый агент **продолжает свои модули вплоть до M0-09**, а не получает «только первые 2 шага». Текущая работа должна давать реальный C# и функционирующий runtime. Большие недоказанные темы (W-18 climate objects, W-04 terrain noise, OQ-23 scaling, NPC offscreen, лут/торговля, unknown quest fields, RuneGiant, эффекты R-3/R-8) — адресные BLOCKED/PARTIAL с исследовательским экспериментом, не повод объявить весь этап невозможным.

## 4. Очередность зависимостей при параллельной реализации

**Пакет I — общая инфраструктура (первым):** Claude создаёт GameSession/GameClock, World/Entity/Item/Action контракты и save-compat; публикует SHA/API в Issue #13. DeepSeek одновременно готовит автономную загрузку S05 и подтверждённые data registries, затем реализует поставщиков контрактов. Совместная часть только после handoff.

**Пакет II — мир и старт:** Claude — seed/region/terrain/chunk streaming, basic menu/new world/creation/save; DeepSeek — исходные biome tables/vegetation/structures/caves/visual creatures. Запуск Unity должен показывать игровой мир с героем, а не пустую сцену.

**Пакет III — путешествия, предметы, существа:** Claude — movement modes/progression/inventory/persistence/AI state; DeepSeek — full visual actors/items/AI spawn/behavior, maps and encounter content. Игра должна жить после выгрузки региона.

**Пакет IV — игровой контент и взаимодействия:** Claude — combat/quests/trading/crafting/skills and UI; DeepSeek — town/dungeon templates, NPC services, animations/VFX/audio, loot data. Проверить взаимодействия в одном мире, не в изолированных prefab-шоурумах.

**Пакет V — завершение:** полное покрытие M0-01…09, длительная игра, 30/60/120 Hz регрессии там, где менялась анимация, FPS/память, нагрузка, save corruption recovery, отсутствие дублирования при чанках, визуальная приёмка; после review финальное решение только продюсер.

Каждый пакет — самостоятельные коммиты/сквозная игровая сцена, не один бесконечный монолитный коммит. Две ветки сходятся по согласованному SHA/контракту. Без подтверждённого merge/game build не утверждать, что изменения обоих агентов уже в одной игре.

## 5. Ожидаемый отчёт каждого агента

`TASK / SCOPE M0-01..09 / CURRENT_IMPLEMENTED / CHANGED FILES / BASE+HEAD / ACTUAL UNITY EDITMODE+PLAYMODE / REAL GAME SCENARIO / ALPHA_REFERENCE+DELTA / PARTIAL+UNKNOWN / BLOCKERS / HANDOFF / NEXT MODULE`. 
Обязательны правдивые статусы: `READY_FOR_REVIEW` vs `ACCEPTED`, не переносить сомнительные формулы без provenance. Завершённый этап и обновление памяти — после личной приёмки продюсера, но агент обязан обновлять рабочие отчёты и issue по ходу продвижения.

## 6. Сквозной критерий завершения ВСЕЙ программы

Создание персонажа и мира → исследование мира Alpha → вода/пещеры/биомы/город → существа/NPC → атака/магия/лут → инвентарь/навыки/крафт/торговец → данж/босс/квест → сохранение → выход → загрузка того же мира с теми же изменениями и NPC состояниями; панель UI/карта/звук; без утраты данных/дублей; стабильная сборка. Провал любого критического сценария = INCOMPLETE или BLOCKED, а не 100% TRANSFERRED.

До подтверждения самостоятельной публикации местного агентного кода — статус **AUTHORIZED / WORK NOT YET OBSERVED**.
