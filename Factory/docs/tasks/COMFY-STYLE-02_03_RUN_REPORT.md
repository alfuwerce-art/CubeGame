# COMFY-STYLE-02/03 — RUN REPORT (ComfyUI под FACTORIA VOXEL_STYLE)

**Дата:** 2026-11-13 · **Ветка:** `factory/work` · **Репозиторий:** `alfuwerce-art/factoria`
**Основание:** TEXT_TZ COMFY-STYLE-02/03 (AUTHORIZED), решение `FACT-D19`
**Статус:** `READY_FOR_REVIEW` — см. §9

---

## 1. Итоговая сводка

```text
TASK=COMFY-STYLE-02/03
STATUS=READY_FOR_REVIEW

COMFY_PATH=D:\programs\ComfyUI
COMFY_VERSION=0.45.0  (frontend 1.58.5)
PYTHON=3.13.14 (embedded python_embeded, изолированный)
TORCH=2.14.1+cu130
CUDA=13.0 (runtime в поставке torch; CUDA Toolkit не устанавливался), GPU RTX 4060 Laptop 8188 MiB, driver 610.47

MANAGER=comfyui_manager 4.2.2 (PyPI, pin из ComfyUI\manager_requirements.txt) — INSTALLED
CRYSTOOLS=ComfyUI-Crystools 1.27.4 @2f18256 — INSTALLED, монитор проверен
IPADAPTER_PLUS=ComfyUI_IPAdapter_plus @a0f451a — INSTALLED (ноды зарегистрированы)
CONTROLNET_AUX=comfyui_controlnet_aux @a587a4c — INSTALLED (Canny/Lineart/DW-Pose/Depth в /object_info)
RGTHREE=rgthree-comfy @bc48c6a — INSTALLED (48 нод)

CHECKPOINT=v1-5-pruned-emaonly-fp16.safetensors
CHECKPOINT_SIZE=2 132 696 762 B (2.03 GB)
CHECKPOINT_SHA256=e9476a13728cd75d8279f6ec8bad753a66a1957ca375a1464dc63b37db6e3916  (СОВПАЛ с LFS-OID HF)
CHECKPOINT_LICENSE=CreativeML OpenRAIL-M

SAFE_BASELINE_LAUNCH=scripts\comfyui\launch_safe_baseline.bat  (--novram --reserve-vram 5)
CPU_FALLBACK_LAUNCH=scripts\comfyui\launch_cpu_fallback.bat    (--cpu)

BASE_WORKFLOW=workflows\factoria_voxel_style\FACTORIA_VOXEL_STYLE_BASE.json
STYLE_REFERENCE_TEMPLATE=workflows\factoria_voxel_style\FACTORIA_VOXEL_STYLE_IPADAPTER.json
IPADAPTER_WORKFLOW=workflows\factoria_voxel_style\FACTORIA_VOXEL_STYLE_IPADAPTER.json
CONTROL_TEMPLATE=workflows\factoria_voxel_style\FACTORIA_VOXEL_STYLE_CONTROL_TEMPLATE.json
VOXEL_STYLE_PROMPT_CONFIG=workflows\factoria_voxel_style\voxel_style_prompt_v1.json

WEIGHTS_DEFERRED=IPAdapter + ControlNet (не скачаны; список §6)

IPADAPTER_REQUIRED_WEIGHTS=см. §6 (не скачаны, ожидают согласования)
CONTROLNET_REQUIRED_WEIGHTS=см. §6 (не скачаны, ожидают согласования)

CUSTOM_NODE_IMPORT_ERRORS=none (в логах сервера нет IMPORT FAILED)
IDLE_RAM=SAFE_BASELINE 1025 MB (working set), CPU_FALLBACK 934 MB
IDLE_VRAM=SAFE_BASELINE +79 MiB относительно baseline, CPU_FALLBACK ≈ 0 (device=cpu)

BLOCKERS=см. §10

GIT_BRANCH=factory/work
PUSH=OK
REMOTE_SHA=f9c90ea (HEAD == origin/factory/work, проверено после push)

NEXT=owner-run first generation + RAM/VRAM measurement
```

---

## 2. Что проверено фактом (а не «настроено»)

