using UnityEngine;
using KitchenChaos.Data;
using KitchenChaos.Audio;

namespace KitchenChaos.Gameplay
{
    /// <summary>
    /// 灶台：放入需要烹饪的食材后自动加热。
    /// 到达 cookTime 变为“熟”；若不及时取走，继续加热超过 burnGrace 会烤糊。
    /// 台面最多一块，空手交互取回。
    /// </summary>
    public class StoveCounter : MonoBehaviour, IInteractable, IProcessingStation
    {
        [SerializeField] private Transform itemAnchor;
        [SerializeField] private float burnGrace = 3.5f; // 熟后多久不拿走会糊

        private IngredientItem placed;
        private float cookTimer;
        private bool hasCooked;
        private float sizzleCooldown;

        public bool IsProcessing => placed != null && !hasCooked && placed.State != IngredientState.Burned;
        public bool IsCooked => placed != null && placed.State == IngredientState.Cooked;
        public bool IsBurned => placed != null && placed.State == IngredientState.Burned;

        public float Progress01
        {
            get
            {
                if (placed == null) return 0f;
                if (hasCooked) return 1f; // 已煎好（等待取走，再放会糊）
                IngredientInfo info = IngredientCatalog.Get(placed.Type);
                float need = info != null ? info.cookTime : 1f;
                return Mathf.Clamp01(cookTimer / Mathf.Max(0.1f, need));
            }
        }

        public bool Interact(PlayerController player)
        {
            if (placed != null)
            {
                // 手持盘子且已煎好：装入盘中（糊的不能装）
                if (player.HasPlate && placed.State == IngredientState.Cooked)
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
                player.SpawnCarried(placed.Ingredient.Clone());
                ClearPlaced();
                if (AudioManager.Instance != null) AudioManager.Instance.Play(SfxType.Pickup);
                return true;
            }

            if (player.HasItem)
            {
                Ingredient data = player.TakeItem();
                if (data == null) return false;

                IngredientInfo info = IngredientCatalog.Get(data.type);
                bool canCook = info != null && info.needsCook
                               && (data.state == IngredientState.Raw || data.state == IngredientState.Cut)
                               && data.state != IngredientState.Cooked
                               && data.state != IngredientState.Burned;
                if (!canCook)
                {
                    player.SpawnCarried(data); // 退回
                    return false;
                }

                Place(data);
                if (AudioManager.Instance != null) AudioManager.Instance.Play(SfxType.Drop);
                return true;
            }

            return false;
        }

        private void Place(Ingredient data)
        {
            GameObject go = new GameObject("StoveIngredient");
            go.transform.SetParent(itemAnchor != null ? itemAnchor : transform, false);
            placed = go.AddComponent<IngredientItem>();
            placed.Setup(data, itemAnchor != null ? itemAnchor : transform, new Vector3(0f, 1.05f, 0f));
            cookTimer = 0f;
            hasCooked = false;
        }

        private void ClearPlaced()
        {
            if (placed != null) Destroy(placed.gameObject);
            placed = null;
            cookTimer = 0f;
            hasCooked = false;
        }

        private void Update()
        {
            if (placed == null) return;

            IngredientInfo info = IngredientCatalog.Get(placed.Type);
            float need = info != null ? info.cookTime : 1f;

            if (placed.State == IngredientState.Burned) return;

            cookTimer += Time.deltaTime;

            // 煎烤循环音
            sizzleCooldown -= Time.deltaTime;
            if (sizzleCooldown <= 0f)
            {
                if (AudioManager.Instance != null) AudioManager.Instance.Play(SfxType.Sizzle);
                sizzleCooldown = 0.9f;
            }

            if (!hasCooked && cookTimer >= need)
            {
                hasCooked = true;
                placed.SetState(IngredientState.Cooked);
                cookTimer = 0f;
                if (AudioManager.Instance != null) AudioManager.Instance.Play(SfxType.Cooked);
            }
            else if (hasCooked && cookTimer >= burnGrace)
            {
                placed.SetState(IngredientState.Burned);
                if (AudioManager.Instance != null) AudioManager.Instance.Play(SfxType.Burned);
            }
        }

        public string GetPrompt()
        {
            if (placed == null) return "灶台：放入待煎食材";
            if (IsBurned) return "烤糊了！按 E 取回（只能丢弃）";
            if (IsCooked) return "已煎好！按 E 取走（再放会糊）";
            return "煎制中…按 E 可取回";
        }

        public Transform GetTransform() => transform;
    }
}
