# S05B-2 — DeepSeek Harness: первое новое семейство движения (отчёт)

**Статус: `READY_FOR_REVIEW`** — не `ACCEPTED`; визуальная приёмка нового семейства — за продюсером.
**Задача:** `S05B2-DEEPSEEK-PILOT-01` по [D62](../decisions/D62-d61-technical-fix-accepted-s05b2-deepseek-authorized.md); ТЗ — [S05B2_DEEPSEEK_BATCH_SPEC](../stages/S05B2_DEEPSEEK_BATCH_SPEC.md).
**Дата:** 2026-11-06. **Исполнитель:** DeepSeek Harness.
**База:** `review/d62-s05b2-deepseek-handoff` @ `64b2b928c78cca3e199eb3933806845a341e605b`. **Рабочая ветка:** `stage/s05b2-deepseek-batch`.
S04, `main`, оригинальные файлы `D:\Games\Cube world` не менялись; ассеты/кадры Alpha не публиковались (всё локально в `.scratch`).

## 0. Итог

| Показатель | Значение |
|---|---|
| Видов в каталоге (`scan`) | **156** в **106** семействах движения |
| Перенесено до S05B-2 (S05B-1, D59) | **15** видов / **3** семейства (гуманоиды, коты, бегуны) |
| Перенесено в этом пакете | **4** вида / **1** новое семейство (`alpha_creature/773a44f932`) |
| Осталось | **137** видов / **102** семейства: 132 `AUTO_READY`, 3 `NEEDS_RESEARCH`, 2 `BLOCKED` |
| Результат пакета | **`BATCH_RESULT=PASS_FORMULA_ONLY`**, 6/6 прогонов PASS, у всех `live_check=NOT_CAPTURED` |
| Unity | 7 шагов × 6 прогонов — PASS; регрессия HumanMale **98/98** при 30/60/120 Гц с прежними числами |
| Python | **301/301 OK**; `transfer_planner validate` — **PASS** (0 проблем, 18 проверок) |
| PowerShell 5.1 | **10/10** скриптов |
| Действия | **`BLOCKED`** у всех 4 видов: `NO_VERIFIED_ACTION_TRIGGER` (без доказанного запуска ИИ; позы не изобретались) |
| Сцена просмотра | отдельный проект `.scratch/review/unity/S05B2_FamilyReview`: `REVIEW_SETUP_RESULT=PASS` (Build/Check/PlayMode) |

Пакет закрыт «технически», но **не** объявляется полным паритетом с Alpha: ходьба — формула Alpha без живого
захвата этого семейства, действий нет, экипировки нет.

## 1. Каталог: было / добавлено / осталось

| Категория | Видов | Семейств | Что известно |
|---|---|---|---|
| Всего (`transfer_planner plan`) | 156 | 106 | 151 `AUTO_READY`, 3 `NEEDS_RESEARCH`, 2 `BLOCKED` |
| Импортировано S05B-1 (D59) | 15 | 3 | гуманоид `51aeec9280` (8 видов), кот `e66d96be5a` (3), бегун `ced63e5e96` (4) |
| Импортировано S05B-2 (D62) | 4 | 1 | `773a44f932`: 0x4c–0x4f |
| Осталось | 137 | 102 | 132 `AUTO_READY` (97 семейств), 3 `NEEDS_RESEARCH` (0x57 Mole, 0x60 Zombie, 0x65 Dragon), 2 `BLOCKED` (0x0b OrcMale `MODEL_NOT_IN_DB`, 0x6d DarkTroll `MODEL_INDEX_UNRESOLVED`); **ни у одного нет профиля действий** |

## 2. Выбранное семейство и обоснование

**`alpha_creature/773a44f932`** — 0x4c SpikeCreature, 0x4d Anubis, 0x4e Horus, 0x4f Jester.

* **Новое семейство движения** — не гуманоид/кот/бегун S05B-1; другой `family_id`, другой набор блоков:
  `body + foot_l + foot_r + hand_l + hand_r + head` (6 частей, без `hair` и без `tail`).
* **Полное покрытие моделей достижимо сразу**: у каждого слота ровно один вариант модели (`slot_variants`
  по 1) → seed (0, 0) покрывает все модели; вариантная матрица не нужна.
* **Нет известных исключений**: не `KIND_SPECIFIC_CODE`, нет крыльев (`slot_9a`) и `slot_98`, реестр моделей
  разобран — вероятность полного тестового переноса высокая.
* **4 вида одного семейства** — проверка повторяемости одного и того же переноса на разных моделях
  (различаются только модели, параметры движения семейства общие).
* Действия не могли быть подтверждены заранее (нет живого захвата и доказанного триггера ИИ этого
  семейства) — поэтому они честно `BLOCKED`, а не сгенерированы.

