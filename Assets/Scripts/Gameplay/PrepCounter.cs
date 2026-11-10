using System.Collections.Generic;
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
        private readonly List<GameObject> options = new List<GameObject>();

        public Ingredient PlacedIngredient => placed != null ? placed.Ingredient : null;
        public PlateHandle Plate => plate;
        public bool HasPlate => plate != null;

        public bool Interact(PlayerController player)
        {
            if (player == null || !player.CanInteract) return false;
            if (plate != null)
            {
                if (player.HasItem)
                {
                    Ingredient data = player.CarriedItem.Ingredient;
                    string reason = plate.GetAddFailureReason(data);
                    if (reason != null) return player.RejectInteraction(reason);
                    if (!plate.Add(data)) return false;
                    player.ClearCarried();
                    if (!Application.isBatchMode) RefreshAssembly();
                    Play(SfxType.Drop);
                    return true;
                }
                if (player.HasPlate) return player.RejectInteraction("手上已经有盘子了，先放回餐盘台或暂存台");
                if (player.CanTake()) return LiftPlate(player);
                return player.RejectInteraction("组装台上已有汉堡，请先腾出双手端走");
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

            if (!player.HandsBusy) return player.RejectInteraction("把盘子放到这里，旁边会出现能叠上的食材");
            if (itemAnchor == null) itemAnchor = transform.Find("ItemAnchor");
            Transform parent = itemAnchor != null ? itemAnchor : transform;
            if (player.HasPlate)
            {
                plate = player.DetachPlate();
                plate.SetPosition(parent, new Vector3(0f, itemAnchor != null ? 0.04f : 0.59f, 0f));
                if (!Application.isBatchMode) RefreshAssembly();
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
            Play(plate != null ? SfxType.PlatePlace : SfxType.Drop);
            return true;
        }

        public bool LiftPlate(PlayerController player)
        {
            if (plate == null || player == null || player.HasPlate || !player.CanTake()) return false;
            PlateHandle stored = plate;
            plate = null;
            ClearOptions();
            player.AttachPlate(stored);
            Play(SfxType.PlatePickup);
            return true;
        }

        public void RefreshAssembly()
        {
            ClearOptions();
            if (plate == null) return;
            if (itemAnchor == null) itemAnchor = transform.Find("ItemAnchor");
            Transform parent = itemAnchor != null ? itemAnchor : transform;
            int shown = 0;
            foreach (StoveCounter stove in FindObjectsByType<StoveCounter>(FindObjectsSortMode.None))
            {
                var cooked = new List<int>();
                stove.CollectCooked(cooked);
                foreach (int index in cooked)
                {
                    Ingredient sample = stove.CookedAt(index);
                    int slot = index;
                    StoveCounter source = stove;
                    shown = AddOption(parent, shown, sample, () => source.TryTakeCooked(slot, out Ingredient taken) ? taken : null);
                }
            }
            foreach (CuttingCounter cutter in FindObjectsByType<CuttingCounter>(FindObjectsSortMode.None))
            {
                Ingredient sample = cutter.PlacedIngredient;
                if (sample == null) continue;
                CuttingCounter source = cutter;
                shown = AddOption(parent, shown, sample, () => source.ReleaseForPlate());
            }
            foreach (IngredientContainer container in FindObjectsByType<IngredientContainer>(FindObjectsSortMode.None))
            {
                Ingredient sample = new Ingredient(container.IngredientType, IngredientState.Raw);
                IngredientType type = container.IngredientType;
                shown = AddOption(parent, shown, sample, () => new Ingredient(type, IngredientState.Raw));
            }
        }

        private int AddOption(Transform parent, int index, Ingredient sample, System.Func<Ingredient> take)
        {
            if (sample == null || plate.GetAddFailureReason(sample) != null) return index;
            GameObject go = new GameObject("AssemblyOption");
            go.transform.SetParent(parent, false);
            float column = index % 4;
            float row = index / 4;
            go.transform.localPosition = new Vector3(-0.3f + column * 0.2f, 0.2f + row * 0.02f, -0.28f);
            go.AddComponent<AssemblyOption>().Setup(this, plate, sample, take);
            options.Add(go);
            return index + 1;
        }

        private void ClearOptions()
        {
            foreach (GameObject option in options)
            {
                if (option == null) continue;
                option.SetActive(false);
                Destroy(option);
            }
            options.Clear();
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
            ClearOptions();
            if (plate != null) plate.ReturnToCounter();
            plate = null;
        }

        private void Play(SfxType sound)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayAt(sound, transform.position + Vector3.up * 0.65f);
        }

        public string GetPrompt()
        {
            if (plate != null) return "组装（" + plate.Count + "/5）：走近旁边食材按 E 叠上，走近汉堡按 E 端走";
            if (placed != null) return "暂存：" + IngredientCatalog.GetName(placed.Type) + "；空手 E 取回，端盘 E 装盘";
            return "组装台：放下盘子后，走近旁边的食材按 E 叠成汉堡";
        }

        public Transform GetTransform() => transform;
    }
}
