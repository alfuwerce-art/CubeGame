# D61 TECHNICAL-FIX — COLOR/TINT-FIX, S05B-A REVIEW-FIX-01, сцена просмотра (отчёт)

**Статус: `READY_FOR_REVIEW`** (не ACCEPTED). **Решение:** [D61](../decisions/D61-mass-transfer-visual-approval.md) §2, [handoff](../agents/S05B2_CLAUDE_DEEPSEEK_HANDOFF.md) шаг 1.
**Дата:** 2026-11-06. **Исполнитель:** Claude Code. **Ветка:** `claude/d61-tint-review-fix` (от `review/d61-scale-transfer-color-fix` @ `dc23e04`).
S05B-2 не запускался, S04 и `main` не менялись, ассеты/кадры Alpha не коммитились (всё изображения — локально в `.scratch`).

## 0. Итог

| Задача | Результат |
|---|---|
| COLOR/TINT-FIX | **PASS**: правило маркеров Alpha восстановлено и доказано тремя независимыми способами; красные «маски», маркерные глаза и пятна исчезли; цвет волос — множитель `hair_rgb` экземпляра (как в Alpha) |
| REVIEW-FIX-01 | **PASS**: типизированный `equipment_observed`, полный `evidence_drift` по всем seed и наблюдённому состоянию, SHA256-кэш библиотек с проверкой payload |
| Сцена просмотра | **PASS**: адаптивная раскладка без наложений (проверено на 12 размерах × 2 режима), причина `ArgumentOutOfRangeException` устранена, PS 5.1 — 10/10 скриптов, реальный PlayMode-обход 0 ошибок |
| Регрессии | **PASS**: S05B-1 76/76 (охват B 105/105), S05B-A 20/20, HumanMale 98/98 ×3 с прежними числами, Python 294/294 |

## 1. COLOR/TINT-FIX — что доказано

### 1.1 Правило маркеров (VERIFIED_ALPHA_CODE)

1. **Флаг загрузки.** Каждая модель грузится `VoxelModel_load_from_file_or_buffer` (0x4E7290, `thiscall(src, size, flag)`); флаг пишется в `model+0x55` и передаётся в `VoxelModel_set_data` (0x4E7650) как `noRemap`. Флаг извлекается из байтов `Cube.exe` (`push 0/1` в каждом из 2557 мест вызова загрузчика ресурсов): 2552 распознаны, 5 нераспознанных — объекты мира (сфинкс, обелиск…), не существа; **0 расхождений** с декомпиляцией на 1103 записях; ни одна модель не грузится с разными флагами. **Флаг 0**: все `*-hair-*`, `*-eyes`, `*-random` предметы (489 моделей). **Флаг 1**: головы, тела, кисти, стопы, коты, бегуны и т. д.
2. **Прозрачность.** `VoxelModel_build_mesh` (0x4E7870) пропускает воксель, если `match_transparent_color(voxel, model+0x55)` (0x4E71D0) истинно. **Исполнено на оригинальных байтах** (`x86emu`): флаг 1 → прозрачен только (0,0,0); флаг 0 → (0,0,0) и ровно (255,0,0), (0,255,0), (0,0,255). (254,0,0), (200,0,0), (255,0,1), белый и т. п. — не маркеры.
3. **Грани.** Соседи проверяются всегда строго (только (0,0,0) или вне сетки), поэтому грань рядом с маркером **не** рисуется: маркер невидим, но не «дыра».
4. **Сверка с живым мешем.** Живой захват игрока (`session2.jsonl`): у меша волос `model+0x55 = 0`, списки маркеров `298 / 4 / 10` = R/G/B `human-hair-m01`, вершин в Alpha **2244**. Правило даёт **2244** (прежнее «все не чёрные» — 4296). Это закрывает открытый вопрос §7 [ALPHA_ANIMATION_TRANSFER](../research/alpha2013/ALPHA_ANIMATION_TRANSFER.md) («меш волос 2244 не совпал»).

### 1.2 Цвет волос — механизм и шейдер (VERIFIED_ALPHA_CODE)

