# TEST_RESULTS — CubeGame Factory Lab

Журнал фактических испытаний. Формат записи соответствует ТЗ §7.
Правило: `NOT_TESTED` / `PARTIAL` / `BLOCKED` ставятся честно; `SMOKE_PASS` — только после реального запуска.

---

## PREP-00 — аудит ПК и установленного ПО

```yaml
id: PREP-00
status: READY_FOR_REVIEW
machine: WINDOWS_11
kind: audit
software: OS / CPU / RAM / GPU / disks / installed tooling
version: Windows 11 Pro 10.0.26200; Ryzen 5 7535HS; 32 GB DDR5-5600; RTX 4060 Laptop 8188 MiB
source_url: n/a (локальный аудит)
license: n/a
install_location: n/a
sample_asset: n/a
command: |
  logs/PREP-00_detection_2026-11-13.txt — консолидированный запуск:
  Get-CimInstance Win32_OperatingSystem | Win32_ComputerSystem | Win32_Processor | Win32_PhysicalMemory
  nvidia-smi --query-gpu=name,driver_version,memory.total,memory.used,memory.free,temperature.gpu,utilization.gpu --format=csv
  Get-Volume | Get-ItemProperty HKLM/HKCU ...\Uninstall\* 
  Get-Command python,py,pip,git,node,npm,code,blender,ffmpeg,ollama,n8n,7z,dotnet
  py -0p ; python -m pip --version ; git --version ; git config --get user.name
  Get-ChildItem C:\,D:\ -Recurse -Depth 3..4 -Filter blender.exe / Cascadeur*.exe / *omfy*
  Get-NetTCPConnection -LocalPort 8188 / 11434
input: локальная машина
output: D:\Projects\CubeGame Factory\logs\PREP-00_detection_2026-11-13.txt (177 строк, UTF-8)
checks:
  - name: Windows / CPU / RAM определены
    result: PASS
  - name: VRAM определена инструментально (nvidia-smi)
    result: PASS
  - name: Свободное место на дисках определено
    result: PASS
  - name: Python / Git / VS Code / Unity / Ollama обнаружены с версиями и путями
    result: PASS
  - name: Blender обнаружен
    result: NOT_FOUND
  - name: ComfyUI обнаружен
    result: NOT_FOUND
  - name: Cascadeur обнаружен
    result: NOT_FOUND
  - name: n8n / Node.js обнаружены
    result: NOT_FOUND
  - name: Unity запускался
    result: NOT_TESTED (запрещено на этом этапе)
issues:
  - ComfyUI отсутствует, хотя ТЗ/память указывают его как имеющийся (USER_REPORTED) — нужен путь от пользователя
  - Blender отсутствует — блокирует PREP-01
  - Git user.name/user.email не настроены
  - C: свободно 68.2 ГБ — мало для весов
  - python3 в PATH — заглушка Microsoft Store
  - реестровая запись 7-Zip ведёт в несуществующий каталог
next_action: PREP-01 — установить Blender 5.2.4 LTS (официальный zip) на D:, проверить GUI и background, затем Rigify
```

### Ключевые измеренные значения (2026-11-13 11:43 MSK)

| Метрика | Значение |
|---|---|
| VRAM всего / занято / свободно | 8188 MiB / 1251 MiB / **6706 MiB** |
| Температура GPU / загрузка | 57 °C / 53 % (фон: браузеры, wallpaper engine, NVIDIA Overlay) |
| RAM всего / свободно | 31.24 ГБ / **18.67 ГБ** |
| Диск C: / D: свободно | 68.2 ГБ / **642.1 ГБ** |
| CUDA | драйвер 610.47, UMD 13.3, Toolkit (`nvcc`) отсутствует |

### Обнаружено с версиями

| Инструмент | Версия | Путь |
|---|---|---|
| Python | 3.13.14 | `C:\Users\bow\AppData\Local\Programs\Python\Python313\python.exe` |
| Python (alt) | 3.14.6 | `C:\Users\bow\AppData\Local\Python\pythoncore-3.14-64\python.exe` |
| pip | 26.1.2 | `...\Python313\Scripts\pip.exe` |
| Git | 2.55.0.windows.1 | `C:\Program Files\Git\cmd\git.exe` |
| VS Code | 1.145.2 | `C:\Users\bow\AppData\Local\Programs\Microsoft VS Code\bin\code.cmd` |
| Unity Hub | 3.22.5 | `D:\Unity Hub\Unity Hub.exe` |
| Unity Editor | 6000.3.27f2 | `D:\Unity\Hub\Editor\6000.3.27f2\Editor\Unity.exe` |
| Unity Editor | 6000.6.7f1 | `D:\Unity\6000.6.7f1\Editor\Unity.exe` |
| Ollama | 0.40.10 (не запущен) | `C:\Users\bow\AppData\Local\Programs\Ollama\ollama.exe` |
| .NET | 7.0.14 / 8.0.34 | `C:\Program Files\dotnet` |
| WinRAR | 7.13 | `C:\Program Files\WinRAR` |

### Не обнаружено (подтверждено поиском)

Blender · Rigify (зависит от Blender) · ComfyUI · Cascadeur · n8n · Node.js/npm/pnpm (системный) · ffmpeg (в PATH) · 7-Zip (запись в реестре устарела) · CUDA Toolkit.

---

## PREP-01 — Blender / Rigify / skinning / экспорт

```yaml
id: PREP-01
status: READY_FOR_REVIEW          # каждая проверенная операция = SMOKE_PASS; приёмка за пользователем
machine: WINDOWS_11
software: Blender 5.2.4 LTS + штатный Rigify 0.6.10 + io_scene_fbx 5.15.0
version: 5.2.4 LTS (build hash cd5dc428cce1, build date 2026-11-10), Python внутри 3.13.13
source_url: https://download.blender.org/release/Blender5.2/blender-5.2.4-windows-x64.zip
license: GPL-2.0-or-later (файлы license\ в поставке; SHA-файл сборки опубликован)
install_location: D:\programs\Blender\5.2\blender.exe
download_sha256: bbd78f8931dc940444da4ff2983374930f9f5c092ae0e330588e9760bb9d967c
sha256_verified: YES (совпало с blender-5.2.4.sha256, строка windows-x64.zip)
install_size: 901.3 MB, 6530 файлов; PATH не изменялся
sample_asset: собственный процедурный манекен CG_PREP01_Mannequin (160 боксов по deform-костям + voxel remesh)
command: |
  $env:BLENDER_USER_CONFIG="D:\Projects\CubeGame Factory\environments\blender\config"
  & 'D:\programs\Blender\5.2\blender.exe' --background --factory-startup --python-exit-code 1 `
      --python "D:\Projects\CubeGame Factory\scripts\blender\prep01_rigify_skinning.py"
