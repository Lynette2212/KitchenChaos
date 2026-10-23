using System.Collections.Generic;
using UnityEngine;
using KitchenChaos.Data;
using KitchenChaos.Gameplay;
using KitchenChaos.Audio;
using KitchenChaos.Visual;

namespace KitchenChaos.Core
{
    // UI uses reference pixels; only this component owns menu/pause/restart hotkeys.
    [DefaultExecutionOrder(-20)]
    public partial class GameplayHUD : MonoBehaviour
    {
        private const float Width = 1280f, Height = 720f;
        private static readonly Color Navy = new Color32(13, 32, 49, 255);
        private static readonly Color HudBackground = new Color32(13, 32, 49, 208);
        private static readonly Color Blue = new Color32(25, 52, 70, 255);
        private static readonly Color Cream = new Color32(249, 244, 231, 255);
        private static readonly Color Muted = new Color32(168, 190, 200, 255);
        private static readonly Color Orange = new Color32(255, 177, 100, 255);
        private static readonly Color Mint = new Color32(126, 214, 185, 255);
        private static readonly Color Red = new Color32(255, 130, 120, 255);
        private GameManager gm;
        private PlayerController player;
        private Font font;
        private Texture2D white;
        private GUIStyle hero, heading, body, small, micro, centered, panel, primary, secondary, upgradeCard;
        private GUIStyle popupSmall, popupBig, comboStyle;
        private readonly List<Texture2D> textures = new List<Texture2D>();
        private readonly List<ToastItem> toasts = new List<ToastItem>();
        private struct ToastItem { public string text; public float time; public bool positive; }
        private bool confirmMenu;
        private GameState observedState;
        private float displayedScore;
        private float scorePulse;
        private int shownScore;
        private int shownCombo;
        private int bannerCombo;
        private float comboBannerTime = -99f;
        private int lastReputation;
        private float repFlash;
        private int lastTickSecond = -1;
        private readonly Dictionary<Order, float> orderAppear = new Dictionary<Order, float>();
        private readonly List<Order> staleOrders = new List<Order>();

        public void Init(GameManager manager, PlayerController p)
        {
            if (gm != null) gm.Toast -= AddToast;
            gm = manager;
            player = p;
            toasts.Clear();
            confirmMenu = false;
            orderAppear.Clear();
            shownScore = 0;
            displayedScore = 0;
            shownCombo = 0;
            lastReputation = 0;
            if (gm != null) { gm.Toast += AddToast; observedState = gm.State; lastReputation = gm.Reputation; }
            CacheStations();
        }

        private void OnDestroy()
        {
            if (gm != null) gm.Toast -= AddToast;
            toasts.Clear();
            foreach (Texture2D texture in textures) if (texture != null) Destroy(texture);
            if (font != null) Destroy(font);
        }

        private void AddToast(string text, bool positive)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            toasts.Add(new ToastItem { text = text, time = Time.unscaledTime, positive = positive });
            if (toasts.Count > 3) toasts.RemoveAt(0);
        }