* **Загрузка цвета.** `Creature_drawModel` перед `draw_mesh` волос (0x420A38) вызывает `render_setUniform30(colour)` (0x448280); дизассемблировано: `colour = byte[creature+0x7A..0x7C]/255` (внешность = `creature+0x78`, `hair_rgb` = raw[2:5]); если `creature+0x7E & 0x400 == 0`, цвет умножается на цвет существа (`param_6`: (1,1,1,1) × эффекты состояния).
* **Шейдер извлечён.** Шейдеры D3D9 (SM3, байткод с таблицами `CTAB`) вшиты в `Cube.exe` — 15 штук. Инициализация рендера (0x447E10) создаёт вершинный шейдер мира из `0x6FD928` и записывает `mov [esi+0x30], 0x20`; `render_setUniform30` делает `push [ecx+0x30]` и `call [edx+0x178]` (`SetVertexShaderConstantF`). В `CTAB` этого шейдера регистр **c32 = `materialColor`**. Дизассемблирование (`d3dcompiler_47`): `o1.rgb = L·vertex.rgb·materialColor.rgb + darknessColor·(1−L)`, затем `lerp(…, 1, white)` (`white` = c39 — вспышка; L — освещение, ограниченное 0…1.2). **Цвет волос умножает цвет вершины** — проверено на байтах тестом `test_uniform30_is_vs_c32_material_colour_and_multiplies`. Сам байткод не коммитится (локально `.scratch/d61/shaders`).

### 1.2a Цвет волос NPC — правило сервера (VERIFIED_ALPHA_CODE)

* `Server.exe` (SHA256 = копии в `D:\Games\Cube world`) при спавне NPC вызывает `generate_entity_appearance` (0x40A840) с `seed = NULL`; эта ветка делает **ровно 5 вызовов `rand()`** (IAT 0x5582F4): `hair = (r3, r2, r1) & 0xFF` → `seed+0x10..0x12`, варианты `s2 = r4 % 100`, `s3 = r5 % 100`. Подтверждено: дизассемблирование + **исполнение байтов сервера** с подставленным `rand()` для 15 видов; внешность сервера = внешности клиента при тех же seed — **60/60**.
* **Какие виды берут цвет спавна** — исполнением `creature_generateAppearance` клиента с двумя пробными цветами (`voxel_markers.hair_rule`):

| Вид | Правило волос |
|---|---|
| HumanMale 0x02, HumanFemale 0x03, ElfMale 0x00, ElfFemale 0x01, LizardmanMale 0x07, LizardmanFemale 0x08 | `RANDOM_PER_NPC`: цвет случайный у каждого NPC (3 байта `rand()` MSVC) |
| FrogmanMale 0x0D, FrogmanFemale 0x0E | `FIXED_BY_KIND` (255,255,255): клиент не копирует цвет спавна — волосы ровно в цветах модели |
| Cat 0x1E/0x1F/0x20, Runner 0x3F–0x42 | блока волос нет |

* Значит, «одного цвета волос вида» у людей, эльфов и ящеров в Alpha **нет** — это случайное число каждого NPC. Сцена показывает: живые NPC этого вида (из захватов), NPC по правилу сервера (поток `rand()` MSVC с `srand(1)`, номер NPC и вариант `s2-s3`), живые цвета других видов, ×1.
* Сверка с живыми NPC (все 6 из `humans_full.jsonl`): для каждого существуют состояния ГСЧ, дающие одновременно его цвет и его модели головы/волос (6–22 состояния). Противоречий нет; как доказательство это слабо (случайных совпадений ожидается 3–7), доказательством служит исполнение кода сервера и клиента.
* Визуальная сверка: `.scratch/s05a2/visual/alpha_attack_sheet.png` — волосы игрока в Alpha золотые без красного (серо-белые воксели × (255,200,50)).

### 1.3 Реализация (общий конвейер — его использует и DeepSeek)

