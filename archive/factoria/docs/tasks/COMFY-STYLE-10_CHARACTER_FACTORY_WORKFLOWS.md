# COMFY-STYLE-10 — архитектура workflow Character Factory

**14 ноября 2026 · READY_FOR_OWNER_REVIEW · дизайн и валидация графов**

## Неизменяемый граф владельца

[CHARACTER_STYLE_WORKFLOW_V1_FROZEN.json](../../workflows/character_factory/frozen/CHARACTER_STYLE_WORKFLOW_V1_FROZEN.json) — побайтовая копия файла «самое адекватное что по идее у нас есть.json» из локального `ComfyUI/user/default/workflows`. SHA-256 исходника и копии `b380616fae8931513a176b7c6bccc25aa09b4e26f879b245859010a92aa1356a`.

| Параметр | Значение активной ноды исходника |
|---|---|
| Checkpoint | `DreamShaperXL_Turbo_v2_1.safetensors` |
| LoRA | `ral-vxl.safetensors`, model 0.6, CLIP 0.6 |
| Холст | 768×1024, batch 1; `nearest-exact`, crop `disabled` |
| Геометрия | `controlnet-union-sdxl-1.0.safetensors`, union type `canny/lineart/anime_lineart/mlsd`, Canny 0.35 / 0.75, strength 0.9, interval 0–1 |
| Sampling | seed 20261114 (fixed), 28 steps, CFG 5, `dpmpp_2m`, `normal`, denoise 1.0 |
| Вход и prompts | `factoria_ref_human_warrior_768x1024.png`; тексты в исходных узлах 4/5 |

В MarkdownNote зафиксированы старые SDXL Base, CFG 6 и karras. В замороженной копии сохранены все поля, включая заметку. Новые графы используют измеренные значения. [FACT-D21](../decisions/FACT-D21-civitai-voxel-lora-license.md) задаёт статус `EXPERIMENTAL / LICENSE_BLOCKED_FOR_PRODUCTION` для RalFinger `ral-vxl`. [COMFY-STYLE-09](COMFY-STYLE-09_STYLE_LOCK_AND_TRAINING_PLAN.md) остаётся отдельным планом работы над стилем и правами.

## Архитектура и входы

Один вызов [compile_character_factory.py](../../scripts/comfyui/compile_character_factory.py) собирает один API prompt и manifest из четырёх источников.

| Источник | Что задаёт |
|---|---|
| [STYLE_LOCK_V1.json](../../workflows/character_factory/shared/STYLE_LOCK_V1.json) | Общие модели и SHA-256, sampler, геометрия крупных и мелких блоков, плотность, текстуры, фиксированная камера и фон, свет, отрицательный prompt. Значения для экспериментов с img2img/IPAdapter отделены от параметров frozen. |
| [Human Male](../../workflows/character_factory/profiles/HUMAN/MALE.json) и [Human Female](../../workflows/character_factory/profiles/HUMAN/FEMALE.json) | Строение тела, головы, ушей и челюсти, дефолт поверхности, каталожный источник и отдельные head/body sockets. Размеры конечностей в собранном теле берутся из входных направляющих. |
| [Каталог Alpha](ALPHA_CHARACTER_EDITOR_CATALOG.md) | Face/Haircut по номеру UI с 1. Компилятор берёт точное описание, индекс с 0, ID ресурса и признак доступности из отдельного JSON расы и пола. Haircut использует также visual_type и secondary_features из каталога. |
| Локальный reference pack | Один identity ID, ревизии геометрии и камеры, сокет, тип и номер опции, изображения по конкретному виду. Для других рас достаточно профиля того же формата и каталожной записи. |

Три шаблона имеют параметризованный граф ComfyUI API. Скрипт подставляет значения и записывает два JSON. Manifest содержит хэш графа, каталога, входов, параметры и происхождение референса; отправка `/prompt` остаётся отдельным шагом после разрешения владельца. Исходные .cub и изображения Alpha в графы не встраиваются. Для будущего сравнения нужны подготовленные владельцем геометрические направляющие, изображение носителя и маски.

## Шаблоны и контроль ракурсов

