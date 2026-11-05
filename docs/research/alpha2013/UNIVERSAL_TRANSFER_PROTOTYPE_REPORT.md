# Универсальный конвейер Alpha → Unity: результаты прототипа

**Дата:** 2026-11-05. **Исполнитель:** Claude Code. **Ветка:** `claude/universal-animation-pipeline` (от `deepseek/s05a2-animation-transfer-review` @ `d8bd51c3`).
**Статус:** `READY_FOR_REVIEW` (не ACCEPTED). S05B — NOT_AUTHORIZED; массовой конвертации не было (2857 `.cub` не обрабатывались; модели читались только для 5 выбранных видов).
**Документы:** [исследование](UNIVERSAL_ANIMATION_SYSTEM_RESEARCH.md) · [архитектура (DRAFT)](../../architecture/UNIVERSAL_ASSET_ANIMATION_PIPELINE.md) · [методичка DeepSeek](../../agents/UNIVERSAL_TRANSFER_PLAYBOOK.md) · код [`tools/alpha_models/universal`](../../../tools/alpha_models/universal/README.md).

## 0. Маркеры

| Маркер | Значение |
|---|---|
| `COMMON_BINDING_MECHANISM` | **FOUND** — один `Creature_drawModel` + внешность вида (слоты, масштабы, смещения, флаги) + блоки отрисовки |
| `STATIC_APPEARANCE` | **PASS** — 156/156 видов исполнены эмулятором по байтам; кот 0x20 и человек 0x02 совпали с памятью Alpha байт-в-байт (0xAC байт) |
| `STATIC_MODEL_REGISTRY` | **PASS (2548/2557)** — индекс → `.cub`; размеры/вершины совпали с мешами Alpha для 5/5 частей кота, 2/2 бегуна, 4/4 тела человека; 1 конфликт, 1 не декодирован |
| `RIG_RESOLUTION` | **PASS** — статический прогноз частей = нарисованные части в 3075/3075 кадрах трёх видов |
| `HUMANOID_REGRESSION` | **PASS** — универсальный путь воспроизвёл ходьбу/покой S05A-2 бит-в-бит (`max |ΔJ| = 0.0`), Unity-регрессия совпала с эталоном |
| `NONHUMANOID_1` (WhiteCat 0x20, 7 частей, хвост) | **TRANSFERRED_FROM_ALPHA** — ходьба: 932 ключа, 9 циклов, «один цикл отложен» 0.129 вокс. / 1.23° (p95 0.24°); Unity PASS |
| `NONHUMANOID_2` (PlainRunner 0x3f, тело + 2 ноги) | **TRANSFERRED_FROM_ALPHA** — ходьба: 872 ключа, 14 циклов, 0.025 вокс. / 0.70°; Unity PASS |
| `NO_ALPHA_RUN_IMPORT` | **PASS** — Cat 0x1e (клип WhiteCat) и Elf 0x00 (клипы человека) импортированы и проиграны без нового запуска Alpha |
| `FAMILY_EQUALITY_RUNTIME` | **NOT_TESTED** — равенство поз видов одного семейства доказано статически, второй вид семейства в рантайме не снимался |
| `NONHUMANOID_IDLE_ATTACK` | **NOT_CAPTURED** — кот и бегун за 20 с ни разу не стояли и не атаковали |
| `VISUAL_ALPHA_UNITY_CAT` | **NOT_DONE** — в момент съёмки персонаж Alpha спал в доме, кота в кадре не было; доказательство переноса — численное (ROUNDTRIP ≤ 4e-6 к матрицам Alpha) |
| `PYTHON_TESTS` | **138/138 OK** (112 прежних + 26 новых) |
| `UNITY_SELFTEST` | **PASS cases=22** (19 прежних + 3 новых, синтетика) |
| `RUN_TRANSFER_NEGATIVE` | **PASS 10/10** |
| `UNIVERSALITY` | **PARTIAL** — 3 плана тела, 3 семейства, 15 видов готовы; 27 видов в очереди исключений |

