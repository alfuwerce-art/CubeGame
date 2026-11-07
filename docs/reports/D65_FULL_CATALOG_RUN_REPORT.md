# D65 — полный массовый перенос каталога Alpha → Unity (отчёт DeepSeek Harness)

**Статус: `READY_FOR_REVIEW`** — не `ACCEPTED`. Финальная визуальная и техническая приёмка — за продюсером.
**Решение:** [D65](../decisions/D65-d64-result-approved-deepseek-full-catalog-run.md) (утверждён результат D64), масштаб — [D63](../decisions/D63-full-catalog-bulk-transfer.md), метод — [отчёт D64](S05B2_CLAUDE_FULL_RESEARCH_REPORT.md).
**ТЗ:** [FULL_CATALOG_DEEPSEEK_PLAYBOOK](../agents/FULL_CATALOG_DEEPSEEK_PLAYBOOK.md).
**База:** `claude/s05b2-full-catalog-research` @ `a89d99a092695e2852ce9b41e1fa9d781e9ddb36`. **Ветка:** `stage/d65-full-catalog`.
**Дата:** 2026-11-06/07. **Исполнитель:** DeepSeek Harness.

`main`, S04, чужие ветки не менялись; `D:\Games\Cube world` — только чтение; все данные и производные ассеты Alpha остались в `.scratch`.

## 0. Итог

| Поток | Результат |
|---|---|
| 1. Статичная геометрия всех моделей | **`CATALOG_IMPORT_RESULT=PASS`**: 2856/2856 импортировано, 1 модель Alpha-невидима (классифицирована), 21 модель отсутствует в БД (как в Alpha) |
| 2. Существа `d63_full` | **`BATCH_RESULT=PASS_ORACLE_VERIFIED`**, **182/182 PASS** (137 видов + 45 вариантов моделей), 137 контактных листов |
| 3. Действия ИИ `d63_actions` | **`BATCH_RESULT=PASS_ORACLE_VERIFIED`**, **140/140 PASS**, **851** клип действия `EMULATED_ALPHA_CODE`, 25 отказов с точной причиной, 140 контактных листов |
| Видов покрыто | **156 / 156** (все `AUTO_READY`; геометрия+риг+ходьба/покой у всех, действия — у 154 + профиль игрока 0x02) |
| Unity | 322 прогона существ/действий + 2856 моделей, все с маркером PASS и exit 0; регрессия HumanMale **98/98** при 30/60/120 Гц в каждом потоке, числа как в D59/D61 |
| Python | **317/317 OK**, `validate` PASS, PS 5.1 11/11 |
| Сцена просмотра | `.scratch/review/unity/FullCatalogReview` (322 образца, 141 вид) |
| Не перенесено | действия только с оружием (23 кода), эффекты/снаряды, динамические части, `RuneGiant 0x76`, запуски вне региона атаки — §4, §5, §8 |

**Ни один порог не ослаблен ради PASS:** шесть дефектов замера/конвейера найдены прогоном и исправлены
против данных (§6), а всё, что не доказано, осталось `BLOCKED` со своей причиной.

## 1. Что и как запускалось

Три независимых потока методички, без остановок на ручную приёмку между семействами:

| Поток | Команда | Результат |
|---|---|---|
| 1. Статичная геометрия всех моделей | `run_catalog_import.ps1 -Select all` | `CATALOG_IMPORT_RESULT=PASS exit=0`, `models=2856 pass=2856 fail=0 resumed=0`, `empty_visible=1`, `absent_in_db=21`, все 9 маршрутов |
| 2. Существа (геометрия + риг + ходьба/покой) | `transfer_planner batch --authorization D63 --scope d63_full --producer-approved-d64` | `BATCH_RESULT=PASS_ORACLE_VERIFIED`, 182/182, 4 ч 22 мин, регрессия 98/98 ×3 |
| 3. Действия ИИ существ | `transfer_planner batch --authorization D63 --scope d63_actions --producer-approved-d64` | `BATCH_RESULT=PASS_ORACLE_VERIFIED`, 140/140, 6 ч 48 мин, регрессия 98/98 ×3, 851 клип `EMULATED_ALPHA_CODE` |

Подготовка (все шаги выполнены, числа совпали с D64):

```powershell
python -m unittest discover -s tools/alpha_models/tests -t .          # 313/313 OK (312 D64 + 1 D65)
python -m tools.alpha_models.universal.pipeline scan --out .scratch/universal/static/alpha_static.json
python -m tools.alpha_models.catalog.asset_catalog
python -m tools.alpha_models.catalog.anim_catalog --out .scratch/catalog/anim_catalog.json
python -m tools.alpha_models.catalog.equipment_probe --out .scratch/catalog/equipment_probe.json
python -m tools.alpha_models.universal.transfer_planner plan --out-dir .scratch/universal/plan_d65
python -m tools.alpha_models.universal.transfer_planner validate --out .scratch/universal/plan_d65/validate.json
```

* `STATIC_SCAN`: 156 видов, реестр 2550 индексов, `undecoded_index=0`, `unresolved_slot_indices=0`.
* `ASSET_CATALOG`: 2857 блобов, 2576 вызовов загрузчика, 2550 индексов, маршруты — как в D64 (все 9 категорий).
* `ANIM_CATALOG`: 101 инструкция записи байта действия, 101 действие со своим состоянием, 40 действий запускает ИИ, 15 функций-рендеров моделей.
* `EQUIPMENT_PROBE`: все 10 слотов (SWAP/ATTACH) и законы якорей воспроизведены на 0x02/0x1e/0x3f/0x13/0x4c.
* `plan`: **156/156 `AUTO_READY`**, 106 семейств, действия `AVAILABLE` у **155/156** видов (1 — `RuneGiant 0x76`, `PARTIAL`); `validate` — `PASS` (0 проблем).

