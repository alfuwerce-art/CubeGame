# S05B-2 / D69 — каталог оригинала → аудит Unity → исправленный перенос (Claude Code)

**Статус: `READY_FOR_REVIEW`** — не `ACCEPTED`; визуальная и техническая приёмка — лично продюсер.
**Решение:** [D69](../decisions/D69-s05b2-audit-and-transfer-implementation.md), ТЗ [S05B2_LIVE_ORIGINAL_VISUAL_AUDIT_TASK](../stages/S05B2_LIVE_ORIGINAL_VISUAL_AUDIT_TASK.md), issue #3.
**Ветка:** `claude/s05b2-d69-alpha-transfer-implementation` (база D65 + документы D66–D69). **Дата:** 2026-11-08.
`main`, S04, чужие ветки, `D:\Games\Cube world` не менялись; данные и производные Alpha — только в `.scratch`.

## 0. Итог

| Шаг | Результат |
|---|---|
| А. Каталог оригинала | [S05B2_ALPHA_ORIGINAL_MODEL_ANIMATION_CATALOG](../research/alpha2013/S05B2_ALPHA_ORIGINAL_MODEL_ANIMATION_CATALOG.md): 156 видов (130 существ, 26 пассивных объектов), модели, анимации и роль каждого по коду Alpha; живая перепись — 6 видов |
| Б. Аудит Unity | [матрица](../research/alpha2013/S05B2_UNITY_TRANSFER_DISPOSITION_MATRIX.md) на каждую модель/ходьбу/покой/действие + [аудит](S05B2_UNITY_TRANSFER_GAP_AUDIT.md) |
| В. Реализация | 3 исправления + 1 перенос недостающего (§2), проверены в Unity |
| Unity | `D69_VERIFY_RESULT=PASS` **29/29** (Dragon, 26 объектов, 2 контрольных существа) + 14/14 видов с добавленными действиями (56 новых клипов; проиграны все: 8/8 у гуманоидов, 5/5 у котов и бегунов); регрессия HumanMale **98/98**, кадры 35927 / нативные 2962 — те же числа, что в D59/D61/D65 |
| Python | Ran 332, OK (0 пропусков) |

## 1. Как получен эталон (шаг А) и ограничение по живой игре

* Запущена локальная копия Alpha (`.scratch/alphatest2`), вход в мир (город Арулон). Новый инструмент
  `runtime/creature_census.py` ставит точку останова на входе `Creature_drawModel` (`0x412A42`) и записывает
  каждое существо, которое рисует игра: вид `+0x64`, индексы моделей `+0x8c..+0x9a`, байт действия, позицию.
  Результат: 9 существ, 6 видов (0x02, 0x03, 0x1a, 0x1e, 0x21, 0x33); индексы моделей совпали с нашим разрешением внешности.
* **По указанию продюсера игра больше не запускалась.** Поэтому остальной каталог построен по исполненному коду и
  данным Alpha (`ALPHA_CODE` / `ALPHA_ASSET`) и прежним живым захватам поз; всё прочее помечено `LIVE_UNOBSERVED`
  (это не «отсутствует»).
* Новые оригинальные факты (§2 аудита): пассивная фракция объектов в `Server.exe`; бескрылый дракон.

## 2. Требование → механизм → изменение кода → тест → итог

