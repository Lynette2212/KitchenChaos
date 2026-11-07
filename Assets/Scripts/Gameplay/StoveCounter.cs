using System.Collections.Generic;
using UnityEngine;
using KitchenChaos.Data;
using KitchenChaos.Core;
using KitchenChaos.Audio;
using KitchenChaos.Visual;

namespace KitchenChaos.Gameplay
{
    /// <summary>
    /// 煎台可以同时排开几份食材。煎熟后大约 5 秒就会焦糊，恒温灶台升级会把这段缓冲加长。
    /// </summary>
    public class StoveCounter : MonoBehaviour, IInteractable, IProcessingStation
    {
        public const int Capacity = 4;
        private const float BurnGrace = 5f;
        private static readonly Vector3[] SlotOffsets =
        {
            Vector3.zero,
            new Vector3(0.18f, 0f, 0.12f),
            new Vector3(-0.18f, 0f, 0.12f),
            new Vector3(0f, 0f, -0.16f)
        };

        [SerializeField] private Transform itemAnchor;
        private readonly Slot[] slots = new Slot[Capacity];
        private AudioSource sizzleSource;
        private AudioManager audioOwner;

        private sealed class Slot
        {
            public IngredientItem item;
            public float cookTimer;
            public float burnTimer;
            public float smokeTimer;
            public float warnCooldown;
            public bool warned;
        }

        public AudioSource SizzleSource => sizzleSource;
        public int OccupiedCount
        {
            get
            {
                int count = 0;
                foreach (Slot slot in slots)
                    if (slot != null && slot.item != null) count++;
                return count;
            }
        }
        public int CookedCount
        {
            get
            {
                int count = 0;
                foreach (Slot slot in slots)
                    if (slot != null && slot.item != null && slot.item.State == IngredientState.Cooked) count++;
                return count;
            }
        }

        private Slot Focus()
        {
            Slot urgentCooked = null;
            float urgentRemaining = float.PositiveInfinity;
            Slot cooking = null;
            Slot burned = null;
            Slot any = null;
            foreach (Slot slot in slots)
            {
                if (slot == null || slot.item == null) continue;
                if (any == null) any = slot;
                if (slot.item.State == IngredientState.Burned)
                {
                    if (burned == null) burned = slot;
                    continue;
                }
                if (slot.item.State == IngredientState.Cooked)
                {
                    float remaining = SecondsUntilBurnOf(slot);
                    if (remaining < urgentRemaining)
                    {
                        urgentRemaining = remaining;
                        urgentCooked = slot;
                    }
                    continue;
                }
                if (cooking == null) cooking = slot;
            }
            if (urgentCooked != null) return urgentCooked;
            if (cooking != null) return cooking;
            return burned != null ? burned : any;
        }

        public Ingredient PlacedIngredient => Focus()?.item != null ? Focus().item.Ingredient : null;
        public bool IsProcessing => GameManager.Instance != null && GameManager.Instance.IsPlaying
            && Focus() != null && !IsCooked && !IsBurned;
        public bool IsCooked => Focus() != null && Focus().item.State == IngredientState.Cooked;
        public bool IsBurned => Focus() != null && Focus().item.State == IngredientState.Burned;
        private float BurnDuration => Mathf.Max(0.01f, BurnGrace
            + (GameManager.Instance != null ? GameManager.Instance.BurnGraceBonus : 0f));
        public float Progress01
        {
            get
            {
                Slot slot = Focus();
                if (slot == null || slot.item == null) return 0f;
                if (slot.item.State == IngredientState.Cooked || slot.item.State == IngredientState.Burned) return 1f;
                return Mathf.Clamp01(slot.cookTimer / CookDuration(slot));
            }
        }
        public float SecondsUntilBurn => IsCooked ? SecondsUntilBurnOf(Focus()) : 0f;
        public float BurnRemaining01 => IsCooked ? Mathf.Clamp01(SecondsUntilBurn / BurnDuration) : 0f;

