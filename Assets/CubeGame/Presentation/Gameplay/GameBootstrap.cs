using System;
using System.Globalization;
using CubeGame.Core.Characters;
using CubeGame.Core.Persistence;
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
    /// -cubeSeed &lt;uint&gt;, -cubeScreenshot &lt;png&gt; (+ -cubeFrames &lt;n&gt;): capture then quit.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        /// <summary>
        /// World generator in use (M0-02): Alpha terrain reconstruction, see <see cref="AlphaTerrainGenerator"/>.
        /// Saves record the id; a save made by another generator (e.g. the M0-01 pilot height field) is refused.
        /// </summary>
        public const string GeneratorId = AlphaTerrainGenerator.Id;

        public const uint DefaultSeed = 666666u;

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
        private int _frames;
        private string _screenshotPath;
        private int _screenshotFrame = -1;
        private string _autoNewSlot;
        private string _autoLoadSlot;
        private uint _commandLineSeed = DefaultSeed;
        private bool _teleport;
        private int _teleportDx;
        private int _teleportDz;

        // Main menu form state.
        private string _formWorldName = "New World";
        private string _formSeed = DefaultSeed.ToString(CultureInfo.InvariantCulture);
        private string _formHeroName = "Hero";
        private int _formClass;
        private int _formSlot;

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

            _session = new GameSession();
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
                TogglePause();
            }

            if (_session.State == SessionState.InGame)
            {
                _session.AdvanceFrame(Time.deltaTime);
                RebaseIfFar();
                SyncHeroFromView();
                if (_worldView != null && _player != null)
                {
                    _worldView.SetFocus(_player.transform.position);
                }
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

        public bool StartNewWorld(string slotKey, string worldName, uint seed, string heroName, CharacterClass heroClass)
        {
            var request = new NewWorldRequest
            {
                WorldName = worldName,
                Seed = seed,
                GeneratorId = GeneratorId,
                HeroName = heroName,
                Class = heroClass,
                Specialization = CharacterSpecialization.Primary,
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

            // Placeholder body until the S05 visual runtime (DeepSeek, issue #13) is attached via IEntityVisualSink.
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "PlayerBody_PLACEHOLDER";
            body.transform.SetParent(_player.transform, false);
            body.transform.localScale = new Vector3(0.8f, 0.9f, 0.8f);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            Destroy(body.GetComponent<Collider>());
            body.GetComponent<Renderer>().sharedMaterial = PilotMaterials.Terrain(null);

            _motor = _player.AddComponent<PlayerMotor>();
            _motor.SetCamera(_camera.transform);
            _orbitCamera.Target = _player.transform;
            _orbitCamera.Distance = OrbitCameraController.DefaultDistance;

            var region = generator.Region(WorldLayout.RegionOf((int)Math.Floor(hero.Position.X), (int)Math.Floor(hero.Position.Z)));
            Debug.Log($"[Game] world built seed={world.Seed} gen={world.GeneratorId} chunks={_worldView.LoadedCount} origin={_worldView.Origin} " +
                      $"vertices={_worldView.LoadedVertices} {loadTimer.Elapsed.TotalMilliseconds:F1}ms hero={hero.Position} " +
                      $"region={region.Key} level={region.Level} base={region.Climate?.BaseHeight}");
        }

        private void DestroyWorldViews()
        {
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
                Kind = new KindId(-1),
                Position = hero.Position,
                Yaw = hero.Yaw,
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
                    break;
            }
        }

        private void DrawMainMenu()
        {
            GUILayout.BeginArea(new Rect(Screen.width / 2f - 220f, 60f, 440f, Screen.height - 120f), GUI.skin.box);
            GUILayout.Label("CubeGame — main menu (M0-01)");
            GUILayout.Space(8f);
            GUILayout.Label("New world");
            _formWorldName = LabeledField("World name", _formWorldName);
            _formSeed = LabeledField("Seed", _formSeed);
            _formHeroName = LabeledField("Hero name", _formHeroName);
            _formClass = GUILayout.Toolbar(_formClass, new[] { "Warrior", "Ranger", "Mage", "Rogue" });
            _formSlot = GUILayout.Toolbar(_formSlot, SlotKeys);
            if (GUILayout.Button("Create world"))
            {
                uint seed = uint.TryParse(_formSeed, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint parsed) ? parsed : DefaultSeed;
                StartNewWorld(SlotKeys[_formSlot], _formWorldName, seed, _formHeroName, CharacterClasses.FromId(_formClass));
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
