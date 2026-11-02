using System;
using System.Globalization;
using CubeGame.Core.Generation;
using CubeGame.Presentation.CameraRig;
using CubeGame.Presentation.Hud;
using CubeGame.Presentation.Player;
using CubeGame.Presentation.Rendering;
using UnityEngine;

namespace CubeGame.Presentation
{
    /// <summary>
    /// Entry point of the S01 pilot scene: builds the deterministic region, the temporary player,
    /// the third-person camera and the debug HUD from code, without any scene-time dependencies.
    ///
    /// Command line arguments (used by the automated run):
    ///   -cubeSeed &lt;uint&gt;        world seed (default 666666)
    ///   -cubeChunks &lt;int&gt;       chunks per axis (default 8 -> 256x256 blocks)
    ///   -cubeScreenshot &lt;path&gt;  capture one screenshot, then quit
    ///   -cubeFrames &lt;int&gt;       frames to wait before the screenshot (default 90)
    /// </summary>
    public sealed class PilotBootstrap : MonoBehaviour
    {
        public const uint DefaultSeed = 666666u;

        [SerializeField] private uint seed = DefaultSeed;
        [SerializeField] private int chunksPerAxis = PilotWorldLayout.ViewChunks;
        [SerializeField] private Material terrainMaterial;

        private PilotWorldView _worldView;
        private Transform _player;
        private PlayerMotor _motor;
        private OrbitCameraController _orbitCamera;

        private string _screenshotPath;
        private int _screenshotFrame = -1;
        private int _frames;
        private float _elapsed;

        public uint Seed => seed;
        public PilotWorldView WorldView => _worldView;
        public Transform Player => _player;

        private void Awake()
        {
            ParseArguments();
            BuildScene();
        }

        private void Update()
        {
            _frames++;
            _elapsed += Time.unscaledDeltaTime;

            if (_screenshotFrame >= 0 && _frames >= _screenshotFrame)
            {
                CaptureAndQuit();
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_orbitCamera != null)
                {
                    _orbitCamera.LockCursor(Cursor.lockState != CursorLockMode.Locked);
                }
            }
        }

        private void ParseArguments()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                switch (args[i])
                {
                    case "-cubeSeed":
                        if (uint.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out uint parsedSeed))
                        {
                            seed = parsedSeed;
                        }

                        break;
                    case "-cubeChunks":
                        if (int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedChunks) && parsedChunks > 0)
                        {
                            chunksPerAxis = parsedChunks;
                        }

                        break;
                    case "-cubeScreenshot":
                        _screenshotPath = args[i + 1];
                        _screenshotFrame = 90;
                        break;
                    case "-cubeFrames":
                        if (int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedFrames) && parsedFrames > 0)
                        {
                            _screenshotFrame = parsedFrames;
                        }

                        break;
                }
            }
        }

        private void BuildScene()
        {
            var worldRoot = new GameObject("WorldRoot");
            _worldView = worldRoot.AddComponent<PilotWorldView>();
            _worldView.TerrainMaterial = terrainMaterial;
            _worldView.Build(seed, chunksPerAxis, 0, 0);

            int spawnX = chunksPerAxis * PilotWorldLayout.ChunkSize / 2;
            int spawnZ = chunksPerAxis * PilotWorldLayout.ChunkSize / 2;
            int groundHeight = new PilotHeightField(seed).HeightAt(spawnX, spawnZ);

            var playerObject = new GameObject("Player");
            var controller = playerObject.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.4f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.stepOffset = 0.35f;
            controller.slopeLimit = 50f;
            playerObject.transform.position = new Vector3(spawnX + 0.5f, groundHeight + 1.2f, spawnZ + 0.5f);

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "PlayerBody";
            body.transform.SetParent(playerObject.transform, false);
            body.transform.localScale = new Vector3(0.8f, 0.9f, 0.8f);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            UnityEngine.Object.Destroy(body.GetComponent<Collider>());

            var bodyRenderer = body.GetComponent<Renderer>();
            bodyRenderer.sharedMaterial = PilotMaterials.Terrain(null);

            _player = playerObject.transform;
            _motor = playerObject.AddComponent<PlayerMotor>();

            Camera camera = Camera.main;
            if (camera == null)
            {
                var cameraObject = new GameObject("PilotCamera");
                camera = cameraObject.AddComponent<Camera>();
                cameraObject.tag = "MainCamera";
                cameraObject.AddComponent<AudioListener>();
            }

            camera.transform.position = _player.position + new Vector3(0f, 5f, -8f);
            camera.fieldOfView = 60f;
            _orbitCamera = camera.gameObject.GetComponent<OrbitCameraController>();
            if (_orbitCamera == null)
            {
                _orbitCamera = camera.gameObject.AddComponent<OrbitCameraController>();
            }

            _orbitCamera.Target = _player;
            _orbitCamera.Distance = OrbitCameraController.DefaultDistance;
            _motor.SetCamera(camera.transform);

            if (FindLight() == null)
            {
                var lightObject = new GameObject("PilotLight");
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.1f;
                lightObject.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            }

            var hudObject = new GameObject("PilotHud");
            var hud = hudObject.AddComponent<PilotDebugHud>();
            hud.Configure(seed, _player, _worldView);

            Debug.Log($"[Pilot] seed={seed} chunks={_worldView.ChunkCount} " +
                      $"vertices={_worldView.LastVertexCount} gen={_worldView.LastBuildMilliseconds:F1}ms " +
                      $"spawn=({spawnX},{groundHeight},{spawnZ})");
        }

        private static Light FindLight()
        {
#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindFirstObjectByType<Light>();
#else
            return UnityEngine.Object.FindObjectOfType<Light>();
#endif
        }

        private void CaptureAndQuit()
        {
            _screenshotFrame = -1;
            string path = string.IsNullOrEmpty(_screenshotPath)
                ? System.IO.Path.Combine(Application.persistentDataPath, "pilot_screenshot.png")
                : _screenshotPath;

            try
            {
                ScreenCapture.CaptureScreenshot(path);
                Debug.Log($"[Pilot] screenshot requested -> {path} " +
                          $"frames={_frames} time={_elapsed:F2}s fps≈{_frames / Mathf.Max(_elapsed, 0.001f):F1}");
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Pilot] screenshot failed: {exception.Message}");
            }

            StartCoroutine(QuitAfterFrames(3));
        }

        private System.Collections.IEnumerator QuitAfterFrames(int frames)
        {
            for (int i = 0; i < frames; i++)
            {
                yield return null;
            }

            Debug.Log("[Pilot] quitting");
            Application.Quit(0);
        }
    }
}
