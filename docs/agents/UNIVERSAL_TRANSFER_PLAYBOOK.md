# Методичка: универсальный перенос моделей и анимаций Alpha → Unity (для DeepSeek)

**Статус технологии:** прототип, `READY_FOR_REVIEW` (не ACCEPTED). Проверено на **трёх планах тела** (гуманоид 8 частей, четвероногое с хвостом 7 частей, «тело + 2 ноги» 3 части), **трёх семействах движения** и **пяти видах** (два из них — без запуска Alpha). **S05B не разрешён**: методичка готовит пакетный перенос, но не запускает его. Массовую обработку не начинать без решения продюсера.
**Читать вместе с:** [ANIMATION_TRANSFER_PLAYBOOK.md](ANIMATION_TRANSFER_PLAYBOOK.md) (гуманоид, атака, вход в мир, захват — шаги A–I остаются в силе), [исследование](../research/alpha2013/UNIVERSAL_ANIMATION_SYSTEM_RESEARCH.md), [архитектура](../architecture/UNIVERSAL_ASSET_ANIMATION_PIPELINE.md), [отчёт прототипа](../research/alpha2013/UNIVERSAL_TRANSFER_PROTOTYPE_REPORT.md).

---

## 0. Что уже решено — не исследовать заново

| Вопрос | Ответ | Где |
|---|---|---|
| Сколько «ригов» в Alpha? | Один: `Creature_drawModel` (0x4128F0) для всех существ | исследование §1 |
| Откуда части существа? | Внешность `creature+0x78` (0xAC байт): 8 слотов моделей; непустой слот → блок(и) отрисовки | `creature_rig.SLOT_BLOCKS` |
| Как получить внешность вида без Alpha? | Эмуляцией байтов `Spawn_initDefaults` (0x428750) + `creature_generateAppearance` (0x43F7C0) | `alpha_static.AppearanceEmulator` |
| Индекс модели → `.cub`? | Реестр из `GameController_load_game_assets` (0x4EE230), 2548 индексов | `alpha_static.model_registry` |
| Какой блок рисует какую часть и с каким якорем? | Таблица `creature_rig.BLOCKS` (адрес, слот, правило, доказательство, статус) | |
| Какие виды делят анимацию? | Одинаковая подпись семейства (`family_id`): план тела + все параметры движения + видо-зависимый код | `creature_rig.family_id` |
| Зависит ли поза от размера модели? | Для 8 блоков тела — нет (размер только в якоре). Оружие: особая модель 0x348, второе оружие, предмет на спине — исключения | исследование §3 |
| Лапы кота — это «руки»? | Да: передние лапы рисует блок `hand_*` (правая зеркальна), задние — `foot_*`. Имена частей = имена **блоков**, а не анатомии | |

**Главные правила (как в S05A-2):** клип — только реальные матрицы, которые Alpha отправила в GPU; `TRANSFERRED_FROM_ALPHA` — только при пройденной проверке повторяемости; пороги не ослаблять; данные Alpha — только в `.scratch`.

## 1. Окружение

Как в основной методичке §1. Дополнительно:

| Что | Путь |
|---|---|
| Сборка `Cube.exe` | SHA256 `84a7a132a84d4282338e7ea45a1940d32066d64e39cbafed8f2418d5a6dc30bf` (закреплена; другая сборка → `UnknownBuild`) |
| Декомпиляция (только имена видов, подсказка) | `.scratch\CubeWorld-Reversal\cube` |
| Данные универсального конвейера | `.scratch\universal\{static,cap,clips,unity,runs}` |
| Задания (в Git, без данных Alpha) | `tools\alpha_models\universal\jobs\*.json` |

Unity: если продюсер держит открытым Editor на каком-то проекте `.scratch`, **не запускать** туда пакетный Unity (exit 1 без маркеров «Exiting without the bug reporter»), а указать другой `-Project`.

## 2. Пошагово

### Шаг 1. Статическая таблица (без Alpha, ~25 с)

