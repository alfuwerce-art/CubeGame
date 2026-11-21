#!/usr/bin/env python3
"""M1-R0 research extractor: Alpha creature identity <-> Unity model registry.

STATUS: RESEARCH ONLY. Nothing here is M1 production, and nothing here may touch the
Unity project, the canonical M0 branch or the Alpha installation.

What it does (all read-only):
  * Alpha identity  : the in-game display names come from the decompiled name map
                      (Cube.exe World.cpp: World_map_find_or_insert_int + u16string_assignPtrLen),
                      ALPHA_DECOMPILED, display names only;
  * Alpha structure : kind -> appearance block, part slots and voxel sizes from
                      .scratch/catalog/alpha_static.json (ALPHA_CODE_STATIC, exe sha pinned);
  * Alpha behaviour : AI action launches + walk/idle oracle status from the committed
                      evidence JSONs (EMULATED_ALPHA_CODE);
  * Unity side      : the committed kind -> prepared-variant table
                      Assets/CubeGame/Content/Generated/AlphaStagedVisuals.g.cs (UNITY_ASSET_VERIFIED)
                      and the local content cache .scratch/content/actors/<variant> (LOCAL_ONLY);
  * anatomy         : tools.alpha_models.universal.contract.structural_category (structure, never the name).

Output: docs/research/M1_ALPHA_CREATURE_IDENTITY_REGISTRY.csv, one row per Alpha kind (156),
        plus a coverage summary on stdout. No Alpha geometry is written to the repository.

Usage:
    python -m tools.alpha_research.extract_creature_identity_registry
    python tools/alpha_research/extract_creature_identity_registry.py --project . --out docs/research/...
"""
from __future__ import annotations

import argparse
import collections
import csv
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from tools.alpha_models.universal import alpha_static, contract, creature_rig  # noqa: E402
from tools.alpha_models.catalog import d69_catalogs, spawn_roles  # noqa: E402

# The evidence was produced from this exact Cube.exe; a different build invalidates the ids.
EXPECTED_EXE_SHA256 = "84a7a132a84d4282338e7ea45a1940d32066d64e39cbafed8f2418d5a6dc30bf"
KIND_COUNT = 156