        private float CookDuration(Slot slot) => Mathf.Max(0.01f, IngredientCatalog.Get(slot.item.Type)?.cookTime ?? 1f);
        private float SecondsUntilBurnOf(Slot slot)
        {
            if (slot == null || slot.item == null || slot.item.State != IngredientState.Cooked) return 0f;
            return Mathf.Max(0f, BurnDuration - slot.burnTimer);
        }

        private void Awake()
        {
            for (int i = 0; i < slots.Length; i++)
                if (slots[i] == null) slots[i] = new Slot();
        }

        public bool Interact(PlayerController player)
        {
            if (player == null || !player.CanInteract) return false;
            if (player.HasItem)
            {
                Ingredient data = player.CarriedItem.Ingredient;
                string reason = PlaceFailure(data);
                if (reason != null) return player.RejectInteraction(reason);
                Place(player.TakeItem());
                Play(SfxType.Drop);
                return true;
            }
            if (player.HasPlate) return PlateOneCooked(player);
            return TakeOne(player);
        }

        private string PlaceFailure(Ingredient data)
        {
            if (FreeSlot() < 0) return "煎台已经排满，先取走煎好的再放";
            IngredientInfo info = data != null ? IngredientCatalog.Get(data.type) : null;
            if (info == null || !info.needsCook) return "这种食材无需煎制，请使用切菜台或暂存台";
            if (data.state == IngredientState.Cooked) return "食材已经熟了，请装盘或放到暂存台，不能重复煎制";
            if (data.state == IngredientState.Burned) return "烤糊的食材无法重新煎制，请丢进垃圾桶";
            if (info.needsCut && data.state != IngredientState.Cut) return "蘑菇需要先在切菜台切好，再放入灶台";
            if (data.state != IngredientState.Raw && data.state != IngredientState.Cut) return "这份食材无法煎制";
            return null;
        }

        private bool PlateOneCooked(PlayerController player)
        {
            int index = FirstCooked();
            if (index < 0)
            {
                if (FirstBurned() >= 0) return player.RejectInteraction("烤糊了！空手 E 取回，再丢进垃圾桶");
                return player.RejectInteraction(OccupiedCount > 0 ? "煎台上的食材还没好" : "煎台是空的");
            }
            string reason = player.Plate.GetAddFailureReason(slots[index].item.Ingredient);
            if (reason != null) return player.RejectInteraction(reason);
            if (!player.Plate.Add(slots[index].item.Ingredient)) return false;
            ClearSlot(index);
            Play(SfxType.Drop);
            return true;
        }

        private bool TakeOne(PlayerController player)
        {
            int index = FirstCooked();
            if (index < 0) index = FirstOccupied();
            if (index < 0) return player.RejectInteraction("先拿取肉饼或切好的蘑菇，再放入灶台");
            if (!player.CanTake()) return player.RejectInteraction("灶台已有食材，请先腾出双手");
            if (!player.GiveItem(slots[index].item.Ingredient)) return false;
            ClearSlot(index);
            Play(SfxType.Pickup);
            return true;
        }

        public bool TryTakeCooked(int index, out Ingredient data)
        {
            data = null;
            EnsureSlots();
            if (index < 0 || index >= slots.Length || slots[index].item == null) return false;
            if (slots[index].item.State != IngredientState.Cooked) return false;
            data = slots[index].item.Ingredient.Clone();
            ClearSlot(index);
            return true;
        }

        public Ingredient CookedAt(int index)
        {
            EnsureSlots();
            if (index < 0 || index >= slots.Length || slots[index].item == null) return null;
            if (slots[index].item.State != IngredientState.Cooked) return null;
            return slots[index].item.Ingredient;
        }

        public void CollectCooked(List<int> indices)
        {
            indices.Clear();
            EnsureSlots();
            for (int i = 0; i < slots.Length; i++)
                if (CookedAt(i) != null) indices.Add(i);
        }