```powershell
python -m tools.alpha_models.universal.pipeline scan --out .scratch\universal\static\alpha_static.json
python -m tools.alpha_models.universal.pipeline batch --static .scratch\universal\static\alpha_static.json --out .scratch\universal\static\queue.json
```
`STATIC_SCAN` должен показать `kinds: 156`, `undecoded_index: 0`. `queue.json`: для каждого вида — `AUTO_READY` / `NEEDS_CAPTURE` / `EXCEPTION` и причина. Выбирать следующую работу из `NEEDS_CAPTURE`, начиная с семейств с наибольшим числом видов и самых частых планов тела.

### Шаг 2. Захват семейства (Alpha, локальная копия)

Войти в мир (основная методичка, шаг A). Существо нужного вида должно **рисоваться** (быть рядом с игроком). Захват нескольких видов за одну сессию:

```powershell
$p=(Get-Process Cube).Id
python -m tools.alpha_models.runtime.pose_capture $p .scratch\universal\cap\<имя>.jsonl --kinds 0x13,0x14,0x1c --per-kind 1 --records 100000 --seconds 20
```
Вывод перечисляет **все** увиденные существа с видом и слотами (`creature 0x… kind=0x20 indices=[…] TARGET`) — это и есть разведка «кто рядом». Если нужного вида нет — переместить игрока (основная методичка, ввод) или выбрать другой вид того же семейства (`queue.json → families`). Проверить `CUBE_ALIVE` и что Alpha продолжает работать.

### Шаг 3. Клипы семейства

