using System.Collections.Generic;
using UnityEngine;
using KitchenChaos.Data;
using KitchenChaos.Core;
using KitchenChaos.Audio;
using KitchenChaos.Visual;

namespace KitchenChaos.Gameplay
{
    /// <summary>
    /// 平板煎台。一次能排开 4 份肉饼或蘑菇。煎熟后大约 5 秒会焦糊，恒温灶台会把这段时间加长。
    /// </summary>
    public class StoveCounter : MonoBehaviour, IInteractable, IProcessingStation
    {
        public const int Capacity = 4;
        private const float BurnGrace = 5f;
        private static readonly Vector3[] SlotOffsets =
        {
            new Vector3(-0.22f, 0.02f, -0.16f),
            new Vector3(0.22f, 0.02f, -0.16f),
            new Vector3(-0.22f, 0.02f, 0.16f),
            new Vector3(0.22f, 0.02f, 0.16f)
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

        public struct SlotStatus
        {
            public Vector3 position;
            public IngredientState state;
            public float progress01;
            public float burnRemaining01;
            public float secondsUntilBurn;
        }

        public AudioSource SizzleSource => sizzleSource;
        public int OccupiedCount
        {
            get
            {
                int count = 0;
                foreach (Slot slot in slots)
                    if (slot.item != null) count++;
                return count;
            }
        }
        public int CookedCount
        {
            get
            {
                int count = 0;
                foreach (Slot slot in slots)
                    if (slot.item != null && slot.item.State == IngredientState.Cooked) count++;
                return count;
            }
        }

        private Slot Focus()
        {
            Slot cooking = null;
            Slot cooked = null;
            float soonest = float.PositiveInfinity;
            Slot any = null;
            foreach (Slot slot in slots)
            {
                if (slot.item == null) continue;
                if (any == null) any = slot;
                if (slot.item.State == IngredientState.Burned) continue;
                if (slot.item.State == IngredientState.Cooked)
                {
                    float remain = SecondsUntilBurnOf(slot);
                    if (cooked == null || remain < soonest)
                    {
                        soonest = remain;
                        cooked = slot;
                    }
                    continue;
                }
                cooking = slot;
            }
            if (cooking != null) return cooking;
            if (cooked != null) return cooked;
            return any;
        }

        public Ingredient PlacedIngredient => Focus()?.item != null ? Focus().item.Ingredient : null;
        public bool IsProcessing => GameManager.Instance != null && GameManager.Instance.IsPlaying && Focus() != null
            && Focus().item.State != IngredientState.Cooked && Focus().item.State != IngredientState.Burned;
        public bool IsCooked => Focus() != null && Focus().item.State == IngredientState.Cooked;
        public bool IsBurned => Focus() != null && Focus().item.State == IngredientState.Burned;
        public float Progress01
        {
            get
            {
                Slot slot = Focus();
                if (slot == null || slot.item == null) return 0f;
                if (slot.item.State == IngredientState.Cooked || slot.item.State == IngredientState.Burned) return 1f;
                float need = CookDuration(slot);
                return Mathf.Clamp01(slot.cookTimer / need);
            }
        }
        public float SecondsUntilBurn => SoonestBurn();
        public float BurnRemaining01 => IsCooked ? Mathf.Clamp01(SecondsUntilBurn / BurnDuration) : 0f;

        private float CookDuration(Slot slot) => Mathf.Max(0.01f, IngredientCatalog.Get(slot.item.Type)?.cookTime ?? 1f);
        private float BurnDuration => Mathf.Max(0.01f, BurnGrace
            + (GameManager.Instance != null ? GameManager.Instance.BurnGraceBonus : 0f));
        private float SecondsUntilBurnOf(Slot slot)
        {
            if (slot == null || slot.item == null || slot.item.State != IngredientState.Cooked) return 0f;
            return Mathf.Max(0f, BurnDuration - slot.burnTimer);
        }
        private float SoonestBurn()
        {
            float soonest = float.PositiveInfinity;
            bool found = false;
            foreach (Slot slot in slots)
            {
                if (slot.item == null || slot.item.State != IngredientState.Cooked) continue;
                found = true;
                soonest = Mathf.Min(soonest, SecondsUntilBurnOf(slot));
            }
            return found ? soonest : 0f;
        }

        private void Awake()
        {
            for (int i = 0; i < slots.Length; i++) slots[i] = new Slot();
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
            if (FreeSlot() < 0) return "煎台已经排满 4 份，先取走煎好的再放";
            IngredientInfo info = data != null ? IngredientCatalog.Get(data.type) : null;
            if (info == null || !info.needsCook)
                return info != null && info.needsCut
                    ? "这种食材不用煎，先去切菜台切好再装盘"
                    : "这种食材不用上煎台";
            if (data.state == IngredientState.Cooked) return "已经煎好了，留着装汉堡，不用再煎";
            if (data.state == IngredientState.Burned) return "烤糊的食材不能再煎";
            if (info.needsCut && data.state != IngredientState.Cut) return "蘑菇要先切好再上煎台";
            if (data.state != IngredientState.Raw && data.state != IngredientState.Cut) return "这份食材不能上煎台";
            return null;
        }

        private bool PlateOneCooked(PlayerController player)
        {
            int index = FirstCooked();
            if (index < 0)
            {
                if (FirstBurned() >= 0) return player.RejectInteraction("烤糊了，先空手取回再丢进垃圾桶");
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
            if (index < 0) return player.RejectInteraction("煎台是空的。可以一次放上多份肉饼，提前煎好备用");
            if (!player.CanTake()) return player.RejectInteraction("先腾出双手，再从煎台取菜");
            if (!player.GiveItem(slots[index].item.Ingredient)) return false;
            ClearSlot(index);
            Play(SfxType.Pickup);
            return true;
        }

        public bool TryTakeCooked(int index, out Ingredient data)
        {
            data = null;
            if (index < 0 || index >= slots.Length || slots[index].item == null) return false;
            if (slots[index].item.State != IngredientState.Cooked) return false;
            data = slots[index].item.Ingredient.Clone();
            ClearSlot(index);
            return true;
        }

        public Ingredient CookedAt(int index)
        {
            if (index < 0 || index >= slots.Length || slots[index].item == null) return null;
            if (slots[index].item.State != IngredientState.Cooked) return null;
            return slots[index].item.Ingredient;
        }

        public void CollectCooked(List<int> indices)
        {
            indices.Clear();
            for (int i = 0; i < slots.Length; i++)
                if (CookedAt(i) != null) indices.Add(i);
        }

        /// <summary>每个煎位的实时状态，供 HUD 在食材上方画煎制/焦糊状态条。</summary>
        public void CollectSlotStatus(List<SlotStatus> results)
        {
            if (results == null) return;
            results.Clear();
            for (int i = 0; i < slots.Length; i++)
            {
                Slot slot = slots[i];
                if (slot.item == null) continue;
                IngredientState state = slot.item.State;
                results.Add(new SlotStatus
                {
                    position = slot.item.transform.position,
                    state = state,
                    progress01 = state == IngredientState.Cooked || state == IngredientState.Burned
                        ? 1f : Mathf.Clamp01(slot.cookTimer / CookDuration(slot)),
                    burnRemaining01 = state == IngredientState.Cooked ? Mathf.Clamp01(SecondsUntilBurnOf(slot) / BurnDuration) : 0f,
                    secondsUntilBurn = state == IngredientState.Cooked ? SecondsUntilBurnOf(slot) : 0f
                });
            }
        }

        private int FreeSlot()
        {
            for (int i = 0; i < slots.Length; i++)
                if (slots[i].item == null) return i;
            return -1;
        }

        private int FirstCooked()
        {
            for (int i = 0; i < slots.Length; i++)
                if (slots[i].item != null && slots[i].item.State == IngredientState.Cooked) return i;
            return -1;
        }

        private int FirstBurned()
        {
            for (int i = 0; i < slots.Length; i++)
                if (slots[i].item != null && slots[i].item.State == IngredientState.Burned) return i;
            return -1;
        }

        private int FirstOccupied()
        {
            for (int i = 0; i < slots.Length; i++)
                if (slots[i].item != null) return i;
            return -1;
        }

        private void Place(Ingredient data)
        {
            if (itemAnchor == null) itemAnchor = transform.Find("ItemAnchor");
            int index = FreeSlot();
            Transform parent = itemAnchor != null ? itemAnchor : transform;
            GameObject go = new GameObject("GrillSlot" + index);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = SlotOffsets[index];
            go.transform.localScale = Vector3.one * 0.42f;
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
                if (slot.item == null || slot.item.State == IngredientState.Burned) continue;
                stocked = true;
                if (slot.item.State != IngredientState.Cooked) cooking = true;
            }
            if ((cooking || stocked) && sizzleSource == null && AudioManager.Instance != null)
            {
                audioOwner = AudioManager.Instance;
                sizzleSource = audioOwner.CreateLoopSource(transform, SfxType.Sizzle);
            }
            if (audioOwner != null && sizzleSource != null)
                audioOwner.SetLoopLevel(sizzleSource, cooking ? 1f : stocked ? 0.62f : 0f);
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
                            slot.smokeTimer = 0.35f;
                            FeedbackFX.Puff(slot.item.transform.position + Vector3.up * 0.2f, new Color(0.72f, 0.73f, 0.7f), 1, 0.12f, 0.7f, 0.7f);
                        }
                        continue;
                    }
                    slot.cookTimer = need;
                    slot.item.SetState(IngredientState.Cooked);
                    burnDelta = dt - secondsToCook;
                    Play(SfxType.Cooked);
                    Vector3 done = slot.item.transform.position + Vector3.up * 0.2f;
                    FeedbackFX.Burst(done, new Color(1f, 0.78f, 0.32f), 10, 2.2f, 0.06f, 4f, 0.45f);
                    FeedbackFX.Punch(slot.item.transform, 0.3f, 0.35f);
                    FeedbackFX.Popup(done + Vector3.up * 0.2f, "煎好了，先取走", new Color(1f, 0.78f, 0.32f), false);
                }
                else
                {
                    burnDelta = dt;
                }