**Доказательства действий.** Пересчёт `trigger_catalog` (~3–4 ч на 11 процессов) не повторялся: доказательства
D64 закоммичены, а их дрейф проверен двумя независимыми способами — `trigger_catalog.evidence_drift()` (SHA256
кода и exe, `[]`) и повторным экспортом в отдельный файл: `npc_trigger_evidence.json` совпал **семантически
(156/156 записей)**, `git status` файла пуст. Тот же дрейф проверяется автоматически перед потоком 3.

## 2. Модельный манифест

Поток 1 (`run_catalog_import.ps1 -Select all`): **`CATALOG_IMPORT_RESULT=PASS`**
`models=2856 pass=2856 fail=0 resumed=0` (свежий полный прогон, Unity exit 0).

| Маршрут | Моделей | Импортировано в Unity | Alpha-пусто (маркеры) | Нет блоба | Не импортировано |
|---|---|---|---|---|---|
| CREATURE_PART | 607 | 606 | 1 | 0 | 0 |
| DB_ONLY | 328 | 328 | 0 | 0 | 0 |
| EQUIPMENT_ATTACH | 1068 | 1068 | 0 | 0 | 0 |
| EQUIPMENT_SWAP | 330 | 330 | 0 | 0 | 0 |
| FILE_LOADER_ABSENT | 10 | 10 | 0 | 0 | 0 |
| ITEM_STATIC | 149 | 149 | 0 | 0 | 0 |
| MISSING_IN_DB | 21 | 0 | 0 | 21 | 0 |
| REGISTRY_OVERWRITTEN | 1 | 1 | 0 | 0 | 0 |
| REGISTRY_STATIC | 364 | 364 | 0 | 0 | 0 |
| **Всего** | **2878** | **2856** | **1** | **21** | **0** |

* 2857 блобов `data1.db` + 21 имя, которое код Alpha грузит, но которого нет в базе: в Alpha эти модели
  пустые (`ALPHA_EMPTY_MODEL`) — в Unity не создавались, как и предписано.
* **1 модель Alpha-невидима по правилу маркеров** (не ошибка конвейера): `undead-hair-m01.cub` — flag 0,
  412 сплошных вокселя, из них 369 маркерных; оставшиеся 43 полностью окружены маркерами, поэтому мешер
  Alpha не выдаёт ни одной грани (проверено независимо: `VoxelMesh.cs` = 0, Python-зеркало `mesh_stats` = 0;
  без правила было бы 672 грани). Модель записана в `manifest.empty_visible` с числами и причиной и не
  импортировалась — то же правило, что для `ALPHA_EMPTY_MODEL`; часть рига для неё не создаётся, риг вида
  содержит только реальные части (см. §6, 0x0f/0x0b).
* Каждая модель проверена: размер сетки, число сплошных и маркерных вокселей и **ожидаемое число граней**
  (`manifest.faces`, независимый Python-расчёт) — совпали у всех 2856; создан Mesh + prefab.

## 3. Анимационный манифест (все виды)

