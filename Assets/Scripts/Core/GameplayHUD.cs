using System.Collections.Generic;
using UnityEngine;
using KitchenChaos.Data;
using KitchenChaos.Gameplay;

namespace KitchenChaos.Core
{
    /// <summary>
    /// 全部 IMGUI 界面：开始页、游戏内 HUD（分数/声望/计时/订单票/交互提示/加工进度/飘字）、结算页。
    /// 使用 OnGUI 以零 UI Prefab 依赖运行；字体动态取自系统中文字体。
    /// </summary>
    public class GameplayHUD : MonoBehaviour
    {
        private GameManager gm;
        private PlayerController player;

        private Font font;
        private GUIStyle title, labelBig, labelMid, labelSmall, orderTitle, orderBody;
        private Texture2D whiteTex;
        private Texture2D progressBgTex;

        private readonly List<ToastItem> toasts = new List<ToastItem>();
        private struct ToastItem { public string text; public float time; public bool positive; }

        private bool stylesReady;

        public void Init(GameManager manager, PlayerController p)
        {
            gm = manager;
            player = p;
            if (gm != null) gm.Toast += AddToast;
        }

        private void OnDestroy()
        {
            if (gm != null) gm.Toast -= AddToast;
        }

        private void AddToast(string text, bool positive)
        {
            toasts.Add(new ToastItem { text = text, time = Time.time, positive = positive });
            if (toasts.Count > 6) toasts.RemoveAt(0);
        }

        private void OnGUI()
        {
            EnsureAssets();
            if (gm == null) return;

            if (gm.State == GameState.Ready) DrawReady();
            else if (gm.State == GameState.Playing) DrawPlaying();
            else if (gm.State == GameState.GameOver) DrawGameOver();
        }

        // ---------------- 开始页 ----------------

        private void DrawReady()
        {
            DrawDim();
            GUI.Label(new Rect(0, Screen.height * 0.22f, Screen.width, 70), "胡闹厨房 · Kitchen Chaos", title);
            GUI.Label(new Rect(0, Screen.height * 0.22f + 80, Screen.width, 34),
                "切菜、煎烤、装盘，在时限内完成尽可能多的订单！", labelMid);

            GUILayout.BeginArea(new Rect(Screen.width * 0.5f - 220, Screen.height * 0.5f, 440, 240));
            GUILayout.Label("操作说明", labelBig);
            GUILayout.Space(6);
            GUILayout.Label("WASD / 方向键　移动", labelMid);
            GUILayout.Label("E　交互（拿取 / 放置 / 取回 / 出餐）", labelMid);
            GUILayout.Label("长按 E　在切菜台切菜", labelMid);
            GUILayout.Label("流程：食材柜拿料 → 切菜台 / 灶台加工 → 餐盘台端盘装菜 → 出餐台交付", labelSmall);
            GUILayout.Space(14);
            if (GUILayout.Button("开 始 营 业", GUILayout.Height(54)))
            {
                gm.StartGame();
            }
            GUILayout.EndArea();
        }

        // ---------------- 游戏中 ----------------

        private void DrawPlaying()
        {
            DrawTopBar();
            DrawOrderTickets();
            DrawInteractionPrompt();
            DrawToasts();
        }

        private void DrawTopBar()
        {
            // 左：分数 + 连击
            GUI.Box(new Rect(16, 14, 250, 64), GUIContent.none);
            GUI.Label(new Rect(28, 20, 230, 30), "分数：" + gm.Score.Score, labelBig);
            GUI.Label(new Rect(28, 50, 230, 22),
                "完成 " + gm.Score.CompletedOrders + " 单 · 连击 x" + gm.Score.ComboMultiplier, labelSmall);

            // 中：倒计时
            float remain = gm.Timer != null ? gm.Timer.TimeRemaining : 0f;
            int sec = Mathf.CeilToInt(remain);
            string clock = (sec / 60).ToString("00") + ":" + (sec % 60).ToString("00");
            Color old = GUI.color;
            if (sec <= 20) GUI.color = new Color(1f, 0.5f, 0.5f);
            GUI.Box(new Rect(Screen.width * 0.5f - 90, 14, 180, 48), GUIContent.none);
            GUI.Label(new Rect(Screen.width * 0.5f - 90, 20, 180, 38), clock, title);
            GUI.color = old;

            // 右：声望（心心）
            DrawReputation();
        }

