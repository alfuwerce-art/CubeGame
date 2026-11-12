using System;
using System.Collections.Generic;
using System.IO;
using CubeGame.AlphaModels;
using CubeGame.Content;
using CubeGame.Core.Items;
using CubeGame.Features.Contracts;
using CubeGame.Features.Creatures;
using CubeGame.Features.Session;
using CubeGame.Presentation.Visual;
using UnityEngine;

namespace CubeGame.Gameplay
{
    /// <summary>
    /// Visual runtime of the game scene (M0-03C/M0-05C): turns gameplay events into S05 Alpha models and action
    /// clips. It never changes gameplay state. Entities whose model is not staged keep a visible placeholder and
    /// report DATA_MISSING in <see cref="Problems"/> instead of failing the game.
    ///
    /// Model scale: S05 models are in Alpha model voxels; the factor to world voxels is not restored
    /// (PLACEHOLDER_NOT_ALPHA). The hero model is fitted to the hero controller height and the same factor is used
    /// for every creature, so relative sizes stay those of Alpha.
    ///
    /// M0-F2A ownership: this sink is the only owner of (1) the hero visual — composed from the hero's appearance
    /// and equipped items by <see cref="HeroVisualComposer"/> and rebuilt only when that composition changes — and
    /// (2) the visual of every item on the ground (one item → exactly one object, Alpha model or a marked
    /// placeholder). Scene positions are recomputed from world positions every frame, so a floating-origin shift
    /// moves creatures and ground items with the terrain. Generated meshes are destroyed with their object.
    /// </summary>
    public sealed class GameVisualSink : MonoBehaviour, IEntityVisualSink
    {
        /// <summary>Hero visual height in blocks: equals the hero CharacterController height.</summary>
        public const float HeroVisualHeight = 1.8f;

        /// <summary>Walk amplitude of a steady walk in the Alpha captures (creature+0x1188 ≈ 0.19).</summary>
        public const float WalkAmplitude = 0.19f;

        private readonly Dictionary<StableEntityId, Entry> _entries = new Dictionary<StableEntityId, Entry>();
        private readonly Dictionary<StableEntityId, GroundItem> _groundItems = new Dictionary<StableEntityId, GroundItem>();
        private readonly Dictionary<VisualEquipmentSlot, HeroVisualItem> _heroEquipment = new Dictionary<VisualEquipmentSlot, HeroVisualItem>();
        private AlphaActorLibrary _library;
        private Func<WorldPosition, Vector3> _toScene;
        private Transform _heroRoot;
        private string _itemsDirectory;
        private string _contentRoot;
        private long _lastTimeMs = -1;
        private HeroAppearance _heroAppearance = AlphaHeroCreation.Default;
        private bool _heroDirty;
        private string _heroBuiltSignature;
        private Features.Session.GameEventBus _bus;
        private static Material _placeholderMaterial;
        private static Material _bodyPlaceholderMaterial;

        /// <summary>Last composed hero visual (what the hero state asks for).</summary>
        public HeroVisualSpec HeroSpec { get; private set; }

        /// <summary>Number of hero model builds (tests: an unchanged state must not rebuild).</summary>
        public int HeroBuildCount { get; private set; }

        /// <summary>Alpha model voxel → world block factor, measured on the hero model (0 until the hero model exists).</summary>
        public float ModelScale { get; private set; }

        public List<string> Problems { get; } = new List<string>();