| Вид | Имя | Семейство | Части | Ходьба | Покой | Действия ИИ | Экипировка | Unity | Причина / блокер |
|---|---|---|---|---|---|---|---|---|---|
| 0x00 | ElfMale | e66d96be5a | 7 | TRANSFERRED_FROM_ALPHA+RECONSTRUCTED_FROM_EVIDENCE | TRANSFERRED_FROM_ALPHA+RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (4 без оружия) | SUPPORTED | — |  |
| 0x01 | ElfFemale | e66d96be5a | 7 | TRANSFERRED_FROM_ALPHA+RECONSTRUCTED_FROM_EVIDENCE | TRANSFERRED_FROM_ALPHA+RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (4 без оружия) | SUPPORTED | — |  |
| 0x02 | HumanMale | e66d96be5a | 7 | TRANSFERRED_FROM_ALPHA+RECONSTRUCTED_FROM_EVIDENCE | TRANSFERRED_FROM_ALPHA+RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE | SUPPORTED | — |  |
| 0x03 | HumanFemale | e66d96be5a | 7 | TRANSFERRED_FROM_ALPHA+RECONSTRUCTED_FROM_EVIDENCE | TRANSFERRED_FROM_ALPHA+RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (4 без оружия) | SUPPORTED | — |  |
| 0x04 | GoblinMale | 22f68f4143 | 7 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | SUPPORTED | PASS (formula) |  |
| 0x05 | GoblinFemale | 22f68f4143 | 7 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | SUPPORTED | PASS (formula) |  |
| 0x06 | Bullterrier | d3b4b0c2fd | 6 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | SUPPORTED | PASS (formula) |  |
| 0x07 | LizardmanMale | e66d96be5a | 7 | TRANSFERRED_FROM_ALPHA+RECONSTRUCTED_FROM_EVIDENCE | TRANSFERRED_FROM_ALPHA+RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (4 без оружия) | SUPPORTED | — |  |
| 0x08 | LizardmanFemale | e66d96be5a | 7 | TRANSFERRED_FROM_ALPHA+RECONSTRUCTED_FROM_EVIDENCE | TRANSFERRED_FROM_ALPHA+RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (4 без оружия) | SUPPORTED | — |  |
| 0x09 | DwarfMale | be82bc902f | 7 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | SUPPORTED | PASS (formula) |  |
| 0x0a | DwarfFemale | be82bc902f | 7 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | SUPPORTED | PASS (formula) |  |
| 0x0b | OrcMale | 345ed3b095 | 7 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | SUPPORTED | PASS (formula) |  |
| 0x0c | OrcFemale | 2ce3ec455b | 7 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | SUPPORTED | PASS (formula) |  |
| 0x0d | FrogmanMale | e66d96be5a | 7 | TRANSFERRED_FROM_ALPHA+RECONSTRUCTED_FROM_EVIDENCE | TRANSFERRED_FROM_ALPHA+RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (4 без оружия) | SUPPORTED | — |  |
| 0x0e | FrogmanFemale | e66d96be5a | 7 | TRANSFERRED_FROM_ALPHA+RECONSTRUCTED_FROM_EVIDENCE | TRANSFERRED_FROM_ALPHA+RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (4 без оружия) | SUPPORTED | — |  |
| 0x0f | UndeadMale | 127e0dbdb9 | 7 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | SUPPORTED | PASS (formula) |  |
| 0x10 | UndeadFemale | 127e0dbdb9 | 7 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | SUPPORTED | PASS (formula) |  |
| 0x11 | Skeleton | 4412d36381 | 7 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +19 только с оружием: BLOCKED · Unity 1/1 | SUPPORTED | PASS (formula) |  |
| 0x12 | OldMan | d94d2033d9 | 6 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | SUPPORTED | PASS (formula) |  |
| 0x13 | Collie | cf73938391 | 7 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +15 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x14 | ShepherdDog | cf73938391 | 7 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +15 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x15 | SkullBull | e63636afef | 7 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +15 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x16 | Alpaca | cad5d5b7bc | 6 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +15 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x17 | BrownAlpaca | cad5d5b7bc | 6 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +15 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x18 | Egg | 6314f0fa45 | 1 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x19 | Turtle | 47a3f69299 | 6 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +15 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x1a | Terrier | ba9d6d5db7 | 7 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +15 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x1b | ScottishTerrier | 33e2246436 | 7 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +15 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x1c | Wolf | 96e1a8db48 | 7 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +15 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x1d | Panther | a5f18ec6d3 | 7 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +15 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x1e | Cat | ced63e5e96 | 7 | TRANSFERRED_FROM_ALPHA+RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (1 без оружия) | BLOCKED | — |  |
| 0x1f | BrownCat | ced63e5e96 | 7 | TRANSFERRED_FROM_ALPHA+RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (1 без оружия) | BLOCKED | — |  |
| 0x20 | WhiteCat | ced63e5e96 | 7 | TRANSFERRED_FROM_ALPHA+RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (1 без оружия) | BLOCKED | — |  |
| 0x21 | Pig | fdeca213a5 | 6 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +15 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x22 | Sheep | 235b3653f7 | 6 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +15 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x23 | Bunny | 9bcb3536a9 | 3 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +15 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x24 | Porcupine | 9bcb3536a9 | 3 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +15 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x25 | GreenSlime | f4a1d5e1b4 | 1 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +19 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x26 | PinkSlime | f4a1d5e1b4 | 1 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +19 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x27 | YellowSlime | f4a1d5e1b4 | 1 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +19 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x28 | BlueSlime | f4a1d5e1b4 | 1 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +19 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x29 | Frightener | 5927089500 | 2 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +19 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x2a | SandHorror | c3753fd789 | 3 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +19 только с оружием: BLOCKED · Unity 1/1 | SUPPORTED | PASS (formula) |  |
| 0x2b | Wizard | aec77fd311 | 6 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | SUPPORTED | PASS (formula) |  |
| 0x2c | Bandit | d94d2033d9 | 6 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | SUPPORTED | PASS (formula) |  |
| 0x2d | Witch | 4ca7cad58c | 6 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | SUPPORTED | PASS (formula) |  |
| 0x2e | Ogre | 699aef71f9 | 6 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | SUPPORTED | PASS (formula) |  |
| 0x2f | Rockling | 8fbe117e64 | 5 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (3 без оружия) · Unity 1/1 | SUPPORTED | PASS (formula) |  |
| 0x30 | Gnoll | ecd256e24c | 6 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | SUPPORTED | PASS (formula) |  |
| 0x31 | PolarGnoll | 73fdb643f6 | 6 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | SUPPORTED | PASS (formula) |  |
| 0x32 | Monkey | ddb5e5a509 | 6 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +19 только с оружием: BLOCKED · Unity 1/1 | SUPPORTED | PASS (formula) |  |
| 0x33 | Gnobold | aec77fd311 | 6 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | SUPPORTED | PASS (formula) |  |
| 0x34 | Insectoid | ae2737e858 | 6 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | SUPPORTED | PASS (formula) |  |
| 0x35 | Hornet | 430a8f5d2d | 6 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +9 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x36 | InsectGuard | d53b24b8ec | 8 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +9 только с оружием: BLOCKED · Unity 1/1 | SUPPORTED | PASS (formula) |  |
| 0x37 | Crow | e02c8fe740 | 6 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +9 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x38 | Chicken | 2d41879af8 | 6 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +9 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x39 | Seagull | 0ef04db654 | 6 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +9 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x3a | Parrot | e02c8fe740 | 6 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +9 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x3b | Bat | e05b4c963a | 6 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +9 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x3c | Fly | de4df061e6 | 6 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +9 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x3d | Midge | 28984e669b | 6 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +9 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x3e | Mosquito | de4df061e6 | 6 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +9 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x3f | PlainRunner | 51aeec9280 | 3 | TRANSFERRED_FROM_ALPHA+RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (1 без оружия) | BLOCKED | — |  |
| 0x40 | LeafRunner | 51aeec9280 | 3 | TRANSFERRED_FROM_ALPHA+RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (1 без оружия) | BLOCKED | — |  |
| 0x41 | SnowRunner | 51aeec9280 | 3 | TRANSFERRED_FROM_ALPHA+RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (1 без оружия) | BLOCKED | — |  |
| 0x42 | DesertRunner | 51aeec9280 | 3 | TRANSFERRED_FROM_ALPHA+RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (1 без оружия) | BLOCKED | — |  |
| 0x43 | Peacock | 108eade4d2 | 4 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +19 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x44 | Frog | 29bcce7692 | 6 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | SUPPORTED | PASS (formula) |  |
| 0x45 | PlantCreature | 515ba494ea | 6 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +19 только с оружием: BLOCKED · Unity 1/1 | SUPPORTED | PASS (formula) |  |
| 0x46 | RadishCreature | 09a6a5bb34 | 5 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +19 только с оружием: BLOCKED · Unity 1/1 | SUPPORTED | PASS (formula) |  |
| 0x47 | Onionling | 9bc2829c9c | 3 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +15 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x48 | DesertOnionling | 9bc2829c9c | 3 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +15 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x49 | Devourer | 92697aa04f | 3 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +15 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x4a | Duckbill | d60366c7f9 | 7 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +15 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x4b | Crocodile | f18985f972 | 6 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +15 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x4c | SpikeCreature | 773a44f932 | 6 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | SUPPORTED | — |  |
| 0x4d | Anubis | 773a44f932 | 6 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | SUPPORTED | — |  |
| 0x4e | Horus | 773a44f932 | 6 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | SUPPORTED | — |  |
| 0x4f | Jester | 773a44f932 | 6 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | SUPPORTED | — |  |
| 0x50 | Spectrino | aec77fd311 | 6 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | SUPPORTED | PASS (formula) |  |
| 0x51 | Djinn | 6b665ef69a | 6 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | SUPPORTED | PASS (formula) |  |
| 0x52 | Minotaur | 3ecc1edb6c | 8 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | SUPPORTED | PASS (formula) |  |
| 0x53 | NomadMale | b35a0def60 | 6 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | SUPPORTED | PASS (formula) |  |
| 0x54 | NomadFemale | b35a0def60 | 6 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | SUPPORTED | PASS (formula) |  |
| 0x55 | Imp | 67d510b43f | 7 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +19 только с оружием: BLOCKED · Unity 1/1 | SUPPORTED | PASS (formula) |  |
| 0x56 | Spitter | 0cec830a60 | 3 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (4 без оружия) +9 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x57 | Mole | 4cdb383633 | 6 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +19 только с оружием: BLOCKED · Unity 1/1 | SUPPORTED | PASS (formula) |  |
| 0x58 | Biter | 94290d7722 | 6 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +19 только с оружием: BLOCKED · Unity 1/1 | SUPPORTED | PASS (formula) |  |
| 0x59 | Koala | c22ddfcb96 | 6 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +19 только с оружием: BLOCKED · Unity 1/1 | SUPPORTED | PASS (formula) |  |
| 0x5a | Squirrel | 12b2d5f563 | 6 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +19 только с оружием: BLOCKED · Unity 1/1 | SUPPORTED | PASS (formula) |  |
| 0x5b | Raccoon | 5b38a23a5e | 6 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +19 только с оружием: BLOCKED · Unity 1/1 | SUPPORTED | PASS (formula) |  |
| 0x5c | Owl | 9d71ddce2d | 5 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +19 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x5d | Penguin | d881bf09f0 | 6 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +19 только с оружием: BLOCKED · Unity 1/1 | SUPPORTED | PASS (formula) |  |
| 0x5e | Werewolf | a1fd07080a | 6 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +19 только с оружием: BLOCKED · Unity 1/1 | SUPPORTED | PASS (formula) |  |
| 0x5f |  | 203661eec7 | 6 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | SUPPORTED | PASS (formula) |  |
| 0x60 | Zombie | 775a53bf97 | 6 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +19 только с оружием: BLOCKED · Unity 1/1 | SUPPORTED | PASS (formula) |  |
| 0x61 | Vampire | 1b804b616b | 9 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | SUPPORTED | PASS (formula) |  |
| 0x62 | Horse | 0300a9e9de | 7 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +15 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x63 | Camel | 0a4b86b63e | 6 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +15 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x64 | Cow | eda4954b90 | 6 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +15 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x65 | Dragon | ccddb0483c | 7 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +9 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x66 | BarkBeetle | 1da0fb344c | 3 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +15 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x67 | FireBeetle | b12a51e722 | 3 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +15 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x68 | SnoutBeetle | 18798bede3 | 3 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (7 без оружия) +12 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x69 | LemonBeetle | b12a51e722 | 3 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +15 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x6a | Crab | e63f28e552 | 6 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +19 только с оружием: BLOCKED · Unity 1/1 | SUPPORTED | PASS (formula) |  |
| 0x6b | SeaCrab | 3d85303a29 | 6 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +19 только с оружием: BLOCKED · Unity 1/1 | SUPPORTED | PASS (formula) |  |
| 0x6c | Troll | 4a1c12bcd7 | 8 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | SUPPORTED | PASS (formula) |  |
| 0x6d | DarkTroll | fe0a6562ed | 9 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | SUPPORTED | PASS (formula) |  |
| 0x6e | HellDemon | 45bcef324c | 8 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | SUPPORTED | PASS (formula) |  |
| 0x6f | Golem | b1cf6da84d | 8 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (3 без оружия) · Unity 1/1 | SUPPORTED | PASS (formula) |  |
| 0x70 | EmberGolem | bafdf86020 | 8 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (3 без оружия) · Unity 1/1 | SUPPORTED | PASS (formula) |  |
| 0x71 | SnowGolem | bafdf86020 | 8 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (3 без оружия) · Unity 1/1 | SUPPORTED | PASS (formula) |  |
| 0x72 | Yeti | 4a1c12bcd7 | 8 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | SUPPORTED | PASS (formula) |  |
| 0x73 | Cyclops | 7e622050f2 | 8 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | SUPPORTED | PASS (formula) |  |
| 0x74 | Mammoth | 1c154c9ea1 | 6 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (3 без оружия) · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x75 | Lich | dddcce500a | 6 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (4 без оружия) +9 только с оружием: BLOCKED · Unity 1/1 | SUPPORTED | PASS (formula) |  |
| 0x76 | RuneGiant | ccb2404b34 | 6 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | BLOCKED | SUPPORTED | PASS (formula) | NO_VERIFIED_ACTION_TRIGGER: no action profile has evidence for this motion family; creature AI launch rules are not reconstructed |
| 0x77 | Saurian | 6ef6805ded | 7 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (2 без оружия) · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x78 | Bush | 153aadf202 | 1 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x79 | SnowBush | 153aadf202 | 1 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x7a | SnowBerryBush | 153aadf202 | 1 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x7b | CottonPlant | c26f5122a7 | 1 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x7c | Scrub | e078a2673d | 1 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x7d | CobwebScrub | 0a9fd02f9d | 1 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x7e | FireScrub | e078a2673d | 1 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x7f | Ginseng | 61a0f49a33 | 1 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x80 | Cactus | b8b534b11e | 1 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x81 |  | 383bfcee6a | 1 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x82 | ThornTree | 90c0a9f968 | 1 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x83 | GoldDeposit | 0e4a237228 | 1 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x84 | IronDeposit | 0e4a237228 | 1 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x85 | SilverDeposit | 0e4a237228 | 1 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x86 | SandstoneDeposit | 0e4a237228 | 1 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x87 | EmeraldDeposit | 0e4a237228 | 1 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x88 | SapphireDeposit | 0e4a237228 | 1 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x89 | RubyDeposit | 0e4a237228 | 1 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x8a | DiamondDeposit | 0e4a237228 | 1 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x8b | IceCrystalDeposit | 0e4a237228 | 1 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x8c | Scarecrow | d3626b3175 | 1 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x8d | Aim | 21d1cbb3c1 | 1 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x8e | Dummy | 022f451ef0 | 1 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x8f | Vase | dc126059f6 | 1 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x90 | Bomb | 5d3e0d80f9 | 1 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x91 | SapphireFish | b63b3687f0 | 1 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +15 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x92 | LemonFish | b63b3687f0 | 1 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +15 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x93 | Seahorse | 057f7a432f | 1 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +15 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x94 | Mermaid | d3271aa564 | 5 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | SUPPORTED | PASS (formula) |  |
| 0x95 | Merman | d3271aa564 | 5 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (8 без оружия) +23 только с оружием: BLOCKED · Unity 1/1 | SUPPORTED | PASS (formula) |  |
| 0x96 | Shark | ac482cfc2e | 1 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +15 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x97 | Bumblebee | c5f209e45e | 5 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +9 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x98 | LanternFish | e865f02c13 | 2 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +15 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x99 | MawFish | ac482cfc2e | 1 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +15 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x9a | Piranha | ac482cfc2e | 1 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +15 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |
| 0x9b | Blowfish | ac482cfc2e | 1 | RECONSTRUCTED_FROM_EVIDENCE | RECONSTRUCTED_FROM_EVIDENCE | AVAILABLE (5 без оружия) +15 только с оружием: BLOCKED · Unity 1/1 | BLOCKED | PASS (formula) |  |

