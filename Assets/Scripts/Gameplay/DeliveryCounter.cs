using System.Collections.Generic;
using UnityEngine;
using KitchenChaos.Core;
using KitchenChaos.Data;

namespace KitchenChaos.Gameplay
{
    /// <summary>
    /// 出餐台：手持装有食材的盘子在此按 E，系统判断是否匹配当前订单并结算。
    /// </summary>
    public class DeliveryCounter : MonoBehaviour, IInteractable
    {
        private GameManager gameManager;

        public void Init(GameManager manager)
        {
            gameManager = manager;
        }

        public bool Interact(PlayerController player)
        {
            PlateHandle plate = player.GetComponentInChildren<PlateHandle>();
            if (plate == null) return false;
            if (plate.Count == 0) return false;

            if (gameManager == null) return false;

            List<Ingredient> dish = plate.Dump();
            bool success = gameManager.TryDeliverDish(dish);
            // 无论成功失败，盘子仍保留在玩家手上（空盘），可继续装菜或归还
            return true;
        }

        public string GetPrompt()
        {
            return "按 E 出餐（需手持装好的盘子）";
        }

        public Transform GetTransform() => transform;
    }
}
