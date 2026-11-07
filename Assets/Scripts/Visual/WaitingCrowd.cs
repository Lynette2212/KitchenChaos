using UnityEngine;
using KitchenChaos.Core;
using KitchenChaos.Data;

namespace KitchenChaos.Visual
{
    /// <summary>
    /// 橱窗外的等位顾客。订单越接近超时，动作和台词越着急。
    /// </summary>
    public class WaitingCrowd : MonoBehaviour
    {
        private WaitingCustomer[] customers = System.Array.Empty<WaitingCustomer>();
        private Material glass;
        private Material streetGlow;

        public void Bind(WaitingCustomer[] people, Material windowGlass, Material streetWindows)
        {
            customers = people ?? System.Array.Empty<WaitingCustomer>();
            glass = windowGlass;
            streetGlow = streetWindows;
        }

        private void Update()
        {
            float anxiety = 0.25f;
            OrderSystem orders = GameManager.Instance != null ? GameManager.Instance.Orders : null;
            if (orders != null && orders.ActiveOrders.Count > 0)
            {
                float calmest = 1f;
                foreach (Order order in orders.ActiveOrders)
                    calmest = Mathf.Min(calmest, order.TimeRatio);
                anxiety = Mathf.Lerp(1.35f, 0.3f, calmest);
            }
            foreach (WaitingCustomer customer in customers)
                if (customer != null) customer.Tick(anxiety);
        }

        private void OnDestroy()
        {
            if (glass != null) Destroy(glass);
            if (streetGlow != null) Destroy(streetGlow);
        }
    }

    public class WaitingCustomer : MonoBehaviour
    {
        private Vector3 home;
        private Transform head;
        private Transform watchArm;
        private Transform prop;
        private TextMesh bubble;
        private string line;
        private int style;
        private float phase;

        public void Init(string speech, int fidgetStyle)
        {
            home = transform.localPosition;
            head = transform.Find("Head");
            watchArm = transform.Find("WatchArm");
            prop = transform.Find("Prop");
            bubble = GetComponentInChildren<TextMesh>();
            line = speech;
            style = fidgetStyle;
            phase = fidgetStyle * 1.37f;
            if (bubble != null) bubble.text = speech;
        }

        public void Tick(float anxiety)
        {
            float t = Time.time + phase;
            float speed = Mathf.Lerp(1f, 2.8f, anxiety);
            Vector3 offset = Vector3.zero;
            float yaw = -90f;
            switch (style % 4)
            {
                case 0:
                    offset.z = Mathf.Sin(t * 1.6f * speed) * 0.07f;
                    yaw += Mathf.Sin(t * 2.4f * speed) * 8f;
                    break;
                case 1:
                    offset.z = Mathf.PingPong(t * 0.28f * speed, 0.42f) - 0.21f;
                    yaw += Mathf.Sin(t * speed) * 5f;
                    break;
                case 2:
                    offset.y = Mathf.Abs(Mathf.Sin(t * 7f * speed)) * 0.025f;
                    if (prop != null) prop.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 7f * speed) * 10f);
                    break;
                default:
                    yaw += Mathf.Sin(t * 1.8f * speed) * 11f;
                    if (watchArm != null)
                        watchArm.localRotation = Quaternion.Euler(-55f + Mathf.Sin(t * 1.4f * speed) * 22f, 0f, 78f);
                    break;
            }
            transform.localPosition = home + offset;
            transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            if (head != null)
                head.localRotation = Quaternion.Euler(6f + Mathf.Sin(t * 3.1f * speed) * 12f, Mathf.Sin(t * 1.3f * speed) * 16f, 0f);
            if (bubble == null) return;
            bubble.color = Color.Lerp(new Color(0.95f, 0.93f, 0.86f), new Color(0.95f, 0.28f, 0.18f), anxiety);
            bubble.characterSize = 0.026f + Mathf.Sin(t * 4f * speed) * 0.002f * (0.4f + anxiety);
            bubble.text = anxiety > 0.85f ? line + "！" : line;
        }
    }
}
