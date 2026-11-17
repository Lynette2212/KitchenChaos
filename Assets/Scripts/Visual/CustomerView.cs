using UnityEngine;

namespace KitchenChaos.Visual
{
    public class CustomerView : MonoBehaviour
    {
        [SerializeField] private Transform head;
        [SerializeField] private float phase;
        private float cheerTimer;
        private Vector3 home;
        private Quaternion baseLocalRotation;
        private Quaternion baseHeadLocalRotation;

        public Transform BubbleAnchor => head != null ? head : transform;

        private void Awake()
        {
            home = transform.localPosition;
            baseLocalRotation = transform.localRotation;
            if (head != null) baseHeadLocalRotation = head.localRotation;
        }

#if UNITY_EDITOR
        public void Init(Color coatColor, int index)
        {
            if (Application.isPlaying) return;
            phase = index * 1.7f;
            Vector3 look = new Vector3(1f, 0f, -0.35f).normalized;
            float baseYaw = Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg;
            transform.localRotation = Quaternion.Euler(0f, baseYaw, 0f);

            Material coat = VisualFactory.MakeMaterial(coatColor, 0.2f);
            Material skin = VisualFactory.MakeMaterial(index == 0 ? new Color(0.96f, 0.78f, 0.63f)
                : index == 1 ? new Color(0.88f, 0.70f, 0.55f) : new Color(0.99f, 0.82f, 0.70f), 0.3f);
            Material hair = VisualFactory.MakeMaterial(index == 1 ? new Color(0.20f, 0.15f, 0.12f) : new Color(0.30f, 0.22f, 0.15f), 0.15f);
            Material eye = VisualFactory.MakeMaterial(new Color(0.10f, 0.14f, 0.16f), 0.5f);
            Material cup = VisualFactory.MakeMaterial(new Color(0.93f, 0.90f, 0.82f), 0.4f);

            Part(PrimitiveType.Capsule, "Body", new Vector3(0f, 0.45f, 0f), new Vector3(0.42f, 0.45f, 0.36f), coat);
            Part(PrimitiveType.Cube, "Mug", new Vector3(0.26f, 0.62f, 0.22f), new Vector3(0.12f, 0.14f, 0.12f), cup);
            head = new GameObject("Head").transform;
            head.SetParent(transform, false);
            head.localPosition = new Vector3(0f, 0.98f, 0f);
            Part(PrimitiveType.Sphere, "Face", new Vector3(0f, 0.02f, 0f), new Vector3(0.36f, 0.34f, 0.35f), skin, head);
            Part(PrimitiveType.Sphere, "Hair", new Vector3(0f, 0.08f, -0.05f), new Vector3(0.38f, 0.24f, 0.34f), hair, head);
            Part(PrimitiveType.Sphere, "EyeL", new Vector3(-0.075f, 0.02f, 0.17f), Vector3.one * 0.05f, eye, head);
            Part(PrimitiveType.Sphere, "EyeR", new Vector3(0.075f, 0.02f, 0.17f), Vector3.one * 0.05f, eye, head);
        }

        private GameObject Part(PrimitiveType type, string name, Vector3 position, Vector3 scale, Material material, Transform parent = null)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.layer = 0;
            go.transform.SetParent(parent != null ? parent : transform, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            Collider collider = go.GetComponent<Collider>();
            if (collider != null) { collider.enabled = false; DestroyImmediate(collider); }
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }
#endif

        public void Cheer() => cheerTimer = 0.9f;

        private void Update()
        {
            float dt = Time.deltaTime;
            float t = Time.time + phase;
            float lift = Mathf.Abs(Mathf.Sin(t * 1.5f)) * 0.015f;
            if (cheerTimer > 0f)
            {
                cheerTimer = Mathf.Max(0f, cheerTimer - dt);
                lift += Mathf.Sin((1f - cheerTimer / 0.9f) * Mathf.PI) * 0.18f;
            }
            transform.localPosition = home + Vector3.up * lift;
            transform.localRotation = baseLocalRotation * Quaternion.Euler(0f, Mathf.Sin(t * 0.6f) * 4f, 0f);
            if (head != null)
                head.localRotation = baseHeadLocalRotation * Quaternion.Euler(0f, Mathf.Sin(t * 1.2f) * 10f, 0f);
        }
    }
}