| Файл | Изменение |
|---|---|
| [voxel_markers.py](../../tools/alpha_models/universal/voxel_markers.py) | новый: `transparent`/`transparent_by_alpha_code` (исполнение 0x4E71D0), `mesh_stats` (вершины по правилу Alpha), `load_flags` (флаги из байтов загрузчика), `part_colour` (tint как параметр экземпляра) |
| [pipeline.py](../../tools/alpha_models/universal/pipeline.py) | `prepare_unity`: у каждой части `loadFlag`, `loadFlagStatus`, `markerTransparent`, `markers`, `expectedVertices` по правилу Alpha, `tint`/`tintMode`/`tintSource`; модель вне загрузчика существ **с** маркерами → `MODEL_LOAD_FLAG_UNKNOWN` (блокер), без маркеров — флаг не важен; цвет волос — поле задания `hair_rgb`, иначе ×1 с пометкой `NOT_SET` |
| [VoxelMesh.cs](../../tools/alpha_models/transfer/unity/Runtime/VoxelMesh.cs) | `Build(bytes, name, markerTransparent)` — то же правило |
| [VoxelShaded.shader](../../tools/alpha_models/transfer/unity/Runtime/VoxelShaded.shader) | `_Tint` — множитель цвета (аналог `uniform30`) |
| [AlphaRig.cs](../../tools/alpha_models/transfer/unity/Runtime/AlphaRig.cs) | поля части (`loadFlag`, `markerTransparent`, `tint`…), сериализуемые `tints` + `SetTint` (property block не сохраняется в сцене) |
| [AlphaTransferEditor.cs](../../tools/alpha_models/transfer/unity/Editor/AlphaTransferEditor.cs) | сборка рига применяет правило и tint |

Охват 72 образцов: у 66 частей есть чистые R/G/B; **65** (все — волосы, флаг 0) теперь скрыты по правилу Alpha; **1** (`elf-head-m03`, флаг 1) — настоящие цвета модели, остаются видимыми, как в Alpha. У котов и бегунов изменений нет (флаг 1: их красный гребень, синие перья и т. п. — настоящие цвета).

### 1.4 До / после (локально, не публикуется — D54)

* `D:\Projects\CubeGame\.scratch\d61\unity\S05B_VisualReview\ReviewOutput\colour_before_after.png` — 15 видов, крупно голова: верхний ряд до (маркеры нарисованы), нижний — после (правило Alpha + `hair_rgb`).
* «До» целиком: `.scratch\d61\before\*.png` (прежние листы сцены), `.scratch\universal\batch\s05b1\visuals`, `…\s05ba\visuals`; «после»: `.scratch\d61\unity\S05B_VisualReview\ReviewOutput\*.png`, `.scratch\d61\batch\*\visuals`.
* В самой сцене: вкладка «Части/цвет» → «правило маркеров Alpha» (клавиша **M**) переключает до/после на любом образце; **C** — перебор цветов волос из живых наблюдений.

Наблюдения: исчезли красные лица/маски и зелёно-синие «глаза» у людей, эльфов, ящеров, лягушек (это были маркеры модели волос, перекрывавшие настоящее лицо головы); у ящеров исчез красный гребень-маркер; лица, глаза, бороды теперь из моделей головы/волос.

### 1.5 Перекрытие геометрии / z-fighting

Волосы и голова рисуются одной матрицей в одной сетке: в 58 из 65 образцов с волосами есть 5–6 **совпадающих видимых граней** волос и головы (одна плоскость, одна клетка). В Alpha они тоже совпадают, но разрешаются детерминированно: голова (0x4206C2) рисуется раньше волос (0x420A38), а мир рисуется с `D3DRS_ZFUNC = LESSEQUAL` (явная установка 0x4AC364) → побеждают волосы. Исправлено: волосы рисуются отдельным материалом с очередью 2001 (`AlphaRig.HairRenderQueue`) при ZTest `LEqual` — тот же детерминированный исход, что в Alpha; в общем конвейере (`AlphaTransferEditor.BuildRig`) и в сцене просмотра.

## 2. S05B-A REVIEW-FIX-01

