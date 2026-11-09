using System;
using System.Globalization;
using CubeGame.AlphaModels;
using CubeGame.Core.Characters;
using CubeGame.Core.Persistence;
using CubeGame.Features.Combat;
using CubeGame.Features.Items;
using CubeGame.Core.Equipment;
using CubeGame.Core.Items;
using CubeGame.Features.Contracts;
using CubeGame.Features.Persistence;
using CubeGame.Features.Session;
using CubeGame.Features.World;
using CubeGame.Gameplay.Streaming;
using CubeGame.Persistence;
using CubeGame.Presentation.CameraRig;
using CubeGame.Presentation.Player;
using CubeGame.Presentation.Rendering;
using UnityEngine;

namespace CubeGame.Gameplay
{
    /// <summary>
    /// M0-01 entry point of the single CubeGame scene: owns one <see cref="GameSession"/> and presents its
    /// states (main menu → loading → in game ⇄ pause → save → exit to menu / quit). This is the only place
    /// that turns real frame time into simulation time (GameClock). World/player views are built when a
    /// world is entered and destroyed on exit, so "menu → world → menu → world" leaves no leftovers.
    ///
    /// Command line (automation): -cubeSaveDir &lt;dir&gt;, -cubeAutoNew &lt;slot&gt;, -cubeAutoLoad &lt;slot&gt;,
    /// -cubeSeed &lt;uint&gt;, -cubeScreenshot &lt;png&gt; (+ -cubeFrames &lt;n&gt;): capture then quit;
    /// -cubeCamera &lt;yaw&gt; &lt;pitch&gt; &lt;distance&gt;: orbit camera at world entry; -cubeAttackFrame &lt;n&gt;: hero attacks once.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        /// <summary>
        /// World generator in use (M0-02): Alpha terrain reconstruction, see <see cref="AlphaTerrainGenerator"/>.
        /// Saves record the id; a save made by another generator (e.g. the M0-01 pilot height field) is refused.
        /// </summary>
        public const string GeneratorId = AlphaTerrainGenerator.Id;

        public const uint DefaultSeed = 666666u;

        /// <summary>Hero primary attack: Alpha action 57 (two-handed greatsword attack, S05A-2 / C-31).</summary>
        public static readonly ActionId HeroAttackAction = new ActionId(57);

        /// <summary>Training enemy species: Alpha Wolf 0x1c (staged S05 visual with actions).</summary>
        public static readonly KindId TrainingTargetKind = new KindId(0x1c);

        /// <summary>Hero respawn delay after death, game ms. PLACEHOLDER_NOT_ALPHA: Alpha's death/respawn rule is not restored (GS-06).</summary>
        public const long HeroRespawnDelayMs = 5000;

        /// <summary>Training target distance from the hero, blocks (CubeGame test setup, not Alpha; inside the 3-block reach).</summary>
        public const double TrainingTargetDistance = 2.5;

        /// <summary>
        /// Training target bearing relative to the hero's facing, degrees: off the camera line so the hero does not hide it,
        /// still inside the ±70° melee sector (CubeGame test setup, not Alpha).
        /// </summary>
        public const double TrainingTargetBearing = 35.0;

        public static readonly string[] SlotKeys = { "slot0", "slot1", "slot2" };

        [SerializeField] private Material terrainMaterial;

        /// <summary>32-voxel chunks shown around the hero / kept before release (CubeGame decision, PLACEHOLDER until profiling).</summary>
        [SerializeField] private int viewRadius = 6;
        [SerializeField] private int keepRadius = 8;

        /// <summary>Scene distance from the origin that triggers a floating-origin rebase (voxels).</summary>
        public const float RebaseDistance = 512f;

        private GameSession _session;
        private GameSlotStore _slots;
        private string _saveDirectory;

        private GameObject _worldRoot;
        private StreamingWorldView _worldView;
        private WorldState _worldState;
        private GameObject _player;
        private PlayerMotor _motor;
        private OrbitCameraController _orbitCamera;
        private Camera _camera;

        private string _status = string.Empty;
        private string _combatLog = string.Empty;
        private Combatant _trainingTarget;
        private ulong _runtimeSerial;
        private bool _trainingTargetPending;
        private GameVisualSink _visuals;
        private AlphaActionTimingSource _timings;
        private CreatureCombatDriver _creatures;
        private long _heroRespawnAtMs = -1;
        private bool _inventoryOpen;
        private Vector2 _inventoryScroll;
        private readonly System.Collections.Generic.Dictionary<StableEntityId, GameObject> _groundItems =
            new System.Collections.Generic.Dictionary<StableEntityId, GameObject>();
        private Vector3 _lastHeroScenePosition;
        private int _frames;
        private string _screenshotPath;
        private int _screenshotFrame = -1;
        private string _autoNewSlot;
        private string _autoLoadSlot;
        private uint _commandLineSeed = DefaultSeed;
        private bool _teleport;
        private bool _cameraOverride;
        private float _cameraYaw;
        private float _cameraPitch;
        private float _cameraDistance = OrbitCameraController.DefaultDistance;
        private int _attackFrame = -1;
        private int _teleportDx;
        private int _teleportDz;

        // Main menu form state.
        private string _formWorldName = "New World";
        private string _formSeed = DefaultSeed.ToString(CultureInfo.InvariantCulture);
        private string _formHeroName = "Hero";
        private int _formClass;
        private int _formSlot;
        private HeroAppearance _formLook = AlphaHeroCreation.Default;

        public GameSession Session => _session;

        public StreamingWorldView WorldView => _worldView;