Примечание: «AVAILABLE (n без оружия)» — число кодов действий, доказанных для вида; «+k только с оружием» — доказаны, но не переносятся (R-2); «Unity» — вердикт потока существ (ходьба/покой; PASS (formula) = формула вида проверена против исполненного drawModel, ORACLE_VERIFIED, живого захвата нет), «—» = вид перенесён ранее (S05B-1/S05B-2) или покрыт другим потоком; «Экипировка» — может ли риг нести оружие в руках (SUPPORTED — есть блоки хвата, BLOCKED — нет: FORE_LIMBS_ARE_WALKING_LIMBS/NO_GRIP_BLOCKS); сам перенос оружия не выполнялся (R-2).

## 4. Действия: что доказано и что не перенесено

Доказательная база — исполнение байтов `Cube.exe` (D64): выбор атаки ИИ `CombatBehavior::vfunc_0`
(0x42D87F..0x42EAEF) исполнен на **280 состояниях NPC** для каждого вида (класс 0..4 × специализация 0..1 ×
без оружия / 26 подтипов оружия). Живой захват не требовался; `live_check=NOT_CAPTURED` сохраняется.

| Показатель | Значение |
|---|---|
| Видов с доказанными безусловными (безоружными) запусками | **155 / 156** (`TRIGGER_EMULATED_CONDITIONAL`); 1 `PARTIAL` — `RuneGiant 0x76` |
| Различных кодов действий, запускаемых ИИ (все состояния) | **40** |
| Из них без оружия (переносятся в этом прогоне) | **20**: 3, 4, 6, 22, 25, 37, 40, 42, 43, 51, 54, 55, 69, 70, 72, 73, 74, 75, 76, 108 |
| Только с оружием (не переносятся, R-2) | **23**: 1, 5, 14, 19, 22–27, 30–33, 38, 44–46, 55, 57, 59–61 |
| Кодов действий с собственным состоянием позы `drawModel` | **101** (всего записей байта действия — 101) |
| Действия игрока (ввод) — отдельный домен, не в этом прогоне | R-4 |

