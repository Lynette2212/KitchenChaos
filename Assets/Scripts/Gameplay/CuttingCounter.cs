using UnityEngine;
using KitchenChaos.Data;
using KitchenChaos.Core;
using KitchenChaos.Audio;
using KitchenChaos.Visual;

namespace KitchenChaos.Gameplay
{
    public class CuttingCounter : MonoBehaviour, IInteractable, IProcessingStation
    {
        [SerializeField] private Transform itemAnchor;
        private IngredientItem placed;
        private float cutTimer;
        private bool chopping;
        private PlayerController choppingPlayer;

        public Ingredient PlacedIngredient => placed != null ? placed.Ingredient : null;
        private bool CanChop => placed != null && placed.State == IngredientState.Raw
            && choppingPlayer != null && choppingPlayer.CanInteract && choppingPlayer.CanTake()
            && ReferenceEquals(choppingPlayer.CurrentTarget, this);
        public bool IsProcessing => chopping && CanChop;
        public float Progress01 => placed == null ? 0f : placed.State == IngredientState.Cut ? 1f
            : Mathf.Clamp01(cutTimer / CutDuration);
        private float CutDuration => Mathf.Max(0.01f, IngredientCatalog.Get(placed.Type)?.cutTime ?? 1f);

        public bool Interact(PlayerController player)
        {
            if (player == null || !player.CanInteract) return false;
            if (placed != null)
            {
                if (player.HasPlate)
                {
                    string reason = player.Plate.GetAddFailureReason(placed.Ingredient);
                    if (reason != null) return player.RejectInteraction(reason);
                    if (!player.Plate.Add(placed.Ingredient)) return false;
                    ClearPlaced();
                    Play(SfxType.Drop);
                    return true;
                }
                if (player.HasItem) return player.RejectInteraction("切菜台已有食材，请先腾出双手");
                if (placed.State == IngredientState.Cut) return GivePlaced(player);
                choppingPlayer = player;
                chopping = false;
                return true;
            }

            if (!player.HasItem) return player.RejectInteraction("先拿取需要切的原料，再按 E 放上切菜台");
            Ingredient data = player.CarriedItem.Ingredient;
            if (data == null || data.state != IngredientState.Raw || !IngredientCatalog.NeedsCut(data.type))
                return player.RejectInteraction("这里只能切未加工的蔬菜、芝士或蘑菇；其他物品请放暂存台");
            Place(player.TakeItem(), player);
            Play(SfxType.Drop);
            return true;
        }

        public bool TakeBack(PlayerController player)
        {
            if (player == null || !player.CanInteract) return false;
            if (placed == null) return player.RejectInteraction("切菜台上没有可取回的原料");
            if (!player.CanTake()) return player.RejectInteraction("先腾出双手，再按 X 取回原料");
            if (placed.State != IngredientState.Raw) return player.RejectInteraction("食材已切好，按 E 取走");
            return GivePlaced(player);
        }

        private bool GivePlaced(PlayerController player)
        {
            if (!player.GiveItem(placed.Ingredient)) return false;
            ClearPlaced();
            Play(SfxType.Pickup);
            return true;
        }

        private void Place(Ingredient data, PlayerController player)
        {
            if (itemAnchor == null) itemAnchor = transform.Find("ItemAnchor");
            GameObject go = new GameObject("PlacedIngredient");
            go.transform.SetParent(itemAnchor != null ? itemAnchor : transform, false);
            go.transform.localPosition = itemAnchor != null ? Vector3.zero : new Vector3(0f, 0.58f, 0f);
            placed = go.AddComponent<IngredientItem>();
            placed.Setup(data);
            cutTimer = 0f;
            choppingPlayer = player;
            chopping = false;
        }

        private void ClearPlaced()
        {
            if (placed != null)
            {
                placed.gameObject.SetActive(false);
                Destroy(placed.gameObject);
            }
            placed = null;
            cutTimer = 0f;
            chopping = false;
            choppingPlayer = null;
        }

        private void Update()
        {
            chopping = false;
            if (Input.GetKey(KeyCode.E)) AdvanceProcessing(Time.deltaTime);
        }

        public void AdvanceProcessing(float deltaTime)
        {
            chopping = false;
            if (!CanChop || deltaTime <= 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;
            float speed = Mathf.Max(0f, GameManager.Instance.CookingSpeedMultiplier);
            if (speed <= 0f) return;
            chopping = true;
            float previous = cutTimer;
            float need = CutDuration;
            cutTimer = Mathf.Min(need, cutTimer + deltaTime * speed);
            if (Mathf.FloorToInt(cutTimer * 6f) != Mathf.FloorToInt(previous * 6f))
            {
                Play(SfxType.Chop);
                Color tint = IngredientCatalog.GetColor(placed.Type);
                FeedbackFX.Burst(placed.transform.position + Vector3.up * 0.18f, tint, 4, 1.9f, 0.055f, 6f, 0.32f);
                FeedbackFX.Punch(placed.transform, 0.07f, 0.14f);
                FeedbackFX.Shake(0.015f, 0.06f);
            }
            if (cutTimer < need) return;
            Vector3 done = placed.transform.position + Vector3.up * 0.25f;
            placed.SetState(IngredientState.Cut);
            FeedbackFX.Burst(done, IngredientCatalog.GetColor(placed.Type), 12, 2.4f, 0.08f, 5f, 0.5f);
            FeedbackFX.Punch(placed.transform, 0.35f, 0.45f);
            FeedbackFX.Popup(done + Vector3.up * 0.35f, "切好了", new Color(0.55f, 0.95f, 0.8f), false);
            if (AudioManager.Instance != null) AudioManager.Instance.Play(SfxType.ChopDone);
            chopping = false;
        }

        public void ResetStation() => ClearPlaced();

        private static void Play(SfxType sound)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.Play(sound);
        }

        public string GetPrompt()
        {
            if (placed == null) return "切菜台：E 放入原料，空手长按 E 切菜";
            if (placed.State == IngredientState.Cut)
                return IngredientCatalog.NeedsCook(placed.Type)
                    ? "已切好：E 取走，再到灶台煎熟"
                    : "已切好：空手 E 取走，端盘 E 装盘";
            return "空手长按 E 切菜；X 取回未切好的原料";
        }

        public Transform GetTransform() => transform;
    }
}
