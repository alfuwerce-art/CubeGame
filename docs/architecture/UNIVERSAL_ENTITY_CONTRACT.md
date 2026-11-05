# Единый контракт сущности, частей и анимаций (Alpha → Unity)

**Статус: DRAFT** (предложение архитектора, не утверждено продюсером). **Дата:** 2026-11-06. **Автор:** Claude Code, задача S05A2-UNIVERSAL-CONTINUE-01.
**Код:** [contract.py](../../tools/alpha_models/universal/contract.py), [transfer_planner.py](../../tools/alpha_models/universal/transfer_planner.py), [AlphaEntityContract.cs](../../tools/alpha_models/transfer/unity/Runtime/AlphaEntityContract.cs), [AlphaEntityProbe.cs](../../tools/alpha_models/transfer/unity/Runtime/AlphaEntityProbe.cs), [AlphaEntityPlayMode.cs](../../tools/alpha_models/transfer/unity/Editor/AlphaEntityPlayMode.cs).
**Отчёт:** [S05A2_UNIVERSAL_CONTINUATION_REPORT](../reports/S05A2_UNIVERSAL_CONTINUATION_REPORT.md). **Предыдущая архитектура:** [UNIVERSAL_ASSET_ANIMATION_PIPELINE](UNIVERSAL_ASSET_ANIMATION_PIPELINE.md) (остаётся в силе, этот документ её дополняет).
**Границы:** S05A-2 IN_PROGRESS, S05B NOT_AUTHORIZED. Данные Alpha — только `.scratch` (D54).

---

## 1. Зачем

До этой задачи универсальным был путь «вид → внешность → части → семейство движения → клип ходьбы», а действия, ввод, видимость частей и оружие жили в коде одного гуманоида (`kind != 2` в бейкере, порядок 57/58/67 в `prepare-unity`, контроллер, требовавший `action_*`). Контракт отделяет **структуру существа** (что можно вывести для любого вида) от **доказательств** (что проверено на конкретном образце) и делает каждое решение явным полем с причиной.

## 2. Состав контракта (`cubegame.entity_contract/1`)

| Раздел | Содержимое | Откуда |
|---|---|---|
| `entity` | `kind`, `category`, `body_plan`, `topology`, `locomotion_mode`, `family_id` | статическая таблица, `creature_rig.resolve` |
| `parts[]` | `name` (блок отрисовки), `role`, `mirror_of`, `mirrored`, `slot`, `draw_site`, `anchor` + `unity_anchor` + доказательство, `block_status`, `model_index`, `size` | `creature_rig.BLOCKS`, реестр моделей |
| `channels.geometry` | READY / BLOCKED + причины (неразрешённый индекс модели, модели нет в БД, правило якоря не установлено) | |
| `channels.walk` | источники: `family_clip` (`TRANSFERRED_FROM_ALPHA`, вид-источник, захват + SHA256), `param_walk_formula` (`RECONSTRUCTED_FROM_EVIDENCE`, члены формулы) | клипы семейства, `param_walk.classify` |
| `channels.idle` | захваченная стойка или формула при амплитуде 0 | |
| `channels.actions` | профиль действий, триггер, статус, причина блокировки | `ACTION_PROFILES` (доказательства) |
| `channels.equipment.weapon` | SUPPORTED / BLOCKED + структурные причины | блоки хвата + флаги |
| `channels.visibility` | `STATIC_ALL` / `EXPLICIT_NATIVE_DRAW` (маски на ключах) | формат клипа v3 |
| `channels.lifecycle` | входной профиль (только у профиля с доказательством); переходы — `UNITY_TRANSITION` | |
| `readiness` | `AUTO_READY` / `NEEDS_RESEARCH` / `BLOCKED` + причины | |

**Категория существа — по структуре, не по имени:** `WINGED` (есть блоки крыльев), `QUADRUPED` (передние и задние конечности, флаг 0 — передние ходят), `BIPED_WITH_FORE_LIMBS`, `LEGS_ONLY`, `FORE_LIMBS_ONLY`, `LIMBLESS`. Роли частей: `core, head, head_attachment, tail, fore_limb, hind_limb, mid_limb, wing, equipment`. Число частей произвольно: в каталоге встречаются планы из 1–9 частей; в Unity проверены 3, 7 и 8 (с оружием) частей, в синтетических тестах — также 8-частный план «тело+хвост+2 стопы+2 средних конечности+2 крыла» без головы.

