# TASK — ORC-02R: generate NEW Orc geometry from CubeGame Orc reference

**For:** DeepSeek Harness / CubeGame Factory  
**Status:** AUTHORIZED / CURRENT TASK  
**Decision:** [FACT-D18](../decisions/FACT-D18-orc02-unattended-pipeline.md)  
**Repo:** `alfuwerce-art/factoria`, branch `factory/work`

## Non-negotiable goal

Create the **first NEW Orc model** for Factoria.

The existing CubeGame Orc is **REFERENCE ONLY**. Read its dimensions/proportions/silhouette. Do **NOT**:
- split the old voxel mesh and call that generation;
- reuse old voxels/vertices as final geometry;
- scale/reposition old parts and call that a new model;
- spend time recovering Alpha rest pose unless required only to measure proportions.

The output geometry must be procedurally constructed **from scratch** by code/Blender.

## Required pipeline

`CubeGame Orc reference → measurements → HumanoidBase parameters → OrcProfile parameters → NEW large forms → NEW medium forms → NEW small voxel details → editable .blend/.glb → 3 views → producer review`

### 1. Reference extraction
Read the existing Orc read-only and extract only numerical/structural information:
- height;
- shoulder/chest/pelvis widths;
- head size and head/body ratio;
- upper-arm/forearm/hand lengths;
- thigh/shin/foot lengths;
- major silhouette ratios.

Write these measurements to a small JSON descriptor. No closed geometry in GitHub.

### 2. New procedural model generator
Implement a generator that creates **new geometry** from primitive cuboids/voxels of varied sizes.

Minimum new parts:
- head (jaw/chin shape integrated into head);
- eyes;
- brows;
- mouth region;
- optional hair;
- optional beard for male;
- torso;
- pelvis;
- upper arms;
- forearms;
- hands;
- thighs;
- shins;
- feet.

Use 3 detail scales:
- LARGE = silhouette/anatomy;
- MEDIUM = muscle mass, clothing, armor volumes;
- SMALL = face, hair/beard, seams, armor accents.

The generator must not merely subdivide or scatter random boxes. Each layer must have a semantic purpose.

### 3. Orc profile
Keep Orc-specific values in data/config, not hardcoded in generator logic. Example categories:
- broad shoulders;
- thick upper arms/forearms;
- heavy torso;
- larger hands/feet;
- strong brow;
- wider head;
- non-cartoon face;
- asymmetric/heavy armor option;
- detail density.

The exact values are experimental; record them in the profile and iterate.

### 4. LEGO readiness
The **new model** should already be organized into logical independent body modules suitable for future rigging, but modularity is secondary to producing a readable Orc.
Do not create skeleton/skin/animation/emotions yet.

### 5. Automation
One command must:
- read measurements/profile;
- generate the new model;
- open/validate Blender headless;
- save .blend;
- export .glb;
- render front/side/3-4;
- emit JSON report.

No required Blender clicks between stages.

### 6. Acceptance
Technical acceptance requires proof that final geometry is new:
- report counts/IDs showing generated primitives originate from the new generator;
- no source voxel/vertex arrays copied into output;
- source hash only used for traceability;
- same config/seed => reproducible geometry;
- changing OrcProfile changes the new geometry.

Art acceptance is separate: **MODEL_PENDING_REVIEW** until owner reviews the three views.

### 7. Stop conditions
Do NOT move to:
- rig;
- skeleton;
- skinning;
- animation;
- emotions;
- mass generation;
- other species.

Do not use ComfyUI/AI in this task unless separately authorized after the procedural baseline is shown.

## Deliverables
- generator code;
- humanoid base/profile config;
- reference measurement JSON (numbers only);
- .blend + .glb locally;
- front/side/3-4 renders locally;
- validation report;
- exact one-command invocation;
- resource measurements;
- note of any manual interventions.

## Status report
```
TASK=ORC-02R
SOURCE_REFERENCE=
MEASUREMENTS=
NEW_GEOMETRY_PROVEN=
SOURCE_GEOMETRY_REUSED=NO
GENERATOR=
ORC_PROFILE=
ONE_COMMAND=
BLEND=
GLB=
PREVIEWS=
DETERMINISM=
MANUAL_STEPS=
RAM_PEAK=
VRAM_PEAK=
STATUS=
BLOCKERS=
REMOTE_SHA=
NEXT=producer visual review only
```