        public WorldState WorldState => _worldState;

        public Transform PlayerTransform => _player != null ? _player.transform : null;

        public string Status => _status;

        public string SaveDirectory
        {
            get => _saveDirectory;
            set
            {
                _saveDirectory = value;
                _slots = CreateSlots(value);
            }
        }

        public GameSlotStore Slots => _slots;

        private void Awake()
        {
            ParseArguments();
            if (_slots == null)
            {
                _slots = CreateSlots(_saveDirectory);
            }

            _timings = new AlphaActionTimingSource(AlphaActorLibrary.Shared);
            _session = new GameSession(_timings) { ConsumableCooldownMs = CubeGame.Content.AlphaConsumableRules.CooldownMs };
            _session.EnterMainMenu();
            EnsureCameraAndLight();
            _formSeed = _commandLineSeed.ToString(CultureInfo.InvariantCulture);
            Debug.Log($"[Game] boot -> {_session.State}; saves={DescribeSaveDirectory()}");
        }

        private void Start()
        {
            if (!string.IsNullOrEmpty(_autoLoadSlot) && LoadWorld(_autoLoadSlot))
            {
                return;
            }

            if (!string.IsNullOrEmpty(_autoNewSlot))
            {
                StartNewWorld(_autoNewSlot, "Auto World", _commandLineSeed, "Hero", CharacterClass.Warrior);
            }

            if (_teleport && _session.State == SessionState.InGame)
            {
                TeleportHero(WorldLayout.StartVoxel + _teleportDx, WorldLayout.StartVoxel + _teleportDz);
            }
        }

