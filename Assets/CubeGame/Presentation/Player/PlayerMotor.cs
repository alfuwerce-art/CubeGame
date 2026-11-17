using System;
using CubeGame.Core.Movement;
using UnityEngine;

namespace CubeGame.Presentation.Player
{
    /// <summary>
    /// Hero movement: Alpha 2013 locomotion (<see cref="AlphaMovement"/>, numbers from Cube.exe, C2) on top of
    /// CharacterController collision. One Alpha speed unit = one block per second = one Unity unit per second.
    ///
    /// Keys as in Alpha (DirectInput table in WinMain 0x4C91F2, handleKeyPress 0x47E1B0): W/S/A/D move relative to the
    /// camera (in water along the full camera look), Space jumps / rises in water / boosts the glider, left Shift
    /// walks (without it the hero runs, flag 0x40), left Ctrl climbs a wall, G opens/closes the hang glider.
    ///
    /// Collision stays Unity's capsule (CubeGame decision, P-23): Alpha's swept voxel collision is not ported. Water
    /// contact comes from the world voxels (<see cref="WaterAt"/>, set by the game layer); wall contact for climbing
    /// is a short ray in the camera's horizontal direction (CUBEGAME_CONTRACT: Alpha's voxel probe for contact flag 4
    /// was not recovered). Time is stepped in whole milliseconds like Alpha; the fractional rest is carried over.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        /// <summary>Reach of the climbing wall probe beyond the capsule radius, Unity units.</summary>
        public const float WallProbeReach = 0.2f;

        [SerializeField] private Transform cameraTransform;

        private readonly AlphaMovement _movement = new AlphaMovement();
        private CharacterController _controller;
        private double _carryMs;
        private MovementContact _contact;

        /// <summary>Is the voxel containing this scene point water? Null = no water anywhere (pilot scenes).</summary>
        public Func<Vector3, bool> WaterAt { get; set; }

        /// <summary>Alpha action flag 0x40: the hero runs (left Shift not held).</summary>
        public bool Sprinting => _movement.Running;

        public float VerticalSpeed => (float)_movement.VelocityY;

        public System.Func<bool> AcceptsInput { get; set; }

        public float HorizontalSpeed => (float)_movement.HorizontalSpeed;

        public bool Swimming => _movement.InWater;

        public bool Climbing => _movement.Climbing;

        public bool Gliding => _movement.Gliding;

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
            if (_controller == null || (AcceptsInput != null && !AcceptsInput()))
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.G))
            {
                _movement.ToggleGlider(transform.position.y);
            }

            _carryMs += Time.deltaTime * 1000.0;
            int elapsedMs = (int)_carryMs;
            if (elapsedMs <= 0)
            {
                return;
            }

            _carryMs -= elapsedMs;

            var keys = new MovementKeys
            {
                Forward = Input.GetKey(KeyCode.W),
                Back = Input.GetKey(KeyCode.S),
                Left = Input.GetKey(KeyCode.A),
                Right = Input.GetKey(KeyCode.D),
                Jump = Input.GetKey(KeyCode.Space),
                Climb = Input.GetKey(KeyCode.LeftControl),
                Walk = Input.GetKey(KeyCode.LeftShift),
            };

            MovementView view = BuildView(out Vector3 flatForward);
            MovementContact before = MeasureContact(flatForward, false);
            (double dx, double dy, double dz) = _movement.BeginFrame(elapsedMs, keys, view, before);
            CollisionFlags flags = _controller.Move(new Vector3((float)dx, (float)dy, (float)dz));
            _contact = MeasureContact(flatForward, (flags & CollisionFlags.Above) != 0);
            if ((flags & CollisionFlags.Sides) != 0)
            {
                _contact.AtWall = true;
            }

            _movement.EndFrame(elapsedMs, _contact);
        }

        private MovementView BuildView(out Vector3 flatForward)
        {
            Vector3 look = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
            flatForward = new Vector3(look.x, 0f, look.z);
            flatForward = flatForward.sqrMagnitude > 0.0001f ? flatForward.normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, flatForward).normalized;
            return new MovementView { LookX = look.x, LookY = look.y, LookZ = look.z, RightX = right.x, RightZ = right.z };
        }

        private MovementContact MeasureContact(Vector3 flatForward, bool hitCeiling)
        {
            float height = _controller.height;
            Vector3 feet = transform.position + _controller.center - Vector3.up * (height * 0.5f);
            Vector3 centre = transform.position + _controller.center;
            var contact = new MovementContact
            {
                OnGround = IsGrounded,
                HitCeiling = hitCeiling,
                BodyHeight = height,
                PositionY = transform.position.y,
                AtWall = Physics.Raycast(centre, flatForward, _controller.radius + WallProbeReach, ~0, QueryTriggerInteraction.Ignore),
            };

            if (WaterAt != null)
            {
                // Alpha 0x61CF..0x61D074: the highest water block overlapping the body; depth = its top − body centre.
                int bottom = Mathf.FloorToInt(feet.y);
                int top = Mathf.FloorToInt(feet.y + height);
                int waterTop = int.MinValue;
                for (int y = bottom; y <= top; y++)
                {
                    if (WaterAt(new Vector3(centre.x, y + 0.5f, centre.z)))
                    {
                        waterTop = y + 1;
                    }
                }

                if (waterTop != int.MinValue)
                {
                    contact.InWater = true;
                    contact.WaterDepth = waterTop - centre.y;
                }
            }

            return contact;
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
