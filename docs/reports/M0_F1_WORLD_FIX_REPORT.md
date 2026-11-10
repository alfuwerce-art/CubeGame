# M0-F1 WORLD FIX

10 ноября 2026 · `codex/m0-f1-world-runtime` · база `1c6560c8f3e28b4a49f9a435327cd3fcb6e767a5`

**`READY_FOR_REVIEW`** · F1A и F1B сведены в ветке Codex. Приёмка и слияние в `integration/m0-all` — после ревью продюсера.

## F1A и подключение в Pilot

Включён опубликованный F1A Claude `9e509155b126d78491cd51c817f4ccad8a182c90`, код `ac8b55494a402a0877d7d82cd5d5077213182c7b`. Селектор `0x5F56C0` сверили на 3150 входах. Отчёт F1A содержит адреса, константы, проверки расписания, WalkPath и RandomWalk. [Отчёт F1A](M0_F1A_POPULATION_RUNTIME_REPORT.md)

Новые миры `/6` подключают `AlphaRecoveredPopulation` к `AlphaSpawnRuntime` на канале 0. `GameBootstrap` передаёт климат реальной точки и высоту первого несплошного вокселя, синхронизирует шаблоны с активными зонами и подаёт фиксированные тики `GameClock`. Появление связывает шаблон, combatant, AI и визуал одним `ForZoneSpawn`. Смерть снимает боевую запись, оставляет визуал смерти до выгрузки или респауна и запускает таймер. Возвращение использует тот же ID. На шаблоны с данными `Schedule` или `WalkPath` ставится восстановленный драйвер, дикие шаблоны используют RandomWalk. Маршрут расписания от реального `GameClock` проверен на тестовом NPC. Списки реальных деревенских NPC остаются `PARTIAL`.

Схема сейва сохранена. Для канала 0 в `/6` существующий `EntityStateChanged` хранит отрицательный остаток таймера в `Value` и отметку смерти в `TimeMs`. День смерти вычисляется по `AlphaGameDay`. При сохранении удаляются записи с истёкшим таймером или прошедшим днём. Выгруженная зона паркует остаток таймера. Поведение таймера при выгрузке и сохранении — `CUBEGAME_CONTRACT`, соответствующее поведение Alpha `UNKNOWN`. Состояния других каналов, `ObjectRemoved` и воксельные дельты сохраняются по прежним ключам.

## Коллайдер

На базе D90 воспроизвелись оба падения. `TerrainEditSurvivesSaveExitAndLoad` получил Y=86 вместо 80. `VoxelEditBuildsColliderAndSurvivesSaveLoad` получил Y=85 вместо 88. В обоих случаях `WorldState` уже возвращал изменённую колонку. Готовые чанки занимали бюджет `StreamingWorldView.Pump` раньше пересборок.

`WorldDeltaStore` сообщает координаты изменённой колонки. `StreamingWorldView` ставит в FIFO-очередь затронутые загруженные чанки с учётом границы меша. Пересборки получают бюджет первыми. Старая физика отключается при замене чанка. Фоновые задачи пишут в очередь своего экземпляра мира, поэтому результат предыдущего мира не попадает в новый после save/exit/load.

Оба исходных теста прошли с прежними таймаутами. `EditedColliderGetsTheNextBuildEvenWithReadyChunks` удерживает готовые чанки в очереди, даёт один бюджет сборки и проверяет новый коллайдер в том же кадре.

## D3 в игровом маршруте

- `GameBootstrap` создаёт новые миры с генератором `/6`. Один набор провайдеров формирует дома, городские объекты и данжи с сундуками. Дома и данжи поступают в меши через сгенерированные колонки. F1A установил, что прежняя W-18 выборка была неточным переносом селектора существ `0x5F56C0`. В `/6` её отключили, а выбор вида выполняет восстановленный канал 0. `/4` и `/5` сохраняют прежний набор провайдеров и загрузку существующих сейвов.
- `Town` — тип `1` **записи зоны**. Мебель использует имена таблицы объектов мира и отдельный подписанный `PLACEHOLDER_NOT_ALPHA` в сцене. Координаты мебели, выбор объектов и геометрия домов имеют статус `CUBEGAME_CONTRACT`.
- Типы записи `3` и `5` ведут к вызову `0x5CC510` из Alpha `WorldInfo_generateBiomeContent`. Источник гейта — байты `0x5E84A2` и `0x5E84D4`, разобранные в [аудите D3–D6](CODEX_D3_D6_INDEPENDENT_AUDIT.md). Генератор CubeGame выдаёт запись `3`. Запись `5` покрыта тестом гейта. Раскладка, пол и проходы — `CUBEGAME_CONTRACT`. Подтипы Alpha, вход и содержимое комнат остаются `PARTIAL` или `UNKNOWN`.
- Сундук `0x8F` получает `StableEntityId.ForZoneSpawn(zone, 1, index)`. Один план зоны определяет его позицию и карвинг. Зона кэширует базовые колонки. После вытеснения из кэша генератор детерминированно восстанавливает базу, затем `WorldState` применяет пользовательские дельты. Сундуки и мебель выгружаются и восстанавливаются вместе с активной зоной. При активации существующий `ObjectRemoved` подавляет удалённый объект. Интеракции и выдача содержимого относятся к M0-F3.