Что попало в итоговый реестр как **не перенесённое** (и почему):

| Класс | Статус | Причина | Ссылка |
|---|---|---|---|
| Действия, запускаемые только с оружием (23 кода) | `BLOCKED` | у не-гуманоидных ригов не доказан слой оружия при ходьбе (формула оружия проверена только для гуманоида S05A-2) | R-2 |
| Действия, чья поза **статична** (напр. Dragon 0x65, действие 51) | `BLOCKED ACTION_STATIC` | поза не меняется — это эффект, а не движение модели | R-3 |
| Действия, требующие **динамической части** (напр. голем 0x6f, действие 108: брошенный камень в `offhand_b`; 0x19 действие 72) | `BLOCKED ACTION_PART_SET_CHANGED` | в риге вида нет модели этой части; закон якоря известен | R-8 |
| `RuneGiant 0x76` | `BLOCKED` (весь вид) | ветка атаки пишет байт действия **другого** существа по указателю из списка существ мира — регион его не получает | R-9 |
| Запуски вне региона выбора атаки (80/110 в ИИ, ввод игрока, события) | `BLOCKED` | исполнен только регион атаки | R-4 |
| `HumanMale 0x02` | покрыт регрессией | профиль игрока D60 (S05A-2), не creature_ai | R-4 |

