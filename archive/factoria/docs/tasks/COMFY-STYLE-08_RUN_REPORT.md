# COMFY-STYLE-08 — Race Factory & Character Editor Audit

**14 ноября 2026 · READY_FOR_OWNER_REVIEW**

Исходное ТЗ — `CubeGame_Character_Factory_Style_and_Editor_Spec_V1.md`, редакция от 14 ноября, предоставленная владельцем в чате. База Factoria — `2935b526`. Сверка CubeGame выполнена с `integration/m0-all` на `1b75b27e` и [Alpha parity backlog](https://github.com/alfuwerce-art/CubeGame/blob/1b75b27eb3f442528281a03f16eae56a6bce47c2/docs/planning/M0_ALPHA_PARITY_BACKLOG.md).

## A. Сохранённый стиль

Архивный Human PNG — `reference_controlnet_00006_.png`. Копия в папке референсов владельца совпала с PNG из ComfyUI побайтно. Связь с идентификатором ТЗ `20261114-120058` остаётся `UNKNOWN`. Обе формы графа получены из метаданных архива и сохранены рядом с [происхождением](../../workflows/race_factory_v1/human_v1_provenance.json). Графы COMFY-STYLE-07 остались отдельными файлами.

| Параметр | Human из архива | Восемь кадров COMFY-STYLE-08 |
|---|---|---|
| Checkpoint | DreamShaper XL Turbo v2.1 | тот же |
| LoRA | `ral-vxl.safetensors`, model/CLIP 0.6 | та же, по указанию владельца |
| Sampling | seed 20261114, 28 steps, CFG 5.0, `dpmpp_2m` / `normal`, denoise 1.0 | тот же |
| Холст | 768×1024, batch 1 | тот же |
| Референс | ControlNet Union SDXL Canny 0.9, пороги 0.35/0.75, интервал 0–1 | Canny 0.6, остальные параметры совпадают |
| Статус | визуальный кандидат | локальные контрольные кадры для оценки владельцем |

Единый [API-граф](../../workflows/race_factory_v1/race_template.api.json) подставляет отдельно анатомию, внешний вид и подготовленный референс. Общий блок задаёт фронтальный ракурс, кубическую геометрию и нейтральный фон. [COMFY-STYLE-07](COMFY-STYLE-07_RUN_REPORT.md) содержит сравнение IPAdapter, Canny, img2img и совмещённого режима. Здесь один граф и одна сила Canny для сопоставимости рас. Контрольные кадры `ral-vxl` подпадают под `EXPERIMENTAL / LICENSE_BLOCKED_FOR_PRODUCTION` по [FACT-D21](../decisions/FACT-D21-civitai-voxel-lora-license.md), автор RalFinger. Условия FACT-D17 по визуальной приёмке и FACT-D19 по переходу к 3D действуют для следующего этапа.

## B. Что подтверждено в редакторе Alpha

Сборка `Cube.exe` SHA-256 `84a7a132…dc30bf`, `data1.db` `0e482f6d…639b9d`. [Матрица JSON](COMFY-STYLE-08_ALPHA_MATRIX.json) содержит все 16 сочетаний, каждый индекс опции, ID загрузчика, абсолютный VA вызова, SQL-ключ и измеренные размеры модели. Пути вида `data1.db/blobs/<name>.cub` означают ключ таблицы SQLite. Позиции опций в UI показываются с 1, индексы в памяти и сейве начинаются с 0.

| Race ID | Alpha | Style M / F | Face M / F | Haircut M / F | Связь с карточкой |
|---|---|---|---|---|---|
| 0 | Human | `02 / 03` | 6 / 6 | 15 / 7 | Human |
| 1 | Elf | `00 / 01` | 4 / 6 | 10 / 10 | Elf |
| 2 | Dwarf | `09 / 0A` | 5 / 5 | 3 / 5 | Dwarf |
| 3 | Orc | `0B / 0C` | 5 / 5 | 10 / 4 | Orc. Три мужские позиции без записи в DB |
| 4 | Goblin | `04 / 05` | 5 / 5 | 6 / 6 | Goblin |
| 5 | Lizard | `07 / 08` | 2 / 5 | 6 / 6 | сравнение для Dragonkin |
| 6 | Undead | `0F / 10` | 6 / 6 | 6 / 6 | отдельная игровая раса Alpha |
| 7 | Frogman | `0D / 0E` | 5 / 4 | 5 / 4 | дополнительная раса Alpha |

| Параметр | Код, данные и ограничение |
|---|---|
| Race, Gender, Class | `drawCharacterAppearancePanel` `0x428E40` читает Race `+0x18C`, Gender `+0x194` (Male 0 / Female 1) и Class `+0x190` (Warrior 0, Ranger 1, Mage 2, Rogue 3). `CharCreation_refresh_model` `0x42C080` пишет kind `baseStyle[race]+gender` в `Creature+0x64` и class+1 в `Creature+0x140`. Стартовое оснащение зависит от класса через `0x47742E`. Ограничителей класса в выборе race/sex и в lookup внешности не найдено. Статус `ALPHA_DECOMPILED`. |
| Face, Haircut | `0x42C080` использует два независимых диапазона для kind. Индексы `+0x198/+0x19C`: <0 переходит к последней позиции, ≥ count переходит к 0. При изменении race/gender этот код перенормирует индексы по новому диапазону. Face выбирает целую голову, Haircut — второй воксельный блок. Lizard разделяет набор Haircut между полами. Домен проверен по switch и каталогу. |
| Волосы, бороды и головные уборы | `Haircut` подставляет один overlay. Сравнительная проекция `dwarf-hair-m01.cub` показывает геометрию ниже лица. Роль отдельных вариантов как Beard/Cap/Headband в меню Alpha не подтверждена. Три слота Orc male `0x525…0x527` используют loader VA `0x4F1EC5/0x4F1F09/0x4F1F4D`, но соответствующих `orc-hair-m08…10.cub` нет в проверенной DB и на диске установки. Результат выбора этих трёх слотов в оригинальном UI — `UNKNOWN`. |
| Hair color | `0x428E40` задаёт 16 базовых RGB и семь hit-tested рядов оттенков (три тёмных, базовый, три светлых). В матрице 112 расчётных RGB с усечением float→byte; default `FFC832` подтверждён сейвом. `0x42C080` передаёт RGB в appearance, D61 проследил его применение к блоку hair. Отдельные каналы Beard/Skin/Scales и их варианты остаются `UNKNOWN` за пределами наблюдаемого UI. |
| Глаза и лицо | В проверенных первых Face обе видимые радужки и окружающие детали лежат в RGB-вокселях `*-head*.cub`. Например первый Human Male имеет синие воксели `#003290/#003BA7` в области глаз, Elf Male — фиолетовые. Заголовок .cub содержит размеры и затем RGB по сетке. Отдельный eye mesh, UV-текстура, палитра или сохранённый eye ID для героя в исследованном пути не обнаружены. Глазные модели монстров проходят другим путём. |
| Уши, рога, гребни, хвост | Проекции Elf/Goblin/Orc показывают уши внутри головы. У Lizard `0x07/0x08` есть head, hair, body, hand, foot, а tail ID `-1`. У Dragon `0x65` другой actor, в том числе `dragon-tail.cub`. Отдельный настраиваемый слот ушей, рогов или гребня героев в Alpha не установлен. |
| Эффекты | Skeleton `0x11` имеет два цветовых кластера в `skeleton-eyes.cub` `0x149` (RGB-модель из 20 непустых вокселей, включая цвет-маркер) в overlay-поле hair. Флаги `0x428` в восстановленном блоке внешности, ветвь `& 0x400` в `Creature_drawModel` `0x4128F0`. Это конфигурация существа. Выбор эффекта и его палитры для рас героя в Alpha не установлен. `Particles 1` относится к предоставленному референсу Enderman. |
| Текстуры и локализация | Прямой запрос по расширениям в DB дал 2857 `.cub` в `data1.db`, 104 PNG-ключа интерфейса в `data3.db`, два XML-словаря в `data4.db`. `resource1.dat` используется при рисовании текста панели. Полный аудит материалов существ вне пути редактора остаётся `PARTIAL`. |
| Сериализация | `GameWorld_serialize_state` `0x44D790` и `Creature::deserializeState` `0x44BE40` читают/пишут appearance payload `0x14`: `+0x00 u32 race`, `+0x04 u8 gender`, `+0x08 i32 Face`, `+0x0C i32 Haircut`, `+0x10…12 RGB`. Отдельные class/spec байты идут перед экипировкой (`0x34/35` в распакованном потоке). Проверка read-only `Save/characters.db` → `blobs['0']` → zlib дала 63 753 Б, блок на `0xE7F`, Human/Male/Face 0/Haircut 0/`FFC832`, Warrior, тег версии 11. Семантика других вложенных списков appearance остаётся `PARTIAL`. |

Воспроизводимость: сопоставить `0x42C080` с `SPECIES_RANGES` в `tools/alpha_content/gen_character_models.py` из CubeGame. Построить каталог из локальной Alpha через `tools/alpha_models/catalog/asset_catalog.py --exe <Cube.exe> --db <data1.db> --out-dir <local>`. Получить kind slots через `tools/alpha_models/universal/alpha_static.py --exe <Cube.exe> --db <data1.db> --decomp <Reversal/cube> --out <local.json>`. Открывать SQLite в `mode=ro&immutable=1`, запрашивать `SELECT value FROM blobs WHERE key=?` и разбирать значение `tools.alpha_models.cub_format.parse_stored`. Для повторения измерения save взять длину имени по `0x3E + 13*0x118`, пропустить длину и имя, затем читать `<IB3xII3Bx`. Исходные модели, блобы, проекции и скриншоты хранятся локально.

**CODE EXISTS → RUNTIME CONNECTED → TESTED → ALPHA PROVENANCE.** Для Alpha доказаны вызовы editor→appearance→модель по декомпиляции, таблицы и read-only сейв проверены на указанной сборке. Интерактивный перебор оригинального UI и сохранение после каждого переключения в эту проверку не вошли. Runtime CubeGame F2A уже связывает face/hair/tint с героем, подробности и ограничение OrcFemale — [отчёт F2A](https://github.com/alfuwerce-art/CubeGame/blob/1b75b27eb3f442528281a03f16eae56a6bce47c2/docs/reports/M0_F2A_CHARACTER_RUNTIME_REPORT.md). Для проектируемых слотов CubeGame статус `CUBEGAME_CONTRACT / PROPOSAL`, реализация и переключение в Unity требуют отдельного теста.

## C. Карточки и вопросы канона

[Human](../../races/HUMAN/review.md) · [Elf](../../races/ELF/review.md) · [Dwarf](../../races/DWARF/review.md) · [Goblin](../../races/GOBLIN/review.md) · [Dragonkin](../../races/DRAGONKIN/review.md) · [Orc](../../races/ORC/review.md) · [Skeleton](../../races/SKELETON/review.md) · [Fildor / Enderman](../../races/FILDOR_ENDERMAN/review.md).

- **Fildor / Enderman.** Имя `Фильдор.png` и отображаемое в референсе `Enderman` относятся к одному представленному изображению. В checked-in документах Factoria и CubeGame и в roster Alpha подтверждения канонического имени или тождества не найдено. Рабочий ID `FILDOR_ENDERMAN`, вопрос владельцу — какое имя и роль закреплять в каноне.
- **Dragonkin.** Владелец показывает рога, гребень и хвост. Alpha подтверждает Lizard и отдельного Dragon. Связь Dragonkin с Lizard на уровне лора и пайплайна остаётся открытой. До решения карточка использует Lizard только для сравнения.
- **Skeleton / Undead.** Скелет из референса использован в отдельной карточке. Замена выбираемой Undead из Alpha требует решения владельца. Frogman остаётся в полной матрице Alpha.
- Для Human, Elf и Dwarf использованы локальные файлы без суффикса `(3)/(1)`. Их SHA-256 и размеры приведены в manifests. Побайтовое совпадение с прежними именованными вложениями не установлено. Сравнение высот в карточках основано на пикселях исходных скриншотов и зависит от кадрирования и камеры.

## D. Проект модульного редактора

См. [контракт заменяемых частей](COMFY-STYLE-08_MODULAR_EDITOR.md).

## E. Фронтальные эксперименты

Один запуск на расу с референсом владельца. Граф, модель, веса и seed общие. Prompt-поля `race_anatomy` и `appearance` плюс изображение задаются карточкой. Для каждого запуска публикуются метаданные и ограничения, локальные изображения и сравнительный лист сохраняются в `outputs/factory/race_factory_v1/`. PNG не содержит модульных мешей и не подтверждает совместимость с Mecanim.

| Раса | PNG | Секунды | Peak VRAM карты MiB | Peak RSS ComfyUI MiB | Min available RAM MiB |
|---|---|---:|---:|---:|---:|
| Human | human_00001_.png | 77.4 | 7680 | 13394 | 165 |
| Elf | elf_00001_.png | 58.4 | 7612 | 15246 | 1 |
| Dwarf | dwarf_00001_.png | 52.3 | 7547 | 12834 | 3299 |
| Goblin | goblin_00001_.png | 52.4 | 7547 | 12834 | 4754 |
| Dragonkin | dragonkin_00001_.png | 54.3 | 7546 | 12813 | 4509 |
| Orc | orc_00001_.png | 53.4 | 7545 | 12815 | 4535 |
| Skeleton | skeleton_00001_.png | 53.4 | 7558 | 12825 | 5199 |
| Fildor / Enderman | fildor_enderman_00001_.png | 52.3 | 7558 | 12835 | 5089 |

Все восемь PNG декодируются, имеют 768×1024 и содержат исполненный API prompt, совпадающий с записанным графом. Замеры раз в секунду: nvidia-smi по всей карте, psutil по RSS выделенного процесса и доступной RAM системы. У первых двух прогонов осталось 165 и 1 MiB доступной RAM; затем минимум был от 3299 MiB. Измерения по всей карте включают фоновые приложения. RAM-first остаётся PARTIAL при такой фоновой нагрузке. В Elf и Dwarf маски стоп уточнены вручную, контур Fildor обведён вручную. В нескольких входах сохранились отдельные пиксели фона, особенно у стоп. Фронтальные расхождения с референсами записаны в карточках.

Проверка воспроизводимости — 8/8 входов пересобраны с тем же SHA-256, 8/8 графов совпали с prompt в PNG. Для проверки глаз выполнены локальные ортографические проекции первых Face и overlay из data1.db.
