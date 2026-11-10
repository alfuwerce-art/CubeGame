using System;
using System.Collections.Generic;
using System.IO;
using CubeGame.AlphaModels;
using CubeGame.Content;
using CubeGame.Core.Items;
using CubeGame.Features.Contracts;
using CubeGame.Features.Creatures;
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
    /// </summary>
    public sealed class GameVisualSink : MonoBehaviour, IEntityVisualSink
    {
        /// <summary>Hero visual height in blocks: equals the hero CharacterController height.</summary>
        public const float HeroVisualHeight = 1.8f;

        /// <summary>Walk amplitude of a steady walk in the Alpha captures (creature+0x1188 ≈ 0.19).</summary>
        public const float WalkAmplitude = 0.19f;

        private readonly Dictionary<StableEntityId, Entry> _entries = new Dictionary<StableEntityId, Entry>();
        private readonly Dictionary<StableEntityId, GameObject> _groundItems = new Dictionary<StableEntityId, GameObject>();
        private AlphaActorLibrary _library;
        private Func<WorldPosition, Vector3> _toScene;
        private Transform _heroRoot;
        private string _itemsDirectory;
        private long _lastTimeMs = -1;
        private CharacterVisualRequest _heroRequest;
        private Features.Session.GameEventBus _bus;

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
                _itemsDirectory = Path.Combine(library.Source.Root, "items");
            }
            else
            {
                Problems.Add("DATA_MISSING alpha content cache: " + (library != null ? library.Diagnosis : "no library"));
            }
        }

        public bool TryGetView(StableEntityId entity, out AlphaActorView view)
        {
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
                string name = e.WorldObjectName ?? $"Entity_{e.Kind.Value:x2}";
                entry.Root = new GameObject($"{name}_{e.Entity.Value:x}").transform;
                entry.Root.SetParent(transform, false);
                entry.Root.position = _toScene != null ? _toScene(e.Position) : Vector3.zero;
            }

            entry.Root.rotation = Quaternion.Euler(0f, e.Yaw, 0f);
            if (e.WorldObjectName == null)
            {
                entry.View = isHero ? BuildHero(entry.Root) : BuildCreature(e.Kind, entry.Root);
            }
            if (entry.View == null)
            {
                entry.Placeholder = MakePlaceholder(entry.Root, isHero);
            }

            _entries[e.Entity] = entry;
        }

        public void OnEntityDespawned(EntityDespawned e)
        {
            if (_entries.TryGetValue(e.Entity, out Entry entry))
            {
                if (entry.Root != null)
                {
                    entry.Root.gameObject.SetActive(false);
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
            // D4: the loadout UI publishes raw Alpha item ids; the hero model is rebuilt with the new armour,
            // weapon or off-hand. Slots this layer cannot show (neck/rings/lamp/pet/back) are reported, not faked.
            if (!e.Entity.Equals(StableEntityId.Hero))
            {
                return;
            }

            if (!HeroLoadout.TryApply(_heroRequest, e.Slot, e.Item, _itemsDirectory, out CharacterVisualRequest updated,
                    out string problem))
            {
                Problems.Add($"EQUIP_SLOT {e.Slot}: {problem}");
                return;
            }

            _heroRequest = updated;
            RebuildHero();
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

        /// <summary>Visual of one item on the ground: the Alpha model staged for that raw item id.</summary>
        public void OnWorldItemDropped(WorldItemDropped e)
        {
            OnWorldItemRemoved(new WorldItemRemoved { Item = e.Item });
            GameObject visual = BuildGroundItem(e.Raw, e.Item);
            if (visual == null) return;
            visual.transform.SetParent(transform, false);
            if (_toScene != null) visual.transform.position = _toScene(e.Position);
            _groundItems[e.Item] = visual;
        }

        public void OnWorldItemRemoved(WorldItemRemoved e)
        {
            if (_groundItems.TryGetValue(e.Item, out GameObject visual))
            {
                Destroy(visual);
                _groundItems.Remove(e.Item);
            }
        }

        /// <summary>Number of item visuals currently shown (tests/diagnostics).</summary>
        public int GroundItemCount => _groundItems.Count;

        GameObject BuildGroundItem(RawAlphaItemId raw, StableEntityId item)
        {
            if (_itemsDirectory == null)
            {
                Problems.Add("DATA_MISSING item model: no staged items directory");
                return null;
            }

            string modelName = GroundItemModelName(raw);
            if (string.IsNullOrEmpty(modelName))
            {
                Problems.Add($"DATA_MISSING item model {raw}");
                return null;
            }

            string path = HeroLoadout.ModelFile(_itemsDirectory, modelName);
            if (path == null || !File.Exists(path))
            {
                Problems.Add("DATA_MISSING item model file " + path);
                return null;
            }

            CubeGame.AlphaTransfer.VoxelMesh.Result result =
                CubeGame.AlphaTransfer.VoxelMesh.Build(File.ReadAllBytes(path), "ground_" + item.Value.ToString("x"));
            if (result.mesh == null)
            {
                Problems.Add($"ITEM_MESH {raw}: {result.problem}");
                return null;
            }

            var go = new GameObject($"GroundItem_{item.Value:x}");
            go.AddComponent<MeshFilter>().sharedMesh = result.mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = new Material(Shader.Find("Standard"));
            go.transform.localScale = Vector3.one * (ModelScale > 0f ? ModelScale : 1f);
            go.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            return go;
        }

        /// <summary>Alpha model of a ground item: consumables/held items first, then weapon subtypes.</summary>
        public static string GroundItemModelName(RawAlphaItemId raw)
        {
            if (AlphaHeldItemCatalog.TryModel(raw.Type, raw.Subtype, out AlphaHeldItemDef held))
            {
                return held.Model;
            }

            if (raw.Type == 3 && AlphaItemRules.TryWeaponSubtype(raw.Subtype, out AlphaWeaponSubtypeDef weapon))
            {
                return weapon.DefaultModel;
            }

            if (AlphaEquipmentCatalog.TryModel(raw.Type, raw.Subtype, out AlphaEquipmentDef armour))
            {
                return armour.Model;
            }

            return null;
        }

        void RebuildHero()
        {
            if (!_entries.TryGetValue(StableEntityId.Hero, out Entry entry) || entry.Root == null) return;
            if (entry.View != null)
            {
                Destroy(entry.View.gameObject);
            }

            if (entry.Placeholder != null)
            {
                Destroy(entry.Placeholder);
                entry.Placeholder = null;
            }

            entry.View = BuildHero(entry.Root);
            if (entry.View == null)
            {
                entry.Placeholder = MakePlaceholder(entry.Root, true);
            }
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
                    // CubeGame corpse: lies on its side (set at death) and sinks; hidden when fully sunk.
                    float sink = CombatFeedback.DeathSink(gameTimeMs - entry.DeathTimeMs);
                    entry.Root.position += Vector3.down * sink;
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
                    Destroy(entry.Root.gameObject);
                }
            }

            _entries.Clear();
            foreach (GameObject visual in _groundItems.Values)
            {
                if (visual != null) Destroy(visual);
            }

            _groundItems.Clear();
            BindItems(null);
        }

        private AlphaActorView BuildHero(Transform root)
        {
            if (_library == null || _library.State != AlphaContentState.Ready)
            {
                return null;
            }

            CharacterVisualRequest request = _heroRequest;
            if (request.Kind == 0)
            {
                request = CharacterVisualRequest.Default;
                request.Kind = Features.Session.GameSession.HeroKind.Value;
                string weapon = _itemsDirectory != null ? Path.Combine(_itemsDirectory, WeaponModelFile(WeaponSubtype.Greatsword)) : null;
                if (weapon != null && File.Exists(weapon))
                {
                    request.WeaponSubtype = (int)WeaponSubtype.Greatsword;
                    request.WeaponModelPath = weapon;
                }
                else
                {
                    Problems.Add("DATA_MISSING hero weapon model " + weapon);
                }

                _heroRequest = request;
            }

            CharacterVisualResult result = AlphaPlayerVisualBuilder.Build(request, root, _library, _itemsDirectory);
            Problems.AddRange(result.Problems);
            if (result.View == null || !result.View.Ready)
            {
                Problems.Add("DATA_MISSING hero model: " + result.Status);
                return null;
            }

            result.View.Tick(0f);
            float height = MeasureHeight(result.View.transform, out float minY);
            if (height <= 0.01f)
            {
                Problems.Add("DATA_MISSING hero model has no renderers");
                return result.View;
            }

            ModelScale = HeroVisualHeight / height;
            Fit(result.View.transform, minY);
            return result.View;
        }

        private AlphaActorView BuildCreature(KindId kind, Transform root)
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
                return null;
            }

            view.Tick(0f);
            MeasureHeight(view.transform, out float minY);
            Fit(view.transform, minY);
            return view;
        }

        /// <summary>Scales the model by <see cref="ModelScale"/> and puts its lowest point on the entity root (feet on ground).</summary>
        private void Fit(Transform model, float minY)
        {
            float scale = ModelScale > 0f ? ModelScale : 1f;
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

        /// <summary>
        /// Weapon model file of the staged item payloads. Alpha picks the model by material/rarity (item_icon_id);
        /// the iron variant is a CubeGame choice (PLACEHOLDER_NOT_ALPHA) until M0-04B maps items to models.
        /// </summary>
        public static string WeaponModelFile(WeaponSubtype subtype)
        {
            switch (subtype)
            {
                case WeaponSubtype.Sword: return "iron-sword1.cubdec";
                case WeaponSubtype.Axe: return "iron-axe1.cubdec";
                case WeaponSubtype.Mace: return "iron-mace1.cubdec";
                case WeaponSubtype.Dagger: return "iron-dagger1.cubdec";
                case WeaponSubtype.Fist: return "iron-fist1.cubdec";
                case WeaponSubtype.Longsword: return "iron-longsword1.cubdec";
                case WeaponSubtype.Greatsword: return "iron-greatsword1.cubdec";
                case WeaponSubtype.Greataxe: return "iron-greataxe1.cubdec";
                case WeaponSubtype.Greatmace: return "iron-greatmace1.cubdec";
                default: return string.Empty;
            }
        }

        private static GameObject MakePlaceholder(Transform root, bool hero)
        {
            GameObject body = GameObject.CreatePrimitive(hero ? PrimitiveType.Capsule : PrimitiveType.Cube);
            body.name = hero ? "PlayerBody_PLACEHOLDER" : "Body_PLACEHOLDER";
            Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(root, false);
            body.transform.localScale = hero ? new Vector3(0.8f, 0.9f, 0.8f) : new Vector3(0.9f, 0.9f, 1.4f);
            body.transform.localPosition = new Vector3(0f, hero ? 0.9f : 0.45f, 0f);
            return body;
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
