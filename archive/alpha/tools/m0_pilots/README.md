# M0-00 isolated pilot lab

`tools/m0_pilots` contains the isolated, deterministic pilots required by the M0-00
preparation package. It is **not** production code and is **not** connected to the Unity
scene. Nothing under `Assets/`, `Packages/` or `ProjectSettings/` is modified.

## What is verified

| Pilot | Checks | What it proves |
|---|---:|---|
| `PILOT_WORLD` | 58 | CRT rand vector, region seed/type/variant/orientation formula, world→region→cell mapping, deterministic chunk hashes, world-delta round-trip and corruption rejection |
| `PILOT_MOVEMENT` | 102 | skill-description curves (55 table values), gravity/glide, stamina drain/regen, dash cost/timer, contact/action flags, explicit UNKNOWN speed scale and jump |
| `PILOT_COMBAT` | 81 | ability cooldowns/mana/stamina/channel/melee tables, atomic resource use, hit/block formulas, S03 `DamageResolver` integration, deterministic 5000-attack battle, rewards |
| `PILOT_NPC` | 82 | 31 ms AI clock, stun gate, threat decay/reset/rescan, potion/dodge rules, friendly regen, boss set, spawn star probabilities, stable entity id and state save/restore |
| `PILOT_ITEMS` | 85 | r3 item types/weapon subtypes, 13 storage slots vs 7 rendered, inventory/stack limits, consumable cooldowns, starter kits, S05 visual status, Core enum mismatch audit |
| `CONTRACTS_ECONOMY_QUESTS_SAVES` | 54 | sell price via S04, unknown buy margins, wallet, quest states/types/gate, S02 save round-trip and corruption/version handling, equipment-persistence gap |
| **Total** | **462** | `M0_PILOTS_RESULT=PASS` |

The lab compiles the real `Assets/CubeGame/Core/**` sources; it does not duplicate S03/S04
calculations. It uses a deterministic `FixedCombatRandom`/`SeededCombatRandom` from Core.

## Run

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\m0_pilots\run_pilots.ps1
```

The script compiles with the Roslyn compiler shipped with Unity 6.3 and runs the executable
under Unity's Mono runtime. It writes the log to `.unity-install\m0_pilots\` (gitignored).

## Evidence and honesty rules

- Alpha evidence levels: `DIRECT`, `PARTIAL`, `UNKNOWN`, `PLACEHOLDER_NOT_ALPHA`.
- `CUBEGAME_CONTRACT` marks decisions needed for integration (for example pilot chunk size,
  mode precedence, dash gating) and is never presented as an Alpha fact.
- Speed units (`OQ-23`), jump, hit-chance base (`OQ-01`), block fraction (`OQ-24`), death
  rules, off-screen simulation, trading margins and quest conditions stay explicitly
  unproven/blocked.
- `PASS` here is a technical result of the pilot, not `ACCEPTED` and not authorisation for
  M0-01+.
