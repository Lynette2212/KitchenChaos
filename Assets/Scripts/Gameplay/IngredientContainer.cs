using UnityEngine;
using KitchenChaos.Data;
using KitchenChaos.Audio;

namespace KitchenChaos.Gameplay
{
    /// <summary>
    /// 食材柜：空手交互时获得一份该类型的生食材（无限供应）。
    /// </summary>
    public class IngredientContainer : MonoBehaviour, IInteractable
    {
        [SerializeField] private IngredientType ingredientType = IngredientType.Tomato;

        public IngredientType IngredientType
        {
            get => ingredientType;
            set => ingredientType = value;
        }

        public bool Interact(PlayerController player)
        {
            if (!player.CanTake()) return false;
            player.SpawnCarried(new Ingredient(ingredientType, IngredientState.Raw));
            if (AudioManager.Instance != null) AudioManager.Instance.Play(SfxType.Pickup);
            return true;
        }

        public string GetPrompt()
        {
            return "按 E 拿取 " + IngredientCatalog.GetName(ingredientType);
        }

        public Transform GetTransform() => transform;
    }
}