**`AUTO_READY`** означает: геометрия + риг + ходьба/покой могут быть получены автоматически без нового запуска Alpha. Действия — отдельный канал и для `AUTO_READY` вида чаще всего `BLOCKED`.

## 3. Происхождение (не смешивать)

| Статус | Значение | Где разрешён |
|---|---|---|
| `TRANSFERRED_FROM_ALPHA` | реальные GPU-матрицы Alpha, повторяемость проверена | клипы |
| `EMULATED_ALPHA_CODE` | исполнены исходные байты resolver/drawModel строгим эмулятором | клипы действий профиля |
| `RECONSTRUCTED_FROM_EVIDENCE` | переписанная формула, сверенная с захватами/эмуляцией | ходьба, покой, слой оружия на спине |
| `UNITY_TRANSITION` | политика переходов Unity-пилота | только поле `transitions` |
| `BLOCKED` | недоступно; причина обязательна | каналы |

Вспомогательные пометки `TEST_DATA_NOT_ALPHA`, `PLACEHOLDER_NOT_ALPHA` (скорость/амплитуда входа), `ORIGINAL_NOT_ALPHA` — в клипах контракта **запрещены** (Unity отклоняет `CONTRACT_CLIP_PROVENANCE_NOT_ALLOWED`).

## 4. Категории клипов

`LOOP_LOCOMOTION` (цикл по фазе), `STATIC_POSE` (стойка), `MOTION` (одноразовое действие entry→action→recovery), `HELD_STATE` (удерживаемая поза без таймера), `NATIVE_LIFECYCLE` (время жизни задаёт проверенный входной профиль). Видимость частей — свойство клипа (`EXPLICIT_NATIVE_DRAW`: маска на ключ, матрицы только видимых частей), поэтому появление/исчезновение частей поддержано для любого плана тела.

## 5. Профили действий — доказательства, а не правила тела

```python
ACTION_PROFILES["alpha_actions/humanoid_two_handed_catalog"] = {
  "specimens": [{"kind": 0x02, "seed_variant": [0, 0]}],
  "evidence_families": ["alpha_creature/e66d96be5a"],   # закреплено; drift проверяет VALIDATE
  "trigger": "player_input", "requires_parts": [...], "equipment": {...}, "specimen_equipment": {...},
  "categories": ["MOTION", "HELD_STATE", "NATIVE_LIFECYCLE"], "primary_chain": [57, 58, 67], ...}
```

* Профиль применяется **только** к своему образцу (вид + seed) **и** только пока образец разрешается в закреплённое семейство. Тот же номер вида с другим телом получает `SPECIMEN_APPEARANCE_DIFFERS`.
* Вид того же семейства (например, ElfMale, HumanFemale) получает `POSES_FAMILY_COMPATIBLE_TRIGGER_UNPROVEN`: позы drawModel совпали бы, но условие запуска (раса/класс игрока или ИИ) не доказано — импорт запрещён.
* Остальные — `NO_VERIFIED_ACTION_TRIGGER`. ИИ-запуски существ (`creature_ai`) не восстановлены ни для одного вида; правила ввода гуманоида на животных/монстров не переносятся.
* `action_baker` и `prepare-unity` читают порядок, «двуручные» и «динамические» ID из профиля, а не из кода.

Новый профиль (например, атака кота ИИ) добавляется **записью доказательств** после исследования запуска; ядро не меняется.

## 6. Контроллер и проверки в Unity

