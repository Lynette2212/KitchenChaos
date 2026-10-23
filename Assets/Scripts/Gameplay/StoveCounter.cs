using UnityEngine;
using KitchenChaos.Data;
using KitchenChaos.Core;
using KitchenChaos.Audio;
using KitchenChaos.Visual;

namespace KitchenChaos.Gameplay
{
    public class StoveCounter : MonoBehaviour, IInteractable, IProcessingStation
    {
        [SerializeField] private Transform itemAnchor;
        private const float BurnGrace = 8f;
        private IngredientItem placed;
        private float cookTimer;
        private float burnTimer;
        private AudioSource sizzleSource;
        private AudioManager audioOwner;
        public AudioSource SizzleSource => sizzleSource;
        private float smokeTimer;
        private float warnCooldown;
        private bool warned;

        public Ingredient PlacedIngredient => placed != null ? placed.Ingredient : null;
        public bool IsProcessing => GameManager.Instance != null && GameManager.Instance.IsPlaying
            && placed != null && !IsCooked && !IsBurned;
        public bool IsCooked => placed != null && placed.State == IngredientState.Cooked;
        public bool IsBurned => placed != null && placed.State == IngredientState.Burned;
        private float CookDuration => Mathf.Max(0.01f, IngredientCatalog.Get(placed.Type)?.cookTime ?? 1f);
        private float BurnDuration => Mathf.Max(0.01f, BurnGrace
            + (GameManager.Instance != null ? GameManager.Instance.BurnGraceBonus : 0f));
        public float Progress01 => placed == null ? 0f : IsCooked || IsBurned ? 1f
            : Mathf.Clamp01(cookTimer / CookDuration);
        public float SecondsUntilBurn => IsCooked ? Mathf.Max(0f, BurnDuration - burnTimer) : 0f;
        public float BurnRemaining01 => IsCooked ? Mathf.Clamp01(SecondsUntilBurn / BurnDuration) : 0f;

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
                if (!player.CanTake()) return player.RejectInteraction("灶台已有食材，请先腾出双手");
                if (!player.GiveItem(placed.Ingredient)) return false;
                ClearPlaced();
                Play(SfxType.Pickup);
                return true;
            }

