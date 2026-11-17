using UnityEngine;
using KitchenChaos.Visual;

namespace KitchenChaos.Core
{
    [RequireComponent(typeof(Camera))]
    public class TopDownCamera : MonoBehaviour
    {
        private Vector3 homePosition;

        private void Awake() => homePosition = transform.localPosition;

        private void LateUpdate()
        {
            Vector3 offset = FeedbackFX.ShakeOffset;
            if (transform.parent != null) offset = transform.parent.InverseTransformVector(offset);
            transform.localPosition = homePosition + offset;
        }
    }
}
