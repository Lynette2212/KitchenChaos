using UnityEngine;
using KitchenChaos.Data;
using KitchenChaos.Audio;
using KitchenChaos.Visual;

namespace KitchenChaos.Gameplay
{
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
            if (player == null || !player.CanInteract) return false;
            if (IngredientCatalog.Get(ingredientType) == null)
                return player.RejectInteraction("食材柜尚未配置食材");
            if (player.HasItem)
            {
                Ingredient held = player.CarriedItem.Ingredient;
                if (held != null && held.type == ingredientType)
                {
                    player.ClearCarried();
                    ReturnFeedback();
                    return true;
                }
                string heldName = held != null ? IngredientCatalog.GetName(held.type) : "这份食材";
                return player.RejectInteraction(heldName + "要放回" + IngredientCatalog.GetName(held != null ? held.type : ingredientType) + "柜");
            }

            Ingredient data = new Ingredient(ingredientType, IngredientState.Raw);
            if (player.HasPlate)
            {
                string reason = player.Plate.GetAddFailureReason(data);
                if (reason != null) return player.RejectInteraction(reason);
                if (!player.Plate.Add(data)) return false;
                PickupFeedback();
                return true;
            }
            if (!player.GiveItem(data)) return false;
            PickupFeedback();
            return true;
        }

        private void PickupFeedback()
        {
            Transform sample = transform.Find("Sample");
            if (sample != null) FeedbackFX.Punch(sample, 0.3f, 0.35f);
            FeedbackFX.Burst(transform.position + Vector3.up * 0.85f, IngredientCatalog.GetColor(ingredientType), 6, 1.8f, 0.06f, 5f, 0.4f);
            if (AudioManager.Instance != null) AudioManager.Instance.PlayAt(SfxType.Pickup, transform.position + Vector3.up * 0.65f);
        }

        private void ReturnFeedback()
        {
            Transform sample = transform.Find("Sample");
            if (sample != null) FeedbackFX.Punch(sample, 0.22f, 0.28f);
            if (AudioManager.Instance != null) AudioManager.Instance.PlayAt(SfxType.Drop, transform.position + Vector3.up * 0.65f);
        }

        public string GetPrompt() => "按 E 拿取 " + IngredientCatalog.GetName(ingredientType) + "；手持同种食材时按 E 放回";
        public Transform GetTransform() => transform;
    }
}
