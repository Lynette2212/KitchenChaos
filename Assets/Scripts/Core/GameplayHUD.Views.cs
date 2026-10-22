using System.Collections.Generic;
using UnityEngine;
using KitchenChaos.Data;
using KitchenChaos.Gameplay;
using KitchenChaos.Visual;

namespace KitchenChaos.Core
{
    public partial class GameplayHUD
    {
        private sealed class StationBadge
        {
            public MonoBehaviour owner;
            public Transform anchor;
            public IInteractable target;
            public string title;
            public CuttingCounter cutter;
            public StoveCounter stove;
        }
        private readonly List<StationBadge> stations = new List<StationBadge>();
        private readonly List<Rect> badgeRects = new List<Rect>();
        private Camera kitchenCamera;
        private Vector2 heldScroll;

        // Init runs after KitchenBuilder has finished creating the real counters.
        // Never search the scene or look up Camera.main from OnGUI/Update.
        private void CacheStations()
        {
            stations.Clear();
            kitchenCamera = Camera.main;
            foreach (MonoBehaviour component in FindObjectsByType<MonoBehaviour>())
            {
                if (component.gameObject.scene != gameObject.scene || !(component is IInteractable target)) continue;
                string name;
                if (component is IngredientContainer container) name = IngredientCatalog.GetName(container.IngredientType) + "柜";
                else if (component is CuttingCounter) name = "切菜台";
                else if (component is StoveCounter) name = "灶台";
                else if (component is PlateCounter) name = "餐盘台";
                else if (component is DeliveryCounter) name = "出餐台";
                else if (component is TrashBin) name = "垃圾桶";
                else if (component is PrepCounter) name = "暂存台";
                else name = "工作台";
                stations.Add(new StationBadge { owner = component, anchor = target.GetTransform(), target = target,
                    title = name, cutter = component as CuttingCounter, stove = component as StoveCounter });
            }
            stations.Sort((a, b) => a.anchor.position.x.CompareTo(b.anchor.position.x));
        }

        private void DrawPlaying()
        {
            Fill(new Rect(KitchenRight, 0, Width - KitchenRight, Height), Navy);
            Fill(new Rect(KitchenRight, 0, 1, Height), Blue);
            DrawWorldBadges();
            DrawTopBar();
            DrawOrders();
            DrawComboBanner();
            DrawFxPopups();
            DrawHandsAndControls();
            if (repFlash > 0f) Fill(new Rect(0, 0, KitchenRight, Height), new Color(1f, 0.18f, 0.12f, repFlash * 0.16f));
            DrawToasts();
        }

        private void DrawTopBar()
        {
            Panel(new Rect(16, 16, 944, 80), Navy);
            Text(new Rect(34, 24, 198, 22), "第 " + gm.Day.ToString("00") + " 天 / " + CampaignRules.TotalDays + " 天", small, Orange);
            Text(new Rect(34, 49, 198, 34), CampaignRules.DayName(gm.Day), heading, Cream);
            Text(new Rect(246, 24, 184, 25), "今日交单  " + gm.DayCompletedOrders + " / " + gm.DayTarget, body, Cream);
            Bar(new Rect(246, 64, 174, 6), gm.DayCompletedOrders / (float)Mathf.Max(1, gm.DayTarget), Mint);
            Color scoreInk = Color.Lerp(Cream, Orange, Mathf.Clamp01(scorePulse));
            TextPulse(new Rect(446, 23, 200, 32), Mathf.RoundToInt(displayedScore) + " 分", heading, scoreInk, 1f + scorePulse * 0.12f);
            Text(new Rect(446, 60, 208, 24), gm.Score != null ? gm.Score.Combo + " 连击  ·  " + gm.Score.ComboMultiplier + " 倍得分" : "等待第一份订单", small, Orange);
            float seconds = gm.Timer != null ? Mathf.Max(0f, gm.Timer.TimeRemaining) : 0f;
            Text(new Rect(669, 24, 112, 22), "剩余时间", micro, Muted);
            float timerScale = seconds <= 10f ? 1f + Mathf.Sin(Time.unscaledTime * 9f) * 0.06f : 1f;
            TextPulse(new Rect(669, 47, 118, 35), Clock(seconds), heading, seconds <= 20 ? Red : Cream, timerScale);
            Bar(new Rect(669, 84, 104, 3), gm.Timer != null ? seconds / Mathf.Max(1f, gm.Timer.RoundDuration) : 0f, seconds <= 20 ? Red : Orange);
            Text(new Rect(808, 24, 132, 25), "声望  " + gm.Reputation + " / " + gm.MaxReputation, body, Cream);
            int segments = Mathf.Max(1, gm.MaxReputation);
            float unit = 132f / segments;
            for (int i = 0; i < segments; i++)
                Fill(new Rect(808 + i * unit, 63, Mathf.Max(1, unit - 4), 9), i < gm.Reputation ? (gm.Reputation <= 1 ? Red : Orange) : Blue);
        }