1. **`equipment_observed`** — прежний `"observed" in e["equipment"]` давал `True` всем 14 профилям (текст «…not observed»). Теперь П1 пишет явный boolean (`ai_trigger.equipment_observed`: только при полном блоке экипировки `OBSERVED_FIELDS`), контракт принимает **только** `bool`; запись без него не создаёт профиль (действия вида → BLOCKED, причина в `contract.EVIDENCE_PROBLEMS`). Файл доказательств пересобран воспроизводимо (`batch_runner.rebuild_npc_evidence`): все прежние поля совпали байт-в-байт по смыслу, добавлены `equipment_observed` (**True только у 0x03**, как и сказано в отчёте части B) и `observed_source` (захват, SHA256, селектор записи).
2. **`evidence_drift`** — раньше повторял П1 только для seed (0,0) и состояния по умолчанию, сравнивал лишь набор действий. Теперь `ai_trigger.evidence_drift_kind` повторяет П1 **по всем `seeds_checked`** и на **заявленном наблюдённом состоянии** (захват загружается по селектору с проверкой SHA256; нет файла / другой SHA → дрейф) и сравнивает поля `status, family_id, appearance_flags_7e, seeds_checked, launched_actions, condition_examples, observed_state, equipment_observed, equipment, reason`.
3. **Кэш библиотек** — раньше повторно использовался при `BAKE_RESULT=PASS` + совпадении профиля/набора действий. Теперь ключ `cache_key.json` = SHA256 exe, записи профиля, файла доказательств, кода П2 (`action_baker, alpha_oracle, drawmodel_emu, x86emu, creature_rig, alpha_static, contract`) и запроса; плюс SHA256 **каждого клипа** сверяется с записанным в манифесте. Любое расхождение → каталог библиотеки очищается и запекается заново; причина в `entry.library_cache`.

Тесты: [test_review_fix_01.py](../../tools/alpha_models/tests/test_review_fix_01.py) (11), +3 теста кэша в [test_batch_actions.py](../../tools/alpha_models/tests/test_batch_actions.py).

## 3. Сцена просмотра

* **Раскладка:** одна панель с вкладками (Вид / Анимация / Части-цвет / Проверки / Справка) + строка статуса + шкала времени; `ReviewLayout` гарантирует отсутствие наложений и выхода за экран; при узком окне шкала уходит под панель, панель сворачивается (**Tab**). Проверено в EditMode на 480×270 … 3840×2160 и 600×900 (портрет), панель открыта/закрыта — 0 проблем.
* **`ArgumentOutOfRangeException`:** по стеку — `UnityEditor.Search.SearchDatabase.EnumerateAll ← GetDefaultSearchDatabase ← SearchInit.IndexationOnStartup` (индексатор Unity Search в новом проекте), не код сцены. Устранено в причине: `setup_review.ps1` выключает `indexOnEditorStartup` в `UserSettings/Search.settings` изолированного проекта; фильтр «не считать» из PlayMode-проверки удалён — считаются **все** ошибки. После исправления в журналах Build/Check/PlayMode исключения нет.
* **PowerShell 5.1:** без BOM 5.1 читает UTF-8 как ANSI; тире `—` (E2 80 94) становится `”` и ломает строку (`open_review.ps1` не разбирался: «The string is missing the terminator»). Все 8 скриптов `tools/alpha_models` переведены в ASCII; новый [check_ps51.ps1](../../tools/alpha_models/review/check_ps51.ps1) разбирает каждый скрипт парсером 5.1 и запрещает не-ASCII без BOM — **10/10 PASS** (5.1.26100), шаг включён в `setup_review.ps1`.
* **Реальный PlayMode:** обход настоящего интерфейса — все 72 образца × все вкладки × режимы × источники, перебор цветов волос, переключение правила маркеров (вершин 9224 → 8364 у 0x02 s0-0), ряд из 15.

## 4. Чужие незакоммиченные изменения (сохранены)

До начала работы в рабочем каталоге были: `open_review.ps1` (добавлен UTF-8 BOM — локальное исправление кодировки), `CharacterPilotGripAttack.cs` (только перевод строк), неотслеживаемый `Assets/CubeGame/Core/Domain.meta`. Сохранены в `git stash` («foreign uncommitted before D61 tint fix…») и копией `.scratch/d61/foreign/` (patch + файл). Исправление BOM заменено ASCII-версией скрипта (работает в 5.1 и 7, проверено `check_ps51.ps1`).

## 5. Регрессии

Все прогоны выполнены заново после всех изменений (новые каталоги `.scratch/d61/batch/*`; прежние результаты сохранены как «до»).

