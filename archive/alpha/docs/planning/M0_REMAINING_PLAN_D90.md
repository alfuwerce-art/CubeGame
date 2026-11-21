# M0 REMAINING PLAN — D90

**Status:** HISTORICAL / SUPERSEDED BY D92  
**Canonical branch:** `integration/m0-all`  
**Source:** D90. Активный план: [D92](../decisions/D92-m0-full-alpha-unity-port.md) / [M0 ALPHA FULL UNITY PORT](../stages/M0_ALPHA_FULL_UNITY_PORT_STAGE.md).

| Stage | Scope | Exit criterion |
|---|---|---|
| **M0-F1 WORLD FIX** | recovered spawn/NPC runtime, stale collider, cities/dungeons/chests, persistence/unload | world PlayMode green except explicitly accepted skips; population/content live in Pilot |
| **M0-F2 CHARACTER & COMBAT** | hero appearance/equipment, one loot visual owner, movement modes, ranged/projectiles/abilities/VFX | character/combat paths work end-to-end in runtime |
| **M0-F3 GAMEPLAY LOOP** | progression, items/crafting, NPC interaction/trade, quests, save/load | playable progression loop survives reload |
| **M0-F4 PRESENTATION** | WAV parser/playback, HUD/inventory/skills/map/journal, feedback | all gameplay systems usable without temporary debug UI |
| **M0-F5 FINAL** | Windows build, full scenario, regressions, performance/visual review | complete M0 scenario passes and is READY_FOR_REVIEW |

## M0-F1 details

P1 order:
1. `AlphaRecoveredPopulation` + kind selector + game-time lifecycle in runtime.
2. NPC schedule/patrol integration.
3. Fix `StreamingWorldView` dirty-collider priority/state.
4. Register city/dungeon/chest providers exactly once.
5. Stable IDs + unload/reload + persistence tests.
6. Full EditMode + PlayMode.

Do not mask unknown Alpha rules with silent fallbacks.

## M0-F2 details

- Runtime race/sex/appearance.
- Required equipment slots and correct weapon states.
- Remove duplicate ground-loot rendering; origin-safe positions and resource lifecycle.
- Remaining Alpha movement modes.
- Ranged attack separation from melee fallback.
- Projectile spawn/collision/hit/despawn.
- Proven abilities/VFX; unknowns remain explicit.

## M0-F3 details

- XP/levels/specializations/skills.
- Full equipment/item/wallet loop.
- Chests and crafting only where evidence supports behavior.
- NPC services, interaction and trade.
- Quest acquisition/completion/reward/persistence.
- Save/load entity and world state without duplication.

## M0-F4 details

- Correct RIFF chunk parsing for all 93 sounds.
- Runtime WAV loading/playback/event mapping.
- Replace temporary IMGUI with gameplay UI.
- Map, icons and feedback validation.

## M0-F5 details

Required scenario:
1. create world/character;
2. travel and stream zones;
3. fight melee and ranged;
4. loot/equip/progress;
5. visit city/trade;
6. enter dungeon/kill boss/open chest;
7. complete quest;
8. save/exit/load;
9. verify world/NPC/player state;
10. build Windows player and run smoke/regression.

Final status before producer review: `READY_FOR_REVIEW`, never self-`ACCEPTED`.