```powershell
python -m tools.alpha_models.universal.pipeline build-clips --capture .scratch\universal\cap\<имя>.jsonl --kind 0x20
```
Обязательно: `BUILD_RESULT=PASS`, `usable == frames` (иначе смотреть `frame_problems`), `roundtrip_max_abs` ~1e-14, ходьба `holdout` ≤ 0.25 вокселя и ≤ 3°, `cycles ≥ 3`. Клипы пишутся в `.scratch\universal\clips\alpha_creature_<hash>\`. Если `walk.status = NOT_CAPTURED` — существо не ходило устойчиво; снять дольше. Покой пишется, только если существо стояло (`amp = 0`).

### Шаг 4. Unity: снятый вид и виды семейства без запуска Alpha

Создать задание (пример — `jobs/cat_0x1e.json`):
```json
{"kind": "0x1e", "seed_variant": [0, 0], "variant": "cat_0x1e_no_alpha_run"}
```
Для гуманоидных семейств добавить слой оружия: `"equipment": {"weapon": {"model": "iron-greatsword.cub", "subtype": 15}}` (только подтипы 15/16/17).
```powershell
& tools\alpha_models\transfer\run_transfer.ps1 -Project D:\Projects\CubeGame\.scratch\universal\unity\<Имя> -UniversalJob tools\alpha_models\universal\jobs\<задание>.json
```
Итог только по файлам `<Project>\TransferOutput` и коду выхода: `SELFTEST_RESULT=PASS cases=22`, `TRANSFER_RESULT=PASS` (GEOMETRY N/N, ROUNDTRIP ≤ 1e-3, SEAM CONTINUOUS), `PLAYMODE_RESULT=PASS` (`runtime_pose_max_pos_err=0`, `max_part_rot_deg` ≥ 10 или `max_part_move_vox` ≥ 1), `RESULT=PASS`. Варианты моделей вида: `seed_variant` `[s2, s3]` (голова/волосы и т.п.; диапазоны — `alpha_static.json → kinds → slot_variants`).

### Шаг 5. Регрессия и отчёт

* Регрессия гуманоида (обязательно после любых правок `transfer/unity`): `run_transfer.ps1 -Project .scratch\universal\unity\HumanoidRegression` — числа должны совпасть с эталоном S05A-2 (ходьба 90 кадров / 12.69, атака 115 кадров / 1914 мс / 197.6, GRIP 0.0002 / 2.30).
* `python -m unittest discover -s tools/alpha_models/tests -t .` — всё OK; `run_transfer.ps1 -Test` — 10/10.
* Отчёт: для каждого семейства — виды, части, клипы, `holdout`, маркеры Unity, что не снято. `git status` — ничего из `.scratch`. Статус — `READY_FOR_REVIEW`.

## 3. Как добавить новый блок отрисовки (крылья, slot_98, экипировка)

Это **исследование**, не масштабирование:
1. Захватить вид с этим блоком (`--kinds`). Части с непроверенным блоком отклоняются (`UNVERIFIED_BLOCK`) — это правильно; сырой захват всё равно сохраняет `draw_site`, `slot`, матрицу, размер меша.
2. Найти блок в реконструкции `drawModel` (между соседними проверенными блоками по адресу) и прочитать последнюю цепочку `translate(−size·k)` перед `render_uploadModelViewMatrices` — это правило якоря. Проверить скриптом, что размер модели не используется **внутри** цепочки (как в исследовании §3).
3. Добавить правило в `creature_rig.anchor` и `AlphaRig.Anchor` (+ самотест Unity и тест Python), строку блока — со статусом `VERIFIED` и ссылкой на доказательство.
4. Повторить шаги 3–5. Согласовать с архитектором до объявления блока проверенным.

## 4. Типичные ошибки

| Симптом | Причина | Что делать |
|---|---|---|
| `SLOT_MISMATCH` | меш части не равен ожидаемому слоту (экипировка перекрыла слот или блок размечен неверно) | не исправлять подменой; разобрать, какой слот реально рисуется (`this_slots_hdr` в записи) |
| `UNKNOWN_DRAW_SITE 0x41F8BD` и т.п. | один из 10 блоков `ECX=esi` (спецмодели) | исследование (§3) |
| `usable < frames`, `part set` | нарисовано не то, что предсказано внешностью (экипировка, особое состояние) | смотреть `frame_problems`; оружие учитывается автоматически как слой |
| `APPEARANCE_CHANGED_DURING_CAPTURE` | существо сменило внешность (другая особь, смерть/возрождение) | снять одну особь: `--creature 0x…` |
| `WALK_NOT_REPEATABLE` | поза не функция фазы: смена амплитуды/ускорения, переходы | снять дольше; не ослаблять порог |
| `NO_FAMILY_CLIPS` | у семейства нет клипов (или они другого семейства) | шаг 2–3 для любого вида этого семейства |
| `KIND_SPECIFIC_CODE` | Mole/Zombie/Dragon: в `drawModel` есть ветви по виду | отдельный адаптер, согласовать |
| `MODEL_INDEX_UNRESOLVED` / конфликт | индекс 29 или 1300 | выбрать другой `seed_variant` или исследовать загрузку |
| Unity exit 1 сразу, нет маркеров | проект открыт в интерактивном Unity | другой `-Project` |
| `UnknownBuild` | другой `Cube.exe` | не применять адреса; сообщить архитектору |

## 5. Статусы (не смешивать)

* `TRANSFERRED_FROM_ALPHA` — клип из реальных матриц Alpha, повторяемость подтверждена. Применение клипа к другому виду **того же семейства** — тоже перенос (позы те же по коду), но в отчёте указывать «вид-источник» (`rig_kind_source`) и что вид-получатель не снимался.
* `RECONSTRUCTED_FROM_EVIDENCE` — будущие клипы из восстановленной формулы (подход B), только после сверки с захватом.
* `ORIGINAL_NOT_ALPHA` — собственные движения; универсальный конвейер их не создаёт.
* `BLOCKED` — нет захвата/нет доказательства; писать причину.
* Синтетические данные тестов — `TEST_DATA_NOT_ALPHA`, отдельно от фактических результатов.

## 6. Что делать следующим (порядок для DeepSeek)

1. **Покой и атаки негуманоидов**: снять кота/бегуна дольше (≥ 60 с), найти момент `amp = 0` (покой) и атаки ИИ (`state ≠ 0`); добавить построитель клипа по времени в `uclip` по образцу `alpha_clip.build_time_clip`.
2. **Захват по семействам** из `queue.json` (`NEEDS_CAPTURE`): сначала многовидовые (Collie/ShepherdDog; Anubis/Horus/Jester/SpikeCreature; Wizard/Gnobold/Spectrino; гномы; слизни), затем самые частые планы тела. Один вид на семейство.
3. **Крылья** (`slot_9a`, 13 видов) и **`slot_98`** (10 видов): по одному захвату + правило якоря (§3).
4. **Проверка семейств в рантайме**: при случае снять два разных вида одного семейства (например, Cat 0x1e и WhiteCat 0x20) и сравнить `validate_against` — сейчас равенство доказано только статически.
5. Только после 1–4 и решения продюсера — пакетный режим по очереди.
