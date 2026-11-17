# COMFY-STYLE-12 — MANNEQUIN_V1

**STATUS: AUTHORIZED / PLANNED**  
**Дата:** 2026-11-17  
**Контур:** только D:\Projects\CubeGame Factory\ / ComfyUI. Основной CubeGame не трогать.

## 1. Цель

Настроить отдельный ComfyUI workflow для генерации чистого базового гуманоида-манекена, пригодного как визуальный вход будущего image-to-3D / Tripo и последующей модульной переработки.

Первый эталон этапа — HUMAN MALE / FRONT / T-POSE. До визуальной приёмки владельца не масштабировать на Female, другие расы и другие ракурсы.

Манекен — не готовый персонаж. На нём не должно быть лица, глаз, бровей, рта, волос, бороды, одежды, обуви, брони, оружия, аксессуаров, расовых декоративных деталей, текста или UI.

Нужна одна цельная читаемая 3D-форма с воксельной геометрией, чистыми крупными плоскостями и достаточным объёмом для будущей 3D-реконструкции.

## 2. Почему новый workflow

Не продолжать слепо старые варианты:

- COMFY-STYLE-11 / BASE_BODY_V1 — поза и силуэт сохранялись, но поверхность была слишком плоской.
- COMFY-STYLE-11B / BASE_BODY_V2 — объём стал лучше, но IPAdapter от Human V1 протащил лицо, цветовые акценты и неверную форму головы.
- COMFY-STYLE-11C — трассировка головы была только диагностикой FRONT, не производственным решением.

Для MANNEQUIN_V1 разделить задачи:
- ControlNet = только поза / внешний силуэт / пропорции;
- checkpoint + voxel LoRA = объём и воксельная геометрия;
- IPAdapter = OFF;
- img2img = OFF;
- старт из EmptyLatentImage.

## 3. Модельный стек

Использовать только уже имеющиеся и проверенные локальные веса:

- Checkpoint: sd_xl_base_1.0.safetensors
- LoRA: Voxel_XL_Lora.safetensors / Fictiverse
- ControlNet: controlnet-union-sdxl-1.0.safetensors
- Control mode: Canny / lineart-compatible union mode
- IPAdapter: не подключать
- ral-vxl.safetensors: не использовать в MANNEQUIN_V1 из-за уже зафиксированного production license blocker FACT-D21.

Не скачивать новые веса и не обновлять custom nodes без отдельного разрешения.

## 4. Первый workflow

Создать новый файл:
workflows/character_factory/mannequin/MANNEQUIN_V1.json

При необходимости отдельный воспроизводимый компилятор:
scripts/comfyui/compile_mannequin_v1.py

Старые frozen, BASE_BODY_V1 и BASE_BODY_V2 не изменять.

## 5. Геометрический вход

Взять существующий Human Male FRONT guide как основу, но в ControlNet подавать только чистый технический guide.

Требования:
- один персонаж;
- строго FRONT;
- ортографическое ощущение камеры;
- симметричная T-pose;
- руки горизонтально;
- прямые ноги;
- кисти и стопы целиком;
- голова без внутренних деталей лица;
- нейтральный фон;
- полный силуэт без оружия и одежды.

Если бинарный силуэт плохо удерживает объём, не включать IPAdapter. Допускается подготовить новую детерминированную техническую guide-картинку с крупными плоскостями/ступенями тела и пустой головой. Она остаётся только control input.

## 6. Стартовые параметры

Первый воспроизводимый запуск:

- width: 768
- height: 1024
- batch: 1
- seed: 20261117
- steps: 28
- CFG: 6.0
- sampler: dpmpp_2m
- scheduler: karras
- denoise: 1.0
- LoRA model/clip: 0.60 / 0.60
- ControlNet strength: 0.60
- Control start/end: 0.0 / 1.0

Если первый кадр сохраняет позу, но форма слишком органическая — увеличить voxel LoRA до 0.70.
Если форма хорошая, но T-pose/пропорции плывут — увеличить ControlNet до 0.75.
Не делать большую сетку параметров без причины. Максимум после первого кадра — ещё 3 диагностических варианта с тем же seed.

## 7. Positive prompt

