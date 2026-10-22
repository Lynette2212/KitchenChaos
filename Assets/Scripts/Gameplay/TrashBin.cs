using UnityEngine;
using KitchenChaos.Audio;
using KitchenChaos.Visual;

namespace KitchenChaos.Gameplay
{
    public class TrashBin : MonoBehaviour, IInteractable
    {
        public bool Interact(PlayerController player)
        {
            if (player == null || !player.CanInteract) return false;
            PlateHandle plate = player.Plate;
            if (plate != null)
            {
                if (plate.Count == 0) return player.RejectInteraction("空盘不用丢弃，请到餐盘台归还");
                plate.Dump();
                DumpFeedback();
                return true;
            }
            if (!player.HasItem) return player.RejectInteraction("手上没有需要丢弃的东西");
            player.ClearCarried();
            DumpFeedback();
            return true;
        }

        private void DumpFeedback()
        {
            Vector3 mouth = transform.position + Vector3.up * 0.65f;
            FeedbackFX.Puff(mouth, new Color(0.68f, 0.7f, 0.64f), 3, 0.16f, 0.8f, 0.55f);
            FeedbackFX.Burst(mouth, new Color(0.55f, 0.58f, 0.5f), 6, 1.6f, 0.06f, 6f, 0.4f);
            FeedbackFX.Shake(0.03f, 0.08f);
            if (AudioManager.Instance != null) AudioManager.Instance.Play(SfxType.Drop);
        }

        public string GetPrompt() => "按 E 丢弃手持食材或盘内菜品（保留空盘）";
        public Transform GetTransform() => transform;
    }
}
