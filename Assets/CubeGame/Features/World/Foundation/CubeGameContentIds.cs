// CubeGame.Features.World.Foundation — G2-1: CubeGame-owned content identity.
//
// From G2-1 the world's content decisions belong to CubeGame: which plant stands where, which structures exist, who
// lives in them and what a dungeon entrance is. Alpha assets may still *look* like those things, but no rule in this
// file, in the providers or in the runtime reads an Alpha asset id to decide anything.
//
// The single place where a CubeGame entity is temporarily drawn by an Alpha species is
// CubeGameVisualBindings (marked TEMPORARY_VISUAL_PLACEHOLDER). G3 replaces that table; nothing else moves.
using System;
using System.Collections.Generic;

namespace CubeGame.Features.World.Foundation
{
    /// <summary>CubeGame-native identity strings. These are the ids stored by saves and referenced by the runtime.</summary>
    public static class CubeGameContentIds
    {
        /// <summary>World delta channel of structures and their anchors (zone spawn channels 0…3 are taken).</summary>
        public const int StructureChannel = 7;

        /// <summary>World delta channel of vegetation objects.</summary>
        public const int VegetationChannel = 8;

        /// <summary>Behaviour key of a structure/portal/static marker (no walk, no actions).</summary>
        public const string StaticBehaviourKey = "cubegame.static_object";

        /// <summary>Behaviour key of a service NPC (walks and talks, never fights).</summary>
        public const string ServiceNpcBehaviourKey = "cubegame.service_npc";

        /// <summary>Behaviour key of a regular NPC.</summary>
        public const string VillagerBehaviourKey = "cubegame.villager";

        /// <summary>Behaviour key of a hostile humanoid (uses the creature AI path).</summary>
        public const string HostileBehaviourKey = "creature_ai";

        /// <summary>Behaviour key of a wild creature (creature AI path, temporary Alpha kinds).</summary>
        public const string CreatureBehaviourKey = "creature_ai";

        public const string VegetationIdPrefix = "veg";
        public const string StructureIdPrefix = "structure";
        public const string AnchorIdPrefix = "anchor";
        public const string PortalIdPrefix = "portal";
        public const string DungeonInstancePrefix = "dungeon";

        /// <summary>Persistent id of a vegetation object: seed, zone, rule and the site slot inside the zone.</summary>
        public static string Vegetation(uint seed, int zoneX, int zoneZ, int siteSlot, string ruleId) =>
            $"{VegetationIdPrefix}:{seed}:{zoneX}:{zoneZ}:{siteSlot}:{ruleId}";

        /// <summary>Persistent id of a settlement NPC anchor.</summary>
        public static string Anchor(uint seed, int regionX, int regionZ, WorldAnchorKind kind, int index) =>
            $"{AnchorIdPrefix}:{seed}:{regionX}:{regionZ}:{(int)kind}:{index}";

        /// <summary>Persistent id of a dungeon portal (the entrance object the player interacts with).</summary>
        public static string Portal(uint seed, int zoneX, int zoneZ) =>
            $"{PortalIdPrefix}:{seed}:{zoneX}:{zoneZ}";

        public static bool IsVegetation(string id) => HasPrefix(id, VegetationIdPrefix);

        public static bool IsStructure(string id) => HasPrefix(id, StructureIdPrefix);

        public static bool IsAnchor(string id) => HasPrefix(id, AnchorIdPrefix) || HasPrefix(id, "seam.anchor");

        public static bool IsPortal(string id) => HasPrefix(id, PortalIdPrefix);

        private static bool HasPrefix(string id, string prefix) =>
            !string.IsNullOrEmpty(id) && id.StartsWith(prefix + ":", StringComparison.Ordinal);
    }

    /// <summary>Behaviour families the runtime maps onto its existing movement/AI paths.</summary>
    public enum CubeGameBehaviourFamily
    {
        Static = 0,
        ServiceNpc = 1,
        Villager = 2,
        Hostile = 3,
        Creature = 4,
    }

    /// <summary>
    /// The CubeGame entity catalogue: identity, behaviour, disposition and the (temporary) visual binding.
    /// Five definitions are enough for G2-1: hero prototype, humanoid NPC, hostile humanoid, wild creature, merchant.
    /// </summary>
    public static class CubeGameEntityCatalog
    {
        public const string Hero = "cubegame.hero.prototype";
        public const string NpcHumanoid = "cubegame.npc.humanoid";
        public const string NpcHostile = "cubegame.npc.hostile";
        public const string CreatureWild = "cubegame.creature.wild";
        public const string NpcMerchant = "cubegame.npc.merchant";

