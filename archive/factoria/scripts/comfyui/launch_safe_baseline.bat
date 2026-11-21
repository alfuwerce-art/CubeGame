@echo off
REM ==========================================================================
REM FACTORIA / ComfyUI launcher - SAFE_BASELINE
REM Mode   : --novram (dynamic VRAM off, weights kept in system RAM)
REM Goal   : keep extra VRAM as low as possible (target <= 2 GB above idle)
REM Budget : RAM <= 15 GB, additional VRAM <= 2 GB above pre-launch baseline
REM Source : documented in 03_FACTORY_PROJECT_MEMORY.md / docs/TEST_RESULTS.md
REM NOTE   : --reserve-vram 5 leaves ~3 GB of the 8 GB card for ComfyUI.
REM          Do NOT add --lowvram: --lowvram/--novram share one argparse group.
REM ==========================================================================
setlocal
set "COMFY_DIR=D:\programs\ComfyUI"
REM UTF-8 stdout is mandatory: rgthree-comfy logs emoji, which crashes the server
REM on a cp1251 (Russian Windows) console if stdout encoding is not forced.
set "PYTHONUTF8=1"
set "PYTHONIOENCODING=utf-8"
cd /d "%COMFY_DIR%" || (echo COMFY_DIR not found: %COMFY_DIR% & pause & exit /b 1)

echo [FACTORIA] ComfyUI SAFE_BASELINE  (--novram)
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