one single full-body adult male humanoid game mannequin, exact straight-on orthographic front view, symmetric T-pose, both arms horizontal at shoulder height, straight legs, full hands and feet visible, compact stylized voxel proportions, hard-edged cuboid body volumes, large coherent block surfaces, visible front and side planes, stepped voxel silhouette, distinct shoulder upper-arm forearm hand volumes, distinct thigh shin foot volumes, short neck, large simple blank block head, completely featureless face, neutral matte light-gray material over the entire body, soft even studio lighting, plain neutral gray background, centered, full body

## 8. Negative prompt

eyes, pupils, eyebrows, eyelashes, mouth, lips, teeth, nose detail, ears detail, facial expression, hair, beard, moustache, helmet, hat, clothes, shirt, tunic, trousers, underwear, belt, armor, gloves, boots, shoes, weapon, sword, staff, accessory, jewelry, skin texture, realistic skin, muscles, veins, anatomy markings, nipples, genitals, photorealistic, organic sculpt, rounded body, smooth toy, chibi, anime, large eyes, tiny noisy cubes, excessive bevels, multiple characters, character sheet, collage, extra limbs, missing limbs, bent arms, A-pose, bent knees, perspective camera, three-quarter view, cropped hands, cropped feet, floor props, scenery, text, watermark, logo

## 9. Память / запуск

Сначала использовать существующий xl_safe / RAM-first launcher и снять реальные пики.

Перед добавлением новых CLI-флагов выполнить main.py --help именно установленного ComfyUI 0.45.0 и использовать только реально поддерживаемые этой версией параметры.

Старый лимит Factory остаётся критерием измерения:
- RAM фиксировать;
- VRAM фиксировать до/во время/после;
- если +2 GB VRAM не выдерживается, не называть PASS. Зафиксировать фактическое превышение и рабочую конфигурацию как PARTIAL, не ухудшая качество скрытым уменьшением параметров.

## 10. Что измерить

Для каждого кадра:
- точный API JSON;
- seed / steps / CFG / sampler / scheduler;
- LoRA strength;
- ControlNet strength;
- время;
- пик RSS ComfyUI;
- VRAM карты до старта и пик;
- SHA-256 входного guide;
- SHA-256 результата;
- один ли персонаж;
- полный ли рост;
- симметрия;
- сохранена ли T-pose;
- есть ли лицо/одежда/оружие/аксессуары;
- читается ли объём головы, торса и конечностей;
- пригоден ли кадр как вход будущего image-to-3D.

## 11. Критерии READY_FOR_OWNER_VISUAL_REVIEW

Нужен минимум один кадр, где одновременно:

1. Один персонаж, полный рост.
2. Строгий FRONT.
3. T-pose сохранена.
4. Голова полностью пустая: без глаз/рта/волос/бороды.
5. Нет одежды, обуви, брони, оружия и аксессуаров.
6. Всё тело одного нейтрального серого материала.
7. Форма не выглядит плоской бумажной фигурой.
8. Читаются крупные воксельные объёмы и плоскости.
9. Кисти и стопы не обрезаны.
10. Нет второй фигуры / character sheet.
11. Сохранён guide и все параметры воспроизведения.
12. Результат показан владельцу. Только владелец решает MANNEQUIN_APPROVED или MANNEQUIN_REWORK.

## 12. После визуальной приёмки

Только после MANNEQUIN_APPROVED:

1. FRONT → BACK / LEFT / RIGHT с теми же пропорциями и камерой.
2. Human Female.
3. Затем остальные гуманоидные расы через отдельные race proportion profiles.
4. Face / Hair / Horn / Tusk / clothing / weapon — отдельными модулями, не запекать в базовый манекен.

## 13. Запреты

- Не трогать CubeGame / Unity.
- Не менять frozen workflow.
- Не возвращать Human V1 IPAdapter в MANNEQUIN_V1.
- Не использовать ral-vxl для производственного кандидата.
- Не скачивать новые модели.
- Не масштабировать до визуальной приёмки первого Human Male FRONT.
- Не утверждать пригодность для Tripo только по факту генерации PNG — пока это визуальный вход-кандидат.
