# Factory Reference — генерация персонажей по утверждённым референсам (COMFY-STYLE-07)

Набор ComfyUI-графов, в которых персонаж привязан к **референсу из CubeGame**, а не только к текстовому промпту.
Чекпоинт — обычный SDXL Base 1.0 (в графах по умолчанию стоит доступный DreamShaper XL Turbo, переключается в ноде `A. MODEL`); настройки Turbo (CFG 2 / 4–8 шагов) здесь не применяются. Во всех графах есть нода `STYLE LoRA` — `VoxelXL_v1`, strength 0.6 (0.0 = выключено).

## Файлы

| Файл | Что делает | Ключевые параметры |
|---|---|---|
| `reference_characters_v1.json` | конфиг: модель, сэмплинг, LoRA, референсы, промпты трёх персонажей | steps 28, CFG 6.0, `dpmpp_2m`/`karras`, seed 20261114, LoRA VoxelXL 0.6 |
| `FACTORY_REFERENCE_IPADAPTER.json` | SDXL + CLIP ViT-H + `ip-adapter-plus_sdxl_vit-h` | weight 1.0 (0.6/0.8/1.0), weight_type `linear` / `style transfer` |
| `FACTORY_REFERENCE_CONTROLNET.json` | SDXL + union ControlNet, контур Canny с референса | strength 0.9 (0.6/0.9), Canny 0.35/0.75 |
| `FACTORY_REFERENCE_IMG2IMG.json` | Load Image → VAE Encode → KSampler → VAE Decode | denoise 0.40 (0.25/0.40/0.55) |
| `FACTORY_REFERENCE_IPADAPTER_CONTROLNET.json` | IPAdapter-модель + ControlNet-кондиционирование одновременно | IPA 0.8 + CN 0.8 |
| `reference_test_matrix_v1.json` | 10 прогонов сравнения для человека-воина | см. отчёт |

## Референсы

Готовятся из скриншотов владельца скриптом `scripts/comfyui/prepare_character_reference.py`
(k-means trimap + GrabCut, без AI-весов, без ресемплинга стиля) и кладутся в
`references/characters_v1/` (локально, не публикуются). Для ComfyUI копируются в `input/`:

```
factoria_ref_human_warrior_768x1024.png
factoria_ref_elf_ranger_768x1024.png
factoria_ref_dwarf_warrior_768x1024.png
```

## Как запустить

1. Докачать официальный чекпоинт (перезапускаемый загрузчик):
   ```
   powershell -ExecutionPolicy Bypass -File scripts\comfyui\fetch_checkpoint_parallel.ps1 ^
     -Url https://huggingface.co/stabilityai/stable-diffusion-xl-base-1.0/resolve/main/sd_xl_base_1.0.safetensors ^
     -Target D:\programs\ComfyUI\ComfyUI\models\checkpoints\sd_xl_base_1.0.safetensors ^
     -ExpectedSha256 31e35c80fc4829d14f90153f4c74cd59c90b779f6afe05a74cd6120b893f7e5b ^
     -TotalBytes 6938078334 -Connections 12
   ```
2. Перезапустить ComfyUI (список чекпоинтов читается на старте):
   ```
   powershell -File scripts\comfyui\factoria_launch.ps1 -Preset xl -Port 8190 -DisableAssets -KeepRunning
   ```
3. Собрать/перепроверить графы:
   ```
   D:\programs\ComfyUI\python_embeded\python.exe -s scripts\comfyui\build_reference_workflows.py ^
     --object-info http://127.0.0.1:8190/object_info ^
     --config workflows/factory_reference/reference_characters_v1.json ^
     --out workflows/factory_reference --report measurements/reference_workflows_build.json
   ```
4. Прогнать сравнение человека-воина:
   ```
   D:\programs\ComfyUI\python_embeded\python.exe -s scripts/comfyui/reference_run_batch.py ^
     --url http://127.0.0.1:8190 --server-pid <PID> ^
     --config workflows/factory_reference/reference_characters_v1.json ^
     --matrix workflows/factory_reference/reference_test_matrix_v1.json ^
     --workflow-dir workflows/factory_reference ^
     --comfy-input D:\programs\ComfyUI\ComfyUI\input ^
     --out outputs/factory/reference_v1 ^
     --report measurements/reference_batch_report_v1.json
   ```
5. Контактный лист: `reference_contact_sheet.py --report … --out outputs/factory/reference_v1/contact_sheet.png`.

Лимиты: RAM ≤ 15 ГБ на прогон, VRAM 8 ГБ (768×1024; IPAdapter+ControlNet — с offload). Прогоны только последовательные.