## Проверки

| Прогон | Результат |
|---|---|
| Исходный PlayMode мира, `d90_baseline_world.xml` | 1 passed, 2 failed |
| PlayMode с исправлением коллайдера, `d90_collider.xml` | 4 passed, 0 failed |
| EditMode провайдеров и версий мира, `d90_d3_edit3.xml` | 25 passed, 0 failed |
| PlayMode D3, коллайдера и стриминга, `d90_d3_world3.xml` | 5 passed, 0 failed |
| `creature_kind_evidence --check`, `spawn_pipeline_evidence --check`, `build_spawn_table --check` | PASS, `drift=False`. 3150 входов селектора |
| PlayMode населения после сведения, `d90_f1_population.xml` | 1 passed, 0 failed |
| Полный EditMode после сведения, `d90_f1_edit.xml` | 666 total, 664 passed, 0 failed, 2 skipped |
| Полный PlayMode после сведения, `d90_f1_play.xml` | 60 total, 60 passed, 0 failed, 0 skipped |

Unity `6000.3.27f2`, `error CS = 0` в итоговых логах. Два пропуска EditMode — `FullM0ZoneOracleTests` для отсутствующих локальных `zone_lake.json` и `zone_carves.json`, как на базе D90. XML и логи лежат в `.unity-install/results/`.

PlayMode проверил основной вход `GameBootstrap`, городской визуал, сундуки, выгрузку, восстановление ID и координат, подавление удалённого объекта и сохранение правки внутри вырезанного данжа через save/exit/load. Сценарий населения прошёл фактическую смерть через `CombatSystem`, парковку таймера, тот же ID после загрузки, истечение таймера, ночной запрет и респаун в новом игровом дне. Оба исходных теста коллайдера прошли в адресном и полном прогонах.

## PARTIAL и UNKNOWN

- Точная последовательность `rand()` Alpha, дополнительные гейты клеток и 22 места вне дикой сетки остаются открыты. Естественная популяция использует восстановленный селектор. Списки городских расписаний и путей NPC ещё требуют данных, как и правила экипировки R-1. [Ограничения F1A](M0_F1A_POPULATION_RUNTIME_REPORT.md#7-partial--unknown)
- Для города подтверждён гейт записи. Мебель в сцене представлена именованными прокси. Взаимодействие с городом и сундуком относится к M0-F3. Альфа-вход в данж, полная геометрия и наполнение данжей остаются открыты.

Открытые задачи M0-F2 перечислены в [аудите D3–D6](CODEX_D3_D6_INDEPENDENT_AUDIT.md) и [плане D90](../planning/M0_REMAINING_PLAN_D90.md). Среди них внешность героя, единственный визуал наземного лута, дальний бой и эффекты R-3/R-8.

## Файлы сведения Codex

В `Assets/CubeGame` изменены следующие файлы.

- `Features/World/` — `AlphaRegionGenerator.cs`, `AlphaTerrainGenerator.cs`, `AlphaZoneFeatures.cs`, `WorldDeltaStore.cs`, `WorldState.cs`, `ZoneContent.cs`.
- `Features/Creatures/` — `AlphaContentProviders.cs`, `AlphaDungeonContentProvider.cs`, `AlphaZoneContentProvider.cs`. В `Features/Contracts/` — `GameplayEvents.cs`.
- `Presentation/Gameplay/` — `GameBootstrap.cs`, `GameVisualSink.cs`. В `Presentation/Gameplay/World/` — `D3ZonePopulation.cs`, `StreamingWorldView.cs`, новые `RecoveredSpawnPersistence.cs` и `.meta`.
- `Tests/` — `Creatures/EditMode/AlphaContentProvidersTests.cs`, `Creatures/EditMode/AlphaDungeonContentProviderTests.cs`, `FullM0/EditMode/FullM0PopulationTests.cs`, `FullM0/PlayMode/FullM0WorldStreamingPlayModeTests.cs` и его `CubeGame.Tests.FullM0.PlayMode.asmdef`.

Обновлены `PROJECT_MEMORY.md`, `docs/ACTIVE_STAGE.md` и этот отчёт. Файлы Claude перечислены в его отчёте. `ProjectSettings.asset` сохранён локально. К прежним строкам локального `AGENT_COORDINATION.txt` добавлена передача слота Unity после F1B. Оба локальных файла вне коммита.
