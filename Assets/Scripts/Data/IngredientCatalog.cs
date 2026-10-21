using System.Collections.Generic;
using UnityEngine;

namespace KitchenChaos.Data
{
    /// <summary>
    /// 全部食材的中央配置表：名字、颜色、加工要求与耗时。
    /// 纯代码构建，不依赖外部素材即可运行；图标/材质由 AssetFactory 另行烘焙。
    /// </summary>
    public static class IngredientCatalog
    {
        private static List<IngredientInfo> cache;

        public static IReadOnlyList<IngredientInfo> All
        {
            get
            {
                if (cache == null) Build();
                return cache;
            }
        }

        public static IngredientInfo Get(IngredientType type)
        {
            if (cache == null) Build();
            int index = (int)type;
            if (index >= 0 && index < cache.Count) return cache[index];
            return null;
        }

        public static string GetName(IngredientType type)
        {
            IngredientInfo info = Get(type);
            return info != null ? info.displayName : type.ToString();
        }

        public static Color GetColor(IngredientType type)
        {
            IngredientInfo info = Get(type);
            return info != null ? info.color : Color.magenta;
        }

        public static bool NeedsCut(IngredientType type)
        {
            IngredientInfo info = Get(type);
            return info != null && info.needsCut;
        }

        public static bool NeedsCook(IngredientType type)
        {
            IngredientInfo info = Get(type);
            return info != null && info.needsCook;
        }

        private static void Build()
        {
            cache = new List<IngredientInfo>
            {
                new IngredientInfo { type = IngredientType.Tomato,    displayName = "番茄",   color = new Color(0.90f, 0.25f, 0.20f), needsCut = true,  needsCook = false, cutTime = 1.2f, cookTime = 0f },
                new IngredientInfo { type = IngredientType.Lettuce,   displayName = "生菜",   color = new Color(0.45f, 0.75f, 0.30f), needsCut = true,  needsCook = false, cutTime = 1.0f, cookTime = 0f },
                new IngredientInfo { type = IngredientType.Cheese,    displayName = "芝士",   color = new Color(1.00f, 0.82f, 0.30f), needsCut = true,  needsCook = false, cutTime = 1.0f, cookTime = 0f },
                new IngredientInfo { type = IngredientType.MeatPatty, displayName = "肉饼",   color = new Color(0.62f, 0.33f, 0.22f), needsCut = false, needsCook = true,  cutTime = 0f,   cookTime = 3.0f },
                new IngredientInfo { type = IngredientType.Bun,       displayName = "面包胚", color = new Color(0.86f, 0.66f, 0.38f), needsCut = false, needsCook = false, cutTime = 0f,   cookTime = 0f },
                new IngredientInfo { type = IngredientType.Onion,     displayName = "洋葱",   color = new Color(0.92f, 0.88f, 0.95f), needsCut = true,  needsCook = false, cutTime = 1.4f, cookTime = 0f },
                new IngredientInfo { type = IngredientType.Mushroom,  displayName = "蘑菇",   color = new Color(0.78f, 0.66f, 0.52f), needsCut = true,  needsCook = true,  cutTime = 1.0f, cookTime = 2.4f }
            };
        }
    }
}