## 3. Что сделано

### 3.1 Авторизация D62 в пакетном исполнителе (общий код, не ручной перенос)

| Файл | Изменение |
|---|---|
| [transfer_planner.py](../../tools/alpha_models/universal/transfer_planner.py) | `BATCH_AUTHORIZATIONS` += `("D62", "s05b2")`; текст отказа для остальных работ; код возврата `PASS_FORMULA_ONLY` = 0 |
| [batch_runner.py](../../tools/alpha_models/universal/batch_runner.py) | scope `s05b2` (решение D62, 4 вида, семейство, `coverage=BASE`, `fps_samples=(30,120)`, `live_frames="formula_only"`); селектор по **семействам** с проверкой дрейфа (`SCOPE_DRIFT`, причина — вид выпал из `AUTO_READY`/`FORMULA_VERIFIED`); `judge_entry(live_required=...)`; `paramwalk_alpha_frames`; `live_check` в записи прогона; `BATCH_RESULT=PASS_FORMULA_ONLY` и колонка `Live check` в `summary.md` |
| [spike_0x4c.json](../../tools/alpha_models/universal/jobs/spike_0x4c.json) | задание семейства в Git (только метаданные, без данных Alpha) |
| [test_batch_runner.py](../../tools/alpha_models/tests/test_batch_runner.py) | +6 тестов: селектор семейства, дрейф при потере готовности/формулы, строгость live-скоупа, `PASS_FORMULA_ONLY`, запрет «нулевых живых кадров без объяснения» |
| [test_transfer_contract.py](../../tools/alpha_models/tests/test_transfer_contract.py) | обновлены ожидания D62 (новые отказы для неверных пар `D62/s05b1`, `D62/s05ba`, `D61/s05b2`, `d62/s05b2`, `D62/s05b3` и др.); +1 тест на реальном каталоге: scope `s05b2` = ровно 0x4c–0x4f, без `reference_capture`, формула `FORMULA_VERIFIED`, действия `BLOCKED`, 6 частей |

**Почему `PASS_FORMULA_ONLY`, а не `PASS`:** у семейства нет живого захвата, поэтому сверка формулы с
реальными кадрами Alpha невозможна. Прежний порог «PASS только при `alpha_frames>0`» не ослаблен: для
скоупа с `live_frames="formula_only"` запись помечается `live_check=NOT_CAPTURED`, итог пакета —
`PASS_FORMULA_ONLY`, и это отдельно видно в `summary.json`/`summary.md`. Записи без объяснения нуля в
отчёте формулы (`PARAMWALK_NO_LIVE_AND_NO_REASON`) по-прежнему FAIL.

### 3.2 Перенос семейства (существующим конвейером)

* геометрия, риг и якоря — `pipeline.prepare_unity` из статического каталога и `creature_rig` (никакого
  ручного моделирования);
* правила D61 применены автоматически: у всех частей `loadFlag=1` (`VERIFIED_ALPHA_CODE`), чистых R/G/B
  вокселей нет (`markers 0/0/0`, `markerTransparent=False`), тинт `NONE` («no colour upload for this
  block» — в Alpha цвет для этих блоков не загружается), порядок отрисовки — общий конвейер;
* idle/walk — параметрическая ходьба Alpha (`RECONSTRUCTED_FROM_EVIDENCE`, `FORMULA_VERIFIED`), проверка
  C# против эталонной Python-реализации (900 golden-кейсов) и против собственного рантайма;
* контроллер — `LOCOMOTION_ONLY`, действия отклоняются (`locomotion_only_refused=True`).

## 4. Журнал по видам

| kind | имя | семейство | seed / модели | GEOMETRY | RIG | IDLE | WALK | ACTIONS | EQUIPMENT |
|---|---|---|---|---|---|---|---|---|---|
| 0x4c | SpikeCreature | `773a44f932` | (0,0) / `spike-{body,head,hand,foot}.cub` | PASS 6/6 | PASS | AVAILABLE | AVAILABLE (`RECONSTRUCTED_FROM_EVIDENCE`, `FORMULA_VERIFIED`) | `BLOCKED` `NO_VERIFIED_ACTION_TRIGGER` | NONE |
| 0x4d | Anubis | `773a44f932` | (0,0) / `anubis-{body,head,hand,foot}.cub` | PASS 6/6 | PASS | AVAILABLE | AVAILABLE (то же) | `BLOCKED` | NONE |
| 0x4e | Horus | `773a44f932` | (0,0) / `horus-{body,head,hand,foot}.cub` | PASS 6/6 | PASS | AVAILABLE | AVAILABLE (то же) | `BLOCKED` | NONE |
| 0x4f | Jester | `773a44f932` | (0,0) / `jester-{body,head,hand,foot}.cub` | PASS 6/6 | PASS | AVAILABLE | AVAILABLE (то же) | `BLOCKED` | NONE |

