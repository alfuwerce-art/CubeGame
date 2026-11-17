// CubeGame.Features.World.Foundation — G3 seam: the world asks for entity definitions, not for Alpha models.
//
// Today a spawn carries an Alpha species kind and the presentation layer resolves it against the local Alpha cache.
// From G3 on, creatures/characters are produced by a factory (ComfyUI, Tripo, hand-made, procedural — the world must
// not know which). The contract below is what the world needs and everything the factory may answer:
//
//     EntityDefinition { gameplay identity + behaviour + spawn profile }
//         └── EntityVisualRecipe { source kind + model key + rig/animation package }
//
// The recipe does not say where the model came from beyond a coarse source kind; the world never loads an asset.
// The existing Alpha path is expressed as VisualSourceKind.TemporaryAlpha with the species id as the model key, so
// nothing has to change before G3 starts producing definitions.
using System;
using System.Collections.Generic;
using System.Globalization;
using CubeGame.Features.Contracts;

namespace CubeGame.Features.World.Foundation
{
    /// <summary>Where the visual of an entity comes from. Coarse on purpose: the world must not care about tooling.</summary>
    public enum VisualSourceKind
    {
        /// <summary>Not resolved yet: the runtime must use its placeholder path.</summary>
        None = 0,

        /// <summary>An engine placeholder (capsule/primitive). Never a claim about the final look.</summary>
        Placeholder = 1,

        /// <summary>A model package imported into the project (fbx/cubdec/…): the factory's normal output.</summary>
        ImportedAsset = 2,

        /// <summary>A model produced by an external service and imported later; the world treats it as a package.</summary>
        ExternalGenerated = 3,

        /// <summary>The temporary Alpha-derived visual path (legacy, to be replaced by G3/G10).</summary>
        TemporaryAlpha = 4,
    }

    /// <summary>Rig and animation package an entity body needs. Empty id = the recipe needs no rig.</summary>
    public sealed class RigAnimationPackage
    {
        public string Id = string.Empty;
        public string RigId = string.Empty;
        public string AnimationSetId = string.Empty;
        public int Version = 1;

        public bool IsEmpty => string.IsNullOrEmpty(Id) && string.IsNullOrEmpty(RigId) &&
                               string.IsNullOrEmpty(AnimationSetId);

        public List<string> Validate(string owner)
        {
            var problems = new List<string>();
            if (Version < 1) problems.Add($"ENTITY_RIG_VERSION {owner}");
            if (!IsEmpty && string.IsNullOrEmpty(RigId)) problems.Add($"ENTITY_RIG_ID_MISSING {owner}");
            if (!IsEmpty && string.IsNullOrEmpty(AnimationSetId)) problems.Add($"ENTITY_ANIM_SET_MISSING {owner}");
            return problems;
        }

        public override string ToString() => IsEmpty ? "rig(none)" : $"rig={RigId} anim={AnimationSetId} v{Version}";
    }

    /// <summary>Everything the presentation layer needs to build a body. No asset paths, no Alpha offsets.</summary>
    public sealed class EntityVisualRecipe
    {
        public string Id = string.Empty;
        public VisualSourceKind Source = VisualSourceKind.None;

        /// <summary>Opaque key of the model package (the factory owns its meaning).</summary>
        public string ModelKey = string.Empty;

        /// <summary>Opaque key of the material/skin set.</summary>
        public string MaterialKey = string.Empty;

        /// <summary>Uniform model scale relative to the reference body.</summary>
        public float Scale = 1f;

        public RigAnimationPackage Rig = new RigAnimationPackage();

        public string Status = "CUBEGAME_CONTRACT: visual recipe (G3 seam)";

        public bool IsResolved => Source != VisualSourceKind.None;

        public List<string> Validate(string owner)
        {
            var problems = new List<string>();
            if (string.IsNullOrEmpty(Id)) problems.Add($"ENTITY_RECIPE_ID_EMPTY {owner}");
            if (!Enum.IsDefined(typeof(VisualSourceKind), Source)) problems.Add($"ENTITY_RECIPE_SOURCE {owner}");
            if (Source != VisualSourceKind.None && Source != VisualSourceKind.Placeholder &&
                string.IsNullOrEmpty(ModelKey))
            {
                problems.Add($"ENTITY_RECIPE_MODEL_KEY {owner}");
            }

            if (Scale <= 0f || Scale > 100f) problems.Add($"ENTITY_RECIPE_SCALE {owner}");
            problems.AddRange(Rig.Validate(owner));
            return problems;
        }

        public override string ToString() => $"{Id} {Source} model={ModelKey} {Rig}";
    }

    /// <summary>Gameplay identity of a spawnable entity. This is what the world and G3 exchange.</summary>
    public sealed class EntityDefinition
    {
        public string Id = string.Empty;

        /// <summary>CubeGame family tag ("beast", "humanoid", "plant", "construct"…). Free text, not an Alpha id.</summary>
        public string Family = string.Empty;

        /// <summary>Species kind of the current runtime (temporary; G3 replaces it with a factory key).</summary>
        public int SpeciesKind;

        public string BehaviourKey = string.Empty;
        public string SpawnProfileId = string.Empty;
        public string BiomeTag = string.Empty;
        public int Level = 1;
        public int Stars;

        /// <summary>0 = neutral, 1 = hostile, 2 = passive object (maps onto the runtime faction ids).</summary>
        public int Disposition;

        public EntityVisualRecipe Visual = new EntityVisualRecipe();

        public string Provenance = "CUBEGAME_CONTRACT: entity definition (G3 seam)";