# --------------------------------------------------------------------------- classification tables
# Identity families are CubeGame research labels over the ALPHA_DECOMPILED display names and the
# model names of the appearance block. They are NOT Alpha data and NOT anatomy.
IDENTITY_BY_NAME = {
    "HumanMale": "HUMAN", "HumanFemale": "HUMAN", "OldMan": "HUMAN", "Bandit": "HUMAN",
    "Wizard": "HUMAN", "Witch": "HUMAN", "NomadMale": "HUMAN", "NomadFemale": "HUMAN",
    "Jester": "HUMAN",
    "ElfMale": "ELF", "ElfFemale": "ELF",
    "DwarfMale": "DWARF", "DwarfFemale": "DWARF",
    "OrcMale": "ORC", "OrcFemale": "ORC",
    "GoblinMale": "GOBLIN_KIN", "GoblinFemale": "GOBLIN_KIN", "Gnoll": "GOBLIN_KIN",
    "PolarGnoll": "GOBLIN_KIN", "Gnobold": "GOBLIN_KIN",
    "LizardmanMale": "LIZARDMAN", "LizardmanFemale": "LIZARDMAN",
    "FrogmanMale": "FROGMAN", "FrogmanFemale": "FROGMAN",
    "UndeadMale": "UNDEAD_KIN", "UndeadFemale": "UNDEAD_KIN", "Zombie": "UNDEAD_KIN",
    "Skeleton": "UNDEAD_KIN", "Vampire": "UNDEAD_KIN", "Lich": "UNDEAD_KIN",
    "Ogre": "OGREKIN", "Troll": "OGREKIN", "DarkTroll": "OGREKIN", "Yeti": "OGREKIN",
    "Cyclops": "OGREKIN", "Minotaur": "OGREKIN", "RuneGiant": "OGREKIN",
    "Golem": "CONSTRUCT", "EmberGolem": "CONSTRUCT", "SnowGolem": "CONSTRUCT", "Rockling": "CONSTRUCT",
    "HellDemon": "DEMON", "Imp": "DEMON", "Djinn": "DEMON", "Spectrino": "SPECTRAL",
    "Frightener": "SPECTRAL", "SandHorror": "SPECTRAL",
    "Anubis": "DESERT_DEITY", "Horus": "DESERT_DEITY",
    "Collie": "CANINE", "ShepherdDog": "CANINE", "Terrier": "CANINE", "ScottishTerrier": "CANINE",
    "Wolf": "CANINE", "Werewolf": "CANINE", "Bullterrier": "CANINE",
    "Cat": "FELINE", "BrownCat": "FELINE", "WhiteCat": "FELINE", "Panther": "FELINE",
    "Alpaca": "HOOFED_BEAST", "BrownAlpaca": "HOOFED_BEAST", "Pig": "HOOFED_BEAST",
    "Sheep": "HOOFED_BEAST", "Cow": "HOOFED_BEAST", "Horse": "HOOFED_BEAST",
    "Camel": "HOOFED_BEAST", "Mammoth": "HOOFED_BEAST", "SkullBull": "HOOFED_BEAST",
    "Bunny": "SMALL_BEAST", "Porcupine": "SMALL_BEAST", "Squirrel": "SMALL_BEAST",
    "Raccoon": "SMALL_BEAST", "Koala": "SMALL_BEAST", "Monkey": "SMALL_BEAST",
    "Mole": "SMALL_BEAST", "Biter": "SMALL_BEAST", "Duckbill": "SMALL_BEAST",
    "Crab": "CRUSTACEAN", "SeaCrab": "CRUSTACEAN",
    "Crocodile": "REPTILE", "Turtle": "REPTILE", "Saurian": "REPTILE",
    "SpikeCreature": "MONSTER", "Insectoid": "MONSTER",
    "Chicken": "BIRD", "Crow": "BIRD", "Seagull": "BIRD", "Parrot": "BIRD", "Owl": "BIRD",
    "Penguin": "BIRD", "Peacock": "BIRD", "Bat": "BAT",
    "PlainRunner": "RUNNER_BIRD", "LeafRunner": "RUNNER_BIRD", "SnowRunner": "RUNNER_BIRD",
    "DesertRunner": "RUNNER_BIRD",
    "Hornet": "INSECT", "InsectGuard": "INSECT", "Bumblebee": "INSECT", "Fly": "INSECT",
    "Midge": "INSECT", "Mosquito": "INSECT", "BarkBeetle": "INSECT", "FireBeetle": "INSECT",
    "SnoutBeetle": "INSECT", "LemonBeetle": "INSECT",
    "GreenSlime": "SLIME", "PinkSlime": "SLIME", "YellowSlime": "SLIME", "BlueSlime": "SLIME",
    "Frog": "AMPHIBIAN",
    "Dragon": "DRAGON",
    "PlantCreature": "PLANT_MONSTER", "RadishCreature": "PLANT_MONSTER", "Onionling": "PLANT_MONSTER",
    "DesertOnionling": "PLANT_MONSTER", "Devourer": "PLANT_MONSTER",
    "SapphireFish": "FISH", "LemonFish": "FISH", "Seahorse": "FISH", "Shark": "FISH",
    "LanternFish": "FISH", "MawFish": "FISH", "Piranha": "FISH", "Blowfish": "FISH",
    "Spitter": "FISH",
    "Mermaid": "MERFOLK", "Merman": "MERFOLK",
    "Bush": "VEGETATION", "SnowBush": "VEGETATION", "SnowBerryBush": "VEGETATION",
    "CottonPlant": "VEGETATION", "Scrub": "VEGETATION", "CobwebScrub": "VEGETATION",
    "FireScrub": "VEGETATION", "Ginseng": "VEGETATION", "Cactus": "VEGETATION",
    "ThornTree": "TREE", "GoldDeposit": "ORE_DEPOSIT", "IronDeposit": "ORE_DEPOSIT",
    "SilverDeposit": "ORE_DEPOSIT", "SandstoneDeposit": "ORE_DEPOSIT", "EmeraldDeposit": "ORE_DEPOSIT",
    "SapphireDeposit": "ORE_DEPOSIT", "RubyDeposit": "ORE_DEPOSIT", "DiamondDeposit": "ORE_DEPOSIT",
    "IceCrystalDeposit": "ORE_DEPOSIT",
    "Scarecrow": "TRAINING_TARGET", "Aim": "TRAINING_TARGET", "Dummy": "TRAINING_TARGET",
    "Vase": "BREAKABLE_OBJECT", "Bomb": "EXPLOSIVE_OBJECT", "Egg": "EGG_OBJECT",
}