| Шаблон | Виды и параметры | Выход |
|---|---|---|
| [BASE_BODY_V1](../../workflows/character_factory/base_body/BASE_BODY_V1.json) | FRONT, BACK, LEFT, RIGHT по отдельности. Полная симметричная T-pose с горизонтальными руками, прямыми ногами, читаемыми кистями и стопами. Лысая базовая голова, нейтральный прилегающий матовый слой для чтения формы. Обязателен geometry_guide одного и того же тела на нужном ракурсе. | Один полный RGB-кадр на сером фоне. |
| [FACE_MODULE_V1](../../workflows/character_factory/face/FACE_MODULE_V1.json) | FRONT по умолчанию, LEFT/RIGHT при геометрической необходимости. Общий head carrier из socket pack, геометрический guide, edit mask, module matte и Face ID. У Human 6 Face на пол, набор занятых координат внутри каждого пола совпадает; варианты меняют RGB лица. | Голова целиком с ушами и глазами, отдельный RGBA и RGB для проверки посадки. |
| [HAIRCUT_MODULE_V1](../../workflows/character_factory/haircut/HAIRCUT_MODULE_V1.json) | FRONT, LEFT, RIGHT, BACK, TOP. Общий socket pack на все варианты Face и Haircut данного пола, отдельная проекция на каждый вид. Human Male — 15, Female — 7. Male Haircut 03 использует один ID с признаками HAIR и BEARD. | Изображение на носителе для контроля и RGBA с изолированным Haircut. Нейтральный тёплый серый `#A8A29A` задан как цель. |

В Face/Haircut фото носителя проходит VAEEncode → маска шума → KSampler → VAEDecode. Снаружи edit mask исходные RGB восстанавливаются явной композицией. Отдельная module matte задаёт foreground. Для Haircut маска исключает кожу и носитель; для Face она покрывает всю голову, включая уши. Матовая область задаётся вручную и может уточняться после проверки результата. Она не вычисляется из edit mask и не утверждает, что модель попала в заданный контур. У масок красный канал, 255 — редактировать/оставить, 0 — сохранить исходник/сделать прозрачным соответственно. Перед JoinImageWithAlpha маска foreground инвертируется по контракту этой ноды. Для масок с промежуточными значениями альфа получается мягкой.

Hair color содержит 112 подтверждённых RGB. Значения берутся из `colors.hex_rgb_columns_top_to_bottom` карточки Alpha и хранятся как параметр материала. Палитра не участвует в выборе графа или размножении Haircut. Для игрока Face содержит собственные RGB-воксели глаз; отдельного Alpha workflow для Eye Color нет.

## Identity lock и общая камера

1. Все направляющие одной пары раса/пол получают из одного пространственного носителя, в нейтральной позе и одной ревизии геометрии. Для BASE_BODY центры плеч, локтей, запястий, таза, коленей, щиколоток и плоскость подошв размечаются один раз в координатах объекта. Для головы используются центр, плоскости, глаза и уши из сокета. `LEFT/RIGHT` всегда анатомические стороны, TOP смотрит сверху, перед головы направлен к верхнему краю кадра.
2. Для каждого вида используют его собственный guide и одинаковые ортографические единицы на пиксель, высоту горизонта и позицию опоры. Для head pack масштаб и положение головы одинаковы у Face и Haircut. Для полнофигурного pack масштаб фиксируется отдельно. Canny/ControlNet удерживает силуэт, ширину плеч, длины конечностей и размеры кистей/стоп через входную геометрию. Seed и Prompt фиксируют условия сравнения; качество согласования четырёх кадров проверяется по опорным точкам.
3. Базовый маршрут — ControlNet от направляющей. `--base-img2img` добавляет регистрацию отдельного anchor_plate **того же вида** и начинает с denoise 0.35. Для Face denoise 0.35 удерживает посадку, для Haircut исходный кандидат 0.65 даёт больше пространства для нового объёма. Начальные значения требуют визуальной проверки. Фронтальная картинка не используется как img2img-вход для заднего ракурса.
4. `--ipadapter` дополнительно подключает установленные IPAdapter Plus SDXL ViT-H и CLIP Vision ViT-H. Один identity_reference на pack помогает связать читаемость идентичности и материалов между видами. Стартовый вес 0.4, linear, interval 0–0.8, V only. Для Haircut условный референс должен исключать чужую причёску. ControlNet задаёт геометрию, IPAdapter задаёт дополнительное условие, img2img сохраняет совпадающие с ракурсом детали. Выбранный метод и все настройки отражаются в manifest.

## Head и Body Socket

| Сокет | Основание и размеры |
|---|---|
| `HUMAN_MALE_HEAD_SOCKET_V1` | Alpha Face 01, registry 0x4DE. |
| `HUMAN_FEMALE_HEAD_SOCKET_V1` | Alpha Face 01, registry 0x4F3. Размеры измеренной оболочки совпадают с Male, ссылки на головы и идентичности независимы. |
| `HUMAN_MALE_BODY_SOCKET_V1`, `HUMAN_FEMALE_BODY_SOCKET_V1` | Начало координат между стопами, плоскость подошв по Z=0. На направляющей отмечаются суставы, центр шеи и сопряжение с head socket. Размеры целой сборки проверяются на Human. |

