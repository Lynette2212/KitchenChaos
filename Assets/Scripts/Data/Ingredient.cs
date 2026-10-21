using System;
using UnityEngine;

namespace KitchenChaos.Data
{
    /// <summary>
    /// 一块具体的食材实例：类型 + 当前加工状态。
    /// 玩家手持、灶台加工、盘子组装时都传递这个对象。
    /// </summary>
    [Serializable]
    public class Ingredient
    {
        public IngredientType type;
        public IngredientState state;

        public Ingredient(IngredientType t, IngredientState s = IngredientState.Raw)
        {
            type = t;
            state = s;
        }

        public Ingredient Clone()
        {
            return new Ingredient(type, state);
        }

        public bool IsAcceptable()
        {
            // 烤糊的食材不可用于出餐
            return state != IngredientState.Burned;
        }
    }
}
