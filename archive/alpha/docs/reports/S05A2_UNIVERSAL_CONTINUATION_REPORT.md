# S05A2-UNIVERSAL-CONTINUE-01 — универсализация конвейера Alpha → Unity

**TASK:** S05A2-UNIVERSAL-CONTINUE-01 (2026-11-06, исполнитель — Claude Code, ведущий разработчик/архитектор задачи).
**STATUS:** `READY_FOR_REVIEW`. Этап **S05A-2 — IN_PROGRESS / REVIEW OPEN**, **S05B — NOT_AUTHORIZED**.
**Ветка:** `claude/s05a2-universal-continuation` от `e4f5be6` (`codex/s05a2-action-lifecycle`).
**Архитектура:** [UNIVERSAL_ENTITY_CONTRACT](../architecture/UNIVERSAL_ENTITY_CONTRACT.md) (DRAFT). **Методичка:** [UNIVERSAL_TRANSFER_PLAYBOOK §7](../agents/UNIVERSAL_TRANSFER_PLAYBOOK.md). **Матрица:** [UNIVERSAL_TRANSFER_COVERAGE.csv](../research/alpha2013/UNIVERSAL_TRANSFER_COVERAGE.csv) (только метаданные).

---

## 1. Итог

Конвейер получил **единый контракт сущности/частей/анимаций** и **планировщик переноса** с режимами SCAN / PLAN / VALIDATE / PILOT / BATCH. Действия, вход, видимость частей и оружие больше не определяются кодом одного гуманоида: допуск к действиям даёт только запись доказательств (профиль с образцом и закреплённым семейством), категория существа выводится из структуры блоков, а контроллер Unity получил явный режим `LOCOMOTION_ONLY` для существ без доказанных действий.

Критерий успеха задачи — «новый поддерживаемый тип существа не требует переписывания конвертера или анимационного ядра» — выполнен для всех видов, собранных из уже проверенных блоков: 151 из 156 видов каталога автоматически получают контракт, задание и план (`AUTO_READY` по геометрии + ходьбе/покою), неподдержанные 5 видов названы с причиной. **Действия доступны одному образцу (гуманоид S05A-2)**; для остальных честно `BLOCKED` с причиной — подделка поз не генерировалась.

## 2. Что изменено

| Файл | Изменение |
|---|---|
| [contract.py](../../tools/alpha_models/universal/contract.py) | новый: контракт `cubegame.entity_contract/1`, структурные категории/роли/зеркальные пары, каналы, профили действий как доказательства (`evidence_families` закреплено), проверка клипов (rig, части + необязательные слои экипировки, происхождение, категория, маски видимости), плоский Unity-контракт |
| [transfer_planner.py](../../tools/alpha_models/universal/transfer_planner.py) | новый: SCAN/PLAN/VALIDATE/PILOT/BATCH; автозадания (формула, эталонный захват по SHA256, библиотека действий профиля), `coverage.csv`, allowlist пилота, отказ BATCH |
| [pipeline.py](../../tools/alpha_models/universal/pipeline.py) | `prepare-unity` пишет `Data/contract.json` по фактически импортированным клипам; порядок обязательных действий берётся из профиля (было: жёсткие 57/58/67); отказ импортировать действия без профиля; `ACTION_LIBRARY_NOT_IMPORTED` → `LOCOMOTION_ONLY` |
| [action_baker.py](../../tools/alpha_models/universal/action_baker.py) | допуск по профилю-образцу вместо `kind != 2`; оружие, «двуручные» и «динамические» ID — из профиля; проверка выполняется до загрузки бинарника |
| [AlphaEntityContract.cs](../../tools/alpha_models/transfer/unity/Runtime/AlphaEntityContract.cs) | новый: разбор и проверка контракта в Unity (части/роли/якоря/зеркала, происхождение, категории, режим контроллера, реестр действий, объявленные клипы) |
| [AlphaAnimationController.cs](../../tools/alpha_models/transfer/unity/Runtime/AlphaAnimationController.cs) | `locomotionOnly` + обязательная причина; отказ от action-клипов и запросов; по умолчанию поведение прежнее |
| [AlphaEntityProbe.cs](../../tools/alpha_models/transfer/unity/Runtime/AlphaEntityProbe.cs), [AlphaEntityPlayMode.cs](../../tools/alpha_models/transfer/unity/Editor/AlphaEntityPlayMode.cs) | новые: EditMode `Check` и PlayMode `Run` для любой сущности; кадры в `entity_frames/` |
| [AlphaTransferSelfTests.cs](../../tools/alpha_models/transfer/unity/Editor/AlphaTransferSelfTests.cs) | +10 синтетических кейсов контракта и режима `LOCOMOTION_ONLY` |
| [run_transfer.ps1](../../tools/alpha_models/transfer/run_transfer.ps1) | шаги `ENTITY_CONTRACT_RESULT`, `ENTITY_PLAYMODE_RESULT` (частота `-ControllerFps`); контракт обязателен для `-UniversalJob`; устаревший контракт удаляется в старом пути |
| [entity_contact_sheet.ps1](../../tools/alpha_models/transfer/entity_contact_sheet.ps1) | локальный контактный лист реальных кадров `entity_frames` (System.Drawing, вывод только в `.scratch`) |
| [test_transfer_contract.py](../../tools/alpha_models/tests/test_transfer_contract.py) | новый: 32 теста (синтетика, локальные эталоны, независимость от гуманоида) |

