using UnityEngine;
using KitchenChaos.Data;

namespace KitchenChaos.Core
{
    public enum KitchenUpgrade { QuickHands, RunningShoes, PatientGuests, SafeStove, ExtraHeart, BonusTips }

    public static class CampaignRules
    {
        public const int TotalDays = 5;

        public static int TargetForDay(int day)
        {
            int clamped = Mathf.Clamp(day, 1, TotalDays);
            return clamped == 1 ? 2 : clamped + 2;
        }

        public static float DurationForDay(int day, float baseDuration)
        {
            int clamped = Mathf.Clamp(day, 1, TotalDays);
            float duration = Mathf.Max(90f, baseDuration) + (clamped - 1) * 20f;
            return clamped == 1 ? Mathf.Min(duration, 120f) : duration;
        }

        public static string DayName(int day)
        {
            switch (day)
            {
                case 1: return "初来乍到";
                case 2: return "菜单变多";
                case 3: return "田园午餐";
                case 4: return "招牌大厨";
                default: return "巅峰晚高峰";
            }
        }

        public static int UnlockDay(Recipe recipe)
        {
            if (recipe == null || recipe.ingredients == null || recipe.ingredients.Count == 0) return 1;
            if (recipe.ingredients.Count >= 5) return 4;
            bool onion = false;
            bool bun = false;
            bool patty = false;
            bool cheese = false;
            bool mushroom = false;
            foreach (RecipeIngredient item in recipe.ingredients)
            {
                if (item.type == IngredientType.Onion) onion = true;
                if (item.type == IngredientType.Bun) bun = true;
                if (item.type == IngredientType.MeatPatty) patty = true;
                if (item.type == IngredientType.Cheese) cheese = true;
                if (item.type == IngredientType.Mushroom) mushroom = true;
            }
            if (onion) return 3;
            // 第一天同时开放单品肉饼和芝士汉堡，开局就要切、煎、装盘。
            if (recipe.ingredients.Count == 1) return 1;
            if (recipe.ingredients.Count == 3 && bun && patty && cheese && !mushroom) return 1;
            return 2;
        }

        public static float OrderTimeLimit(Recipe recipe, int day, float patienceBonus)
            => OrderTimeLimit(recipe, day, patienceBonus, OrderUrgency.Normal);

        public static float OrderTimeLimit(Recipe recipe, int day, float patienceBonus, OrderUrgency urgency)
        {
            float processing = 0f;
            foreach (RecipeIngredient item in recipe.ingredients)
            {
                IngredientInfo info = IngredientCatalog.Get(item.type);
                if (info == null) continue;
                if (info.needsCut) processing += info.cutTime;
                if (info.needsCook) processing += info.cookTime;
            }
            float budget = 48f + recipe.ingredients.Count * 12f + processing * 3f - (day - 1) * 3f;
            return Mathf.Max(28f, budget * 0.7f) * OrderUrgencyInfo.TimeScale(urgency) + patienceBonus;
        }

        public static int Stars(float timeRatio, int mistakes) => 1 + (mistakes == 0 ? 1 : 0) + (timeRatio >= 0.25f ? 1 : 0);

        public static string UpgradeName(KitchenUpgrade upgrade)
        {
            switch (upgrade)
            {
                case KitchenUpgrade.QuickHands: return "大厨快手";
                case KitchenUpgrade.RunningShoes: return "轻盈跑鞋";
                case KitchenUpgrade.PatientGuests: return "暖心服务";
                case KitchenUpgrade.SafeStove: return "恒温灶台";
                case KitchenUpgrade.ExtraHeart: return "好评招牌";
                default: return "小费达人";
            }
        }

        public static string UpgradeDescription(KitchenUpgrade upgrade)
        {
            switch (upgrade)
            {
                case KitchenUpgrade.QuickHands: return "切菜与煎制速度 +25%\n更快出餐，也要及时端走熟食。";
                case KitchenUpgrade.RunningShoes: return "移动速度 +15%\n减少来回跑动的时间。";
                case KitchenUpgrade.PatientGuests: return "每张订单耐心 +15 秒\n复杂菜谱也能从容准备。";
                case KitchenUpgrade.SafeStove: return "熟食烧糊缓冲 +5 秒\n默认大约 5 秒就要端走，升级后再多留一会儿。";
                case KitchenUpgrade.ExtraHeart: return "每日声望上限 +1\n多一次挽救失误的机会。";
                default: return "出餐得分 +25%\n保持连击，冲击更高纪录。";
            }
        }
    }
}
