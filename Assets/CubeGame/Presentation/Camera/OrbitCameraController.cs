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

        public float Yaw => _yaw;
        public float Pitch => _pitch;

        /// <summary>Sets the orbit angles directly (automation, cut-scenes); pitch is clamped like mouse look.</summary>
        public void SetAngles(float yaw, float pitch)
        {
            _yaw = yaw;
            _pitch = Mathf.Clamp(pitch, MinPitch, MaxPitch);
            ApplyTransform();
        }

        private void Start()
        {
            _yaw = transform.eulerAngles.y;
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
                _yaw += Input.GetAxis("Mouse X") * MouseSensitivity;
                _pitch -= Input.GetAxis("Mouse Y") * MouseSensitivity;
                _pitch = Mathf.Clamp(_pitch, MinPitch, MaxPitch);
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
            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
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
    }
}
