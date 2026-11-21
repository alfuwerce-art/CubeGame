---
name: cubegame-unreal-validation
description: "Validate Unreal C++ builds, editor automation, playable scenarios and Windows packaging with evidence."
---
# Unreal validation
1. Confirm real engine version, workspace, task scope, build tools and exact project paths.
2. Distinguish UHT/UBT compilation, automated functional tests, PIE, Standalone and packaged Win64 execution; they are not interchangeable.
3. Check scripted editor changes for idempotency; record asset paths and errors, not only process exit codes.
4. Play movement -> hit -> loot -> SaveGame -> reload; record video/screenshots. No static-only substitute.
5. Log FPS/VRAM/RAM from actual measurements, test counts and engine version, failures and manual interventions.
6. Return PASS/PARTIAL/BLOCKED and artifacts, unverified steps marked NOT_RUN. No owner acceptance by agent.