input: scripts\blender\prep01_rigify_skinning.py (создаёт геометрию сам, внешних ассетов нет)
output: |
  outputs\blender\prep01_rigify_skinned.blend      (574 КБ, риг+меш+позы)
  outputs\blender\prep01_rigify_skinned.fbx        (1.54 МБ, экспорт)
  outputs\blender\prep01_rest_pose.png             (evidence, rest)
  outputs\blender\prep01_control_pose.png          (evidence, control-поза)
  outputs\blender\prep01_gui_open.png              (скриншот GUI с открытым .blend)
  outputs\reports\prep01_rigify_skinning.json      (машиночитаемый отчёт)
checks:
  - name: GUI открывается и открывает лабораторный .blend
    result: PASS  (заголовок окна "prep01_rigify_skinned [D:\...\prep01_rigify_skinned.blend] - Blender 5.2.4 LTS", скриншот)
  - name: Headless (--background) запускается и сохраняет .blend
    result: PASS  (exit 0, prep01_bg_smoke.blend 86 КБ)
  - name: Rigify add-on доступен через API
    result: PASS  (bpy.ops.preferences.addon_enable(module='rigify') → enabled)
  - name: Metarig → generate rig на собственной фигуре
    result: PASS  (metarig 159 костей → rig 706 костей: 160 DEF + 546 control)
  - name: Parent with Automatic Weights (встроенный skinning)
    result: PASS  (160 vertex groups, 0 вершин без весов, Armature Modifier → rig)
  - name: Контрольные позы деформируют меш
    result: PASS  (IK-таргеты: 0.562 м, FK-режим: 0.332 м смещения вершин из depsgraph)
  - name: Экспорт FBX и обратный импорт
    result: PASS  (FBX 1.54 МБ → импорт: 1 armature, 1 mesh, 145 vgroups, поворот кости даёт 0.566 м)
issues:
  - addon_utils.enable('rigify') оставляет аддон полу-зарегистрированным: generate падает "'RigifyParameters' object has no attribute 'make_custom_pivot'"
  - 160 отдельных закрытых боксов ломают Bone Heat Weighting полностью (0 весов из 1280 вершин)
  - авто-веса не нормированы: сумма весов на вершину min 0.732 / p50 0.971 / max 0.9999
  - DEF-кости Rigify управляются constraints: прямой поворот DEF-кости не деформирует меш (0.0 м)
  - FBX экспортирует весь control rig (706 костей), а не чистый game-skeleton
next_action: PREP-02 — round-trip .blend/.fbx/.glb с контролем осей, масштаба, материалов и анимационного action
```

**Воспроизводимость:** полный прогон — 11.8–12.0 с (два прогона), пиковый рабочий набор процесса Blender 394–402 МБ,
свободная RAM до/после 20.43 → 20.45 ГБ, рендер Cycles на CPU (VRAM не занималась).
Лог: `logs\PREP-01_rigify_skinning_2026-11-13.txt`, диагностика: `logs\PREP-01_diagnostics_2026-11-13.txt`.

**Измеренные величины**

| Метрика | Значение |
|---|---|
| Костей в metarig / в сгенерированном rig | 159 / 706 |
| Deform-костей / control-костей | 160 / 546 |
| Вершин манекена: до / после voxel remesh | 1280 (160 оболочек) / 3926 (1 оболочка) |
| Vertex groups / вершин без весов | 160 / 0 |
| Сумма весов на вершину (min / p50 / max) | 0.7323 / 0.9711 / 0.9999 |
| Смещение меша: IK-контролы / FK-контролы / прямой DEF | 0.562 м / 0.332 м / 0.0 м |
| Рендер 640×800, Cycles CPU, 24 сэмпла | rest 1.0–1.3 с, posed 0.7 с |
| .blend / .fbx / отчёт | 587 722 Б / 1 577 084 Б / 5.9 КБ |
| FBX обратный импорт | 1 armature (706 костей), 1 mesh, 145 vgroups, 0.566 м |

**Найденные ловушки (проверено на практике, не теория)**

1. **`addon_utils.enable("rigify")` непригоден в 5.2.4.** Операторы регистрируются, но
   генерация падает: `Generation has thrown an exception: 'RigifyParameters' object has no
   attribute 'make_custom_pivot'`. Рабочий путь — `bpy.ops.preferences.addon_enable(module='rigify')`.
2. **Bone Heat Weighting не работает на несвязной геометрии.** 160 отдельных боксов
   (по одному на кость) → предупреждение `Bone Heat Weighting: failed to find solution for one
   or more bones` и **0 весов на всех 1280 вершинах**. После склейки боксов штатным
   Voxel Remesh в одну оболочку веса назначаются всем вершинам.
3. **Rigify управляет деформацией через control-кости.** Прямой поворот `DEF-upper_arm.L`
   не даёт смещения (0.0 м); работают `hand_ik.*` / `foot_ik.*` (IK) либо `*_fk.*` при
   `IK_FK = 1.0` на `upper_arm_parent.*` / `thigh_parent.*`.
4. **Авто-веса не нормированы:** 2426 из 3926 вершин имеют сумму весов ниже 1.0 на 2 %+.
   Для продакшена потребуется шаг нормализации (зафиксировано в `PIPELINE_GAPS.md`, F8).
5. **FBX несёт весь control rig (706 костей).** Для Unity нужен отдельный deform-only
   экспорт — это задача PREP-02/PREP-04, а не «уже готовый game skeleton».
6. **`rig_ui.py` и автозапуск скриптов:** при открытии .blend в GUI Blender показывает
   диалог «automatic execution ... was disabled» — штатное поведение Rigify, не ошибка.

**Что этим НЕ доказано:** пригодность auto-weights для реальных плеч/таза/доспехов;
fitted skeleton под пропорции произвольной модели (Rigify не Rig Fitter); корректность
осей/единиц/bind pose в Unity; воспроизводимость на моделях CubeGame (не запускались).

---

## PREP-02 — форматы и экспорт

```yaml
id: PREP-02
status: PLANNED
checks: []   # выполняется после PREP-01
note: |
  В PREP-01 уже проверен минимальный экспорт: FBX тестового ригнутого меша и обратный
  импорт в пустую сцену (см. выше). PREP-02 должен закрыть полный round-trip
  .blend/.fbx/.glb с масштабом, осями, материалами и анимационным action.