        public List<string> Validate()
        {
            var problems = new List<string>();
            if (string.IsNullOrEmpty(Id)) problems.Add("ENTITY_ID_EMPTY");
            if (string.IsNullOrEmpty(Family)) problems.Add($"ENTITY_FAMILY_EMPTY {Id}");
            if (string.IsNullOrEmpty(BehaviourKey)) problems.Add($"ENTITY_BEHAVIOUR_EMPTY {Id}");
            if (Level < 1) problems.Add($"ENTITY_LEVEL {Id}");
            if (Stars < 0) problems.Add($"ENTITY_STARS {Id}");
            if (Disposition < 0 || Disposition > 2) problems.Add($"ENTITY_DISPOSITION {Id}");
            problems.AddRange(Visual.Validate(Id));
            return problems;
        }

        public override string ToString() =>
            $"{Id} family={Family} species=0x{SpeciesKind:x2} lvl={Level} stars={Stars} " +
            $"behaviour={BehaviourKey} {Visual}";
    }

    /// <summary>Definition source of the world: a registry in the runtime, a test double in tests.</summary>
    public interface IEntityDefinitionSource
    {
        IEnumerable<EntityDefinition> All { get; }

        bool TryGet(string id, out EntityDefinition definition);
    }

    /// <summary>Runtime registry. The world never resolves visuals itself; it asks this for a definition.</summary>
    public sealed class EntityDefinitionRegistry : IEntityDefinitionSource
    {
        private readonly Dictionary<string, EntityDefinition> _byId =
            new Dictionary<string, EntityDefinition>(StringComparer.Ordinal);

        public IEnumerable<EntityDefinition> All => _byId.Values;

        public int Count => _byId.Count;

        public bool Register(EntityDefinition definition)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (string.IsNullOrEmpty(definition.Id)) return false;
            _byId[definition.Id] = definition;
            return true;
        }

        public bool TryGet(string id, out EntityDefinition definition) =>
            _byId.TryGetValue(id ?? string.Empty, out definition);

        public List<string> Validate()
        {
            var problems = new List<string>();
            foreach (EntityDefinition definition in _byId.Values) problems.AddRange(definition.Validate());
            return problems;
        }
    }

    /// <summary>Ready-to-spawn record: the world's half of the handshake with G3.</summary>
    public readonly struct ResolvedEntitySpawn
    {
        public ResolvedEntitySpawn(EntityDefinition definition, double x, double y, double z, float yaw,
            uint appearanceSeed)
        {
            Definition = definition;
            X = x;
            Y = y;
            Z = z;
            Yaw = yaw;
            AppearanceSeed = appearanceSeed;
        }

        public EntityDefinition Definition { get; }

        public double X { get; }

        public double Y { get; }

        public double Z { get; }

        public float Yaw { get; }

        /// <summary>Deterministic seed for appearance variation (the factory may use it, the world does not).</summary>
        public uint AppearanceSeed { get; }

        public string VisualKey => Definition?.Visual?.ModelKey ?? string.Empty;

        public string RigKey => Definition?.Visual?.Rig?.RigId ?? string.Empty;

        public override string ToString() =>
            $"spawn {Definition?.Id} at ({X:0},{Y:0},{Z:0}) yaw={Yaw:0} visual={VisualKey} rig={RigKey}";
    }

    /// <summary>Pure contract helpers: resolve a definition into a spawn, validate a registry.</summary>
    public static class EntitySpawnContract
    {
        public static ResolvedEntitySpawn Resolve(EntityDefinition definition, double x, double y, double z, float yaw,
            uint appearanceSeed)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            return new ResolvedEntitySpawn(definition, x, y, z, yaw, appearanceSeed);
        }

        /// <summary>
        /// Appearance seed of a spawn: stable for the same stable id, so a reload produces the same look without the
        /// world storing anything about the model.
        /// </summary>
        public static uint AppearanceSeedOf(ulong stableId) => (uint)(stableId ^ (stableId >> 32));

        /// <summary>
        /// Adapter of the current runtime: a <see cref="SpawnDefinition"/> (Alpha species + behaviour) becomes a
        /// CubeGame definition whose visual is still the temporary Alpha path. G3 swaps the recipe, not the world.
        /// </summary>
        public static EntityDefinition FromSpawn(SpawnDefinition spawn, string family, string modelKey = null)
        {
            if (spawn == null) throw new ArgumentNullException(nameof(spawn));
            int disposition = spawn.Faction == FactionPassive ? 2 : (spawn.Faction == FactionMonster ? 1 : 0);
            return new EntityDefinition
            {
                Id = $"{SpawnIdPrefix}{spawn.Region.X}:{spawn.Region.Z}:{spawn.SpawnIndex}",
                Family = string.IsNullOrEmpty(family) ? "unknown" : family,
                SpeciesKind = spawn.Kind.Value,
                BehaviourKey = spawn.BehaviourKey ?? string.Empty,
                Level = spawn.Level,
                Stars = spawn.Quality,
                Disposition = disposition,
                Visual = new EntityVisualRecipe
                {
                    Id = $"alpha.species.{spawn.Kind.Value.ToString(CultureInfo.InvariantCulture)}",
                    Source = VisualSourceKind.TemporaryAlpha,
                    ModelKey = modelKey ?? spawn.Kind.Value.ToString(CultureInfo.InvariantCulture),
                    Status = "TEMPORARY_RUNTIME: Alpha species visual until G3 factory definitions exist",
                },
                Provenance = "CUBEGAME_CONTRACT: adapted from a runtime spawn definition",
            };
        }

        public const string SpawnIdPrefix = "spawn.";

        /// <summary>Alpha faction 1 (monster): hostile towards the hero.</summary>
        public const int FactionMonster = 1;

        /// <summary>Alpha faction 6 (passive object/plant): no walk, no actions.</summary>
        public const int FactionPassive = 6;
    }
}
