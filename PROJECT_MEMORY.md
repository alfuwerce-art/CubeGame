# CubeGame — PROJECT MEMORY

**Updated:** 2026-11-10 (documentation review copy)  
**Canonical branch:** `integration/m0-all` — not changed by this review  
**Current M0 governing plan:** [D90](docs/decisions/D90-m0-five-stage-finish-plan.md)  
**Current stage from canonical checkpoint:** [M0-F4 PRESENTATION](docs/ACTIVE_STAGE.md) — `ACTIVE`

## Current development state
- **M0-F2 accepted:** `4b9480836beeba859340938486f4200d16c1549e`. F1+F2 integrated; [full report](docs/reports/M0_F2_INTEGRATION_REPORT.md).
- **M0-F3 accepted:** `3cd79b4366dda4ff0d2410be22ee11d084eefaf1`. Gameplay loop of combat, loot, trade/services, simple crafting, missions and save/load tested; 754 EditMode PASS / 2 SKIP; 70 PlayMode PASS. [F3 report](docs/reports/M0_F3_GAMEPLAY_LOOP_REPORT.md).
- **M0-F4 active at last verified canonical checkpoint:** runtime audio/WAV parser, gameplay HUD, inventory/skills/services, map/journal/icons, integration tests. [F4 spec](docs/stages/M0_F4_PRESENTATION_STAGE.md).
- **M0-F5 future:** Windows build, continuous Alpha foundation loop, regression, producer review; does **not** equal automatic public release.
- Target M0 loop: `new world → character → travel → combat → loot → city → trade → dungeon → boss → quest → save/load → continue`.

## Product and design authority (review pending promotion)
- [Documentation index](docs/README.md); [GDD](docs/game-design/GDD_DRAFT.md); [proposed unified Roadmap](docs/planning/ROADMAP_CONSOLIDATION_PROPOSAL.md).
- [D91](docs/decisions/D91-document-consolidation-release-batches.md): producer approves family exemplars and production batches, with technical checks per Asset ID; complete solo public game before network; D60 overseas campaign/continent is a later milestone after initial public solo launch.
- [D92](docs/decisions/D92-fildor-first-solo-release.md): rare Fildor NPCs are required in the first solo public release; player-selectable race remains future DLC.
- [M1 Character / Creature Factory concept](docs/stages/M1_MODEL_ANIMATION_EVOLUTION_STAGE.md) and [S06 visual/weather concept](docs/stages/S06_RENDER_ENVIRONMENT_EVOLUTION_STAGE.md) are `APPROVED CONCEPT / FUTURE STAGE / NOT_AUTHORIZED`; actual seasons planned as postlaunch update (D81/D82).

## Unfinished / unknown Alpha systems
[M0_ALPHA_PARITY_BACKLOG](docs/planning/M0_ALPHA_PARITY_BACKLOG.md) is mandatory. Mark `PARTIAL`, `UNKNOWN`, `PLACEHOLDER_NOT_ALPHA`, or `CUBEGAME_CONTRACT` explicitly; don't lose unresolved behavior when upgrading systems. This remains an Alpha foundation, not a claim of complete original parity.

## Operational rules
- Read `PROJECT_MEMORY.md`, `docs/ACTIVE_STAGE.md`, `AGENTS.md`, current stage spec and applicable Dxx.
- GitHub source of truth; one Unity slot; no concurrent writes to same file; no force push without express approval.
- Original Alpha client stays read-only; do not publish original/converted Alpha assets; preserve accepted save formats.
- `ACCEPTED` only by producer. Stage design approval is not a development authorization.

## Historical reference
[Archived navigation and original memory snapshot](docs/history/M0_F1_F3_HISTORY_INDEX.md); [D90](docs/decisions/D90-m0-five-stage-finish-plan.md). Older S05 statuses and D71–D89 workflow plans are historical, not instructions for the current F4.