        public const string CatalogVersion = "cubegame.entities/v1";

        /// <summary>Definitions of the catalogue, in a stable order (registry insertion order is not an id space).</summary>
        public static IReadOnlyList<EntityDefinition> All()
        {
            return new List<EntityDefinition>
            {
                new EntityDefinition
                {
                    Id = Hero,
                    Family = "player",
                    BehaviourKey = "cubegame.hero",
                    Disposition = 0,
                    Level = 1,
                    Visual = new EntityVisualRecipe
                    {
                        Id = "recipe.hero.placeholder",
                        Source = VisualSourceKind.TemporaryAlpha,
                        ModelKey = "alpha.species.2",
                        Status = "TEMPORARY_VISUAL_PLACEHOLDER: Alpha human male model until G3",
                    },
                    Provenance = "CUBEGAME_NATIVE: hero prototype definition",
                },
                new EntityDefinition
                {
                    Id = NpcHumanoid,
                    Family = "humanoid",
                    BehaviourKey = CubeGameContentIds.VillagerBehaviourKey,
                    Disposition = 0,
                    Level = 3,
                    Visual = new EntityVisualRecipe
                    {
                        Id = "recipe.npc.humanoid",
                        Source = VisualSourceKind.TemporaryAlpha,
                        ModelKey = "alpha.species.2",
                        Scale = 0.95f,
                        Status = "TEMPORARY_VISUAL_PLACEHOLDER: Alpha humanoid model until G3",
                    },
                    Provenance = "CUBEGAME_NATIVE: villager definition",
                },
                new EntityDefinition
                {
                    Id = NpcHostile,
                    Family = "humanoid",
                    BehaviourKey = CubeGameContentIds.HostileBehaviourKey,
                    Disposition = 1,
                    Level = 4,
                    Visual = new EntityVisualRecipe
                    {
                        Id = "recipe.npc.hostile",
                        Source = VisualSourceKind.TemporaryAlpha,
                        ModelKey = "alpha.species.3",
                        Status = "TEMPORARY_VISUAL_PLACEHOLDER: Alpha hostile humanoid model until G3",
                    },
                    Provenance = "CUBEGAME_NATIVE: hostile humanoid definition",
                },
                new EntityDefinition
                {
                    Id = CreatureWild,
                    Family = "beast",
                    BehaviourKey = CubeGameContentIds.CreatureBehaviourKey,
                    Disposition = 1,
                    Level = 2,
                    Visual = new EntityVisualRecipe
                    {
                        Id = "recipe.creature.wild",
                        Source = VisualSourceKind.TemporaryAlpha,
                        ModelKey = "alpha.species.28",
                        Status = "TEMPORARY_VISUAL_PLACEHOLDER: Alpha quadruped model until G3",
                    },
                    Provenance = "CUBEGAME_NATIVE: wild creature definition",
                },
                new EntityDefinition
                {
                    Id = NpcMerchant,
                    Family = "humanoid",
                    BehaviourKey = CubeGameContentIds.ServiceNpcBehaviourKey,
                    SpawnProfileId = "spawn.settlement",
                    Disposition = 0,
                    Level = 2,
                    Visual = new EntityVisualRecipe
                    {
                        Id = "recipe.npc.merchant",
                        Source = VisualSourceKind.TemporaryAlpha,
                        ModelKey = "alpha.species.2",
                        MaterialKey = "alpha.palette.merchant",
                        Status = "TEMPORARY_VISUAL_PLACEHOLDER: Alpha humanoid model until G3",
                    },
                    Provenance = "CUBEGAME_NATIVE: merchant/service definition",
                },
            };
        }

        /// <summary>Registry with the default catalogue (failures of a definition are reported, never thrown).</summary>
        public static EntityDefinitionRegistry CreateRegistry(List<string> problems = null)
        {
            var registry = new EntityDefinitionRegistry();
            foreach (EntityDefinition definition in All())
            {
                if (!registry.Register(definition))
                {
                    problems?.Add($"ENTITY_REGISTER_FAILED {definition.Id}");
                }
            }

            foreach (string problem in registry.Validate()) problems?.Add(problem);
            if (registry.Count != 5) problems?.Add($"ENTITY_CATALOG_SIZE {registry.Count}");
            return registry;
        }

