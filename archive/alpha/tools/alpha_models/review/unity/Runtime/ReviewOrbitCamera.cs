// ReviewOrbitCamera.cs — free orbit / zoom / pan camera of the review scene (input comes from
// ReviewViewer's IMGUI events, so it works with either Unity input backend).
using UnityEngine;

namespace CubeGame.AlphaTransfer.Review
{
    [RequireComponent(typeof(Camera))]
    public class ReviewOrbitCamera : MonoBehaviour
    {
        public Vector3 target = new Vector3(0, 0.3f, 0);
        public float yaw = 200f, pitch = 12f, distance = 6f;
        public float minDistance = 0.3f, maxDistance = 80f;

        public void Rotate(float dx, float dy)
        {
            yaw += dx * 0.3f;
            pitch = Mathf.Clamp(pitch + dy * 0.3f, -89f, 89f);
            Apply();
        }

        public void Zoom(float steps)
        {
            distance = Mathf.Clamp(distance * Mathf.Pow(1.1f, steps), minDistance, maxDistance);
            Apply();
        }

        public void Pan(float dx, float dy)
        {
            float k = distance * 0.0015f;
            target += (-transform.right * dx + transform.up * dy) * k;
            Apply();
        }

        public void SetView(float newYaw, float newPitch) { yaw = newYaw; pitch = newPitch; Apply(); }

        public void Frame(Bounds b)
        {
            target = b.center;
            var cam = GetComponent<Camera>();
            float radius = Mathf.Max(b.extents.magnitude, 0.05f);
            distance = Mathf.Clamp(radius / Mathf.Sin(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.1f, minDistance, maxDistance);
            Apply();
        }

        public void Apply()
        {
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            transform.position = target - rot * Vector3.forward * distance;
            transform.rotation = rot;
        }

        void LateUpdate() => Apply();
    }
}
