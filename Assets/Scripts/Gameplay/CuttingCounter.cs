using UnityEngine;
using KitchenChaos.Data;
using KitchenChaos.Audio;

namespace KitchenChaos.Gameplay
{
    /// <summary>
    /// 切菜台：放入需要切的食材，按住 E 加工到“切好”状态；不需要切的食材无法放入。
    /// 台面上最多放一块食材，可随时取回。
    /// </summary>
    public class CuttingCounter : MonoBehaviour, IInteractable, IProcessingStation
    {
        [SerializeField] private Transform itemAnchor;

        private IngredientItem placed;
        private float cutTimer;
        private bool chopping;
        private PlayerController choppingPlayer;

        public bool IsProcessing => chopping;
        public float Progress01
        {
            get
            {
                if (placed == null) return 0f;
                IngredientInfo info = IngredientCatalog.Get(placed.Type);
                float need = info != null ? info.cutTime : 1f;
                return need <= 0f ? 1f : Mathf.Clamp01(cutTimer / need);
            }
        }

        public bool Interact(PlayerController player)
        {
            // 台面有食材：
            if (placed != null)
            {
                // 手持盘子且食材已切好：装入盘中
                if (player.HasPlate && placed.State == IngredientState.Cut)
                {
                    if (player.Plate.Add(placed.Ingredient.Clone()))
                    {
                        ClearPlaced();
                        if (AudioManager.Instance != null) AudioManager.Instance.Play(SfxType.Drop);
                        return true;
                    }
                    return false;
                }

                if (player.HasItem || player.HasPlate) return false;

                // 短按交互用于取回
                if (!chopping)
                {
                    player.SpawnCarried(placed.Ingredient.Clone());
                    ClearPlaced();
                    if (AudioManager.Instance != null) AudioManager.Instance.Play(SfxType.Pickup);
                    return true;
                }
                return false;
            }

            // 台面为空：放入需要切的生食材
            if (player.HasItem)
            {
                Ingredient data = player.TakeItem();
                if (data == null) return false;

                if (data.state != IngredientState.Raw || !IngredientCatalog.NeedsCut(data.type))
                {
                    // 不符合加工条件，退回玩家
                    player.SpawnCarried(data);
                    return false;
                }

                Place(data, player);
                if (AudioManager.Instance != null) AudioManager.Instance.Play(SfxType.Drop);
                return true;
            }

            return false;
        }

        private void Place(Ingredient data, PlayerController player)
        {
            GameObject go = new GameObject("PlacedIngredient");
            go.transform.SetParent(itemAnchor != null ? itemAnchor : transform, false);
            placed = go.AddComponent<IngredientItem>();
            placed.Setup(data, itemAnchor != null ? itemAnchor : transform, new Vector3(0f, 1.0f, 0f));
            cutTimer = 0f;
            choppingPlayer = player;
        }

        private void ClearPlaced()
        {
            if (placed != null) Destroy(placed.gameObject);
            placed = null;
            cutTimer = 0f;
            chopping = false;
            choppingPlayer = null;
        }

        private void Update()
        {
            if (placed == null || choppingPlayer == null)
            {
                chopping = false;
                return;
            }

            // 已切好的食材留在台面等待装盘/取回，不再加工
            if (placed.State == IngredientState.Cut)
            {
                chopping = false;
                return;
            }

            // 玩家必须在附近且按住 E
            float dist = Vector3.Distance(choppingPlayer.transform.position, transform.position);
            bool holding = Input.GetKey(KeyCode.E) && dist < 1.6f;
            chopping = holding;

            if (!holding) return;

            IngredientInfo info = IngredientCatalog.Get(placed.Type);
            float need = info != null ? info.cutTime : 1f;
            cutTimer += Time.deltaTime;

            if (Mathf.FloorToInt(cutTimer * 6f) != Mathf.FloorToInt((cutTimer - Time.deltaTime) * 6f))
            {
                if (AudioManager.Instance != null) AudioManager.Instance.Play(SfxType.Chop);
            }

            if (cutTimer >= need)
            {
                placed.SetState(IngredientState.Cut);
                // 加工完成后，若玩家空手则自动入手机，流程更顺手；否则留在台面待装盘/取回
                Ingredient done = placed.Ingredient.Clone();
                bool autoHand = choppingPlayer != null && choppingPlayer.CanTake();
                ClearPlaced();
                if (autoHand)
                {
                    choppingPlayer.SpawnCarried(done);
                }
                else
                {
                    Place(done, choppingPlayer);
                }
            }
        }

        public string GetPrompt()
        {
            if (placed != null)
            {
                return chopping ? "切菜中…" : "按 E 取回 " + IngredientCatalog.GetName(placed.Type);
            }
            return "切菜台：放入需要切的食材并长按 E";
        }

        public Transform GetTransform() => transform;
    }
}
