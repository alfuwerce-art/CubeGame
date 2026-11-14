# COMFY-STYLE-11B — независимые входы позы и стиля

**READY_FOR_OWNER_VISUAL_REVIEW · 14 ноября 2026**

**Визуальная проверка `PARTIAL`.** Human Male FRONT V2 сохраняет горизонтальную T-pose и полный рост, а голова, корпус и конечности читаются как объёмные блоки. Все пять вариантов содержат глаза и рот. Цветовые акценты и швы также мешают использовать кадр как чистую базу модульного персонажа.

## Входы и граф

- **Поза.** Из `HUMAN_MALE_BASE_FRONT_GUIDE_V1.png` построен двоичный силуэт. Пиксели, отличные от фона RGB (158, 158, 158), сведены к белому. `ImageScale → Canny → ControlNet` получает только внешний контур фигуры на исходном холсте. Контур задаёт положение и габариты в проекции. Из одного FRONT кадра нельзя подтвердить 3D-позу или ортографическую геометрию.
- **Стиль.** Архивный `human_v1_latest_available.png` побайтово совпал с `reference_controlnet_00006_.png`. Встроенный граф совпал с архивным API COMFY-STYLE-08. Связь с устаревшим идентификатором `20261114-120058` остаётся `UNKNOWN`. `IPAdapterAdvanced` в режиме `style transfer` получает это изображение в отдельной ветке. Для портретного входа CLIP Vision использует центральную квадратную область.
- **Сэмплирование.** `BASE_BODY_V2` начинает с пустого latent. В COMFY-STYLE-11 серая направляющая использовалась одновременно в Canny и img2img. Между V1 и V2 изменились входы, способ инициализации и промпты, поэтому сравнение кадров показывает суммарный эффект. Внутри каждой серии менялся один вес.

| Файл | SHA-256 |
|---|---|
| Исходная направляющая `HUMAN_MALE_BASE_FRONT_GUIDE_V1.png` | `9b618a41dda4b57989c0ec96be4718f099b29969bf8c9e88b7cd2a1de97a4ff7` |
| Вход Canny `HUMAN_MALE_FRONT_POSE_SILHOUETTE.png` | `c849c7d5998c99f512e8c6eac6098d3d27b617776c64be2117d15f4f6e735bca` |
| Референс `HUMAN_V1_STYLE_REFERENCE.png` | `b9f3d6e41e68e8239ab61f70fc5ddf81f95e0e85124903841bdd2e1195977e77` |
| Отклонённый `HUMAN_MALE_BASE_FRONT_V1.png` | `a5785c9db2d5dba39d74253d9a0ea6b51a158d5ae6613d1e6884d23f79dda755` |
| Кадр сравнения `HUMAN_MALE_BASE_FRONT_V2.png` | `dea26a722921c0b30e9c57f0abb090d883dd9abb77e782f94f5595d87d48cfcd` |

[Шаблон BASE_BODY_V2](../../workflows/character_factory/base_body/BASE_BODY_V2.json) · [Компилятор](../../scripts/comfyui/compile_human_base_front_v2.py) · [Параметры сравнения и происхождение входов](../../measurements/comfy_style_11b/EXPERIMENT.json)

## Пять диагностических кадров

Сначала проверены 0.9, 0.6 и 0.4 Canny при IPAdapter 0.5. Наименьшая из испытанных сил, при которой по кадру сохранились горизонтальные руки и симметричный силуэт, составила 0.4. При ней проверены 0.3 и 0.7 IPAdapter. Общий кадр 0.4/0.5 использован в обеих сериях и в четырёхпанельном сравнении.

