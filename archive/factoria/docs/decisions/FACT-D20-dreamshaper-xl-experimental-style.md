# FACT-D20 — DreamShaper XL + Pixel-Textured 3D как ЭКСПЕРИМЕНТАЛЬНЫЙ стилевой стек

**Дата:** 2026-11-13 · **Статус:** экспериментальный выбор исполнителя в рамках AUTHORIZED COMFY-STYLE-04.
**Не является** утверждением канонического стиля CubeGame: приёмка — только владелец после первой генерации.
**Задача:** `docs/tasks/COMFY-STYLE-04_DREAMSHAPER_XL_PIXEL3D.md`
**Отчёт:** `docs/tasks/COMFY-STYLE-04_RUN_REPORT.md`

## Решение

| Роль | Файл | Источник | Лицензия | Размер | SHA-256 (HF LFS) |
|---|---|---|---|---|---|
| Основная модель (SDXL) | `DreamShaperXL_Turbo_v2_1.safetensors` | HF `Lykon/dreamshaper-xl-v2-turbo` (официальный репозиторий автора Lykon) | **CreativeML OpenRAIL++-M** (`openrail++`) | 6 939 220 250 B | `4496b36d48bfd7cfe4e5dbce3485db567bcefa2bef7238d290dbd45612125083` |
| Стилевой модуль (SDXL LoRA) | `VoxelXL_v1.safetensors` | HF `Fictiverse/Voxel_XL_Lora` | **openrail** | 912 554 516 B | `7d9a5f11e1b38d97f75d2b84bfb5bb3bf95cd0e5f2500b002d13374eb4f88b5c` |
| Style reference (IPAdapter) | `ip-adapter-plus_sdxl_vit-h.safetensors` | HF `h94/IP-Adapter` (`sdxl_models/`) | **apache-2.0** | 847 517 512 B | `3f5062b8400c94b7159665b21ba5c62acdcd7682262743d7f2aefedef00e6581` |
| CLIP Vision (нужен IPAdapter) | `CLIP-ViT-H-14-laion2B-s32B-b79K.safetensors` | HF `h94/IP-Adapter` (`models/image_encoder/model.safetensors`) | CLIP ViT-H-14 (open_clip, MIT), веса обучены на LAION-2B — использовать локально, не перепубликовать | 2 528 373 448 B | `6ca9667da1ca9e0b0f75e46bb030f7e011f44f86cbfb8d5a36590fcd7507b030` |
| ControlNet (поза/силуэт) | `controlnet-union-sdxl-1.0.safetensors` | HF `xinsir/controlnet-union-sdxl-1.0` | **apache-2.0** | 2 512 030 408 B | `a9e13fd61f3193887791c8a0dd07a07202174dc47d5ddaea94ea1344f07c7467` |

## Почему именно эти файлы

**Checkpoint — Turbo v2.1, а не «классический» DreamShaper XL 1.0.**
На HF `Lykon/dreamshaper-xl-1-0` опубликован **только в diffusers-раскладке**
(unet/text_encoder/text_encoder_2/vae отдельными файлами), одиночного `.safetensors` для
`CheckpointLoaderSimple` там нет; single-file вариант существует на Civitai, но требует
аккаунта/токена — учетные данные не используются. `Lykon/dreamshaper-xl-v2-turbo` — официальный
репозиторий того же автора, содержит готовый одиночный SDXL-checkpoint (6.94 ГБ) с опубликованным
SHA-256 и совместим с `CheckpointLoaderSimple`. Turbo-дистилляция даёт рабочий режим 6–10 шагов /
CFG 1.5–2.5, что для 8 ГБ VRAM и итераций по стилю практичнее 30 шагов.
Ограничение: turbo слабее слушает CFG и тоньше в деталях, чем классический DSXL — это записано
как известное ограничение, а не как «лучший выбор».

**LoRA — VoxelXL, а не «pixel art».** Требование ТЗ — объёмный персонаж с пиксельными
поверхностями, а не 2D-спрайт. `nerijs/pixel-art-xl` (популярная «пиксельная» LoRA) даёт именно
2D pixel art и по ТЗ §4 отвергнута. `VoxelXL_v1` — SDXL-LoRA с документированным триггером
`voxel style`; примеры автора (4 изображения) скачаны и просмотрены локально: объёмные
voxel-животные и изометрический замок, то есть **3D-геометрия**, а не плоские спрайты.
`ntc-ai/SDXL-LoRA-slider.voxels` (8.8 МБ, MIT) — более дешёвый, но слабый «слайдер»
вокселизации: оставлен как отмеченная альтернатива, не скачивался.

**ControlNet — один union-файл вместо трёх.** `controlnet-union-sdxl-1.0` покрывает canny,
lineart, openpose/dwpose и depth в одном 2.5 ГБ файле; три отдельных SDXL ControlNet по ~2.5 ГБ
(7.5 ГБ) на 8 ГБ VRAM и на диске хуже без выигрыша. promax-вариант (+12 контролей) не взят:
лишние возможности не нужны, а размер тот же.

**IPAdapter — vit-h, а не vit-bigG.** `ip-adapter-plus_sdxl_vit-h` (848 МБ) требует CLIP ViT-H
(2.5 ГБ), который уже нужен для SD1.5-ветки; `ip-adapter_sdxl` требует ViT-bigG (3.69 ГБ) —
на 8 ГБ VRAM это лишние ~1.2 ГБ весов ради того же эффекта.

## Ожидания по памяти (НЕ измерено на момент решения)

SDXL fp16: UNet ≈ 5.1 ГБ + text encoders ≈ 1.6 ГБ + VAE ≈ 0.16 ГБ ≈ **6.9 ГБ весов**.
При 8 ГБ карты и `--reserve-vram 1` это возможно только с частичной выгрузкой/подкачкой;
`--novram --reserve-vram 5` (режим SD1.5) для SDXL заведомо тяжёлый. Итоговые пики RAM/VRAM
фиксируются только фактическим прогоном (см. отчёт); до замера режим не объявляется оптимальным.

## Последствия

- Стиль остаётся **экспериментальным**: `READY_FOR_OWNER_GENERATION_REVIEW`, не `STYLE_APPROVED`.
- Если LoRA даёт «игрушечный» воксельный вид, а не игрового персонажа — альтернатива уже
  подготовлена (`DREAMSHAPER_XL_REFERENCE_LOCK.json` + утверждённый референс).
- Обучение style-LoRA не начинается: сначала доказательство, что prompt+LoRA/IPAdapter держат стиль.
