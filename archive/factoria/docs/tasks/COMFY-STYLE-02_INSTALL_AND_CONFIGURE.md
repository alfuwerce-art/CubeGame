# TASK — COMFY-STYLE-02: install/configure the complete minimal ComfyUI style stack

**Status:** AUTHORIZED  
**Date:** 2026-11-13  
**Project:** Factoria  
**Repo:** `alfuwerce-art/factoria`, branch `factory/work`  
**Local:** `D:\Projects\CubeGame Factory`  
**Decision:** [FACT-D19](../decisions/FACT-D19-comfyui-style-lock-stage.md)

## Goal

Prepare the existing portable ComfyUI installation so the owner can immediately test image generation and style locking afterward. DeepSeek's job here is **installation/configuration/smoke validation**, not artistic generation.

Do not create a new ComfyUI installation if the existing one is healthy. Inspect current version/paths first and preserve the existing environment.

## Install / verify

Install or verify exactly this minimal stack:

1. **ComfyUI-Manager** — official Comfy Org manager. Prefer the currently supported built-in/official Manager path for the installed ComfyUI version; do not create a duplicate legacy Manager folder if Manager is already present.
2. **ComfyUI-Crystools** — resource monitor for CPU/GPU/RAM/VRAM and timing.
3. **ComfyUI_IPAdapter_plus** — reference-image/style conditioning nodes. Note in report that upstream is in maintenance-only mode since 2025-04-14; install only if compatible with current ComfyUI and smoke import succeeds.
4. **comfyui-controlnet-aux** — preprocessors for future silhouette/edge/depth conditioning. This is nodes/preprocessors only at this stage; no ControlNet model weights required yet.
5. **rgthree-comfy** — workflow organization/quality-of-life.

Use each project's documented installation method. Install Python dependencies with the portable ComfyUI embedded Python, not global Python. Record exact repository URL, commit SHA/version, requirements installed, and any dependency changes.

## Base checkpoint

Install exactly one baseline checkpoint for the first generation test:

- `Comfy-Org/stable-diffusion-1.5`
- `v1-5-pruned-emaonly-fp16.safetensors`
- expected file size ~2.13 GB
- SHA-256 published by source: `e9476a13728cd75d8279f6ec8bad753a66a1957ca375a1464dc63b37db6e3916`
- license: CreativeML OpenRAIL-M
- location: normal ComfyUI `models/checkpoints` path

Verify file hash after download.

**Do not install SDXL/FLUX/Qwen image checkpoints in this task.**

## Do NOT download yet

Do not download IPAdapter encoder/model weights or ControlNet weights yet. Their custom nodes must be installed and load successfully, but model weights are deferred until the base SD1.5 memory/generation test confirms the hard limits.

This intentionally separates:
- node installation/configuration;
- base checkpoint;
- later optional style-reference weights.

## Launch presets

Create reproducible Windows launchers/config notes for:

1. **SAFE_BASELINE**: ComfyUI with `--novram`, batch=1, 512-class workflow.
2. **CPU_FALLBACK**: ComfyUI with `--cpu` if SAFE_BASELINE exceeds +2 GB VRAM.

Do not modify global PATH, CUDA, driver, or system Python.

## Workflow preparation

Create a ready-to-open baseline workflow JSON using only core nodes + Crystools monitoring:

`CheckpointLoaderSimple → CLIPTextEncode positive/negative → EmptyLatentImage 512x512 batch 1 → KSampler → VAEDecode → SaveImage`

Prepare placeholders/comments for later:
- IPAdapter style/reference input;
- ControlNet conditioning input;
- optional LoRA loader.

Do **not** require those optional nodes in the base workflow so the owner can test SD1.5 immediately.

Also create a second **STYLE_REFERENCE_TEMPLATE** workflow with the installed IPAdapter nodes wired structurally, but leave IPAdapter model/CLIP Vision model selectors unresolved/disabled because their weights are not downloaded yet.

## Smoke validation only

DeepSeek may:
- start ComfyUI;
- verify web/API comes up;
- verify all custom node packs import with no missing-module errors;
- open/validate workflow JSONs structurally;
- verify checkpoint is detected;
- verify Crystools resource monitor is available.

**Do not run the artistic Orc generation.** Owner will perform the first real generation afterward.

If a plugin breaks current ComfyUI, isolate/disable that plugin and report it rather than downgrading the entire environment blindly.

## Hard resource policy

Future generation must stay within:
- total AI-task RAM <=15 GB peak;
- additional VRAM <=2 GB over pre-launch baseline.

Installation itself is not a generation PASS. Do not claim compatibility with the memory cap until an actual generation is measured.

## Evidence / deliverables

Record:
- existing ComfyUI version and portable path;
- Python/Torch/CUDA versions used by portable environment;
- each node pack repo + commit SHA;
- dependencies changed;
- checkpoint path, file size, SHA-256, license;
- ComfyUI startup flags;
- startup log showing no missing custom-node imports;
- workflow file paths;
- baseline idle RAM/VRAM after startup (informational only);
- any blockers.

Update `03_FACTORY_PROJECT_MEMORY.md` and push only code/config/docs/logs to `factory/work`. Do not commit checkpoint/model weight files.

## Final report format

```
TASK=COMFY-STYLE-02
STATUS=
COMFY_VERSION=
COMFY_PATH=
MANAGER=
CRYSTOOLS=
IPADAPTER_PLUS=
CONTROLNET_AUX=
RGTHREE=
CHECKPOINT=
CHECKPOINT_SIZE=
CHECKPOINT_SHA256=
CHECKPOINT_LICENSE=
SAFE_BASELINE_LAUNCH=
CPU_FALLBACK_LAUNCH=
BASE_WORKFLOW=
STYLE_REFERENCE_TEMPLATE=
CUSTOM_NODE_IMPORT_ERRORS=
IDLE_RAM=
IDLE_VRAM=
WEIGHTS_DEFERRED=IPAdapter + ControlNet
BLOCKERS=
GIT_BRANCH=factory/work
PUSH=
REMOTE_SHA=
NEXT=owner generation test + memory measurement
```