## 1. Что сделано

1. **Статический путь (без Alpha).** `x86emu.py` — строгий интерпретатор x86-32 (неподдерживаемая инструкция → ошибка, импорт без заглушки → ошибка). Им исполняются `Spawn_initDefaults` и `creature_generateAppearance` для каждого вида; реестр моделей декодируется строгими шаблонами инструкций. Сборка `Cube.exe` закреплена SHA256.
2. **Универсальный риг.** `creature_rig.py`: 17 блоков отрисовки (9 проверены), правило якоря хвоста `(sx/2, sy, sz/2)` и исключение головы вида 0x65, разрешение рига по внешности, подпись и ID семейства движения, экипировка как слой.
3. **Универсальный захват (подход A).** `pose_capture.py --kinds K1,K2 --per-kind N`: несколько существ разных видов за одну сессию; каждая часть получает слот по равенству указателя меша `self[slot]`; вид и полная внешность пишутся в запись. Старый режим `--target player` не изменился.
4. **Клипы и проверки.** `uclip.py`: часть = блок по адресу **и** совпадение слота; кадр годен только если части = статический прогноз; постоянство внешности; ходьба по фазе с проверкой «один цикл отложен» и прежними порогами (0.25 вокс., 3°); скорость фазы — только по устойчивым кадрам.
5. **Оркестратор.** `pipeline.py`: `scan`, `build-clips`, `prepare-unity` (модели вида + клипы его семейства; чужие клипы и непроверенные блоки → FAIL), `batch` (очередь 156 видов).
6. **Unity** (общий код S05A-2, обратно совместимые правки): `AlphaRig.rigId` и правило `tail`; ID рига из `rig.json`; проверка подтипа оружия — если есть оружие; PlayMode-проба: контракт гуманоида без изменений, для универсальных ригов атака/покой необязательны (`not_available`), проверка «ходьба не застыла» анатомически нейтральна. `run_transfer.ps1 -UniversalJob`.

## 2. Результаты по образцам

### Гуманоид (образец 1, человек 0x02 — регрессия архитектуры)

* Универсальный `build-clips` на захвате S05A-2 `session2.jsonl`: 983 кадра, 982 годных, 216 ключей, 9 циклов, 0.1886 вокс. / 2.374° (p95 0.357°) — числа S05A-2; ключи ходьбы и покоя **совпали бит-в-бит** с эталонными клипами.
* Unity, исходный набор моделей (меч), отдельный проект: SELFTEST 22/22, GEOMETRY 8/8, ROUNDTRIP ≤ 8e-6, SEAM непрерывен, GRIP 0.0002 / 2.30, PlayMode: ходьба 90 кадров (размах стопы 12.69), атака 115 кадров = 1914 мс (путь клинка 197.6), покой 5 кадров, поза = клип (0 / 0°). **Совпадает с эталоном S05A-2.**
* Семейство человека (`alpha_creature/e66d96be5a`) по статике включает 8 видов: ElfM/F, HumanM/F, LizardmanM/F, FrogmanM/F. **Elf 0x00 без запуска Alpha**: модели `body4`, `elf-head-m01`, `elf-hair-m01`, `hand2`, `foot` + меч; GEOMETRY 8/8, ROUNDTRIP ≤ 8e-6, PlayMode PASS (ходьба + покой).

### Негуманоид (образец 2): кошки — WhiteCat 0x20

Выбор обоснован данными: кот рисовался рядом с игроком (виден в прошлых захватах S05A-2 со слотами `[408,-1,409,410,407,411,-1,-1]`), его план тела отличается от гуманоида (нет волос и оружия, **есть хвост** — новый блок с другим якорем; лапы — блоки рук/ног), и он сам ходит под управлением ИИ.