# Anatomy archetype proposal per anatomy class (see the family matrix for the reasoning).
RIG_BY_ANATOMY = {
    "BIPED": "CG_Humanoid_v1",
    "BIPED_SPLIT_ARMS": "CG_Humanoid_v1 + optional elbow blocks",
    "QUADRUPED": "CG_Quadruped_v1",
    "WINGED_BIPED": "CG_Humanoid_v1 + wing attachment",
    "WINGED_LEGS": "CG_Avian_v1",
    "WINGED_HEAD_BODY": "CG_AvianHeadBody_v1",
    "LEGS_ONLY": "CG_Biped_LegsOnly_v1 (avian/runner candidate)",
    "HEAD_BODY_LEGS": "CG_AvianHeadBody_v1",
    "FORE_LIMBS_ONLY": "CG_TorsoArms_v1",
    "LIMBLESS_BODY": "CG_Limbless_v1",
    "LIMBLESS_HEAD": "CG_HeadOnly_Floating_v1",
    "STATIC_OBJECT": "CG_StaticObject_v1 (no character rig)",
}

INTELLIGENCE_BY_RULE = {
    "PASSIVE": "PASSIVE_OBJECT_NO_BEHAVIOUR",
    "TOWN_NPC": "SAPIENT_TOWN_NPC",
    "HUMANOID": "SAPIENT_CULTURE_INFERRED",
    "HUMANOID_MONSTER": "CREATURE_HUMANOID_INFERRED",
    "BEAST": "CREATURE_BEAST_INFERRED",
    "MONSTER": "CREATURE_MONSTER_INFERRED",
    "UNKNOWN": "UNKNOWN",
}

# Motion personality proposals are grouped by identity family, not written per kind.
MOTION_BY_FAMILY = {
    "HUMAN": "shared humanoid base; neutral upright stance, measured walk, shoulder-led turns",
    "ELF": "lighter frame, longer stride, high head carriage, minimal torso sway",
    "DWARF": "short heavy stride, wide stance, low centre of mass, deliberate turns",
    "ORC": "forward-leaning heavy gait, wide arm swing, planted turns",
    "GOBLIN_KIN": "quick shuffling cadence, hunched shoulders, twitchy head turns",
    "LIZARDMAN": "long low strides, stiff tail-less pelvis, head thrust forward",
    "FROGMAN": "wide splayed feet, hopping cadence, deep knee bend",
    "UNDEAD_KIN": "asymmetric dragging gait, dropped shoulders, slow recovery",
    "OGREKIN": "mass-driven slow cadence with foot slam, arm-swing carry",
    "CONSTRUCT": "blocky mechanical steps, no torso bob, abrupt stops",
    "DEMON": "predatory stalk, weight on the balls of the feet, snap turns",
    "SPECTRAL": "no ground contact; hover drift with trailing limbs",
    "DESERT_DEITY": "ritual upright posture, wide elbow carry, slow deliberate head turns",
    "CANINE": "quadruped trot, low head, tail counter-swing",
    "FELINE": "silent low crouch walk, long spine flex, tail balance",
    "HOOFED_BEAST": "four-beat walk, heavy hind drive, head bob",
    "SMALL_BEAST": "fast short-legged scamper, hop component",
    "CRUSTACEAN": "sideways crab scuttle, wide leg base",
    "REPTILE": "belly-low crawl / short-legged waddle, tail drag",
    "MONSTER": "irregular heavy lurch, asymmetrical limb timing",
    "BIRD": "head-bob ground walk, wing flare on turns, hop takeoff",
    "BAT": "hanging crouch, wing-first launch",
    "RUNNER_BIRD": "long-legged run cycle, neck stabilised, wing-less balance arms",
    "INSECT": "high-frequency leg cycle, minimal body bob, darting turns",
    "SLIME": "squash-and-stretch bounce locomotion, no limbs",
    "PLANT_MONSTER": "rooted sway blended with short heavy steps",
    "FISH": "no ground gait; swim undulation and hover-idle",
    "MERFOLK": "no ground gait; upright torso drift, arm-led propulsion",
    "AMPHIBIAN": "wide frog stance, hop-and-settle cadence, deep knee compression",
    "DRAGON": "heavy quadruped prowl, neck-led turns, wingless silhouette",
    "UNKNOWN_NAME_HUMAN_LIKE": "shared humanoid base; identity not recovered from the Alpha name map",
    "VEGETATION": "static; wind sway only",
    "TREE": "static; wind sway only",
    "ORE_DEPOSIT": "static",
    "TRAINING_TARGET": "static",
    "BREAKABLE_OBJECT": "static",
    "EXPLOSIVE_OBJECT": "static until triggered",
    "EGG_OBJECT": "static",
    "UNKNOWN": "UNKNOWN",
}