| Проверка | Результат |
|---|---|
| S05B-1, 15 видов (`--authorization D59 --scope s05b1`) | **`BATCH_RESULT=PASS` 76/76**, охват B **105/105** моделей вариантов (эльфы 14+16, люди 23+15, ящеры 8+11, лягушки 10+8; коты/бегуны — один вид модели) |
| S05B-A, 14 профилей NPC (`--authorization D60 --scope s05ba`) | **`BATCH_RESULT=PASS` 20/20**, все действия каждого вида выполнены; `evidence_drift` (полный, все seed) — без дрейфа; библиотеки перезапечены с `cache_key.json` |
| Регрессия HumanMale (в обоих пакетах) | **98/98** при 30 / 60 / 120 Гц, кадров 18 023 / 35 927 / 71 585 — **те же числа**, что до исправлений; ошибка позы 0 / 0° |
| Сцена просмотра (`setup_review.ps1`, основной проект) | данные 72/72, PS 5.1 10/10, Build PASS, Check PASS (362 источника, 48 445 поз, 0 проблем, раскладка 12 размеров × 2), PlayMode PASS (0 ошибок, `ArgumentOutOfRangeException` отсутствует) |
| Python | **294/294 OK** (новые: `test_voxel_markers` 15, `test_review_fix_01` 11, кэш +3) |


## 6. Оставшиеся ограничения

Три прежних блокера закрыты:
* ~~пиксельный шейдер не извлечён~~ → шейдеры извлечены из `Cube.exe`, умножение подтверждено (§1.2);
* ~~живые цвета только у HumanMale/HumanFemale~~ → правило для всех 15 видов определено исполнением кода (§1.2a); у лягушек цвет фиксирован, у остальных гуманоидов он случаен по определению;
* ~~правило сервера не найдено~~ → `generate_entity_appearance` 0x40A840, исполнено (§1.2a).

Остаётся (не блокеры):
* Начальное состояние `rand()` сервера в конкретной сессии (вызов `srand`) не восстанавливалось — для показа используется поток с `srand(1)`; конкретный живой NPC воспроизводится только по захвату.
* Освещение Alpha (`L`, `darknessColor`, туман) в сцене просмотра не воспроизводится — нейтральный свет; сравнение цвета — в пределах множителя `materialColor`.
* Базовый цвет существа (`param_6`: заморозка, урон, эффекты) — нейтральный (1,1,1,1).
* `elf-head-m03` содержит чистые R/G/B при флаге 1 — по правилу Alpha это настоящие цвета (рисуются).
* Прежние ограничения S05B-1/S05B-A сохраняются (live_check `NOT_CAPTURED`, экипировка NPC наблюдалась только у 0x03, скорость ходьбы `PLACEHOLDER_NOT_ALPHA`, переходы `UNITY_TRANSITION`).

## 7. Воспроизведение

```
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools\alpha_models\review\setup_review.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File tools\alpha_models\review\open_review.ps1 -Play
python -m unittest discover -s tools/alpha_models/tests -t .
python -m tools.alpha_models.universal.transfer_planner batch --authorization D59 --scope s05b1 --out-dir .scratch/d61/batch/s05b1
python -m tools.alpha_models.universal.transfer_planner batch --authorization D60 --scope s05ba --out-dir .scratch/d61/batch/s05ba
```

## 8. Для DeepSeek (S05B-2, после решения продюсера)

* Правило маркеров и флаг загрузки применяются автоматически в `prepare_unity` для любого вида; `MODEL_LOAD_FLAG_UNKNOWN` — блокер вида (модель с маркерами вне загрузчика существ), не подменять.
* Цвет волос: `prepare_unity` сам определяет правило вида (`voxel_markers.hair_rule`). `FIXED_BY_KIND` применяется автоматически; для `RANDOM_PER_NPC` поле задания `"hair_rgb": [r,g,b]` — живой образец или `voxel_markers.server_npc_draw(msvc_rand_stream(state))`; без него — `NOT_SET` (×1), это не ошибка. Не выбирать «цвет вида» — его нет.
* `library_cache` в записи пакета показывает, почему библиотека переиспользована/перезапечена; `evidence_drift` теперь длится ~2 мин на 14 видов.
