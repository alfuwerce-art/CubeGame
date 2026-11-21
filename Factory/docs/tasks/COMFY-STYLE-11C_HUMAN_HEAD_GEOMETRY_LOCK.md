# COMFY-STYLE-11C — Human Head Geometry Lock

**READY_FOR_OWNER_VISUAL_REVIEW · 14 ноября 2026**

**Отзыв владельца о V3:** головы визуально различаются. Ревизия carrier и повторное сравнение описаны в [проверке формы головы](COMFY-STYLE-11C_HUMAN_HEAD_CARRIER_REVISION.md).

Владелец отклонил COMFY-STYLE-11B из-за геометрии головы. Для V3 сохранены корпус, T-pose, свет и фон V2. Единственный запрос ComfyUI отработал внутри маски головы и шеи. Финальный композит отличается от V2 только внутри этой маски.

## Геометрия и посадка

[HUMAN_MALE_HEAD_CARRIER_V1](../../workflows/character_factory/profiles/HUMAN/HUMAN_MALE_HEAD_CARRIER_V1.json) использует [HUMAN_MALE_HEAD_SOCKET_V1](../../workflows/character_factory/profiles/HUMAN/MALE.json). Из профиля задействованы размер `14 × 8 × 8` в порядке X, глубина, Z, верхняя плоскость, центр, центры ушей и neck anchor. Ступени челюсти и видимые плоскости построены как новый технический FRONT-контур для CubeGame. Посадка и форма плоскостей остаются проектными до проверки в 3D. Регистрация carrier предусматривает Base Body, Face 01–06 и Haircut 01–15.

| Параметр | V3 |
|---|---|
| Границы головы с ушами по координатам граней | X `188–580`, Y `118–342` |
| Ширина × высота | `392 × 224 px`, `28 px/voxel` |
| Центр головы | `(384, 230)` |
| Центры ушей | `(244, 230)` и `(524, 230)` |
| Neck anchor и стык | `(384, 342)`, стык Y `359`, интервал `17 px` |
| Ширина головы относительно высоты фигуры | около `53%` по границам `392 / (852 − 118)` |

Для V2 ширина головы с ушами около `245 px`, то есть около `36%` высоты фигуры. В Human V1 визуально измеренная ширина между наружными краями ушей около `390 px`. Волосы закрывают верх головы Human V1, поэтому высота его черепа и точное отношение размера черепа к телу остаются неизвестными. В V3 размер в пикселях — проектный выбор для данного кадра и масштаба корпуса. Широкий контур, крупные уши, ступенчатый подбородок и короткая шея видны в сравнении. Глаза, брови, рот и волосы визуально отсутствуют.

## Контроль и воспроизведение

- **Body Pose Lock.** Из направляющей T-pose COMFY-STYLE-11B удалена область головы и шеи выше Y `350`. Canny `0.35/0.75`, ControlNet Union SDXL `0.4`, интервал `0–1`.
- **Head Geometry Lock.** Отдельная карта carrier и его маска на полном холсте. Canny `0.20/0.45`, ControlNet Union SDXL `0.85`, интервал `0–1`. Маска ограничивает шум сэмплера. После визуальной проверки единственного сэмпла его светотень использована внутри carrier с непрозрачностью `0.85` и мягким переходом по внутреннему краю. Финальный контур и шов определяет маска carrier. За её пределами проверено `0` отличающихся RGB-пикселей.
- **Style Lock.** Human V1, IPAdapter Plus SDXL ViT-H `style transfer`, `0.5`, `V only`, интервал `0–0.8`, с маской внимания головы. Веса IPAdapter не определяют размеры carrier.
- **Sampling.** DreamShaper XL Turbo v2.1, экспериментальный `ral-vxl` model/CLIP `0.6/0.6`, seed `20261114`, `28` steps, CFG `5`, `dpmpp_2m / normal`, masked img2img `denoise=0.34`, `768×1024`, batch `1`. ComfyUI `0.45.0`, IPAdapter Plus `2.0.0`, PyTorch `2.14.1+cu130`, RTX 4060 Laptop, `--novram --reserve-vram 5`.

В изолированной очереди зарегистрирован один успешный запрос. Серверное время `76.94 с`, пик выборочного измерения выделенной VRAM всей карты `5531 MiB`, пик RSS ComfyUI `15119 MiB`. Встроенный prompt сырого PNG совпал с отправленным графом после сверки хешей, добавленных `LoadImage`. Подготовка входов и композит повторены без новой генерации. Хеши входов, API-графа, результата и листа совпали.

[Отправленный граф](../../measurements/comfy_style_11c/HUMAN_MALE_BASE_FRONT_V3.api.json) · [Параметры запуска и входы](../../measurements/comfy_style_11c/RUN.json) · [Итоговые хеши и координаты](../../measurements/comfy_style_11c/COMPOSITE.json)

Из корня Factoria передать папку локального V2 в [prepare_human_head_front_v3.py](../../scripts/comfyui/prepare_human_head_front_v3.py): `--source <V2_DIR> --input-dir <COMFY_INPUT> --graph <API_JSON>`. Полученный граф принимает локальный ComfyUI с уже установленным IPAdapter Plus. Полученный сырой PNG после визуальной проверки передать в [composite_human_head_front_v3.py](../../scripts/comfyui/composite_human_head_front_v3.py): `--source <V2_DIR> --sample <RAW_PNG> --out <V3_DIR>`.

Файлы владельца хранятся локально в `outputs/factory/human_base_front_v3/`. Для просмотра доступны `HUMAN_MALE_BASE_FRONT_V3.png`, `HUMAN_MALE_BASE_FRONT_V3_COMPARISON.jpg`, прозрачный `HUMAN_MALE_HEAD_CARRIER_V1.png` и отдельная карта геометрии. Лист показывает `HUMAN V1 STYLE | V2 REJECTED | V3 HEAD LOCK`.