            if (!player.HasItem) return player.RejectInteraction("先拿取肉饼或切好的蘑菇，再放入灶台");
            Ingredient data = player.CarriedItem.Ingredient;
            IngredientInfo info = data != null ? IngredientCatalog.Get(data.type) : null;
            if (info == null || !info.needsCook)
                return player.RejectInteraction("这种食材无需煎制，请使用切菜台或暂存台");
            if (data.state == IngredientState.Cooked)
                return player.RejectInteraction("食材已经熟了，请装盘或放到暂存台，不能重复煎制");
            if (data.state == IngredientState.Burned)
                return player.RejectInteraction("烤糊的食材无法重新煎制，请丢进垃圾桶");
            if (info.needsCut && data.state != IngredientState.Cut)
                return player.RejectInteraction("蘑菇需要先在切菜台切好，再放入灶台");
            if (data.state != IngredientState.Raw && data.state != IngredientState.Cut)
                return player.RejectInteraction("这份食材无法煎制");
            Place(player.TakeItem());
            Play(SfxType.Drop);
            return true;
        }

        private void Place(Ingredient data)
        {
            if (itemAnchor == null) itemAnchor = transform.Find("ItemAnchor");
            GameObject go = new GameObject("StoveIngredient");
            go.transform.SetParent(itemAnchor != null ? itemAnchor : transform, false);
            go.transform.localPosition = itemAnchor != null ? Vector3.zero : new Vector3(0f, 0.61f, 0f);
            placed = go.AddComponent<IngredientItem>();
            placed.Setup(data);
            cookTimer = 0f;
            burnTimer = 0f;
            smokeTimer = 0f;
            warnCooldown = 0f;
            warned = false;
            SyncSizzle();
        }

        private void ClearPlaced()
        {
            if (placed != null)
            {
                placed.gameObject.SetActive(false);
                Destroy(placed.gameObject);
            }
            placed = null;
            cookTimer = 0f;
            burnTimer = 0f;
            smokeTimer = 0f;
            warnCooldown = 0f;
            warned = false;
            SyncSizzle();
        }

        private void Update()
        {
            AdvanceProcessing(Time.deltaTime);
            SyncSizzle();
        }

        private void SyncSizzle()
        {
            // IsProcessing is false while paused, but the pan still contains its ingredient.
            bool hasFood = isActiveAndEnabled && placed != null && !IsBurned;
            if (hasFood && sizzleSource == null && AudioManager.Instance != null)
            {
                audioOwner = AudioManager.Instance;
                sizzleSource = audioOwner.CreateLoopSource(transform, SfxType.Sizzle);
            }
            if (audioOwner != null && sizzleSource != null)
                audioOwner.SetLoopLevel(sizzleSource, hasFood ? (IsCooked ? 0.62f : 1f) : 0f);
        }

        private void OnDisable()
        {
            if (audioOwner != null && sizzleSource != null) audioOwner.SetLoopLevel(sizzleSource, 0f);
        }

        private void OnDestroy()
        {
            if (audioOwner != null && sizzleSource != null) audioOwner.ReleaseLoopSource(sizzleSource);
        }

        public void AdvanceProcessing(float dt)
        {
            GameManager gm = GameManager.Instance;
            if (gm == null || !gm.IsPlaying || placed == null || IsBurned
                || dt <= 0f || float.IsNaN(dt) || float.IsInfinity(dt)) return;

            float burnDelta = dt;
            if (!IsCooked)
            {
                float speed = Mathf.Max(0f, gm.CookingSpeedMultiplier);
                if (speed <= 0f) return;
                float need = CookDuration;
                float secondsToCook = Mathf.Max(0f, need - cookTimer) / speed;
                if (dt < secondsToCook)
                {
                    cookTimer = Mathf.Min(need, cookTimer + dt * speed);
                    burnDelta = 0f;
                }
                else
                {
                    cookTimer = need;
                    placed.SetState(IngredientState.Cooked);
                    SyncSizzle();
                    burnDelta = dt - secondsToCook;
                    Play(SfxType.Cooked);
                    Vector3 done = placed.transform.position + Vector3.up * 0.3f;
                    FeedbackFX.Burst(done, new Color(1f, 0.78f, 0.32f), 14, 2.6f, 0.08f, 5f, 0.55f);
                    FeedbackFX.Punch(placed.transform, 0.35f, 0.45f);
                    FeedbackFX.Popup(done + Vector3.up * 0.35f, "煎好了！", new Color(1f, 0.78f, 0.32f), false);
                    FeedbackFX.Shake(0.05f, 0.14f);
                }
            }

            smokeTimer -= dt;
            if (!IsCooked && smokeTimer <= 0f)
            {
                smokeTimer = 0.3f;
                FeedbackFX.Puff(placed.transform.position + Vector3.up * 0.35f, new Color(0.72f, 0.73f, 0.7f), 1, 0.17f, 0.85f, 0.85f);
            }

            if (!IsCooked) return;

            if (BurnRemaining01 < 0.35f)
            {
                if (!warned)
                {
                    warned = true;
                    warnCooldown = 0f;
                    FeedbackFX.Popup(placed.transform.position + Vector3.up * 0.6f, "快糊了！", new Color(1f, 0.5f, 0.44f), false);
                }
                warnCooldown -= dt;
                if (warnCooldown <= 0f)
                {
                    warnCooldown = 0.8f;
                    Play(SfxType.Warning);
                    FeedbackFX.Puff(placed.transform.position + Vector3.up * 0.4f, new Color(0.35f, 0.34f, 0.32f), 1, 0.14f, 1f, 0.7f);
                }
            }

            burnTimer = Mathf.Min(BurnDuration, burnTimer + burnDelta);
            if (burnTimer < BurnDuration) return;
            Vector3 charred = placed.transform.position + Vector3.up * 0.3f;
            placed.SetState(IngredientState.Burned);
            SyncSizzle();
            Play(SfxType.Burned);
            FeedbackFX.Burst(charred, new Color(0.16f, 0.14f, 0.12f), 16, 2.6f, 0.09f, 7f, 0.65f);
            FeedbackFX.Puff(charred + Vector3.up * 0.2f, new Color(0.28f, 0.27f, 0.25f), 5, 0.24f, 1.2f, 1.1f);
            FeedbackFX.Popup(charred + Vector3.up * 0.4f, "糊了！", new Color(1f, 0.45f, 0.4f), true);
            FeedbackFX.Shake(0.2f, 0.32f);
        }

        public void ResetStation() => ClearPlaced();

        private void Play(SfxType sound)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayAt(sound, transform.position + Vector3.up * 0.65f);
        }

        public string GetPrompt()
        {
            if (placed == null) return "灶台：E 放入肉饼或切好的蘑菇，自动煎制";
            if (IsBurned) return "烤糊了！空手 E 取回，再丢进垃圾桶";
            if (IsCooked) return "已煎好！E 取走或装盘，距烤糊 " + SecondsUntilBurn.ToString("0.0") + " 秒";
            return "煎制中…空手 E 可取回";
        }

        public Transform GetTransform() => transform;
    }
}