        private void Update()
        {
            toasts.RemoveAll(t => Time.unscaledTime - t.time > 3.5f);
            if (gm == null) return;
            if (observedState != gm.State)
            {
                confirmMenu = false;
                if (gm.State == GameState.Ready)
                {
                    toasts.Clear();
                    displayedScore = 0f; shownScore = 0; shownCombo = 0; scorePulse = 0f;
                    comboBannerTime = -99f; orderAppear.Clear(); repFlash = 0f; lastTickSecond = -1;
                }
                observedState = gm.State;
            }
            UpdateJuice();
            if (confirmMenu)
            {
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P) || Input.GetKeyDown(KeyCode.N)) confirmMenu = false;
                else if (Input.GetKeyDown(KeyCode.Y)) ReturnMenu();
                return;
            }
            bool enter = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
            if (gm.State == GameState.Ready && enter) StartJourney();
            else if ((gm.State == GameState.Playing || gm.State == GameState.Paused) &&
                     (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))) gm.TogglePause();
            else if (gm.State == GameState.DayComplete)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) SelectUpgrade(0);
                else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) SelectUpgrade(1);
                else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) SelectUpgrade(2);
            }
            else if ((gm.State == GameState.GameOver || gm.State == GameState.Victory) && Input.GetKeyDown(KeyCode.R)) StartJourney();
        }

        private void StartJourney()
        {
            if (gm.State != GameState.Ready && gm.State != GameState.GameOver && gm.State != GameState.Victory) return;
            toasts.Clear(); confirmMenu = false;
            gm.StartGame(); observedState = gm.State;
        }

        private void SelectUpgrade(int index)
        {
            if (gm.State != GameState.DayComplete || gm.UpgradeChoices == null || index >= gm.UpgradeChoices.Length) return;
            toasts.Clear();
            gm.ChooseUpgrade(index); observedState = gm.State;
        }

        private void ReturnMenu()
        {
            confirmMenu = false; toasts.Clear();
            gm.ReturnToMenu(); observedState = gm.State;
        }

        private void UpdateJuice()
        {
            if (gm == null) return;
            float dt = Time.unscaledDeltaTime;
            int target = gm.Score != null ? gm.Score.Score : 0;
            if (target > shownScore) scorePulse = 1f;
            shownScore = target;
            displayedScore = Mathf.MoveTowards(displayedScore, target, Mathf.Max(60f, Mathf.Abs(target - displayedScore) * 6f) * dt);
            if (gm.Score != null)
            {
                if (gm.Score.Combo > shownCombo && gm.Score.Combo >= 2)
                {
                    comboBannerTime = Time.unscaledTime;
                    bannerCombo = gm.Score.Combo;
                }
                shownCombo = gm.Score.Combo;
            }
            if (gm.Reputation < lastReputation) repFlash = 1f;
            lastReputation = gm.Reputation;
            scorePulse = Mathf.Max(0f, scorePulse - dt * 3.2f);
            repFlash = Mathf.Max(0f, repFlash - dt * 1.6f);
            TrackOrderReveals();
            if (gm.State == GameState.Playing && gm.Timer != null)
            {
                int second = Mathf.CeilToInt(gm.Timer.TimeRemaining);
                if (second <= 10 && second >= 1 && second != lastTickSecond)
                {
                    lastTickSecond = second;
                    if (AudioManager.Instance != null) AudioManager.Instance.Play(SfxType.Tick, 0.9f + (10 - second) * 0.04f);
                }
            }
            else lastTickSecond = -1;
        }

        private void TrackOrderReveals()
        {
            List<Order> orders = gm.Orders != null ? gm.Orders.ActiveOrders : null;
            if (orders == null) return;
            foreach (Order order in orders)
                if (order != null && !orderAppear.ContainsKey(order)) orderAppear[order] = Time.unscaledTime;
            staleOrders.Clear();
            foreach (KeyValuePair<Order, float> entry in orderAppear)
                if (!orders.Contains(entry.Key)) staleOrders.Add(entry.Key);
            foreach (Order order in staleOrders) orderAppear.Remove(order);
        }

        private void OnGUI()
        {
            if (gm == null || Screen.width <= 0 || Screen.height <= 0) return;
            EnsureAssets();
            Matrix4x4 matrix = GUI.matrix;
            Color color = GUI.color, content = GUI.contentColor, background = GUI.backgroundColor;
            bool enabled = GUI.enabled;
            int depth = GUI.depth;
            try
            {
                GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(Screen.width / Width, Screen.height / Height, 1f));
                GUI.color = GUI.contentColor = GUI.backgroundColor = Color.white;
                GUI.depth = -20;
                GameState state = gm.State;
                if (state != GameState.Ready)
                {
                    GUI.enabled = enabled && state == GameState.Playing && !confirmMenu;
                    DrawPlaying();
                }
                GUI.enabled = enabled && !confirmMenu;
                if (state == GameState.Ready) DrawReady();
                else if (state == GameState.Paused) DrawPaused();
                else if (state == GameState.DayComplete) DrawDayComplete();
                else if (state == GameState.GameOver || state == GameState.Victory) DrawEnding(state == GameState.Victory);
                GUI.enabled = enabled;
                if (confirmMenu) DrawMenuConfirmation();
            }
            finally
            {
                GUI.matrix = matrix; GUI.color = color; GUI.contentColor = content;
                GUI.backgroundColor = background; GUI.enabled = enabled; GUI.depth = depth;
            }
        }

        private void DrawReady()
        {
            Fill(new Rect(0, 0, Width, Height), Navy);
            Fill(new Rect(48, 44, 36, 4), Orange);
            Text(new Rect(96, 30, 600, 30), "KITCHEN CHAOS  /  五日营业计划", small, Muted);
            Text(new Rect(48, 79, 690, 64), "胡闹厨房 · 深蓝小馆", hero, Cream);
            Text(new Rect(48, 156, 660, 56), "从第一份香煎料理，到第五天的晚高峰。\n备菜、装盘、准时出餐，经营属于你的五日厨房。", body, Muted);
            Panel(new Rect(770, 46, 462, 164), Blue);
            Text(new Rect(794, 61, 410, 26), "本地营业纪录", small, Muted);
            Stat(794, 103, 142, "最高总分", gm.BestScore.ToString());
            Stat(936, 103, 138, "最远章节", gm.BestDay + " / " + CampaignRules.TotalDays);
            Stat(1074, 103, 140, "通关次数", gm.TotalWins.ToString());
            Text(new Rect(48, 222, 800, 30), "五天章节   /   每次收工，选择一项厨房升级", body, Cream);
            string[] notes = { "熟悉取放与火候", "组合食材，练习装盘", "从容安排备菜顺序", "多步料理，保持连击", "迎接最后的营业挑战" };
            for (int i = 0; i < CampaignRules.TotalDays; i++)
            {
                float x = 48 + i * 240;
                Panel(new Rect(x, 266, 224, 130), i == 0 ? Cream : Blue);
                Color ink = i == 0 ? Navy : Cream;
                Text(new Rect(x + 18, 278, 188, 24), "第 " + (i + 1).ToString("00") + " 天", small, i == 0 ? Blue : Orange);
                Text(new Rect(x + 18, 310, 188, 32), CampaignRules.DayName(i + 1), heading, ink);
                Text(new Rect(x + 18, 355, 194, 24), notes[i], small, i == 0 ? Blue : Muted);
            }
            Panel(new Rect(48, 428, 740, 244), Blue);
            Text(new Rect(72, 448, 660, 34), "第一人称开灶，记住这几件事", heading, Cream);
            Text(new Rect(72, 493, 690, 155), "鼠标看向 · WASD 移动 · Shift / Space 短冲刺\n对准柜台：E 拿放、装盘、出餐；长按 E 切菜\nX 取回未切食材 · Esc / P 暂停并释放鼠标\n暂存台放盘继续备菜；蘑菇必须先切再煎\n按订单要求装盘，留意火候，保持连击。", body, Muted);
            Panel(new Rect(812, 428, 420, 244), Cream);
            Text(new Rect(836, 450, 372, 36), "五天，一间好餐厅。", heading, Navy);
            Text(new Rect(836, 495, 372, 64), "完成当天交单目标即可收工。\n挑选升级，再迎接明天的客人。", body, Blue);
            if (GUI.Button(new Rect(836, 577, 372, 54), "开始营业    Enter", primary)) StartJourney();
            Text(new Rect(836, 640, 372, 22), "本地自动记录 · 无需联网", micro, Blue);
            Text(new Rect(48, 682, 950, 24), "BGM: Morning Brew / Cozy Music Sampler — Spaceyyy · 已调整音量与边缘淡化 · 食材与音效采样: Kenney (CC0)", micro, Muted);
            if (GUI.Button(new Rect(1012, 680, 104, 28), "音乐来源", secondary))
                Application.OpenURL("https://opengameart.org/content/cozy-music-sampler-%E2%80%94-7-seamless-loops-lofi-farm-fantasy-tavern-halloween-winter-caf%C3%A9-space");
            if (GUI.Button(new Rect(1124, 680, 108, 28), "CC BY 4.0", secondary))
                Application.OpenURL("https://creativecommons.org/licenses/by/4.0/");
        }

        private void DrawPaused()
        {
            Dim(); Panel(new Rect(386, 108, 508, 504), Navy);
            Text(new Rect(418, 132, 444, 54), "营业已暂停", hero, Cream);
            Text(new Rect(418, 196, 444, 28), "订单、灶火与计时已暂停 · 鼠标已释放", body, Muted);
            Text(new Rect(418, 237, 444, 70), "当前升级\n" + gm.UpgradeSummary, small, Muted);
            AudioManager audio = AudioManager.Instance;
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && audio != null;
            float musicLevel = audio != null ? Mathf.Clamp01(audio.musicVolume) : 0f;
            float sfxLevel = audio != null ? Mathf.Clamp01(audio.volume) : 0f;
            Text(new Rect(418, 323, 104, 28), "音乐", body, Cream);
            musicLevel = GUI.HorizontalSlider(new Rect(528, 331, 250, 24), musicLevel, 0f, 1f);
            Text(new Rect(798, 323, 64, 28), Mathf.RoundToInt(musicLevel * 100f) + "%", small, Muted);
            Text(new Rect(418, 381, 104, 28), "音效", body, Cream);
            sfxLevel = GUI.HorizontalSlider(new Rect(528, 389, 250, 24), sfxLevel, 0f, 1f);
            Text(new Rect(798, 381, 64, 28), Mathf.RoundToInt(sfxLevel * 100f) + "%", small, Muted);
            if (audio != null) { audio.musicVolume = musicLevel; audio.volume = sfxLevel; }
            GUI.enabled = enabled;
            if (GUI.Button(new Rect(418, 463, 444, 54), "继续营业    Esc / P", primary)) gm.TogglePause();
            if (GUI.Button(new Rect(418, 535, 444, 46), "回到主菜单", secondary)) confirmMenu = true;
        }

        private void DrawDayComplete()
        {
            Dim(); Panel(new Rect(142, 83, 996, 554), Navy);
            Text(new Rect(174, 105, 650, 50), "第 " + gm.Day + " 天 · 顺利收工", hero, Cream);
            Text(new Rect(174, 158, 620, 28), CampaignRules.DayName(gm.Day) + "  /  今日评级 " + gm.DayStars + " / 3 星", body, Muted);
            for (int i = 0; i < 3; i++)
            {
                Panel(new Rect(884 + i * 66, 117, 56, 56), i < gm.DayStars ? Orange : Blue);
                Text(new Rect(884 + i * 66, 117, 56, 56), i < gm.DayStars ? "得星" : "未得", centered, i < gm.DayStars ? Navy : Muted);
            }
            Stat(174, 211, 260, "本日完成", gm.DayCompletedOrders + " 单");
            Stat(484, 211, 260, "本日失误", gm.DayFailedOrders + " 单");
            Stat(794, 211, 300, "旅程总分", (gm.Score != null ? gm.Score.Score : 0).ToString());
            Text(new Rect(174, 282, 920, 30), "三选一 · 升级后进入第 " + Mathf.Min(gm.Day + 1, CampaignRules.TotalDays) + " 天   /   累计 " + gm.TotalStars + " 星", body, Cream);
            var choices = gm.UpgradeChoices;
            if (choices != null) for (int i = 0; i < Mathf.Min(3, choices.Length); i++)
            {
                float x = 174 + i * 318;
                bool chosen = GUI.Button(new Rect(x, 328, 296, 242), GUIContent.none, upgradeCard);
                Text(new Rect(x + 22, 345, 252, 24), "厨房升级  /  0" + (i + 1), small, Blue);
                Text(new Rect(x + 22, 380, 252, 42), CampaignRules.UpgradeName(choices[i]), heading, Navy);
                Text(new Rect(x + 22, 431, 252, 86), CampaignRules.UpgradeDescription(choices[i]), body, Blue);
                Text(new Rect(x + 22, 530, 252, 25), "选择这一项    [ " + (i + 1) + " ]", small, Navy);
                if (chosen) { SelectUpgrade(i); return; }
            }
            Text(new Rect(174, 584, 716, 30), "达标 1 星 · 零失误 +1 星 · 剩余时间不少于 25% 再 +1 星", small, Muted);
            if (GUI.Button(new Rect(954, 582, 152, 34), "回到主菜单", secondary)) confirmMenu = true;
        }

        private void DrawEnding(bool victory)
        {
            Dim(); Panel(new Rect(252, 84, 776, 552), Navy);
            Text(new Rect(292, 113, 696, 56), victory ? "五日圆满 · 主厨诞生" : "这次旅程，先到这里", hero, victory ? Orange : Cream);
            Text(new Rect(292, 185, 696, 75), string.IsNullOrWhiteSpace(gm.EndReason) ? "本次营业已结束。" : gm.EndReason, body, Muted);
            var score = gm.Score;
            Stat(292, 283, 232, "最终总分", (score != null ? score.Score : 0).ToString());
            Stat(524, 283, 232, "完成订单", (score != null ? score.CompletedOrders : 0) + " 单");
            Stat(756, 283, 232, "失误订单", (score != null ? score.FailedOrders : 0) + " 单");
            Stat(292, 391, 232, "到达章节", gm.Day + " / " + CampaignRules.TotalDays);
            Stat(524, 391, 232, "最高连击", (score != null ? score.BestCombo : 0) + " 连");
            Stat(756, 391, 232, "旅程星级", gm.TotalStars + " / " + CampaignRules.TotalDays * 3);
            if (GUI.Button(new Rect(292, 520, 336, 54), "再开一程    R", primary)) StartJourney();
            if (GUI.Button(new Rect(648, 520, 340, 54), "回到主菜单", secondary)) confirmMenu = true;
            Text(new Rect(292, 589, 696, 24), "本地纪录   " + gm.BestScore + " 分   /   到达第 " + gm.BestDay + " 天   /   通关 " + gm.TotalWins + " 次", small, Muted);
        }

        private void DrawMenuConfirmation()
        {
            Dim(); Panel(new Rect(390, 222, 500, 276), Cream);
            Text(new Rect(422, 242, 436, 46), "离开本次旅程？", heading, Navy);
            Text(new Rect(422, 300, 436, 68), "返回主菜单会清空当前厨房，\n本次旅程无法继续。已保存的本地纪录保留。", body, Blue);
            if (GUI.Button(new Rect(422, 390, 436, 42), "保留现场    Esc / N", secondary)) confirmMenu = false;
            if (GUI.Button(new Rect(422, 444, 436, 34), "确认离开    Y", primary)) ReturnMenu();
        }

        private void Stat(float x, float y, float width, string caption, string value)
        {
            Text(new Rect(x, y, width, 24), caption, small, Muted);
            Text(new Rect(x, y + 29, width, 36), value, heading, Cream);
        }
        private void Dim() { Fill(new Rect(0, 0, Width, Height), new Color(0.025f, 0.07f, 0.11f, 0.84f)); }
        private void Fill(Rect rect, Color color)
        {
            Color old = GUI.color; GUI.color = color; GUI.DrawTexture(rect, white); GUI.color = old;
        }
        private void Panel(Rect rect, Color color)
        {
            Color old = GUI.color; GUI.color = color; GUI.Box(rect, GUIContent.none, panel); GUI.color = old;
        }
        private void Text(Rect rect, string text, GUIStyle style, Color color)
        {
            Color old = GUI.contentColor; GUI.contentColor = color; GUI.Label(rect, text, style); GUI.contentColor = old;
        }
        private void Bar(Rect rect, float value, Color color)
        {
            Fill(rect, Blue); Fill(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(value), rect.height), color);
        }
        private GUIStyle Type(int size, bool bold = false, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var style = new GUIStyle { font = font, fontSize = size, fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
                alignment = alignment, wordWrap = true, richText = false, clipping = TextClipping.Clip };
            style.normal.textColor = Color.white;
            return style;
        }
        private GUIStyle ButtonStyle(Color normal, Color hover, Color active, Color ink)
        {
            var style = Type(17, true, TextAnchor.MiddleCenter);
            style.border = new RectOffset(8, 8, 8, 8); style.padding = new RectOffset(12, 12, 4, 4);
            style.normal.background = MakeTexture(normal); style.normal.textColor = ink;
            style.hover.background = MakeTexture(hover); style.hover.textColor = ink;
            style.active.background = MakeTexture(active); style.active.textColor = ink;
            style.focused.background = style.hover.background; style.focused.textColor = ink;
            return style;
        }
        private void EnsureAssets()
        {
            if (white != null) return;
            font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Noto Sans CJK SC", "PingFang SC" }, 18);
            if (font != null) font.hideFlags = HideFlags.HideAndDontSave;
            white = MakeTexture(Color.white, false);
            panel = new GUIStyle { border = new RectOffset(8, 8, 8, 8) };
            panel.normal.background = MakeTexture(Color.white);
            hero = Type(36, true); heading = Type(24, true); body = Type(16); small = Type(14); micro = Type(12);
            centered = Type(14, true, TextAnchor.MiddleCenter);
            popupSmall = Type(16, true, TextAnchor.MiddleCenter);
            popupBig = Type(28, true, TextAnchor.MiddleCenter);
            comboStyle = Type(34, true, TextAnchor.MiddleCenter);
            primary = ButtonStyle(Orange, new Color32(255, 202, 140, 255), new Color32(232, 149, 72, 255), Navy);
            secondary = ButtonStyle(Blue, new Color32(44, 78, 99, 255), new Color32(19, 42, 60, 255), Cream);
            upgradeCard = ButtonStyle(Cream, Color.white, new Color32(255, 223, 183, 255), Navy);
        }
        private Texture2D MakeTexture(Color color, bool rounded = true)
        {
            const int size = 24;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(Mathf.Abs(x + 0.5f - size / 2f) - 4f, 0f);
                float dy = Mathf.Max(Mathf.Abs(y + 0.5f - size / 2f) - 4f, 0f);
                Color pixel = color; if (rounded) pixel.a *= Mathf.Clamp01(8f - Mathf.Sqrt(dx * dx + dy * dy));
                pixels[y * size + x] = pixel;
            }
            texture.SetPixels(pixels); texture.Apply(false, true); textures.Add(texture); return texture;
        }
    }
}
