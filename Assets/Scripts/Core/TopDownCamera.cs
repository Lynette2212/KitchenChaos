using UnityEngine;
using KitchenChaos.Visual;

namespace KitchenChaos.Core
{
    /// <summary>固定斜俯视 2.5D 相机：整间厨房一屏可见，不跟随玩家。</summary>
    public class TopDownCamera : MonoBehaviour
    {
        public Vector3 focus = new Vector3(0.4f, 0f, 0.2f);
        public float pitch = 52f;
        public float distance = 12f;
        public float size = 4.7f;

        private Camera view;

        private void Awake()
        {
            view = GetComponent<Camera>();
            Apply();
        }

        public void Apply()
        {
            Quaternion rotation = Quaternion.Euler(pitch, 0f, 0f);
            transform.SetPositionAndRotation(focus - rotation * Vector3.forward * distance, rotation);
            if (view == null) view = GetComponent<Camera>();
            if (view == null) return;
            view.orthographic = true;
            view.orthographicSize = size;
        }

        private void LateUpdate()
        {
            Apply();
            transform.position += FeedbackFX.ShakeOffset;
        }
    }
}
