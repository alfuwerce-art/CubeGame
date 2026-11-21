# TOOL_INVENTORY — CubeGame Factory Lab

**Снимок системы:** 2026-11-13 14:45 (Europe/Moscow)
**Этап:** PREP-00 выполнен; **PREP-01 выполнен** (Blender); **PREP-03 установка + runtime smoke** (ComfyUI, без AI-моделей)
**Доказательство:** `logs/PREP-00_detection_2026-11-13.txt` (аудит), `logs/PREP-01_rigify_skinning_2026-11-13.txt` (Blender/Rigify), `logs/PREP-03_run_gpu_2026-11-13.txt` и `logs/PREP-03_cuda_sanity_2026-11-13.txt` (ComfyUI).
**Важно:** установлены только **Blender 5.2.4 LTS** (`D:\programs\Blender\5.2\`) и **ComfyUI 0.45.0 portable** (`D:\programs\ComfyUI\`); 7-Zip CLI `7zr.exe` лежит в `D:\programs\_installers\` и PATH не меняет.

---

## 1. Аппаратная база (подтверждена)

| Параметр | Факт | Комментарий для Factory |
|---|---|---|
| Машина | ASUS TUF Gaming A17 FA707NV, x64 | ноутбук |
| ОС | Windows 11 Pro, 10.0.26200 (build 26200), x64 | установлена 2026-06-03, последняя загрузка 2026-11-13 04:14 |
| CPU | AMD Ryzen 5 7535HS, 6 ядер / 12 потоков, 3.3 ГГц, L3 16 МБ | пересчёт скинов, Rigify, рендер — реально ограничены 6 ядрами; GPU-тяжёлые задачи важны |
| RAM | 32 ГБ = 2 × 16 ГБ Samsung DDR5-5600; доступно ОС 31.24 ГБ | на момент аудита свободно **18.67 ГБ** |
| GPU (дискретная) | **NVIDIA GeForce RTX 4060 Laptop, 8188 MiB VRAM** | драйвер **610.47** (2026-05-19), CUDA UMD 13.3; занято 1251 MiB, **свободно 6706 MiB**, 57 °C |
| GPU (встроенная) | AMD Radeon (в CPU 7535HS), ~0.5 ГБ | для Factory не используется |
| Диск C: | NTFS, 475.9 ГБ, **свободно 68.2 ГБ** | тесно; крупные веса и кэши сюда не ставить |
| Диск D: | NTFS, 953.9 ГБ, **свободно 642.1 ГБ** | основной диск для инструментов, сэмплов и AI-весов |
| PowerShell | Windows PowerShell 5.1.26100.9818 (pwsh 7 отсутствует) | все команды в журналах — PS 5.1; учитывать кодировку UTF-16 в `Tee-Object` |

**Вывод по железу:** конфигурация соответствует ТЗ (RTX 4060 Laptop 8 ГБ VRAM, 32 ГБ RAM). Ограничения: 8 ГБ VRAM (~6.7 ГБ фактически свободно) — локальные AI-модели только по одной за раз, тяжёлые 3D/AI-кандидаты проверять документально до скачивания. Место: D: достаточно, C: не использовать для весов.

---

## 2. Обнаруженные инструменты

| Компонент | Версия | Фактический путь | Источник установки | Статус | Примечание |
|---|---|---|---|---|---|
| **Blender** | **5.2.4 LTS** (build `cd5dc428cce1`, 2026-11-10, Python 3.13.13) | `D:\programs\Blender\5.2\blender.exe` | `download.blender.org` (официальный zip, SHA-256 сверен) | **INSTALLED + SMOKE_PASS** | портативная распаковка, 901.3 МБ; PATH не менялся; конфиг изолирован в `environments\blender\config` |
| **Rigify** | **0.6.10** (встроенный аддон `addons_core`) | `D:\programs\Blender\5.2\5.2\scripts\addons_core\rigify\` | поставляется с Blender | **SMOKE_PASS** | metarig→rig: 160 deform + 546 control; включать только через `bpy.ops.preferences.addon_enable` |
| **io_scene_fbx** | 5.15.0 (встроенный) | `...\addons_core\io_scene_fbx\` | поставляется с Blender | **SMOKE_PASS** | экспорт 1.54 МБ + обратный импорт с весами |
| **io_scene_gltf2** | 5.2.41 (встроенный) | `...\addons_core\io_scene_gltf2\` | поставляется с Blender | DETECTED_NOT_TESTED | проверить в PREP-02 |
| **ComfyUI** | **0.45.0** portable (embedded python 3.13.14, torch 2.14.1+cu130, CUDA 13.0, frontend 1.58.5, templates 0.11.105) | `D:\programs\ComfyUI\` (5.75 ГБ после COMFY-STYLE-02; сервер `ComfyUI\main.py`) | GitHub release `Comfy-Org/ComfyUI` v0.45.0 (`.7z`, SHA-256 сверен) | **INSTALLED + SMOKE_PASS (runtime/API/nodes/workflows)** | изолированный embedded python; порт 8188; presets `--novram`/`--cpu` проверены; checkpoint SD1.5 fp16 установлен, хэш сверен |
| **ComfyUI-Manager** | `comfyui_manager` **4.2.2** | `python_embeded\Lib\site-packages\comfyui_manager` (PyPI-пакет, отдельного клона в `custom_nodes` нет) | PyPI; версия = pin из `ComfyUI\manager_requirements.txt` релиза 0.45.0 | **INSTALLED** | управление custom nodes; GPL-3.0 |
| **ComfyUI-Crystools** | **1.27.4** (`2f18256`) | `ComfyUI\custom_nodes\ComfyUI-Crystools` (5.3 МБ) | `github.com/crystian/ComfyUI-Crystools` | **INSTALLED + монитор проверен** | RAM/VRAM/CPU/GPU/время; роуты `/crystools/monitor/*` отвечают 200, панель в UI |
| **ComfyUI_IPAdapter_plus** | `a0f451a` | `ComfyUI\custom_nodes\ComfyUI_IPAdapter_plus` (0.8 МБ) | `github.com/cubiq/ComfyUI_IPAdapter_plus` | **INSTALLED** (`IPAdapterUnifiedLoader`, `IPAdapterAdvanced` в `/object_info`) | веса IPAdapter/CLIP Vision **не скачаны** |
| **comfyui_controlnet_aux** | `a587a4c` | `ComfyUI\custom_nodes\comfyui_controlnet_aux` (47.1 МБ) | `github.com/Fannovel16/comfyui_controlnet_aux` | **INSTALLED** (`Canny`, `LineArtPreprocessor`, `DWPreprocessor`, `DepthAnythingPreprocessor`) | веса ControlNet/препроцессоров **не скачаны** |
| **rgthree-comfy** | `bc48c6a` | `ComfyUI\custom_nodes\rgthree-comfy` (6.6 МБ) | `github.com/rgthree/rgthree-comfy` | **INSTALLED** (48 нод) | требует UTF-8 stdout при перенаправлении вывода (см. launcher'ы) |
| **SD 1.5 checkpoint** | `v1-5-pruned-emaonly-fp16.safetensors` (2.03 ГБ) | `ComfyUI\models\checkpoints\` | HF `Comfy-Org/stable-diffusion-1.5` (архив `runwayml/stable-diffusion-v1-5`) | **INSTALLED**, SHA-256 сверен | CreativeML OpenRAIL-M; единственный чекпоинт на этапе |
| **7-Zip CLI (7zr.exe)** | 26.04 | `D:\programs\_installers\7zr.exe` | `7-zip.org/a/7zr.exe` (официальный) | INSTALLED (один файл) | нужен для распаковки `.7z`; PATH не менялся |
| Python (3.13) | **3.13.14** (64-bit) | `C:\Users\bow\AppData\Local\Programs\Python\Python313\python.exe` | python.org (user install) | DETECTED | в PATH первым; `venv` работает (`import venv` OK) |
| Python (3.14) | 3.14.6 | `C:\Users\bow\AppData\Local\Python\pythoncore-3.14-64\python.exe` | python.org / Python Manager | DETECTED | доступен через `py -3.14`; **не** использовать для Lab без причины |
| pip | 26.1.2 | `...\Python313\Scripts\pip.exe` | вместе с Python | DETECTED | в глобальном окружении 47 пакетов; `torch`/`numpy` **отсутствуют** (чистая база, не AI-окружение) |
| Git | **2.55.0.windows.1** | `C:\Program Files\Git\cmd\git.exe` | git-scm.com | DETECTED | `user.name` / `user.email` **не настроены** — commit невозможен без локальной настройки |
| VS Code | **1.145.2** | `C:\Users\bow\AppData\Local\Programs\Microsoft VS Code\bin\code.cmd` | Microsoft | DETECTED | редактор кода/логов |
| Unity Hub | **3.22.5** | `D:\Unity Hub\Unity Hub.exe` | Unity Technologies | DETECTED_NOT_TESTED | только обнаружение, не запускался |
| Unity Editor | **6000.3.27f2** | `D:\Unity\Hub\Editor\6000.3.27f2\Editor\Unity.exe` | Unity Hub | DETECTED_NOT_TESTED | целевая LTS-линия из ТЗ присутствует |
| Unity Editor | 6000.6.7f1 | `D:\Unity\6000.6.7f1\Editor\Unity.exe` | Unity (отдельно от Hub) | DETECTED_NOT_TESTED | вторая версия; **не открывать** CubeGame на этом этапе |
| Ollama | **0.40.10** | `C:\Users\bow\AppData\Local\Programs\Ollama\ollama.exe` | ollama.com | DETECTED_NOT_RUNNING | API `127.0.0.1:11434` не отвечает, процесс не запущен |
| .NET Runtimes | 7.0.14, 8.0.34 | `C:\Program Files\dotnet\` | Microsoft | DETECTED | нужны некоторым GUI/CLI-утилитам |
| WinRAR | 7.13 | `C:\Program Files\WinRAR` | win.rar GmbH | DETECTED | может распаковать Blender `.zip`; `Expand-Archive` тоже доступен |
| NVIDIA App / драйвер | 610.47 | `C:\Program Files\NVIDIA Corporation\` | NVIDIA | DETECTED | CUDA UMD 13.3; **CUDA Toolkit (nvcc) отсутствует** |

---

## 3. Отсутствующие инструменты (подтверждено поиском)

| Компонент | Статус | Как проверялось | Что это значит |
|---|---|---|---|
| **Blender** | **INSTALLED 2026-11-13 12:19** | PREP-00: не найден (PATH, реестр, рекурсивный поиск); PREP-01: скачан официальный `blender-5.2.4-windows-x64.zip`, SHA-256 `bbd78f89…967c` сверен, распакован в `D:\programs\Blender\5.2\` | **блокер PREP-01 снят**: версия, GUI, headless, Rigify, skinning, FBX проверены (`docs/TEST_RESULTS.md`, PREP-01) |
| **Rigify** | N/A | часть Blender | проверить сразу после установки Blender |
| **ComfyUI** | **INSTALLED 2026-11-13 14:26** | PREP-00: не найден (нет каталога `*omfy*`, ярлыков, реестра, порт 8188 не слушался); решение владельца — старый экземпляр удалён, установка с нуля | **блокер PREP-03 снят:** официальный portable 0.45.0 (SHA-256 `60c12e8c…a6f`), сервер/UI/API-workflow без моделей/CUDA проверены (`docs/TEST_RESULTS.md`, PREP-03). Веса AI-моделей отсутствуют |
| **Cascadeur** | **NOT_INSTALLED** | нет `Cascadeur*.exe`, нет в реестре | кандидат PREP-05; до установки проверить лицензию |
| **n8n** | **NOT_INSTALLED** | нет в PATH/реестре | плюс: **системного Node.js/npm нет** — n8n потребовал бы установку Node.js (не делать без доказанной необходимости) |
| **Node.js / npm / pnpm** | **NOT_FOUND** (системный) | нет в PATH | связка `node` встречается только внутри Codex runtime и не является системным инструментом |
| **ffmpeg** | NOT_FOUND (в PATH) | нет команды | может понадобиться для превью/видео; не блокер |
| **7-Zip** | STALE_REGISTRY | запись Uninstall указывает `D:\programs\7-Zip\`, но каталога и `7z.exe` нет | считать неустановленным, не полагаться |
| **CUDA Toolkit** | NOT_FOUND | `nvcc` отсутствует | для ComfyUI/PyTorch обычно достаточно драйвера + CUDA-сборки wheel; отдельный Toolkit пока не нужен |

---

## 4. Ограничения, конфликты и ловушки

1. **`python3` — ловушка Windows.** `C:\Users\bow\AppData\Local\Microsoft\WindowsApps\python3.exe` — Store-заглушка, отвечает «Python was not found». В скриптах использовать только `python` или `py -3.13`, никогда `python3`.
2. **Два Python.** 3.13.14 (в PATH, по умолчанию) и 3.14.6 (через `py`). Для Lab использовать **изолированный venv на 3.13.14** и фиксировать версию; 3.14 не трогать.
3. **Глобальное Python-окружение уже содержит 47 сторонних пакетов** (крипто/VPN-утилиты). Ничего в него не устанавливать — только `venv` в `environments\`.
4. **Git-идентичность не задана.** `git config user.name/user.email` пустые → первый commit упадёт. Настроить **локально в репозитории** (не глобально) при первой фиксации.
5. **C: почти заполнен (68.2 ГБ).** Blender и любые веса — на D:.
6. **Глобальные изменения запрещены:** PATH, драйверы, CUDA, переменные окружения не менялись и не должны меняться на этом этапе.
7. **Посторонний фон:** на момент аудита работали браузеры, ChatGPT/Codex, Happ, DeepSeek Harness, wallpaper engine. Они держат ~1.25 ГБ VRAM из 8. Чужие процессы **не останавливались**. Для AI-тестов позже учитывать фактически свободные ~6.7 ГБ VRAM, а не 8.
8. **`D:\Games\CubeGame`** — игровая установка, а **`D:\Projects\CubeGame`** — рабочий репозиторий CubeGame (`.git` подтверждён). Оба вне лаборатории и не затрагивались.
9. **Каталог проекта (актуально с 2026-11-13 12:05 MSK):** единственная рабочая папка — `D:\Projects\CubeGame Factory`. Она содержит четыре документа, журналы `docs/`, доказательства `logs/` и git-репозиторий. Ранее созданный `D:\Projects\CubeGameFactoryLab` — архив прошлой сессии, для новой работы не использовать (SUPERSEDED, FACT-D16 в памяти).

---

## 5. Что чем доказано (обновлено после PREP-01 / PREP-03)

**Доказано реальным запуском:**
- Blender 5.2.4 LTS устанавливается портативно, запускается в GUI и в `--background`, сохраняет `.blend`, открывает его в GUI.
- Штатный Rigify 0.6.10 включается через API, строит control rig из human metarig, а `Parent with Automatic Weights` даёт работающий skinning на собственной тестовой фигуре.
- Control-кости (IK-таргеты и FK при `IK_FK=1`) деформируют меш; Rigify DEF-кости напрямую не управляемы (constraints).
- FBX-экспорт и обратный импорт сохраняют армтуру, веса и рабочую деформацию.
- ComfyUI 0.45.0 portable: сервер стартует (тёплый ~11 с, холодный ~146 с), веб-интерфейс отдаётся, API `POST /prompt` → `/history` → `/view` работает, минимальный граф без моделей выполняется за 1 с и сохраняет PNG.
- Встроенный torch portable-сборки реально считает на GPU: matmul 2048×2048 за 0.12 с, пик allocated 56.1 МБ.

**Не доказано:**
- Пригодность auto-weights для реальных плеч/таза/доспехов и необычных тел (проверялся процедурный манекен).
- Подгонка скелета под пропорции произвольной модели: Rigify **не** Rig Fitter (PREP-04).
- Корректность осей/единиц/bind pose и анимаций в Unity — Unity не запускался (запрещено на этом этапе).
- Что экспортированный FBX пригоден как game-skeleton: выгружается весь control rig (706 костей).
- Способность ComfyUI генерировать 3D-модели, качество/лицензии AI-выходов, поведение VRAM под реальными моделями (весов нет).
- Совместимость каких-либо AI-моделей с 8 ГБ VRAM.
