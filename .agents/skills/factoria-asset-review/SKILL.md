---
name: factoria-asset-review
description: Verify a Factoria mannequin, rig, animation clip or exported character for CubeGame handoff.
---

- Start with the supplied asset package and the [production workspace](../../../docs/CHARACTER_PRODUCTION_WORKSPACE.md). Preserve hashes and the owner reference. Run DCC checks against a copy.
- Record rig topology, units, rest transforms, mesh weights, sockets and output hashes. Check a Blender round-trip before inspecting the FBX Avatar and a representative clip in an isolated Unity Editor project.
- Record real observed measurements and the remaining artistic decisions separately. Game integration and PlayMode are outside the current mannequin task.