Общие поля: `trigger_evidence=нет` (`NO_VERIFIED_ACTION_TRIGGER`: для семейства нет доказанного правила
запуска ИИ), `provenance walk/idle = RECONSTRUCTED_FROM_EVIDENCE`, `live_check=NOT_CAPTURED`,
`Unity = PASS` (все 7 шагов), локальные артефакты — контактные листы
`.scratch/universal/batch/s05b2/visuals/0x4c_spikecreature.png` … `0x4f_jester.png`, кадры прогонов —
`.scratch/universal/batch/s05b2/runs/<entry>/entity_frames/`.

**Причина блокировки действий (у всех 4 видов):** запуск действия доказывается исполнением байтов тика
ИИ на **наблюдённом** состоянии NPC конкретного вида (П1, D60). Живого наблюдения вида этого семейства нет,
а подставлять состояние другого семейства или выдумывать триггер запрещено. Реестр для Claude — §8.

## 5. Проверки: команды, коды возврата, числа

```powershell
# 0. база
python -m unittest discover -s tools/alpha_models/tests -t .            # 301/301 OK, exit 0
python -m tools.alpha_models.universal.transfer_planner plan --out-dir .scratch/universal/plan_d62
python -m tools.alpha_models.universal.transfer_planner validate --out .scratch/universal/plan_d62/validate.json
#    VALIDATE_RESULT=PASS problems=0 checks=18 (включая JOB spike_0x4c.json OK)
# 1. авторизация и план пакета
python -m tools.alpha_models.universal.transfer_planner batch --authorization D62 --scope s05b2 --dry-run
#    BATCH_RESULT=DRY_RUN, entries=6 (0x4c 60/30/120 Гц; 0x4d/0x4e/0x4f 60 Гц), покрытие моделей 100%
# 2. прогон
python -m tools.alpha_models.universal.transfer_planner batch --authorization D62 --scope s05b2
#    BATCH_RESULT=PASS_FORMULA_ONLY, counts={"PASS":6}, live_check={"CAPTURED":0,"NOT_CAPTURED":6}
#    отказы проверены тестами: D59/s05b2, D62/s05b1, D62/s05ba, D61/s05b2, S05B → NOT_AUTHORIZED (exit 3)
```

Шаги одного прогона (`run_transfer.ps1`, Unity 6000.3.27f2, изолированный проект):

| Шаг | Результат |
|---|---|
| `SELFTEST_RESULT` | PASS, `cases=40 failed=0` |
| `TRANSFER_RESULT` | PASS: GEOMETRY 6/6 (ожидаемые вершины совпали), ROUNDTRIP ≤ 0.000002, SEAM CONTINUOUS |
| `PLAYMODE_RESULT` | PASS, 90 кадров ходьбы |
| `PARAMWALK_RESULT` | PASS, `golden=900`, `alpha_frames=0` + строка `ALPHA not_available (formula-only kind)`; SELF 8/8 |
| `LOCOMOTION_PLAYMODE_RESULT` | PASS, 211 кадров, развёртка амплитуды/boost, наклон головы 9.9995° (ожидание 9.9995°) |
| `ENTITY_CONTRACT_RESULT` | PASS: `LOCOMOTION_ONLY`, `actions=BLOCKED`, `walk_sources=RECONSTRUCTED_FROM_EVIDENCE` |
| `ENTITY_PLAYMODE_RESULT` | PASS: ошибка формулы 0.000000 вокселя/0.000000°, `walk_max_part_rot_deg≈143.6`, `move≈11.64` вокселя, `idle_drift=0` |

Регрессия системы действий HumanMale (в том же пакете): **98/98 действий** при 30/60/120 Гц, кадры
**18 023 / 35 927 / 71 585**, native-lifecycle **1484 / 2962 / 5919** — те же числа, что в D59/D61;
ошибка поз 0.000000 / 0.000000°.

Сцена просмотра нового семейства (отдельный проект, те же сценарные скрипты, что принял продюсер):

```
REVIEW_DATA_RESULT=PASS specimens=4 kinds=4 failed=0
PS51_CHECK_RESULT=PASS
REVIEW_BUILD_RESULT=PASS, REVIEW_CHECK_RESULT=PASS specimens=4 sources=12 samples=804 problems=0
REVIEW_PLAYMODE_RESULT=PASS specimens=4 frames=51 errors=0 marker_toggle_vertices=8832->8832
```
`8832 -> 8832` — ожидаемо: в моделях этого семейства нет маркерных вокселей (правило D61 ничего не скрывает).

