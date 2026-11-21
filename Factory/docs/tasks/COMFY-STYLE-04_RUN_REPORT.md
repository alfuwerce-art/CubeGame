# COMFY-STYLE-04 — RUN REPORT (DreamShaper XL + Pixel-Textured 3D)

**Дата:** 2026-11-13 (обновлён 2026-11-14 после download recovery) · **Ветка:** `factory/work` · **Репозиторий:** `alfuwerce-art/factoria`
**Основание:** AUTHORIZED_FOR_SETUP, решение `FACT-D20` (экспериментальный стиль)
**Статус:** `READY_FOR_OWNER_GENERATION_REVIEW` — все веса скачаны и сверены по SHA-256 (см. §7, §9),
6 из 7 workflow прошли серверную валидацию; реальные пики RAM/VRAM и художественная оценка —
NOT_TESTED (шаг владельца).

---

## 1. Сводка

```text
TASK=COMFY-STYLE-04
STATUS=READY_FOR_OWNER_GENERATION_REVIEW   (веса все скачаны и проверены 2026-11-14; генерация и пики — за владельцем)

COMFY_PATH=D:\programs\ComfyUI (существующая установка, вторая не создавалась)
COMFY_VERSION=0.45.0
FACTORIA_LAB=D:\Projects\CubeGame Factory   (рабочая папка лаборатории/репозитория)
CUBEGAME_PROJECT=D:\Projects\CubeGame       (игровой проект; НЕ изменялся)
PYTHON=3.13.14 embedded | TORCH=2.14.1+cu130 | CUDA=13.0 | GPU RTX 4060 Laptop 8188 MiB

CHECKPOINT=DreamShaperXL_Turbo_v2_1.safetensors (Lykon/dreamshaper-xl-v2-turbo, openrail++)
CHECKPOINT_SIZE=6 939 220 250 B
CHECKPOINT_SHA256=4496b36d48bfd7cfe4e5dbce3485db567bcefa2bef7238d290dbd45612125083
SD15_BACKUP=v1-5-pruned-emaonly-fp16.safetensors — сохранён, не изменялся

STYLE_MODULE=VoxelXL_v1.safetensors (Fictiverse/Voxel_XL_Lora, openrail, trigger "voxel style")
IPADAPTER=ip-adapter-plus_sdxl_vit-h.safetensors (apache-2.0) + CLIP-ViT-H-14 (2.53 GB)
CONTROLNET=controlnet-union-sdxl-1.0.safetensors (xinsir, apache-2.0)

LAUNCHER_DEFAULT=scripts\comfyui\launch_dreamshaper_xl.bat        (--reserve-vram 1)
LAUNCHER_RAM_FIRST=scripts\comfyui\launch_dreamshaper_xl_novram.bat (--novram --reserve-vram 5)
LAUNCHER_CPU=scripts\comfyui\launch_cpu_fallback.bat              (--cpu)

WORKFLOWS=workflows\dreamshaper_xl\{DREAMSHAPER_XL_BASE,DREAMSHAPER_XL_PIXEL_STYLE,
          DREAMSHAPER_XL_REFERENCE_LOCK,DREAMSHAPER_XL_POSE_CONTROL}.json
PROMPTS=workflows\dreamshaper_xl\prompt_orc_v1.json (§first_test)

GIT_BRANCH=factory/work
```

## 2. Фактическая среда (проверено)

| Пункт | Факт |
|---|---|
| ComfyUI | `D:\programs\ComfyUI`, 0.45.0, portable, `python_embeded` 3.13.14, torch 2.14.1+cu130 |
| Рабочая папка Factoria | `D:\Projects\CubeGame Factory` — репозиторий лаборатории (`factory/work`) |
| Игровой проект CubeGame | `D:\Projects\CubeGame` — существует, **не открывался и не изменялся** (только `git status`, read-only) |
| Вторая установка ComfyUI | не создавалась |
| SD 1.5 | сохранён как резервный (`v1-5-pruned-emaonly-fp16.safetensors`, 2.03 ГБ) |
| Существующие workflow | не изменялись (`workflows/factoria_voxel_style/*` на месте) |
| Загрузка во время работ | сервер владельца на порту 8188 (pid 24988, `--novram --reserve-vram 5`) + Unity занимали RAM/GPU — поэтому тяжёлые прогоны не запускались |

## 3. Выбор моделей (обоснование — `docs/decisions/FACT-D20-dreamshaper-xl-experimental-style.md`)

