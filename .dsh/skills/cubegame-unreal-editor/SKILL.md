---
name: cubegame-unreal-editor
description: "Automate authorized Unreal Editor tasks through verified Python and MCP; no runtime Python."
---
# Unreal Editor Python/MCP
1. Read AGENTS, ACTIVE_STAGE and docs/experiments/UNREAL_FIRST_PLAYABLE.md. Confirm actual installed UE, Python plugin and MCP handshake. If absent return BLOCKED.
2. Only edit Unreal/CubeGameTrial/ from the assigned task branch. Do not modify Unity assets or global DSH settings.
3. Inspect actual Unreal API names/version; don't invent Editor functions. Script one deterministic scoped editor operation in Scripts/Editor; log planned asset paths and changes.
4. Prefer reusable idempotent scripts for repeated operations. Validate twice; retain scripts, asset IDs, logs, screenshots and diffs. Don't open MCP endpoints to external interfaces.
5. C++ owns game runtime and state. Python is editor-only; no gameplay claims from editor-only success.
