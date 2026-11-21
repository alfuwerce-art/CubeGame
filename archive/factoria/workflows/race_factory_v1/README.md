# COMFY-STYLE-08 — воспроизведение фронтов

[Отчёт, аудит и карточки](../../docs/tasks/COMFY-STYLE-08_RUN_REPORT.md). Human из архива имеет отдельные UI/API JSON, графы COMFY-STYLE-07 доступны в соседнем каталоге `factory_reference`.

`ral-vxl.safetensors` (RalFinger) используется локально для сравнения по указанию владельца. См. [FACT-D21](../../docs/decisions/FACT-D21-civitai-voxel-lora-license.md). Изображения с ограниченной LoRA и пользовательские референсы хранятся в локальных артефактах.

Из корня Factoria переустановки моделей не требуется. Сверить SHA-256 установленных весов с `races/*/generation_profile.json`. Переменные в примере — существующие локальные пути и PID выделенного ComfyUI.

```powershell
& $comfyPython -B scripts/comfyui/prepare_race_factory_v1.py --sources $ownerScreenshots --out $comfyInput
& $comfyPython -B scripts/comfyui/race_factory_v1.py --build-dir $evidenceDir --url http://127.0.0.1:8191 --comfy-input $comfyInput --comfy-output $comfyOutput --server-pid $comfyPid
```

Параметр `--race HUMAN ELF` ограничивает повтор выбранными расами. Без `--url` раннер только собирает API JSON. Подготовка использует Pillow, OpenCV и NumPy из существующего окружения ComfyUI. Раннер проверяет SHA-256 входа и читает исполненный prompt из PNG. Измерение памяти использует psutil и NVIDIA SMI.

ComfyUI 0.45.0, PyTorch 2.14.1+cu130, RTX 4060 Laptop. Выделенный сервер запускался на `127.0.0.1:8191` с `--novram --reserve-vram 5 --disable-all-custom-nodes --offline` и отдельными каталогами input, output, temp, user. Кадры ставились в очередь по одному. После холодного старта измерена сильная нехватка свободной RAM, см. отчёт.
