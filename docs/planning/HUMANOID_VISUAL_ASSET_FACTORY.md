# FACTORIA — UNIVERSAL HUMANOID VISUAL ASSET FACTORY

**Status:** ARCHITECTURE APPROVED IN PRINCIPLE / IMPLEMENTATION PLANNED  
**Date:** 2026-11-18  
**Repository:** alfuwerce-art/factoria, branch factory/work  
**Isolation:** D:\Projects\CubeGame Factory; never modify CubeGame or Unity without separate authorization.

## Goal

Build a reproducible generator of ALL categories of visual humanoid assets, not a collection of one-off character pictures. One system accepts an asset recipe, generates an approved-style image, converts it to an independent 3D module where necessary, adapts its scale and sockets to an approved modular mannequin, validates outputs, and registers the result in a reusable library.

This is the **full long-term scope**, not authorization to run a massive production batch. Follow AGENTS.md PREP restrictions and explicit exceptions. Actual implementation must proceed through independent pilots.

## Product taxonomy (extensible)

1. BODY_BASE: race/sex/archetype mannequin, shared skeleton and rig.
2. FACE_MODULE: face/eyes/brows/nose/jaw/mouth and expression data; pixel or geometry as appropriate; reusable face carrier.
3. HAIR_MODULE: hairstyle, beard, moustache; geometry separated from color palette.
4. RACE_FEATURE: ears, tusks, horns, crests, etc.; race-specific sockets.
5. CLOTHING: chest, undershirt, coat, sleeves, waist, legs, skirts, feet, hands, cloak/back.
6. ARMOR: helmet, pauldrons, torso, bracers, gloves, hip armor, greaves, boots.
7. WEAPON: one-handed and two-handed swords, axes, maces, daggers, spears, bows, staves, wands, shields, etc.; attach to weapon grip packages.
8. ACCESSORY: belts, pouches, bags, jewellery, quivers, back accessories.
9. PALETTE_MATERIAL: skin, hair, fabric, leather, metal, wood, pixel palettes/texture variation (not a distinct mesh for every color).
10. CHARACTER_RECIPE: composition manifest rather than a separately baked model for each NPC.

Reuse the already audited Alpha character-editor catalog as a reference to coverage (race/sex, faces, hairstyles, colors), without publishing proprietary Alpha assets. Keep agreed exclusion and race-specific option counts. Do not replace catalog facts with invented counts.

## Architecture / contracts

- Canonical approved style: Human Male/Female MANNEQUIN V3, with a plain square head, no hair-shaped rim, small modular ears, stepped chin, short body, shoulder width greater than narrow pelvis.
- Separate BODY SHAPE from APPEARANCE and GEAR. Female/male and race variations are profiles, not independent generators.
- Each asset descriptor: asset_id, type, version, source / license, race / sex compatibility, base rig archetype, required sockets, scale units, reference ids and SHA-256, prompt/seed/graph SHA-256, generated preview paths, geometry paths, palette references, bone attachments/weights, validation status.
- Each output must support independent replacement and compositing. No merged clothing/weapon/skin geometry by default.
- RGB alone is not sufficient to certify geometrical compatibility. 2D generated images are candidates, not production 3D assets.
- No one mesh per palette color, no one model per random NPC. Deterministic NPC recipe selects from approved asset variants subject to race/culture/faction/role constraints.
- A humanoid rig skeleton is shared by compatible characters; weights and offsets must be fitted per module. Modular voxel pieces should use rigid or carefully controlled skinning; no accidental fused accessories.
- Existing Character Factory compilers and STYLE_LOCK are reusable; do not overwrite frozen workflows.

## Pipeline

1. ASSET_REQUEST: select category, variant, mannequin identity, socket, approved style and constraints.
2. IMAGE_GENERATION: ComfyUI generated isolated views/region only; source/provenance preserved.
3. IMAGE_GATE: verify visual style, perspective, completeness, forbidden artifacts, identity and consistent scale. Rejection -> controlled retry or manual review.
4. GEOMETRY: choose procedural/Blender for simple cuboids and weapons; image-to-3D/Tripo candidate for complex shapes only after measured pilot; neutral geometry validation.
5. GEOMETRY_GATE: cleanup topology, independent parts, manifoldness when needed, consistent UV/palette, volume within socket fit.
6. FITTING: adjust to approved mannequin/socket; test neutral and extreme pose and collision/intersections; preserve original reference and source geometry.
7. RIGGING: assign rigid attachment or skin weights, register hand grips, test animation with existing rig.
8. EXPORT: independently export .blend, .glb, and if useful .fbx, turntable/front/side/back previews, manifest/report.
9. LIBRARY: approved assets enter searchable catalog; random NPC and editor assemble by deterministic recipes, not per-NPC model generation.

## Automation

Use existing ComfyUI graph compilation/API and Blender CLI with a small orchestrator. One category-specific configuration per asset family; avoid creating one monolithic text prompt or a duplicate new AI stack. Maintain versioning, batch manifests, retries, output directories, logs, deterministic seeds and SHA-256.

Proposed CLI contract (not a claimed existing executable):

python scripts/factory_assets/run_asset.py --spec manifests/assets/<asset>.json --stage image|geometry|fit|validate|all

The implementation must verify installed dependencies/Tripo actual availability and licenses; do not assume 3D service availability. Do not download new AI weights, purchase cloud services or change global environment automatically. Existing ral-vxl has production licensing blocker; exclude it from production.

## Stage plan, acceptance gates

**P0 — baseline:** confirm the exact approved V3 source file SHA-256; create a validated 3D mannequin with armature and sockets before modules requiring skinned fit. Do not call an approved image an approved 3D geometry.

**P1 — first vertical slice:** one hair module, one face variant, one clothing top, one pair of trousers, one boots pair, one sword. Demonstrate isolated generation, fitted geometry, socket attachment, rig posing, and saved/reloaded assembly; document which modules fail and why.

**P2 — category expansion:** after one end-to-end PASS, implement category-specific profiles for complete taxonomy. Retain separate treatment of face pixels, rigid weapons, skinned apparel, clipping/hiding, and race features.

**P3 — variation + NPC:** palette expansion, race/sex compatibility tables, seeded assembly randomizer; verify reproducibility and absence of illegal combinations.

**P4 — batch scaling:** approved path 1 -> 2 -> 5 -> 10 -> 25 -> 50 -> batch. Only scale after measuring per-asset correction cost and resolving repeated error sources. No automatic approval on test completion.

## Metrics

- Time / compute / RSS / incremental VRAM by generation step and pipeline total
- Reference matching and rejected visual artifact count
- Correct category and isolated output, correct slot and valid scale
- Distinct mesh objects, socket transforms, deformation under idle/walk/attack test
- Clipping, intersections and missing material/texture/rig dependencies
- Reproducibility (seed/version/hash), license eligibility
- Manual correction minutes per accepted output
- Status: PLANNED / PARTIAL / READY_FOR_OWNER_REVIEW / ACCEPTED only after user

## Constraints

- Current Factoria AGENTS / PREP remains active; this approved production direction does not silently authorize a new tool installation or massive asset-generation batch.
- Only work in Factoria. No edits to CubeGame, Unity, Alpha data or GitHub main.
- The V3 style lock is approved as a visual target but its ComfyUI reconstruction and runtime budgets remain PARTIAL / READY_FOR_OWNER_VISUAL_REVIEW until approved.
- Technical contract comes before volume; the first complete pipeline output demonstrates value.
