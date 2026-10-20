using System.Collections.Generic;
using UnityEngine;
using KitchenChaos.Core;
using KitchenChaos.Data;

namespace KitchenChaos.UI
{
    public class OrderPanelManager : MonoBehaviour
    {
        public OrderSystem OrderSystem;
        public GameManager GameManager;

        private readonly List<OrderUI> cards = new List<OrderUI>();
        private const float CardWidth = 270f;
        private const float CardHeight = 116f;
        private const float CardGap = 12f;
        private const float RightMargin = 24f;
        private const float TopMargin = 92f;

        private int lastScreenWidth;
        private int lastScreenHeight;
        private int lastOrderCount = -1;

        public void Initialize(OrderSystem orderSystem, GameManager gameManager)
        {
            OrderSystem = orderSystem;
            GameManager = gameManager;
            OrderSystem.OrdersChanged += Refresh;
            EnsureCardCount();
            Refresh();
        }

        private void OnDestroy()
        {
            if (OrderSystem != null) OrderSystem.OrdersChanged -= Refresh;
        }

        private void Update()
        {
            if (OrderSystem == null) return;

            if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight || OrderSystem.ActiveOrders.Count != lastOrderCount)
            {
                Refresh();
            }
        }

        private void Refresh()
        {
            if (OrderSystem == null) return;

            EnsureCardCount();
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            lastOrderCount = OrderSystem.ActiveOrders.Count;

            for (int i = 0; i < cards.Count; i++)
            {
                bool hasOrder = i < OrderSystem.ActiveOrders.Count;
                cards[i].gameObject.SetActive(hasOrder);
                if (!hasOrder) continue;

                cards[i].SetOrder(OrderSystem.ActiveOrders[i]);
                cards[i].Area = new Rect(
                    Screen.width - CardWidth - RightMargin,
                    TopMargin + i * (CardHeight + CardGap),
                    CardWidth,
                    CardHeight);
            }
        }

        private void EnsureCardCount()
        {
            int target = Mathf.Max(3, OrderSystem.MaxActiveOrders);
            while (cards.Count < target)
            {
                GameObject cardObject = new GameObject("OrderCard_" + cards.Count);
                cardObject.transform.SetParent(transform, false);
                OrderUI card = cardObject.AddComponent<OrderUI>();
                cards.Add(card);
            }
        }

        public bool TryDeliverAt(Vector2 screenPosition, IngredientType type, bool isCooked)
        {
            Vector2 guiPosition = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            foreach (OrderUI card in cards)
            {
                if (!card.gameObject.activeSelf || card.Order == null) continue;
                if (!card.Area.Contains(guiPosition)) continue;

                if (!isCooked)
                {
                    if (GameManager != null && GameManager.UI != null) GameManager.UI.ShowMessage("订单只收熟食，先去灶台烹饪。");
                    return true;
                }

                if (GameManager != null) GameManager.Deliver(card.Order, type);
                return true;
            }

            return false;
        }
    }
}
