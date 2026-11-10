using System;
using System.Collections.Generic;
using System.Globalization;
using CubeGame.AlphaModels;
using CubeGame.Core.Characters;
using CubeGame.Core.Persistence;
using CubeGame.Features.Combat;
using CubeGame.Features.Items;
using CubeGame.Core.Equipment;
using CubeGame.Core.Items;
using CubeGame.Features.Contracts;
using CubeGame.Features.Creatures;
using CubeGame.Features.Persistence;
using CubeGame.Features.Session;
using CubeGame.Features.World;
using CubeGame.Gameplay.Streaming;
using CubeGame.Persistence;
using CubeGame.Presentation.CameraRig;
using CubeGame.Presentation.Player;
using CubeGame.Presentation.Rendering;
using CubeGame.Presentation.Visual;
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
    public sealed partial class GameBootstrap : MonoBehaviour
    {
        /// <summary>
        /// World generator in use (M0-02): Alpha terrain reconstruction, see <see cref="AlphaTerrainGenerator"/>.
        /// Saves record the id; a save made by another generator (e.g. the M0-01 pilot height field) is refused.
        /// </summary>
        public const string GeneratorId = AlphaTerrainGenerator.WorldRuntimeId;

        public const uint DefaultSeed = 666666u;

        /// <summary>
        /// Alpha action 57 (two-handed attack, S05A-2 / C-31): what <see cref="AlphaAttackRoute"/> selects for the
        /// starting two-handed weapon. No longer a fallback for ranged/magic input (M0-F2B).
        /// </summary>
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
        private Light _sun;
        private float _sunDayIntensity;

        private string _status = string.Empty;
        private string _combatLog = string.Empty;
        private Combatant _trainingTarget;
        private ulong _runtimeSerial;
        private bool _trainingTargetPending;
        private GameVisualSink _visuals;
        private AlphaActionTimingSource _timings;
        private CreatureCombatDriver _creatures;
        private ProjectileVisuals _projectileVisuals;
        private VoxelColumn _terrainColumn;
        private int _terrainColumnX = int.MinValue, _terrainColumnZ = int.MinValue;
        private WorldPopulation _population;
        private AlphaSpawnRuntime _recoveredPopulation;
        private GameClock _populationClock;
        private ZoneKey? _populationCenter;
        private readonly WorldPosition[] _populationPlayers = new WorldPosition[1];
        private readonly Dictionary<StableEntityId, ZoneKey> _recoveredCorpses = new Dictionary<StableEntityId, ZoneKey>();
        private long _heroRespawnAtMs = -1;
        private Vector3 _lastHeroScenePosition;
        private int _frames;
        private string _screenshotPath;
        private int _screenshotFrame = -1;
        private bool _validateVisual;
        private int _validateFrame = 600;
        private bool _saveCheckpoint;
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
            CreateInterface();
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
            UpdateInterfaceInput();

            if (_session.State == SessionState.InGame)
            {
                ApplyHeroMovementState(_session.Hero);
                if (_motor != null && _motor.enabled && Input.GetMouseButtonDown(0) && !PointerOverInterface())
                {
                    HeroAttack();
                }

                RebaseIfFar();
                SyncHeroFromView();
                SyncHeroCombatant();
                UpdatePopulationZones();
                UpdateInteractionTarget();
                _session.AdvanceFrame(Time.deltaTime);
                if (_projectileVisuals != null)
                {
                    _projectileVisuals.Sync();
                }

                ApplyWorldLighting();
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

            if (_validateVisual && _frames >= _validateFrame && _session.State == SessionState.InGame)
            {
                _validateVisual = false;
                RunVisualValidation();
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
            SlotLoadResult result = _slots.Load(slotKey, null);
            if (!result.IsSuccess)
            {
                return Report(false, $"load {slotKey} failed: {result}");
            }

            string generatorId = result.World.GeneratorId;
            if (!D3ZonePopulation.Supports(generatorId) && generatorId != AlphaTerrainGenerator.Id)
            {
                return Report(false, $"load {slotKey} failed: unknown world generator '{generatorId}'");
            }

            if (!_session.BeginLoadedWorld(slotKey, result.World, result.Hero, result.ClockTick, result.WorldDeltas, result.WorldTimeOffsetMs))
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

            if (_recoveredPopulation != null)
            {
                RecoveredSpawnPersistence.PrepareSave(_session.WorldDeltas, _recoveredPopulation, AlphaGameDay.Of(_session.Clock));
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

            if (_session.Combat != null)
            {
                _session.Combat.Projectiles.Clear(_session.Clock?.TimeMs ?? 0);
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
            SyncHeroCombatant();
            HeroState hero = _session.Hero;
            // M0-F2B: weapon state -> one attack path (melee / projectile / staff volume). An unknown Alpha rule is
            // refused with its reason; ranged and magic input never fall back to a melee swing.
            AttackPlan plan = AlphaAttackRoute.Select(hero.Sheet.Class,
                hero.Loadout.GetEquipped(EquipmentSlot.MainHand), hero.Loadout.GetEquipped(EquipmentSlot.OffHand));
            LastAttackPlan = plan;
            if (!_session.Combat.TryStartAttack(hero.Id, plan, _session.Clock.TimeMs))
            {
                _combatLog = $"attack refused: {_session.Combat.LastRejection}";
                return false;
            }

            return true;
        }

        /// <summary>Path chosen by the last <see cref="HeroAttack"/> (tests/diagnostics).</summary>
        public AttackPlan LastAttackPlan { get; private set; }

        public ProjectileVisuals ProjectileVisuals => _projectileVisuals;

        /// <summary>
        /// Hero combatant from the view: Alpha size of the hero's species as the hit box, and the camera look direction
        /// as the aim (Alpha creature +0x160; its writer for the player is not decoded - CUBEGAME_CONTRACT).
        /// </summary>
        private void SyncHeroCombatant()
        {
            if (_session.Combat == null || _session.Hero == null || !_session.Combat.TryGet(_session.Hero.Id, out Combatant hero))
            {
                return;
            }

            ApplyAlphaSize(hero, _session.Hero.Kind);
            if (_camera != null)
            {
                Vector3 look = _camera.transform.forward;
                hero.SetAim(look.x, look.y, look.z);
            }
        }

        /// <summary>Alpha species size (appearance +0x08, <see cref="Content.AlphaCreatureSizes"/>) as the combat box of any creature or the hero.</summary>
        public static void ApplyAlphaSize(Combatant combatant, KindId kind)
        {
            if (combatant != null && Content.AlphaCreatureSizes.TryGet(kind.Value, out double width, out double height, out double depth))
            {
                combatant.HitWidth = width;
                combatant.HitHeight = height;
                combatant.HitDepth = depth;
            }
        }

        /// <summary>
        /// Projectile terrain query: solid voxels of the effective world (edits included); a block whose chunk is not
        /// streamed in reports <see cref="TerrainCell.NotLoaded"/> (the projectile is removed - CUBEGAME_CONTRACT).
        /// </summary>
        private TerrainCell ProjectileTerrainAt(int x, int y, int z)
        {
            if (_worldView == null || _worldState == null || !_worldView.IsLoaded(WorldLayout.ChunkOf(x, z)))
            {
                return TerrainCell.NotLoaded;
            }

            if (_session.WorldDeltas != null && _session.WorldDeltas.TryGetVoxel(x, y, z, out uint edited))
            {
                return Voxel.IsSolid(edited) ? TerrainCell.Solid : TerrainCell.Air;
            }

            if (x != _terrainColumnX || z != _terrainColumnZ || _terrainColumn == null)
            {
                _terrainColumn = _worldState.EffectiveColumn(x, z);
                _terrainColumnX = x;
                _terrainColumnZ = z;
            }

            return _terrainColumn != null && Voxel.IsSolid(_terrainColumn.Read(y)) ? TerrainCell.Solid : TerrainCell.Air;
        }

        /// <summary>
        /// Spawns a hostile training target in front of the hero (runtime entity, not saved; M0-06 replaces it with
        /// world spawns). HP from Core for a level-1 Warrior sheet: PLACEHOLDER_NOT_ALPHA for creature HP.
        /// </summary>
        public Combatant SpawnTrainingTarget() => SpawnTrainingTargetAt(TrainingTargetDistance, TrainingTargetBearing);

        /// <summary>Automation/tests: the training target at <paramref name="distance"/> blocks, <paramref name="bearing"/>° from the hero's facing.</summary>
        public Combatant SpawnTrainingTargetAt(double distance, double bearing)
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
            double yaw = (hero.Yaw + bearing) * Math.PI / 180.0;
            double x = hero.Position.X + Math.Sin(yaw) * distance;
            double z = hero.Position.Z + Math.Cos(yaw) * distance;
            int ground = Math.Max(_worldState.SurfaceAt((int)Math.Floor(x), (int)Math.Floor(z)), _worldState.Generator.WaterLevel);
            double y = Math.Abs(ground + 1 - hero.Position.Y) <= 2.0 ? ground + 1 : hero.Position.Y;

            var target = Combatant.CreateCreature(StableEntityId.ForRuntime(++_runtimeSerial), TrainingTargetKind, "Wolf", 1, 0);
            ApplyAlphaSize(target, TrainingTargetKind);
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

        private void OnEquipmentChanged(EquipmentSlot slot, ItemInstance item)
        {
            _session.Events.Publish(new EquipmentVisualChanged
            {
                TimeMs = _session.Clock?.TimeMs ?? 0,
                Entity = StableEntityId.Hero,
                Slot = VisualSlotOf(slot, item),
                Item = item != null ? new RawAlphaItemId((int)item.Type, item.Subtype) : (RawAlphaItemId?)null,
                // M0-F2A: the bytes that select the drawn model (item_icon_id 0x4EC400).
                Material = item != null ? (int)item.Modifier : 0,
                Rarity = item?.Rarity ?? 0,
                Seed = item?.Seed ?? 0,
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

        // M0-F2A: ground items are drawn only by GameVisualSink (one owner, origin-safe); the bootstrap keeps the log.
        private void OnWorldItemRemoved(WorldItemRemoved removed)
        {
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
                ShowPanel(GamePanel.None, false);
                SetGameplayInput(false);
                _combatLog = $"you died - respawn in {HeroRespawnDelayMs / 1000} s";
                return;
            }

            if (_trainingTarget != null && died.Entity.Equals(_trainingTarget.Id))
            {
                _combatLog += $"  {_trainingTarget.Name} died";
            }

            if (_recoveredPopulation != null && _recoveredPopulation.NotifyDeath(died.Entity, AlphaGameDay.Of(_session.Clock)))
            {
                _recoveredPopulation.TryGet(died.Entity, out AlphaSpawnTemplate template);
                RecoveredSpawnPersistence.RecordDeath(_session.WorldDeltas, template, _session.Clock.TimeMs);
                _recoveredCorpses[died.Entity] = template.Zone;
                _session.Combat.Unregister(died.Entity);
                _creatures?.Remove(died.Entity);
                _combatLog += $"  {died.Entity} killed (respawn pending)";
            }
            else if (_population != null && _population.MarkKilled(died.Entity, died.TimeMs))
            {
                _combatLog += $"  {died.Entity} killed (stays dead)";
            }
        }

        /// <summary>World population (C6): live spawns of the zones around the hero.</summary>
        public WorldPopulation Population => _population;
        public AlphaSpawnRuntime RecoveredPopulation => _recoveredPopulation;

        private void StartPopulation()
        {
            _population = new WorldPopulation(_session.WorldDeltas);
            if (_worldState.Generator is AlphaTerrainGenerator terrain && D3ZonePopulation.Supports(terrain.GeneratorId))
            {
                _population.AddSource(new D3ZonePopulation(terrain, _worldState.Deltas), D3ZonePopulation.Channel);
                if (terrain.GeneratorId == GeneratorId)
                {
                    _population.AddSource(new TownNpcPopulation(terrain), TownNpcPopulation.Channel);
                    var candidates = AlphaCreatureKindSelector.FromClimate(
                        (x, z) => new AlphaClimateSample(terrain.Humidity(x, z), terrain.Temperature(x, z),
                            terrain.RareCellTerm(x, z), terrain.Mountain(x, z)),
                        (x, z) => FirstNonSolid(terrain.Voxels(x, z)));
                    var source = AlphaRecoveredPopulation.WithKindSelector(_session.World.Seed, candidates,
                        (zone, lx, lz) => GroundAt(zone.X * (double)WorldLayout.ZoneSize + lx, zone.Z * (double)WorldLayout.ZoneSize + lz));
                    _recoveredPopulation = new AlphaSpawnRuntime();
                    _recoveredPopulation.AddSource(source);
                    RecoveredSpawnPersistence.Restore(_worldState.Deltas, _recoveredPopulation);
                    _recoveredPopulation.Created += OnRecoveredSpawned;
                    _recoveredPopulation.Despawned += OnRecoveredDespawned;
                    _populationClock = _session.Clock;
                    _populationClock.Ticked += TickRecoveredPopulation;
                }
            }
            _population.Spawned += OnSpawnAppeared;
            _population.Despawned += OnSpawnGone;
            UpdatePopulationZones();
            if (_recoveredPopulation != null)
            {
                AdvanceRecoveredPopulation(_populationClock, 0);
            }
            Debug.Log($"[Game] population: {_population.LiveCount} content, {_recoveredPopulation?.LiveCount ?? 0} recovered in {_population.ActiveZones.Count} zones");
        }

        private static int FirstNonSolid(VoxelColumn column)
        {
            int height = column.Bottom;
            while (Voxel.IsSolid(column.Read(height))) height++;
            return height;
        }

        private void UpdatePopulationZones()
        {
            WorldPosition hero = _session.Hero.Position;
            ZoneKey zone = WorldLayout.ZoneOf((int)Math.Floor(hero.X), (int)Math.Floor(hero.Z));
            if (_populationCenter.HasValue && _populationCenter.Value.Equals(zone)) return;

            _populationCenter = zone;
            _population.Update(hero);
            if (_recoveredPopulation == null) return;
            _recoveredPopulation.SetLoadedZones(_population.ActiveZones);
            if (_recoveredCorpses.Count == 0) return;

            foreach (var corpse in new List<KeyValuePair<StableEntityId, ZoneKey>>(_recoveredCorpses))
            {
                if (_recoveredPopulation.IsLoaded(corpse.Value)) continue;
                _recoveredCorpses.Remove(corpse.Key);
                _session.Events.Publish(new EntityDespawned { TimeMs = _session.Clock.TimeMs, Entity = corpse.Key });
            }
        }

        private void TickRecoveredPopulation(GameClock clock)
        {
            long previousTime = (clock.Tick - 1) * 1000L / clock.TicksPerSecond;
            AdvanceRecoveredPopulation(clock, (int)(clock.TimeMs - previousTime));
        }

        private void AdvanceRecoveredPopulation(GameClock clock, int elapsedMs)
        {
            _populationPlayers[0] = _session.Hero.Position;
            _recoveredPopulation.Tick(elapsedMs, clock.WorldTimeMs, AlphaGameDay.Of(clock), _populationPlayers, RecoveredPosition);
        }

        private WorldPosition RecoveredPosition(StableEntityId id)
        {
            if (_session.Combat.TryGet(id, out Combatant creature)) return creature.Position;
            _recoveredPopulation.TryGet(id, out AlphaSpawnTemplate template);
            return template.Definition.Position;
        }

        private void StopPopulation()
        {
            if (_recoveredPopulation != null)
            {
                _populationClock.Ticked -= TickRecoveredPopulation;
                _recoveredPopulation.Clear();
                _recoveredPopulation = null;
                _populationClock = null;
                _recoveredCorpses.Clear();
            }
            _populationCenter = null;
            if (_population == null)
            {
                return;
            }

            _population.Clear();
            _population.Spawned -= OnSpawnAppeared;
            _population.Despawned -= OnSpawnGone;
            _population = null;
        }

        private void OnSpawnAppeared(LiveSpawn live) => SpawnWorldEntity(live.Id, live.Definition);

        private void OnRecoveredSpawned(AlphaSpawnTemplate template)
        {
            _recoveredCorpses.Remove(template.Id);
            SpawnWorldEntity(template.Id, template.Definition);
            if (_creatures != null && _creatures.TryGetAi(template.Id, out var ai))
            {
                ai.AttachAlphaMovement(AlphaNpcMovementDriver.ForTemplate(template.Data, () => _populationClock.WorldTimeMs));
            }
        }

        private void SpawnWorldEntity(StableEntityId id, SpawnDefinition def)
        {
            if (D3ZonePopulation.IsStaticContent(def.BehaviourKey) || def.BehaviourKey == TownNpcPopulation.BehaviourKey)
            {
                PublishSpawnVisual(id, def);
                return;
            }

            if (def.BehaviourKey != AlphaRecoveredPopulation.BehaviourKey || def.Faction == (int)CreatureFaction.Passive || _session.Combat == null)
            {
                return;
            }

            var creature = Combatant.CreateCreature(id, def.Kind, $"Creature 0x{def.Kind.Value:x2}", def.Level, 0);
            ApplyAlphaSize(creature, def.Kind);
            creature.Position = def.Position;
            creature.Yaw = def.Yaw;
            creature.Hostile = def.Faction == (int)CreatureFaction.Monster;
            creature.IsBoss = CreatureKinds.IsBoss(def.Kind.Value);
            _session.Combat.Register(creature);
            _creatures?.Add(creature, def.Level, 0, def.Faction, def.ClassId, def.SpecializationId, def.MainHandWeapon);
            PublishSpawnVisual(id, def);
        }

        private void PublishSpawnVisual(StableEntityId id, SpawnDefinition def)
        {
            string worldObjectName = null;
            if (def.BehaviourKey == AlphaCityContentProvider.ServiceBehaviourKey
                && Content.AlphaWorldContent.TryObject(def.Kind.Value, out var worldObject))
            {
                worldObjectName = worldObject.Name;
            }

            _session.Events.Publish(new EntitySpawned
            {
                TimeMs = _session.Clock.TimeMs,
                Entity = id,
                Kind = def.Kind,
                WorldObjectName = worldObjectName,
                Position = def.Position,
                Yaw = def.Yaw,
                AppearanceSeed = (uint)(id.Value ^ (id.Value >> 32)),
            });
        }

        private void OnSpawnGone(LiveSpawn live) => DespawnWorldEntity(live.Id, live.Definition);

        private void OnRecoveredDespawned(AlphaSpawnTemplate template, AlphaDespawnReason reason) =>
            DespawnWorldEntity(template.Id, template.Definition);

        private void DespawnWorldEntity(StableEntityId id, SpawnDefinition definition)
        {
            bool combatantRemoved = _session.Combat != null && _session.Combat.Unregister(id);
            if (combatantRemoved)
            {
                _creatures?.Remove(id);
            }

            if (combatantRemoved || D3ZonePopulation.IsStaticContent(definition.BehaviourKey)
                || definition.BehaviourKey == TownNpcPopulation.BehaviourKey)
            {
                _session.Events.Publish(new EntityDespawned { TimeMs = _session.Clock?.TimeMs ?? 0, Entity = id });
            }
        }

        public GameVisualSink Visuals => _visuals;

        private void ApplyHeroMovementState(HeroState hero)
        {
            if (_motor == null)
            {
                return;
            }

            _motor.Movement.ClimbingRank = hero.SkillRanks[HeroSkill.Climbing];
            _motor.Movement.HangGlidingRank = hero.SkillRanks[HeroSkill.HangGliding];
            _motor.Movement.SwimmingRank = hero.SkillRanks[HeroSkill.Swimming];
            _motor.Movement.HasGlider = hero.Inventory.HangGliderEquipped;
        }

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
            if (_saveCheckpoint && _session != null && _session.State == SessionState.InGame)
            {
                SaveCheckpoint();
            }

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
            ApplyWorldLighting();
            PublishHeroSpawn();
            _session.Events.Subscribe<HitResolved>(OnHitResolved);
            _session.Events.Subscribe<EntityDied>(OnEntityDied);
            _heroRespawnAtMs = -1;
            _session.Events.Subscribe<WorldItemRemoved>(OnWorldItemRemoved);
            BindWorldPresentation();
            _session.Hero.Inventory.EquipmentChanged += OnEquipmentChanged;
            PublishEquipment();
            _creatures = new CreatureCombatDriver(_session, GroundAt, kind => _timings.AttackActionsOf(kind), _session.World.Seed.Value);
            _session.Clock.Ticked += _creatures.Tick;
            StartPopulation();
            // The target appears once the hero stands on the ground and the camera has a facing (see Update).
            _trainingTargetPending = true;
            return Report(true, description);
        }

        private void BuildWorldViews()
        {
            DestroyWorldViews();
            WorldInfo world = _session.World;
            HeroState hero = _session.Hero;

            if (!D3ZonePopulation.TryCreateGenerator(world.GeneratorId, world.Seed.Value, out IWorldGenerator generator))
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
            _visuals.BindItems(_session.Events);   // D4: Alpha models of items on the ground

            // M0-F2B: projectiles - gameplay state in CombatSystem, one visual owner, terrain from the streamed world.
            _terrainColumn = null;
            _terrainColumnX = _terrainColumnZ = int.MinValue;
            _session.Combat.Projectiles.TerrainAt = ProjectileTerrainAt;
            GameVisualSink sink = _visuals;
            _projectileVisuals = visualObject.AddComponent<ProjectileVisuals>();
            _projectileVisuals.Initialise(_session.Events, _session.Combat.Projectiles, p => view.ToScene(p.X, p.Y, p.Z),
                () => sink != null ? sink.ModelScale : 0f,
                AlphaActorLibrary.Shared.State == AlphaContentState.Ready ? AlphaActorLibrary.Shared.Source?.Root : null);
            _lastHeroScenePosition = heroPoint;

            _motor = _player.AddComponent<PlayerMotor>();
            ApplyHeroMovementState(hero);
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
            UnbindWorldPresentation();
            ShowPanel(GamePanel.None, false);
            if (_sun != null)
            {
                _sun.intensity = _sunDayIntensity;
            }

            DespawnTrainingTarget();
            StopPopulation();
            if (_session?.Combat != null)
            {
                // Projectiles are runtime-only: never saved, never carried into another world.
                _session.Combat.Projectiles.Clear(_session.Clock?.TimeMs ?? 0);
                _session.Combat.Projectiles.TerrainAt = null;
            }

            if (_session?.Hero != null)
            {
                _session.Hero.Inventory.EquipmentChanged -= OnEquipmentChanged;
            }

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
            _projectileVisuals = null;
            _terrainColumn = null;
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
            ApplyRebase(StreamingWorldView.OriginFor(x, z));
        }

        /// <summary>
        /// Automation/tests: moves the floating origin by (<paramref name="dx"/>, <paramref name="dz"/>) blocks while the
        /// hero, creatures and projectiles keep their game positions.
        /// </summary>
        public void ShiftOrigin(int dx, int dz)
        {
            if (_worldView == null || _player == null)
            {
                return;
            }

            _worldView.ToWorld(_player.transform.position, out double x, out _, out double z);
            ApplyRebase(StreamingWorldView.OriginFor(x + dx, z + dz));
        }

        private void ApplyRebase(Vector2Int origin)
        {
            Vector3 p = _player.transform.position;
            Vector3 shift = _worldView.Rebase(origin);
            _player.transform.position = p + shift;
            _lastHeroScenePosition += shift;
            if (_projectileVisuals != null)
            {
                _projectileVisuals.Sync();
            }

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
                _motor.enabled = enabled && _panel == GamePanel.None && !HeroDead;
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

            Light light = RenderSettings.sun != null ? RenderSettings.sun : FindFirstObjectByType<Light>();
            if (light == null)
            {
                var lightObject = new GameObject("Sun");
                light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.1f;
                lightObject.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            }

            if (light.type == LightType.Directional)
            {
                _sun = light;
                _sunDayIntensity = light.intensity;
                // Alpha draws with real-time sun shadows; a new directional light defaults to none.
                light.shadows = LightShadows.Soft;
                light.shadowStrength = 0.8f;
                light.shadowBias = 0.02f;
            }
        }

        private void ApplyWorldLighting()
        {
            // CUBEGAME_CONTRACT: apply Alpha's time weight to Unity brightness with a visible night floor.
            float brightness = Mathf.Lerp(0.4f, 1f, (float)_session.Clock.TerrainDaylightWeight);
            _worldView?.SetDaylightBrightness(brightness);
            if (_sun != null)
            {
                _sun.intensity = _sunDayIntensity * brightness;
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
            if (_panel != GamePanel.None && _session.State == SessionState.InGame)
            {
                _panelMessage = message;
            }
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
                    case "-cubeValidateVisual":
                        _validateVisual = true;
                        _validateFrame = 600;
                        break;
                    case "-cubeValidateFrame":
                        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int vframes) && vframes > 0)
                        {
                            _validateFrame = vframes;
                        }

                        break;
                    case "-cubeSaveCheckpoint":
                        _saveCheckpoint = true;
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

        /// <summary>R4/R5 visual self-check: writes [Validate] PASS/FAIL markers readable from a Player log.</summary>
        private void RunVisualValidation()
        {
            AlphaActorLibrary library = AlphaActorLibrary.Shared;
            bool contentReady = library != null && library.State == AlphaContentState.Ready;
            Debug.Log($"[Validate] ALPHA_CONTENT_READY={(contentReady ? "PASS" : "FAIL")} state={library?.State} origin={library?.Source?.Origin}");

            AlphaActorView heroView = null;
            bool heroVisible = _visuals != null && _visuals.TryGetView(StableEntityId.Hero, out heroView) && heroView != null;
            bool heroNotPrimitive = heroVisible && heroView.Rig != null;
            Debug.Log($"[Validate] PLAYER_MODEL_VISIBLE={(heroVisible ? "PASS" : "FAIL")}");
            Debug.Log($"[Validate] PLAYER_NOT_PRIMITIVE={(heroNotPrimitive ? "PASS" : "FAIL")}");

            AlphaActorView wolfView = null;
            bool wolfVisible = _trainingTarget != null && _visuals != null &&
                               _visuals.TryGetView(_trainingTarget.Id, out wolfView) && wolfView != null;
            Debug.Log($"[Validate] KNOWN_CREATURE_MODEL_VISIBLE={(wolfVisible ? "PASS" : "FAIL")}");

            int magenta = 0;
            if (heroView != null) magenta += CountErrorMaterials(heroView);
            if (wolfView != null) magenta += CountErrorMaterials(wolfView);
            Debug.Log($"[Validate] NO_MAGENTA={(magenta == 0 ? "PASS" : "FAIL")} MAGENTA_COUNT={magenta}");

            bool heroAnimating = heroView != null && heroView.Ready && heroView.Controller != null && heroView.Controller.Initialized;
            bool wolfAnimating = wolfView != null && wolfView.Ready && wolfView.Controller != null && wolfView.Controller.Initialized;
            bool animRunning = heroAnimating && wolfAnimating;
            Debug.Log($"[Validate] ANIMATION_RUNNING={(animRunning ? "PASS" : "FAIL")} hero={heroAnimating} wolf={wolfAnimating}");

            bool combatActionSeen = wolfView != null && wolfView.Clips != null && wolfView.Clips.FramesApplied > 0;
            Debug.Log($"[Validate] COMBAT_ACTION_ANIMATION={(combatActionSeen ? "PASS" : "NOT_TRIGGERED")} wolf_frames={wolfView?.Clips?.FramesApplied ?? 0} hero_frames={heroView?.Clips?.FramesApplied ?? 0}");

            AlphaActorView[] allViews = FindObjectsByType<AlphaActorView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var actorIds = new List<string>();
            foreach (AlphaActorView v in allViews) actorIds.Add(v.Ready ? v.ActorId : v.ActorId + "(not-ready)");
            Debug.Log($"[Validate] REAL_ACTOR_VIEWS={allViews.Length} [{string.Join(", ", actorIds)}]");

            // R6: catalog scale — every staged actor resolves to a ready definition (file -> rig -> clips).
            var catalogProblems = new List<string>();
            var defs = library.LoadAll(catalogProblems);
            int readyDefs = 0;
            foreach (var def in defs) if (def.Ready) readyDefs++;
            Debug.Log($"[Validate] CATALOG_RESOLVE ready={readyDefs}/{defs.Count} problems={catalogProblems.Count}");

            if (heroView != null) Debug.Log($"[Validate] hero {heroView.Status()}");
            if (wolfView != null) Debug.Log($"[Validate] wolf {wolfView.Status()}");
            Debug.Log($"[Validate] problems=[{(_visuals != null ? string.Join("; ", _visuals.Problems) : string.Empty)}]");
        }

        private static int CountErrorMaterials(AlphaActorView view)
        {
            int count = 0;
            foreach (Renderer renderer in view.GetComponentsInChildren<Renderer>(true))
            {
                if (RuntimeMaterials.IsErrorMaterial(renderer.sharedMaterial)) count++;
            }

            return count;
        }

        // ----- Temporary IMGUI menus (M0-08 replaces them with the final UI; the game API above stays) -----

    }
}