        private void DrawOrders()
        {
            Text(new Rect(996, 22, 182, 38), "出餐清单", heading, Cream);
            var orders = gm.Orders != null ? gm.Orders.ActiveOrders : null;
            int count = orders != null ? Mathf.Min(4, orders.Count) : 0;
            Text(new Rect(1190, 27, 66, 28), count + " / 4", centered, Orange);
            if (count == 0)
            {
                Panel(new Rect(992, 90, 272, 150), Blue);
                Text(new Rect(1012, 114, 232, 34), "准备迎接下一位客人", body, Cream);
                Text(new Rect(1012, 159, 232, 58), "还没有待办订单。\n整理台面，腾出双手。", small, Muted);
            }
            float y = 82f;
            for (int i = 0; i < count; i++)
            {
                Order order = orders[i];
                if (order == null || order.recipe == null) continue;
                float appear;
                if (!orderAppear.TryGetValue(order, out appear)) appear = Time.unscaledTime;
                float reveal = Mathf.Clamp01((Time.unscaledTime - appear) / 0.22f);
                float ease = 1f - (1f - reveal) * (1f - reveal) * (1f - reveal);
                float offsetX = (1f - ease) * 26f;
                if (order.TimeRatio <= 0.25f) offsetX += Mathf.Sin(Time.unscaledTime * 42f + i * 1.7f) * 2.2f;
                var ingredients = order.recipe.ingredients;
                int rows = ingredients != null ? ingredients.Count : 0;
                float height = Mathf.Max(108f, 54f + rows * 18f);
                Rect rect = new Rect(992 + offsetX, y, 272, height);
                Color urgency = order.TimeRatio > 0.5f ? Mint : order.TimeRatio > 0.25f ? Orange : Red;
                Panel(rect, Cream);
                Fill(new Rect(rect.x + 12, rect.y + 12, 3, 18), urgency);
                Text(new Rect(rect.x + 23, rect.y + 6, 172, 26), order.recipe.recipeName, body, Navy);
                Text(new Rect(rect.x + 200, rect.y + 6, 62, 26), Mathf.CeilToInt(Mathf.Max(0, order.timeRemaining)) + "秒", centered, order.TimeRatio <= 0.25f ? new Color32(164, 47, 38, 255) : Blue);
                for (int j = 0; j < rows; j++)
                {
                    RecipeIngredient item = ingredients[j];
                    if (item == null) continue;
                    float rowY = rect.y + 34 + j * 18;
                    Fill(new Rect(rect.x + 14, rowY + 7, 4, 4), Blue);
                    Text(new Rect(rect.x + 25, rowY, 144, 18), IngredientCatalog.GetName(item.type), small, Navy);
                    string state = item.type == IngredientType.Mushroom && item.requiredState == IngredientState.Cooked ? "先切后煎" : StateName(item.requiredState);
                    Text(new Rect(rect.x + 177, rowY, 80, 18), state, micro, Blue);
                }
                Bar(new Rect(rect.x + 14, rect.yMax - 10, rect.width - 28, 4), order.TimeRatio, urgency);
                y += height + 8;
            }
            Text(new Rect(996, 693, 268, 19), "食材与加工状态须完全匹配 · 出餐台交付", micro, Muted);
        }

