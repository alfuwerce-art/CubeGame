# G3 — HUMANOID CHARACTER FACTORY

**Status:** PLANNED / PRODUCER DIRECTION APPROVED  
**Decision:** [D94 — humanoid modular appearance and NPC generation](../decisions/D94-g3-humanoid-modular-appearance.md)  
**Input:** accepted G2/G2-1 world/entity seams; M1 Character / Creature Factory concept  
**Primary reference:** [M1_MODEL_ANIMATION_EVOLUTION_STAGE.md](M1_MODEL_ANIMATION_EVOLUTION_STAGE.md)

## Goal

Build the production path for CubeGame humanoid characters before expanding the non-humanoid creature catalog.

The first G3 objective is not one polished hero and not thousands of prebuilt NPC models.

The objective is:

`all humanoid race mannequins → shared visual module library → one modular assembly path → Character Creator + deterministic NPC randomizer → common rig/animation package → equipment → world integration`.

## Core rule

A generated humanoid NPC is a **recipe**, not a unique authored full model.

```text
Race
+ AppearanceRecipe
+ RigAnimationPackage
+ EquipmentRecipe
= runtime humanoid
```

Player and NPC use the same underlying Factory.

## G3-0 — Humanoid Race Mannequins

Create the base mannequin for every supported humanoid race and each sex/body variation that requires a distinct editor base.

Each mannequin must define:

- race identity;
- compatible Humanoid skeleton topology;
- body proportions;
- modular split boundaries;
- attachment/sockets;
- face/head interface;
- equipment interface;
- neutral production pose;
- scale/pivot/orientation.

Do not add final hairstyles, beard sets, armor sets or world-NPC randomization before the mannequin contract is stable.

### G3-0 PASS

- every supported humanoid race has an accepted mannequin base;
- modular boundaries are compatible with the Factory;
- race proportions do not require a separate runtime technology;
- compatible races can bind to the common Humanoid rig contract;
- mannequins can be previewed and assembled in the same runtime path.

## G3-1 — Character Editor Visual Library

Produce all visual categories required by the Character Creator.

Typical categories:

- face/head variants;
- hairstyles;
- beard/moustache variants where applicable;
- skin/body colors;
- hair colors;
- ears;
- tusks/fangs;
- horns/crests;
- other race-specific traits.

Each race gets a `RaceAppearanceProfile` defining which slots and values are legal.

The module library is the source for both player creation and NPC appearance generation.

### G3-1 rule

Do not author separate NPC-only faces/hair merely to create variety. Add reusable modules to the race library.

## G3-2 — AppearanceRecipe + Modular Assembly

Define the appearance subset of the character recipe.

Conceptual fields:

```text
Race
Sex / variation
BodyProfile
Face
Hair
Beard
SkinColor
HairColor
RaceTraitSlots:
  Ears
  Tusks
  Horns
  Crests
  ...
```

The Factory must reject incompatible race/module combinations.

Both Character Creator and NPC generation must resolve through this same assembly implementation.

## G3-3 — Simple race-based NPC appearance randomizer

The world/population system chooses race and persistent NPC identity.

Appearance generation is intentionally simple:

```text
WorldSeed + PersistentNpcId
        ↓
AppearanceSeed
        ↓
RaceAppearanceProfile
        ↓
random/weighted module choices
        ↓
AppearanceRecipe
```

Requirements:

- deterministic;
- race-safe;
- visually varied;
- no dependence on render distance;
- no reroll after save/load;
- optional slots may use probability;
- no complex personality/biome/faction simulation inside appearance selection.

Profession/faction/level may later affect equipment and behavior, but not the stable face/body appearance by default.

## G3-4 — Humanoid Rig + Base Animations

After the modular body/appearance contract is proven:

- bind compatible humanoids to common Humanoid skeleton topology;
- create/validate weights;
- base locomotion;
- idle;
- walk/run;
- attack foundations;
- dodge;
- hurt/death;
- retargeting;
- IK/procedural corrections;
- body/race correction profiles.

Race variation should preferably come from proportions/corrections and small animation tuning rather than a completely separate animation library per race.

## G3-5 — Equipment / Armor / Weapons

Equipment remains a separate modular layer.

Minimum contract:

- head;
- chest;
- hands;
- legs;
- feet;
- weapon/offhand;
- race/body compatibility;
- sockets/grips;
- hide/replace rules for covered body modules.

Equipment choice is not part of the permanent physical appearance seed.

## G3-6 — World NPC Integration

Bind the completed Factory to the G2-1 entity/world seams.

World population provides:

- persistent NPC ID;
- race;
- gameplay definition/role;
- position/state.

Factory resolves:

- deterministic AppearanceRecipe;
- visual modules;
- rig/animation package;
- equipment binding.

Verify unload/reload and save/load preserve the same character identity.

## Non-humanoid creatures

Do not mass-generate the creature catalog during early G3.

The Creature Factory remains an architectural capability. Actual creatures are produced when required by current regions, enemies, bosses or other content milestones.

Examples:

`region requires wolf → build/validate wolf family`

`dungeon requires spider → build/validate arachnid package`

`boss milestone requires dragon → build that package`

This avoids speculative asset production.

## G3 overall acceptance

G3 humanoid foundation is accepted when:

- all supported humanoid race mannequins exist;
- Character Creator visual categories are available per race;
- player/manual selection and NPC/random selection produce the same AppearanceRecipe type;
- deterministic NPC appearance survives reload;
- common Humanoid rig/animation contract works across accepted race bodies;
- equipment is modular and independent;
- a generated world NPC is assembled through the G2-1 EntityVisualRecipe/EntityDefinition seam;
- no separate full model library is required for every NPC combination.