```

---

## PREP-03 — ComfyUI и локальная AI-инфраструктура

```yaml
id: PREP-03
status: PARTIAL        # установка и API-прогон доказаны; AI-модели и генерация 3D НЕ проверены
machine: WINDOWS_11
software: ComfyUI 0.45.0 (portable, official Comfy-Org release)
version: 0.45.0; python 3.13.14 (embedded); torch 2.14.1+cu130; CUDA runtime 13.0
source_url: https://github.com/Comfy-Org/ComfyUI/releases/download/v0.45.0/ComfyUI_windows_portable_nvidia.7z
license: GPL-3.0 (LICENSE в поставке; подтверждено по файлу репозитория v0.45.0)
install_location: D:\programs\ComfyUI  (portable, embedded python, 4.25 ГБ, 63 891 файл)
download_size: 2 071 479 423 Б (1.93 ГБ, .7z)
download_sha256: 60c12e8c7af778047ad0413722dd3dc0f6ca773a41cbf5cb229ee9bc92c44a6f
sha256_verified: YES (совпало с digest, опубликованным в GitHub release assets)
sample_asset: собственный минимальный граф EmptyImage → SaveImage (без чекпоинтов и весов)
command: |
  powershell -NoProfile -ExecutionPolicy Bypass -File "D:\Projects\CubeGame Factory\scripts\comfyui\prep03_run_smoke.ps1"
  powershell -NoProfile -ExecutionPolicy Bypass -File "...\prep03_run_smoke.ps1" -CpuMode
  # внутри: python_embeded\python.exe -s ComfyUI\main.py --windows-standalone-build --port 8188 `
  #            --disable-auto-launch --reserve-vram 5    (или --cpu)
input: |
  график из встроенных нод: EmptyImage(width=96, height=96, color=0x00C8FF) → SaveImage
  API-скрипт: scripts\comfyui\prep03_comfyui_smoke.py (только стандартная библиотека Python)
output: |
  outputs\comfyui\prep03_smoke_from_api.png                (96×96 PNG, скачан через /view)
  outputs\reports\prep03_comfyui_smoke_gpu_reserve5.json   (прогон GPU, reserve-vram 5)
  outputs\reports\prep03_comfyui_smoke_cpu.json            (прогон CPU)
  logs\PREP-03_run_gpu_2026-11-13.txt / PREP-03_run_cpu_2026-11-13.txt
  logs\PREP-03_cuda_sanity_2026-11-13.txt                  (matmul на CUDA встроенным torch)
  workflows\comfyui_prep03_smoke_api.json                  (граф для повторного запуска)
  серверная сторона: D:\programs\ComfyUI\ComfyUI\output\factoria_prep03_smoke_0000N_.png
checks:
  - name: Реальный запуск ComfyUI (GPU, --reserve-vram 5)
    result: PASS  (тёплый старт 10.8–11.8 с; первый холодный старт ~146 с)
  - name: Веб-интерфейс отдаётся
    result: PASS  (GET / → HTTP 200, 19 885 Б HTML, ComfyUI 0.45.0)
  - name: Python и зависимости рабочие
    result: PASS  (embedded python 3.13.14, torch 2.14.1+cu130, cuda_available=True, RTX 4060 Laptop, capability 8.9)
  - name: Минимальный workflow выполнен (без моделей)
    result: PASS  (prompt_id получен, /history → status_str=success, completed=true, 1.0 с)
  - name: Результат сохранён и скачан
    result: PASS  (96×96 PNG, 504 Б, /view 200; файл также есть в ComfyUI\output)
  - name: API-механизм (POST /prompt, /history, /view, /queue)
    result: PASS  (очередь пуста после выполнения)
  - name: CUDA-вычисления встроенным torch
    result: PASS  (matmul 2048×2048 за 0.12 с, пик allocated 56.1 МБ / reserved 68 МБ)
  - name: Генерация 3D-модели
    result: NOT_TESTED  (требуются веса AI-моделей — не скачивались без согласования)
issues:
  - первый холодный старт ~146 с (миграция SQLite-БД ComfyUI + скан ассетов), тёплый ~11 с
  - nvidia-smi на Windows/WDDM не отдаёт память процесса (compute-apps = 0) — VRAM мерилась по всей карте
  - граф без моделей не создаёт CUDA-тензоров: torch_vram_total = 0, прирост VRAM ≈ 80–90 МБ (инициализация)
next_action: >
  PREP-02 (форматы/экспорт) — следующий обязательный шаг. Для PREP-03 дальше требуется
  согласование конкретной AI-модели (источник, размер, лицензия, VRAM) и только затем
  тест реальной генерации/переработки геометрии.
```

**Измеренные величины (2026-11-13)**

| Метрика | GPU (`--reserve-vram 5`) | CPU (`--cpu`) |
|---|---|---|
| Готовность сервера (тёплый старт) | 10.8–11.8 с | 10.7–11.8 с |
| Выполнение workflow | 1.0 с | 1.0–1.02 с |
| Пиковый рабочий набор процесса ComfyUI | **972–973 МБ** | **858–860 МБ** |
| Свободная RAM до / минимум во время теста | 19.37 → 18.43 ГБ | 19.41 → 18.52 ГБ |
| Занятая VRAM всей карты до / пик | 1350 → 1436 MiB (**+86 MiB**) | 1348 → 1355 MiB (+7 MiB, шум) |
| RAM после теста | 19.45 ГБ | 19.43 ГБ |
| Общее время прогона (скрипт) | 14.4–15.1 с | 13.4–14.5 с |

**Карточка памяти локального AI (§4.1 AGENTS.md) — что измерено и что нет**

| Параметр | Факт |
|---|---|
| Название / версия | ComfyUI 0.45.0 portable NVIDIA (torch 2.14.1+cu130, CUDA 13.0) |
| Источник / лицензия | официальный GitHub release Comfy-Org/ComfyUI v0.45.0; GPL-3.0 |
| Вес / диск | архив 1.93 ГБ; установлено 4.25 ГБ; **генеративных чекпоинтов нет** (`models\checkpoints` пуст); в поставке 6 служебных TAESD-превью-кодеков ≈14 МБ — часть официальной сборки, нами не скачивались |
| Настройки запуска | `--reserve-vram 5` (резерв 5 ГБ из 8 ГБ VRAM → ComfyUI ≈ 3 ГБ), `--disable-auto-launch`, порт 8188; CPU-режим `--cpu` |
| RAM / VRAM | пик RAM процесса ≈ 0.97 ГБ; прирост VRAM ≈ 86 МБ на инициализацию; реальная нагрузка моделью **не измерена** |
| Время | холодный старт 146 с, тёплый 11 с, smoke-workflow 1.0 с |
| Выход / качество | PNG 96×96 (цвет 0x00C8FF) — доказан runtime, **не** качество генерации |
| Проблемы | нет OOM/подкачки; WDDM ограничивает per-process VRAM-метрику |
| Статус | **PARTIAL** — инфраструктура готова, пригодность AI-моделей не проверена |

**Чего этот тест НЕ доказывает:** способность генерировать 3D-модели, качество и лицензионную
чистоту AI-выходов, поведение VRAM под реальными моделями (SD/3D/motion), скорость на 8 ГБ VRAM,
пригодность для стиля CubeGame. Успешный API-прогон — это только «сервер и конвейер запускаются».

---

## COMFY-STYLE-02/03 — ComfyUI под FACTORIA VOXEL_STYLE

```yaml
id: COMFY-STYLE-02/03
status: READY_FOR_REVIEW     # установка/ноды/чекпоинт/launch/валидация workflow доказаны;
                             # художественной генерации и пиковых замеров НЕ было (запрещено на этапе)
