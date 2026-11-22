---
name: cubegame-unreal-gameplay
description: "Implement an authorized C++-first Unreal gameplay feature with compiler and playable evidence."
---
# Unreal C++ gameplay
1. Read AGENTS, ACTIVE_STAGE, task and docs/architecture/ADR-0002-unreal-cpp-first-proposal.md.
2. Verify .uproject, Source module, Build.cs, installed UE/UHT/UBT, base SHA and git status. Search existing classes before adding new.
3. Write minimal C++ gameplay logic and tests. Respect Unreal reflection rules: UCLASS/UPROPERTY/UFUNCTION, generated headers, memory ownership and module dependencies.
4. Blueprint is optional for visual configuration, bindings and AnimBP; never hide core rules in opaque sprawling event graphs. Editor Python is not runtime code.
5. Compile with real toolchain; inspect logs, run PIE/Standalone and actual input/combat/save tests. Win64 package when required.
6. Report paths, diff, commands, errors, test oracles, video, SHA and PARTIAL if verification is incomplete.
