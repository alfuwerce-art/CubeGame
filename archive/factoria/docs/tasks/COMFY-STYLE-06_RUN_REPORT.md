# COMFY-STYLE-06 — RUN REPORT (SDXL Base 1.0 + Fictiverse Voxel XL)

**Дата:** 2026-11-14 · **Ветка:** `factory/work` · **Исполнитель:** DeepSeek
**Основание:** COMFY-STYLE-05 провалил визуальную приёмку (на SDXL-Turbo при CFG 2.0 LoRA Fictiverse не влияла).
**Статус:** `READY_FOR_OWNER_VISUAL_REVIEW` — LoRA на обычном SDXL **заработала** (технически подтверждено),
но остаются ограничения; художественная приёмка не выполнена и не заявляется.

---

## 1. Сводка

```text
TASK=COMFY-STYLE-06
STATUS=READY_FOR_OWNER_VISUAL_REVIEW

CHECKPOINT=sd_xl_base_1.0.safetensors
  source = https://huggingface.co/stabilityai/stable-diffusion-xl-base-1.0  (Stability AI, официальный)
  size   = 6 938 078 334 B      license = openrail++ (CreativeML OpenRAIL++-M)
  sha256 = 31e35c80fc4829d14f90153f4c74cd59c90b779f6afe05a74cd6120b893f7e5b   → СОВПАЛ (дважды: скрипт + Get-FileHash)

DREAMSHAPER_XL_TURBO=сохранён (6 617.8 MB), старые workflow не изменялись
WORKFLOWS:
  workflows/factory_characters_v2/FACTORY_CHARACTER_SDXL_BASE_V2.json       (28 шагов, CFG 6.0, 768x1024)
  workflows/factory_characters_v2/FACTORY_CHARACTER_SDXL_BASE_V2_DIAG.json  (короткий промпт)
  workflows/factory_characters_v2/factory_test_matrix_v2.json               (10 прогонов)
  workflows/factory_characters_v2/sdxl_base_v2.json                         (конфиг)
CONFIG_LOKI: VoxelXL_v1.safetensors, trigger `voxel style`, strengths 0.4/0.6/0.8
RAL: НЕ используется (FACT-D21)
SERVER: отдельный экземпляр на порту 8191 (--reserve-vram 1 --disable-assets); 8188 владельца не останавливался
```

**Важно (координация):** параллельная сессия в этом же репозитории уже ведёт этап COMFY-STYLE-07 и удалила
`workflows/factory_characters` (этап 05). Работа этого этапа положена в отдельный каталог
`workflows/factory_characters_v2/`, чтобы ничего не перезаписывать. Загрузка SDXL Base 1.0 нужна обоим
этапам — файл один, скачан один раз.

## 2. Аудит (п.1–2, 4 ТЗ)

| Проверка | Результат |
|---|---|
| SDXL Base 1.0: источник, лицензия, размер, SHA-256 | openrail++, 6 938 078 334 B, хэш совпал |
| DreamShaper XL Turbo сохранён | да, в `models/checkpoints` рядом |
| Прежние workflow | не изменялись (этап 05 переведён в `factory_characters_v2`) |
| LoRA Fictiverse / IPAdapter / CLIP Vision / ControlNet | на месте, SHA-256 сверены ранее |
| ComfyUI видит новый checkpoint | `CheckpointLoaderSimple.ckpt_name` содержит `sd_xl_base_1.0.safetensors` |
| Серверная валидация графов | `FACTORY_CHARACTER_SDXL_BASE_V2.json` и `..._DIAG.json` — **PASS** (`validate_prompt`) |

## 3. Настройки и прогоны (п.5–10 ТЗ)

Сетка: 28 шагов (диапазон 25–30), CFG 5/6/7, LoRA 0.4/0.6/0.8, 768×1024, batch 1, seed 777001,
sampler-свип: `dpmpp_2m/karras`, `dpmpp_sde/karras`, `euler_ancestral/normal`.

| Прогон | Sampler / scheduler | CFG | LoRA | Время | Пик VRAM (вся карта) | Пик RAM процесса |
|---|---|---|---|---|---|---|
| `v2_smp_dpmpp_2m` | dpmpp_2m / karras | 6.0 | 0.6 | **34.1 с** | 7 688 MB | 6 338 MB |
| `v2_smp_dpmpp_sde` | dpmpp_sde / karras | 6.0 | 0.6 | 55.3 с | 7 598 MB | 6 795 MB |
| `v2_smp_euler_ancestral` | euler_ancestral / normal | 6.0 | 0.6 | 29.1 с | 7 757 MB | 6 806 MB |
| `v2_cfg5` | dpmpp_2m / karras | 5.0 | 0.6 | 30.2 с | 7 634 MB | 6 808 MB |
| `v2_cfg6` | dpmpp_2m / karras | 6.0 | 0.6 | 31.1 с | 7 751 MB | 6 810 MB |
| `v2_cfg7` | dpmpp_2m / karras | 7.0 | 0.6 | 31.1 с | 7 685 MB | 6 823 MB |
| `v2_lora4` | dpmpp_2m / karras | 6.0 | 0.4 | 32.5 с | 7 737 MB | 6 817 MB |
| `v2_lora6` | dpmpp_2m / karras | 6.0 | 0.6 | 33.1 с | 7 635 MB | 6 824 MB |
| `v2_lora8` | dpmpp_2m / karras | 6.0 | 0.8 | 32.1 с | 7 763 MB | 6 824 MB |
| `v2_diag_short` | dpmpp_2m / karras | 6.0 | 0.6 | 31.1 с | 7 733 MB | 6 849 MB |