        private void DrawReputation()
        {
            float w = 30f;
            float startX = Screen.width - 16 - gm.startReputation * (w + 6);
            for (int i = 0; i < gm.startReputation; i++)
            {
                Rect r = new Rect(startX + i * (w + 6), 20, w, 28);
                bool filled = i < gm.Reputation;
                GUI.color = filled ? new Color(1f, 0.35f, 0.4f) : new Color(0.4f, 0.4f, 0.4f, 0.6f);
                GUI.DrawTexture(r, whiteTex);
                GUI.color = Color.white;
            }
        }

        private void DrawOrderTickets()
        {
            const float cardW = 210f, cardH = 96f, gap = 10f, margin = 16f, top = 78f;
            List<Order> orders = gm.Orders != null ? gm.Orders.ActiveOrders : new List<Order>();

            for (int i = 0; i < orders.Count; i++)
            {
                Order o = orders[i];
                Rect r = new Rect(Screen.width - cardW - margin, top + i * (cardH + gap), cardW, cardH);
                GUI.Box(r, GUIContent.none);

                GUI.Label(new Rect(r.x + 10, r.y + 6, r.width - 20, 24), o.recipe.recipeName, orderTitle);
                GUI.Label(new Rect(r.x + 10, r.y + 30, r.width - 20, 40), o.recipe.GetRequirementText(), orderBody);

                // 倒计时进度条
                Rect barBg = new Rect(r.x + 10, r.y + r.height - 16, r.width - 20, 8);
                GUI.DrawTexture(barBg, progressBgTex);
                float ratio = o.TimeRatio;
                Color bc = ratio > 0.5f ? new Color(0.4f, 0.85f, 0.5f)
                    : ratio > 0.25f ? new Color(0.95f, 0.75f, 0.3f)
                    : new Color(0.95f, 0.35f, 0.35f);
                GUI.color = bc;
                GUI.DrawTexture(new Rect(barBg.x, barBg.y, barBg.width * ratio, barBg.height), whiteTex);
                GUI.color = Color.white;
            }
        }

        private void DrawInteractionPrompt()
        {
            if (player == null) return;

            // 交互提示
            IInteractable target = player.CurrentTarget;
            if (target != null)
            {
                string prompt = "[E] " + target.GetPrompt();
                Vector3 world = target.GetTransform().position + Vector3.up * 1.6f;
                Vector3 sp = Camera.main.WorldToScreenPoint(world);
                if (sp.z > 0)
                {
                    Rect r = new Rect(sp.x - 130, Screen.height - sp.y - 18, 260, 36);
                    GUI.Box(r, prompt, labelMid);
                }
            }

            // 加工进度条（切菜/煎烤）
            IProcessingStation processing = FindProcessingNearPlayer();
            if (processing != null && processing.Progress01 > 0f)
            {
                Rect r = new Rect(Screen.width * 0.5f - 120, Screen.height - 70, 240, 16);
                GUI.DrawTexture(r, progressBgTex);
                GUI.color = new Color(1f, 0.85f, 0.4f);
                GUI.DrawTexture(new Rect(r.x, r.y, r.width * processing.Progress01, r.height), whiteTex);
                GUI.color = Color.white;
            }

            // 手上持有物提示（左下）
            string carry = player.HasPlate
                ? "手持：盘子（" + player.Plate.Count + " 样食材）"
                : player.HasItem
                    ? "手持：" + IngredientCatalog.GetName(player.CarriedItem.Type) + Recipe.StateSuffix(player.CarriedItem.State)
                    : "手持：空";
            GUI.Label(new Rect(16, Screen.height - 34, 320, 24), carry, labelSmall);
        }

