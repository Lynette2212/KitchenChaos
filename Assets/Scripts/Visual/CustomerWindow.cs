using UnityEngine;
using KitchenChaos.Core;
using KitchenChaos.Data;

namespace KitchenChaos.Visual
{
    /// <summary>西侧取餐窗外等位的顾客：看厨房、催单、收到餐后道谢。</summary>
    public class CustomerWindow : MonoBehaviour
    {
        public struct Bubble
        {
            public Vector3 position;
            public string text;
            public bool urgent;
        }

        private CustomerView[] people = System.Array.Empty<CustomerView>();
        private int speaker;
        private float rotateIn = 5.5f;
        private float thanksTimer;
        private string thanksLine = string.Empty;
        private int lastCompleted;
        private Bubble bubble;

        public Bubble ActiveBubble => bubble;

        private void Awake()
        {
            BuildWindow();
            BuildPeople();
            GameManager gm = GameManager.Instance;
            lastCompleted = gm != null ? gm.DayCompletedOrders : 0;
        }

        private void BuildWindow()
        {
            Material wood = VisualFactory.Surface("Oak", new Vector2(2f, 1f));
            Material stone = VisualFactory.Surface("Terrazzo");
            Part("Window sill", new Vector3(-4.55f, 0.5f, 0.6f), new Vector3(0.18f, 1f, 3.8f), stone);
            Part("Window post N", new Vector3(-4.55f, 1.7f, -1.3f), new Vector3(0.14f, 1.5f, 0.14f), wood);
            Part("Window post S", new Vector3(-4.55f, 1.7f, 2.5f), new Vector3(0.14f, 1.5f, 0.14f), wood);
            Part("Window beam", new Vector3(-4.55f, 2.42f, 0.6f), new Vector3(0.14f, 0.12f, 3.8f), wood);
            Part("Window shelf", new Vector3(-4.42f, 1.02f, 0.6f), new Vector3(0.3f, 0.05f, 3.6f), wood);
        }

        private void BuildPeople()
        {
            people = new CustomerView[3];
            string[] names = { "顾客 A", "顾客 B", "顾客 C" };
            Color[] coats =
            {
                new Color(0.73f, 0.49f, 0.26f),
                new Color(0.31f, 0.48f, 0.55f),
                new Color(0.55f, 0.34f, 0.42f)
            };
            float[] zs = { 1.6f, 0.3f, -1.0f };
            for (int i = 0; i < people.Length; i++)
            {
                GameObject go = new GameObject(names[i]);
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(-5.15f, 0f, zs[i]);
                CustomerView view = go.AddComponent<CustomerView>();
                view.Init(coats[i], i);
                people[i] = view;
            }
        }

        private void Update()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null) return;
            if (gm.DayCompletedOrders > lastCompleted)
            {
                lastCompleted = gm.DayCompletedOrders;
                thanksTimer = 3f;
                thanksLine = "谢谢款待！";
                foreach (CustomerView person in people) if (person != null) person.Cheer();
            }

            if (thanksTimer > 0f)
            {
                thanksTimer -= Time.deltaTime;
                bubble = new Bubble { position = people[0].BubbleAnchor.position, text = thanksLine, urgent = false };
                return;
            }

            rotateIn -= Time.deltaTime;
            if (rotateIn <= 0f)
            {
                rotateIn = 5.5f;
                speaker = (speaker + 1) % people.Length;
            }

            Order order = null;
            OrderSystem orders = gm.Orders;
            bool urgent = false;
            if (orders != null && orders.ActiveOrders.Count > 0)
            {
                order = orders.ActiveOrders[Mathf.Min(speaker, orders.ActiveOrders.Count - 1)];
                urgent = order != null && order.TimeRatio < 0.3f;
            }
            string name = order != null && order.recipe != null ? order.recipe.recipeName : "好吃的";
            bubble = new Bubble
            {
                position = people[speaker].BubbleAnchor.position,
                urgent = urgent,
                text = order == null
                    ? (speaker == 0 ? "今天吃点啥？" : speaker == 1 ? "闻起来好香…" : "老板，加油！")
                    : urgent
                        ? (speaker == 0 ? "快点呀，要来不及了！" : speaker == 1 ? "好饿好饿…" : "等好久啦")
                        : (speaker == 0 ? "我想要一份「" + name + "」" : speaker == 1 ? "闻起来好香…" : "看起来好好吃")
            };
        }

        private void Part(string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            Collider collider = go.GetComponent<Collider>();
            if (collider != null) { collider.enabled = false; Destroy(collider); }
            go.GetComponent<Renderer>().sharedMaterial = material;
        }
    }

    public class CustomerView : MonoBehaviour
    {
        private Transform head;
        private float phase;
        private float cheerTimer;
        private float baseYaw;
        private Vector3 home;

        public Transform BubbleAnchor => head != null ? head : transform;

        public void Init(Color coatColor, int index)
        {
            home = transform.localPosition;
            phase = index * 1.7f;
            Vector3 look = new Vector3(1f, 0f, -0.35f).normalized;
            baseYaw = Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg;

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
            transform.localRotation = Quaternion.Euler(0f, baseYaw + Mathf.Sin(t * 0.6f) * 4f, 0f);
            if (head != null)
                head.localRotation = Quaternion.Euler(0f, Mathf.Sin(t * 1.2f) * 10f, 0f);
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
            if (collider != null) { collider.enabled = false; Destroy(collider); }
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }
    }
}