        public int ModelCount
        {
            get
            {
                int count = 0;
                foreach (Entry entry in _entries.Values)
                {
                    if (entry.View != null && entry.View.Ready)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public void Initialise(AlphaActorLibrary library, Func<WorldPosition, Vector3> toScene, Transform heroRoot)
        {
            _library = library;
            _toScene = toScene;
            _heroRoot = heroRoot;
            if (library != null && library.State == AlphaContentState.Ready && library.Source != null)
            {
                _contentRoot = library.Source.Root;
                _itemsDirectory = Path.Combine(library.Source.Root, "items");
            }
            else
            {
                Problems.Add("DATA_MISSING alpha content cache: " + (library != null ? library.Diagnosis : "no library"));
            }
        }

        public bool TryGetView(StableEntityId entity, out AlphaActorView view)
        {
            if (entity.Equals(StableEntityId.Hero)) FlushHero();
            view = _entries.TryGetValue(entity, out Entry entry) ? entry.View : null;
            return view != null;
        }

        public bool TryGetRoot(StableEntityId entity, out Transform root)
        {
            root = _entries.TryGetValue(entity, out Entry entry) ? entry.Root : null;
            return root != null;
        }

        public void OnEntitySpawned(EntitySpawned e)
        {
            OnEntityDespawned(new EntityDespawned { Entity = e.Entity });
            bool isHero = e.Entity.Equals(StableEntityId.Hero);
            var entry = new Entry { Position = e.Position, Yaw = e.Yaw, IsHero = isHero };
            if (isHero && _heroRoot != null)
            {
                entry.Root = new GameObject("HeroVisual").transform;
                entry.Root.SetParent(_heroRoot, false);
            }
            else
            {
                string name = e.WorldObjectId.HasValue
                    ? e.WorldObjectName ?? $"WorldObject_{e.WorldObjectId:x2}"
                    : $"Entity_{e.Kind.Value:x2}";
                entry.Root = new GameObject($"{name}_{e.Entity.Value:x}").transform;
                entry.Root.SetParent(transform, false);
                entry.Root.position = _toScene != null ? _toScene(e.Position) : Vector3.zero;
            }

            entry.Root.rotation = Quaternion.Euler(0f, e.Yaw, 0f);
            _entries[e.Entity] = entry;
            if (isHero)
            {
                // The hero visual follows the saved character: appearance from the spawn, items from the equipment events.
                if (e.Appearance.HasValue)
                {
                    _heroAppearance = e.Appearance.Value;
                }
                else
                {
                    Problems.Add("HERO_APPEARANCE_MISSING spawn without appearance; Alpha editor defaults used");
                }

                _heroBuiltSignature = null;
                _heroDirty = true;
                FlushHero();
                return;
            }

            if (!e.WorldObjectId.HasValue && e.WorldObjectName == null)
            {
                entry.View = BuildCreature(e.Kind, entry.Root, e.Entity.Kind == EntityIdKind.ZoneSpawn);
            }

            if (entry.View == null)
            {
                entry.Placeholder = MakePlaceholder(entry.Root, false);
            }
        }

        public void OnEntityDespawned(EntityDespawned e)
        {
            if (_entries.TryGetValue(e.Entity, out Entry entry))
            {
                if (entry.Root != null)
                {
                    entry.Root.gameObject.SetActive(false);
                    DestroyView(entry.View);
                    Destroy(entry.Root.gameObject);
                }

                _entries.Remove(e.Entity);
            }
        }

        public void OnEntityMoved(EntityMoved e)
        {
            if (!_entries.TryGetValue(e.Entity, out Entry entry))
            {
                return;
            }

            entry.Position = e.Position;
            entry.Yaw = e.Yaw;
            entry.Speed = e.Speed;
            entry.Mode = e.Mode;
        }

        public void OnEquipmentChanged(EquipmentVisualChanged e)
        {
            // M0-F2A: the sink keeps the hero's equipped items (with material/rarity/seed) and recomposes the whole
            // visual from them; a slot that cannot be drawn is reported by the composer, never left stale.
            if (!e.Entity.Equals(StableEntityId.Hero))
            {
                return;
            }

            HeroVisualItem? item = HeroVisualItem.FromEvent(e);
            if (item.HasValue)
            {
                _heroEquipment[e.Slot] = item.Value;
            }
            else
            {
                _heroEquipment.Remove(e.Slot);
            }

            _heroDirty = true;
        }

        /// <summary>Equipped items the hero visual is composed from (tests/diagnostics).</summary>
        public IReadOnlyDictionary<VisualEquipmentSlot, HeroVisualItem> HeroEquipment => _heroEquipment;

        /// <summary>
        /// Recomposes the hero visual and rebuilds the model only when the composition changed. Called every frame
        /// and before the hero view is handed out, so several equipment events of one frame cause one rebuild.
        /// </summary>
        public void FlushHero()
        {
            if (!_heroDirty)
            {
                return;
            }

            _heroDirty = false;
            HeroVisualSpec spec = HeroVisualComposer.Compose(_heroAppearance, _heroEquipment);
            HeroSpec = spec;
            if (!_entries.TryGetValue(StableEntityId.Hero, out Entry entry) || entry.Root == null)
            {
                return;
            }

            string signature = spec.Signature;
            if (signature == _heroBuiltSignature && (entry.View != null || entry.Placeholder != null))
            {
                return;
            }

            foreach (string problem in spec.Problems)
            {
                Problems.Add("HERO_VISUAL " + problem);
            }

            DestroyView(entry.View);
            entry.View = null;
            if (entry.Placeholder != null)
            {
                entry.Placeholder.SetActive(false);
                Destroy(entry.Placeholder);
                entry.Placeholder = null;
            }

            entry.View = BuildHero(entry.Root, spec);
            if (entry.View == null)
            {
                entry.Placeholder = MakePlaceholder(entry.Root, true);
            }

            _heroBuiltSignature = signature;
            HeroBuildCount++;
        }

        /// <summary>
        /// Subscribes the sink to the item events (the sink interface carries entity events only). The game
        /// bootstrap calls this once the bus exists; without it ground items keep their gameplay placeholder.
        /// </summary>
        public void BindItems(Features.Session.GameEventBus bus)
        {
            if (_bus != null)
            {
                _bus.Unsubscribe<WorldItemDropped>(OnWorldItemDropped);
                _bus.Unsubscribe<WorldItemRemoved>(OnWorldItemRemoved);
            }

            _bus = bus;
            if (_bus == null) return;
            _bus.Subscribe<WorldItemDropped>(OnWorldItemDropped);
            _bus.Subscribe<WorldItemRemoved>(OnWorldItemRemoved);
        }

        /// <summary>
        /// Visual of one item on the ground: the Alpha model the item selector picks for its bytes, or a marked
        /// placeholder cube when that model is not staged — always exactly one object per item.
        /// </summary>
        public void OnWorldItemDropped(WorldItemDropped e)
        {
            OnWorldItemRemoved(new WorldItemRemoved { Item = e.Item });
            var ground = new GroundItem { Position = e.Position };
            ground.Root = new GameObject($"GroundItem_{e.Item.Value:x}");
            ground.Root.transform.SetParent(transform, false);
            ground.Model = BuildGroundModel(e, ground.Root.transform);
            ground.Model = ground.Model ?? GroundPlaceholder(e.Raw, ground.Root.transform);
            ground.ModelName = ground.Model.name;
            _groundItems[e.Item] = ground;
            PlaceGroundItem(ground);
        }

        public void OnWorldItemRemoved(WorldItemRemoved e)
        {
            if (_groundItems.TryGetValue(e.Item, out GroundItem ground))
            {
                DestroyGroundItem(ground);
                _groundItems.Remove(e.Item);
            }
        }

        /// <summary>Number of item visuals currently shown (tests/diagnostics).</summary>
        public int GroundItemCount => _groundItems.Count;

        /// <summary>Scene object of a ground item (tests/diagnostics).</summary>
        public bool TryGetGroundItem(StableEntityId item, out Transform root, out string model)
        {
            root = null;
            model = null;
            if (!_groundItems.TryGetValue(item, out GroundItem ground) || ground.Root == null) return false;
            root = ground.Root.transform;
            model = ground.ModelName;
            return true;
        }

        GameObject BuildGroundModel(WorldItemDropped e, Transform parent)
        {
            if (_itemsDirectory == null)
            {
                Problems.Add("DATA_MISSING item model: no staged items directory");
                return null;
            }

            string modelName = GroundItemModelName(e.Raw, e.Material, e.Rarity, e.Seed);
            if (string.IsNullOrEmpty(modelName))
            {
                Problems.Add($"DATA_MISSING item model {e.Raw} material {e.Material}");
                return null;
            }

            string path = Path.Combine(_itemsDirectory, AlphaCharacterModels.PayloadName(modelName));
            if (!File.Exists(path))
            {
                Problems.Add("DATA_MISSING item model file " + path);
                return null;
            }

            CubeGame.AlphaTransfer.VoxelMesh.Result result =
                CubeGame.AlphaTransfer.VoxelMesh.Build(File.ReadAllBytes(path), "ground_" + Path.GetFileNameWithoutExtension(path));
            if (result.mesh == null)
            {
                Problems.Add($"ITEM_MESH {e.Raw}: {result.problem}");
                return null;
            }

            Material material = AlphaVisualResources.Base;
            if (material == null)
            {
                Destroy(result.mesh);
                Problems.Add("ITEM_MATERIAL_MISSING " + AlphaVisualResources.ShaderName);
                return null;
            }

            // CUBEGAME_CONTRACT: the model lies centred on its drop point, bottom on the ground, turned 45°; Alpha's
            // ground item pose (spin/bob) is not restored. Scale = the hero model factor (Alpha voxel → block).
            var go = new GameObject(Path.GetFileNameWithoutExtension(path));
            go.transform.SetParent(parent, false);
            var mesh = new GameObject("mesh");
            mesh.transform.SetParent(go.transform, false);
            Bounds bounds = result.mesh.bounds;
            mesh.transform.localPosition = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
            mesh.AddComponent<MeshFilter>().sharedMesh = result.mesh;
            mesh.AddComponent<MeshRenderer>().sharedMaterial = material;
            go.transform.localScale = Vector3.one * (ModelScale > 0f ? ModelScale : CubeGame.AlphaModels.AlphaRigBuilder.RootScale);
            go.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            return go;
        }

        /// <summary>PLACEHOLDER_NOT_ALPHA: visible cube for an item whose model is not staged (one shared material).</summary>
        static GameObject GroundPlaceholder(RawAlphaItemId raw, Transform parent)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "GroundItem_PLACEHOLDER";
            Destroy(cube.GetComponent<Collider>());
            cube.transform.SetParent(parent, false);
            cube.transform.localScale = Vector3.one * 0.35f;
            cube.transform.localPosition = new Vector3(0f, 0.2f, 0f);
            if (_placeholderMaterial == null)
            {
                _placeholderMaterial = CubeGame.Presentation.Rendering.RuntimeMaterials.Solid(
                    "GroundItem_PLACEHOLDER", new Color(0.9f, 0.8f, 0.2f));
            }

            cube.GetComponent<Renderer>().sharedMaterial = _placeholderMaterial;
            return cube;
        }

        void PlaceGroundItem(GroundItem ground)
        {
            if (ground.Root != null && _toScene != null)
            {
                ground.Root.transform.position = _toScene(ground.Position);
            }
        }

        static void DestroyGroundItem(GroundItem ground)
        {
            if (ground.Root == null) return;
            ground.Root.SetActive(false);
            foreach (MeshFilter filter in ground.Root.GetComponentsInChildren<MeshFilter>(true))
            {
                // Only meshes this sink generated; the placeholder cube shares Unity's built-in mesh.
                if (filter.sharedMesh != null && filter.sharedMesh.name.StartsWith("ground_", StringComparison.Ordinal))
                {
                    Destroy(filter.sharedMesh);
                }
            }

            Destroy(ground.Root);
        }

        /// <summary>
        /// Alpha model of a ground item: the executed item_icon_id selector over its bytes; raw-only consumables
        /// fall back to the held-item catalogue when the selector has no entry.
        /// </summary>
        public static string GroundItemModelName(RawAlphaItemId raw, int material = 0, int rarity = 0, int seed = 0)
        {
            if (AlphaCharacterModels.TrySelectItemModel(raw.Type, raw.Subtype, material, rarity, seed, out string model, out _))
            {
                return model;
            }

            if (AlphaHeldItemCatalog.TryModel(raw.Type, raw.Subtype, out AlphaHeldItemDef held))
            {
                return held.Model;
            }

            return null;
        }

        /// <summary>Destroys a view together with the meshes its rig generated (shared materials stay).</summary>
        static void DestroyView(AlphaActorView view)
        {
            if (view == null) return;
            view.gameObject.SetActive(false);
            foreach (MeshFilter filter in view.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh != null) Destroy(filter.sharedMesh);
            }

            Destroy(view.gameObject);
        }

        public void OnActionStarted(ActionStarted e)
        {
            if (!_entries.TryGetValue(e.Entity, out Entry entry) || entry.View == null)
            {
                return;
            }

            if (!entry.View.TryPlayAction(e.Action.Value, out string reason))
            {
                Problems.Add($"ANIM_MISSING {e.Entity} action {e.Action.Value}: {reason}");
            }
        }

        public void OnActionPhaseChanged(ActionPhaseChanged e)
        {
        }

        public void OnActionResolved(ActionResolved e)
        {
        }

        public void OnHitResolved(HitResolved e)
        {
            // CUBEGAME_CONTRACT (CombatFeedback): a short scale pulse on the target; Alpha's hit effects are R-3.
            if (_entries.TryGetValue(e.Target, out Entry entry)) entry.HitTimeMs = e.TimeMs;
        }

        public void OnEntityDied(EntityDied e)
        {
            if (!_entries.TryGetValue(e.Entity, out Entry entry) || entry.Root == null)
            {
                return;
            }

            // Alpha death presentation is not restored (PLACEHOLDER_NOT_ALPHA): the body lies on its side and sinks.
            entry.Dead = true;
            entry.DeathTimeMs = e.TimeMs;
            entry.Root.rotation = Quaternion.Euler(0f, entry.Yaw, CombatFeedback.DeathTiltDegrees);
        }

        public void OnFrame(long gameTimeMs, double interpolationAlpha)
        {
            float dt = _lastTimeMs < 0 ? 0f : Mathf.Max(0f, (gameTimeMs - _lastTimeMs) / 1000f);
            _lastTimeMs = gameTimeMs;
            FlushHero();
            foreach (GroundItem ground in _groundItems.Values)
            {
                PlaceGroundItem(ground);
            }

            foreach (Entry entry in _entries.Values)
            {
                if (entry.Root == null)
                {
                    continue;
                }

                if (!entry.IsHero && _toScene != null)
                {
                    entry.Root.position = _toScene(entry.Position);
                }

                if (entry.Dead)
                {
                    // CubeGame corpse: lies on its side (set at death) and sinks; hidden when fully sunk. The hero
                    // visual is a child of the player, so its sink is a local offset (not accumulated per frame).
                    float sink = CombatFeedback.DeathSink(gameTimeMs - entry.DeathTimeMs);
                    if (entry.IsHero)
                    {
                        entry.Root.localPosition = Vector3.down * sink;
                    }
                    else
                    {
                        entry.Root.position += Vector3.down * sink;
                    }
                    if (CombatFeedback.CorpseFinished(gameTimeMs - entry.DeathTimeMs))
                    {
                        foreach (Renderer renderer in entry.Root.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
                    }

                    continue;
                }

                entry.Root.rotation = Quaternion.Euler(0f, entry.Yaw, 0f);
                float flash = CombatFeedback.FlashIntensity(gameTimeMs - entry.HitTimeMs);
                entry.Root.localScale = flash > 0f ? Vector3.one * (1f + 0.06f * flash) : Vector3.one;

                if (entry.View == null || entry.Dead)
                {
                    continue;
                }

                bool moving = entry.Speed > 0.2f && entry.Mode != LocomotionMode.Idle;
                entry.View.ApplyLocomotion(new AlphaLocomotionState
                {
                    // Speed unit mapping is OQ-23 OPEN: steady Alpha walk ≈ 3.7 at the hero walk speed (PLACEHOLDER_NOT_ALPHA).
                    SpeedXY = moving ? 3.7f * entry.Speed / 5f : 0f,
                    Amplitude = moving ? WalkAmplitude : 0f,
                    Boost = entry.Mode == LocomotionMode.Run,
                    OnGround = entry.Mode != LocomotionMode.Fall && entry.Mode != LocomotionMode.Jump,
                });
                entry.View.Tick(dt);
            }
        }

        private void OnDestroy()
        {
            foreach (Entry entry in _entries.Values)
            {
                if (entry.Root != null)
                {
                    DestroyView(entry.View);
                    Destroy(entry.Root.gameObject);
                }
            }

            _entries.Clear();
            foreach (GroundItem ground in _groundItems.Values)
            {
                DestroyGroundItem(ground);
            }

            _groundItems.Clear();
            BindItems(null);
        }

        private AlphaActorView BuildHero(Transform root, HeroVisualSpec spec)
        {
            if (_library == null || _library.State != AlphaContentState.Ready)
            {
                return null;
            }

            CharacterVisualRequest request = spec.ToRequest(_contentRoot);
            CharacterVisualResult result = AlphaPlayerVisualBuilder.Build(request, root, _library, _itemsDirectory);
            foreach (string problem in result.Problems)
            {
                Problems.Add("HERO_VISUAL " + problem);
            }

            if (result.View == null || !result.View.Ready)
            {
                // A view that failed to initialise is removed, never left under the placeholder.
                DestroyView(result.View);
                Problems.Add("DATA_MISSING hero model: " + result.Status);
                return null;
            }

            result.View.Tick(0f);
            float height = MeasureBodyHeight(result.View, out float minY);
            if (height <= 0.01f)
            {
                Problems.Add("DATA_MISSING hero model has no renderers");
                return result.View;
            }

            // PLACEHOLDER_NOT_ALPHA: the Alpha voxel → block factor is fitted once per world so that the reference
            // species HumanMale 0x02 body (no weapon, no off-hand) is HeroVisualHeight tall. M0-F2B species-scale rule:
            // Alpha draws every creature at one voxel scale (creature_getScaleVec4 = 1, CHARACTER_PROPORTIONS_PILOT), so the
            // factor never depends on the hero's own race — a dwarf hero and a dwarf NPC are drawn at the same scale, and
            // relative species size comes from the species' Alpha models only.
            if (ModelScale <= 0f)
            {
                float reference = MeasureReferenceBodyHeight(root);
                ModelScale = HeroVisualHeight / (reference > 0.01f ? reference : height);
                if (reference <= 0.01f)
                {
                    Problems.Add("SCALE_REFERENCE_MISSING HumanMale 0x02 body; fitted on the hero body");
                }
            }

            Fit(result.View.transform, minY, spec.Kind);
            return result.View;
        }

        /// <summary>
        /// M0-F2B species scale of a kind: Alpha draws each creature through a placement matrix scaled per kind (emulated
        /// <c>Creature_drawModel</c>, <see cref="AlphaCreatureSizes.TryGetSpeciesScale"/>); the captured actors have it divided
        /// out, so it is re-applied here, relative to the HumanMale reference. 1 for a kind without data.
        /// </summary>
        public static float SpeciesScale(int kind) => AlphaCreatureSizes.TryGetSpeciesScale(kind, out float factor) ? factor : 1f;

        /// <summary>Kind whose body defines <see cref="ModelScale"/> (HumanMale, the shared humanoid action rig).</summary>
        public const int ScaleReferenceKind = HeroVisualComposer.ActionRigKind;

        /// <summary>Body height (voxel units) of the reference HumanMale with the default appearance and no items; 0 if unavailable.</summary>
        private float MeasureReferenceBodyHeight(Transform near)
        {
            var temp = new GameObject("ScaleReference");
            temp.transform.SetParent(near, false);
            try
            {
                var empty = new Dictionary<VisualEquipmentSlot, HeroVisualItem>();
                HeroVisualSpec spec = HeroVisualComposer.Compose(AlphaHeroCreation.Default, empty);
                if (spec.Kind != ScaleReferenceKind)
                {
                    return 0f;
                }

                CharacterVisualResult result = AlphaPlayerVisualBuilder.Build(spec.ToRequest(_contentRoot), temp.transform, _library, _itemsDirectory);
                if (result.View == null || !result.View.Ready)
                {
                    DestroyView(result.View);
                    return 0f;
                }

                result.View.Tick(0f);
                float height = MeasureBodyHeight(result.View, out _);
                DestroyView(result.View);
                return height;
            }
            finally
            {
                temp.SetActive(false);
                Destroy(temp);
            }
        }

        /// <summary>Height of the rig parts without the weapon block and the off-hand (mesh bounds, renderer state ignored).</summary>
        private static float MeasureBodyHeight(AlphaActorView view, out float minY)
        {
            minY = 0f;
            CubeGame.AlphaTransfer.AlphaRig rig = view.Rig;
            if (rig == null) return MeasureHeight(view.transform, out minY);
            bool any = false;
            float lo = float.MaxValue, hi = float.MinValue;
            for (int i = 0; i < rig.meshes.Length; i++)
            {
                if (rig.partNames[i] == "weapon" || rig.meshes[i] == null) continue;
                MeshFilter filter = rig.meshes[i].GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                Bounds b = filter.sharedMesh.bounds;
                Matrix4x4 m = rig.meshes[i].localToWorldMatrix;
                for (int c = 0; c < 8; c++)
                {
                    var corner = new Vector3((c & 1) == 0 ? b.min.x : b.max.x, (c & 2) == 0 ? b.min.y : b.max.y, (c & 4) == 0 ? b.min.z : b.max.z);
                    float y = m.MultiplyPoint3x4(corner).y;
                    lo = Mathf.Min(lo, y);
                    hi = Mathf.Max(hi, y);
                    any = true;
                }
            }

            if (!any) return 0f;
            minY = lo;
            return hi - lo;
        }

        private AlphaActorView BuildCreature(KindId kind, Transform root, bool worldSpawn)
        {
            if (_library == null || _library.State != AlphaContentState.Ready)
            {
                return null;
            }

            if (!CreatureVisualCatalog.TryResolve(kind.Value, _library, out string actorId, out string reason))
            {
                Problems.Add("DATA_MISSING " + reason);
                return null;
            }

            if (!_library.TryGet(actorId, out AlphaActorDefinition definition))
            {
                Problems.Add("DATA_MISSING actor " + actorId);
                return null;
            }

            var problems = new List<string>();
            AlphaActorView view = AlphaActorView.Spawn(definition, root, problems);
            Problems.AddRange(problems);
            if (view == null || !view.Ready)
            {
                DestroyView(view);
                return null;
            }

            if (worldSpawn && definition.Contract?.parts != null)
            {
                // The staged rig can contain equipment from its reference actor. World spawn gear
                // needs its own template-to-item mapping (R-1); keep the rig joints for animation.
                foreach (var part in definition.Contract.parts)
                {
                    if (part.role != "equipment") continue;
                    int index = view.Rig.IndexOf(part.name);
                    if (index >= 0 && view.Rig.meshes[index] != null)
                        view.Rig.meshes[index].gameObject.SetActive(false);
                }
            }

            view.Tick(0f);
            MeasureHeight(view.transform, out float minY);
            Fit(view.transform, minY, kind.Value);
            return view;
        }

        /// <summary>
        /// Scales the model by <see cref="ModelScale"/> × the species scale of <paramref name="kind"/> (one rule for the hero
        /// and every creature, <see cref="SpeciesScale"/>) and puts its lowest point on the entity root (feet on ground).
        /// </summary>
        private void Fit(Transform model, float minY, int kind)
        {
            float scale = (ModelScale > 0f ? ModelScale : 1f) * SpeciesScale(kind);
            model.localScale = Vector3.one * scale;
            model.localPosition = new Vector3(0f, -(minY - model.parent.position.y) * scale, 0f);
        }

        private static float MeasureHeight(Transform model, out float minY)
        {
            minY = 0f;
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return 0f;
            }

            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
            }

            minY = bounds.min.y;
            return bounds.size.y;
        }