| Что установлено автоматически | Значение |
|---|---|
| `.cub` | `cat-body3`, `cat-head3`, `cat-hand3` (×2, правая зеркальна), `cat-foot3` (×2), `cat-tail3` |
| Частей в отрисовке | 7 (прогноз = факт в 1025/1025 кадрах) |
| Соответствие узлам | body/head/hand_l/hand_r/foot_l/foot_r/tail — по адресу блока и слоту `self[0x31/0x2F/0x33/0x36/0x35]` |
| Параметры | масштаб головы 0.9, хвоста 1.1, наклон хвоста −30°, смещения тела (0,0,−4), головы (0,9,−1), лап (3,4,−8.5), задних лап (3,−4.1,−8.5), хвоста (0,−8,−1), флаги 0x31 |
| Состояния | ходьба (state 0, амплитуда 0.19, без множителя ×1.25 — флаг ИИ `0x124&0x40` не выставлен); покой и атаки не наблюдались |
| Отличия от гуманоида | хвост (якорь у конца модели), лапы как руки/ноги, флаг 0x01 (иная формула качания), нет оружия/волос |
| Семейство | `alpha_creature/ced63e5e96` = Cat 0x1e, BrownCat 0x1f, WhiteCat 0x20 |

Unity WhiteCat: GEOMETRY 7/7 (вершины = Alpha), ROUNDTRIP ≤ 3e-6, SEAM непрерывен, PlayMode 90 кадров, поза = клип (0 / 0°), макс. поворот части 97.7°. **Cat 0x1e без запуска Alpha** (`cat-head` 2728 вершин вместо 2920 у `cat-head3`): то же — PASS.

### Третий план тела: PlainRunner 0x3f (тело + 2 ноги)

Снят в той же пакетной сессии. 1025/1025 кадров годны, 14 циклов, 0.025 вокс. / 0.70°. Unity PASS: GEOMETRY 3/3, ROUNDTRIP ≤ 4e-6. Здесь выявлена **анатомическая зависимость прежней проверки**: в данных самой Alpha стопа бегуна смещается вперёд лишь на 0.76 вокселя, но поворачивается на 75° (высокая модель вращается вокруг центра), поэтому гуманоидный критерий «стопа ≥ 4 вокселя» к нему неприменим. Для гуманоида критерий оставлен без изменений; для универсальных ригов введена проверка «ходьба не застыла» (поворот части ≥ 10° или сдвиг ≥ 1 воксель в реальных кадрах PlayMode; у бегуна 81.9°), при этом требование «поза рантайма = поза клипа» сохранено.

## 3. Масштабируемость (вопрос 7 задания)

См. архитектуру §6. Кратко: 156 видов → 19 планов тела → 106 семейств; клипы есть у 3 семейств → **15 видов AUTO_READY**; 114 видов (80 семейств) ждут по одному захвату на семейство; 27 видов — исключения (крылья 13, `slot_98` 10, видо-зависимый код 3, конфликт модели 1). Число `.cub` (2857) не равно числу анимационных систем: анимационная система одна, параметров движения — 106 наборов, а модели — это сменная геометрия слотов.

## 4. Тесты и команды воспроизведения

