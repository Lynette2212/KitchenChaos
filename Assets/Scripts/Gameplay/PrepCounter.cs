using UnityEngine;
using KitchenChaos.Data;
using KitchenChaos.Audio;

namespace KitchenChaos.Gameplay
{
    public class PrepCounter : MonoBehaviour, IInteractable
    {
        [SerializeField] private Transform itemAnchor;
        private IngredientItem placed;
        private PlateHandle plate;

        public Ingredient PlacedIngredient => placed != null ? placed.Ingredient : null;
        public PlateHandle Plate => plate;
        public bool HasPlate => plate != null;

        public bool Interact(PlayerController player)
        {
            if (player == null || !player.CanInteract) return false;
            if (plate != null)
            {
                if (player.CanTake())
                {
                    PlateHandle stored = plate;
                    plate = null;
                    player.AttachPlate(stored);
                    Play(SfxType.Pickup);
                    return true;
                }
                if (!player.HasItem) return player.RejectInteraction("暂存台已有一个盘子，请先腾出双手取走");
                Ingredient data = player.CarriedItem.Ingredient;
                string reason = plate.GetAddFailureReason(data);
                if (reason != null) return player.RejectInteraction(reason);
                if (!plate.Add(data)) return false;
                player.ClearCarried();
                Play(SfxType.Drop);
                return true;
            }

            if (placed != null)
            {
                if (player.HasPlate)
                {
                    string reason = player.Plate.GetAddFailureReason(placed.Ingredient);
                    if (reason != null) return player.RejectInteraction(reason);
                    if (!player.Plate.Add(placed.Ingredient)) return false;
                    ClearIngredient();
                    Play(SfxType.Drop);
                    return true;
                }
                if (!player.CanTake()) return player.RejectInteraction("暂存台只能放一份食材或一个盘子，请先取走台面物品");
                if (!player.GiveItem(placed.Ingredient)) return false;
                ClearIngredient();
                Play(SfxType.Pickup);
                return true;
            }

            if (!player.HandsBusy) return player.RejectInteraction("暂存台是空的：可放一份食材或一整个盘子");
            if (itemAnchor == null) itemAnchor = transform.Find("ItemAnchor");
            Transform parent = itemAnchor != null ? itemAnchor : transform;
            if (player.HasPlate)
            {
                plate = player.DetachPlate();
                plate.SetPosition(parent, new Vector3(0f, itemAnchor != null ? 0.04f : 0.59f, 0f));
            }
            else
            {
                Ingredient data = player.TakeItem();
                GameObject go = new GameObject("PrepIngredient");
                go.transform.SetParent(parent, false);
                go.transform.localPosition = itemAnchor != null ? Vector3.zero : new Vector3(0f, 0.58f, 0f);
                placed = go.AddComponent<IngredientItem>();
                placed.Setup(data);
            }
            Play(SfxType.Drop);
            return true;
        }

        private void ClearIngredient()
        {
            if (placed == null) return;
            placed.gameObject.SetActive(false);
            Destroy(placed.gameObject);
            placed = null;
        }

        public void ResetStation()
        {
            ClearIngredient();
            if (plate != null) plate.ReturnToCounter();
            plate = null;
        }

        private static void Play(SfxType sound)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.Play(sound);
        }

        public string GetPrompt()
        {
            if (plate != null) return "暂存盘（" + plate.Count + "/5）：空手 E 取盘，持熟食 E 装盘";
            if (placed != null) return "暂存：" + IngredientCatalog.GetName(placed.Type) + "；空手 E 取回，端盘 E 装盘";
            return "暂存台：E 放下一份食材或整盘菜";
        }

        public Transform GetTransform() => transform;
    }
}
