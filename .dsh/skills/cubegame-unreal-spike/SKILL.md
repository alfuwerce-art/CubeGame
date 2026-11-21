---
name: cubegame-unreal-spike
description: "Plan and inspect only an explicitly authorized isolated Unreal Engine 5.8 editor scripting, C++ gameplay, Geometry Scripting or rigging experiment."
---
# Unreal isolated scripting spike — **not a migration**

## Guard
Always read [Unreal spike](../../../docs/research/UNREAL_SCRIPTING_SPIKE.md). No UE installation, C++ toolchain change, new project, API subscription or engine migration **until explicitly authorized**. Do not touch current Unity code, `D:\Projects\CubeGame` local checkout, private references or other projects.

## Sequence (after authorization)
1. Detect actual UE/editor, compiler, plugins, available disk/RAM/VRAM and C++ settings; record versions and working directory.
2. C++ Third Person or minimal test project; Python is **Editor-only**, not gameplay runtime. Blueprint can be appropriate, do not prohibit it by dogma.
3. Write one idempotent Editor Python task for asset scanning/creation and log JSON; run twice, compare counts and side effects.
4. Implement one small C++ runtime action, build and run. If using Geometry Script, one voxel-like prop only, test pivot and render.
5. For motion evaluate Control Rig/IK Retargeter on one legal test skeleton with clip; log actual deformation and video evidence.
6. Compare equivalent Unity workflows with the **same** inputs and criteria, including token usage when measurable, human interventions and elapsed time.
7. No overwriting canonical, no factory, no bulk assets. Submit research report for owner choice `KEEP_UNITY` or further study.

Outputs: complete commands, versions, logs, screenshots, edits, size/VRAM, `NOT_RUN/BLOCKED/FAIL/PASS`, decision pending.