Принятая продюсером сцена 15 видов пересобрана после правок review-кода (число видов теперь берётся из
`index.json → expectedKinds`, подпись «все виды в ряд» стала нейтральной): `REVIEW_DATA_RESULT=PASS
specimens=72 kinds=15 failed=0`, `REVIEW_BUILD_RESULT=PASS`, `REVIEW_CHECK_RESULT=PASS`,
`REVIEW_PLAYMODE_RESULT=PASS` — регрессии сцены нет.

## 6. Доказательность и ограничения

* **`live_check=NOT_CAPTURED`.** Формула ходьбы сверена с эталонной реализацией (900 кейсов) и с рантаймом
  Unity, но **не** с живыми кадрами Alpha этого семейства. `TRANSFERRED_FROM_ALPHA` не присваивался.
* **Скорость перемещения** — `PLACEHOLDER_NOT_ALPHA` (3.7 — медиана живых захватов других семейств, OQ-23).
* **Действия** — `BLOCKED`, позы не изобретались; оружие/экипировка не имитировались (`EQUIPMENT=NONE`).
* **Переходы** — `UNITY_TRANSITION` (не извлечены из Alpha).
* Геометрия и цвет — реальные модели Alpha: маркеров нет, тинт не требуется; это проверено правилом D61,
  а не «на глаз».
* Ограничение метода: `PASS_FORMULA_ONLY` — новый статус исполнителя; он **не** равен полному PASS и требует
  ревью архитектора (§8, B-4).

## 7. Как открыть новое семейство в Unity (для продюсера)

```powershell
# 1. собрать/обновить изолированную сцену просмотра (один раз; данные — из прогона пакета)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools\alpha_models\review\setup_review.ps1 `
  -Project D:\Projects\CubeGame\.scratch\review\unity\S05B2_FamilyReview `
  -BatchDir .scratch/universal/batch/s05b2

# 2. открыть сцену (или сразу PlayMode)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools\alpha_models\review\open_review.ps1 `
  -Project D:\Projects\CubeGame\.scratch\review\unity\S05B2_FamilyReview -Play
```

В сцене: 4 вида ряда (SpikeCreature / Anubis / Horus / Jester).
`↑/↓` — вид, `←/→` — вариант, `1/2/3` — Idle/Walk/Action, `L` — все виды в ряд, `F` — целиком,
`Space` — пауза, `,`/`.` — кадр, `−`/`=` — скорость, `Tab` — свернуть панель, `H` — скрыть интерфейс.
Вкладка «Проверки» показывает происхождение клипов (`RECONSTRUCTED_FROM_EVIDENCE`) и блокировку действий.
`Action` для этого семейства пуст по построению (`BLOCKED`), это ожидаемо, а не ошибка сцены.

Без Unity: готовые локальные листы —
`.scratch/review/unity/S05B2_FamilyReview/ReviewOutput/lineup_walk_34.png`, `lineup_idle_front.png`,
`variants_0x4c..4f.png` и контактные листы `.scratch/universal/batch/s05b2/visuals/*.png`.

## 8. Блокеры и реестр для Claude

| # | Блокер | Затронуто | Что нужно |
|---|---|---|---|
| B-1 | Действия семейства без живого наблюдения: триггер ИИ доказывается на наблюдаемом состоянии NPC (П1) | все 137 неимпортированных видов | либо разрешённый контролируемый захват одного вида семейства, либо решение архитектора о способе доказать триггер без наблюдения (сейчас `BLOCKED`, без заглушек) |
| B-2 | `KIND_SPECIFIC_CODE` в `drawModel` | 0x57 Mole, 0x60 Zombie, 0x65 Dragon | отдельный адаптер вида (исследование, не пакетный режим) |
| B-3 | Реестр/модели | 0x0b OrcMale (`MODEL_NOT_IN_DB head`), 0x6d DarkTroll (`MODEL_INDEX_UNRESOLVED hair:29`) | адресная проверка загрузки моделей |
| B-4 | Новый статус `PASS_FORMULA_ONLY` | массовые партии новых семейств | ревью архитектора: подтвердить статус либо закрепить обязательный живой захват на семейство |

## 9. Git

* Ветка: `stage/s05b2-deepseek-batch` (от `review/d62-s05b2-deepseek-handoff` @ `64b2b92`).
* Коммит пакета: **`5b8b473af0ca457c472904159e8d90a6efd9c425`** (код, тесты, задание, отчёт и обновления
  `ACTIVE_STAGE`/`PROJECT_MEMORY`/методички); запись SHA в отчёте — отдельный docs-коммит.
* Публикация: `git push origin stage/s05b2-deepseek-batch` (постоянное разрешение продюсера). Push ≠ `ACCEPTED`.
* В Git попали только код, тесты, задания (метаданные) и документы; `.cub`/`.cubdec`, кадры и сцены Alpha
  остались в игнорируемом `.scratch`.
