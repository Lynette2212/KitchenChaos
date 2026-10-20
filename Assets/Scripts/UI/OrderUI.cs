using UnityEngine;
using KitchenChaos.Data;

namespace KitchenChaos.UI
{
    public class OrderUI : MonoBehaviour
    {
        public Order Order { get; private set; }
        public Rect Area { get; set; }

        private Font font;
        private GUIStyle cardStyle;
        private GUIStyle titleStyle;
        private GUIStyle textStyle;
        private GUIStyle smallStyle;

        private void Awake()
        {
            font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 16);
        }

        public void SetOrder(Order order)
        {
            Order = order;
        }

        private void OnGUI()
        {
            if (Order == null || Order.recipe == null || Area.width <= 0f) return;

            EnsureStyles();
            GUI.depth = 10;

            GUI.Box(Area, GUIContent.none, cardStyle);
            GUI.Label(new Rect(Area.x + 12f, Area.y + 8f, Area.width - 24f, 26f), Order.recipe.recipeName, titleStyle);

            int total = Order.recipe.ingredients.Count;
            int delivered = total - Order.remaining.Count;
            GUI.Label(new Rect(Area.x + 12f, Area.y + 36f, Area.width - 24f, 42f), "还需要：" + Order.GetRemainingText(), textStyle);
            GUI.Label(new Rect(Area.x + 12f, Area.y + 80f, Area.width - 24f, 20f),
                "进度 " + delivered + "/" + total + "    奖励 " + Order.recipe.score, smallStyle);
        }

        private void EnsureStyles()
        {
            if (cardStyle != null) return;

            cardStyle = new GUIStyle(GUI.skin.box);

            titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.font = font;
            titleStyle.fontSize = 17;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.normal.textColor = Color.white;
            titleStyle.wordWrap = true;

            textStyle = new GUIStyle(GUI.skin.label);
            textStyle.font = font;
            textStyle.fontSize = 14;
            textStyle.normal.textColor = new Color(0.85f, 0.9f, 1f);
            textStyle.wordWrap = true;

            smallStyle = new GUIStyle(textStyle);
            smallStyle.fontSize = 13;
            smallStyle.normal.textColor = new Color(0.7f, 0.75f, 0.8f);
        }
    }
}
