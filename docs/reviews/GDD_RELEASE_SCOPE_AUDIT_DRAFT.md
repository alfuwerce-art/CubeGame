# GDD scope audit — main game vs later updates (DRAFT)

**Date:** 2026-11-07  
**Status:** REVIEW PROPOSAL ONLY — no producer acceptance for the changes below.  
**Sources:** GDD_DRAFT.md §§3–9, ROADMAP_DRAFT.md M0–M3, D72–D82, current ACTIVE_STAGE.

## Existing binding constraints
- D78: finish Alpha 2013 transfer to Unity and integrated playable experience before evolutionary features.
- D82 ACCEPTED: 9 biomes/41 micro-biomes, new additional creatures/fauna, seasons for an update after completing the main game; original Alpha content preserved.
- D80: Fildor as rare NPCs in the main world, player selection reserved for a possible later DLC. Overseas continent D60 planned for main game.
- D81: demographics/children/aging were **not automatically deferred** by D82.
- Multiplayer is later stage M3; exact first-launch requirement unresolved.
- S05A-2 REVIEW OPEN; S05B NOT_AUTHORIZED.

## Proposed scope tier A — main game after Alpha integration
- Stable procedural world/seed, persistence, stream regions, base Alpha biomes/fauna/enemies.
- Character/class/equipment, combat, inventory, progression, revised models and animations.
- Main graphics/lighting/water/foliage/UI/audio standards.
- D66 magic/combat progression and learnable abilities with a bounded implementation plan.
- Dungeon exploration, bosses, artifacts, D60 optional four-act campaign (content boundaries TBD).
- Overseas continent and rare Fildor NPCs, bounded content & QA targets TBD.
- Meaningful reactive quests, NPC/faction/world consequences at a verifiable scale.

## Proposed tier B — bounded pilot implementations in main game (not approved as reductions)
- D73 economy: first working resource→production→storage→delivery→market chain, gradual scaling only after tests. **Does not cancel** accepted rich-economy direction.
- Factions: membership, reputation, limited territorial conflict; complex diplomacy/sieges subject to separate decision.
- D72 social/neural NPCs: start from one NPC→small group→settlement only if grounded world state, save/load, fallback, latency, performance pass. Scale/voice later subject to explicit approval.
- D74 dynamic quests: start with real-world causal chain and autonomous consequences before high variety.
- Demography: propose defer full reproduction/aging/population generational simulation; static age variation as possible initial fallback. **Requires explicit producer decision (D81 currently not deferred).**

## Existing post-main-game tier C
- D79 catalog of 9/41 (new expanded geography and generation).
- Additional creatures, animals, monsters beyond original Alpha set.
- Seasons D81.
- Fildor player selection: potential DLC.
- Multiplayer: later M3; first release timing must be decided expressly.

## Key decisions still requiring producer approval
1. Do the limited tier-B versions satisfy the first main-game release, or must some be complete?
2. Defer or retain D81 full demography in base game?
3. Define objective release gate: a finishable playable game with tested Alpha+M1 features, bounded RPG depth, stable saves/performance, and no unfinished blocking features.
4. Bound D60 overseas continent and main story content; define NPC/LLM offline fallback & cost.

## Non-authorization
This audit does not modify accepted game design. It opens no Unity stage and does not change D82 or current technical boundaries.