Не менялись: формула ходьбы и её члены, клипы и хват меча (исправленный хват второй руки сохранён), 93 + 9 payloads Codex, S04, `main`, установка Alpha.

## 3. Проверки

Все прогоны — из единственной рабочей копии `D:\Projects\CubeGame`, Unity 6000.3.27f2, без запуска Alpha. Вердикт = маркер отчёта **и** код выхода 0 каждого шага (`run_transfer.ps1`), отчёты свежей сессии.

**Python / раннер**

| Проверка | Результат |
|---|---|
| `python -m unittest discover -s tools/alpha_models/tests -t .` | **224 OK** (192 прежних без изменений + 32 новых `test_transfer_contract`) |
| `run_transfer.ps1 -Test` (вердикт/изоляция) | **10/10 PASS** |
| `transfer_planner validate` (13 заданий репозитория, 4 клипа семейств, 2 манифеста действий, дрейф профиля) | **PASS**, 0 проблем |
| `transfer_planner batch` | `NOT_AUTHORIZED`, exit 3 (любой `--authorization`) |
| `transfer_planner pilot` вида вне allowlist | `REFUSED`, exit 2 (тест) |

**Unity-пилот эталонов (`transfer_planner pilot --execute`, задания сгенерированы автоматически)**

| Эталон | Категория / части | Шаги | Ключевые числа |
|---|---|---|---|
| HumanMale 0x02 | BIPED_WITH_FORE_LIMBS, 8 (с оружием), `ACTIONS` | 10/10 PASS | самотесты 40/40; GEOMETRY/ROUNDTRIP 103 клипа ≤ 1e-3; GRIP 57/58/67 main 0.0000, radial ≤ 2.366; каталог 98/98 действий, 35 927 кадров @60, ошибки поз 0; native 919/919 + сценарий 2 962 кадров; формула vs Alpha 446 кадров; сущность: ходьба → стоп → покой (дрейф 0) → `action_057` (3.467 с) → ходьба, поза = формула (0 / 0°) |
| WhiteCat 0x20 | QUADRUPED, 7, `LOCOMOTION_ONLY` | 7/7 PASS | GEOMETRY 7/7; ROUNDTRIP захваченного `walk` 932 ключа 0.000003; формула vs Alpha **484 кадра, max 0.000338 вокселя / 0°**; сущность: 3 запроса действий отклонены, поза = формула 0 |
| Cat 0x1e (**не снимался**) | QUADRUPED, 7, `LOCOMOTION_ONLY` | 7/7 PASS | собственные модели вида (голова 2728 вершин); клип семейства + формула сверены с **484 живыми кадрами WhiteCat** того же семейства, max 0.000338 |
| PlainRunner 0x3f | LEGS_ONLY, 3, `LOCOMOTION_ONLY` | 7/7 PASS | GEOMETRY 3/3; ROUNDTRIP 872 ключа 0.000004; формула vs Alpha 484 кадра, max 0.000200 |

**Регрессия ручного задания Codex `human_0x02_lifecycle.json` в исходном проекте `.scratch/s05a2/unity/AlphaTransfer`**