ANATOMY_RU = {
    "BIPED": "двуногое с передними конечностями",
    "BIPED_SPLIT_ARMS": "двуногое, плечо и предплечье отдельными блоками",
    "QUADRUPED": "четвероногое",
    "WINGED_BIPED": "двуногое с крыльями",
    "WINGED_LEGS": "две ноги + крылья",
    "WINGED_HEAD_BODY": "голова-тело + крылья + ноги",
    "LEGS_ONLY": "только две ноги",
    "HEAD_BODY_LEGS": "голова-тело + ноги",
    "FORE_LIMBS_ONLY": "торс + передние конечности, без ног",
    "LIMBLESS_BODY": "без конечностей, тело",
    "LIMBLESS_HEAD": "без конечностей, голова",
    "STATIC_OBJECT": "статический объект",
}

# Fallback identity when the Alpha display-name map has no entry (kind 0x5f, 0x81): inferred from the
# appearance models only, never presented as an Alpha name.
IDENTITY_BY_MODEL = {
    "santa-body.cub": "UNKNOWN_NAME_HUMAN_LIKE",
    "fir-tree.cub": "TREE",
}

# Identities that read as an animal body plan (used only for the behaviour proposal, not for anatomy).
ANIMAL_IDENTITIES = {"CANINE", "FELINE", "HOOFED_BEAST", "SMALL_BEAST", "REPTILE", "CRUSTACEAN",
                     "BIRD", "BAT", "RUNNER_BIRD", "INSECT", "FISH", "SLIME", "PLANT_MONSTER",
                     "AMPHIBIAN"}
SAPIENT_IDENTITIES = {"HUMAN", "ELF", "DWARF", "ORC", "GOBLIN_KIN", "LIZARDMAN", "FROGMAN",
                      "UNDEAD_KIN", "OGREKIN", "MERFOLK", "DESERT_DEITY"}
MONSTER_IDENTITIES = {"CONSTRUCT", "DEMON", "SPECTRAL", "DRAGON", "MONSTER"}


# --------------------------------------------------------------------------- evidence loading
def load_json(path: Path, default=None):
    return json.loads(path.read_text(encoding="utf-8")) if path.exists() else default


def alpha_kind_names(decomp_root: Path, exe: Path | None) -> dict[int, str]:
    """kind -> in-game display name from the decompiled name map (ALPHA_DECOMPILED).

    The map assigns the name right after World_map_find_or_insert_int; some names are string
    literals in World.cpp, others are wide-string pointers into Cube.exe (&DAT_...), which the
    existing extractor resolves from the image. Cube.exe is only READ here.
    """
    path = decomp_root / "world" / "World.cpp"
    if not path.exists():
        return {}
    image = None
    if exe is not None:
        try:
            from tools.alpha_models.anim import pe_bytes as pb
            image = pb.load(str(exe))
        except Exception:  # noqa: BLE001 - a missing loader only drops the DAT-resolved names
            image = None
    names = alpha_static.kind_names(str(decomp_root), image)
    return {k: v for k, v in names.items() if v}


def staged_visuals(path: Path) -> dict[int, dict]:
    """kind -> committed prepared variant (Assets/CubeGame/Content/Generated/AlphaStagedVisuals.g.cs)."""
    if not path.exists():
        return {}
    text = path.read_text(encoding="utf-8")
    pat = re.compile(r'new AlphaStagedVisualDef\((\d+), "([^"]*)", "([^"]*)", (\d+), (\d+), "([^"]*)"\)')
    out = {}
    for kind, variant, mode, parts, clips, rig in pat.findall(text):
        out[int(kind)] = {"variant": variant, "mode": mode, "parts": int(parts), "clips": int(clips),
                          "rig": rig}
    return out


