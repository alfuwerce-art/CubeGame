# FACT-D19 — ComfyUI stage: style lock before 3D generation

**Decision date:** 2026-11-13  
**Status:** APPROVED / CURRENT DIRECTION  
**Project:** Factoria

## Goal

Pause further ORC-02R geometry iteration. The next active experiment is to **lock the visual style of characters in ComfyUI** before continuing image→3D work.

This stage does **not** judge a final game model. Its goal is to prove that one controlled ComfyUI workflow can repeatedly produce characters in the same approved voxel/cuboid visual language.

## Resource cap

Hard owner limit for the entire AI image-generation task:
- RAM <= 15 GB peak;
- additional VRAM <= 2 GB peak above baseline;
- include model weights, VAE/text encoder, intermediate tensors and runtime overhead.

Because normal GPU-resident image models usually exceed +2 GB VRAM, the first benchmark must use ComfyUI `--novram` or `--cpu`/CPU-offload style execution and measure real peaks. Do not assume a model fits from file size alone.

## First benchmark

Start with the lightest practical Stable Diffusion 1.x-class checkpoint/workflow at batch=1 and native low resolution (e.g. 512-class). No SDXL/FLUX/Qwen image workflow until measured evidence shows both caps can be respected.

Measure:
- baseline RAM/VRAM before ComfyUI;
- idle after model load;
- peak during text encoding;
- peak during sampling;
- peak during VAE decode;
- wall time/image.

If RAM >15 GB or additional VRAM >2 GB, stop and mark the configuration REJECTED.

## Style-lock output

Once a configuration fits:
1. Use the approved Orc visual reference and written style constraints.
2. Generate multiple Orc concepts while keeping the same style.
3. Change identity/details while preserving the style.
4. Select an approved style recipe/workflow.
5. Only after style lock, test the approved image through an image-to-3D tool such as TripoSR.

No rigging, animation, emotion system, Unity integration or mass production in this stage.
