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
            // 手持盘子时：可直接把“无需加工”的食材（如面包）放进盘里，
            // 否则面包这类即食食材没有任何途径能装盘，汉堡无法组装。
            if (player.HasPlate)
            {
                bool readyToPlate = !IngredientCatalog.NeedsCut(ingredientType)
                                    && !IngredientCatalog.Get(ingredientType).needsCook;
                if (!readyToPlate) return false;
                if (player.Plate.Add(new Ingredient(ingredientType, IngredientState.Raw)))
                {
                    if (AudioManager.Instance != null) AudioManager.Instance.Play(SfxType.Drop);
                    return true;
                }
                return false;
            }

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
