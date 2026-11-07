# S05B-2 / D69 вЂ” РјР°С‚СЂРёС†Р° РїРµСЂРµРЅРѕСЃР° Alpha в†’ Unity (KEEP / DISABLE_IN_GAME / FIX / ADD_MISSING / UNKNOWN)

РЎСЂР°РІРЅРµРЅРёРµ **РЅР°С€РµРіРѕ** Unity-РїРµСЂРµРЅРѕСЃР° (D65 + РёСЃРїСЂР°РІР»РµРЅРёСЏ D69) СЃ [РєР°С‚Р°Р»РѕРіРѕРј РѕСЂРёРіРёРЅР°Р»Р°](S05B2_ALPHA_ORIGINAL_MODEL_ANIMATION_CATALOG.md). РЎРіРµРЅРµСЂРёСЂРѕРІР°РЅРѕ `d69_catalogs`; СЂРµР·СѓР»СЊС‚Р°С‚С‹ Unity вЂ” РёР· СЃРІРѕРґРѕРє РїР°РєРµС‚РѕРІ D59вЂ“D65 Рё РїСЂРѕРІРµСЂРєРё D69 (`.scratch/d69/verify`).

## РЎРІРѕРґРєР°

Р’РµСЂРґРёРєС‚С‹: `ADD_MISSING` 212, `DISABLE_IN_GAME` 234, `FIX` 1, `KEEP` 990, `UNKNOWN_NEEDS_RESEARCH` 1.

РС‚РѕРі D69: `BLOCKED` 26, `DEFERRED_TO_M0` 131, `DISABLED_IN_GAME` 234, `FIXED` 6, `KEPT` 311, `TRANSFERRED` 730.

## РќРµ KEEP (РІСЃС‘, С‡С‚Рѕ С‚СЂРµР±СѓРµС‚ РґРµР№СЃС‚РІРёСЏ)

