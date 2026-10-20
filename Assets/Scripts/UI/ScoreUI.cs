using UnityEngine;
using KitchenChaos.Core;

namespace KitchenChaos.UI
{
    public class ScoreUI : MonoBehaviour
    {
        public ScoreManager ScoreManager;

        private Font font;
        private GUIStyle scoreStyle;
        private GUIStyle detailStyle;

        public void Initialize(ScoreManager scoreManager)
        {
            ScoreManager = scoreManager;
        }

        private void Awake()
        {
            font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 16);
        }

        private void OnGUI()
        {
            if (ScoreManager == null) return;

            EnsureStyles();
            GUI.depth = 10;
            GUI.Box(new Rect(16f, 16f, 300f, 72f), GUIContent.none);
            GUI.Label(new Rect(28f, 22f, 276f, 30f), "分数：" + ScoreManager.Score, scoreStyle);
            GUI.Label(new Rect(28f, 54f, 276f, 22f),
                "连击 x" + ScoreManager.ComboMultiplier + "    完成 " + ScoreManager.CompletedOrders + " 单", detailStyle);
        }

        private void EnsureStyles()
        {
            if (scoreStyle != null) return;

            scoreStyle = new GUIStyle(GUI.skin.label);
            scoreStyle.font = font;
            scoreStyle.fontSize = 20;
            scoreStyle.fontStyle = FontStyle.Bold;
            scoreStyle.normal.textColor = Color.white;

            detailStyle = new GUIStyle(GUI.skin.label);
            detailStyle.font = font;
            detailStyle.fontSize = 13;
            detailStyle.normal.textColor = new Color(0.75f, 0.8f, 0.85f);
        }
    }
}