| Гц | Шаги | Каталог: действий / кадров | Native: кадров | Совпадение с отчётом Codex |
|---|---|---|---|---|
| 30 | 10/10 PASS, exit 0 | 98/98 / 18 023 | 1 484 | да |
| 60 | (пилот 0x02 выше, то же содержание задания) | 98/98 / 35 927 | 2 962 | да |
| 120 | 10/10 PASS, exit 0 | 98/98 / 71 585 | 5 919 | да |

Порядок `requiredClips` из профиля побайтово равен прежнему `actions.json`. Пороги не менялись.

**Сущностная проба на разных частотах.** Первые прогоны шли на 60 Гц: раннер передавал `-controllerFps` только старому контроллеру — найдено и исправлено в ходе задачи. После исправления WhiteCat 0x20 и HumanMale 0x02 прогнаны заново целиком (все шаги) на 30 и 120 Гц:

| Эталон | 30 Гц | 120 Гц |
|---|---|---|
| WhiteCat 0x20 (7 шагов) | PASS, 69 кадров, поза = формула 0 / 0°, 3 отказа действий | PASS, 239 кадров, 0 / 0°, 3 отказа |
| HumanMale 0x02 (10 шагов) | PASS, 173 кадра, `action_057` 3.500 с, 0 / 0° | PASS, 653 кадра, `action_057` 3.458 с, 0 / 0° |

**Визуально:** `.scratch/universal/visuals/entity_contact_sheet.png` — реальные кадры Unity PlayMode всех четырёх эталонов (ходьба, остановка, покой, удар гуманоида / отказ действий у кота и бегуна), просмотрены.

## 4. Матрица покрытия каталога (156 видов, статическая таблица, без запуска Alpha)

| Категория (по структуре) | AUTO_READY | NEEDS_RESEARCH | BLOCKED |
|---|---|---|---|
| BIPED_WITH_FORE_LIMBS | 58 | 2 (Mole 0x57, Zombie 0x60 — видо-зависимый код drawModel) | 2 (OrcMale 0x0b — конфликт реестра модели головы; DarkTroll 0x6d — индекс волос 29 не разрешён) |
| QUADRUPED | 22 | 1 (Dragon 0x65 — особый якорь головы и вектор прицеливания) | 0 |
| LEGS_ONLY | 15 | 0 | 0 |
| WINGED | 13 | 0 | 0 |
| FORE_LIMBS_ONLY | 4 | 0 | 0 |
| LIMBLESS (1–2 части) | 39 | 0 | 0 |
| **Итого** | **151** | **3** | **2** |

| Канал | Итог |
|---|---|
| Семейства движения | 106: AUTO_READY 101, NEEDS_RESEARCH 3, BLOCKED 2 |
| Ходьба | 15 видов: захваченный клип семейства (`TRANSFERRED_FROM_ALPHA`) + формула; 138 — только формула (`RECONSTRUCTED_FROM_EVIDENCE`); 3 — нет источника |
| Действия | 1 вид `AVAILABLE` (HumanMale 0x02, профиль гуманоида); 7 — `POSES_FAMILY_COMPATIBLE_TRIGGER_UNPROVEN` (то же семейство drawModel: ElfMale/Female, HumanFemale, LizardmanMale/Female, FrogmanMale/Female); 148 — `NO_VERIFIED_ACTION_TRIGGER` |
| Оружие | слой поддержан структурно только для планов с двумя блоками хвата и не ходящими передними конечностями; подтипы 15/16/17 |

`AUTO_READY` для 39 «безногих» видов означает только покачивание тела по формуле; играют ли они вообще движение в Alpha — не исследовано. Скорость/амплитуда входа во всех автозаданиях — `PLACEHOLDER_NOT_ALPHA`.

## 5. Независимость от гуманоида

| Проверка | Результат |
|---|---|
| Категория/семейство/готовность не зависят от `name_hint` (синтетический вид с именем «HumanMale» и телом кота) | PASS (тест) |
| Номер вида-образца с другим телом не получает действия (`SPECIMEN_APPEARANCE_DIFFERS`) | PASS (тест); найдено и исправлено в ходе задачи |
| В `contract.py`/`transfer_planner.py` ID действий гуманоида и вид 0x02 встречаются только внутри `ACTION_PROFILES`/`PILOT_ALLOWLIST` | PASS (AST-тест) |
| `AlphaEntityContract`/`AlphaAnimationController`/`AlphaEntityProbe` не содержат имён частей и ID действий (кроме явного отказного запроса `action_057` у кота) | PASS (тест) |
| Синтетический 8-частный план «тело+хвост+2 стопы+2 средние конечности+2 крыла» без головы → контракт, формула, `AUTO_READY` | PASS (тест) |