| Роль | Файл | Лицензия | Размер | SHA-256 |
|---|---|---|---|---|
| Checkpoint | `DreamShaperXL_Turbo_v2_1.safetensors` | openrail++ | 6 939 220 250 B | `4496b36d…5083` |
| LoRA | `VoxelXL_v1.safetensors` | openrail | 912 554 516 B | `7d9a5f11…8b5c` |
| IPAdapter | `ip-adapter-plus_sdxl_vit-h.safetensors` | apache-2.0 | 847 517 512 B | `3f5062b8…6581` |
| CLIP Vision | `CLIP-ViT-H-14-laion2B-s32B-b79K.safetensors` | open_clip/MIT, веса LAION-2B | 2 528 373 448 B | `6ca9667d…b030` |
| ControlNet | `controlnet-union-sdxl-1.0.safetensors` | apache-2.0 | 2 512 030 408 B | `a9e13fd6…7467` |

Отклонено и почему:
- `nerijs/pixel-art-xl` — 2D pixel art, прямо противоречит требованию «объёмный 3D-персонаж»;
- одиночный single-file DreamShaper XL 1.0 — только Civitai (нужен аккаунт/токен; не используется),
  на HF `Lykon/dreamshaper-xl-1-0` есть лишь diffusers-раскладка;
- 3 отдельных SDXL ControlNet (canny + lineart + openpose ≈ 7.5 ГБ) — заменены одним union-файлом;
- `ip-adapter_sdxl` (ViT-bigG 3.69 ГБ) — дороже по VRAM без выигрыша для нашей задачи;
- `ntc-ai/SDXL-LoRA-slider.voxels` (8.8 МБ) — оставлен как дешёвая альтернатива, не скачивался.

## 4. Workflow

Все четыре собраны **программно** из `prompt_orc_v1.json` и живого `/api/object_info`
(`scripts/comfyui/build_dreamshaper_xl_workflows.py`), затем проверены структурно
(`measurements/dreamshaper_xl_build_report.json`).

| Workflow | Ноды / связи | Что внутри | Статус |
|---|---|---|---|
| `DREAMSHAPER_XL_BASE.json` | 8 / 9 | SDXL txt2img 768×1024 batch 1, 8 шагов, CFG 2.0, fixed seed, SaveImage `factoria/xl_base` | собран, структурно PASS |
| `DREAMSHAPER_XL_PIXEL_STYLE.json` | 9 / 11 | + `LoraLoader` VoxelXL (weight 1.0), триггер `voxel style` первым в промпте | собран, структурно PASS |
| `DREAMSHAPER_XL_REFERENCE_LOCK.json` | 11 / 13 | + `LoadImage` → `IPAdapterUnifiedLoader` → `IPAdapterAdvanced` (weight 0.7, `style transfer`) | собран, структурно PASS |
| `DREAMSHAPER_XL_POSE_CONTROL.json` | 14 / 16 | + ControlNet-ветка: LoadImage → Canny/Lineart/DW-Pose (MUTE) → ControlNetLoader (MUTE) → ControlNetApplyAdvanced (**BYPASS**) | собран, структурно PASS |

Принципы, чтобы не было «незаметно неправильной ветки»:
- LoRA/IPAdapter-ноды **не** замаскированы: нет весов → явная ошибка при Queue, тихого отката нет;
- ControlNet-ветка выключена видимо (BYPASS + MUTE) и включается владельцем осознанно;
- промпты лежат в контракте и продублированы в нодах; первый Orc-тест — отдельные ноды
  `POSITIVE - first Orc test` / `NEGATIVE`.

## 5. Запуск

```bat
D:\Projects\CubeGame Factory\scripts\comfyui\launch_dreamshaper_xl.bat        :: --reserve-vram 1
D:\Projects\CubeGame Factory\scripts\comfyui\launch_dreamshaper_xl_novram.bat :: RAM-first
D:\Projects\CubeGame Factory\scripts\comfyui\launch_cpu_fallback.bat         :: --cpu
```

`--novram --reserve-vram 5` (режим SD1.5) для SDXL проверен как **нежелательный по умолчанию**:
он оставляет карте ~3 ГБ при ~6.9 ГБ весов SDXL. Поэтому для SDXL сделан отдельный launcher с
`--reserve-vram 1` (~7 ГБ карты). Оба варианта оставлены, чтобы владелец мог сравнить на практике.
`--lowvram/--highvram/--gpu-only/--cpu/--novram` — одна взаимоисключающая группа, вместе не подаются.