                if (slot.item.State != IngredientState.Cooked) continue;
                if (Mathf.Clamp01(SecondsUntilBurnOf(slot) / BurnDuration) < 0.35f)
                {
                    if (!slot.warned)
                    {
                        slot.warned = true;
                        slot.warnCooldown = 0f;
                        FeedbackFX.Popup(slot.item.transform.position + Vector3.up * 0.45f, "快糊了！", new Color(1f, 0.5f, 0.44f), false);
                    }
                    slot.warnCooldown -= dt;
                    if (slot.warnCooldown <= 0f)
                    {
                        slot.warnCooldown = 0.8f;
                        Play(SfxType.Warning);
                        FeedbackFX.Puff(slot.item.transform.position + Vector3.up * 0.3f, new Color(0.35f, 0.34f, 0.32f), 1, 0.14f, 1f, 0.7f);
                    }
                }
                slot.burnTimer = Mathf.Min(BurnDuration, slot.burnTimer + burnDelta);
                if (slot.burnTimer < BurnDuration) continue;
                Vector3 charred = slot.item.transform.position + Vector3.up * 0.2f;
                slot.item.SetState(IngredientState.Burned);
                if (gm != null) gm.RegisterBurntIngredient(slot.item.Type);
                Play(SfxType.Burned);
                FeedbackFX.Burst(charred, new Color(0.16f, 0.14f, 0.12f), 12, 2.2f, 0.08f, 6f, 0.55f);
                FeedbackFX.Popup(charred + Vector3.up * 0.3f, "糊了！", new Color(1f, 0.45f, 0.4f), true);
                FeedbackFX.Shake(0.16f, 0.24f);
            }
            SyncSizzle();
        }

        public void ResetStation()
        {
            for (int i = 0; i < slots.Length; i++) ClearSlot(i);
        }

        private void Play(SfxType sound)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayAt(sound, transform.position + Vector3.up * 0.65f);
        }

        public string GetPrompt()
        {
            if (OccupiedCount == 0) return "煎台：可一次放下 4 份肉饼或蘑菇，煎熟后大约 " + BurnGrace.ToString("0") + " 秒会焦糊";
            if (IsBurned) return "烤糊了！空手取回，再丢进垃圾桶";
            if (CookedCount > 0) return "煎台 " + CookedCount + " 份已好 / " + OccupiedCount + " 份在台上，最近一份 " + SecondsUntilBurn.ToString("0.0") + " 秒后焦糊";
            return "煎台 " + OccupiedCount + " 份在煎。空手取一份，端盘直接装";
        }

        public Transform GetTransform() => transform;
    }
}