| Canny | IPAdapter | Наблюдение | Время, с | Пик VRAM карты, MiB | Граф и измерения |
|---:|---:|---|---:|---:|---|
| 0.9 | 0.5 | T-pose, объёмные блоки. Глаза, рот, акценты у стоп, мелкие стыки. | 69.3 | 7504 | [API](../../measurements/comfy_style_11b/HUMAN_MALE_BASE_FRONT_V2_CN90_IPA50.api.json) · [manifest](../../measurements/comfy_style_11b/HUMAN_MALE_BASE_FRONT_V2_CN90_IPA50.manifest.json) |
| 0.6 | 0.5 | T-pose и полный рост. Глаза, рот, цвет у стоп и выраженные стыки. | 43.4 | 7517 | [API](../../measurements/comfy_style_11b/HUMAN_MALE_BASE_FRONT_V2_CN60_IPA50.api.json) · [manifest](../../measurements/comfy_style_11b/HUMAN_MALE_BASE_FRONT_V2_CN60_IPA50.manifest.json) |
| 0.4 | 0.5 | T-pose и крупная квадратная голова с читаемыми боковыми плоскостями. Глаза, рот, вставки у стоп и торса. | 77.4 | 6845 | [API](../../measurements/comfy_style_11b/HUMAN_MALE_BASE_FRONT_V2_CN40_IPA50.api.json) · [manifest](../../measurements/comfy_style_11b/HUMAN_MALE_BASE_FRONT_V2_CN40_IPA50.manifest.json) |
| 0.4 | 0.3 | Меньше цветовых акцентов у стоп. Видны глаза, рот, вставка на торсе и шов у таза. | 52.3 | 7004 | [API](../../measurements/comfy_style_11b/HUMAN_MALE_BASE_FRONT_V2_CN40_IPA30.api.json) · [manifest](../../measurements/comfy_style_11b/HUMAN_MALE_BASE_FRONT_V2_CN40_IPA30.manifest.json) |
| 0.4 | 0.7 | Кадр сохраняет T-pose. Синие глаза, брови, рот, цветные детали у головы, пояс и тёмные панели на ногах. | 46.2 | 7093 | [API](../../measurements/comfy_style_11b/HUMAN_MALE_BASE_FRONT_V2_CN40_IPA70.api.json) · [manifest](../../measurements/comfy_style_11b/HUMAN_MALE_BASE_FRONT_V2_CN40_IPA70.manifest.json) |

Во всех результатах одна фигура в полный рост, без очевидного оружия. У кадра сравнения нет читаемой причёски и бороды. Плоский серый вид V1 сменился светотенью по крупным плоскостям. Сохранить однотонный нейтральный материал и пустую голову не удалось даже при весе IPAdapter 0.3. Видны тёплая палитра, пол с тенями и отражением. Пять кадров получены с одним seed.

## Воспроизведение

| Параметр | Значение для кадра сравнения |
|---|---|
| Checkpoint | `DreamShaperXL_Turbo_v2_1.safetensors` |
| LoRA | `ral-vxl.safetensors`, model 0.6, CLIP 0.6, локальный эксперимент по [FACT-D21](../decisions/FACT-D21-civitai-voxel-lora-license.md) |
| Seed, steps, CFG | `20261114`, `28`, `5` |
| Sampler, scheduler, denoise | `dpmpp_2m`, `normal`, `1.0` |
| ControlNet | `controlnet-union-sdxl-1.0.safetensors`, union `canny/lineart/anime_lineart/mlsd`, сила `0.4`, пороги `0.35/0.75`, интервал `0–1` |
| IPAdapter | `ip-adapter-plus_sdxl_vit-h.safetensors`, `CLIP-ViT-H-14-laion2B-s32B-b79K.safetensors`, `style transfer`, вес `0.5`, `concat`, `V only`, интервал `0–0.8` |
| Холст | `768×1024`, batch `1` |
| Runtime | ComfyUI `0.45.0`, ComfyUI_IPAdapter_plus `2.0.0`, PyTorch `2.14.1+cu130`, RTX 4060 Laptop, `--novram --reserve-vram 5` |

Команда из корня Factoria создаёт точный граф и оба входа в отдельной папке. Аргументы `--guide` и `--style-reference` принимают исходные PNG из локального архива.

```powershell
python scripts/comfyui/compile_human_base_front_v2.py `
  --guide outputs/factory/human_base_front_v1/HUMAN_MALE_BASE_FRONT_GUIDE_V1.png `
  --style-reference outputs/factory/race_factory_v1/human_v1_latest_available.png `
  --input-dir <COMFY_INPUT> --out <GRAPH_OUTPUT> --control 0.4 --weight 0.5
```

Загрузить API JSON в очередь локального ComfyUI с указанным входным каталогом и установленным плагином. SHA-256 входов, моделей, графов и PNG зафиксированы в манифестах. Каждый RGB PNG сверён с отправленным графом по встроенному prompt после проверки служебных SHA-256 в `LoadImage.is_changed`. В изолированной очереди завершились пять запросов.

Локальная папка `outputs/factory/human_base_front_v2/` содержит все исходные PNG прогона, копии входов и `HUMAN_MALE_BASE_FRONT_V2_COMPARISON.jpg` с четырьмя панелями `POSE GUIDE / REJECTED V1 / NEW V2 / HUMAN V1 STYLE`. `SWEEP_COMPARISON.jpg` показывает пять тестов. Изображения с экспериментальным `ral-vxl` хранятся локально.