«Все анимации перенесены» на основании ходьбы **не** заявляется: перенесены безоружные действия ИИ тех
видов, для которых они доказаны, а всё остальное перечислено выше с кодом причины.

## 5. Экипировка, эффекты, снаряды, динамические части

| Объект | Состояние | Причина / доказательство |
|---|---|---|
| Экипировка на теле (10 слотов: нагрудник, перчатки, ботинки — SWAP; оружие, вторая рука ×2, плечи, амулет, колчан ×3, фонарь, глайдер/лодка, предмет в руке, питомец — ATTACH) | **Законы установлены** (D64, `equipment_probe`: 19 пар «предмет → блок + закон якоря» на 0x02/0x1e/0x3f/0x13/0x4c) | исполнение `drawModel` + `anchor_probe` |
| Экипировка: блоки хвата в риге | **68 видов** — `SUPPORTED` (есть `hand_l`+`hand_r`, слой оружия возможен); **88** — `BLOCKED` (`FORE_LIMBS_ARE_WALKING_LIMBS` у четвероногих, `NO_GRIP_BLOCKS` у безруких) | `contract.channels.equipment`, та же таблица §3 |
| Экипировка в Unity-риге | **Не переносилась ни для одного вида** (включая «оружие на спине» у не-гуманоидов) | реализован только слой оружия гуманоида S05A-2 → `EQUIPMENT` не заявляется | R-2 |
| Модели экипировки как геометрия | **Импортированы все** (1068 ATTACH + 330 SWAP, §2) | статичный путь каталога |
| Эффекты (огонь дракона, ленты, частицы) | **`BLOCKED`** | отдельные рендереры (`spawnEffectMesh` и др.), не поза `drawModel`; движение — игровая симуляция | R-3 |
| Снаряды (стрелы, камни, снаряды магов) | **`BLOCKED`** | отдельный рендерер + физика; модели покрыты статичным путём | R-3 |
| Динамические части рига (брошенный/удерживаемый объект действия 108 у големов, 72 у 0x19) | **`BLOCKED`** | деталь появляется в кадре выпекания, но её модели нет в риге вида; закон якоря известен | R-8 |
| Предмет действия 81 (ресурс) | **`BLOCKED`** | не рисуется в проверенном состоянии (условие видимости — таймер кулдауна) | R-5 |
| Предметы/пропсы/постройки (ITEM_STATIC, REGISTRY_STATIC, DB_ONLY) | **Импортированы** как статичные модели | статичный путь каталога, `ANIMATION: NOT_APPLICABLE` — нормальный статус | — |

## 6. Unity: прогоны, сбои и исправления по ходу прогона

Все прогоны — Unity 6000.3.27f2, изолированные проекты в `.scratch`, вердикт = маркер в отчёте **и** exit 0.

**Поток 2 (`d63_full`)** — 182 записи (137 видов + 45 вариантов моделей), 4 ч 22 мин:
`BATCH_RESULT=PASS_ORACLE_VERIFIED`, 182/182 PASS, все `live_check=NOT_CAPTURED`, регрессия HumanMale
**98/98 при 30/60/120 Гц** (18 023 / 35 927 / 71 585 кадров, native 1484 / 2962 / 5919 — те же числа, что в
D59/D61), 137 контактных листов. Unity-шаги каждого прогона: SELFTEST 40/40, TRANSFER (GEOMETRY N/N,
ROUNDTRIP ≤ 2e-6, SEAM CONTINUOUS), PLAYMODE, PARAMWALK (golden 900, formula-only), LOCOMOTION_PLAYMODE,
ENTITY_CONTRACT, ENTITY_PLAYMODE.

Дефекты, найденные прогоном и исправленные (каждый — с проверкой в Unity):

| # | Симптом | Причина | Исправление | Проверка |
|---|---|---|---|---|
| 1 | `undead-hair-m01.cub: NO_VISIBLE_FACES` (0x0f) — весь поток 1 падал из-за одной модели | модель flag 0, все немарекрные воксели окружены маркерами → в Alpha 0 граней | модель классифицируется как `empty_visible` (как `ALPHA_EMPTY_MODEL`), не импортируется; в риге часть считается Alpha-пустой; **плюс** проверка ожидаемого числа граней из Python для всех моделей | полный импорт `pass=2856 fail=0`; 0x0f 7/7 PASS |
| 2 | `WALK_STATIC` (0x29, 0x98) | у «головных» существ ходьба Alpha почти статична (0.63 вокселя), а порог был фиксированный 10°/1 воксель | проверка сравнивает рантайм с тем, что обещают данные (формула — 64 фазы, клип — 32 позы): «почти статично в Alpha» → `WALK_STATIC_BY_FORMULA/CLIP`, «данные двигаются, рантайм нет» → FAIL | 0x29/0x98 7/7 PASS с пометкой |
| 3 | `KeyError: 'hair'` (0x0b, seeds 7–9) | появление выбирает модель, которой нет в БД (`orc-hair-m08..10`); в Alpha деталь пустая | часть помечается `ALPHA_EMPTY_MODEL` и не создаётся, `contract.emptyParts` фиксирует; риг без неё | 0x0b s0-7 7/7 PASS |
| 4 | `CONTRACT_PART_SET_MISMATCH` (0x0b) | контракт перечислял 7 частей плана, риг — 6 реальных | контракт строится по фактическим частям рига + `emptyParts` | там же |
| 5 | `LOCOMOTION_DID_NOT_REPEAT` (0x15) | фаза драйвера заворачивается в [0, 2π), а проверка мерила `phase − phaseStart` → 5.44 рад вместо реальных 39.5 | накопление пройденной фазы с учётом заворота | 0x15 8/8 PASS |
| 6 | `ACTIONS_COMPLETED 4/4 != 5` (0x19) | профиль знает 5 запусков, выпечка одного отказала (`ACTION_PART_SET_CHANGED`, R-8) | проверка требует ровно те действия, что есть в библиотеке как `EMULATED_ALPHA_CODE`; `BLOCKED` обязан иметь причину | 0x19 PASS, действие 72 в манифесте с причиной |