machine: WINDOWS_11
software: ComfyUI 0.45.0 portable + custom nodes + SD 1.5 fp16
version: ComfyUI 0.45.0, frontend 1.58.5, python 3.13.14 (embedded), torch 2.14.1+cu130, CUDA 13.0
source_url: >
  checkpoint: https://huggingface.co/Comfy-Org/stable-diffusion-1.5/resolve/main/v1-5-pruned-emaonly-fp16.safetensors
  nodes: PyPI comfyui_manager==4.2.2; github crystian/ComfyUI-Crystools@2f18256,
         cubiq/ComfyUI_IPAdapter_plus@a0f451a, Fannovel16/comfyui_controlnet_aux@a587a4c,
         rgthree/rgthree-comfy@bc48c6a
license: checkpoint CreativeML OpenRAIL-M; Manager GPL-3.0; Crystools MIT; IPAdapter_plus GPL-3.0;
         controlnet_aux Apache-2.0; rgthree MIT
install_location: D:\programs\ComfyUI (portable; 4.25 ГБ → 5.75 ГБ после установки нод)
download_sha256: e9476a13728cd75d8279f6ec8bad753a66a1957ca375a1464dc63b37db6e3916
sha256_verified: YES (2 132 696 762 Б, файл downloaded частями и сверен целиком)
sample_asset: собственные workflow FACTORIA_VOXEL_STYLE_* + prompt-контракт; изображений не генерировалось
command: |
  # запуск (два режима)
  scripts\comfyui\launch_safe_baseline.bat          # --novram --reserve-vram 5
  scripts\comfyui\launch_cpu_fallback.bat           # --cpu
  powershell -File scripts\comfyui\factoria_launch.ps1 -Preset safe -MeasureIdle

  # сборка + серверная валидация workflow
  python_embeded\python.exe -s scripts\comfyui\build_factoria_voxel_workflows.py `
      --object-info measurements\comfyui_object_info_2026-11-13_full.json `
      --config workflows\factoria_voxel_style\voxel_style_prompt_v1.json `
      --out workflows\factoria_voxel_style
  python_embeded\python.exe -s scripts\comfyui\validate_factoria_workflows.py `
      --comfy D:\programs\ComfyUI\ComfyUI --workflow-dir workflows\factoria_voxel_style `
      --report measurements\factoria_voxel_style_validation.json

  # проверка чекпоинта без генерации
  python_embeded\python.exe -s scripts\comfyui\check_sd15_checkpoint.py <ckpt>
  set FACTORIA_CKPT=<ckpt> && python_embeded\python.exe -s scripts\comfyui\load_checkpoint_probe.py
input: |
  prompt-контракт voxel_style_prompt_v1.json; локальный style reference (у владельца, в git не попадает)
output: |
  workflows\factoria_voxel_style\FACTORIA_VOXEL_STYLE_{BASE,IPADAPTER,CONTROL_TEMPLATE}.json
  workflows\factoria_voxel_style\voxel_style_prompt_v1.json
  measurements\comfyui_object_info_2026-11-13_full.json
  measurements\factoria_voxel_style_build_report.json
  measurements\factoria_voxel_style_validation.json
  logs\COMFY-STYLE_*.log
checks:
  - name: Существующий ComfyUI найден, второй не устанавливался
    result: PASS  (D:\programs\ComfyUI 0.45.0, portable; embedded python 3.13.14, torch 2.14.1+cu130)
  - name: PIP-установка нод не сломала CUDA-сборку torch
    result: PASS  (constraints-файл; после установок torch 2.14.1+cu130, cuda.is_available()=True)
  - name: Импорт custom nodes без ошибок
    result: PASS  (нет IMPORT FAILED; Crystools 1.27.4, rgthree 48 нод, 1213 типов нод в /object_info)
  - name: Web UI и API
    result: PASS  (GET / → 200, 19 885 Б; /api/object_info, /system_stats работают)
  - name: Crystools мониторинг
    result: PASS  (/crystools/monitor/GPU → RTX 4060 Laptop; /HDD → C:, D:; ws-событие crystools.monitor)
  - name: Checkpoint скачан и сверен
    result: PASS  (2 132 696 762 Б, SHA-256 = e9476a13…3916, safetensors 1145 тензоров: UNet 686, VAE 248, CLIP 197, dtype F16)
  - name: Checkpoint загружается движком ComfyUI (без сэмплинга, CPU)
    result: PASS  (load 1.1 с; BaseModel 859 520 964 параметров; VAE и CLIP присутствуют; +2.1 ГБ RAM, VRAM 0)
  - name: Checkpoint детектируется в UI
    result: PASS  (CheckpointLoaderSimple.ckpt_name = ["v1-5-pruned-emaonly-fp16.safetensors"])
  - name: SAFE_BASELINE (--novram) запускается
    result: PASS  (ready 11.2–11.7 с; device cuda:0; WS 1025–1026 MB; VRAM +76…79 MiB)
  - name: Launcher владельца (`launch_safe_baseline.bat`) стартует сервер
    result: PASS  (`cmd /c ...bat` → /system_stats 200 за 14.3 с, device cuda:0, ноды загружены)
  - name: CPU_FALLBACK (--cpu) запускается
    result: PASS  (ready 11.2 с; device cpu; WS 934 MB; VRAM ≈0; лог Crystools «CUDA is available, but torch is using CPU»)
  - name: Workflow принимаются валидатором ComfyUI
    result: PASS  (BASE: validate_prompt PASS, 7 нод, output SaveImage; IPADAPTER/CONTROL: структурно PASS, упираются только в отсутствующий локальный reference)
  - name: Художественная генерация / пиковые замеры
    result: NOT_TESTED  (запрещено этапом: делается владельцем после отчёта)
issues:
  - rgthree-comfy печатает emoji → при перенаправленном stdout на cp1251 сервер падал UnicodeEncodeError; лечится PYTHONUTF8=1/PYTHONIOENCODING=utf-8
  - IPAdapter/ControlNet веса не скачивались (сознательно): IPADAPTER/CONTROL workflow не запускаются до их установки
  - single-stream скачивание с HF на этой машине временами падало до 20–60 КБ/с; чекпоинт докачан параллельными диапазонами
next_action: >
  Владелец: запустить SAFE_BASELINE, открыть FACTORIA_VOXEL_STYLE_BASE.json, заполнить character block,
  Queue, замерить RAM/VRAM через Crystools. Затем решить по весам IPAdapter (CLIP Vision 2.5 ГБ + ip-adapter-plus 98 МБ)
  и повторить с style reference.
```

