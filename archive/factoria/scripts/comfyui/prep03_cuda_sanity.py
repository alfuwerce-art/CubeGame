"""PREP-03: проверка, что встроенный PyTorch portable-сборки реально считает на GPU.

Не требует моделей и не скачивает веса: одна матричная операция на CUDA.
Печатает версию torch, устройство, capability, время операции и пиковую
выделенную VRAM. Запуск:
    D:\\programs\\ComfyUI\\python_embeded\\python.exe -s scripts\\comfyui\\prep03_cuda_sanity.py
"""

import json
import time

import torch

out = {
    "torch": torch.__version__,
    "cuda_build": torch.version.cuda,
    "cuda_available": torch.cuda.is_available(),
    "device_count": torch.cuda.device_count() if torch.cuda.is_available() else 0,
}

if torch.cuda.is_available():
    dev = torch.device("cuda:0")
    props = torch.cuda.get_device_properties(0)
    out["device_name"] = props.name
    out["capability"] = f"{props.major}.{props.minor}"
    out["total_vram_MB"] = round(props.total_memory / 2**20, 1)

    n = 2048
    torch.cuda.reset_peak_memory_stats()
    t0 = time.time()
    a = torch.randn(n, n, device=dev)
    b = torch.randn(n, n, device=dev)
    c = a @ b
    torch.cuda.synchronize()
    out["matmul_size"] = [n, n]
    out["matmul_seconds"] = round(time.time() - t0, 4)
    out["matmul_checksum"] = round(float(c[0, 0].item()), 4)
    out["peak_allocated_MB"] = round(torch.cuda.max_memory_allocated() / 2**20, 1)
    out["peak_reserved_MB"] = round(torch.cuda.max_memory_reserved() / 2**20, 1)
    out["result"] = "PASS" if abs(out["matmul_checksum"]) > 0 else "FAIL"
else:
    out["result"] = "NO_CUDA"

print("PREP03_CUDA_JSON=" + json.dumps(out, ensure_ascii=False, indent=2))
