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

        [SerializeField] private CustomerView[] people = System.Array.Empty<CustomerView>();
        private int speaker;
        private float rotateIn = 5.5f;
        private float thanksTimer;
        private string thanksLine = string.Empty;
        private int lastCompleted;
        private Bubble bubble;

        public Bubble ActiveBubble => bubble;

        private void Start()
        {
            GameManager gm = GameManager.Instance;
            lastCompleted = gm != null ? gm.DayCompletedOrders : 0;
        }

#if UNITY_EDITOR
        public void Build()
        {
            if (Application.isPlaying) return;
            BuildWindow();
            BuildPeople();
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

        private void Part(string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            Collider collider = go.GetComponent<Collider>();
            if (collider != null) { collider.enabled = false; DestroyImmediate(collider); }
            go.GetComponent<Renderer>().sharedMaterial = material;
        }
#endif

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
    }
}