**Измеренные величины (idle, без генерации, 2026-11-13)**

| Метрика | SAFE_BASELINE (`--novram --reserve-vram 5`) | CPU_FALLBACK (`--cpu`) |
|---|---|---|
| Готовность сервера (тёплый старт) | 11.2–11.7 с | 11.2 с |
| Рабочий набор процесса ComfyUI | **1025–1026 МБ** | **934 МБ** |
| Свободная RAM до / после | 17.58 → 16.57 ГБ | 17.62 → 16.70 ГБ |
| VRAM всей карты до / после | 1476 → 1555 MiB (**+79 MiB**) | 1480 → 1473 MiB (≈0) |
| Устройство torch | `cuda:0 RTX 4060 Laptop` | `cpu` |
| Загрузка чекпоинта (CPU, без сэмплинга) | — | 1.1 с, +2.1 ГБ RAM, VRAM 0 |

**Карточка памяти локального AI (§4.1 AGENTS.md)**

| Параметр | Факт |
|---|---|
| Название / версия | SD 1.5 fp16 ema-only `v1-5-pruned-emaonly-fp16.safetensors` в ComfyUI 0.45.0 |
| Источник / лицензия | HF `Comfy-Org/stable-diffusion-1.5` (архив `runwayml/stable-diffusion-v1-5`); CreativeML OpenRAIL-M |
| Вес / диск | 2.03 ГБ (2 132 696 762 Б); ComfyUI после всех установок 5.75 ГБ |
| Настройки запуска | `--novram --reserve-vram 5` (SAFE_BASELINE) / `--cpu` (CPU_FALLBACK); порт 8188; `PYTHONUTF8=1` |
| RAM / VRAM | idle: 1025 МБ RAM / +79 MiB VRAM; загрузка чекпоинта: +2.1 ГБ RAM / 0 VRAM (CPU); **пик генерации не измерен** |
| Время | старт 11.2–11.7 с; загрузка чекпоинта 1.1 с; время генерации не измерялось |
| Выход / качество | изображений не генерировалось — выход этапа это готовый инструмент (workflow + launcher) |
| Проблемы | нет OOM/подкачки; веса IPAdapter/ControlNet отсутствуют; WDDM не даёт per-process VRAM |
| Статус | **READY_FOR_REVIEW** (инструмент готов, художественная пригодность стиля не проверена) |

**Чего этот тест НЕ доказывает:** что стиль держится между персонажами; качество concept-изображений;
пиковый расход RAM/VRAM при генерации; работоспособность IPAdapter/ControlNet без их весов; отображение
workflow на канвасе в браузере (проверена структура и серверная валидация, не рендеринг UI).

---

## COMFY-STYLE-06 — SDXL Base 1.0 + Fictiverse Voxel XL (CFG 5-7)

```yaml
id: COMFY-STYLE-06
status: READY_FOR_OWNER_VISUAL_REVIEW
machine: WINDOWS_11
software: ComfyUI 0.45.0 + SDXL Base 1.0 + Fictiverse VoxelXL
version: checkpoint 31e35c80…7e5b (6 938 078 334 B), LoRA 7d9a5f11…8b5c
source_url: https://huggingface.co/stabilityai/stable-diffusion-xl-base-1.0
license: openrail++ (checkpoint), openrail (LoRA, требует отдельной проверки)
install_location: D:\programs\ComfyUI\ComfyUI\models\checkpoints\sd_xl_base_1.0.safetensors
download_sha256: 31e35c80fc4829d14f90153f4c74cd59c90b779f6afe05a74cd6120b893f7e5b
sha256_verified: YES
command: |
  factoria_launch.ps1 -Preset xl -Port 8191 -DisableAssets -KeepRunning
  factory_run_batch.py --config workflows/factory_characters_v2/sdxl_base_v2.json
      --matrix workflows/factory_characters_v2/factory_test_matrix_v2.json
output: outputs/factory/characters_v2/ (PNG + manifests), measurements/factory_batch_report_v2.json, contact_sheet_v2.png
checks:
  - name: Официальный SDXL Base 1.0 скачан и сверен (скрипт + Get-FileHash)
    result: PASS
  - name: DreamShaper XL Turbo и прежние workflow сохранены
    result: PASS
  - name: Серверная валидация V2 графов
    result: PASS (оба)
  - name: 10 прогонов (sampler sweep, CFG 5-7, LoRA 0.4-0.8, short-vs-long prompt)
    result: PASS 10/10, 0 OOM; 29-55 с на 768x1024/28 шагов; пик RAM процесса 6.3-6.9 ГБ
  - name: LoRA Fictiverse на обычном SDXL
    result: PASS (блочная геометрия и пиксельные поверхности появляются; на Turbo не было)
  - name: Один персонаж в кадре
    result: FAIL (2-3 фигуры в 8 из 10 кадров, несмотря на усиленный negative)
  - name: Раса сохраняется
    result: PARTIAL (короткий промпт даёт орка, длинный структурный - бородатого человека)
  - name: 1024x1024
    result: NOT_TESTED (свободно ~2.8 ГБ VRAM: карту занимала модель параллельной сессии)
issues:
  - character sheet / несколько фигур - единственный оставшийся блокер для Tripo
  - GPU делился с моделью другой сессии (пики VRAM "вся карта" завышены)
next_action: >
  Решение владельца по способу борьбы с несколькими фигурами (img2img/маска, CFG 7-8, кроп+img2img),
  затем повторный прогон и визуальная приёмка.
```

---

## COMFY-STYLE-05 — Фабрика персонажей (главный граф + 3 режима + метрики)

