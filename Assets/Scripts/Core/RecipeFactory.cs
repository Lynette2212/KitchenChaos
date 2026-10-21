using System.Collections.Generic;
using UnityEngine;
using KitchenChaos.Data;

namespace KitchenChaos.Core
{
    /// <summary>
    /// 当 Resources/Recipes 中没有配方资产时使用的内置默认菜单，
    /// 保证空工程也能立刻玩。正式运行优先加载 Resources 中的 .asset。
    /// </summary>
    public static class RecipeFactory
    {
        public static List<Recipe> CreateDefaultRecipes()
        {
            return new List<Recipe>
            {
                Build("芝士汉堡", 120,
                    (IngredientType.Bun, IngredientState.Raw),
                    (IngredientType.MeatPatty, IngredientState.Cooked),
                    (IngredientType.Cheese, IngredientState.Cut)),

                Build("至尊汉堡", 180,
                    (IngredientType.Bun, IngredientState.Raw),
                    (IngredientType.MeatPatty, IngredientState.Cooked),
                    (IngredientType.Cheese, IngredientState.Cut),
                    (IngredientType.Tomato, IngredientState.Cut),
                    (IngredientType.Lettuce, IngredientState.Cut)),

                Build("田园沙拉", 90,
                    (IngredientType.Lettuce, IngredientState.Cut),
                    (IngredientType.Tomato, IngredientState.Cut),
                    (IngredientType.Onion, IngredientState.Cut)),

                Build("香煎肉饼", 80,
                    (IngredientType.MeatPatty, IngredientState.Cooked)),

                Build("蘑菇芝士堡", 150,
                    (IngredientType.Bun, IngredientState.Raw),
                    (IngredientType.Mushroom, IngredientState.Cooked),
                    (IngredientType.Cheese, IngredientState.Cut)),

                Build("番茄蘑菇盘", 110,
                    (IngredientType.Mushroom, IngredientState.Cooked),
                    (IngredientType.Tomato, IngredientState.Cut))
            };
        }

        private static Recipe Build(string name, int score, params (IngredientType type, IngredientState state)[] items)
        {
            Recipe r = ScriptableObject.CreateInstance<Recipe>();
            r.name = name;
            r.recipeName = name;
            r.score = score;
            r.ingredients = new List<RecipeIngredient>();
            foreach ((IngredientType type, IngredientState state) it in items)
            {
                r.ingredients.Add(new RecipeIngredient { type = it.type, requiredState = it.state });
            }
            return r;
        }
    }
}
