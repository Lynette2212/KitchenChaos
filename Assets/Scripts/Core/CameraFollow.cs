using UnityEngine;

namespace KitchenChaos.Core
{
    /// <summary>斜俯视相机，平滑跟随玩家并限制在厨房范围内。</summary>
    public class CameraFollow : MonoBehaviour
    {
        public Transform target;
        public Vector3 offset = new Vector3(0f, 10f, -8.5f);
        public float smooth = 6f;
        public Vector2 xBounds = new Vector2(-3.6f, 3.6f);
        public Vector2 zBounds = new Vector2(-2.8f, 2.8f);

        private void LateUpdate()
        {
            if (target == null) return;

            Vector3 desired = new Vector3(
                Mathf.Clamp(target.position.x, xBounds.x, xBounds.y),
                0f,
                Mathf.Clamp(target.position.z, zBounds.x, zBounds.y)) + offset;

            transform.position = Vector3.Lerp(transform.position, desired, smooth * Time.deltaTime);
            transform.LookAt(target.position + Vector3.up * 0.5f);
        }
    }
}