```yaml
id: COMFY-STYLE-05
status: READY_FOR_OWNER_VISUAL_REVIEW
machine: WINDOWS_11
software: ComfyUI 0.45.0 + DreamShaper XL Turbo + VoxelXL/ral-vxl LoRA + IPAdapter/ControlNet блоки
version: checkpoint 4496b36d…5083, LoRA 7d9a5f11…8b5c и 9dc480ee…a25, IPAdapter 3f5062b8…6581, ControlNet a9e13fd6…7467
source_url: локальные веса из COMFY-STYLE-04/04B (HF + Civitai), SHA-256 перепроверены 2026-11-14
license: openrail++ (checkpoint), openrail (Fictiverse), RentCivit/no-derivatives (ral-vxl — только эксперимент)
install_location: D:\programs\ComfyUI\ComfyUI\models\*
sample_asset: 19 технических прогонов (орк/человек/эльф, 4 ракурса) + контактные листы
command: |
  scripts\comfyui\factoria_launch.ps1 -Preset xl -Port 8190 -DisableAssets -KeepRunning
  python_embeded\python.exe -s scripts\comfyui\factory_run_batch.py --url http://127.0.0.1:8190 --server-pid <pid> ...
  python_embeded\python.exe -s scripts\comfyui\factory_contact_sheet.py --report measurements\factory_batch_report.json --out outputs\factory\characters\contact_sheet.png
input: workflows\factory_characters\characters_v1.json + factory_test_matrix_v1.json
output: outputs\factory\characters\<run>.png + <run>.manifest.json, contact_sheet{,_races,_views,_tuning}.png, measurements\factory_batch_report*.json
checks:
  - name: Аудит весов (размер + SHA-256, 6 файлов)
    result: PASS
  - name: Серверная валидация 6 графов (validate_prompt)
    result: PASS (6/6)
  - name: Технические прогоны (BASE / FICTIVERSE 0.4-0.8 / RAL 0.4-0.8 / расы / ракурсы)
    result: PASS 19 прогонов, 0 FAIL, 1 SKIP (1024x1024 — свободно 2.4 ГБ VRAM)
  - name: Время и память (768x1024, 8 шагов, CFG 2)
    result: PASS 8-10.6 с; пик VRAM всей карты 7.6-7.7 ГБ (собственный ~6.2 ГБ); пик RAM процесса 2.6-2.9 ГБ
  - name: Влияние LoRA FICTIVERSE на SDXL Turbo
    result: FAIL (визуально не влияет при 0.4-1.0 → нужен обычный SDXL, CFG 5-7)
  - name: Соответствие FACE_STYLE_LOCK (RAL)
    result: PARTIAL (воксельно, но «игрушечное» лицо и chibi-пропорции)
  - name: Один персонаж в кадре
    result: PARTIAL (3 из 7 орк-прогонов дали 2-4 фигуры, negative при CFG 2 слабый)
  - name: Художественная приёмка
    result: NOT_TESTED (только владелец)
issues:
  - на turbo CFG 2 LoRA и negative работают слабо — стиль не достигается
  - 1024x1024 требует освободить VRAM (занято ~1.5 ГБ другими приложениями)
  - ral-vxl запрещает коммерческое использование и производные (FACT-D21)
next_action: >
  Владелец смотрит контактные листы; при необходимости согласуется загрузка обычного SDXL
  (DreamShaper XL 1.0 / SDXL base) для честной проверки LoRA при CFG 5-7.
```

---

## COMFY-STYLE-04B — Civitai «Voxel Style [SDXL]» + Voxel Face Style Lock

```yaml
id: COMFY-STYLE-04B
status: READY_FOR_OWNER_TEST     # LoRA установлена, SHA-256 совпал, ComfyUI её видит,
                                 # 3 новых workflow прошли серверную валидацию;
                                 # генерация НЕ проверена (SDXL-чекпоинт ещё качается)
machine: WINDOWS_11
software: ComfyUI 0.45.0 + Civitai LoRA ral-vxl (SDXL) + DreamShaper XL Turbo workflows
version: LoRA Civitai model 461862 / version 513975 (author RalFinger)
source_url: https://civitai.com/api/download/models/513975  (metadata: https://civitai.com/api/v1/models/461862)
license: allowCommercialUse=RentCivit, allowDerivatives=False, allowNoCredit=False -> LICENSE_BLOCKED_FOR_PRODUCTION
install_location: D:\programs\ComfyUI\ComfyUI\models\loras\ral-vxl.safetensors
download_size: 228 453 420 B
download_sha256: 9dc480ee98c12a499bee1fc19ee0e13969a3c6b14be0e6ec133a11d563e83a25
sha256_verified: YES (совпал с хэшем, опубликованным Civitai для файла)
sample_asset: workflows DREAMSHAPER_XL_CIVITAI_VOXEL{,_HUMAN,_ELF}.json + prompt_voxel_face_lock_v1.json
command: |
  # загрузка (отдельный экземпляр, не конфликтует с fetch_style04_models.ps1)
  powershell -NoProfile -ExecutionPolicy Bypass -File scripts\comfyui\fetch_checkpoint_parallel.ps1 `
      -Url https://civitai.com/api/download/models/513975 `
      -Target D:\programs\ComfyUI\ComfyUI\models\loras\ral-vxl.safetensors `
      -ExpectedSha256 9dc480ee...a25 -TotalBytes 228453420 -Connections 6
  # сборка workflow + проверка
  python_embeded\python.exe -s scripts\comfyui\build_civitai_voxel_workflows.py ...
  python_embeded\python.exe -s scripts\comfyui\validate_factoria_workflows.py ... --substitute-ckpt v1-5-pruned-emaonly-fp16.safetensors
output: workflows\dreamshaper_xl\DREAMSHAPER_XL_CIVITAI_VOXEL.json (+_HUMAN,_ELF), prompt_voxel_face_lock_v1.json, measurements\civitai_voxel_build_report.json, measurements\dreamshaper_xl_validation.json
checks:
  - name: Метаданные Civitai (версия/тип/размер/trigger/лицензия)
    result: PASS  (LORA, version 513975, baseModel SDXL 1.0, trigger ral-vxl, 228 453 420 B)
  - name: Загрузка + SHA-256
    result: PASS  (228 453 420 B; sha256 9dc480ee…a25 совпал)
  - name: Структура LoRA
    result: PASS  (2958 тензоров: 2166 UNet + 792 TE1, kohya-формат с alpha)
  - name: ComfyUI видит LoRA
    result: PASS  (LoraLoader.lora_name = ["ral-vxl.safetensors"])
  - name: Серверная валидация новых workflow
    result: PASS  (CIVITAI_VOXEL / _HUMAN / _ELF: 8 нод в prompt, output SaveImage; with --substitute-ckpt)
  - name: Fictiverse Voxel XL сохранён
    result: PASS  (не заменялся; его workflow ждёт свои веса)
  - name: Совместимость с SDXL Turbo (реальный прогон)
    result: NOT_TESTED  (чекпоинт DreamShaper XL ещё докачивается)
  - name: Художественная приёмка лиц/рас
    result: NOT_TESTED  (владелец)
issues:
  - лицензия требует разрешения автора для коммерческого использования и запрещает производные (FACT-D21)
  - turbo (CFG 2) ослабляет влияние LoRA — сила 0.6 выбрана компромиссно, серия 0.4/0.8 для проверки
  - BODY_STYLE_LOCK / FACE_STYLE_LOCK — только текстовые блоки; региональное управление и inpaint-корректор не реализованы
next_action: >
  После загрузки DreamShaper XL: запустить DREAMSHAPER_XL_CIVITAI_VOXEL.json (seed 424242, LoRA 0.6),
  затем 0.4/0.8 и расовые варианты; сравнить единство стиля и читаемость лиц при уменьшении до ~256 px.
```

---

## COMFY-STYLE-04 — DreamShaper XL + Pixel-Textured 3D

