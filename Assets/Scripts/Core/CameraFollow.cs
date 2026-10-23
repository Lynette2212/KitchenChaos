using UnityEngine;

namespace KitchenChaos.Core
{
    public class CameraFollow : MonoBehaviour
    {
        public Transform target;
        public float eyeHeight = 1.55f;
        public float mouseSensitivity = 2f;
        public float Pitch { get; private set; } = 12f;

        public void SetViewAngles(float yaw, float pitch)
        {
            if (target == null) return;
            target.rotation = Quaternion.Euler(0f, Mathf.Repeat(yaw, 360f), 0f);
            Pitch = Mathf.Clamp(pitch, -75f, 75f);
            RefreshPose();
        }

        public void AdvanceLook(Vector2 delta)
        {
            if (target == null) return;
            SetViewAngles(target.eulerAngles.y + delta.x * mouseSensitivity,
                Pitch - delta.y * mouseSensitivity);
        }

        public void RefreshPose()
        {
            if (target == null) return;
            transform.SetPositionAndRotation(target.position + Vector3.up * eyeHeight,
                Quaternion.Euler(Pitch, target.eulerAngles.y, 0f));
        }

        private void LateUpdate() => RefreshPose();
    }
}