Обе головы имеют занятый диапазон X 2–15, глубина 4–11, Z 2–9 в индексации ячеек. В координатах граней размер 14×8×8, центр [9, 8, 6], верх Z=10, затылок D=4, фронт D=12. Ось X направлена анатомически вправо, +D вперёд, +Z вверх. Центры ушей и радужек получены из RGB-областей Face 01. Линия лба, нижняя плоскость шеи Z=2 и точка посадки [9, 8, 2] — проектные ориентиры, подлежащие проверке с body. Все будущие модули одного пола используют тот же head carrier, его оси, камеру и масштаб. Это координатное предложение для 2D-контроля и дальнейшего измерения стыка в 3D.

## Пример сборки Haircut 03

Reference pack для одного вида. Имена PNG относятся к папке ComfyUI input. Остальные ракурсы получают свои записи в `views` с тем же `identity_id`, `geometry_revision` и `camera_revision`.

```json
{
  "asset_type": "HAIRCUT_MODULE",
  "race": "HUMAN",
  "gender": "MALE",
  "option_ui_number": 3,
  "identity_id": "human_male_v1",
  "socket_id": "HUMAN_MALE_HEAD_SOCKET_V1",
  "geometry_revision": "human_male_carrier_v1",
  "camera_revision": "head_ortho_v1",
  "views": {
    "FRONT": {
      "geometry_guide": "factory_v1/human_male/h03_front_guide.png",
        "edit_mask": "factory_v1/human_male/h03_front_edit.png",
      "module_matte": "factory_v1/human_male/h03_front_matte.png"
    }
  }
}
```

```powershell
& $comfyPython -B scripts/comfyui/compile_character_factory.py --asset HAIRCUT_MODULE --race HUMAN --gender MALE --option 3 --view FRONT --reference-pack $pack --socket-pack $socketPack --input-dir $comfyInput --out $compiled
```

Рядом хранится один head socket pack для всех опций данного пола и identity. Компилятор принимает его отдельно через --socket-pack и сверяет расу, пол, identity, сокет, обе ревизии и фактический SHA-256 carrier. Поле views каждого вида содержит путь и ожидаемый хэш. Пример структуры:

~~~json
{
  "race": "HUMAN",
  "gender": "MALE",
  "identity_id": "human_male_v1",
  "socket_id": "HUMAN_MALE_HEAD_SOCKET_V1",
  "geometry_revision": "human_male_carrier_v1",
  "camera_revision": "head_ortho_v1",
  "views": {
    "FRONT": {
      "file": "factory_v1/human_male/head_front.png",
      "sha256": "<SHA-256 подготовленного FRONT PNG>"
    }
  }
}
~~~

Для Face pack задаёт `asset_type=FACE_MODULE`, head socket и выбранный option. Для Base Body pack задаёт `asset_type=BASE_BODY`, body socket, убирает option, а по виду передаёт geometry_guide. Опциональные входы `anchor_plate` по виду и `identity_reference` на весь pack нужны только для соответствующих флагов. Компилятор проверяет PNG, размер 768×1024 для зарегистрированных кадров/масок, соответствие pack профилю и наличие выбранного ассета. Команды используют Python ComfyUI с Pillow.

## Будущая проверка на Human

После разрешения владельца подготовить по одному независимому body/head plate pack на пол. Сначала проверить T-pose FRONT и соответствие точек на BACK/LEFT/RIGHT для одного тела. Затем проверить FACE 01 у обоих полов и одну смену Face с сохранением оболочки и положения глаз. После этого проверить один Human Male Haircut 03 и один Human Female Haircut, сравнить контур, лоб, уши, затылок и альфу на разрешённых видах. RGB на носителе нужен для сравнения центров, RGBA — для оценки отделимости. Итоговый формат подачи в будущий image-to-3D и фактическая посадка требуют отдельной проверки. Порог совпадения силуэта и допуски стыка выбираются после первых измерений.

## Выполненная валидация

Побайтовая копия совпала с исходником. Компилятор собрал, а локальный валидатор ComfyUI 0.45.0 принял **168 API-графов** на временных PNG-заглушках. Восемь Base Body, 36 Face, 110 Haircut охватывают все варианты и предусмотренные виды Human обоих полов. Дополнительно проверены 12 каталожных пар остальных шести рас с временными профилями совместимости, один Base Body img2img и один Base Body img2img + IPAdapter. Отрицательные проверки отвергли отсутствующий Orc Male Haircut 08, Human Female Haircut 08, Face BACK и SHA-256 носителя с расхождением. Локальная проверка не создавала очередь ComfyUI и не загружала веса для inference.

Открытые условия — зарегистрированные изображения носителей и маски, качество identity lock на кадрах, 3D-стыки, ресурсные замеры и художественная приёмка. Проверка схемы ComfyUI не требует smoke generation.