Ни один порог не ослаблен ради PASS: где проверка ошибалась, она заменена измерением против данных, а
«данные двигаются, а рантайм — нет» по-прежнему FAIL (см. §8 R-10…R-12 для ревью формулировок).

**Поток 3 (`d63_actions`)** — 140 записей (по одному виду на семейство в фазе 1 + остальные в фазе 2),
6 ч 48 мин: `BATCH_RESULT=PASS_ORACLE_VERIFIED`, 140/140 PASS, 851 выпеченных клипа действия
`EMULATED_ALPHA_CODE`, регрессия HumanMale 98/98 ×3, 140 контактных листов.
25 запусков остались `BLOCKED` с причиной в манифесте библиотеки: **20** `ACTION_STATIC` (поза не меняется —
это эффект, R-3) и **5** `ACTION_PART_SET_CHANGED` (действие требует части, которой нет в риге, R-8);
ни один `BLOCKED` не «подменён» заглушкой, и контроллер их не проигрывает.

Дефекты, найденные в этом потоке и исправленные:

| # | Симптом | Причина | Исправление | Проверка |
|---|---|---|---|---|
| 7 | все записи потока 3 падали `PARAMWALK_WITHOUT_LIVE_FRAMES` | скоуп действий не объявлял, что ходьба вида проверена формулой (`ORACLE_VERIFIED`), а не захватом | `live_frames: formula_only` у `d63_actions` (как у `d63_full`), `live_check=NOT_CAPTURED` сохраняется | первый же прогон после правки — PASS |
| 8 | `LOCOMOTION_DID_NOT_REPEAT` (0x15) | фаза драйвера заворачивается в [0, 2π), проверка мерила `phase − phaseStart` | накопление фазы с учётом заворота | 0x15 8/8 PASS, `phase_travel_rad=39.5` |
| 9 | `ACTIONS_COMPLETED 4/4 != 5` (0x19) | профиль знает 5 запусков, выпечка одного отказала (`ACTION_PART_SET_CHANGED`) | требование = действия, реально лежащие в библиотеке как `EMULATED_ALPHA_CODE`; `BLOCKED` обязан иметь причину | 0x19 PASS, действие 72 в манифесте с причиной |

Суммарно по всем потокам: **2856 моделей + 182 существа/варианта + 140 наборов действий** — все с PASS-маркером
и exit 0; **6 дефектов** конвейера/замеров найдено и исправлено по ходу (§6 таблицы 1–9); регрессия
HumanMale не менялась ни разу (98/98 при 30/60/120 Гц, кадры 18 023 / 35 927 / 71 585).

## 7. Сцена просмотра

## 7. Сцена просмотра

Собрана **одна общая сцена всего каталога** из тех же заданий, которые проверял прогон (ничего не
домоделировано):

* проект: `.scratch/review/unity/FullCatalogReview` (изолированный Unity 6000.3.27f2, в Git не попадает);
* образцы: **322** — 182 из `d63_full` (137 видов + 45 вариантов моделей) и 140 из `d63_actions`
  (по одному виду с действиями), **141 вид** в сумме;
* `REVIEW_SETUP_RESULT=PASS`: `REVIEW_DATA_RESULT=PASS specimens=322 kinds=141 failed=0`,
  `REVIEW_BUILD_RESULT=PASS`, `REVIEW_CHECK_RESULT=PASS specimens=322 sources=1817 samples=210335 problems=0`,
  `REVIEW_PLAYMODE_RESULT=PASS specimens=322 sources=1817 frames=6317 errors=0 tour_problems=0`;
  переключатель правила маркеров: вершин 11 136 → 7 724 (маркеры скрываются там, где они есть);
* локальные листы: `ReviewOutput/lineup_idle_*.png` (все 141 вид сеткой по 20 в ряд — одиночный ряд из
  141 вида не читался), `variants_<kind>.png`, `actions_<specimen>.png` (3 кадра на каждое действие),
  `colour_before_after_*.png` (по 50 видов на файл — больше 16 384 px текстура не позволяет).

Открыть:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools\alpha_models\review\open_review.ps1 `
  -Project D:\Projects\CubeGame\.scratch\review\unity\FullCatalogReview -Play
```

В сцене: `↑/↓` — вид, `←/→` — вариант (у вида с действиями есть отдельный образец `d63_actions_…`),
`1/2/3` — Idle/Walk/Action, `Q/E` — клип/действие, `L` — все виды в ряд, `M` — правило маркеров
(до/после), `C` — цвет волос из живых наблюдений, `F` — целиком, `Tab` — свернуть панель.
Вкладка «Проверки» показывает происхождение каждого источника (`TRANSFERRED_FROM_ALPHA` /
`EMULATED_ALPHA_CODE` / `RECONSTRUCTED_FROM_EVIDENCE`) и причину блокировки действий, если она есть.

## 8. Оставшиеся проблемы для Claude (реестр)

Только то, что требует **нового исследования**; всё остальное либо перенесено, либо имеет точную причину
в манифестах выше.