```yaml
id: COMFY-STYLE-04
status: READY_FOR_OWNER_GENERATION_REVIEW  # все веса скачаны и сверены по SHA-256 (recovery 2026-11-14);
                       # 6/7 workflow прошли серверную валидацию; генерация и пики — за владельцем
machine: WINDOWS_11
software: ComfyUI 0.45.0 + SDXL DreamShaper XL Turbo v2.1 + VoxelXL LoRA + SDXL IPAdapter + union ControlNet
version: ComfyUI 0.45.0; python 3.13.14 embedded; torch 2.14.1+cu130; CUDA 13.0
source_url: |
  checkpoint: https://huggingface.co/Lykon/dreamshaper-xl-v2-turbo/resolve/main/DreamShaperXL_Turbo_v2_1.safetensors
  lora:       https://huggingface.co/Fictiverse/Voxel_XL_Lora/resolve/main/VoxelXL_v1.safetensors
  ipadapter:  https://huggingface.co/h94/IP-Adapter/resolve/main/sdxl_models/ip-adapter-plus_sdxl_vit-h.safetensors
  clipvision: https://huggingface.co/h94/IP-Adapter/resolve/main/models/image_encoder/model.safetensors
  controlnet: https://huggingface.co/xinsir/controlnet-union-sdxl-1.0/resolve/main/diffusion_pytorch_model.safetensors
license: openrail++ (checkpoint) / openrail (LoRA) / apache-2.0 (IPAdapter, ControlNet) / CLIP ViT-H MIT
install_location: D:\programs\ComfyUI\ComfyUI\models\{checkpoints,loras,ipadapter,clip_vision,controlnet}
download_sha256: |
  checkpoint 4496b36d48bfd7cfe4e5dbce3485db567bcefa2bef7238d290dbd45612125083 (6 939 220 250 B)
  lora       7d9a5f11e1b38d97f75d2b84bfb5bb3bf95cd0e5f2500b002d13374eb4f88b5c (912 554 516 B)
  ipadapter  3f5062b8400c94b7159665b21ba5c62acdcd7682262743d7f2aefedef00e6581 (847 517 512 B)
  clipvision 6ca9667da1ca9e0b0f75e46bb030f7e011f44f86cbfb8d5a36590fcd7507b030 (2 528 373 448 B)
  controlnet a9e13fd61f3193887791c8a0dd07a07202174dc47d5ddaea94ea1344f07c7467 (2 512 030 408 B)
sha256_verified: YES (все 6 файлов: независимая перепроверка Get-FileHash — размер и SHA-256 совпали)
sample_asset: workflow DREAMSHAPER_XL_* + prompt_orc_v1.json; генерации не выполнялось
command: |
  # загрузка (идемпотентная, продолжается с места; watchdog + backoff, 4 соединения по умолчанию)
  powershell -NoProfile -ExecutionPolicy Bypass -File scripts\comfyui\fetch_style04_models.ps1 -Connections 4
  # запуск SDXL
  scripts\comfyui\launch_dreamshaper_xl.bat          # --reserve-vram 1
  scripts\comfyui\launch_dreamshaper_xl_novram.bat   # --novram --reserve-vram 5
  # технический прогон + замер пиков (не художественная оценка)
  python_embeded\python.exe -s scripts\comfyui\xl_run_workflow.py --url http://127.0.0.1:8188 `
      --workflow workflows\dreamshaper_xl\DREAMSHAPER_XL_BASE.json --server-pid <pid> `
      --out outputs\comfyui\style04_tech_test --report measurements\style04_technical_run.json
output: workflows\dreamshaper_xl\{DREAMSHAPER_XL_BASE,DREAMSHAPER_XL_PIXEL_STYLE,DREAMSHAPER_XL_REFERENCE_LOCK,DREAMSHAPER_XL_POSE_CONTROL}.json, prompt_orc_v1.json, README.md; measurements\dreamshaper_xl_build_report.json
checks:
  - name: Вторая установка ComfyUI не создавалась; глобальные Python/CUDA/PATH не менялись
    result: PASS
  - name: SD 1.5 checkpoint и прежние workflow сохранены
    result: PASS  (v1-5-pruned-emaonly-fp16.safetensors на месте; workflows/factoria_voxel_style/* не изменялись)
  - name: 4 workflow собраны и проверены против /api/object_info и серверным validate_prompt
    result: PASS  (BASE, PIXEL_STYLE, POSE_CONTROL — PASS; REFERENCE_LOCK — только отсутствующий локальный референс)
  - name: Ноды LoRA/IPAdapter/ControlNet зарегистрированы
    result: PASS  (в /object_info присутствуют все используемые типы)
  - name: Целостность весов (SHA-256)
    result: PASS  (6 файлов: DreamShaper XL, CLIP ViT-H, IPAdapter SDXL, ControlNet union, VoxelXL, ral-vxl)
  - name: Загрузка SDXL через torch/CUDA
    result: NOT_TESTED  (чекпоинт готов, но прогон ещё не выполнялся)
  - name: Пики RAM/VRAM и время генерации
    result: NOT_TESTED  (на момент работ на машине работали ComfyUI владельца и Unity)
  - name: Художественная оценка стиля
    result: NOT_TESTED  (по ТЗ — только владелец)
issues:
  - HF на этой машине режет одиночный поток до 20–60 КБ/с; 12 параллельных диапазонов дают ~0.9–1.0 МБ/с
  - SDXL fp16 ≈6.9 ГБ весов при 8 ГБ VRAM — режим запуска выбирается после измерения, не «по рекламе»
  - turbo-модель требует 6–10 шагов и CFG 1.5–2.5; тонкая детализация слабее классического DSXL
next_action: >
  Дождаться завершения fetch_style04_models.ps1 (или перезапустить — продолжит с места), затем
  загрузить чекпоинт probe-скриптом, выполнить технический прогон BASE и зафиксировать пики RAM/VRAM.
  После этого владелец открывает DREAMSHAPER_XL_PIXEL_STYLE.json и оценивает персонажа.
```

**Наблюдения по памяти (не измерения, а ожидания):** SDXL fp16 — UNet ≈5.1 ГБ + text encoders ≈1.6 ГБ
+ VAE ≈0.16 ГБ. На 8 ГБ VRAM это ~7 ГБ весов; `--reserve-vram 1` оставляет карте ~7 ГБ,
`--novram --reserve-vram 5` — только ~3 ГБ. Пики будут измерены и записаны в
`measurements/style04_technical_run.json` после загрузки весов.

---

## ORC-02R — процедурная генерация НОВОЙ модели Orc (2026-11-13, итерация `MODEL_REWORK`)

