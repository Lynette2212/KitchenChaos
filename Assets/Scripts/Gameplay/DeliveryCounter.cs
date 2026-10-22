using System.Collections.Generic;
using UnityEngine;
using KitchenChaos.Core;
using KitchenChaos.Data;
using KitchenChaos.Audio;
using KitchenChaos.Visual;

namespace KitchenChaos.Gameplay
{
    public class DeliveryCounter : MonoBehaviour, IInteractable
    {
        private GameManager gameManager;
        private bool delivering;

        public void Init(GameManager manager) => gameManager = manager;

        public bool Interact(PlayerController player)
        {
            if (player == null || !player.CanInteract || delivering) return false;
            PlateHandle plate = player.Plate;
            if (plate == null) return player.RejectInteraction("请先用盘子装好菜，再来出餐");
            if (plate.Count == 0) return player.RejectInteraction("盘子还是空的，请先装入订单所需食材");
            GameManager manager = gameManager != null ? gameManager : GameManager.Instance;
            if (manager == null || !manager.IsPlaying) return false;

            delivering = true;
            try
            {
                // 结算可能同步触发换日并销毁手持物，之后不再访问玩家和盘子。
                List<Ingredient> dish = plate.Dump();
                bool success = manager.TryDeliverDish(dish);
                Celebrate(success, manager);
                return true;
            }
            finally
            {
                delivering = false;
            }
        }

        private void Celebrate(bool success, GameManager manager)
        {
            Vector3 origin = transform.position + Vector3.up * 0.7f;
            Transform bell = transform.Find("Bell");
            if (bell == null) bell = transform;
            if (success)
            {
                int gained = manager.Score != null ? manager.Score.LastGained : 0;
                int combo = manager.Score != null ? manager.Score.Combo : 0;
                FeedbackFX.Confetti(origin + Vector3.up * 0.2f, 26);
                FeedbackFX.Burst(origin, new Color(1f, 0.82f, 0.35f), 10, 2.8f, 0.08f, 5f, 0.5f);
                FeedbackFX.Punch(bell, 0.5f, 0.5f);
                FeedbackFX.Popup(origin + Vector3.up * 0.55f, "+" + gained, new Color(1f, 0.84f, 0.4f), true);
                FeedbackFX.Shake(0.07f, 0.16f);
                if (AudioManager.Instance != null)
                    AudioManager.Instance.Play(SfxType.Bell, 1f + Mathf.Min(combo, 12) * 0.04f);
            }
            else
            {
                FeedbackFX.Burst(origin, new Color(1f, 0.45f, 0.42f), 14, 2.6f, 0.09f, 7f, 0.55f);
                FeedbackFX.Popup(origin + Vector3.up * 0.5f, "配方不对", new Color(1f, 0.5f, 0.45f), true);
                FeedbackFX.Shake(0.2f, 0.3f);
            }
        }

        public string GetPrompt() => "按 E 出餐（需手持装好的盘子，出餐后保留空盘）";
        public Transform GetTransform() => transform;
    }
}