| Проверка | Команда/метод | Результат |
|---|---|---|
| Существующий ComfyUI найден, второй не ставился | `Test-Path D:\programs\ComfyUI`, `comfyui_version.py` | 0.45.0, portable, 4.25 ГБ до наших установок |
| Embedded python / torch / CUDA | `python_embeded\python.exe -c "import torch..."` | Python 3.13.14, torch 2.14.1+cu130, CUDA 13.0, `cuda.is_available()=True` |
| Сервер стартует в SAFE_BASELINE | `factoria_launch.ps1 -Preset safe -MeasureIdle` | `server_ready=True` за 11.2 с (тёплый старт), `/system_stats` 200 |
| **Launcher владельца** (`launch_safe_baseline.bat`, двойной клик) | `cmd /c scripts\comfyui\launch_safe_baseline.bat` → опрос `/system_stats` | READY за **14.3 с**, `device=cuda:0 RTX 4060 Laptop`, ноды поднялись (rgthree 48 нод) |
| Сервер стартует в CPU_FALLBACK | `factoria_launch.ps1 -Preset cpu -MeasureIdle` | `server_ready=True` за 11.2 с, `device=cpu`, лог Crystools: `CUDA is available, but torch is using CPU` |
| Web UI | `GET http://127.0.0.1:8188/` | HTTP 200, 19 885 Б |
| Custom nodes импортируются без ошибок | лог сервера `logs\COMFY-STYLE_server_*` | нет строк `IMPORT FAILED`/`Cannot import`; есть только информационное `comfy_kitchen backend triton: unavailable` (triton не требуется) |
| Crystools мониторинг | `GET /crystools/monitor/GPU` → `[{"index":0,"name":"NVIDIA GeForce RTX 4060 Laptop GPU"}]`; `GET /crystools/monitor/HDD` → `["C:\\","D:\\"]`; ws-событие `crystools.monitor` | backend работает, панель в UI присутствует (`custom_nodes\ComfyUI-Crystools\web\`) |
| Checkpoint детектируется | `/api/object_info` → `CheckpointLoaderSimple.ckpt_name` | `["v1-5-pruned-emaonly-fp16.safetensors"]` |
| Workflow принимается валидатором ComfyUI | `validate_factoria_workflows.py` (в процессе: `nodes.init_extra_nodes()` + `execution.validate_prompt`) | BASE: **PASS**, `prompt_nodes=7`, `outputs=['9']`; IPADAPTER и CONTROL_TEMPLATE: структурно PASS, упираются только в отсутствующий локальный reference-файл (owner action) |
| Структурная сборка workflow | `build_factoria_voxel_workflows.py` → проверка типов нод, слотов, типов связей, количества и значений widget'ов против живого `/object_info` | 3/3 PASS (`measurements\factoria_voxel_style_build_report.json`) |

**Совместимость IPAdapter_plus (требование COMFY-STYLE-02):** upstream `cubiq/ComfyUI_IPAdapter_plus` находится
в режиме maintenance-only (последний push 2025-04-14, GitHub API `pushed_at`), но **совместим с установленным
ComfyUI 0.45.0**: ноды `IPAdapterUnifiedLoader`, `IPAdapterAdvanced`, `IPAdapterModelLoader` регистрируются без
ошибок импорта и присутствуют в `/api/object_info` (1213 типов нод). При поломке в будущем — изолировать ноду,
а не откатывать всё окружение.

---

## 3. Установка custom nodes (детали)

Подробная таблица — `docs/INSTALLATION_LOG.md`, §1 (пункты 4–9).

- Все pip-установки — **только** в `D:\programs\ComfyUI\python_embeded` с файлом ограничений
  `scripts\comfyui\constraints_comfyui_embedded.txt`. После установки проверено, что
  `torch 2.14.1+cu130` и `cuda.is_available()=True` не изменились.
- ComfyUI-Manager поставлен как официальный PyPI-пакет (`comfyui_manager==4.2.2`), потому что
  именно эта версия прописана в `ComfyUI\manager_requirements.txt` самого релиза 0.45.0 —
  отдельного клона в `custom_nodes` нет, дубликата нет.
- Crystools, IPAdapter_plus, controlnet_aux, rgthree-comfy — shallow-клоны официальных репозиториев
  с зафиксированными commit'ами.
- **Найден и обойдён дефект запуска:** при перенаправлении stdout в файл (и в консоли с cp1251)
  `rgthree-comfy` печатает emoji и роняет сервер `UnicodeEncodeError`. В обоих launcher'ах
  выставлены `PYTHONUTF8=1` и `PYTHONIOENCODING=utf-8`. Без этого ComfyUI не стартовал.

---

## 4. Режимы запуска

| Режим | Файл | Аргументы | Смысл |
|---|---|---|---|
| SAFE_BASELINE | `scripts\comfyui\launch_safe_baseline.bat` | `--windows-standalone-build --disable-auto-launch --novram --reserve-vram 5 --port 8188` | динамическая VRAM выключена, веса живут в RAM, карте оставлено ~3 ГБ |
| CPU_FALLBACK | `scripts\comfyui\launch_cpu_fallback.bat` | `--windows-standalone-build --disable-auto-launch --cpu --port 8188` | ноль инференса на GPU |

`--lowvram` сознательно **не** добавлен: `--lowvram` и `--novram` находятся в одной
взаимоисключающей группе argparse, junto они не запускаются.

Воспроизводимый запуск с замером: `scripts\comfyui\factoria_launch.ps1 -Preset safe|cpu|gpu [-MeasureIdle] [-KeepRunning]`.

---

## 5. Измерения (idle, без генерации)

| Метрика | SAFE_BASELINE (`--novram --reserve-vram 5`) | CPU_FALLBACK (`--cpu`) |
|---|---|---|
| Старт до `/system_stats` 200 | 11.2 с (тёплый; первый холодный старт в PREP-03 ≈146 с) | 11.2 с |
| Рабочий набор процесса ComfyUI | **1025 MB** | **934 MB** |
| Свободная RAM до / после | 17.58 → 16.57 GB | 17.62 → 16.70 GB |
| VRAM всей карты до / после | 1476 → 1555 MiB (**+79 MiB**) | 1480 → 1473 MiB (**−7 MiB**) |
| Устройство torch | `cuda:0 NVIDIA GeForce RTX 4060 Laptop GPU` | `cpu` |

Лимиты этапа: RAM ≤ 15 GB, дополнительная VRAM ≤ 2 GB. **Idle укладывается с запасом**, но
это ничего не доказывает про пик генерации — пики измеряются владельцем на первом реальном
прогоне (см. §10).

Условие замера: параллельно шла догрузка checkpoint (curl, ~40 МБ RAM, 0 VRAM), другие
приложения не останавливались; до старта карта была занята на ~1.45 ГБ чужими процессами.

---

## 6. Требуемые веса, которые НЕ скачаны (нужно согласование)

Ничего из этого не загружалось — только определены точные файлы, размеры и лицензии.

### IPAdapter (для `FACTORIA_VOXEL_STYLE_IPADAPTER.json`)

| Файл | Куда | Размер | SHA-256 | Источник / лицензия |
|---|---|---|---|---|
| `ip-adapter-plus_sd15.safetensors` | `models\ipadapter\` | 98 183 288 B | `a1c250be40455cc61a43da1201ec3f1edaea71214865fb47f57927e06cbe4996` | HF `h94/IP-Adapter` → `models/ip-adapter-plus_sd15.safetensors`; **apache-2.0** |
| `CLIP-ViT-H-14-laion2B-s32B-b79K.safetensors` | `models\clip_vision\` | 2 528 373 448 B | `6ca9667da1ca9e0b0f75e46bb030f7e011f44f86cbfb8d5a36590fcd7507b030` | HF `h94/IP-Adapter` → `models/image_encoder/model.safetensors`, переименовать; CLIP ViT-H-14 (open_clip, MIT), веса обучены на LAION-2B |

Ожидаемая память (оценка, требует замера): CLIP Vision ViT-H ≈ 2.5 ГБ весов → при `--novram`
живёт в RAM, на GPU ~1.0–1.5 ГБ на время кодирования референса; IPAdapter-адаптер ≈ 100 МБ.
Итого ожидаемый прирост RAM ≈ +2.5 GB, VRAM ≈ +1–2 GB — **на границе лимита**, поэтому
измерять обязательно; при превышении — вариант `ip-adapter_sd15_light` (44.6 МБ) или CPU-режим.

### ControlNet (для `FACTORIA_VOXEL_STYLE_CONTROL_TEMPLATE.json`)

| Файл | Куда | Размер | SHA-256 | Лицензия |
|---|---|---|---|---|
| `control_v11p_sd15_canny_fp16.safetensors` | `models\controlnet\` | 722 601 100 B | `8932b66e15aae835b3490dbf989f56c253104cee08a88bf21283762f557c9f10` | ControlNet 1.1 (lllyasviel), Apache-2.0 — подтвердить по исходному репозиторию |
| `control_v11p_sd15_lineart_fp16.safetensors` | `models\controlnet\` | 722 601 100 B | `10559106d1bb8196298b7a81565ede9279295d2b2df15165b9dbe189994def56` | то же |
| `control_v11p_sd15_openpose_fp16.safetensors` | `models\controlnet\` | 722 601 100 B | `4003c1da17b0e4ba444e02140e1c0d83bb24b79e4dcfd613c3a554d38f0f89c7` | то же |

Источник: HF `comfyanonymous/ControlNet-v1-1_fp16_safetensors` (официальное зеркало fp16-версий).
Каждый файл ≈ 0.69 GiB; грузить по одному, начиная с canny.

### Препроцессоры (`comfyui_controlnet_aux`)

Веса скачиваются самой нодой при первом использовании (не сейчас):
`yolox_l.onnx` (~200 МБ) + `dw-ll_ucoco_384.onnx` (~200 МБ) в
`custom_nodes\comfyui_controlnet_aux\ckpts`.

**Ничего из §6 не качалось** — сознательно, до отдельного согласования владельца.

---

## 7. Workflow

Собраны **программно** из prompt-контракта и живого `/object_info`
(`scripts\comfyui\build_factoria_voxel_workflows.py`), а не написаны руками: у UI-формата
ComfyUI значения widget'ов позиционные, ручная правка почти гарантированно ломает порядок.

| Workflow | Ноды / связи | Что внутри | Статус проверки |
|---|---|---|---|
| `FACTORIA_VOXEL_STYLE_BASE.json` | 9 / 11 | CheckpointLoaderSimple → 2×CLIPTextEncode → EmptyLatentImage(**512×512**, batch 1) → KSampler(dpmpp_2m/karras, 28 шагов, cfg 7.0, fixed seed) → VAEDecode → SaveImage(`factoria/voxel_style_base`); плюс MarkdownNote с инструкцией и **muted** `LoraLoader` (заготовка под будущую style-LoRA, не обучается) | **PASS** серверным `validate_prompt` |
| `FACTORIA_VOXEL_STYLE_IPADAPTER.json` | 12 / 13 | BASE + LoadImage(reference) + IPAdapterUnifiedLoader(PLUS) + IPAdapterAdvanced(weight 0.75, `style transfer`, start 0.0/end 1.0) + mute-слот CLIPVisionLoader | структурно PASS; не запускается до загрузки весов (так и задумано) |
| `FACTORIA_VOXEL_STYLE_CONTROL_TEMPLATE.json` | 18 / 20 | IPADAPTER + ветка ControlNet: LoadImage(control) → Canny/Lineart/DW-Pose (MUTE) → ControlNetLoader (MUTE) → ControlNetApplyAdvanced (**BYPASS**) → KSampler | структурно PASS; базовая генерация работает при выключенной ветке |
| `voxel_style_prompt_v1.json` | — | style_prefix, character_block (10 полей), negative, style_lock_invariants, style_variables, дефолты генерации/IPAdapter | единый источник стиля |

Особенности, важные для запуска «как есть»:
- все необязательные узлы (CLIP Vision, ControlNet, препроцессоры) стоят в MUTE/BYPASS, чтобы
  кнопка Queue не падала на незаполненных весах;
- reference image не вшит: workflow ждёт локальный файл `ComfyUI/input/factoria_style_reference.png`
  (выбирается в ноде LoadImage);
- монитор ресурсов — глобальная панель Crystools, отдельные ноды не нужны.

---

## 8. Что осталось неподтверждённым

1. **Открытие workflow в браузере** — я не запускал UI в браузере (автоматизация браузера в
   этой сессии недоступна и не устанавливалась). Проверено: JSON структурно соответствует формату
   litegraph (те же поля, что в официальных шаблонах ComfyUI), а граф принимается серверным
   `validate_prompt` — то есть при Queue сервер его валидирует. Само отображение на канвасе
   подтверждает владелец при первом открытии.
2. **Пики RAM/VRAM на реальной генерации** — не измерялись (запрещено на этом этапе).
3. **Качество стиля** (держится ли voxel-язык между персонажами) — проверяется только
   генерацией; это следующий шаг владельца.
4. **IPAdapter/ControlNet** — ноды установлены, но без весов реальный прогон невозможен.

---

## 9. Итоговый статус

**`READY_FOR_REVIEW`**

**Обновление 2026-11-13 (после отчёта):** владелец запустил `FACTORIA_VOXEL_STYLE_BASE.json`
на своём сервере — в `/history` два успешных прогона (`status=success`, `completed=true`,
файлы `voxel_style_base_00001_.png`, `voxel_style_base_00002_.png`). То есть базовый workflow
исполняется end-to-end на реальной машине; пики RAM/VRAM в этих прогонах инструментально не
фиксировались (владелец смотрел панель Crystools по своему усмотрению). Художественная оценка —
за владельцем.

Что это значит по правилам лаборатории: инструмент собран и проверен фактом (установка, ноды,
чекпоинт + SHA-256, два режима запуска, сборка и серверная валидация workflow, idle RAM/VRAM),
но **приёмка (`ACCEPTED`) — только за владельцем**, а художественная пригодность стиля этим
отчётом не доказана.

Проверенные факты (кратко):

| Проверка | Результат |
|---|---|
| Checkpoint скачан целиком | 2 132 696 762 Б ровно, SHA-256 `e9476a13…3916` — **совпал** с LFS-OID HF |
| Структура checkpoint | safetensors: 1145 тензоров (UNet 686, VAE 248, CLIP 197), dtype F16 |
| Загрузка движком ComfyUI (CPU, без сэмплинга) | PASS: `BaseModel`, 859 520 964 параметров, VAE и CLIP на месте, 1.1 с, +2.1 ГБ RAM, VRAM 0 |
| Checkpoint виден в UI | `CheckpointLoaderSimple.ckpt_name = ["v1-5-pruned-emaonly-fp16.safetensors"]` |
| SAFE_BASELINE / CPU_FALLBACK | оба стартуют; WS 1026 / 934 МБ; VRAM +76 MiB / ≈0 |
| Custom nodes | 1213 типов нод, ни одного `IMPORT FAILED` |
| Workflow BASE | принимается серверным `validate_prompt` (7 нод, output `SaveImage`) |
| Workflow IPADAPTER / CONTROL | структурно PASS; единственный блокер — локальный reference-файл (шаг владельца) |

---

## 10. Блокеры и следующий шаг

**Блокеров нет.** Есть два ожидаемых ограничения, оба снимаются решением владельца:

1. **Веса IPAdapter и ControlNet не скачаны** (сознательно, по ТЗ). До их установки
   `FACTORIA_VOXEL_STYLE_IPADAPTER.json` и `..._CONTROL_TEMPLATE.json` не выполняются —
   базовый `..._BASE.json` работает полностью. Список файлов с размерами, SHA-256 и лицензиями — §6.
2. **Reference image отсутствует локально.** Workflow ждёт `ComfyUI/input/factoria_style_reference.png`
   (имя можно изменить в ноде LoadImage). Файл владельца не публикуется и в репозиторий не попадает.

**Следующий шаг (владелец):**

```text
1. scripts\comfyui\launch_safe_baseline.bat
2. http://127.0.0.1:8188 → Workflow → Open → workflows\factoria_voxel_style\FACTORIA_VOXEL_STYLE_BASE.json
3. В ноде "POSITIVE - style + character" заменить блок после "|" (раса/пол/телосложение/лицо/одежда/...),
   стилевой префикс не менять.
4. Queue → смотреть результат в ComfyUI/output/factoria/voxel_style_base_*.png
5. Crystools (верхняя панель): зафиксировать RAM и VRAM в момент генерации — это первые реальные пики.
6. Решение: если стиль между разными персонажами «плывёт» → отдельное ТЗ на Factoria Style LoRA;
   если держится → согласовать веса IPAdapter и проверить перенос стиля по Orc reference.
```

Технические заметки для следующей сессии:
- если пик VRAM превысит +2 ГБ — сначала `--novram` (уже включён), затем `ip-adapter_sd15_light` (44 МБ),
  затем `launch_cpu_fallback.bat`;
- HF на этой машине периодически режет одиночный поток до 20–60 КБ/с; для больших файлов использовать
  `scripts\comfyui\fetch_checkpoint_parallel.ps1` (параллельные диапазоны + проверка SHA-256);
- ComfyUI нельзя запускать с перенаправленным stdout без `PYTHONUTF8=1` — иначе rgthree роняет сервер.

