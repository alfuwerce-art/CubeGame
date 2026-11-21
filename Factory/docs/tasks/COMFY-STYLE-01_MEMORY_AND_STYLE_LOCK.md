# TASK — COMFY-STYLE-01: memory benchmark and style-lock baseline

**Status:** AUTHORIZED  
**Decision:** [FACT-D19](../decisions/FACT-D19-comfyui-style-lock-stage.md)

## Objective

Determine a ComfyUI image-generation configuration that stays within **RAM <=15 GB** and **additional VRAM <=2 GB**, then use it as the baseline for locking the Factoria character style.

## Phase A — memory only

Do not evaluate art quality first.

1. Record Windows baseline RAM and NVIDIA VRAM with ComfyUI stopped.
2. Launch the existing portable ComfyUI using a minimal-memory mode (`--novram` preferred for the first run; `--cpu` if necessary).
3. Test one lightweight SD 1.x-class checkpoint at batch=1, native ~512 resolution, simple txt2img, no ControlNet/IP-Adapter/upscaler.
4. Record process/system RAM and GPU VRAM at model load, sampling, and VAE decode.
5. Hard fail if total AI-task RAM exceeds 15 GB or additional VRAM exceeds 2 GB.
6. Record generation time and exact model/checkpoint/license/file size.

Do not download any checkpoint before reporting its source, license and download size if that download still requires owner approval under AGENTS.

## Phase B — style lock only after Phase A fits

Use the approved Orc reference and generate a small controlled set. The goal is style consistency, not final model quality.

Keep fixed:
- voxel/cuboid language;
- block sizes hierarchy;
- face/detail density;
- proportions style;
- lighting/background presentation.

Vary only character-specific attributes to test whether the style survives.

Deliver:
- workflow JSON;
- exact launch flags;
- exact checkpoint;
- measured RAM/VRAM peaks;
- generation time;
- generated samples kept local unless rights are clear;
- report with PASS/REJECTED and next recommendation.

Do not start TripoSR until the style recipe is accepted.