        private void EnsureSlots()
        {
            for (int i = 0; i < slots.Length; i++)
                if (slots[i] == null) slots[i] = new Slot();
        }

        private int FreeSlot()
        {
            EnsureSlots();
            for (int i = 0; i < slots.Length; i++)
                if (slots[i].item == null) return i;
            return -1;
        }

        private int FirstCooked()
        {
            for (int i = 0; i < slots.Length; i++)
                if (slots[i] != null && slots[i].item != null && slots[i].item.State == IngredientState.Cooked) return i;
            return -1;
        }

        private int FirstBurned()
        {
            for (int i = 0; i < slots.Length; i++)
                if (slots[i] != null && slots[i].item != null && slots[i].item.State == IngredientState.Burned) return i;
            return -1;
        }

        private int FirstOccupied()
        {
            for (int i = 0; i < slots.Length; i++)
                if (slots[i] != null && slots[i].item != null) return i;
            return -1;
        }

        private void Place(Ingredient data)
        {
            EnsureSlots();
            if (itemAnchor == null) itemAnchor = transform.Find("ItemAnchor");
            int index = FreeSlot();
            Transform parent = itemAnchor != null ? itemAnchor : transform;
            GameObject go = new GameObject("StoveIngredient");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = itemAnchor != null ? SlotOffsets[index] : new Vector3(0f, 0.61f, 0f);
            IngredientItem item = go.AddComponent<IngredientItem>();
            item.Setup(data);
            slots[index].item = item;
            slots[index].cookTimer = 0f;
            slots[index].burnTimer = 0f;
            slots[index].smokeTimer = 0f;
            slots[index].warnCooldown = 0f;
            slots[index].warned = false;
            SyncSizzle();
        }

        private void ClearSlot(int index)
        {
            Slot slot = slots[index];
            if (slot.item != null)
            {
                slot.item.gameObject.SetActive(false);
                Destroy(slot.item.gameObject);
            }
            slot.item = null;
            slot.cookTimer = 0f;
            slot.burnTimer = 0f;
            slot.smokeTimer = 0f;
            slot.warnCooldown = 0f;
            slot.warned = false;
            SyncSizzle();
        }

        private void Update()
        {
            AdvanceProcessing(Time.deltaTime);
            SyncSizzle();
        }

