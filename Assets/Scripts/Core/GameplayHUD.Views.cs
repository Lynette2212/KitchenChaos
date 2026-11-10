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
        private readonly List<StoveCounter.SlotStatus> stoveSlots = new List<StoveCounter.SlotStatus>();
        private Camera kitchenCamera;
        private CustomerWindow customerWindow;

        // Init runs after KitchenBuilder has finished creating the real counters.
        // Never search the scene or look up Camera.main from OnGUI/Update.
        private void CacheStations()
        {
            stations.Clear();
            kitchenCamera = Camera.main;
            customerWindow = FindAnyObjectByType<CustomerWindow>();
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
            DrawStoveStatusBars();
            DrawChefExpression();
            DrawCustomerBubble();
            DrawTopBar();
            DrawOrders();
            DrawStoveWarning();
            DrawComboBanner();
            DrawHandsAndControls();
            DrawToasts();
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
            Panel(new Rect(16, 16, 944, 64), HudBackground);
            Text(new Rect(34, 21, 190, 20), "第 " + gm.Day.ToString("00") + " 天 / " + CampaignRules.TotalDays + " 天", small, Orange);
            Text(new Rect(34, 40, 190, 32), CampaignRules.DayName(gm.Day), heading, Navy);
            Text(new Rect(238, 25, 120, 22), gm.DayCompletedOrders + " / " + gm.DayTarget + " 单", small, Navy);
            Bar(new Rect(238, 54, 120, 4), gm.DayCompletedOrders / (float)Mathf.Max(1, gm.DayTarget), Mint);
            Color scoreInk = Color.Lerp(Navy, Orange, Mathf.Clamp01(scorePulse));
            TextPulse(new Rect(414, 20, 200, 32), Mathf.RoundToInt(displayedScore) + " 分", heading, scoreInk, 1f + scorePulse * 0.12f);
            if (gm.Score != null && gm.Score.Combo > 1)
                Text(new Rect(414, 53, 200, 18), "×" + gm.Score.Combo + " 连击", small, Orange);
            float seconds = gm.Timer != null ? Mathf.Max(0f, gm.Timer.TimeRemaining) : 0f;
            float timerScale = seconds <= 10f ? 1f + Mathf.Sin(Time.unscaledTime * 9f) * 0.06f : 1f;
            TextPulse(new Rect(660, 18, 140, 40), Clock(seconds), heading, seconds <= 20 ? Red : Navy, timerScale);
            Bar(new Rect(660, 60, 120, 3), gm.Timer != null ? seconds / Mathf.Max(1f, gm.Timer.RoundDuration) : 0f, seconds <= 20 ? Red : Orange);
            int segments = Mathf.Max(1, gm.MaxReputation);
            float unit = 132f / segments;
            Text(new Rect(808, 21, 136, 20), gm.Reputation + " / " + gm.MaxReputation, small, Muted);
            for (int i = 0; i < segments; i++)
                Fill(new Rect(808 + i * unit, 46, Mathf.Max(1, unit - 4), 8), i < gm.Reputation ? (gm.Reputation <= 1 ? Red : Orange) : Track);
        }

        private void DrawOrders()
        {
            Panel(new Rect(984, 12, 292, 56), HudBackground);
            Text(new Rect(996, 18, 182, 32), "出餐清单", heading, Navy);
            var orders = gm.Orders != null ? gm.Orders.ActiveOrders : null;
            int count = orders != null ? Mathf.Min(4, orders.Count) : 0;
            int slots = gm.Orders != null ? Mathf.Max(1, gm.Orders.maxActiveOrders) : 4;
            Text(new Rect(1190, 22, 66, 24), count + " / " + slots, centered, Orange);
            if (count == 0)
            {
                Text(new Rect(996, 76, 268, 20), "等待新订单…", small, Muted);
                return;
            }
            float y = 76f;
            for (int i = 0; i < count; i++)
            {
                Order order = orders[i];
                if (order == null || order.recipe == null) continue;
                var ingredients = order.recipe.ingredients;
                int rows = ingredients != null ? ingredients.Count : 0;
                float height = 32f + rows * 16f + 8f;
                float offsetX = 0f;
                if (order.TimeRatio <= 0.25f) offsetX = Mathf.Sin(Time.unscaledTime * 42f + i * 1.7f) * 1.5f;
                Rect rect = new Rect(992 + offsetX, y, 272, height);
                Color accent = UrgencyColor(order.urgency);
                Color tint = order.urgency == OrderUrgency.Critical ? new Color32(255, 239, 233, 245)
                    : order.urgency == OrderUrgency.Express ? new Color32(255, 247, 232, 245) : Cream;
                Panel(rect, tint);
                Text(new Rect(rect.x + 12, rect.y + 3, 150, 26), order.recipe.recipeName, body, Navy);
                Color pill = UrgencyPillColor(order.urgency);
                Fill(new Rect(rect.x + 170, rect.y + 5, 88, 21), pill);
                Text(new Rect(rect.x + 170, rect.y + 5, 88, 21), OrderUrgencyInfo.Name(order.urgency) + " "
                    + Mathf.CeilToInt(Mathf.Max(0, order.timeRemaining)) + "s", microCenter,
                    order.urgency == OrderUrgency.Critical ? Cream : Navy);
                for (int j = 0; j < rows; j++)
                {
                    RecipeIngredient item = ingredients[j];
                    if (item == null) continue;
                    string state = item.type == IngredientType.Mushroom && item.requiredState == IngredientState.Cooked ? "先切后煎" : StateName(item.requiredState);
                    float rowY = rect.y + 28f + j * 16f;
                    Fill(new Rect(rect.x + 14, rowY + 5, 3, 3), accent);
                    Text(new Rect(rect.x + 24, rowY, 150, 16), IngredientCatalog.GetName(item.type), micro, Navy);
                    Text(new Rect(rect.x + 178, rowY, 80, 16), state, micro, Blue);
                }
                y += height + 6f;
            }
        }

        private static Color UrgencyColor(OrderUrgency urgency)
        {
            switch (urgency)
            {
                case OrderUrgency.Critical: return new Color32(190, 58, 44, 255);
                case OrderUrgency.Express: return new Color32(198, 104, 24, 255);
                default: return new Color32(37, 96, 114, 255);
            }
        }

        private static Color UrgencyPillColor(OrderUrgency urgency)
        {
            switch (urgency)
            {
                case OrderUrgency.Critical: return new Color32(190, 58, 44, 255);
                case OrderUrgency.Express: return new Color32(255, 177, 100, 255);
                default: return new Color32(224, 232, 219, 255);
            }
        }

        private void DrawHandsAndControls()
        {
            if (player == null) return;
            if (player.HandsBusy)
            {
                int plateRows = player.HasPlate ? Mathf.Max(1, player.Plate.Count) : 0;
                int itemRows = player.HasItem ? 1 : 0;
                int rows = Mathf.Max(1, plateRows + itemRows);
                float height = 38f + rows * 18f;
                float top = 704f - height;
                Panel(new Rect(16, top, 304, height), HudBackground);
                Text(new Rect(30, top + 6, 274, 22), player.HasPlate && player.HasItem ? "餐盘 + 食材" : player.HasPlate ? "餐盘 · " + player.Plate.Count + " / 5" : "食材", body, Orange);
                int row = 0;
                if (player.HasPlate && player.Plate.Count > 0)
                {
                    var contents = player.Plate.Contents;
                    for (int i = 0; i < contents.Count; i++)
                    {
                        Ingredient item = contents[i];
                        if (item == null) continue;
                        DrawHeldRow(new Rect(30, top + 30 + row * 18, 266, 18), item.type, item.state);
                        row++;
                    }
                }
                if (player.HasItem)
                    DrawHeldRow(new Rect(30, top + 30 + row * 18, 266, 18), player.CarriedItem.Type, player.CarriedItem.State);
            }

            IInteractable target = player.CurrentTarget;
            if (target != null)
            {
                Panel(new Rect(330, 646, 620, 46), HudBackground);
                Text(new Rect(344, 650, 592, 38), target.GetPrompt(), small, Navy);
            }
            Text(new Rect(1112, 698, 152, 16), "Esc 暂停 · F11 全屏", micro, Muted);
        }

        private void DrawHeldRow(Rect rect, IngredientType type, IngredientState state)
        {
            Fill(new Rect(rect.x, rect.y + 7, 4, 4), state == IngredientState.Burned ? Red : Mint);
            Text(new Rect(rect.x + 13, rect.y, 143, rect.height), IngredientCatalog.GetName(type), small, Navy);
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
                if (!TryWorldToHUDPoint(station.anchor.position + Vector3.up * 0.95f, out Vector2 point)) return;
                Ingredient food = station.cutter != null ? station.cutter.PlacedIngredient : station.stove != null ? station.stove.PlacedIngredient : null;
                float width = food != null ? 224f : Mathf.Clamp(station.title.Length * 14 + 24, 76, 144);
                float height = food != null ? 58f : 26f;
                Rect rect = new Rect(Mathf.Clamp(point.x - width / 2f, 16f, Width - width - 16f),
                    Mathf.Clamp(point.y - height - 26f, 24f, Height - height - 24f), width, height);
                Color accent = Orange;
                string status = "";
                float progress = 0f;
                if (food != null)
                {
                    if (station.stove != null)
                    {
                        var stove = station.stove;
                        progress = stove.IsBurned ? 1f : stove.IsCooked ? stove.BurnRemaining01 : stove.Progress01;
                        accent = stove.IsBurned ? Red : stove.IsCooked ? (stove.BurnRemaining01 < 0.35f ? Red : Orange) : Mint;
                        status = stove.IsBurned ? "已焦糊 · 取回丢弃"
                            : stove.IsCooked ? stove.CookedCount + " 份可取 · 距焦糊 " + stove.SecondsUntilBurn.ToString("0.0") + " 秒"
                            : "煎制中  " + Mathf.RoundToInt(stove.Progress01 * 100f) + "% · 共 " + stove.OccupiedCount + " 份";
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


        private void DrawChefExpression()
        {
            if (player == null || player.Chef == null) return;
            ChefModel.ChefMood mood = player.Chef.Mood;
            if (mood == ChefModel.ChefMood.None) return;
            if (!TryWorldToHUDPoint(player.transform.position + Vector3.up * 2.15f, out Vector2 point)) return;
            string face = mood == ChefModel.ChefMood.Focus ? "-_-"
                : mood == ChefModel.ChefMood.Happy ? "^_^" : "x_x";
            Color accent = mood == ChefModel.ChefMood.Focus ? Blue
                : mood == ChefModel.ChefMood.Happy ? Mint : Red;
            Rect rect = new Rect(point.x - 32f, point.y - 40f, 64f, 34f);
            if (rect.xMin < 16f || rect.xMax > Width - 16f || rect.yMin < 104f) return;
            Panel(rect, HudBackground);
            Fill(new Rect(rect.x + 8f, rect.yMax - 6f, rect.width - 16f, 2f), accent);
            Text(new Rect(rect.x + 2f, rect.y + 1f, rect.width - 4f, 26f), face, popupSmall, accent);
            Fill(new Rect(rect.center.x - 4f, rect.yMax, 8f, 8f), HudBackground);
        }

        private void DrawCustomerBubble()
        {
            if (customerWindow == null) return;
            CustomerWindow.Bubble bubble = customerWindow.ActiveBubble;
            if (string.IsNullOrEmpty(bubble.text)) return;
            if (!TryWorldToHUDPoint(bubble.position + Vector3.up * 0.55f, out Vector2 point)) return;
            float width = Mathf.Clamp(bubble.text.Length * 13f + 22f, 96f, 216f);
            Rect rect = new Rect(Mathf.Clamp(point.x - width / 2f, 16f, 960f - width),
                Mathf.Clamp(point.y - 44f, 104f, 590f), width, 36f);
            Panel(rect, HudBackground);
            Fill(new Rect(rect.x + 8f, rect.yMax - 7f, rect.width - 16f, 2f), bubble.urgent ? Red : Orange);
            Text(new Rect(rect.x + 11f, rect.y + 3f, rect.width - 22f, 28f), bubble.text, small, Navy);
            Fill(new Rect(Mathf.Clamp(point.x - 4f, rect.x + 12f, rect.xMax - 16f), rect.yMax, 8f, 8f), HudBackground);
        }

        private void DrawStoveStatusBars()
        {
            if (kitchenCamera == null || gm.State != GameState.Playing) return;
            foreach (StationBadge station in stations)
            {
                StoveCounter stove = station.stove;
                if (stove == null || !stove.isActiveAndEnabled) continue;
                stove.CollectSlotStatus(stoveSlots);
                foreach (StoveCounter.SlotStatus slot in stoveSlots)
                {
                    if (!TryWorldToHUDPoint(slot.position + Vector3.up * 0.38f, out Vector2 point)) continue;
                    if (point.y < 118f || point.y > 604f || point.x < 22f || point.x > 972f) continue;
                    const float width = 52f, height = 6f;
                    Rect bar = new Rect(point.x - width / 2f, point.y - height, width, height);
                    Fill(new Rect(bar.x - 1.5f, bar.y - 1.5f, bar.width + 3f, bar.height + 3f), HudBackground);
                    Fill(bar, Track);
                    Color ink = slot.state == IngredientState.Burned ? Red
                        : slot.state == IngredientState.Cooked ? (slot.burnRemaining01 < 0.35f ? Red : Orange) : Mint;
                    float fill = slot.state == IngredientState.Burned ? 1f
                        : slot.state == IngredientState.Cooked ? slot.burnRemaining01 : slot.progress01;
                    Fill(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(fill), bar.height), ink);
                    if (slot.state == IngredientState.Cooked)
                        Text(new Rect(point.x - 34f, point.y + 1f, 68f, 15f), slot.secondsUntilBurn.ToString("0.0") + " 秒", microCenter, ink);
                    else if (slot.state == IngredientState.Burned)
                        Text(new Rect(point.x - 34f, point.y + 1f, 68f, 15f), "糊了", microCenter, Red);
                }
            }
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
            if (gm.State != GameState.Playing) return;
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
            if (urgent == null || urgent.stove.PlacedIngredient == null) return;
            Color ink = urgent.stove.BurnRemaining01 < 0.35f ? Red : Orange;
            string food = IngredientCatalog.GetName(urgent.stove.PlacedIngredient.type);
            Panel(new Rect(16, 112, 432, 32), HudBackground);
            Text(new Rect(28, 116, 408, 24), "灶台 · " + food + "已熟 · 距焦糊 " + remaining.ToString("0.0") + " 秒", small, ink);
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
            Text(new Rect(rect.x + 2, rect.y + 3, rect.width, rect.height), "×" + bannerCombo + " 连击", comboStyle, new Color(0.1f, 0.16f, 0.14f, alpha * 0.18f));
            Text(rect, "×" + bannerCombo + " 连击", comboStyle, new Color(Orange.r, Orange.g, Orange.b, alpha));
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
                if (bounds.xMin < 16f || bounds.xMax > 976f || bounds.yMin < 104f || bounds.yMax > 612f) continue;
                if (bounds.Overlaps(new Rect(16, 540, 304, 164)) ||
                    bounds.Overlaps(new Rect(336, 620, 624, 84))) continue;
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