```yaml
id: ORC-02R
status: MODEL_PENDING_REVIEW   # технический PASS, визуальной приёмки владельцем нет
machine: WINDOWS_11
software: HumanoidBase v4 (Python stdlib) + Blender 5.2.4 headless
version: scripts/orc02r + profiles/orc.json (seed 20261113, voxel 0.0268 м)
source_url: локальный read-only источник CubeGame (measurements/orc_reference.json, только числа) + утверждённый визуальный эталон Orc
license: собственный процедурный код; источник CubeGame — только измерительный референс
install_location: D:\Projects\CubeGame Factory\scripts\orc02r
sample_asset: новая модель Orc (21 модуль, 19 540 вокселей, 1196 кубоидов)
command: >
  python scripts\orc02r\run_orc02r.py --reference measurements\orc_reference.json
  --profile scripts\orc02r\profiles\orc.json --out outputs\prototypes\orc_02r\run1
input: measurements\orc_reference.json + scripts\orc02r\profiles\orc.json
output: outputs\prototypes\orc_02r\run1\{orc_new.glb, descriptor.json, validation.json, report.json, blender\*}
checks:
  - name: Одна команда без ручных кликов
    result: PASS  (~90 с: генерация 0.23 с, Blender 73.8 с)
  - name: SOURCE_GEOMETRY_REUSED
    result: NO  (генератор читает только числа; запуск при недоступном источнике даёт тот же GLB)
  - name: Детерминизм
    result: PASS  (два независимых прогона → одинаковый SHA-256 GLB 937e1f09…)
  - name: Профиль влияет на геометрию
    result: PASS  (chest_width 19 / jaw_width 12 / наплечник на другую сторону → другой GLB, +15 кубоидов)
  - name: Blender: импорт/целостность
    result: PASS  (21 модуль, 9568 вершин, 14 352 треугольника, 0 вырожденных, 0 NaN)
  - name: Blender: оси/масштаб
    result: PASS  (1.099 × 0.536 × 1.93 м с булавой, низ на z = 0)
  - name: Независимое редактирование модулей
    result: PASS  (сдвиг forearm_L не двигает остальные)
  - name: Стыки без пересечений
    result: PASS  (0 пересечений; шов канавкой, открытый зазор 1 воксель только кисть↔предплечье)
  - name: .blend сохранён и открыт заново
    result: PASS  (214 560 Б, 21 объект)
  - name: GLB round-trip
    result: PASS  (имена/позиции/треугольники/материал/габариты совпадают)
  - name: 3 вида (front/side/3-4)
    result: PASS  (Cycles CPU, 900×1300)
issues:
  - сетка 0.0268 м ограничивает лицо: глаз 2×1 воксель, клык 2 вокселя шириной, пальцы без разделения (швы цветом)
  - шапка волос и хохолок ступенчатые (воксельные лесенки)
  - наплечник ступенчатый, но ярусы держатся на кожаной основе без отдельных металлических пластин
  - булава пересекает кулак (держится в руке) — осознанно, исключена из проверки стыков
next_action: >
  Визуальная приёмка владельцем трёх ракурсов: MODEL_APPROVED (к риг-контракту) или MODEL_REWORK
  (список правок → повторный прогон одной командой). Риг/анимации/эмоции не начаты по условиям задачи.
```

**Карточка памяти (RAM/VRAM):** пик RAM процесса Blender **1283 МБ**, пик pagefile **1489 МБ**,
прирост VRAM всей карты **0 МБ** (рендер Cycles CPU), RAM/VRAM под AI не использовались —
в конвейере нет AI-моделей.

**Чего этот тест НЕ доказывает:** пригодность модели в Unity, качество рига/анимации
(их нет по условиям задачи), «PRODUCTION_READY» и художественную приёмку.

---

## COMFY-STYLE-07 — генерация персонажей по референсам (2026-11-14)

``yaml
id: COMFY-STYLE-07
status: PARTIAL
machine: WINDOWS_11
software: ComfyUI 0.45.0 portable (python_embeded, torch 2.14.1+cu130)
checkpoint_used: DreamShaperXL_Turbo_v2_1.safetensors (ВРЕМЕННО, 28 шагов / CFG 6.0)
checkpoint_target: sd_xl_base_1.0.safetensors (openrail++, SHA-256 31e35c80…7e5b) — докачивается
adapters: ip-adapter-plus_sdxl_vit-h (apache-2.0) + CLIP-ViT-H; controlnet-union-sdxl-1.0 (apache-2.0)
inputs: references/characters_v1/human_warrior_reference_768x1024.png (из скриншота владельца), elf, dwarf
command: |
  python -s scripts\comfyui\reference_run_batch.py --url http://127.0.0.1:8190 --server-pid <pid> \
    --config workflows/factory_reference/reference_characters_dreamshaper_turbo.json \
    --matrix workflows/factory_reference/reference_test_matrix_v1.json \
    --workflow-dir workflows/factory_reference --comfy-input D:\programs\ComfyUI\ComfyUI\input \
    --out outputs/factory/reference_v1_turbo --report measurements/reference_batch_report_turbo_v1.json
output: outputs/factory/reference_v1_turbo/*.png, contact_sheet.png, measurements/reference_batch_report_turbo_v1.json
checks:
  - name: Структурная валидация 4 графов
    result: PASS (IPADAPTER / CONTROLNET / IMG2IMG / IPADAPTER_CONTROLNET)
  - name: Plumbing 4 графов (турбо-чекпоинт, 640x832)
    result: PASS (4/4, пик VRAM 7711 MB)
  - name: Матрица человек-воин (10 прогонов, 768x1024, 28 шагов, CFG 6.0)
    result: PASS 10/10, 247.7 с, пик VRAM 7743 MB, пик RSS 7490 MB
  - name: Кросс-расовый тест (эльф/дворф, CN 0.6 и IPA 0.8)
    result: PASS 4/4, 111.5 с
  - name: Близость к референсу (эвристика L1/IoU)
    result: img2img d0.25 L1 2.19 / IoU 0.992; ControlNet 0.6 IoU 0.535; IPAdapter 1.0 IoU 0.354
issues:
  - основной чекпоинт SDXL Base 1.0 ещё качается (65-75 %), финальный прогон не выполнен
  - ControlNet Canny >= 0.6 на референсе человека даёт наложение второй фигуры
  - IPAdapter style transfer теряет компактные пропорции CubeGame
  - 1024x1024 на 8 ГБ VRAM не проверяется
next_action: докачать SDXL Base 1.0, перезапустить ComfyUI, повторить тот же матрикс и сравнить контактные листы
``

## COMFY-STYLE-08 — Alpha editor и восемь фронтов

READY_FOR_OWNER_REVIEW. 8/8 PNG 768×1024 с проверенными prompt и хэшем локального входа. Пик GPU 7680 MiB по карте, пик RSS 15246 MiB, минимум доступной RAM 1 MiB. [Рецепты, условия и таблица измерений](tasks/COMFY-STYLE-08_RUN_REPORT.md).
