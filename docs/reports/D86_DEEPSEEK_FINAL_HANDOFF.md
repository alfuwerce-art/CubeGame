# D86 — финальный handoff DeepSeek: интеграционный пакет зоны контента

**Ветка:** `deepseek/d85-visual-unity`. **Репозиторий:** `D:\Projects\CubeGame-deepseek`.
**Статус:** `READY_FOR_REVIEW` (не ACCEPTED; приёмка — продюсер/ChatGPT).
**Голова:** `2858a53` + текущий коммит. Все факты ниже подтверждены прогонами, ссылки на артефакты — в конце.

## 1. Что готово к подключению (API)

| Область | API | Назначение |
|---|---|---|
| Кэш/акторы | `AlphaContentSource.Resolve()`, `AlphaActorLibrary.TryGet(variant)`, `AlphaActorView.Spawn` | загрузка 156 подготовленных видов, визуал + анимации |
| Движение/действия | `AlphaActorView.ApplyLocomotion`, `TryPlayAction(code)`, `Tick(dt)`, `TryGetActionTiming(code, out timing)` | ходьба/покой/действия и Alpha-тайминги (windup/actionEnd/recovery) |
| Бой | `CreatureAgent.ActionRequested`, `CreatureAgent.TryGetActionTiming` | AI просит действие, игровой слой планирует урон на windup |
| Существа | `CreatureSpawner.SpawnOne/TickAll/DespawnBeyond`, `CreatureStateStore`, `CreatureAi` | спавн/деспавн, блоб `M0NPC|1|…`, AI 31 мс, троттлинг запросов 250 мс |
| Климат/W-18 | `AlphaScatterSelector`, `AlphaScatterTree`, `IAlphaClimateGrid` + `AlphaScatterField`, `AlphaVegetationPlanner` | выбор объектов по климату, детерминированное размещение |
| Мир | `AlphaStructurePlacementPlanner`, `AlphaDungeonLayoutPlanner`, `AlphaDungeonChests`, `AlphaNpcServices` | дома/данж/сундуки/сервисы NPC |
| Предметы | `AlphaItemRules`, `AlphaItemSlotRules`, `AlphaWeaponLayer`, `AlphaOffhandLayer`, `AlphaHeldItemLayer`, `AlphaEquipmentLayer`, `AlphaConsumableRules` | якоря, слоты, броня/оружие/расходники, кулдауны |
| Иконки | `AlphaItemIconRenderer`, `AlphaIconAtlas.TryLoad/TryGetSprite` | иконки и атлас для UI |
| Персонаж | `AlphaPlayerVisualBuilder` + `CharacterVisualRequest.Default` | визуал игрока из дескриптора (раса/волосы/оружие/рука/броня/предмет) |
| Отчёты | `AlphaContentSummary.Report()`, `AlphaContentManifest.BuildJson()` | стартовый лог и машинный манифест контента |
| Демо | `AlphaContentShowcase` | пустая сцена: игрок + существа + дома + данж + сундуки |

## 2. Доказательства (реальные прогоны)

- **EditMode: 355/355 passed** — `AlphaOutput/editmode.xml`.
- **PlayMode: 46/46 passed** — `AlphaOutput/playmode.xml`; включает: человека с клипом, дракона, волка с библиотекой, пассивный объект, AI→хук действия, тайминг у агента, viz despawn/respawn, 12 существ × 600 кадров (0.172 мс/кадр), churn 200 spawn/destroy (0 mesh-утечек, +41.8 МБ), игрока с бронёй/зельем, атлас иконок, витрину.
- **Preview: PASS** — 156 акторов, **420 кадров**, 0 проблем; кадры мира/экипировки.
- **Манифест**: `AlphaOutput/content_manifest.json` — `kinds=156, actionsKinds=129, actionClips=900, heldItems=14, equipmentItems=32, itemSlots=11, worldObjects=67, itemNames=133, skills=27, dungeonChestItems=10`.
- **Генераторы `--check`**: CONTENT_TABLES, WORLD_CTOR_EVIDENCE, STAGED_VISUALS, SCATTER_EVIDENCE, SCATTER_TREE, HELD_ITEM_MODELS, EQUIPMENT_MODELS — PASS.
- **Инвариант таблицы ↔ кэша**: 156/156 видов (режим, клипы, действия, тайминги) — EditMode.

## 3. Что НЕ готово (честно)

| Пункт | Причина |
|---|---|
| RuneGiant 0x76 поведение/действия | R-9 `BLOCKED`, библиотеки нет |
| Лампа/пет/шея/плечо | законы `lamp`/`pet`/`neck`/`shoulder` не реализованы; API отказывает, не рисует |
| Родитель `special_item` (планер/лодка) | доказан только `center`; рука — `CUBEGAME_CONTRACT` |
| Эффект расходников (B-U1) | лечение/эффект в коде не восстановлены; кулдаун/действия доказаны |
| VFX/снаряды (R-3/R-8) | данных нет |
| Аудио | данных нет |
| W-18 живая сверка | требует запуска Alpha/эталона — не делалось; порт `World_placeObjectWithSpacing @ 0x005effa0` не выполнен (noise+terrain) |
| Торговля/задания (F-U5, J-U4/U5/U7) | зона Claude/неизвестно |
| Финальная сборка/профилирование с GameSession | зона Claude; приёмка не запрашивалась |

## 4. Следующие шаги (предложение)

1. Claude подключает API из §1 к `GameSession`/сцене, использует манифест для bootstrap-лога.
2. ChatGPT проверяет этот пакет и решает судьбу `TRANSLATED_FROM_CODE` W-18 (live-сверка или приёмка как есть).
3. После интеграции — сквозной профиль FPS/памяти на сцене Claude; наш смоук и churn переносятся как baseline.
4. Дальнейшие партии DeepSeek — только по новым доказательствам (R-9, VFX, B-U1) или по запросу Claude.

## 5. Артефакты

`AlphaOutput/editmode.xml`, `AlphaOutput/playmode.xml`, `AlphaOutput/preview.txt`, `AlphaOutput/content_manifest.json`, `AlphaOutput/icons/atlas.png`/`atlas.json`, `AlphaOutput/content_status.txt`. Все — локальные (не в Git), воспроизводятся командами из handoff M0-02.
