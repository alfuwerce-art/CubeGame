using CubeGame.Core.Movement;
using UnityEngine;

namespace CubeGame.Presentation.Player
{
    /// <summary>
    /// Hero movement: Alpha 2013 locomotion (<see cref="AlphaMovement"/>, numbers from Cube.exe, C2) on top of
    /// CharacterController collision. One Alpha speed unit = one block per second = one Unity unit per second.
    ///
    /// Keys as in Alpha (DirectInput table in WinMain 0x4C91F2): W/S/A/D move relative to the camera, Space jumps
    /// (hold to jump higher, 100 ms boost), left Shift walks — without it the hero runs (action flag 0x40).
    ///
    /// Collision stays Unity's capsule (CubeGame decision, I8/P-23): Alpha's swept voxel collision is not ported.
    /// Time is stepped in whole milliseconds like Alpha; the fractional rest is carried to the next frame.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        [SerializeField] private Transform cameraTransform;

        private readonly AlphaMovement _movement = new AlphaMovement();
        private CharacterController _controller;
        private double _carryMs;

        /// <summary>Alpha action flag 0x40: the hero runs (left Shift not held).</summary>
        public bool Sprinting => _movement.Running;

        public float VerticalSpeed => (float)_movement.VelocityY;

        public float HorizontalSpeed => (float)_movement.HorizontalSpeed;

        public AlphaMovement Movement => _movement;

        public void SetCamera(Transform cameraTarget)
        {
            cameraTransform = cameraTarget;
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        private void OnDisable()
        {
            _movement.Stop();
            _carryMs = 0.0;
        }

        private void Update()
        {
            if (_controller == null)
            {
                return;
            }

            _carryMs += Time.deltaTime * 1000.0;
            int elapsedMs = (int)_carryMs;
            if (elapsedMs <= 0)
            {
                return;
            }

            _carryMs -= elapsedMs;

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

            // Alpha adds 80 blocks/s² per pressed key along the camera axes (a diagonal is not normalised).
            Vector3 direction = forward * inputZ + right * inputX;
            bool walkHeld = Input.GetKey(KeyCode.LeftShift);
            bool jumpHeld = Input.GetKey(KeyCode.Space);

            (double dx, double dy, double dz) = _movement.BeginFrame(
                elapsedMs, direction.x, direction.z, walkHeld, jumpHeld, IsGrounded);
            CollisionFlags flags = _controller.Move(new Vector3((float)dx, (float)dy, (float)dz));
            _movement.EndFrame(elapsedMs, IsGrounded, (flags & CollisionFlags.Above) != 0);
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

                if (_movement.VelocityY > 0.0)
                {
                    return false;
                }

                Vector3 origin = transform.position + Vector3.up * 0.1f;
                return Physics.Raycast(origin, Vector3.down, 0.25f, ~0, QueryTriggerInteraction.Ignore);
            }
        }
    }
}