        private IProcessingStation FindProcessingNearPlayer()
        {
            if (player == null || player.CurrentTarget == null) return null;
            return player.CurrentTarget as IProcessingStation;
        }

        private void DrawToasts()
        {
            float baseY = Screen.height - 120f;
            for (int i = toasts.Count - 1; i >= 0; i--)
            {
                float age = Time.time - toasts[i].time;
                if (age > 3f) continue;
                float alpha = Mathf.Clamp01(1f - Mathf.Max(0f, age - 2f));
                GUI.color = toasts[i].positive
                    ? new Color(0.7f, 1f, 0.7f, alpha)
                    : new Color(1f, 0.7f, 0.7f, alpha);
                GUI.Label(new Rect(18, baseY - (toasts.Count - 1 - i) * 22, 520, 22), toasts[i].text, labelSmall);
            }
            GUI.color = Color.white;
        }

        // ---------------- 结算页 ----------------

        private void DrawGameOver()
        {
            DrawDim();
            GUI.Label(new Rect(0, Screen.height * 0.2f, Screen.width, 60), "营业时间结束！", title);

            GUILayout.BeginArea(new Rect(Screen.width * 0.5f - 200, Screen.height * 0.36f, 400, 220));
            GUILayout.Label("最终分数：" + gm.Score.Score, labelBig);
            GUILayout.Label("完成订单：" + gm.Score.CompletedOrders + " 单", labelMid);
            GUILayout.Label("失败订单：" + gm.Score.FailedOrders + " 单", labelMid);
            GUILayout.Label("最高连击：x" + gm.Score.BestCombo, labelMid);
            GUILayout.Space(18);
            if (GUILayout.Button("再 来 一 局 (R)", GUILayout.Height(48))) Restart();
            GUILayout.EndArea();
        }

        private void DrawDim()
        {
            GUI.color = new Color(0f, 0f, 0f, 0.72f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), whiteTex);
            GUI.color = Color.white;
        }

        private void Update()
        {
            if (gm != null && gm.State == GameState.GameOver && Input.GetKeyDown(KeyCode.R))
            {
                Restart();
            }
        }

        private void Restart()
        {
            // 重新载入当前场景，彻底重置所有运行时对象
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }

        // ---------------- 样式资源 ----------------

        private void EnsureAssets()
        {
            if (stylesReady) return;

            font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "SimHei", "PingFang SC", "Arial" }, 16);

            whiteTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Color[] px = { Color.white, Color.white, Color.white, Color.white };
            whiteTex.SetPixels(px);
            whiteTex.Apply();

            progressBgTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Color[] bg = { new Color(0, 0, 0, 0.5f), new Color(0, 0, 0, 0.5f), new Color(0, 0, 0, 0.5f), new Color(0, 0, 0, 0.5f) };
            progressBgTex.SetPixels(bg);
            progressBgTex.Apply();

            GUI.skin.box.normal.background = MakePanelTexture();

            title = NewStyle(34, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            labelBig = NewStyle(24, FontStyle.Bold, TextAnchor.MiddleLeft, Color.white);
            labelMid = NewStyle(17, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.92f, 0.95f, 1f));
            labelSmall = NewStyle(14, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.8f, 0.85f, 0.9f));
            orderTitle = NewStyle(16, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(1f, 0.95f, 0.7f));
            orderBody = NewStyle(13, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.85f, 0.9f, 1f));
            orderBody.wordWrap = true;

            stylesReady = true;
        }

        private GUIStyle NewStyle(int size, FontStyle style, TextAnchor anchor, Color color)
        {
            GUIStyle s = new GUIStyle(GUI.skin.label);
            s.font = font;
            s.fontSize = size;
            s.fontStyle = style;
            s.alignment = anchor;
            s.normal.textColor = color;
            return s;
        }

        private Texture2D MakePanelTexture()
        {
            Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            Color c = new Color(0.08f, 0.1f, 0.14f, 0.82f);
            for (int y = 0; y < 4; y++)
                for (int x = 0; x < 4; x++)
                    tex.SetPixel(x, y, c);
            tex.Apply();
            return tex;
        }
    }
}
