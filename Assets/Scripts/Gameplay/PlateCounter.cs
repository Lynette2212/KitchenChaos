using System.Collections.Generic;
using UnityEngine;
using KitchenChaos.Audio;
using KitchenChaos.Data;
using KitchenChaos.Visual;

namespace KitchenChaos.Gameplay
{
    public class PlateCounter : MonoBehaviour, IInteractable
    {
        [SerializeField] private int plateCount = 4;
        [SerializeField] private Transform stackAnchor;
        [SerializeField] private List<GameObject> plateVisuals = new List<GameObject>();
        private int initialPlateCount;
        private bool initialized;

        public int PlateCount => plateCount;

        private void Awake() => InitializeStock();
        private void Start() => BuildVisualStack();

        private void InitializeStock()
        {
            if (initialized) return;
            initialPlateCount = Mathf.Max(0, plateCount);
            plateCount = initialPlateCount;
            initialized = true;
        }

        public void BuildVisualStack()
        {
            if (stackAnchor == null) stackAnchor = transform.Find("ItemAnchor");
            for (int i = 0; i < plateVisuals.Count; i++)
                if (plateVisuals[i] != null) plateVisuals[i].SetActive(i < plateCount);
            for (int i = plateVisuals.Count; i < plateCount; i++)
            {
                GameObject disk = VisualFactory.CreateProp("plate-dinner");
                disk.name = "Plate";
                disk.transform.SetParent(stackAnchor != null ? stackAnchor : transform, false);
                disk.transform.localScale = new Vector3(1f, 0.8f, 1f);
                disk.transform.localPosition = new Vector3(0f, (stackAnchor != null ? 0.025f : 0.575f) + i * 0.065f, 0f);
                plateVisuals.Add(disk);
            }
        }

        public bool Interact(PlayerController player)
        {
            if (player == null || !player.CanInteract) return false;
            InitializeStock();
            if (player.HasPlate)
            {
                if (player.Plate.Count > 0)
                    return player.RejectInteraction("盘里还有食材，请先放到暂存台；餐盘台只能归还空盘");
                PlateHandle handle = player.DetachPlate();
                handle.ReturnToCounter();
                plateCount++;
                BuildVisualStack();
                FeedbackFX.Puff(StackTop() + Vector3.up * 0.1f, new Color(0.9f, 0.92f, 0.95f), 2, 0.1f, 0.4f, 0.35f);
                Play(SfxType.PlatePlace);
                return true;
            }
            if (plateCount <= 0) return player.RejectInteraction("没有干净盘子了，请归还空盘或取回暂存的盘子");

            GameObject go = new GameObject("HeldPlate");
            PlateHandle plate = go.AddComponent<PlateHandle>();
            Ingredient held = player.HasItem ? player.CarriedItem.Ingredient : null;
            if (held != null)
            {
                string reason = plate.GetAddFailureReason(held);
                if (reason != null)
                {
                    Destroy(go);
                    return player.RejectInteraction(reason);
                }
            }

            plate.Init(Vector3.zero);
            plateCount--;
            BuildVisualStack();
            player.AttachPlate(plate);
            if (held != null)
            {
                plate.Add(held);
                player.ClearCarried();
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlayAt(SfxType.Drop, player.transform.position + Vector3.up * 1.2f);
            }
            FeedbackFX.Punch(plate.transform.Find("PlateBody"), 0.3f, 0.35f);
            FeedbackFX.Puff(StackTop() + Vector3.up * 0.1f, new Color(0.9f, 0.92f, 0.95f), 2, 0.1f, 0.4f, 0.35f);
            Play(SfxType.PlatePickup);
            return true;
        }

        private Vector3 StackTop()
        {
            int top = Mathf.Min(plateCount, plateVisuals.Count) - 1;
            if (top >= 0 && plateVisuals[top] != null)
                return plateVisuals[top].transform.position;
            return transform.position + Vector3.up * 0.6f;
        }

        public void ResetStation()
        {
            InitializeStock();
            plateCount = initialPlateCount;
            BuildVisualStack();
        }

        private void Play(SfxType sound)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayAt(sound, StackTop());
        }

        public string GetPrompt() => "餐盘台：E 端盘，手里的食材直接装盘；E 归还空盘（剩 " + plateCount + " 个）";
        public Transform GetTransform() => transform;
    }
}