```powershell
# статика и очередь (без Alpha)
python -m tools.alpha_models.universal.pipeline scan  --out .scratch\universal\static\alpha_static.json
python -m tools.alpha_models.universal.pipeline batch --static .scratch\universal\static\alpha_static.json --out .scratch\universal\static\queue.json
# захват (локальная копия Alpha в мире)
& tools\alpha_models\runtime\alpha_enter_world.ps1 -ShotDir D:\Projects\CubeGame\.scratch\universal\nav
python -m tools.alpha_models.runtime.pose_capture <pid> .scratch\universal\cap\batch1.jsonl --kinds 0x20,0x3f,0x2 --per-kind 1 --records 100000 --seconds 20
# клипы семейств
python -m tools.alpha_models.universal.pipeline build-clips --capture .scratch\universal\cap\batch1.jsonl --kind 0x20
python -m tools.alpha_models.universal.pipeline build-clips --capture .scratch\universal\cap\batch1.jsonl --kind 0x3f
python -m tools.alpha_models.universal.pipeline build-clips --capture .scratch\s05a2\cap\session2.jsonl --kind 0x2
# Unity (изолированные проекты 6000.3.27f2)
& tools\alpha_models\transfer\run_transfer.ps1 -Project D:\Projects\CubeGame\.scratch\universal\unity\WhiteCat    -UniversalJob tools\alpha_models\universal\jobs\whitecat_0x20.json
& tools\alpha_models\transfer\run_transfer.ps1 -Project D:\Projects\CubeGame\.scratch\universal\unity\Cat1e       -UniversalJob tools\alpha_models\universal\jobs\cat_0x1e.json
& tools\alpha_models\transfer\run_transfer.ps1 -Project D:\Projects\CubeGame\.scratch\universal\unity\PlainRunner -UniversalJob tools\alpha_models\universal\jobs\plainrunner_0x3f.json
& tools\alpha_models\transfer\run_transfer.ps1 -Project D:\Projects\CubeGame\.scratch\universal\unity\ElfMale     -UniversalJob tools\alpha_models\universal\jobs\elf_male_0x00.json
& tools\alpha_models\transfer\run_transfer.ps1 -Project D:\Projects\CubeGame\.scratch\universal\unity\HumanoidRegression
& tools\alpha_models\transfer\run_transfer.ps1 -Test
python -m unittest discover -s tools/alpha_models/tests -t .
```

| Прогон | Результат |
|---|---|
| Python (все) | 138/138 OK |
| `run_transfer -Test` | 10/10 |
| HumanoidRegression | SELFTEST 22/22, TRANSFER PASS, PLAYMODE PASS, exit 0 |
| WhiteCat 0x20 | PASS / PASS / PASS, exit 0 |
| Cat 0x1e (без Alpha) | PASS / PASS / PASS, exit 0 |
| PlainRunner 0x3f | PASS / PASS / PASS, exit 0 (первый прогон — PLAYMODE FAIL `WALK_FEET_STATIC 0.76`, причина и решение — §2) |
| Elf 0x00 (без Alpha) | PASS / PASS / PASS, exit 0 |

Захваты: `.scratch/universal/cap/batch1.jsonl` (SHA256 `d8e20297553d5934…`, 3075 записей, 20 с, игра жива после отсоединения), `survey1.jsonl` (разведка). Alpha запускалась только из `.scratch\alphatest2` (с резервной копией сохранения), `D:\Games\Cube world` — только чтение (сверка SHA256).

## 5. Ограничения

* Проверены только ходьба и покой; атака — только у гуманоида (путь S05A-2). Клипы ходьбы — одна амплитуда (0.19); промежуточные скорости ИИ не воспроизводятся.
* Равенство поз внутри семейства — статическое доказательство (одна функция + равные параметры); рантайм-сверки двух видов одного семейства нет.
* 8 блоков отрисовки не проверены (крылья, `slot_98`, экипировка), 10 вызовов `ECX=esi` не размечены; виды с ними — в очереди исключений.
* Пол сцены в Unity стоит на высоте гуманоида — у кота лапы уходят под пол (только визуальная деталь сцены, на матрицы не влияет).
* Пользовательский Unity Editor был открыт на `.scratch\s05a2\unity\AlphaTransfer`; первый прогон регрессии туда завершился exit 1 без маркеров (корректно распознан как FAIL), регрессия выполнена в отдельном проекте. Подготовка данных при этом обновила скрипты и данные в открытом проекте (обратно совместимые, тот же набор моделей/клипов).
* Имена видов (`name_hint`) — из декомпиляции, только подсказка.
