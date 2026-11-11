using UnityEngine;

namespace CubeGame.Presentation.CameraRig
{
    /// <summary>
    /// Third-person orbit camera of the pilot.
    ///
    /// PLACEHOLDER_NOT_ALPHA: distance, height, FOV and mouse sensitivity are pilot values.
    /// Alpha 2013 camera parameters were not measured yet (S00-01), see
    /// docs/research/alpha2013/GENERATION_MOVEMENT_SPEC.md section 4.
    /// </summary>
    public sealed class OrbitCameraController : MonoBehaviour
    {
        public const float DefaultDistance = 8.0f;
        public const float MinPitch = -15.0f;
        public const float MaxPitch = 80.0f;
        public const float MouseSensitivity = 3.0f;

        [SerializeField] private Transform target;
        [SerializeField] private float distance = DefaultDistance;
        [SerializeField] private float heightOffset = 1.6f;

        private float _yaw;
        private float _pitch = 20f;
        private float _shownYaw;
        private float _shownPitch = 20f;

        public System.Func<bool> AcceptsInput { get; set; }

        public Transform Target
        {
            get => target;
            set => target = value;
        }

        public float Distance
        {
            get => distance;
            set => distance = value;
        }

        public float Yaw => _shownYaw;
        public float Pitch => _shownPitch;

        /// <summary>Sets the orbit angles directly (automation, cut-scenes); pitch is clamped like mouse look.</summary>
        public void SetAngles(float yaw, float pitch)
        {
            _yaw = yaw;
            _pitch = Mathf.Clamp(pitch, MinPitch, MaxPitch);
            _shownYaw = _yaw;
            _shownPitch = _pitch;
            ApplyTransform();
        }

        private void Start()
        {
            _yaw = transform.eulerAngles.y;
            _shownYaw = _yaw;
            _shownPitch = _pitch;
            ApplyTransform();
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            if ((AcceptsInput == null || AcceptsInput()) &&
                (Input.GetMouseButton(1) || Cursor.lockState == CursorLockMode.Locked))
            {
                float sensitivity = MouseSensitivity * SpeedScale;
                _yaw += Input.GetAxis("Mouse X") * sensitivity;
                _pitch += (InvertY ? 1f : -1f) * Input.GetAxis("Mouse Y") * sensitivity;
                _pitch = Mathf.Clamp(_pitch, MinPitch, MaxPitch);
            }

            if (Smoothness <= 0f)
            {
                _shownYaw = _yaw;
                _shownPitch = _pitch;
            }
            else
            {
                float factor = Mathf.Clamp01(1f - Smoothness);
                _shownYaw = Mathf.LerpAngle(_shownYaw, _yaw, factor);
                _shownPitch = Mathf.Lerp(_shownPitch, _pitch, factor);
            }

            ApplyTransform();
        }

        private void ApplyTransform()
        {
            if (target == null)
            {
                return;
            }

            Vector3 pivot = target.position + Vector3.up * heightOffset;
            Quaternion rotation = Quaternion.Euler(_shownPitch, _shownYaw, 0f);
            Vector3 offset = rotation * new Vector3(0f, 0f, -distance);
            transform.position = pivot + offset;
            transform.rotation = Quaternion.LookRotation(pivot - transform.position, Vector3.up);
        }

        /// <summary>Enables free mouse look without holding the right button (used by the pilot demo).</summary>
        public void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        // ----- Alpha options.cfg adapters (cameraSpeed / cameraSmoothness / invertY) -----
        // The exact Alpha conversion of these three bytes into look rotation is UNKNOWN; the port keeps the
        // installed defaults (speed 50, smoothness 80, invertY 0) as the neutral value so the stock settings
        // reproduce the previous behaviour exactly. Marked CUBEGAME_CONTRACT in the M0 P0 report.

        /// <summary>cameraSpeed 50 = 1.0 (the default the port was tuned on).</summary>
        public float SpeedScale { get; set; } = 1f;

        /// <summary>cameraSmoothness 80 = the tuned follow; 0 disables smoothing, 100 = slowest.</summary>
        public float Smoothness { get; set; }

        public bool InvertY { get; set; }
    }
}
