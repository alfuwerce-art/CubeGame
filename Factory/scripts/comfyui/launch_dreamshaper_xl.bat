@echo off
REM ==========================================================================
REM FACTORIA / ComfyUI launcher - DREAMSHAPER XL (SDXL, 8 GB VRAM profile)
REM
REM Why a separate launcher: SDXL fp16 needs ~5.1 GB for the UNet alone plus two
REM text encoders (~0.25 + 1.39 GB), so the SD1.5 setting
REM "--novram --reserve-vram 5" (only ~3 GB usable VRAM) forces heavy CPU offload.
REM This launcher keeps ComfyUI's dynamic VRAM but reserves 1 GB for Windows and
REM other apps, i.e. ~7 GB usable on the 8 GB RTX 4060 Laptop.
REM
REM If it still overflows (ComfyUI log shows constant unload/reload, or WDDM
REM shared memory kicks in), fall back in this order:
REM   1) launch_dreamshaper_xl_novram.bat   (all weights in RAM, slowest)
REM   2) launch_cpu_fallback.bat            (--cpu, no GPU inference at all)
REM
REM Limits of the stage: RAM <= 15 GB, additional VRAM <= 2 GB above pre-launch
REM baseline. NEVER combine --novram/--lowvram/--highvram/--gpu-only/--cpu:
REM they share one mutually exclusive argparse group.
REM ==========================================================================
setlocal
set "COMFY_DIR=D:\programs\ComfyUI"
cd /d "%COMFY_DIR%" || (echo COMFY_DIR not found: %COMFY_DIR% & pause & exit /b 1)
REM UTF-8 stdout is mandatory: rgthree-comfy logs emoji and kills the server on cp1251.
set "PYTHONUTF8=1"
set "PYTHONIOENCODING=utf-8"

echo [FACTORIA] ComfyUI DREAMSHAPER XL  (--reserve-vram 1, dynamic VRAM)
echo [FACTORIA] UI: http://127.0.0.1:8188
python_embeded\python.exe -s ComfyUI\main.py ^
  --windows-standalone-build ^
  --disable-auto-launch ^
  --reserve-vram 1 ^
  --port 8188

echo.
echo [FACTORIA] ComfyUI stopped (exit code %ERRORLEVEL%)
pause