* `prepare-unity` пишет `Data/contract.json` (плоская форма для `JsonUtility`) со списком **фактически** импортированных клипов. Если профиль есть, но задание не импортирует библиотеку, режим честно становится `LOCOMOTION_ONLY` с причиной `ACTION_LIBRARY_NOT_IMPORTED`.
* `AlphaAnimationController.locomotionOnly` — явный режим «ходьба/остановка/покой»: требует непустую причину блокировки, отклоняет любые action/native-клипы в библиотеке и любые запросы действий (событие `action_refused:ACTIONS_BLOCKED`). По умолчанию выключен: прежний отказ `CONTROLLER_NO_REQUIRED_ACTIONS` сохранён.
* `AlphaEntityPlayMode.Check` (EditMode) сверяет контракт с ригом (набор частей, якоря, роли, симметрия зеркальных пар), библиотекой (происхождение, категория, объявленные клипы) и режимом контроллера.
* `AlphaEntityPlayMode.Run` (PlayMode, фиксированный шаг 30/60/120 Гц): ходьба ≥ 1 цикла → остановка → покой (дрейф 0) → действие (ACTIONS) или отказ (LOCOMOTION_ONLY) → возврат в локомоцию; каждый кадр локомоции поза рига = формула (≤ 1e-3 вокселя, ≤ 0.05°), кадры сохраняются в `TransferOutput/entity_frames/`.
* `run_transfer.ps1`: для `-UniversalJob` контракт обязателен (`ENTITY_CONTRACT_RESULT`, `ENTITY_PLAYMODE_RESULT` входят в итоговый `RESULT`); устаревший `contract.json` в старом пути S05A-2 удаляется.

## 7. Планировщик переноса

| Режим | Что делает | Выход (`.scratch`) |
|---|---|---|
| `scan` | все виды статической таблицы → контракты | `scan.json` |
| `plan` | контракты → план: AUTO_READY / NEEDS_RESEARCH / BLOCKED + задания + матрица | `plan.json`, `plan.md`, `coverage.csv`, `jobs/*.json` |
| `validate` | задания репозитория, клипы семейств, манифесты действий, дрейф профилей | `VALIDATE_RESULT` |
| `pilot` | только `PILOT_ALLOWLIST` (0x02, 0x20, 0x1e, 0x3f); `--execute` — Unity | `pilot/jobs`, `run_<kind>.log`, проекты `unity/Pilot_<kind>` |
| `batch` | **отказ** `NOT_AUTHORIZED` (exit 3), пока `BATCH_AUTHORIZATIONS` пуст | — |

Автозадание сам находит: захват семейства для сверки с Alpha (имя + SHA256 из клипа), локальную библиотеку действий профиля (вид, семейство, seed, оружие, `BAKE_RESULT=PASS`), формулу ходьбы. Скорость/амплитуда входа помечены `PLACEHOLDER_NOT_ALPHA`.

## 8. Как добавить новый тип существа без переписывания ядра

| Что появилось | Что сделать | Код ядра |
|---|---|---|
| вид из проверенных блоков | ничего: `plan` → `AUTO_READY`, задание сгенерировано | не меняется |
| новый блок отрисовки | строка `creature_rig.BLOCKS` + правило якоря + роль в `BLOCK_ROLES` (и `AlphaRig.Anchor` при новом правиле) | таблицы данных |
| новый член формулы ходьбы | `param_walk.TERMS` + реализация члена (по методичке PARAMETRIC_WALK) | формула, не конвейер |
| действия существа | исследование запуска → запись в `ACTION_PROFILES` (образец, семейство, триггер, категории) → бейкинг | данные профиля |
| видо-зависимый код drawModel | адаптер вида (в подписи семейства уже учтён) | отдельный модуль |
| новый вид экипировки | `EQUIPMENT_BLOCKS` + условия `weapon_layer_support` + якорь | данные + условие |

## 9. Известные ограничения

* Профиль действий один (гуманоид S05A-2). У 148 видов нет доказанного триггера, у 7 — совместимые позы без доказанного запуска.
* Компоненты, сознательно привязанные к профилю гуманоида: `lifecycle_pipeline.py`, `lifecycle_research.py`, `AlphaNativeLifecycle/Driver/Probe`, проверки хвата (`GRIP`) — включаются только при наличии `lifecycle.json`/`actions.json` из библиотеки профиля.
* `AUTO_READY` по формуле для «только тело» (39 видов `LIMBLESS`) означает только покачивание тела; есть ли у них движение в игре — не исследовано.
* `transfer_planner` использует внешность seed (0, 0) статической таблицы; другие seed проверяются в `prepare-unity` эмулятором.