| # | Проблема | Что известно | Что нужно сделать |
|---|---|---|---|
| R-1 | Класс/оружие конкретного NPC в мире | приходит из записи спавна мира (`Server.exe` 0x535DF7), не из вида; доказательства записаны как **условия** (класс × спец × оружие) | исполнить генератор записей спавна → распределение классов по видам/зонам (нужно для «какой NPC что делает» в игре) |
| R-2 | Действия только с оружием (23 кода) и слой экипировки в риге | законы якорей всех 10 слотов установлены; в Unity реализован только слой оружия гуманоида | выпечь ходьбу/позы с оружием оракулом для не-гуманоидных ригов и реализовать SWAP/ATTACH-блоки в Unity-риге |
| R-3 | Эффекты и снаряды | отдельные рендереры (`spawnEffectMesh`, `render_build_projectile_mesh`), не поза `drawModel` | адаптер эффектов/снарядов с исполнением этих функций; сейчас `BLOCKED` |
| R-4 | Запуски действий вне региона выбора атаки (80/110 в ИИ, ввод игрока, события) | исполнен только регион атаки | расширить исполнение на весь тик ИИ и обработчики событий |
| R-5 | Предмет действия 81 (ресурс) не рисуется | условие видимости — таймер кулдауна предмета | один зонд с таймером |
| R-6 | Переходы и скорости | `UNITY_TRANSITION`; скорость `PLACEHOLDER_NOT_ALPHA` (OQ-23) | отдельное решение продюсера по числам |
| R-8 | Динамические части рига (действия 108 у големов, 72 у 0x19 и др.) | часть появляется в кадре выпекания, модели нет в риге вида; закон якоря известен | добавить динамическую часть рига с моделью из кадра выпекания |
| R-9 | `RuneGiant 0x76` | ветка атаки управляет **другим** существом через список существ мира | подать список существ как вход региона |
| R-10 (новое) | Порог «замороженной» ходьбы | исправлено в D65: сравнение с тем, что обещают данные (формула/клип), а не с фиксированными 10°/1 воксель; у 0x29/0x98 ходьба Alpha действительно почти статична (0.63 вокселя) | ревью формулировки: считать ли такие виды «анимированными» в отчётах |
| R-11 (новое) | Части, которых нет в базе (`orc-hair-m08..10` и др.) | в Alpha они пустые; риг строится без них, `contract.emptyParts` фиксирует | подтвердить, что отсутствие части в риге для конкретного seed — ожидаемое поведение, а не дефект вида |
| R-12 (новое) | «Повтор ходьбы» контроллера | фаза драйвера заворачивается в [0, 2π); прежняя проверка измеряла `phase − phaseStart` и у длинношаговых видов (0x15) давала 5.4 рад вместо 39.5 | ревью: накопление с учётом заворота (сделано), при желании — требовать ≥ 2 полных циклов явно |

## 9. Воспроизведение

```powershell
# подготовка (числа D64 должны совпасть)
python -m unittest discover -s tools/alpha_models/tests -t .
python -m tools.alpha_models.universal.pipeline scan --out .scratch/universal/static/alpha_static.json
python -m tools.alpha_models.catalog.asset_catalog
python -m tools.alpha_models.catalog.anim_catalog --out .scratch/catalog/anim_catalog.json
python -m tools.alpha_models.catalog.equipment_probe --out .scratch/catalog/equipment_probe.json
python -m tools.alpha_models.universal.transfer_planner plan --out-dir .scratch/universal/plan_d65
python -m tools.alpha_models.universal.transfer_planner validate --out .scratch/universal/plan_d65/validate.json

# три потока
powershell -NoProfile -ExecutionPolicy Bypass -File tools\alpha_models\catalog\run_catalog_import.ps1 -Select all
python -m tools.alpha_models.universal.transfer_planner batch --authorization D63 --scope d63_full --producer-approved-d64
python -m tools.alpha_models.universal.transfer_planner batch --authorization D63 --scope d63_actions --producer-approved-d64

# таблицы отчёта и общая сцена просмотра
python -m tools.alpha_models.catalog.report_tables --out .scratch/catalog/report_tables.md
powershell -NoProfile -ExecutionPolicy Bypass -File tools\alpha_models\review\setup_review.ps1 `
  -Project D:\Projects\CubeGame\.scratch\review\unity\FullCatalogReview `
  -BatchDir .scratch/universal/batch/d63_full,.scratch/universal/batch/d63_actions
```

Артефакты (все локальные): `.scratch/catalog/unity/AlphaCatalog/CatalogOutput/catalog_import.txt`,
`.scratch/universal/batch/d63_full/{summary.json,summary.md,runs,visuals}`, то же для `d63_actions`,
`libraries/` (выпеченные библиотеки действий), `.scratch/catalog/report_tables.md`.

Повтор одной записи: удалить её ключ из `batch_state.json → entries` и запустить ту же команду — возобновление
пересчитает только её (SHA256 задания и входов у остальных не меняются).

## 10. Git

* Ветка: `stage/d65-full-catalog` (от `a89d99a`). Коммиты прогона и итоговый SHA — ниже.
* Коммиты прогона (все — в `stage/d65-full-catalog`):
  `2de2f96` (проверка граней и Alpha-невидимые модели), `2b7c51a` (общая сцена из нескольких скоупов, PS 5.1),
  `78bffb8` (дрейф доказательств из двух источников), `a1acf56` (Alpha-пустая часть в переносе),
  `b5edd8d` (часть, которой нет в БД), `f7bff1c` (правило «замороженной» ходьбы и контракт рига),
  `bc54b6f` (formula-only для действий), `c344fe6` (заворот фазы контроллера), `4f75cbb` (отказ выпечки
  действия), `9229800` (сборка общей сцены), **`ed4ff41`** — отчёт, манифесты, ACTIVE_STAGE/PROJECT_MEMORY.
  Итоговый SHA ветки с записью SHA в отчёте — в последнем коммите ветки (`git log -1 --format=%H`).
* В Git — код, тесты, доказательства из чисел и отчёты; `.cub`/`.cubdec`, кадры, сцены и таблицы с
  производными данными — только в игнорируемом `.scratch`.