| kind | РёРјСЏ | С‡С‚Рѕ | РѕСЂРёРіРёРЅР°Р» | Unity | РІРµСЂРґРёРєС‚ | РёС‚РѕРі D69 | СЃРµСЂСЊС‘Р·РЅРѕСЃС‚СЊ | РїСЂРёС‡РёРЅР°/РёСЃРїСЂР°РІР»РµРЅРёРµ | РёСЃРїРѕР»РЅРёС‚РµР»СЊ |
|---|---|---|---|---|---|---|---|---|---|
| 0x00 | ElfMale | action 37 | class action вЂ” AI launch (ALPHA_CODE) | D69: baked with the merged profile, played by the Unity controller | ADD_MISSING | TRANSFERRED | MEDIUM | launch proven by the executed AI region in other NPC class/spec states (D64); was missing from the D60 single-state profile; game trigger DEFERRED_TO_M0 | Claude (D69) |
| 0x00 | ElfMale | action 40 | class action вЂ” AI launch (ALPHA_CODE) | D69: baked with the merged profile, played by the Unity controller | ADD_MISSING | TRANSFERRED | MEDIUM | launch proven by the executed AI region in other NPC class/spec states (D64); was missing from the D60 single-state profile; game trigger DEFERRED_TO_M0 | Claude (D69) |
| 0x00 | ElfMale | action 42 | class action вЂ” AI launch (ALPHA_CODE) | D69: baked with the merged profile, played by the Unity controller | ADD_MISSING | TRANSFERRED | MEDIUM | launch proven by the executed AI region in other NPC class/spec states (D64); was missing from the D60 single-state profile; game trigger DEFERRED_TO_M0 | Claude (D69) |
| 0x00 | ElfMale | action 43 | class action вЂ” AI launch (ALPHA_CODE) | D69: baked with the merged profile, played by the Unity controller | ADD_MISSING | TRANSFERRED | MEDIUM | launch proven by the executed AI region in other NPC class/spec states (D64); was missing from the D60 single-state profile; game trigger DEFERRED_TO_M0 | Claude (D69) |
| 0x00 | ElfMale | weapon-only actions | [1, 5, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57, 59, 60, 61] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x01 | ElfFemale | action 37 | class action вЂ” AI launch (ALPHA_CODE) | D69: baked with the merged profile, played by the Unity controller | ADD_MISSING | TRANSFERRED | MEDIUM | launch proven by the executed AI region in other NPC class/spec states (D64); was missing from the D60 single-state profile; game trigger DEFERRED_TO_M0 | Claude (D69) |
| 0x01 | ElfFemale | action 40 | class action вЂ” AI launch (ALPHA_CODE) | D69: baked with the merged profile, played by the Unity controller | ADD_MISSING | TRANSFERRED | MEDIUM | launch proven by the executed AI region in other NPC class/spec states (D64); was missing from the D60 single-state profile; game trigger DEFERRED_TO_M0 | Claude (D69) |
| 0x01 | ElfFemale | action 42 | class action вЂ” AI launch (ALPHA_CODE) | D69: baked with the merged profile, played by the Unity controller | ADD_MISSING | TRANSFERRED | MEDIUM | launch proven by the executed AI region in other NPC class/spec states (D64); was missing from the D60 single-state profile; game trigger DEFERRED_TO_M0 | Claude (D69) |
| 0x01 | ElfFemale | action 43 | class action вЂ” AI launch (ALPHA_CODE) | D69: baked with the merged profile, played by the Unity controller | ADD_MISSING | TRANSFERRED | MEDIUM | launch proven by the executed AI region in other NPC class/spec states (D64); was missing from the D60 single-state profile; game trigger DEFERRED_TO_M0 | Claude (D69) |
| 0x01 | ElfFemale | weapon-only actions | [1, 5, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57, 59, 60, 61] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x02 | HumanMale | action 3 | melee hit вЂ” AI launch (ALPHA_CODE) | not in the Unity library (0x02 carries the player-input profile) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC HumanMale (city NPCs are 0x02) needs a creature-AI profile next to the player profile of the same kind: a controller decision for M0-06 (poses: same family as the other humanoids) | M0-06 (DeepSeek) |
| 0x02 | HumanMale | action 4 | melee hit 2 вЂ” AI launch (ALPHA_CODE) | not in the Unity library (0x02 carries the player-input profile) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC HumanMale (city NPCs are 0x02) needs a creature-AI profile next to the player profile of the same kind: a controller decision for M0-06 (poses: same family as the other humanoids) | M0-06 (DeepSeek) |
| 0x02 | HumanMale | action 6 | special/heavy вЂ” AI launch (ALPHA_CODE) | not in the Unity library (0x02 carries the player-input profile) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC HumanMale (city NPCs are 0x02) needs a creature-AI profile next to the player profile of the same kind: a controller decision for M0-06 (poses: same family as the other humanoids) | M0-06 (DeepSeek) |
| 0x02 | HumanMale | action 37 | class action вЂ” AI launch (ALPHA_CODE) | not in the Unity library (0x02 carries the player-input profile) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC HumanMale (city NPCs are 0x02) needs a creature-AI profile next to the player profile of the same kind: a controller decision for M0-06 (poses: same family as the other humanoids) | M0-06 (DeepSeek) |
| 0x02 | HumanMale | action 40 | class action вЂ” AI launch (ALPHA_CODE) | not in the Unity library (0x02 carries the player-input profile) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC HumanMale (city NPCs are 0x02) needs a creature-AI profile next to the player profile of the same kind: a controller decision for M0-06 (poses: same family as the other humanoids) | M0-06 (DeepSeek) |
| 0x02 | HumanMale | action 42 | class action вЂ” AI launch (ALPHA_CODE) | not in the Unity library (0x02 carries the player-input profile) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC HumanMale (city NPCs are 0x02) needs a creature-AI profile next to the player profile of the same kind: a controller decision for M0-06 (poses: same family as the other humanoids) | M0-06 (DeepSeek) |
| 0x02 | HumanMale | action 43 | class action вЂ” AI launch (ALPHA_CODE) | not in the Unity library (0x02 carries the player-input profile) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC HumanMale (city NPCs are 0x02) needs a creature-AI profile next to the player profile of the same kind: a controller decision for M0-06 (poses: same family as the other humanoids) | M0-06 (DeepSeek) |
| 0x02 | HumanMale | action 54 | attack (humanoid default) вЂ” AI launch (ALPHA_CODE) | not in the Unity library (0x02 carries the player-input profile) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC HumanMale (city NPCs are 0x02) needs a creature-AI profile next to the player profile of the same kind: a controller decision for M0-06 (poses: same family as the other humanoids) | M0-06 (DeepSeek) |
| 0x02 | HumanMale | weapon-only actions | [1, 5, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57, 59, 60, 61] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x03 | HumanFemale | action 37 | class action вЂ” AI launch (ALPHA_CODE) | D69: baked with the merged profile, played by the Unity controller | ADD_MISSING | TRANSFERRED | MEDIUM | launch proven by the executed AI region in other NPC class/spec states (D64); was missing from the D60 single-state profile; game trigger DEFERRED_TO_M0 | Claude (D69) |
| 0x03 | HumanFemale | action 40 | class action вЂ” AI launch (ALPHA_CODE) | D69: baked with the merged profile, played by the Unity controller | ADD_MISSING | TRANSFERRED | MEDIUM | launch proven by the executed AI region in other NPC class/spec states (D64); was missing from the D60 single-state profile; game trigger DEFERRED_TO_M0 | Claude (D69) |
| 0x03 | HumanFemale | action 42 | class action вЂ” AI launch (ALPHA_CODE) | D69: baked with the merged profile, played by the Unity controller | ADD_MISSING | TRANSFERRED | MEDIUM | launch proven by the executed AI region in other NPC class/spec states (D64); was missing from the D60 single-state profile; game trigger DEFERRED_TO_M0 | Claude (D69) |
| 0x03 | HumanFemale | action 43 | class action вЂ” AI launch (ALPHA_CODE) | D69: baked with the merged profile, played by the Unity controller | ADD_MISSING | TRANSFERRED | MEDIUM | launch proven by the executed AI region in other NPC class/spec states (D64); was missing from the D60 single-state profile; game trigger DEFERRED_TO_M0 | Claude (D69) |
| 0x03 | HumanFemale | weapon-only actions | [1, 5, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57, 59, 60, 61] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x04 | GoblinMale | weapon-only actions | [1, 5, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57, 59, 60, 61] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x05 | GoblinFemale | weapon-only actions | [1, 5, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57, 59, 60, 61] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x06 | Bullterrier | weapon-only actions | [1, 5, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57, 59, 60, 61] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x07 | LizardmanMale | action 37 | class action вЂ” AI launch (ALPHA_CODE) | D69: baked with the merged profile, played by the Unity controller | ADD_MISSING | TRANSFERRED | MEDIUM | launch proven by the executed AI region in other NPC class/spec states (D64); was missing from the D60 single-state profile; game trigger DEFERRED_TO_M0 | Claude (D69) |
| 0x07 | LizardmanMale | action 40 | class action вЂ” AI launch (ALPHA_CODE) | D69: baked with the merged profile, played by the Unity controller | ADD_MISSING | TRANSFERRED | MEDIUM | launch proven by the executed AI region in other NPC class/spec states (D64); was missing from the D60 single-state profile; game trigger DEFERRED_TO_M0 | Claude (D69) |
| 0x07 | LizardmanMale | action 42 | class action вЂ” AI launch (ALPHA_CODE) | D69: baked with the merged profile, played by the Unity controller | ADD_MISSING | TRANSFERRED | MEDIUM | launch proven by the executed AI region in other NPC class/spec states (D64); was missing from the D60 single-state profile; game trigger DEFERRED_TO_M0 | Claude (D69) |
| 0x07 | LizardmanMale | action 43 | class action вЂ” AI launch (ALPHA_CODE) | D69: baked with the merged profile, played by the Unity controller | ADD_MISSING | TRANSFERRED | MEDIUM | launch proven by the executed AI region in other NPC class/spec states (D64); was missing from the D60 single-state profile; game trigger DEFERRED_TO_M0 | Claude (D69) |
| 0x07 | LizardmanMale | weapon-only actions | [1, 5, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57, 59, 60, 61] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x08 | LizardmanFemale | action 37 | class action вЂ” AI launch (ALPHA_CODE) | D69: baked with the merged profile, played by the Unity controller | ADD_MISSING | TRANSFERRED | MEDIUM | launch proven by the executed AI region in other NPC class/spec states (D64); was missing from the D60 single-state profile; game trigger DEFERRED_TO_M0 | Claude (D69) |
| 0x08 | LizardmanFemale | action 40 | class action вЂ” AI launch (ALPHA_CODE) | D69: baked with the merged profile, played by the Unity controller | ADD_MISSING | TRANSFERRED | MEDIUM | launch proven by the executed AI region in other NPC class/spec states (D64); was missing from the D60 single-state profile; game trigger DEFERRED_TO_M0 | Claude (D69) |
| 0x08 | LizardmanFemale | action 42 | class action вЂ” AI launch (ALPHA_CODE) | D69: baked with the merged profile, played by the Unity controller | ADD_MISSING | TRANSFERRED | MEDIUM | launch proven by the executed AI region in other NPC class/spec states (D64); was missing from the D60 single-state profile; game trigger DEFERRED_TO_M0 | Claude (D69) |
| 0x08 | LizardmanFemale | action 43 | class action вЂ” AI launch (ALPHA_CODE) | D69: baked with the merged profile, played by the Unity controller | ADD_MISSING | TRANSFERRED | MEDIUM | launch proven by the executed AI region in other NPC class/spec states (D64); was missing from the D60 single-state profile; game trigger DEFERRED_TO_M0 | Claude (D69) |
| 0x08 | LizardmanFemale | weapon-only actions | [1, 5, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57, 59, 60, 61] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x09 | DwarfMale | weapon-only actions | [1, 5, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57, 59, 60, 61] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x0a | DwarfFemale | weapon-only actions | [1, 5, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57, 59, 60, 61] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x0b | OrcMale | weapon-only actions | [1, 5, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57, 59, 60, 61] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x0c | OrcFemale | weapon-only actions | [1, 5, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57, 59, 60, 61] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x0d | FrogmanMale | action 37 | class action вЂ” AI launch (ALPHA_CODE) | D69: baked with the merged profile, played by the Unity controller | ADD_MISSING | TRANSFERRED | MEDIUM | launch proven by the executed AI region in other NPC class/spec states (D64); was missing from the D60 single-state profile; game trigger DEFERRED_TO_M0 | Claude (D69) |
| 0x0d | FrogmanMale | action 40 | class action вЂ” AI launch (ALPHA_CODE) | D69: baked with the merged profile, played by the Unity controller | ADD_MISSING | TRANSFERRED | MEDIUM | launch proven by the executed AI region in other NPC class/spec states (D64); was missing from the D60 single-state profile; game trigger DEFERRED_TO_M0 | Claude (D69) |
| 0x0d | FrogmanMale | action 42 | class action вЂ” AI launch (ALPHA_CODE) | D69: baked with the merged profile, played by the Unity controller | ADD_MISSING | TRANSFERRED | MEDIUM | launch proven by the executed AI region in other NPC class/spec states (D64); was missing from the D60 single-state profile; game trigger DEFERRED_TO_M0 | Claude (D69) |
| 0x0d | FrogmanMale | action 43 | class action вЂ” AI launch (ALPHA_CODE) | D69: baked with the merged profile, played by the Unity controller | ADD_MISSING | TRANSFERRED | MEDIUM | launch proven by the executed AI region in other NPC class/spec states (D64); was missing from the D60 single-state profile; game trigger DEFERRED_TO_M0 | Claude (D69) |
| 0x0d | FrogmanMale | weapon-only actions | [1, 5, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57, 59, 60, 61] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x0e | FrogmanFemale | action 37 | class action вЂ” AI launch (ALPHA_CODE) | D69: baked with the merged profile, played by the Unity controller | ADD_MISSING | TRANSFERRED | MEDIUM | launch proven by the executed AI region in other NPC class/spec states (D64); was missing from the D60 single-state profile; game trigger DEFERRED_TO_M0 | Claude (D69) |
| 0x0e | FrogmanFemale | action 40 | class action вЂ” AI launch (ALPHA_CODE) | D69: baked with the merged profile, played by the Unity controller | ADD_MISSING | TRANSFERRED | MEDIUM | launch proven by the executed AI region in other NPC class/spec states (D64); was missing from the D60 single-state profile; game trigger DEFERRED_TO_M0 | Claude (D69) |
| 0x0e | FrogmanFemale | action 42 | class action вЂ” AI launch (ALPHA_CODE) | D69: baked with the merged profile, played by the Unity controller | ADD_MISSING | TRANSFERRED | MEDIUM | launch proven by the executed AI region in other NPC class/spec states (D64); was missing from the D60 single-state profile; game trigger DEFERRED_TO_M0 | Claude (D69) |
| 0x0e | FrogmanFemale | action 43 | class action вЂ” AI launch (ALPHA_CODE) | D69: baked with the merged profile, played by the Unity controller | ADD_MISSING | TRANSFERRED | MEDIUM | launch proven by the executed AI region in other NPC class/spec states (D64); was missing from the D60 single-state profile; game trigger DEFERRED_TO_M0 | Claude (D69) |
| 0x0e | FrogmanFemale | weapon-only actions | [1, 5, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57, 59, 60, 61] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x0f | UndeadMale | weapon-only actions | [1, 5, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57, 59, 60, 61] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x10 | UndeadFemale | weapon-only actions | [1, 5, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57, 59, 60, 61] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x11 | Skeleton | weapon-only actions | [1, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x12 | OldMan | weapon-only actions | [1, 5, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57, 59, 60, 61] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x13 | Collie | weapon-only actions | [22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x14 | ShepherdDog | weapon-only actions | [22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x15 | SkullBull | weapon-only actions | [22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x16 | Alpaca | weapon-only actions | [22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x17 | BrownAlpaca | weapon-only actions | [22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x18 | Egg | walk | drawModel formula exists (ALPHA_CODE); game use: PRODUCER_RULE_D67 | D65: parametric walk active in the controller | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | STATIC_OBJECT controller mode: walk request refused, pose frozen; formula kept as research data | Claude (D69) |
| 0x18 | Egg | action 3 | melee hit вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x18 | Egg | action 4 | melee hit 2 вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x18 | Egg | action 6 | special/heavy вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x18 | Egg | action 37 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x18 | Egg | action 40 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x18 | Egg | action 42 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x18 | Egg | action 43 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x18 | Egg | action 54 | attack (humanoid default) вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x19 | Turtle | action 72 | turtle attack вЂ” AI launch (ALPHA_CODE); needs a dynamic part | BLOCKED ACTION_PART_SET_CHANGED | ADD_MISSING | BLOCKED | MEDIUM | thrown/held object model is not in the kind's rig (R-8) | next S05 task |
| 0x19 | Turtle | weapon-only actions | [22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x1a | Terrier | weapon-only actions | [22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x1b | ScottishTerrier | weapon-only actions | [22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x1c | Wolf | weapon-only actions | [22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x1d | Panther | weapon-only actions | [22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x1e | Cat | action 37 | class action вЂ” AI launch (ALPHA_CODE) | D69: baked with the merged profile, played by the Unity controller | ADD_MISSING | TRANSFERRED | MEDIUM | launch proven by the executed AI region in other NPC class/spec states (D64); was missing from the D60 single-state profile; game trigger DEFERRED_TO_M0 | Claude (D69) |
| 0x1e | Cat | action 40 | class action вЂ” AI launch (ALPHA_CODE) | D69: baked with the merged profile, played by the Unity controller | ADD_MISSING | TRANSFERRED | MEDIUM | launch proven by the executed AI region in other NPC class/spec states (D64); was missing from the D60 single-state profile; game trigger DEFERRED_TO_M0 | Claude (D69) |
| 0x1e | Cat | action 42 | class action вЂ” AI launch (ALPHA_CODE) | D69: baked with the merged profile, played by the Unity controller | ADD_MISSING | TRANSFERRED | MEDIUM | launch proven by the executed AI region in other NPC class/spec states (D64); was missing from the D60 single-state profile; game trigger DEFERRED_TO_M0 | Claude (D69) |
| 0x1e | Cat | action 43 | class action вЂ” AI launch (ALPHA_CODE) | D69: baked with the merged profile, played by the Unity controller | ADD_MISSING | TRANSFERRED | MEDIUM | launch proven by the executed AI region in other NPC class/spec states (D64); was missing from the D60 single-state profile; game trigger DEFERRED_TO_M0 | Claude (D69) |
| 0x1e | Cat | weapon-only actions | [22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x1f | BrownCat | action 37 | class action вЂ” AI launch (ALPHA_CODE) | D69: baked with the merged profile, played by the Unity controller | ADD_MISSING | TRANSFERRED | MEDIUM | launch proven by the executed AI region in other NPC class/spec states (D64); was missing from the D60 single-state profile; game trigger DEFERRED_TO_M0 | Claude (D69) |
| 0x1f | BrownCat | action 40 | class action вЂ” AI launch (ALPHA_CODE) | D69: baked with the merged profile, played by the Unity controller | ADD_MISSING | TRANSFERRED | MEDIUM | launch proven by the executed AI region in other NPC class/spec states (D64); was missing from the D60 single-state profile; game trigger DEFERRED_TO_M0 | Claude (D69) |
| 0x1f | BrownCat | action 42 | class action вЂ” AI launch (ALPHA_CODE) | D69: baked with the merged profile, played by the Unity controller | ADD_MISSING | TRANSFERRED | MEDIUM | launch proven by the executed AI region in other NPC class/spec states (D64); was missing from the D60 single-state profile; game trigger DEFERRED_TO_M0 | Claude (D69) |
| 0x1f | BrownCat | action 43 | class action вЂ” AI launch (ALPHA_CODE) | D69: baked with the merged profile, played by the Unity controller | ADD_MISSING | TRANSFERRED | MEDIUM | launch proven by the executed AI region in other NPC class/spec states (D64); was missing from the D60 single-state profile; game trigger DEFERRED_TO_M0 | Claude (D69) |
| 0x1f | BrownCat | weapon-only actions | [22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x20 | WhiteCat | action 37 | class action вЂ” AI launch (ALPHA_CODE) | D69: baked with the merged profile, played by the Unity controller | ADD_MISSING | TRANSFERRED | MEDIUM | launch proven by the executed AI region in other NPC class/spec states (D64); was missing from the D60 single-state profile; game trigger DEFERRED_TO_M0 | Claude (D69) |
| 0x20 | WhiteCat | action 40 | class action вЂ” AI launch (ALPHA_CODE) | D69: baked with the merged profile, played by the Unity controller | ADD_MISSING | TRANSFERRED | MEDIUM | launch proven by the executed AI region in other NPC class/spec states (D64); was missing from the D60 single-state profile; game trigger DEFERRED_TO_M0 | Claude (D69) |
| 0x20 | WhiteCat | action 42 | class action вЂ” AI launch (ALPHA_CODE) | D69: baked with the merged profile, played by the Unity controller | ADD_MISSING | TRANSFERRED | MEDIUM | launch proven by the executed AI region in other NPC class/spec states (D64); was missing from the D60 single-state profile; game trigger DEFERRED_TO_M0 | Claude (D69) |
| 0x20 | WhiteCat | action 43 | class action вЂ” AI launch (ALPHA_CODE) | D69: baked with the merged profile, played by the Unity controller | ADD_MISSING | TRANSFERRED | MEDIUM | launch proven by the executed AI region in other NPC class/spec states (D64); was missing from the D60 single-state profile; game trigger DEFERRED_TO_M0 | Claude (D69) |
| 0x20 | WhiteCat | weapon-only actions | [22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x21 | Pig | weapon-only actions | [22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x22 | Sheep | weapon-only actions | [22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x23 | Bunny | weapon-only actions | [22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x24 | Porcupine | weapon-only actions | [22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x25 | GreenSlime | weapon-only actions | [1, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x26 | PinkSlime | weapon-only actions | [1, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x27 | YellowSlime | weapon-only actions | [1, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x28 | BlueSlime | weapon-only actions | [1, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x29 | Frightener | action 37 | class action вЂ” AI launch (ALPHA_CODE); pose static | BLOCKED ACTION_STATIC | ADD_MISSING | BLOCKED | MEDIUM | the visible part is an effect (separate renderer spawnEffectMesh), R-3; no pose to transfer | next S05/M0 effects task |
| 0x29 | Frightener | action 40 | class action вЂ” AI launch (ALPHA_CODE); pose static | BLOCKED ACTION_STATIC | ADD_MISSING | BLOCKED | MEDIUM | the visible part is an effect (separate renderer spawnEffectMesh), R-3; no pose to transfer | next S05/M0 effects task |
| 0x29 | Frightener | action 42 | class action вЂ” AI launch (ALPHA_CODE); pose static | BLOCKED ACTION_STATIC | ADD_MISSING | BLOCKED | MEDIUM | the visible part is an effect (separate renderer spawnEffectMesh), R-3; no pose to transfer | next S05/M0 effects task |
| 0x29 | Frightener | action 43 | class action вЂ” AI launch (ALPHA_CODE); pose static | BLOCKED ACTION_STATIC | ADD_MISSING | BLOCKED | MEDIUM | the visible part is an effect (separate renderer spawnEffectMesh), R-3; no pose to transfer | next S05/M0 effects task |
| 0x29 | Frightener | weapon-only actions | [1, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x2a | SandHorror | weapon-only actions | [1, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x2b | Wizard | weapon-only actions | [1, 5, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57, 59, 60, 61] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x2c | Bandit | weapon-only actions | [1, 5, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57, 59, 60, 61] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x2d | Witch | weapon-only actions | [1, 5, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57, 59, 60, 61] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x2e | Ogre | weapon-only actions | [1, 5, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57, 59, 60, 61] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x2f | Rockling | action 108 | throw (golem/rockling) вЂ” AI launch (ALPHA_CODE); needs a dynamic part | BLOCKED ACTION_PART_SET_CHANGED | ADD_MISSING | BLOCKED | MEDIUM | thrown/held object model is not in the kind's rig (R-8) | next S05 task |
| 0x30 | Gnoll | weapon-only actions | [1, 5, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57, 59, 60, 61] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x31 | PolarGnoll | weapon-only actions | [1, 5, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57, 59, 60, 61] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x32 | Monkey | weapon-only actions | [1, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x33 | Gnobold | weapon-only actions | [1, 5, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57, 59, 60, 61] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x34 | Insectoid | weapon-only actions | [1, 5, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57, 59, 60, 61] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x35 | Hornet | action 51 | breath/flight state (dragon/winged) вЂ” AI launch (ALPHA_CODE); pose static | BLOCKED ACTION_STATIC | ADD_MISSING | BLOCKED | MEDIUM | the visible part is an effect (separate renderer spawnEffectMesh), R-3; no pose to transfer | next S05/M0 effects task |
| 0x35 | Hornet | weapon-only actions | [23, 30, 31, 32, 33, 38, 44, 45, 46] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x36 | InsectGuard | action 51 | breath/flight state (dragon/winged) вЂ” AI launch (ALPHA_CODE); pose static | BLOCKED ACTION_STATIC | ADD_MISSING | BLOCKED | MEDIUM | the visible part is an effect (separate renderer spawnEffectMesh), R-3; no pose to transfer | next S05/M0 effects task |
| 0x36 | InsectGuard | weapon-only actions | [23, 30, 31, 32, 33, 38, 44, 45, 46] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x37 | Crow | action 51 | breath/flight state (dragon/winged) вЂ” AI launch (ALPHA_CODE); pose static | BLOCKED ACTION_STATIC | ADD_MISSING | BLOCKED | MEDIUM | the visible part is an effect (separate renderer spawnEffectMesh), R-3; no pose to transfer | next S05/M0 effects task |
| 0x37 | Crow | weapon-only actions | [23, 30, 31, 32, 33, 38, 44, 45, 46] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x38 | Chicken | action 51 | breath/flight state (dragon/winged) вЂ” AI launch (ALPHA_CODE); pose static | BLOCKED ACTION_STATIC | ADD_MISSING | BLOCKED | MEDIUM | the visible part is an effect (separate renderer spawnEffectMesh), R-3; no pose to transfer | next S05/M0 effects task |
| 0x38 | Chicken | weapon-only actions | [23, 30, 31, 32, 33, 38, 44, 45, 46] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x39 | Seagull | action 51 | breath/flight state (dragon/winged) вЂ” AI launch (ALPHA_CODE); pose static | BLOCKED ACTION_STATIC | ADD_MISSING | BLOCKED | MEDIUM | the visible part is an effect (separate renderer spawnEffectMesh), R-3; no pose to transfer | next S05/M0 effects task |
| 0x39 | Seagull | weapon-only actions | [23, 30, 31, 32, 33, 38, 44, 45, 46] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x3a | Parrot | action 51 | breath/flight state (dragon/winged) вЂ” AI launch (ALPHA_CODE); pose static | BLOCKED ACTION_STATIC | ADD_MISSING | BLOCKED | MEDIUM | the visible part is an effect (separate renderer spawnEffectMesh), R-3; no pose to transfer | next S05/M0 effects task |
| 0x3a | Parrot | weapon-only actions | [23, 30, 31, 32, 33, 38, 44, 45, 46] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x3b | Bat | action 51 | breath/flight state (dragon/winged) вЂ” AI launch (ALPHA_CODE); pose static | BLOCKED ACTION_STATIC | ADD_MISSING | BLOCKED | MEDIUM | the visible part is an effect (separate renderer spawnEffectMesh), R-3; no pose to transfer | next S05/M0 effects task |
| 0x3b | Bat | weapon-only actions | [23, 30, 31, 32, 33, 38, 44, 45, 46] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x3c | Fly | action 51 | breath/flight state (dragon/winged) вЂ” AI launch (ALPHA_CODE); pose static | BLOCKED ACTION_STATIC | ADD_MISSING | BLOCKED | MEDIUM | the visible part is an effect (separate renderer spawnEffectMesh), R-3; no pose to transfer | next S05/M0 effects task |
| 0x3c | Fly | weapon-only actions | [23, 30, 31, 32, 33, 38, 44, 45, 46] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x3d | Midge | action 51 | breath/flight state (dragon/winged) вЂ” AI launch (ALPHA_CODE); pose static | BLOCKED ACTION_STATIC | ADD_MISSING | BLOCKED | MEDIUM | the visible part is an effect (separate renderer spawnEffectMesh), R-3; no pose to transfer | next S05/M0 effects task |
| 0x3d | Midge | weapon-only actions | [23, 30, 31, 32, 33, 38, 44, 45, 46] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x3e | Mosquito | action 51 | breath/flight state (dragon/winged) вЂ” AI launch (ALPHA_CODE); pose static | BLOCKED ACTION_STATIC | ADD_MISSING | BLOCKED | MEDIUM | the visible part is an effect (separate renderer spawnEffectMesh), R-3; no pose to transfer | next S05/M0 effects task |
| 0x3e | Mosquito | weapon-only actions | [23, 30, 31, 32, 33, 38, 44, 45, 46] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x3f | PlainRunner | action 37 | class action вЂ” AI launch (ALPHA_CODE) | D69: baked with the merged profile, played by the Unity controller | ADD_MISSING | TRANSFERRED | MEDIUM | launch proven by the executed AI region in other NPC class/spec states (D64); was missing from the D60 single-state profile; game trigger DEFERRED_TO_M0 | Claude (D69) |
| 0x3f | PlainRunner | action 40 | class action вЂ” AI launch (ALPHA_CODE) | D69: baked with the merged profile, played by the Unity controller | ADD_MISSING | TRANSFERRED | MEDIUM | launch proven by the executed AI region in other NPC class/spec states (D64); was missing from the D60 single-state profile; game trigger DEFERRED_TO_M0 | Claude (D69) |
| 0x3f | PlainRunner | action 42 | class action вЂ” AI launch (ALPHA_CODE) | D69: baked with the merged profile, played by the Unity controller | ADD_MISSING | TRANSFERRED | MEDIUM | launch proven by the executed AI region in other NPC class/spec states (D64); was missing from the D60 single-state profile; game trigger DEFERRED_TO_M0 | Claude (D69) |
| 0x3f | PlainRunner | action 43 | class action вЂ” AI launch (ALPHA_CODE) | D69: baked with the merged profile, played by the Unity controller | ADD_MISSING | TRANSFERRED | MEDIUM | launch proven by the executed AI region in other NPC class/spec states (D64); was missing from the D60 single-state profile; game trigger DEFERRED_TO_M0 | Claude (D69) |
| 0x3f | PlainRunner | weapon-only actions | [1, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x40 | LeafRunner | action 37 | class action вЂ” AI launch (ALPHA_CODE) | D69: baked with the merged profile, played by the Unity controller | ADD_MISSING | TRANSFERRED | MEDIUM | launch proven by the executed AI region in other NPC class/spec states (D64); was missing from the D60 single-state profile; game trigger DEFERRED_TO_M0 | Claude (D69) |
| 0x40 | LeafRunner | action 40 | class action вЂ” AI launch (ALPHA_CODE) | D69: baked with the merged profile, played by the Unity controller | ADD_MISSING | TRANSFERRED | MEDIUM | launch proven by the executed AI region in other NPC class/spec states (D64); was missing from the D60 single-state profile; game trigger DEFERRED_TO_M0 | Claude (D69) |
| 0x40 | LeafRunner | action 42 | class action вЂ” AI launch (ALPHA_CODE) | D69: baked with the merged profile, played by the Unity controller | ADD_MISSING | TRANSFERRED | MEDIUM | launch proven by the executed AI region in other NPC class/spec states (D64); was missing from the D60 single-state profile; game trigger DEFERRED_TO_M0 | Claude (D69) |
| 0x40 | LeafRunner | action 43 | class action вЂ” AI launch (ALPHA_CODE) | D69: baked with the merged profile, played by the Unity controller | ADD_MISSING | TRANSFERRED | MEDIUM | launch proven by the executed AI region in other NPC class/spec states (D64); was missing from the D60 single-state profile; game trigger DEFERRED_TO_M0 | Claude (D69) |
| 0x40 | LeafRunner | weapon-only actions | [1, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x41 | SnowRunner | action 37 | class action вЂ” AI launch (ALPHA_CODE) | D69: baked with the merged profile, played by the Unity controller | ADD_MISSING | TRANSFERRED | MEDIUM | launch proven by the executed AI region in other NPC class/spec states (D64); was missing from the D60 single-state profile; game trigger DEFERRED_TO_M0 | Claude (D69) |
| 0x41 | SnowRunner | action 40 | class action вЂ” AI launch (ALPHA_CODE) | D69: baked with the merged profile, played by the Unity controller | ADD_MISSING | TRANSFERRED | MEDIUM | launch proven by the executed AI region in other NPC class/spec states (D64); was missing from the D60 single-state profile; game trigger DEFERRED_TO_M0 | Claude (D69) |
| 0x41 | SnowRunner | action 42 | class action вЂ” AI launch (ALPHA_CODE) | D69: baked with the merged profile, played by the Unity controller | ADD_MISSING | TRANSFERRED | MEDIUM | launch proven by the executed AI region in other NPC class/spec states (D64); was missing from the D60 single-state profile; game trigger DEFERRED_TO_M0 | Claude (D69) |
| 0x41 | SnowRunner | action 43 | class action вЂ” AI launch (ALPHA_CODE) | D69: baked with the merged profile, played by the Unity controller | ADD_MISSING | TRANSFERRED | MEDIUM | launch proven by the executed AI region in other NPC class/spec states (D64); was missing from the D60 single-state profile; game trigger DEFERRED_TO_M0 | Claude (D69) |
| 0x41 | SnowRunner | weapon-only actions | [1, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x42 | DesertRunner | action 37 | class action вЂ” AI launch (ALPHA_CODE) | D69: baked with the merged profile, played by the Unity controller | ADD_MISSING | TRANSFERRED | MEDIUM | launch proven by the executed AI region in other NPC class/spec states (D64); was missing from the D60 single-state profile; game trigger DEFERRED_TO_M0 | Claude (D69) |
| 0x42 | DesertRunner | action 40 | class action вЂ” AI launch (ALPHA_CODE) | D69: baked with the merged profile, played by the Unity controller | ADD_MISSING | TRANSFERRED | MEDIUM | launch proven by the executed AI region in other NPC class/spec states (D64); was missing from the D60 single-state profile; game trigger DEFERRED_TO_M0 | Claude (D69) |
| 0x42 | DesertRunner | action 42 | class action вЂ” AI launch (ALPHA_CODE) | D69: baked with the merged profile, played by the Unity controller | ADD_MISSING | TRANSFERRED | MEDIUM | launch proven by the executed AI region in other NPC class/spec states (D64); was missing from the D60 single-state profile; game trigger DEFERRED_TO_M0 | Claude (D69) |
| 0x42 | DesertRunner | action 43 | class action вЂ” AI launch (ALPHA_CODE) | D69: baked with the merged profile, played by the Unity controller | ADD_MISSING | TRANSFERRED | MEDIUM | launch proven by the executed AI region in other NPC class/spec states (D64); was missing from the D60 single-state profile; game trigger DEFERRED_TO_M0 | Claude (D69) |
| 0x42 | DesertRunner | weapon-only actions | [1, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x43 | Peacock | weapon-only actions | [1, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x44 | Frog | weapon-only actions | [1, 5, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57, 59, 60, 61] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x45 | PlantCreature | weapon-only actions | [1, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x46 | RadishCreature | weapon-only actions | [1, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x47 | Onionling | weapon-only actions | [22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x48 | DesertOnionling | weapon-only actions | [22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x49 | Devourer | weapon-only actions | [22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x4a | Duckbill | weapon-only actions | [22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x4b | Crocodile | weapon-only actions | [22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x4c | SpikeCreature | weapon-only actions | [1, 5, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57, 59, 60, 61] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x4d | Anubis | weapon-only actions | [1, 5, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57, 59, 60, 61] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x4e | Horus | weapon-only actions | [1, 5, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57, 59, 60, 61] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x4f | Jester | weapon-only actions | [1, 5, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57, 59, 60, 61] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x50 | Spectrino | weapon-only actions | [1, 5, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57, 59, 60, 61] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x51 | Djinn | weapon-only actions | [1, 5, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57, 59, 60, 61] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x52 | Minotaur | weapon-only actions | [1, 5, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57, 59, 60, 61] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x53 | NomadMale | weapon-only actions | [1, 5, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57, 59, 60, 61] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x54 | NomadFemale | weapon-only actions | [1, 5, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57, 59, 60, 61] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x55 | Imp | weapon-only actions | [1, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x56 | Spitter | weapon-only actions | [23, 30, 31, 32, 33, 38, 44, 45, 46] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x57 | Mole | weapon-only actions | [1, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x58 | Biter | weapon-only actions | [1, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x59 | Koala | weapon-only actions | [1, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x5a | Squirrel | weapon-only actions | [1, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x5b | Raccoon | weapon-only actions | [1, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x5c | Owl | weapon-only actions | [1, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x5d | Penguin | weapon-only actions | [1, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x5e | Werewolf | weapon-only actions | [1, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x5f | ? | weapon-only actions | [1, 5, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57, 59, 60, 61] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x60 | Zombie | weapon-only actions | [1, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x61 | Vampire | weapon-only actions | [1, 5, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57, 59, 60, 61] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x62 | Horse | weapon-only actions | [22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x63 | Camel | weapon-only actions | [22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x64 | Cow | weapon-only actions | [22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x65 | Dragon | model/rig | head anchored at sy*0.2 (ALPHA_CODE) | D65 2/2 runs PASS; head mesh used the centre anchor | FIX | FIXED | HIGH | head shifted ~13 grid voxels (0.58 world units) into the body in every pose; fixed: anchor rule head_0x65 in pipeline/contract/AlphaRig; end-to-end test vs executed drawModel | Claude (D69) |
| 0x65 | Dragon | action 51 | breath/flight state (dragon/winged) вЂ” AI launch (ALPHA_CODE); pose static | BLOCKED ACTION_STATIC | ADD_MISSING | BLOCKED | MEDIUM | the visible part is an effect (separate renderer spawnEffectMesh), R-3; no pose to transfer | next S05/M0 effects task |
| 0x65 | Dragon | weapon-only actions | [23, 30, 31, 32, 33, 38, 44, 45, 46] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x66 | BarkBeetle | weapon-only actions | [22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x67 | FireBeetle | weapon-only actions | [22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x68 | SnoutBeetle | weapon-only actions | [23, 24, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x69 | LemonBeetle | weapon-only actions | [22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x6a | Crab | weapon-only actions | [1, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x6b | SeaCrab | weapon-only actions | [1, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x6c | Troll | weapon-only actions | [1, 5, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57, 59, 60, 61] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x6d | DarkTroll | weapon-only actions | [1, 5, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57, 59, 60, 61] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x6e | HellDemon | weapon-only actions | [1, 5, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57, 59, 60, 61] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x6f | Golem | action 108 | throw (golem/rockling) вЂ” AI launch (ALPHA_CODE); needs a dynamic part | BLOCKED ACTION_PART_SET_CHANGED | ADD_MISSING | BLOCKED | MEDIUM | thrown/held object model is not in the kind's rig (R-8) | next S05 task |
| 0x70 | EmberGolem | action 108 | throw (golem/rockling) вЂ” AI launch (ALPHA_CODE); needs a dynamic part | BLOCKED ACTION_PART_SET_CHANGED | ADD_MISSING | BLOCKED | MEDIUM | thrown/held object model is not in the kind's rig (R-8) | next S05 task |
| 0x71 | SnowGolem | action 108 | throw (golem/rockling) вЂ” AI launch (ALPHA_CODE); needs a dynamic part | BLOCKED ACTION_PART_SET_CHANGED | ADD_MISSING | BLOCKED | MEDIUM | thrown/held object model is not in the kind's rig (R-8) | next S05 task |
| 0x72 | Yeti | weapon-only actions | [1, 5, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57, 59, 60, 61] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x73 | Cyclops | weapon-only actions | [1, 5, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57, 59, 60, 61] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x75 | Lich | weapon-only actions | [23, 30, 31, 32, 33, 38, 44, 45, 46] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x76 | RuneGiant | actions | AI drives another creature (R-9) | no actions | UNKNOWN_NEEDS_RESEARCH | BLOCKED | LOW | world creature list is an input of the AI region | M0 AI research |
| 0x78 | Bush | walk | NOT_USED_BY_ALPHA_GAME (ALPHA_CODE: faction 6 -> no RandomWalkBehavior 0x5360AE) | D65: parametric walk active in the controller | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | STATIC_OBJECT controller mode: walk request refused, pose frozen; formula kept as research data | Claude (D69) |
| 0x78 | Bush | action 3 | melee hit вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x78 | Bush | action 4 | melee hit 2 вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x78 | Bush | action 6 | special/heavy вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x78 | Bush | action 37 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x78 | Bush | action 40 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x78 | Bush | action 42 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x78 | Bush | action 43 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x78 | Bush | action 54 | attack (humanoid default) вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x79 | SnowBush | walk | NOT_USED_BY_ALPHA_GAME (ALPHA_CODE: faction 6 -> no RandomWalkBehavior 0x5360AE) | D65: parametric walk active in the controller | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | STATIC_OBJECT controller mode: walk request refused, pose frozen; formula kept as research data | Claude (D69) |
| 0x79 | SnowBush | action 3 | melee hit вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x79 | SnowBush | action 4 | melee hit 2 вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x79 | SnowBush | action 6 | special/heavy вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x79 | SnowBush | action 37 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x79 | SnowBush | action 40 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x79 | SnowBush | action 42 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x79 | SnowBush | action 43 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x79 | SnowBush | action 54 | attack (humanoid default) вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x7a | SnowBerryBush | walk | NOT_USED_BY_ALPHA_GAME (ALPHA_CODE: faction 6 -> no RandomWalkBehavior 0x5360AE) | D65: parametric walk active in the controller | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | STATIC_OBJECT controller mode: walk request refused, pose frozen; formula kept as research data | Claude (D69) |
| 0x7a | SnowBerryBush | action 3 | melee hit вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x7a | SnowBerryBush | action 4 | melee hit 2 вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x7a | SnowBerryBush | action 6 | special/heavy вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x7a | SnowBerryBush | action 37 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x7a | SnowBerryBush | action 40 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x7a | SnowBerryBush | action 42 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x7a | SnowBerryBush | action 43 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x7a | SnowBerryBush | action 54 | attack (humanoid default) вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x7b | CottonPlant | walk | NOT_USED_BY_ALPHA_GAME (ALPHA_CODE: faction 6 -> no RandomWalkBehavior 0x5360AE) | D65: parametric walk active in the controller | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | STATIC_OBJECT controller mode: walk request refused, pose frozen; formula kept as research data | Claude (D69) |
| 0x7b | CottonPlant | action 3 | melee hit вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x7b | CottonPlant | action 4 | melee hit 2 вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x7b | CottonPlant | action 6 | special/heavy вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x7b | CottonPlant | action 37 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x7b | CottonPlant | action 40 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x7b | CottonPlant | action 42 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x7b | CottonPlant | action 43 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x7b | CottonPlant | action 54 | attack (humanoid default) вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x7c | Scrub | walk | NOT_USED_BY_ALPHA_GAME (ALPHA_CODE: faction 6 -> no RandomWalkBehavior 0x5360AE) | D65: parametric walk active in the controller | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | STATIC_OBJECT controller mode: walk request refused, pose frozen; formula kept as research data | Claude (D69) |
| 0x7c | Scrub | action 3 | melee hit вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x7c | Scrub | action 4 | melee hit 2 вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x7c | Scrub | action 6 | special/heavy вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x7c | Scrub | action 37 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x7c | Scrub | action 40 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x7c | Scrub | action 42 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x7c | Scrub | action 43 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x7c | Scrub | action 54 | attack (humanoid default) вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x7d | CobwebScrub | walk | NOT_USED_BY_ALPHA_GAME (ALPHA_CODE: faction 6 -> no RandomWalkBehavior 0x5360AE) | D65: parametric walk active in the controller | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | STATIC_OBJECT controller mode: walk request refused, pose frozen; formula kept as research data | Claude (D69) |
| 0x7d | CobwebScrub | action 3 | melee hit вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x7d | CobwebScrub | action 4 | melee hit 2 вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x7d | CobwebScrub | action 6 | special/heavy вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x7d | CobwebScrub | action 37 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x7d | CobwebScrub | action 40 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x7d | CobwebScrub | action 42 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x7d | CobwebScrub | action 43 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x7d | CobwebScrub | action 54 | attack (humanoid default) вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x7e | FireScrub | walk | NOT_USED_BY_ALPHA_GAME (ALPHA_CODE: faction 6 -> no RandomWalkBehavior 0x5360AE) | D65: parametric walk active in the controller | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | STATIC_OBJECT controller mode: walk request refused, pose frozen; formula kept as research data | Claude (D69) |
| 0x7e | FireScrub | action 3 | melee hit вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x7e | FireScrub | action 4 | melee hit 2 вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x7e | FireScrub | action 6 | special/heavy вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x7e | FireScrub | action 37 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x7e | FireScrub | action 40 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x7e | FireScrub | action 42 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x7e | FireScrub | action 43 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x7e | FireScrub | action 54 | attack (humanoid default) вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x7f | Ginseng | walk | NOT_USED_BY_ALPHA_GAME (ALPHA_CODE: faction 6 -> no RandomWalkBehavior 0x5360AE) | D65: parametric walk active in the controller | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | STATIC_OBJECT controller mode: walk request refused, pose frozen; formula kept as research data | Claude (D69) |
| 0x7f | Ginseng | action 3 | melee hit вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x7f | Ginseng | action 4 | melee hit 2 вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x7f | Ginseng | action 6 | special/heavy вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x7f | Ginseng | action 37 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x7f | Ginseng | action 40 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x7f | Ginseng | action 42 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x7f | Ginseng | action 43 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x7f | Ginseng | action 54 | attack (humanoid default) вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x80 | Cactus | walk | NOT_USED_BY_ALPHA_GAME (ALPHA_CODE: faction 6 -> no RandomWalkBehavior 0x5360AE) | D65: parametric walk active in the controller | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | STATIC_OBJECT controller mode: walk request refused, pose frozen; formula kept as research data | Claude (D69) |
| 0x80 | Cactus | action 3 | melee hit вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x80 | Cactus | action 4 | melee hit 2 вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x80 | Cactus | action 6 | special/heavy вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x80 | Cactus | action 37 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x80 | Cactus | action 40 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x80 | Cactus | action 42 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x80 | Cactus | action 43 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x80 | Cactus | action 54 | attack (humanoid default) вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x81 | ? | walk | drawModel formula exists (ALPHA_CODE); game use: PRODUCER_RULE_D67 | D65: parametric walk active in the controller | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | STATIC_OBJECT controller mode: walk request refused, pose frozen; formula kept as research data | Claude (D69) |
| 0x81 | ? | action 3 | melee hit вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x81 | ? | action 4 | melee hit 2 вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x81 | ? | action 6 | special/heavy вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x81 | ? | action 37 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x81 | ? | action 40 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x81 | ? | action 42 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x81 | ? | action 43 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x81 | ? | action 54 | attack (humanoid default) вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x82 | ThornTree | walk | NOT_USED_BY_ALPHA_GAME (ALPHA_CODE: faction 6 -> no RandomWalkBehavior 0x5360AE) | D65: parametric walk active in the controller | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | STATIC_OBJECT controller mode: walk request refused, pose frozen; formula kept as research data | Claude (D69) |
| 0x82 | ThornTree | action 3 | melee hit вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x82 | ThornTree | action 4 | melee hit 2 вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x82 | ThornTree | action 6 | special/heavy вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x82 | ThornTree | action 37 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x82 | ThornTree | action 40 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x82 | ThornTree | action 42 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x82 | ThornTree | action 43 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x82 | ThornTree | action 54 | attack (humanoid default) вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x83 | GoldDeposit | walk | NOT_USED_BY_ALPHA_GAME (ALPHA_CODE: faction 6 -> no RandomWalkBehavior 0x5360AE) | D65: parametric walk active in the controller | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | STATIC_OBJECT controller mode: walk request refused, pose frozen; formula kept as research data | Claude (D69) |
| 0x83 | GoldDeposit | action 3 | melee hit вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x83 | GoldDeposit | action 4 | melee hit 2 вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x83 | GoldDeposit | action 6 | special/heavy вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x83 | GoldDeposit | action 37 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x83 | GoldDeposit | action 40 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x83 | GoldDeposit | action 42 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x83 | GoldDeposit | action 43 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x83 | GoldDeposit | action 54 | attack (humanoid default) вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x84 | IronDeposit | walk | NOT_USED_BY_ALPHA_GAME (ALPHA_CODE: faction 6 -> no RandomWalkBehavior 0x5360AE) | D65: parametric walk active in the controller | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | STATIC_OBJECT controller mode: walk request refused, pose frozen; formula kept as research data | Claude (D69) |
| 0x84 | IronDeposit | action 3 | melee hit вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x84 | IronDeposit | action 4 | melee hit 2 вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x84 | IronDeposit | action 6 | special/heavy вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x84 | IronDeposit | action 37 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x84 | IronDeposit | action 40 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x84 | IronDeposit | action 42 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x84 | IronDeposit | action 43 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x84 | IronDeposit | action 54 | attack (humanoid default) вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x85 | SilverDeposit | walk | NOT_USED_BY_ALPHA_GAME (ALPHA_CODE: faction 6 -> no RandomWalkBehavior 0x5360AE) | D65: parametric walk active in the controller | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | STATIC_OBJECT controller mode: walk request refused, pose frozen; formula kept as research data | Claude (D69) |
| 0x85 | SilverDeposit | action 3 | melee hit вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x85 | SilverDeposit | action 4 | melee hit 2 вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x85 | SilverDeposit | action 6 | special/heavy вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x85 | SilverDeposit | action 37 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x85 | SilverDeposit | action 40 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x85 | SilverDeposit | action 42 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x85 | SilverDeposit | action 43 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x85 | SilverDeposit | action 54 | attack (humanoid default) вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x86 | SandstoneDeposit | walk | drawModel formula exists (ALPHA_CODE); game use: SAME_OBJECT_CLASS | D65: parametric walk active in the controller | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | STATIC_OBJECT controller mode: walk request refused, pose frozen; formula kept as research data | Claude (D69) |
| 0x86 | SandstoneDeposit | action 3 | melee hit вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x86 | SandstoneDeposit | action 4 | melee hit 2 вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x86 | SandstoneDeposit | action 6 | special/heavy вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x86 | SandstoneDeposit | action 37 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x86 | SandstoneDeposit | action 40 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x86 | SandstoneDeposit | action 42 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x86 | SandstoneDeposit | action 43 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x86 | SandstoneDeposit | action 54 | attack (humanoid default) вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x87 | EmeraldDeposit | walk | NOT_USED_BY_ALPHA_GAME (ALPHA_CODE: faction 6 -> no RandomWalkBehavior 0x5360AE) | D65: parametric walk active in the controller | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | STATIC_OBJECT controller mode: walk request refused, pose frozen; formula kept as research data | Claude (D69) |
| 0x87 | EmeraldDeposit | action 3 | melee hit вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x87 | EmeraldDeposit | action 4 | melee hit 2 вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x87 | EmeraldDeposit | action 6 | special/heavy вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x87 | EmeraldDeposit | action 37 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x87 | EmeraldDeposit | action 40 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x87 | EmeraldDeposit | action 42 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x87 | EmeraldDeposit | action 43 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x87 | EmeraldDeposit | action 54 | attack (humanoid default) вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x88 | SapphireDeposit | walk | NOT_USED_BY_ALPHA_GAME (ALPHA_CODE: faction 6 -> no RandomWalkBehavior 0x5360AE) | D65: parametric walk active in the controller | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | STATIC_OBJECT controller mode: walk request refused, pose frozen; formula kept as research data | Claude (D69) |
| 0x88 | SapphireDeposit | action 3 | melee hit вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x88 | SapphireDeposit | action 4 | melee hit 2 вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x88 | SapphireDeposit | action 6 | special/heavy вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x88 | SapphireDeposit | action 37 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x88 | SapphireDeposit | action 40 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x88 | SapphireDeposit | action 42 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x88 | SapphireDeposit | action 43 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x88 | SapphireDeposit | action 54 | attack (humanoid default) вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x89 | RubyDeposit | walk | NOT_USED_BY_ALPHA_GAME (ALPHA_CODE: faction 6 -> no RandomWalkBehavior 0x5360AE) | D65: parametric walk active in the controller | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | STATIC_OBJECT controller mode: walk request refused, pose frozen; formula kept as research data | Claude (D69) |
| 0x89 | RubyDeposit | action 3 | melee hit вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x89 | RubyDeposit | action 4 | melee hit 2 вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x89 | RubyDeposit | action 6 | special/heavy вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x89 | RubyDeposit | action 37 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x89 | RubyDeposit | action 40 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x89 | RubyDeposit | action 42 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x89 | RubyDeposit | action 43 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x89 | RubyDeposit | action 54 | attack (humanoid default) вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x8a | DiamondDeposit | walk | NOT_USED_BY_ALPHA_GAME (ALPHA_CODE: faction 6 -> no RandomWalkBehavior 0x5360AE) | D65: parametric walk active in the controller | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | STATIC_OBJECT controller mode: walk request refused, pose frozen; formula kept as research data | Claude (D69) |
| 0x8a | DiamondDeposit | action 3 | melee hit вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x8a | DiamondDeposit | action 4 | melee hit 2 вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x8a | DiamondDeposit | action 6 | special/heavy вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x8a | DiamondDeposit | action 37 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x8a | DiamondDeposit | action 40 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x8a | DiamondDeposit | action 42 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x8a | DiamondDeposit | action 43 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x8a | DiamondDeposit | action 54 | attack (humanoid default) вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x8b | IceCrystalDeposit | walk | drawModel formula exists (ALPHA_CODE); game use: SAME_OBJECT_CLASS | D65: parametric walk active in the controller | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | STATIC_OBJECT controller mode: walk request refused, pose frozen; formula kept as research data | Claude (D69) |
| 0x8b | IceCrystalDeposit | action 3 | melee hit вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x8b | IceCrystalDeposit | action 4 | melee hit 2 вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x8b | IceCrystalDeposit | action 6 | special/heavy вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x8b | IceCrystalDeposit | action 37 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x8b | IceCrystalDeposit | action 40 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x8b | IceCrystalDeposit | action 42 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x8b | IceCrystalDeposit | action 43 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x8b | IceCrystalDeposit | action 54 | attack (humanoid default) вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x8c | Scarecrow | walk | NOT_USED_BY_ALPHA_GAME (ALPHA_CODE: faction 6 -> no RandomWalkBehavior 0x5360AE) | D65: parametric walk active in the controller | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | STATIC_OBJECT controller mode: walk request refused, pose frozen; formula kept as research data | Claude (D69) |
| 0x8c | Scarecrow | action 3 | melee hit вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x8c | Scarecrow | action 4 | melee hit 2 вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x8c | Scarecrow | action 6 | special/heavy вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x8c | Scarecrow | action 37 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x8c | Scarecrow | action 40 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x8c | Scarecrow | action 42 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x8c | Scarecrow | action 43 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x8c | Scarecrow | action 54 | attack (humanoid default) вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x8d | Aim | walk | drawModel formula exists (ALPHA_CODE); game use: SAME_OBJECT_CLASS | D65: parametric walk active in the controller | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | STATIC_OBJECT controller mode: walk request refused, pose frozen; formula kept as research data | Claude (D69) |
| 0x8d | Aim | action 3 | melee hit вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x8d | Aim | action 4 | melee hit 2 вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x8d | Aim | action 6 | special/heavy вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x8d | Aim | action 37 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x8d | Aim | action 40 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x8d | Aim | action 42 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x8d | Aim | action 43 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x8d | Aim | action 54 | attack (humanoid default) вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x8e | Dummy | walk | drawModel formula exists (ALPHA_CODE); game use: SAME_OBJECT_CLASS | D65: parametric walk active in the controller | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | STATIC_OBJECT controller mode: walk request refused, pose frozen; formula kept as research data | Claude (D69) |
| 0x8e | Dummy | action 3 | melee hit вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x8e | Dummy | action 4 | melee hit 2 вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x8e | Dummy | action 6 | special/heavy вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x8e | Dummy | action 37 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x8e | Dummy | action 40 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x8e | Dummy | action 42 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x8e | Dummy | action 43 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x8e | Dummy | action 54 | attack (humanoid default) вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x8f | Vase | walk | NOT_USED_BY_ALPHA_GAME (ALPHA_CODE: faction 6 -> no RandomWalkBehavior 0x5360AE) | D65: parametric walk active in the controller | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | STATIC_OBJECT controller mode: walk request refused, pose frozen; formula kept as research data | Claude (D69) |
| 0x8f | Vase | action 3 | melee hit вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x8f | Vase | action 4 | melee hit 2 вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x8f | Vase | action 6 | special/heavy вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x8f | Vase | action 37 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x8f | Vase | action 40 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x8f | Vase | action 42 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x8f | Vase | action 43 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x8f | Vase | action 54 | attack (humanoid default) вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x90 | Bomb | walk | NOT_USED_BY_ALPHA_GAME (ALPHA_CODE: faction 6 -> no RandomWalkBehavior 0x5360AE) | D65: parametric walk active in the controller | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | STATIC_OBJECT controller mode: walk request refused, pose frozen; formula kept as research data | Claude (D69) |
| 0x90 | Bomb | action 3 | melee hit вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x90 | Bomb | action 4 | melee hit 2 вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x90 | Bomb | action 6 | special/heavy вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x90 | Bomb | action 37 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x90 | Bomb | action 40 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x90 | Bomb | action 42 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x90 | Bomb | action 43 | class action вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x90 | Bomb | action 54 | attack (humanoid default) вЂ” AI launch (ALPHA_CODE); NOT used by the Alpha game (passive) | EMULATED_ALPHA_CODE | DISABLE_IN_GAME | DISABLED_IN_GAME | HIGH | decorative/passive object never acts; research clip kept in .scratch library | Claude (D69) |
| 0x91 | SapphireFish | weapon-only actions | [22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x92 | LemonFish | weapon-only actions | [22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x93 | Seahorse | weapon-only actions | [22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x94 | Mermaid | weapon-only actions | [1, 5, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57, 59, 60, 61] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x95 | Merman | weapon-only actions | [1, 5, 14, 19, 22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55, 57, 59, 60, 61] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x96 | Shark | weapon-only actions | [22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x97 | Bumblebee | action 51 | breath/flight state (dragon/winged) вЂ” AI launch (ALPHA_CODE); pose static | BLOCKED ACTION_STATIC | ADD_MISSING | BLOCKED | MEDIUM | the visible part is an effect (separate renderer spawnEffectMesh), R-3; no pose to transfer | next S05/M0 effects task |
| 0x97 | Bumblebee | weapon-only actions | [23, 30, 31, 32, 33, 38, 44, 45, 46] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x98 | LanternFish | action 37 | class action вЂ” AI launch (ALPHA_CODE); pose static | BLOCKED ACTION_STATIC | ADD_MISSING | BLOCKED | MEDIUM | the visible part is an effect (separate renderer spawnEffectMesh), R-3; no pose to transfer | next S05/M0 effects task |
| 0x98 | LanternFish | action 40 | class action вЂ” AI launch (ALPHA_CODE); pose static | BLOCKED ACTION_STATIC | ADD_MISSING | BLOCKED | MEDIUM | the visible part is an effect (separate renderer spawnEffectMesh), R-3; no pose to transfer | next S05/M0 effects task |
| 0x98 | LanternFish | action 42 | class action вЂ” AI launch (ALPHA_CODE); pose static | BLOCKED ACTION_STATIC | ADD_MISSING | BLOCKED | MEDIUM | the visible part is an effect (separate renderer spawnEffectMesh), R-3; no pose to transfer | next S05/M0 effects task |
| 0x98 | LanternFish | action 43 | class action вЂ” AI launch (ALPHA_CODE); pose static | BLOCKED ACTION_STATIC | ADD_MISSING | BLOCKED | MEDIUM | the visible part is an effect (separate renderer spawnEffectMesh), R-3; no pose to transfer | next S05/M0 effects task |
| 0x98 | LanternFish | weapon-only actions | [22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x99 | MawFish | weapon-only actions | [22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x9a | Piranha | weapon-only actions | [22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |
| 0x9b | Blowfish | weapon-only actions | [22, 23, 24, 25, 26, 27, 30, 31, 32, 33, 38, 44, 45, 46, 55] launched only with a weapon (ALPHA_CODE) | not transferred (no NPC weapon layer on this rig) | ADD_MISSING | DEFERRED_TO_M0 | MEDIUM | NPC weapon comes from the world spawn record (R-1/R-2); needs the M0 NPC equipment system | M0 (DeepSeek) |

## KEEP (РїРµСЂРµРЅРµСЃРµРЅРѕ РІРµСЂРЅРѕ)

| kind | РёРјСЏ | С‡С‚Рѕ | Unity | РёС‚РѕРі |
|---|---|---|---|---|
| 0x00 | ElfMale | model/rig | D65 13/13 runs PASS | KEPT |
| 0x00 | ElfMale | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x00 | ElfMale | action 3 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x00 | ElfMale | action 4 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x00 | ElfMale | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x00 | ElfMale | action 54 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x01 | ElfFemale | model/rig | D65 11/11 runs PASS | KEPT |
| 0x01 | ElfFemale | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x01 | ElfFemale | action 3 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x01 | ElfFemale | action 4 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x01 | ElfFemale | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x01 | ElfFemale | action 54 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x02 | HumanMale | model/rig | D65 15/15 runs PASS | KEPT |
| 0x02 | HumanMale | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x03 | HumanFemale | model/rig | D65 10/10 runs PASS | KEPT |
| 0x03 | HumanFemale | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x03 | HumanFemale | action 3 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x03 | HumanFemale | action 4 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x03 | HumanFemale | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x03 | HumanFemale | action 54 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x04 | GoblinMale | model/rig | D65 7/7 runs PASS | KEPT |
| 0x04 | GoblinMale | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x04 | GoblinMale | action 3 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x04 | GoblinMale | action 4 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x04 | GoblinMale | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x04 | GoblinMale | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x04 | GoblinMale | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x04 | GoblinMale | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x04 | GoblinMale | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x04 | GoblinMale | action 54 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x05 | GoblinFemale | model/rig | D65 7/7 runs PASS | KEPT |
| 0x05 | GoblinFemale | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x05 | GoblinFemale | action 3 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x05 | GoblinFemale | action 4 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x05 | GoblinFemale | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x05 | GoblinFemale | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x05 | GoblinFemale | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x05 | GoblinFemale | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x05 | GoblinFemale | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x05 | GoblinFemale | action 54 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x06 | Bullterrier | model/rig | D65 2/2 runs PASS | KEPT |
| 0x06 | Bullterrier | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x06 | Bullterrier | action 3 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x06 | Bullterrier | action 4 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x06 | Bullterrier | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x06 | Bullterrier | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x06 | Bullterrier | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x06 | Bullterrier | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x06 | Bullterrier | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x06 | Bullterrier | action 54 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x07 | LizardmanMale | model/rig | D65 7/7 runs PASS | KEPT |
| 0x07 | LizardmanMale | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x07 | LizardmanMale | action 3 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x07 | LizardmanMale | action 4 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x07 | LizardmanMale | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x07 | LizardmanMale | action 54 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x08 | LizardmanFemale | model/rig | D65 7/7 runs PASS | KEPT |
| 0x08 | LizardmanFemale | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x08 | LizardmanFemale | action 3 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x08 | LizardmanFemale | action 4 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x08 | LizardmanFemale | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x08 | LizardmanFemale | action 54 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x09 | DwarfMale | model/rig | D65 6/6 runs PASS | KEPT |
| 0x09 | DwarfMale | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x09 | DwarfMale | action 3 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x09 | DwarfMale | action 4 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x09 | DwarfMale | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x09 | DwarfMale | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x09 | DwarfMale | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x09 | DwarfMale | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x09 | DwarfMale | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x09 | DwarfMale | action 54 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x0a | DwarfFemale | model/rig | D65 6/6 runs PASS | KEPT |
| 0x0a | DwarfFemale | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x0a | DwarfFemale | action 3 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x0a | DwarfFemale | action 4 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x0a | DwarfFemale | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x0a | DwarfFemale | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x0a | DwarfFemale | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x0a | DwarfFemale | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x0a | DwarfFemale | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x0a | DwarfFemale | action 54 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x0b | OrcMale | model/rig | D65 11/11 runs PASS | KEPT |
| 0x0b | OrcMale | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x0b | OrcMale | action 3 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x0b | OrcMale | action 4 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x0b | OrcMale | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x0b | OrcMale | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x0b | OrcMale | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x0b | OrcMale | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x0b | OrcMale | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x0b | OrcMale | action 54 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x0c | OrcFemale | model/rig | D65 6/6 runs PASS | KEPT |
| 0x0c | OrcFemale | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x0c | OrcFemale | action 3 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x0c | OrcFemale | action 4 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x0c | OrcFemale | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x0c | OrcFemale | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x0c | OrcFemale | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x0c | OrcFemale | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x0c | OrcFemale | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x0c | OrcFemale | action 54 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x0d | FrogmanMale | model/rig | D65 6/6 runs PASS | KEPT |
| 0x0d | FrogmanMale | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x0d | FrogmanMale | action 3 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x0d | FrogmanMale | action 4 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x0d | FrogmanMale | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x0d | FrogmanMale | action 54 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x0e | FrogmanFemale | model/rig | D65 5/5 runs PASS | KEPT |
| 0x0e | FrogmanFemale | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x0e | FrogmanFemale | action 3 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x0e | FrogmanFemale | action 4 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x0e | FrogmanFemale | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x0e | FrogmanFemale | action 54 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x0f | UndeadMale | model/rig | D65 7/7 runs PASS | KEPT |
| 0x0f | UndeadMale | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x0f | UndeadMale | action 3 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x0f | UndeadMale | action 4 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x0f | UndeadMale | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x0f | UndeadMale | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x0f | UndeadMale | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x0f | UndeadMale | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x0f | UndeadMale | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x0f | UndeadMale | action 54 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x10 | UndeadFemale | model/rig | D65 7/7 runs PASS | KEPT |
| 0x10 | UndeadFemale | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x10 | UndeadFemale | action 3 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x10 | UndeadFemale | action 4 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x10 | UndeadFemale | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x10 | UndeadFemale | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x10 | UndeadFemale | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x10 | UndeadFemale | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x10 | UndeadFemale | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x10 | UndeadFemale | action 54 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x11 | Skeleton | model/rig | D65 2/2 runs PASS | KEPT |
| 0x11 | Skeleton | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x11 | Skeleton | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x11 | Skeleton | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x11 | Skeleton | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x11 | Skeleton | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x11 | Skeleton | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x12 | OldMan | model/rig | D65 2/2 runs PASS | KEPT |
| 0x12 | OldMan | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x12 | OldMan | action 3 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x12 | OldMan | action 4 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x12 | OldMan | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x12 | OldMan | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x12 | OldMan | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x12 | OldMan | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x12 | OldMan | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x12 | OldMan | action 54 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x13 | Collie | model/rig | D65 2/2 runs PASS | KEPT |
| 0x13 | Collie | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x13 | Collie | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x13 | Collie | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x13 | Collie | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x13 | Collie | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x13 | Collie | action 75 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x14 | ShepherdDog | model/rig | D65 2/2 runs PASS | KEPT |
| 0x14 | ShepherdDog | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x14 | ShepherdDog | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x14 | ShepherdDog | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x14 | ShepherdDog | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x14 | ShepherdDog | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x14 | ShepherdDog | action 75 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x15 | SkullBull | model/rig | D65 2/2 runs PASS | KEPT |
| 0x15 | SkullBull | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x15 | SkullBull | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x15 | SkullBull | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x15 | SkullBull | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x15 | SkullBull | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x15 | SkullBull | action 75 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x16 | Alpaca | model/rig | D65 2/2 runs PASS | KEPT |
| 0x16 | Alpaca | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x16 | Alpaca | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x16 | Alpaca | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x16 | Alpaca | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x16 | Alpaca | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x16 | Alpaca | action 75 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x17 | BrownAlpaca | model/rig | D65 2/2 runs PASS | KEPT |
| 0x17 | BrownAlpaca | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x17 | BrownAlpaca | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x17 | BrownAlpaca | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x17 | BrownAlpaca | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x17 | BrownAlpaca | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x17 | BrownAlpaca | action 75 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x18 | Egg | model/rig | D65 2/2 runs PASS | KEPT |
| 0x18 | Egg | idle | standing pose (formula amp 0) | KEPT |
| 0x19 | Turtle | model/rig | D65 2/2 runs PASS | KEPT |
| 0x19 | Turtle | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x19 | Turtle | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x19 | Turtle | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x19 | Turtle | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x19 | Turtle | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x1a | Terrier | model/rig | D65 2/2 runs PASS | KEPT |
| 0x1a | Terrier | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x1a | Terrier | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x1a | Terrier | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x1a | Terrier | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x1a | Terrier | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x1a | Terrier | action 75 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x1b | ScottishTerrier | model/rig | D65 2/2 runs PASS | KEPT |
| 0x1b | ScottishTerrier | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x1b | ScottishTerrier | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x1b | ScottishTerrier | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x1b | ScottishTerrier | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x1b | ScottishTerrier | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x1b | ScottishTerrier | action 75 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x1c | Wolf | model/rig | D65 2/2 runs PASS | KEPT |
| 0x1c | Wolf | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x1c | Wolf | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x1c | Wolf | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x1c | Wolf | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x1c | Wolf | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x1c | Wolf | action 75 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x1d | Panther | model/rig | D65 2/2 runs PASS | KEPT |
| 0x1d | Panther | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x1d | Panther | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x1d | Panther | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x1d | Panther | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x1d | Panther | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x1d | Panther | action 75 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x1e | Cat | model/rig | D65 6/6 runs PASS | KEPT |
| 0x1e | Cat | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x1e | Cat | action 75 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x1f | BrownCat | model/rig | D65 2/2 runs PASS | KEPT |
| 0x1f | BrownCat | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x1f | BrownCat | action 75 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x20 | WhiteCat | model/rig | D65 2/2 runs PASS | KEPT |
| 0x20 | WhiteCat | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x20 | WhiteCat | action 75 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x21 | Pig | model/rig | D65 2/2 runs PASS | KEPT |
| 0x21 | Pig | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x21 | Pig | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x21 | Pig | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x21 | Pig | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x21 | Pig | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x21 | Pig | action 75 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x22 | Sheep | model/rig | D65 2/2 runs PASS | KEPT |
| 0x22 | Sheep | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x22 | Sheep | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x22 | Sheep | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x22 | Sheep | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x22 | Sheep | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x22 | Sheep | action 75 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x23 | Bunny | model/rig | D65 2/2 runs PASS | KEPT |
| 0x23 | Bunny | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x23 | Bunny | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x23 | Bunny | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x23 | Bunny | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x23 | Bunny | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x23 | Bunny | action 75 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x24 | Porcupine | model/rig | D65 2/2 runs PASS | KEPT |
| 0x24 | Porcupine | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x24 | Porcupine | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x24 | Porcupine | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x24 | Porcupine | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x24 | Porcupine | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x24 | Porcupine | action 75 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x25 | GreenSlime | model/rig | D65 2/2 runs PASS | KEPT |
| 0x25 | GreenSlime | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x25 | GreenSlime | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x25 | GreenSlime | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x25 | GreenSlime | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x25 | GreenSlime | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x25 | GreenSlime | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x26 | PinkSlime | model/rig | D65 2/2 runs PASS | KEPT |
| 0x26 | PinkSlime | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x26 | PinkSlime | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x26 | PinkSlime | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x26 | PinkSlime | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x26 | PinkSlime | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x26 | PinkSlime | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x27 | YellowSlime | model/rig | D65 2/2 runs PASS | KEPT |
| 0x27 | YellowSlime | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x27 | YellowSlime | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x27 | YellowSlime | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x27 | YellowSlime | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x27 | YellowSlime | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x27 | YellowSlime | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x28 | BlueSlime | model/rig | D65 2/2 runs PASS | KEPT |
| 0x28 | BlueSlime | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x28 | BlueSlime | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x28 | BlueSlime | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x28 | BlueSlime | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x28 | BlueSlime | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x28 | BlueSlime | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x29 | Frightener | model/rig | D65 2/2 runs PASS | KEPT |
| 0x29 | Frightener | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x29 | Frightener | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x2a | SandHorror | model/rig | D65 2/2 runs PASS | KEPT |
| 0x2a | SandHorror | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x2a | SandHorror | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x2a | SandHorror | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x2a | SandHorror | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x2a | SandHorror | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x2a | SandHorror | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x2b | Wizard | model/rig | D65 2/2 runs PASS | KEPT |
| 0x2b | Wizard | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x2b | Wizard | action 3 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x2b | Wizard | action 4 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x2b | Wizard | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x2b | Wizard | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x2b | Wizard | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x2b | Wizard | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x2b | Wizard | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x2b | Wizard | action 54 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x2c | Bandit | model/rig | D65 2/2 runs PASS | KEPT |
| 0x2c | Bandit | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x2c | Bandit | action 3 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x2c | Bandit | action 4 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x2c | Bandit | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x2c | Bandit | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x2c | Bandit | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x2c | Bandit | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x2c | Bandit | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x2c | Bandit | action 54 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x2d | Witch | model/rig | D65 2/2 runs PASS | KEPT |
| 0x2d | Witch | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x2d | Witch | action 3 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x2d | Witch | action 4 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x2d | Witch | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x2d | Witch | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x2d | Witch | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x2d | Witch | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x2d | Witch | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x2d | Witch | action 54 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x2e | Ogre | model/rig | D65 2/2 runs PASS | KEPT |
| 0x2e | Ogre | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x2e | Ogre | action 3 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x2e | Ogre | action 4 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x2e | Ogre | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x2e | Ogre | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x2e | Ogre | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x2e | Ogre | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x2e | Ogre | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x2e | Ogre | action 54 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x2f | Rockling | model/rig | D65 2/2 runs PASS | KEPT |
| 0x2f | Rockling | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x2f | Rockling | action 54 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x2f | Rockling | action 76 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x30 | Gnoll | model/rig | D65 2/2 runs PASS | KEPT |
| 0x30 | Gnoll | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x30 | Gnoll | action 3 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x30 | Gnoll | action 4 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x30 | Gnoll | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x30 | Gnoll | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x30 | Gnoll | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x30 | Gnoll | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x30 | Gnoll | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x30 | Gnoll | action 54 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x31 | PolarGnoll | model/rig | D65 2/2 runs PASS | KEPT |
| 0x31 | PolarGnoll | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x31 | PolarGnoll | action 3 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x31 | PolarGnoll | action 4 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x31 | PolarGnoll | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x31 | PolarGnoll | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x31 | PolarGnoll | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x31 | PolarGnoll | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x31 | PolarGnoll | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x31 | PolarGnoll | action 54 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x32 | Monkey | model/rig | D65 2/2 runs PASS | KEPT |
| 0x32 | Monkey | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x32 | Monkey | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x32 | Monkey | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x32 | Monkey | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x32 | Monkey | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x32 | Monkey | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x33 | Gnobold | model/rig | D65 2/2 runs PASS | KEPT |
| 0x33 | Gnobold | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x33 | Gnobold | action 3 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x33 | Gnobold | action 4 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x33 | Gnobold | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x33 | Gnobold | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x33 | Gnobold | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x33 | Gnobold | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x33 | Gnobold | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x33 | Gnobold | action 54 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x34 | Insectoid | model/rig | D65 2/2 runs PASS | KEPT |
| 0x34 | Insectoid | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x34 | Insectoid | action 3 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x34 | Insectoid | action 4 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x34 | Insectoid | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x34 | Insectoid | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x34 | Insectoid | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x34 | Insectoid | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x34 | Insectoid | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x34 | Insectoid | action 54 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x35 | Hornet | model/rig | D65 2/2 runs PASS | KEPT |
| 0x35 | Hornet | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x35 | Hornet | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x35 | Hornet | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x35 | Hornet | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x35 | Hornet | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x36 | InsectGuard | model/rig | D65 2/2 runs PASS | KEPT |
| 0x36 | InsectGuard | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x36 | InsectGuard | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x36 | InsectGuard | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x36 | InsectGuard | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x36 | InsectGuard | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x37 | Crow | model/rig | D65 2/2 runs PASS | KEPT |
| 0x37 | Crow | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x37 | Crow | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x37 | Crow | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x37 | Crow | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x37 | Crow | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x38 | Chicken | model/rig | D65 2/2 runs PASS | KEPT |
| 0x38 | Chicken | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x38 | Chicken | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x38 | Chicken | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x38 | Chicken | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x38 | Chicken | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x39 | Seagull | model/rig | D65 2/2 runs PASS | KEPT |
| 0x39 | Seagull | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x39 | Seagull | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x39 | Seagull | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x39 | Seagull | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x39 | Seagull | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x3a | Parrot | model/rig | D65 2/2 runs PASS | KEPT |
| 0x3a | Parrot | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x3a | Parrot | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x3a | Parrot | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x3a | Parrot | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x3a | Parrot | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x3b | Bat | model/rig | D65 2/2 runs PASS | KEPT |
| 0x3b | Bat | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x3b | Bat | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x3b | Bat | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x3b | Bat | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x3b | Bat | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x3c | Fly | model/rig | D65 2/2 runs PASS | KEPT |
| 0x3c | Fly | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x3c | Fly | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x3c | Fly | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x3c | Fly | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x3c | Fly | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x3d | Midge | model/rig | D65 2/2 runs PASS | KEPT |
| 0x3d | Midge | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x3d | Midge | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x3d | Midge | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x3d | Midge | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x3d | Midge | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x3e | Mosquito | model/rig | D65 2/2 runs PASS | KEPT |
| 0x3e | Mosquito | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x3e | Mosquito | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x3e | Mosquito | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x3e | Mosquito | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x3e | Mosquito | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x3f | PlainRunner | model/rig | D65 6/6 runs PASS | KEPT |
| 0x3f | PlainRunner | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x3f | PlainRunner | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x40 | LeafRunner | model/rig | D65 2/2 runs PASS | KEPT |
| 0x40 | LeafRunner | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x40 | LeafRunner | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x41 | SnowRunner | model/rig | D65 2/2 runs PASS | KEPT |
| 0x41 | SnowRunner | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x41 | SnowRunner | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x42 | DesertRunner | model/rig | D65 2/2 runs PASS | KEPT |
| 0x42 | DesertRunner | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x42 | DesertRunner | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x43 | Peacock | model/rig | D65 2/2 runs PASS | KEPT |
| 0x43 | Peacock | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x43 | Peacock | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x43 | Peacock | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x43 | Peacock | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x43 | Peacock | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x43 | Peacock | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x44 | Frog | model/rig | D65 2/2 runs PASS | KEPT |
| 0x44 | Frog | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x44 | Frog | action 3 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x44 | Frog | action 4 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x44 | Frog | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x44 | Frog | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x44 | Frog | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x44 | Frog | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x44 | Frog | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x44 | Frog | action 54 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x45 | PlantCreature | model/rig | D65 2/2 runs PASS | KEPT |
| 0x45 | PlantCreature | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x45 | PlantCreature | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x45 | PlantCreature | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x45 | PlantCreature | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x45 | PlantCreature | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x45 | PlantCreature | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x46 | RadishCreature | model/rig | D65 2/2 runs PASS | KEPT |
| 0x46 | RadishCreature | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x46 | RadishCreature | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x46 | RadishCreature | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x46 | RadishCreature | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x46 | RadishCreature | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x46 | RadishCreature | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x47 | Onionling | model/rig | D65 2/2 runs PASS | KEPT |
| 0x47 | Onionling | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x47 | Onionling | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x47 | Onionling | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x47 | Onionling | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x47 | Onionling | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x47 | Onionling | action 75 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x48 | DesertOnionling | model/rig | D65 2/2 runs PASS | KEPT |
| 0x48 | DesertOnionling | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x48 | DesertOnionling | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x48 | DesertOnionling | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x48 | DesertOnionling | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x48 | DesertOnionling | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x48 | DesertOnionling | action 75 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x49 | Devourer | model/rig | D65 2/2 runs PASS | KEPT |
| 0x49 | Devourer | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x49 | Devourer | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x49 | Devourer | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x49 | Devourer | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x49 | Devourer | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x49 | Devourer | action 75 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x4a | Duckbill | model/rig | D65 2/2 runs PASS | KEPT |
| 0x4a | Duckbill | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x4a | Duckbill | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x4a | Duckbill | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x4a | Duckbill | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x4a | Duckbill | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x4a | Duckbill | action 75 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x4b | Crocodile | model/rig | D65 2/2 runs PASS | KEPT |
| 0x4b | Crocodile | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x4b | Crocodile | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x4b | Crocodile | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x4b | Crocodile | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x4b | Crocodile | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x4b | Crocodile | action 75 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x4c | SpikeCreature | model/rig | D65 4/4 runs PASS | KEPT |
| 0x4c | SpikeCreature | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x4c | SpikeCreature | action 3 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x4c | SpikeCreature | action 4 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x4c | SpikeCreature | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x4c | SpikeCreature | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x4c | SpikeCreature | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x4c | SpikeCreature | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x4c | SpikeCreature | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x4c | SpikeCreature | action 54 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x4d | Anubis | model/rig | D65 2/2 runs PASS | KEPT |
| 0x4d | Anubis | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x4d | Anubis | action 3 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x4d | Anubis | action 4 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x4d | Anubis | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x4d | Anubis | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x4d | Anubis | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x4d | Anubis | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x4d | Anubis | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x4d | Anubis | action 54 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x4e | Horus | model/rig | D65 2/2 runs PASS | KEPT |
| 0x4e | Horus | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x4e | Horus | action 3 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x4e | Horus | action 4 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x4e | Horus | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x4e | Horus | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x4e | Horus | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x4e | Horus | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x4e | Horus | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x4e | Horus | action 54 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x4f | Jester | model/rig | D65 2/2 runs PASS | KEPT |
| 0x4f | Jester | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x4f | Jester | action 3 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x4f | Jester | action 4 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x4f | Jester | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x4f | Jester | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x4f | Jester | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x4f | Jester | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x4f | Jester | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x4f | Jester | action 54 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x50 | Spectrino | model/rig | D65 2/2 runs PASS | KEPT |
| 0x50 | Spectrino | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x50 | Spectrino | action 3 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x50 | Spectrino | action 4 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x50 | Spectrino | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x50 | Spectrino | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x50 | Spectrino | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x50 | Spectrino | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x50 | Spectrino | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x50 | Spectrino | action 54 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x51 | Djinn | model/rig | D65 2/2 runs PASS | KEPT |
| 0x51 | Djinn | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x51 | Djinn | action 3 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x51 | Djinn | action 4 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x51 | Djinn | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x51 | Djinn | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x51 | Djinn | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x51 | Djinn | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x51 | Djinn | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x51 | Djinn | action 54 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x52 | Minotaur | model/rig | D65 2/2 runs PASS | KEPT |
| 0x52 | Minotaur | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x52 | Minotaur | action 3 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x52 | Minotaur | action 4 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x52 | Minotaur | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x52 | Minotaur | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x52 | Minotaur | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x52 | Minotaur | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x52 | Minotaur | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x52 | Minotaur | action 54 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x53 | NomadMale | model/rig | D65 2/2 runs PASS | KEPT |
| 0x53 | NomadMale | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x53 | NomadMale | action 3 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x53 | NomadMale | action 4 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x53 | NomadMale | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x53 | NomadMale | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x53 | NomadMale | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x53 | NomadMale | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x53 | NomadMale | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x53 | NomadMale | action 54 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x54 | NomadFemale | model/rig | D65 2/2 runs PASS | KEPT |
| 0x54 | NomadFemale | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x54 | NomadFemale | action 3 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x54 | NomadFemale | action 4 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x54 | NomadFemale | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x54 | NomadFemale | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x54 | NomadFemale | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x54 | NomadFemale | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x54 | NomadFemale | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x54 | NomadFemale | action 54 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x55 | Imp | model/rig | D65 2/2 runs PASS | KEPT |
| 0x55 | Imp | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x55 | Imp | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x55 | Imp | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x55 | Imp | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x55 | Imp | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x55 | Imp | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x56 | Spitter | model/rig | D65 2/2 runs PASS | KEPT |
| 0x56 | Spitter | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x56 | Spitter | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x56 | Spitter | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x56 | Spitter | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x56 | Spitter | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x57 | Mole | model/rig | D65 2/2 runs PASS | KEPT |
| 0x57 | Mole | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x57 | Mole | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x57 | Mole | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x57 | Mole | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x57 | Mole | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x57 | Mole | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x58 | Biter | model/rig | D65 2/2 runs PASS | KEPT |
| 0x58 | Biter | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x58 | Biter | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x58 | Biter | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x58 | Biter | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x58 | Biter | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x58 | Biter | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x59 | Koala | model/rig | D65 2/2 runs PASS | KEPT |
| 0x59 | Koala | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x59 | Koala | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x59 | Koala | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x59 | Koala | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x59 | Koala | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x59 | Koala | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x5a | Squirrel | model/rig | D65 2/2 runs PASS | KEPT |
| 0x5a | Squirrel | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x5a | Squirrel | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x5a | Squirrel | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x5a | Squirrel | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x5a | Squirrel | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x5a | Squirrel | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x5b | Raccoon | model/rig | D65 2/2 runs PASS | KEPT |
| 0x5b | Raccoon | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x5b | Raccoon | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x5b | Raccoon | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x5b | Raccoon | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x5b | Raccoon | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x5b | Raccoon | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x5c | Owl | model/rig | D65 2/2 runs PASS | KEPT |
| 0x5c | Owl | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x5c | Owl | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x5c | Owl | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x5c | Owl | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x5c | Owl | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x5c | Owl | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x5d | Penguin | model/rig | D65 2/2 runs PASS | KEPT |
| 0x5d | Penguin | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x5d | Penguin | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x5d | Penguin | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x5d | Penguin | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x5d | Penguin | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x5d | Penguin | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x5e | Werewolf | model/rig | D65 2/2 runs PASS | KEPT |
| 0x5e | Werewolf | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x5e | Werewolf | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x5e | Werewolf | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x5e | Werewolf | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x5e | Werewolf | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x5e | Werewolf | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x5f | ? | model/rig | D65 2/2 runs PASS | KEPT |
| 0x5f | ? | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x5f | ? | action 3 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x5f | ? | action 4 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x5f | ? | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x5f | ? | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x5f | ? | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x5f | ? | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x5f | ? | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x5f | ? | action 54 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x60 | Zombie | model/rig | D65 2/2 runs PASS | KEPT |
| 0x60 | Zombie | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x60 | Zombie | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x60 | Zombie | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x60 | Zombie | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x60 | Zombie | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x60 | Zombie | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x61 | Vampire | model/rig | D65 2/2 runs PASS | KEPT |
| 0x61 | Vampire | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x61 | Vampire | action 3 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x61 | Vampire | action 4 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x61 | Vampire | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x61 | Vampire | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x61 | Vampire | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x61 | Vampire | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x61 | Vampire | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x61 | Vampire | action 54 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x62 | Horse | model/rig | D65 2/2 runs PASS | KEPT |
| 0x62 | Horse | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x62 | Horse | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x62 | Horse | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x62 | Horse | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x62 | Horse | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x62 | Horse | action 75 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x63 | Camel | model/rig | D65 2/2 runs PASS | KEPT |
| 0x63 | Camel | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x63 | Camel | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x63 | Camel | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x63 | Camel | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x63 | Camel | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x63 | Camel | action 75 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x64 | Cow | model/rig | D65 2/2 runs PASS | KEPT |
| 0x64 | Cow | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x64 | Cow | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x64 | Cow | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x64 | Cow | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x64 | Cow | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x64 | Cow | action 75 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x65 | Dragon | wings | no wing part | KEPT |
| 0x65 | Dragon | walk/idle | parametric walk formula = executed drawModel (D64/D65) | FIXED |
| 0x65 | Dragon | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | FIXED |
| 0x65 | Dragon | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | FIXED |
| 0x65 | Dragon | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | FIXED |
| 0x65 | Dragon | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | FIXED |
| 0x66 | BarkBeetle | model/rig | D65 2/2 runs PASS | KEPT |
| 0x66 | BarkBeetle | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x66 | BarkBeetle | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x66 | BarkBeetle | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x66 | BarkBeetle | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x66 | BarkBeetle | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x66 | BarkBeetle | action 75 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x67 | FireBeetle | model/rig | D65 2/2 runs PASS | KEPT |
| 0x67 | FireBeetle | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x67 | FireBeetle | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x67 | FireBeetle | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x67 | FireBeetle | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x67 | FireBeetle | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x67 | FireBeetle | action 75 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x68 | SnoutBeetle | model/rig | D65 2/2 runs PASS | KEPT |
| 0x68 | SnoutBeetle | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x68 | SnoutBeetle | action 22 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x68 | SnoutBeetle | action 25 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x68 | SnoutBeetle | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x68 | SnoutBeetle | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x68 | SnoutBeetle | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x68 | SnoutBeetle | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x68 | SnoutBeetle | action 55 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x69 | LemonBeetle | model/rig | D65 2/2 runs PASS | KEPT |
| 0x69 | LemonBeetle | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x69 | LemonBeetle | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x69 | LemonBeetle | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x69 | LemonBeetle | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x69 | LemonBeetle | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x69 | LemonBeetle | action 75 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x6a | Crab | model/rig | D65 2/2 runs PASS | KEPT |
| 0x6a | Crab | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x6a | Crab | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x6a | Crab | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x6a | Crab | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x6a | Crab | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x6a | Crab | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x6b | SeaCrab | model/rig | D65 2/2 runs PASS | KEPT |
| 0x6b | SeaCrab | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x6b | SeaCrab | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x6b | SeaCrab | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x6b | SeaCrab | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x6b | SeaCrab | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x6b | SeaCrab | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x6c | Troll | model/rig | D65 2/2 runs PASS | KEPT |
| 0x6c | Troll | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x6c | Troll | action 3 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x6c | Troll | action 4 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x6c | Troll | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x6c | Troll | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x6c | Troll | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x6c | Troll | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x6c | Troll | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x6c | Troll | action 54 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x6d | DarkTroll | model/rig | D65 2/2 runs PASS | KEPT |
| 0x6d | DarkTroll | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x6d | DarkTroll | action 3 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x6d | DarkTroll | action 4 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x6d | DarkTroll | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x6d | DarkTroll | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x6d | DarkTroll | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x6d | DarkTroll | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x6d | DarkTroll | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x6d | DarkTroll | action 54 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x6e | HellDemon | model/rig | D65 2/2 runs PASS | KEPT |
| 0x6e | HellDemon | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x6e | HellDemon | action 3 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x6e | HellDemon | action 4 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x6e | HellDemon | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x6e | HellDemon | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x6e | HellDemon | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x6e | HellDemon | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x6e | HellDemon | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x6e | HellDemon | action 54 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x6f | Golem | model/rig | D65 2/2 runs PASS | KEPT |
| 0x6f | Golem | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x6f | Golem | action 54 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x6f | Golem | action 76 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x70 | EmberGolem | model/rig | D65 2/2 runs PASS | KEPT |
| 0x70 | EmberGolem | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x70 | EmberGolem | action 54 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x70 | EmberGolem | action 76 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x71 | SnowGolem | model/rig | D65 2/2 runs PASS | KEPT |
| 0x71 | SnowGolem | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x71 | SnowGolem | action 54 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x71 | SnowGolem | action 76 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x72 | Yeti | model/rig | D65 2/2 runs PASS | KEPT |
| 0x72 | Yeti | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x72 | Yeti | action 3 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x72 | Yeti | action 4 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x72 | Yeti | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x72 | Yeti | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x72 | Yeti | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x72 | Yeti | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x72 | Yeti | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x72 | Yeti | action 54 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x73 | Cyclops | model/rig | D65 2/2 runs PASS | KEPT |
| 0x73 | Cyclops | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x73 | Cyclops | action 3 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x73 | Cyclops | action 4 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x73 | Cyclops | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x73 | Cyclops | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x73 | Cyclops | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x73 | Cyclops | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x73 | Cyclops | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x73 | Cyclops | action 54 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x74 | Mammoth | model/rig | D65 2/2 runs PASS | KEPT |
| 0x74 | Mammoth | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x74 | Mammoth | action 69 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x74 | Mammoth | action 73 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x74 | Mammoth | action 74 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x75 | Lich | model/rig | D65 2/2 runs PASS | KEPT |
| 0x75 | Lich | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x75 | Lich | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x75 | Lich | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x75 | Lich | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x75 | Lich | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x76 | RuneGiant | model/rig | D65 1/1 runs PASS | KEPT |
| 0x76 | RuneGiant | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x77 | Saurian | model/rig | D65 2/2 runs PASS | KEPT |
| 0x77 | Saurian | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x77 | Saurian | action 70 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x77 | Saurian | action 75 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x78 | Bush | model/rig | D65 2/2 runs PASS | KEPT |
| 0x78 | Bush | idle | standing pose (formula amp 0) | KEPT |
| 0x79 | SnowBush | model/rig | D65 2/2 runs PASS | KEPT |
| 0x79 | SnowBush | idle | standing pose (formula amp 0) | KEPT |
| 0x7a | SnowBerryBush | model/rig | D65 2/2 runs PASS | KEPT |
| 0x7a | SnowBerryBush | idle | standing pose (formula amp 0) | KEPT |
| 0x7b | CottonPlant | model/rig | D65 2/2 runs PASS | KEPT |
| 0x7b | CottonPlant | idle | standing pose (formula amp 0) | KEPT |
| 0x7c | Scrub | model/rig | D65 2/2 runs PASS | KEPT |
| 0x7c | Scrub | idle | standing pose (formula amp 0) | KEPT |
| 0x7d | CobwebScrub | model/rig | D65 2/2 runs PASS | KEPT |
| 0x7d | CobwebScrub | idle | standing pose (formula amp 0) | KEPT |
| 0x7e | FireScrub | model/rig | D65 2/2 runs PASS | KEPT |
| 0x7e | FireScrub | idle | standing pose (formula amp 0) | KEPT |
| 0x7f | Ginseng | model/rig | D65 2/2 runs PASS | KEPT |
| 0x7f | Ginseng | idle | standing pose (formula amp 0) | KEPT |
| 0x80 | Cactus | model/rig | D65 2/2 runs PASS | KEPT |
| 0x80 | Cactus | idle | standing pose (formula amp 0) | KEPT |
| 0x81 | ? | model/rig | D65 2/2 runs PASS | KEPT |
| 0x81 | ? | idle | standing pose (formula amp 0) | KEPT |
| 0x82 | ThornTree | model/rig | D65 2/2 runs PASS | KEPT |
| 0x82 | ThornTree | idle | standing pose (formula amp 0) | KEPT |
| 0x83 | GoldDeposit | model/rig | D65 2/2 runs PASS | KEPT |
| 0x83 | GoldDeposit | idle | standing pose (formula amp 0) | KEPT |
| 0x84 | IronDeposit | model/rig | D65 2/2 runs PASS | KEPT |
| 0x84 | IronDeposit | idle | standing pose (formula amp 0) | KEPT |
| 0x85 | SilverDeposit | model/rig | D65 2/2 runs PASS | KEPT |
| 0x85 | SilverDeposit | idle | standing pose (formula amp 0) | KEPT |
| 0x86 | SandstoneDeposit | model/rig | D65 2/2 runs PASS | KEPT |
| 0x86 | SandstoneDeposit | idle | standing pose (formula amp 0) | KEPT |
| 0x87 | EmeraldDeposit | model/rig | D65 2/2 runs PASS | KEPT |
| 0x87 | EmeraldDeposit | idle | standing pose (formula amp 0) | KEPT |
| 0x88 | SapphireDeposit | model/rig | D65 2/2 runs PASS | KEPT |
| 0x88 | SapphireDeposit | idle | standing pose (formula amp 0) | KEPT |
| 0x89 | RubyDeposit | model/rig | D65 2/2 runs PASS | KEPT |
| 0x89 | RubyDeposit | idle | standing pose (formula amp 0) | KEPT |
| 0x8a | DiamondDeposit | model/rig | D65 2/2 runs PASS | KEPT |
| 0x8a | DiamondDeposit | idle | standing pose (formula amp 0) | KEPT |
| 0x8b | IceCrystalDeposit | model/rig | D65 2/2 runs PASS | KEPT |
| 0x8b | IceCrystalDeposit | idle | standing pose (formula amp 0) | KEPT |
| 0x8c | Scarecrow | model/rig | D65 2/2 runs PASS | KEPT |
| 0x8c | Scarecrow | idle | standing pose (formula amp 0) | KEPT |
| 0x8d | Aim | model/rig | D65 2/2 runs PASS | KEPT |
| 0x8d | Aim | idle | standing pose (formula amp 0) | KEPT |
| 0x8e | Dummy | model/rig | D65 2/2 runs PASS | KEPT |
| 0x8e | Dummy | idle | standing pose (formula amp 0) | KEPT |
| 0x8f | Vase | model/rig | D65 2/2 runs PASS | KEPT |
| 0x8f | Vase | idle | standing pose (formula amp 0) | KEPT |
| 0x90 | Bomb | model/rig | D65 2/2 runs PASS | KEPT |
| 0x90 | Bomb | idle | standing pose (formula amp 0) | KEPT |
| 0x91 | SapphireFish | model/rig | D65 2/2 runs PASS | KEPT |
| 0x91 | SapphireFish | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x91 | SapphireFish | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x91 | SapphireFish | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x91 | SapphireFish | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x91 | SapphireFish | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x91 | SapphireFish | action 75 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x92 | LemonFish | model/rig | D65 2/2 runs PASS | KEPT |
| 0x92 | LemonFish | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x92 | LemonFish | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x92 | LemonFish | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x92 | LemonFish | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x92 | LemonFish | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x92 | LemonFish | action 75 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x93 | Seahorse | model/rig | D65 2/2 runs PASS | KEPT |
| 0x93 | Seahorse | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x93 | Seahorse | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x93 | Seahorse | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x93 | Seahorse | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x93 | Seahorse | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x93 | Seahorse | action 75 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x94 | Mermaid | model/rig | D65 4/4 runs PASS | KEPT |
| 0x94 | Mermaid | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x94 | Mermaid | action 3 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x94 | Mermaid | action 4 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x94 | Mermaid | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x94 | Mermaid | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x94 | Mermaid | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x94 | Mermaid | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x94 | Mermaid | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x94 | Mermaid | action 54 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x95 | Merman | model/rig | D65 4/4 runs PASS | KEPT |
| 0x95 | Merman | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x95 | Merman | action 3 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x95 | Merman | action 4 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x95 | Merman | action 6 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x95 | Merman | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x95 | Merman | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x95 | Merman | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x95 | Merman | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x95 | Merman | action 54 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x96 | Shark | model/rig | D65 2/2 runs PASS | KEPT |
| 0x96 | Shark | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x96 | Shark | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x96 | Shark | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x96 | Shark | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x96 | Shark | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x96 | Shark | action 75 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x97 | Bumblebee | model/rig | D65 2/2 runs PASS | KEPT |
| 0x97 | Bumblebee | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x97 | Bumblebee | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x97 | Bumblebee | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x97 | Bumblebee | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x97 | Bumblebee | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x98 | LanternFish | model/rig | D65 2/2 runs PASS | KEPT |
| 0x98 | LanternFish | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x98 | LanternFish | action 75 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x99 | MawFish | model/rig | D65 2/2 runs PASS | KEPT |
| 0x99 | MawFish | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x99 | MawFish | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x99 | MawFish | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x99 | MawFish | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x99 | MawFish | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x99 | MawFish | action 75 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x9a | Piranha | model/rig | D65 2/2 runs PASS | KEPT |
| 0x9a | Piranha | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x9a | Piranha | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x9a | Piranha | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x9a | Piranha | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x9a | Piranha | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x9a | Piranha | action 75 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x9b | Blowfish | model/rig | D65 2/2 runs PASS | KEPT |
| 0x9b | Blowfish | walk/idle | parametric walk formula = executed drawModel (D64/D65) | KEPT |
| 0x9b | Blowfish | action 37 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x9b | Blowfish | action 40 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x9b | Blowfish | action 42 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x9b | Blowfish | action 43 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
| 0x9b | Blowfish | action 75 | EMULATED_ALPHA_CODE clip, played by the Unity controller | TRANSFERRED |
