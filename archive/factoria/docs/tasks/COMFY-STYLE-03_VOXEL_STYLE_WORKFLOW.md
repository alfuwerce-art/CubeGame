# TASK — COMFY-STYLE-03: Factoria VOXEL_STYLE workflow

**Status:** AUTHORIZED / PREPARE AFTER COMFY-STYLE-02  
**Date:** 2026-11-13  
**Project:** Factoria  
**Repo:** `alfuwerce-art/factoria`, branch `factory/work`

## Goal

Prepare a ComfyUI workflow whose purpose is **not generic image generation**, but locking the approved Factoria voxel/cuboid character style from a local visual reference.

The reference image is local-only and must not be published to GitHub unless rights are explicitly cleared.

## Style conditioning stack

### A. IPAdapter = visual style reference
Use the approved Orc reference image as the primary visual style condition.

Required intent:
- transfer voxel/cuboid visual language;
- preserve block-size hierarchy;
- preserve compact chunky character proportions;
- preserve pixel/block face treatment;
- preserve low-smoothness / hard-edged shapes;
- avoid copying the exact identity, pose, costume or composition when generating other characters.

Prefer a style-oriented IPAdapter mode where supported. Start conservatively; weight is a tuneable parameter, not a fixed truth.

### B. Text prompt = identity/content
Text defines the requested character:
- race/species;
- gender/body type;
- clothing/armor;
- accessories;
- color accents;
- pose requirement;
- front/side/3-4 presentation.

Text must also reinforce:
- voxel/cuboid construction;
- blocks of varied sizes;
- hard edges;
- modular body segments;
- non-cartoon proportions;
- game-ready concept presentation.

### C. ControlNet = structure later
Prepare a disabled/optional branch for silhouette/pose control using controlnet-aux preprocessors.

Do not require ControlNet weights in the first style-lock test. After IPAdapter style is proven, add only the specific ControlNet required for:
- silhouette/edge preservation (Canny/Lineart), or
- pose (OpenPose/DWPose).

### D. LoRA = future persistent style lock
Do not train LoRA yet.

If IPAdapter + prompt still drifts across different characters, the next escalation is a dedicated Factoria style LoRA trained only on approved style examples. LoRA would become the persistent style prior; IPAdapter would remain useful for per-character reference images.

## VOXEL_STYLE prompt contract

Positive prompt template should contain a stable style prefix and a variable character block.

Stable style intent:
- stylized voxel/cuboid 3D character;
- hard-edged block geometry;
- varied block sizes (large silhouette blocks, medium anatomy/clothing blocks, small face/detail blocks);
- compact readable silhouette;
- modular limbs;
- pixel/block face;
- no smooth organic sculpting;
- neutral studio/background presentation suitable for later image-to-3D reconstruction.

Variable block:
- species / sex / age band;
- body type;
- face traits;
- outfit / armor;
- accessories;
- palette;
- pose / camera.

Negative intent:
- smooth plastic;
- rounded cartoon;
- anime;
- photoreal skin;
- painterly brushwork;
- soft clay;
- low-information Minecraft cube-only character;
- exaggerated chibi;
- fused limbs;
- cluttered background;
- extreme perspective;
- motion blur.

## Workflow variants to prepare

1. `FACTORIA_VOXEL_STYLE_BASE.json`
   - SD1.5 baseline;
   - txt2img only;
   - fixed style prefix;
   - 512-class, batch 1;
   - resource monitoring.

2. `FACTORIA_VOXEL_STYLE_IPADAPTER.json`
   - same base;
   - local style reference image;
   - IPAdapter conditioning;
   - owner can tune style weight;
   - no ControlNet required.

3. `FACTORIA_VOXEL_STYLE_CONTROL_TEMPLATE.json`
   - IPAdapter branch + optional disabled ControlNet branch;
   - used later for front/profile/pose consistency.

## Memory constraints

Hard owner limits for the entire generation task:
- RAM <=15 GB peak;
- additional VRAM <=2 GB over baseline.

The IPAdapter model itself is small compared with the required CLIP Vision encoder, but the complete runtime must be measured. Do not claim the IPAdapter workflow fits until an actual run records peaks.

First test order:
1. BASE;
2. IPAdapter;
3. optional ControlNet only if the first two pass.

If IPAdapter breaks the cap, keep the style spec but mark the workflow `REJECTED_BY_MEMORY` and do not silently relax the limit.

## Output acceptance

This stage does NOT require a final game model.

Success means:
- different generated Orc variants clearly share the same voxel visual language;
- changing character attributes does not destroy the style;
- output remains suitable as a clean image-to-3D reference;
- style recipe is reproducible from saved workflow + prompt contract + reference.

Status:
- `STYLE_BASELINE_PENDING_REVIEW`
- `STYLE_REWORK`
- `STYLE_APPROVED` only by owner.

## Restrictions

No rigging, animation, emotions, Unity integration or TripoSR in this task.
No publication of the local reference image or generated derivative images unless explicitly approved.
