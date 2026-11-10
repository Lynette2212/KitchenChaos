using UnityEngine;

namespace KitchenChaos.Visual
{
    /// <summary>
    /// 导入的 Q 版厨师：模型本身是静态网格，这里用整体位移/倾斜做走路、切菜、庆祝和失误动作，
    /// 表情通过头顶小气泡呈现（专注、开心、失误）。
    /// </summary>
    public class ChefModel : MonoBehaviour
    {
        public enum ChefMood { None, Focus, Happy, Oops }

        private Transform visual;
        private Renderer[] renderers = System.Array.Empty<Renderer>();
        private float phase;
        private float moveBlend;
        private float mistakeTimer;
        private float celebrateTimer;
        private float pulseTimer;
        private float moodTimer;
        private ChefMood mood = ChefMood.None;
        private bool carrying;
        private bool working;

        public bool IsMistakeActive => mistakeTimer > 0f;
        public bool IsWorking => working;
        public ChefMood Mood
        {
            get
            {
                if (moodTimer > 0f && mood != ChefMood.None) return mood;
                if (mistakeTimer > 0f) return ChefMood.Oops;
                return working ? ChefMood.Focus : ChefMood.None;
            }
        }

        public void SetMoving(bool moving, float speed01)
        {
            moveBlend = Mathf.MoveTowards(moveBlend, moving ? Mathf.Clamp01(0.4f + speed01 * 0.6f) : 0f,
                Time.deltaTime * 7f);
        }

        public void SetCarrying(bool hasItem) => carrying = hasItem;

        public void SetWorking(bool value) => working = value;

        public void PlayMistake()
        {
            mistakeTimer = 0.55f;
            SetMood(ChefMood.Oops, 1.5f);
        }

        public void Celebrate()
        {
            celebrateTimer = 0.75f;
            SetMood(ChefMood.Happy, 2.4f);
        }

        public void Pulse() => pulseTimer = 0.2f;

        public void SetMood(ChefMood value, float seconds)
        {
            mood = value;
            moodTimer = Mathf.Max(0f, seconds);
        }

        private void Awake() => Build();

        public void Build()
        {
            if (visual != null) return;
            visual = new GameObject("ChefVisual").transform;
            visual.SetParent(transform, false);

            GameObject prefab = Resources.Load<GameObject>("Art/Chef/Chef");
            GameObject model = prefab != null ? Instantiate(prefab, visual, false) : null;
            if (model == null)
            {
                BuildFallback();
                return;
            }
            model.name = "ChefMesh";
            var facing = new GameObject("ChefFacing").transform;
            facing.SetParent(visual, false);
            model.transform.SetParent(facing, false);
            DestroyColliders(model);
            renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                Destroy(model);
                BuildFallback();
                return;
            }

            Texture2D skin = Resources.Load<Texture2D>("Art/Chef/Chef_BaseColor");
            if (skin != null)
            {
                var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "ChefSkin" };
                material.SetTexture("_BaseMap", skin);
                material.SetFloat("_Smoothness", 0.22f);
                foreach (Renderer renderer in renderers) renderer.sharedMaterial = material;
            }

            Bounds bounds = UnionBounds();
            float height = Mathf.Max(0.05f, bounds.size.y);
            float scale = 1.3f / height;
            model.transform.localScale = Vector3.one * scale;
            bounds = UnionBounds();
            Vector3 center = bounds.center - transform.position;
            model.transform.localPosition += new Vector3(-center.x, transform.position.y - bounds.min.y, -center.z);
            // 模型的脸朝自身 -X，绕 Y 补 +90° 后统一为本地 +Z，和移动朝向一致。
            facing.localRotation = Quaternion.Euler(0f, 90f, 0f);
            foreach (Renderer renderer in renderers)
            {
                renderer.gameObject.layer = 0;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
        }

        private void BuildFallback()
        {
            Material coat = VisualFactory.MakeMaterial(new Color(0.97f, 0.95f, 0.89f), 0.22f);
            Material apron = VisualFactory.MakeMaterial(new Color(0.12f, 0.27f, 0.35f), 0.18f);
            Material skin = VisualFactory.MakeMaterial(new Color(0.98f, 0.80f, 0.66f), 0.32f);
            Part(PrimitiveType.Capsule, "Body", new Vector3(0f, 0.52f, 0f), new Vector3(0.40f, 0.32f, 0.36f), coat);
            Part(PrimitiveType.Sphere, "Head", new Vector3(0f, 1.00f, 0f), new Vector3(0.36f, 0.33f, 0.35f), skin);
            Part(PrimitiveType.Cube, "Apron", new Vector3(0f, 0.46f, 0.185f), new Vector3(0.32f, 0.30f, 0.03f), apron);
            renderers = visual.GetComponentsInChildren<Renderer>();
        }

        private GameObject Part(PrimitiveType type, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.layer = 0;
            go.transform.SetParent(visual, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            Collider collider = go.GetComponent<Collider>();
            if (collider != null) { collider.enabled = false; Destroy(collider); }
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        private static void DestroyColliders(GameObject model)
        {
            foreach (Collider collider in model.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
                Destroy(collider);
            }
        }

        private Bounds UnionBounds()
        {
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private void Update()
        {
            if (visual == null) return;
            float dt = Time.deltaTime;
            if (moodTimer > 0f) moodTimer = Mathf.Max(0f, moodTimer - dt);
            if (pulseTimer > 0f) pulseTimer = Mathf.Max(0f, pulseTimer - dt);

            if (mistakeTimer > 0f)
            {
                mistakeTimer = Mathf.Max(0f, mistakeTimer - dt);
                float shake = Mathf.Sin(mistakeTimer * 70f) * 22f;
                visual.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(mistakeTimer * 26f)) * 0.05f, 0f);
                visual.localRotation = Quaternion.Euler(0f, shake, Mathf.Sin(mistakeTimer * 55f) * 3f);
                return;
            }
            if (celebrateTimer > 0f)
            {
                celebrateTimer = Mathf.Max(0f, celebrateTimer - dt);
                float t = 1f - celebrateTimer / 0.75f;
                float lift = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * 0.26f;
                visual.localPosition = new Vector3(0f, lift, 0f);
                visual.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * Mathf.PI * 2f) * 7f);
                visual.localScale = new Vector3(1f + lift * 0.2f, 1f - lift * 0.35f, 1f + lift * 0.2f);
                return;
            }

            visual.localScale = Vector3.one;
            phase += dt * (working ? 15f : Mathf.Lerp(2f, 12f, moveBlend));
            float bob = working ? Mathf.Abs(Mathf.Sin(phase)) * 0.055f : Mathf.Abs(Mathf.Sin(phase)) * 0.03f * moveBlend;
            float lean = working ? Mathf.Sin(phase) * 7f : carrying ? -3f : 0f;
            if (pulseTimer > 0f) lean -= Mathf.Sin((1f - pulseTimer / 0.2f) * Mathf.PI) * 5f;
            float rock = working ? 0f : Mathf.Sin(phase) * 2.5f * moveBlend;
            float waddle = working ? Mathf.Sin(phase * 0.5f) * 2f : Mathf.Sin(phase * 0.5f) * 4f * moveBlend;
            visual.localPosition = new Vector3(0f, bob, 0f);
            visual.localRotation = Quaternion.Euler(lean, waddle, rock);
        }
    }
}
