using UnityEngine;
using KitchenChaos.Data;

namespace KitchenChaos.Gameplay
{
    public class IngredientItem : MonoBehaviour
    {
        public IngredientType Type = IngredientType.Tomato;
        public bool IsSource = true;
        public bool IsCooked;

        public string DisplayName => IsCooked ? Recipe.GetCookedName(Type) : Recipe.GetIngredientName(Type);
        public Color DisplayColor => Recipe.GetIngredientColor(Type);
    }
}