        private void DrawHandsAndControls()
        {
            Panel(new Rect(16, 552, 304, 152), Navy);
            Panel(new Rect(332, 552, 628, 94), Navy);
            Panel(new Rect(332, 654, 628, 50), Navy);
            if (player == null) return;
            string held = player.HasPlate ? "手持餐盘 · " + player.Plate.Count + " 份" : player.HasItem ? "手持食材" : "双手空空";
            Text(new Rect(32, 562, 270, 24), held, body, Orange);
            if (player.HasPlate && player.Plate.Count > 0)
            {
                var contents = player.Plate.Contents;
                Rect viewport = new Rect(32, 590, 272, 96);
                // Hidden built-in scrollbars: no default GUI.skin assets; wheel still scrolls overflow.
                heldScroll = GUI.BeginScrollView(viewport, heldScroll, new Rect(0, 0, 262, contents.Count * 19), false, false, GUIStyle.none, GUIStyle.none);
                try
                {
                    for (int i = 0; i < contents.Count; i++)
                    {
                        Ingredient item = contents[i];
                        if (item == null) continue;
                        DrawHeldRow(new Rect(0, i * 19, 262, 19), item.type, item.state);
                    }
                }
                finally { GUI.EndScrollView(); }
                if (contents.Count > 5) Text(new Rect(222, 567, 82, 18), "滚轮查看", micro, Muted);
            }
            else if (player.HasItem)
            {
                DrawHeldRow(new Rect(32, 594, 262, 24), player.CarriedItem.Type, player.CarriedItem.State);
                Text(new Rect(32, 632, 272, 48), "加工后再装盘；\n也可以先放在暂存台。", small, Muted);
            }
            else Text(new Rect(32, 596, 272, 72), player.HasPlate ? "空盘准备就绪。\n加工食材先切 / 煎，再装盘。" : "靠近食材柜，按 E 拿料。\n需要装盘时，到餐盘台取盘。", small, Muted);
            IInteractable target = player.CurrentTarget;
            Text(new Rect(350, 559, 590, 23), target != null ? "E  /  当前交互" : "E  /  靠近柜台开始操作", small, Orange);
            Text(new Rect(350, 584, 592, 35), target != null ? target.GetPrompt() : "先备料，再装盘；查看右侧订单的每一项加工要求。", body, Cream);
            Text(new Rect(350, 621, 596, 19), "长按 E 切菜 · X 取回未切食材 · 暂存台放盘继续备菜 · 蘑菇先切再煎", micro, Muted);
            float dash = Mathf.Clamp01(player.DashReady01);
            Text(new Rect(350, 665, 132, 26), "Shift / Space", small, Cream);
            Text(new Rect(491, 659, 218, 26), player.IsDashing ? "冲刺中" : dash >= 0.999f ? "冲刺就绪" : "冲刺冷却  " + Mathf.RoundToInt(dash * 100) + "%", small, dash >= 0.999f ? Mint : Muted);
            Bar(new Rect(491, 688, 202, 4), dash, Mint);
            if (GUI.Button(new Rect(774, 662, 172, 34), "暂停   Esc / P", secondary)) gm.TogglePause();
        }

        private void DrawHeldRow(Rect rect, IngredientType type, IngredientState state)
        {
            Fill(new Rect(rect.x, rect.y + 7, 4, 4), state == IngredientState.Burned ? Red : Mint);
            Text(new Rect(rect.x + 13, rect.y, 143, rect.height), IngredientCatalog.GetName(type), small, Cream);
            Text(new Rect(rect.x + 159, rect.y, 98, rect.height), StateName(state), small, state == IngredientState.Burned ? Red : Muted);
        }
        private static string StateName(IngredientState state)
        {
            switch (state)
            {
                case IngredientState.Cut: return "已切好";
                case IngredientState.Cooked: return "已煎熟";
                case IngredientState.Burned: return "已焦糊";
                default: return "原料";
            }
        }
        private static string Clock(float seconds)
        {
            int value = Mathf.CeilToInt(Mathf.Max(0, seconds));
            return (value / 60).ToString("00") + ":" + (value % 60).ToString("00");
        }