def cache_state(cache_root: Path, variant: str) -> dict:
    """What the local content cache holds for one prepared variant (LOCAL_ONLY)."""
    state = {"present": False, "rig": False, "models": [], "parts": 0, "clips": 0,
             "missing_part_files": [], "size_blocks": None}
    actor = cache_root / "actors" / variant
    if not actor.is_dir():
        return state
    state["present"] = True
    rig_path = actor / "rig.json"
    if not rig_path.exists():
        return state
    state["rig"] = True
    try:
        rig = json.loads(rig_path.read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return state
    parts = rig.get("parts") or []
    state["parts"] = len(parts)
    all_models = []
    for part in parts:
        model = part.get("model")
        if model:
            all_models.append(model)
            if contract.BLOCK_ROLES.get(part.get("name"), "") != "equipment":
                state["models"].append(model)
        file = part.get("file")
        if file and not (actor / file).exists():
            state["missing_part_files"].append(file)
    state["all_models"] = all_models
    clips = actor / "clips"
    if clips.is_dir():
        state["clips"] = len(list(clips.glob("*.json")))
    body = next((p for p in parts if p.get("name") == "body"), None)
    if body and body.get("size"):
        state["size_blocks"] = body["size"]
    return state


# --------------------------------------------------------------------------- classification
def anatomy_of(parts: list[str], appearance: dict) -> str:
    """Structural anatomy label from the part set and the locomotion flag (never from the name)."""
    base = contract.structural_category(parts, appearance)
    has_wings = "slot9a_a" in parts
    has_body = "body" in parts
    has_head = "head" in parts
    has_hands = "hand_l" in parts
    has_feet = "foot_l" in parts
    if base == "WINGED":
        if not has_hands and not has_body:
            return "WINGED_HEAD_BODY"
        if not has_hands:
            return "WINGED_LEGS"
        return "WINGED_BIPED"
    if base == "BIPED_WITH_FORE_LIMBS":
        return "BIPED_SPLIT_ARMS" if "slot98_a" in parts else "BIPED"
    if base == "QUADRUPED":
        return "QUADRUPED"
    if base == "LEGS_ONLY":
        return "HEAD_BODY_LEGS" if (has_head and not has_body) else "LEGS_ONLY"
    if base == "FORE_LIMBS_ONLY":
        return "FORE_LIMBS_ONLY"
    if base == "LIMBLESS":
        return "LIMBLESS_HEAD" if (has_head and not has_body) else "LIMBLESS_BODY"
    return "UNKNOWN"


def intelligence_of(anatomy: str, identity: str, passive_basis: str | None,
                    in_town: bool, has_equipment_actions: bool) -> tuple[str, str]:
    """(group, basis). Alpha exposes no recovered 'intelligence' attribute, so this is a
    design-facing proposal; the basis states exactly what the Alpha evidence supports."""
    if passive_basis:
        return INTELLIGENCE_BY_RULE["PASSIVE"], f"Alpha spawn faction 6 / producer rule ({passive_basis})"
    if in_town:
        return INTELLIGENCE_BY_RULE["TOWN_NPC"], "LIVE_ALPHA_OBSERVED: rendered in the Arulon city start"
    if identity in SAPIENT_IDENTITIES:
        hand_note = "hands + weapon-only AI actions (EMULATED_ALPHA_CODE)" if has_equipment_actions \
            else "hands; no weapon-only AI action recovered"
        return (INTELLIGENCE_BY_RULE["HUMANOID"],
                f"{hand_note}; culture-like Alpha display name; no recovered Alpha attribute for intelligence")
    if identity in ANIMAL_IDENTITIES:
        return (INTELLIGENCE_BY_RULE["BEAST"],
                "animal model set and Alpha display name; no recovered Alpha attribute for intelligence")
    if identity in MONSTER_IDENTITIES:
        return (INTELLIGENCE_BY_RULE["MONSTER"],
                "monster model set; no recovered Alpha attribute for intelligence")
    if identity == "UNKNOWN_NAME_HUMAN_LIKE":
        return (INTELLIGENCE_BY_RULE["UNKNOWN"],
                "human-like part set, but the Alpha display name is missing, so no behaviour class is claimed")
    return (INTELLIGENCE_BY_RULE["UNKNOWN"], "no Alpha evidence for the behaviour class")


def signature_of(actions: list[int]) -> str:
    """Signature-animation proposal anchored on the action codes Alpha's AI actually launches."""
    if not actions:
        return "NONE_PROVEN (no AI action launch recovered)"
    named = [f"{a} ({d69_catalogs.ACTION_NAMES[a]})" if a in d69_catalogs.ACTION_NAMES else str(a)
             for a in actions]
    return "proven action codes: " + ", ".join(named)


# --------------------------------------------------------------------------- main
def resolve_exe(project: Path) -> Path | None:
    """Cube.exe used only to resolve wide-string pointers of the display-name map (read-only)."""
    for candidate in (project / ".scratch" / "alphatest2" / "Cube.exe",
                      Path(r"D:\Games\Cube world\Cube.exe")):
        if candidate.exists():
            return candidate
    return None


def build(project: Path, decomp: Path, out_csv: Path) -> dict:
    static_path = project / ".scratch" / "catalog" / "alpha_static.json"
    static = load_json(static_path)
    if not static or static.get("exe_sha256") != EXPECTED_EXE_SHA256:
        static = alpha_static.scan(str(project / ".scratch" / "alphatest2" / "Cube.exe"),
                                   str(project / ".scratch" / "alphatest2" / "data1.db"),
                                   str(decomp))
    if static.get("exe_sha256") != EXPECTED_EXE_SHA256:
        raise SystemExit(f"unexpected Cube.exe sha256 {static.get('exe_sha256')}")

    names = alpha_kind_names(decomp, resolve_exe(project))
    variants = staged_visuals(project / "Assets" / "CubeGame" / "Content" / "Generated"
                              / "AlphaStagedVisuals.g.cs")
    triggers = load_json(project / "tools" / "alpha_models" / "catalog" / "npc_trigger_evidence.json",
                         {"kinds": {}})["kinds"]
    walk = load_json(project / "tools" / "alpha_models" / "catalog" / "walk_oracle_evidence.json",
                     {"kinds": {}})["kinds"]
    census = d69_catalogs.live_census()
    cache_root = project / ".scratch" / "content"
    registry = static["registry"]

    rows = []
    for kind in range(KIND_COUNT):
        key = f"0x{kind:02x}"
        entry = static["kinds"].get(key)
        if not entry:
            raise SystemExit(f"kind {key} missing from the Alpha kind table")
        appearance = entry.get("appearance") or {}
        if not appearance:
            raise SystemExit(f"kind {key} has no appearance block")
        res = creature_rig.resolve(kind, appearance)
        parts = res.parts
        models = [registry.get(str(res.models[p]), f"#{res.models[p]}") for p in parts]
        anatomy = anatomy_of(parts, appearance)
        variant = variants.get(kind, {})
        cache = cache_state(cache_root, variant.get("variant", ""))
        trig = triggers.get(key, {})
        walk_status = (walk.get(key) or {}).get("status")
        live = census.get(key)
        passive = spawn_roles.load_evidence()["passive_kinds"].get(key)
        passive_basis = passive.get("basis") if passive else None

        alpha_name = names.get(kind)
        name_source = "ALPHA_DECOMPILED (World.cpp display-name map)" if alpha_name else None
        if alpha_name is None:
            hint = entry.get("name_hint")
            alpha_name = hint if hint else "UNKNOWN"
            if hint:
                name_source = "ALPHA_DECOMPILED hint (alpha_static name_hint) — no World.cpp map entry"
            else:
                name_source = "NONE (no display-name entry in the World.cpp map)"
        identity = IDENTITY_BY_NAME.get(alpha_name)
        if identity is None:
            identity = "UNKNOWN"
            for model in models:
                if model in IDENTITY_BY_MODEL:
                    identity = IDENTITY_BY_MODEL[model]
                    break
        identity_basis = (f"Alpha display name '{alpha_name}' + appearance models "
                          f"({', '.join(sorted(set(models)))})") if names.get(kind) else \
                         (f"no Alpha display name; identity inferred from the appearance models "
                          f"({', '.join(sorted(set(models)))}) — INFERRED, not an Alpha name")

        role = "PASSIVE_OBJECT" if passive_basis else "CREATURE"
        actions = sorted(int(a) for a in (trig.get("unarmed_actions") or {}))
        weapon_actions = sorted(int(a) for a in (trig.get("weapon_actions") or {}))
        intel = intelligence_of(anatomy, identity, passive_basis, bool(live), bool(weapon_actions))

        if alpha_name.endswith("Male"):
            species_variant = "MALE_VARIANT"
        elif alpha_name.endswith("Female"):
            species_variant = "FEMALE_VARIANT"
        else:
            species_variant = "SINGLE_FORM (no sex variant in the Alpha name map)"

        # Unity side locators.
        unity_registry = "VERIFIED" if variant else "MISSING"
        if variant and cache["present"] and cache["rig"] and not cache["missing_part_files"]:
            mapping = "LOCAL_ONLY"
        elif variant and cache["present"]:
            mapping = "PARTIAL"
        else:
            mapping = "UNMAPPED"
        if not variant:
            mapping = "UNMAPPED"
        cache_match = ("MATCH" if sorted(cache["models"]) == sorted(models) else
                       "MISMATCH" if cache["models"] else "NO_CACHE")
        unity_model_path = (f"alpha-content://actors/{variant['variant']}/models/*.cubdec"
                            if variant else "UNMAPPED")
        unity_guid = "NONE (models are decoded from .cub at runtime via VoxelMesh; no imported mesh asset)"
        mesh_id = (f"{len(cache.get('all_models', []))} parts: "
                   f"{', '.join(sorted(set(cache.get('all_models', []))))}"
                   if cache.get("all_models") else "UNMAPPED")
        runtime_ref = (f"CubeGame.Content.AlphaStagedVisuals.All[{kind}] -> "
                       f"AlphaActorLibrary.TryGet(\"{variant.get('variant')}\")") if variant else "UNMAPPED"

        sizes = static.get("model_sizes", {})
        part_sizes = []
        for part in parts:
            model_name = registry.get(str(res.models[part]), "")
            size = sizes.get(model_name)
            if size:
                part_sizes.append(f"{part}={model_name}:{'x'.join(str(int(v)) for v in size)}")
        kind_size = appearance.get("size", [0, 0, 0])

        unknown = []
        if kind not in names:
            unknown.append("alpha_display_name")
        if not walk_status or walk_status != "ORACLE_VERIFIED":
            unknown.append(f"walk_oracle={walk_status or 'NOT_RUN'}")
        if not actions:
            unknown.append("ai_action_launch_unproven")
        if not passive_basis:
            unknown.append("hostility/faction per kind not recovered (only passive faction 6 is proven)")
            unknown.append("NPC equipment profile R-1/R-2 open")
        if mapping != "LOCAL_ONLY":
            unknown.append("unity_geometry_not_resolvable_locally")
        if cache_match != "MATCH":
            unknown.append(f"cache_model_{cache_match.lower()}")
        if anatomy == "BIPED_SPLIT_ARMS":
            unknown.append("slot98 = elbow block (S05A2 research), not a second limb pair")
        if not unknown:
            unknown.append("none")

        rows.append({
            "entry_id": f"M1R0-KIND-{kind:02X}",
            "alpha_kind": f"0x{kind:02x}",
            "unity_model_path": unity_model_path,
            "unity_guid": unity_guid,
            "mesh_id": mesh_id,
            "runtime_ref": runtime_ref,
            "unity_variant": variant.get("variant", ""),
            "unity_controller_mode": variant.get("mode", ""),
            "unity_clip_count": variant.get("clips", ""),
            "unity_registry_status": unity_registry,
            "unity_cache_model_match": cache_match,
            "mapping_status": mapping,
            "alpha_creature_name": alpha_name,
            "alpha_species_variant": species_variant,
            "alpha_identity_evidence": f"{name_source}; role={role}"
                                        + (f"; passive_basis={passive_basis}" if passive_basis else ""),
            "alpha_game_role": role,
            "anatomy_family": anatomy,
            "anatomy_parts": ";".join(parts),
            "alpha_models": ";".join(sorted(set(models))),
            "appearance_flags": f"0x{appearance.get('flags', 0):04x}",
            "voxel_size_blocks": "x".join(f"{v:g}" for v in kind_size),
            "visual_features": (f"{len(parts)} draw blocks, creature {kind_size[0]:g}x{kind_size[1]:g}"
                                f"x{kind_size[2]:g} blocks; " + (", ".join(part_sizes) or "no model sizes")),
            "identity_family": identity,
            "identity_basis": identity_basis,
            "intelligence_behavior_group": intel[0],
            "intelligence_basis": intel[1],
            "locomotion_evidence": (f"drawModel walk/idle oracle={walk_status}" if not passive_basis
                                    else "not used by Alpha (passive object, faction 6)"),
            "combat_behavior_evidence": (f"AI launches unarmed {actions}; weapon-only {weapon_actions}"
                                         if actions else
                                         f"no unarmed launch proven; weapon-only {weapon_actions}"),
            "motion_personality_proposal": MOTION_BY_FAMILY.get(identity, "UNKNOWN"),
            "rig_archetype_candidate": RIG_BY_ANATOMY.get(anatomy, "CUSTOM"),
            "signature_animation_proposal": signature_of(actions),
            "confidence": ("HIGH" if names.get(kind) and walk_status == "ORACLE_VERIFIED"
                           and mapping == "LOCAL_ONLY" and cache_match == "MATCH"
                           else "MEDIUM" if names.get(kind) else "LOW"),
            "unknowns": "; ".join(dict.fromkeys(unknown)),
            "source_provenance": f"ALPHA:{name_source};UNITY:{'AlphaStagedVisuals+local cache' if variant else 'none'};"
                                 f"INFERRED:identity_family,intelligence_behavior_group,motion_personality_proposal,"
                                 f"rig_archetype_candidate",
        })

    out_csv.parent.mkdir(parents=True, exist_ok=True)
    with out_csv.open("w", encoding="utf-8", newline="") as fh:
        writer = csv.DictWriter(fh, fieldnames=list(rows[0].keys()))
        writer.writeheader()
        writer.writerows(rows)

    model_users = collections.defaultdict(set)
    for row in rows:
        for model in row["alpha_models"].split(";"):
            if model:
                model_users[model].add(row["alpha_kind"])
    shared = {m: sorted(v) for m, v in model_users.items() if len(v) > 1}

    summary = {
        "kinds": len(rows),
        "named": sum(1 for r in rows if not r["alpha_creature_name"].startswith("UNKNOWN")),
        "mapping": dict(collections.Counter(r["mapping_status"] for r in rows)),
        "unity_registry": dict(collections.Counter(r["unity_registry_status"] for r in rows)),
        "cache_model_match": dict(collections.Counter(r["unity_cache_model_match"] for r in rows)),
        "confidence": dict(collections.Counter(r["confidence"] for r in rows)),
        "anatomy": dict(collections.Counter(r["anatomy_family"] for r in rows)),
        "identity": dict(sorted(collections.Counter(r["identity_family"] for r in rows).items())),
        "intelligence": dict(collections.Counter(r["intelligence_behavior_group"] for r in rows)),
        "role": dict(collections.Counter(r["alpha_game_role"] for r in rows)),
        "rig": dict(collections.Counter(r["rig_archetype_candidate"] for r in rows)),
        "walk_oracle": dict(collections.Counter((walk.get(f"0x{k:02x}") or {}).get("status", "NOT_RUN")
                                                for k in range(KIND_COUNT))),
        "clip_count": sum(int(r["unity_clip_count"] or 0) for r in rows),
        "distinct_alpha_models": len(model_users),
        "models_used_by_several_kinds": len(shared),
        "shared_model_examples": dict(list(shared.items())[:12]),
        "out": str(out_csv),
    }
    return summary


def main(argv=None):
    ap = argparse.ArgumentParser(description="M1-R0 creature identity registry (research only)")
    ap.add_argument("--project", default=str(ROOT))
    ap.add_argument("--decomp", default=str(ROOT / ".scratch" / "CubeWorld-Reversal" / "cube"))
    ap.add_argument("--out", default=str(ROOT / "docs" / "research"
                                         / "M1_ALPHA_CREATURE_IDENTITY_REGISTRY.csv"))
    args = ap.parse_args(argv)
    summary = build(Path(args.project), Path(args.decomp), Path(args.out))
    print(json.dumps(summary, ensure_ascii=False, indent=1))


if __name__ == "__main__":
    main()