        private void SyncSizzle()
        {
            bool cooking = false;
            bool stocked = false;
            foreach (Slot slot in slots)
            {
                if (slot == null || slot.item == null || slot.item.State == IngredientState.Burned) continue;
                stocked = true;
                if (slot.item.State != IngredientState.Cooked) cooking = true;
            }
            bool hasFood = isActiveAndEnabled && (cooking || stocked);
            if (hasFood && sizzleSource == null && AudioManager.Instance != null)
            {
                audioOwner = AudioManager.Instance;
                sizzleSource = audioOwner.CreateLoopSource(transform, SfxType.Sizzle);
            }
            if (audioOwner != null && sizzleSource != null)
                audioOwner.SetLoopLevel(sizzleSource, hasFood ? (cooking ? 1f : 0.62f) : 0f);
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
            if (gm == null || !gm.IsPlaying || dt <= 0f || float.IsNaN(dt) || float.IsInfinity(dt)) return;
            float speed = Mathf.Max(0f, gm.CookingSpeedMultiplier);
            EnsureSlots();
            foreach (Slot slot in slots)
            {
                if (slot.item == null || slot.item.State == IngredientState.Burned) continue;
                float burnDelta = 0f;
                if (slot.item.State != IngredientState.Cooked)
                {
                    if (speed <= 0f) continue;
                    float need = CookDuration(slot);
                    float secondsToCook = Mathf.Max(0f, need - slot.cookTimer) / speed;
                    if (dt < secondsToCook)
                    {
                        slot.cookTimer = Mathf.Min(need, slot.cookTimer + dt * speed);
                        slot.smokeTimer -= dt;
                        if (slot.smokeTimer <= 0f)
                        {
                            slot.smokeTimer = 0.3f;
                            FeedbackFX.Puff(slot.item.transform.position + Vector3.up * 0.35f, new Color(0.72f, 0.73f, 0.7f), 1, 0.17f, 0.85f, 0.85f);
                        }
                        continue;
                    }
                    slot.cookTimer = need;
                    slot.item.SetState(IngredientState.Cooked);
                    burnDelta = dt - secondsToCook;
                    Play(SfxType.Cooked);
                    Vector3 done = slot.item.transform.position + Vector3.up * 0.3f;
                    FeedbackFX.Burst(done, new Color(1f, 0.78f, 0.32f), 14, 2.6f, 0.08f, 5f, 0.55f);
                    FeedbackFX.Punch(slot.item.transform, 0.35f, 0.45f);
                    FeedbackFX.Popup(done + Vector3.up * 0.35f, "煎好了！", new Color(1f, 0.78f, 0.32f), false);
                    FeedbackFX.Shake(0.05f, 0.14f);
                }
                else
                {
                    burnDelta = dt;
                }

                if (slot.item.State != IngredientState.Cooked) continue;
                float remaining01 = Mathf.Clamp01(SecondsUntilBurnOf(slot) / BurnDuration);
                if (remaining01 < 0.35f)
                {
                    if (!slot.warned)
                    {
                        slot.warned = true;
                        slot.warnCooldown = 0f;
                        FeedbackFX.Popup(slot.item.transform.position + Vector3.up * 0.6f, "快糊了！", new Color(1f, 0.5f, 0.44f), false);
                    }
                    slot.warnCooldown -= dt;
                    if (slot.warnCooldown <= 0f)
                    {
                        slot.warnCooldown = 0.8f;
                        Play(SfxType.Warning);
                        FeedbackFX.Puff(slot.item.transform.position + Vector3.up * 0.4f, new Color(0.35f, 0.34f, 0.32f), 1, 0.14f, 1f, 0.7f);
                    }
                }

                slot.burnTimer = Mathf.Min(BurnDuration, slot.burnTimer + burnDelta);
                if (slot.burnTimer < BurnDuration) continue;
                Vector3 charred = slot.item.transform.position + Vector3.up * 0.3f;
                slot.item.SetState(IngredientState.Burned);
                Play(SfxType.Burned);
                FeedbackFX.Burst(charred, new Color(0.16f, 0.14f, 0.12f), 16, 2.6f, 0.09f, 7f, 0.65f);
                FeedbackFX.Puff(charred + Vector3.up * 0.2f, new Color(0.28f, 0.27f, 0.25f), 5, 0.24f, 1.2f, 1.1f);
                FeedbackFX.Popup(charred + Vector3.up * 0.4f, "糊了！", new Color(1f, 0.45f, 0.4f), true);
                FeedbackFX.Shake(0.2f, 0.32f);
            }
            SyncSizzle();
        }

        public void ResetStation()
        {
            EnsureSlots();
            for (int i = 0; i < slots.Length; i++) ClearSlot(i);
        }

        private void Play(SfxType sound)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayAt(sound, transform.position + Vector3.up * 0.65f);
        }

        public string GetPrompt()
        {
            Slot slot = Focus();
            if (slot == null || slot.item == null) return "灶台：E 放入肉饼或切好的蘑菇，煎熟后约 " + BurnGrace.ToString("0") + " 秒会焦糊";
            if (IsBurned) return "烤糊了！空手 E 取回，再丢进垃圾桶";
            if (IsCooked) return "已煎好 " + CookedCount + " 份！E 取走或装盘，距烤糊 " + SecondsUntilBurn.ToString("0.0") + " 秒";
            return "煎制中…空手 E 可取回（" + OccupiedCount + " 份在台上）";
        }

        public Transform GetTransform() => transform;
    }
}