Сознательно привязаны к профилю гуманоида и включаются только его библиотекой: `lifecycle_pipeline.py`, `lifecycle_research.py`, `native_lifecycle.py`, `AlphaNative*`, проверки `GRIP`, `AlphaPlayModeProbe` старого пути S05A-2.

## 6. Оставшиеся блокеры

1. **Действия негуманоидов:** ни для одного вида не восстановлен ИИ-запуск (`CombatBehavior`/world update). Без этого позы drawModel для состояний действий нельзя связывать с существом.
2. **Совместимые по семейству виды** (7): позы гуманоида совпали бы, но запуск (раса/класс игрока, ИИ) не доказан.
3. **Видо-зависимый код** Mole/Zombie/Dragon — отдельные адаптеры.
4. **Реестр моделей:** OrcMale (конфликт имён), DarkTroll (индекс 29).
5. **Скорость фазы** выведена по двум размерам (`INFERRED`); динамика амплитуды Alpha не восстановлена.
6. **Полный gameplay lifecycle** гуманоида остаётся `PARTIAL` (GUI/ресурсы/world), 51/80 — BLOCKED_CONTEXT (без изменений).

## 7. Готовность к S05B — рекомендации

**Технически готово** к пакетному режиму: геометрия + риг + ходьба/покой для 151 вида с автоматической проверкой контракта, формулы, сверкой с захватом семейства (где он есть) и PlayMode на любой частоте. Механизм пакета = `plan` + `pilot`-шаги на каждый вид; включается одной записью решения в `BATCH_AUTHORIZATIONS` и реализацией цикла (сейчас намеренно `NotImplementedError`).

**Не готово** и не должно входить в S05B без отдельного исследования: действия любых существ, кроме образца; видо-зависимые виды; экипировка кроме двуручного оружия гуманоида.

Черновик ТЗ первого шага (не разрешение): [S05B1_LOCOMOTION_BATCH_DRAFT](../stages/S05B1_LOCOMOTION_BATCH_DRAFT.md).

Предлагаемый порядок, если продюсер решит открыть S05B:
1. S05B-1: пакет «геометрия + ходьба/покой» только для `AUTO_READY` семейств, где есть **живой захват семейства** (15 видов) — сверка с Alpha в каждом прогоне.
2. S05B-2: семейства «только формула» по категориям (WINGED, QUADRUPED, LEGS_ONLY…), с одним контрольным захватом на категорию.
3. Параллельное исследование: ИИ-запуск действий одного негуманоида (например, кота) → первый новый профиль действий.
4. Вопрос продюсеру: нужны ли в S05B 39 «безногих» статичных объектов.

## 8. Неподтверждено / ограничения

* Unity проверен на четырёх эталонах; остальные 147 `AUTO_READY` видов в Unity **не запускались** (S05B не разрешён) — их готовность выведена статически.
* Захват WhiteCat используется как эталон для Cat 0x1e (вид-получатель не снимался).
* Визуальные кадры и все данные Alpha — только локально в `.scratch` (не в Git).

## 9. Локальные материалы (`D:\Projects\CubeGame\.scratch\universal\`)

* `plan/plan.json`, `plan/plan.md`, `plan/coverage.csv`, `plan/jobs/auto_*.json`, `plan/validate.json`
* `pilot_run1/` — первый пилот всех четырёх (0x02: `run_0x02.log`); `pilot/` — финальный пилот 0x20/0x1e/0x3f на замороженном коде; задания `pilot/jobs/auto_*.json`
* проекты `unity/Pilot_0x20`, `Pilot_0x1e`, `Pilot_0x3f`, `Pilot_0x02` → `TransferOutput/` (`entity_contract.txt`, `entity_playmode.txt`, `entity_frames/`, история прогонов в `TransferOutput_history/`)
* `human_lifecycle_{30,120}.log` и `human_lifecycle_{30,120}_TransferOutput_reports/` — регрессия задания Codex; `entity_<kind>_{30,120}.log`, `entity_reports/` — сущностная проба на 30/120 Гц
* `visuals/entity_contact_sheet.png`
* `python_full.log`