        private void Update()
        {
            _frames++;
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_inventoryOpen)
                {
                    ToggleInventory();
                }
                else
                {
                    TogglePause();
                }
            }

            if (_session.State == SessionState.InGame && Input.GetKeyDown(KeyCode.I))
            {
                ToggleInventory();
            }

            if (_session.State == SessionState.InGame && !_inventoryOpen && Input.GetKeyDown(KeyCode.Q))
            {
                UseFirst(LootRules.LifePotion);
            }

            if (_session.State == SessionState.InGame)
            {
                if (_motor != null && _motor.enabled && Input.GetMouseButtonDown(0) && GUIUtility.hotControl == 0)
                {
                    HeroAttack();
                }

                _session.AdvanceFrame(Time.deltaTime);
                RebaseIfFar();
                SyncHeroFromView();
                PublishHeroMovement();
                if (_heroRespawnAtMs >= 0 && _session.Clock.TimeMs >= _heroRespawnAtMs)
                {
                    RespawnHero();
                }

                if (_trainingTargetPending && _motor != null && _motor.IsGrounded)
                {
                    _trainingTargetPending = false;
                    SpawnTrainingTarget();
                }
                if (_worldView != null && _player != null)
                {
                    _worldView.SetFocus(_player.transform.position);
                }
            }

            if (_attackFrame >= 0 && _frames >= _attackFrame && _session.State == SessionState.InGame && _trainingTarget != null)
            {
                _attackFrame = -1;
                HeroAttack();
            }

            if (_screenshotFrame >= 0 && _frames >= _screenshotFrame)
            {
                _screenshotFrame = -1;
                ScreenCapture.CaptureScreenshot(_screenshotPath);
                Debug.Log($"[Game] screenshot -> {_screenshotPath} state={_session.State} tick={_session.Clock?.Tick}");
                StartCoroutine(QuitAfterFrames(3));
            }
        }

        // ----- Public game API (UI, automation, PlayMode tests) -----

        public bool StartNewWorld(string slotKey, string worldName, uint seed, string heroName, CharacterClass heroClass, HeroAppearance? appearance = null)
        {
            var request = new NewWorldRequest
            {
                WorldName = worldName,
                Seed = seed,
                GeneratorId = GeneratorId,
                HeroName = heroName,
                Class = heroClass,
                Specialization = CharacterSpecialization.Primary,
                Appearance = appearance ?? AlphaHeroCreation.Default,
            };

            if (!_session.BeginNewWorld(slotKey, request, DateTime.UtcNow.Ticks))
            {
                return Report(false, $"new world refused: {_session.LastRejection}");
            }

            return EnterLoadedWorld($"new world '{worldName}' seed={seed} in {slotKey}");
        }

        public bool LoadWorld(string slotKey)
        {
            SlotLoadResult result = _slots.Load(slotKey, GeneratorId);
            if (!result.IsSuccess)
            {
                return Report(false, $"load {slotKey} failed: {result}");
            }

            if (!_session.BeginLoadedWorld(slotKey, result.World, result.Hero, result.ClockTick, result.WorldDeltas))
            {
                return Report(false, $"load refused: {_session.LastRejection}");
            }

            return EnterLoadedWorld($"loaded {slotKey}: '{result.World.Name}' tick={result.ClockTick}");
        }

        public bool SaveCheckpoint()
        {
            if (HeroDead)
            {
                return Report(false, "save refused: the hero is dead");
            }

            SyncHeroFromView();
            if (!_session.BeginSave())
            {
                return Report(false, $"save refused: {_session.LastRejection}");
            }

            SaveResult result = _slots.Save(_session, DateTime.UtcNow.Ticks);
            _session.EndSave();
            return Report(result.IsSuccess, result.IsSuccess
                ? $"saved {_session.SlotKey} tick={_session.Clock.Tick}"
                : $"save failed: {result}");
        }

        public bool ExitToMenu(bool saveFirst)
        {
            if (saveFirst && !SaveCheckpoint())
            {
                return false;
            }

            if (!_session.ExitToMenu())
            {
                return Report(false, $"exit refused: {_session.LastRejection}");
            }

            DestroyWorldViews();
            return Report(true, "returned to main menu");
        }

        /// <summary>
        /// Sets the top solid block of one column through the world delta store (persistent, saved with the slot).
        /// Gameplay entry point for terrain changes (Alpha: bombs / changed-voxel flag 0x80); the view rebuilds affected regions.
        /// </summary>
        public bool EditColumn(int blockX, int blockZ, int surface)
        {
            if (_session.State != SessionState.InGame || _session.WorldDeltas == null)
            {
                return Report(false, "terrain edit refused: no running world");
            }

            return _session.WorldDeltas.SetColumnSurface(blockX, blockZ, surface, _session.Clock.TimeMs)
                || Report(false, $"terrain edit refused at ({blockX},{blockZ})");
        }

        /// <summary>Sets one voxel (packed Alpha voxel, 0 = air) through the world delta store (persistent, saved with the slot).</summary>
        public bool EditVoxel(int blockX, int blockY, int blockZ, uint voxel)
        {
            if (_session.State != SessionState.InGame || _session.WorldDeltas == null)
            {
                return Report(false, "voxel edit refused: no running world");
            }

            return _session.WorldDeltas.SetVoxel(blockX, blockY, blockZ, voxel, _session.Clock.TimeMs)
                || Report(false, $"voxel edit refused at ({blockX},{blockY},{blockZ})");
        }

        public Combatant TrainingTarget => _trainingTarget;

        public string CombatLog => _combatLog;

        /// <summary>Hero primary attack (left mouse). Gameplay decides phases and damage; visuals only follow events.</summary>
        public bool HeroAttack()
        {
            if (_session.State != SessionState.InGame || _session.Combat == null)
            {
                return Report(false, "attack refused: no running world");
            }

            SyncHeroFromView();
            if (!_session.Combat.TryStartAttack(_session.Hero.Id, HeroAttackAction, _session.Clock.TimeMs, "action_057"))
            {
                _combatLog = $"attack refused: {_session.Combat.LastRejection}";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Spawns a hostile training target in front of the hero (runtime entity, not saved; M0-06 replaces it with
        /// world spawns). HP from Core for a level-1 Warrior sheet: PLACEHOLDER_NOT_ALPHA for creature HP.
        /// </summary>
        public Combatant SpawnTrainingTarget()
        {
            if (_session.State != SessionState.InGame || _session.Combat == null || _worldState == null)
            {
                Report(false, "spawn refused: no running world");
                return null;
            }

            _trainingTargetPending = false;
            DespawnTrainingTarget();
            SyncHeroFromView();
            HeroState hero = _session.Hero;
            double yaw = (hero.Yaw + TrainingTargetBearing) * Math.PI / 180.0;
            double x = hero.Position.X + Math.Sin(yaw) * TrainingTargetDistance;
            double z = hero.Position.Z + Math.Cos(yaw) * TrainingTargetDistance;
            int ground = Math.Max(_worldState.SurfaceAt((int)Math.Floor(x), (int)Math.Floor(z)), _worldState.Generator.WaterLevel);
            double y = Math.Abs(ground + 1 - hero.Position.Y) <= 2.0 ? ground + 1 : hero.Position.Y;

            var target = Combatant.CreateCreature(StableEntityId.ForRuntime(++_runtimeSerial), TrainingTargetKind, "Wolf", 1, 0);
            target.Position = new WorldPosition(x, y, z);
            target.Yaw = hero.Yaw + 180f;
            _session.Combat.Register(target);
            _creatures?.Add(target, 1, 0);
            _trainingTarget = target;

            _session.Events.Publish(new EntitySpawned
            {
                TimeMs = _session.Clock.TimeMs,
                Entity = target.Id,
                Kind = target.Kind,
                Position = target.Position,
                Yaw = target.Yaw,
            });
            _combatLog = $"spawned {target}";
            Debug.Log($"[Game] training target {target} at {target.Position} scene {_worldView.ToScene(x, y, z)} " +
                      $"hero {hero.Position} yaw {hero.Yaw:F1} scene {_player.transform.position} " +
                      $"visual {(_visuals != null && _visuals.TryGetRoot(target.Id, out Transform root) ? root.position.ToString() : "none")} " +
                      $"problems [{(_visuals != null ? string.Join("; ", _visuals.Problems) : string.Empty)}]");
            return target;
        }

        private void DespawnTrainingTarget()
        {
            if (_trainingTarget != null && _session.Combat != null)
            {
                _session.Combat.Unregister(_trainingTarget.Id);
                _creatures?.Remove(_trainingTarget.Id);
                _session.Events.Publish(new EntityDespawned { TimeMs = _session.Clock?.TimeMs ?? 0, Entity = _trainingTarget.Id });
            }

            _trainingTarget = null;
        }

        private void OnHitResolved(HitResolved hit)
        {
            _combatLog = $"hit {hit.Target} damage {hit.Damage:F1} hp left {hit.TargetHealthAfter:F1}{(hit.Killed ? " KILLED" : string.Empty)}";
            Debug.Log($"[Combat] {_combatLog}");
        }

        /// <summary>Uses the first bag stack of an Alpha item (Q: life potion).</summary>
        public bool UseFirst(RawAlphaItemId raw)
        {
            if (_session.Hero == null)
            {
                return false;
            }

            int slot = _session.Hero.Inventory.FindFirst(raw);
            if (slot < 0)
            {
                _combatLog = $"no {raw} in the bags";
                return false;
            }

            bool used = _session.TryUseItem(slot);
            _combatLog = used ? $"used {raw}" : $"use refused: {_session.LastRejection}";
            return used;
        }

        public void ToggleInventory()
        {
            _inventoryOpen = !_inventoryOpen;
            if (_motor != null)
            {
                _motor.enabled = !_inventoryOpen && !HeroDead;
            }
        }

        public bool InventoryOpen => _inventoryOpen;

        private void OnEquipmentChanged(EquipmentSlot slot, ItemInstance item)
        {
            _session.Events.Publish(new EquipmentVisualChanged
            {
                TimeMs = _session.Clock?.TimeMs ?? 0,
                Entity = StableEntityId.Hero,
                Slot = VisualSlotOf(slot, item),
                Item = item != null ? new RawAlphaItemId((int)item.Type, item.Subtype) : (RawAlphaItemId?)null,
            });
        }

        private void PublishEquipment()
        {
            foreach (EquipmentSlot slot in EquipmentLoadout.Slots)
            {
                ItemInstance item = _session.Hero.Loadout.GetEquipped(slot);
                if (item != null)
                {
                    OnEquipmentChanged(slot, item);
                }
            }
        }

        /// <summary>Visual slot of an equipment slot (Alpha type 5 gloves → hands, 6 boots → feet).</summary>
        public static VisualEquipmentSlot VisualSlotOf(EquipmentSlot slot, ItemInstance item)
        {
            switch (slot)
            {
                case EquipmentSlot.MainHand: return VisualEquipmentSlot.MainHand;
                case EquipmentSlot.OffHand: return VisualEquipmentSlot.OffHand;
                case EquipmentSlot.Chest: return VisualEquipmentSlot.Chest;
                case EquipmentSlot.Shoulders: return VisualEquipmentSlot.Shoulders;
                case EquipmentSlot.Gloves: return VisualEquipmentSlot.Hands;
                case EquipmentSlot.Boots: return VisualEquipmentSlot.Feet;
                default: return VisualEquipmentSlot.MainHand;
            }
        }

        private void OnWorldItemDropped(WorldItemDropped dropped)
        {
            if (_worldView == null)
            {
                return;
            }

            // Ground item placeholder until the visual layer shows item models (D88: DeepSeek).
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "GroundItem_PLACEHOLDER";
            Destroy(cube.GetComponent<Collider>());
            cube.transform.SetParent(_worldRoot.transform, true);
            cube.transform.localScale = Vector3.one * 0.35f;
            cube.transform.position = _worldView.ToScene(dropped.Position.X, dropped.Position.Y + 0.2, dropped.Position.Z);
            cube.GetComponent<Renderer>().material.color = dropped.Raw.Type == 1 ? Color.red : new Color(0.9f, 0.8f, 0.2f);
            _groundItems[dropped.Item] = cube;
        }

        private void OnWorldItemRemoved(WorldItemRemoved removed)
        {
            if (_groundItems.TryGetValue(removed.Item, out GameObject cube))
            {
                Destroy(cube);
                _groundItems.Remove(removed.Item);
            }

            _combatLog = "picked up an item";
        }

        public bool HeroDead => _session.Hero != null && _session.Hero.Health.IsDead;

        public CreatureCombatDriver Creatures => _creatures;

        private double GroundAt(double x, double z)
        {
            if (_worldState == null)
            {
                return 0.0;
            }

            return Math.Max(_worldState.SurfaceAt((int)Math.Floor(x), (int)Math.Floor(z)), _worldState.Generator.WaterLevel) + 1;
        }

        /// <summary>Brings a dead hero back with full HP at the world start (PLACEHOLDER_NOT_ALPHA, GS-06).</summary>
        public void RespawnHero()
        {
            _heroRespawnAtMs = -1;
            HeroState hero = _session.Hero;
            if (hero == null)
            {
                return;
            }

            if (hero.Health.IsDead && !_session.ReviveHero())
            {
                Report(false, _session.LastRejection);
                return;
            }

            WorldPosition start = WorldState.StartPosition(_worldState.Generator, _session.WorldDeltas);
            TeleportHero((int)Math.Floor(start.X), (int)Math.Floor(start.Z));
            SetGameplayInput(true);
            PublishHeroSpawn();
            _combatLog = "respawned at the world start";
        }

        private void OnEntityDied(EntityDied died)
        {
            if (died.Entity.Equals(StableEntityId.Hero))
            {
                _heroRespawnAtMs = died.TimeMs + HeroRespawnDelayMs;
                SetGameplayInput(false);
                _combatLog = $"you died - respawn in {HeroRespawnDelayMs / 1000} s";
                return;
            }

            if (_trainingTarget != null && died.Entity.Equals(_trainingTarget.Id))
            {
                _combatLog += $"  {_trainingTarget.Name} died";
            }
        }

        public GameVisualSink Visuals => _visuals;

        /// <summary>Publishes the hero's movement (position, facing, speed, mode) and drives the visual frame.</summary>
        private void PublishHeroMovement()
        {
            if (_player == null || _session.Hero == null)
            {
                return;
            }

            Vector3 scene = _player.transform.position;
            Vector3 delta = scene - _lastHeroScenePosition;
            _lastHeroScenePosition = scene;
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            float speed = new Vector2(delta.x, delta.z).magnitude / dt;
            bool grounded = _motor == null || _motor.IsGrounded;
            LocomotionMode mode = !grounded ? (delta.y > 0f ? LocomotionMode.Jump : LocomotionMode.Fall)
                : speed < 0.2f ? LocomotionMode.Idle
                : _motor != null && _motor.Sprinting ? LocomotionMode.Run : LocomotionMode.Walk;

            HeroState hero = _session.Hero;
            _session.Events.Publish(new EntityMoved
            {
                TimeMs = _session.Clock.TimeMs,
                Entity = hero.Id,
                Position = hero.Position,
                Yaw = hero.Yaw,
                Mode = mode,
                Speed = speed,
            });

            long frameMs = (long)((_session.Clock.Tick + _session.Clock.Alpha) * 1000.0 / _session.Clock.TicksPerSecond);
            foreach (IEntityVisualSink sink in _session.Events.VisualSinks)
            {
                sink.OnFrame(frameMs, _session.Clock.Alpha);
            }
        }

        /// <summary>Automation/debug: places the hero above column (x, z) and loads the surroundings.</summary>
        public void TeleportHero(int x, int z)
        {
            if (_worldView == null || _player == null)
            {
                return;
            }

            int ground = Math.Max(_worldState.SurfaceAt(x, z), _worldState.Generator.WaterLevel);
            Vector2Int origin = StreamingWorldView.OriginFor(x, z);
            Vector3 shift = _worldView.Rebase(origin);
            _player.transform.position = _worldView.ToScene(x + 0.5, ground + 3.0, z + 0.5);
            Physics.SyncTransforms();
            _lastHeroScenePosition = _player.transform.position;
            _worldView.LoadAround(_player.transform.position, 2);
            SyncHeroFromView();
            Report(true, $"teleported to ({x},{z}) ground {ground} (shift {shift})");
        }

        public void TogglePause()
        {
            if (_session.State == SessionState.InGame)
            {
                _session.Pause();
                SetGameplayInput(false);
            }
            else if (_session.State == SessionState.Paused)
            {
                _session.Resume();
                SetGameplayInput(true);
            }
        }

        public void QuitGame()
        {
            _session.Quit();
            DestroyWorldViews();
            Debug.Log("[Game] quit");
#if UNITY_EDITOR
            if (!Application.isBatchMode && UnityEditor.EditorApplication.isPlaying)
            {
                UnityEditor.EditorApplication.isPlaying = false;
                return;
            }
#endif
            Application.Quit(0);
        }

        // ----- World / view lifecycle -----

        private bool EnterLoadedWorld(string description)
        {
            try
            {
                BuildWorldViews();
            }
            catch (Exception exception)
            {
                DestroyWorldViews();
                _session.AbortLoading(exception.Message);
                return Report(false, $"world build failed: {exception.Message}");
            }

            if (!_session.CompleteLoading())
            {
                return Report(false, $"loading not completed: {_session.LastRejection}");
            }

            SetGameplayInput(true);
            PublishHeroSpawn();
            _session.Events.Subscribe<HitResolved>(OnHitResolved);
            _session.Events.Subscribe<EntityDied>(OnEntityDied);
            _heroRespawnAtMs = -1;
            _session.Events.Subscribe<WorldItemDropped>(OnWorldItemDropped);
            _session.Events.Subscribe<WorldItemRemoved>(OnWorldItemRemoved);
            _session.Hero.Inventory.EquipmentChanged += OnEquipmentChanged;
            PublishEquipment();
            _creatures = new CreatureCombatDriver(_session, GroundAt, kind => _timings.AttackActionsOf(kind), _session.World.Seed.Value);
            _session.Clock.Ticked += _creatures.Tick;
            // The target appears once the hero stands on the ground and the camera has a facing (see Update).
            _trainingTargetPending = true;
            return Report(true, description);
        }

        private void BuildWorldViews()
        {
            DestroyWorldViews();
            WorldInfo world = _session.World;
            HeroState hero = _session.Hero;

            if (!WorldState.TryCreateGenerator(world.GeneratorId, world.Seed.Value, out IWorldGenerator generator))
            {
                throw new InvalidOperationException($"unknown world generator '{world.GeneratorId}'");
            }

            _worldState = new WorldState(generator, _session.WorldDeltas);
            if (hero.NeedsGroundPlacement)
            {
                hero.Position = WorldState.StartPosition(generator, _session.WorldDeltas);
                hero.NeedsGroundPlacement = false;
            }

            _worldRoot = new GameObject("WorldRoot");
            _worldView = _worldRoot.AddComponent<StreamingWorldView>();
            _worldView.Initialise(_worldState, new WorldStreamingPolicy(viewRadius, keepRadius), terrainMaterial,
                StreamingWorldView.OriginFor(hero.Position.X, hero.Position.Z));
            Vector3 heroPoint = _worldView.ToScene(hero.Position.X, hero.Position.Y, hero.Position.Z);
            var loadTimer = System.Diagnostics.Stopwatch.StartNew();
            _worldView.LoadAround(heroPoint, 2);
            loadTimer.Stop();

            _player = new GameObject("Player");
            var controller = _player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.4f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.stepOffset = 0.35f;
            controller.slopeLimit = 50f;
            _player.transform.SetPositionAndRotation(heroPoint, Quaternion.Euler(0f, hero.Yaw, 0f));
            Physics.SyncTransforms();

            // Visual runtime: S05 Alpha models and clips driven only by gameplay events (placeholders when the cache is missing).
            var visualObject = new GameObject("GameVisuals");
            visualObject.transform.SetParent(_worldRoot.transform, false);
            _visuals = visualObject.AddComponent<GameVisualSink>();
            StreamingWorldView view = _worldView;
            _visuals.Initialise(AlphaActorLibrary.Shared, p => view.ToScene(p.X, p.Y, p.Z), _player.transform);
            _session.Events.AddVisualSink(_visuals);
            _lastHeroScenePosition = heroPoint;

            _motor = _player.AddComponent<PlayerMotor>();
            _motor.SetCamera(_camera.transform);
            WorldState waterWorld = _worldState;
            _motor.WaterAt = p =>
            {
                view.ToWorld(p, out double wx, out double wy, out double wz);
                return Voxel.IsWater(waterWorld.VoxelAt((int)Math.Floor(wx), (int)Math.Floor(wy), (int)Math.Floor(wz)));
            };
            _orbitCamera.Target = _player.transform;
            _orbitCamera.Distance = OrbitCameraController.DefaultDistance;
            if (_cameraOverride)
            {
                _orbitCamera.Distance = _cameraDistance;
                _orbitCamera.SetAngles(_cameraYaw, _cameraPitch);
            }

            var region = generator.Region(WorldLayout.RegionOf((int)Math.Floor(hero.Position.X), (int)Math.Floor(hero.Position.Z)));
            Debug.Log($"[Game] world built seed={world.Seed} gen={world.GeneratorId} chunks={_worldView.LoadedCount} origin={_worldView.Origin} " +
                      $"vertices={_worldView.LoadedVertices} {loadTimer.Elapsed.TotalMilliseconds:F1}ms hero={hero.Position} " +
                      $"region={region.Key} level={region.Level} base={region.Climate?.BaseHeight}");
        }

        private void DestroyWorldViews()
        {
            DespawnTrainingTarget();
            foreach (GameObject ground in _groundItems.Values)
            {
                if (ground != null)
                {
                    Destroy(ground);
                }
            }

            _groundItems.Clear();
            if (_session?.Hero != null)
            {
                _session.Hero.Inventory.EquipmentChanged -= OnEquipmentChanged;
            }

            _inventoryOpen = false;
            _trainingTargetPending = false;
            _heroRespawnAtMs = -1;
            _creatures = null;
            _combatLog = string.Empty;
            if (_orbitCamera != null)
            {
                _orbitCamera.Target = null;
            }

            if (_worldView != null)
            {
                _worldView.Clear();
            }

            if (_worldRoot != null)
            {
                Destroy(_worldRoot);
            }

            if (_player != null)
            {
                Destroy(_player);
            }

            _worldRoot = null;
            _worldView = null;
            _visuals = null;
            _worldState = null;
            _player = null;
            _motor = null;
            SetCursorLocked(false);
        }

        private void SyncHeroFromView()
        {
            if (_player == null || _session.Hero == null)
            {
                return;
            }

            if (_worldView != null)
            {
                _worldView.ToWorld(_player.transform.position, out double x, out double y, out double z);
                _session.Hero.Position = new WorldPosition(x, y, z);
            }
            _session.Hero.Yaw = _orbitCamera != null ? _orbitCamera.Yaw : _player.transform.eulerAngles.y;
        }

        /// <summary>Floating origin: keeps scene coordinates small although Alpha positions reach 2^24 voxels.</summary>
        private void RebaseIfFar()
        {
            if (_worldView == null || _player == null)
            {
                return;
            }

            Vector3 p = _player.transform.position;
            if (Mathf.Abs(p.x) < RebaseDistance && Mathf.Abs(p.z) < RebaseDistance)
            {
                return;
            }

            _worldView.ToWorld(p, out double x, out _, out double z);
            Vector3 shift = _worldView.Rebase(StreamingWorldView.OriginFor(x, z));
            _player.transform.position = p + shift;
            _lastHeroScenePosition += shift;
            if (_camera != null)
            {
                _camera.transform.position += shift;
            }

            Physics.SyncTransforms();
            Debug.Log($"[Game] floating origin -> {_worldView.Origin}");
        }

        private void PublishHeroSpawn()
        {
            HeroState hero = _session.Hero;
            _session.Events.Publish(new EntitySpawned
            {
                TimeMs = _session.Clock.TimeMs,
                Entity = hero.Id,
                Kind = hero.Kind,
                Position = hero.Position,
                Yaw = hero.Yaw,
                Appearance = hero.Appearance,
            });
        }

        private void SetGameplayInput(bool enabled)
        {
            if (_motor != null)
            {
                _motor.enabled = enabled;
            }

            SetCursorLocked(false);
        }

        private static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void EnsureCameraAndLight()
        {
            _camera = Camera.main;
            if (_camera == null)
            {
                var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
                _camera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
                _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.backgroundColor = new Color(0.45f, 0.62f, 0.82f);
                _camera.farClipPlane = 600f;
            }

            _orbitCamera = _camera.GetComponent<OrbitCameraController>();
            if (_orbitCamera == null)
            {
                _orbitCamera = _camera.gameObject.AddComponent<OrbitCameraController>();
            }

            if (FindFirstObjectByType<Light>() == null)
            {
                var lightObject = new GameObject("Sun");
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.1f;
                lightObject.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            }
        }

        private GameSlotStore CreateSlots(string directory)
        {
            return new GameSlotStore(new FileSaveStore(directory), new JsonSaveSerializer(), new JsonSessionSerializer(), new JsonWorldSerializer());
        }

        private string DescribeSaveDirectory()
        {
            return string.IsNullOrEmpty(_saveDirectory) ? "persistentDataPath/CubeGame/saves" : _saveDirectory;
        }

        private bool Report(bool success, string message)
        {
            _status = message;
            if (success)
            {
                Debug.Log($"[Game] {message}");
            }
            else
            {
                Debug.LogWarning($"[Game] {message}");
            }

            return success;
        }

        private void ParseArguments()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                string value = args[i + 1];
                switch (args[i])
                {
                    case "-cubeSaveDir":
                        _saveDirectory = value;
                        break;
                    case "-cubeAutoNew":
                        _autoNewSlot = value;
                        break;
                    case "-cubeAutoLoad":
                        _autoLoadSlot = value;
                        break;
                    case "-cubeSeed":
                        uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _commandLineSeed);
                        break;
                    case "-cubeScreenshot":
                        _screenshotPath = value;
                        _screenshotFrame = 120;
                        break;
                    case "-cubeTeleport":
                        if (i + 2 < args.Length
                            && int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out _teleportDx)
                            && int.TryParse(args[i + 2], NumberStyles.Integer, CultureInfo.InvariantCulture, out _teleportDz))
                        {
                            _teleport = true;
                        }

                        break;
                    case "-cubeCamera":
                        if (i + 3 < args.Length
                            && float.TryParse(args[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out _cameraYaw)
                            && float.TryParse(args[i + 2], NumberStyles.Float, CultureInfo.InvariantCulture, out _cameraPitch)
                            && float.TryParse(args[i + 3], NumberStyles.Float, CultureInfo.InvariantCulture, out _cameraDistance))
                        {
                            _cameraOverride = true;
                        }

                        break;
                    case "-cubeAttackFrame":
                        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _attackFrame);
                        break;
                    case "-cubeFrames":
                        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int frames) && frames > 0)
                        {
                            _screenshotFrame = frames;
                        }

                        break;
                }
            }
        }

        private System.Collections.IEnumerator QuitAfterFrames(int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                yield return null;
            }

            QuitGame();
        }

        // ----- Temporary IMGUI menus (M0-08 replaces them with the final UI; the game API above stays) -----

        private void OnGUI()
        {
            switch (_session.State)
            {
                case SessionState.MainMenu:
                    DrawMainMenu();
                    break;
                case SessionState.Paused:
                    DrawPauseMenu();
                    break;
                case SessionState.InGame:
                    DrawHud();
                    if (_inventoryOpen)
                    {
                        DrawInventory();
                    }

                    break;
            }
        }

        /// <summary>
        /// Hero editor (C1, Alpha fields of drawCharacterAppearancePanel 0x428E40): race, gender, face, haircut, hair colour
        /// palette 16×7, class, name. Temporary IMGUI; the final interface is C10.
        /// </summary>
        private void DrawHeroEditor()
        {
            GUILayout.Label("Hero");
            _formHeroName = LabeledField("Name", _formHeroName);
            if (_formHeroName.Length > AlphaHeroCreation.MaxNameLength)
            {
                _formHeroName = _formHeroName.Substring(0, AlphaHeroCreation.MaxNameLength);
            }

            HeroAppearance a = _formLook;
            int race = GUILayout.SelectionGrid(a.Race, AlphaHeroCreation.Races, 4);
            int gender = GUILayout.Toolbar(a.Gender, new[] { "Male", "Female" });
            KindId kind = AlphaHeroCreation.KindOf(race, gender);
            int face = a.Face;
            int hair = a.Haircut;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("<", GUILayout.Width(30f)))
            {
                face--;
            }

            GUILayout.Label($"Face {AlphaHeroCreation.Wrap(face, AlphaHeroCreation.FaceCount(kind)) + 1}/{AlphaHeroCreation.FaceCount(kind)}", GUILayout.Width(110f));
            if (GUILayout.Button(">", GUILayout.Width(30f)))
            {
                face++;
            }

            if (GUILayout.Button("<", GUILayout.Width(30f)))
            {
                hair--;
            }

            GUILayout.Label($"Haircut {AlphaHeroCreation.Wrap(hair, AlphaHeroCreation.HaircutCount(kind)) + 1}/{AlphaHeroCreation.HaircutCount(kind)}", GUILayout.Width(110f));
            if (GUILayout.Button(">", GUILayout.Width(30f)))
            {
                hair++;
            }

            GUILayout.EndHorizontal();

            byte r = a.HairR;
            byte g = a.HairG;
            byte b = a.HairB;
            GUILayout.Label($"Hair colour ({r}, {g}, {b})");
            Color saved = GUI.backgroundColor;
            for (int row = 0; row < AlphaHeroCreation.PaletteRows; row++)
            {
                GUILayout.BeginHorizontal();
                for (int column = 0; column < AlphaHeroCreation.PaletteColumns; column++)
                {
                    var c = AlphaHeroCreation.PaletteColor(column, row);
                    GUI.backgroundColor = new Color32(c.R, c.G, c.B, 255);
                    if (GUILayout.Button(GUIContent.none, GUILayout.Width(22f), GUILayout.Height(14f)))
                    {
                        (r, g, b) = c;
                    }
                }

                GUILayout.EndHorizontal();
            }

            GUI.backgroundColor = saved;
            _formLook = AlphaHeroCreation.Normalize(new HeroAppearance(race, gender, face, hair, r, g, b));
            _formClass = GUILayout.Toolbar(_formClass, new[] { "Warrior", "Ranger", "Mage", "Rogue" });
        }

        private void DrawMainMenu()
        {
            GUILayout.BeginArea(new Rect(Screen.width / 2f - 220f, 60f, 440f, Screen.height - 120f), GUI.skin.box);
            GUILayout.Label("CubeGame — main menu (M0-01)");
            GUILayout.Space(8f);
            GUILayout.Label("New world");
            _formWorldName = LabeledField("World name", _formWorldName);
            _formSeed = LabeledField("Seed", _formSeed);
            DrawHeroEditor();
            _formSlot = GUILayout.Toolbar(_formSlot, SlotKeys);
            if (GUILayout.Button("Create world"))
            {
                uint seed = uint.TryParse(_formSeed, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint parsed) ? parsed : DefaultSeed;
                StartNewWorld(SlotKeys[_formSlot], _formWorldName, seed, _formHeroName, CharacterClasses.FromId(_formClass), _formLook);
            }

            GUILayout.Space(12f);
            GUILayout.Label("Load");
            foreach (string slot in SlotKeys)
            {
                GUI.enabled = _slots.Exists(slot);
                if (GUILayout.Button(_slots.Exists(slot) ? $"Load {slot}" : $"{slot} (empty)"))
                {
                    LoadWorld(slot);
                }
            }

            GUI.enabled = true;
            GUILayout.Space(12f);
            if (GUILayout.Button("Quit"))
            {
                QuitGame();
            }

            if (!string.IsNullOrEmpty(_status))
            {
                GUILayout.Label(_status);
            }

            GUILayout.EndArea();
        }

        private void DrawPauseMenu()
        {
            GUILayout.BeginArea(new Rect(Screen.width / 2f - 160f, Screen.height / 2f - 140f, 320f, 280f), GUI.skin.box);
            GUILayout.Label($"Paused — {_session.World?.Name}");
            if (GUILayout.Button("Resume"))
            {
                TogglePause();
            }

            if (GUILayout.Button("Save"))
            {
                SaveCheckpoint();
            }

            if (GUILayout.Button("Save and exit to menu"))
            {
                ExitToMenu(true);
            }

            if (GUILayout.Button("Exit to menu without saving"))
            {
                ExitToMenu(false);
            }

            if (GUILayout.Button("Quit game"))
            {
                QuitGame();
            }

            GUILayout.Label(_status);
            GUILayout.EndArea();
        }

        private void DrawHud()
        {
            HeroState hero = _session.Hero;
            GUI.Label(new Rect(10f, 10f, 700f, 22f),
                $"{_session.World.Name}  seed {_session.World.Seed}  time {_session.Clock.TimeMs / 1000.0:F1}s  " +
                $"{hero.Name} {hero.Sheet.Class} L{hero.Sheet.Progression.Level}  pos {hero.Position}  " +
                $"{(_worldView != null ? _worldView.Center.ToString() : "-")} loaded {(_worldView != null ? _worldView.LoadedCount : 0)}  [Esc] menu");
            GUI.Label(new Rect(10f, 32f, 700f, 22f),
                $"HP {hero.Health.CurrentHealth:F0}/{hero.Health.MaxHealth:F0}  weapon {hero.Loadout.GetEquipped(EquipmentSlot.MainHand)?.ToString() ?? "none"}  " +
                $"potions {hero.Inventory.CountOf(LootRules.LifePotion)}  [LMB] attack  [Q] potion  [I] inventory");
            if (_trainingTarget != null)
            {
                GUI.Label(new Rect(10f, 54f, 700f, 22f),
                    $"{_trainingTarget.Name}: HP {_trainingTarget.Health.CurrentHealth:F0}/{_trainingTarget.Health.MaxHealth:F0}{(_trainingTarget.IsDead ? " (dead)" : string.Empty)}");
            }

            GUI.Label(new Rect(10f, 76f, 700f, 22f), _combatLog);
        }

        /// <summary>Temporary inventory window (M0-08 replaces it): equipment, bags, equip/unequip/use.</summary>
        private void DrawInventory()
        {
            HeroState hero = _session.Hero;
            GUILayout.BeginArea(new Rect(Screen.width - 470f, 40f, 450f, Screen.height - 80f), GUI.skin.box);
            GUILayout.Label($"Inventory  {hero.Inventory.UsedSlots}/{HeroInventory.Capacity}  [I] close");
            foreach (EquipmentSlot slot in EquipmentLoadout.Slots)
            {
                ItemInstance item = hero.Loadout.GetEquipped(slot);
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{slot}: {(item != null ? item.ToString() : "-")}", GUILayout.Width(360f));
                if (item != null && GUILayout.Button("Off", GUILayout.Width(60f)) && !hero.Inventory.TryUnequip(slot))
                {
                    _combatLog = hero.Inventory.LastRejection;
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.Space(6f);
            _inventoryScroll = GUILayout.BeginScrollView(_inventoryScroll);
            for (int i = 0; i < HeroInventory.Capacity; i++)
            {
                InventoryEntry entry = hero.Inventory[i];
                if (entry == null)
                {
                    continue;
                }

                GUILayout.BeginHorizontal();
                GUILayout.Label($"[{i / HeroInventory.SlotsPerBag}:{i % HeroInventory.SlotsPerBag}] {entry}", GUILayout.Width(330f));
                if (entry.IsCore && GUILayout.Button("Equip", GUILayout.Width(80f)) && !hero.Inventory.TryEquip(i, out _))
                {
                    _combatLog = hero.Inventory.LastRejection;
                }
                else if (!entry.IsCore && entry.Raw.Type == 1 && GUILayout.Button("Use", GUILayout.Width(80f)) && !_session.TryUseItem(i))
                {
                    _combatLog = _session.LastRejection;
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private static string LabeledField(string label, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(100f));
            string result = GUILayout.TextField(value ?? string.Empty);
            GUILayout.EndHorizontal();
            return result;
        }
    }
}
