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
            Ingredient data = new Ingredient(ingredientType, IngredientState.Raw);
            if (IngredientCatalog.Get(ingredientType) == null)
                return player.RejectInteraction("食材柜尚未配置食材");
            if (player.HasPlate)
            {
                string reason = player.Plate.GetAddFailureReason(data);
                if (reason != null) return player.RejectInteraction(reason);
                if (!player.Plate.Add(data)) return false;
                PickupFeedback();
                return true;
            }
            if (!player.CanTake()) return player.RejectInteraction("手上已有食材，请先放到加工台或暂存台");
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

        public string GetPrompt() => "按 E 拿取 " + IngredientCatalog.GetName(ingredientType);
        public Transform GetTransform() => transform;
    }
}