        private static GameObject MakePlaceholder(Transform root, bool hero)
        {
            GameObject body = GameObject.CreatePrimitive(hero ? PrimitiveType.Capsule : PrimitiveType.Cube);
            body.name = hero ? "PlayerBody_PLACEHOLDER" : "Body_PLACEHOLDER";
            Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(root, false);
            body.transform.localScale = hero ? new Vector3(0.8f, 0.9f, 0.8f) : new Vector3(0.9f, 0.9f, 1.4f);
            body.transform.localPosition = new Vector3(0f, hero ? 0.9f : 0.45f, 0f);
            // Explicit committed material: the primitive's built-in default material is not relied on in a Player.
            if (_bodyPlaceholderMaterial == null)
            {
                _bodyPlaceholderMaterial = CubeGame.Presentation.Rendering.RuntimeMaterials.Solid(
                    "Body_PLACEHOLDER", new Color(0.6f, 0.6f, 0.6f));
            }

            body.GetComponent<Renderer>().sharedMaterial = _bodyPlaceholderMaterial;
            return body;
        }

        private sealed class GroundItem
        {
            public GameObject Root;
            public GameObject Model;
            public string ModelName;
            public WorldPosition Position;
        }

        private sealed class Entry
        {
            public Transform Root;
            public AlphaActorView View;
            public GameObject Placeholder;
            public WorldPosition Position;
            public float Yaw;
            public float Speed;
            public LocomotionMode Mode;
            public bool IsHero;
            public bool Dead;
            public long DeathTimeMs;
            public long HitTimeMs = long.MinValue / 2;
        }
    }
}
