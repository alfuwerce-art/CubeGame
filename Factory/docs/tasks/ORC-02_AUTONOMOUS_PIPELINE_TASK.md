# TASK — ORC-02: unattended voxel redesign pipeline (source Orc)

**For:** DeepSeek Harness / CubeGame Factory  
**Status:** SUPERSEDED by `ORC-02R_NEW_MODEL_GENERATION_TASK.md` — old wording caused source-geometry disassembly/reassembly instead of generation of a new model  
**Decision:** [FACT-D18](../decisions/FACT-D18-orc02-unattended-pipeline.md)  
**Repo:** `alfuwerce-art/factoria`, branch `factory/work`. **Local:** `D:\Projects\CubeGame Factory`.

## Goal
Build a **repeatable unattended pipeline**, not a one-off Blender model. Obtain a read-only working copy of the *existing* Orc mesh from CubeGame (exact source/provenance documented, no source modifications) and run one-command modular voxel redesign. The final orc must be visually reviewed by producer; tech PASS does not equal art approval.

## Do in this order
1. Read `AGENTS.md`, 01/02/03 project docs, FACT-D17, FACT-D18, ORC-01 report. Check working tree and existing local Orc assets **read-only**. Choose the already converted/available Orc geometry first; **do not rerun Alpha extraction or start Unity**. If none safely accessible, report the exact blocker instead of substituting ORC-01 mock geometry.
2. Implement minimal `run_orc02` CLI with machine-readable manifest + config (input file, stable source hash, profile, seed, dimensions, detail parameters, output location). Isolate core functions from Orc-specific parameters. Do not install heavy AI tools.
3. Process source-derived anatomy to modular independent head, eyes, brows, optional hair/beard, torso/pelvis, shoulder/upper arm, forearm, hand, thigh, shin and foot. Jaw is part of head. Retain deliberate gaps and cubic transitions. Where source anatomy is ambiguous, report missing landmark/mapping explicitly: do not silently invent source identity.
4. Generate varied-size block detailing with coherent silhouette and surfaces, not random cube noise. Preserve a pixelated expressive (not cartoon) face; **no facial expressions yet**.
5. Blender headless import/validation/save .blend/export .glb and 3 views; deterministic output; validate part separation, no unwanted collisions, axes, scale, colors, geometry, object names, and round-trip. Record per-stage timings, RAM and VRAM.
6. Execute **two full runs** with same input/config/seed and compare canonical geometry descriptors and hashes (ignore genuinely nondeterministic container metadata if any). Test a changed config affects geometry, and a missing/corrupt source returns a meaningful nonzero error.
7. Produce exact command and self-contained readme for future operator to run independently. Stop at `MODEL_PENDING_REVIEW` for art. Report count and duration of human interventions.

## Safety
- Read-only CubeGame, do not touch Unity runtime, `.scratch`, Alpha, game git branches, saves or assets. No closed meshes/derivatives on public GitHub. Do not overwrite existing Factoria outputs; stage to a new run directory.
- No rig, skeleton, skinning, animation, emotions, face-rig, bulk generation, cloud costs or heavy model weights.
- If AI is later proposed, existing budget is 15 GB RAM and +2 GB VRAM inclusive of process overhead.
- If the selected source geometry derives from Alpha, do not claim the result is cleared for commercial use; redesign must be independently evaluated.

## Acceptance
`PIPELINE_TECH_PASS` requires **actual original Orc geometry as input**, two headless runs, editable modules, three preview images, validated GLB & BLEND, logs and one-command instructions. `ORC-02=PARTIAL` if input pipeline works but artistic source conversion/detail is incomplete. `BLOCKED_SOURCE` if lawful editable source cannot be acquired. `MODEL_APPROVED` only after producer reviews visuals.

## Deliverables/Report
Commit scripts, tests, manifests, config samples, and inspectable reports (exclude restricted meshes, checkpoints, secrets). Update `03_FACTORY_PROJECT_MEMORY.md`; push `factory/work` without force. Report: `SOURCE_ID`, `SOURCE_RIGHTS`, `SOURCE_HASH`, `INPUT_GEOMETRY_PROVEN`, `ONE_COMMAND`, `RUN1`, `RUN2`, `DETERMINISM`, `BLEND`, `GLB`, `PARTS`, `PREVIEWS`, `RAM_PEAK`, `VRAM_PEAK`, `MANUAL_STEPS`, `STATUS`, `REMOTE_SHA`, `BLOCKERS`, `NEXT`.
