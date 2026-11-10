# M0 F1–F3 — historical documentation index (2026-11-10)

This file preserves the navigation and meaning of older snapshots consolidated out of `PROJECT_MEMORY.md` and `docs/ACTIVE_STAGE.md` in the documentation review. Original text is recoverable from Git history, including [PROJECT_MEMORY original](https://github.com/alfuwerce-art/CubeGame/blob/05336984ea48925a957a518ae1ee059be8299768/PROJECT_MEMORY.md) and [ACTIVE_STAGE original](https://github.com/alfuwerce-art/CubeGame/blob/05336984ea48925a957a518ae1ee059be8299768/docs/ACTIVE_STAGE.md).

## Accepted checkpoints
- **M0-F2 integration:** ACCEPTED on `4b9480836beeba859340938486f4200d16c1549e`; includes M0-F1 world, F2A character, F2B combat; EditMode 744 PASS / 2 SKIP; PlayMode 69 PASS. [Integration report](../reports/M0_F2_INTEGRATION_REPORT.md).
- **M0-F3 gameplay loop:** ACCEPTED on `3cd79b4366dda4ff0d2410be22ee11d084eefaf1`; EditMode 754 PASS / 2 SKIP, PlayMode 70 PASS / 0 FAIL. [F3 report](../reports/M0_F3_GAMEPLAY_LOOP_REPORT.md).

## Historically reviewed work (not independently upgraded by this archival index)
- [M0-F1 world fix](../reports/M0_F1_WORLD_FIX_REPORT.md): report header retains its historical READY_FOR_REVIEW; overall result was included in later accepted F2 integration.
- [M0-F2A character](../reports/M0_F2A_CHARACTER_RUNTIME_REPORT.md): historical READY_FOR_REVIEW; integrated under F2 acceptance.
- [M0-F2B combat](../reports/M0_F2B_COMBAT_RUNTIME_REPORT.md): historical READY_FOR_REVIEW; integrated under F2 acceptance.
- [D1/D2 spawn and patrol recovery](../reports/D1_D2_SPAWN_PATROL_REVIEW_FIX.md); [D3–D6 independent audit](../reports/CODEX_D3_D6_INDEPENDENT_AUDIT.md).
- [D90 five-stage M0 plan](../decisions/D90-m0-five-stage-finish-plan.md) supersedes D71–D89 agent scheduling, not the design decisions stored in those records.

## Known Alpha provenance constraints
See [M0_ALPHA_PARITY_BACKLOG](../planning/M0_ALPHA_PARITY_BACKLOG.md), including reconstructed dungeon generation, spawn and NPC schedule limitations, partial weapon behavior, missing/approximate crafting and quest rules. Do not interpret F2/F3 acceptance as strict 1:1 Alpha parity.

Historical technical constants, earlier failures, branch/SHA metadata and detailed evidence remain in their original reports and immutable Git versions; the current project summary intentionally does not duplicate them.
