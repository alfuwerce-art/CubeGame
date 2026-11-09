using System;
using System.Collections.Generic;
using System.IO;
using CubeGame.AlphaModels;
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
        private AlphaActorLibrary _library;
        private Func<WorldPosition, Vector3> _toScene;
        private Transform _heroRoot;
        private string _itemsDirectory;
        private long _lastTimeMs = -1;

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
                entry.Root = new GameObject($"Entity_{e.Kind.Value:x2}_{e.Entity.Value:x}").transform;
                entry.Root.SetParent(transform, false);
                entry.Root.position = _toScene != null ? _toScene(e.Position) : Vector3.zero;
            }

            entry.Root.rotation = Quaternion.Euler(0f, e.Yaw, 0f);
            entry.View = isHero ? BuildHero(entry.Root) : BuildCreature(e.Kind, entry.Root);
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
            // M0-04B: the loadout UI publishes this; the starting weapon is applied when the hero model is built.
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
        }

        public void OnEntityDied(EntityDied e)
        {
            if (!_entries.TryGetValue(e.Entity, out Entry entry) || entry.Root == null)
            {
                return;
            }

            // Alpha death presentation is not restored (PLACEHOLDER_NOT_ALPHA): the body lies on its side.
            entry.Dead = true;
            entry.Root.rotation = Quaternion.Euler(0f, entry.Yaw, 90f);
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

                if (!entry.Dead)
                {
                    entry.Root.rotation = Quaternion.Euler(0f, entry.Yaw, 0f);
                }

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
        }

        private AlphaActorView BuildHero(Transform root)
        {
            if (_library == null || _library.State != AlphaContentState.Ready)
            {
                return null;
            }

            CharacterVisualRequest request = CharacterVisualRequest.Default;
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
        }
    }
}