**10/10 прогонов PASS, 0 FAIL, 0 OOM.** Пик VRAM «вся карта» включает модель параллельной сессии
(~2.8 ГБ в тот момент) → собственный пик ComfyUI ниже. Пик RAM процесса 6.3–6.9 ГБ (выше, чем у Turbo,
из-за offload при нехватке VRAM). Crystools/телеметрия снимались раннером.

### Выбор sampler/scheduler по факту

- **лучший баланс: `dpmpp_2m` / `karras`** — 29–34 с, чистая картинка, стабильно повторяемо;
- `dpmpp_sde` / karras — 55 с (самый медленный) и заметно более шумные/артефактные кадры на этих настройках;
- `euler_ancestral` / normal — быстрый (29 с), но даёт более «серые»/недорисованные кадры при 28 шагах.

## 4. Что показали изображения (техническая оценка, НЕ художественная приёмка)

1. **LoRA Fictiverse на обычном SDXL наконец работает.** При CFG 5–7 и 28 шагах появляется блочная
   геометрия и «пиксельные» (клетчатые/блочные) поверхности — на Turbo (CFG 2) этого не было вовсе.
   Эффект монотонно усиливается с силой LoRA: 0.4 — блочность только в броне/ткани, лицо гладкое;
   0.6 — выраженные воксельные пластины, лицо взрослое; 0.8 — сильнее вокселизация, детализация ниже.
   **Рекомендация по силе: 0.6** (0.8 — при необходимости усилить стиль ценой лица).
2. **Длинный структурный промпт «размывает» расу:** в прогонах с длинным промптом получаются
   бородатые люди в броне, а не орки (зелёная кожа и клыки не удерживаются).
3. **Короткий диагностический промпт даёт орков** (зелёная кожа, клыки, блочная геометрия) — то есть
   для расы работает короткая формулировка + LoRA, а не длинный список атрибутов.
4. **Главная нерешённая проблема — «character sheet»:** в 8 из 10 кадров по 2–3 фигуры/ракурса вместо
   одной, несмотря на усиленный negative (`character sheet, turnaround, multiple views, multiple characters,
   two characters, three characters, collage, split image, repeated figures, duplicate, single character only`).
   На CFG 6 negative работает, но недостаточно. Это блокер для Tripo (нужен один персонаж в кадре).
5. **Кадрирование/конечности:** обрезанных голов/стоп нет, A-pose в целом соблюдается.
6. **Лицо:** взрослое (без chibi), но детализация воксельного лица нестабильна — при 0.6–0.8 лицо
   упрощается; читаемость на игровом масштабе не проверялась.

Контактный лист: `outputs/factory/characters_v2/contact_sheet_v2.png` (10 кадров, подписи: sampler/CFG/LoRA,
seed, время, пик VRAM).

## 5. Выводы и следующий шаг

- **Стилевой стек рабочий:** SDXL Base 1.0 + `VoxelXL` (0.6) + `dpmpp_2m/karras`, CFG 6, 28 шагов,
  768×1024 — это рекомендуемая база для дальнейших итераций.
- **Промпт надо сокращать:** рабочий вариант — короткое описание расы + стилевой триггер, а не длинный
  список атрибутов (длинный текст теряет расу).
- **Блокер один: несколько фигур в кадре.** Варианты решения (нужно решение владельца, без скачивания
  новых моделей): (а) img2img/региональная маска по первому удачному кадру; (б) более сильный negative +
  CFG 7–8 с проверкой деградации; (в) кроп кадра и повторный img2img для доводки.
- 1024×1024 не проверялся: свободной VRAM было ~2.8 ГБ (модель параллельной сессии занимала карту).
- RAL LoRA не использовалась (лицензия).

## 6. Ограничения

- На момент прогонов на GPU параллельно висела модель другой сессии — пики VRAM «вся карта» завышены,
  но собственные замеры стабильны; повторный прогон на свободной карте даст более чистые цифры.
- Независимого регионального управления BODY/FACE нет (только текст).
- Fictiverse: openrail — коммерческое использование требует отдельной проверки.

## 7. Как воспроизвести

```powershell
# отдельный экземпляр (8188 владельца не трогаем)
powershell -NoProfile -ExecutionPolicy Bypass -File `
  "D:\Projects\CubeGame Factory\scripts\comfyui\factoria_launch.ps1" -Preset xl -Port 8191 -DisableAssets -KeepRunning
$py = 'D:\programs\ComfyUI\python_embeded\python.exe'
& $py -s "D:\Projects\CubeGame Factory\scripts\comfyui\factory_run_batch.py" `
  --url http://127.0.0.1:8191 --server-pid <PID> `
  --config "D:\Projects\CubeGame Factory\workflows\factory_characters_v2\sdxl_base_v2.json" `
  --matrix "D:\Projects\CubeGame Factory\workflows\factory_characters_v2\factory_test_matrix_v2.json" `
  --workflow-dir "D:\Projects\CubeGame Factory\workflows\factory_characters_v2" `
  --out "D:\Projects\CubeGame Factory\outputs\factory\characters_v2" `
  --report "D:\Projects\CubeGame Factory\measurements\factory_batch_report_v2.json"
& $py -s "D:\Projects\CubeGame Factory\scripts\comfyui\factory_contact_sheet.py" `
  --report "...\measurements\factory_batch_report_v2.json" --out "...\outputs\factory\characters_v2\contact_sheet_v2.png"
```

UI: `http://127.0.0.1:8191` → `Workflow → Open` → `FACTORY_CHARACTER_SDXL_BASE_V2_DIAG.json` (для расы)
или `FACTORY_CHARACTER_SDXL_BASE_V2.json` (структурный промпт).