        /// <summary>Definition id an anchor/service kind maps to. Unknown kinds are a data error.</summary>
        public static bool TryDefinitionForAnchor(WorldAnchorKind kind, out string definitionId)
        {
            switch (kind)
            {
                case WorldAnchorKind.Inn:
                case WorldAnchorKind.Shop:
                case WorldAnchorKind.Smith:
                    definitionId = NpcMerchant;
                    return true;
                case WorldAnchorKind.QuestGiver:
                case WorldAnchorKind.Guard:
                case WorldAnchorKind.Villager:
                case WorldAnchorKind.SettlementCenter:
                    definitionId = NpcHumanoid;
                    return true;
                case WorldAnchorKind.DungeonEntrance:
                case WorldAnchorKind.CampCenter:
                case WorldAnchorKind.LandmarkCenter:
                    definitionId = string.Empty;
                    return false;
                default:
                    definitionId = string.Empty;
                    return false;
            }
        }
    }

    /// <summary>
    /// TEMPORARY_VISUAL_PLACEHOLDER table: CubeGame entity definition → the Alpha species kind that currently draws it
    /// and the Alpha behaviour key the existing AI/population path expects. Nothing in the world content providers
    /// reads this table; only the presentation/runtime adapter does, and G3 replaces it.
    /// </summary>
    public static class CubeGameVisualBindings
    {
        public const string Status =
            "TEMPORARY_VISUAL_PLACEHOLDER: CubeGame entity definitions are drawn with Alpha species models until G3";

        private static readonly Dictionary<string, int> Species = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            { CubeGameEntityCatalog.Hero, 0x02 },
            { CubeGameEntityCatalog.NpcHumanoid, 0x02 },
            { CubeGameEntityCatalog.NpcHostile, 0x03 },
            { CubeGameEntityCatalog.CreatureWild, 0x1c },
            { CubeGameEntityCatalog.NpcMerchant, 0x02 },
        };

        /// <summary>Alpha species kind used as the placeholder body of a definition (0 when unbound).</summary>
        public static int SpeciesOf(string definitionId) =>
            Species.TryGetValue(definitionId ?? string.Empty, out int kind) ? kind : 0;

        public static bool TryGetSpecies(string definitionId, out int speciesKind) =>
            Species.TryGetValue(definitionId ?? string.Empty, out speciesKind);

        /// <summary>
        /// Alpha behaviour key the existing population/AI path expects for a CubeGame behaviour family. New families map
        /// onto the current runtime without teaching that runtime about CubeGame ids.
        /// </summary>
        public static string LegacyBehaviourKey(string cubeGameBehaviourKey)
        {
            switch (cubeGameBehaviourKey)
            {
                case CubeGameContentIds.StaticBehaviourKey:
                    return AlphaZoneContentProviderKeys.StaticObject;
                case CubeGameContentIds.ServiceNpcBehaviourKey:
                case CubeGameContentIds.VillagerBehaviourKey:
                    return AlphaZoneContentProviderKeys.StaticObject;
                default:
                    return AlphaZoneContentProviderKeys.CreatureAi;
            }
        }

        /// <summary>Behaviour families defined by the catalogue (used by tests and the debug overlay).</summary>
        public static IReadOnlyDictionary<string, CubeGameBehaviourFamily> Families()
        {
            return new Dictionary<string, CubeGameBehaviourFamily>(StringComparer.Ordinal)
            {
                { CubeGameEntityCatalog.Hero, CubeGameBehaviourFamily.Creature },
                { CubeGameEntityCatalog.NpcHumanoid, CubeGameBehaviourFamily.Villager },
                { CubeGameEntityCatalog.NpcHostile, CubeGameBehaviourFamily.Hostile },
                { CubeGameEntityCatalog.CreatureWild, CubeGameBehaviourFamily.Creature },
                { CubeGameEntityCatalog.NpcMerchant, CubeGameBehaviourFamily.ServiceNpc },
            };
        }

        public static List<string> Validate()
        {
            var problems = new List<string>();
            foreach (EntityDefinition definition in CubeGameEntityCatalog.All())
            {
                if (!TryGetSpecies(definition.Id, out int kind) || kind <= 0)
                {
                    problems.Add($"VISUAL_PLACEHOLDER_MISSING {definition.Id}");
                }

                if (definition.Visual == null || definition.Visual.Source != VisualSourceKind.TemporaryAlpha)
                {
                    problems.Add($"VISUAL_PLACEHOLDER_SOURCE {definition.Id}");
                }
            }

            return problems;
        }
    }

    /// <summary>Behaviour keys the pre-G2-1 content provider used; kept so the runtime adapter needs no magic strings.</summary>
    public static class AlphaZoneContentProviderKeys
    {
        public const string StaticObject = "static_object";
        public const string CreatureAi = "creature_ai";
    }
}
