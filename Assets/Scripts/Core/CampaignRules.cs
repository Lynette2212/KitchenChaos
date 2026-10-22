using UnityEngine;
using KitchenChaos.Data;

namespace KitchenChaos.Core
{
    public enum KitchenUpgrade { QuickHands, RunningShoes, PatientGuests, SafeStove, ExtraHeart, BonusTips }

    public static class CampaignRules
    {
        public const int TotalDays = 5;

        public static int TargetForDay(int day) => Mathf.Clamp(day, 1, TotalDays) + 2;
        public static float DurationForDay(int day, float baseDuration) => Mathf.Max(90f, baseDuration) + (Mathf.Clamp(day, 1, TotalDays) - 1) * 20f;

        public static string DayName(int day)
        {
            switch (day)
            {
                case 1: return "初来乍到";
                case 2: return "汉堡开张";
                case 3: return "田园午餐";
                case 4: return "招牌大厨";
                default: return "巅峰晚高峰";
            }
        }

        public static int UnlockDay(Recipe recipe)
        {
            if (recipe.ingredients.Count >= 5) return 4;
            foreach (RecipeIngredient item in recipe.ingredients)
                if (item.type == IngredientType.Onion) return 3;
            return recipe.ingredients.Count > 1 ? 2 : 1;
        }

        public static float OrderTimeLimit(Recipe recipe, int day, float patienceBonus)
        {
            float processing = 0f;
            foreach (RecipeIngredient item in recipe.ingredients)
            {
                IngredientInfo info = IngredientCatalog.Get(item.type);
                if (info.needsCut) processing += info.cutTime;
                if (info.needsCook) processing += info.cookTime;
            }
            return Mathf.Max(40f, 48f + recipe.ingredients.Count * 12f + processing * 3f - (day - 1) * 3f + patienceBonus);
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
                case KitchenUpgrade.SafeStove: return "熟食烧糊缓冲 +5 秒\n放心处理另一边的工作。";
                case KitchenUpgrade.ExtraHeart: return "每日声望上限 +1\n多一次挽救失误的机会。";
                default: return "出餐得分 +25%\n保持连击，冲击更高纪录。";
            }
        }
    }
}
