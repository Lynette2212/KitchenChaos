using System.Collections.Generic;
using UnityEngine;

namespace KitchenChaos.Data
{
    public enum IngredientType
    {
        Tomato,
        Lettuce,
        Cheese,
        Meat
    }

    [CreateAssetMenu(fileName = "Recipe", menuName = "Kitchen Chaos/Recipe")]
    public class Recipe : ScriptableObject
    {
        public string recipeName = "新菜谱";
        public List<IngredientType> ingredients = new List<IngredientType>();
        public int score = 100;

        public static string GetIngredientName(IngredientType type)
        {
            switch (type)
            {
                case IngredientType.Tomato: return "番茄";
                case IngredientType.Lettuce: return "生菜";
                case IngredientType.Cheese: return "芝士";
                case IngredientType.Meat: return "肉";
                default: return type.ToString();
            }
        }

        public static string GetCookedName(IngredientType type)
        {
            return "熟" + GetIngredientName(type);
        }

        public static Color GetIngredientColor(IngredientType type)
        {
            switch (type)
            {
                case IngredientType.Tomato: return new Color(0.85f, 0.2f, 0.2f);
                case IngredientType.Lettuce: return new Color(0.3f, 0.75f, 0.3f);
                case IngredientType.Cheese: return new Color(0.95f, 0.8f, 0.25f);
                case IngredientType.Meat: return new Color(0.55f, 0.25f, 0.2f);
                default: return Color.white;
            }
        }
    }
}
