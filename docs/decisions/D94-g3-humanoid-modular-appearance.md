# D94 — G3 humanoid modular appearance and NPC generation

**Date:** 2026-11-17
**Status:** APPROVED
**Scope:** G3 Character / Creature Factory production strategy

## Decision

CubeGame does not prebuild a separate complete model for every humanoid NPC.

All supported humanoid races are produced from a shared modular character system:

`Race mannequin + race-specific visual module library + AppearanceRecipe + shared Humanoid rig/animation contract + equipment`.

The player Character Creator and world NPC generation use the **same visual module library and the same assembly path**. They differ only in how module values are selected:

- Player: selects allowed appearance values manually in the Character Creator.
- NPC: receives allowed appearance values from a simple deterministic race-based randomizer.

## Humanoid race mannequins

Before mass NPC production, G3 creates and validates the base mannequin set for every supported humanoid race and the sex/body variation required by the Character Creator.

A race mannequin defines the stable body/anatomy basis for that race. It is not a finished NPC.

Compatible humanoid races should keep one common Humanoid skeleton topology where practical, while race/body proportions may differ.

## RaceAppearanceProfile

Each humanoid race owns a machine-readable set of allowed visual modules and optional probabilities.

Examples of slots:

- body/profile;
- face/head;
- hair;
- beard/moustache where applicable;
- skin/body color;
- hair color;
- ears;
- tusks/fangs;
- horns;
- crests;
- other race-specific visual traits.

Examples of race-specific rules:

- an Orc profile may expose tusks and Orc-only faces/body parts;
- a horned/draconic humanoid profile may expose horn/crest variants;
- a Dwarf profile may expose its own beard/body ranges;
- a race may prohibit a slot entirely.

Exact race catalogs and probability values are content data, not hard-coded generator logic.

## Simple NPC appearance randomizer

NPC appearance generation must remain deliberately simple.

Population/world logic chooses the NPC's race and persistent identity. The appearance layer then performs:

```text
WorldSeed + PersistentNpcId
        ↓
AppearanceSeed
        ↓
RaceAppearanceProfile
        ↓
random/weighted choice for each allowed appearance slot
        ↓
AppearanceRecipe
        ↓
Humanoid Factory
```

The same `WorldSeed + PersistentNpcId` must reproduce the same appearance.

The appearance randomizer does **not** need complex simulation logic. Biome, faction, profession, combat role and level do not alter face/hair/horns/etc. by default. Those systems may separately choose equipment, profession or gameplay state.

## Player and NPC assembly

```text
PLAYER:
Race → Character Creator → AppearanceRecipe
                              ↓
                         Humanoid Factory

NPC:
Race → RaceAppearanceProfile → deterministic randomizer → AppearanceRecipe
                                                       ↓
                                                  Humanoid Factory
```

There is one assembly technology, not separate player/NPC model systems.

## Equipment

Clothing, armor and weapons remain modular layers separate from base identity/appearance.

NPC equipment may later be selected by role, faction, level or loot rules without changing the NPC's stable physical appearance.

## Creature production strategy

The general Creature Factory architecture remains valid, but CubeGame will **not** mass-produce every creature family in advance.

After the humanoid pipeline is proven, non-humanoid creatures are created on demand for actual game content:

`current region/content need → required creature/family → model/rig/animation package → Factory integration`.

The scaling sequence `1 → 2 → 5 → 10 → ...` is a validation technique where useful, not a requirement to manufacture the full creature catalog before gameplay needs it.

## G3 production order

1. **G3-0 — Humanoid Race Mannequins**
   Base mannequins for all supported humanoid races/required sex variations.

2. **G3-1 — Character Editor Visual Library**
   Faces, hair, beards, colors and race-specific traits required by the editor.

3. **G3-2 — AppearanceRecipe + Modular Assembly**
   One data-driven assembly path shared by player and NPC.

4. **G3-3 — Race-based NPC Appearance Randomizer**
   Simple deterministic randomization from `WorldSeed + PersistentNpcId`.

5. **G3-4 — Humanoid Rig + Base Animation Package**
   Shared skeleton topology where compatible; body/race corrections where needed.

6. **G3-5 — Equipment / Armor / Weapon Modules**
   Independent modular attachments.

7. **G3-6 — World NPC Integration**
   World population spawns race + persistent ID; factory produces the visual character recipe.

8. **Creature Factory — on demand**
   Non-humanoid creatures are produced when required by current region/content milestones.

## Consequences

- No separate handcrafted full model is required for every generated humanoid NPC.
- The Character Creator directly proves the same module library used by NPCs.
- NPC variety scales combinatorially from reusable race modules.
- Stable NPC identity survives reload because appearance is deterministic.
- Race-specific traits remain controlled by data.
- Creature production follows real content demand instead of speculative bulk production.
