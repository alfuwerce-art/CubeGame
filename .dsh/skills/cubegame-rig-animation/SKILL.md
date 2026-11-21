---
name: cubegame-rig-animation
description: "Rig and animate one authorized character or creature asset, checking real deformation, contacts and engine import without reactivating old automated rig pipeline."
---
# Rig / animation of one approved asset

## Preconditions
- Owner has approved a specific model or authorized a strictly isolated demonstration. D95 still closes previous automated rig factory; no universal mannequin / multi-character fitting workflows.
- Read Art Direction, GDD, target action, existing animation/rig ownership and asset license. Identify actual installed Blender/Unity/Unreal versions; do not assume availability.

## Workflow
1. Inventory geometry, body segmentation (including possible floating voxel limbs), scale, pivots and desired action. Choose **minimum meaningful bones** appropriate to this model, not a universal skeleton by default.
2. Test rest pose, joint rotations, skinning weights or rigid-part parenting. For intentionally disconnected parts, use rigid transforms where appropriate rather than forced deformations.
3. Create one idle or walk clip (or the specific authorized action). Test root motion, foot placement/sliding, hand grip, pose changes, loop seams, limb intersection and angle range.
4. Export with actual engine-compatible settings; inspect source rig, names, rest pose, animation duration and import logs.
5. Verify **runtime movement in the game or isolated test scene**, not just an animation graph. Capture front, side, 3/4 motion and at least one clip recording.
6. If experimenting with Unreal, IK Rig / Control Rig / Retargeter only in [isolated Unreal spike](../../../docs/research/UNREAL_SCRIPTING_SPIKE.md), version check first.
7. Report defect list, fixes, tests, license and rollback. Agent cannot approve appearance by itself.

`RIGGED_ASSET + CLIP_PLAYING` ≠ production-ready animation. No batch pipeline unless separately authorized in future.
