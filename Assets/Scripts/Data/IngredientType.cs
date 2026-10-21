using UnityEngine;

namespace KitchenChaos.Data
{
    /// <summary>
    /// 厨房内可出现的全部基础食材。
    /// </summary>
    public enum IngredientType
    {
        Tomato = 0,     // 番茄
        Lettuce = 1,    // 生菜
        Cheese = 2,     // 芝士
        MeatPatty = 3,  // 肉饼
        Bun = 4,        // 面包胚
        Onion = 5,      // 洋葱
        Mushroom = 6    // 蘑菇
    }

    /// <summary>
    /// 单块食材所处的加工状态。状态只会沿 Raw -> Cut / Cooked -> Burned 推进。
    /// </summary>
    public enum IngredientState
    {
        Raw = 0,
        Cut = 1,
        Cooked = 2,
        Burned = 3
    }

    /// <summary>
    /// 食材的静态展示/玩法配置，由 IngredientCatalog 程序化构建。
    /// </summary>
    [System.Serializable]
    public class IngredientInfo
    {
        public IngredientType type;
        public string displayName;
        public Color color;
        public bool needsCut;     // 是否需要先切
        public bool needsCook;    // 是否需要煎/煮
        public float cutTime;
        public float cookTime;
    }
}
