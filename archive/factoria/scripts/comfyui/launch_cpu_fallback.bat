@echo off
REM ==========================================================================
REM FACTORIA / ComfyUI launcher - CPU_FALLBACK
REM Mode   : --cpu (no GPU inference at all)
REM Goal   : guaranteed 0 MB additional VRAM when SAFE_BASELINE is not enough
REM Budget : RAM <= 15 GB; VRAM delta should stay ~0 (UI only)
REM WARNING: sampling on CPU for SD1.5 at 512x768 is slow (minutes per image).
REM ==========================================================================
setlocal
set "COMFY_DIR=D:\programs\ComfyUI"
REM UTF-8 stdout is mandatory (see launch_safe_baseline.bat): cp1251 console
REM + rgthree emoji log = server crash during startup.
set "PYTHONUTF8=1"
set "PYTHONIOENCODING=utf-8"
cd /d "%COMFY_DIR%" || (echo COMFY_DIR not found: %COMFY_DIR% & pause & exit /b 1)

echo [FACTORIA] ComfyUI CPU_FALLBACK  (--cpu)
echo [FACTORIA] UI: http://127.0.0.1:8188
python_embeded\python.exe -s ComfyUI\main.py ^
  --windows-standalone-build ^
  --disable-auto-launch ^
  --cpu ^
  --port 8188

echo.
echo [FACTORIA] ComfyUI stopped (exit code %ERRORLEVEL%)
pause
