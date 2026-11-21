---
name: cubegame-voxel-asset
description: "Produce or assess exactly one original voxel-pixel model asset from an approved visual brief using authorized Blender, Unity or scripted modeling tools."
---
# One voxel/pixel model — **not** a factory

## Stop conditions
D95 cancelled model factory / automated mass generation, old mannequin templates and Style Lock. This skill is **one-shot original asset craftsmanship**, not approval to revive ComfyUI/Tripo production flows. Require **one precise asset + reference + permitted tool**. If missing, prepare brief only.

## Steps
1. Read [Art Direction](../../../docs/art/ART_DIRECTION.md), GDD and requested asset brief. Verify the precise reference exists and usage rights; never publish local private assets automatically.
2. Identify actual target engine (Unity canonical; UE only separately approved experiment). Choose geometry representation / voxel scale / pivot / unit system *for this asset*, not global decisions.
3. Build one rough silhouette with deterministic geometry where possible; no automatic batch and no random workflow expansion. For Blender, if installed, use documented Python commands; otherwise explain environment blocker. Do not claim a third-party skill installed.
4. Inspect at least **front, side, 3/4** and at real in-game camera distance. If character also check back, face/hand/foot scale. Explicitly list deviations from references.
5. Export editable source (e.g. `.blend`) and a requested engine-friendly file after license/origin review; check transforms, normals, materials, no holes/clipping or unintended smoothing.
6. Import into authorized test scene, check render, bounds/collider/pivots, fps/memory where relevant; present snapshots/video.
7. Request owner visual verdict. Only after acceptance may a related rig/animation be attempted.

## Evidence
`ASSET_ID / SOURCE_REFERENCE / SOURCE_EDITABLE / EXPORT / VIEWS / UNITY_OR_UE_IMPORT / LICENSE / TESTS / STATUS`. `MODEL_READY_FOR_OWNER_REVIEW` is not `ART_APPROVED`.