| Требование оригинала | Механизм переноса | Изменение | Тест | Итог |
|---|---|---|---|---|
| Голова 0x65 крепится в `(sx/2, sy·0.2, sz/2)` | якорь меша в Unity = якорь, с которым раскладывались клипы | `contract.unity_anchor(rule, kind)` → `head_0x65`; `pipeline.py`; `AlphaRig.Anchor`; список правил `AlphaTransferEditor` | `test_mesh_anchor_equals_clip_anchor_for_every_kind` (все виды), `DragonEndToEndTests` (итоговые матрицы = исполненный `drawModel`; на старом правиле падает: голова 0.58 ед.); Unity 0x65 PASS со всеми 4 действиями | **FIXED** |
| Пассивные объекты не ходят и не действуют (`0x5360AE`, `0x53E2A8`) | игровая политика сущности `STATIC_OBJECT` из доказательств спавна | `catalog/spawn_roles.py` + `spawn_role_evidence.json` (байты по адресам); канал `game_usage` и режим в `contract.py`; `pipeline.py` не импортирует клипы действий в игровую сущность; `AlphaEntityContract`/`AlphaAnimationController` (стойка, отказ движения и действий); `AlphaEntityProbe` (проверка: запрос ходьбы отклонён, поза неподвижна); `batch_runner.judge_entry`; подпись в просмотрщике | `SpawnRoleEvidenceTests`, `StaticObjectContractTests`; Unity 26/26 PASS (`static_object=True move_refused=True`, действия отклонены) | **DISABLED_IN_GAME**, клипы исследования сохранены |
| Действия 37/40/42/43 запускаются ИИ у 14 видов | профили D60 + условные запуски D64 того же семейства | `contract._merge_d64_into_d60` (`D69_MERGED_LAUNCHES`, условия класс/спец сохраняются) | `D69MergedLaunches`; обновлены 2 теста D60 (часть D60 не изменилась); Unity 14/14 видов с добавленными действиями (56 новых клипов; проиграны все: 8/8 у гуманоидов, 5/5 у котов и бегунов) | **TRANSFERRED** (`EMULATED_ALPHA_CODE`; игровой триггер — `DEFERRED_TO_M0`) |
| Дракон бескрылый | — | ничего не добавлено (крылья противоречили бы оригиналу) | скан `Cube.exe`: индекс 428 только в загрузчике | KEPT |

## 3. Что не сделано (честно)

| Пункт | Итог | Причина / следующий шаг |
|---|---|---|
| Эффекты (огонь дракона 51 и др.), снаряды — R-3 | `BLOCKED` | отдельные рендереры Alpha; в M0-05C — заглушки, затем перенос |
| Брошенный камень 108 (големы, рокилинг), черепаха 72 — R-8 | `BLOCKED` | нужна скрытая по умолчанию динамическая часть рига у существ: изменение рига, проигрывателя клипов и контракта ходьбы; закон якоря и модель известны |
| Действия NPC только с оружием (23 кода) — R-1/R-2 | `DEFERRED_TO_M0` | оружие NPC из записи спавна мира (M0-06B) |
| HumanMale 0x02 как NPC | `DEFERRED_TO_M0` | профиль ИИ рядом с профилем игрока того же вида — решение контроллера M0-06 |
| RuneGiant 0x76 — R-9 | `BLOCKED` | ИИ управляет другим существом |
| Скорости (OQ-23), переходы | без изменений | `PLACEHOLDER_NOT_ALPHA` / `UNITY_TRANSITION` |
| Сцена просмотра FullCatalogReview | не пересобрана | она открыта у продюсера в редакторе; код подписи готов; пересборка: `tools/alpha_models/review/setup_review.ps1` после закрытия редактора |

## 4. Проверки (фактически выполнены)

```text
python -m unittest discover -s tools/alpha_models/tests -t .     Ran 332, OK (0 пропусков)
python -m tools.alpha_models.catalog.spawn_roles                 SPAWN_ROLE_RESULT=PASS passive_kinds=26
python -m tools.alpha_models.universal.d69_verify                D69_VERIFY_RESULT=PASS entries=29 pass=29;
                                                                  regression_0x02_60hz PASS frames=35927 actions=98/98 native=2962
d69_verify (14 видов, объединённые профили)                       14/14 видов с добавленными действиями (56 новых клипов; проиграны все: 8/8 у гуманоидов, 5/5 у котов и бегунов)
python -m tools.alpha_models.catalog.d69_catalogs                 оба каталога + матрица
```
Визуальные до/после (локально, не в Git): `.scratch/d69/visual/dragon_before_*.png`, `dragon_after_*.png`;
кадры всех прогонов — `.scratch/d69/verify/runs/*/entity_frames`.

## 5. Изменённый и новый код

`catalog/spawn_roles.py`, `catalog/spawn_role_evidence.json`, `catalog/d69_catalogs.py`, `runtime/creature_census.py`,
`universal/d69_verify.py` (новые); `universal/contract.py`, `universal/pipeline.py`, `universal/batch_runner.py`,
`transfer/unity/Runtime/{AlphaRig,AlphaEntityContract,AlphaAnimationController,AlphaEntityProbe}.cs`,
`transfer/unity/Editor/AlphaTransferEditor.cs`, `review/unity/Runtime/ReviewSpecimen.cs`; тесты
`tests/test_d69_transfer_fixes.py` (новый), `tests/test_npc_actions.py`.

Следующий этап проекта — [ТЗ M0-00](../stages/M0_00_FULL_ALPHA_GAME_TZ.md) (DRAFT, на утверждение).
