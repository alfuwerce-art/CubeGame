using UnityEngine;

namespace CubeGame.Presentation.Player
{
    /// <summary>
    /// Temporary pilot character movement on top of CharacterController.
    ///
    /// PLACEHOLDER_NOT_ALPHA: walk/sprint/jump values and the camera-relative control scheme are
    /// pilot decisions. Alpha 2013 movement was not measured yet (S00-04), see
    /// docs/research/alpha2013/GENERATION_MOVEMENT_SPEC.md section 4.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        public const float WalkSpeed = 5.0f;
        public const float SprintSpeed = 8.0f;
        public const float JumpSpeed = 7.0f;
        public const float Gravity = -24.0f;

        [SerializeField] private Transform cameraTransform;

        private CharacterController _controller;
        private float _verticalSpeed;

        public bool Sprinting => Input.GetKey(KeyCode.LeftShift);
        public float VerticalSpeed => _verticalSpeed;

        public void SetCamera(Transform cameraTarget)
        {
            cameraTransform = cameraTarget;
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        private void Update()
        {
            if (_controller == null)
            {
                return;
            }

            float inputX = 0f;
            float inputZ = 0f;
            if (Input.GetKey(KeyCode.W))
            {
                inputZ += 1f;
            }

            if (Input.GetKey(KeyCode.S))
            {
                inputZ -= 1f;
            }

            if (Input.GetKey(KeyCode.D))
            {
                inputX += 1f;
            }

            if (Input.GetKey(KeyCode.A))
            {
                inputX -= 1f;
            }

            Vector3 forward = Vector3.forward;
            Vector3 right = Vector3.right;
            if (cameraTransform != null)
            {
                forward = cameraTransform.forward;
                forward.y = 0f;
                forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
                right = Vector3.Cross(Vector3.up, forward).normalized * -1f;
            }

            Vector3 direction = forward * inputZ + right * inputX;
            if (direction.sqrMagnitude > 1f)
            {
                direction.Normalize();
            }

            float speed = Sprinting ? SprintSpeed : WalkSpeed;
            Vector3 horizontal = direction * (speed * Time.deltaTime);

            if (IsGrounded)
            {
                if (_verticalSpeed < 0f)
                {
                    _verticalSpeed = -2f;
                }

                if (Input.GetKeyDown(KeyCode.Space))
                {
                    _verticalSpeed = JumpSpeed;
                }
            }

            _verticalSpeed += Gravity * Time.deltaTime;
            _controller.Move(horizontal + Vector3.up * (_verticalSpeed * Time.deltaTime));
        }

        /// <summary>
        /// CharacterController keeps a skin width (0.08 m) between itself and the ground, and its
        /// isGrounded flag is only refreshed by Move(). At high frame rates the per-frame downward
        /// step becomes smaller than that gap, so isGrounded flickers to false while the character
        /// visibly stands on the terrain (jump and step stop working). A short downward probe makes
        /// the grounded state frame-rate independent. This is the gameplay truth; the raw
        /// CharacterController.isGrounded is not reliable enough to test against.
        /// </summary>
        public bool IsGrounded
        {
            get
            {
                if (_controller != null && _controller.isGrounded)
                {
                    return true;
                }

                if (_verticalSpeed > 0f)
                {
                    return false;
                }

                Vector3 origin = transform.position + Vector3.up * 0.1f;
                return Physics.Raycast(origin, Vector3.down, 0.25f, ~0, QueryTriggerInteraction.Ignore);
            }
        }
    }
}
