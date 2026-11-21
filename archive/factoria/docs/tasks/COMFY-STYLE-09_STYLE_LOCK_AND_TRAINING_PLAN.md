# COMFY-STYLE-09 — Style Lock, Cross-Race Consistency & Training Feasibility

**Date:** 2026-11-14
**Status:** PROPOSED_EXPERIMENT / NOT_OWNER_ACCEPTED
**Repo/branch:** alfuwerce-art/factoria : factory/work
**Source:** COMFY-STYLE-08 owner visual review and archived recipes.
**Scope:** Factoria only; no Unity/CubeGame changes.

## Owner review
COMFY-STYLE-08 outputs are TECHNICAL_PASS but VISUAL_PARTIAL. Eight races read correctly but block density, head/hair detail, facial proportions, outfits, and material stylization drift between races. Freeze current results as reproducible comparison baseline, NOT approved game assets.

## 1. Immutable reproducibility baseline
Archive exact workflow graph and runner source, model/LoRA/ControlNet/CLIP hashes, prompt, negative, seed, sampler, scheduler, steps, CFG, canvas, Canny thresholds and strength, reference preparation/crop and image hashes, launch flags, output hashes, time/RAM/VRAM, and licenses. Record original archived Human separately: Canny 0.9 vs eight-race matrix 0.6. Keep both. Do not silently change baseline or overwrite results. Repair misleading UI labels (checkpoint actually Turbo; LoRA actually ral-vxl; CFG actually 5.0), while preserving underlying values.

## 2. One visual grammar across races
Build a measurable style specification and face specification using *owner-supplied/rights-cleared, independent* game references: head/body ratio and silhouette, voxel block scale hierarchy (large primary volumes; medium limbs/outfit; few small face details), block density per region, flat/low-frequency color regions, pixel texture treatment, simple eyes, restrained hair and costume. Character identity should change via race-specific shape, colors and modular slots, NOT the level of detail. Catalog HUMAN, ELF, DWARF, GOBLIN, DRAGONKIN, ORC, SKELETON, FILDOR_ENDERMAN with explicit per-race anatomy exceptions.

## 3. Deterministic control before training
Compare original baseline (including Canny 0.6 and 0.9) with SDXL Base/Fictiverse where compatible, img2img low-denoise and reference segmentation/inpaint, and IPAdapter. Fix reference framing, character height on canvas, Canny edge density, same render/lighting and common style prompt. For cross-race comparisons keep all parameters constant except identity/reference. Avoid overfitting to a single seed; add at least 3 seeds for finalists. Produce contact sheets grouped by race and method. Measure visual judgments along with automated silhouette/detail density metrics, not metrics alone.

## 4. Training feasibility gate
Investigate a Factoria Style LoRA ONLY after a rights-cleared dataset is curated. Use original owner-created images/assets and/or independently licensed materials with explicit permission to train and use commercially. Never use ral-vxl-generated outputs as training examples, pseudo-labels, target images or distilled supervision; FACT-D21 prohibits derivative training and production. Keep experimental ral-vxl artifacts isolated. Confirm DreamShaper/SDXL/Fictiverse and dataset terms independently.

Build dataset inventory and provenance/rights manifests. First collect diverse images covering all race families and head/face/body variations; hold out entire identities/poses for validation. No numeric claim that 8 reference screenshots are enough. Train a small pilot style LoRA with a license-compatible SDXL base using a VRAM-safe configuration or external GPU only with owner approval. Do not install training stacks, spend on cloud, or begin training without approval of dataset and compute plan. Compare pretrained control-only solution vs pilot LoRA on unseen races and seeds, reviewing style stability, recognizable race, faces, hand/foot separation and identity leakage.

## 5. Modular character editor compatibility
Keep head, face, eyes, hair, beard, ears, horns, crest, jaw, tail, outfit and palettes as explicit recipe channels. Pixel-perfect visual similarity in a generated still does not establish swappable 3D parts or rig compatibility. Do not proceed to Tripo or production batches without owner approval.

## Acceptance deliverables
- Frozen V08 baseline and provenance manifests
- Corrected labels / no misleading node metadata
- Independent style grammar and per-race reference inventory
- Controlled cross-race comparison with contact sheets and VRAM/RAM
- Dataset legality/provenance audit and training feasibility proposal
- No artistic ACCEPTED, no production use of ral-vxl, no automatic training authorization

**Executor handoff:** Codex or DeepSeek under owner coordination.
