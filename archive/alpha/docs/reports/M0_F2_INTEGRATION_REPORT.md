# M0-F2 INTEGRATION CHECKPOINT

10 ноября 2026 · **`ACCEPTED` продюсером** · `4b9480836beeba859340938486f4200d16c1549e`

Продюсер принял объединённый runtime мира M0-F1, персонажа M0-F2A и боя M0-F2B. GitHub подтвердил fast-forward `integration/m0-all` с `d21e5fa` на утверждённый SHA.

## 1. Исходные ветки

| Что | Ветка | SHA |
|---|---|---|
| Canonical | `integration/m0-all` | `d21e5fa` (документы M1/S06 поверх `1c6560c`) |
| M0-F1 WORLD FIX | `codex/m0-f1-world-runtime` | `748d650` (в нём F1A Claude `9e50915`) |
| M0-F2A CHARACTER RUNTIME | `claude/m0-f2a-character-runtime` | `5f197f9` |
| DeepSeek combat evidence | локальный коммит DeepSeek | `08ae4e3` → в F2B как `28fa7a3` |
| M0-F2B COMBAT RUNTIME | `claude/m0-f2b-combat-runtime` | `9857b2e` |

Цепочка F2B: `1c6560c → … → 748d650 (F1) → 5f197f9 (F2A) → 28fa7a3 (DeepSeek) → 9857b2e (F2B)`. Поэтому сливалась одна ветка F2B, без cherry-pick по этапам.

## 2. Слияние

- `integration/m0-f2-final-review` создана от `origin/integration/m0-all` = `d21e5fa`.
- Merge-коммит `23cea9a`: `git merge --no-ff claude/m0-f2b-combat-runtime`. История F2B не переписывалась, force-push не было.
- **Конфликтов нет.** Canonical после общей базы `1c6560c` менял только три документа: `docs/planning/ROADMAP_DRAFT.md`, `docs/stages/M1_MODEL_ANIMATION_EVOLUTION_STAGE.md` и `docs/stages/S06_RENDER_ENVIRONMENT_EVOLUTION_STAGE.md`. F2B эти файлы не трогал. Все три присутствуют в результате без изменений. Gameplay-код с двух сторон не пересекался, поэтому выбирать сторону не пришлось.
- Исторические документы не удалялись. Локальные `ProjectSettings/ProjectSettings.asset` (только переводы строк) и `docs/agents/AGENT_COORDINATION.txt` (журнальные записи других агентов) в коммиты не входят. Так же было в F1/F2A/F2B.

## 3. Состав (CODE EXISTS)

Предки HEAD: `748d650`, `9e50915`, `5f197f9`, `28fa7a3` и `9857b2e` — да.

**DeepSeek evidence.** `08ae4e3` сам предком не является: в F2B он перенесён как `28fa7a3`. Автор DeepSeek сохранён, `git diff 08ae4e3 28fa7a3` пуст, `patch-id` совпадает (`8024f9f7…`). Файлы на месте: `AlphaCombatRuntimeData.g.cs`, `gen_combat_runtime_data.py`, evidence JSON, тесты и отчёт.

| Этап | Результат | Где |
|---|---|---|
| F1 | recovered population | `Features/Creatures/AlphaRecoveredPopulation.cs`, `RecoveredSpawnPersistence.cs` |
| F1 | город/данж/сундук | `AlphaDungeonContentProvider.cs`, `D3ZonePopulation.cs` |
| F1 | collider fix | `StreamingWorldView.cs` (очередь пересборок) |
| F1 | persistence, stable ID | `StableEntityId` (`Features/Contracts/Ids.cs`), `WorldDeltaStore.cs` |
| F2A | внешность, экипировка | `HeroVisualComposer` (`Presentation/Visual/HeroVisual.cs`), `AlphaPlayerVisualBuilder`, `AlphaCharacterModels.g.cs` |
| F2A | один владелец лута, floating origin, восстановление визуала | `GameVisualSink.cs` |
| F2B | melee / ranged / staff / hitscan, без melee-fallback | `Features/Combat/AttackRoute.cs`, `CombatSystem.cs`, `ProjectileSystem.cs` |
| F2B | владелец визуала снарядов | `Presentation/Gameplay/ProjectileVisuals.cs` |
| F2B | масштаб вида, hit box Alpha | `AlphaCreatureSizes.g.cs`, `Content/AlphaCreatureSizes.cs` |

Runtime-подключение и тесты подтверждены прогонами ниже (§5–§6).

