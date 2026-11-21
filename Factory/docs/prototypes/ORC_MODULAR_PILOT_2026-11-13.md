# ORC MODULAR PILOT — 2026-11-13

**State:** TECHNICAL_GEOMETRY_TESTED / **BLENDER_LOCAL_VALIDATED (2026-11-13, ORC-01)** / VISUAL_NOT_APPROVED.  
**Priority:** owner authorized beginning a one-character prototype now. This is an **exception for one isolated experimental model**, not authorization to start M1, edit CubeGame, import closed Alpha assets, or launch production batches.

> **Локальная проверка выполнена:** см. [ORC01_LOCAL_VALIDATION.md](ORC01_LOCAL_VALIDATION.md) — генератор отработал на Windows, GLB импортирован в Blender 5.2.4 (20 независимых модулей), `.blend` сохранён и переоткрыт, GLB round-trip без потерь, три вида отрендерены. Найдены и минимально исправлены два дефекта (пересечение 4 стыков конечностей; `COLOR_0` без `normalized`). Визуальной приёмки нет.

## Tested independent prototype

A standalone Python 3 standard-library-only script created an **original**, parametric voxel Orc sample as a glTF 2.0 binary GLB. It was built in an isolated execution environment, **not on the owner's Windows PC**.

- 20 named independent modules: pelvis, torso, head, eyes, brows, mouth, beard, hair; upper arm/forearm/hand on L and R; thigh/shin/foot on L and R.
- 211 multi-size box elements, 2532 triangles, 1688 vertices.
- Explicit gaps between rigid limb modules; independent geometry allows later bone parenting.
- Main head includes chin/jaw silhouette. No separate animated jaw.
- Original color-coded armor, clothing, face, hair and beard placeholders.
- GLB was parsed using trimesh 4.11.1: 20 geometry parts, 2532 faces, bounds in glTF coordinates (Y-up, meters) from x=-1.045 to 1.045, y=0 to 3.13, z=-0.406 to 0.530.

**No artistic approval, real mesh conversion, Blender round-trip, skeleton, skinning, animation, or Unity validation.** The prototype lacks required final art detail and can only serve as a technical feasibility example for separate voxel modules. It is not suitable for game release.

Source files and 71 KB GLB were generated as a session handoff archive (`factoria_orc_prototype.zip`). This archive **has not been committed to GitHub**; therefore the exact source and binary must be copied into the canonical local Factoria checkout for a Windows Blender validation, and then only distributable source/report files can be published. Do not represent the branch as containing this code.

## Next local tasks (ordered)

1. Synchronize handoff source into `D:\Projects\CubeGame Factory\scripts\voxel\`; do not overwrite existing files.
2. Run Python generator (`python scripts/voxel/orc_prototype.py --out outputs/prototypes --seed 17`) in existing isolated environment. Requires no AI models or GPU.
3. Import generated GLB into Blender 5.2.4; verify 20 editable object modules, correct meter scale/Y-up, materials and intact disconnected limbs. Capture 3 views and actual process RAM/VRAM.
4. For the **existing Orc redesign stream**, obtain a legitimately usable 3D working-copy mesh exported through an authorized pathway; at present user-provided references are PNG images, not editable geometry. Do not claim to have converted an existing Orc until this file is supplied and independently verified.
5. Improve one actual candidate's artistic quality to reference level **before** facial-expression experiments. Owner decides MODEL_APPROVED or MODEL_REWORK following [FACT-D17](../decisions/FACT-D17-character-visual-first-face-emotions.md).
6. PREP-02 and PREP-03 statuses are not upgraded based on this isolated test.

## Boundaries

Maintain memory budget for AI experiments <=15 GB RAM and <=2 GB *additional* VRAM. This geometry pilot itself does not use AI weights. Do not alter CubeGame Unity runtime, import original Alpha assets, or force-push.
