using System.Collections.Generic;
using UnityEngine;

namespace KitchenChaos.Data
{
    /// <summary>
    /// 一道菜的配方：由若干“对状态有要求”的食材组成。
    /// ScriptableObject 资产保存在 Assets/Resources/Recipes，可在 Inspector 扩展。
    /// </summary>
    [CreateAssetMenu(fileName = "NewRecipe", menuName = "Kitchen Chaos/Recipe")]
    public class Recipe : ScriptableObject
    {
        [Tooltip("中文菜名")]
        public string recipeName = "新菜品";

        [Tooltip("完成后基础分数（会再乘订单速度奖励与连击）")]
        public int score = 100;

        [Tooltip("该菜品需要的食材及其要求状态")]
        public List<RecipeIngredient> ingredients = new List<RecipeIngredient>();

        /// <summary>
        /// 判断一盘已组装的食材是否满足本配方（忽略顺序、忽略多余食材是否可接受）。
        /// 返回满足的要求条目数，配合完全匹配使用。
        /// </summary>
        public bool Matches(List<Ingredient> plate)
        {
            if (plate == null) return false;

            // 数量必须一致
            if (plate.Count != ingredients.Count) return false;

            // 复制一份需求，逐个被盘中食材消耗
            List<RecipeIngredient> need = new List<RecipeIngredient>(ingredients);
            foreach (Ingredient held in plate)
            {
                int found = need.FindIndex(r => r.type == held.type && r.requiredState == held.state);
                if (found < 0) return false;
                need.RemoveAt(found);
            }

            return need.Count == 0;
        }

        public string GetRequirementText()
        {
            List<string> parts = new List<string>();
            foreach (RecipeIngredient r in ingredients)
            {
                parts.Add(IngredientCatalog.GetName(r.type) + StateSuffix(r.requiredState));
            }
            return string.Join(" + ", parts);
        }

        public static string StateSuffix(IngredientState state)
        {
            switch (state)
            {
                case IngredientState.Cut: return "(切)";
                case IngredientState.Cooked: return "(熟)";
                case IngredientState.Burned: return "(糊)";
                default: return string.Empty;
            }
        }
    }

    [System.Serializable]
    public class RecipeIngredient
    {
        public IngredientType type;
        public IngredientState requiredState = IngredientState.Raw;
    }
}