## 4. Generated data

Каждый генератор с `--check` запускался на ветке интеграции.

| Генератор | Результат |
|---|---|
| `gen_combat_runtime_data.py --check` (DeepSeek/F2B) | PASS, attacks=106 weapons=26 checks=42, drift=[] |
| `creature_kind_evidence.py --check` (F1) | PASS, 3150 входов, drift=False |
| `spawn_pipeline_evidence.py --check` (F1) | PASS, drift=False |
| `build_spawn_table.py --check` | PASS, drift=False |
| `attack_selection.py --check` | PASS, checks=31 |
| `build_equipment_models` / `build_held_item_models` / `build_staged_visuals` / `build_world_evidence` | PASS (32 / 14 / 156 / 67+133+27) |
| `extract_scatter_table` / `scatter_tree_translate2` / `extract_sounds` | PASS |
| `gen_alpha_tables.py --check` | PASS, hash `b2cafaad5e6c8c02` |
| `python -m tools.alpha_models.catalog.item_layer --check` | OK (запуск только как модуль) |
| `build_content_tables.py --check` | PASS после чистого checkout. См. ниже |
| `build_transfer_registry.py --check` | **FAIL, существовал до merge.** См. ниже |

У F2A `gen_character_models.py` и F2B `gen_creature_sizes.py` нет `--check`. Оба перегенерированы, вывод сравнён с закоммиченным без учёта CR. **Drift нет**: 183 модели внешности, 1565 моделей предметов, 156 видов.

**`build_content_tables` — не drift данных.** Первый запуск дал FAIL: в заголовке `AlphaContentTables.g.cs` различалась только строка sha256 для `alpha_confirmed_tables.json`. Генератор хэширует сырые байты источника. Закоммиченный хэш `23da…` — это CRLF-форма того же содержимого. В моей рабочей копии этот JSON лежал с LF (`ecc1…`), остальные источники — с CRLF. После `git checkout` JSON проверка проходит. Таблицы не менялись. Зависимость хэша от окончаний строк остаётся как особенность инструмента.

**`build_transfer_registry` — существовал до merge.** Ошибка: `unknown status 'IMPLEMENTED (правило, сейв, редактор); модели рас — D4' for P-01`. Её вызывает строка 109 `docs/research/alpha2013/M0_00B_ALPHA_GAMEPLAY_COVERAGE_MATRIX.md`, изменённая до F1 (`48ca8bc`). Матрица и инструмент между `d21e5fa` и HEAD не менялись, F1/F2 к ошибке отношения не имеют. Не исправлено: выбор статуса — смысловое решение. Открыто для продюсера/F3.

## 5. Unity-регрессия

Unity `6000.3.27f2`. XML и логи лежат в `AlphaOutput/` (вне git).

| Прогон | Total | Passed | Failed | Skipped | error CS |
|---|---|---|---|---|---|
| EditMode после merge, `f2int_editmode.xml` | 746 | 744 | 0 | 2 | 0 |
| PlayMode после merge (68 F2B + новый сценарий, первая версия), `f2int_playmode.xml` | 69 | 68 | 1 | 0 | 0 |
| Новый сквозной сценарий после правок теста, `f2int_e2e.xml` | 1 | 1 | 0 | 0 | 0 |
| **Итоговый EditMode, `f2int_final_editmode.xml`** | **746** | **744** | **0** | 2 | **0** |
| **Итоговый PlayMode, `f2int_final_playmode.xml`** | **69** | **69** | **0** | 0 | **0** |

Два пропуска EditMode те же, что в F1/F2A/F2B: `FullM0ZoneOracleTests` без локальных `zone_lake.json` и `zone_carves.json`.

Все 68 прежних PlayMode-тестов прошли после merge с первого раза. Единственное падение — первая версия нового сценария. Три правки касались самого теста, runtime не менялся:
1. Тест стрелял, пока шёл последний melee-взмах. Отказ «still performing an action» — правильное поведение. Добавлено ожидание конца действия.
2. Тест вызывал `TryStartAttack(id, ActionId)` — прежнюю перегрузку без плана. Теперь он использует маршрут F2B `AlphaAttackRoute.ForAction(0x1C)`. См. §7.
3. Проверка «владелец не поражается» считала удары волков по герою. Теперь она считает только попадания героя по себе.

## 6. Сквозной сценарий F1+F2

`Tests/FullM0/PlayMode/FullM0F2IntegrationPlayModeTests.WorldCharacterAndCombatRunTogetherThroughSaveExitLoad` идёт в одной сцене `GameBootstrap`, в одном runtime:

