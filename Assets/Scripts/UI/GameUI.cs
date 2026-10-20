using System.Collections.Generic;
using UnityEngine;
using KitchenChaos.Core;
using KitchenChaos.Data;
using KitchenChaos.Gameplay;

namespace KitchenChaos.UI
{
    public class GameUI : MonoBehaviour
    {
        public GameManager GameManager;
        public Camera Camera;
        public List<CookingStation> Stations = new List<CookingStation>();
        public List<IngredientItem> Bins = new List<IngredientItem>();

        private Font font;
        private GUIStyle smallStyle;
        private GUIStyle centerStyle;
        private GUIStyle timerStyle;
        private GUIStyle bigStyle;
        private GUIStyle worldStyle;

        private readonly List<string> messages = new List<string>();
        private readonly List<float> messageTimes = new List<float>();

        public void Initialize(GameManager gameManager, List<CookingStation> stations, List<IngredientItem> bins, Camera targetCamera)
        {
            GameManager = gameManager;
            Stations = stations ?? new List<CookingStation>();
            Bins = bins ?? new List<IngredientItem>();
            Camera = targetCamera;
        }

        private void Awake()
        {
            font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "Arial" }, 16);
        }

        public void ShowMessage(string message)
        {
            if (string.IsNullOrEmpty(message)) return;

            messages.Add(message);
            messageTimes.Add(Time.time);
            while (messages.Count > 5)
            {
                messages.RemoveAt(0);
                messageTimes.RemoveAt(0);
            }
        }

        private void OnGUI()
        {
            if (GameManager == null) return;

            EnsureStyles();
            DrawTimer();
            DrawOrdersHeader();
            DrawWorldLabels();
            DrawInstructions();
            DrawMessages();

            if (!GameManager.IsPlaying) DrawGameOver();
        }

        private void DrawTimer()
        {
            float remaining = GameManager.Timer != null ? GameManager.Timer.TimeRemaining : 0f;
            int seconds = Mathf.CeilToInt(remaining);
            string clock = (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
            Color original = GUI.color;
            if (seconds <= 20) GUI.color = new Color(1f, 0.55f, 0.55f);

            GUI.Box(new Rect(Screen.width * 0.5f - 100f, 16f, 200f, 42f), GUIContent.none);
            GUI.Label(new Rect(Screen.width * 0.5f - 100f, 20f, 200f, 34f), "剩余 " + clock, timerStyle);
            GUI.color = original;
        }

        private void DrawOrdersHeader()
        {
            GUI.Label(new Rect(Screen.width - 294f, 62f, 270f, 26f), "订单", centerStyle);
        }

        private void DrawWorldLabels()
        {
            if (Camera == null) return;

            GUI.depth = 5;

            foreach (IngredientItem bin in Bins)
            {
                if (bin == null) continue;
                DrawWorldLabel(bin.transform.position + Vector3.up * 0.85f, "[" + Recipe.GetIngredientName(bin.Type) + "]", new Color(0.9f, 0.95f, 1f));
            }

            foreach (CookingStation station in Stations)
            {
                if (station == null) continue;
                Color color = station.IsReady ? new Color(0.55f, 1f, 0.65f) : new Color(1f, 0.9f, 0.65f);
                DrawWorldLabel(station.transform.position + Vector3.up * 0.85f, station.StationName + "：" + station.GetStatusText(), color);
            }
        }

        private void DrawWorldLabel(Vector3 worldPosition, string text, Color color)
        {
            Vector3 screenPosition = Camera.WorldToScreenPoint(worldPosition);
            if (screenPosition.z < 0f) return;

            float guiY = Screen.height - screenPosition.y;
            Rect rect = new Rect(screenPosition.x - 120f, guiY - 20f, 240f, 24f);
            Color original = GUI.color;
            GUI.color = color;
            GUI.Label(rect, text, worldStyle);
            GUI.color = original;
        }

        private void DrawInstructions()
        {
            GUI.Label(new Rect(16f, Screen.height - 58f, Screen.width - 32f, 24f),
                "拖拽：下方食材 → 中间灶台 → 烹饪完成后拖到右侧订单", centerStyle);
            GUI.Label(new Rect(16f, Screen.height - 32f, Screen.width - 32f, 22f),
                "倒计时 120 秒 · 连续完成订单提高连击倍率", centerStyle);
        }

        private void DrawMessages()
        {
            float baseY = Screen.height - 100f;
            for (int i = 0; i < messages.Count; i++)
            {
                float age = Time.time - messageTimes[i];
                if (age > 6f) continue;

                float alpha = Mathf.Clamp01(1f - Mathf.Max(0f, age - 4f) / 2f);
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.Label(new Rect(20f, baseY - i * 22f, 760f, 22f), messages[i], smallStyle);
            }
            GUI.color = Color.white;
        }

        private void DrawGameOver()
        {
            GUI.depth = -200;
            GUI.color = new Color(0f, 0f, 0f, 0.78f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            Rect panel = new Rect(Screen.width * 0.5f - 260f, Screen.height * 0.5f - 150f, 520f, 300f);
            GUI.Box(panel, GUIContent.none);
            GUI.Label(new Rect(panel.x + 20f, panel.y + 24f, panel.width - 40f, 44f), "时间到！", bigStyle);
            GUI.Label(new Rect(panel.x + 20f, panel.y + 86f, panel.width - 40f, 32f),
                "最终分数：" + GameManager.Score.Score, timerStyle);
            GUI.Label(new Rect(panel.x + 20f, panel.y + 126f, panel.width - 40f, 28f),
                "完成订单：" + GameManager.Score.CompletedOrders + " 单", centerStyle);
            GUI.Label(new Rect(panel.x + 20f, panel.y + 210f, panel.width - 40f, 30f), "按 R 重新开始", centerStyle);
        }

        private void EnsureStyles()
        {
            if (smallStyle != null) return;

            smallStyle = new GUIStyle(GUI.skin.label);
            smallStyle.font = font;
            smallStyle.fontSize = 14;
            smallStyle.normal.textColor = Color.white;
            smallStyle.wordWrap = true;

            centerStyle = new GUIStyle(smallStyle);
            centerStyle.alignment = TextAnchor.MiddleCenter;

            timerStyle = new GUIStyle(smallStyle);
            timerStyle.fontSize = 22;
            timerStyle.fontStyle = FontStyle.Bold;
            timerStyle.alignment = TextAnchor.MiddleCenter;

            bigStyle = new GUIStyle(smallStyle);
            bigStyle.fontSize = 32;
            bigStyle.fontStyle = FontStyle.Bold;

            worldStyle = new GUIStyle(GUI.skin.box);
            worldStyle.font = font;
            worldStyle.fontSize = 13;
            worldStyle.alignment = TextAnchor.MiddleCenter;
            worldStyle.normal.textColor = Color.white;
        }
    }
}
