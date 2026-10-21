using UnityEngine;
using KitchenChaos.Audio;

namespace KitchenChaos.Gameplay
{
    /// <summary>
    /// 垃圾桶：丢弃手上的食材或整盘菜。
    /// </summary>
    public class TrashBin : MonoBehaviour, IInteractable
    {
        public bool Interact(PlayerController player)
        {
            PlateHandle plate = player.GetComponentInChildren<PlateHandle>();
            if (plate != null)
            {
                if (plate.Count == 0)
                {
                    // 空盘不允许丢，提示去餐盘台归还
                    return false;
                }
                plate.Dump();
                if (AudioManager.Instance != null) AudioManager.Instance.Play(SfxType.Drop);
                return true;
            }

            if (player.HasItem)
            {
                player.TakeItem();
                if (AudioManager.Instance != null) AudioManager.Instance.Play(SfxType.Drop);
                return true;
            }

            return false;
        }

        public string GetPrompt()
        {
            return "按 E 丢弃手上的东西";
        }

        public Transform GetTransform() => transform;
    }
}