        private void DrawWorldBadges()
        {
            if (kitchenCamera == null) return;
            badgeRects.Clear();
            foreach (StationBadge station in stations)
            {
                if (station.owner == null || station.anchor == null || !station.owner.isActiveAndEnabled) continue;
                Vector3 screen = kitchenCamera.WorldToScreenPoint(station.anchor.position + Vector3.up * 0.95f);
                if (screen.z <= 0 || !kitchenCamera.pixelRect.Contains(new Vector2(screen.x, screen.y))) continue;
                // WorldToScreenPoint is in physical pixels (and already includes Camera.rect).
                // Invert the active UI matrix rather than scaling/offsetting the camera viewport twice.
                Vector3 point = GUI.matrix.inverse.MultiplyPoint3x4(new Vector3(screen.x, Screen.height - screen.y, 0));
                if (point.x < 16 || point.x > KitchenRight - 16 || point.y < 102 || point.y > 548) continue;
                Ingredient food = station.cutter != null ? station.cutter.PlacedIngredient : station.stove != null ? station.stove.PlacedIngredient : null;
                bool selected = player != null && ReferenceEquals(player.CurrentTarget, station.target);
                float width = food != null ? 150 : Mathf.Clamp(station.title.Length * 13 + 20, 68, 116);
                float height = food != null ? 61 : 26;
                Rect rect = PlaceBadge(new Rect(Mathf.Clamp(point.x - width / 2, 18, KitchenRight - width - 18), Mathf.Clamp(point.y - height - 6, 104, 542 - height), width, height));
                Color accent = selected ? Orange : Muted;
                string status = "";
                float progress = 0;
                if (food != null)
                {
                    if (station.stove != null)
                    {
                        var stove = station.stove;
                        progress = stove.IsCooked ? stove.BurnRemaining01 : stove.Progress01;
                        accent = stove.IsBurned ? Red : stove.IsCooked ? (stove.BurnRemaining01 < 0.35f ? Red : Orange) : Mint;
                        status = stove.IsBurned ? "已焦糊 · 取回丢弃" : stove.IsCooked ? "已熟快取！防糊 " + Mathf.CeilToInt(stove.BurnRemaining01 * 100) + "%" : "煎制中  " + Mathf.RoundToInt(progress * 100) + "%";
                    }
                    else
                    {
                        bool cut = food.state == IngredientState.Cut;
                        progress = cut ? 1f : station.cutter.Progress01;
                        accent = cut ? Mint : Orange;
                        status = cut ? "已切好 · E 取走" : "长按 E 切菜  " + Mathf.RoundToInt(progress * 100) + "%";
                    }
                }
                Panel(rect, Navy);
                Fill(new Rect(rect.x + 7, rect.y + 7, 3, rect.height - 14), accent);
                Text(new Rect(rect.x + 12, rect.y + 2, rect.width - 18, 23), station.title + (food != null ? " · " + IngredientCatalog.GetName(food.type) : ""), micro, selected ? Orange : Cream);
                if (food == null) continue;
                Text(new Rect(rect.x + 12, rect.y + 24, rect.width - 18, 22), status, micro, accent);
                Bar(new Rect(rect.x + 12, rect.yMax - 10, rect.width - 22, 4), progress, accent);
            }
        }

        private Rect PlaceBadge(Rect original)
        {
            Rect result = original;
            for (int attempt = 0; attempt < 9; attempt++)
            {
                int step = (attempt + 1) / 2;
                result.y = original.y + (attempt % 2 == 1 ? -step : step) * (original.height + 4);
                if (result.y < 104 || result.yMax > 542) continue;
                bool overlaps = false;
                foreach (Rect other in badgeRects) if (new Rect(other.x - 3, other.y - 3, other.width + 6, other.height + 6).Overlaps(result)) { overlaps = true; break; }
                if (!overlaps) { badgeRects.Add(result); return result; }
            }
            badgeRects.Add(original);
            return original;
        }