## 6. Технические проверки

| Проверка | Результат |
|---|---|
| Целостность весов (SHA-256) | см. §7 — статус на момент отчёта |
| Структурная сборка 4 workflow против `/api/object_info` | **PASS** |
| Регистрация нод (LoRA, IPAdapter, ControlNet, препроцессоры) | **PASS** (в `/object_info` 1213 типов, все нужные присутствуют) |
| Ошибки импорта custom nodes | **нет** (проверено ранее в COMFY-STYLE-02/03, состояние не менялось) |
| Прежние workflow / SD 1.5 | не изменялись |
| Загрузка SDXL через torch/CUDA | ожидает завершения загрузки чекпоинта |
| Реальный прогон и пики RAM/VRAM | **NOT_TESTED** — на машине работали ComfyUI владельца и Unity |

## 7. Состояние загрузок

**Все веса скачаны и проверены (2026-11-14, recovery — см. §9).**

| Файл | Размер | SHA-256 | Статус |
|---|---|---|---|
| `DreamShaperXL_Turbo_v2_1.safetensors` | 6 939 220 250 B | `4496b36d…5083` | **OK** |
| `CLIP-ViT-H-14-laion2B-s32B-b79K.safetensors` | 2 528 373 448 B | `6ca9667d…b030` | **OK** |
| `ip-adapter-plus_sdxl_vit-h.safetensors` | 847 517 512 B | `3f5062b8…6581` | **OK** |
| `controlnet-union-sdxl-1.0.safetensors` | 2 512 030 408 B | `a9e13fd6…7467` | **OK** (докачан после зависания) |
| `VoxelXL_v1.safetensors` | 912 554 516 B | `7d9a5f11…8b5c` | **OK** |
| `ral-vxl.safetensors` (Civitai) | 228 453 420 B | `9dc480ee…a25` | **OK** |

Скорость HF на этой машине: 0.4–1.0 МБ/с (одиночный поток падает до 20–60 КБ/с, 24 соединения хуже).
Загрузчик после recovery работает на **4 соединениях по умолчанию** с watchdog'ом и backoff
(подробности — §9.2). Повторный запуск безопасен и идемпотентен:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File `
  "D:\Projects\CubeGame Factory\scripts\comfyui\fetch_style04_models.ps1" -Connections 4
```

**Не запускать два экземпляра одновременно** (два писателя в одни части).

## 8. Что осталось (следующий шаг)

Все веса скачаны и проверены (см. §7 и §9). Осталось измерить и оценить — это шаг владельца:

1. Проверить загрузку чекпоинта без генерации:
   `python_embeded\python.exe -s scripts\comfyui\load_checkpoint_probe.py` (с `FACTORIA_CKPT`).
2. Технический прогон графа (не художественная оценка) и замер пиков RAM/VRAM:
   `scripts\comfyui\xl_run_workflow.py --workflow workflows\dreamshaper_xl\DREAMSHAPER_XL_BASE.json ...`.
3. Владелец: открыть `DREAMSHAPER_XL_PIXEL_STYLE.json` (или `DREAMSHAPER_XL_CIVITAI_VOXEL.json`),
   Queue, оценить персонажа и записать пики из Crystools.

## 9. Download recovery 2026-11-14 (после зависания ControlNet)

### 9.1 Причина

Симптом: на `controlnet-union-sdxl-1.0.safetensors` 11 из 12 диапазонов завершились, а оставшиеся
соединения «висели» часами: размеры частей не менялись, при этом TCP был `ESTABLISHED` на edge
HuggingFace, а свежий одиночный Range-запрос к тому же файлу мгновенно получал `HTTP 206` + данные.

Причина — две независимые вещи:

1. **Сторона CDN.** HF периодически принимает TCP-соединение под Range-запрос и не отправляет тело
   (соединение остаётся `ESTABLISHED` с нулевым трафиком) либо «дотягивает» тело и не закрывает
   соединение. Свежий сокет к тому же файлу при этом отдаёт `206` и данные за секунды.
2. **Дефект загрузчика (главная причина остановки).** Старая версия:
   - не задавала `--connect-timeout` — зависание до тела запроса ничем не ограничено;
   - полагалась на `--speed-limit/--speed-time`, которые **не срабатывают до начала передачи**;
   - держала `--max-time 3600`, то есть одно зависшее соединение блокировало файл до часа;
   - и, критично, ждала завершения **всех** соединений раунда (`WaitForExit`) и только потом
     сливала `.tail` и проверяла прогресс → одно мёртвое соединение замораживало весь файл.

