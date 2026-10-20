using System;
using System.Collections.Generic;

namespace KitchenChaos.Data
{
    [Serializable]
    public class Order
    {
        public Recipe recipe;
        public List<IngredientType> remaining;

        public Order(Recipe recipe)
        {
            this.recipe = recipe;
            remaining = recipe != null ? new List<IngredientType>(recipe.ingredients) : new List<IngredientType>();
        }

        public bool IsComplete => remaining.Count == 0;

        public bool TryDeliver(IngredientType type)
        {
            int index = remaining.IndexOf(type);
            if (index < 0) return false;

            remaining.RemoveAt(index);
            return true;
        }

        public string GetRemainingText()
        {
            if (IsComplete) return "已完成";

            List<string> names = new List<string>();
            foreach (IngredientType type in remaining)
            {
                names.Add(Recipe.GetCookedName(type));
            }
            return string.Join("、", names);
        }
    }
}