        private void DrawToasts()
        {
            // 只显示最新一条：连续出餐时旧提示会叠在柜台上挡住视线。
            if (toasts.Count == 0) return;
            ToastItem item = toasts[toasts.Count - 1];
            float age = Time.unscaledTime - item.time;
            float alpha = Mathf.Clamp01(3.5f - age);
            if (alpha <= 0) return;
            Rect rect = new Rect(182, 495, 596, 43);
            Color background = Blue; background.a = alpha * 0.98f;
            Color ink = item.positive ? Mint : Red; ink.a = alpha;
            Panel(rect, background);
            Fill(new Rect(rect.x + 12, rect.y + 11, 3, 21), ink);
            Text(new Rect(rect.x + 26, rect.y + 3, rect.width - 42, 37), item.text, small, ink);
        }

        private void DrawComboBanner()
        {
            float age = Time.unscaledTime - comboBannerTime;
            if (age < 0f || age > 1.25f) return;
            float p = age / 1.25f;
            float alpha = p > 0.65f ? 1f - (p - 0.65f) / 0.35f : 1f;
            float scale = Mathf.Lerp(1.55f, 1f, Mathf.Clamp01(p * 7f));
            Rect rect = new Rect(368, 112, 240, 54);
            Matrix4x4 matrix = GUI.matrix;
            GUIUtility.ScaleAroundPivot(new Vector2(scale, scale), rect.center);
            Color plate = Blue; plate.a = Mathf.Min(1f, alpha * 2f);
            Panel(rect, plate);
            Fill(new Rect(rect.x + 70, rect.yMax - 4, rect.width - 140, 3), new Color(1f, 0.78f, 0.3f, alpha));
            Text(new Rect(rect.x + 2, rect.y + 3, rect.width, rect.height), "×" + bannerCombo + " 连击", comboStyle, new Color(0f, 0f, 0f, alpha * 0.5f));
            Text(rect, "×" + bannerCombo + " 连击", comboStyle, new Color(1f, 0.78f, 0.3f, alpha));
            GUI.matrix = matrix;
        }

        private void DrawFxPopups()
        {
            if (kitchenCamera == null) return;
            FeedbackFX fx = FeedbackFX.Instance;
            if (fx == null) return;
            IReadOnlyList<FeedbackFX.PopupItem> popups = fx.Popups;
            for (int i = 0; i < popups.Count; i++)
            {
                FeedbackFX.PopupItem popup = popups[i];
                Vector3 screen = kitchenCamera.WorldToScreenPoint(popup.worldPos);
                if (screen.z <= 0f) continue;
                Vector3 point = GUI.matrix.inverse.MultiplyPoint3x4(new Vector3(screen.x, Screen.height - screen.y, 0f));
                if (point.x < 20f || point.x > KitchenRight - 20f) continue;
                float t = Mathf.Clamp01(popup.age / popup.life);
                float alpha = 1f - Mathf.Clamp01((t - 0.55f) / 0.45f);
                if (alpha <= 0f) continue;
                // 抬到柜台状态牌上方，避免两行文字叠在一起
                float rise = 132f + t * 40f;
                float scale = Mathf.Lerp(1.45f, 1f, Mathf.Clamp01(t * 5f));
                GUIStyle style = popup.big ? popupBig : popupSmall;
                float centerX = Mathf.Clamp(point.x, 160f, KitchenRight - 160f);
                // 出餐台靠近货架，限制上浮高度，避免飘字压住货架名称。
                float centerY = Mathf.Clamp(point.y - rise, 188f, 516f);
                Rect rect = new Rect(centerX - 140f, centerY - 26f, 280f, 46f);
                Matrix4x4 matrix = GUI.matrix;
                GUIUtility.ScaleAroundPivot(new Vector2(scale, scale), new Vector2(centerX, centerY));
                Text(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), popup.text, style, new Color(0f, 0f, 0f, alpha * 0.55f));
                Text(rect, popup.text, style, new Color(popup.color.r, popup.color.g, popup.color.b, alpha));
                GUI.matrix = matrix;
            }
        }

        private void TextPulse(Rect rect, string text, GUIStyle style, Color color, float scale)
        {
            if (Mathf.Abs(scale - 1f) < 0.001f) { Text(rect, text, style, color); return; }
            Matrix4x4 matrix = GUI.matrix;
            GUIUtility.ScaleAroundPivot(new Vector2(scale, scale), new Vector2(rect.x, rect.center.y));
            Text(rect, text, style, color);
            GUI.matrix = matrix;
        }
    }
}