1. **New world.** Генератор `/6` с recovered population F1. Герой — дварф-женщина с нестандартной внешностью. Видна одна модель героя, один `Player` и один `WorldRoot`.
2. **Movement.** `CharacterController` двигает героя, мировая позиция сессии следует за ним, герой стоит на сгенерированном terrain.
3. **Melee.** Melee-маршрут, волк убит, ровно одна смерть, ни одного снаряда, опыт начислен один раз.
4. **Loot.** Убийство даёт лут на земле, один визуал на предмет.
5. **Ranged.** Лук из сумки меняет визуал героя. Стрела Alpha появляется в сцене, летит, попадает один раз через `CombatSystem.ResolveHit` и удаляется вместе с визуалом.
6. **Hitscan.** Группа `0x1C`: один луч, одно попадание, урон, ни одного летящего снаряда. Владелец собственным ударом не поражается.
7. **Origin shift.** `ShiftOrigin(64,64)`: визуал существа, герой и лут остаются на своих мировых позициях.
8. **World streaming.** Телепорт на 1500 блоков и обратно: дальние чанки выгружаются, цель подгружается, дублей `Player`/`WorldRoot` нет, лут согласован.
9. **Save → exit → load.** На выходе мир и игрок уничтожены. После загрузки совпадают внешность, сигнатура визуала, лук в руке, опыт, уровень, число вещей в сумке и позиция. Снарядов 0, лут не дублирован: наземные предметы не сохраняются — правило F2A.
10. **Продолжение.** Новая стрела после загрузки попадает в новую цель.

Шаги, которые отдельно уже покрыты прежними тестами (город/данж/сундуки, коллайдер, таймер респауна через save/load, все расы и полы), прошли в том же полном PlayMode.

## 7. Открытые PARTIAL / UNKNOWN

Новых не появилось. Унаследованы:

- **F1:** точный поток `rand()` Alpha, гейты клеток, 22 места создания вне сетки, списки расписаний городских NPC, экипировка NPC R-1. Таймер респауна при выгрузке и сохранении — `CUBEGAME_CONTRACT`. Вход, геометрия и содержимое данжа — `PARTIAL`/`UNKNOWN`. Мебель — `PLACEHOLDER_NOT_ALPHA`.
- **F2A:** OrcFemale `0x0C` рисуется собственным актором. Блоки наплечников, амулета и лампы — `PARTIAL`.
- **F2B:** формулы урона Alpha для дальнего боя, подруливание бумеранга. Способности и бомба `0x68` без ввода. Dodge — `UNKNOWN`. Визуал луча — `PLACEHOLDER`. Hitscan в сквозном сценарии запущен через `CombatSystem` сцены, а не вводом героя: у героя нет оружия этой группы.
- **Наблюдение, интеграция: атаки существ.** `CombatSystem.TryStartAttack(id, ActionId)` — прежняя перегрузка без `AttackPlan`. Её `ActiveAction.Plan` по умолчанию `AttackPlan.Melee`, поэтому любое действие разрешается как melee-сектор. Ввод героя её не использует: `HeroAttack` идёт через `AlphaAttackRoute`. Но `CreatureCombatDriver.TryBeginAction` (`Presentation/Gameplay/CreatureCombatDriver.cs:171`) вызывает именно её. Если ИИ существа выберет действие из групп снаряда, посоха или луча, оно будет разрешено как melee, а не по маршруту Alpha. Это вне объёма F2B (там ввод героя). Выбирают ли восстановленные существа такие действия — `UNKNOWN`. В интеграции не исправлялось: это gameplay-изменение. Кандидат в F3: передать `AlphaAttackRoute.ForAction(actionCode)`.
- `build_transfer_registry` — §4.

## 8. Готовность к M0-F3

M0-F3 открыт от принятого коммита. Замечание по маршрутам атак существ (§7) и ошибка реестра P-01 (§4) переданы в M0-F3.

## 9. Файлы этого шага

- Merge `23cea9a`: F1/F2A/DeepSeek/F2B, без ручных правок.
- `Assets/CubeGame/Tests/FullM0/PlayMode/FullM0F2IntegrationPlayModeTests.cs` и `.meta` (новые).
- `docs/reports/M0_F2_INTEGRATION_REPORT.md` (этот файл), `PROJECT_MEMORY.md`, `docs/ACTIVE_STAGE.md`.