Параллелизм 12 **не является доказанной причиной**: диапазоны независимы, а прогон на 4
соединениях шёл ровно (~1 МБ/с). 12 лишь увеличивало число сокетов, попадающих под зависание.

Дополнительно найдена **ловушка измерения**: Windows может отдавать устаревший размер файла, пока
curl держит его открытым. Проверки «GrowthMB = 0 за 30 с» по каталогу частей ненадёжны;
достоверны строки прогресса загрузчика и размеры после закрытия процесса.

### 9.2 Что изменено в `scripts\comfyui\fetch_checkpoint_parallel.ps1`

| Изменение | Зачем |
|---|---|
| Preflight HTTP Range (`GET 0-0` → требование `206` + `content-range`) | не начинать параллельную загрузку, если Range не поддерживается |
| `--connect-timeout 20`, `--max-time 1800` | ограничить зависание на connect и на попытку |
| **Per-connection watchdog** (`-StallSeconds 60`): убивается только зависшее соединение | один мёртвый сокет больше не блокирует файл |
| Повтор с ограниченным экспоненциальным backoff (1→2→4…→30 с), только если раунд без прироста; `-MaxAttempts 6` на часть | не молотить сервер и не терять прогресс |
| `-MaxConcurrent` отдельно от `-Connections` (по умолчанию 4) | 12-частевую раскладку можно продолжать 4 сокетами |
| Байтовое возобновление через `<part>.tail`; части **никогда не удаляются** | «не начинать 13 ГБ заново» |
| Проверка размера каждой части перед переиспользованием и перед сборкой; сборка только при точном совпадении; атомарная замена; SHA-256 | модель не считается готовой без проверки |
| Прогресс: МБ, %, мгновенная скорость (по окну, не средняя), ETA, время последних данных | видно, жива ли загрузка |
| Параллелизм по умолчанию снижен до 4 | меньше сокетов под зависание |

### 9.3 Результаты восстановления

| Шаг | Результат |
|---|---|
| Диагностика до вмешательства | 6 соединений `ESTABLISHED` к HF edge, 0 прироста файлов >40 мин; свежий Range-запрос → `206` + 6.3 МБ за 30 с |
| Остановка старого загрузчика | только его дерево (28372 → 25928 и его curl'ы), после диагностики |
| ControlNet возобновлён | 11/12 частей переиспользованы (2 261.8 МБ), докачано **133.9 МБ за 172 с** |
| ControlNet SHA-256 | `a9e13fd61f3193887791c8a0dd07a07202174dc47d5ddaea94ea1344f07c7467` — **совпал** (2 512 030 408 B) |
| VoxelXL LoRA (п.9 задачи) | 912 554 516 B, 4 соединения, ~15 мин, SHA `7d9a5f11…8b5c` — **совпал** |
| Независимая перепроверка 6 файлов (`Get-FileHash`) | размер + SHA-256 **OK**: DreamShaper XL, CLIP ViT-H, ipadapter SDXL, ControlNet union, VoxelXL, ral-vxl |
| Остатки `.parts` | нет |
| Серверная валидация workflow | **6/7 PASS** (`BASE`, `PIXEL_STYLE`, `POSE_CONTROL`, `CIVITAI_VOXEL` орк/человек/эльф); `REFERENCE_LOCK` — только отсутствующий референс (`PASS_WITH_OWNER_ACTION`) |

**Статус восстановления: `RECOVERY_VERIFIED`.** Все веса скачаны и проверены; генерация и пики
RAM/VRAM по-прежнему **не измерялись** (`NOT_TESTED`) — следующий шаг владельца.

---

## 10. Известные ограничения

- Turbo-модель: 6–10 шагов и CFG 1.5–2.5; тонкая детализация слабее, чем у классического DSXL.
- LoRA `VoxelXL` может давать «игрушечный» воксельный вид; альтернатива — `REFERENCE_LOCK`.
- SDXL на 8 ГБ VRAM с большой вероятностью превысит лимит «+2 ГБ VRAM» — это будет измерено и
  зафиксировано честно; лимит не «подкручивается».
- IPAdapter/ControlNet требуют ещё ~5.9 ГБ весов на диске (место есть: 630 ГБ свободно).
