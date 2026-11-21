@echo off
REM ==========================================================================
REM FACTORIA / ComfyUI launcher - DREAMSHAPER XL, RAM-first fallback
REM
REM Same as launch_dreamshaper_xl.bat but with --novram --reserve-vram 5:
REM the SD1.5-era behaviour that keeps model weights in system RAM. Use it when
REM the dynamic-VRAM launcher exceeds the VRAM budget or the driver starts
REM swapping into shared memory. Expect a large slowdown for SDXL.
REM ==========================================================================
setlocal
set "COMFY_DIR=D:\programs\ComfyUI"
cd /d "%COMFY_DIR%" || (echo COMFY_DIR not found: %COMFY_DIR% & pause & exit /b 1)
set "PYTHONUTF8=1"
set "PYTHONIOENCODING=utf-8"

echo [FACTORIA] ComfyUI DREAMSHAPER XL  (--novram --reserve-vram 5, RAM-first)
echo [FACTORIA] UI: http://127.0.0.1:8188
python_embeded\python.exe -s ComfyUI\main.py ^
  --windows-standalone-build ^
  --disable-auto-launch ^
  --novram ^
  --reserve-vram 5 ^
  --port 8188

echo.
echo [FACTORIA] ComfyUI stopped (exit code %ERRORLEVEL%)
pause
