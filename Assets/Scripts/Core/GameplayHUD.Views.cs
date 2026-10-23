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
        private Camera kitchenCamera;

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
            DrawFxPopups();
            DrawWorldBadges();
            DrawTopBar();
            DrawOrders();
            DrawStoveWarning();
            DrawComboBanner();
            DrawHandsAndControls();
            DrawToasts();
            if (gm.State == GameState.Playing) DrawCrosshair();
            if (repFlash > 0f)
            {
                Color flash = new Color(1f, 0.18f, 0.12f, repFlash * 0.65f);
                Fill(new Rect(0, 0, Width, 3), flash);
                Fill(new Rect(0, Height - 3, Width, 3), flash);
                Fill(new Rect(0, 3, 3, Height - 6), flash);
                Fill(new Rect(Width - 3, 3, 3, Height - 6), flash);
            }
        }

        private void DrawTopBar()
        {
            Panel(new Rect(16, 16, 944, 80), HudBackground);
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
                Panel(new Rect(992, 82, 272, 76), HudBackground);
                Text(new Rect(1012, 92, 232, 26), "准备迎接下一位客人", body, Cream);
                Text(new Rect(1012, 123, 232, 22), "整理台面，腾出双手。", small, Muted);
            }
            float y = 82f;
            Color card = Cream; card.a = 0.94f;
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
                Panel(rect, card);
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
            if (player == null) return;
            int rows = player.HasPlate ? Mathf.Max(1, player.Plate.Count) : 1;
            float height = 46f + rows * 19f;
            float top = 704f - height;
            Panel(new Rect(16, top, 304, height), HudBackground);
            string held = player.HasPlate ? "手持餐盘 · " + player.Plate.Count + " / 5 份" : player.HasItem ? "手持食材" : "双手空空";
            Text(new Rect(32, top + 8, 270, 24), held, body, Orange);
            if (player.HasPlate && player.Plate.Count > 0)
            {
                var contents = player.Plate.Contents;
                for (int i = 0; i < contents.Count; i++)
                {
                    Ingredient item = contents[i];
                    if (item == null) continue;
                    DrawHeldRow(new Rect(32, top + 36 + i * 19, 262, 19), item.type, item.state);
                }
            }
            else if (player.HasItem)
                DrawHeldRow(new Rect(32, top + 36, 262, 19), player.CarriedItem.Type, player.CarriedItem.State);
            else Text(new Rect(32, top + 36, 272, 19), player.HasPlate ? "看向熟食，E 装盘。" : "看向食材柜拿料，或到餐盘台取盘。", small, Muted);

            Panel(new Rect(336, 620, 624, 84), HudBackground);
            IInteractable target = player.CurrentTarget;
            Text(new Rect(350, 628, 596, 30), target != null ? target.GetPrompt() : "靠近并看向柜台，准星变橙后按 E 操作。", body, target != null ? Cream : Muted);
            Text(new Rect(350, 661, 596, 20), "鼠标看向 · WASD 移动 · 长按 E 切菜 · X 取回", small, Muted);
            float dash = Mathf.Clamp01(player.DashReady01);
            string dashState = player.IsDashing ? "冲刺中" : dash >= 0.999f ? "就绪" : "冷却 " + Mathf.RoundToInt(dash * 100) + "%";
            Text(new Rect(350, 682, 342, 18), "Shift / Space 短冲刺 · " + dashState, micro, dash >= 0.999f ? Mint : Muted);
            Text(new Rect(722, 682, 224, 18), "Esc / P 暂停 · 释放鼠标", small, Cream);
            Bar(new Rect(350, 700, 160, 2), dash, Mint);
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
            if (player == null || player.CurrentTarget == null || gm.State != GameState.Playing) return;
            foreach (StationBadge station in stations)
            {
                if (!ReferenceEquals(player.CurrentTarget, station.target)) continue;
                if (station.owner == null || station.anchor == null || !station.owner.isActiveAndEnabled) return;
                if (!TryWorldToHUDPoint(station.anchor.position + Vector3.up * 0.95f, out _)) return;
                Ingredient food = station.cutter != null ? station.cutter.PlacedIngredient : station.stove != null ? station.stove.PlacedIngredient : null;
                float width = food != null ? 224f : Mathf.Clamp(station.title.Length * 14 + 24, 76, 144);
                float height = food != null ? 58f : 26f;
                Rect rect = new Rect((Width - width) / 2f, Height / 2f - 24f - height, width, height);
                Color accent = Orange;
                string status = "";
                float progress = 0f;
                if (food != null)
                {
                    if (station.stove != null)
                    {
                        var stove = station.stove;
                        progress = stove.IsCooked ? stove.BurnRemaining01 : stove.Progress01;
                        accent = stove.IsBurned ? Red : stove.IsCooked ? (stove.BurnRemaining01 < 0.35f ? Red : Orange) : Mint;
                        status = stove.IsBurned ? "已焦糊 · 取回丢弃" : stove.IsCooked ? "已熟 · 距焦糊 " + stove.SecondsUntilBurn.ToString("0.0") + " 秒" : "煎制中  " + Mathf.RoundToInt(progress * 100) + "%";
                    }
                    else
                    {
                        bool cut = food.state == IngredientState.Cut;
                        progress = cut ? 1f : station.cutter.Progress01;
                        accent = cut ? Mint : Orange;
                        status = cut ? "已切好 · E 取走" : "长按 E 切菜  " + Mathf.RoundToInt(progress * 100) + "%";
                    }
                }
                Panel(rect, HudBackground);
                Text(new Rect(rect.x + 10, rect.y + 2, rect.width - 20, 22), station.title + (food != null ? " · " + IngredientCatalog.GetName(food.type) : ""), centered, Orange);
                if (food != null)
                {
                    Text(new Rect(rect.x + 10, rect.y + 24, rect.width - 20, 20), status, centered, accent);
                    Bar(new Rect(rect.x + 12, rect.yMax - 8, rect.width - 24, 3), progress, accent);
                }
                return;
            }
        }

        private void DrawCrosshair()
        {
            bool selected = player != null && player.CurrentTarget != null;
            float size = selected ? 4f : 2f;
            Rect rect = new Rect((Width - size) / 2f, (Height - size) / 2f, size, size);
            Fill(new Rect(rect.x - 1, rect.y - 1, size + 2, size + 2), HudBackground);
            Fill(rect, selected ? Orange : Color.white);
        }

        private bool TryWorldToHUDPoint(Vector3 worldPosition, out Vector2 point)
        {
            point = Vector2.zero;
            if (kitchenCamera == null || !kitchenCamera.isActiveAndEnabled) return false;
            Vector3 screen = kitchenCamera.WorldToScreenPoint(worldPosition);
            if (!(screen.z >= kitchenCamera.nearClipPlane && screen.z <= kitchenCamera.farClipPlane) ||
                !kitchenCamera.pixelRect.Contains(new Vector2(screen.x, screen.y))) return false;
            Vector3 local = GUI.matrix.inverse.MultiplyPoint3x4(new Vector3(screen.x, Screen.height - screen.y, 0f));
            point = new Vector2(local.x, local.y);
            return point.x >= 0f && point.x <= Width && point.y >= 0f && point.y <= Height;
        }

        private void DrawStoveWarning()
        {
            if (kitchenCamera == null || gm.State != GameState.Playing) return;
            StationBadge urgent = null;
            float remaining = float.MaxValue;
            foreach (StationBadge station in stations)
            {
                if (station.owner == null || station.anchor == null || !station.owner.isActiveAndEnabled || station.stove == null) continue;
                if (player != null && ReferenceEquals(player.CurrentTarget, station.target)) continue;
                if (!station.stove.IsCooked || station.stove.IsBurned || station.stove.SecondsUntilBurn >= remaining) continue;
                remaining = station.stove.SecondsUntilBurn;
                urgent = station;
            }
            if (urgent == null) return;
            Vector3 offset = Vector3.ProjectOnPlane(urgent.anchor.position - kitchenCamera.transform.position, Vector3.up);
            Vector3 forward = Vector3.ProjectOnPlane(kitchenCamera.transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.Cross(kitchenCamera.transform.right, Vector3.up);
            float angle = Vector3.SignedAngle(forward, offset, Vector3.up);
            float turn = Mathf.Abs(angle);
            string direction = turn < 25f ? "前方" : turn > 155f ? "身后" : turn > 115f ? (angle < 0f ? "左后方" : "右后方") : (angle < 0f ? "左侧" : "右侧");
            Color ink = urgent.stove.BurnRemaining01 < 0.35f ? Red : Orange;
            string food = IngredientCatalog.GetName(urgent.stove.PlacedIngredient.type);
            Panel(new Rect(16, 112, 432, 32), HudBackground);
            Text(new Rect(28, 116, 408, 24), direction + "灶台 · " + food + "已熟 · 距焦糊 " + remaining.ToString("0.0") + " 秒", small, ink);
        }

        private void DrawToasts()
        {
            // 只显示最新一条：连续出餐时旧提示会叠在柜台上挡住视线。
            if (toasts.Count == 0) return;
            ToastItem item = toasts[toasts.Count - 1];
            float age = Time.unscaledTime - item.time;
            float alpha = Mathf.Clamp01(3.5f - age);
            if (alpha <= 0) return;
            Rect rect = new Rect(16, 156, 432, 48);
            Color background = HudBackground; background.a *= alpha;
            Color ink = item.positive ? Mint : Red; ink.a = alpha;
            Panel(rect, background);
            Fill(new Rect(rect.x + 12, rect.y + 12, 3, 24), ink);
            Text(new Rect(rect.x + 26, rect.y + 4, rect.width - 42, 40), item.text, small, ink);
        }

        private void DrawComboBanner()
        {
            float age = Time.unscaledTime - comboBannerTime;
            if (age < 0f || age > 1.25f) return;
            float p = age / 1.25f;
            float alpha = p > 0.65f ? 1f - (p - 0.65f) / 0.35f : 1f;
            float scale = Mathf.Lerp(1.2f, 1f, Mathf.Clamp01(p * 7f));
            Rect rect = new Rect(Width / 2f - 120f, 116, 240, 44);
            Matrix4x4 matrix = GUI.matrix;
            GUIUtility.ScaleAroundPivot(new Vector2(scale, scale), rect.center);
            Color plate = HudBackground; plate.a *= alpha;
            Panel(rect, plate);
            Fill(new Rect(rect.x + 70, rect.yMax - 4, rect.width - 140, 3), new Color(1f, 0.78f, 0.3f, alpha));
            Text(new Rect(rect.x + 2, rect.y + 3, rect.width, rect.height), "×" + bannerCombo + " 连击", comboStyle, new Color(0f, 0f, 0f, alpha * 0.5f));
            Text(rect, "×" + bannerCombo + " 连击", comboStyle, new Color(1f, 0.78f, 0.3f, alpha));
            GUI.matrix = matrix;
        }

        private void DrawFxPopups()
        {
            if (kitchenCamera == null || gm.State != GameState.Playing) return;
            FeedbackFX fx = FeedbackFX.Instance;
            if (fx == null) return;
            IReadOnlyList<FeedbackFX.PopupItem> popups = fx.Popups;
            for (int i = 0; i < popups.Count; i++)
            {
                FeedbackFX.PopupItem popup = popups[i];
                if (popup.life <= 0f || !TryWorldToHUDPoint(popup.worldPos, out Vector2 point)) continue;
                float t = Mathf.Clamp01(popup.age / popup.life);
                float alpha = 1f - Mathf.Clamp01((t - 0.55f) / 0.45f);
                if (alpha <= 0f) continue;
                float rise = 24f + t * 28f;
                float scale = Mathf.Lerp(1.15f, 1f, Mathf.Clamp01(t * 5f));
                GUIStyle style = popup.big ? popupBig : popupSmall;
                float centerX = point.x;
                float centerY = point.y - rise;
                Rect rect = new Rect(centerX - 140f, centerY - 23f, 280f, 46f);
                Rect bounds = new Rect(centerX - 140f * scale, centerY - 23f * scale, 282f * scale, 48f * scale);
                if (bounds.xMin < 16f || bounds.xMax > 976f || bounds.yMin < 212f || bounds.yMax > 612f) continue;
                if (bounds.Overlaps(new Rect(512, 272, 256, 112)) ||
                    bounds.Overlaps(new Rect(650, 430, 230, 140)) ||
                    bounds.Overlaps(new Rect(16, 560, 304, 144))) continue;
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
